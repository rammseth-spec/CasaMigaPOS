using System.Text.Json;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed class CashService(ILocalUnitOfWork database, UserSession identity, IClock clock)
{
    public CashSessionView Open(Guid terminal, Money fund) => database.Run(store =>
    {
        var user = Authorize(store, "cash.open");
        if (store.FindTerminal(terminal) is null) throw new ArgumentException("Terminal no configurada.");
        if (store.FindOpenSession(terminal) is not null) throw new CashAlreadyOpenException();
        var at = clock.UtcNow;
        var opened = CashSession.Open(Guid.NewGuid(), terminal, user.Id, at, fund);
        store.AddCashSession(opened);
        store.AddAudit(new(Guid.NewGuid(), user.Id, terminal, "cash.open", opened.Id, at,
            JsonSerializer.Serialize(new { openingCents = fund.Cents, state = "OPEN" })));
        return new CashSessionView(opened, user.DisplayName);
    });

    public CashSessionView? Current(Guid terminal) => database.Run(store =>
    {
        Authorize(store, "cash.view");
        if (store.FindTerminal(terminal) is null) throw new ArgumentException("Terminal no configurada.");
        return store.FindOpenSession(terminal);
    });

    public bool CanOpen() => database.Run(store =>
        identity.Current is { } current && store.FindUser(current.Id) is { Active: true } && store.HasPermission(current.Id, "cash.open"));

    private LocalUser Authorize(ILocalStore store, string permission)
    {
        if (identity.Current is not { } current || store.FindUser(current.Id) is not { Active: true } user || !store.HasPermission(user.Id, permission))
            throw new AccessDeniedException();
        return user;
    }
}
