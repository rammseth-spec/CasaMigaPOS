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

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "CasaMigaInventoryViewModel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        database = new(Path.Combine(root, "casamiga.db")); database.Initialize(); work = new(database); session = new();
        var identity = new IdentityService(work, new LocalPasswords(), clock, session);
        var password = Guid.NewGuid().ToString("N"); identity.Bootstrap("admin", "Inventario UI", password);
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
        Assert.AreEqual("ADVERTENCIA: El saldo materializado no coincide con el ledger. No se realizó ninguna reparación.", vm.ReconciliationMessage);
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
    public void ReadOnlyOperationIdBindingIsExplicitlyOneWay()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Presentation", "InventoryView.xaml");
        var xaml = File.ReadAllText(path);
        StringAssert.Contains(xaml, "Text=\"{Binding PendingOperationId,Mode=OneWay}\"");
    }

    [TestMethod]
    public void InventoryAssociationHelpExplainsConditionalConsumption()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Presentation", "InventoryView.xaml"));
        StringAssert.Contains(xaml, "al confirmar la venta cuando el consumo automático está ACTIVO.");
        Assert.IsFalse(xaml.Contains("todavía no descuenta", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ManagerWritesReceiptAndWasteButCannotConfigureOpenOrAdjust()
    {
        var item = Item(); Open(item, 1500m); Role("Manager");
        var vm = ViewModel(); await vm.LoadAsync(); Select(vm, item); await vm.OpenDetailAsync();
        vm.BeginOperation("receipt"); Assert.IsTrue(vm.CanSubmitOperation);
        vm.Quantity = "100"; vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "g");
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
