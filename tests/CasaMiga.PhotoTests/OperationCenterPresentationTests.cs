using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass,DoNotParallelize]
public sealed class OperationCenterPresentationTests
{
    [TestMethod]
    public Task HeaderOpensAnInTreeResponsivePanelAndEscapeClosesItBeforeFullscreen()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var window=Offscreen(new MainWindow{DataContext=fixture.Model});
        try
        {
            window.Show();WpfTestHost.Layout(window,1280,820);
            var trigger=Descendants<Button>(window).Single(x=>AutomationProperties.GetName(x)=="Centro operativo");
            trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WpfTestHost.Layout(window,1280,820);
            var panel=Descendants<OperationCenterView>(window).Single();
            Assert.IsTrue(panel.IsVisible);
            Assert.AreSame(window,Window.GetWindow(panel));
            Assert.AreEqual(0,Descendants<Popup>(window).Count(x=>x.IsOpen),"The principal surface is not a Popup.");
            Assert.IsTrue(panel.ActualWidth<=420&&panel.ActualHeight<=820);
            Send(window,Key.F11);Assert.AreEqual(WindowStyle.None,window.WindowStyle);
            trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.IsTrue(fixture.Model.Operations!.IsPanelOpen);
            Send(window,Key.Escape);
            Assert.IsFalse(fixture.Model.Operations.IsPanelOpen);
            Assert.AreEqual(WindowStyle.None,window.WindowStyle,"Escape closes the panel before leaving fullscreen.");
        }
        finally{window.Close();}
    });

    [TestMethod]
    public Task LoginShowsOnlyLocalClockAndKeepsTheCompositionInsideCommonResolutions()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();fixture.Model.Logout();
        foreach(var size in new[]{new Size(1280,820),new Size(1920,1080)})
        {
            var window=Offscreen(new MainWindow{DataContext=fixture.Model});
            try
            {
                window.Show();WpfTestHost.Layout(window,size.Width,size.Height);
                var clock=Descendants<Border>(window).Single(x=>AutomationProperties.GetName(x)=="Hora local");
                Assert.IsTrue(clock.IsVisible);
                Assert.IsTrue(IsInside(clock,window));
                Assert.IsFalse(Descendants<OperationCenterView>(window).Single().IsVisible,"Operational details remain hidden before login.");
                Assert.AreEqual(0,Descendants<TextBlock>(clock).Count(x=>x.Text.Contains("activo",StringComparison.OrdinalIgnoreCase)||x.Text.Contains("listo",StringComparison.OrdinalIgnoreCase)));
            }
            finally{window.Close();}
        }
    });

    [TestMethod]
    public Task AlertUsesOneNonBlockingPulseKeepsFullscreenAndRequestsBackgroundAttention()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var sounds=0;var attention=0;
        var window=Offscreen(new MainWindow{DataContext=fixture.Model});
        window.OperationAlertSound=()=>sounds++;
        window.TaskbarAttentionOverride=()=>attention++;
        window.Show();WpfTestHost.Layout(window,1280,820);
        var center=fixture.Model.Operations!;
        Assert.IsTrue(center.CreateTimer("Horno","0","1",OperationIconKind.Oven,OperationPriority.Urgent));
        Send(window,Key.F11);
        Assert.AreEqual(WindowStyle.None,window.WindowStyle);
        window.WindowState=WindowState.Minimized;
        center.Tick(DateTimeOffset.Now.AddMinutes(2));
        Assert.AreEqual(1,sounds);
        Assert.AreEqual(1,attention,"An inactive window requests taskbar attention without focus theft.");
        Assert.AreEqual(WindowStyle.None,window.WindowStyle,"The brief alert does not exit F11.");
        Assert.AreEqual(WindowState.Minimized,window.WindowState,"The alert never forces the window to the foreground.");
        Assert.AreEqual("1 urgente",center.HeaderStatus);
        var trigger=(Button)window.FindName("OperationsStatusButton");
        if(SystemParameters.ClientAreaAnimation)Assert.IsTrue(trigger.HasAnimatedProperties,"The 600 ms double pulse starts without blocking the dispatcher.");
        window.Close();

        Assert.IsTrue(center.CreateTimer("Segundo horno","0","1",OperationIconKind.Oven));
        center.Tick(DateTimeOffset.Now.AddMinutes(4));
        Assert.AreEqual(1,sounds,"Closing the window detaches the alert handler.");
    });

    [TestMethod]
    public Task OperationalHeaderUsesRealCashStateAndKeepsClockClickable()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var window=Offscreen(new MainWindow{DataContext=fixture.Model});
        try
        {
            window.Show();WpfTestHost.Layout(window,1280,820);
            Assert.IsFalse(fixture.Model.HasOpenCash);
            Assert.AreEqual("Caja cerrada",((TextBlock)window.FindName("CashStatusText")).Text);
            var clock=(Button)window.FindName("OperationsButton");
            clock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsTrue(fixture.Model.Operations!.IsPanelOpen,"The clock remains the entry point to the operations panel.");
            fixture.Model.Operations.ClosePanel();

            fixture.Model.ShowOpening();fixture.Model.Amount="0.00";await fixture.Model.OpenAsync();
            WpfTestHost.Layout(window,1280,820);
            Assert.IsTrue(fixture.Model.HasOpenCash,fixture.Model.Message);
            Assert.AreEqual("Caja abierta",((TextBlock)window.FindName("CashStatusText")).Text);
        }
        finally{window.Close();}
    });

    [TestMethod]
    public Task HeaderStatusIsHiddenWithoutActivityAndPrioritizesReadyUrgentAndActive()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var center=fixture.Model.Operations!;
        Assert.IsFalse(center.HasHeaderStatus);
        Assert.IsTrue(center.CreateTimer("Activo","0","10"));
        Assert.IsTrue(center.HasHeaderStatus);Assert.StartsWith("1 activo",center.HeaderStatus);
        Assert.IsTrue(center.CreateTimer("Listo","0","1"));
        center.Tick(DateTimeOffset.Now.AddMinutes(2));
        Assert.AreEqual("1 listo · 1 activo",center.HeaderStatus);
        Assert.IsTrue(center.CreateTimer("Urgente","0","1",OperationIconKind.Oven,OperationPriority.Urgent));
        center.Tick(DateTimeOffset.Now.AddMinutes(4));
        Assert.AreEqual("1 urgente",center.HeaderStatus,"Urgent ready work has precedence over lower-priority counts.");
    });

    [TestMethod]
    public Task HeaderFitsThreeTargetResolutionsAndOperationTicksDoNotRefreshShellViews()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var center=fixture.Model.Operations!;
        Assert.IsTrue(center.CreateTimer("Horno","0","10",OperationIconKind.Oven));
        var pos=fixture.Model.Pos;var catalog=fixture.Model.Catalog;var shellNotifications=0;
        fixture.Model.PropertyChanged+=(_,_)=>shellNotifications++;
        center.Tick(DateTimeOffset.Now.AddSeconds(5));
        Assert.AreSame(pos,fixture.Model.Pos);Assert.AreSame(catalog,fixture.Model.Catalog);Assert.AreEqual(0,shellNotifications);

        foreach(var size in new[]{new Size(1280,820),new Size(1920,1080),new Size(3440,1440)})
        {
            var window=Offscreen(new MainWindow{DataContext=fixture.Model});
            try
            {
                window.Show();WpfTestHost.Layout(window,size.Width,size.Height);
                foreach(var name in new[]{"CashStatusChip","OperationsStatusButton","OperationsButton","FullScreenButton"})
                {
                    var element=(FrameworkElement)window.FindName(name);
                    Assert.IsTrue(element.IsVisible,$"{name} must be visible at {size.Width}×{size.Height}.");
                    Assert.IsTrue(IsInside(element,window),$"{name} must stay inside the header at {size.Width}×{size.Height}.");
                }
            }
            finally{window.Close();}
        }

        var fullScreen=Offscreen(new MainWindow{DataContext=fixture.Model});
        try{fullScreen.Show();WpfTestHost.Layout(fullScreen,1280,820);Send(fullScreen,Key.F11);Assert.AreEqual(WindowStyle.None,fullScreen.WindowStyle);Assert.AreEqual("F11",((Button)fullScreen.FindName("FullScreenButton")).Content);}
        finally{fullScreen.Close();}
    });

    [TestMethod]
    public Task PanelRendersConsistentVectorIconsAndCompactSelectorsAtBothResolutions()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var center=fixture.Model.Operations!;
        Assert.IsTrue(center.CreateTimer("Entrega","0","5",OperationIconKind.Delivery));
        Assert.IsTrue(center.CreateTimer("Horneado","0","10",OperationIconKind.Oven));
        Assert.IsTrue(center.CreateTimer("Fermentación","0","15",OperationIconKind.Fermentation));
        Assert.IsTrue(center.SaveNote("Limpiar área","","",OperationIconKind.Cleaning));
        foreach(var size in new[]{new Size(1280,820),new Size(1920,1080)})
        {
            var window=Offscreen(new MainWindow{DataContext=fixture.Model});
            try
            {
                window.OperationAlertSound=()=>{};window.TaskbarAttentionOverride=()=>{};
                window.Show();center.OpenPanel();WpfTestHost.Layout(window,size.Width,size.Height);
                var panel=Descendants<OperationCenterView>(window).Single();
                Assert.IsTrue(panel.IsVisible);
                Assert.IsFalse(Descendants<Border>(panel).Single(x=>AutomationProperties.GetName(x)=="Editor de temporizador").IsVisible);
                Assert.IsFalse(Descendants<Border>(panel).Single(x=>AutomationProperties.GetName(x)=="Editor de nota").IsVisible);
                Assert.IsTrue(Descendants<System.Windows.Shapes.Path>(panel).Any(x=>AutomationProperties.GetName(x)=="Entrega"));
                Assert.IsTrue(Descendants<System.Windows.Shapes.Path>(panel).Any(x=>AutomationProperties.GetName(x)=="Limpieza"));
                var selectors=Descendants<ComboBox>(panel).Where(x=>AutomationProperties.GetName(x).Contains("Categoría",StringComparison.Ordinal)).ToArray();
                Assert.AreEqual(2,selectors.Length);
                if(size.Height==820)
                    Assert.IsTrue(Descendants<Border>(panel).Count(x=>AutomationProperties.GetName(x)=="Tarjeta de temporizador"&&IsInside(x,panel))>=3,
                        "At 1280×820 at least three compact timer cards fit before scrolling.");
                Assert.IsTrue(IsInside(panel,window));
            }
            finally{window.Close();}
        }
    });

    [TestMethod]
    public Task InlineEditorsAndSnoozeChoicesUseProgressiveDisclosureAndEscapePriority()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var center=fixture.Model.Operations!;
        var due=DateTimeOffset.Now.AddMinutes(-1);
        Assert.IsTrue(center.SaveNote("Revisar pedido",due.ToString("dd/MM/yyyy"),due.ToString("HH:mm")));
        center.Tick(DateTimeOffset.Now);
        var window=Offscreen(new MainWindow{DataContext=fixture.Model});
        try
        {
            window.OperationAlertSound=()=>{};window.TaskbarAttentionOverride=()=>{};
            window.Show();center.OpenPanel();WpfTestHost.Layout(window,1280,820);
            var panel=Descendants<OperationCenterView>(window).Single();
            var timerEditor=Descendants<Border>(panel).Single(x=>AutomationProperties.GetName(x)=="Editor de temporizador");
            var noteEditor=Descendants<Border>(panel).Single(x=>AutomationProperties.GetName(x)=="Editor de nota");
            Assert.IsFalse(timerEditor.IsVisible);
            Assert.IsFalse(noteEditor.IsVisible);

            Descendants<Button>(panel).Single(x=>AutomationProperties.GetName(x)=="Nuevo temporizador").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WpfTestHost.Layout(window,1280,820);
            Assert.IsTrue(timerEditor.IsVisible);
            Send(window,Key.Escape);
            WpfTestHost.Layout(window,1280,820);
            Assert.IsFalse(timerEditor.IsVisible,"The first Escape cancels the active editor.");
            Assert.IsTrue(center.IsPanelOpen,"Cancelling an editor must keep the operations panel open.");

            var postpone=Descendants<Button>(panel).Single(x=>x.Content?.ToString()=="Posponer"&&x.IsVisible);
            postpone.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WpfTestHost.Layout(window,1280,820);
            var choices=Descendants<WrapPanel>(panel).Single(x=>AutomationProperties.GetName(x)=="Opciones para posponer");
            Assert.IsTrue(choices.IsVisible);
            var five=Descendants<Button>(choices).Single(x=>x.Content?.ToString()=="5 min");
            five.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WpfTestHost.Layout(window,1280,820);
            Assert.IsFalse(choices.IsVisible);
            Assert.IsFalse(center.Notes.Single().IsReady);

            Send(window,Key.Escape);
            Assert.IsFalse(center.IsPanelOpen,"With no transient editor open, Escape closes the panel.");
        }
        finally{window.Close();}
    });

    private static bool IsInside(FrameworkElement child,FrameworkElement root)
    {
        var point=child.TransformToAncestor(root).Transform(new Point());
        return point.X>=0&&point.Y>=0&&point.X+child.ActualWidth<=root.ActualWidth+1&&point.Y+child.ActualHeight<=root.ActualHeight+1;
    }
    private static T Offscreen<T>(T window)where T:Window{window.Width=1280;window.Height=820;window.Left=-10000;window.Top=-10000;window.ShowInTaskbar=false;window.ShowActivated=false;window.WindowStartupLocation=WindowStartupLocation.Manual;return window;}
    private static void Send(Window window,Key key){var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};window.RaiseEvent(args);}
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject{for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}}
    private sealed class Fixture:IDisposable
    {
        private readonly string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"CasaMigaOperationsUi",Guid.NewGuid().ToString("N"));
        public MainViewModel Model{get;}=new();
        public async Task InitializeAsync(){await Model.InitializeAsync(new FoundationOptions(directory,IsDevelopment:true));var password=Guid.NewGuid().ToString("N");await Model.BootstrapAsync("operations-admin","Operación",password,password);await Model.LoginAsync("operations-admin",password);Assert.IsTrue(Model.CanNavigate,Model.Message);}
        public void Dispose()=>Directory.Delete(directory,true);
    }
}
