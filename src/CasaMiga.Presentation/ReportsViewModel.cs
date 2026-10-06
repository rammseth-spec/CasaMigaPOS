using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record ProductReportView(int Rank, ProductReportRow Row, long TotalNetCents)
{
    public string Name => Row.ProductName;
    public long Sold => Row.QuantitySold;
    public string SoldAmount => ReportsViewModel.Format(Row.Sold);
    public string Refunded => Row.QuantityRefunded == 0 ? "—" : Row.QuantityRefunded.ToString(CultureInfo.InvariantCulture);
    public string RefundedAmount => Row.Refunded.Cents == 0 ? "—" : ReportsViewModel.Format(Row.Refunded);
    public string Net => ReportsViewModel.Format(Row.Net);
    public string Share => TotalNetCents <= 0 ? "—" : (Row.Net.Cents * 100m / TotalNetCents).ToString("0.0", CultureInfo.InvariantCulture) + " %";
}
public sealed record PaymentReportView(PaymentReportRow Row)
{
    public string Method => Payment.Label(Row.Method);
    public int Sales => Row.Sales;
    public string Sold => ReportsViewModel.Format(Row.Sold);
    public string Refunded => Row.Refunded.Cents == 0 ? "—" : ReportsViewModel.Format(Row.Refunded);
    public string Net => ReportsViewModel.Format(Row.Net);
}
public sealed record CashierReportView(CashierReportRow Row)
{
    public string Name => Row.Name;
    public int Sales => Row.Sales;
    public string Sold => ReportsViewModel.Format(Row.Sold);
    public string Average => Row.Sales == 0 ? "—" : ReportsViewModel.Format(new Money(Row.Sold.Cents / Row.Sales));
}
public sealed record SessionReportView(SessionReportRow Row)
{
    public string Opened => Row.OpenedAt.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
    public string Closed => Row.ClosedAt?.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture) ?? "Abierta";
    public string OpenedBy => Row.OpenedBy;
    public string Opening => ReportsViewModel.Format(Row.Opening);
    public string CashSales => ReportsViewModel.Format(Row.CashSales);
    public string Refunds => Row.Refunds.Cents == 0 ? "—" : ReportsViewModel.Format(Row.Refunds);
    public string Expected => ReportsViewModel.Format(Row.Expected);
    public string Counted => Row.Counted is { } c ? ReportsViewModel.Format(c) : "Sin conteo";
    public string Difference => Row.Difference is not { } d ? "—" : d.Cents == 0 ? "Exacta" : d.Cents > 0 ? "Sobrante " + ReportsViewModel.Format(d) : "Faltante " + ReportsViewModel.Format(new(-d.Cents));
}

/// <summary>Un indicador del comparativo: valor actual, valor anterior y tendencia (-1, 0, 1) para colorear.</summary>
public sealed record ComparisonKpi(string Label, string Value, string Previous, string Change, int Trend);
public sealed record ComparisonView(ComparisonRow Row)
{
    public string Name => Row.ProductName;
    public long Quantity => Row.Quantity;
    public long PreviousQuantity => Row.PreviousQuantity;
    public string QuantityChange => ReportsViewModel.Change(Row.QuantityChange, Row.Quantity, Row.PreviousQuantity);
    public int QuantityTrend => Math.Sign(Row.Quantity - Row.PreviousQuantity);
    public string Net => ReportsViewModel.Format(Row.Net);
    public string PreviousNet => ReportsViewModel.Format(Row.PreviousNet);
    public string NetChange => ReportsViewModel.Change(Row.NetChange, Row.Net.Cents, Row.PreviousNet.Cents);
    public int NetTrend => Math.Sign(Row.Net.Cents - Row.PreviousNet.Cents);
}

/// <summary>Celda del mapa de calor; <see cref="Intensity"/> va de 0 (sin ventas) a 1 (la hora más concurrida).</summary>
public sealed record HeatCellView(string Text, double Intensity, bool Hot, string Tip);
public sealed record HeatRowView(string Day, IReadOnlyList<HeatCellView> Cells, string Sales, string Amount);

