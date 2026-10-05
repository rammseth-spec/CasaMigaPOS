using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class RecipeUsabilityPresentationTests
{
    [TestMethod]
    [DataRow(1280,820)]
    [DataRow(1920,1080)]
    public Task IngredientSearchFiltersWithoutSubmitKeepsSelectionAndClears(int width,int height)=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var(vm,view)=await fixture.EditorAsync();WpfTestHost.Layout(view,width-220,height*2);
        vm.AddIngredient(false);var row=vm.Ingredients.Single();row.Item=vm.Items.Single(x=>x.Id==fixture.Flour.Id);WpfTestHost.Layout(view,width-220,height*2);
        var search=Descendants<TextBox>(view).Single(x=>AutomationProperties.GetName(x)=="Buscar ingrediente por nombre o código");
        search.Text="SMOKE-VOL-001";await WaitUntil(()=>vm.IngredientMatches.Count==1);
        Assert.AreEqual(fixture.Milk.Id,vm.IngredientMatches.Single().Id);
        Assert.AreEqual(fixture.Flour.Id,row.Item!.Id);
        CollectionAssert.AreEquivalent(new[]{fixture.Flour.Id,fixture.Milk.Id},row.AvailableItems.Select(x=>x.Id).ToArray());
        search.Text="no-existe";await WaitUntil(()=>vm.IngredientMatches.Count==0);WpfTestHost.Layout(view,width-220,height*2);
        AssertVisible(view,"No encontramos ingredientes con esa búsqueda.");
        Button(view,"× Limpiar").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await WaitUntil(()=>vm.IngredientMatches.Count==3);
        Assert.AreEqual("",vm.IngredientSearch);Assert.AreEqual(3,row.AvailableItems.Count);Assert.AreEqual(fixture.Flour.Id,row.Item.Id);
        fixture.AssertNoEffects();
    });

    [TestMethod]
    public Task MaterializedEditorPreservesPersistedSelectionsThroughoutFilteringAndCancel()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var(vm,view)=await fixture.EditorAsync();
        vm.Name="Borrador sintético con tres dimensiones";
        foreach(var entry in new[]{
            (fixture.Flour,"500","g",IngredientCountPolicy.NotApplicable),
            (fixture.Milk,"250","ml",IngredientCountPolicy.NotApplicable),
            (fixture.Packets,"2","pieza",IngredientCountPolicy.ExactWholeCount)})
        {
            vm.AddIngredient(false);var row=vm.Ingredients[^1];row.Item=vm.Items.Single(x=>x.Id==entry.Item1.Id);row.Quantity=entry.Item2;row.Unit=entry.Item3;row.Policy=entry.Item4;
        }
        await vm.SaveFormulaVersionAsync();vm.SelectedVersion=vm.Versions.Single();await vm.EditFormulaVersionAsync();WpfTestHost.Layout(view,1060,1800);
        var collections=vm.Ingredients.Select(x=>x.AvailableItems).ToArray();
        AssertRows(vm,fixture);AssertMaterializedSelections(view,fixture);

        var search=Descendants<TextBox>(view).Single(x=>AutomationProperties.GetName(x)=="Buscar ingrediente por nombre o código");
        search.Text="SIN-RESULTADOS-999";await WaitUntil(()=>vm.IngredientMatches.Count==0);WpfTestHost.Layout(view,1060,1800);
        AssertVisible(view,"No encontramos ingredientes con esa búsqueda.");AssertRows(vm,fixture);AssertMaterializedSelections(view,fixture);
        CollectionAssert.AreEqual(collections,vm.Ingredients.Select(x=>x.AvailableItems).ToArray());

        search.Text="SMOKE-MASA-001";await WaitUntil(()=>vm.IngredientMatches.Count==1);WpfTestHost.Layout(view,1060,1800);
        Assert.AreEqual(fixture.Flour.Id,vm.IngredientMatches.Single().Id);AssertRows(vm,fixture);AssertMaterializedSelections(view,fixture);
        CollectionAssert.AreEqual(collections,vm.Ingredients.Select(x=>x.AvailableItems).ToArray());

        Button(view,"× Limpiar").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await WaitUntil(()=>vm.IngredientMatches.Count==3);WpfTestHost.Layout(view,1060,1800);
        AssertRows(vm,fixture);AssertMaterializedSelections(view,fixture);

        Button(view,"Cancelar").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));vm.SelectedVersion=vm.Versions.Single();await vm.EditFormulaVersionAsync();WpfTestHost.Layout(view,1060,1800);
        AssertRows(vm,fixture);AssertMaterializedSelections(view,fixture);fixture.AssertNoCommercialEffects();
    });

    [TestMethod]
    public Task DimensionsUnitsAndPoliciesAreHumanAndCompatible()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var(vm,view)=await fixture.EditorAsync();vm.AddIngredient(false);var row=vm.Ingredients.Single();
        row.Item=vm.Items.Single(x=>x.Id==fixture.Flour.Id);WpfTestHost.Layout(view);
        CollectionAssert.AreEqual(new[]{"g","kg"},row.UnitChoices.ToArray());Assert.IsFalse(row.UnitChoices.Contains("ml"));Assert.AreEqual(IngredientCountPolicy.NotApplicable,row.Policy);
        row.Item=vm.Items.Single(x=>x.Id==fixture.Packets.Id);WpfTestHost.Layout(view);
        CollectionAssert.AreEqual(new[]{"pieza"},row.UnitChoices.ToArray());Assert.AreEqual("pieza",row.Unit);Assert.AreEqual(IngredientCountPolicy.ExactWholeCount,row.Policy);
        CollectionAssert.AreEqual(new[]{"Sólo piezas completas","Redondear hacia arriba"},row.AvailablePolicies.Select(x=>x.Label).ToArray());
        var dimension=Descendants<ComboBox>(view).Single(x=>AutomationProperties.GetName(x)=="Dimensión del rendimiento");
        CollectionAssert.AreEqual(new[]{"Masa","Volumen","Piezas"},dimension.Items.Cast<InventoryDimensionChoice>().Select(x=>x.Label).ToArray());
        dimension.SelectedValue=InventoryDimension.Volume;WpfTestHost.Layout(view);
        CollectionAssert.AreEqual(new[]{"ml","L"},vm.YieldUnitChoices.ToArray());Assert.AreEqual("ml",vm.YieldUnit);
        fixture.AssertNoEffects();
    });

    [TestMethod]
    public Task SearchKeepsExplicitAddAndCompatibleChoicesForEveryDimension()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var(vm,view)=await fixture.EditorAsync();WpfTestHost.Layout(view);
        var search=Descendants<TextBox>(view).Single(x=>AutomationProperties.GetName(x)=="Buscar ingrediente por nombre o código");
        foreach(var expected in new[]{fixture.Flour,fixture.Milk,fixture.Packets})
        {
            var before=vm.Ingredients.Count;search.Text=expected.Code;await WaitUntil(()=>vm.IngredientMatches.Count==1&&vm.IngredientMatches[0].Id==expected.Id);WpfTestHost.Layout(view);
            Assert.AreEqual(before,vm.Ingredients.Count,"Buscar no debe crear filas automáticamente.");
            Button(view,"Agregar ingrediente").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));var row=vm.Ingredients[^1];row.Item=vm.IngredientMatches.Single();
            CollectionAssert.AreEqual(expected.Dimension switch{InventoryDimension.Mass=>new[]{"g","kg"},InventoryDimension.Volume=>new[]{"ml","L"},_=>new[]{"pieza"}},row.UnitChoices.ToArray());
            if(expected.Dimension==InventoryDimension.Count)CollectionAssert.AreEqual(new[]{IngredientCountPolicy.ExactWholeCount,IngredientCountPolicy.CeilingWholeCount},row.AvailablePolicies.Select(x=>x.Value).ToArray());
        }
        fixture.AssertNoEffects();
    });

    [TestMethod]
    public Task AlternativeActionsCreateAGroupThenOptionsWithoutExposingIds()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var(vm,view)=await fixture.EditorAsync();WpfTestHost.Layout(view);
        AssertVisible(view,"Aún no hay grupos de alternativas. Crea uno sólo cuando varios ingredientes puedan sustituirse.");
        Assert.IsFalse(Button(view,"Agregar opción al grupo").IsEnabled);Assert.IsFalse(Button(view,"Quitar opción").IsEnabled);
        Button(view,"Nuevo grupo de alternativas").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));WpfTestHost.Layout(view);
        Assert.AreEqual(1,vm.Alternatives.Count);Assert.AreEqual(1,vm.Alternatives[0].Group);Assert.AreEqual(1,vm.Alternatives[0].Preference);
        Assert.IsTrue(Button(view,"Agregar opción al grupo").IsEnabled);Assert.IsFalse(vm.CanSaveFormulaDraft);
        Button(view,"Agregar opción al grupo").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));WpfTestHost.Layout(view);
        Assert.AreEqual(2,vm.Alternatives.Count);Assert.IsTrue(vm.Alternatives.All(x=>x.Group==1));CollectionAssert.AreEqual(new[]{1,2},vm.Alternatives.Select(x=>x.Preference).ToArray());
        var grid=Descendants<DataGrid>(view).Single(x=>AutomationProperties.GetName(x)=="Grupos y opciones de alternativas");
        Assert.IsFalse(grid.Columns.Any(x=>Equals(x.Header,"Id")||Equals(x.Header,"GUID")));
        Assert.IsTrue(Descendants<ContextHelp>(view).Any(x=>x.HelpText==vm.AlternativesHelpText));
        fixture.AssertNoEffects();
    });

    [TestMethod]
    public Task DraftButtonsVersionAndPublishedStateFollowTheExistingWorkflow()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var(vm,view)=await fixture.EditorAsync();WpfTestHost.Layout(view);
        var version=Descendants<TextBox>(view).Single(x=>AutomationProperties.GetName(x)=="Número de versión automático");
        Assert.IsTrue(version.IsReadOnly);Assert.AreEqual("1",version.Text);Assert.IsFalse(Button(view,"Guardar borrador").IsEnabled);
        vm.Name="Versión sintética";vm.AddIngredient(false);var row=vm.Ingredients.Single();row.Item=vm.Items.Single(x=>x.Id==fixture.Flour.Id);row.Quantity="500";row.Unit="g";WpfTestHost.Layout(view);
        Assert.IsTrue(Button(view,"Guardar borrador").IsEnabled);
        await vm.SaveFormulaVersionAsync();vm.SelectedVersion=vm.Versions.Single();await vm.PublishAsync();vm.SelectedVersion=vm.Versions.Single();await vm.ActivateAsync();
        WpfTestHost.Layout(view);Assert.AreEqual("Activa · Publicada",vm.Versions.Single().DisplayState);AssertVisible(view,"Activa · Publicada");
        Assert.IsFalse(Button(view,"Editar borrador").IsEnabled);Assert.IsFalse(Button(view,"Publicar versión").IsEnabled);Assert.IsFalse(Button(view,"Activar versión").IsEnabled);
        fixture.AssertNoCommercialEffects();
    });

    [TestMethod]
    public Task RecipeHelpAndLongEditorKeepSharedScrollBehavior()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var(vm,view)=await fixture.EditorAsync();WpfTestHost.Layout(view,1060,1600);
        Assert.IsTrue(NestedScrollBehavior.GetIsEnabled(view));Assert.IsTrue(Descendants<ContextHelp>(view).Any(x=>x.HelpText==vm.YieldHelpText));Assert.IsTrue(Descendants<ContextHelp>(view).Any(x=>x.HelpText==vm.PolicyHelpText));
        var page=new ScrollViewer{Content=view,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};WpfTestHost.Layout(page,1060,620);
        Assert.IsGreaterThan(620d,page.ExtentHeight);Assert.AreEqual(0d,page.HorizontalOffset);
        fixture.AssertNoEffects();
    });

    [TestMethod]
    public Task ContextHelpIsOperationalCompactResponsiveAndClosesWithEscape()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var(vm,view)=await fixture.EditorAsync();WpfTestHost.Layout(view,1060,1800);
        foreach(var text in new[]{vm.YieldHelpText,vm.PolicyHelpText,vm.DirectIngredientsHelpText,vm.AlternativesHelpText,vm.PreparationsHelpText,vm.PublishedNotice})
        {
            Assert.IsTrue(text.Contains("Cómo se usa",StringComparison.Ordinal)||text.StartsWith("BORRADOR",StringComparison.Ordinal));
            Assert.IsFalse(text.Contains("Casa Miga",StringComparison.OrdinalIgnoreCase));
        }
        Assert.IsTrue(vm.PolicyHelpText.Contains("2.4",StringComparison.Ordinal));Assert.IsTrue(vm.PolicyHelpText.Contains("3 piezas",StringComparison.Ordinal));
        Assert.IsTrue(Descendants<ContextHelp>(view).Any(x=>x.HelpText==vm.DirectIngredientsHelpText));

        var help=new ContextHelp{HelpText=vm.AlternativesHelpText};var window=new Window{Content=help,Width=1100,Height=820,Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
        try
        {
            window.Show();var panel=Descendants<Expander>(help).Single();panel.IsExpanded=true;WpfTestHost.Layout(help,1060,800);
            Assert.IsGreaterThanOrEqualTo(420d,panel.ActualWidth);Assert.IsLessThanOrEqualTo(520d,panel.ActualWidth);Assert.IsLessThan(400d,panel.ActualHeight);
        }
        finally{window.Close();}
        var narrowHelp=new ContextHelp{HelpText=vm.AlternativesHelpText,Width=340};var narrowWindow=new Window{Content=narrowHelp,Width=380,Height=820,Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
        try
        {
            narrowWindow.Show();var panel=Descendants<Expander>(narrowHelp).Single();panel.IsExpanded=true;WpfTestHost.Layout(narrowHelp,340,800);
            Assert.IsLessThanOrEqualTo(340d,panel.ActualWidth);Assert.AreEqual(TextWrapping.Wrap,Descendants<TextBlock>(narrowHelp).Single(x=>x.Text==vm.AlternativesHelpText).TextWrapping);
            narrowHelp.Focus();Keyboard.Focus(narrowHelp);SendKey(narrowHelp,narrowWindow,Key.Escape);WpfTestHost.Layout(narrowHelp,340,800);Assert.IsFalse(panel.IsExpanded);
        }
        finally{narrowWindow.Close();}
        fixture.AssertNoEffects();await Task.CompletedTask;
    });

    private static Button Button(DependencyObject root,string content)=>Descendants<Button>(root).Single(x=>Visible(x)&&Equals(x.Content,content));
    private static void AssertRows(RecipeViewModel vm,Fixture fixture)
    {
        Assert.AreEqual(3,vm.Ingredients.Count);
        var mass=vm.Ingredients.Single(x=>x.Item?.Id==fixture.Flour.Id);Assert.AreEqual("500",mass.Quantity);Assert.AreEqual("g",mass.Unit);Assert.AreEqual(IngredientCountPolicy.NotApplicable,mass.Policy);
        var volume=vm.Ingredients.Single(x=>x.Item?.Id==fixture.Milk.Id);Assert.AreEqual("250",volume.Quantity);Assert.AreEqual("ml",volume.Unit);Assert.AreEqual(IngredientCountPolicy.NotApplicable,volume.Policy);
        var count=vm.Ingredients.Single(x=>x.Item?.Id==fixture.Packets.Id);Assert.AreEqual("2",count.Quantity);Assert.AreEqual("pieza",count.Unit);Assert.AreEqual(IngredientCountPolicy.ExactWholeCount,count.Policy);
    }
    private static void AssertMaterializedSelections(DependencyObject view,Fixture fixture)
    {
        var selected=Descendants<ComboBox>(view).Where(x=>Visible(x)&&AutomationProperties.GetName(x)=="Seleccionar ingrediente").Select(x=>(x.SelectedItem as RecipeItemChoice)?.Id).ToArray();
        CollectionAssert.AreEquivalent(new Guid?[]{fixture.Flour.Id,fixture.Milk.Id,fixture.Packets.Id},selected);
    }
    private static void AssertVisible(DependencyObject root,string text)=>Assert.IsTrue(Descendants<TextBlock>(root).Any(x=>Visible(x)&&x.Text==text),text);
    private static void SendKey(UIElement source,Window window,Key key)
    {
        var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};
        source.RaiseEvent(args);if(!args.Handled){args.RoutedEvent=Keyboard.KeyDownEvent;source.RaiseEvent(args);}
    }
    private static bool Visible(DependencyObject element){for(DependencyObject? node=element;node is not null;node=VisualTreeHelper.GetParent(node))if(node is UIElement{Visibility:not Visibility.Visible})return false;return true;}
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private static async Task WaitUntil(Func<bool> done){var limit=DateTime.UtcNow.AddSeconds(6);while(!done()&&DateTime.UtcNow<limit)await Task.Delay(35);Assert.IsTrue(done());}

    private sealed class Fixture:IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"CasaMigaRecipes",Guid.NewGuid().ToString("N"));
        private readonly Database database;private readonly SqliteLocalUnitOfWork work;private readonly UserSession user=new();private readonly IClock clock=new SystemClock();private readonly TerminalIdentity terminal;
        private readonly CatalogService catalog;private readonly InventoryService inventory;private readonly RecipeService recipes;
        public InventoryItem Flour{get;} public InventoryItem Milk{get;} public InventoryItem Packets{get;}
        public Fixture()
        {
            Directory.CreateDirectory(root);database=new(Path.Combine(root,"casamiga.db"));database.Initialize();work=new(database);terminal=TerminalConfiguration.LoadOrCreate(root);work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
            var identity=new IdentityService(work,new LocalPasswords(),clock,user);var password=Guid.NewGuid().ToString("N");identity.Bootstrap("admin","Administrador de pruebas",password);Assert.IsTrue(identity.Login("admin",password));
            catalog=new(work,user,clock,terminal.Id);inventory=new(new SqliteInventoryStore(database,clock),work,user,clock,terminal.Id);recipes=new(new SqliteProductionUnitOfWork(database),user,clock,terminal.Id);
            Flour=inventory.CreateInventoryItem("SMOKE-MASA-001","Ingrediente masa sintético",InventoryItemKind.RawMaterial,InventoryDimension.Mass);
            Milk=inventory.CreateInventoryItem("SMOKE-VOL-001","Ingrediente volumen sintético",InventoryItemKind.RawMaterial,InventoryDimension.Volume);
            Packets=inventory.CreateInventoryItem("SMOKE-COUNT-001","Ingrediente piezas sintético",InventoryItemKind.Supply,InventoryDimension.Count);
        }
        public async Task<(RecipeViewModel Vm,RecipeView View)> EditorAsync()
        {
            var vm=new RecipeViewModel(recipes,inventory,catalog,(_,_)=>{});await vm.LoadAsync();vm.NewFormula();vm.Code="UX-RECETA";vm.Name="Receta sintética";await vm.CreateFormulaAsync();vm.NewFormulaVersion();
            var view=new RecipeView{DataContext=vm};WpfTestHost.Layout(view);return(vm,view);
        }
        public void AssertNoCommercialEffects(){using var c=database.Open(true);foreach(var table in new[]{"sale","inventory_operation","production_batch"}){using var command=c.CreateCommand();command.CommandText=$"SELECT count(*) FROM {table}";Assert.AreEqual(0L,(long)command.ExecuteScalar()!,table);}}
        public void AssertNoEffects(){AssertNoCommercialEffects();using var c=database.Open(true);using var command=c.CreateCommand();command.CommandText="SELECT count(*) FROM formula_version";Assert.AreEqual(0L,(long)command.ExecuteScalar()!);}
        public void Dispose()=>Directory.Delete(root,true);
    }
}
