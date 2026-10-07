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
public sealed class SalesHistoryUsabilityTests
{
    [TestMethod]
    public async Task HumanFiltersUseLocalDatesAndTheRealCurrentCashSession()
    {
        using var fixture=new Fixture();var vm=fixture.ViewModel();await vm.LoadAsync();
        Assert.AreEqual("Todos · 5 ventas",vm.FilterSummary);Assert.IsTrue(vm.Sales.All(x=>x.HumanIdentifier.StartsWith("CAJA PRUEBA · Venta #")));
        Assert.IsTrue(vm.Sales.All(x=>x.Method=="Efectivo"));Assert.IsFalse(vm.Sales.Any(x=>x.HumanIdentifier.Contains(fixture.CurrentSessionId.ToString(),StringComparison.OrdinalIgnoreCase)));

        await vm.TodayAsync();Assert.AreEqual("Hoy · 3 ventas",vm.FilterSummary);Assert.IsTrue(vm.Sales.All(x=>x.Date.StartsWith("07/10/2026 · ")));
        await vm.YesterdayAsync();Assert.AreEqual("Ayer · 1 venta",vm.FilterSummary);Assert.AreEqual("06/10/2026 · 12:00",vm.Sales.Single().Date);
        vm.FromDate=new DateTime(2026,10,6);vm.ThroughDate=new DateTime(2026,10,7);await vm.SearchDatesAsync();Assert.AreEqual("Rango personalizado · 4 ventas",vm.FilterSummary);
        await vm.CurrentSessionAsync();Assert.AreEqual("Sesión actual · 2 ventas",vm.FilterSummary);Assert.IsTrue(vm.Sales.All(x=>fixture.CurrentSaleIds.Contains(x.SaleId)));

        var withoutSession=fixture.ViewModel(()=>null);await withoutSession.CurrentSessionAsync();
        Assert.AreEqual("No hay una sesión de caja abierta.",withoutSession.Message);Assert.AreEqual("Sesión actual · 0 ventas",withoutSession.FilterSummary);Assert.HasCount(0,withoutSession.Sales);
        var emptySession=fixture.ViewModel(()=>Guid.NewGuid());await emptySession.CurrentSessionAsync();
        Assert.IsTrue(emptySession.HasNoSales);Assert.AreEqual("No hay ventas registradas en esta sesión.",emptySession.EmptyMessage);Assert.AreEqual("Sesión actual · 0 ventas",emptySession.FilterSummary);
        fixture.AssertReadOnly();
    }

    [TestMethod]
    public async Task DetailUsesSnapshotsQuantitiesAndFinancialLabelsWithoutTechnicalIds()
    {
        using var fixture=new Fixture();var vm=fixture.ViewModel();await vm.CurrentSessionAsync();vm.SelectedSale=vm.Sales.Single(x=>x.SaleId==fixture.DetailedSaleId);await vm.OpenDetailAsync();
        StringAssert.StartsWith(vm.DetailTitle,"CAJA PRUEBA · Venta #");StringAssert.Contains(vm.DetailContext,"Responsable: Administrador de prueba");
        CollectionAssert.AreEqual(new[]{"2 piezas","1 pieza"},vm.DetailItems.Select(x=>x.Quantity).ToArray());
        Assert.AreEqual("$41.00",vm.DetailSubtotal);Assert.AreEqual("$41.00",vm.DetailTotal);Assert.AreEqual("Efectivo",vm.DetailPaymentMethod);Assert.AreEqual("$50.00",vm.DetailReceived);Assert.AreEqual("$9.00",vm.DetailChange);
        var text=string.Join('|',new[]{vm.DetailTitle,vm.DetailContext,vm.DetailPaymentMethod}.Concat(vm.DetailItems.SelectMany(x=>new[]{x.Name,x.Quantity,x.UnitPrice,x.LineTotal})));
        Assert.IsFalse(text.Contains(fixture.CurrentSessionId.ToString(),StringComparison.OrdinalIgnoreCase));Assert.IsFalse(text.Contains("CASH",StringComparison.Ordinal));Assert.IsFalse(text.Contains("Aplicado",StringComparison.Ordinal));
        Assert.IsTrue(vm.ReprintAllowed,"The administrator keeps the dedicated reprint permission, without executing it.");fixture.AssertReadOnly();
    }

