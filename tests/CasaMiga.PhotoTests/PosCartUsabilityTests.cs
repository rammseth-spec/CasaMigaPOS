using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class PosCartUsabilityTests
{
    [TestMethod]
    public async Task CartSummaryQuantitiesTotalsAndEmptyStateStayConsistent()
    {
        using var fixture=new Fixture();var vm=fixture.Pos();await vm.EnterAsync();
        Assert.AreEqual("0 productos · 0 piezas",vm.CartSummary);Assert.AreEqual("$0.00",vm.Subtotal);Assert.AreEqual("$0.00",vm.Total);Assert.IsFalse(vm.CanCheckout);

        await vm.AddAsync(fixture.Products[0].Id);
        Assert.AreEqual("1 producto · 1 pieza",vm.CartSummary);Assert.AreEqual(1,vm.Lines.Count);Assert.AreEqual("$13.00",vm.Total);
        await vm.AddAsync(fixture.Products[0].Id);
        Assert.AreEqual("1 producto · 2 piezas",vm.CartSummary);Assert.AreEqual(2,vm.Lines.Single().Quantity);Assert.AreEqual("$26.00",vm.Total);
        await vm.AddAsync(fixture.Products[1].Id);
        Assert.AreEqual("2 productos · 3 piezas",vm.CartSummary);Assert.AreEqual(2,vm.Lines.Count);Assert.AreEqual("$41.00",vm.Subtotal);

        await vm.DecrementAsync(fixture.Products[0].Id);Assert.AreEqual(1,vm.Lines.Single(x=>x.Id==fixture.Products[0].Id).Quantity);
        Assert.IsFalse(vm.Lines.Single(x=>x.Id==fixture.Products[0].Id).CanDecrease,"At one piece the minus action stays disabled; removal remains explicit.");
        await vm.DecrementAsync(fixture.Products[0].Id);
        Assert.AreEqual(1,vm.Lines.Single(x=>x.Id==fixture.Products[0].Id).Quantity);StringAssert.Contains(vm.Message,"cantidad mínima");
        await vm.RemoveAsync(fixture.Products[0].Id);Assert.AreEqual("1 producto · 1 pieza",vm.CartSummary);
        vm.Discard(true);Assert.AreEqual("0 productos · 0 piezas",vm.CartSummary);Assert.IsFalse(vm.CanCheckout);
        fixture.AssertNoCommercialEffects();
    }

    [TestMethod]
    public async Task ConcurrentCartCommandsAreSerializedWithoutDuplicatesOrLostUpdates()
    {
        using var fixture=new Fixture();var vm=fixture.Pos();await vm.EnterAsync();
        await Task.WhenAll(Enumerable.Range(0,6).Select(_=>vm.AddAsync(fixture.Products[0].Id)));
        Assert.AreEqual(1,vm.Lines.Count);Assert.AreEqual(6,vm.Lines.Single().Quantity);Assert.AreEqual("1 producto · 6 piezas",vm.CartSummary);Assert.AreEqual("$78.00",vm.Total);
        await Task.WhenAll(Enumerable.Range(0,3).Select(_=>vm.IncrementAsync(fixture.Products[0].Id)));
        Assert.AreEqual(9,vm.Lines.Single().Quantity);Assert.AreEqual("$117.00",vm.Total);
        fixture.AssertNoCommercialEffects();
    }

    [TestMethod]
    [DataRow(1280,820)]
    [DataRow(1920,1080)]
    public Task CartRowsRemainReadableScrollableAndKeepTheFooterFixed(int width,int height)=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var vm=fixture.Pos();await vm.EnterAsync();
        foreach(var product in fixture.Products.Take(6))await vm.AddAsync(product.Id);
        var contentWidth=width-280;var contentHeight=height-190;var view=new PosView{DataContext=vm};var window=new Window{Content=view,Width=contentWidth,Height=contentHeight,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        try
        {
            window.Show();WpfTestHost.Layout(window,contentWidth,contentHeight);
            var cart=Descendants<ScrollViewer>(view).Single(x=>AutomationProperties.GetName(x)=="Partidas del carrito");
            Assert.IsTrue(cart.ViewportHeight>0);if(width==1280)Assert.IsTrue(cart.ExtentHeight>cart.ViewportHeight,"Six compact rows must use the cart's own vertical scroll at 1280x820.");Assert.AreEqual(0d,cart.ScrollableWidth,0.5);
            var fullyVisible=Descendants<Border>(view).Where(x=>fixture.Products.Any(p=>AutomationProperties.GetName(x)==p.Name)).Count(x=>IsFullyVisible(x,cart));
            Assert.IsTrue(fullyVisible>=2,$"Expected at least two rows with photo at {width}x{height}; found {fullyVisible}. Viewport={cart.ViewportHeight:F1}, extent={cart.ExtentHeight:F1}.");
            var longName=Descendants<TextBlock>(cart).Single(x=>x.Text==fixture.LongProduct.Name);Assert.AreEqual(TextTrimming.CharacterEllipsis,longName.TextTrimming);Assert.IsTrue(longName.ActualHeight<=22,"One line; the full name is in the tooltip.");
            var pay=Descendants<Button>(view).Single(x=>AutomationProperties.GetName(x)=="Cobrar");var footerY=pay.TransformToAncestor(view).Transform(new Point()).Y;
            cart.ScrollToEnd();WpfTestHost.Layout(window,contentWidth,contentHeight);
            Assert.AreEqual(footerY,pay.TransformToAncestor(view).Transform(new Point()).Y,0.5,"The totals and actions must remain fixed while cart lines scroll.");
            Assert.AreEqual("6 productos · 6 piezas",vm.CartSummary);
        }
        finally{window.Close();}
        fixture.AssertNoCommercialEffects();
    });

    [TestMethod]
    public Task ANewProductOutsideTheViewportIsBroughtIntoViewWithoutDuplicatingLines()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var vm=fixture.Pos();await vm.EnterAsync();
        foreach(var product in fixture.Products.Take(5))await vm.AddAsync(product.Id);
        var view=new PosView{DataContext=vm};var window=new Window{Content=view,Width=1000,Height=630,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        try
        {
            window.Show();WpfTestHost.Layout(window,1000,630);
            var cart=Descendants<ScrollViewer>(view).Single(x=>AutomationProperties.GetName(x)=="Partidas del carrito");cart.ScrollToTop();
            var card=Descendants<Button>(view).Single(x=>x.Tag is Guid id&&id==fixture.Products[5].Id);
            card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(()=>!vm.Busy&&vm.Lines.Any(x=>x.Id==fixture.Products[5].Id));
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            WpfTestHost.Layout(window,1000,630);
            Assert.IsTrue(cart.VerticalOffset>0,"A newly added line outside the viewport should become visible.");
            Assert.AreEqual(6,vm.Lines.Count);

            var before=cart.VerticalOffset;card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(()=>!vm.Busy&&vm.Lines.Single(x=>x.Id==fixture.Products[5].Id).Quantity==2);
            Assert.AreEqual(6,vm.Lines.Count);Assert.AreEqual(before,cart.VerticalOffset,0.5,"Incrementing an existing line should not force the cart to another position.");
        }
        finally{window.Close();}
        fixture.AssertNoCommercialEffects();
    });

    [TestMethod]
    public Task EmptyCartShowsZeroTotalsAndDisablesEveryCheckoutAction()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var vm=fixture.Pos();await vm.EnterAsync();var view=new PosView{DataContext=vm};WpfTestHost.Layout(view,1280,820);
        foreach(var text in new[]{"Carrito vacío","Toca un pan para agregarlo."})Assert.IsTrue(Descendants<TextBlock>(view).Any(x=>x.Text==text&&((FrameworkElement)x.Parent).Visibility==Visibility.Visible),text);
        Assert.AreEqual(2,Descendants<TextBlock>(view).Count(x=>x.Text=="$0.00"));
        foreach(var label in new[]{"Limpiar","Cobrar","Cobrar e imprimir ticket"})Assert.IsFalse(Descendants<Button>(view).Single(x=>AutomationProperties.GetName(x)==label).IsEnabled,label);
        fixture.AssertNoCommercialEffects();
    });

    [TestMethod]
    public Task CartMutationsKeepCatalogContainersImagesAndScrollStable()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var vm=fixture.Pos();await vm.EnterAsync();
        await vm.CategoryAsync(fixture.Category.Id);vm.Search="CART";await vm.SearchAsync();
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var products=vm.Products;var view=new PosView{DataContext=vm};var window=new Window{Content=view,Width=1000,Height=630,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        try
        {
            window.Show();WpfTestHost.Layout(window,1000,630);
            var productList=Descendants<ItemsControl>(view).Single(x=>ReferenceEquals(x.ItemsSource,products));
            var catalogScroll=Descendants<ScrollViewer>(view).Single(x=>Descendants<ItemsControl>(x).Any(y=>ReferenceEquals(y,productList)));
            catalogScroll.ScrollToVerticalOffset(Math.Min(120,catalogScroll.ScrollableHeight));WpfTestHost.Layout(window,1000,630);
            var initialOffset=catalogScroll.VerticalOffset;var card=Descendants<Button>(view).Single(x=>x.Tag is Guid id&&id==fixture.Products[0].Id);var content=card.Content;
            var search=vm.Search;var category=vm.Categories.Single(x=>x.Selected).Id;var pageLabel=vm.PageLabel;
            var enabledTransitions=0;card.IsEnabledChanged+=(_,_)=>enabledTransitions++;
            var broadNotifications=0;vm.PropertyChanged+=(_,args)=>{if(string.IsNullOrEmpty(args.PropertyName)||args.PropertyName==nameof(vm.Products))broadNotifications++;};
            var initialPhotoLoads=fixture.PhotoLoadCount;

            await vm.AddAsync(fixture.Products[0].Id);await vm.AddAsync(fixture.Products[0].Id);await vm.IncrementAsync(fixture.Products[0].Id);
            await vm.DecrementAsync(fixture.Products[0].Id);await vm.RemoveAsync(fixture.Products[0].Id);await vm.AddAsync(fixture.Products[1].Id);vm.Discard(true);
            WpfTestHost.Layout(window,1000,630);

            Assert.AreSame(products,vm.Products);Assert.AreSame(products,productList.ItemsSource);Assert.AreSame(card,Descendants<Button>(view).Single(x=>x.Tag is Guid id&&id==fixture.Products[0].Id));Assert.AreSame(content,card.Content);
            Assert.AreEqual(initialPhotoLoads,fixture.PhotoLoadCount,"Cart mutations must not reload product photos.");Assert.AreEqual(initialOffset,catalogScroll.VerticalOffset,0.5,"Cart mutations must preserve catalog scroll.");
            Assert.AreEqual(search,vm.Search);Assert.AreEqual(category,vm.Categories.Single(x=>x.Selected).Id);Assert.AreEqual(pageLabel,vm.PageLabel,"Cart mutations must preserve the active search, category and page.");
            Assert.AreEqual(0,enabledTransitions,$"Catalog cards must not be disabled and re-enabled by cart mutations. Broad notifications: {broadNotifications}.");Assert.AreEqual(0,broadNotifications,"Cart mutations must not notify the catalog or all properties.");
        }
        finally{window.Close();}
        fixture.AssertNoCommercialEffects();
    });

    private static bool IsFullyVisible(FrameworkElement child,FrameworkElement viewport)
    {
        try{var point=child.TransformToAncestor(viewport).Transform(new Point());return point.Y>=-0.5&&point.Y+child.ActualHeight<=viewport.ActualHeight+0.5;}catch(InvalidOperationException){return false;}
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private static async Task WaitUntil(Func<bool> done){var limit=DateTime.UtcNow.AddSeconds(8);while(!done()&&DateTime.UtcNow<limit)await Task.Delay(30);Assert.IsTrue(done());}

    private sealed class Fixture:IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"CasaMiga1K4B",Guid.NewGuid().ToString("N"));
        private readonly Database database;private readonly SqliteLocalUnitOfWork work;private readonly UserSession user=new();private readonly IClock clock=new SystemClock();private readonly TerminalIdentity terminal;private readonly FakePhotos photos=new();
        public IReadOnlyList<Product> Products{get;}public Product LongProduct=>Products[4];public Category Category{get;}
        public int PhotoLoadCount=>photos.LoadCount;
        public Fixture()
        {
            Directory.CreateDirectory(root);database=new(Path.Combine(root,"casamiga.db"));database.Initialize();work=new(database);terminal=TerminalConfiguration.LoadOrCreate(root);work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
            var identity=new IdentityService(work,new LocalPasswords(),clock,user);var password="Aa1!"+Guid.NewGuid().ToString("N")[..10];identity.Bootstrap("admin","Administrador sintético",password);Assert.IsTrue(identity.Login("admin",password));
            var catalog=new CatalogService(work,user,clock,terminal.Id);Category=catalog.CreateCategory("Pan dulce");
            var names=new[]{"Concha sintética","Dona sintética","Cuernito sintético","Galleta sintética","Producto con un nombre deliberadamente largo que debe ocupar como máximo dos renglones","Muffin sintético","Panqué sintético"};
            Products=names.Select((name,index)=>catalog.CreateProduct(Category.Id,$"CART-{index+1:00}",name,"",new Money(1300+index*200))).ToArray();
            var actor=user.Current??throw new AssertFailedException("Synthetic identity was not established.");work.Run(store=>{store.AddCashSession(CashSession.Open(Guid.NewGuid(),terminal.Id,actor.Id,clock.UtcNow,new Money(0)));return true;});
        }
        public PosViewModel Pos()
        {
            var settings=new SettingsProvider();var checkout=new CheckoutService(work,user,clock,terminal.Id,new CheckoutJournal(root),new CheckoutReceiptSnapshot(settings,TimeZoneInfo.Utc));var receipts=new ReceiptService(work,user,clock,terminal.Id,TimeZoneInfo.Utc,settings,new EscPosReceiptRenderer(),new NoPrintTransport());
            return new(new PosService(work,user,terminal.Id),checkout,receipts,photos,()=>"Administrador sintético",(_,_)=>{});
        }
        public void AssertNoCommercialEffects()
        {
            using var connection=database.Open(true);foreach(var table in new[]{"sale","inventory_operation","production_batch","receipt_print_request"}){using var command=connection.CreateCommand();command.CommandText=$"SELECT count(*) FROM {table}";Assert.AreEqual(0L,(long)command.ExecuteScalar()!,table);}
            using var sessions=connection.CreateCommand();sessions.CommandText="SELECT count(*) FROM cash_session";Assert.AreEqual(1L,(long)sessions.ExecuteScalar()!);
        }
        public void Dispose()=>Directory.Delete(root,true);
    }
    private sealed class FakePhotos:IProductPhotoUiGateway
    {
        public int LoadCount;
        public Task<ProductPhotoVisual> LoadAsync(Guid productId,CancellationToken cancellationToken=default){Interlocked.Increment(ref LoadCount);return Task.FromResult(new ProductPhotoVisual(1,null,null,"Sin foto",null,null));}
        public Task<ProductPhotoVisual> ImportAsync(Guid productId,long revision,string originalName,string provenance,PhotoCrop crop,byte[] original,CancellationToken cancellationToken=default)=>throw new AssertFailedException("Cart tests must not import photos.");
        public Task<ProductPhotoVisual> RemoveAsync(Guid productId,long revision,CancellationToken cancellationToken=default)=>throw new AssertFailedException("Cart tests must not remove photos.");
    }
    private sealed class SettingsProvider:IPrintingSettingsProvider
    {
        public PrintingSettingsResult Read()=>new(new(1,false,"TEST",48,"ibm850","Disabled",false,new BusinessReceiptProfile("CASA MIGA PRUEBA"),2),null);
    }
    private sealed class NoPrintTransport:IReceiptPrintTransport
    {
        public PrintSubmissionResult Submit(PrintSubmission submission)=>throw new AssertFailedException("Cart tests must never print.");
    }
}
