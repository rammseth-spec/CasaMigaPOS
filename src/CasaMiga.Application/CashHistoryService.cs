using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed record CashClosureHistoryQuery(int Page = 0);
public sealed record CashClosureHistoryRow(Guid Id, string TerminalName, DateTimeOffset OpenedAt,
    string OpenedByName, Money OpeningFund, DateTimeOffset ClosedAt, string ClosedByName,
    string CountedByName, Money? Expected, Money? Counted, Money? Difference, string? DiscrepancyNote);
public sealed record CashClosureHistoryPage(IReadOnlyList<CashClosureHistoryRow> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)(((long)Total + PageSize - 1) / PageSize));
}

public sealed class CashHistoryService(ILocalReadUnitOfWork database, UserSession identity, Guid terminal)
{
    private void Authorize(ILocalStore store)
    {
        if (identity.Current is not { } actor || store.FindUser(actor.Id) is not { Active: true }
            || !store.HasPermission(actor.Id, "cash.view")) throw new AccessDeniedException();
        if (store.FindTerminal(terminal) is null) throw new ArgumentException("Caja no configurada.");
    }
    public CashClosureHistoryPage Search(CashClosureHistoryQuery query) => database.Read(store =>
    {
        Authorize(store);
        if (query.Page is < 0 or > 100000) throw new ArgumentException("La página solicitada no es válida.");
        return store.QueryCashClosures(terminal, query.Page);
    });
    public CashClosureHistoryRow? Detail(Guid id) => database.Read(store =>
    {
        Authorize(store);
        if (id == Guid.Empty) throw new ArgumentException("Selecciona un cierre.");
        return store.FindCashClosure(terminal, id);
    });
}
