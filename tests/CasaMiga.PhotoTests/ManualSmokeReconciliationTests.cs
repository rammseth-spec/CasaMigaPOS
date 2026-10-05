using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class ManualSmokeReconciliationTests
{
    [TestMethod]
    public Task RecipeListInFinalShellShowsCurrentModeEmptyStateAndDisabledDetail() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await ShellFixture.CreateAsync();
        await fixture.Model.ShowRecipesAsync();
        fixture.Layout();
        var view = fixture.VisibleView<RecipeView>();

        Assert.AreSame(fixture.Model.Recipes, view.DataContext, "The shell must bind its actual recipe model.");
        AssertRenderedText(view, "Puedes crear y editar recetas y perfiles");
        Assert.IsFalse(Descendants<TextBlock>(view).Any(x => x.IsVisible && x.Text == "Configuración habilitada"));
        AssertRenderedText(view, "No hay recetas registradas.");
        Assert.IsFalse(VisibleButton(view, "Ver detalle").IsEnabled);
        Assert.IsTrue(VisibleButton(view, "Nueva receta").IsEnabled, "Ready/permissions must not disable the whole view and mask the selection assertion.");
        Capture(fixture.Window, "r1-recipes-empty.png");

        await fixture.Model.Recipes!.OpenFormulaAsync();
        fixture.Layout();
        Assert.IsTrue(fixture.Model.Recipes.IsList);
        AssertRenderedText(view, "Selecciona una receta.");
        Assert.IsFalse(VisibleButton(view, "Ver detalle").IsEnabled, "The defensive validation must not enable an invalid action.");
        fixture.AssertNoCommercialEffects();
    });

    [TestMethod]
    public Task RecipeDimensionPopupInFinalShellRendersAllSpanishLabelsAndKeepsEnumSelection() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await ShellFixture.CreateAsync();
        await fixture.Model.ShowRecipesAsync();
        fixture.Layout();
        var view = fixture.VisibleView<RecipeView>();
        VisibleButton(view, "Nueva receta").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        fixture.Model.Recipes!.Code="DIMENSION-UX";fixture.Model.Recipes.Name="Receta sintética de dimensiones";
        await fixture.Model.Recipes.CreateFormulaAsync();fixture.Model.Recipes.NewFormulaVersion();
        fixture.Layout();
        Assert.IsTrue(fixture.Model.Recipes!.IsFormulaEditor);
        var combo = Descendants<ComboBox>(view).Single(x => x.IsVisible && AutomationProperties.GetName(x) == "Dimensión del rendimiento");
        combo.BringIntoView();
        combo.Focus();
        Keyboard.Focus(combo);
        fixture.Layout();
        combo.IsDropDownOpen = true;
        fixture.Layout();
        try
        {
            Assert.IsTrue(combo.IsDropDownOpen, "Exercise a real loaded popup, not just ItemsSource objects.");
            var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
            Assert.IsTrue(popup.IsOpen);
            Assert.IsNotNull(popup.Child);
            foreach (var text in new[] { "Masa", "Volumen", "Piezas" }) AssertRenderedText(popup.Child, text);
            Assert.IsFalse(Descendants<TextBlock>(popup.Child).Any(x => x.IsVisible && x.Text is "Mass" or "Volume" or "Count"));
            Capture(popup.Child, "r1-dimension-popup.png");
        }
        finally { combo.IsDropDownOpen = false; }

        var expected = new[] { InventoryDimension.Mass, InventoryDimension.Volume, InventoryDimension.Count };
        var labels = new[] { "Masa", "Volumen", "Piezas" };
        for (var index = 0; index < expected.Length; index++)
        {
            combo.SelectedIndex = index;
            fixture.Layout();
            Assert.IsInstanceOfType<InventoryDimension>(combo.SelectedValue);
            Assert.AreEqual(expected[index], (InventoryDimension)combo.SelectedValue);
            Assert.AreEqual(expected[index], fixture.Model.Recipes.YieldDimension);
            AssertRenderedText(combo, labels[index]);
        }
        Capture(fixture.Window, "r1-recipe-dimensions.png");
        fixture.AssertNoCommercialEffects();
    });

    [TestMethod]
    public Task EmptyProductionInFinalShellShowsReasonAndDisablesAllInvalidPlanActions() => WpfTestHost.OnUi(async () =>
    {
        using var fixture = await ShellFixture.CreateAsync();
        await fixture.Model.ShowProductionAsync();
        fixture.Layout();
        var view = fixture.VisibleView<ProductionView>();
        Assert.AreSame(fixture.Model.Production, view.DataContext);
        var newButton = VisibleButton(view, "Nueva producción");
        Assert.IsTrue(newButton.IsEnabled);
        newButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        fixture.Layout();

        var model = fixture.Model.Production!;
        Assert.IsTrue(model.IsBuilder);
        Assert.IsTrue(model.Ready);
        Assert.IsTrue(model.CanCreate, "A permission restriction must not mask the invalid-plan assertions.");
        AssertRenderedText(view, "No hay perfiles productivos activos.");
        Assert.IsFalse(VisibleButton(view, "Agregar producto").IsEnabled);
        Assert.IsFalse(VisibleButton(view, "Quitar seleccionado").IsEnabled);
        Assert.IsFalse(VisibleButton(view, "Calcular necesidades").IsEnabled);
        Assert.IsFalse(VisibleButton(view, "Crear producción").IsEnabled);
        Assert.IsTrue(VisibleButton(view, "Cancelar").IsEnabled);
        var grid = Descendants<DataGrid>(view).Single(x => x.IsVisible && ReferenceEquals(x.ItemsSource, model.Plan));
        Assert.AreEqual(1, model.Plan.Count);
        grid.SelectedItem = model.Plan.Single();
        fixture.Layout();
        Assert.AreSame(model.Plan.Single(), model.SelectedPlan);
        Assert.IsFalse(VisibleButton(view, "Quitar seleccionado").IsEnabled, "A selected sole row must remain non-removable.");

        foreach (var quantity in new[] { "", "0", "-1", "1.5", "abc", "1" })
        {
            model.Plan[0].Planned = quantity;
            fixture.Layout();
            Assert.IsFalse(VisibleButton(view, "Calcular necesidades").IsEnabled, $"No active profile exists; quantity '{quantity}' cannot make the plan valid.");
            Assert.IsFalse(VisibleButton(view, "Crear producción").IsEnabled);
        }
        Capture(fixture.Window, "r1-production-no-profiles.png");
        fixture.AssertNoCommercialEffects();
    });

    private static Button VisibleButton(DependencyObject root, string content) =>
        Descendants<Button>(root).Single(x => x.IsVisible && Equals(x.Content, content));

    private static void AssertRenderedText(DependencyObject root, string text) =>
        Assert.IsTrue(Descendants<TextBlock>(root).Any(x => x.IsVisible && x.ActualWidth > 0 && x.ActualHeight > 0 && x.Text == text),
            $"Expected a visible, measured WPF TextBlock: {text}");

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T value) yield return value;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void Capture(UIElement view, string name)
    {
        var directory = Environment.GetEnvironmentVariable("CASAMIGA_TEST_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.RenderSize.Width), (int)Math.Ceiling(view.RenderSize.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

    private sealed class ShellFixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "CasaMigaManualSmokeR1", Guid.NewGuid().ToString("N"));
        public MainViewModel Model { get; } = new();
        public MainWindow Window { get; private set; } = null!;

        public static async Task<ShellFixture> CreateAsync()
        {
            var fixture = new ShellFixture();
            try
            {
                await fixture.Model.InitializeAsync(new FoundationOptions(fixture.directory, IsDevelopment: true));
                Assert.IsTrue(fixture.Model.IsBootstrap, fixture.Model.Message);
                var password = Guid.NewGuid().ToString("N");
                await fixture.Model.BootstrapAsync("r1-admin", "Prueba aislada R1", password, password);
                await fixture.Model.LoginAsync("r1-admin", password);
                Assert.IsTrue(fixture.Model.CanNavigate, fixture.Model.Message);
                fixture.Window = new MainWindow
                {
                    DataContext = fixture.Model, Width = 1280, Height = 820,
                    Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual
                };
                fixture.Window.Show();
                fixture.Layout();
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        public void Layout() => WpfTestHost.Layout(Window, 1280, 820);
        public T VisibleView<T>() where T : FrameworkElement => Descendants<T>(Window).Single(x => x.IsVisible);

        public void AssertNoCommercialEffects()
        {
            using var connection = new Database(Path.Combine(directory, "casamiga.db")).Open(true);
            foreach (var table in new[] { "inventory_operation", "inventory_movement", "production_batch", "sale", "payment", "cash_session" })
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT count(*) FROM {table}";
                Assert.AreEqual(0L, (long)command.ExecuteScalar()!, table);
            }
        }

        public void Dispose()
        {
            Window?.Close();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
