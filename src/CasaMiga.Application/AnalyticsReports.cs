using CasaMiga.Domain;

namespace CasaMiga.Application;

// Hechos crudos (UTC) que entrega el almacén; los reportes agrupan por día y hora LOCAL en esta capa.
// Las ventas canceladas no cuentan como demanda ni como afluencia.
public sealed record SaleLineFact(DateTimeOffset At, Guid ProductId, string ProductName, int Quantity);
public sealed record SaleFact(DateTimeOffset At, long TotalCents);
public sealed record ShiftFact(string OpenedBy, bool Closed, long? DifferenceCents);
public sealed record RefundFact(string Reason, long TotalCents, string SaleCashier);

public sealed record ForecastRow(Guid ProductId, string ProductName, long LastQuantity, decimal Average, int Suggested, int DaysWithSales);
public sealed record ProductionForecast(DateOnly Target, int WeeksRequested, int WeeksWithData, IReadOnlyList<ForecastRow> Rows)
{
    public long SuggestedTotal => Rows.Sum(r => (long)r.Suggested);
}

public sealed record HeatCell(int Hour, int Sales, Money Amount);
public sealed record HeatRow(DayOfWeek Day, IReadOnlyList<HeatCell> Cells)
{
    public int Sales => Cells.Sum(c => c.Sales);
    public Money Amount => new(Cells.Sum(c => c.Amount.Cents));
}
public sealed record SalesHeatmap(DateOnly From, DateOnly Through, int FirstHour, int LastHour, IReadOnlyList<HeatRow> Rows)
{
    public int MaxSales => Rows.SelectMany(r => r.Cells).Select(c => c.Sales).DefaultIfEmpty(0).Max();
    public int TotalSales => Rows.Sum(r => r.Sales);
    public IReadOnlyList<HeatCell> ByHour => Enumerable.Range(FirstHour, LastHour - FirstHour + 1)
        .Select(h => new HeatCell(h, Rows.Sum(r => r.Cells[h - FirstHour].Sales), new(Rows.Sum(r => r.Cells[h - FirstHour].Amount.Cents)))).ToList();
    public (DayOfWeek Day, int Hour, int Sales)? Peak => Rows.SelectMany(r => r.Cells.Select(c => (r.Day, c.Hour, c.Sales)))
        .Where(c => c.Sales > 0).OrderByDescending(c => c.Sales).Select(c => ((DayOfWeek, int, int)?)c).FirstOrDefault();
}

public sealed record ComparisonRow(string ProductName, long Quantity, Money Net, long PreviousQuantity, Money PreviousNet)
{
    public decimal? QuantityChange => Analytics.Change(Quantity, PreviousQuantity);
    public decimal? NetChange => Analytics.Change(Net.Cents, PreviousNet.Cents);
}
/// <summary>Ventas de un día local (sin canceladas) para las gráficas por día.</summary>
public sealed record DaySales(DateOnly Day, int Sales, Money Amount);
public sealed record PeriodComparison(SalesReport Current, SalesReport Previous, IReadOnlyList<ComparisonRow> Products,
    IReadOnlyList<DaySales>? CurrentDays = null, IReadOnlyList<DaySales>? PreviousDays = null)
{
    public decimal? SalesChange => Analytics.Change(Current.Data.SaleCount, Previous.Data.SaleCount);
    public decimal? NetChange => Analytics.Change(Current.Net.Cents, Previous.Net.Cents);
    public decimal? TicketChange => Analytics.Change(Current.AverageTicket.Cents, Previous.AverageTicket.Cents);
}

public sealed record CashierControlRow(string Name, int Shifts, int ClosedShifts, int ShiftsWithDifference, Money Shortages, Money Overages, int Refunds, Money Refunded)
{
    public Money Net => Overages - Shortages;
}
public sealed record RefundReasonRow(string Reason, int Count, Money Amount);
public sealed record CashControlReport(DateOnly From, DateOnly Through, IReadOnlyList<CashierControlRow> Cashiers, IReadOnlyList<RefundReasonRow> Reasons);

/// <summary>Cálculos de los reportes analíticos. Son puros: se prueban sin base de datos.</summary>
public static class Analytics
{
    public const decimal SafetyMargin = 0.10m;
    public const int ForecastWeeks = 4;

    /// <summary>Cambio porcentual; null cuando no hay base para comparar.</summary>
    public static decimal? Change(long current, long previous) => previous == 0 ? null : Math.Round((current - previous) * 100m / Math.Abs(previous), 1);

