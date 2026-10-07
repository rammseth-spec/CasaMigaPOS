using CasaMiga.Domain;

namespace CasaMiga.Application;

// Charolas a consignación (docs/CONSIGNMENT.md).
public sealed record ConsignmentStore(Guid Id, string Name, string Contact, string Phone, string Address, bool Active);
public sealed record ConsignmentPrice(Guid ProductId, string ProductName, string CategoryName, Money RetailPrice, Money? Price, string? Sku = null);
public sealed record ConsignmentLine(Guid ProductId, int Quantity);
public sealed record ConsignmentTemplateLine(Guid ProductId, string ProductName, int Quantity);

public sealed record ConsignmentDeliveryItem(int Position, Guid ProductId, string ProductName, Money UnitPrice, Money RetailPrice, int Quantity)
{
    public Money Value => UnitPrice.Multiply(Quantity);
}
public sealed record ConsignmentSettlementItem(int Position, int Sold, int Returned, Money LineTotal);
public sealed record ConsignmentSettlement(Guid DeliveryId, Guid OperationId, Guid CollectedBy, DateTimeOffset CollectedAt, Money SoldTotal, string Note,
    IReadOnlyList<ConsignmentSettlementItem> Items)
{
    public int Sold => Items.Sum(i => i.Sold);
    public int Returned => Items.Sum(i => i.Returned);
}
public sealed record ConsignmentVoid(Guid DeliveryId, Guid OperationId, Guid VoidedBy, DateTimeOffset VoidedAt, string Reason);
public enum ConsignmentState { AtStore, Collected, Voided }
public sealed record ConsignmentDelivery(Guid Id, Guid OperationId, Guid StoreId, string StoreName, Guid TerminalId, Guid DeliveredBy, string DeliveredByName,
    DateTimeOffset DeliveredAt, string Note, IReadOnlyList<ConsignmentDeliveryItem> Items, ConsignmentSettlement? Settlement = null, ConsignmentVoid? Void = null)
{
    public int Pieces => Items.Sum(i => i.Quantity);
    public Money Value => new(Items.Sum(i => i.Value.Cents));
    public ConsignmentState State => Void is not null ? ConsignmentState.Voided : Settlement is not null ? ConsignmentState.Collected : ConsignmentState.AtStore;
}
public sealed record ConsignmentPayment(Guid Id, Guid OperationId, Guid StoreId, Guid ReceivedBy, DateTimeOffset ReceivedAt, Money Amount,
    PaymentMethod Method, string? Reference, string Note);

/// <summary>Una tienda en la ruta del día: charolas que siguen en la tienda y lo que debe.</summary>
public sealed record ConsignmentStoreStatus(ConsignmentStore Store, IReadOnlyList<ConsignmentDelivery> AtStore, Money Balance, DateTimeOffset? LastDelivery, int TemplatePieces);
/// <summary>Resultado de recoger una charola: la recolección y, si se cobró en el momento, el pago.</summary>
public sealed record ConsignmentCollection(ConsignmentDelivery Delivery, ConsignmentPayment? Payment, Money Balance);

public sealed record ConsignmentMovement(DateTimeOffset At, string Concept, Money Charge, Money Payment);
public sealed record ConsignmentAccount(ConsignmentStore Store, IReadOnlyList<ConsignmentMovement> Movements, Money Balance)
{
    public Money Charged => new(Movements.Sum(m => m.Charge.Cents));
    public Money Paid => new(Movements.Sum(m => m.Payment.Cents));
}

public sealed record ConsignmentStoreRow(Guid StoreId, string StoreName, int Trays, int Delivered, int Collected, int Sold, int Returned, int AtStore,
    Money Charged, Money RetailValue, Money Paid, Money Balance)
{
    /// <summary>Porcentaje vendido de las charolas ya recogidas.</summary>
    public decimal? SellThrough => Sold + Returned == 0 ? null : Math.Round(Sold * 100m / (Sold + Returned), 1);
    /// <summary>Lo que esas piezas habrían dejado en tu mostrador por encima del precio de charola.</summary>
    public Money LeftToStores => RetailValue - Charged;
}
public sealed record ConsignmentProductRow(Guid ProductId, string ProductName, int Delivered, int Sold, int Returned, Money Charged, Money RetailValue)
{
    public decimal? SellThrough => Sold + Returned == 0 ? null : Math.Round(Sold * 100m / (Sold + Returned), 1);
}
public sealed record ConsignmentReport(DateOnly From, DateOnly Through, IReadOnlyList<ConsignmentStoreRow> Stores, IReadOnlyList<ConsignmentProductRow> Products)
{
    public int Trays => Stores.Sum(s => s.Trays);
    public int Delivered => Stores.Sum(s => s.Delivered);
    public int Sold => Stores.Sum(s => s.Sold);
    public int Returned => Stores.Sum(s => s.Returned);
    public int AtStore => Stores.Sum(s => s.AtStore);
    public Money Charged => new(Stores.Sum(s => s.Charged.Cents));
    public Money RetailValue => new(Stores.Sum(s => s.RetailValue.Cents));
    public Money Paid => new(Stores.Sum(s => s.Paid.Cents));
    public Money Balance => new(Stores.Sum(s => s.Balance.Cents));
    public Money LeftToStores => RetailValue - Charged;
    public decimal? SellThrough => Sold + Returned == 0 ? null : Math.Round(Sold * 100m / (Sold + Returned), 1);
}

public sealed class ConsignmentException(string message) : Exception(message);
