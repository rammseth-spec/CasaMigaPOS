using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Presentation;

public sealed class CheckoutViewModel(CheckoutService service, ReceiptService receipts, Money total, bool recovery) : INotifyPropertyChanged
{
    /// <summary>Nombre de la caja: da la clave del folio visible (C01-001061).</summary>
    public string TerminalName { get; init; } = "";
    private string received="";
    private CheckoutRequest? attempt;
    private bool printRequested;
    public string Received { get=>received; set {received=value;Notify();} }
    private PaymentMethod method=PaymentMethod.Cash;
    public PaymentMethod Method { get=>method; set {method=value;Notify();} }
    public bool IsCash { get=>method==PaymentMethod.Cash; set {if(value)Method=PaymentMethod.Cash;} }
    public bool IsCard { get=>method==PaymentMethod.Card; set {if(value)Method=PaymentMethod.Card;} }
    public bool IsTransfer { get=>method==PaymentMethod.Transfer; set {if(value)Method=PaymentMethod.Transfer;} }
    public bool ShowCashFields=>IsCapture&&IsCash;
    public bool ShowReference=>IsCapture&&!IsCash;
    public string Reference { get; set; }="";
    public string Instructions=>IsCash?"Confirma sólo después de recibir el efectivo. Volver conserva el carrito."
        :"Confirma sólo cuando la terminal bancaria o la transferencia muestre el pago aprobado por el total exacto.";
    public bool Busy { get; private set; }
    public bool Ready=>!Busy;
    public bool Acknowledged { get; private set; }
    public bool CanClose=>!Busy && (Result is null || Acknowledged);
    public bool Recovery { get; private set; }=recovery;
    public bool IsCapture=>!Recovery && Result is null;
    public bool CanConfirm=>!Busy && IsCapture && (!IsCash || TryReceived(out _));
    public Sale? Result { get; private set; }
    public string Total=>PosViewModel.Format(total);
    public string Change=>TryReceived(out var amount)?PosViewModel.Format(total.ChangeFrom(amount)):"—";
    public string Message { get; private set; }="";
    public string PrintMessage { get; private set; }="";
    public ReceiptPrintResult? PrintResult { get; private set; }
    public bool CanRetryPrint=>!Busy && Result is not null && PrintResult?.State is ReceiptPrintState.Requested or ReceiptPrintState.Failed or ReceiptPrintState.Unknown;
    /// <summary>Venta confirmada sin nada que atender (ticket enviado o sin imprimir, sin error): el cobro regresa solo al POS.</summary>
    public bool ReturnsAutomatically=>Result is not null && !Busy && !Acknowledged && !CanRetryPrint && Message.Length==0;
    public string ResultText=>Result is not { } sale ? "" : sale.Payment.Kind==PaymentMethod.Cash
        ? $"Venta confirmada\nFolio: {DisplayFolio.For(TerminalName,sale.Sequence)}\nTotal: {PosViewModel.Format(sale.Total)}\nRecibido: {PosViewModel.Format(sale.Payment.Received)}\nCambio: {PosViewModel.Format(sale.Payment.Change)}"
        : $"Venta confirmada\nFolio: {DisplayFolio.For(TerminalName,sale.Sequence)}\nTotal: {PosViewModel.Format(sale.Total)}\nPago: {Payment.Label(sale.Payment.Kind)}{(sale.Payment.Reference is { } r ? $" · Ref. {r}" : "")}";
    public bool HasResult=>Result is not null;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
    private bool TryReceived(out Money amount)
    {
        amount=default;
        if(!decimal.TryParse(Received,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var pesos))return false;
        try {amount=Money.FromPesos(pesos);return amount.Cents>=total.Cents;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
    }
    public Task ConfirmAsync(bool print)=>Run(async()=>
    {
        if(!IsCapture)return;
        var amount=total;
        if(IsCash&&!TryReceived(out amount))throw new CheckoutException("El recibido debe cubrir el total, con máximo dos decimales.");
        printRequested=print;
        var chosen=method;var reference=IsCash?null:Reference;
        attempt=await Task.Run(()=>service.Prepare(amount,chosen,reference));
        Result=await Task.Run(()=>service.Confirm(attempt));
        if(print)await PrintOriginalAsync();
        else PrintMessage="Venta cobrada correctamente. Quedó registrada en Historial sin imprimir ticket.";
    });
    public Task ResolveAsync()=>Run(async()=>
    {
        Result=await Task.Run(service.Resolve);
        if(Result is null)Message="Se verificó la base: este intento no tiene una venta confirmada. Vuelve al POS para revisar el carrito.";
        else if(printRequested)await PrintOriginalAsync();
        else PrintMessage="Venta confirmada y registrada en Historial. No se imprimió ticket durante la recuperación.";
    });
    private async Task PrintOriginalAsync()
    {
        try
        {
            PrintResult=await Task.Run(()=>receipts.PrintOriginal(Result!));
            PrintMessage=PrintResult.State is ReceiptPrintState.Failed or ReceiptPrintState.Unknown
                ? "Venta cobrada correctamente. No fue posible imprimir el ticket. Puedes reimprimirlo desde Historial."
                : PrintResult.Message;
        }
        catch(Exception){PrintMessage="Venta cobrada correctamente. No fue posible imprimir el ticket. Puedes reimprimirlo desde Historial.";}
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
        catch(CheckoutInventoryException e){Message=CheckoutInventoryMessages.Format(e,attempt?.Cart.Lines.FirstOrDefault(l=>l.ProductId==e.ProductId)?.ProductName);}
        catch(CheckoutException e){Message=e.Message;}
        catch(ArgumentException e){Message=IsCash?"Revisa el importe recibido. El cobro no pudo completarse.":e.Message;}
        catch(OverflowException){Message="El importe excede el límite permitido. No se confirmó una nueva venta.";}
        catch(Exception){Message="No se pudo completar o verificar el cobro. Revisa el resultado pendiente antes de recibir dinero nuevamente.";}
        finally
        {
            try {Recovery=service.HasPending && Result is null;}catch {Recovery=true;Message="El registro del intento requiere revisión técnica. No realices otro cobro.";}
            Busy=false;Notify();
        }
    }
}
