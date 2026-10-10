using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

/// <summary>Lo que la caja tomó de la web (docs/PLAN_COMPLETAR.md, D1 y D4) se dibuja dentro del marco de QAv1.</summary>
[TestClass, DoNotParallelize]
public sealed class DesktopParityPresentationTests
{
    [TestMethod]
    public Task ProfitTabAndSalesHistoryFiltersAreDrawn() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = new Fixture();
        var window = OffscreenWindow(new MainWindow { DataContext = fixture.Model });
        try
        {
            window.Show(); WpfTestHost.Layout(window, 1280, 820);
            await fixture.InitializeAsync(); WpfTestHost.Layout(window, 1280, 820);

            await fixture.Model.ShowReportsAsync(); WpfTestHost.Layout(window, 1280, 820);
            var reports = fixture.Model.Reports!;
            Assert.IsTrue(reports.CanViewProfit);
            await reports.ShowAsync(ReportTab.Profit); WpfTestHost.Layout(window, 1280, 820);
            Assert.IsTrue(reports.HasProfit, reports.Message);
            var perBread = Descendants<DataGrid>(window).Where(g => g.IsVisible).ToList();
            Assert.IsTrue(perBread.Count >= 2 && perBread.All(g => g.ActualHeight > 0), "Por pan y Costo por pieza se dibujan.");
            StringAssert.Contains(reports.MissingCosts, "sin costo");
            Capture(window, "reportes-ganancia.png");

            await fixture.Model.ShowSalesHistoryAsync(); WpfTestHost.Layout(window, 1280, 820);
            var history = fixture.Model.SalesHistory!;
            Assert.AreEqual(3, history.Sales.Count);
            history.SelectedPaymentMethod = SalesHistoryViewModel.PaymentMethods.Single(m => m.Value == "CARD");
            history.ProductFilter = "concha";
            await history.ApplyFiltersAsync(); WpfTestHost.Layout(window, 1280, 820);
            Assert.AreEqual(1, history.Sales.Count, history.Message);
            Assert.AreEqual("Todos · Tarjeta · con «concha» · 1 venta", history.FilterSummary);
            var method = Descendants<ComboBox>(window).Single(c => c.IsVisible && AutomationProperties.GetName(c) == "Forma de pago");
            Assert.IsTrue(method.ActualWidth > 0, "El filtro de forma de pago se dibuja.");
            Capture(window, "historial-filtros.png");
        }
        finally { window.Close(); }
    });

    private static void Capture(Window window, string name)
    {
        var folder = Environment.GetEnvironmentVariable("CASAMIGA_CONSIGNMENT_EVIDENCE"); if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(Path.Combine(folder, name)); png.Save(file);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T item) yield return item;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static T OffscreenWindow<T>(T window) where T : Window
    {
        window.Width = 1280; window.Height = 820; window.Left = -10000; window.Top = -10000;
        window.ShowInTaskbar = false; window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual;
        return window;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "CasaMigaParityUi", Guid.NewGuid().ToString("N"));
        public MainViewModel Model { get; } = new();
        public async Task InitializeAsync()
        {
            await Model.InitializeAsync(new FoundationOptions(directory, IsDevelopment: true));
            var password = "Aa1!" + Guid.NewGuid().ToString("N")[..12];
            await Model.BootstrapAsync("admin", "Administrador", password, password);
            await Model.LoginAsync("admin", password); Assert.IsTrue(Model.CanNavigate, Model.Message);

            // Tres ventas de hoy con los servicios reales sobre la misma base: dos en efectivo y una con tarjeta.
            var clock = new SystemClock(); var work = new SqliteLocalUnitOfWork(new Database(Path.Combine(directory, "casamiga.db")));
            var session = new UserSession(); Assert.IsTrue(new IdentityService(work, new LocalPasswords(), clock, session).Login("admin", password));
            var terminal = TerminalConfiguration.LoadOrCreate(directory);
            var catalog = new CatalogService(work, session, clock, terminal.Id, true);
            var category = catalog.CreateCategory("Pan dulce").Id;
            var concha = catalog.CreateProduct(category, "C1", "Concha de vainilla", "", new Money(1900));
            var bolillo = catalog.CreateProduct(category, "B1", "Bolillo", "", new Money(500));
            var cash = new CashService(work, session, clock).Open(terminal.Id, new Money(50000)).Session;
            void Sell(Product product, int quantity, PaymentMethod method) => work.Run(store =>
            {
                var total = product.SalePrice.Cents * quantity;
                store.AddSale(new Sale(Guid.NewGuid(), Guid.NewGuid(), session.Current!.Id, terminal.Id, cash.Id, store.NextSaleSequence(terminal.Id), clock.UtcNow,
                    [new SaleItem(product.Id, product.Name, product.SalePrice, quantity)], Guid.NewGuid(), new Money(total), method, method == PaymentMethod.Cash ? null : "4242"));
                return true;
            });
            Sell(concha, 2, PaymentMethod.Cash); Sell(bolillo, 4, PaymentMethod.Cash); Sell(concha, 3, PaymentMethod.Card);
        }
        public void Dispose() { try { Directory.Delete(directory, true); } catch (IOException) { } }
    }
}
