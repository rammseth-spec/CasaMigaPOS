using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record SalesHistoryRowView(SalesHistoryRow Row)
{
    public Guid SaleId => Row.SaleId;
    public string Folio => Row.Folio;
    public string Date => Row.ConfirmedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture);
    public string Total => Format(Row.Total);
    public string Method => Payment.Label(Payment.Parse(Row.PaymentMethod));
    public string Cashier => Row.CashierName;
    public string Terminal => Row.TerminalName;
    public string CashSession => Row.CashSessionId.ToString("D");
    public string Refunded => Row.Refunded.Cents == 0 ? "" : Row.Refunded.Cents == Row.Total.Cents ? "Total" : Format(Row.Refunded);
    private static string Format(Money value)=>(value.Cents/100m).ToString("C2",CultureInfo.GetCultureInfo("es-MX"));
}

public sealed record SalesHistoryItemView(int Position, SaleItem Item)
{
    public string ProductId=>Item.ProductId.ToString("D");
    public string Name=>Item.ProductName;
    public string UnitPrice=>Format(Item.UnitPrice);
    public int Quantity=>Item.Quantity;
    public string LineTotal=>Format(Item.LineTotal);
    private static string Format(Money value)=>(value.Cents/100m).ToString("C2",CultureInfo.GetCultureInfo("es-MX"));
}

public sealed class RefundLineView(RefundableLine line)
{
    public int Position=>line.Position;
    public string Name=>line.ProductName;
    public string UnitPrice=>(line.UnitPrice.Cents/100m).ToString("C2",CultureInfo.GetCultureInfo("es-MX"));
    public int Sold=>line.Sold;
    public int Refunded=>line.Refunded;
    public int Remaining=>line.Remaining;
    public int ToReturn {get;set;}
}

public sealed class SalesHistoryViewModel(SalesHistoryService service,ReceiptService receipts,Action<string,string?> log,RefundService? refunds=null):INotifyPropertyChanged
{
    public bool RefundAllowed {get;private set;}
    public RefundOptions? RefundOptions {get;private set;}
    public IReadOnlyList<RefundLineView> RefundLines {get;private set;}=[];
    public string RefundReason {get;set;}="";
    public string RefundMessage {get;private set;}="";
    public bool RefundSucceeded {get;private set;}
    public bool ShowRefunds=>Detail is not null&&RefundAllowed;
    public bool CanCancelSale=>ShowRefunds&&RefundOptions?.CanCancel==true&&!Busy;
    public bool CanReturnItems=>ShowRefunds&&RefundOptions?.CanReturn==true&&!Busy;
    public string RefundBlocker=>RefundOptions?.Blocker??"";
    public string RefundHistory=>RefundOptions is not {Refunds.Count:>0} o?"Sin cancelaciones ni devoluciones.":
        string.Join("\n",o.Refunds.Select(r=>$"{(r.Kind==RefundKind.Cancellation?"CANCELACIÓN":"DEVOLUCIÓN")} · {r.RefundedAt.ToLocalTime():dd/MM/yyyy HH:mm} · {Format(r.Total)} · {r.UserName} · {r.Reason} · "
            +string.Join(", ",r.Items.Select(i=>$"{i.Quantity} × {i.ProductName}"))))+$"\nTotal devuelto: {Format(o.RefundedTotal)}";
    private int page;
    private SalesHistoryMode mode=SalesHistoryMode.Recent;
    public bool Busy {get;private set;}
    public bool Ready=>!Busy;
    public bool IsList=>Detail is null;
    public bool IsDetail=>Detail is not null;
    public string Folio {get;set;}="";
    public string FromDate {get;set;}="";
    public string ThroughDate {get;set;}="";
    public IReadOnlyList<SalesHistoryRowView> Sales {get;private set;}=[];
    public SalesHistoryRowView? SelectedSale {get;set;}
    public SalesHistoryDetail? Detail {get;private set;}
    public IReadOnlyList<SalesHistoryItemView> DetailItems {get;private set;}=[];
    public string PageLabel {get;private set;}="";
    public bool HasPrevious=>page>0;
    public bool HasNext {get;private set;}
    public string Message {get;private set;}="";
    public bool ReprintAllowed {get;private set;}
    public bool ShowReprintTicket=>Detail is not null&&ReprintAllowed;
    public bool CanReprintTicket=>ShowReprintTicket&&!Busy;
    public ReceiptPrintResult? ReprintResult {get;private set;}
    public string ReprintMessage {get;private set;}="";
    public string ReprintButtonText=>ReprintResult?.State is ReceiptPrintState.Failed or ReceiptPrintState.Unknown
        ? "REINTENTAR REIMPRESIÓN":"REIMPRIMIR TICKET";
    public string DetailTitle=>Detail is null?"":$"Venta {Detail.Sale.Folio}";
    public string DetailContext=>Detail is null?"":$"Fecha: {Detail.Sale.ConfirmedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss zzz}\nCajero actual: {Detail.CashierName}\nUserId: {Detail.Sale.UserId:D}\nTerminal actual: {Detail.TerminalName}\nTerminalId: {Detail.Sale.TerminalId:D}\nCashSessionId: {Detail.Sale.CashSessionId:D}\nSaleId: {Detail.Sale.Id:D}";
    public string DetailPayment=>Detail is null?"":$"Subtotal: {Format(Detail.Sale.Subtotal)}\nTotal: {Format(Detail.Sale.Total)}\nMétodo: {Payment.Label(Detail.Sale.Payment.Kind)}{(Detail.Sale.Payment.Reference is { } reference ? $" · Ref. {reference}" : "")}\nAplicado: {Format(Detail.Sale.Payment.Applied)}\nRecibido: {Format(Detail.Sale.Payment.Received)}\nCambio: {Format(Detail.Sale.Payment.Change)}";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
    private static string Format(Money value)=>(value.Cents/100m).ToString("C2",CultureInfo.GetCultureInfo("es-MX"));
    public bool CanView()=>service.CanView();

