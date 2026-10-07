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
    public event EventHandler? InventoryRequested;
    public event EventHandler? RecipesRequested;
    public event EventHandler? ProductionRequested;
    public event EventHandler? LogoutRequested;
    private void CashClick(object s,RoutedEventArgs e)=>CashRequested?.Invoke(this,EventArgs.Empty);
    private void CatalogClick(object s,RoutedEventArgs e)=>CatalogRequested?.Invoke(this,EventArgs.Empty);
    private void SalesClick(object s,RoutedEventArgs e)=>SalesRequested?.Invoke(this,EventArgs.Empty);
    private void InventoryClick(object s,RoutedEventArgs e)=>InventoryRequested?.Invoke(this,EventArgs.Empty);
    private void RecipesClick(object s,RoutedEventArgs e)=>RecipesRequested?.Invoke(this,EventArgs.Empty);
    private void ProductionClick(object s,RoutedEventArgs e)=>ProductionRequested?.Invoke(this,EventArgs.Empty);
    private void LogoutClick(object s,RoutedEventArgs e)=>LogoutRequested?.Invoke(this,EventArgs.Empty);
    private async void SearchClick(object s,RoutedEventArgs e){await Model.SearchAsync();ProductCatalogScroll.ScrollToTop();}
    private async void CategoryClick(object s,RoutedEventArgs e){await Model.CategoryAsync(((Button)s).CommandParameter as Guid?);ProductCatalogScroll.ScrollToTop();}
    private async void ProductClick(object s,RoutedEventArgs e)=>await Model.AddAsync((Guid)((Button)s).Tag);
    private async void IncreaseClick(object s,RoutedEventArgs e)=>await Model.IncrementAsync((Guid)((Button)s).Tag);
    private async void DecreaseClick(object s,RoutedEventArgs e)=>await Model.DecrementAsync((Guid)((Button)s).Tag);
    private async void RemoveClick(object s,RoutedEventArgs e)=>await Model.RemoveAsync((Guid)((Button)s).Tag);
    private async void PreviousClick(object s,RoutedEventArgs e){await Model.PageAsync(-1);ProductCatalogScroll.ScrollToTop();}
    private async void NextClick(object s,RoutedEventArgs e){await Model.PageAsync(1);ProductCatalogScroll.ScrollToTop();}
    private bool checkoutOpen;
    private async void CheckoutClick(object s,RoutedEventArgs e)=>await OpenCheckoutAsync(false);
    private async void CheckoutAndPrintClick(object s,RoutedEventArgs e)=>await OpenCheckoutAsync(true);
    private async Task OpenCheckoutAsync(bool printSelected)
    {
        if(!Model.Ready || checkoutOpen)return;
        checkoutOpen=true;
        try
        {
            new CheckoutDialog(Window.GetWindow(this),Model.Checkout,Model.Receipts,Model.TotalMoney,Model.RecoveryRequired,printSelected,Model.TerminalName).ShowDialog();
            await Model.AfterCheckoutAsync();
        }
        finally{checkoutOpen=false;}
    }
    private void PosVisibilityChanged(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(IsVisible)Dispatcher.BeginInvoke(ProductCatalogScroll.ScrollToTop);
    }
    private void ClearClick(object s,RoutedEventArgs e)
    {
        if(Model.HasLines && !DiscardDialog.Confirm(Window.GetWindow(this),"limpiar el carrito"))return;
        Model.Discard(true);
    }
    private async void PosKeyDown(object s,KeyEventArgs e)
    {
        if(e.Key==Key.Enter && Keyboard.FocusedElement==SearchInput){e.Handled=true;await Model.SearchAsync();ProductCatalogScroll.ScrollToTop();}
        else if(e.Key==Key.Escape){e.Handled=true;SearchInput.Focus();SearchInput.SelectAll();}
    }
}
