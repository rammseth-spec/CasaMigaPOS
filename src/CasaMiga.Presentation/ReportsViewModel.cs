using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record ProductReportView(ProductReportRow Row)
{
    public string Name => Row.ProductName;
    public long Sold => Row.QuantitySold;
    public string SoldAmount => ReportsViewModel.Format(Row.Sold);
    public long Refunded => Row.QuantityRefunded;
    public string RefundedAmount => Row.Refunded.Cents == 0 ? "" : ReportsViewModel.Format(Row.Refunded);
    public string Net => ReportsViewModel.Format(Row.Net);
}
public sealed record CashierReportView(CashierReportRow Row)
{
    public string Name => Row.Name;
    public int Sales => Row.Sales;
    public string Sold => ReportsViewModel.Format(Row.Sold);
}
public sealed record SessionReportView(SessionReportRow Row)
{
    public string Opened => Row.OpenedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    public string Closed => Row.ClosedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "Abierta";
    public string OpenedBy => Row.OpenedBy;
    public string Opening => ReportsViewModel.Format(Row.Opening);
    public string CashSales => ReportsViewModel.Format(Row.CashSales);
    public string Refunds => Row.Refunds.Cents == 0 ? "" : ReportsViewModel.Format(Row.Refunds);
    public string Expected => ReportsViewModel.Format(Row.Expected);
    public string Counted => Row.Counted is { } c ? ReportsViewModel.Format(c) : "Sin conteo";
    public string Difference => Row.Difference is not { } d ? "" : d.Cents == 0 ? "Exacta" : d.Cents > 0 ? "Sobrante " + ReportsViewModel.Format(d) : "Faltante " + ReportsViewModel.Format(new(-d.Cents));
}

public sealed class ReportsViewModel(ReportService service, Action<string, string?> log) : INotifyPropertyChanged
{
    public bool Busy { get; private set; }
    public bool Ready => !Busy;
    public string FromDate { get; set; } = Today;
    public string ThroughDate { get; set; } = Today;
    public SalesReport? Report { get; private set; }
    public bool HasReport => Report is not null;
    public IReadOnlyList<ProductReportView> Products { get; private set; } = [];
    public IReadOnlyList<CashierReportView> Cashiers { get; private set; } = [];
    public IReadOnlyList<SessionReportView> Sessions { get; private set; } = [];
    public string Summary => Report is not { } r ? "" :
        $"Ventas: {r.Data.SaleCount}   ·   Vendido: {Format(r.Data.Gross)}   ·   Devuelto: {Format(r.Data.Refunded)}   ·   Neto: {Format(r.Net)}   ·   Ticket promedio: {Format(r.AverageTicket)}";
    public string Message { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    private static string Today => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    internal static string Format(Money value) => (value.Cents / 100m).ToString("C2", CultureInfo.GetCultureInfo("es-MX"));

    public Task LoadAsync() { FromDate = Today; ThroughDate = Today; return GenerateAsync(); }
    public Task TodayAsync() { FromDate = Today; ThroughDate = Today; return GenerateAsync(); }
    public Task MonthAsync()
    {
        var now = DateTime.Now;
        FromDate = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); ThroughDate = Today;
        return GenerateAsync();
    }
    public async Task GenerateAsync()
    {
        if (Busy) return;
        Busy = true; Message = ""; Notify();
        try
        {
            var from = Parse(FromDate, "Desde"); var through = Parse(ThroughDate, "Hasta");
            Report = await Task.Run(() => service.Sales(from, through));
            Products = Report.Data.Products.Select(p => new ProductReportView(p)).ToArray();
            Cashiers = Report.Data.Cashiers.Select(c => new CashierReportView(c)).ToArray();
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
    public void Forget() { Report = null; Products = []; Cashiers = []; Sessions = []; Message = ""; Notify(); }

    private static DateOnly Parse(string value, string label)
    {
        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw new ReportException($"{label} debe usar el formato AAAA-MM-DD.");
        return parsed;
    }
}