    public Task LoadAsync()=>Run(async()=>
    {
        Folio="";FromDate="";ThroughDate="";page=0;mode=SalesHistoryMode.Recent;ClearDetail();
        await Refresh();
    });
    public Task SearchFolioAsync()=>Run(async()=>{FromDate="";ThroughDate="";mode=SalesHistoryMode.Folio;page=0;await Refresh();});
    public Task SearchDatesAsync()=>Run(async()=>{Folio="";mode=SalesHistoryMode.DateRange;page=0;await Refresh();});
    public Task ClearAsync()=>Run(async()=>{Folio="";FromDate="";ThroughDate="";mode=SalesHistoryMode.Recent;page=0;await Refresh();});
    public Task PageAsync(int delta)=>Run(async()=>{page=Math.Max(0,page+delta);await Refresh();});
    public Task OpenDetailAsync()=>Run(async()=>
    {
        if(SelectedSale is null)throw new SalesHistoryException("Selecciona una venta.");
        Detail=await Task.Run(()=>service.Detail(SelectedSale.SaleId))??throw new SalesHistoryException("La venta seleccionada ya no está disponible.");
        DetailItems=Detail.Sale.Items.Select((item,index)=>new SalesHistoryItemView(index+1,item)).ToArray();
        ReprintAllowed=await Task.Run(receipts.CanReprint);
        RefundMessage="";RefundSucceeded=false;
        await LoadRefundsAsync();
    });
    private async Task LoadRefundsAsync()
    {
        RefundAllowed=refunds is not null&&await Task.Run(refunds.CanRefund);
        if(!RefundAllowed||Detail is null){RefundOptions=null;RefundLines=[];return;}
        var saleId=Detail.Sale.Id;
        RefundOptions=await Task.Run(()=>refunds!.Options(saleId));
        RefundLines=RefundOptions.Lines.Select(l=>new RefundLineView(l)).ToArray();
        RefundReason="";
    }
    public Task CancelSaleAsync()=>RefundAsync(true);
    public Task ReturnItemsAsync()=>RefundAsync(false);
    private async Task RefundAsync(bool cancel)
    {
        if(Busy||Detail is null||refunds is null)return;
        Busy=true;Message="";RefundMessage="";RefundSucceeded=false;Notify();
        try
        {
            var saleId=Detail.Sale.Id;var reason=RefundReason;
            var lines=RefundLines.Select(l=>new RefundLine(l.Position,l.ToReturn)).ToArray();
            var result=await Task.Run(()=>cancel?refunds.Cancel(saleId,reason):refunds.Return(saleId,lines,reason));
            log(cancel?"sale.cancelled":"sale.returned",null);
            await LoadRefundsAsync();
            RefundSucceeded=true;
            RefundMessage=$"{(cancel?"Venta cancelada":"Devolución registrada")}. Entrega {Format(result.Total)} en efectivo al cliente.";
        }
        catch(AccessDeniedException error){RefundAllowed=false;RefundMessage=error.Message;log("authorization.denied",null);}
        catch(RefundException error){RefundMessage=error.Message;}
        catch(ArgumentException error){RefundMessage=error.Message;}
        catch(Exception error){RefundMessage="No se pudo registrar la devolución. Revisa el historial antes de reintentar.";log("sale.refund.failed",error.GetType().Name);}
        finally{Busy=false;Notify();}
    }
    public async Task ReprintAsync()
    {
        if(Busy||Detail is null||!ReprintAllowed)return;
        Busy=true;Message="";ReprintMessage="";Notify();
        try
        {
            var prior=ReprintResult;
            ReprintResult=await Task.Run(()=>prior?.State is ReceiptPrintState.Failed or ReceiptPrintState.Unknown
                ? receipts.Retry(prior.RequestId)
                : receipts.Reprint(Detail.Sale.Id));
            ReprintMessage=ReprintResult.Message;
        }
        catch(AccessDeniedException error)
        {
            ReprintAllowed=false;ReprintMessage=error.Message;log("authorization.denied",null);
        }
        catch(InvalidOperationException error)
        {
            ReprintMessage=error.Message;log("receipt.reprint.failed",error.GetType().Name);
        }
        catch(Exception error)
        {
            ReprintMessage="No se pudo solicitar la reimpresión.";log("receipt.reprint.failed",error.GetType().Name);
        }
        finally{Busy=false;Notify();}
    }
    public void Back(){if(Busy)return;ClearDetail();Message="";Notify();}
    public void Forget(){Sales=[];SelectedSale=null;ClearDetail();Message="";page=0;mode=SalesHistoryMode.Recent;Notify();}

