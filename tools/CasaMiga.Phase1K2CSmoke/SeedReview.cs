using System.Globalization;
using System.IO;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualBasic.FileIO;

/// <summary>
/// Prepara una copia de revisión NUEVA con el catálogo autorizado v2 versionado en docs/catalog (sin fotos: los mosaicos
/// originales no están en Git). Crea administrador y un cajero con contraseña temporal; las credenciales se imprimen una sola vez.
/// Exige carpeta nueva y vacía: nunca toca una instalación existente. Herramienta de desarrollo; no se instala en las cajas.
/// </summary>
internal static class SeedReview
{
    private sealed record Row(int Id, string Name, int PriceMxn, string Category, string Note);

    public static int Run(string csvPath, string targetPath)
    {
        var target = Path.GetFullPath(targetPath);
        if (!Path.IsPathFullyQualified(target) || target.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("Se requiere una ruta local absoluta.");
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()) throw new InvalidOperationException("La copia de revisión requiere una carpeta nueva y vacía.");
        var rows = Read(csvPath);
        Directory.CreateDirectory(target);
        using var lease = new DirectoryLease(target);
        var clock = new SystemClock();
        new FoundationRuntime(new FoundationOptions(target, true)).Start();
        var unit = new SqliteLocalUnitOfWork(new Database(Path.Combine(target, "casamiga.db")));
        var terminal = TerminalConfiguration.LoadOrCreate(target);
        var session = new UserSession(new CheckoutJournal(target));
        var identity = new IdentityService(unit, new LocalPasswords(), clock, session);

        var adminPassword = PasswordPolicy.GenerateTemporary();
        identity.Bootstrap("admin", "Administrador", adminPassword);
        if (!identity.Login("admin", adminPassword)) throw new InvalidOperationException("No se pudo iniciar sesión con el administrador recién creado.");
        var cashierTemporary = identity.CreateUser("cajero1", "Cajero de prueba", "Cashier");

        var catalog = new CatalogService(unit, session, clock, terminal.Id, true);
        var categories = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var name in rows.Select(r => r.Category).Distinct(StringComparer.Ordinal))
            categories.Add(name, catalog.CreateCategory(name, "Catálogo autorizado v2 · docs/catalog", categories.Count).Id);
        foreach (var row in rows)
            catalog.CreateProduct(categories[row.Category], $"CM-{row.Id:000}", row.Name, row.Note, new Money(checked(row.PriceMxn * 100L)), false);
        session.SignOut();

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Target = target, Terminal = terminal.Name, Categories = categories.Count, Products = rows.Count,
            Admin = "admin", AdminPassword = adminPassword, Cashier = "cajero1", CashierTemporaryPassword = cashierTemporary
        }));
        return 0;
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
