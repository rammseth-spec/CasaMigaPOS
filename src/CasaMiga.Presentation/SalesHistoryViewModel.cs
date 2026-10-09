using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record SalesHistoryRowView(SalesHistoryRow Row, TimeZoneInfo TimeZone)
{
    public Guid SaleId => Row.SaleId;
    // Folio visible (C01-001061); el interno sigue en Row.Folio.
    public string Folio => DisplayFolio.FromInternal(Row.Folio, Row.TerminalName);
    public string SaleNumber => $"Venta {Folio}";
    public string Date => TimeZoneInfo.ConvertTime(Row.ConfirmedAt,TimeZone).ToString("dd/MM/yyyy · HH:mm",CultureInfo.InvariantCulture);
    public string Total => Format(Row.Total);
    public string Method => PaymentLabel(Row.PaymentMethod);
    public string Cashier => Row.CashierName;
    public string Terminal => Row.TerminalName;
    public string Refunded => Row.Refunded.Cents == 0 ? "" : Row.Refunded.Cents == Row.Total.Cents ? "Total" : Format(Row.Refunded);
    private static string Format(Money value)=>(value.Cents/100m).ToString("C2",CultureInfo.GetCultureInfo("es-MX"));
    internal static string PaymentLabel(string method)=>Payment.Label(Payment.Parse(method));
}

public sealed record SalesHistoryItemView(int Position, SaleItem Item)
{
    public string Name=>Item.ProductName;
    public string UnitPrice=>Format(Item.UnitPrice);
    public string Quantity=>$"{Item.Quantity} {(Item.Quantity==1?"pieza":"piezas")}";
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

public sealed class SalesHistoryViewModel(SalesHistoryService service,ReceiptService receipts,TimeZoneInfo timeZone,
    IClock clock,Func<Guid?> currentCashSession,Action<string,string?> log,RefundService? refunds=null):INotifyPropertyChanged
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
    private bool CashSale=>Detail?.Sale.Payment.Kind is null or PaymentMethod.Cash;
    public string RefundMediumNote=>CashSale
        ?"El efectivo sale de la caja abierta de esta terminal. El inventario no se modifica."
        :$"Venta pagada con {Payment.Label(Detail!.Sale.Payment.Kind).ToLowerInvariant()}: se reembolsa por el mismo medio y no sale efectivo de la caja. El inventario no se modifica.";
    public string CancelConfirmation=>Detail is null?"":$"Se cancelará la venta {DisplayFolio.For(Detail.TerminalName,Detail.Sale.Sequence)}. {RefundService.CustomerInstruction(Detail.Sale.Payment.Kind,Detail.Sale.Total)} No se puede deshacer. ¿Continuar?";
    public string ReturnConfirmation=>(CashSale
        ?"Se registrará la devolución de las piezas indicadas y se entregará el efectivo correspondiente."
        :$"Se registrará la devolución de las piezas indicadas y se reembolsará por {Payment.Label(Detail!.Sale.Payment.Kind).ToLowerInvariant()}; no sale efectivo de la caja.")+" No se puede deshacer. ¿Continuar?";
    public string RefundHistory=>RefundOptions is not {Refunds.Count:>0} o?"Sin cancelaciones ni devoluciones.":
        string.Join("\n",o.Refunds.Select(r=>$"{(r.Kind==RefundKind.Cancellation?"CANCELACIÓN":"DEVOLUCIÓN")} · {Local(r.RefundedAt):dd/MM/yyyy HH:mm} · {Format(r.Total)} · {r.UserName} · {r.Reason} · "
            +string.Join(", ",r.Items.Select(i=>$"{i.Quantity} × {i.ProductName}"))))+$"\nTotal devuelto: {Format(o.RefundedTotal)}";
    private int page;
    private Guid? cashSessionId;
    private SalesHistoryMode mode=SalesHistoryMode.Recent;
    private string fromDateText="";
    private string throughDateText="";
    private string fromDateError="";
    private string throughDateError="";
    private string dateRangeError="";
    public bool Busy {get;private set;}
    public bool Ready=>!Busy;
    public bool IsList=>Detail is null;
    public bool IsDetail=>Detail is not null;
    public bool IsAllFilter=>mode==SalesHistoryMode.Recent;
    public bool IsTodayFilter=>mode==SalesHistoryMode.DateRange&&FromDate==TodayDateTime&&ThroughDate==TodayDateTime;
    public bool IsYesterdayFilter=>mode==SalesHistoryMode.DateRange&&FromDate==TodayDateTime.AddDays(-1)&&ThroughDate==TodayDateTime.AddDays(-1);
    public bool IsSessionFilter=>mode==SalesHistoryMode.CashSession;
    public string Folio {get;set;}="";
    public DateTime? FromDate {get;set;}
    public DateTime? ThroughDate {get;set;}
    public string FromDateText {get=>fromDateText;set{if(fromDateText==value)return;fromDateText=value??"";FromDateError="";DateRangeError="";Notify();}}
    public string ThroughDateText {get=>throughDateText;set{if(throughDateText==value)return;throughDateText=value??"";ThroughDateError="";DateRangeError="";Notify();}}
    public string FromDateError {get=>fromDateError;private set{fromDateError=value;}}
    public string ThroughDateError {get=>throughDateError;private set{throughDateError=value;}}
    public string DateRangeError {get=>dateRangeError;private set{dateRangeError=value;}}
    public bool HasFromDateError=>FromDateError.Length>0;
    public bool HasThroughDateError=>ThroughDateError.Length>0;
    public IReadOnlyList<SalesHistoryRowView> Sales {get;private set;}=[];
    public SalesHistoryRowView? SelectedSale {get;set;}
    public SalesHistoryDetail? Detail {get;private set;}
    public IReadOnlyList<SalesHistoryItemView> DetailItems {get;private set;}=[];
    public string PageLabel {get;private set;}="";
    public string FilterSummary {get;private set;}="";
    public bool HasPrevious=>page>0;
    public bool HasNext {get;private set;}
    public bool HasNoSales=>Sales.Count==0;
    public string EmptyMessage=>mode switch
    {
        SalesHistoryMode.Recent=>"No hay ventas registradas.",
        SalesHistoryMode.CashSession=>"No hay ventas registradas en esta sesión.",
        _=>"No encontramos ventas para este periodo."
    };
    public string Message {get;private set;}="";
    public bool ReprintAllowed {get;private set;}
    public bool ShowReprintTicket=>Detail is not null&&ReprintAllowed;
    public bool CanReprintTicket=>ShowReprintTicket&&!Busy;
    public ReceiptPrintResult? ReprintResult {get;private set;}
    public string ReprintMessage {get;private set;}="";
    public string ReprintButtonText=>ReprintResult?.State is ReceiptPrintState.Failed or ReceiptPrintState.Unknown
        ? "Reintentar reimpresión":"Reimprimir ticket";
    public string DetailTitle=>Detail is null?"":$"Venta {DisplayFolio.For(Detail.TerminalName,Detail.Sale.Sequence)}";
    public string DetailContext=>Detail is null?"":$"Fecha: {Local(Detail.Sale.ConfirmedAt):dd/MM/yyyy · HH:mm}\nResponsable: {Detail.CashierName}\nCaja: {Detail.TerminalName}";
    public string DetailSubtotal=>Detail is null?"":Format(Detail.Sale.Subtotal);
    public string DetailTotal=>Detail is null?"":Format(Detail.Sale.Total);
    public string DetailPaymentMethod=>Detail is null?"":Payment.Label(Detail.Sale.Payment.Kind)+(Detail.Sale.Payment.Reference is { } reference?$" · Ref. {reference}":"");
    public string DetailReceived=>Detail is null?"":Format(Detail.Sale.Payment.Received);
    public string DetailChange=>Detail is null?"":Format(Detail.Sale.Payment.Change);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
    private static string Format(Money value)=>(value.Cents/100m).ToString("C2",CultureInfo.GetCultureInfo("es-MX"));
    private DateTimeOffset Local(DateTimeOffset value)=>TimeZoneInfo.ConvertTime(value,timeZone);
    private DateOnly Today=>DateOnly.FromDateTime(Local(clock.UtcNow).DateTime);
    private DateTime TodayDateTime=>Today.ToDateTime(TimeOnly.MinValue);
    public bool CanView()=>service.CanView();

