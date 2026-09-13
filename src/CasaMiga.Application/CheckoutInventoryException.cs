using CasaMiga.Domain;
namespace CasaMiga.Application;

public enum CheckoutInventoryError
{
    InventoryTrackingConfigurationInvalid, InventoryNotInitialized, InsufficientInventory,
    InventoryItemInactive, InventoryConsumptionOverflow, InventoryConcurrencyConflict, InventoryRecoveryInconsistent
}
public sealed class CheckoutInventoryException : CheckoutException
{
    public CheckoutInventoryError Error { get; }
    public Guid? ProductId { get; }
    public Guid? InventoryItemId { get; }
    public long? RequiredAtomic { get; }
    public long? AvailableAtomic { get; }
    public InventoryDimension? Dimension { get; }
    public CheckoutInventoryException(CheckoutInventoryError error, Guid? productId=null,
        Guid? itemId=null, long? required=null, long? available=null, InventoryDimension? dimension=null)
        : base(error switch
        {
            CheckoutInventoryError.InsufficientInventory => "No hay existencia suficiente para completar la venta.",
            CheckoutInventoryError.InventoryNotInitialized => "El artículo no tiene saldo inicial de inventario.",
            CheckoutInventoryError.InventoryItemInactive => "Un artículo de inventario está inactivo.",
            CheckoutInventoryError.InventoryConcurrencyConflict => "El inventario está ocupado o cambió. Intenta nuevamente.",
            CheckoutInventoryError.InventoryRecoveryInconsistent => "El cobro guardado tiene una inconsistencia. No vuelvas a cobrar; solicita revisión.",
            CheckoutInventoryError.InventoryConsumptionOverflow => "La cantidad de consumo excede el rango permitido.",
            _ => "La configuración de inventario de un producto requiere revisión."
        })
    { Error=error;ProductId=productId;InventoryItemId=itemId;RequiredAtomic=required;AvailableAtomic=available;Dimension=dimension; }
}
