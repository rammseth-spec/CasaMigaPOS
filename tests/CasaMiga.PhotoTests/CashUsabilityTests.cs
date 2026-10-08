using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass,DoNotParallelize]
public sealed class CashUsabilityTests
{
    [TestMethod,DataRow(1280,820),DataRow(1920,1080)]
    public Task CashFlowIsBlindReadableAndImmutable(int width,int height)=>WpfTestHost.OnUi(async()=>
    {
        using var f=new Fixture();var vm=new MainViewModel();await vm.InitializeAsync(new(f.Root,true));await vm.LoginAsync("review",f.Password);
        Assert.IsTrue(vm.IsDashboard,vm.Message);Assert.IsTrue(vm.HasNoOpenCash);Assert.AreEqual("CAJA CERRADA",vm.CashLabel);Assert.IsTrue(vm.HasNoCashClosures);
        var window=new MainWindow{DataContext=vm,Width=width,Height=height,ShowInTaskbar=false};
        try
        {
            window.Show();WpfTestHost.Layout(window,width,height);
            vm.ShowOpening();vm.Amount="-1";await vm.OpenAsync();Assert.IsTrue(vm.IsOpening);Assert.IsFalse(vm.HasOpenCash);
            vm.Amount="500.001";await vm.OpenAsync();Assert.IsFalse(vm.HasOpenCash);
            vm.Amount="500.00";await Task.WhenAll(vm.OpenAsync(),vm.OpenAsync());Assert.IsTrue(vm.HasOpenCash,vm.Message);Assert.AreEqual(1L,f.Scalar("SELECT count(*) FROM cash_session"));
            StringAssert.Contains(vm.CashDetails,"$500.00");Assert.IsFalse(vm.CashDetails.Contains("Efectivo esperado"));
            await vm.ShowCashAsync();Assert.IsTrue(vm.CanClose);await vm.BeginClosingAsync();Assert.IsTrue(vm.IsCounting);Assert.AreEqual("",vm.ClosingDetails);
            WpfTestHost.Layout(window,width,height);Assert.IsFalse(Descendants<TextBlock>(window).Any(x=>x.IsVisible&&x.Text.Contains("Efectivo esperado")));
            foreach(var invalid in new[]{"","abc","NaN","Infinity","-1","1.001"}){vm.CountedAmount=invalid;await vm.SubmitCountAsync();Assert.IsTrue(vm.IsCounting);}
            vm.CountedAmount="530";await vm.SubmitCountAsync();Assert.IsTrue(vm.IsClosingResult,vm.Message);StringAssert.Contains(vm.ClosingDetails,"Sobrante de $30.00");StringAssert.Contains(vm.ClosingDetails,"Ventas en efectivo: $0.00");
            Assert.IsTrue(vm.RequiresDiscrepancyNote);Assert.IsFalse(vm.CanConfirmClose);vm.DiscrepancyNote="   ";await vm.ConfirmCloseAsync();Assert.IsTrue(vm.IsClosingResult);StringAssert.Contains(vm.Message,"nota");
            vm.DiscrepancyNote="Conteo sintético revisado";Assert.IsTrue(vm.CanConfirmClose);
            WpfTestHost.Layout(window,width,height);Capture(window,$"cash-result-{width}.png");
            var confirm=Descendants<Button>(window).Single(x=>Equals(x.Content,"Confirmar cierre definitivo"));Assert.IsTrue(confirm.IsVisible&&confirm.IsEnabled);Assert.IsTrue(confirm.ActualWidth>100);
            await Task.WhenAll(vm.ConfirmCloseAsync(),vm.ConfirmCloseAsync());Assert.IsTrue(vm.HasNoOpenCash,vm.Message);Assert.AreEqual(1,vm.CashClosures.Count);
            WpfTestHost.Layout(window,width,height);Capture(window,$"cash-list-{width}.png");
            var grid=Descendants<DataGrid>(window).Single(x=>x.IsVisible&&Equals(System.Windows.Automation.AutomationProperties.GetName(x),"Cierres anteriores"));
            Assert.AreEqual(0d,Descendants<ScrollViewer>(grid).First().ScrollableWidth,0.5);
            vm.SelectedCashClosure=vm.CashClosures[0];await vm.ShowCashClosureAsync();Assert.IsTrue(vm.HasCashClosureDetail);StringAssert.Contains(vm.CashClosureDetail!.Details,"Conteo sintético revisado");Assert.IsFalse(vm.CashClosureDetail.Details.Contains(vm.CashClosureDetail.Row.Id.ToString()));
            WpfTestHost.Layout(window,width,height);Capture(window,$"cash-history-{width}.png");
            Send(window,Key.F11);Assert.AreEqual(WindowStyle.None,window.WindowStyle);Send(window,Key.Escape);Assert.AreNotEqual(WindowStyle.None,window.WindowStyle);Assert.IsTrue(vm.HasCashClosureDetail);
            Send(window,Key.Escape);Assert.IsFalse(vm.HasCashClosureDetail);
            vm.ShowOpening();vm.Amount="0";await vm.OpenAsync();await vm.BeginClosingAsync();vm.CountedAmount="0";await vm.SubmitCountAsync();Assert.IsFalse(vm.RequiresDiscrepancyNote);Assert.IsTrue(vm.CanConfirmClose);await vm.ConfirmCloseAsync();Assert.AreEqual(2,vm.CashClosures.Count);
            Assert.AreEqual(0L,f.Scalar("SELECT count(*) FROM sale"));Assert.AreEqual(0L,f.Scalar("SELECT count(*) FROM receipt_print_request"));Assert.AreEqual(0L,f.Scalar("SELECT count(*) FROM receipt_print_event"));
        }
        finally{window.Close();vm.Logout();}
    });

