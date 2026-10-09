using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace CasaMiga.Presentation;
public partial class MainWindow : Window
{
    private bool isFullScreen;
    private WindowState windowStateBeforeFullScreen;
    private WindowStyle windowStyleBeforeFullScreen;
    private ResizeMode resizeModeBeforeFullScreen;
    private readonly DispatcherTimer operationTick = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private OperationCenterViewModel? observedOperations;
    internal Action OperationAlertSound { get; set; } = () => SystemSounds.Asterisk.Play();
    internal Action? TaskbarAttentionOverride { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        operationTick.Tick += OperationTick;
    }
    private MainViewModel Model => (MainViewModel)DataContext;
    private async void BootstrapClick(object sender, RoutedEventArgs e)
    {
        var password = BootstrapPassword.Password; var confirmation = BootstrapConfirmation.Password;
        await Model.BootstrapAsync(BootstrapUser.Text, BootstrapName.Text, password, confirmation);
        if (Model.IsLogin)
        {
            BootstrapPassword.Clear(); BootstrapConfirmation.Clear();
            LoginUser.Text = BootstrapUser.Text.Trim();
            LoginPassword.Focus();
        }
    }
    private async void LoginClick(object sender, RoutedEventArgs e)
    {
        var password = LoginPassword.Password; LoginPassword.Clear();
        await Model.LoginAsync(LoginUser.Text, password);
        if (Model.CanNavigate) EnterFullScreen();
    }
    private void LoginPasswordKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; LoginClick(sender,e); } }
    private void ShowOpeningClick(object sender, RoutedEventArgs e) { Model.ShowOpening(); OpeningAmount.Focus(); OpeningAmount.SelectAll(); }
    private void CancelOpeningClick(object sender, RoutedEventArgs e) => Model.CancelOpening();
    private async void OpenClick(object sender, RoutedEventArgs e) => await Model.OpenAsync();
    private async void PosNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowPosAsync();
    private void TogglePosSidebarClick(object sender,RoutedEventArgs e)=>Model.TogglePosSidebar();
    private async void PosCashRequested(object? sender,EventArgs e)=>await Model.ShowCashAsync();
    private async void PosCatalogRequested(object? sender,EventArgs e)=>await Model.ShowCatalogAsync();
    private async void PosSalesRequested(object? sender,EventArgs e)=>await Model.ShowSalesHistoryAsync();
    private async void PosInventoryRequested(object? sender,EventArgs e)=>await Model.ShowInventoryAsync();
    private async void PosRecipesRequested(object? sender,EventArgs e)=>await Model.ShowRecipesAsync();
    private async void PosProductionRequested(object? sender,EventArgs e)=>await Model.ShowProductionAsync();
    private void PosLogoutRequested(object? sender,EventArgs e)=>LogoutClick(this,new RoutedEventArgs());
    private async void CatalogNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowCatalogAsync();
    private async void SalesNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowSalesHistoryAsync();
    private async void InventoryNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowInventoryAsync();
    private async void RecipesNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowRecipesAsync();
    private async void ProductionNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowProductionAsync();
    private async void CashNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowCashAsync();
    private async void BeginClosingClick(object sender, RoutedEventArgs e)
    {
        if(!DiscardBefore("cerrar caja"))return;
        await Model.BeginClosingAsync();
        if(Model.IsCounting) CountedInput.Focus(); else if(Model.IsClosingResult) BackResult.Focus();
    }
    private void BackClosingClick(object sender, RoutedEventArgs e) => Model.BackFromClosing();
    private async void SubmitCountClick(object sender, RoutedEventArgs e) { await Model.SubmitCountAsync(); if(Model.IsClosingResult) BackResult.Focus(); }
    private async void ConfirmCloseClick(object sender, RoutedEventArgs e) => await Model.ConfirmCloseAsync();
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (ContextHelp.TryHandleEscape(e)) return;
        if (PopupEscape.TryHandle(e)) return;
        if(e.Key==Key.Escape && Model.Operations?.IsPanelOpen==true)
        {
            e.Handled=true;
            if(!OperationsPanel.TryHandleEscape())Model.Operations.ClosePanel();
            return;
        }
        if(e.Key==Key.F11)
        { e.Handled=true; ToggleFullScreen(); }
        else if(e.Key==Key.Escape && isFullScreen)
        { e.Handled=true; ExitFullScreen(); }
        else if(e.Key==Key.Escape && Model.HasCashClosureDetail)
        { e.Handled=true; Model.HideCashClosure(); }
        else if(e.Key==Key.Escape && (Model.IsCounting || Model.IsClosingResult || Model.IsOpening))
        { e.Handled=true; if(Model.IsOpening) Model.CancelOpening(); else Model.BackFromClosing(); }
        else if(e.Key==Key.Enter && Keyboard.FocusedElement==CountedInput && Model.IsCounting)
        {e.Handled=true; SubmitCountClick(sender,e);}
        else if(e.Key==Key.Enter && Keyboard.FocusedElement==OpeningAmount && Model.IsOpening)
        {e.Handled=true; OpenClick(sender,e);}
    }
    private void FullScreenClick(object sender, RoutedEventArgs e) => ToggleFullScreen();
    private void OperationsClick(object sender,RoutedEventArgs e)=>Model.Operations?.OpenPanel();
    private void OperationsBackdropClick(object sender,MouseButtonEventArgs e)=>Model.Operations?.ClosePanel();
    private void WindowLoaded(object sender,RoutedEventArgs e){EnsureOperationAlertSubscription();Model.Operations?.Tick(DateTimeOffset.Now);operationTick.Start();}
    private void WindowClosed(object? sender,EventArgs e)
    {
        operationTick.Stop();
        if(observedOperations is not null)observedOperations.AlertRaised-=OperationAlertRaised;
        observedOperations=null;
    }
    private void WindowDeactivated(object? sender,EventArgs e)=>Model.Operations?.ClosePanel();
    private void WindowStateChanged(object? sender,EventArgs e){if(WindowState==WindowState.Minimized)Model.Operations?.ClosePanel();}
    private void OperationTick(object? sender,EventArgs e)
    {
        EnsureOperationAlertSubscription();
        Model.Operations?.Tick(DateTimeOffset.Now);
    }
    private void EnsureOperationAlertSubscription()
    {
        if(ReferenceEquals(observedOperations,Model.Operations))return;
        if(observedOperations is not null)observedOperations.AlertRaised-=OperationAlertRaised;
        observedOperations=Model.Operations;
        if(observedOperations is not null)observedOperations.AlertRaised+=OperationAlertRaised;
    }
    private void OperationAlertRaised(object? sender,OperationAlertEventArgs e)
    {
        OperationAlertSound();
        AnimateOperationIndicator();
        if(!IsActive||WindowState==WindowState.Minimized)(TaskbarAttentionOverride??RequestTaskbarAttention)();
    }
    private void AnimateOperationIndicator()
    {
        if(!SystemParameters.ClientAreaAnimation)return;
        if(OperationsStatusButton.RenderTransform is not TranslateTransform transform)
        {
            transform=new TranslateTransform();OperationsStatusButton.RenderTransform=transform;OperationsStatusButton.RenderTransformOrigin=new Point(.5,.5);
        }
        var pulse=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(600)};
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromPercent(0)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(.68,KeyTime.FromPercent(.2)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromPercent(.4)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(.68,KeyTime.FromPercent(.6)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromPercent(1)));
        var shake=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(600)};
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromPercent(0)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(-3,KeyTime.FromPercent(.12)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(3,KeyTime.FromPercent(.24)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(-2,KeyTime.FromPercent(.36)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(2,KeyTime.FromPercent(.48)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromPercent(.66)));
        OperationsStatusButton.BeginAnimation(OpacityProperty,pulse,HandoffBehavior.SnapshotAndReplace);
        transform.BeginAnimation(TranslateTransform.XProperty,shake,HandoffBehavior.SnapshotAndReplace);
    }
    private void RequestTaskbarAttention()
    {
        var info=new FlashWindowInfo
        {
            Size=(uint)Marshal.SizeOf<FlashWindowInfo>(),
            Window=new WindowInteropHelper(this).Handle,
            Flags=FlashTaskbar,
            Count=3,
            Timeout=0
        };
        if(info.Window!=IntPtr.Zero)FlashWindowEx(ref info);
    }
    private void ToggleFullScreen()
    {
        ContextHelp.CloseAll();
        Model.Operations?.ClosePanel();
        if (isFullScreen) { ExitFullScreen(); return; }
        EnterFullScreen();
    }
    private void EnterFullScreen()
    {
        if (isFullScreen) return;
        windowStateBeforeFullScreen = WindowState;
        windowStyleBeforeFullScreen = WindowStyle;
        resizeModeBeforeFullScreen = ResizeMode;
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        isFullScreen = true;
        FullScreenButton.Content = "F11";
        FullScreenButton.ToolTip = "Salir de pantalla completa · F11";
    }
    private void ExitFullScreen()
    {
        if (!isFullScreen) return;
        WindowState = WindowState.Normal;
        WindowStyle = windowStyleBeforeFullScreen;
        ResizeMode = resizeModeBeforeFullScreen;
        WindowState = windowStateBeforeFullScreen;
        isFullScreen = false;
        FullScreenButton.Content = "F11";
        FullScreenButton.ToolTip = "Pantalla completa · F11";
    }
    private bool DiscardBefore(string action)
    {
        if(Model.HasPendingCheckout){MessageBox.Show(this,"Verifica el cobro pendiente en el POS antes de continuar.","Cobro pendiente");return false;}
        if(!Model.HasCart)return true;
        if(!DiscardDialog.Confirm(this,action))return false;
        Model.DiscardCart();return true;
    }
    private void LogoutClick(object sender, RoutedEventArgs e)
    {
        if(Model.Busy || Model.Pos?.Busy==true || !DiscardBefore("salir del usuario"))return;
        ExitFullScreen();LoginPassword.Clear();Model.Logout();LoginUser.Focus();
    }
    private void WindowClosing(object? sender,System.ComponentModel.CancelEventArgs e)
    {
        if(Model.Busy || Model.Pos?.Busy==true){e.Cancel=true;return;}
        if(!DiscardBefore("cerrar la aplicación"))e.Cancel=true;
    }

    private const uint FlashTaskbar=0x00000002;
    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWindowInfo info);
}