    [TestMethod]
    [DataRow(1280,820)]
    [DataRow(1920,1080)]
    public Task HistoryAndDetailFitReviewSizesWithoutHorizontalScrolling(int width,int height)=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var vm=fixture.ViewModel();await vm.LoadAsync();
        var contentWidth=width-280;var contentHeight=height-190;var view=new SalesHistoryView{DataContext=vm};var window=new Window{Content=view,Width=contentWidth,Height=contentHeight,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        try
        {
            window.Show();WpfTestHost.Layout(window,contentWidth,contentHeight);
            var grid=Descendants<DataGrid>(view).Single(x=>AutomationProperties.GetName(x)=="Historial de ventas"&&x.Visibility==Visibility.Visible);
            Assert.IsFalse(grid.Columns.Any(x=>Equals(x.Header,"Folio")||Equals(x.Header,"CashSessionId")));Assert.AreEqual(0d,Descendants<ScrollViewer>(grid).First().ScrollableWidth,0.5);
            Assert.IsTrue(Descendants<Button>(view).Any(x=>Equals(x.Content,"Sesión actual")&&x.IsVisible));
            vm.SelectedSale=vm.Sales.First();await vm.OpenDetailAsync();WpfTestHost.Layout(window,contentWidth,contentHeight);
            var detail=Descendants<DataGrid>(view).Single(x=>AutomationProperties.GetName(x)=="Partidas de venta"&&x.Visibility==Visibility.Visible);
            Assert.AreEqual(0d,Descendants<ScrollViewer>(detail).First().ScrollableWidth,0.5);Assert.IsTrue(Descendants<Button>(view).Any(x=>Equals(x.Content,"Volver")&&x.IsVisible));
        }
        finally{window.Close();}
        fixture.AssertReadOnly();
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}
    }

    private sealed class Fixture:IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"CasaMiga1K4C",Guid.NewGuid().ToString("N"));
        private readonly Database database;private readonly SqliteLocalUnitOfWork work;private readonly UserSession user=new();private readonly FixedClock clock=new();private readonly TerminalIdentity terminal;private readonly Product concha;private readonly Product dona;
        private readonly long salesBefore;private readonly long paymentsBefore;private readonly long auditsBefore;private readonly Guid priorSessionId;
        public Guid CurrentSessionId{get;}public IReadOnlySet<Guid> CurrentSaleIds{get;}public Guid DetailedSaleId{get;}
        public Fixture()
        {
            Directory.CreateDirectory(root);database=new(Path.Combine(root,"casamiga.db"));database.Initialize();work=new(database);terminal=new(Guid.NewGuid(),"CAJA PRUEBA");work.Run(store=>{store.EnsureTerminal(terminal,clock.UtcNow);return true;});
            var identity=new IdentityService(work,new LocalPasswords(),clock,user);var password=Guid.NewGuid().ToString("N")+"!Aa";identity.Bootstrap("admin","Administrador de prueba",password);Assert.IsTrue(identity.Login("admin",password));
            var catalog=new CatalogService(work,user,clock,terminal.Id);var category=catalog.CreateCategory("Pan dulce");concha=catalog.CreateProduct(category.Id,"CONCHA","Concha de vainilla","",new Money(1300));dona=catalog.CreateProduct(category.Id,"DONA","Dona especial","",new Money(1500));
            priorSessionId=Guid.NewGuid();CurrentSessionId=Guid.NewGuid();var actor=user.Current!.Id;
            work.Run(store=>{store.AddCashSession(CashSession.Open(priorSessionId,terminal.Id,actor,clock.UtcNow.AddDays(-3),new Money(0)));return true;});
            _=Persist(priorSessionId,new(2026,10,5,18,0,0,TimeSpan.Zero),[(concha,1)],1300);
            _=Persist(priorSessionId,new(2026,10,6,18,0,0,TimeSpan.Zero),[(dona,1)],1500);
            _=Persist(priorSessionId,new(2026,10,7,15,0,0,TimeSpan.Zero),[(concha,1)],1300);
            work.Run(store=>{var prior=store.FindSession(priorSessionId)!;store.CloseSession(prior.Close(actor,clock.UtcNow.AddDays(-2),new Money(4100),new Money(4100)));store.AddCashSession(CashSession.Open(CurrentSessionId,terminal.Id,actor,clock.UtcNow.AddHours(-4),new Money(0)));return true;});
            DetailedSaleId=Persist(CurrentSessionId,new(2026,10,7,17,30,0,TimeSpan.Zero),[(concha,2),(dona,1)],5000);
            var second=Persist(CurrentSessionId,new(2026,10,7,17,45,0,TimeSpan.Zero),[(dona,1)],2000);CurrentSaleIds=new HashSet<Guid>{DetailedSaleId,second};
            salesBefore=Count("sale");paymentsBefore=Count("payment");auditsBefore=Count("audit_entry");
        }
        public SalesHistoryViewModel ViewModel(Func<Guid?>? session=null)
        {
            var settings=new SettingsProvider();var receipts=new ReceiptService(work,user,clock,terminal.Id,TimeZoneInfo.Utc,settings,new EscPosReceiptRenderer(),new NoPrintTransport());
            return new(new SalesHistoryService(work,user,Zone),receipts,Zone,clock,session??(()=>CurrentSessionId),(_,_)=>{});
        }
        private Guid Persist(Guid session,DateTimeOffset at,(Product Product,int Quantity)[] lines,long received)
            =>work.Run(store=>{var sale=new Sale(Guid.NewGuid(),Guid.NewGuid(),user.Current!.Id,terminal.Id,session,store.NextSaleSequence(terminal.Id),at,lines.Select(x=>new SaleItem(x.Product.Id,x.Product.Name,x.Product.SalePrice,x.Quantity)),Guid.NewGuid(),new Money(received));store.AddSale(sale);return sale.Id;});
        public void AssertReadOnly(){Assert.AreEqual(salesBefore,Count("sale"));Assert.AreEqual(paymentsBefore,Count("payment"));Assert.AreEqual(auditsBefore,Count("audit_entry"));}
        private long Count(string table){using var connection=database.Open(true);using var command=connection.CreateCommand();command.CommandText=$"SELECT count(*) FROM {table}";return (long)command.ExecuteScalar()!;}
        public void Dispose()=>Directory.Delete(root,true);
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.CreateCustomTimeZone("CasaMiga1K4C",TimeSpan.FromHours(-6),"CasaMiga1K4C","CasaMiga1K4C");
    }
    private sealed class FixedClock:IClock{public DateTimeOffset UtcNow=>new(2026,10,7,18,0,0,TimeSpan.Zero);}
    private sealed class SettingsProvider:IPrintingSettingsProvider{public PrintingSettingsResult Read()=>new(new(1,false,"TEST",48,"ibm850","Disabled",false,new BusinessReceiptProfile("CASA MIGA PRUEBA"),2),null);}
    private sealed class NoPrintTransport:IReceiptPrintTransport{public PrintSubmissionResult Submit(PrintSubmission submission)=>throw new AssertFailedException("History usability tests must never print.");}
}
