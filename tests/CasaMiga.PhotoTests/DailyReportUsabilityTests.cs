using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class DailyReportUsabilityTests
{
    [TestMethod]
    public Task EmptyReportDistinguishesConfirmedZeroesFromUnavailableAreasAndUsesRobustDateEntry() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = new Fixture();
        var model = new MainViewModel();
        await model.InitializeAsync(new(fixture.Root, true));
        await model.LoginAsync("report-review", fixture.Password);
        Assert.IsTrue(model.CanViewDailyReport);
        await model.ShowDailyReportAsync();
        Assert.IsTrue(model.IsDailyReport, model.Message);
        Assert.IsNotNull(model.DailyReport);
        Assert.AreEqual("$0.00", model.DailyReport.Sales);
        Assert.AreEqual("0", model.DailyReport.Tickets);
        Assert.AreEqual("0", model.DailyReport.Pieces);
        Assert.AreEqual("$0.00", model.DailyReport.AverageTicket);
        Assert.IsTrue(model.DailyReport.HasNoSales);
        Assert.IsTrue(model.DailyReport.HasNoCashSessions);
        Assert.AreEqual("No disponible", model.DailyReport.OpeningFund);

        var view = new DailyReportView { DataContext = model.DailyReport };
        var window = new Window { Content = view, Width = 1100, Height = 900, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        try
        {
            window.Show(); WpfTestHost.Layout(window, 1100, 900);
            Assert.HasCount(1, Descendants<DateEntryControl>(view));
            Assert.IsTrue(VisibleText(view, "No hubo ventas registradas este día."));
            Assert.IsTrue(VisibleText(view, "Producción"));
            Assert.IsGreaterThanOrEqualTo(2, Descendants<TextBlock>(view).Count(x => x.IsVisible && x.Text == "No disponible"));
            Assert.IsTrue(VisibleText(view, "Pendiente de configuración"));
            Assert.IsTrue(VisibleText(view, "Se habilitará al completar recetas, inventario y reservas."));
        }
        finally { window.Close(); model.Logout(); }
    });

    [TestMethod]
    [DataRow(1280, 820)]
    [DataRow(1920, 1080)]
    [DataRow(3440, 1440)]
    public Task FullShellKeepsDailyReportReadableWithoutGlobalHorizontalScroll(int width, int height) => WpfTestHost.OnUi(async () =>
    {
        using var fixture = new Fixture(seedSales: true);
        var model = new MainViewModel();
        await model.InitializeAsync(new(fixture.Root, true));
        await model.LoginAsync("report-review", fixture.Password);
        await model.ShowDailyReportAsync();
        var window = new MainWindow { DataContext = model, Width = width, Height = height, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        try
        {
            window.Show(); WpfTestHost.Layout(window, width, height);
            var view = Descendants<DailyReportView>(window).Single(x => x.IsVisible);
            Assert.IsTrue(view.ActualWidth > 500);
            var global = Descendants<ScrollViewer>(window).First(x => x.IsVisible && x.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled);
            Assert.AreEqual(0d, global.ScrollableWidth, 0.5);
            var products = Descendants<DataGrid>(view).Single(x => AutomationProperties.GetName(x) == "Productos más vendidos");
            Assert.HasCount(4, products.Columns);
            Assert.AreEqual("Producto", products.Columns[1].Header);
            Assert.IsTrue(VisibleText(view, "Ventas por hora"));
            Assert.IsTrue(model.DailyReport!.HasHourlySales);
            Assert.IsTrue(Descendants<Border>(view).Any(x => AutomationProperties.GetName(x).Contains("ticket", StringComparison.Ordinal)));
            // Riel del diseño QAv1: nombre corto visible y el completo en el nombre accesible; el ícono es una geometría del tema.
            var navigation = Descendants<Button>(window).Single(x => AutomationProperties.GetName(x) == "Ir a Reporte diario");
            Assert.AreEqual("Diario", navigation.Content);
            Assert.IsTrue(navigation.Tag is Geometry);
            Assert.IsTrue(Descendants<System.Windows.Shapes.Path>(navigation).Any(), "The navigation entry must render its vector icon.");
            var fullScreen = (Button)window.FindName("FullScreenButton");
            Assert.IsTrue(fullScreen.IsVisible);
            Assert.AreEqual("Pantalla completa · F11", fullScreen.ToolTip);
        }
        finally { window.Close(); model.Logout(); }
    });

    [TestMethod]
    public Task FullScreenControlExplainsClickF11AndEscapeStates() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = new Fixture();
        var model = new MainViewModel();
        await model.InitializeAsync(new(fixture.Root, true));
        await model.LoginAsync("report-review", fixture.Password);
        var window = new MainWindow { DataContext = model, Width = 1280, Height = 820, ShowInTaskbar = false };
        try
        {
            window.Show(); WpfTestHost.Layout(window, 1280, 820);
            var button = (Button)window.FindName("FullScreenButton");
            // Botón de sólo ícono del diseño QAv1: la ayuda emergente explica el estado y F11.
            Assert.AreEqual("Pantalla completa · F11", button.ToolTip);
            Assert.AreEqual("Alternar pantalla completa", AutomationProperties.GetName(button));

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WpfTestHost.Layout(window, 1280, 820);
            Assert.AreEqual(WindowStyle.None, window.WindowStyle);
            Assert.AreEqual("Salir de pantalla completa · F11", button.ToolTip);

            Send(window, Key.F11);
            Assert.AreEqual(WindowStyle.SingleBorderWindow, window.WindowStyle);
            Assert.AreEqual("Pantalla completa · F11", button.ToolTip);
            Send(window, Key.F11);
            Send(window, Key.Escape);
            Assert.AreEqual(WindowStyle.SingleBorderWindow, window.WindowStyle);
            Assert.AreEqual("Pantalla completa · F11", button.ToolTip);
        }
        finally { window.Close(); model.Logout(); }
    });

    [TestMethod]
    public void CashSessionCopyShowsOpeningClosingDurationAndHumanResponsibility()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("DailyReportView", TimeSpan.FromHours(-6), "DailyReportView", "DailyReportView");
        var opened = new DateTimeOffset(2026, 10, 9, 14, 0, 0, TimeSpan.Zero);
        var closed = opened.AddHours(12).AddMinutes(15);
        var row = new DailyCashSessionSummaryView(new(
            Guid.NewGuid(), "CAJA PRINCIPAL", "Administradora", null, opened, closed, false,
            new Money(50000), new Money(20000), new Money(70000), new Money(70000), new Money(0), null), zone);

        Assert.AreEqual("08:00 → 20:15", row.Journey);
        Assert.AreEqual("Apertura: 08:00", row.Opening);
        Assert.AreEqual("Cierre: 20:15", row.Closing);
        Assert.AreEqual("Duración: 12 h 15 min", row.Duration);
        Assert.AreEqual("Responsable: Administradora", row.Responsible);
    }

    private static bool VisibleText(DependencyObject root, string text) =>
        Descendants<TextBlock>(root).Any(x => x.IsVisible && x.Text == text);
    private static void Send(Window window, Key key)
    {
        var source = PresentationSource.FromVisual(window)!;
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T value) yield return value;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "CasaMigaDailyReportWpf", Guid.NewGuid().ToString("N"));
        public string Password { get; } = "DailyReport-Review!2026";
        public Fixture(bool seedSales = false)
        {
            Directory.CreateDirectory(Root);
            var database = new Database(Path.Combine(Root, "casamiga.db")); database.Initialize();
            var work = new SqliteLocalUnitOfWork(database); var clock = new SystemClock(); var session = new UserSession();
            var identity = new IdentityService(work, new LocalPasswords(), clock, session);
            identity.Bootstrap("report-review", "Revisión reporte", Password);
            if (!seedSales) return;
            Assert.IsTrue(identity.Login("report-review", Password));
            var terminal = TerminalConfiguration.LoadOrCreate(Root);
            work.Run(store => { store.EnsureTerminal(terminal, clock.UtcNow); return true; });
            var catalog = new CatalogService(work, session, clock, terminal.Id);
            var category = catalog.CreateCategory("Reporte");
            var product = catalog.CreateProduct(category.Id, "REPORT-WPF", "Concha de prueba", "", new Money(1300));
            var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZoneInfo.Local).DateTime);
            var localOpening = localDay.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Unspecified);
            var openedAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localOpening, TimeZoneInfo.Local), TimeSpan.Zero);
            var cash = CashSession.Open(Guid.NewGuid(), terminal.Id, session.Current!.Id, openedAt, new Money(50000));
            work.Run(store => { store.AddCashSession(cash); return true; });
            AddSale(work, session.Current.Id, terminal.Id, cash.Id, product, openedAt.AddHours(1).AddMinutes(10), 2);
            AddSale(work, session.Current.Id, terminal.Id, cash.Id, product, openedAt.AddHours(2).AddMinutes(20), 1);
            session.SignOut();
        }
        private static void AddSale(SqliteLocalUnitOfWork work, Guid user, Guid terminal, Guid cash, Product product, DateTimeOffset at, int quantity) => work.Run(store =>
        {
            var total = checked(product.SalePrice.Cents * quantity);
            store.AddSale(new Sale(Guid.NewGuid(), Guid.NewGuid(), user, terminal, cash,
                store.NextSaleSequence(terminal), at,
                [new SaleItem(product.Id, product.Name, product.SalePrice, quantity)],
                Guid.NewGuid(), new Money(total)));
            return true;
        });
        public void Dispose() => Directory.Delete(Root, true);
    }
}
