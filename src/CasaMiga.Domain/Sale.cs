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

public sealed record Payment
{
    public Guid Id { get; }
    public string Method => "CASH";
    public Money Applied { get; }
    public Money Received { get; }
    public Money Change { get; }
    public Payment(Guid id, Money applied, Money received)
    {
        if (id == Guid.Empty) throw new ArgumentException("Identidad de pago inválida.");
        Change = applied.ChangeFrom(received);
        Id = id; Applied = applied; Received = received;
    }
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
        long sequence, DateTimeOffset at, IEnumerable<SaleItem> items, Guid paymentId, Money received)
    {
        if (id == Guid.Empty || operationId == Guid.Empty || userId == Guid.Empty || terminalId == Guid.Empty
            || cashSessionId == Guid.Empty || sequence < 1) throw new ArgumentException("Identidad de venta inválida.");
        var copy = items.ToArray();
        if (copy.Length == 0 || copy.Select(i => i.ProductId).Distinct().Count() != copy.Length)
            throw new ArgumentException("La venta debe tener partidas únicas y no estar vacía.");
        var total = new Money(0);
        foreach (var item in copy) total += item.LineTotal;
        Payment = new(paymentId, total, received);
        Id = id; OperationId = operationId; UserId = userId; TerminalId = terminalId;
        CashSessionId = cashSessionId; Sequence = sequence; ConfirmedAt = at.ToUniversalTime();
        Items = Array.AsReadOnly(copy); Subtotal = total;
    }
}
