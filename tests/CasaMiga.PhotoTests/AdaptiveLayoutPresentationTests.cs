using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class AdaptiveLayoutPresentationTests
{
    [TestMethod]
    public void NarrowViewportsReduceSidebarAndMargins()
    {
        var shell = new MainViewModel();
        shell.SetViewportWidth(853); // 1280 px at 150% scaling.
        Assert.IsTrue(shell.IsCompactHeader);
        Assert.AreEqual(200d, shell.SidebarWidth);
        Assert.AreEqual(new Thickness(12), shell.ShellContentMargin);
        shell.SetViewportWidth(1920);
        Assert.IsFalse(shell.IsCompactHeader);
        Assert.AreEqual(248d, shell.SidebarWidth);
        Assert.AreEqual(new Thickness(24, 20, 24, 20), shell.ShellContentMargin);
    }

    [TestMethod]
    public Task PosColumnsRemainAccessibleWithExpandedCompactSidebar() => WpfTestHost.OnUi(() =>
    {
        var view = new PosView { DataContext = new { Ready = true } };
        WpfTestHost.Layout(view, 624, 560); // 820 DIP window - 180 sidebar - 16 content margins.
        var layout = (Grid)view.Content;
        Assert.IsGreaterThanOrEqualTo(320, layout.ColumnDefinitions[0].ActualWidth);
        Assert.IsGreaterThanOrEqualTo(300, layout.ColumnDefinitions[1].ActualWidth);
        Assert.IsLessThanOrEqualTo(624, layout.ColumnDefinitions[0].ActualWidth + layout.ColumnDefinitions[1].ActualWidth);
    });

    [TestMethod]
    public Task ListsExpandIntoAvailableHeightInsteadOfKeepingLegacyFixedHeights() => WpfTestHost.OnUi(() =>
    {
        var rows = Enumerable.Range(1, 30).Select(number => new
        {
            Code = $"ART-{number:000}", Name = $"Artículo de prueba {number}", Kind = "Materia prima",
            Balance = "1 kg", Unit = "kg", Status = "Activo", Product = "No"
        }).ToArray();
        AssertList(new InventoryView { DataContext = new { Ready = true, IsList = true, Items = rows } }, "Artículos de inventario", 310);
        AssertList(new CatalogView { DataContext = new { Ready = true, IsProducts = true, Products = rows } }, "Productos", 190);
        AssertList(new CatalogView { DataContext = new { Ready = true, IsCategories = true, Categories = rows } }, "Categorías", 190);
        AssertList(new RecipeView { DataContext = new { Ready = true, IsList = true, Formulas = rows } }, "Recetas", 360);
        AssertList(new ProductionView { DataContext = new { Ready = true, IsDrafts = true, Rows = rows } }, "Producciones en borrador", 360);
    });

    private static void AssertList(UserControl view, string name, double oldHeight)
    {
        WpfTestHost.Layout(view, 1100, 850);
        var grid = Descendants<DataGrid>(view).Single(item => AutomationProperties.GetName(item) == name);
        Assert.IsGreaterThan(oldHeight + 80, grid.ActualHeight, $"{name} should fill the available viewport.");
        Assert.IsTrue(grid.EnableRowVirtualization, $"{name} must keep row virtualization.");
        WpfTestHost.Layout(view, 1100, 560);
        Assert.IsGreaterThan(100, grid.ActualHeight, $"{name} must retain an accessible list on a short viewport.");
    }

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
