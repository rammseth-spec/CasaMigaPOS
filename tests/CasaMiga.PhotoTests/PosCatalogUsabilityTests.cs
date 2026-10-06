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
public sealed class PosCatalogUsabilityTests
{
    [TestMethod]
    public async Task PosLiveSearchFindsNameAndSkuAndKeepsTheSelectedCategory()
    {
        using var fixture=new Fixture();
        var vm=fixture.Pos();await vm.EnterAsync();await vm.CategoryAsync(fixture.Savory.Id);
        Assert.AreEqual(fixture.Savory.Id,vm.Categories.Single(x=>x.Selected).Id);

        vm.Search="Bolillo mantequilla";
        await WaitUntil(()=>!vm.IsSearching&&vm.Products.Count==1);
        Assert.AreEqual("Bolillo mantequilla",vm.Products.Single().Name);
        Assert.AreEqual(fixture.Savory.Id,vm.Categories.Single(x=>x.Selected).Id);

        vm.Search="CM-LARGO-01";
        await WaitUntil(()=>!vm.IsSearching&&vm.Products.Count==1);
        StringAssert.StartsWith(vm.Products.Single().Name,"Producto con un nombre deliberadamente largo");
        await vm.ClearSearchAsync();
        Assert.AreEqual(2,vm.Products.Count);
        Assert.AreEqual(fixture.Savory.Id,vm.Categories.Single(x=>x.Selected).Id);
        fixture.AssertNoCommercialEffects();
    }

