using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CasaMiga.Application;
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
        using var fixture = new Fixture();
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
            Assert.IsTrue(Descendants<Button>(window).Any(x => Equals(x.Content, "Reporte diario")));
            var navigation = Descendants<Button>(window).Single(x => Equals(x.Content, "Reporte diario"));
            Assert.IsTrue(navigation.Tag is string geometry && geometry.StartsWith("M", StringComparison.Ordinal));
            Assert.IsTrue(Descendants<System.Windows.Shapes.Path>(navigation).Any(), "The navigation entry must render its vector icon.");
            Assert.IsTrue(Descendants<Button>(window).Any(x => Equals(x.Content, "F11") && x.IsVisible));
        }
        finally { window.Close(); model.Logout(); }
    });

    private static bool VisibleText(DependencyObject root, string text) =>
        Descendants<TextBlock>(root).Any(x => x.IsVisible && x.Text == text);
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
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            var database = new Database(Path.Combine(Root, "casamiga.db")); database.Initialize();
            var work = new SqliteLocalUnitOfWork(database); var clock = new SystemClock(); var session = new UserSession();
            var identity = new IdentityService(work, new LocalPasswords(), clock, session);
            identity.Bootstrap("report-review", "Revisión reporte", Password);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
