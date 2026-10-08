using System.Globalization;
using System.Text.Json;
using CasaMiga.Domain;

namespace CasaMiga.Application;

/// <summary>
/// Charolas a consignación (docs/CONSIGNMENT.md). Se dejan piezas surtidas en tiendas cliente con el precio de charola de cada producto;
/// al recogerlas, lo vendido se carga a la cuenta de la tienda y lo que regresa se aparta para hacer Piedra.
/// Los cobros a tiendas no forman parte del efectivo esperado de la caja del mostrador.
/// </summary>
public sealed class ConsignmentService(ILocalUnitOfWork database, UserSession identity, IClock clock, Guid terminalId, TimeZoneInfo timeZone)
{
    public const string ViewPermission = "consignment.view";
    public const string ManagePermission = "consignment.manage";
    public const int MaxPiecesPerLine = 999;
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

    public bool CanView() => database.Run(store => Actor(store, ViewPermission) is not null);
    public bool CanManage() => database.Run(store => Actor(store, ManagePermission) is not null);

    // ---------- Tiendas ----------
    public IReadOnlyList<ConsignmentStore> Stores(bool includeInactive = false) => View(store => store.ListConsignmentStores(includeInactive));

    /// <param name="newId">Sólo para altas hechas en la versión web (con <paramref name="id"/> nulo): identificador que trae la nube.</param>
    public ConsignmentStore SaveStore(Guid? id, string name, string contact, string phone, string address, Guid? newId = null)
    {
        name = name.Trim(); contact = contact.Trim(); phone = phone.Trim(); address = address.Trim();
        if (name.Length is < 1 or > 120) throw new ArgumentException("Escribe el nombre de la tienda (hasta 120 caracteres).");
        if (contact.Length > 120 || phone.Length > 40 || address.Length > 200) throw new ArgumentException("Contacto, teléfono o dirección demasiado largos.");
        return Manage((store, actor, at) =>
        {
            var existing = id is { } key ? store.FindConsignmentStore(key) ?? throw new ConsignmentException("No se encontró la tienda.") : null;
            if (store.ConsignmentStoreNameTaken(name, existing?.Id)) throw new ConsignmentException($"Ya existe una tienda llamada «{name}».");
            var saved = new ConsignmentStore(existing?.Id ?? newId ?? Guid.NewGuid(), name, contact, phone, address, existing?.Active ?? true);
            store.SaveConsignmentStore(saved, existing is null, at);
            Audit(store, actor, existing is null ? "consignment.store.created" : "consignment.store.updated", saved.Id, at,
                new { saved.Name, saved.Contact, saved.Phone, saved.Address });
            return saved;
        });
    }

    public ConsignmentStore SetStoreActive(Guid id, bool active) => Manage((store, actor, at) =>
    {
        var existing = store.FindConsignmentStore(id) ?? throw new ConsignmentException("No se encontró la tienda.");
        if (!active && store.QueryConsignmentDeliveries(null, null, id, openOnly: true).Count > 0)
            throw new ConsignmentException("Primero recoge o anula las charolas que siguen en esa tienda.");
        var saved = existing with { Active = active };
        store.SaveConsignmentStore(saved, false, at);
        Audit(store, actor, active ? "consignment.store.activated" : "consignment.store.deactivated", id, at, new { saved.Name });
        return saved;
    });

    public IReadOnlyList<ConsignmentTemplateLine> Template(Guid storeId) => View(store => store.ConsignmentTemplate(storeId));

    public void SaveTemplate(Guid storeId, IReadOnlyList<ConsignmentLine> lines) => Manage((store, actor, at) =>
    {
        _ = store.FindConsignmentStore(storeId) ?? throw new ConsignmentException("No se encontró la tienda.");
        var normalized = Normalize(lines, allowEmpty: true);
        foreach (var line in normalized)
            if (store.FindProduct(line.ProductId) is not { IsActive: true }) throw new ConsignmentException("Uno de los productos ya no está activo.");
        store.ReplaceConsignmentTemplate(storeId, normalized);
        Audit(store, actor, "consignment.template.saved", storeId, at, new { pieces = normalized.Sum(l => l.Quantity), lines = normalized });
        return true;
    });