    public Task LoadAsync()=>Run(async()=>
    {
        ResetFilters();mode=SalesHistoryMode.Recent;ClearDetail();await Refresh();
    });
    public Task ShowAllAsync()=>Run(async()=>{ResetFilters();mode=SalesHistoryMode.Recent;await Refresh();});
    public Task TodayAsync()=>ApplyDay(Today);
    public Task YesterdayAsync()=>ApplyDay(Today.AddDays(-1));
    private Task ApplyDay(DateOnly day)=>Run(async()=>
    {
        Folio="";cashSessionId=null;page=0;mode=SalesHistoryMode.DateRange;
        FromDate=day.ToDateTime(TimeOnly.MinValue);ThroughDate=FromDate;FromDateText=FormatDate(day);ThroughDateText=FormatDate(day);ClearDateErrors();await Refresh();
    });
    public Task SearchFolioAsync()=>Run(async()=>{ClearDates();cashSessionId=null;mode=SalesHistoryMode.Folio;page=0;await Refresh();});
    public Task SearchDatesAsync()
    {
        ClearDateErrors();
        var fromValid=TryParseDate(FromDateText,out var from);
        var throughValid=TryParseDate(ThroughDateText,out var through);
        if(!fromValid)FromDateError=DateError(FromDateText);
        if(!throughValid)ThroughDateError=DateError(ThroughDateText);
        if(!fromValid||!throughValid)return RejectDateRange("Revisa las fechas indicadas.");
        if(from>through)
        {
            DateRangeError="La fecha desde no puede ser posterior a la fecha hasta.";
            return RejectDateRange(DateRangeError);
        }
        return ApplyDateRangeAsync(from,through);
    }
    public Task SearchDatesAsync(string fromText,string throughText)
    {
        FromDateText=fromText;ThroughDateText=throughText;return SearchDatesAsync();
    }
    public void ValidateFromDateText()
    {
        if(Busy)return;FromDateError=string.IsNullOrWhiteSpace(FromDateText)||TryParseDate(FromDateText,out _)?"":DateError(FromDateText);Notify();
    }
    public void ValidateThroughDateText()
    {
        if(Busy)return;ThroughDateError=string.IsNullOrWhiteSpace(ThroughDateText)||TryParseDate(ThroughDateText,out _)?"":DateError(ThroughDateText);Notify();
    }
    public Task CurrentSessionAsync()=>Run(async()=>
    {
        Folio="";ClearDates();page=0;mode=SalesHistoryMode.CashSession;cashSessionId=currentCashSession();
        if(cashSessionId is null)
        {
            Sales=[];SelectedSale=null;HasNext=false;PageLabel="";FilterSummary="Sesión actual · 0 ventas";ClearDetail();
            Message="No hay una sesión de caja abierta.";return;
        }
        await Refresh();
    });
    public Task ClearAsync()=>ShowAllAsync();
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
            var saleId=Detail.Sale.Id;var reason=RefundReason;var method=Detail.Sale.Payment.Kind;
            var lines=RefundLines.Select(l=>new RefundLine(l.Position,l.ToReturn)).ToArray();
            var result=await Task.Run(()=>cancel?refunds.Cancel(saleId,reason):refunds.Return(saleId,lines,reason));
            log(cancel?"sale.cancelled":"sale.returned",null);
            await LoadRefundsAsync();
            RefundSucceeded=true;
            RefundMessage=$"{(cancel?"Venta cancelada":"Devolución registrada")}. {RefundService.CustomerInstruction(method,result.Total)}";
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
    public void Forget(){Sales=[];SelectedSale=null;ClearDetail();Message="";FilterSummary="";page=0;cashSessionId=null;mode=SalesHistoryMode.Recent;Notify();}

