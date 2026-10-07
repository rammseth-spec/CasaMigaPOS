using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class ConsignmentPresentationTests
{
    [TestMethod]
    public Task StoreTemplateAndDeliveryRowsAreDrawn() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = new Fixture(); await fixture.InitializeAsync();
        var model = fixture.Model.Consignment!;
        var view = new ConsignmentView { DataContext = model };
        var window = OffscreenWindow(new Window { Content = new ScrollViewer { Content = view } });
        try
        {
            window.Show(); WpfTestHost.Layout(view);
            await model.ShowAsync(ConsignmentTab.Stores);
            await model.SelectStoreAsync(model.Stores.Single());
            WpfTestHost.Layout(view);
            Assert.AreEqual("", model.Message);
            var template = List(view, "Charola habitual");
            Assert.AreEqual(23, template.Items.Count, "La charola habitual muestra cada pan con precio de charola.");
            Assert.IsTrue(template.ActualHeight > 0, "Las filas de la charola habitual se dibujan.");
            Assert.AreEqual(12, template.Items.Cast<TrayLineView>().Sum(l => l.Quantity));

            await model.ShowAsync(ConsignmentTab.Route);
            await model.StartDeliverAsync(model.Route.Single());
            WpfTestHost.Layout(view);
            // Como el Punto de venta: tarjetas para todos los panes con precio y, a la derecha, sólo los que van en la charola.
            Assert.IsTrue(model.IsDeliverWorkspace); Assert.IsFalse(model.ShowTabs);
            var tiles = List(view, "Panes para la charola");
            Assert.AreEqual(23, tiles.Items.Count); Assert.IsTrue(tiles.ActualHeight > 0);
            var tray = List(view, "Piezas de la charola");
            Assert.AreEqual(3, tray.Items.Count, "La charola habitual llega precargada."); Assert.IsTrue(tray.ActualHeight > 0);
            StringAssert.Contains(model.DeliverSummary, "12 piezas");

            model.DeliverSearch = "AZUCAR"; WpfTestHost.Layout(view);
            Assert.AreEqual("Dona de azúcar", ((TrayLineView)tiles.Items.Cast<object>().Single()).Name, "Busca sin mayúsculas ni acentos.");
            model.DeliverSearch = "P12"; Assert.AreEqual("Pan de prueba 12", model.DeliverTiles.Single().Name, "También por SKU.");
            model.DeliverSearch = ""; model.SelectCategory("Dona"); WpfTestHost.Layout(view);
            Assert.AreEqual(1, tiles.Items.Count); Assert.IsTrue(model.DeliverCategories.Single(c => c.Name == "Dona").Selected);

            // Tocar la tarjeta suma una pieza y aparece en la charola con su burbuja.
            var tile = Descendants<Button>(tiles).Single(b => AutomationProperties.GetName(b) == "Agregar Dona de azúcar a la charola");
            for (var i = 0; i < 3; i++) ((IInvokeProvider)new ButtonAutomationPeer(tile).GetPattern(PatternInterface.Invoke)!).Invoke();
            WpfTestHost.Layout(view);
            Assert.AreEqual(4, tray.Items.Count); Assert.AreEqual(15, model.DeliverPieces);
            Assert.AreEqual(3, model.TrayLines.Single(l => l.Name == "Dona de azúcar").Quantity);
            model.SelectCategory(null); Assert.AreEqual(23, tiles.Items.Count);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public Task PickingAStoreFromTheListShowsItsTemplateInTheShell() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = new Fixture();
        // Igual que App.OnStartup: la ventana se muestra antes de inicializar (la pantalla nace sin datos).
        var window = OffscreenWindow(new MainWindow { DataContext = fixture.Model });
        try
        {
            window.Show(); WpfTestHost.Layout(window, 1280, 820);
            await fixture.InitializeAsync(); WpfTestHost.Layout(window, 1280, 820);
            var model = fixture.Model.Consignment!;
            // Mismo recorrido que en la revisión manual: entregar, ver el reporte y luego elegir la tienda.
            await model.StartDeliverAsync(model.Route.Single()); WpfTestHost.Layout(window, 1280, 820);
            await model.ShowAsync(ConsignmentTab.Report); WpfTestHost.Layout(window, 1280, 820);
            await model.ShowAsync(ConsignmentTab.Stores); WpfTestHost.Layout(window, 1280, 820);
            var stores = Descendants<ListBox>(window).Single(l => AutomationProperties.GetName(l) == "Tiendas");
            stores.SelectedIndex = 0;
            for (var i = 0; i < 50 && (model.Busy || model.SelectedStore is null); i++) await Task.Delay(20);
            WpfTestHost.Layout(window, 1280, 820);
            Assert.IsNotNull(model.SelectedStore, model.Message);
            var template = List(window, "Charola habitual");
            Assert.AreEqual(23, template.Items.Count, "Al elegir la tienda en la lista se ve su charola habitual.");
            Assert.IsTrue(template.ActualHeight > 0);
        }
        finally { window.Close(); }
    });

    private static ItemsControl List(DependencyObject root, string name) =>
        Descendants<ItemsControl>(root).Single(i => i is not ListBox && AutomationProperties.GetName(i) == name);

    private static T OffscreenWindow<T>(T window) where T : Window
    {
        window.Width = 1280; window.Height = 820; window.Left = -10000; window.Top = -10000;
        window.ShowInTaskbar = false; window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual;
        return window;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "CasaMigaConsignmentUi", Guid.NewGuid().ToString("N"));
        public MainViewModel Model { get; } = new();
        public async Task InitializeAsync()
        {
            await Model.InitializeAsync(new FoundationOptions(directory, IsDevelopment: true));
            var password = "Aa1!" + Guid.NewGuid().ToString("N")[..12];
            await Model.BootstrapAsync("charolas", "Prueba de charolas", password, password);
            await Model.LoginAsync("charolas", password); Assert.IsTrue(Model.CanNavigate, Model.Message);
            Assert.IsTrue(Model.CanViewConsignment);

            // Catálogo, precio de charola, tienda y charola habitual con los servicios reales sobre la misma base.
            var clock = new SystemClock(); var work = new SqliteLocalUnitOfWork(new Database(Path.Combine(directory, "casamiga.db")));
            var session = new UserSession(); var identity = new IdentityService(work, new LocalPasswords(), clock, session);
            Assert.IsTrue(identity.Login("charolas", password));
            var terminal = TerminalConfiguration.LoadOrCreate(directory);
            var catalog = new CatalogService(work, session, clock, terminal.Id, true);
            var category = catalog.CreateCategory("Pan dulce").Id;
            var trays = new ConsignmentService(work, session, clock, terminal.Id, TimeZoneInfo.Local);
            // Como el catálogo real: muchos panes con precio de charola y una charola habitual de varios panes.
            var breads = Enumerable.Range(1, 22).Select(i => catalog.CreateProduct(category, $"P{i}", $"Pan de prueba {i:00}", "", new Money(1300))).ToList();
            breads.Add(catalog.CreateProduct(catalog.CreateCategory("Dona").Id, "D1", "Dona de azúcar", "", new Money(1300)));
            foreach (var bread in breads) trays.SetPrice(bread.Id, new Money(1050));
            var store = trays.SaveStore(null, "Abarrotes Lupita", "Lupita", "", "");
            trays.SaveTemplate(store.Id, [new(breads[3].Id, 4), new(breads[9].Id, 4), new(breads[15].Id, 4)]);
            await Model.ShowConsignmentAsync(); Assert.IsTrue(Model.IsConsignment, Model.Message);
        }
        public void Dispose() { try { Directory.Delete(directory, true); } catch (IOException) { } }
    }
}