    // ---------- Precios de charola ----------
    public IReadOnlyList<ConsignmentPrice> Prices() => View(store => store.ConsignmentPrices());

    /// <summary>Precio de charola de un producto; null lo quita (ya no se puede poner en charolas).</summary>
    public void SetPrice(Guid productId, Money? price)
    {
        if (price is { Cents: < 0 }) throw new ArgumentException("El precio de charola no puede ser negativo.");
        Manage((store, actor, at) =>
        {
            var product = store.FindProduct(productId) ?? throw new ConsignmentException("No se encontró el producto.");
            var before = store.FindConsignmentPrice(productId);
            if (before == price) return true;
            store.SetConsignmentPrice(productId, price, at);
            Audit(store, actor, "consignment.price.changed", productId, at,
                new { product = product.Name, beforeCents = before?.Cents, afterCents = price?.Cents, retailCents = product.SalePrice.Cents });
            return true;
        });
    }

    // ---------- Ruta del día ----------
    public IReadOnlyList<ConsignmentStoreStatus> Route() => View(store =>
    {
        var open = store.QueryConsignmentDeliveries(null, null, null, openOnly: true).ToLookup(d => d.StoreId);
        return store.ListConsignmentStores(includeInactive: false).Select(s => new ConsignmentStoreStatus(s,
            open[s.Id].OrderBy(d => d.DeliveredAt).ToList(), store.ConsignmentBalance(s.Id), store.LastConsignmentDelivery(s.Id),
            store.ConsignmentTemplate(s.Id).Sum(l => l.Quantity))).ToList();
    });

    public ConsignmentDelivery Deliver(Guid storeId, IReadOnlyList<ConsignmentLine> lines, string note)
    {
        note = note.Trim();
        if (note.Length > 200) throw new ArgumentException("La nota admite hasta 200 caracteres.");
        var normalized = Normalize(lines, allowEmpty: false);
        var operation = Guid.NewGuid();
        return Idempotent(operation, store => store.FindConsignmentDeliveryByOperation(operation), () => Manage((store, actor, at) =>
        {
            var target = store.FindConsignmentStore(storeId) ?? throw new ConsignmentException("No se encontró la tienda.");
            if (!target.Active) throw new ConsignmentException("La tienda está desactivada.");
            var items = normalized.Select((line, index) =>
            {
                var product = store.FindProduct(line.ProductId) is { IsActive: true } p ? p : throw new ConsignmentException("Uno de los productos ya no está activo.");
                var price = store.FindConsignmentPrice(product.Id) ?? throw new ConsignmentException($"{product.Name} no tiene precio de charola. Defínelo en Precios.");
                return new ConsignmentDeliveryItem(index + 1, product.Id, product.Name, price, product.SalePrice, line.Quantity);
            }).ToList();
            var delivery = new ConsignmentDelivery(Guid.NewGuid(), operation, target.Id, target.Name, terminalId, actor.Id, actor.DisplayName, at, note, items);
            store.AddConsignmentDelivery(delivery);
            Audit(store, actor, "consignment.delivered", delivery.Id, at, new
            {
                storeId = target.Id, store = target.Name, pieces = delivery.Pieces, valueCents = delivery.Value.Cents, note,
                items = items.Select(i => new { i.Position, i.ProductId, product = i.ProductName, i.Quantity, unitPriceCents = i.UnitPrice.Cents, retailCents = i.RetailPrice.Cents })
            });
            return delivery;
        }));
    }