public sealed record CashierControlView(CashierControlRow Row)
{
    public string Name => Row.Name;
    public string Shifts => Row.Shifts == 0 ? "—" : Row.ClosedShifts == Row.Shifts ? Row.Shifts.ToString(CultureInfo.InvariantCulture) : $"{Row.ClosedShifts} de {Row.Shifts}";
    public string WithDifference => Row.ClosedShifts == 0 ? "—" : Row.ShiftsWithDifference.ToString(CultureInfo.InvariantCulture);
    public string Shortages => Row.Shortages.Cents == 0 ? "—" : ReportsViewModel.Format(Row.Shortages);
    public string Overages => Row.Overages.Cents == 0 ? "—" : ReportsViewModel.Format(Row.Overages);
    public string Net => Row.Net.Cents == 0 ? (Row.ClosedShifts == 0 ? "—" : "Exacto") : (Row.Net.Cents > 0 ? "+" : "−") + ReportsViewModel.Format(new(Math.Abs(Row.Net.Cents)));
    public int NetTrend => Math.Sign(Row.Net.Cents);
    public string Refunds => Row.Refunds == 0 ? "—" : Row.Refunds.ToString(CultureInfo.InvariantCulture);
    public string Refunded => Row.Refunded.Cents == 0 ? "—" : ReportsViewModel.Format(Row.Refunded);
}
public sealed record RefundReasonView(RefundReasonRow Row)
{
    public string Reason => Row.Reason;
    public int Count => Row.Count;
    public string Amount => ReportsViewModel.Format(Row.Amount);
}

public sealed record ForecastView(ForecastRow Row, int Days)
{
    public string Name => Row.ProductName;
    public string Last => Row.LastQuantity == 0 ? "—" : Row.LastQuantity.ToString(CultureInfo.InvariantCulture);
    public string Average => Row.Average.ToString("0.0", CultureInfo.InvariantCulture);
    public int Suggested => Row.Suggested;
    public string Basis => $"{Row.DaysWithSales} de {Days}";
}

public enum ReportTab { Summary, Comparison, Heatmap, CashControl, Forecast }

public sealed class ReportsViewModel(ReportService service, Action<string, string?> log) : INotifyPropertyChanged
{
    public bool Busy { get; private set; }
    public bool Ready => !Busy;
    public ReportTab Tab { get; private set; } = ReportTab.Summary;
    public bool IsSummary => Tab == ReportTab.Summary;
    public bool IsComparison => Tab == ReportTab.Comparison;
    public bool IsHeatmap => Tab == ReportTab.Heatmap;
    public bool IsCashControl => Tab == ReportTab.CashControl;
    public bool IsForecast => Tab == ReportTab.Forecast;
    public bool IsPeriodTab => !IsForecast;

    public DateTime? FromSelected { get; set; } = DateTime.Today;
    public DateTime? ThroughSelected { get; set; } = DateTime.Today;
    public SalesReport? Report { get; private set; }
    public bool HasReport => Report is not null;
    public IReadOnlyList<ProductReportView> Products { get; private set; } = [];
    public IReadOnlyList<CashierReportView> Cashiers { get; private set; } = [];
    public IReadOnlyList<PaymentReportView> Payments { get; private set; } = [];
    public IReadOnlyList<SessionReportView> Sessions { get; private set; } = [];
    public bool HasProducts => Products.Count > 0;
    public bool HasCashiers => Cashiers.Count > 0;
    public bool HasPayments => Payments.Count > 0;
    public bool HasSessions => Sessions.Count > 0;
    public bool NoProducts => HasReport && !HasProducts;
    public bool NoCashiers => HasReport && !HasCashiers;
    public bool NoPayments => HasReport && !HasPayments;
    public bool NoSessions => HasReport && !HasSessions;
    public string PeriodLabel => Report is not { } r ? "" : Period(r.From, r.Through);
    public string SaleCount => Report?.Data.SaleCount.ToString("N0", Spanish) ?? "0";
    public string Gross => Format(Report?.Data.Gross ?? new(0));
    public string Refunded => Format(Report?.Data.Refunded ?? new(0));
    public string Net => Format(Report?.Net ?? new(0));
    public string AverageTicket => Format(Report?.AverageTicket ?? new(0));

