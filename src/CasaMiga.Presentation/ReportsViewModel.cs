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

public sealed class ReportsViewModel(ReportService service, Action<string, string?> log) : INotifyPropertyChanged
{
    public bool Busy { get; private set; }
    public bool Ready => !Busy;
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
    public string PeriodLabel => Report is not { } r ? "" : r.From == r.Through
        ? $"{r.From.ToString("dddd d 'de' MMMM yyyy", Spanish)}"
        : $"Del {r.From.ToString("d MMM yyyy", Spanish)} al {r.Through.ToString("d MMM yyyy", Spanish)}";
    public string SaleCount => Report?.Data.SaleCount.ToString("N0", Spanish) ?? "0";
    public string Gross => Format(Report?.Data.Gross ?? new(0));
    public string Refunded => Format(Report?.Data.Refunded ?? new(0));
    public string Net => Format(Report?.Net ?? new(0));
    public string AverageTicket => Format(Report?.AverageTicket ?? new(0));
    public string Message { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");
    internal static string Format(Money value) => (value.Cents / 100m).ToString("C2", Spanish);

    public Task LoadAsync() => RangeAsync(DateTime.Today, DateTime.Today);
    public Task TodayAsync() => RangeAsync(DateTime.Today, DateTime.Today);
    public Task YesterdayAsync() => RangeAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1));
    public Task LastSevenDaysAsync() => RangeAsync(DateTime.Today.AddDays(-6), DateTime.Today);
    public Task MonthAsync() => RangeAsync(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);
    private Task RangeAsync(DateTime from, DateTime through) { FromSelected = from; ThroughSelected = through; return GenerateAsync(); }

    public async Task GenerateAsync()
    {
        if (Busy) return;
        Busy = true; Message = ""; Notify();
        try
        {
            if (FromSelected is not { } from || ThroughSelected is not { } through) throw new ReportException("Elige la fecha inicial y la final.");
            Report = await Task.Run(() => service.Sales(DateOnly.FromDateTime(from), DateOnly.FromDateTime(through)));
            var totalNet = Report.Data.Products.Sum(p => p.Net.Cents);
            Products = Report.Data.Products.Select((p, i) => new ProductReportView(i + 1, p, totalNet)).ToArray();
            Cashiers = Report.Data.Cashiers.Select(c => new CashierReportView(c)).ToArray();
            Payments = Report.Data.ByMethod.Select(m => new PaymentReportView(m)).ToArray();
            Sessions = Report.Data.Sessions.Select(s => new SessionReportView(s)).ToArray();
        }
        catch (AccessDeniedException error) { Forget(); Message = error.Message; log("authorization.denied", null); }
        catch (ReportException error) { Message = error.Message; }
        catch (Exception error) { Message = "No se pudo generar el reporte."; log("report.failed", error.GetType().Name); }
        finally { Busy = false; Notify(); }
    }
    public string? Csv() => Report is null ? null : ReportService.ToCsv(Report, TimeZoneInfo.Local);
    public string SuggestedFileName => Report is null ? "reporte.csv" : $"CasaMiga-ventas-{Report.From:yyyy-MM-dd}_{Report.Through:yyyy-MM-dd}.csv";
    public void Exported(string path) { Message = $"Reporte guardado en {path}"; log("report.exported", null); Notify(); }
    public void ExportFailed() { Message = "No se pudo guardar el archivo. Revisa la carpeta elegida."; Notify(); }
    public void Forget() { Report = null; Products = []; Cashiers = []; Payments = []; Sessions = []; Message = ""; Notify(); }
}