    /// <summary>Promedio del mismo día de la semana en las últimas semanas con venta, más un margen de seguridad.</summary>
    public static ProductionForecast Forecast(DateOnly target, int weeks, IEnumerable<SaleLineFact> lines, TimeZoneInfo zone)
    {
        var days = Enumerable.Range(1, weeks).Select(k => target.AddDays(-7 * k)).ToHashSet();
        var sold = lines.Select(l => (Day: DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(l.At, zone).DateTime), Line: l))
            .Where(x => days.Contains(x.Day)).ToList();
        // Sólo cuentan los días en que la sucursal vendió algo: un día cerrado no es demanda cero.
        var openDays = sold.Select(x => x.Day).Distinct().Count();
        var last = target.AddDays(-7);
        var rows = sold.GroupBy(x => x.Line.ProductId).Select(g =>
        {
            var name = g.MaxBy(x => x.Line.At).Line.ProductName;
            var average = Math.Round(g.Sum(x => (decimal)x.Line.Quantity) / openDays, 1);
            var suggested = (int)Math.Ceiling(average * (1 + SafetyMargin));
            return new ForecastRow(g.Key, name, g.Where(x => x.Day == last).Sum(x => (long)x.Line.Quantity), average, suggested, g.Select(x => x.Day).Distinct().Count());
        }).OrderByDescending(r => r.Suggested).ThenBy(r => r.ProductName, StringComparer.CurrentCulture).ToList();
        return new(target, weeks, openDays, rows);
    }

    /// <summary>Ventas por día de la semana y hora local. Siempre muestra al menos de 7:00 a 20:00.</summary>
    public static SalesHeatmap Heatmap(DateOnly from, DateOnly through, IEnumerable<SaleFact> sales, TimeZoneInfo zone)
    {
        var local = sales.Select(s => (Time: TimeZoneInfo.ConvertTime(s.At, zone), s.TotalCents)).ToList();
        var first = Math.Min(7, local.Select(s => s.Time.Hour).DefaultIfEmpty(7).Min());
        var last = Math.Max(20, local.Select(s => s.Time.Hour).DefaultIfEmpty(20).Max());
        DayOfWeek[] week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        var buckets = local.ToLookup(s => (s.Time.DayOfWeek, s.Time.Hour));
        var rows = week.Select(day => new HeatRow(day, Enumerable.Range(first, last - first + 1).Select(hour =>
        {
            var bucket = buckets[(day, hour)];
            return new HeatCell(hour, bucket.Count(), new Money(bucket.Sum(s => s.TotalCents)));
        }).ToList())).ToList();
        return new(from, through, first, last, rows);
    }

    /// <summary>Una entrada por cada día del periodo, también los días sin ventas.</summary>
    public static IReadOnlyList<DaySales> Daily(DateOnly from, DateOnly through, IEnumerable<SaleFact> sales, TimeZoneInfo zone)
    {
        var byDay = sales.GroupBy(s => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.At, zone).DateTime))
            .ToDictionary(g => g.Key, g => (Sales: g.Count(), Cents: g.Sum(s => s.TotalCents)));
        return Enumerable.Range(0, through.DayNumber - from.DayNumber + 1).Select(from.AddDays)
            .Select(day => byDay.TryGetValue(day, out var v) ? new DaySales(day, v.Sales, new(v.Cents)) : new DaySales(day, 0, new(0))).ToList();
    }

    public static IReadOnlyList<ComparisonRow> CompareProducts(SalesReportData current, SalesReportData previous)
    {
        var map = new Dictionary<Guid, (string Name, long Quantity, long Net, long PreviousQuantity, long PreviousNet)>();
        foreach (var p in current.Products) map[p.ProductId] = (p.ProductName, p.QuantitySold - p.QuantityRefunded, p.Net.Cents, 0, 0);
        foreach (var p in previous.Products)
        {
            var row = map.GetValueOrDefault(p.ProductId, (p.ProductName, 0, 0, 0, 0));
            map[p.ProductId] = (row.Name, row.Quantity, row.Net, p.QuantitySold - p.QuantityRefunded, p.Net.Cents);
        }
        return map.Values.Select(v => new ComparisonRow(v.Name, v.Quantity, new(v.Net), v.PreviousQuantity, new(v.PreviousNet)))
            .OrderByDescending(r => r.Net.Cents).ThenByDescending(r => r.PreviousNet.Cents).ThenBy(r => r.ProductName, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>Faltantes y sobrantes por quien abrió la caja; devoluciones por quien hizo la venta original.</summary>
    public static CashControlReport CashControl(DateOnly from, DateOnly through, IEnumerable<ShiftFact> shifts, IEnumerable<RefundFact> refunds)
    {
        var shiftList = shifts.ToList(); var refundList = refunds.ToList();
        var cashiers = shiftList.Select(s => s.OpenedBy).Concat(refundList.Select(r => r.SaleCashier)).Distinct().Select(name =>
        {
            var own = shiftList.Where(s => s.OpenedBy == name).ToList();
            var counted = own.Where(s => s.Closed && s.DifferenceCents is not null).Select(s => s.DifferenceCents!.Value).ToList();
            var refunded = refundList.Where(r => r.SaleCashier == name).ToList();
            return new CashierControlRow(name, own.Count, counted.Count, counted.Count(d => d != 0),
                new Money(-counted.Where(d => d < 0).Sum()), new Money(counted.Where(d => d > 0).Sum()),
                refunded.Count, new Money(refunded.Sum(r => r.TotalCents)));
        }).OrderByDescending(c => c.Shortages.Cents).ThenByDescending(c => c.Refunded.Cents).ThenBy(c => c.Name, StringComparer.CurrentCulture).ToList();
        var reasons = refundList.GroupBy(r => r.Reason.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new RefundReasonRow(g.Key, g.Count(), new Money(g.Sum(r => r.TotalCents))))
            .OrderByDescending(r => r.Count).ThenByDescending(r => r.Amount.Cents).ThenBy(r => r.Reason, StringComparer.CurrentCulture).ToList();
        return new(from, through, cashiers, reasons);
    }
}
