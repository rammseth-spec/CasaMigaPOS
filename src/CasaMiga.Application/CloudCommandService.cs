using System.Text.Json.Nodes;
using CasaMiga.Domain;

namespace CasaMiga.Application;

/// <summary>Cambio hecho en la versión web para esta sucursal (docs/WEB.md).</summary>
public sealed record CloudCommand(Guid Id, long Sequence, string Kind, JsonObject Payload, Guid RequestedById, string RequestedByName, DateTimeOffset? RequestedAt = null);
public sealed record CloudCommandResult(Guid Id, long Sequence, string Status, string? Message)
{
    public bool Applied => Status == "applied";
}

/// <summary>
/// Aplica en esta caja los cambios hechos en la web, en orden y con las mismas reglas y permisos que en la pantalla:
/// actúa como quien lo pidió si existe aquí; si no (por ejemplo, el administrador de otra sucursal), como el administrador local.
/// Un cambio que las reglas rechazan queda registrado con el motivo; un error inesperado (disco, base) se reintenta después.
/// Aplicar dos veces el mismo cambio no repite nada.
/// </summary>
public sealed class CloudCommandService(ILocalUnitOfWork database, IClock clock, Guid terminalId, TimeZoneInfo timeZone)
{
    /// <summary>
    /// Desde dónde pedir cambios. Se relee un tramo ya aplicado porque dos cambios simultáneos en la web pueden hacerse visibles
    /// fuera de orden (el 42 antes que el 41); los ya aplicados se reconocen por su identificador y no se repiten.
    /// </summary>
    public const long Overlap = 20;
    public long LastSequence() => database.Run(store => store.LastCloudCommandSequence());
    public long PullCursor() => Math.Max(0, LastSequence() - Overlap);
    public IReadOnlyList<CloudCommandResult> Unreported() => database.Run(store => store.UnreportedCloudCommands());
    public void MarkReported(IReadOnlyCollection<Guid> ids) => database.Run(store => { store.MarkCloudCommandsReported(ids, clock.UtcNow); return true; });

    public bool IsKnown(Guid id) => database.Run(store => store.FindCloudCommand(id)) is not null;

    public CloudCommandResult Apply(CloudCommand command)
    {
        if (database.Run(store => store.FindCloudCommand(command.Id)) is { } done) return done;
        string? rejection = null;
        try
        {
            var actor = database.Run(store => Actor(store, command.RequestedById)) ?? throw new AccessDeniedException();
            var session = new UserSession();
            session.SignIn(new UserIdentity(actor.Id, actor.DisplayName));
            Execute(command, session);
        }
        catch (Exception error) when (error is AccessDeniedException or ArgumentException or CatalogException or ConsignmentException or InvalidCommandException or KeyNotFoundException)
        {
            rejection = error.Message;
        }
        var result = new CloudCommandResult(command.Id, command.Sequence, rejection is null ? "applied" : "rejected", rejection);
        database.Run(store => { store.AddCloudCommand(result, command.Kind, command.RequestedByName, clock.UtcNow); return true; });
        return result;
    }

    /// <summary>Quien pidió el cambio si existe aquí; si existe pero está desactivado, nadie (se rechaza); si no existe, el administrador local.</summary>
    private static LocalUser? Actor(ILocalStore store, Guid requestedBy)
    {
        if (store.FindUser(requestedBy) is { } same) return same.Active ? same : null;
        return store.ListUsers().FirstOrDefault(u => u.Role == "Administrator" && u.Active) is { } admin ? store.FindUser(admin.Id) : null;
    }

