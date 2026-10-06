using System.Globalization;
using System.Text;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed record ProductReportRow(Guid ProductId, string ProductName, long QuantitySold, Money Sold, long QuantityRefunded, Money Refunded)
{
    public Money Net => Sold - Refunded;
}
public sealed record CashierReportRow(Guid UserId, string Name, int Sales, Money Sold);
public sealed record SessionReportRow(Guid SessionId, string OpenedBy, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, bool Closed,
    Money Opening, Money CashSales, Money Refunds, Money? Counted, Money? Difference)
{
    public Money Expected => Opening + CashSales - Refunds;
}
public sealed record SalesReportData(int SaleCount, Money Gross, Money Refunded, IReadOnlyList<ProductReportRow> Products,
    IReadOnlyList<CashierReportRow> Cashiers, IReadOnlyList<SessionReportRow> Sessions);
public sealed record SalesReport(DateOnly From, DateOnly Through, SalesReportData Data)
{
    public Money Net => Data.Gross - Data.Refunded;
    public Money AverageTicket => Data.SaleCount == 0 ? new(0) : new(Data.Gross.Cents / Data.SaleCount);
}
public sealed class ReportException(string message) : Exception(message);

/// <summary>Reportes de sólo lectura por días locales. Las ventas cuentan por fecha de venta y las devoluciones por fecha de devolución.</summary>
public sealed class ReportService(ILocalReadUnitOfWork database, UserSession identity, TimeZoneInfo timeZone)
{
    public const int MaxDays = 366;

    public bool CanView() => database.Read(store => identity.Current is { } actor && store.FindUser(actor.Id) is { Active: true } && store.HasPermission(actor.Id, "sales.view"));

    public SalesReport Sales(DateOnly from, DateOnly through)
    {
        if (from > through) throw new ReportException("La fecha Desde no puede ser posterior a Hasta.");
        if (through.DayNumber - from.DayNumber + 1 > MaxDays) throw new ReportException($"El periodo máximo es de {MaxDays} días.");
        if (through == DateOnly.MaxValue) throw new ReportException("La fecha Hasta excede el límite permitido.");
        var fromUtc = ToUtc(from); var untilUtc = ToUtc(through.AddDays(1));
        return database.Read(store =>
        {
            if (identity.Current is not { } actor || store.FindUser(actor.Id) is not { Active: true } || !store.HasPermission(actor.Id, "sales.view"))
                throw new AccessDeniedException();
            return new SalesReport(from, through, store.QuerySalesReport(fromUtc, untilUtc));
        });
    }

    public static string ToCsv(SalesReport report, TimeZoneInfo timeZone)
    {
        var csv = new StringBuilder();
        void Row(params object?[] values) => csv.AppendLine(string.Join(",", values.Select(Cell)));
        Row("Reporte de ventas Casa Miga", $"{report.From:yyyy-MM-dd}", $"{report.Through:yyyy-MM-dd}");
        Row();
        Row("Ventas", "Vendido", "Devuelto", "Neto", "Ticket promedio");
        Row(report.Data.SaleCount, Amount(report.Data.Gross), Amount(report.Data.Refunded), Amount(report.Net), Amount(report.AverageTicket));
        Row();
        Row("Producto", "Piezas vendidas", "Vendido", "Piezas devueltas", "Devuelto", "Neto");
        foreach (var p in report.Data.Products) Row(p.ProductName, p.QuantitySold, Amount(p.Sold), p.QuantityRefunded, Amount(p.Refunded), Amount(p.Net));
        Row();
        Row("Cajero", "Ventas", "Vendido");
        foreach (var c in report.Data.Cashiers) Row(c.Name, c.Sales, Amount(c.Sold));
        Row();
        Row("Apertura", "Cierre", "Abrió", "Estado", "Fondo", "Ventas efectivo", "Devoluciones", "Esperado", "Contado", "Diferencia");
        foreach (var s in report.Data.Sessions)
            Row(Local(s.OpenedAt, timeZone), s.ClosedAt is { } c ? Local(c, timeZone) : "", s.OpenedBy, s.Closed ? "Cerrada" : "Abierta",
                Amount(s.Opening), Amount(s.CashSales), Amount(s.Refunds), Amount(s.Expected), s.Counted is { } n ? Amount(n) : "", s.Difference is { } d ? Amount(d) : "");
        return csv.ToString();

        static string Amount(Money value) => (value.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        static string Local(DateTimeOffset at, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(at, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        static string Cell(object? value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            // Evita que Excel interprete nombres como fórmulas.
            if (text.Length > 0 && "=+-@".Contains(text[0]) && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) text = "'" + text;
            return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }
    }

    private DateTimeOffset ToUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local)) throw new ReportException("La fecha coincide con una hora local inexistente.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }
}
