using CasaMiga.Domain;
namespace CasaMiga.Application;

public sealed record CashCount(Guid SessionId, Guid CountedBy, DateTimeOffset CountedAt, Money Counted);
public sealed record ClosingResult(CashSession Session, CashCount Count, Money Expected, Money Difference);
public sealed class CashClosingException(string message) : Exception(message);

public sealed class CashClosingService(ILocalUnitOfWork database, UserSession identity, IClock clock)
{
    // Future cash movements are summed here, in the same transaction. No sales/movements exist in 1B.
    private static Money Expected(CashSession session) => session.OpeningFund;
    private LocalUser Authorize(ILocalStore store, CashSession session)
    {
        identity.RequireEmptyCart();
        if (identity.Current is not { } actor || store.FindUser(actor.Id) is not { Active: true } user
            || !store.HasPermission(actor.Id,"cash.close")) throw new AccessDeniedException();
        if (session.OpenedBy != actor.Id && !store.HasPermission(actor.Id,"cash.close.other"))
            throw new CashClosingException("Esta caja pertenece a otro usuario. Necesitas permiso para cerrar cajas ajenas.");
        return user;
    }
    private static CashSession Get(ILocalStore store, Guid id) => store.FindSession(id)
        ?? throw new CashClosingException("No se encontró la sesión de caja.");
    private static void RequireOpen(CashSession session)
    {
        if (session.State != CashState.Open) throw new CashClosingException("Esta sesión ya está cerrada.");
    }
    private static ClosingResult Result(CashSession session, CashCount count)
    {
        var expected=Expected(session);
        return new(session,count,expected,new Money(checked(count.Counted.Cents-expected.Cents)));
    }
    private T Run<T>(Func<ILocalStore,T> operation) => identity.Synchronized(() => database.Run(operation));
    public ClosingResult? Resume(Guid id) => Run<ClosingResult?>(store =>
    {
        var session=Get(store,id); Authorize(store,session); RequireOpen(session);
        return store.FindCount(id) is { } count ? Result(session,count) : null;
    });
    public ClosingResult SubmitCount(Guid id, Money counted) => Run(store =>
    {
        var session=Get(store,id); var actor=Authorize(store,session); RequireOpen(session);
        if(counted.Cents<0) throw new ArgumentOutOfRangeException(nameof(counted));
        if(store.FindCount(id) is not null) throw new CashClosingException("El conteo ya fue confirmado; puedes retomar el resultado, pero no modificarlo.");
        var count=new CashCount(id,actor.Id,clock.UtcNow,counted);
        store.AddCount(count);
        store.AddAudit(new(Guid.NewGuid(),actor.Id,session.TerminalId,"cash.count",id,count.CountedAt,
            System.Text.Json.JsonSerializer.Serialize(new {countedCents=counted.Cents})));
        return Result(session,count);
    });
    public CashSession Close(Guid id) => Run(store =>
    {
        var session=Get(store,id); var actor=Authorize(store,session); RequireOpen(session);
        var count=store.FindCount(id) ?? throw new CashClosingException("Confirma primero el conteo ciego.");
        var closed=session.Close(actor.Id,clock.UtcNow,Expected(session),count.Counted);
        store.CloseSession(closed);
        store.AddAudit(new(Guid.NewGuid(),actor.Id,session.TerminalId,"cash.close",id,closed.ClosedAt!.Value,
            System.Text.Json.JsonSerializer.Serialize(new {openedBy=session.OpenedBy,closedBy=actor.Id,
                closedByOtherUser=closed.ClosedByOtherUser,countedBy=count.CountedBy,countedAt=count.CountedAt,
                openingCents=session.OpeningFund.Cents,expectedCents=closed.Expected!.Value.Cents,
                countedCents=count.Counted.Cents,differenceCents=closed.Difference!.Value.Cents})));
        return closed;
    });
}
