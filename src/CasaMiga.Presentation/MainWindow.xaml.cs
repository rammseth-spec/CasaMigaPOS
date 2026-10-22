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

    private readonly System.Windows.Threading.DispatcherTimer signInClock = new() { Interval = TimeSpan.FromSeconds(15) };

    public MainWindow()
    {
        InitializeComponent();
        // Reloj y estado de la pantalla de acceso: sólo se actualizan con la sesión cerrada.
        signInClock.Tick += (_, _) => { if (DataContext is MainViewModel { IsSignedOut: true } model) model.RefreshSignInStatus(); };
        signInClock.Start();
        Closed += (_, _) => signInClock.Stop();
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
    // Requisitos en vivo: verde lo cumplido, rojo lo pendiente; el botón se habilita sólo con todo cumplido y confirmado.
    private void BootstrapPasswordChanged(object sender, RoutedEventArgs e)
    {
        (BootstrapPassword.State, BootstrapConfirmation.State) = BootstrapChecklist.Update(BootstrapPassword.Password, BootstrapConfirmation.Password);
        BootstrapButton.IsEnabled = BootstrapChecklist.IsSatisfied;
    }
    private void BootstrapKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (BootstrapButton.IsEnabled) BootstrapClick(sender, e);
    }
    private void ChangePasswordChanged(object sender, RoutedEventArgs e)
    {
        (ChangeNew.State, ChangeConfirmation.State) = ChangeChecklist.Update(ChangeNew.Password, ChangeConfirmation.Password);
        ChangeSaveButton.IsEnabled = ChangeChecklist.IsSatisfied;
    }
    private void ChangeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (ChangeSaveButton.IsEnabled) ChangePasswordClick(sender, e);
    }
    private async void LoginClick(object sender, RoutedEventArgs e)
    {
        var password = LoginPassword.Password; LoginPassword.Clear();
        await Model.LoginAsync(LoginUser.Text, password, Model.RememberMe);
        if (Model.CanNavigate) EnterFullScreen();
        else if (Model.IsChangePassword) ChangeCurrent.Focus();
    }
    private async void DevelopmentLoginClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string username }) return;
        await Model.LoginAsDevelopmentAsync(username);
        if (Model.CanNavigate) EnterFullScreen();
        else if (Model.IsChangePassword) ChangeCurrent.Focus();
    }
    private async void ForgotPasswordClick(object sender, RoutedEventArgs e) => await Model.ForgotPasswordAsync(LoginUser.Text);
    private void ShowChangePasswordClick(object sender, RoutedEventArgs e)
    {
        LoginPassword.Clear(); Model.ShowChangePassword(LoginUser.Text);
        if (Model.ChangePasswordUser.Length == 0) ChangeUser.Focus(); else ChangeCurrent.Focus();
    }
    private void CancelChangePasswordClick(object sender, RoutedEventArgs e) { ClearChangePassword(); Model.CancelChangePassword(); LoginPassword.Focus(); }
    private async void ChangePasswordClick(object sender, RoutedEventArgs e)
    {
        string current = ChangeCurrent.Password, next = ChangeNew.Password, confirmation = ChangeConfirmation.Password;
        ClearChangePassword();
        await Model.ChangePasswordAsync(current, next, confirmation);
        if (Model.CanNavigate) EnterFullScreen();
        else if (Model.IsLogin) { LoginUser.Text = Model.ChangePasswordUser; LoginPassword.Focus(); }
        else ChangeCurrent.Focus();
    }
    private void ClearChangePassword() { ChangeCurrent.Clear(); ChangeNew.Clear(); ChangeConfirmation.Clear(); }
    private async void UsersNavigationClick(object sender, RoutedEventArgs e) => await Model.ShowUsersAsync();
    private async void CreateUserClick(object sender, RoutedEventArgs e) => await Model.CreateUserAsync();
    private async void SaveUserClick(object sender, RoutedEventArgs e) => await Model.SaveUserAsync();
    private async void ToggleUserClick(object sender, RoutedEventArgs e)
    {
        if (Model.SelectedUser is not { } target) return;
        if (target.Active && AppDialog.Show(this, $"{target.Username} ya no podrá iniciar sesión hasta que lo reactives. ¿Continuar?",
            "Desactivar usuario", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await Model.ToggleUserAsync();
    }
    private async void ResetPasswordClick(object sender, RoutedEventArgs e)
    {
        if (Model.SelectedUser is not { } target) { await Model.ResetPasswordAsync(); return; }
        if (AppDialog.Show(this, $"Se asignará una contraseña temporal a {target.Username} y deberá cambiarla al entrar. ¿Continuar?",
            "Restablecer contraseña", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await Model.ResetPasswordAsync();
    }
    private void LoginPasswordKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; LoginClick(sender,e); } }
    private void ShowOpeningClick(object sender, RoutedEventArgs e) { Model.ShowOpening(); OpeningAmount.Focus(); OpeningAmount.SelectAll(); }
    private void CancelOpeningClick(object sender, RoutedEventArgs e) => Model.CancelOpening();
    private async void OpenClick(object sender, RoutedEventArgs e) => await Model.OpenAsync();
    private async void PosNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowPosAsync();
    private async void PosCashRequested(object? sender,EventArgs e)=>await Model.ShowCashAsync();
    private async void PosCatalogRequested(object? sender,EventArgs e)=>await Model.ShowCatalogAsync();
    private async void PosSalesRequested(object? sender,EventArgs e)=>await Model.ShowSalesHistoryAsync();
    private async void PosInventoryRequested(object? sender,EventArgs e)=>await Model.ShowInventoryAsync();
    private async void PosRecipesRequested(object? sender,EventArgs e)=>await Model.ShowRecipesAsync();
    private async void PosProductionRequested(object? sender,EventArgs e)=>await Model.ShowProductionAsync();
    private void PosLogoutRequested(object? sender,EventArgs e)=>LogoutClick(this,new RoutedEventArgs());
    private async void CatalogNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowCatalogAsync();
    private async void SalesNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowSalesHistoryAsync();
    private async void TicketsNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowTicketsAsync();
    private async void ReportsNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowReportsAsync();
    private async void ConsignmentNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowConsignmentAsync();
    private async void SyncNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowSyncAsync();
    private async void SyncNowClick(object sender,RoutedEventArgs e){ if (Model.Sync is { } sync) await sync.RunAsync(); }
    private async void BackupNowClick(object sender,RoutedEventArgs e){ if (Model.Sync is { } sync) await sync.BackupAsync(); }
    private async void RegisterTerminalClick(object sender,RoutedEventArgs e)
    {
        if (Model.Sync is not { } sync) return;
        var secret = EnrollmentSecret.Password; EnrollmentSecret.Clear();
        await sync.RegisterAsync(secret);
    }
    private async void DailyReportNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowDailyReportAsync();
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
        FullScreenButton.ToolTip = "Salir de pantalla completa · F11";
        FullScreenButton.Tag = FindResource("IconCollapse");
    }
    private void ExitFullScreen()
    {
        if (!isFullScreen) return;
        WindowState = WindowState.Normal;
        WindowStyle = windowStyleBeforeFullScreen;
        ResizeMode = resizeModeBeforeFullScreen;
        WindowState = windowStateBeforeFullScreen;
        isFullScreen = false;
        FullScreenButton.ToolTip = "Pantalla completa · F11";
        FullScreenButton.Tag = FindResource("IconExpand");
    }
    private bool DiscardBefore(string action)
    {
        if(Model.HasPendingCheckout){AppDialog.Show(this,"Verifica el cobro pendiente en el POS antes de continuar.","Cobro pendiente");return false;}
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
        if(!DiscardBefore("cerrar la aplicación")){e.Cancel=true;return;}
        Model.Sync?.Dispose();
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