    [TestMethod] public Task CashierCanViewHistoryButCannotCloseOthers()=>WpfTestHost.OnUi(async()=>
    {
        using var f=new Fixture();var vm=new MainViewModel();await vm.InitializeAsync(new(f.Root,true));await vm.LoginAsync("review",f.Password);vm.ShowOpening();vm.Amount="0";await vm.OpenAsync();vm.Logout();
        f.Scalar("UPDATE app_user SET role_name='Cashier'");await vm.LoginAsync("review",f.Password);Assert.IsTrue(vm.HasOpenCash);Assert.IsFalse(vm.CanClose);await vm.BeginClosingAsync();Assert.IsFalse(vm.IsCounting);StringAssert.Contains(vm.Message,"permiso");vm.Logout();
    });
    private static void Send(Window window,Key key)=>window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T item)yield return item;foreach(var nested in Descendants<T>(child))yield return nested;}}
    private static void Capture(Window window,string name)
    {
        var folder=Environment.GetEnvironmentVariable("CASAMIGA_CASH_EVIDENCE");if(string.IsNullOrEmpty(folder))return;
        Directory.CreateDirectory(folder);var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(folder,name));png.Save(file);
    }
    private sealed class Fixture:IDisposable
    {
        public string Root {get;}=Path.Combine(Path.GetTempPath(),"CasaMigaCashUi",Guid.NewGuid().ToString("N"));
        public string Password {get;}=Guid.NewGuid().ToString("N")+"!Aa";
        private readonly Database database;
        public Fixture()
        {
            Directory.CreateDirectory(Root);database=new(Path.Combine(Root,"casamiga.db"));database.Initialize();
            var work=new SqliteLocalUnitOfWork(database);var terminal=TerminalConfiguration.LoadOrCreate(Root);var clock=new SystemClock();work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
            new IdentityService(work,new LocalPasswords(),clock,new UserSession()).Bootstrap("review","Persona de revisión",Password);
        }
        public object? Scalar(string sql){using var c=database.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
        public void Dispose()=>Directory.Delete(Root,true);
    }
}
