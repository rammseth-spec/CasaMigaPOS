using System.Globalization;
using System.Text.Json;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public enum CashMovementKind { Expense, Withdrawal, Deposit }
public enum ExpenseCategory { Supplies, Gas, Cleaning, Transport, Maintenance, Other }

/// <summary>Gasto, retiro o entrada de efectivo en la caja abierta. Inmutable: un error se corrige con el movimiento contrario.</summary>
public sealed record CashMovement(Guid Id, Guid OperationId, Guid CashSessionId, Guid TerminalId, Guid UserId, string UserName,
    CashMovementKind Kind, ExpenseCategory? Category, Money Amount, string Note, DateTimeOffset OccurredAt)
{
    /// <summary>Efecto en el cajón: las entradas suman; gastos y retiros restan.</summary>
    public Money Effect => Kind == CashMovementKind.Deposit ? Amount : new Money(checked(-Amount.Cents));
}

public sealed record CashMovementTotals(Money Expenses, Money Withdrawals, Money Deposits)
{
    public static CashMovementTotals None { get; } = new(new(0), new(0), new(0));
    public Money Net => new(checked(Deposits.Cents - Expenses.Cents - Withdrawals.Cents));
    public bool Any => Expenses.Cents != 0 || Withdrawals.Cents != 0 || Deposits.Cents != 0;
}

public sealed record CashMovementRequest(Guid OperationId, CashMovementKind Kind, ExpenseCategory? Category, Money Amount, string Note);

public sealed class CashMovementException(string message) : Exception(message);

/// <summary>
/// Movimientos de efectivo de la caja abierta de esta terminal (docs/CASH_MOVEMENTS.md).
/// Cajero: gastos y entradas. Retiros: sólo encargado y administrador. Afectan el efectivo esperado del corte.
/// </summary>
public sealed class CashMovementService(ILocalUnitOfWork database, UserSession identity, IClock clock, Guid terminalId)
{
    public const string RecordPermission = "cash.movement";
    public const string WithdrawalPermission = "cash.withdrawal";
    public const int MaxNote = 200;
    private static readonly CultureInfo Mexico = CultureInfo.GetCultureInfo("es-MX");

    public static string Label(CashMovementKind kind) => kind switch
    {
        CashMovementKind.Expense => "Gasto",
        CashMovementKind.Withdrawal => "Retiro",
        CashMovementKind.Deposit => "Entrada",
        _ => kind.ToString(),
    };

    public static string Label(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Supplies => "Insumos",
        ExpenseCategory.Gas => "Gas",
        ExpenseCategory.Cleaning => "Limpieza",
        ExpenseCategory.Transport => "Transporte",
        ExpenseCategory.Maintenance => "Mantenimiento",
        ExpenseCategory.Other => "Otro",
        _ => category.ToString(),
    };

    public static string Code(CashMovementKind kind) => kind switch
    {
        CashMovementKind.Expense => "EXPENSE", CashMovementKind.Withdrawal => "WITHDRAWAL", CashMovementKind.Deposit => "DEPOSIT",
        _ => throw new ArgumentException("Movimiento inválido."),
    };

    public static CashMovementKind ParseKind(string code) => code switch
    {
        "EXPENSE" => CashMovementKind.Expense, "WITHDRAWAL" => CashMovementKind.Withdrawal, "DEPOSIT" => CashMovementKind.Deposit,
        _ => throw new InvalidDataException("Movimiento de caja desconocido."),
    };

    public static string Code(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Supplies => "SUPPLIES", ExpenseCategory.Gas => "GAS", ExpenseCategory.Cleaning => "CLEANING",
        ExpenseCategory.Transport => "TRANSPORT", ExpenseCategory.Maintenance => "MAINTENANCE", ExpenseCategory.Other => "OTHER",
        _ => throw new ArgumentException("Categoría inválida."),
    };

    public static ExpenseCategory ParseCategory(string code) => code switch
    {
        "SUPPLIES" => ExpenseCategory.Supplies, "GAS" => ExpenseCategory.Gas, "CLEANING" => ExpenseCategory.Cleaning,
        "TRANSPORT" => ExpenseCategory.Transport, "MAINTENANCE" => ExpenseCategory.Maintenance, "OTHER" => ExpenseCategory.Other,
        _ => throw new InvalidDataException("Categoría de gasto desconocida."),
    };

    private T Run<T>(Func<ILocalStore, T> operation) => identity.Synchronized(() => database.Run(operation));

