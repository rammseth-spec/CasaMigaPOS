using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record DailyProductSummaryView(int Position, DailyProductSummary Summary)
{
    public string Name => Summary.ProductName;
    public string Pieces => $"{Summary.Pieces:N0}";
    public string Revenue => DailyReportViewModel.Format(Summary.Revenue);
}

public sealed record DailyCashSessionSummaryView(DailyCashSessionSummary Session, TimeZoneInfo TimeZone)
{
    public string State => Session.IsOpen ? "ABIERTA" : "CERRADA";
    public string StateDetail => Session.IsOpen ? "Conteo pendiente" : DailyReportViewModel.Difference(Session.Difference);
    public string Identity => $"{Session.TerminalName} · Responsable: {Session.OpenedByName}" +
        (Session.ClosedByName is { } closer && !string.Equals(closer, Session.OpenedByName, StringComparison.Ordinal)
            ? $" · Cerró: {closer}" : "");
    public string Times => $"Apertura {Local(Session.OpenedAt):HH:mm}" +
        (Session.ClosedAt is { } closed ? $" · Cierre {Local(closed):HH:mm}" : " · En curso");
    public string Financial => $"Fondo {DailyReportViewModel.Format(Session.OpeningFund)} · " +
        $"Efectivo vendido {DailyReportViewModel.Format(Session.CashSales)} · " +
        $"Esperado {DailyReportViewModel.Optional(Session.Expected)}";
    public string Count => Session.IsOpen
        ? "Contado: Pendiente · Diferencia: Pendiente"
        : $"Contado: {DailyReportViewModel.Optional(Session.Counted)} · {DailyReportViewModel.Difference(Session.Difference)}";
    public bool HasNote => !string.IsNullOrWhiteSpace(Session.DiscrepancyNote) || (!Session.IsOpen && Session.Difference?.Cents != 0);
    public string Note => !string.IsNullOrWhiteSpace(Session.DiscrepancyNote)
        ? $"Nota: {Session.DiscrepancyNote}"
        : "Sin nota registrada";
    private DateTime Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, TimeZone).DateTime;
}

