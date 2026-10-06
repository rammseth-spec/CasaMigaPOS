namespace CasaMiga.Domain;

public sealed record SaleItem
{
    public Guid ProductId { get; }
    public string ProductName { get; }
    public Money UnitPrice { get; }
    public int Quantity { get; }
    public Money LineTotal { get; }
    public SaleItem(Guid productId, string name, Money unitPrice, int quantity)
    {
        var validated = new CartLine(productId, name, unitPrice, quantity);
        ProductId = validated.ProductId; ProductName = validated.ProductName;
        UnitPrice = validated.UnitPrice; Quantity = validated.Quantity; LineTotal = validated.LineTotal;
    }
}

public enum PaymentMethod { Cash, Card, Transfer }

public sealed record Payment
{
    public Guid Id { get; }
    public PaymentMethod Kind { get; }
    public string Method => Code(Kind);
    public string? Reference { get; }
    public Money Applied { get; }
    public Money Received { get; }
    public Money Change { get; }
    public Payment(Guid id, Money applied, Money received, PaymentMethod kind = PaymentMethod.Cash, string? reference = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Identidad de pago inválida.");
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Forma de pago inválida.");
        reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (kind == PaymentMethod.Cash && reference is not null) throw new ArgumentException("El pago en efectivo no lleva referencia.");
        if (reference is { Length: > 40 }) throw new ArgumentException("La referencia admite máximo 40 caracteres.");
        if (kind != PaymentMethod.Cash && received != applied) throw new ArgumentException("Tarjeta y transferencia se cobran por el total exacto.");
        Change = applied.ChangeFrom(received);
        Id = id; Kind = kind; Reference = reference; Applied = applied; Received = received;
    }
    public static string Code(PaymentMethod kind) => kind switch
    {
        PaymentMethod.Cash => "CASH", PaymentMethod.Card => "CARD", PaymentMethod.Transfer => "TRANSFER",
        _ => throw new ArgumentException("Forma de pago inválida.")
    };
    public static PaymentMethod Parse(string code) => code switch
    {
        "CASH" => PaymentMethod.Cash, "CARD" => PaymentMethod.Card, "TRANSFER" => PaymentMethod.Transfer,
        _ => throw new InvalidDataException("Forma de pago desconocida.")
    };
    public static string Label(PaymentMethod kind) => kind switch
    {
        PaymentMethod.Cash => "Efectivo", PaymentMethod.Card => "Tarjeta", PaymentMethod.Transfer => "Transferencia", _ => kind.ToString()
    };
}

// A candidate remains uncommitted until Application successfully commits the whole aggregate.
public sealed class Sale
{
    public Guid Id { get; }
    public Guid OperationId { get; }
    public Guid UserId { get; }
    public Guid TerminalId { get; }
    public Guid CashSessionId { get; }
    public long Sequence { get; }
    public string Folio => $"{TerminalId:N}-{Sequence:D8}";
    public DateTimeOffset ConfirmedAt { get; }
    public IReadOnlyList<SaleItem> Items { get; }
    public Money Subtotal { get; }
    public Money Total => Subtotal;
    public Payment Payment { get; }
    public Sale(Guid id, Guid operationId, Guid userId, Guid terminalId, Guid cashSessionId,
        long sequence, DateTimeOffset at, IEnumerable<SaleItem> items, Guid paymentId, Money received,
        PaymentMethod method = PaymentMethod.Cash, string? reference = null)
    {
        if (id == Guid.Empty || operationId == Guid.Empty || userId == Guid.Empty || terminalId == Guid.Empty
            || cashSessionId == Guid.Empty || sequence < 1) throw new ArgumentException("Identidad de venta inválida.");
        var copy = items.ToArray();
        if (copy.Length == 0 || copy.Select(i => i.ProductId).Distinct().Count() != copy.Length)
            throw new ArgumentException("La venta debe tener partidas únicas y no estar vacía.");
        var total = new Money(0);
        foreach (var item in copy) total += item.LineTotal;
        Payment = new(paymentId, total, received, method, reference);
        Id = id; OperationId = operationId; UserId = userId; TerminalId = terminalId;
        CashSessionId = cashSessionId; Sequence = sequence; ConfirmedAt = at.ToUniversalTime();
        Items = Array.AsReadOnly(copy); Subtotal = total;
    }
}
