using System.Globalization;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public enum SalesHistoryMode { Recent, Folio, DateRange, CashSession }

public sealed record SalesHistoryQuery(
    SalesHistoryMode Mode = SalesHistoryMode.Recent,
    string Folio = "",
    DateOnly? From = null,
    DateOnly? Through = null,
    int Page = 0,
    Guid? CashSessionId = null);

public sealed record SalesHistoryFilter(
    Guid? TerminalId,
    long? Sequence,
    DateTimeOffset? FromUtc,
    DateTimeOffset? UntilUtc,
    int Page,
    Guid? CashSessionId = null);

public sealed record SalesHistoryRow(
    Guid SaleId,
    string Folio,
    long Sequence,
    DateTimeOffset ConfirmedAt,
    Money Total,
    string PaymentMethod,
    Guid UserId,
    string CashierName,
    Guid TerminalId,
    string TerminalName,
    Guid CashSessionId);

public sealed record SalesHistoryPage(
    IReadOnlyList<SalesHistoryRow> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record SalesHistoryDetail(Sale Sale, string CashierName, string TerminalName);

public sealed class SalesHistoryException(string message) : Exception(message);

public sealed class SalesHistoryService(
    ILocalReadUnitOfWork database,
    UserSession identity,
    TimeZoneInfo timeZone)
{
    private Guid Authorize(ILocalStore store)
    {
        if (identity.Current is not { } actor
            || store.FindUser(actor.Id) is not { Active: true }
            || !store.HasPermission(actor.Id, "sales.view"))
            throw new AccessDeniedException();
        return actor.Id;
    }

    public bool CanView() => database.Read(store =>
        identity.Current is { } actor
        && store.FindUser(actor.Id) is { Active: true }
        && store.HasPermission(actor.Id, "sales.view"));

    public SalesHistoryPage Search(SalesHistoryQuery query) => database.Read(store =>
    {
        Authorize(store);
        if (query.Page < 0 || query.Page > 100000)
            throw new SalesHistoryException("La página solicitada no es válida.");

        var filter = query.Mode switch
        {
            SalesHistoryMode.Recent when string.IsNullOrWhiteSpace(query.Folio)
                && query.From is null && query.Through is null && query.CashSessionId is null
                => new SalesHistoryFilter(null, null, null, null, query.Page),
            SalesHistoryMode.Folio when query.From is null && query.Through is null && query.CashSessionId is null
                => FolioFilter(query.Folio, query.Page),
            SalesHistoryMode.DateRange when string.IsNullOrWhiteSpace(query.Folio) && query.CashSessionId is null
                => DateFilter(query.From, query.Through, query.Page),
            SalesHistoryMode.CashSession when string.IsNullOrWhiteSpace(query.Folio)
                && query.From is null && query.Through is null && query.CashSessionId is { } session && session != Guid.Empty
                => new SalesHistoryFilter(null, null, null, null, query.Page, session),
            _ => throw new SalesHistoryException("Usa la búsqueda por folio o por fechas, no ambas.")
        };
        return store.QuerySales(filter);
    });

    public SalesHistoryDetail? Detail(Guid saleId) => database.Read(store =>
    {
        Authorize(store);
        if (saleId == Guid.Empty) throw new SalesHistoryException("Selecciona una venta válida.");
        return store.FindSaleDetail(saleId);
    });

    private static SalesHistoryFilter FolioFilter(string value, int page)
    {
        var folio = value.Trim();
        if (folio.Length < 41 || folio[32] != '-'
            || !Guid.TryParseExact(folio[..32], "N", out var terminal)
            || !long.TryParse(folio[33..], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            || sequence < 1
            || !string.Equals(folio, $"{terminal:N}-{sequence:D8}", StringComparison.OrdinalIgnoreCase))
            throw new SalesHistoryException("El folio no tiene un formato válido.");
        return new(terminal, sequence, null, null, page);
    }

    private SalesHistoryFilter DateFilter(DateOnly? from, DateOnly? through, int page)
    {
        if (from is null && through is null)
            throw new SalesHistoryException("Indica al menos una fecha.");
        if (from is { } first && through is { } last && first > last)
            throw new SalesHistoryException("La fecha Desde no puede ser posterior a Hasta.");
        DateTimeOffset? fromUtc = from is { } start ? ToUtc(start) : null;
        DateTimeOffset? untilUtc = through is { } end && end < DateOnly.MaxValue ? ToUtc(end.AddDays(1)) : null;
        if (through == DateOnly.MaxValue)
            throw new SalesHistoryException("La fecha Hasta excede el límite permitido.");
        return new(null, null, fromUtc, untilUtc, page);
    }

    private DateTimeOffset ToUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
            throw new SalesHistoryException("La fecha coincide con una hora local inexistente.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }
}