public sealed class DailyReportViewModel(
    DailyReportService service,
    TimeZoneInfo timeZone,
    Action<string, string?> log) : INotifyPropertyChanged
{
    private DailyOperationsReport? report;
    private string dateText = service.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool Busy { get; private set; }
    public bool IsReady => !Busy;
    public string DateText { get => dateText; set { dateText = value ?? ""; DateError = ""; Notify(); } }
    public string DateError { get; private set; } = "";
    public bool HasDateError => DateError.Length > 0;
    public string Message { get; private set; } = "";
    public string DayLabel => report is null ? "" : report.Day.ToString("dddd d 'de' MMMM 'de' yyyy", Spanish);
    public string Status => report is null ? "" : report.CashSessions.Any(x => x.IsOpen)
        ? (report.Day == service.Today ? "EN CURSO" : "CAJA ABIERTA")
        : report.CashSessions.Count > 0 ? "CERRADO" : "SIN SESIÓN DE CAJA";
    public string Sales => Format(report?.Sales ?? new Money(0));
    public string Tickets => (report?.Tickets ?? 0).ToString("N0", Spanish);
    public string Pieces => (report?.Pieces ?? 0).ToString("N0", Spanish);
    public string AverageTicket => Format(report?.AverageTicket ?? new Money(0));
    public bool HasSales => report?.Tickets > 0;
    public bool HasNoSales => report is not null && report.Tickets == 0;
    public string HumanSummary => report is null ? "" :
        $"{report.Tickets:N0} {(report.Tickets == 1 ? "venta" : "ventas")} · " +
        $"{report.Pieces:N0} {(report.Pieces == 1 ? "pieza" : "piezas")} · {Format(report.Sales)} vendidos" +
        CashSummary(report.CashSessions);
    public IReadOnlyList<DailyProductSummaryView> TopProducts { get; private set; } = [];
    public IReadOnlyList<DailyCashSessionSummaryView> CashSessions { get; private set; } = [];
    public bool HasCashSessions => CashSessions.Count > 0;
    public bool HasNoCashSessions => report is not null && CashSessions.Count == 0;
    public string CashSessionCount => report is null ? "" : $"{report.CashSessions.Count} " +
        (report.CashSessions.Count == 1 ? "sesión en el día" : "sesiones en el día");
    public string OpeningFund => Aggregate(report?.CashSessions.Select(x => (Money?)x.OpeningFund));
    public string CashSales => Aggregate(report?.CashSessions.Select(x => (Money?)x.CashSales));
    public string Expected => Aggregate(report?.CashSessions.Select(x => x.Expected));
    public string Counted => report?.CashSessions.Any(x => x.IsOpen) == true
        ? "Pendiente"
        : Aggregate(report?.CashSessions.Select(x => x.Counted));
    public string CashDifference => report?.CashSessions.Any(x => x.IsOpen) == true
        ? "Pendiente"
        : AggregateDifference(report?.CashSessions.Select(x => x.Difference));

    public bool CanView() => service.CanView();

    public Task LoadAsync() => ReadAsync(service.Today);
    public Task TodayAsync() { DateText = service.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture); return ReadAsync(service.Today); }
    public Task RefreshAsync()
    {
        if (!DateOnly.TryParseExact(DateText.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var day))
        {
            DateError = "Usa una fecha válida en formato dd/mm/aaaa.";
            Notify();
            return Task.CompletedTask;
        }
        return ReadAsync(day);
    }
    public void ValidateDateText()
    {
        if (DateOnly.TryParseExact(DateText.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _)) return;
        DateError = DateText.Length == 10
            ? "Usa una fecha válida en formato dd/mm/aaaa."
            : "Completa la fecha en formato dd/mm/aaaa.";
        Notify();
    }
    public void Forget()
    {
        report = null; TopProducts = []; CashSessions = []; Message = ""; DateError = "";
        DateText = service.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture); Notify();
    }

    private async Task ReadAsync(DateOnly day)
    {
        if (Busy) return;
        Busy = true; Message = ""; DateError = ""; Notify();
        try
        {
            report = await Task.Run(() => service.Read(day));
            DateText = day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            TopProducts = report.TopProducts.Select((x, index) => new DailyProductSummaryView(index + 1, x)).ToArray();
            CashSessions = report.CashSessions.Select(x => new DailyCashSessionSummaryView(x, timeZone)).ToArray();
        }
        catch (DailyReportException error) { DateError = error.Message; }
        catch (AccessDeniedException error) { Message = error.Message; log("authorization.denied", null); }
        catch (Exception error) { Message = "No se pudo consultar el reporte diario."; log("daily.report.failed", error.GetType().Name); }
        finally { Busy = false; Notify(); }
    }

    private static string CashSummary(IReadOnlyList<DailyCashSessionSummary> sessions)
    {
        if (sessions.Count == 0) return " · Sin sesión de caja";
        if (sessions.Any(x => x.IsOpen)) return " · Caja en curso";
        if (sessions.Any(x => x.Difference is null)) return " · Caja cerrada · diferencia no disponible";
        var difference = Sum(sessions.Select(x => x.Difference!.Value));
        return difference.Cents == 0 ? " · Caja cerrada sin diferencia"
            : difference.Cents > 0 ? $" · Caja cerrada · sobrante de {Format(difference)}"
            : $" · Caja cerrada · faltante de {Format(new Money(checked(-difference.Cents)))}";
    }

    private static string Aggregate(IEnumerable<Money?>? values)
    {
        if (values is null) return "No disponible";
        var copy = values.ToArray();
        return copy.Length == 0 || copy.Any(x => x is null) ? "No disponible" : Format(Sum(copy.Select(x => x!.Value)));
    }
    private static string AggregateDifference(IEnumerable<Money?>? values)
    {
        if (values is null) return "No disponible";
        var copy = values.ToArray();
        return copy.Length == 0 || copy.Any(x => x is null) ? "No disponible" : Difference(Sum(copy.Select(x => x!.Value)));
    }
    private static Money Sum(IEnumerable<Money> values)
    {
        var total = new Money(0);
        foreach (var value in values) total += value;
        return total;
    }
    internal static string Format(Money value) => (value.Cents / 100m).ToString("C2", Spanish);
    internal static string Optional(Money? value) => value is { } money ? Format(money) : "No disponible";
    internal static string Difference(Money? value) => value switch
    {
        null => "Diferencia no disponible",
        { Cents: 0 } => "Sin diferencia · $0.00",
        { Cents: > 0 } money => $"Sobrante · +{Format(money)}",
        { } money => $"Faltante · -{Format(new Money(checked(-money.Cents)))}"
    };
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");
}
