namespace CasaMiga.Domain;

public enum CashState { Open, Closed }

public sealed class CashSession
{
    public Guid Id { get; }
    public Guid TerminalId { get; }
    public Guid OpenedBy { get; }
    public DateTimeOffset OpenedAt { get; }
    public Money OpeningFund { get; }
    public CashState State { get; }
    public DateTimeOffset? ClosedAt { get; }
    public Guid? ClosedBy { get; }
    public Money? Expected { get; }
    public Money? Counted { get; }
    public Money? Difference { get; }

    private CashSession(Guid id, Guid terminal, Guid user, DateTimeOffset at, Money fund,
        CashState state, DateTimeOffset? closedAt, Guid? closedBy, Money? expected, Money? counted, Money? difference)
    {
        if (id == Guid.Empty || terminal == Guid.Empty || user == Guid.Empty) throw new ArgumentException("Identidad inválida.");
        if (fund.Cents < 0) throw new ArgumentOutOfRangeException(nameof(fund), "El fondo no puede ser negativo.");
        if (state == CashState.Open && (closedAt is not null || closedBy is not null)) throw new ArgumentException("Estado incoherente.");
        if (state == CashState.Closed && (closedAt is null || closedBy is null)) throw new ArgumentException("Faltan datos de cierre.");
        Id = id; TerminalId = terminal; OpenedBy = user; OpenedAt = at.ToUniversalTime(); OpeningFund = fund;
        State = state; ClosedAt = closedAt; ClosedBy = closedBy; Expected = expected; Counted = counted; Difference = difference;
    }

    public static CashSession Open(Guid id, Guid terminal, Guid user, DateTimeOffset at, Money fund) =>
        new(id, terminal, user, at, fund, CashState.Open, null, null, null, null, null);

    public bool ClosedByOtherUser => ClosedBy is { } closer && closer != OpenedBy;
    public CashSession Close(Guid actor, DateTimeOffset at, Money expected, Money counted)
    {
        if(State != CashState.Open) throw new InvalidOperationException("La sesión ya está cerrada.");
        if(actor == Guid.Empty || at < OpenedAt) throw new ArgumentException("Datos de cierre inválidos.");
        if(counted.Cents < 0 || expected.Cents < 0) throw new ArgumentOutOfRangeException(nameof(counted));
        return new(Id,TerminalId,OpenedBy,OpenedAt,OpeningFund,CashState.Closed,at.ToUniversalTime(),actor,
            expected,counted,new Money(checked(counted.Cents-expected.Cents)));
    }

    // Rehydration has no public setters and there is deliberately no Reopen operation.
    public static CashSession Restore(Guid id, Guid terminal, Guid user, DateTimeOffset at, Money fund,
        CashState state, DateTimeOffset? closedAt, Guid? closedBy, Money? expected, Money? counted, Money? difference) =>
        new(id, terminal, user, at, fund, state, closedAt, closedBy, expected, counted, difference);
}
