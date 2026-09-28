using System.Windows;
using System.Windows.Input;
using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Presentation;

public partial class CheckoutDialog : Window
{
    private CheckoutViewModel Model=>(CheckoutViewModel)DataContext;
    public CheckoutDialog(Window owner,CheckoutService service,ReceiptService receipts,Money total,bool recovery)
    {
        InitializeComponent();Owner=owner;DataContext=new CheckoutViewModel(service,receipts,total,recovery);
        Loaded+=(_,_)=>ReceivedInput.Focus();
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape && !Model.Busy){e.Handled=true;Close();}};
    }
    private async void ConfirmWithoutPrintClick(object sender,RoutedEventArgs e)=>await Model.ConfirmAsync(false);
    private async void ConfirmAndPrintClick(object sender,RoutedEventArgs e)=>await Model.ConfirmAsync(true);
    private async void ResolveClick(object sender,RoutedEventArgs e)=>await Model.ResolveAsync();
    private async void RetryPrintClick(object sender,RoutedEventArgs e)=>await Model.RetryPrintAsync();
    private async void BackClick(object sender,RoutedEventArgs e){if(await Model.LeaveAsync())Close();}
    private void DialogClosing(object? sender,System.ComponentModel.CancelEventArgs e){if(!Model.CanClose)e.Cancel=true;}
}