    // Comparativo
    public PeriodComparison? Comparison { get; private set; }
    public IReadOnlyList<ComparisonKpi> ComparisonKpis { get; private set; } = [];
    public IReadOnlyList<ComparisonView> ComparisonProducts { get; private set; } = [];
    public bool HasComparisonProducts => ComparisonProducts.Count > 0;
    public bool NoComparisonProducts => Comparison is not null && !HasComparisonProducts;
    public string ComparisonLabel => Comparison is not { } c ? "" : $"{Period(c.Current.From, c.Current.Through)} contra {Period(c.Previous.From, c.Previous.Through).ToLower(Spanish)}";
    public bool NoPreviousData => Comparison is { Previous.Data.SaleCount: 0 };

    // Ventas por hora
    public SalesHeatmap? Heatmap { get; private set; }
    public IReadOnlyList<string> HeatHours { get; private set; } = [];
    public IReadOnlyList<HeatRowView> HeatRows { get; private set; } = [];
    public IReadOnlyList<HeatCellView> HeatTotals { get; private set; } = [];
    public bool HasHeatmap => Heatmap is { TotalSales: > 0 };
    public bool NoHeatmap => Heatmap is { TotalSales: 0 };
    public string HeatPeak => Heatmap?.Peak is not { } p ? "" : $"Hora pico: {Capitalized(DayName(p.Day))} de {p.Hour:00}:00 a {p.Hour:00}:59 · {Plural(p.Sales, "venta", "ventas")}";
    public bool HeatNeedsWeek => Heatmap is { } h && h.Through.DayNumber - h.From.DayNumber < 6;

    // Control de caja
    public CashControlReport? CashControl { get; private set; }
    public IReadOnlyList<CashierControlView> CashierControl { get; private set; } = [];
    public IReadOnlyList<RefundReasonView> RefundReasons { get; private set; } = [];
    public bool HasCashierControl => CashierControl.Count > 0;
    public bool NoCashierControl => CashControl is not null && !HasCashierControl;
    public bool HasRefundReasons => RefundReasons.Count > 0;
    public bool NoRefundReasons => CashControl is not null && !HasRefundReasons;
    public string ShortageTotal => Format(new(CashControl?.Cashiers.Sum(c => c.Shortages.Cents) ?? 0));
    public string OverageTotal => Format(new(CashControl?.Cashiers.Sum(c => c.Overages.Cents) ?? 0));
    public string RefundTotal => Format(new(CashControl?.Reasons.Sum(r => r.Amount.Cents) ?? 0));
    public string RefundCount => (CashControl?.Reasons.Sum(r => r.Count) ?? 0).ToString("N0", Spanish);

    // Pronóstico de producción
    public DateTime? ForecastSelected { get; set; } = DateTime.Today.AddDays(1);
    public ProductionForecast? Forecast { get; private set; }
    public IReadOnlyList<ForecastView> ForecastRows { get; private set; } = [];
    public bool HasForecast => ForecastRows.Count > 0;
    public bool NoForecast => Forecast is not null && !HasForecast;
    public string ForecastTitle => Forecast is not { } f ? "" : $"Para el {f.Target.ToString("dddd d 'de' MMMM", Spanish)}";
    public string ForecastTotal => Forecast is not { } f ? "" : f.SuggestedTotal.ToString("N0", Spanish);
    public string ForecastBasis => Forecast is not { } f ? "" :
        $"Promedio de los últimos {f.WeeksRequested} {Plural(DayName(f.Target.DayOfWeek))} con venta, más {Analytics.SafetyMargin * 100:0} % de margen, redondeado hacia arriba."
        + (f.WeeksWithData < f.WeeksRequested ? $" Hay datos de {f.WeeksWithData} de {f.WeeksRequested} semanas: la sugerencia mejora con más historial." : "");

