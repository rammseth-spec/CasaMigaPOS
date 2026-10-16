using System.Globalization;
using System.IO;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualBasic.FileIO;

/// <summary>
/// Prepara una copia de revisión NUEVA con el catálogo autorizado v2 versionado en docs/catalog.
/// Opcional: fotos provisionales por SKU (--photos) y ventas de prueba de los últimos días (--demo-days) hechas con los
/// servicios reales (caja, cobro, devoluciones, cierre), de modo que respetan todas las reglas de la base.
/// Exige carpeta nueva y vacía: nunca toca una instalación existente. Herramienta de desarrollo; no se instala en las cajas.
/// </summary>
internal static class SeedReview
{
    private sealed record Row(int Id, string Name, int PriceMxn, string Category, string Note);
    private sealed class DemoClock : IClock { public DateTimeOffset UtcNow { get; set; } }

    public static int Run(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Uso: seed-review <catálogo csv> <carpeta nueva> [--photos <carpeta>] [--demo-days N]");
        var csvPath = args[0]; var target = Path.GetFullPath(args[1]);
        string? photos = null; var demoDays = 0;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--photos" && i + 1 < args.Length) photos = Path.GetFullPath(args[++i]);
            else if (args[i] == "--demo-days" && i + 1 < args.Length && int.TryParse(args[++i], out var days) && days is >= 1 and <= 60) demoDays = days;
            else throw new ArgumentException($"Opción no reconocida: {args[i]}");
        }
        if (!Path.IsPathFullyQualified(target) || target.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("Se requiere una ruta local absoluta.");
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()) throw new InvalidOperationException("La copia de revisión requiere una carpeta nueva y vacía.");
        var rows = Read(csvPath);
        Directory.CreateDirectory(target);
        using var lease = new DirectoryLease(target);
        var clock = new SystemClock();
        new FoundationRuntime(new FoundationOptions(target, true)).Start();
        var database = new Database(Path.Combine(target, "casamiga.db"));
        var unit = new SqliteLocalUnitOfWork(database);
        var terminal = TerminalConfiguration.LoadOrCreate(target);
        var journal = new CheckoutJournal(target);
        var session = new UserSession(journal);
        var identity = new IdentityService(unit, new LocalPasswords(), clock, session);

        var adminPassword = PasswordPolicy.GenerateTemporary();
        identity.Bootstrap("admin", "Administrador", adminPassword);
        if (!identity.Login("admin", adminPassword)) throw new InvalidOperationException("No se pudo iniciar sesión con el administrador recién creado.");
        var cashierTemporary = identity.CreateUser("cajero1", "Cajero de prueba", "Cashier");

