using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Presentation;

public sealed class CheckoutViewModel(CheckoutService service, ReceiptService receipts, Money total, bool recovery) : INotifyPropertyChanged
{
    private string received="";
    public string Received { get=>received; set {received=value;Notify();} }
    public bool Busy { get; private set; }
    public bool Ready=>!Busy;
    public bool Acknowledged { get; private set; }
    public bool CanClose=>!Busy && (Result is null || Acknowledged);
    public bool Recovery { get; private set; }=recovery;
    public bool IsCapture=>!Recovery && Result is null;
    public bool CanConfirm=>!Busy && IsCapture && TryReceived(out _);
    public Sale? Result { get; private set; }
    public string Total=>PosViewModel.Format(total);
    public string Change=>TryReceived(out var amount)?PosViewModel.Format(total.ChangeFrom(amount)):"—";
    public string Message { get; private set; }="";
    public string PrintMessage { get; private set; }="";
    public ReceiptPrintResult? PrintResult { get; private set; }
    public bool CanRetryPrint=>!Busy && Result is not null && PrintResult?.State is ReceiptPrintState.Requested or ReceiptPrintState.Failed or ReceiptPrintState.Unknown;
    public string ResultText=>Result is { } sale ? $"Venta confirmada\nFolio: {sale.Folio}\nTotal: {PosViewModel.Format(sale.Total)}\nRecibido: {PosViewModel.Format(sale.Payment.Received)}\nCambio: {PosViewModel.Format(sale.Payment.Change)}" : "";
    public bool HasResult=>Result is not null;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
    private bool TryReceived(out Money amount)
    {
        amount=default;
        if(!decimal.TryParse(Received,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var pesos))return false;
        try {amount=Money.FromPesos(pesos);return amount.Cents>=total.Cents;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
    }
    public Task ConfirmAsync()=>Run(async()=>
    {
        if(!TryReceived(out var amount))throw new CheckoutException("El recibido debe cubrir el total, con máximo dos decimales.");
        Result=await Task.Run(()=>service.Confirm(service.Prepare(amount)));
        await PrintOriginalAsync();
    });
    public Task ResolveAsync()=>Run(async()=>
    {
        Result=await Task.Run(service.Resolve);
        if(Result is null)Message="Se verificó la base: este intento no tiene una venta confirmada. Vuelve al POS para revisar el carrito.";
        else await PrintOriginalAsync();
    });
    private async Task PrintOriginalAsync()
    {
        try{PrintResult=await Task.Run(()=>receipts.PrintOriginal(Result!));PrintMessage=PrintResult.Message;}
        catch(Exception){PrintMessage="La venta quedó confirmada, pero no se pudo preparar el ticket. Reintenta después de revisar la configuración.";}
    }
    public Task RetryPrintAsync()=>Run(async()=>
    {
        if(PrintResult is null)throw new InvalidOperationException("No hay un intento de impresión para reintentar.");
        try{PrintResult=await Task.Run(()=>receipts.Retry(PrintResult.RequestId));PrintMessage=PrintResult.Message;}
        catch(Exception){PrintMessage="No se pudo reintentar el ticket. La venta permanece confirmada.";}
    });
    public async Task<bool> LeaveAsync()
    {
        if(Busy)return false;
        if(Result is null)return true;
        await Run(async()=>{await Task.Run(()=>service.Acknowledge(Result.OperationId));Acknowledged=true;});
        return Acknowledged;
    }
    private async Task Run(Func<Task> action)
    {
        if(Busy)return;Busy=true;Message="";Notify();
        try {await action();}
        catch(AccessDeniedException e){Message=e.Message;}
        catch(CheckoutException e){Message=e.Message;}
        catch(ArgumentException){Message="Revisa el importe recibido. El cobro no pudo completarse.";}
        catch(OverflowException){Message="El importe excede el límite permitido. No se confirmó una nueva venta.";}
        catch(Exception){Message="No se pudo completar o verificar el cobro. Revisa el resultado pendiente antes de recibir dinero nuevamente.";}
        finally
        {
            try {Recovery=service.HasPending && Result is null;}catch {Recovery=true;Message="El registro del intento requiere revisión técnica. No realices otro cobro.";}
            Busy=false;Notify();
        }
    }
}
