namespace CasaMiga.Domain;

public sealed record CartLine
{
    public Guid ProductId { get; }
    public string ProductName { get; }
    public Money UnitPrice { get; }
    public int Quantity { get; }
    public Money LineTotal => UnitPrice.Multiply(Quantity);
    public CartLine(Guid productId, string productName, Money unitPrice, int quantity)
    {
        if (productId == Guid.Empty || string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Producto inválido.");
        if (unitPrice.Cents < 0 || quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
        _ = unitPrice.Multiply(quantity);
        ProductId = productId; ProductName = productName; UnitPrice = unitPrice; Quantity = quantity;
    }
    public CartLine WithQuantity(int quantity) => new(ProductId, ProductName, UnitPrice, quantity);
}

/// <summary>Cart is transient and not a sale. Immutable snapshots; no persistence.</summary>
public sealed class Cart
{
    public Guid UserId { get; }
    public Guid TerminalId { get; }
    public Guid CashSessionId { get; }
    public IReadOnlyList<CartLine> Lines { get; }
    public Money Subtotal { get; }
    public Money PreliminaryTotal => Subtotal;
    public bool IsEmpty => Lines.Count == 0;
    public Cart(Guid userId, Guid terminalId, Guid cashSessionId) : this(userId, terminalId, cashSessionId, []) { }
    private Cart(Guid userId, Guid terminalId, Guid cashSessionId, IEnumerable<CartLine> lines)
    {
        if (userId == Guid.Empty || terminalId == Guid.Empty || cashSessionId == Guid.Empty) throw new ArgumentException("Contexto de carrito inválido.");
        UserId = userId; TerminalId = terminalId; CashSessionId = cashSessionId;
        var copy = lines.ToArray(); var total = new Money(0);
        foreach (var line in copy) total += line.LineTotal;
        Lines = Array.AsReadOnly(copy); Subtotal = total;
    }
    private Cart Replace(IEnumerable<CartLine> lines) => new(UserId, TerminalId, CashSessionId, lines);
    public Cart Add(Guid id, string name, Money price)
    {
        if (Lines.Any(l => l.ProductId == id)) return Increment(id);
        return Replace(Lines.Append(new CartLine(id, name, price, 1)));
    }
    private CartLine Find(Guid id) => Lines.SingleOrDefault(l => l.ProductId == id) ?? throw new ArgumentException("Partida inexistente.");
    public Cart Increment(Guid id) => SetQuantity(id, checked(Find(id).Quantity + 1));
    public Cart Decrement(Guid id)
    {
        var line = Find(id);
        if (line.Quantity == 1) throw new ArgumentException("La cantidad mínima es 1. Usa Eliminar para quitar la partida.");
        return SetQuantity(id, line.Quantity - 1);
    }
    public Cart SetQuantity(Guid id, int quantity)
    {
        var changed = Find(id).WithQuantity(quantity);
        return Replace(Lines.Select(l => l.ProductId == id ? changed : l));
    }
    public Cart Remove(Guid id) { _ = Find(id); return Replace(Lines.Where(l => l.ProductId != id)); }
    public Cart Clear() => Replace([]);
}
