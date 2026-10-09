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

        // Desactivado aquí después de pedir el cambio: se rechaza, nunca se aplica como administrador.
        identity.SetActive(ana, false);
        var inactive = Commands.Apply(Command("category.save", new() { ["id"] = Guid.NewGuid().ToString(), ["name"] = "Sin permiso", ["description"] = "", ["sortOrder"] = 3 }, ana));
        Assert.AreEqual("rejected", inactive.Status);
        Assert.AreEqual(0L, Convert.ToInt64(Sql("SELECT count(*) FROM category WHERE name='Sin permiso'")));

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

    [TestMethod]
    public void RouteFromThePhoneKeepsTheTimeItHappened()
    {
        var trays = new ConsignmentService(work, user, clock, terminal.Id, TimeZoneInfo.Utc);
        trays.SetPrice(product.Id, new Money(1500));
        var store = trays.SaveStore(null, "Abarrotes Lupita", "", "", "");
        var onRoute = DateTimeOffset.UtcNow.AddHours(-3);
        CloudCommand Route(string kind, JsonObject payload, DateTimeOffset at) => new(Guid.NewGuid(), ++sequence, kind, payload, user.Current!.Id, "Admin en ruta", at);

        var deliver = Route("consignment.deliver", new() { ["storeId"] = store.Id.ToString(), ["lines"] = new JsonArray(new JsonObject { ["productId"] = product.Id.ToString(), ["quantity"] = 6 }), ["note"] = "" }, onRoute);
        Ok(Commands.Apply(deliver));
        var delivery = trays.Route().Single(s => s.Store.Id == store.Id).AtStore.Single();
        Assert.AreEqual(deliver.Id, delivery.OperationId, "The web change id is the operation id: a retry never duplicates the tray.");
        Assert.IsTrue(Math.Abs((delivery.DeliveredAt - onRoute).TotalSeconds) < 1, "Recorded at the time it happened on the route, not when the till applied it.");

        Ok(Commands.Apply(Route("consignment.collect", new() { ["deliveryId"] = delivery.Id.ToString(), ["returned"] = new JsonArray(new JsonObject { ["position"] = 1, ["quantity"] = 2 }),
            ["paidCents"] = 3000, ["method"] = "CASH", ["reference"] = null, ["note"] = "" }, onRoute.AddHours(1))));
        Assert.AreEqual(3000L, trays.Account(store.Id).Balance.Cents, "4 sold × $15 = $60 charged, $30 paid.");
        Ok(Commands.Apply(Route("consignment.payment", new() { ["storeId"] = store.Id.ToString(), ["amountCents"] = 3000, ["method"] = "TRANSFER", ["reference"] = "SPEI 1", ["note"] = "" }, onRoute.AddHours(2))));
        Assert.AreEqual(0L, trays.Account(store.Id).Balance.Cents);
        var tooMuch = Commands.Apply(Route("consignment.payment", new() { ["storeId"] = store.Id.ToString(), ["amountCents"] = 100, ["method"] = "CASH", ["note"] = "" }, onRoute));
        Assert.AreEqual("rejected", tooMuch.Status, "The till still applies its own rules (no balance left).");

        var second = Route("consignment.deliver", new() { ["storeId"] = store.Id.ToString(), ["lines"] = new JsonArray(new JsonObject { ["productId"] = product.Id.ToString(), ["quantity"] = 3 }), ["note"] = "" }, DateTimeOffset.UtcNow.AddDays(1));
        Ok(Commands.Apply(second));
        var future = trays.Route().Single(s => s.Store.Id == store.Id).AtStore.Single();
        Assert.IsTrue(future.DeliveredAt <= DateTimeOffset.UtcNow, "A time in the future is never recorded.");
        Ok(Commands.Apply(Route("consignment.void", new() { ["deliveryId"] = future.Id.ToString(), ["reason"] = "Tienda cerrada" }, DateTimeOffset.UtcNow)));
        Assert.IsEmpty(trays.Route().Single(s => s.Store.Id == store.Id).AtStore);
    }

    [TestMethod]
    public void IngredientCostFromTheWebTravelsBackWithTheItem()
    {
        var item = Guid.NewGuid();
        Sql($"INSERT INTO inventory_item(id,code,name,kind,dimension,active,created_utc,modified_utc) VALUES('{item}','HAR-1','Harina','RAW_MATERIAL','MASS',1,'2026-10-01T00:00:00Z','2026-10-01T00:00:00Z')");
        Ok(Commands.Apply(Command("inventory.cost", new() { ["itemId"] = item.ToString(), ["costCents"] = 2850, ["perAtomic"] = 1_000_000, ["unitLabel"] = "kg" })));
        Assert.AreEqual(2850L, Convert.ToInt64(Sql($"SELECT cost_cents FROM inventory_item_cost WHERE item_id='{item}'")));
        Assert.AreEqual(1L, Convert.ToInt64(Sql($"SELECT count(*) FROM audit_entry WHERE action='inventory.cost.set' AND entity_id='{item}'")));
        Ok(Commands.Apply(Command("inventory.cost", new() { ["itemId"] = item.ToString(), ["costCents"] = 3000, ["perAtomic"] = 1_000_000, ["unitLabel"] = "kg" })));
        var payload = new SqliteSyncOutbox(db).PendingEnvelopes(500, int.MaxValue).Last(e => e.Entry.Entity == "inventory_item" && e.Entry.EntityId == item.ToString()).Payload;
        Assert.AreEqual(3000L, (long)payload["cost"]!["cost_cents"]!, "The latest cost goes up with the item.");
        Assert.AreEqual("rejected", Commands.Apply(Command("inventory.cost", new() { ["itemId"] = Guid.NewGuid().ToString(), ["costCents"] = 1, ["perAtomic"] = 1, ["unitLabel"] = "kg" })).Status);
        Assert.AreEqual("rejected", Commands.Apply(Command("inventory.cost", new() { ["itemId"] = item.ToString(), ["costCents"] = -1, ["perAtomic"] = 1, ["unitLabel"] = "kg" })).Status);
    }

    private CloudCommandService WithModules => new(work, clock, terminal.Id, TimeZoneInfo.Utc,
        new CloudCommandModules(new SqliteInventoryStore(db, clock), work, new SqliteProductionUnitOfWork(db), _ => throw new NotSupportedException()));
    private long Balance(Guid item) => Convert.ToInt64(Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{item}'"));

    [TestMethod]
    public void InventoryFromTheWebUsesTheSameRulesAsTheScreen()
    {
        var inventory = new InventoryService(new SqliteInventoryStore(db, clock), work, user, clock, terminal.Id);
        var flour = inventory.CreateInventoryItem("HAR", "Harina", InventoryItemKind.RawMaterial, InventoryDimension.Mass).Id;
        var main = SqliteInventoryStore.MainLocationId.ToString();
        JsonObject Line(string quantity, string unit, string? reason = null, string? note = null) =>
            new() { ["itemId"] = flour.ToString(), ["locationId"] = main, ["quantity"] = quantity, ["unitCode"] = unit, ["reason"] = reason, ["note"] = note };

        var refused = WithModules.Apply(Command("inventory.receive", Line("2", "kg")));
        StringAssert.Contains(refused.Message, "no tiene saldo inicial", "Receiving needs a starting count first.");
        var opening = Command("inventory.count", Line("5", "kg", "PhysicalCountVariance"));
        Ok(WithModules.Apply(opening));
        Assert.AreEqual(5_000_000L, Balance(flour), "The first count is the opening balance.");
        Assert.AreEqual(1L, Convert.ToInt64(Sql($"SELECT count(*) FROM inventory_operation WHERE id='{opening.Id}' AND kind='OPENING_BALANCE'")));

        Ok(WithModules.Apply(Command("inventory.receive", new() { ["itemId"] = flour.ToString(), ["locationId"] = main, ["quantity"] = "2.5", ["unitCode"] = "kg", ["reference"] = "Factura 18", ["note"] = "Proveedor Rosa" })));
        Assert.AreEqual(7_500_000L, Balance(flour));
        var waste = Command("inventory.waste", Line("500", "g", "Damaged"));
        Ok(WithModules.Apply(waste));
        Ok(WithModules.Apply(waste));
        Assert.AreEqual(7_000_000L, Balance(flour), "Applying the same waste twice discounts it once.");
        Ok(WithModules.Apply(Command("inventory.count", Line("6.8", "kg", "PhysicalCountVariance"))));
        Assert.AreEqual(6_800_000L, Balance(flour));
        Assert.AreEqual(1L, Convert.ToInt64(Sql("SELECT count(*) FROM inventory_operation WHERE kind='ADJUSTMENT_LOSS'")));

        Assert.AreEqual("El motivo OTHER exige una observación.", WithModules.Apply(Command("inventory.waste", Line("1", "kg", "Other"))).Message);
        Assert.AreEqual("rejected", WithModules.Apply(Command("inventory.waste", Line("1", "piece", "Damaged"))).Status, "A piece is not a mass.");
        Assert.AreEqual("Cambio con formato inválido.", WithModules.Apply(Command("inventory.waste", Line("1", "kg", "1"))).Message);
        Assert.AreEqual("Cambio con formato inválido.", WithModules.Apply(Command("inventory.receive", Line("-1", "kg"))).Message);
        Assert.AreEqual(6_800_000L, Balance(flour));

        var old = Commands.Apply(Command("inventory.receive", Line("1", "kg")));
        Assert.AreEqual("Esta caja no puede aplicar ese cambio; actualiza Casa Miga.", old.Message, "Without the modules the till refuses instead of guessing.");
    }

    [TestMethod]
    public void OnlyTheAdministratorCountsInventoryFromTheWeb()
    {
        var inventory = new InventoryService(new SqliteInventoryStore(db, clock), work, user, clock, terminal.Id);
        var flour = inventory.CreateInventoryItem("HAR", "Harina", InventoryItemKind.RawMaterial, InventoryDimension.Mass).Id;
        inventory.RegisterOpeningBalance(new(Guid.NewGuid(), flour, SqliteInventoryStore.MainLocationId, 5, "kg"));
        var identity = new IdentityService(work, new LocalPasswords(), clock, user);
        var temporary = identity.CreateUser("ana", "Ana", "Manager");
        identity.ChangePassword("ana", temporary, "Ana#2026centro");
        Assert.IsTrue(identity.Login("admin", password));
        var ana = work.Run(s => s.FindUser("ANA"))!.Id;
        JsonObject Line(string quantity) => new() { ["itemId"] = flour.ToString(), ["locationId"] = SqliteInventoryStore.MainLocationId.ToString(), ["quantity"] = quantity, ["unitCode"] = "kg", ["reason"] = "PhysicalCountVariance" };

        Assert.AreEqual("rejected", WithModules.Apply(Command("inventory.count", Line("4"), ana)).Status, "A manager receives and records waste, but does not adjust.");
        Ok(WithModules.Apply(Command("inventory.receive", new() { ["itemId"] = flour.ToString(), ["locationId"] = SqliteInventoryStore.MainLocationId.ToString(), ["quantity"] = "1", ["unitCode"] = "kg" }, ana)));
        Assert.AreEqual(6_000_000L, Balance(flour));
        Assert.AreEqual(ana.ToString(), (string)Sql("SELECT actor_id FROM inventory_operation WHERE kind='RECEIPT'")!, "The operation is signed by who asked for it.");
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
    public async Task ChangesThatBecomeVisibleOutOfOrderAreNotSkipped()
    {
        var transport = new Transport();
        var engine = new SyncEngine(new SqliteSyncOutbox(db), transport, directory, commands: Commands);
        await engine.RegisterAsync("https://sync.example.test/", "secreto-correcto", "Centro", terminal.Id, terminal.Name);
        CloudCommand Price(long seq, long cents) => new(Guid.NewGuid(), seq, "product.save",
            new() { ["id"] = product.Id.ToString(), ["categoryId"] = product.CategoryId.ToString(), ["sku"] = null, ["name"] = "Concha", ["description"] = "", ["priceCents"] = cents, ["trackInventory"] = true },
            user.Current!.Id, "Web");
        // Dos guardados simultáneos en la web: el 42 se ve antes que el 41.
        transport.Pending.Add(Price(42, 2600));
        await engine.RunOnceAsync();
        transport.Pending.Add(Price(41, 2500));
        await engine.RunOnceAsync();
        CollectionAssert.AreEqual(new long[] { 42, 41 }, transport.Reported.Select(r => r.Sequence).ToArray(), "41 is applied late; 42 is not applied or reported twice.");
        Assert.AreEqual(2L, Convert.ToInt64(Sql("SELECT count(*) FROM cloud_command")));
        Assert.AreEqual(42L, Commands.LastSequence());
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
