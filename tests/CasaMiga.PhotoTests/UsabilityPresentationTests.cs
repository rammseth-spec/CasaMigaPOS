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
public sealed class UsabilityPresentationTests
{
    [TestMethod]
    public Task EmptyNestedGridReproducesWheelTrapAndOptInTransfersToPage()=>WpfTestHost.OnUi(()=>
    {
        var grid=new DataGrid{Height=110};var (page,module)=Page(grid);
        var inner=Descendants<ScrollViewer>(grid).First();
        Assert.AreEqual(0d,inner.ScrollableHeight);
        Wheel(inner,-120);WpfTestHost.Layout(page,600,240);
        Assert.AreEqual(0d,page.VerticalOffset,"Baseline: empty grid consumes native wheel.");
        NestedScrollBehavior.SetIsEnabled(module,true);Wheel(inner,-120);WpfTestHost.Layout(page,600,240);
        Assert.IsGreaterThan(0d,page.VerticalOffset);
    });

    [TestMethod]
    public Task NestedScrollableGridKeepsWheelUntilItsDirectionalBoundary()=>WpfTestHost.OnUi(()=>
    {
        var grid=new DataGrid{Height=120,ItemsSource=Enumerable.Range(0,60).Select(x=>new{Label=$"Fila {x}"}).ToArray()};
        grid.Columns.Add(new DataGridTextColumn{Header="Nombre",Binding=new System.Windows.Data.Binding("Label")});
        var(page,module)=Page(grid);NestedScrollBehavior.SetIsEnabled(module,true);
        var inner=Descendants<ScrollViewer>(grid).First();Assert.IsGreaterThan(0d,inner.ScrollableHeight);
        Wheel(inner,-120);WpfTestHost.Layout(page,600,240);
        Assert.IsGreaterThan(0d,inner.VerticalOffset);Assert.AreEqual(0d,page.VerticalOffset);
        inner.ScrollToBottom();WpfTestHost.Layout(page,600,240);
        Wheel(inner,-120);WpfTestHost.Layout(page,600,240);Assert.IsGreaterThan(0d,page.VerticalOffset);
        var outerBefore=page.VerticalOffset;Wheel(inner,120);WpfTestHost.Layout(page,600,240);
        Assert.IsLessThan(inner.ScrollableHeight,inner.VerticalOffset);Assert.AreEqual(outerBefore,page.VerticalOffset);
        inner.ScrollToTop();WpfTestHost.Layout(page,600,240);Wheel(inner,120);WpfTestHost.Layout(page,600,240);
        Assert.IsLessThan(outerBefore,page.VerticalOffset);
    });

