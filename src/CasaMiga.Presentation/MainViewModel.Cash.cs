using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record CashClosureDisplay(CashClosureHistoryRow Row)
{
    private static string MoneyText(Money? value) => value is { } money ? (money.Cents/100m).ToString("C2",System.Globalization.CultureInfo.GetCultureInfo("es-MX")) : "No disponible";
    public string Date => Row.ClosedAt.ToLocalTime().ToString("dd/MM/yyyy · HH:mm");
    public string Terminal => Row.TerminalName;
    public string Owner => Row.ClosedByName;
    public string Expected => MoneyText(Row.Expected);
    public string Counted => MoneyText(Row.Counted);
    public string Difference => Row.Difference is { } d ? MainViewModel.DifferenceLabel(d) : "No disponible";
    public string OpenedDate => Row.OpenedAt.ToLocalTime().ToString("dd/MM/yyyy · HH:mm");
    public string OpenedBy => Row.OpenedByName;
    public string ClosedDate => Date;
    public string ClosedBy => Owner;
    public string CountedBy => Row.CountedByName;
    public string OpeningFund => MoneyText(Row.OpeningFund);
    public string CashSales => MoneyText(Row.Expected is { } e ? e-Row.OpeningFund : null);
    public string Note => Row.DiscrepancyNote??"Sin nota registrada";
    public string Details => $"Caja: {Terminal}\nApertura: {Row.OpenedAt.ToLocalTime():dd/MM/yyyy · HH:mm}\nAbierta por: {Row.OpenedByName}\nCierre: {Date}\nCerrada por: {Owner}\nConteo realizado por: {Row.CountedByName}\n\nFondo inicial: {MoneyText(Row.OpeningFund)}\n+ Ventas en efectivo: {MoneyText(Row.Expected is { } e ? e-Row.OpeningFund : null)}\n= Efectivo esperado: {Expected}\nEfectivo contado: {Counted}\n{Difference}\n\nNota de discrepancia:\n{Row.DiscrepancyNote??"Sin nota registrada"}";
}

public sealed partial class MainViewModel
{
    private CashHistoryService? cashHistory;
    private int cashHistoryPage, cashHistoryTotal, cashHistoryPages=1;
    public bool CanClose { get; private set; }
    private string discrepancyNote="";
    public string DiscrepancyNote { get=>discrepancyNote; set { discrepancyNote=value; if(Message.Contains("nota",StringComparison.OrdinalIgnoreCase))Message=""; Notify(); } }
    public bool RequiresDiscrepancyNote => closingResult?.Difference.Cents is { } difference && difference!=0;
    public string DiscrepancyNoteLabel => "Nota de discrepancia · obligatoria";
    public string DiscrepancyNoteCount => $"{DiscrepancyNote.Length}/500 caracteres";
    public bool CanConfirmClose => !Busy && IsClosingResult && DiscrepancyNote.Trim().Length<=500 && (!RequiresDiscrepancyNote || !string.IsNullOrWhiteSpace(DiscrepancyNote));
    public IReadOnlyList<CashClosureDisplay> CashClosures { get; private set; }=[];
    public CashClosureDisplay? SelectedCashClosure { get; set; }
    public CashClosureDisplay? CashClosureDetail { get; private set; }
    public bool ShowsCashDashboard => CashClosureDetail is null;
    public bool HasCashClosureDetail => CashClosureDetail is not null;
    public bool HasNoCashClosures => cashHistoryTotal==0;
    public string CashHistorySummary => $"{cashHistoryTotal} {(cashHistoryTotal==1?"cierre":"cierres")} · página {cashHistoryPage+1} de {cashHistoryPages}";
    public bool CanPreviousCashHistory => !Busy && cashHistoryPage>0;
    public bool CanNextCashHistory => !Busy && cashHistoryPage+1<cashHistoryPages;
    private void ClearCashHistory() { CashClosures=[];SelectedCashClosure=null;CashClosureDetail=null;cashHistoryPage=0;cashHistoryTotal=0;cashHistoryPages=1;DiscrepancyNote=""; }
    private async Task RefreshCashHistory()
    {
        var result=await Task.Run(()=>cashHistory!.Search(new(cashHistoryPage)));
        if(result.Page>=result.TotalPages){cashHistoryPage=result.TotalPages-1;result=await Task.Run(()=>cashHistory!.Search(new(cashHistoryPage)));}
        CashClosures=result.Items.Select(x=>new CashClosureDisplay(x)).ToArray();cashHistoryTotal=result.Total;cashHistoryPages=result.TotalPages;
        SelectedCashClosure=null;CashClosureDetail=null;
    }
    public Task PreviousCashHistoryAsync()=>Execute(async()=>{if(cashHistoryPage>0)cashHistoryPage--;await RefreshCashHistory();});
    public Task NextCashHistoryAsync()=>Execute(async()=>{if(cashHistoryPage+1<cashHistoryPages)cashHistoryPage++;await RefreshCashHistory();});
    public Task ShowCashClosureAsync()=>Execute(async()=>
    {
        if(SelectedCashClosure is null){Message="Selecciona un cierre anterior.";return;}
        var row=await Task.Run(()=>cashHistory!.Detail(SelectedCashClosure.Row.Id));
        CashClosureDetail=row is null?null:new(row);
        if(row is null)Message="No se encontró el cierre seleccionado.";
    });
    public void HideCashClosure(){if(Busy)return;CashClosureDetail=null;Notify();}
}
