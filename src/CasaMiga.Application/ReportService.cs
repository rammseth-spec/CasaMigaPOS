using System.Globalization;
using System.Text;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed record ProductReportRow(Guid ProductId, string ProductName, long QuantitySold, Money Sold, long QuantityRefunded, Money Refunded)
{
    public Money Net => Sold - Refunded;
}
public sealed record CashierReportRow(Guid UserId, string Name, int Sales, Money Sold);
public sealed record PaymentReportRow(PaymentMethod Method, int Sales, Money Sold, Money Refunded)
{
    public Money Net => Sold - Refunded;
}
public sealed record SessionReportRow(Guid SessionId, string OpenedBy, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, bool Closed,
    Money Opening, Money CashSales, Money Refunds, Money? Counted, Money? Difference, Money Movements = default)
{
    // Movements: entradas menos gastos y retiros de efectivo del turno.
    public Money Expected => Opening + CashSales - Refunds + Movements;
}
public sealed record SalesReportData(int SaleCount, Money Gross, Money Refunded, IReadOnlyList<ProductReportRow> Products,
    IReadOnlyList<CashierReportRow> Cashiers, IReadOnlyList<SessionReportRow> Sessions, IReadOnlyList<PaymentReportRow>? Payments = null)
{
    public IReadOnlyList<PaymentReportRow> ByMethod => Payments ?? [];
}
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
    public const int MaxForecastWeeks = 12;

    public bool CanView() => database.Read(store => identity.Current is { } actor && store.FindUser(actor.Id) is { Active: true } && store.HasPermission(actor.Id, "sales.view"));

    public SalesReport Sales(DateOnly from, DateOnly through)
    {
        Validate(from, through);
        var fromUtc = ToUtc(from); var untilUtc = ToUtc(through.AddDays(1));
        return database.Read(store => { Authorize(store); return new SalesReport(from, through, store.QuerySalesReport(fromUtc, untilUtc)); });
    }

    /// <summary>Compara contra el periodo inmediato anterior de la misma duración: hoy contra ayer, 7 días contra los 7 previos.</summary>
    public PeriodComparison Compare(DateOnly from, DateOnly through)
    {
        Validate(from, through);
        var days = through.DayNumber - from.DayNumber + 1;
        if (from.DayNumber < days) throw new ReportException("La fecha Desde excede el límite permitido.");
        var previousFrom = from.AddDays(-days);
        var previousUtc = ToUtc(previousFrom); var fromUtc = ToUtc(from); var untilUtc = ToUtc(through.AddDays(1));
        return database.Read(store =>
        {
            Authorize(store);
            var current = new SalesReport(from, through, store.QuerySalesReport(fromUtc, untilUtc));
            var previous = new SalesReport(previousFrom, from.AddDays(-1), store.QuerySalesReport(previousUtc, fromUtc));
            return new PeriodComparison(current, previous, Analytics.CompareProducts(current.Data, previous.Data),
                Analytics.Daily(from, through, store.QuerySaleFacts(fromUtc, untilUtc), timeZone),
                Analytics.Daily(previousFrom, from.AddDays(-1), store.QuerySaleFacts(previousUtc, fromUtc), timeZone));
        });
    }

    public SalesHeatmap Heatmap(DateOnly from, DateOnly through)
    {
        Validate(from, through);
        var fromUtc = ToUtc(from); var untilUtc = ToUtc(through.AddDays(1));
        return database.Read(store => { Authorize(store); return Analytics.Heatmap(from, through, store.QuerySaleFacts(fromUtc, untilUtc), timeZone); });
    }

    public CashControlReport CashControl(DateOnly from, DateOnly through)
    {
        Validate(from, through);
        var fromUtc = ToUtc(from); var untilUtc = ToUtc(through.AddDays(1));
        return database.Read(store =>
        {
            Authorize(store);
            var shifts = store.QuerySalesReport(fromUtc, untilUtc).Sessions.Select(s => new ShiftFact(s.OpenedBy, s.Closed, s.Difference?.Cents));
            return Analytics.CashControl(from, through, shifts, store.QueryRefundFacts(fromUtc, untilUtc));
        });
    }

    /// <summary>Piezas sugeridas para <paramref name="target"/> con base en el mismo día de las semanas anteriores.</summary>
    public ProductionForecast Forecast(DateOnly target, int weeks = Analytics.ForecastWeeks)
    {
        if (weeks is < 1 or > MaxForecastWeeks) throw new ReportException($"El pronóstico usa de 1 a {MaxForecastWeeks} semanas.");
        if (target.DayNumber < 7 * weeks || target == DateOnly.MaxValue) throw new ReportException("La fecha del pronóstico excede el límite permitido.");
        var fromUtc = ToUtc(target.AddDays(-7 * weeks)); var untilUtc = ToUtc(target.AddDays(-6));
        return database.Read(store => { Authorize(store); return Analytics.Forecast(target, weeks, store.QuerySaleLines(fromUtc, untilUtc), timeZone); });
    }

    public static string ToCsv(SalesReport report, TimeZoneInfo timeZone)
    {
        var csv = new Csv();
        csv.Row("Reporte de ventas Casa Miga", $"{report.From:yyyy-MM-dd}", $"{report.Through:yyyy-MM-dd}");
        csv.Row();
        csv.Row("Ventas", "Vendido", "Devuelto", "Neto", "Ticket promedio");
        csv.Row(report.Data.SaleCount, Csv.Amount(report.Data.Gross), Csv.Amount(report.Data.Refunded), Csv.Amount(report.Net), Csv.Amount(report.AverageTicket));
        csv.Row();
        csv.Row("Producto", "Piezas vendidas", "Vendido", "Piezas devueltas", "Devuelto", "Neto");
        foreach (var p in report.Data.Products) csv.Row(p.ProductName, p.QuantitySold, Csv.Amount(p.Sold), p.QuantityRefunded, Csv.Amount(p.Refunded), Csv.Amount(p.Net));
        csv.Row();
        csv.Row("Forma de pago", "Ventas", "Vendido", "Devuelto", "Neto");
        foreach (var m in report.Data.ByMethod) csv.Row(Payment.Label(m.Method), m.Sales, Csv.Amount(m.Sold), Csv.Amount(m.Refunded), Csv.Amount(m.Net));
        csv.Row();
        csv.Row("Cajero", "Ventas", "Vendido");
        foreach (var c in report.Data.Cashiers) csv.Row(c.Name, c.Sales, Csv.Amount(c.Sold));
        csv.Row();
        csv.Row("Apertura", "Cierre", "Abrió", "Estado", "Fondo", "Ventas efectivo", "Devoluciones", "Movimientos", "Esperado", "Contado", "Diferencia");
        foreach (var s in report.Data.Sessions)
            csv.Row(Local(s.OpenedAt, timeZone), s.ClosedAt is { } c ? Local(c, timeZone) : "", s.OpenedBy, s.Closed ? "Cerrada" : "Abierta",
                Csv.Amount(s.Opening), Csv.Amount(s.CashSales), Csv.Amount(s.Refunds), Csv.Amount(s.Movements), Csv.Amount(s.Expected), s.Counted is { } n ? Csv.Amount(n) : "", s.Difference is { } d ? Csv.Amount(d) : "");
        return csv.ToString();

        static string Local(DateTimeOffset at, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(at, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    public static string ToCsv(PeriodComparison comparison)
    {
        var (current, previous) = (comparison.Current, comparison.Previous);
        var csv = new Csv();
        csv.Row("Comparativo Casa Miga", $"{current.From:yyyy-MM-dd}", $"{current.Through:yyyy-MM-dd}", "contra", $"{previous.From:yyyy-MM-dd}", $"{previous.Through:yyyy-MM-dd}");
        csv.Row();
        csv.Row("Indicador", "Periodo", "Anterior", "Cambio %");
        csv.Row("Ventas", current.Data.SaleCount, previous.Data.SaleCount, Csv.Percent(comparison.SalesChange));
        csv.Row("Neto", Csv.Amount(current.Net), Csv.Amount(previous.Net), Csv.Percent(comparison.NetChange));
        csv.Row("Ticket promedio", Csv.Amount(current.AverageTicket), Csv.Amount(previous.AverageTicket), Csv.Percent(comparison.TicketChange));
        csv.Row();
        csv.Row("Producto", "Piezas", "Piezas antes", "Cambio piezas %", "Neto", "Neto antes", "Cambio neto %");
        foreach (var p in comparison.Products)
            csv.Row(p.ProductName, p.Quantity, p.PreviousQuantity, Csv.Percent(p.QuantityChange), Csv.Amount(p.Net), Csv.Amount(p.PreviousNet), Csv.Percent(p.NetChange));
        return csv.ToString();
    }

    public static string ToCsv(SalesHeatmap heatmap)
    {
        var csv = new Csv();
        var hours = Enumerable.Range(heatmap.FirstHour, heatmap.LastHour - heatmap.FirstHour + 1).ToList();
        csv.Row("Ventas por hora Casa Miga", $"{heatmap.From:yyyy-MM-dd}", $"{heatmap.Through:yyyy-MM-dd}");
        csv.Row();
        csv.Row([ "Día", .. hours.Select(h => (object?)$"{h:00}:00"), "Ventas", "Vendido" ]);
        foreach (var row in heatmap.Rows)
            csv.Row([ Csv.DayName(row.Day), .. row.Cells.Select(c => (object?)c.Sales), row.Sales, Csv.Amount(row.Amount) ]);
        return csv.ToString();
    }

    public static string ToCsv(CashControlReport report)
    {
        var csv = new Csv();
        csv.Row("Control de caja Casa Miga", $"{report.From:yyyy-MM-dd}", $"{report.Through:yyyy-MM-dd}");
        csv.Row();
        csv.Row("Cajero", "Turnos", "Turnos contados", "Con diferencia", "Faltantes", "Sobrantes", "Neto", "Devoluciones", "Monto devuelto");
        foreach (var c in report.Cashiers)
            csv.Row(c.Name, c.Shifts, c.ClosedShifts, c.ShiftsWithDifference, Csv.Amount(c.Shortages), Csv.Amount(c.Overages), Csv.Amount(c.Net), c.Refunds, Csv.Amount(c.Refunded));
        csv.Row();
        csv.Row("Motivo de devolución", "Veces", "Monto");
        foreach (var r in report.Reasons) csv.Row(r.Reason, r.Count, Csv.Amount(r.Amount));
        return csv.ToString();
    }

    public static string ToCsv(ProductionForecast forecast)
    {
        var csv = new Csv();
        csv.Row("Pronóstico de producción Casa Miga", $"{forecast.Target:yyyy-MM-dd}", Csv.DayName(forecast.Target.DayOfWeek),
            $"{forecast.WeeksWithData} de {forecast.WeeksRequested} semanas", $"margen {Analytics.SafetyMargin * 100:0} %");
        csv.Row();
        csv.Row("Producto", "Última semana", "Promedio", "Sugerido", "Días con venta");
        foreach (var r in forecast.Rows) csv.Row(r.ProductName, r.LastQuantity, r.Average.ToString("0.0", CultureInfo.InvariantCulture), r.Suggested, r.DaysWithSales);
        return csv.ToString();
    }

    private void Validate(DateOnly from, DateOnly through)
    {
        if (from > through) throw new ReportException("La fecha Desde no puede ser posterior a Hasta.");
        if (through.DayNumber - from.DayNumber + 1 > MaxDays) throw new ReportException($"El periodo máximo es de {MaxDays} días.");
        if (through == DateOnly.MaxValue) throw new ReportException("La fecha Hasta excede el límite permitido.");
    }

    private void Authorize(ILocalStore store)
    {
        if (identity.Current is not { } actor || store.FindUser(actor.Id) is not { Active: true } || !store.HasPermission(actor.Id, "sales.view"))
            throw new AccessDeniedException();
    }

    private DateTimeOffset ToUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local)) throw new ReportException("La fecha coincide con una hora local inexistente.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    private sealed class Csv
    {
        private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");
        private readonly StringBuilder text = new();
        public void Row(params object?[] values) => text.AppendLine(string.Join(",", values.Select(Cell)));
        public override string ToString() => text.ToString();
        public static string Amount(Money value) => (value.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        public static string Percent(decimal? value) => value is { } v ? v.ToString("0.0", CultureInfo.InvariantCulture) : "";
        public static string DayName(DayOfWeek day) => Spanish.DateTimeFormat.GetDayName(day);
        private static string Cell(object? value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            // Evita que Excel interprete nombres como fórmulas.
            if (text.Length > 0 && "=+-@".Contains(text[0]) && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) text = "'" + text;
            return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }
    }
}