    public string Message { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");
    internal static string Format(Money value) => (value.Cents / 100m).ToString("C2", Spanish);
    internal static string Change(decimal? change, long current, long previous) => change switch
    {
        null when current == 0 && previous == 0 => "—",
        null => "Nuevo",
        0 => "= 0 %",
        > 0 => "▲ " + change.Value.ToString("0.0", Spanish) + " %",
        _ => "▼ " + Math.Abs(change.Value).ToString("0.0", Spanish) + " %",
    };
    private static string DayName(DayOfWeek day) => Spanish.DateTimeFormat.GetDayName(day);
    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Spanish) + text[1..];
    private static string Plural(string day) => day.EndsWith('s') ? day : day + "s";
    private static string Plural(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count.ToString("N0", Spanish)} {many}";
    private static string Period(DateOnly from, DateOnly through) => from == through
        ? Capitalized(from.ToString("dddd d 'de' MMMM yyyy", Spanish))
        : $"Del {from.ToString("d MMM yyyy", Spanish)} al {through.ToString("d MMM yyyy", Spanish)}";

    public Task LoadAsync() => RangeAsync(DateTime.Today, DateTime.Today);
    public Task TodayAsync() => RangeAsync(DateTime.Today, DateTime.Today);
    public Task YesterdayAsync() => RangeAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1));
    public Task LastSevenDaysAsync() => RangeAsync(DateTime.Today.AddDays(-6), DateTime.Today);
    public Task MonthAsync() => RangeAsync(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);
    private Task RangeAsync(DateTime from, DateTime through) { FromSelected = from; ThroughSelected = through; return GenerateAsync(); }

    public async Task ShowAsync(ReportTab tab)
    {
        Tab = tab; Message = ""; Notify();
        if (tab == ReportTab.Forecast && Forecast is null) await ForecastAsync();
    }

    public Task GenerateAsync() => RunAsync(async () =>
    {
        if (FromSelected is not { } fromDate || ThroughSelected is not { } throughDate) throw new ReportException("Elige la fecha inicial y la final.");
        var (from, through) = (DateOnly.FromDateTime(fromDate), DateOnly.FromDateTime(throughDate));
        var (report, comparison, heatmap, control) = await Task.Run(() => (service.Sales(from, through), service.Compare(from, through), service.Heatmap(from, through), service.CashControl(from, through)));
        Report = report;
        var totalNet = report.Data.Products.Sum(p => p.Net.Cents);
        Products = report.Data.Products.Select((p, i) => new ProductReportView(i + 1, p, totalNet)).ToArray();
        Cashiers = report.Data.Cashiers.Select(c => new CashierReportView(c)).ToArray();
        Payments = report.Data.ByMethod.Select(m => new PaymentReportView(m)).ToArray();
        Sessions = report.Data.Sessions.Select(s => new SessionReportView(s)).ToArray();
        ShowComparison(comparison); ShowHeatmap(heatmap); ShowCashControl(control);
    });

    public Task ForecastAsync() => RunAsync(async () =>
    {
        if (ForecastSelected is not { } target) throw new ReportException("Elige el día que quieres planear.");
        var forecast = await Task.Run(() => service.Forecast(DateOnly.FromDateTime(target)));
        Forecast = forecast;
        ForecastRows = forecast.Rows.Select(r => new ForecastView(r, forecast.WeeksWithData)).ToArray();
    });

    private async Task RunAsync(Func<Task> work)
    {
        if (Busy) return;
        Busy = true; Message = ""; Notify();
        try { await work(); }
        catch (AccessDeniedException error) { Forget(); Message = error.Message; log("authorization.denied", null); }
        catch (ReportException error) { Message = error.Message; }
        catch (Exception error) { Message = "No se pudo generar el reporte."; log("report.failed", error.GetType().Name); }
        finally { Busy = false; Notify(); }
    }

    private void ShowComparison(PeriodComparison comparison)
    {
        Comparison = comparison;
        var (now, before) = (comparison.Current, comparison.Previous);
        ComparisonKpis =
        [
            new("Ventas", now.Data.SaleCount.ToString("N0", Spanish), before.Data.SaleCount.ToString("N0", Spanish), Change(comparison.SalesChange, now.Data.SaleCount, before.Data.SaleCount), Math.Sign(now.Data.SaleCount - before.Data.SaleCount)),
            new("Neto", Format(now.Net), Format(before.Net), Change(comparison.NetChange, now.Net.Cents, before.Net.Cents), Math.Sign(now.Net.Cents - before.Net.Cents)),
            new("Ticket promedio", Format(now.AverageTicket), Format(before.AverageTicket), Change(comparison.TicketChange, now.AverageTicket.Cents, before.AverageTicket.Cents), Math.Sign(now.AverageTicket.Cents - before.AverageTicket.Cents)),
        ];
        ComparisonProducts = comparison.Products.Select(p => new ComparisonView(p)).ToArray();
    }

    private void ShowHeatmap(SalesHeatmap heatmap)
    {
        Heatmap = heatmap;
        var max = Math.Max(1, heatmap.MaxSales);
        // Una hora con poca venta debe notarse: la intensidad mínima visible es 0.12.
        HeatCellView Cell(string where, HeatCell c, int scale) => new(c.Sales == 0 ? "" : c.Sales.ToString("N0", Spanish), c.Sales == 0 ? 0 : 0.12 + 0.88 * c.Sales / scale, c.Sales * 2 > scale,
            $"{where} · {c.Hour:00}:00 a {c.Hour:00}:59 · {Plural(c.Sales, "venta", "ventas")} · {Format(c.Amount)}");
        HeatHours = Enumerable.Range(heatmap.FirstHour, heatmap.LastHour - heatmap.FirstHour + 1).Select(h => $"{h}").ToArray();
        HeatRows = heatmap.Rows.Select(r => new HeatRowView(Capitalized(DayName(r.Day))[..3], r.Cells.Select(c => Cell(Capitalized(DayName(r.Day)), c, max)).ToArray(),
            r.Sales == 0 ? "—" : r.Sales.ToString("N0", Spanish), r.Sales == 0 ? "" : Format(r.Amount))).ToArray();
        var byHour = heatmap.ByHour;
        var hourMax = Math.Max(1, byHour.Max(c => c.Sales));
        HeatTotals = byHour.Select(c => Cell("Todos los días", c, hourMax)).ToArray();
    }

    private void ShowCashControl(CashControlReport report)
    {
        CashControl = report;
        CashierControl = report.Cashiers.Select(c => new CashierControlView(c)).ToArray();
        RefundReasons = report.Reasons.Select(r => new RefundReasonView(r)).ToArray();
    }

    public string? Csv() => Tab switch
    {
        ReportTab.Comparison when Comparison is { } c => ReportService.ToCsv(c),
        ReportTab.Heatmap when Heatmap is { } h => ReportService.ToCsv(h),
        ReportTab.CashControl when CashControl is { } c => ReportService.ToCsv(c),
        ReportTab.Forecast when Forecast is { } f => ReportService.ToCsv(f),
        ReportTab.Summary when Report is { } r => ReportService.ToCsv(r, TimeZoneInfo.Local),
        _ => null,
    };
    public bool CanExport => Tab == ReportTab.Forecast ? Forecast is not null : Report is not null;
    public string SuggestedFileName => Tab switch
    {
        ReportTab.Forecast when Forecast is { } f => $"CasaMiga-produccion-{f.Target:yyyy-MM-dd}.csv",
        _ when Report is { } r => $"CasaMiga-{Tab switch { ReportTab.Comparison => "comparativo", ReportTab.Heatmap => "ventas-por-hora", ReportTab.CashControl => "control-de-caja", _ => "ventas" }}-{r.From:yyyy-MM-dd}_{r.Through:yyyy-MM-dd}.csv",
        _ => "reporte.csv",
    };
    public void Exported(string path) { Message = $"Reporte guardado en {path}"; log("report.exported", null); Notify(); }
    public void ExportFailed() { Message = "No se pudo guardar el archivo. Revisa la carpeta elegida."; Notify(); }
    public void Forget()
    {
        Report = null; Products = []; Cashiers = []; Payments = []; Sessions = [];
        Comparison = null; ComparisonKpis = []; ComparisonProducts = [];
        Heatmap = null; HeatHours = []; HeatRows = []; HeatTotals = [];
        CashControl = null; CashierControl = []; RefundReasons = [];
        Forecast = null; ForecastRows = [];
        Tab = ReportTab.Summary; Message = ""; Notify();
    }
}
