using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            Assert.IsTrue(window.DiscrepancyNoteEditor.IsVisible);Assert.IsFalse(window.NoDiscrepancyNoteMessage.IsVisible);
            Assert.IsTrue(Descendants<TextBlock>(window.DiscrepancyNoteEditor).Any(x=>x.IsVisible&&x.Text=="25/500 caracteres"));
            var confirm=Descendants<Button>(window).Single(x=>Equals(x.Content,"Confirmar cierre definitivo"));Assert.IsTrue(confirm.IsVisible&&confirm.IsEnabled);Assert.IsTrue(confirm.ActualWidth>100);
            await Task.WhenAll(vm.ConfirmCloseAsync(),vm.ConfirmCloseAsync());Assert.IsTrue(vm.HasNoOpenCash,vm.Message);Assert.AreEqual(1,vm.CashClosures.Count);
            WpfTestHost.Layout(window,width,height);Capture(window,$"cash-list-{width}.png");
            var grid=Descendants<DataGrid>(window).Single(x=>x.IsVisible&&Equals(System.Windows.Automation.AutomationProperties.GetName(x),"Cierres anteriores"));
            Assert.AreEqual(0d,Descendants<ScrollViewer>(grid).First().ScrollableWidth,0.5);
            vm.SelectedCashClosure=vm.CashClosures[0];await vm.ShowCashClosureAsync();Assert.IsTrue(vm.HasCashClosureDetail);StringAssert.Contains(vm.CashClosureDetail!.Details,"Conteo sintético revisado");Assert.IsFalse(vm.CashClosureDetail.Details.Contains(vm.CashClosureDetail.Row.Id.ToString()));
            WpfTestHost.Layout(window,width,height);Capture(window,$"cash-history-{width}.png");
            var detailView=Descendants<CashHistoryView>(window).Single(x=>x.IsVisible);var detail=(FrameworkElement)detailView.FindName("CashClosureDetailPanel");
            Assert.IsTrue(detail.IsVisible);Assert.IsLessThan(390d,detail.ActualHeight);Assert.IsTrue(Descendants<Button>(detail).Any(x=>x.IsVisible&&Equals(x.Content,"Volver")));
            Assert.IsFalse(Descendants<Button>(detail).Any(x=>x.IsVisible&&Equals(x.Content,"Cerrar detalle")));
            Send(window,Key.F11);Assert.AreEqual(WindowStyle.None,window.WindowStyle);Send(window,Key.Escape);Assert.AreNotEqual(WindowStyle.None,window.WindowStyle);Assert.IsTrue(vm.HasCashClosureDetail);
            Send(window,Key.Escape);Assert.IsFalse(vm.HasCashClosureDetail);
            vm.ShowOpening();vm.Amount="0";await vm.OpenAsync();await vm.BeginClosingAsync();vm.CountedAmount="0";await vm.SubmitCountAsync();Assert.IsFalse(vm.RequiresDiscrepancyNote);Assert.IsTrue(vm.CanConfirmClose);
            WpfTestHost.Layout(window,width,height);Assert.IsFalse(window.DiscrepancyNoteEditor.IsVisible);Assert.IsTrue(window.NoDiscrepancyNoteMessage.IsVisible);
            Assert.IsFalse(Descendants<TextBox>(window).Any(x=>x.IsVisible&&System.Windows.Automation.AutomationProperties.GetName(x)=="Nota de discrepancia"));
            Capture(window,$"cash-result-exact-{width}.png");
            await vm.ConfirmCloseAsync();Assert.AreEqual(2,vm.CashClosures.Count);
            Assert.AreEqual(0L,f.Scalar("SELECT count(*) FROM sale"));Assert.AreEqual(0L,f.Scalar("SELECT count(*) FROM receipt_print_request"));Assert.AreEqual(0L,f.Scalar("SELECT count(*) FROM receipt_print_event"));
        }
        finally{window.Close();vm.Logout();}
    });

    [TestMethod,DataRow(1280,820,5),DataRow(1920,1080,6)]
    public Task CashHistoryUsesAvailableHeightAndKeepsPagingFooter(int width,int height,int minimumVisibleRows)=>WpfTestHost.OnUi(async()=>
    {
        using var f=new Fixture();f.SeedClosures(28);Assert.AreEqual(10L,f.Scalar("SELECT count(*) FROM cash_session WHERE discrepancy_note IS NULL"));var vm=new MainViewModel();await vm.InitializeAsync(new(f.Root,true));await vm.LoginAsync("review",f.Password);
        var window=new MainWindow{DataContext=vm,Width=width,Height=height,ShowInTaskbar=false};
        try
        {
            window.Show();WpfTestHost.Layout(window,width,height);
            Capture(window,$"cash-list-filled-{width}.png");
            var view=Descendants<CashHistoryView>(window).Single(x=>x.IsVisible);
            var grid=(DataGrid)view.FindName("CashHistoryGrid");var footer=(FrameworkElement)view.FindName("CashHistoryFooter");
            Assert.AreEqual(25,grid.Items.Count);Assert.IsTrue(grid.IsVisible&&footer.IsVisible);
            var header=Descendants<DataGridColumnHeader>(grid).First();
            var visibleRows=(int)Math.Floor((grid.ActualHeight-header.ActualHeight)/grid.MinRowHeight);
            Assert.IsGreaterThanOrEqualTo(minimumVisibleRows,visibleRows,$"Filas visibles a {width}x{height}: {visibleRows}; alto de tabla {grid.ActualHeight:F1}.");
            Assert.AreEqual(0d,Descendants<ScrollViewer>(grid).First().ScrollableWidth,0.5);
            Assert.IsLessThanOrEqualTo(view.ActualHeight+2,footer.TranslatePoint(new Point(0,footer.ActualHeight),view).Y);
            Assert.AreEqual("28 cierres · página 1 de 2",vm.CashHistorySummary);
            var withoutNote=vm.CashClosures.First(x=>x.Row.DiscrepancyNote is null);
            await vm.NextCashHistoryAsync();WpfTestHost.Layout(window,width,height);Assert.AreEqual(3,grid.Items.Count);Assert.AreEqual("28 cierres · página 2 de 2",vm.CashHistorySummary);
            Assert.IsGreaterThanOrEqualTo(minimumVisibleRows,(int)Math.Floor((grid.ActualHeight-header.ActualHeight)/grid.MinRowHeight));
            var withNote=vm.CashClosures.First(x=>x.Row.DiscrepancyNote is not null);vm.SelectedCashClosure=withNote;await vm.ShowCashClosureAsync();StringAssert.Contains(vm.CashClosureDetail!.Note,"SMOKE");vm.HideCashClosure();
            vm.SelectedCashClosure=withoutNote;await vm.ShowCashClosureAsync();Assert.AreEqual("Sin nota registrada",vm.CashClosureDetail!.Note);
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
        public void SeedClosures(int total)
        {
            var work=new SqliteLocalUnitOfWork(database);var identity=new UserSession();var clock=new SystemClock();
            Assert.IsTrue(new IdentityService(work,new LocalPasswords(),clock,identity).Login("review",Password));
            var terminal=TerminalConfiguration.LoadOrCreate(Root);var cash=new CashService(work,identity,clock);var closing=new CashClosingService(work,identity,clock);
            for(var i=0;i<total;i++)
            {
                var session=cash.Open(terminal.Id,new Money(50000)).Session;var kind=i%3;
                var counted=kind==0?50000L:kind==1?53000L:45000L;var note=kind==0?null:"SMOKE · diferencia sintética";
                closing.SubmitCount(session.Id,new Money(counted));var closed=closing.Close(session.Id,note);
                Assert.AreEqual(note,closed.DiscrepancyNote);
            }
            identity.SignOut();
        }
        public void Dispose()=>Directory.Delete(Root,true);
    }
}
