using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class InventoryViewModelTests
{
    private string root = null!;
    private Database database = null!;
    private SqliteLocalUnitOfWork work = null!;
    private SqliteInventoryStore store = null!;
    private InventoryService service = null!;
    private CatalogService catalog = null!;
    private UserSession session = null!;
    private Guid actor;
    private TerminalIdentity terminal = null!;
    private readonly TestClock clock = new();

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 12, 18, 0, 0, TimeSpan.Zero);
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

    private sealed class FaultingStore(IInventoryStore inner) : IInventoryStore
    {
        public const string PrivateDiagnostic = "private database path and captured customer note";
        public bool FailDetail { get; set; }
        public bool FailDetailAfterAdd { get; set; }
        public bool FailAddBeforeCommit { get; set; }
        public bool FailAddAfterCommit { get; set; }
        public int AddCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public int QueryCalls { get; private set; }
        public Action<InventoryListQuery>? BeforeQuery { get; set; }
        public InventoryItem? FindItem(Guid id) => inner.FindItem(id);
        public InventoryItemUnit? FindItemUnit(Guid itemId, string code) => inner.FindItemUnit(itemId, code);
        public InventoryLocationView? FindLocation(Guid id) => inner.FindLocation(id);
        public InventoryBalanceRecord? FindBalance(Guid itemId, Guid locationId) => inner.FindBalance(itemId, locationId);
        public PersistedInventoryOperation Apply(InventoryOperationRequest request, InventoryBalanceExpectation? expectation = null) => inner.Apply(request, expectation);
        public PersistedInventoryOperation? FindOperation(Guid operationId) => inner.FindOperation(operationId);
        public PersistedInventoryOperation? Recover(InventoryOperationRequest request) => inner.Recover(request);
        public IReadOnlyList<InventoryReconciliationDifference> Reconcile() => inner.Reconcile();
        public void CreateItem(InventoryItem item, AuditEntry audit) => inner.CreateItem(item, audit);
        public void UpdateItem(InventoryItem item, AuditEntry audit) => inner.UpdateItem(item, audit);
        public void AddItemUnit(InventoryItemUnit unit, AuditEntry audit)
        {
            AddCalls++;
            if (FailAddBeforeCommit) throw new IOException(PrivateDiagnostic);
            inner.AddItemUnit(unit, audit);
            if (FailDetailAfterAdd) FailDetail = true;
            if (FailAddAfterCommit) throw new IOException(PrivateDiagnostic);
        }
        public InventoryProductLinkView? FindActiveProductLink(Guid productId) => inner.FindActiveProductLink(productId);
        public void LinkProduct(ProductInventoryLink link, AuditEntry audit) => inner.LinkProduct(link, audit);
        public void UnlinkProduct(Guid productId, AuditEntry audit) => inner.UnlinkProduct(productId, audit);
        public InventoryListPage QueryItems(InventoryListQuery query) { QueryCalls++; BeforeQuery?.Invoke(query); return inner.QueryItems(query); }
        public InventoryItemDetail? FindItemDetail(Guid id)
        {
            DetailCalls++;
            if (FailDetail) throw new IOException(PrivateDiagnostic);
            return inner.FindItemDetail(id);
        }
        public InventoryMovementPage QueryMovements(InventoryMovementQuery query) => inner.QueryMovements(query);
        public IReadOnlyList<InventoryReconciliationStatus> QueryReconciliation() => inner.QueryReconciliation();
    }

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "CasaMigaInventoryViewModel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        database = new(Path.Combine(root, "casamiga.db")); database.Initialize(); work = new(database); session = new();
        var identity = new IdentityService(work, new LocalPasswords(), clock, session);
        var password = "Aa1!"+Guid.NewGuid().ToString("N")[..12]; identity.Bootstrap("admin", "Inventario UI", password);
        Assert.IsTrue(identity.Login("admin", password)); actor = session.Current!.Id;
        terminal = TerminalConfiguration.LoadOrCreate(root);
        work.Run(local => { local.EnsureTerminal(terminal, clock.UtcNow); return true; });
        store = new(database, clock); service = new(store, work, session, clock, terminal.Id);
        catalog = new(work, session, clock, terminal.Id, true);
    }

    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);
    private InventoryViewModel ViewModel(InventoryService? inventory = null) =>
        new(inventory ?? service, catalog, (_, _) => { });
    private object? Sql(string sql)
    {
        using var connection = database.Open(); using var command = new SqliteCommand(sql, connection);
        return command.ExecuteScalar();
    }
    private void Role(string role) => Sql($"UPDATE app_user SET role_name='{role}' WHERE id='{actor:D}'");
    private InventoryItem Item(string code = "FLOUR", string name = "Harina") =>
        service.CreateInventoryItem(code, name, InventoryItemKind.RawMaterial, InventoryDimension.Mass);
    private void Open(InventoryItem item, decimal quantity = 0m) => service.RegisterOpeningBalance(
        new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, quantity, "g"));
    private static void Select(InventoryViewModel vm, InventoryItem item)
    {
        vm.SelectedItem = vm.Items.Single(x => x.Id == item.Id);
    }

    private InventoryViewModel FaultingViewModel(FaultingStore inventory, List<(string Event, string? Detail)> log) =>
        new(new InventoryService(inventory, work, session, clock, terminal.Id), catalog,
            (eventName, detail) => log.Add((eventName, detail)));

    private static void FillFlourUnit(InventoryViewModel vm)
    {
        vm.NewUnit(); vm.UnitCode = "HAR-BULTO-25KG"; vm.UnitName = "Bulto 25 kg"; vm.UnitFactor = "25000";
    }

    private string CashAndSalesSnapshot()
    {
        using var connection = database.Open();
        var result = new System.Text.StringBuilder();
        foreach (var table in new[] { "cash_session", "sale", "payment" })
        {
            using var command = new SqliteCommand($"SELECT * FROM {table} ORDER BY id", connection);
            using var rows = command.ExecuteReader();
            result.Append(table).Append(':');
            while (rows.Read())
                for (var column = 0; column < rows.FieldCount; column++)
                    result.Append(rows.GetName(column)).Append('=').Append(rows.GetValue(column)).Append('|');
        }
        return result.ToString();
    }

    private static void AssertSafeDiagnostic(List<(string Event, string? Detail)> log, string eventName)
    {
        Assert.IsTrue(log.Any(x => x.Event == eventName && x.Detail == nameof(IOException)));
        Assert.IsFalse(log.Any(x => x.Event.Contains(FaultingStore.PrivateDiagnostic, StringComparison.Ordinal)
            || (x.Detail?.Contains(FaultingStore.PrivateDiagnostic, StringComparison.Ordinal) ?? false)));
    }

    [TestMethod]
    public async Task NewSearchWinsWhenPreviousDatabaseReadFinishesLater()
    {
        var flour = Item();
        var faults = new FaultingStore(store);
        var vm = FaultingViewModel(faults, []); await vm.LoadAsync();
        using var release = new ManualResetEventSlim(false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        faults.BeforeQuery = query =>
        {
            if (query.Search != "slow") return;
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Old read was not released.");
        };
        vm.Search = "slow"; var oldSearch = vm.SearchAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(vm.Ready, "Search does not block typing through Busy.");
            vm.Search = "Harina"; await vm.SearchAsync();
            Assert.AreEqual(flour.Id, vm.Items.Single().Id);
            release.Set(); await oldSearch;
            Assert.AreEqual("Harina", vm.Search);
            Assert.AreEqual(flour.Id, vm.Items.Single().Id, "An old query must not overwrite current results.");
            Assert.IsFalse(vm.IsSearching);
        }
        finally { release.Set(); await oldSearch; vm.Forget(); }
    }

    [TestMethod]
    public async Task ConfirmationUsesCommittedResultWhenBalanceChangesAfterPreview()
    {
        var flour = Item(); Open(flour, 50_000m);
        var vm = ViewModel(); await vm.LoadAsync(); Select(vm, flour); await vm.OpenDetailAsync();
        vm.BeginOperation("receipt"); vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "kg");
        vm.Quantity = "25"; vm.Reference = "Factura de prueba";
        Assert.AreEqual(75_000_000L, vm.OperationPreview!.AfterAtomic);
        service.ReceiveInventory(new(Guid.NewGuid(), flour.Id, InventoryLocationIds.Main, 1m, "kg"));
        await vm.SubmitOperationAsync();
        Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        StringAssert.Contains(vm.Message, "+25 kg");
        StringAssert.Contains(vm.Message, "Existencia resultante: 76 kg");
        StringAssert.Contains(vm.Message, "Factura de prueba");
        Assert.AreEqual(76_000_000L, vm.Detail!.Balance!.QuantityAtomic);
    }

    [TestMethod]
    public async Task FlourPresentationPersistsAcrossRestartAndRepeatedDetailWithoutInitializingInventoryOrChangingCash()
    {
        new CashService(work, session, clock).Open(terminal.Id, new Money(50000));
        var financialBefore = CashAndSalesSnapshot();
        var vm = ViewModel(); await vm.LoadAsync(); vm.NewItem();
        vm.ItemCode = "MP-HAR-001"; vm.ItemName = "Harina";
        vm.SelectedItemKind = vm.ItemKinds.Single(x => x.Value == InventoryItemKind.RawMaterial);
        vm.SelectedDimension = vm.Dimensions.Single(x => x.Value == InventoryDimension.Mass);
        await vm.SaveItemAsync();
        var item = vm.Detail!.Item;
        FillFlourUnit(vm); await vm.SaveUnitAsync();
        Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        Assert.AreEqual("25 kg", vm.Units.Single().Quantity);
        Assert.IsTrue(vm.Units.Single().IsActive);
        Assert.IsNull(vm.Detail.Balance); Assert.AreEqual(0, vm.ProductLinks.Count); Assert.AreEqual(0, vm.History.Count);

        var reopenedDatabase = new Database(Path.Combine(root, "casamiga.db"));
        var reopenedService = new InventoryService(new SqliteInventoryStore(reopenedDatabase, clock),
            new SqliteLocalUnitOfWork(reopenedDatabase), session, clock, terminal.Id);
        var reopened = ViewModel(reopenedService); await reopened.LoadAsync();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Select(reopened, item); await reopened.OpenDetailAsync();
            Assert.IsTrue(reopened.IsDetail); Assert.IsTrue(reopened.Ready);
            Assert.IsFalse(reopened.RecoveryRequired);
            Assert.AreEqual("HAR-BULTO-25KG", reopened.Units.Single().Code);
            Assert.AreEqual("Bulto 25 kg", reopened.Units.Single().Name);
            Assert.AreEqual(25_000_000L, reopened.Units.Single().Unit.AtomicUnitsPerWholeUnit);
            Assert.IsTrue(reopened.Units.Single().IsActive); Assert.IsNull(reopened.Detail!.Balance);
            Assert.AreEqual(0, reopened.ProductLinks.Count); Assert.AreEqual(0, reopened.History.Count);
            reopened.Back(); Assert.IsTrue(reopened.IsList);
        }
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_item WHERE code='MP-HAR-001'"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_item_unit WHERE code='HAR-BULTO-25KG'"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_item_unit u LEFT JOIN inventory_item i ON i.id=u.item_id WHERE i.id IS NULL"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_balance"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_operation"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_movement"));
        Assert.AreEqual(financialBefore, CashAndSalesSnapshot());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PresentationActiveStateComesFromPersistedValue(bool active)
    {
        var item = Item(); service.AddInventoryItemUnit(item.Id, "SACK25", "Saco 25 kg", 25_000_000);
        if (!active) Sql("UPDATE inventory_item_unit SET active=0 WHERE code='SACK25'");
        var vm = ViewModel(); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        Assert.AreEqual(active, vm.Units.Single().IsActive);
        Assert.AreEqual(active, vm.Units.Single().Unit.IsActive);
        Assert.IsNull(vm.Detail!.Balance);
    }

    [TestMethod]
    public async Task DetailReadFailureClearsStaleStateAndAllowsExplicitRetryWithoutWriting()
    {
        var item = Item(); service.AddInventoryItemUnit(item.Id, "SACK25", "Saco 25 kg", 25_000_000);
        var faults = new FaultingStore(store); var log = new List<(string Event, string? Detail)>();
        var vm = FaultingViewModel(faults, log); await vm.LoadAsync(); Select(vm, item);
        faults.FailDetail = true; await vm.OpenDetailAsync();
        StringAssert.Contains(vm.Message, "No se pudo cargar");
        Assert.IsTrue(vm.IsList); Assert.IsNull(vm.Detail); Assert.AreEqual(0, vm.History.Count);
        Assert.IsFalse(vm.MessageIsSuccess); Assert.IsFalse(vm.RecoveryRequired); Assert.IsTrue(vm.Ready);
        Assert.AreEqual(0, faults.AddCalls); AssertSafeDiagnostic(log, "inventory.ui.read.failed");
        faults.FailDetail = false; await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        Assert.IsTrue(vm.IsDetail); Assert.AreEqual("SACK25", vm.Units.Single().Code);
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_item_unit"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_operation"));
    }

    [TestMethod]
    public async Task ConfirmedPresentationWithRefreshFailureCannotBeSavedTwiceAndCanBeReadAgain()
    {
        new CashService(work, session, clock).Open(terminal.Id, new Money(50000));
        var financialBefore = CashAndSalesSnapshot(); var item = Item("MP-HAR-001", "Harina");
        var faults = new FaultingStore(store) { FailDetailAfterAdd = true };
        var log = new List<(string Event, string? Detail)>();
        var vm = FaultingViewModel(faults, log); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        FillFlourUnit(vm); await vm.SaveUnitAsync();
        StringAssert.Contains(vm.Message, "Operación confirmada"); StringAssert.Contains(vm.Message, "No repitas");
        Assert.IsFalse(vm.MessageIsSuccess); Assert.IsFalse(vm.RecoveryRequired); Assert.IsTrue(vm.Ready);
        Assert.IsTrue(vm.IsList); Assert.IsNull(vm.Detail); Assert.AreEqual(0, vm.History.Count);
        Assert.AreEqual(1, faults.AddCalls);
        AssertSafeDiagnostic(log, "inventory.ui.refresh.failed_after_commit");
        await vm.SaveUnitAsync(); Assert.AreEqual(1, faults.AddCalls);
        faults.FailDetail = false; faults.FailDetailAfterAdd = false;
        await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        Assert.AreEqual("HAR-BULTO-25KG", vm.Units.Single().Code); Assert.IsTrue(vm.Units.Single().IsActive);
        await vm.SaveUnitAsync(); Assert.AreEqual(1, faults.AddCalls);
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_item_unit"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM audit_entry WHERE action='inventory.unit.added'"));
        Assert.IsNull(vm.Detail!.Balance); Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_operation"));
        Assert.AreEqual(financialBefore, CashAndSalesSnapshot());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task UncertainPresentationWriteBlocksFurtherActionsUntilNewViewModel(bool committed)
    {
        new CashService(work, session, clock).Open(terminal.Id, new Money(50000));
        var financialBefore = CashAndSalesSnapshot(); var item = Item("MP-HAR-001", "Harina");
        var faults = new FaultingStore(store) { FailAddBeforeCommit = !committed, FailAddAfterCommit = committed };
        var log = new List<(string Event, string? Detail)>();
        var vm = FaultingViewModel(faults, log); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        FillFlourUnit(vm); await vm.SaveUnitAsync();
        Assert.IsTrue(vm.RecoveryRequired); Assert.IsFalse(vm.Ready); Assert.IsFalse(vm.Busy);
        Assert.IsFalse(vm.MessageIsSuccess); StringAssert.Contains(vm.Message, "No se pudo verificar");
        AssertSafeDiagnostic(log, "inventory.ui.write.uncertain");
        var warning = vm.Message; var queries = faults.QueryCalls; var detailReads = faults.DetailCalls;
        await vm.SaveUnitAsync(); await vm.LoadAsync(); vm.NewUnit(); vm.NewItem(); vm.Back();
        await vm.SearchAsync(); await vm.OpenDetailAsync();
        Assert.AreEqual(warning, vm.Message); Assert.IsTrue(vm.RecoveryRequired);
        Assert.AreEqual(1, faults.AddCalls); Assert.AreEqual(queries, faults.QueryCalls); Assert.AreEqual(detailReads, faults.DetailCalls);
        Assert.AreEqual(committed ? 1L : 0L, Sql("SELECT count(*) FROM inventory_item_unit"));
        Assert.AreEqual(committed ? 1L : 0L, Sql("SELECT count(*) FROM audit_entry WHERE action='inventory.unit.added'"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_operation"));
        Assert.AreEqual(financialBefore, CashAndSalesSnapshot());

        var restarted = ViewModel(); await restarted.LoadAsync(); Select(restarted, item); await restarted.OpenDetailAsync();
        Assert.IsTrue(restarted.Ready); Assert.IsFalse(restarted.RecoveryRequired);
        Assert.AreEqual(committed ? 1 : 0, restarted.Units.Count); Assert.IsNull(restarted.Detail!.Balance);
    }

    [TestMethod]
    public async Task RejectedPresentationValidationAndDuplicateCodeAllowCorrectionWithoutRecoveryLock()
    {
        var item = Item(); var faults = new FaultingStore(store);
        var vm = FaultingViewModel(faults, []); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        FillFlourUnit(vm); vm.UnitFactor = "0"; await vm.SaveUnitAsync();
        Assert.IsFalse(vm.RecoveryRequired); Assert.IsTrue(vm.Ready); Assert.IsTrue(vm.IsUnitEditor);
        Assert.IsFalse(vm.MessageIsSuccess); Assert.AreEqual(0, faults.AddCalls);
        vm.UnitFactor = "25000"; await vm.SaveUnitAsync(); Assert.IsTrue(vm.MessageIsSuccess);
        FillFlourUnit(vm); await vm.SaveUnitAsync();
        Assert.IsFalse(vm.RecoveryRequired); Assert.IsTrue(vm.Ready); Assert.IsTrue(vm.IsUnitEditor);
        Assert.IsFalse(vm.MessageIsSuccess); Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_item_unit"));
        vm.UnitCode = "SACK50"; vm.UnitName = "Saco 50 kg"; vm.UnitFactor = "50000";
        await vm.SaveUnitAsync(); Assert.IsTrue(vm.MessageIsSuccess); Assert.AreEqual(2, vm.Units.Count);
        Assert.IsNull(vm.Detail!.Balance); Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_operation"));
    }

    [TestMethod]
    public async Task ConcurrencyRefreshFailureDoesNotEscapeOrRetryTheRejectedCount()
    {
        var item = Item(); Open(item, 10m); FaultingStore? faults = null;
        faults = new(new InterleavingStore(store, () =>
        {
            service.ReceiveInventory(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 1m, "g"));
            faults!.FailDetail = true;
        }));
        var log = new List<(string Event, string? Detail)>(); var vm = FaultingViewModel(faults, log);
        await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        vm.BeginOperation("count"); var rejectedOperation = Guid.Parse(vm.PendingOperationId);
        vm.Quantity = "12"; vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "g");
        await vm.SubmitOperationAsync();
        StringAssert.Contains(vm.Message, "No se pudo cargar");
        Assert.IsTrue(vm.Ready); Assert.IsFalse(vm.RecoveryRequired); Assert.IsTrue(vm.IsList); Assert.IsNull(vm.Detail);
        Assert.IsFalse(vm.MessageIsSuccess); Assert.AreEqual("", vm.PendingOperationId);
        Assert.AreEqual(11_000L, store.FindBalance(item.Id, InventoryLocationIds.Main)!.QuantityAtomic);
        Assert.IsNull(store.FindOperation(rejectedOperation)); Assert.AreEqual(2L, Sql("SELECT count(*) FROM inventory_operation"));
        AssertSafeDiagnostic(log, "inventory.ui.read.failed");
        faults.FailDetail = false; await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        Assert.AreEqual(11_000L, vm.Detail!.Balance!.QuantityAtomic);
    }

    [TestMethod]
    public async Task NavigationAndActionsFollowAdministratorManagerAndCashierPermissions()
    {
        var admin = ViewModel(); Assert.IsTrue(admin.CanEnter()); await admin.LoadAsync();
        Assert.IsTrue(admin.CanView); Assert.IsTrue(admin.CanConfigure); Assert.IsTrue(admin.CanReceive);
        Assert.IsTrue(admin.CanWaste); Assert.IsTrue(admin.CanAdjust);

        Role("Manager"); var manager = ViewModel(); Assert.IsTrue(manager.CanEnter()); await manager.LoadAsync();
        Assert.IsTrue(manager.CanView); Assert.IsFalse(manager.CanConfigure); Assert.IsTrue(manager.CanReceive);
        Assert.IsTrue(manager.CanWaste); Assert.IsFalse(manager.CanAdjust);

        Role("Cashier"); var cashier = ViewModel(); Assert.IsFalse(cashier.CanEnter()); await cashier.LoadAsync();
        Assert.IsFalse(cashier.CanView);
        Assert.AreEqual("No tienes permiso para realizar esta operación.", cashier.Message);
    }

    [TestMethod]
    public async Task ListSearchFiltersPagingAndDetailUseApplicationQueries()
    {
        var target = Item("FLOUR", "Harina especial");
        service.CreateInventoryItem("BREAD", "Pan terminado", InventoryItemKind.FinishedGood, InventoryDimension.Count);
        for (var index = 0; index < 50; index++) Item($"ITEM-{index:00}", $"Artículo {index:00}");
        var vm = ViewModel(); await vm.LoadAsync();
        Assert.AreEqual(50, vm.Items.Count); Assert.IsTrue(vm.HasNext);
        await vm.PageAsync(1); Assert.AreEqual(2, vm.Items.Count); Assert.IsTrue(vm.HasPrevious);
        vm.Search = "harina"; await vm.SearchAsync(); Assert.AreEqual(1, vm.Items.Count);
        Select(vm, target); await vm.OpenDetailAsync();
        Assert.IsTrue(vm.IsDetail); Assert.IsTrue(vm.DetailTitle.Contains("FLOUR"));
        Assert.IsTrue(vm.DetailBalance.Contains("sin saldo inicial"));
        Assert.AreEqual("Inventario consistente.", vm.ReconciliationMessage);
        vm.Back(); vm.SelectedActiveFilter = vm.ActiveFilters.Single(x => x.Value == false); await vm.SearchAsync();
        Assert.AreEqual(0, vm.Items.Count);
        vm.Search = ""; vm.SelectedActiveFilter = vm.ActiveFilters.Single(x => x.Value == true);
        vm.SelectedKindFilter = vm.KindFilters.Single(x => x.Value == InventoryItemKind.FinishedGood);
        await vm.SearchAsync(); Assert.AreEqual("BREAD", vm.Items.Single().Code);
    }

    [TestMethod]
    public async Task ConfigurationCreatesEditsTogglesPresentationsAndProductLinks()
    {
        var vm = ViewModel(); await vm.LoadAsync(); vm.NewItem();
        vm.ItemCode = "BUTTER"; vm.ItemName = "Mantequilla";
        vm.SelectedItemKind = vm.ItemKinds.Single(x => x.Value == InventoryItemKind.RawMaterial);
        vm.SelectedDimension = vm.Dimensions.Single(x => x.Value == InventoryDimension.Mass);
        await vm.SaveItemAsync(); Assert.IsTrue(vm.IsDetail); Assert.AreEqual("Mantequilla", vm.Detail!.Item.Name);
        vm.EditItem(); vm.ItemName = "Mantequilla sin sal"; await vm.SaveItemAsync();
        Assert.AreEqual("Mantequilla sin sal", vm.Detail!.Item.Name);
        vm.NewUnit(); vm.UnitCode = "SACK25"; vm.UnitName = "Saco 25 kg"; vm.UnitFactor = "25000";
        await vm.SaveUnitAsync(); Assert.AreEqual("Saco 25 kg", vm.Units.Single().Name);

        var category = catalog.CreateCategory("PAN");
        var product = catalog.CreateProduct(category.Id, "P-1", "Concha sintética", "", new Money(1000));
        await vm.ShowLinkEditorAsync(); vm.SelectedProduct = vm.Products.Single(x => x.Product.Id == product.Id);
        vm.LinkQuantityAtomic = "1"; await vm.SaveLinkAsync(); Assert.AreEqual(product.Id, vm.ProductLinks.Single().ProductId);
        StringAssert.Contains(vm.Message, "cuando el consumo automático esté ACTIVO.");
        Assert.IsTrue(product.TrackInventory);
        vm.SelectedLink = vm.ProductLinks.Single(); await vm.UnlinkAsync(); Assert.AreEqual(0, vm.ProductLinks.Count);
        await vm.ToggleActiveAsync(); Assert.IsFalse(vm.Detail!.Item.IsActive);
    }

    [TestMethod]
    public async Task OperationsKeepExactQuantitiesStableIntentAndRefreshHistory()
    {
        var item = Item(); var vm = ViewModel(); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        vm.BeginOperation("opening"); var openingId = vm.PendingOperationId; vm.Quantity = "0";
        await vm.SubmitOperationAsync(); Assert.IsTrue(vm.MessageIsSuccess);
        Assert.AreEqual(0L, vm.Detail!.Balance!.QuantityAtomic); Assert.AreEqual("", vm.PendingOperationId);

        vm.BeginOperation("receipt"); var receiptId = vm.PendingOperationId; vm.Quantity = "2.5";
        vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "kg"); vm.Reference = "REM-UI"; vm.Note = "Entrega sintética";
        var first = vm.SubmitOperationAsync(); var duplicateClick = vm.SubmitOperationAsync(); await Task.WhenAll(first, duplicateClick);
        Assert.AreEqual(2_500_000L, vm.Detail!.Balance!.QuantityAtomic);
        Assert.AreEqual(2L, Sql("SELECT count(*) FROM inventory_operation"));

        vm.BeginOperation("waste"); vm.Quantity = "500"; vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "g");
        vm.SelectedWasteReason = vm.WasteReasons.Single(x => x.Value == WasteReason.Other); vm.Note = "Prueba controlada";
        await vm.SubmitOperationAsync(); Assert.AreEqual(2_000_000L, vm.Detail!.Balance!.QuantityAtomic);

        vm.BeginOperation("count"); vm.Quantity = "3"; vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "kg");
        await vm.SubmitOperationAsync(); Assert.AreEqual(3_000_000L, vm.Detail!.Balance!.QuantityAtomic);
        vm.BeginOperation("count"); vm.Quantity = "1"; vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "kg");
        await vm.SubmitOperationAsync(); Assert.AreEqual(1_000_000L, vm.Detail!.Balance!.QuantityAtomic);
        vm.BeginOperation("count"); vm.Quantity = "1"; vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "kg");
        await vm.SubmitOperationAsync(); Assert.AreEqual("El conteo coincide con la existencia registrada. No se realizó ningún movimiento.", vm.Message);
        Assert.IsTrue(vm.MessageIsSuccess); Assert.AreEqual(5, vm.History.Count); Assert.AreEqual("Inventario consistente.", vm.ReconciliationMessage);
        Assert.AreNotEqual(openingId, receiptId);
        await vm.ToggleActiveAsync(); Assert.IsTrue(vm.Detail!.Item.IsActive);
        Assert.AreEqual("No se puede desactivar un artículo con existencia.", vm.Message);
    }

    [TestMethod]
    public async Task RejectionsPreserveOperationIdAndShowStableMessagesWithoutAutomaticRetry()
    {
        var item = Item(); Open(item, 1m); var vm = ViewModel(); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        vm.BeginOperation("waste"); var insufficientId = vm.PendingOperationId; vm.Quantity = "2";
        vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "kg"); await vm.SubmitOperationAsync();
        Assert.AreEqual("No hay existencia suficiente para realizar la operación.", vm.Message);
        Assert.AreEqual(insufficientId, vm.PendingOperationId);

        vm.Back(); vm.BeginOperation("receipt"); var conflictId = Guid.Parse(vm.PendingOperationId);
        service.ReceiveInventory(new(conflictId, item.Id, InventoryLocationIds.Main, 1m, "g"));
        vm.Quantity = "2"; vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "g"); await vm.SubmitOperationAsync();
        Assert.AreEqual("La operación no puede repetirse porque sus datos cambiaron.", vm.Message);
        Assert.AreEqual(conflictId.ToString("D"), vm.PendingOperationId);
        Assert.AreEqual("La unidad seleccionada no es válida.", InventoryViewModel.ErrorMessage(InventoryError.InvalidUnit));
        Assert.AreEqual("Se detectó una inconsistencia de inventario. La operación no fue reparada automáticamente.",
            InventoryViewModel.ErrorMessage(InventoryError.InconsistentBalance));
    }

    [TestMethod]
    public async Task CountConcurrencyRefreshesBalanceAndPreservesIntentForOperatorReview()
    {
        var item = Item(); Open(item, 10m);
        var concurrentService = new InventoryService(new InterleavingStore(store, () =>
            service.ReceiveInventory(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 1m, "g"))),
            work, session, clock, terminal.Id);
        var vm = ViewModel(concurrentService); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        vm.BeginOperation("count"); var operationId = vm.PendingOperationId; vm.Quantity = "12";
        vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "g"); await vm.SubmitOperationAsync();
        Assert.AreEqual("La existencia cambió mientras realizabas la operación. Actualiza los datos y vuelve a revisar el conteo.", vm.Message);
        Assert.AreEqual(operationId, vm.PendingOperationId); Assert.IsTrue(vm.IsOperationEditor);
        Assert.AreEqual(11_000L, vm.Detail!.Balance!.QuantityAtomic);
        Assert.AreEqual(2L, Sql("SELECT count(*) FROM inventory_operation"));
    }

    [TestMethod]
    public async Task PositiveOpeningAndReconciliationWarningAreShownWithoutRepair()
    {
        var item = Item(); var vm = ViewModel(); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        vm.BeginOperation("opening"); vm.Quantity = "1.25"; vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "kg");
        await vm.SubmitOperationAsync(); Assert.AreEqual(1_250_000L, vm.Detail!.Balance!.QuantityAtomic);
        Sql($"UPDATE inventory_balance SET quantity_atomic=42 WHERE item_id='{item.Id:D}' AND location_id='{InventoryLocationIds.Main:D}'");
        vm.Back(); Select(vm, item); await vm.OpenDetailAsync();
        Assert.IsTrue(vm.ReconciliationWarning);
        Assert.AreEqual("ADVERTENCIA: La existencia registrada no coincide con el historial de movimientos. No se realizó ninguna reparación.", vm.ReconciliationMessage);
        Assert.AreEqual(42L, vm.Detail!.Balance!.QuantityAtomic);
    }

    [TestMethod]
    [DataRow(InventoryError.Unauthorized, "No tienes permiso para realizar esta operación.")]
    [DataRow(InventoryError.InactiveUser, "El usuario actual ya no está activo.")]
    [DataRow(InventoryError.InvalidUnit, "La unidad seleccionada no es válida.")]
    [DataRow(InventoryError.DimensionMismatch, "La unidad no corresponde al tipo de artículo.")]
    [DataRow(InventoryError.InvalidQuantity, "La cantidad ingresada no es válida.")]
    [DataRow(InventoryError.InvalidNote, "La observación no es válida.")]
    [DataRow(InventoryError.NotInitialized, "Este artículo todavía no tiene saldo inicial.")]
    [DataRow(InventoryError.AlreadyInitialized, "Este artículo ya fue inicializado.")]
    [DataRow(InventoryError.InsufficientStock, "No hay existencia suficiente para realizar la operación.")]
    [DataRow(InventoryError.IdempotencyConflict, "La operación no puede repetirse porque sus datos cambiaron.")]
    [DataRow(InventoryError.InventoryItemInactive, "El artículo está inactivo.")]
    [DataRow(InventoryError.NoChange, "El conteo coincide con la existencia registrada. No se realizó ningún movimiento.")]
    [DataRow(InventoryError.ConcurrencyConflict, "La existencia cambió mientras realizabas la operación. Actualiza los datos y vuelve a revisar el conteo.")]
    [DataRow(InventoryError.InconsistentBalance, "Se detectó una inconsistencia de inventario. La operación no fue reparada automáticamente.")]
    public void StableApplicationErrorsHaveSpanishPresentationMessages(InventoryError error, string expected) =>
        Assert.AreEqual(expected, InventoryViewModel.ErrorMessage(error));

    [TestMethod]
    public void OperationIdentityIsNotExposedInOperationalXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Presentation", "InventoryView.xaml");
        var xaml = File.ReadAllText(path);
        Assert.IsFalse(xaml.Contains("OperationId", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InventoryAssociationHelpExplainsConditionalConsumption()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Presentation", "InventoryView.xaml"));
        StringAssert.Contains(xaml, "{Binding AssociationExplanation}");
        StringAssert.Contains(ViewModel().AssociationExplanation, "el producto controla inventario");
        StringAssert.Contains(ViewModel().AssociationExplanation, "Asociar no activa el consumo automático");
        Assert.IsFalse(xaml.Contains("todavía no descuenta", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ManagerWritesReceiptAndWasteButCannotConfigureOpenOrAdjust()
    {
        var item = Item(); Open(item, 1500m); Role("Manager");
        var vm = ViewModel(); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        vm.BeginOperation("receipt"); Assert.IsFalse(vm.CanSubmitOperation, "Una cantidad vacía ya no permite confirmar.");
        vm.Quantity = "100"; vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "g");
        Assert.IsTrue(vm.CanSubmitOperation);
        await vm.SubmitOperationAsync(); Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        Assert.AreEqual(1_600_000L, vm.Detail!.Balance!.QuantityAtomic);
        vm.BeginOperation("waste"); vm.Quantity = "100";
        await vm.SubmitOperationAsync(); Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        Assert.AreEqual(1_500_000L, vm.Detail!.Balance!.QuantityAtomic);
        vm.NewItem(); Assert.IsFalse(vm.IsItemEditor);
        vm.BeginOperation("opening"); Assert.IsFalse(vm.IsOperationEditor);
        vm.BeginOperation("count"); Assert.IsFalse(vm.IsOperationEditor);
        Assert.AreEqual(InventoryError.Unauthorized, Assert.ThrowsExactly<InventoryApplicationException>(() => Item("DENIED")).Error);
        Assert.AreEqual(InventoryError.Unauthorized, Assert.ThrowsExactly<InventoryApplicationException>(() =>
            service.AdjustToPhysicalCount(new(Guid.NewGuid(), item.Id, InventoryLocationIds.Main, 1m, "g", AdjustmentReason.PhysicalCountVariance))).Error);
        Sql("DELETE FROM role_permission WHERE role_name='Manager' AND permission_name='inventory.receive'");
        vm.BeginOperation("receipt"); vm.Quantity = "1"; await vm.SubmitOperationAsync();
        Assert.AreEqual(InventoryViewModel.ErrorMessage(InventoryError.Unauthorized), vm.Message);
        Assert.AreEqual(1_500_000L, store.FindBalance(item.Id, InventoryLocationIds.Main)!.QuantityAtomic);
    }

    [TestMethod]
    public async Task OperationalUnitsExcludeTechnicalAtomicUnits()
    {
        foreach (var dimension in Enum.GetValues<InventoryDimension>())
        {
            var item = service.CreateInventoryItem(dimension.ToString(), dimension.ToString(), InventoryItemKind.Supply, dimension);
            var vm = ViewModel(); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync(); vm.BeginOperation("opening");
            var expected = dimension switch { InventoryDimension.Mass => new[] { "g", "kg" }, InventoryDimension.Volume => new[] { "ml", "L" }, _ => new[] { "piece" } };
            CollectionAssert.AreEqual(expected, vm.UnitChoices.Select(x => x.Code).ToArray());
        }
    }

    [TestMethod]
    public void HumanFormattingPreservesExactAtomicQuantities()
    {
        Assert.AreEqual("1.5 kg", InventoryDisplay.Format(1_500_000, InventoryDimension.Mass));
        Assert.AreEqual("500 g", InventoryDisplay.Format(500_000, InventoryDimension.Mass));
        Assert.AreEqual("1 g", InventoryDisplay.Format(1000, InventoryDimension.Mass));
        Assert.AreEqual("0.001 g", InventoryDisplay.Format(1, InventoryDimension.Mass));
        Assert.AreEqual("0.001 ml", InventoryDisplay.Format(1, InventoryDimension.Volume));
        Assert.AreEqual("1 L", InventoryDisplay.Format(1_000_000, InventoryDimension.Volume));
        Assert.AreEqual("3 pieza", InventoryDisplay.Format(3, InventoryDimension.Count));
        Assert.AreEqual(25_000_000L, InventoryDisplay.ParseCapture("25000", InventoryDimension.Mass));
        Assert.AreEqual(1L, InventoryDisplay.ParseCapture("0.001", InventoryDimension.Mass));
        Assert.ThrowsExactly<ArgumentException>(() => InventoryDisplay.ParseCapture("0.0001", InventoryDimension.Mass));
        Assert.ThrowsExactly<ArgumentException>(() => InventoryDisplay.ParseCapture("0.5", InventoryDimension.Count));
    }
}
