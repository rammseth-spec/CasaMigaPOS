using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
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
        search.Text="MP-LECHE";await WaitUntil(()=>vm.IngredientMatches.Count==1);
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

    private static Button Button(DependencyObject root,string content)=>Descendants<Button>(root).Single(x=>Visible(x)&&Equals(x.Content,content));
    private static void AssertVisible(DependencyObject root,string text)=>Assert.IsTrue(Descendants<TextBlock>(root).Any(x=>Visible(x)&&x.Text==text),text);
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
            Flour=inventory.CreateInventoryItem("MP-HAR-001","Harina",InventoryItemKind.RawMaterial,InventoryDimension.Mass);
            Milk=inventory.CreateInventoryItem("MP-LECHE","Leche",InventoryItemKind.RawMaterial,InventoryDimension.Volume);
            Packets=inventory.CreateInventoryItem("MP-SOBRES","Sobres",InventoryItemKind.Supply,InventoryDimension.Count);
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