    private async Task Refresh()
    {
        var from=ParseDate(FromDate,"Desde");var through=ParseDate(ThroughDate,"Hasta");
        var result=await Task.Run(()=>service.Search(new(mode,Folio,from,through,page)));
        Sales=result.Items.Select(x=>new SalesHistoryRowView(x)).ToArray();SelectedSale=null;
        PageLabel=$"Página {page+1} · {result.Total} ventas · hasta {result.PageSize} por página";
        HasNext=(page+1)*result.PageSize<result.Total;ClearDetail();
    }
    private void ClearDetail()
    {
        Detail=null;DetailItems=[];ReprintAllowed=false;ReprintResult=null;ReprintMessage="";
        RefundAllowed=false;RefundOptions=null;RefundLines=[];RefundReason="";RefundMessage="";RefundSucceeded=false;
    }
    private static DateOnly? ParseDate(string value,string label)
    {
        if(string.IsNullOrWhiteSpace(value))return null;
        if(!DateOnly.TryParseExact(value.Trim(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed))
            throw new SalesHistoryException($"{label} debe usar el formato AAAA-MM-DD.");
        return parsed;
    }
    private async Task Run(Func<Task> action)
    {
        if(Busy)return;Busy=true;Message="";Notify();
        try{await action();}
        catch(AccessDeniedException error){Sales=[];Detail=null;DetailItems=[];Message=error.Message;log("authorization.denied",null);}
        catch(SalesHistoryException error){Message=error.Message;}
        catch(OverflowException){Message="La consulta excede el límite permitido.";}
        catch(Exception error){Message="No se pudo consultar el historial.";log("sales.history.failed",error.GetType().Name);}
        finally{Busy=false;Notify();}
    }
}
