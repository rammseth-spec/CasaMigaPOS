using System.Windows;
using System.Windows.Input;
namespace CasaMiga.Presentation;
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private MainViewModel Model => (MainViewModel)DataContext;
    private async void BootstrapClick(object sender, RoutedEventArgs e)
    {
        var password = BootstrapPassword.Password; var confirmation = BootstrapConfirmation.Password;
        BootstrapPassword.Clear(); BootstrapConfirmation.Clear();
        await Model.BootstrapAsync(BootstrapUser.Text, BootstrapName.Text, password, confirmation);
        if (Model.IsLogin) LoginUser.Focus();
    }
    private async void LoginClick(object sender, RoutedEventArgs e)
    {
        var password = LoginPassword.Password; LoginPassword.Clear();
        await Model.LoginAsync(LoginUser.Text, password);
    }
    private void LoginPasswordKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; LoginClick(sender,e); } }
    private void ShowOpeningClick(object sender, RoutedEventArgs e) { Model.ShowOpening(); OpeningAmount.Focus(); OpeningAmount.SelectAll(); }
    private void CancelOpeningClick(object sender, RoutedEventArgs e) => Model.CancelOpening();
    private async void OpenClick(object sender, RoutedEventArgs e) => await Model.OpenAsync();
    private async void PosNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowPosAsync();
    private async void PosCashRequested(object? sender,EventArgs e)=>await Model.ShowCashAsync();
    private async void PosCatalogRequested(object? sender,EventArgs e)=>await Model.ShowCatalogAsync();
    private void PosLogoutRequested(object? sender,EventArgs e)=>LogoutClick(this,new RoutedEventArgs());
    private async void CatalogNavigationClick(object sender,RoutedEventArgs e)=>await Model.ShowCatalogAsync();
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
        if(e.Key==Key.Escape && (Model.IsCounting || Model.IsClosingResult || Model.IsOpening))
        { e.Handled=true; if(Model.IsOpening) Model.CancelOpening(); else Model.BackFromClosing(); }
        else if(e.Key==Key.Enter && Keyboard.FocusedElement==CountedInput && Model.IsCounting)
        {e.Handled=true; SubmitCountClick(sender,e);}
        else if(e.Key==Key.Enter && Keyboard.FocusedElement==OpeningAmount && Model.IsOpening)
        {e.Handled=true; OpenClick(sender,e);}
    }
    private bool DiscardBefore(string action)
    {
        if(!Model.HasCart)return true;
        if(!DiscardDialog.Confirm(this,action))return false;
        Model.DiscardCart();return true;
    }
    private void LogoutClick(object sender, RoutedEventArgs e)
    {
        if(Model.Busy || Model.Pos?.Busy==true || !DiscardBefore("salir del usuario"))return;
        LoginPassword.Clear();Model.Logout();LoginUser.Focus();
    }
    private void WindowClosing(object? sender,System.ComponentModel.CancelEventArgs e)
    {
        if(Model.Busy || Model.Pos?.Busy==true){e.Cancel=true;return;}
        if(!DiscardBefore("cerrar la aplicación"))e.Cancel=true;
    }
}
