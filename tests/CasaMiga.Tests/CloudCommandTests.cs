using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>Cambios hechos en la versión web que la caja aplica con sus propias reglas (docs/WEB.md).</summary>
[TestClass]
public sealed class CloudCommandTests : SalesFixture
{
    // Hash generado por la nube para «Prueba#2026nube» (ver CloudPasswordCompatibilityTests).
    private const string CloudHash = "AQAAAAIAAzRQAAAAEBxRae3FEw6FtrMg/PLCO2PyRY0QCpPaCsZdt3OpOY7AbpYhUQCF95JqER4xcPLzdA==";
    private long sequence;
    private CloudCommandService Commands => new(work, clock, terminal.Id, TimeZoneInfo.Utc);
    private CloudCommand Command(string kind, JsonObject payload, Guid? by = null) => new(Guid.NewGuid(), ++sequence, kind, payload, by ?? user.Current!.Id, "Administrador web");
    private Product Product(Guid id) => work.Run(s => s.FindProduct(id))!;
    private static void Ok(CloudCommandResult result) => Assert.IsTrue(result.Applied, result.Message);

    [TestMethod]
    public void ProductsAndPricesChangeWithTheSameIdInEveryTerminal()
    {
        var price = Command("product.save", new() { ["id"] = product.Id.ToString(), ["categoryId"] = product.CategoryId.ToString(), ["sku"] = "CM-1", ["name"] = "Concha", ["description"] = "", ["priceCents"] = 2200, ["trackInventory"] = true });
        Ok(Commands.Apply(price));
        Assert.AreEqual(2200L, Product(product.Id).SalePrice.Cents);
        Assert.AreEqual(price.Id, Commands.Apply(price).Id, "Applying the same change again does nothing new.");
        Assert.AreEqual(1L, Convert.ToInt64(Sql("SELECT count(*) FROM cloud_command")));

        var fresh = Guid.NewGuid();
        Ok(Commands.Apply(Command("product.save", new() { ["id"] = fresh.ToString(), ["categoryId"] = product.CategoryId.ToString(), ["sku"] = null, ["name"] = "Rol de canela", ["description"] = "Nuevo", ["priceCents"] = 2500, ["trackInventory"] = false })));
        Assert.AreEqual("Rol de canela", Product(fresh).Name, "The product keeps the id chosen on the web.");
        Ok(Commands.Apply(Command("product.active", new() { ["id"] = fresh.ToString(), ["active"] = false })));
        Assert.IsFalse(Product(fresh).IsActive);

        var rejected = Commands.Apply(Command("product.save", new() { ["id"] = Guid.NewGuid().ToString(), ["categoryId"] = Guid.NewGuid().ToString(), ["sku"] = null, ["name"] = "X", ["description"] = "", ["priceCents"] = 100, ["trackInventory"] = true }));
        Assert.AreEqual("rejected", rejected.Status);
        StringAssert.Contains(rejected.Message, "categoría");
        var malformed = Commands.Apply(Command("product.save", new() { ["id"] = "no" }));
        Assert.AreEqual("Cambio con formato inválido.", malformed.Message);
        Assert.AreEqual("rejected", Commands.Apply(Command("sale.delete", new())).Status);

        Assert.AreEqual(sequence, Commands.LastSequence());
        Assert.HasCount(6, Commands.Unreported());
        Commands.MarkReported(Commands.Unreported().Select(r => r.Id).ToArray());
        Assert.IsEmpty(Commands.Unreported());
        Assert.AreEqual(1L, Convert.ToInt64(Sql($"SELECT count(*) FROM audit_entry WHERE action='product.create' AND entity_id='{fresh}'")));
    }

