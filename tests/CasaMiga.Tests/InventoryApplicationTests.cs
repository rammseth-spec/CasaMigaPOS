using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class InventoryApplicationTests
{
    private string root = null!;
    private Database database = null!;
    private SqliteLocalUnitOfWork work = null!;
    private SqliteInventoryStore store = null!;
    private InventoryService service = null!;
    private UserSession session = null!;
    private Guid actor;
    private TerminalIdentity terminal = null!;
    private readonly TestClock clock = new();

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 12, 14, 0, 0, TimeSpan.Zero);
    }

    private sealed class InterleavingStore(IInventoryStore inner, Action interleave) : IInventoryStore
    {
        private bool pending = true;
        public InventoryItem? FindItem(Guid id) => inner.FindItem(id);
        public InventoryItemUnit? FindItemUnit(Guid itemId, string code) => inner.FindItemUnit(itemId, code);
        public InventoryLocationView? FindLocation(Guid id) => inner.FindLocation(id);
        public InventoryBalanceRecord? FindBalance(Guid itemId, Guid locationId) => inner.FindBalance(itemId, locationId);
        public PersistedInventoryOperation Apply(InventoryOperationRequest request, InventoryBalanceExpectation? expectation = null)
        {
            if (expectation is not null && pending) { pending = false; interleave(); }
            return inner.Apply(request, expectation);
        }
        public PersistedInventoryOperation? FindOperation(Guid operationId) => inner.FindOperation(operationId);
        public PersistedInventoryOperation? Recover(InventoryOperationRequest request) => inner.Recover(request);
        public IReadOnlyList<InventoryReconciliationDifference> Reconcile() => inner.Reconcile();
        public void CreateItem(InventoryItem item, AuditEntry audit) => inner.CreateItem(item, audit);
        public void UpdateItem(InventoryItem item, AuditEntry audit) => inner.UpdateItem(item, audit);
        public void AddItemUnit(InventoryItemUnit unit, AuditEntry audit) => inner.AddItemUnit(unit, audit);
        public InventoryProductLinkView? FindActiveProductLink(Guid productId) => inner.FindActiveProductLink(productId);
        public void LinkProduct(ProductInventoryLink link, AuditEntry audit) => inner.LinkProduct(link, audit);
        public void UnlinkProduct(Guid productId, AuditEntry audit) => inner.UnlinkProduct(productId, audit);
        public InventoryListPage QueryItems(InventoryListQuery query) => inner.QueryItems(query);
        public InventoryItemDetail? FindItemDetail(Guid id) => inner.FindItemDetail(id);
        public InventoryMovementPage QueryMovements(InventoryMovementQuery query) => inner.QueryMovements(query);
        public IReadOnlyList<InventoryReconciliationStatus> QueryReconciliation() => inner.QueryReconciliation();
    }

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "CasaMigaInventoryApplication", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        database = new(Path.Combine(root, "casamiga.db"));
        database.Initialize();
        work = new(database);
        session = new();
        var identity = new IdentityService(work, new LocalPasswords(), clock, session);
        var password = Guid.NewGuid().ToString("N");
        identity.Bootstrap("admin", "Inventario", password);
        Assert.IsTrue(identity.Login("admin", password));
        actor = session.Current!.Id;
        terminal = TerminalConfiguration.LoadOrCreate(root);
        work.Run(local => { local.EnsureTerminal(terminal, clock.UtcNow); return true; });
        store = new(database, clock);
        service = new(store, work, session, clock, terminal.Id);
    }

    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    private object? Sql(string sql)
    {
        using var connection = database.Open();
        using var command = new SqliteCommand(sql, connection);
        return command.ExecuteScalar();
    }

    private void Role(string role) => Sql($"UPDATE app_user SET role_name='{role}' WHERE id='{actor:D}'");
    private InventoryItem Item(string code = "FLOUR", string name = "Harina") =>
        service.CreateInventoryItem(code, name, InventoryItemKind.RawMaterial, InventoryDimension.Mass);
    private PersistedInventoryOperation Open(InventoryItem item, decimal amount = 0m, Guid? operationId = null) =>
        service.RegisterOpeningBalance(new(operationId ?? Guid.NewGuid(), item.Id,
            InventoryLocationIds.Main, amount, "g"));

    [TestMethod]
    public void OpeningSupportsZeroAndRetryButRejectsSecondInitializationAndManager()
    {
        var item = Item(); var operationId = Guid.NewGuid();
        var first = Open(item, 0m, operationId);
        var retry = Open(item, 0m, operationId);
        Assert.AreEqual(first.OperationId, retry.OperationId);
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_operation"));
        Assert.AreEqual(0L, store.FindBalance(item.Id, InventoryLocationIds.Main)!.QuantityAtomic);
        var second = Assert.ThrowsExactly<InventoryApplicationException>(() => Open(item, 1m));
        Assert.AreEqual(InventoryError.AlreadyInitialized, second.Error);
        var sugar = Item("SUGAR");
        Role("Manager");
        var denied = Assert.ThrowsExactly<InventoryApplicationException>(() => Open(sugar));
        Assert.AreEqual(InventoryError.Unauthorized, denied.Error);
    }

    [TestMethod]
    public void ReceiptPreservesOptionalNotePositiveIntentAndIdempotencyConflict()
    {
        var item = Item(); Open(item);
        var operationId = Guid.NewGuid();
        var command = new ReceiveInventoryCommand(operationId, item.Id,
            InventoryLocationIds.Main, 2.5m, "kg", "REM-1", " Entrega parcial ");
        var result = service.ReceiveInventory(command);
        Assert.AreEqual("Entrega parcial", result.Note);
        Assert.AreEqual("REM-1", result.ExternalReference);
        Assert.AreEqual(2_500_000L, result.Movements.Single().DeltaAtomic);
        var retry = service.ReceiveInventory(command);
        Assert.AreEqual(result.OperationId, retry.OperationId);
        Assert.AreEqual(result.RequestSha256, retry.RequestSha256);
        Assert.AreEqual(result.Movements.Single().Id, retry.Movements.Single().Id);
        var conflict = Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(command with { Quantity = 3m }));
        Assert.AreEqual(InventoryError.IdempotencyConflict, conflict.Error);
        Assert.AreEqual(InventoryError.InvalidQuantity,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(command with { OperationId = Guid.NewGuid(), Quantity = 0 })).Error);
        Assert.AreEqual(InventoryError.InvalidQuantity,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(command with { OperationId = Guid.NewGuid(), Quantity = -1 })).Error);
        Assert.AreEqual(InventoryError.InvalidNote,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(command with { OperationId = Guid.NewGuid(), Note = " " })).Error);
        var unopened = Item("UNOPENED");
        Assert.AreEqual(InventoryError.NotInitialized,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(
                new(Guid.NewGuid(), unopened.Id, InventoryLocationIds.Main, 1m, "g"))).Error);
    }

    [TestMethod]
    public void ManagerCanReceiveAndWasteButCashierAndRevokedOrInactiveUsersAreRejected()
    {
        var item = Item(); Open(item);
        Role("Manager");
        service.ReceiveInventory(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 3m, "g"));
        service.RecordWaste(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 1m, "g", WasteReason.Damaged));
        Assert.AreEqual(2_000L, store.FindBalance(item.Id, InventoryLocationIds.Main)!.QuantityAtomic);
        Assert.AreEqual(InventoryError.Unauthorized,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.AdjustToPhysicalCount(
                new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 2m, "g", AdjustmentReason.DataCorrection))).Error);
        Role("Cashier");
        Assert.AreEqual(InventoryError.Unauthorized,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.List(new())).Error);
        Role("Manager"); Sql("DELETE FROM role_permission WHERE role_name='Manager' AND permission_name='inventory.receive'");
        Assert.AreEqual(InventoryError.Unauthorized,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(
                new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 1m, "g"))).Error);
        Sql($"UPDATE app_user SET active=0 WHERE id='{actor:D}'");
        Assert.AreEqual(InventoryError.InactiveUser,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.MovementHistory(new())).Error);
    }

    [TestMethod]
    public void WasteUsesPositiveInputNegativeLedgerReasonsAndStockProtection()
    {
        var item = Item(); Open(item, 5m);
        var waste = service.RecordWaste(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main,
            2m, "g", WasteReason.Other, " Derrame de prueba "));
        Assert.AreEqual(-2_000L, waste.Movements.Single().DeltaAtomic);
        Assert.AreEqual("OTHER", waste.ReasonCode); Assert.AreEqual("Derrame de prueba", waste.Note);
        Assert.AreEqual(InventoryError.InvalidNote,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.RecordWaste(
                new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 1m, "g", WasteReason.Other))).Error);
        Assert.AreEqual(InventoryError.InsufficientStock,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.RecordWaste(
                new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 4m, "g", WasteReason.Spill))).Error);
    }

    [TestMethod]
    public void PhysicalCountCreatesGainLossAndNoChangeWithoutZeroMovement()
    {
        var item = Item(); Open(item, 10m);
        var gainCommand = new AdjustToPhysicalCountCommand(Guid.NewGuid(), item.Id, InventoryLocationIds.Main,
            12m, "g", AdjustmentReason.PhysicalCountVariance);
        var gain = service.AdjustToPhysicalCount(gainCommand);
        Assert.IsTrue(gain.Changed); Assert.AreEqual(InventoryMovementKind.AdjustmentGain, gain.Operation!.Kind);
        Assert.AreEqual(2_000L, gain.Operation.Movements.Single().DeltaAtomic);
        var gainRetry = service.AdjustToPhysicalCount(gainCommand);
        Assert.AreEqual(gain.Operation.OperationId, gainRetry.Operation!.OperationId);
        Assert.AreEqual(gain.Operation.Movements.Single().Id, gainRetry.Operation.Movements.Single().Id);
        Assert.AreEqual(InventoryError.IdempotencyConflict,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.AdjustToPhysicalCount(
                gainCommand with { CountedQuantity = 13m })).Error);
        var loss = service.AdjustToPhysicalCount(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main,
            7m, "g", AdjustmentReason.DataCorrection));
        Assert.AreEqual(InventoryMovementKind.AdjustmentLoss, loss.Operation!.Kind);
        Assert.AreEqual(-5_000L, loss.Operation.Movements.Single().DeltaAtomic);
        var count = Convert.ToInt64(Sql("SELECT count(*) FROM inventory_operation"));
        var auditCount = Convert.ToInt64(Sql("SELECT count(*) FROM audit_entry"));
        var noChange = service.AdjustToPhysicalCount(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main,
            7m, "g", AdjustmentReason.Other, "Conteo verificado"));
        Assert.IsFalse(noChange.Changed); Assert.AreEqual(InventoryAdjustmentOutcome.NoChange, noChange.Outcome); Assert.IsNull(noChange.Operation);
        Assert.AreEqual(count, Sql("SELECT count(*) FROM inventory_operation"));
        Assert.AreEqual(auditCount, Sql("SELECT count(*) FROM audit_entry"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_movement WHERE delta_atomic=0"));
    }

    [TestMethod]
    public void PhysicalCountRejectsAStaleBalanceVersionWithoutApplyingItsDelta()
    {
        var item = Item(); Open(item, 10m);
        var receiptCapture = InventoryCapture.Standard(1m, InventoryUnits.Gram);
        var receipt = new InventoryOperationRequest(Guid.NewGuid(), actor, terminal.Id, InventoryMovementKind.Receipt,
            [InventoryOperationLine.Change(item.Id, InventoryLocationIds.Main,
                InventoryDelta.Increase(receiptCapture.Quantity), receiptCapture)]);
        var interleaving = new InterleavingStore(store, () => store.Apply(receipt));
        var concurrentService = new InventoryService(interleaving, work, session, clock, terminal.Id);
        var error = Assert.ThrowsExactly<InventoryApplicationException>(() => concurrentService.AdjustToPhysicalCount(
            new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 12m, "g", AdjustmentReason.PhysicalCountVariance)));
        Assert.AreEqual(InventoryError.ConcurrencyConflict, error.Error);
        Assert.AreEqual(11_000L, store.FindBalance(item.Id, InventoryLocationIds.Main)!.QuantityAtomic);
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_operation WHERE kind LIKE 'ADJUSTMENT_%'"));
        Assert.IsEmpty(store.Reconcile());
    }

    [TestMethod]
    public void ConfigurationIsAuditedAtomicAndPreservesDimensionAndStockRules()
    {
        var item = Item(" flour ", " Harina ");
        Assert.AreEqual("FLOUR", item.Code);
        var updated = service.UpdateInventoryItemMetadata(item.Id, "Harina de trigo", InventoryItemKind.Supply, InventoryDimension.Mass);
        Assert.AreEqual(InventoryItemKind.Supply, updated.Kind);
        Assert.AreEqual(InventoryError.DimensionMismatch,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.UpdateInventoryItemMetadata(item.Id,
                updated.Name, updated.Kind, InventoryDimension.Volume)).Error);
        var unit = service.AddInventoryItemUnit(item.Id, "SACK25", "Saco 25 kg", 25_000_000);
        Assert.AreEqual(50_000_000L, unit.ConvertWholeUnits(2).AtomicUnits);
        Open(updated, 0m); Assert.IsFalse(service.SetInventoryItemActive(item.Id, false).IsActive);
        Assert.IsTrue(service.SetInventoryItemActive(item.Id, true).IsActive);
        service.ReceiveInventory(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 1m, "g"));
        Assert.AreEqual(InventoryError.InsufficientStock,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.SetInventoryItemActive(item.Id, false)).Error);

        Sql("CREATE TRIGGER fail_inventory_config_audit BEFORE INSERT ON audit_entry WHEN NEW.action='inventory.item.created' BEGIN SELECT RAISE(ABORT,'fault'); END");
        Assert.ThrowsExactly<InventoryApplicationException>(() => service.CreateInventoryItem("ROLLBACK", "Rollback",
            InventoryItemKind.Supply, InventoryDimension.Count));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_item WHERE code='ROLLBACK'"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM audit_entry WHERE action='inventory.item.created'"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM audit_entry WHERE action='inventory.item.updated'"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM audit_entry WHERE action='inventory.item.deactivated'"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM audit_entry WHERE action='inventory.item.activated'"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM audit_entry WHERE action='inventory.unit.added'"));
        Role("Manager");
        Assert.AreEqual(InventoryError.Unauthorized,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.CreateInventoryItem("DENIED", "Denegado",
                InventoryItemKind.Supply, InventoryDimension.Count)).Error);
    }

    [TestMethod]
    public void CaptureValidatesDimensionExactConversionAndWholePresentations()
    {
        var item = Item(); Open(item);
        service.AddInventoryItemUnit(item.Id, "SACK", "Saco", 25_000_000);
        var receipt = service.ReceiveInventory(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 2m, "SACK"));
        Assert.AreEqual(50_000_000L, receipt.Movements.Single().DeltaAtomic);
        Assert.AreEqual(InventoryError.InvalidQuantity,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(
                new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 0.5m, "SACK"))).Error);
        Assert.AreEqual(InventoryError.DimensionMismatch,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(
                new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 1m, "L"))).Error);
        Assert.AreEqual(InventoryError.InvalidUnit,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(
                new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 1m, "UNKNOWN"))).Error);
        Assert.AreEqual(InventoryError.InvalidQuantity,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.ReceiveInventory(
                new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 0.0001m, "g"))).Error);
    }

    [TestMethod]
    public void ProductLinkIsExplicitAuditedAndDoesNotAffectCheckout()
    {
        var item = Item();
        var productId = Guid.NewGuid();
        work.Run(local =>
        {
            var category = new Category(Guid.NewGuid(), "Pan", "", 0, true, clock.UtcNow, clock.UtcNow);
            local.SaveCategory(category, true);
            local.SaveProduct(new(productId, category.Id, null, "Concha", "", new Money(1900), true, true,
                clock.UtcNow, clock.UtcNow), true);
            return true;
        });
        Assert.IsNull(store.FindActiveProductLink(productId));
        var link = service.LinkProductToInventoryItem(productId, item.Id, 100);
        Assert.AreEqual(item.Id, link.InventoryItemId);
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_movement"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale"));
        Assert.AreEqual(InventoryError.Conflict,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.LinkProductToInventoryItem(productId, item.Id, 100)).Error);
        service.UnlinkProductFromInventoryItem(productId);
        Assert.IsNull(store.FindActiveProductLink(productId));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM audit_entry WHERE action='inventory.product_link.created'"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM audit_entry WHERE action='inventory.product_link.removed'"));
        Assert.AreEqual(InventoryError.InvalidQuantity,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.LinkProductToInventoryItem(productId, item.Id, 0)).Error);
        Assert.AreEqual(InventoryError.ProductNotFound,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.LinkProductToInventoryItem(Guid.NewGuid(), item.Id, 1)).Error);
    }

    [TestMethod]
    public void ReadModelsFilterPageOrderHistoryDetailAndReconciliationWithoutWrites()
    {
        var zeta = Item("ZETA", "Zeta"); var alpha = Item("ALPHA", "Álpha"); var beta = Item("BETA", "Beta");
        Open(alpha); clock.UtcNow = clock.UtcNow.AddMinutes(1); service.ReceiveInventory(new(Guid.NewGuid(), alpha.Id, InventoryLocationIds.Main,
            1m, "kg", "R-1", "Completa"));
        var audits = Sql("SELECT count(*) FROM audit_entry");
        var first = service.List(new(Active: true, Page: 0, PageSize: 2));
        var second = service.List(new(Active: true, Page: 1, PageSize: 2));
        Assert.AreEqual(3, first.Total); Assert.HasCount(2, first.Items); Assert.HasCount(1, second.Items);
        var repeated = service.List(new(Active: true, Page: 0, PageSize: 2));
        CollectionAssert.AreEqual(first.Items.Select(x => x.InventoryItemId).ToArray(), repeated.Items.Select(x => x.InventoryItemId).ToArray());
        Assert.IsEmpty(first.Items.Select(x => x.InventoryItemId).Intersect(second.Items.Select(x => x.InventoryItemId)));
        Assert.AreEqual(beta.Id, service.List(new(Search: "bet", Active: null)).Items.Single().InventoryItemId);
        Assert.AreEqual(3, service.List(new(Kind: InventoryItemKind.RawMaterial, Active: null)).Total);
        Assert.AreEqual(0, service.List(new(Search: "inexistente", Active: null)).Total);
        Assert.AreEqual(1_000_000L, service.Detail(alpha.Id).Balance!.QuantityAtomic);
        Assert.IsEmpty(service.Detail(zeta.Id).ProductLinks);
        var history = service.MovementHistory(new(alpha.Id, 0, 1));
        Assert.AreEqual(2, history.Total); Assert.HasCount(1, history.Items);
        Assert.AreEqual(InventoryMovementKind.Receipt, history.Items[0].Kind);
        Assert.AreEqual("Completa", history.Items[0].Note);
        Assert.IsTrue(service.ReconciliationStatus().All(x => x.Consistent));
        Sql($"UPDATE inventory_balance SET quantity_atomic=quantity_atomic+1 WHERE item_id='{alpha.Id:D}'");
        var inconsistent = service.ReconciliationStatus().Single(x => x.InventoryItemId == alpha.Id);
        Assert.IsFalse(inconsistent.Consistent);
        Assert.AreEqual(inconsistent.ReconstructedAtomic + 1, inconsistent.MaterializedAtomic);
        Assert.AreEqual(InventoryError.InconsistentBalance,
            Assert.ThrowsExactly<InventoryApplicationException>(() => service.AdjustToPhysicalCount(
                new(Guid.NewGuid(), alpha.Id, InventoryLocationIds.Main, 1m, "kg", AdjustmentReason.DataCorrection))).Error);
        Assert.AreEqual(audits, Sql("SELECT count(*) FROM audit_entry"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM payment"));
    }
}
