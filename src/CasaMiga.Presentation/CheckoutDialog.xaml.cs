using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Presentation;

public partial class CheckoutDialog : Window
{
    /// <summary>Tiempo que se muestra una venta confirmada antes de regresar solo al POS.</summary>
    public static readonly TimeSpan AutoReturnDelay=TimeSpan.FromSeconds(3);
    private readonly DispatcherTimer autoReturn=new(){Interval=AutoReturnDelay};
    private CheckoutViewModel Model=>(CheckoutViewModel)DataContext;
    public CheckoutDialog(Window owner,CheckoutService service,ReceiptService receipts,Money total,bool recovery,bool printSelected=false,string terminalName="")
    {
        InitializeComponent();Owner=owner;DataContext=new CheckoutViewModel(service,receipts,total,recovery){TerminalName=terminalName};
        Title=printSelected?"Cobrar + imprimir ticket":"Cobrar sin imprimir ticket";
        WithoutPrintButton.IsDefault=!printSelected;
        AndPrintButton.IsDefault=printSelected;
        Loaded+=(_,_)=>ReceivedInput.Focus();
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape && !Model.Busy){e.Handled=true;Close();}};
        // Venta confirmada sin errores ni ticket por reintentar: se muestra el resultado unos segundos y se vuelve al POS.
        Model.PropertyChanged+=(_,_)=>{if(Model.ReturnsAutomatically&&!autoReturn.IsEnabled)autoReturn.Start();};
        autoReturn.Tick+=async(_,_)=>{autoReturn.Stop();if(Model.ReturnsAutomatically&&await Model.LeaveAsync())Close();};
        Closed+=(_,_)=>autoReturn.Stop();
    }
    private async void ConfirmWithoutPrintClick(object sender,RoutedEventArgs e)=>await Model.ConfirmAsync(false);
    private async void ConfirmAndPrintClick(object sender,RoutedEventArgs e)=>await Model.ConfirmAsync(true);
    private async void ResolveClick(object sender,RoutedEventArgs e)=>await Model.ResolveAsync();
    private async void RetryPrintClick(object sender,RoutedEventArgs e)=>await Model.RetryPrintAsync();
    private async void BackClick(object sender,RoutedEventArgs e){autoReturn.Stop();if(await Model.LeaveAsync())Close();}
    private void DialogClosing(object? sender,System.ComponentModel.CancelEventArgs e){if(!Model.CanClose)e.Cancel=true;}
}