    private async Task Refresh()
    {
        DateOnly? from=FromDate is { } first?DateOnly.FromDateTime(first):null;
        DateOnly? through=ThroughDate is { } last?DateOnly.FromDateTime(last):null;
        var result=await Task.Run(()=>service.Search(new(mode,Folio,from,through,page,cashSessionId)));
        Sales=result.Items.Select(x=>new SalesHistoryRowView(x,timeZone)).ToArray();SelectedSale=null;
        var pageCount=result.Total==0?0:(result.Total+result.PageSize-1)/result.PageSize;
        PageLabel=result.Total==0?"":$"Página {page+1} de {pageCount} · {result.Total} {(result.Total==1?"venta":"ventas")} · {result.PageSize} por página";
        FilterSummary=$"{FilterLabel()} · {result.Total} {(result.Total==1?"venta":"ventas")}";
        HasNext=(page+1)*result.PageSize<result.Total;ClearDetail();
    }
    private string FilterLabel()=>mode switch
    {
        SalesHistoryMode.Recent=>"Todos",
        SalesHistoryMode.CashSession=>"Sesión actual",
        SalesHistoryMode.DateRange when IsTodayFilter=>"Hoy",
        SalesHistoryMode.DateRange when IsYesterdayFilter=>"Ayer",
        SalesHistoryMode.DateRange=>"Rango personalizado",
        _=>"Venta encontrada"
    };
    private Task ApplyDateRangeAsync(DateOnly from,DateOnly through)
    {
        if(from>through)return RejectDateRange("La fecha desde no puede ser posterior a la fecha hasta.");
        return Run(async()=>
        {
            Folio="";cashSessionId=null;mode=SalesHistoryMode.DateRange;page=0;
            FromDate=from.ToDateTime(TimeOnly.MinValue);ThroughDate=through.ToDateTime(TimeOnly.MinValue);FromDateText=FormatDate(from);ThroughDateText=FormatDate(through);ClearDateErrors();await Refresh();
        });
    }
    private Task RejectDateRange(string message)
    {
        if(!Busy){Message=message;Notify();}
        return Task.CompletedTask;
    }
    private static bool TryParseDate(string? text,out DateOnly value)=>DateOnly.TryParseExact(
        text?.Trim(),"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out value);
    private static string FormatDate(DateOnly value)=>value.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture);
    private static string DateError(string value)=>string.IsNullOrWhiteSpace(value)?"Captura una fecha en formato dd/mm/aaaa.":"Fecha inválida. Usa dd/mm/aaaa.";
    private void ClearDateErrors(){FromDateError="";ThroughDateError="";DateRangeError="";}
    private void ClearDates(){FromDate=null;ThroughDate=null;fromDateText="";throughDateText="";ClearDateErrors();}
    private void ResetFilters(){Folio="";ClearDates();cashSessionId=null;page=0;}
    private void ClearDetail()
    {
        Detail=null;DetailItems=[];ReprintAllowed=false;ReprintResult=null;ReprintMessage="";
        RefundAllowed=false;RefundOptions=null;RefundLines=[];RefundReason="";RefundMessage="";RefundSucceeded=false;
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
