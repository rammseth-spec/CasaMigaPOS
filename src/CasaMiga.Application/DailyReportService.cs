using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed record DailyProductSummary(string ProductName, long Pieces, Money Revenue);

public sealed record DailySaleMoment(DateTimeOffset ConfirmedAt, Money Amount);

public sealed record DailyHourlySales(int Hour, Money Sales, int Tickets);

public sealed record DailyCashSessionSummary(
    Guid Id,
    string TerminalName,
    string OpenedByName,
    string? ClosedByName,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    bool IsOpen,
    Money OpeningFund,
    Money CashSales,
    Money? Expected,
    Money? Counted,
    Money? Difference,
    string? DiscrepancyNote);

public sealed record DailyReportSnapshot(
    Money Sales,
    int Tickets,
    long Pieces,
    IReadOnlyList<DailySaleMoment> SaleMoments,
    IReadOnlyList<DailyProductSummary> TopProducts,
    IReadOnlyList<DailyCashSessionSummary> CashSessions);

public sealed record DailyOperationsReport(
    DateOnly Day,
    DateTimeOffset FromUtc,
    DateTimeOffset UntilUtc,
    Money Sales,
    int Tickets,
    long Pieces,
    Money AverageTicket,
    IReadOnlyList<DailyHourlySales> HourlySales,
    DateTimeOffset? FirstSaleAt,
    DateTimeOffset? LastSaleAt,
    DailyHourlySales? PeakHour,
    IReadOnlyList<DailyProductSummary> TopProducts,
    IReadOnlyList<DailyCashSessionSummary> CashSessions);

public sealed class DailyReportException(string message) : Exception(message);

public sealed class DailyReportService(
    ILocalReadUnitOfWork database,
    UserSession identity,
    TimeZoneInfo timeZone,
    IClock clock)
{
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, timeZone).DateTime);

    public bool CanView() => database.Read(store =>
        identity.Current is { } actor
        && store.FindUser(actor.Id) is { Active: true }
        && store.HasPermission(actor.Id, "sales.view")
        && store.HasPermission(actor.Id, "cash.view"));

    public DailyOperationsReport Read(DateOnly day)
    {
        if (day == DateOnly.MaxValue)
            throw new DailyReportException("La fecha excede el límite permitido.");

        var from = ToUtc(day);
        var until = ToUtc(day.AddDays(1));
        var snapshot = database.Read(store =>
        {
            if (identity.Current is not { } actor
                || store.FindUser(actor.Id) is not { Active: true }
                || !store.HasPermission(actor.Id, "sales.view")
                || !store.HasPermission(actor.Id, "cash.view"))
                throw new AccessDeniedException();
            return store.QueryDailyReport(from, until);
        });
        var average = snapshot.Tickets == 0
            ? new Money(0)
            : new Money(checked((long)decimal.Round(
                (decimal)snapshot.Sales.Cents / snapshot.Tickets,
                0,
                MidpointRounding.AwayFromZero)));

        var localSales = snapshot.SaleMoments
            .Select(x => (At: TimeZoneInfo.ConvertTime(x.ConfirmedAt, timeZone), x.Amount))
            .OrderBy(x => x.At)
            .ToArray();
        var grouped = localSales
            .GroupBy(x => x.At.Hour)
            .ToDictionary(
                group => group.Key,
                group => new DailyHourlySales(group.Key, Sum(group.Select(x => x.Amount)), group.Count()));
        IReadOnlyList<DailyHourlySales> hourly = localSales.Length == 0
            ? []
            : Enumerable.Range(localSales[0].At.Hour, localSales[^1].At.Hour - localSales[0].At.Hour + 1)
                .Select(hour => grouped.TryGetValue(hour, out var bucket)
                    ? bucket
                    : new DailyHourlySales(hour, new Money(0), 0))
                .ToArray();
        var peak = hourly
            .Where(x => x.Tickets > 0)
            .OrderByDescending(x => x.Sales.Cents)
            .ThenByDescending(x => x.Tickets)
            .ThenBy(x => x.Hour)
            .FirstOrDefault();

        return new(day, from, until, snapshot.Sales, snapshot.Tickets, snapshot.Pieces, average,
            hourly,
            localSales.Length == 0 ? null : localSales[0].At,
            localSales.Length == 0 ? null : localSales[^1].At,
            peak,
            snapshot.TopProducts, snapshot.CashSessions);
    }

    private static Money Sum(IEnumerable<Money> values)
    {
        var total = new Money(0);
        foreach (var value in values) total += value;
        return total;
    }

    private DateTimeOffset ToUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
            throw new DailyReportException("La fecha coincide con una hora local inexistente.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }
}