    public bool CanRecord() => Run(store => Can(store, RecordPermission));
    public bool CanWithdraw() => Run(store => Can(store, RecordPermission) && Can(store, WithdrawalPermission));

    /// <summary>Movimientos de la caja abierta de esta terminal, del más reciente al más antiguo. Vacío si no hay caja abierta.</summary>
    public IReadOnlyList<CashMovement> Current() => Run(store =>
    {
        Authorize(store, RecordPermission);
        return store.FindOpenSession(terminalId) is { } open ? store.ListCashMovements(open.Session.Id) : [];
    });

    /// <summary>Con el conteo ciego ya confirmado, el cajón queda congelado (también tras reiniciar la app).</summary>
    public bool IsCounted() => Run(store =>
    {
        Authorize(store, RecordPermission);
        return store.FindOpenSession(terminalId) is { } open && store.FindCount(open.Session.Id) is not null;
    });

    public CashMovement Record(CashMovementRequest request) => Run(store =>
    {
        var actor = Authorize(store, RecordPermission);
        // Reintento de la misma operación (resultado incierto): devuelve lo ya registrado, sin duplicar.
        if (store.FindCashMovementByOperation(request.OperationId) is { } done) return done;
        if (request.Kind == CashMovementKind.Withdrawal && !store.HasPermission(actor.Id, WithdrawalPermission))
            throw new CashMovementException("Sólo el encargado o el administrador registran retiros.");
        if (request.OperationId == Guid.Empty) throw new ArgumentException("Operación inválida.");
        if (request.Amount.Cents <= 0) throw new ArgumentException("Escribe un importe mayor a cero.");
        if (request.Kind == CashMovementKind.Expense && request.Category is null) throw new ArgumentException("Elige la categoría del gasto.");
        if (request.Kind != CashMovementKind.Expense && request.Category is not null) throw new ArgumentException("Sólo los gastos llevan categoría.");
        var note = (request.Note ?? "").Trim();
        if (note.Length > MaxNote) throw new ArgumentException($"La nota admite hasta {MaxNote} caracteres.");
        if (note.Any(char.IsControl)) throw new ArgumentException("La nota no admite saltos de línea ni caracteres de control.");
        if (request.Category == ExpenseCategory.Other && note.Length < 3) throw new ArgumentException("Explica el gasto en la nota cuando la categoría es «Otro».");

        var open = store.FindOpenSession(terminalId) ?? throw new CashMovementException("Abre la caja antes de registrar movimientos de efectivo.");
        var session = open.Session;
        if (store.FindCount(session.Id) is not null)
            throw new CashMovementException("La caja ya tiene su conteo de cierre; los movimientos se registran antes de contar.");
        if (request.Kind != CashMovementKind.Deposit)
        {
            var available = session.OpeningFund + store.CashSalesTotal(session.Id) - store.CashRefundsTotal(session.Id) + store.CashMovementTotals(session.Id).Net;
            if (available.Cents < request.Amount.Cents)
                throw new CashMovementException($"No hay suficiente efectivo esperado en caja para sacar {Format(request.Amount)}.");
        }

        var movement = new CashMovement(Guid.NewGuid(), request.OperationId, session.Id, terminalId, actor.Id, actor.DisplayName,
            request.Kind, request.Category, request.Amount, note, clock.UtcNow);
        store.AddCashMovement(movement);
        store.AddAudit(new(Guid.NewGuid(), actor.Id, terminalId, "cash." + request.Kind.ToString().ToLowerInvariant(), movement.Id, movement.OccurredAt,
            JsonSerializer.Serialize(new
            {
                operationId = movement.OperationId, cashSessionId = session.Id, kind = Code(movement.Kind),
                category = movement.Category is { } c ? Code(c) : null, amountCents = movement.Amount.Cents, note = movement.Note,
            })));
        return movement;
    });

    private bool Can(ILocalStore store, string permission) =>
        identity.Current is { } current && store.FindUser(current.Id) is { Active: true } && store.HasPermission(current.Id, permission);

    private LocalUser Authorize(ILocalStore store, string permission)
    {
        if (identity.Current is not { } current || store.FindUser(current.Id) is not { Active: true } user || !store.HasPermission(user.Id, permission))
            throw new AccessDeniedException();
        return user;
    }

    private static string Format(Money money) => (money.Cents / 100m).ToString("C2", Mexico);
}
