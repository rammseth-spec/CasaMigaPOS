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
    public string Method => Row.PaymentMethod;
    public string Cashier => Row.CashierName;
    public string Terminal => Row.TerminalName;
    public string CashSession => Row.CashSessionId.ToString("D");
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

public sealed class SalesHistoryViewModel(SalesHistoryService service,ReceiptService receipts,Action<string,string?> log):INotifyPropertyChanged
{
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
    public string DetailPayment=>Detail is null?"":$"Subtotal: {Format(Detail.Sale.Subtotal)}\nTotal: {Format(Detail.Sale.Total)}\nMétodo: {Detail.Sale.Payment.Method}\nAplicado: {Format(Detail.Sale.Payment.Applied)}\nRecibido: {Format(Detail.Sale.Payment.Received)}\nCambio: {Format(Detail.Sale.Payment.Change)}";
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
    });
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