        var catalog = new CatalogService(unit, session, clock, terminal.Id, true);
        var categories = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var name in rows.Select(r => r.Category).Distinct(StringComparer.Ordinal))
            categories.Add(name, catalog.CreateCategory(name, "Catálogo autorizado v2 · docs/catalog", categories.Count).Id);
        var products = new List<(Row Row, Product Product)>();
        foreach (var row in rows)
            products.Add((row, catalog.CreateProduct(categories[row.Category], $"CM-{row.Id:000}", row.Name, row.Note, new Money(checked(row.PriceMxn * 100L)), false)));

        var imported = 0;
        if (photos is not null)
        {
            var photoService = new ProductPhotoService(new SqliteProductPhotoStore(database), new ProductPhotoFileStore(target), new WpfProductPhotoProcessor(), session, clock, terminal.Id);
            foreach (var (row, product) in products)
            {
                var file = Path.Combine(photos, $"CM-{row.Id:000}.jpg");
                if (!File.Exists(file)) continue;
                using var input = File.OpenRead(file);
                var edge = ImageEdge(file);
                photoService.Import(new(product.Id, 0, Guid.NewGuid(), Path.GetFileName(file), "PROVISIONAL", new PhotoCrop(0, 0, edge)), input);
                imported++;
            }
        }

        var demo = demoDays > 0 ? SeedSales(unit, journal, session, identity, adminPassword, terminal, products, demoDays) : null;
        var trays = demoDays > 0 ? SeedConsignment(unit, session, identity, adminPassword, terminal, products, demoDays) : null;
        if (session.Current is not null) session.SignOut();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Target = target, Terminal = terminal.Name, Categories = categories.Count, Products = rows.Count, Photos = imported,
            Admin = "admin", AdminPassword = adminPassword, Cashier = "cajero1", CashierTemporaryPassword = cashierTemporary,
            DemoSales = demo?.Sales ?? 0, DemoRefunds = demo?.Refunds ?? 0, DemoShifts = demo?.Shifts ?? 0, DemoCashiers = demo?.Cashiers,
            DemoTrayStores = trays?.Stores ?? 0, DemoTrays = trays?.Deliveries ?? 0, DemoTraysCollected = trays?.Collected ?? 0, DemoTrayPayments = trays?.Payments ?? 0
        }));
        return 0;
    }

    private sealed record TrayDemoResult(int Stores, int Deliveries, int Collected, int Payments);

    /// <summary>
    /// Charolas deterministas con el servicio real: tres tiendas, entrega diaria y recolección al día siguiente.
    /// Una tienda paga completo, otra a veces deja saldo y otra paga cada semana; la charola de ayer sigue en la tienda.
    /// </summary>
    private static TrayDemoResult SeedConsignment(SqliteLocalUnitOfWork unit, UserSession session, IdentityService identity, string adminPassword,
        TerminalIdentity terminal, List<(Row Row, Product Product)> products, int days)
    {
        var random = new Random(4012);
        var clock = new DemoClock { UtcNow = DateTimeOffset.UtcNow };
        if (session.Current is not null) session.SignOut();
        if (!identity.Login("admin", adminPassword)) throw new InvalidOperationException("No se pudo iniciar sesión como admin.");
        var service = new ConsignmentService(unit, session, clock, terminal.Id, TimeZoneInfo.Local);

        // Precio de charola: 80 % del de mostrador, redondeado a 50 centavos. Las bebidas no van en charola.
        var bread = products.Where(p => !p.Row.Category.Equals("Bebida", StringComparison.OrdinalIgnoreCase)).Select(p => p.Product).ToList();
        foreach (var product in bread)
            service.SetPrice(product.Id, new Money((long)(Math.Round(product.SalePrice.Cents * 0.8m / 50m, MidpointRounding.AwayFromZero) * 50)));

        var plans = new[]
        {
            (Name: "Abarrotes Lupita", Contact: "Lupita Hernández", Phone: "55 1234 5678", Address: "Av. Juárez 120", Kinds: 3, Each: 4, Pays: 0),
            (Name: "Tienda Don Pepe", Contact: "José Ramírez", Phone: "55 8765 4321", Address: "Calle Hidalgo 45", Kinds: 5, Each: 3, Pays: 1),
            (Name: "Miscelánea La Esquina", Contact: "Rosa Martínez", Phone: "55 2468 1357", Address: "Esquina Morelos y 5 de Mayo", Kinds: 4, Each: 3, Pays: 2),
        };
        var stores = new List<(ConsignmentStore Store, int Pays)>();
        foreach (var plan in plans)
        {
            var store = service.SaveStore(null, plan.Name, plan.Contact, plan.Phone, plan.Address);
            var picks = bread.OrderBy(_ => random.Next()).Take(plan.Kinds).Select(p => new ConsignmentLine(p.Id, plan.Each)).ToList();
            service.SaveTemplate(store.Id, picks);
            stores.Add((store, plan.Pays));
        }

        DateTimeOffset At(DateOnly day, int hour, int minute) =>
            new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified), TimeZoneInfo.Local), TimeSpan.Zero);
        int deliveries = 0, collected = 0, payments = 0;
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (var offset = days; offset >= 1; offset--)
        {
            var day = today.AddDays(-offset);
            for (int s = 0; s < stores.Count; s++)
            {
                var (store, pays) = stores[s];
                // Primero se recoge la charola de ayer y después se deja la nueva.
                clock.UtcNow = At(day, 7, 0).AddMinutes(10 + s * 25);
                if (service.Route().Single(r => r.Store.Id == store.Id).AtStore.FirstOrDefault() is { } tray)
                {
                    var returned = tray.Items.ToDictionary(i => i.Position, i => Math.Min(i.Quantity, random.Next(100) < 70 ? 0 : random.Next(1, 3)));
                    var sold = new Money(tray.Items.Sum(i => i.UnitPrice.Cents * (i.Quantity - returned[i.Position])));
                    var owed = service.Route().Single(r => r.Store.Id == store.Id).Balance + sold;
                    var paid = pays switch
                    {
                        0 => owed,                                                        // paga todo
                        1 => offset % 3 == 0 ? new Money(sold.Cents / 2 / 100 * 100) : sold, // a veces deja la mitad
                        _ => offset % 7 == 0 ? owed : new Money(0),                       // paga cada semana
                    };
                    var method = random.Next(100) < 80 ? PaymentMethod.Cash : PaymentMethod.Transfer;
                    var result = service.Collect(tray.Id, returned, paid, method, method == PaymentMethod.Transfer ? $"SPEI {random.Next(10000, 99999)}" : null, "");
                    collected++;
                    if (result.Payment is not null) payments++;
                }
                clock.UtcNow = At(day, 7, 0).AddMinutes(20 + s * 25);
                service.Deliver(store.Id, service.Template(store.Id).Select(l => new ConsignmentLine(l.ProductId, l.Quantity)).ToList(), "");
                deliveries++;
            }
        }
        session.SignOut();
        return new(stores.Count, deliveries, collected, payments);
    }

    private static int ImageEdge(string file)
    {
        using var stream = File.OpenRead(file);
        var frame = System.Windows.Media.Imaging.BitmapFrame.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation, System.Windows.Media.Imaging.BitmapCacheOption.None);
        return Math.Min(frame.PixelWidth, frame.PixelHeight);
    }

    private sealed record DemoResult(int Sales, int Refunds, int Shifts, Dictionary<string, string> Cashiers);

    /// <summary>Ventas sintéticas deterministas (semilla fija) de los últimos días, cerrando cada turno; hoy queda sin caja abierta.</summary>
    private static DemoResult SeedSales(SqliteLocalUnitOfWork unit, CheckoutJournal journal, UserSession session, IdentityService identity, string adminPassword,
        TerminalIdentity terminal, List<(Row Row, Product Product)> products, int days)
    {
        var random = new Random(2026);
        var demoClock = new DemoClock();
        var cashiers = new Dictionary<string, string>();
        foreach (var (user, display) in new[] { ("ana", "Cajera Ana"), ("luis", "Cajero Luis") })
        {
            var temporary = identity.CreateUser(user, display, "Cashier");
            var password = PasswordPolicy.GenerateTemporary();
            identity.ChangePassword(user, temporary, password);
            cashiers[user] = password;
        }
        session.SignOut();
        var cash = new CashService(unit, session, demoClock);
        var closing = new CashClosingService(unit, session, demoClock);
        var pos = new PosService(unit, session, terminal.Id);
        var checkout = new CheckoutService(unit, session, demoClock, terminal.Id, journal);
        var refunds = new RefundService(unit, session, demoClock, terminal.Id);

        // Popularidad relativa por SKU (conchas, bolillo, cuernito y café se venden más; octubre: pan de muerto).
        var weight = new Dictionary<int, int> { [1] = 12, [2] = 10, [3] = 9, [4] = 7, [5] = 3, [6] = 6, [7] = 6, [8] = 4, [9] = 3, [10] = 4, [11] = 3, [12] = 4, [13] = 6,
            [14] = 3, [15] = 2, [16] = 3, [17] = 4, [18] = 3, [19] = 3, [20] = 6, [21] = 3, [22] = 3, [23] = 3, [24] = 8, [25] = 2, [26] = 2, [27] = 4, [28] = 3, [29] = 3, [30] = 5, [31] = 3 };
        var hours = new (int Hour, int Weight)[] { (7, 6), (8, 10), (9, 9), (10, 6), (11, 4), (12, 3), (13, 4), (14, 3), (15, 3), (16, 4), (17, 7), (18, 9), (19, 7), (20, 3) };
        var reasons = new[] { "Pan aplastado", "Cliente cambió de opinión", "Cobro duplicado" };
        var zone = TimeZoneInfo.Local;
        DateTimeOffset At(DateOnly day, int hour, int minute) =>
            new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified), zone), TimeSpan.Zero);
        T Pick<T>(IReadOnlyList<(T Value, int Weight)> options)
        {
            var roll = random.Next(options.Sum(o => o.Weight));
            foreach (var (value, w) in options) { if ((roll -= w) < 0) return value; }
            return options[^1].Value;
        }
        void SignIn(string user, string password) { if (session.Current is not null) session.SignOut(); if (!identity.Login(user, password)) throw new InvalidOperationException($"No se pudo iniciar sesión como {user}."); }

        var productOptions = products.Select(p => (p.Product, weight.GetValueOrDefault(p.Row.Id, 2))).ToList();
        int sales = 0, refundCount = 0, shifts = 0;
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (var offset = days; offset >= 1; offset--)
        {
            var day = today.AddDays(-offset);
            var (user, password) = offset % 2 == 0 ? ("ana", cashiers["ana"]) : ("luis", cashiers["luis"]);
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var count = (weekend ? 42 : 30) + random.Next(0, 9);
            var times = Enumerable.Range(0, count).Select(_ => At(day, Pick(hours.Select(h => (h.Hour, h.Weight)).ToList()), random.Next(0, 60))).OrderBy(t => t).ToList();

            demoClock.UtcNow = At(day, 7, 0);
            SignIn(user, password);
            var shift = cash.Open(terminal.Id, new Money(50000)).Session; shifts++;
            var refundAt = random.Next(5, count - 5);
            for (int i = 0; i < times.Count; i++)
            {
                demoClock.UtcNow = times[i];
                var lines = random.Next(1, 5);
                foreach (var product in Enumerable.Range(0, lines).Select(_ => Pick(productOptions)).Distinct())
                {
                    pos.Add(product.Id);
                    var extra = product.Sku == "CM-003" ? random.Next(0, 6) : random.Next(0, 3);
                    for (int q = 0; q < extra; q++) pos.Increment(product.Id);
                }
                var total = session.Cart!.Subtotal;
                var roll = random.Next(100);
                var method = roll < 65 ? PaymentMethod.Cash : roll < 90 ? PaymentMethod.Card : PaymentMethod.Transfer;
                var received = method == PaymentMethod.Cash ? RoundUpCash(total, random) : total;
                var reference = method == PaymentMethod.Card ? random.Next(1000, 9999).ToString(CultureInfo.InvariantCulture) : null;
                var sale = checkout.Confirm(checkout.Prepare(received, method, reference));
                checkout.Acknowledge(sale.OperationId);
                sales++;

                if (i == refundAt && offset % 3 != 0)
                {
                    demoClock.UtcNow = times[i].AddMinutes(5);
                    SignIn("admin", adminPassword);
                    var reason = reasons[random.Next(reasons.Length)];
                    if (offset == days) refunds.Cancel(sale.Id, "Venta capturada por error");
                    else refunds.Return(sale.Id, [new(1, 1)], reason);
                    refundCount++;
                    SignIn(user, password);
                }
            }

            // Cierre a las 21:00 por el administrador: casi siempre exacto, algún día con faltante o sobrante.
            demoClock.UtcNow = At(day, 21, 0);
            SignIn("admin", adminPassword);
            var expected = unit.Run(s => s.FindSession(shift.Id)!.OpeningFund + s.CashSalesTotal(shift.Id) - s.CashRefundsTotal(shift.Id));
            var difference = offset == 2 ? -2000 : offset == 4 ? 500 : 0;
            closing.SubmitCount(shift.Id, new Money(expected.Cents + difference));
            closing.Close(shift.Id);
            session.SignOut();
        }
        return new(sales, refundCount, shifts, cashiers);
    }

    private static Money RoundUpCash(Money total, Random random)
    {
        if (random.Next(100) < 25) return total; // pago exacto
        foreach (var bill in new long[] { 2000, 5000, 10000, 20000, 50000 })
            if (bill >= total.Cents) return new Money(bill);
        return new Money((total.Cents / 50000 + 1) * 50000);
    }

    private static IReadOnlyList<Row> Read(string csvPath)
    {
        var rows = new List<Row>();
        using var parser = new TextFieldParser(csvPath) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var header = parser.ReadFields() ?? throw new InvalidDataException("Catálogo vacío.");
        if (!header.Take(4).SequenceEqual(["id", "nombre", "precio_mxn", "categoria"])) throw new InvalidDataException("Encabezado de catálogo inesperado.");
        while (!parser.EndOfData)
        {
            var f = parser.ReadFields()!;
            if (f.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(new(int.Parse(f[0], CultureInfo.InvariantCulture), f[1].Trim(), int.Parse(f[2], CultureInfo.InvariantCulture), f[3].Trim(), f.Length > 4 ? f[4].Trim() : ""));
        }
        if (rows.Count == 0 || rows.Select(r => r.Id).Distinct().Count() != rows.Count || rows.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count() != rows.Count)
            throw new InvalidDataException("El catálogo tiene filas vacías o identidades duplicadas.");
        if (rows.Any(r => r.PriceMxn <= 0 || r.Name.Length == 0 || r.Category.Length == 0)) throw new InvalidDataException("Precio, nombre o categoría inválidos.");
        return rows.OrderBy(r => r.Id).ToList();
    }
}
