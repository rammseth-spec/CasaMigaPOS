using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace CasaMiga.Presentation;
public partial class PosView : UserControl
{
    public PosView()=>InitializeComponent();
    private PosViewModel Model=>(PosViewModel)DataContext;
    public event EventHandler? CashRequested;
    public event EventHandler? CatalogRequested;
    public event EventHandler? SalesRequested;
    public event EventHandler? LogoutRequested;
    private void CashClick(object s,RoutedEventArgs e)=>CashRequested?.Invoke(this,EventArgs.Empty);
    private void CatalogClick(object s,RoutedEventArgs e)=>CatalogRequested?.Invoke(this,EventArgs.Empty);
    private void SalesClick(object s,RoutedEventArgs e)=>SalesRequested?.Invoke(this,EventArgs.Empty);
    private void LogoutClick(object s,RoutedEventArgs e)=>LogoutRequested?.Invoke(this,EventArgs.Empty);
    private async void SearchClick(object s,RoutedEventArgs e)=>await Model.SearchAsync();
    private async void CategoryClick(object s,RoutedEventArgs e)=>await Model.CategoryAsync(((Button)s).Tag as Guid?);
    private async void ProductClick(object s,RoutedEventArgs e)=>await Model.AddAsync((Guid)((Button)s).Tag);
    private async void IncreaseClick(object s,RoutedEventArgs e)=>await Model.IncrementAsync((Guid)((Button)s).Tag);
    private async void DecreaseClick(object s,RoutedEventArgs e)=>await Model.DecrementAsync((Guid)((Button)s).Tag);
    private async void RemoveClick(object s,RoutedEventArgs e)=>await Model.RemoveAsync((Guid)((Button)s).Tag);
    private async void PreviousClick(object s,RoutedEventArgs e)=>await Model.PageAsync(-1);
    private async void NextClick(object s,RoutedEventArgs e)=>await Model.PageAsync(1);
    private async void CheckoutClick(object s,RoutedEventArgs e)
    {
        if(!Model.Ready)return;
        new CheckoutDialog(Window.GetWindow(this),Model.Checkout,Model.TotalMoney,Model.RecoveryRequired).ShowDialog();
        await Model.AfterCheckoutAsync();
    }
    private void ClearClick(object s,RoutedEventArgs e)
    {
        if(Model.HasLines && !DiscardDialog.Confirm(Window.GetWindow(this),"limpiar el carrito"))return;
        Model.Discard(true);
    }
    private async void PosKeyDown(object s,KeyEventArgs e)
    {
        if(e.Key==Key.Enter && Keyboard.FocusedElement==SearchInput){e.Handled=true;await Model.SearchAsync();}
        else if(e.Key==Key.Escape){e.Handled=true;SearchInput.Focus();SearchInput.SelectAll();}
    }
}
