using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class ProductionBuilderLayoutTests
{
    private const string LongDetail = "Una preparación no tiene todos sus insumos obligatorios o una alternativa activa. Texto sintético adicional para forzar varias líneas.";

    [TestMethod]
    public void ViewportShareIsBoundedAndIgnoresUnmeasuredViewports()
    {
        var converter = new ViewportShareConverter { Share = 0.5, Min = 250, Max = 640 };
        Assert.AreEqual(250d, converter.Convert(0d, typeof(double), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(250d, converter.Convert(double.PositiveInfinity, typeof(double), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(250d, converter.Convert(300d, typeof(double), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(400d, converter.Convert(800d, typeof(double), null!, CultureInfo.InvariantCulture));
        Assert.AreEqual(640d, converter.Convert(3000d, typeof(double), null!, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public Task ProductChooserGrowsWithViewportAndKeepsRowsCompact() => WpfTestHost.OnUi(() =>
    {
        var rows = Enumerable.Range(1, 36).Select(n => new
        {
            ProductName = $"Producto sintético {n}", Status = "RECETA INCOMPLETA", ProfileName = $"Perfil sintético {n}",
            VersionLabel = "Versión 1", Detail = LongDetail
        }).ToArray();
        var view = new ProductionView { DataContext = new { Ready = true, IsBuilder = true, FilteredProfiles = rows, Plan = new ObservableCollection<object>() } };
        using var host = new Host(view);

        Show(host, 1100, 850);
        var chooser = Grid(view, "Productos disponibles para producción");
        Assert.IsGreaterThan(180 + 150, chooser.ActualHeight, "The chooser must use the visible form height instead of the legacy 180 DIP.");
        Assert.IsTrue(chooser.EnableRowVirtualization);
        var renderedRows = Descendants<DataGridRow>(chooser).Where(x => x.IsVisible).ToArray();
        Assert.IsGreaterThanOrEqualTo(6, renderedRows.Length);
        Assert.IsTrue(renderedRows.All(x => x.ActualHeight <= 50.5), "Long explanations are limited to two lines per row.");
        var detail = Descendants<TextBlock>(chooser).First(x => x.Text == LongDetail);
        Assert.AreEqual(LongDetail, detail.ToolTip, "The complete explanation stays available.");
        Assert.AreEqual(TextTrimming.WordEllipsis, detail.TextTrimming);
        Assert.IsFalse(Descendants<DataGrid>(view).Any(x => x.IsVisible && AutomationProperties.GetName(x) == "Productos planeados para producción"), "An empty plan shows its empty state instead of a placeholder row.");

        Show(host, 1100, 560);
        Assert.IsGreaterThanOrEqualTo(250, chooser.ActualHeight, "A short window keeps an accessible list and scrolls the form instead.");

        Show(host, 640, 850);
        var status = chooser.Columns.Single(x => Equals(x.Header, "Estado"));
        var version = chooser.Columns.Single(x => Equals(x.Header, "Versión"));
        Assert.IsGreaterThanOrEqualTo(175, status.ActualWidth, "Fixed columns must not collapse in narrow windows.");
        Assert.IsGreaterThanOrEqualTo(95, version.ActualWidth);
        Assert.AreEqual(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(chooser), "Narrow windows scroll the table horizontally.");
    });

    [TestMethod]
    public Task ListCellsAreLimitedToTwoLinesWithFullTextTooltip() => WpfTestHost.OnUi(() =>
    {
        var rows = Enumerable.Range(1, 12).Select(n => new
        {
            Date = "16/10/2026 07:00", State = "Confirmada", Creator = "Administrador sintético con nombre muy largo para revisión", Terminal = "Terminal sintética",
            Products = "Bolillo, Telera, Baguette, Concha de vainilla, Concha de chocolate, Rol de canela", Result = LongDetail, OutputResult = LongDetail
        }).ToArray();
        var view = new ProductionView { DataContext = new { Ready = true, IsHistory = true, Rows = rows } };
        using var host = new Host(view);
        Show(host, 1100, 850);
        var history = Grid(view, "Historial de producción");
        var rendered = Descendants<DataGridRow>(history).Where(x => x.IsVisible).ToArray();
        Assert.IsNotEmpty(rendered);
        Assert.IsTrue(rendered.All(x => x.ActualHeight <= 50.5), "History rows keep a uniform compact height.");
        Assert.IsTrue(Descendants<TextBlock>(history).Any(x => x.Text == LongDetail && Equals(x.ToolTip, LongDetail)));
    });

    private static void Show(Host host, double width, double height) { host.Window.Width = width; host.Window.Height = height; WpfTestHost.Layout(host.Window, width, height); }

    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(UIElement content)
        {
            Window = new Window { Content = content, WindowStyle = WindowStyle.None, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            Window.Show();
        }
        public void Dispose() => Window.Close();
    }

    private static DataGrid Grid(DependencyObject root, string name) => Descendants<DataGrid>(root).Single(x => AutomationProperties.GetName(x) == name);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