    [TestMethod]
    public void UsersCreatedOnTheWebSignInWithTheTemporaryPasswordAndMustChangeIt()
    {
        var id = Guid.NewGuid();
        Ok(Commands.Apply(Command("user.create", new() { ["id"] = id.ToString(), ["username"] = "maria", ["displayName"] = "María", ["role"] = "Cashier", ["passwordHash"] = CloudHash })));
        var identity = new IdentityService(work, new LocalPasswords(), clock, new UserSession());
        Assert.AreEqual(LoginOutcome.PasswordChangeRequired, identity.Authenticate("maria", "Prueba#2026nube"));
        Ok(Commands.Apply(Command("user.update", new() { ["id"] = id.ToString(), ["displayName"] = "María López", ["role"] = "Manager" })));
        Assert.AreEqual("Manager", work.Run(s => s.FindUser(id))!.Role);
        Ok(Commands.Apply(Command("user.active", new() { ["id"] = id.ToString(), ["active"] = false })));
        Assert.IsFalse(work.Run(s => s.FindUser(id))!.Active);

        var admin = user.Current!.Id;
        Assert.AreEqual("rejected", Commands.Apply(Command("user.reset_password", new() { ["id"] = admin.ToString(), ["passwordHash"] = CloudHash })).Status, "The administrator is never reset from the web.");
        Assert.AreEqual("rejected", Commands.Apply(Command("user.create", new() { ["id"] = Guid.NewGuid().ToString(), ["username"] = "maria", ["displayName"] = "Otra", ["role"] = "Cashier", ["passwordHash"] = CloudHash })).Status);
        Assert.IsTrue(identity.Login("admin", password), "The administrator password is untouched.");
    }

    [TestMethod]
    public void ChangesActAsTheRequesterHereOrAsTheLocalAdministrator()
    {
        var identity = new IdentityService(work, new LocalPasswords(), clock, user);
        var temporary = identity.CreateUser("ana", "Ana", "Manager");
        identity.ChangePassword("ana", temporary, "Ana#2026centro");
        Assert.IsTrue(identity.Login("admin", password));
        var ana = work.Run(s => s.FindUser("ANA"))!.Id;

        var denied = Commands.Apply(Command("user.create", new() { ["id"] = Guid.NewGuid().ToString(), ["username"] = "x", ["displayName"] = "X", ["role"] = "Cashier", ["passwordHash"] = CloudHash }, ana));
        Assert.AreEqual("No tienes permiso para esta operación.", denied.Message, "A local manager keeps the local permissions.");
        Ok(Commands.Apply(Command("category.save", new() { ["id"] = Guid.NewGuid().ToString(), ["name"] = "Salado", ["description"] = "", ["sortOrder"] = 2 }, ana)));
        var category = (string)Sql("SELECT id FROM category WHERE name='Salado'")!;
        Assert.AreEqual(ana.ToString(), Sql($"SELECT actor_id FROM audit_entry WHERE action='category.create' AND entity_id='{category}'"));

        // El administrador de otra sucursal no existe aquí: actúa el administrador local.
        Ok(Commands.Apply(Command("category.save", new() { ["id"] = category, ["name"] = "Pan salado", ["description"] = "", ["sortOrder"] = 2, ["active"] = false }, Guid.NewGuid())));
        Assert.AreEqual("Pan salado", work.Run(s => s.FindCategory(Guid.Parse(category)))!.Name);
        Assert.IsFalse(work.Run(s => s.FindCategory(Guid.Parse(category)))!.IsActive);
    }

    [TestMethod]
    public void TrayStoresAndTemplatesFromTheWeb()
    {
        var trays = new ConsignmentService(work, user, clock, terminal.Id, TimeZoneInfo.Utc);
        trays.SetPrice(product.Id, new Money(1500));
        var store = Guid.NewGuid();
        Ok(Commands.Apply(Command("consignment.store.save", new() { ["id"] = store.ToString(), ["name"] = "Abarrotes Lupita", ["contact"] = "Lupita", ["phone"] = "555", ["address"] = "" })));
        Assert.AreEqual("Abarrotes Lupita", trays.Stores().Single(s => s.Id == store).Name);
        Ok(Commands.Apply(Command("consignment.template.save", new() { ["storeId"] = store.ToString(), ["lines"] = new JsonArray(new JsonObject { ["productId"] = product.Id.ToString(), ["quantity"] = 6 }) })));
        Assert.AreEqual(6, trays.Template(store).Single().Quantity);
        Ok(Commands.Apply(Command("consignment.store.save", new() { ["id"] = store.ToString(), ["name"] = "Lupita", ["contact"] = "", ["phone"] = "", ["address"] = "", ["active"] = false })));
        Assert.IsFalse(trays.Stores(includeInactive: true).Single(s => s.Id == store).Active);
    }

