using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record CashMovementKindOption(CashMovementKind Kind, string Label, bool Selected);
public sealed record ExpenseCategoryOption(ExpenseCategory Category, string Label, bool Selected);

public sealed record CashMovementRow(CashMovement Movement)
{
    public string Time => Movement.OccurredAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    public string Kind => CashMovementService.Label(Movement.Kind);
    public string Detail => string.Join(" · ", new[] { Movement.Category is { } c ? CashMovementService.Label(c) : null, Movement.Note.Length > 0 ? Movement.Note : null, Movement.UserName }
        .Where(part => part is not null));
    public bool IsIncome => Movement.Kind == CashMovementKind.Deposit;
    public string Amount => (IsIncome ? "+" : "−") + CashMovementsViewModel.Format(Movement.Amount);
}

/// <summary>Gastos, entradas y retiros de la caja abierta (pantalla Caja). Los importes del cajón siguen ocultos hasta el conteo ciego.</summary>
public sealed class CashMovementsViewModel(CashMovementService service, Action<string, string?> log) : INotifyPropertyChanged
{
    private static readonly CultureInfo Mexico = CultureInfo.GetCultureInfo("es-MX");
    internal static string Format(Money money) => (money.Cents / 100m).ToString("C2", Mexico);

    public bool Busy { get; private set; }
    public bool Ready => !Busy;
    public string Message { get; private set; } = "";
    public bool MessageIsSuccess { get; private set; }
    public bool CanRecord { get; private set; }
    public bool CanWithdraw { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public CashMovementKind Kind { get; private set; } = CashMovementKind.Expense;
    public ExpenseCategory? Category { get; private set; }
    public bool IsExpense => Kind == CashMovementKind.Expense;
    public IReadOnlyList<CashMovementKindOption> Kinds =>
        new[] { CashMovementKind.Expense, CashMovementKind.Deposit, CashMovementKind.Withdrawal }
            .Where(k => k != CashMovementKind.Withdrawal || CanWithdraw)
            .Select(k => new CashMovementKindOption(k, CashMovementService.Label(k), k == Kind)).ToArray();
    public IReadOnlyList<ExpenseCategoryOption> Categories => Enum.GetValues<ExpenseCategory>()
        .Select(c => new ExpenseCategoryOption(c, CashMovementService.Label(c), c == Category)).ToArray();
    public string Hint => Kind switch
    {
        CashMovementKind.Expense => "Efectivo que sale para pagar algo del negocio.",
        CashMovementKind.Deposit => "Efectivo que entra y no es venta: cambio que se trae o fondo extra.",
        _ => "Efectivo que sale a resguardo: depósito al banco o entrega al dueño. No es gasto.",
    };
    public string ActionText => Kind switch
    {
        CashMovementKind.Expense => "Registrar gasto",
        CashMovementKind.Deposit => "Registrar entrada",
        _ => "Registrar retiro",
    };
    public string NotePlaceholder => Category == ExpenseCategory.Other ? "Nota (obligatoria con «Otro»)" : "Nota (opcional)";
    public string AmountText { get; set; } = "";
    public string Note { get; set; } = "";

    public IReadOnlyList<CashMovementRow> Movements { get; private set; } = [];
    public bool HasMovements => Movements.Count > 0;
    public string TotalsText
    {
        get
        {
            Money Sum(CashMovementKind kind) => new(Movements.Where(m => m.Movement.Kind == kind).Sum(m => m.Movement.Amount.Cents));
            return $"Gastos {Format(Sum(CashMovementKind.Expense))} · Retiros {Format(Sum(CashMovementKind.Withdrawal))} · Entradas {Format(Sum(CashMovementKind.Deposit))}";
        }
    }

    // La misma operación se reintenta con el mismo identificador hasta que se registra: un resultado incierto no duplica.
    private Guid operation = Guid.NewGuid();

    public void SelectKind(CashMovementKind kind)
    {
        Kind = kind; Category = kind == CashMovementKind.Expense ? Category : null; Message = ""; operation = Guid.NewGuid(); Notify();
    }

    public void SelectCategory(ExpenseCategory category) { Category = category; Message = ""; Notify(); }

    public Task LoadAsync() => RunAsync(async () =>
    {
        (CanRecord, CanWithdraw) = await Task.Run(() => (service.CanRecord(), service.CanWithdraw()));
        if (!CanWithdraw && Kind == CashMovementKind.Withdrawal) Kind = CashMovementKind.Expense;
        Movements = CanRecord ? (await Task.Run(service.Current)).Select(m => new CashMovementRow(m)).ToArray() : [];
    });

    public Task RecordAsync() => RunAsync(async () =>
    {
        if (!ConsignmentViewModel.TryParseMoney(AmountText, out var amount) || amount is not { Cents: > 0 } money)
            throw new ArgumentException("Escribe el importe, por ejemplo 150.00.");
        var request = new CashMovementRequest(operation, Kind, Kind == CashMovementKind.Expense ? Category : null, money, Note);
        var saved = await Task.Run(() => service.Record(request));
        operation = Guid.NewGuid(); AmountText = ""; Note = ""; Category = null;
        Movements = (await Task.Run(service.Current)).Select(m => new CashMovementRow(m)).ToArray();
        Message = $"{CashMovementService.Label(saved.Kind)} de {Format(saved.Amount)} registrado.";
        MessageIsSuccess = true;
        log("cash.movement.recorded", null);
    });

    public void Forget() { Movements = []; AmountText = ""; Note = ""; Category = null; Kind = CashMovementKind.Expense; Message = ""; Notify(); }

    private async Task RunAsync(Func<Task> work)
    {
        if (Busy) return;
        Busy = true; Message = ""; MessageIsSuccess = false; Notify();
        try { await work(); }
        catch (AccessDeniedException error) { Message = error.Message; log("authorization.denied", null); }
        catch (Exception error) when (error is CashMovementException or ArgumentException) { Message = error.Message; }
        catch (Exception error) { Message = "No se registró el movimiento. Intenta de nuevo; no se duplica."; log("cash.movement.failed", error.GetType().Name); }
        finally { Busy = false; Notify(); }
    }
}
