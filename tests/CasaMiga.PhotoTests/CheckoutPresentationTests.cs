using CasaMiga.Application;
using System.IO;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass]
public sealed class CheckoutPresentationTests
{
    private string directory=null!;private Database database=null!;private SqliteLocalUnitOfWork work=null!;private UserSession session=null!;private TerminalIdentity terminal=null!;private Product product=null!;private PosService pos=null!;private readonly IClock clock=new SystemClock();
    [TestInitialize]public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"CasaMigaCheckoutUi",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);database=new(Path.Combine(directory,"casamiga.db"));database.Initialize();work=new(database);session=new(new CheckoutJournal(directory));terminal=TerminalConfiguration.LoadOrCreate(directory);work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
        var identity=new IdentityService(work,new LocalPasswords(),clock,session);var password=Guid.NewGuid().ToString("N");identity.Bootstrap("admin","Administrador",password);Assert.IsTrue(identity.Login("admin",password));var catalog=new CatalogService(work,session,clock,terminal.Id);var category=catalog.CreateCategory("Pan");product=catalog.CreateProduct(category.Id,"P1","Concha","",new Money(1900));new CashService(work,session,clock).Open(terminal.Id,new Money(0));pos=new(work,session,terminal.Id);
    }
    [TestCleanup]public void Cleanup()=>Directory.Delete(directory,true);
    [TestMethod]public async Task CobrarPersistsHistoryWithoutPrinting()
    {
        var transport=new ProbeTransport();var vm=Create(transport);pos.Add(product.Id);vm=Create(transport);vm.Received="20.00";await vm.ConfirmAsync(false);
        Assert.IsNotNull(vm.Result);Assert.AreEqual(0,transport.Calls);Assert.AreEqual(1L,Count("sale"));StringAssert.Contains(vm.PrintMessage,"Historial");
    }
    [TestMethod]public async Task CobrarEImprimirPersistsThenUsesTransport()
    {
        pos.Add(product.Id);var transport=new ProbeTransport();var vm=Create(transport);vm.Received="20.00";await vm.ConfirmAsync(true);
        Assert.IsNotNull(vm.Result);Assert.AreEqual(1,transport.Calls);Assert.AreEqual(1L,Count("sale"));
    }
    [TestMethod]public async Task PrintFailureNeverRollsBackCheckout()
    {
        pos.Add(product.Id);var transport=new ProbeTransport{Failure=new("WRITE_PRINTER_5",false)};var vm=Create(transport);vm.Received="20.00";await vm.ConfirmAsync(true);
        Assert.IsNotNull(vm.Result);Assert.AreEqual(1L,Count("sale"));StringAssert.Contains(vm.PrintMessage,"reimprimirlo desde Historial");
    }
    [TestMethod]public async Task BothActionsShareBusyDoubleSubmitProtection()
    {
        pos.Add(product.Id);var transport=new BlockingTransport();var vm=Create(transport);vm.Received="20.00";var first=vm.ConfirmAsync(true);Assert.IsTrue(transport.Entered.Wait(TimeSpan.FromSeconds(5)));await vm.ConfirmAsync(false);transport.Release.Set();await first;
        Assert.AreEqual(1L,Count("sale"));Assert.AreEqual(1,transport.Calls);
    }
    [TestMethod]public void CropViewModelMovesAndResizesWithinImage()
    {
        var crop=new PhotoCropViewModel(1200,800);Assert.AreEqual(new PhotoCrop(200,0,800),crop.Crop);crop.ResizeBy(-300);crop.MoveBy(1000,1000);Assert.AreEqual(new PhotoCrop(700,300,500),crop.Crop);crop.ResizeBy(-1000);Assert.AreEqual(256,crop.Crop.Edge);
    }
    private CheckoutViewModel Create(IReceiptPrintTransport transport)
    {
        var settings=new SettingsProvider();var checkout=new CheckoutService(work,session,clock,terminal.Id,new CheckoutJournal(directory),new CheckoutReceiptSnapshot(settings,TimeZoneInfo.Utc));var receipts=new ReceiptService(work,session,clock,terminal.Id,TimeZoneInfo.Utc,settings,new EscPosReceiptRenderer(),transport);return new(checkout,receipts,new Money(1900),false);
    }
    private long Count(string table){using var connection=database.Open(true);using var command=connection.CreateCommand();command.CommandText=$"SELECT count(*) FROM {table}";return (long)command.ExecuteScalar()!;}
    private sealed class SettingsProvider:IPrintingSettingsProvider
    {
        private static BusinessReceiptProfile Profile=>new("CASA MIGA PRUEBA");
        public PrintingSettingsResult Read()=>new(new(1,true,"TEST",48,"windows-1252","Disabled",false,Profile,16),null);
    }
    private sealed class ProbeTransport:IReceiptPrintTransport
    {
        public int Calls;public PrintTransportException? Failure;public PrintSubmissionResult Submit(PrintSubmission submission){Interlocked.Increment(ref Calls);if(Failure is not null)throw Failure;return new(7);}
    }
    private sealed class BlockingTransport:IReceiptPrintTransport
    {
        public int Calls;public ManualResetEventSlim Entered{get;}=new(false);public ManualResetEventSlim Release{get;}=new(false);public PrintSubmissionResult Submit(PrintSubmission submission){Interlocked.Increment(ref Calls);Entered.Set();if(!Release.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException();return new(8);}
    }
}