    /// <summary>Recoge una charola: <paramref name="returned"/> son las piezas que regresan por partida (las demás se vendieron).</summary>
    public ConsignmentCollection Collect(Guid deliveryId, IReadOnlyDictionary<int, int> returned, Money paidNow, PaymentMethod method, string? reference, string note)
    {
        note = note.Trim();
        if (note.Length > 200) throw new ArgumentException("La nota admite hasta 200 caracteres.");
        if (paidNow.Cents < 0) throw new ArgumentException("El cobro no puede ser negativo.");
        reference = Reference(paidNow.Cents > 0 ? method : PaymentMethod.Cash, reference);
        var operation = Guid.NewGuid();
        return Idempotent(operation, store => store.FindConsignmentDeliveryBySettlement(operation) is { } done
            ? new ConsignmentCollection(done, store.FindConsignmentPaymentByOperation(operation), store.ConsignmentBalance(done.StoreId)) : null, () => Manage((store, actor, at) =>
        {
            var delivery = store.FindConsignmentDelivery(deliveryId) ?? throw new ConsignmentException("No se encontró la charola.");
            if (delivery.State != ConsignmentState.AtStore) throw new ConsignmentException(delivery.State == ConsignmentState.Voided ? "Esa charola se anuló." : "Esa charola ya se recogió.");
            foreach (var position in returned.Keys)
                if (delivery.Items.All(i => i.Position != position)) throw new ArgumentException("La partida no pertenece a la charola.");
            var items = delivery.Items.Select(i =>
            {
                var back = returned.GetValueOrDefault(i.Position);
                if (back < 0 || back > i.Quantity) throw new ArgumentException($"De {i.ProductName} se entregaron {i.Quantity}; regresaron entre 0 y {i.Quantity}.");
                return new ConsignmentSettlementItem(i.Position, i.Quantity - back, back, i.UnitPrice.Multiply(i.Quantity - back));
            }).ToList();
            var settlement = new ConsignmentSettlement(delivery.Id, operation, actor.Id, at, new(checked(items.Sum(i => i.LineTotal.Cents))), note, items);
            var owed = store.ConsignmentBalance(delivery.StoreId) + settlement.SoldTotal;
            if (paidNow.Cents > owed.Cents) throw new ConsignmentException($"El cobro no puede ser mayor que lo que debe la tienda ({Format(owed)}).");
            store.AddConsignmentSettlement(settlement);
            Audit(store, actor, "consignment.collected", delivery.Id, at, new
            {
                storeId = delivery.StoreId, store = delivery.StoreName, sold = settlement.Sold, returned = settlement.Returned, soldCents = settlement.SoldTotal.Cents, note,
                items = items.Select(i => new { i.Position, i.Sold, i.Returned, lineTotalCents = i.LineTotal.Cents })
            });
            ConsignmentPayment? payment = null;
            if (paidNow.Cents > 0)
            {
                payment = new ConsignmentPayment(Guid.NewGuid(), operation, delivery.StoreId, actor.Id, at, paidNow, method, reference, "Al recoger la charola");
                store.AddConsignmentPayment(payment);
                AuditPayment(store, actor, payment, delivery.StoreName, at);
            }
            return new ConsignmentCollection(delivery with { Settlement = settlement }, payment, owed - paidNow);
        }));
    }

    public ConsignmentDelivery Void(Guid deliveryId, string reason)
    {
        reason = reason.Trim();
        if (reason.Length is < 3 or > 200) throw new ArgumentException("Escribe el motivo (de 3 a 200 caracteres).");
        return Manage((store, actor, at) =>
        {
            var delivery = store.FindConsignmentDelivery(deliveryId) ?? throw new ConsignmentException("No se encontró la charola.");
            if (delivery.State != ConsignmentState.AtStore) throw new ConsignmentException("Sólo se anula una charola que sigue en la tienda.");
            var cancel = new ConsignmentVoid(delivery.Id, Guid.NewGuid(), actor.Id, at, reason);
            store.AddConsignmentVoid(cancel);
            Audit(store, actor, "consignment.voided", delivery.Id, at, new { storeId = delivery.StoreId, store = delivery.StoreName, pieces = delivery.Pieces, reason });
            return delivery with { Void = cancel };
        });
    }

    public ConsignmentPayment ReceivePayment(Guid storeId, Money amount, PaymentMethod method, string? reference, string note)
    {
        note = note.Trim();
        if (note.Length > 200) throw new ArgumentException("La nota admite hasta 200 caracteres.");
        if (amount.Cents <= 0) throw new ArgumentException("Escribe el importe cobrado.");
        reference = Reference(method, reference);
        var operation = Guid.NewGuid();
        return Idempotent(operation, store => store.FindConsignmentPaymentByOperation(operation), () => Manage((store, actor, at) =>
        {
            var target = store.FindConsignmentStore(storeId) ?? throw new ConsignmentException("No se encontró la tienda.");
            var owed = store.ConsignmentBalance(storeId);
            if (amount.Cents > owed.Cents) throw new ConsignmentException(owed.Cents == 0 ? "La tienda no tiene saldo pendiente." : $"El cobro no puede ser mayor que el saldo ({Format(owed)}).");
            var payment = new ConsignmentPayment(Guid.NewGuid(), operation, storeId, actor.Id, at, amount, method, reference, note);
            store.AddConsignmentPayment(payment);
            AuditPayment(store, actor, payment, target.Name, at);
            return payment;
        }));
    }