    [TestMethod]
    public Task PosEscapeClearsOnlyTheSearchAndLeavesTheCategorySelected()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var vm=fixture.Pos();await vm.EnterAsync();await vm.CategoryAsync(fixture.Savory.Id);
        var view=new PosView{DataContext=vm};var window=new Window{Content=view,Width=1280,Height=820,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        try
        {
            window.Show();WpfTestHost.Layout(window,1280,820);
            var search=Descendants<TextBox>(view).Single(x=>AutomationProperties.GetName(x)=="Buscar producto por nombre o SKU");
            search.Focus();Keyboard.Focus(search);search.Text="Bolillo";
            await WaitUntil(()=>!vm.IsSearching&&vm.Products.Count==1);
            SendKey(search,window,Key.Escape);
            await WaitUntil(()=>!vm.IsSearching&&vm.Search.Length==0&&vm.Products.Count==2);
            Assert.AreEqual(fixture.Savory.Id,vm.Categories.Single(x=>x.Selected).Id);
        }
        finally{window.Close();}
        fixture.AssertNoCommercialEffects();
    });

    [TestMethod]
    public async Task CatalogFiltersLiveRejectsStaleQueriesAndPreservesAValidSelection()
    {
        using var fixture=new Fixture();var vm=fixture.Catalog();await vm.LoadAsync();
        vm.FilterCategoryId=fixture.Sweet.Id;vm.FilterCategoryId=fixture.Savory.Id;
        await WaitUntil(()=>!vm.IsSearching&&vm.Products.Count==2&&vm.Products.All(x=>x.Product.CategoryId==fixture.Savory.Id));
        vm.SelectedProduct=vm.Products.Single(x=>x.Product.Sku=="CM-003");

        vm.Search="CM-003";
        await WaitUntil(()=>!vm.IsSearching&&vm.Products.Count==1);
        Assert.AreEqual("Bolillo mantequilla",vm.SelectedProduct?.Product.Name);
        vm.Search="sin coincidencias 1K4A";
        await WaitUntil(()=>!vm.IsSearching&&vm.Products.Count==0);
        Assert.IsTrue(vm.HasNoProducts);StringAssert.Contains(vm.EmptyProductsMessage,"No encontramos");Assert.IsNull(vm.SelectedProduct);

        await vm.ClearSearchAsync();
        Assert.AreEqual(fixture.Savory.Id,vm.FilterCategoryId);
        Assert.AreEqual(2,vm.Products.Count);
        vm.ShowInactive=true;
        await WaitUntil(()=>!vm.IsSearching&&vm.Products.Count==3);
        Assert.IsTrue(vm.Products.Any(x=>!x.Product.IsActive));
        fixture.AssertNoCommercialEffects();
    }

    [TestMethod]
    public async Task InlineCategoryCreationKeepsTheProductDraftAndSelectsTheNewCategory()
    {
        using var fixture=new Fixture();var vm=fixture.Catalog();await vm.LoadAsync();vm.NewProduct();
        vm.Name="Producto sintético de categoría nueva";vm.Sku="CM-NUEVA-01";vm.Description="Borrador que debe conservarse";vm.Price="17.50";vm.TrackInventory=false;
        vm.BeginInlineCategory();vm.NewCategoryName="Categoría sintética nueva";await vm.CreateInlineCategoryAsync();
        Assert.AreEqual("Producto sintético de categoría nueva",vm.Name);Assert.AreEqual("CM-NUEVA-01",vm.Sku);Assert.AreEqual("17.50",vm.Price);Assert.IsFalse(vm.TrackInventory);
        Assert.AreEqual("Categoría sintética nueva",vm.Categories.Single(x=>x.Id==vm.ProductCategoryId).Name);Assert.IsFalse(vm.IsInlineCategoryEditor);

        await vm.SaveAsync();
        Assert.AreEqual("CM-NUEVA-01",vm.SelectedProduct?.Product.Sku);
        vm.EditProduct();await WaitUntil(()=>vm.CurrentPhotoStatus=="Foto provisional");
        Assert.AreEqual("PROVISIONAL",vm.PhotoProvenance,"Editing an existing provisional image must not silently relabel it as real.");
        fixture.AssertNoCommercialEffects();
    }

    [TestMethod]
    [DataRow(1280,820)]
    [DataRow(1920,1080)]
    public Task CardsAndHumanImageLanguageRemainReadableAtReviewSizes(int width,int height)=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var pos=fixture.Pos();await pos.EnterAsync();await pos.CategoryAsync(fixture.Savory.Id);
        var posView=new PosView{DataContext=pos};WpfTestHost.Layout(posView,width-430,height-180);
        var name=Descendants<TextBlock>(posView).Single(x=>x.Text==fixture.LongProduct.Name);
        Assert.AreEqual(TextWrapping.Wrap,name.TextWrapping);Assert.IsTrue(name.ActualHeight<=name.MaxHeight+0.5);Assert.IsTrue(name.ActualWidth>0);
        Assert.IsTrue(Descendants<TextBlock>(posView).Any(x=>x.Text.EndsWith("16.00",StringComparison.Ordinal)));
        Assert.IsTrue(Descendants<TextBlock>(posView).Any(x=>x.Text=="Sin imagen"));

        var catalog=fixture.Catalog();await catalog.LoadAsync();catalog.SelectedProduct=catalog.Products.Single(x=>x.Product.Id==fixture.LongProduct.Id);catalog.EditProduct();
        await WaitUntil(()=>catalog.CurrentPhotoStatus=="Foto provisional");
        var catalogView=new CatalogView{DataContext=catalog};WpfTestHost.Layout(catalogView,width-220,height*2);
        Assert.IsTrue(Descendants<TextBlock>(catalogView).Any(x=>x.Text=="Origen de la imagen"));
        Assert.IsTrue(Descendants<TextBlock>(catalogView).Any(x=>x.Text=="Foto provisional"));
        fixture.AssertNoCommercialEffects();
    });

    private static void SendKey(UIElement source,Window window,Key key)
    {
        var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};
        source.RaiseEvent(args);if(!args.Handled){args.RoutedEvent=Keyboard.KeyDownEvent;source.RaiseEvent(args);}
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private static async Task WaitUntil(Func<bool> done){var limit=DateTime.UtcNow.AddSeconds(8);while(!done()&&DateTime.UtcNow<limit)await Task.Delay(35);Assert.IsTrue(done());}

    private sealed class Fixture:IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"CasaMiga1K4A",Guid.NewGuid().ToString("N"));
        private readonly Database database;private readonly SqliteLocalUnitOfWork work;private readonly UserSession user=new();private readonly IClock clock=new SystemClock();private readonly TerminalIdentity terminal;private readonly CatalogService catalog;private readonly IProductPhotoUiGateway photos=new FakePhotos();
        public Category Sweet{get;}public Category Savory{get;}public Product LongProduct{get;}
        public Fixture()
        {
            Directory.CreateDirectory(root);database=new(Path.Combine(root,"casamiga.db"));database.Initialize();work=new(database);terminal=TerminalConfiguration.LoadOrCreate(root);work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
            var identity=new IdentityService(work,new LocalPasswords(),clock,user);var password=Guid.NewGuid().ToString("N")+"!Aa";identity.Bootstrap("admin","Administrador sintético",password);Assert.IsTrue(identity.Login("admin",password));
            catalog=new(work,user,clock,terminal.Id);Sweet=catalog.CreateCategory("Pan dulce");Savory=catalog.CreateCategory("Pan salado");var beverage=catalog.CreateCategory("Bebida");
            catalog.CreateProduct(Sweet.Id,"CM-DULCE-01","Concha sintética","",new Money(1300));catalog.CreateProduct(Savory.Id,"CM-003","Bolillo mantequilla","",new Money(800));
            LongProduct=catalog.CreateProduct(Savory.Id,"CM-LARGO-01","Producto con un nombre deliberadamente largo para verificar dos renglones","",new Money(1600));
            var inactive=catalog.CreateProduct(Savory.Id,"CM-INACTIVO-01","Producto inactivo sintético","",new Money(900));catalog.SetProductActive(inactive.Id,false);
            catalog.CreateProduct(beverage.Id,"CM-BEBIDA-01","Bebida sintética","",new Money(1200));
            var actor=user.Current??throw new AssertFailedException("Synthetic identity was not established.");
            work.Run(store=>{store.AddCashSession(CashSession.Open(Guid.NewGuid(),terminal.Id,actor.Id,clock.UtcNow,new Money(0)));return true;});
        }
        public CatalogViewModel Catalog()=>new(catalog,photos,true,(_,_)=>{});
        public PosViewModel Pos()
        {
            var settings=new SettingsProvider();
            var checkout=new CheckoutService(work,user,clock,terminal.Id,new CheckoutJournal(root),new CheckoutReceiptSnapshot(settings,TimeZoneInfo.Utc));
            var receipts=new ReceiptService(work,user,clock,terminal.Id,TimeZoneInfo.Utc,settings,new EscPosReceiptRenderer(),new NoPrintTransport());
            return new(new PosService(work,user,terminal.Id),checkout,receipts,photos,()=>"Administrador sintético",(_,_)=>{});
        }
        public void AssertNoCommercialEffects()
        {
            using var connection=database.Open(true);foreach(var table in new[]{"sale","inventory_operation","production_batch","receipt_print_request"}){using var command=connection.CreateCommand();command.CommandText=$"SELECT count(*) FROM {table}";Assert.AreEqual(0L,(long)command.ExecuteScalar()!,table);}
            using var audit=connection.CreateCommand();audit.CommandText="SELECT count(*) FROM audit_entry WHERE action IN ('cash.open','cash.close')";Assert.AreEqual(0L,(long)audit.ExecuteScalar()!,"cash operations");
        }
        public void Dispose()=>Directory.Delete(root,true);
    }
    private sealed class FakePhotos:IProductPhotoUiGateway
    {
        public Task<ProductPhotoVisual> LoadAsync(Guid productId,CancellationToken cancellationToken=default)=>Task.FromResult(new ProductPhotoVisual(1,Guid.NewGuid(),"TEST","Foto provisional",null,null));
        public Task<ProductPhotoVisual> ImportAsync(Guid productId,long revision,string originalName,string provenance,PhotoCrop crop,byte[] original,CancellationToken cancellationToken=default)=>Task.FromResult(new ProductPhotoVisual(revision+1,Guid.NewGuid(),"TEST",provenance=="PROVISIONAL"?"Foto provisional":"Foto real",null,null));
        public Task<ProductPhotoVisual> RemoveAsync(Guid productId,long revision,CancellationToken cancellationToken=default)=>Task.FromResult(new ProductPhotoVisual(revision+1,null,null,"Sin foto",null,null));
    }
    private sealed class SettingsProvider:IPrintingSettingsProvider
    {
        public PrintingSettingsResult Read()=>new(new(1,false,"TEST",48,"ibm850","Disabled",false,new BusinessReceiptProfile("CASA MIGA PRUEBA"),2),null);
    }
    private sealed class NoPrintTransport:IReceiptPrintTransport
    {
        public PrintSubmissionResult Submit(PrintSubmission submission)=>throw new AssertFailedException("The usability fixture must never print.");
    }
}
