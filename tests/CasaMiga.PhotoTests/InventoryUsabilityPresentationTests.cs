using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class InventoryUsabilityPresentationTests
{
    [TestMethod]
    [DataRow(1280, 820)]
    [DataRow(1920, 1080)]
    public Task TypingSearchFiltersWithoutSubmitPreservesValidSelectionAndClearRestoresList(int width, int height) => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await Fixture.ShowAsync(width, height);
        var vm = fixture.Inventory;
        var view = fixture.View;
        var box = Descendants<TextBox>(view).Single(x => AutomationProperties.GetName(x) == "Buscar inventario");
        var grid = Grid(view, "Artículos de inventario");
        Assert.AreEqual(3, grid.Items.Count);
        Assert.IsFalse(Button(view, "Ver detalle").IsEnabled);
        grid.SelectedItem = vm.Items.Single(x => x.Id == fixture.Flour.Id);
        fixture.Layout();
        Assert.IsTrue(Button(view, "Ver detalle").IsEnabled);

        box.Focus(); Keyboard.Focus(box);
        box.Text = "H"; box.Text = "Hari"; box.Text = "Harina";
        Assert.IsTrue(box.IsEnabled, "Typing must stay responsive during the debounce/query.");
        await WaitUntil(() => !vm.IsSearching && vm.Items.Count == 1 && vm.Items[0].Id == fixture.Flour.Id);
        fixture.Layout();
        Assert.AreEqual("Harina", vm.Search);
        Assert.AreEqual(fixture.Flour.Id, vm.SelectedItem?.Id);
        Assert.IsTrue(Button(view, "× Limpiar").IsVisible);

        box.Text = "MP-LECHE";
        await WaitUntil(() => !vm.IsSearching && vm.Items.Count == 1 && vm.Items[0].Id == fixture.Milk.Id);
        fixture.Layout();
        Assert.IsNull(vm.SelectedItem, "A selected article filtered out must not remain actionable.");
        Assert.IsFalse(Button(view, "Ver detalle").IsEnabled);

        box.Text = "sin coincidencias 123";
        await WaitUntil(() => !vm.IsSearching && vm.Items.Count == 0);
        fixture.Layout();
        AssertText(view, "No encontramos artículos con esa búsqueda.");
        Assert.IsFalse(Button(view, "Ver detalle").IsEnabled);
        Capture(fixture.Window, $"1k3b-search-empty-{width}.png");
        Button(view, "× Limpiar").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitUntil(() => !vm.IsSearching && vm.Items.Count == 3 && vm.Search.Length == 0);
        fixture.Layout();
        Assert.AreEqual("", box.Text);
        Assert.IsTrue(box.IsKeyboardFocusWithin, "Clear returns focus to the search field.");
        Assert.IsFalse(Descendants<System.Windows.Controls.Button>(view).Any(x => x.IsVisible && Equals(x.Content, "× Limpiar")));
        Assert.IsFalse(grid.Columns.Any(x => Equals(x.Header, "Saldo MAIN")));
        Assert.IsTrue(grid.Columns.Any(x => Equals(x.Header, "Existencia actual")));
        fixture.AssertUnchanged();
    });

    [TestMethod]
    [DataRow(1280, 820)]
    [DataRow(1920, 1080)]
    public Task DetailRendersExactEquivalenceHumanProductIdentityAndCleanLocalHistory(int width, int height) => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await Fixture.ShowAsync(width, height);
        await fixture.OpenFlourAsync();
        var vm = fixture.Inventory; var view = fixture.View;
        AssertText(view, "Existencia actual: 50 kg");
        var units = Grid(view, "Presentaciones del artículo");
        units.BringIntoView(); fixture.Layout();
        AssertText(units, "1 Bulto 25 kg = 25 kg");
        Assert.IsFalse(Button(view, "Desvincular seleccionado").IsEnabled);
        Assert.IsTrue(Button(view, "Editar").IsEnabled);
        Assert.IsTrue(Button(view, "Entrada").IsEnabled);
        Assert.IsTrue(Button(view, "Merma").IsEnabled);
        Assert.IsTrue(Button(view, "Conteo físico").IsEnabled);
        var links = Grid(view, "Asociaciones de productos");
        links.BringIntoView(); fixture.Layout();
        Assert.IsFalse(links.Columns.Any(x => Equals(x.Header, "ProductId")));
        AssertText(links, "Pan de prueba"); AssertText(links, "PAN-PRUEBA"); AssertText(links, "100 g");
        Assert.IsFalse(Descendants<TextBlock>(links).Any(x => x.IsVisible && x.Text.Contains(fixture.Product.Id.ToString(), StringComparison.OrdinalIgnoreCase)));
        links.SelectedIndex = 0; fixture.Layout();
        Assert.IsTrue(Button(view, "Desvincular seleccionado").IsEnabled);
        Assert.AreEqual(fixture.Product.Id, vm.SelectedLink!.ProductId, "Identity stays internal for the existing unlink command.");
        AssertText(view, vm.AssociationExplanation);
        Capture(fixture.Window, $"1k3b-detail-{width}.png");

        var history = Grid(view, "Historial de inventario");
        history.BringIntoView(); fixture.Layout();
        Assert.IsTrue(history.Columns.Any(x => Equals(x.Header, "Existencia anterior")));
        Assert.IsTrue(history.Columns.Any(x => Equals(x.Header, "Existencia resultante")));
        Assert.IsFalse(history.Columns.Any(x => x.Header is "Delta" or "Antes" or "Después"));
        var row = vm.History.Single();
        Assert.AreEqual(fixture.Clock.UtcNow.ToLocalTime().ToString("dd/MM/yyyy · HH:mm", CultureInfo.InvariantCulture), row.Date);
        AssertText(history, row.Date);
        StringAssert.StartsWith(row.Delta, "+");
        Assert.IsFalse(row.Date.Contains("+00:00") || row.Date.Contains("-06:00"));
        Capture(fixture.Window, $"1k3b-history-{width}.png");
        fixture.AssertUnchanged();
    });

    [TestMethod]
    public Task LeavingAndReturningDuringDebounceDoesNotShowResultsForAnOldFilter() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await Fixture.ShowAsync();
        var vm = fixture.Inventory;
        var box = Descendants<TextBox>(fixture.View).Single(x => AutomationProperties.GetName(x) == "Buscar inventario");
        box.Text = "MP-LECHE";
        Button(fixture.View, "Nuevo artículo").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        fixture.Layout(); Assert.IsTrue(vm.IsItemEditor);
        Button(fixture.View, "Cancelar").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitUntil(() => !vm.IsSearching && vm.Items.Count == 1 && vm.Items[0].Id == fixture.Milk.Id);
        fixture.Layout();
        Assert.IsTrue(vm.IsList); Assert.AreEqual("MP-LECHE", box.Text);
        Assert.IsFalse(Button(fixture.View, "Ver detalle").IsEnabled);
        fixture.AssertUnchanged();
    });

    [TestMethod]
    public Task CancelledDebounceRestoresNextPageWhenTheFilterReturnsToItsAppliedValue() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await Fixture.ShowAsync();
        for (var index = 0; index < 50; index++)
            fixture.Service.CreateInventoryItem($"UX-{index:000}", $"Insumo extra {index:000}", InventoryItemKind.Supply, InventoryDimension.Count);
        var vm = fixture.Inventory;
        await vm.SearchAsync(); fixture.Layout();
        Assert.AreEqual(50, vm.Items.Count);
        Assert.IsTrue(Button(fixture.View, "Siguiente").IsEnabled);
        var firstPageIds = vm.Items.Select(x => x.Id).ToHashSet();
        var box = Descendants<TextBox>(fixture.View).Single(x => AutomationProperties.GetName(x) == "Buscar inventario");
        box.Text = "H";
        box.Text = "";
        Assert.IsTrue(vm.IsSearching);
        Button(fixture.View, "Nuevo artículo").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        fixture.Layout(); Assert.IsTrue(vm.IsItemEditor);
        Button(fixture.View, "Cancelar").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitUntil(() => vm.IsList && !vm.IsSearching);
        fixture.Layout();
        Assert.AreEqual("", box.Text);
        Assert.IsTrue(Button(fixture.View, "Siguiente").IsEnabled, "Cancelling a query must not erase pagination for the already applied empty filter.");
        Button(fixture.View, "Siguiente").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitUntil(() => !vm.Busy && vm.Items.Count == 3);
        fixture.Layout();
        StringAssert.StartsWith(vm.PageLabel, "Página 2");
        Assert.IsTrue(vm.Items.All(x => !firstPageIds.Contains(x.Id)), "The enabled button must load the actual second page.");
        Assert.IsTrue(Button(fixture.View, "Anterior").IsEnabled);
        Assert.IsFalse(Button(fixture.View, "Siguiente").IsEnabled);
        fixture.AssertUnchanged();
    });

    [TestMethod]
    [DataRow("receipt", "1", "HAR-BULTO-25KG", 25_000_000L, 75_000_000L, "Cantidad recibida")]
    [DataRow("waste", "2", "kg", -2_000_000L, 48_000_000L, "Cantidad de merma")]
    [DataRow("count", "48", "kg", -2_000_000L, 48_000_000L, "Nueva existencia física")]
    public Task OperationPreviewIsVisibleExactAndContextHelpCanCloseWithoutRegistering(string operation, string amount, string unit, long delta, long after, string quantityLabel) => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await Fixture.ShowAsync();
        await fixture.OpenFlourAsync();
        var vm = fixture.Inventory; var view = fixture.View;
        vm.BeginOperation(operation); fixture.Layout();
        Assert.IsFalse(Button(view, "Confirmar").IsEnabled, "An empty quantity must not be confirmable.");
        var quantity = Bound<TextBox>(view, TextBox.TextProperty, "Quantity");
        var units = Bound<ComboBox>(view, ComboBox.SelectedItemProperty, "SelectedUnit");
        units.SelectedItem = vm.UnitChoices.Single(x => x.Code == unit);
        quantity.Text = amount; fixture.Layout();
        Assert.IsTrue(vm.HasOperationPreview, vm.PreviewError);
        Assert.AreEqual(50_000_000L, vm.OperationPreview!.BeforeAtomic);
        Assert.AreEqual(delta, vm.OperationPreview.DeltaAtomic);
        Assert.AreEqual(after, vm.OperationPreview.AfterAtomic);
        AssertText(view, quantityLabel);
        Assert.IsFalse(Descendants<TextBlock>(view).Any(x => x.IsVisible && x.Text == "Cantidad absoluta"));
        AssertText(view, vm.PreviewCurrent); AssertText(view, vm.PreviewMovement); AssertText(view, vm.PreviewResult);
        if (operation == "count") AssertText(view, vm.PreviewDifference);
        if (operation == "receipt")
        {
            AssertText(view, "Referencia / factura / nota (opcional)");
            AssertText(view, "1 Bulto 25 kg = 25 kg · captura presentaciones enteras");
        }
        Assert.IsTrue(Button(view, "Confirmar").IsEnabled, vm.PreviewError);
        var help = Descendants<ContextHelp>(view).Single(x => x.IsVisible && x.HelpText == vm.OperationHelpText);
        var expander = Descendants<Expander>(help).Single();
        Assert.IsFalse(expander.IsExpanded);
        expander.IsExpanded = true; help.BringIntoView(); fixture.Layout();
        AssertText(help, vm.OperationHelpText);
        expander.IsExpanded = false; fixture.Layout();
        Assert.IsFalse(Descendants<TextBlock>(help).Any(x => x.IsVisible && x.Text == vm.OperationHelpText));
        quantity.BringIntoView(); fixture.Layout();
        Capture(fixture.Window, $"1k3b-preview-{operation}.png");
        Button(view, "Cancelar").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        fixture.Layout(); Assert.IsTrue(vm.IsDetail);
        fixture.AssertUnchanged();
    });

    [TestMethod]
    public Task InvalidQuantitiesNeverOfferConfirmationAndZeroCountHasExactDifference() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await Fixture.ShowAsync(); await fixture.OpenFlourAsync();
        var vm = fixture.Inventory; var view = fixture.View;
        vm.BeginOperation("waste"); fixture.Layout();
        vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "kg");
        foreach (var quantity in new[] { "", "abc", "-1", "0", "51", "0.0000001", "9223372036854775808" })
        {
            Bound<TextBox>(view, TextBox.TextProperty, "Quantity").Text = quantity; fixture.Layout();
            Assert.IsFalse(Button(view, "Confirmar").IsEnabled, quantity);
            Assert.IsFalse(vm.HasOperationPreview, quantity);
        }
        vm.Back(); vm.BeginOperation("receipt"); fixture.Layout();
        vm.SelectedUnit = vm.UnitChoices.Single(x => x.Code == "HAR-BULTO-25KG");
        Bound<TextBox>(view, TextBox.TextProperty, "Quantity").Text = "0.5"; fixture.Layout();
        Assert.IsFalse(Button(view, "Confirmar").IsEnabled, "Presentations must remain whole units.");
        vm.Back(); vm.BeginOperation("count"); fixture.Layout();
        Bound<TextBox>(view, TextBox.TextProperty, "Quantity").Text = "0"; fixture.Layout();
        Assert.AreEqual(-50_000_000L, vm.OperationPreview!.DeltaAtomic);
        Assert.AreEqual(0L, vm.OperationPreview.AfterAtomic);
        Assert.IsTrue(Button(view, "Confirmar").IsEnabled, "An exact zero physical count is allowed.");
        vm.Back(); fixture.AssertUnchanged();
    });

    [TestMethod]
    public Task UninitializedArticleOffersOpeningHelpAndDisablesMovementsThatRequireBalance() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await Fixture.ShowAsync();
        var item = fixture.Service.CreateInventoryItem("MP-NUEVO", "Artículo nuevo de prueba", InventoryItemKind.RawMaterial, InventoryDimension.Mass);
        var vm = fixture.Inventory;
        await vm.SearchAsync(); vm.SelectedItem = vm.Items.Single(x => x.Id == item.Id); await vm.OpenDetailAsync(); fixture.Layout();
        var view = fixture.View;
        Assert.IsTrue(Button(view, "Saldo inicial").IsEnabled);
        Assert.IsFalse(Button(view, "Entrada").IsEnabled);
        Assert.IsFalse(Button(view, "Merma").IsEnabled);
        Assert.IsFalse(Button(view, "Conteo físico").IsEnabled);
        Button(view, "Saldo inicial").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); fixture.Layout();
        AssertText(view, "Existencia al comenzar");
        var help = Descendants<ContextHelp>(view).Single(x => x.IsVisible && x.HelpText == vm.OperationHelpText);
        var expander = Descendants<Expander>(help).Single(); expander.IsExpanded = true; fixture.Layout();
        AssertText(help, vm.OperationHelpText);
        StringAssert.Contains(vm.OperationHelpText, "entrada de compra");
        expander.IsExpanded = false;
        Bound<TextBox>(view, TextBox.TextProperty, "Quantity").Text = "0"; fixture.Layout();
        Assert.AreEqual(0L, vm.OperationPreview!.BeforeAtomic); Assert.AreEqual(0L, vm.OperationPreview.AfterAtomic);
        Assert.IsTrue(Button(view, "Confirmar").IsEnabled);
        vm.Back(); fixture.AssertUnchanged();
    });

    [TestMethod]
    public void CustomPresentationsZeroCountsAndOverflowPreserveExactRules()
    {
        using var fixture = new Fixture();
        var detail = fixture.Service.Detail(fixture.Flour.Id);
        var sack = InventoryOperationPreview.Calculate(detail, "receipt", 1m, "HAR-BULTO-25KG");
        Assert.AreEqual(25_000_000L, sack.DeltaAtomic); Assert.AreEqual(75_000_000L, sack.AfterAtomic);
        var zero = InventoryOperationPreview.Calculate(detail, "count", 0m, "HAR-BULTO-25KG");
        Assert.AreEqual(-50_000_000L, zero.DeltaAtomic); Assert.AreEqual(0L, zero.AfterAtomic);
        var unchanged = InventoryOperationPreview.Calculate(detail, "count", 2m, "HAR-BULTO-25KG");
        Assert.AreEqual(0L, unchanged.DeltaAtomic); Assert.AreEqual(50_000_000L, unchanged.AfterAtomic);
        AssertError(InventoryError.InvalidQuantity, () => InventoryOperationPreview.Calculate(detail, "receipt", 0.5m, "HAR-BULTO-25KG"));
        AssertError(InventoryError.InvalidQuantity, () => InventoryOperationPreview.Calculate(detail, "receipt", decimal.MaxValue, "kg"));
        AssertError(InventoryError.InsufficientStock, () => InventoryOperationPreview.Calculate(detail, "waste", 51m, "kg"));
        AssertError(InventoryError.DimensionMismatch, () => InventoryOperationPreview.Calculate(detail, "receipt", 1m, "L"));
        AssertError(InventoryError.InvalidUnit, () => InventoryOperationPreview.Calculate(detail, "receipt", 1m, "NO-EXISTE"));
        AssertError(InventoryError.InvalidQuantity, () => InventoryOperationPreview.Calculate(fixture.Service.Detail(fixture.Bags.Id), "receipt", 0.5m, "piece"));
        fixture.AssertUnchanged();
    }

    [TestMethod]
    [DataRow(InventoryDimension.Mass, "receipt", "1.001", "g", 1_001L)]
    [DataRow(InventoryDimension.Mass, "waste", "0.001", "g", -1L)]
    [DataRow(InventoryDimension.Mass, "count", "0.001", "g", -49_999_999L)]
    [DataRow(InventoryDimension.Volume, "receipt", "0.001", "ml", 1L)]
    [DataRow(InventoryDimension.Volume, "waste", "1.001", "ml", -1_001L)]
    [DataRow(InventoryDimension.Volume, "count", "1.999999", "L", -125_001L)]
    [DataRow(InventoryDimension.Count, "receipt", "2", "piece", 2L)]
    [DataRow(InventoryDimension.Count, "waste", "1", "piece", -1L)]
    [DataRow(InventoryDimension.Count, "count", "3", "piece", -4L)]
    public void PreviewAgreesWithPersistedServiceLedgerAtAtomicPrecision(InventoryDimension dimension, string operation, string quantity, string unit, long expectedDelta)
    {
        using var fixture = new Fixture();
        var item = dimension switch { InventoryDimension.Mass => fixture.Flour, InventoryDimension.Volume => fixture.Milk, _ => fixture.Bags };
        var amount = decimal.Parse(quantity, CultureInfo.InvariantCulture);
        var preview = InventoryOperationPreview.Calculate(fixture.Service.Detail(item.Id), operation, amount, unit);
        Assert.AreEqual(expectedDelta, preview.DeltaAtomic);
        var id = Guid.NewGuid();
        var persisted = operation switch
        {
            "receipt" => fixture.Service.ReceiveInventory(new(id, item.Id, InventoryLocationIds.Main, amount, unit)),
            "waste" => fixture.Service.RecordWaste(new(id, item.Id, InventoryLocationIds.Main, amount, unit, WasteReason.Damaged)),
            _ => fixture.Service.AdjustToPhysicalCount(new(id, item.Id, InventoryLocationIds.Main, amount, unit, AdjustmentReason.PhysicalCountVariance)).Operation!
        };
        var movement = persisted.Movements.Single();
        Assert.AreEqual(preview.BeforeAtomic, movement.BalanceBeforeAtomic);
        Assert.AreEqual(preview.DeltaAtomic, movement.DeltaAtomic);
        Assert.AreEqual(preview.AfterAtomic, movement.BalanceAfterAtomic);
        Assert.AreEqual(preview.AfterAtomic, fixture.Service.Detail(item.Id).Balance!.QuantityAtomic);
        fixture.AssertNoSales();
    }

    private static T Bound<T>(DependencyObject root, DependencyProperty property, string path) where T : FrameworkElement =>
        Descendants<T>(root).Single(x => x.IsVisible && BindingOperations.GetBinding(x, property)?.Path.Path == path);
    private static System.Windows.Controls.Button Button(DependencyObject root, string content) =>
        Descendants<System.Windows.Controls.Button>(root).Single(x => x.IsVisible && Equals(x.Content, content));
    private static DataGrid Grid(DependencyObject root, string name) => Descendants<DataGrid>(root).Single(x => x.IsVisible && AutomationProperties.GetName(x) == name);
    private static void AssertText(DependencyObject root, string text) => Assert.IsTrue(
        Descendants<TextBlock>(root).Any(x => x.IsVisible && x.ActualWidth > 0 && x.ActualHeight > 0 && x.Text == text), $"Expected measured visible text: {text}");
    private static void AssertError(InventoryError expected, Action action) =>
        Assert.AreEqual(expected, Assert.ThrowsExactly<InventoryApplicationException>(action).Error);
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static async Task WaitUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!done() && DateTime.UtcNow < deadline) await Task.Delay(35);
        Assert.IsTrue(done(), "The real asynchronous search must finish without Enter or SearchAsync from the test.");
    }
    private static void Capture(FrameworkElement view, string name)
    {
        var directory = Environment.GetEnvironmentVariable("CASAMIGA_TEST_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name)); encoder.Save(stream);
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 3, 20, 35, 0, TimeSpan.Zero); }
    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "CasaMigaInventoryUsability", Guid.NewGuid().ToString("N"));
        private readonly string password = Guid.NewGuid().ToString("N");
        private readonly Database database;
        private readonly int width; private readonly int height;
        private readonly string baseline;
        public FixedClock Clock { get; } = new();
        public InventoryService Service { get; }
        public InventoryItem Flour { get; }
        public InventoryItem Milk { get; }
        public InventoryItem Bags { get; }
        public Product Product { get; }
        public MainViewModel Model { get; } = new();
        public MainWindow Window { get; private set; } = null!;
        public InventoryViewModel Inventory => Model.Inventory!;
        public InventoryView View => Descendants<InventoryView>(Window).Single(x => x.IsVisible);

        public Fixture(int width = 1280, int height = 820)
        {
            this.width = width; this.height = height;
            Directory.CreateDirectory(directory); database = new(Path.Combine(directory, "casamiga.db")); database.Initialize();
            var work = new SqliteLocalUnitOfWork(database); var session = new UserSession();
            var terminal = TerminalConfiguration.LoadOrCreate(directory);
            work.Run(s => { s.EnsureTerminal(terminal, Clock.UtcNow); return true; });
            var identity = new IdentityService(work, new LocalPasswords(), Clock, session);
            identity.Bootstrap("inventory-ux", "Prueba aislada Inventario", password);
            Assert.IsTrue(identity.Login("inventory-ux", password));
            var catalog = new CatalogService(work, session, Clock, terminal.Id);
            Service = new(new SqliteInventoryStore(database, Clock), work, session, Clock, terminal.Id);
            Flour = Service.CreateInventoryItem("MP-HAR-001", "Harina", InventoryItemKind.RawMaterial, InventoryDimension.Mass);
            Milk = Service.CreateInventoryItem("MP-LECHE", "Leche", InventoryItemKind.RawMaterial, InventoryDimension.Volume);
            Bags = Service.CreateInventoryItem("EMP-BOLSA", "Bolsas", InventoryItemKind.Packaging, InventoryDimension.Count);
            Service.AddInventoryItemUnit(Flour.Id, "HAR-BULTO-25KG", "Bulto 25 kg", 25_000_000L);
            Service.RegisterOpeningBalance(new(Guid.NewGuid(), Flour.Id, InventoryLocationIds.Main, 50m, "kg", "Nota de prueba"));
            Service.RegisterOpeningBalance(new(Guid.NewGuid(), Milk.Id, InventoryLocationIds.Main, 2.125m, "L"));
            Service.RegisterOpeningBalance(new(Guid.NewGuid(), Bags.Id, InventoryLocationIds.Main, 7m, "piece"));
            var category = catalog.CreateCategory("Pruebas");
            Product = catalog.CreateProduct(category.Id, "PAN-PRUEBA", "Pan de prueba", "", new Money(1300));
            Service.LinkProductToInventoryItem(Product.Id, Flour.Id, 100_000L);
            baseline = InventorySnapshot();
        }
        public static async Task<Fixture> ShowAsync(int width = 1280, int height = 820)
        {
            var fixture = new Fixture(width, height);
            try
            {
                await fixture.Model.InitializeAsync(new FoundationOptions(fixture.directory, IsDevelopment: true));
                await fixture.Model.LoginAsync("inventory-ux", fixture.password); Assert.IsTrue(fixture.Model.CanNavigate, fixture.Model.Message);
                await fixture.Model.ShowInventoryAsync();
                fixture.Window = new MainWindow { DataContext = fixture.Model, Width = width, Height = height, Left = -10000, Top = -10000,
                    ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
                fixture.Window.Show(); fixture.Layout(); return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }
        public async Task OpenFlourAsync()
        {
            Inventory.SelectedItem = Inventory.Items.Single(x => x.Id == Flour.Id);
            await Inventory.OpenDetailAsync(); Layout(); Assert.IsTrue(Inventory.IsDetail, Inventory.Message);
        }
        public void Layout() => WpfTestHost.Layout(Window, width, height);
        public void AssertUnchanged() { Assert.AreEqual(baseline, InventorySnapshot(), "Search, preview and cancellation must not write inventory state."); AssertNoSales(); }
        public void AssertNoSales()
        {
            using var connection = database.Open(true);
            foreach (var table in new[] { "sale", "payment", "cash_session", "production_batch" })
            {
                using var command = connection.CreateCommand(); command.CommandText = $"SELECT count(*) FROM {table}";
                Assert.AreEqual(0L, (long)command.ExecuteScalar()!, table);
            }
        }
        private string InventorySnapshot()
        {
            using var connection = database.Open(true); var rows = new List<string>();
            foreach (var table in new[] { "inventory_balance", "inventory_operation", "inventory_movement", "inventory_item_unit", "product_inventory_link" })
            {
                using var command = connection.CreateCommand(); command.CommandText = $"SELECT * FROM {table} ORDER BY 1";
                using var reader = command.ExecuteReader();
                while (reader.Read()) rows.Add(table + "|" + string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "NULL" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture))));
            }
            return string.Join("\n", rows);
        }
        public void Dispose() { Window?.Close(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