    public ConsignmentAccount Account(Guid storeId) => View(store =>
    {
        var target = store.FindConsignmentStore(storeId) ?? throw new ConsignmentException("No se encontró la tienda.");
        var charges = store.QueryConsignmentDeliveries(null, null, storeId, openOnly: false).Where(d => d.Settlement is not null).Select(d =>
            new ConsignmentMovement(d.Settlement!.CollectedAt, $"Charola del {Local(d.DeliveredAt):ddd d MMM}: {d.Settlement.Sold} vendidas, {d.Settlement.Returned} regresaron",
                d.Settlement.SoldTotal, new(0)));
        var payments = store.QueryConsignmentPayments(null, null, storeId).Select(p =>
            new ConsignmentMovement(p.ReceivedAt, $"Pago con {Payment.Label(p.Method).ToLower(Spanish)}" + (p.Note.Length > 0 ? $" · {p.Note}" : ""), new(0), p.Amount));
        return new ConsignmentAccount(target, charges.Concat(payments).OrderByDescending(m => m.At).ToList(), store.ConsignmentBalance(storeId));
    });

    // ---------- Reporte ----------
    public ConsignmentReport Report(DateOnly from, DateOnly through)
    {
        if (from > through) throw new ReportException("La fecha Desde no puede ser posterior a Hasta.");
        if (through.DayNumber - from.DayNumber + 1 > ReportService.MaxDays) throw new ReportException($"El periodo máximo es de {ReportService.MaxDays} días.");
        if (through == DateOnly.MaxValue) throw new ReportException("La fecha Hasta excede el límite permitido.");
        var fromUtc = ToUtc(from); var untilUtc = ToUtc(through.AddDays(1));
        return View(store => Summarize(from, through, store.QueryConsignmentDeliveries(fromUtc, untilUtc, null, openOnly: false),
            store.QueryConsignmentPayments(fromUtc, untilUtc, null), store.ListConsignmentStores(includeInactive: true).ToDictionary(s => s.Id, s => store.ConsignmentBalance(s.Id))));
    }

