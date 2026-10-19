using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class TableReadabilityPresentationTests
{
    [TestMethod]
    public Task SharedTableSystemWrapsHeadersAndLongValuesWithoutFixedRows() => WpfTestHost.OnUi(() =>
    {
        foreach (var (width, height, label) in new[]
        {
            (1280d, 820d, "1280×820"),
            (1920d, 1080d, "1920×1080"),
            (3440d, 1440d, "3440×1440"),
            (1920d, 1080d, "F11")
        })
        {
            var grid = new DataGrid
            {
                Width = 720,
                Height = 260,
                CanUserResizeColumns = false,
                ItemsSource = new[] { new TableRow("MP-HAR-001", "Harina panadera de fuerza para producción diaria", "Motivo: Producción multiproducto registrada\nReferencia: Lote de producción local\nDetalle: salida y consumo confirmados") }
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Disabled);
            var textStyle = (Style)System.Windows.Application.Current.FindResource("CasaMigaTableTextStyle");
            var headerStyle = (Style)System.Windows.Application.Current.FindResource("CasaMigaTableTextHeaderStyle");
            grid.Columns.Add(new DataGridTextColumn { Header = "Código de artículo", Binding = new Binding(nameof(TableRow.Code)), Width = new DataGridLength(150), ElementStyle = textStyle, HeaderStyle = headerStyle });
            grid.Columns.Add(new DataGridTextColumn { Header = "Nombre completo", Binding = new Binding(nameof(TableRow.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 190, ElementStyle = textStyle, HeaderStyle = headerStyle });
            grid.Columns.Add(new DataGridTextColumn { Header = "Detalle operativo", Binding = new Binding(nameof(TableRow.Detail)), Width = new DataGridLength(1.25, DataGridLengthUnitType.Star), MinWidth = 220, ElementStyle = textStyle, HeaderStyle = headerStyle });
            var window = new Window { Content = grid, Width = width, Height = height, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            try
            {
                window.Show(); WpfTestHost.Layout(window, width, height);
                Assert.AreEqual(48d, grid.MinRowHeight, label);
                Assert.IsFalse(grid.CanUserReorderColumns, label);
                Assert.IsFalse(grid.CanUserResizeColumns, label);
                Assert.AreEqual(ScrollBarVisibility.Disabled, ScrollViewer.GetHorizontalScrollBarVisibility(grid), label);
                var row = Descendants<DataGridRow>(grid).Single();
                Assert.IsTrue(double.IsNaN(row.Height), label);
                Assert.IsGreaterThan(48d, row.ActualHeight, label);
                var headerTexts = Descendants<DataGridColumnHeader>(grid).SelectMany(Descendants<TextBlock>).Where(x => !string.IsNullOrWhiteSpace(x.Text)).ToArray();
                Assert.AreEqual(3, headerTexts.Length, label);
                Assert.IsTrue(headerTexts.All(x => x.TextWrapping == TextWrapping.Wrap), label);
                var detail = Descendants<TextBlock>(row).Single(x => x.Text.Contains("Producción multiproducto", StringComparison.Ordinal));
                Assert.AreEqual(TextWrapping.Wrap, detail.TextWrapping, label);
                var cells = Descendants<DataGridCell>(row).ToArray();
                Assert.IsTrue(cells.All(x => x.VerticalContentAlignment == VerticalAlignment.Center), label);
                Assert.IsTrue(cells.All(x => x.FocusVisualStyle is not null), label);
            }
            finally { window.Close(); }
        }
    });

    [TestMethod]
    public Task OperationalViewsUseSharedResponsiveTableContract() => WpfTestHost.OnUi(() =>
    {
        var views = new UserControl[] { new InventoryView(), new ProductionView(), new RecipeView(), new SalesHistoryView(), new DailyReportView(), new CashHistoryView(), new CatalogView() };
        foreach (var view in views)
        {
            WpfTestHost.Layout(view, 1060, 1800);
            foreach (var grid in Descendants<DataGrid>(view))
            {
                Assert.AreEqual(48d, grid.MinRowHeight, $"{view.GetType().Name}: {AutomationProperties.GetName(grid)}");
                Assert.IsFalse(grid.CanUserReorderColumns, $"{view.GetType().Name}: {AutomationProperties.GetName(grid)}");
                Assert.IsTrue(double.IsNaN(grid.RowHeight), $"{view.GetType().Name}: {AutomationProperties.GetName(grid)}");
                Assert.IsTrue(double.IsNaN(grid.ColumnHeaderHeight), $"{view.GetType().Name}: {AutomationProperties.GetName(grid)}");
                Assert.IsTrue(grid.Columns.OfType<DataGridTextColumn>().All(x => x.ElementStyle is not null), $"{view.GetType().Name}: every text column uses the wrapping cell style");
                Assert.IsTrue(grid.Columns.Where(x => x.Header is string).All(x => x.HeaderStyle is not null), $"{view.GetType().Name}: every text header uses the wrapping header style");
            }
        }
        var inventory = views.OfType<InventoryView>().Single();
        var inventoryMain = Grid(inventory, "Artículos de inventario");
        CollectionAssert.AreEqual(new[] { "Código", "Nombre", "Tipo", "Existencia actual", "Unidad", "Estado", "Producto" }, inventoryMain.Columns.Select(x => x.Header?.ToString()).ToArray());
        Assert.IsTrue(inventoryMain.Columns.All(x => x.MinWidth >= 70));
        var associations = Grid(inventory, "Asociaciones de productos");
        CollectionAssert.AreEqual(new[] { "Producto", "SKU", "Cantidad por venta" }, associations.Columns.Select(x => x.Header?.ToString()).ToArray());
        var movements = Grid(inventory, "Historial de inventario");
        CollectionAssert.AreEqual(new[] { "Fecha", "Operación", "Movimiento", "Existencia", "Usuario", "Detalle" }, movements.Columns.Select(x => x.Header?.ToString()).ToArray());
        Assert.IsTrue(movements.Columns.All(x => x.MinWidth >= 105));
        var production = views.OfType<ProductionView>().Single();
        CollectionAssert.AreEqual(new[] { "Producto", "Piezas planeadas", "Piezas reales", "Comparación" }, Grid(production, "Resultado real de producción").Columns.Select(x => x.Header?.ToString()).ToArray());
        var cash = Grid(views.OfType<CashHistoryView>().Single(), "Cierres anteriores");
        Assert.IsTrue(cash.Columns.All(x => x.MinWidth >= 100));
        var sales = Grid(views.OfType<SalesHistoryView>().Single(), "Historial de ventas");
        Assert.AreEqual(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(sales));
        Assert.AreEqual(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(inventoryMain));
        Assert.AreEqual(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(cash));
        var daily = Grid(views.OfType<DailyReportView>().Single(), "Productos más vendidos");
        Assert.AreEqual(ScrollBarVisibility.Disabled, ScrollViewer.GetHorizontalScrollBarVisibility(daily));
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static DataGrid Grid(DependencyObject root, string name) => Descendants<DataGrid>(root).Single(x => AutomationProperties.GetName(x) == name);

    private sealed record TableRow(string Code, string Name, string Detail);
}