    private void Execute(CloudCommand command, UserSession session)
    {
        var p = command.Payload;
        var catalog = new CatalogService(database, session, clock, terminalId);
        var identity = new IdentityService(database, new NoPasswords(), clock, session);
        var trays = new ConsignmentService(database, session, clock, terminalId, timeZone);
        // Lo registrado en la ruta conserva la hora en que se hizo en el celular, aunque la caja lo aplique después (sin pasar de ahora).
        var onRoute = new ConsignmentService(database, session, new FixedClock(Earlier(command.RequestedAt, clock.UtcNow)), terminalId, timeZone);
        switch (command.Kind)
        {
            case "category.save":
            {
                var id = Id(p, "id");
                if (database.Run(store => store.FindCategory(id)) is null) catalog.CreateCategory(Text(p, "name"), Text(p, "description"), Int(p, "sortOrder"), id);
                else catalog.EditCategory(id, Text(p, "name"), Text(p, "description"), Int(p, "sortOrder"));
                if (p["active"] is not null) catalog.SetCategoryActive(id, Bool(p, "active"));
                break;
            }
            case "product.save":
            {
                var id = Id(p, "id");
                var sku = p["sku"] is null ? null : Text(p, "sku");
                var price = new Money(Long(p, "priceCents"));
                if (database.Run(store => store.FindProduct(id)) is null)
                    catalog.CreateProduct(Id(p, "categoryId"), sku, Text(p, "name"), Text(p, "description"), price, Bool(p, "trackInventory"), id);
                else catalog.EditProduct(id, Id(p, "categoryId"), sku, Text(p, "name"), Text(p, "description"), price, Bool(p, "trackInventory"));
                break;
            }
            case "product.active": catalog.SetProductActive(Id(p, "id"), Bool(p, "active")); break;
            case "user.create": identity.CreateUserWithHash(Id(p, "id"), Text(p, "username"), Text(p, "displayName"), Text(p, "role"), Text(p, "passwordHash")); break;
            case "user.update": identity.UpdateUser(Id(p, "id"), Text(p, "displayName"), Text(p, "role")); break;
            case "user.active": identity.SetActive(Id(p, "id"), Bool(p, "active")); break;
            case "user.reset_password": identity.ResetPasswordWithHash(Id(p, "id"), Text(p, "passwordHash")); break;
            case "consignment.store.save":
            {
                var id = Id(p, "id");
                var exists = database.Run(store => store.FindConsignmentStore(id)) is not null;
                trays.SaveStore(exists ? id : null, Text(p, "name"), Text(p, "contact"), Text(p, "phone"), Text(p, "address"), exists ? null : id);
                if (p["active"] is not null && database.Run(store => store.FindConsignmentStore(id))!.Active != Bool(p, "active")) trays.SetStoreActive(id, Bool(p, "active"));
                break;
            }
            case "consignment.deliver":
                onRoute.Deliver(Id(p, "storeId"), Lines(p, "lines"), Text(p, "note"), command.Id);
                break;
            case "consignment.collect":
                onRoute.Collect(Id(p, "deliveryId"),
                    (p["returned"] as JsonArray ?? throw new InvalidCommandException()).Select(r => r as JsonObject ?? throw new InvalidCommandException())
                        .ToDictionary(r => Int(r, "position"), r => Int(r, "quantity")),
                    new Money(Long(p, "paidCents")), Method(p), p["reference"] is null ? null : Text(p, "reference"), Text(p, "note"), command.Id);
                break;
            case "consignment.payment":
                onRoute.ReceivePayment(Id(p, "storeId"), new Money(Long(p, "amountCents")), Method(p), p["reference"] is null ? null : Text(p, "reference"), Text(p, "note"), command.Id);
                break;
            case "consignment.void":
                onRoute.Void(Id(p, "deliveryId"), Text(p, "reason"));
                break;
            case "consignment.template.save":
                trays.SaveTemplate(Id(p, "storeId"), (p["lines"] as JsonArray ?? throw new InvalidCommandException())
                    .Select(l => new ConsignmentLine(Id(l as JsonObject ?? throw new InvalidCommandException(), "productId"), Int((JsonObject)l!, "quantity"))).ToList());
                break;
            default: throw new InvalidCommandException("Esta caja no reconoce ese cambio; actualiza Casa Miga.");
        }
    }

    private sealed class InvalidCommandException(string message = "Cambio con formato inválido.") : Exception(message);
    private sealed class FixedClock(DateTimeOffset at) : IClock { public DateTimeOffset UtcNow => at; }
    private static DateTimeOffset Earlier(DateTimeOffset? requested, DateTimeOffset now) => requested is { } at && at < now ? at.ToUniversalTime() : now;
    private static PaymentMethod Method(JsonObject p)
    {
        try { return Payment.Parse(Text(p, "method")); }
        catch (InvalidDataException) { throw new InvalidCommandException(); }
    }
    private static List<ConsignmentLine> Lines(JsonObject p, string name) =>
        (p[name] as JsonArray ?? throw new InvalidCommandException())
            .Select(l => l as JsonObject ?? throw new InvalidCommandException())
            .Select(l => new ConsignmentLine(Id(l, "productId"), Int(l, "quantity"))).ToList();
    /// <summary>Los cambios de la web nunca crean contraseñas aquí: ya traen su hash.</summary>
    private sealed class NoPasswords : IPasswords
    {
        public string Hash(string password) => throw new InvalidCommandException();
        public PasswordCheck Verify(string? hash, string password) => PasswordCheck.Failed;
    }

    private static string Text(JsonObject p, string name)
    {
        try { return p[name]?.GetValue<string>() ?? throw new InvalidCommandException(); }
        catch (Exception error) when (error is InvalidOperationException or FormatException) { throw new InvalidCommandException(); }
    }
    // Por texto JSON: igual si el valor llegó por red (JsonElement) o se armó en memoria (int, long, bool).
    private static long Long(JsonObject p, string name) =>
        p[name] is JsonValue value && long.TryParse(value.ToJsonString(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : throw new InvalidCommandException();
    private static int Int(JsonObject p, string name) => Long(p, name) is var v and >= int.MinValue and <= int.MaxValue ? (int)v : throw new InvalidCommandException();
    private static bool Bool(JsonObject p, string name) => (p[name] as JsonValue)?.ToJsonString() switch
    {
        "true" => true, "false" => false, _ => throw new InvalidCommandException(),
    };
    private static Guid Id(JsonObject p, string name) => Guid.TryParse(Text(p, name), out var id) && id != Guid.Empty ? id : throw new InvalidCommandException();
}