    /// <summary>Cálculo puro del reporte: charolas entregadas en el periodo (sin anuladas) y pagos recibidos en el periodo.</summary>
    public static ConsignmentReport Summarize(DateOnly from, DateOnly through, IEnumerable<ConsignmentDelivery> deliveries, IEnumerable<ConsignmentPayment> payments,
        IReadOnlyDictionary<Guid, Money> balances)
    {
        var valid = deliveries.Where(d => d.State != ConsignmentState.Voided).ToList();
        var paid = payments.GroupBy(p => p.StoreId).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount.Cents));
        var names = valid.GroupBy(d => d.StoreId).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.DeliveredAt).First().StoreName);
        var storeIds = valid.Select(d => d.StoreId).Concat(paid.Keys).Distinct();
        var stores = storeIds.Select(id =>
        {
            var own = valid.Where(d => d.StoreId == id).ToList();
            var collected = own.Where(d => d.Settlement is not null).ToList();
            var lines = collected.SelectMany(d => d.Items.Join(d.Settlement!.Items, i => i.Position, s => s.Position, (i, s) => (Item: i, Settled: s))).ToList();
            return new ConsignmentStoreRow(id, names.GetValueOrDefault(id, ""), own.Count, own.Sum(d => d.Pieces), collected.Count,
                lines.Sum(l => l.Settled.Sold), lines.Sum(l => l.Settled.Returned), own.Where(d => d.Settlement is null).Sum(d => d.Pieces),
                new(lines.Sum(l => l.Settled.LineTotal.Cents)), new(lines.Sum(l => l.Item.RetailPrice.Cents * l.Settled.Sold)),
                new(paid.GetValueOrDefault(id)), balances.GetValueOrDefault(id, new Money(0)));
        }).Where(s => s.StoreName.Length > 0 || s.Paid.Cents > 0)
          .OrderByDescending(s => s.Charged.Cents).ThenBy(s => s.StoreName, StringComparer.CurrentCulture).ToList();
        var products = valid.SelectMany(d => d.Items.Select(i => (Item: i, Settled: d.Settlement?.Items.FirstOrDefault(s => s.Position == i.Position))))
            .GroupBy(x => x.Item.ProductId).Select(g => new ConsignmentProductRow(g.Key, g.Last().Item.ProductName, g.Sum(x => x.Item.Quantity),
                g.Sum(x => x.Settled?.Sold ?? 0), g.Sum(x => x.Settled?.Returned ?? 0), new(g.Sum(x => x.Settled?.LineTotal.Cents ?? 0)),
                new(g.Sum(x => x.Item.RetailPrice.Cents * (x.Settled?.Sold ?? 0)))))
            .OrderByDescending(p => p.Sold).ThenBy(p => p.ProductName, StringComparer.CurrentCulture).ToList();
        return new(from, through, stores, products);
    }

    // ---------- Apoyo ----------
    private static List<ConsignmentLine> Normalize(IReadOnlyList<ConsignmentLine> lines, bool allowEmpty)
    {
        var result = new List<ConsignmentLine>();
        foreach (var group in lines.Where(l => l.Quantity != 0).GroupBy(l => l.ProductId))
        {
            var quantity = group.Sum(l => l.Quantity);
            if (quantity is < 1 or > MaxPiecesPerLine) throw new ArgumentException($"Cada pan admite de 1 a {MaxPiecesPerLine} piezas.");
            result.Add(new(group.Key, quantity));
        }
        if (result.Count == 0 && !allowEmpty) throw new ArgumentException("Agrega al menos una pieza a la charola.");
        return result;
    }

    private static string? Reference(PaymentMethod method, string? reference)
    {
        reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (method == PaymentMethod.Cash) return null;
        if (reference is { Length: > 60 }) throw new ArgumentException("La referencia admite hasta 60 caracteres.");
        return reference;
    }

    private T Idempotent<T>(Guid operation, Func<ILocalStore, T?> stored, Func<T> run) where T : class
    {
        try { return run(); }
        catch (Exception error) when (error is not (ConsignmentException or AccessDeniedException or ArgumentException or ReportException))
        {
            // Resultado incierto (p. ej. fallo al confirmar): si la operación quedó guardada, se informa como hecha.
            if (database.Run(store => stored(store)) is { } done) return done;
            throw;
        }
    }

    private T View<T>(Func<ILocalStore, T> read) => database.Run(store =>
    {
        _ = Actor(store, ViewPermission) ?? throw new AccessDeniedException();
        return read(store);
    });

    private T Manage<T>(Func<ILocalStore, LocalUser, DateTimeOffset, T> write) => identity.Synchronized(() => database.Run(store =>
    {
        var actor = Actor(store, ManagePermission) ?? throw new AccessDeniedException();
        return write(store, actor, clock.UtcNow);
    }));

    private LocalUser? Actor(ILocalStore store, string permission) =>
        identity.Current is { } current && store.FindUser(current.Id) is { Active: true } user && store.HasPermission(user.Id, permission) ? user : null;

    private void Audit(ILocalStore store, LocalUser actor, string action, Guid entity, DateTimeOffset at, object details) =>
        store.AddAudit(new(Guid.NewGuid(), actor.Id, terminalId, action, entity, at, JsonSerializer.Serialize(details)));

    private void AuditPayment(ILocalStore store, LocalUser actor, ConsignmentPayment payment, string storeName, DateTimeOffset at) =>
        Audit(store, actor, "consignment.payment", payment.Id, at, new
        {
            storeId = payment.StoreId, store = storeName, amountCents = payment.Amount.Cents, method = Payment.Code(payment.Method), payment.Reference, payment.Note
        });

    private DateTime Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, timeZone).DateTime;

    private DateTimeOffset ToUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local)) throw new ReportException("La fecha coincide con una hora local inexistente.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    private static string Format(Money value) => (value.Cents / 100m).ToString("C2", Spanish);
}