    [TestMethod]
    public Task ClosedComboAndSmallWheelDeltasScrollPageWithoutChangingSelection()=>WpfTestHost.OnUi(()=>
    {
        var combo=new ComboBox{ItemsSource=new[]{"Masa","Volumen","Piezas"},SelectedIndex=1};
        var(page,module)=Page(combo);
        var window=new Window{Content=page,Width=600,Height=240,Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
        try
        {
        window.Show();combo.Focus();Keyboard.Focus(combo);WpfTestHost.Layout(page,600,240);
        Assert.IsTrue(combo.IsKeyboardFocusWithin,"Reproduce wheel selection with actual WPF keyboard focus.");
        Wheel(combo,-120);WpfTestHost.Layout(page,600,240);Assert.AreEqual(2,combo.SelectedIndex,"Without the behavior, a focused closed combo changes selection.");
        combo.SelectedIndex=1;NestedScrollBehavior.SetIsEnabled(module,true);
        Wheel(combo,-15);WpfTestHost.Layout(page,600,240);
        Assert.IsGreaterThan(0d,page.VerticalOffset);Assert.AreEqual(1,combo.SelectedIndex);
        var small=page.VerticalOffset;Wheel(combo,-120);WpfTestHost.Layout(page,600,240);
        Assert.IsGreaterThan(small,page.VerticalOffset);Assert.AreEqual(1,combo.SelectedIndex);
        page.ScrollToVerticalOffset(200);WpfTestHost.Layout(page,600,240);Assert.AreEqual(200d,page.VerticalOffset,"Explicit scrollbar/offset control is unaffected.");
        }
        finally{window.Close();}
    });

    [TestMethod]
    public Task OpenComboRetainsNativeWheelAndKeyboardPath()=>WpfTestHost.OnUi(()=>
    {
        var combo=new ComboBox{ItemsSource=Enumerable.Range(1,60).ToArray(),SelectedIndex=1,MaxDropDownHeight=150};
        var(page,module)=Page(combo);NestedScrollBehavior.SetIsEnabled(module,true);
        var window=new Window{Content=page,Width=600,Height=240,Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
        try
        {
            window.Show();WpfTestHost.Layout(page,600,240);combo.IsDropDownOpen=true;WpfTestHost.Layout(page,600,240);
            Assert.IsTrue(combo.IsDropDownOpen,"The popup requires a loaded presentation source.");
            var preview=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent};
            combo.RaiseEvent(preview);Assert.IsFalse(preview.Handled,"An open combo must retain the event for its popup.");
            Assert.IsTrue(combo.IsDropDownOpen);Assert.AreEqual(0d,page.VerticalOffset);
            var popup=(System.Windows.Controls.Primitives.Popup)combo.Template.FindName("PART_Popup",combo);
            var popupViewer=Descendants<ScrollViewer>(popup.Child).First();
            Wheel(popupViewer,-120);WpfTestHost.Layout(page,600,240);
            Assert.IsGreaterThan(0d,popupViewer.VerticalOffset);Assert.AreEqual(0d,page.VerticalOffset);
            combo.IsDropDownOpen=false;
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,Key.End){RoutedEvent=Keyboard.KeyDownEvent});
            Assert.AreEqual(59,combo.SelectedIndex,"The native End key selects the last item.");
        }
        finally{combo.IsDropDownOpen=false;window.Close();}
    });

    [TestMethod]
    public Task EmptyStatesAndSelectionButtonsRenderAcrossModules()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();
        var inventory=fixture.Inventory();await inventory.LoadAsync();var inventoryView=new InventoryView{DataContext=inventory};WpfTestHost.Layout(inventoryView);
        AssertVisible(inventoryView,"No hay artículos con los filtros seleccionados.");Assert.IsFalse(Button(inventoryView,"Ver detalle").IsEnabled);
        var recipes=fixture.Recipes();await recipes.LoadAsync();var recipeView=new RecipeView{DataContext=recipes};WpfTestHost.Layout(recipeView);
        AssertVisible(recipeView,"No hay recetas registradas.");Assert.IsFalse(Button(recipeView,"Ver detalle").IsEnabled);
        recipes.NewFormula();WpfTestHost.Layout(recipeView);Assert.IsFalse(Button(recipeView,"Agregar ingrediente").IsEnabled);Assert.IsFalse(Button(recipeView,"Agregar grupo/opción").IsEnabled);
        await recipes.ShowProfilesAsync();WpfTestHost.Layout(recipeView);AssertVisible(recipeView,"No hay perfiles de producto registrados.");
        recipes.NewProfile();WpfTestHost.Layout(recipeView);AssertVisible(recipeView,"No hay artículos terminados disponibles. Crea primero un artículo de tipo Producto terminado en Inventario.");Assert.IsFalse(Button(recipeView,"Crear perfil").IsEnabled);
        Assert.IsFalse(Button(recipeView,"Agregar ingrediente").IsEnabled);Assert.IsFalse(Button(recipeView,"Agregar preparación").IsEnabled);
        var production=fixture.Production();await production.LoadAsync();var productionView=new ProductionView{DataContext=production};production.NewProduction();WpfTestHost.Layout(productionView);
        AssertVisible(productionView,"No hay perfiles productivos activos.");Assert.IsFalse(Button(productionView,"Calcular necesidades").IsEnabled);Assert.IsFalse(Button(productionView,"Crear producción").IsEnabled);Assert.IsFalse(Button(productionView,"Quitar seleccionado").IsEnabled);
        await production.ShowDraftsAsync();WpfTestHost.Layout(productionView);AssertVisible(productionView,"No hay producciones en borrador.");Assert.IsFalse(Button(productionView,"Ver detalle").IsEnabled);
        await production.ShowHistoryAsync();WpfTestHost.Layout(productionView);AssertVisible(productionView,"Aún no hay producciones confirmadas.");
        fixture.AssertNoOperations();
    });

    [TestMethod]
    public Task OperationIdentityRemainsInternalInAllInventoryForms()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();fixture.InventoryService.CreateInventoryItem("MP-HAR-001","Harina",InventoryItemKind.RawMaterial,InventoryDimension.Mass);
        var vm=fixture.Inventory();await vm.LoadAsync();vm.SelectedItem=vm.Items.Single();Assert.IsTrue(vm.CanOpenDetail);await vm.OpenDetailAsync();
        var view=new InventoryView{DataContext=vm};
        foreach(var operation in new[]{"opening","receipt","waste","count"})
        {
            vm.BeginOperation(operation);var id=vm.PendingOperationId;Assert.IsTrue(Guid.TryParse(id,out _));WpfTestHost.Layout(view);
            Assert.IsFalse(Descendants<TextBlock>(view).Any(x=>x.Text.Contains(id)||x.Text.Contains("OperationId")));
            Assert.IsFalse(Descendants<TextBox>(view).Any(x=>x.Text.Contains(id)));
            Assert.AreEqual(id,vm.PendingOperationId);vm.Back();
        }
        await vm.ShowLinkEditorAsync();WpfTestHost.Layout(view);
        var grid=Descendants<DataGrid>(view).Single(x=>AutomationProperties.GetName(x)=="Productos para asociar");
        Assert.IsFalse(grid.Columns.Any(x=>Equals(x.Header,"ProductId")));Assert.IsFalse(Button(view,"Asociar").IsEnabled);
        fixture.AssertNoOperations();
    });

    [TestMethod]
    public Task DimensionComboRendersLabelsAndPreservesEnumValue()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var vm=fixture.Recipes();await vm.LoadAsync();vm.NewFormula();
        var view=new RecipeView{DataContext=vm};WpfTestHost.Layout(view);
        var combo=Descendants<ComboBox>(view).Single(x=>AutomationProperties.GetName(x)=="Dimensión del rendimiento");
        CollectionAssert.AreEqual(new[]{"Masa","Volumen","Piezas"},combo.Items.Cast<InventoryDimensionChoice>().Select(x=>x.Label).ToArray());
        combo.SelectedValue=InventoryDimension.Volume;Assert.AreEqual(InventoryDimension.Volume,vm.YieldDimension);WpfTestHost.Layout(view);
        Assert.IsTrue(Descendants<TextBlock>(combo).Any(x=>x.Text=="Volumen"));fixture.AssertNoOperations();
    });

    [TestMethod]
    public Task DisabledButtonRemainsLegibleAndContextHelpIsReusable()=>WpfTestHost.OnUi(()=>
    {
        var button=new Button{Content="Ver detalle",IsEnabled=false};WpfTestHost.Layout(button,200,60);
        Assert.AreEqual(((SolidColorBrush)System.Windows.Application.Current.FindResource("TextPrimaryBrush")).Color,((SolidColorBrush)button.Foreground).Color);
        var border=(Border)button.Template.FindName("Surface",button);Assert.AreEqual(1d,border.Opacity);
        var help=new ContextHelp{HelpText="Texto de prueba de ayuda."};WpfTestHost.Layout(help,360,200);
        var expander=Descendants<Expander>(help).Single();Assert.IsFalse(expander.IsExpanded);expander.IsExpanded=true;WpfTestHost.Layout(help,360,200);
        AssertVisible(help,"Texto de prueba de ayuda.");expander.IsExpanded=false;
    });

    private static (ScrollViewer Page,UserControl Module) Page(UIElement child)
    {
        var stack=new StackPanel();stack.Children.Add(child);stack.Children.Add(new Border{Height=900});
        var module=new UserControl{Content=stack};var page=new ScrollViewer{Content=module,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        WpfTestHost.Layout(page,600,240);return(page,module);
    }
    private static void Wheel(UIElement source,int delta)
    {
        var args=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,delta){RoutedEvent=UIElement.PreviewMouseWheelEvent};source.RaiseEvent(args);
        if(!args.Handled){args.RoutedEvent=UIElement.MouseWheelEvent;source.RaiseEvent(args);}
    }
    private static bool Visible(DependencyObject element)
    {
        for(DependencyObject? node=element;node is not null;node=VisualTreeHelper.GetParent(node))if(node is UIElement {Visibility:not Visibility.Visible})return false;
        return true;
    }
    private static Button Button(DependencyObject view,string content)=>Descendants<Button>(view).Single(x=>Equals(x.Content,content)&&Visible(x));
    private static void AssertVisible(DependencyObject view,string text)=>Assert.IsTrue(Descendants<TextBlock>(view).Any(x=>x.Text==text&&Visible(x)),text);
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private sealed class Fixture:IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"CasaMigaUsability",Guid.NewGuid().ToString("N"));
        private readonly Database database;private readonly SqliteLocalUnitOfWork work;private readonly UserSession user=new();private readonly IClock clock=new SystemClock();private readonly TerminalIdentity terminal;private readonly CatalogService catalog;private readonly RecipeService recipes;
        public InventoryService InventoryService{get;}
        public Fixture()
        {
            Directory.CreateDirectory(root);database=new(Path.Combine(root,"casamiga.db"));database.Initialize();work=new(database);terminal=TerminalConfiguration.LoadOrCreate(root);work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
            var identity=new IdentityService(work,new LocalPasswords(),clock,user);var password=Guid.NewGuid().ToString("N");identity.Bootstrap("admin","Prueba UX",password);Assert.IsTrue(identity.Login("admin",password));
            catalog=new(work,user,clock,terminal.Id);InventoryService=new(new SqliteInventoryStore(database,clock),work,user,clock,terminal.Id);
            recipes=new(new SqliteProductionUnitOfWork(database),user,clock,terminal.Id);
        }
        public InventoryViewModel Inventory()=>new(InventoryService,catalog,(_,_)=>{});
        public RecipeViewModel Recipes()=>new(recipes,InventoryService,catalog,(_,_)=>{});
        public ProductionViewModel Production()=>new(new(new SqliteProductionUnitOfWork(database),user,clock,terminal.Id),recipes,InventoryService,catalog,user,terminal.Id,(_,_)=>{});
        public void AssertNoOperations(){using var c=database.Open(true);foreach(var table in new[]{"sale","inventory_operation","production_batch"}){using var command=c.CreateCommand();command.CommandText=$"SELECT count(*) FROM {table}";Assert.AreEqual(0L,(long)command.ExecuteScalar()!,table);}}
        public void Dispose()=>Directory.Delete(root,true);
    }
}