    private sealed class Transport : ISyncTransport
    {
        public List<CloudCommand> Pending { get; } = [];
        public List<CloudCommandResult> Reported { get; } = [];
        public List<SyncEnvelope> Sent { get; } = [];
        public Task<string> RegisterAsync(Uri api, string enrollmentSecret, BranchIdentity branch, Guid terminalId, string terminalName, CancellationToken cancel) => Task.FromResult("K" + new string('k', 42));
        public Task<SyncBatchResult> SendAsync(Uri api, string terminalKey, Guid branchId, IReadOnlyList<SyncEnvelope> batch, CancellationToken cancel)
        { Sent.AddRange(batch); return Task.FromResult(new SyncBatchResult(batch.Select(b => b.Entry.Sequence).ToArray(), [])); }
        public Task<IReadOnlyList<CloudCommand>> PullCommandsAsync(Uri api, string terminalKey, long after, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<CloudCommand>>(Pending.Where(c => c.Sequence > after).ToList());
        public Task ReportCommandsAsync(Uri api, string terminalKey, IReadOnlyList<CloudCommandResult> results, CancellationToken cancel) { Reported.AddRange(results); return Task.CompletedTask; }
    }

    [TestMethod]
    public async Task SyncAppliesWebChangesReportsThemAndUploadsTheResult()
    {
        var transport = new Transport();
        var outbox = new SqliteSyncOutbox(db);
        var engine = new SyncEngine(outbox, transport, directory, commands: Commands);
        await engine.RegisterAsync("https://sync.example.test/", "secreto-correcto", "Centro", terminal.Id, terminal.Name);
        Assert.AreEqual(SyncState.UpToDate, (await engine.RunOnceAsync()).State);

        transport.Pending.Add(Command("product.save", new() { ["id"] = product.Id.ToString(), ["categoryId"] = product.CategoryId.ToString(), ["sku"] = null, ["name"] = "Concha", ["description"] = "", ["priceCents"] = 2400, ["trackInventory"] = true }));
        transport.Sent.Clear();
        var status = await engine.RunOnceAsync();
        Assert.AreEqual(SyncState.UpToDate, status.State);
        Assert.AreEqual("applied", transport.Reported.Single().Status, transport.Reported.Single().Message);
        Assert.AreEqual(2400L, (long)transport.Sent.Last(e => e.Entry.Entity == "product").Payload["product"]!["price_cents"]!, "The new price goes back up to the cloud in the same run.");
        Assert.AreEqual(0L, outbox.PendingCount());

        await engine.RunOnceAsync();
        Assert.HasCount(1, transport.Reported, "Nothing is applied or reported twice.");
    }

    private sealed class Handler(string body) : HttpMessageHandler
    {
        public List<(string Method, string Url, string? Authorization, string? Body)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), request.Content is null ? null : await request.Content.ReadAsStringAsync(cancel)));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    [TestMethod]
    public async Task HttpTransportReadsCommandsAndReportsResults()
    {
        var id = Guid.NewGuid(); var by = Guid.NewGuid();
        var handler = new Handler($$"""{"commands":[{"id":"{{id}}","seq":7,"kind":"product.active","payload":{"id":"{{product.Id}}","active":false},"requestedBy":{"id":"{{by}}","name":"Ana"},"requestedAt":"2026-10-07T12:00:00Z"}]}""");
        var transport = new HttpSyncTransport(new HttpClient(handler));
        var key = new string('a', 43);
        var pulled = await transport.PullCommandsAsync(new Uri("https://sync.example.test/"), key, 6, default);
        var command = pulled.Single();
        Assert.AreEqual((id, 7L, "product.active", by, "Ana"), (command.Id, command.Sequence, command.Kind, command.RequestedById, command.RequestedByName));
        Assert.AreEqual("https://sync.example.test/api/sync/commands?after=6", handler.Requests[0].Url);
        Assert.AreEqual("Bearer " + key, handler.Requests[0].Authorization);
        Ok(Commands.Apply(command));
        Assert.IsFalse(Product(product.Id).IsActive, "A command parsed from the network applies like one built in memory.");

        await transport.ReportCommandsAsync(new Uri("https://sync.example.test/"), key, [new(id, 7, "rejected", "El SKU ya existe.")], default);
        var report = JsonNode.Parse(handler.Requests[1].Body!)!["results"]![0]!;
        Assert.AreEqual(id.ToString(), (string?)report["id"]);
        Assert.AreEqual("rejected", (string?)report["status"]);
        Assert.AreEqual("El SKU ya existe.", (string?)report["message"]);
    }
}
