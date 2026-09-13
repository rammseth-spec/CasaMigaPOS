using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Presentation;
public static class CheckoutInventoryMessages
{
    public static string Format(CheckoutInventoryException error,string? productName)
    {
        var product=productName??"el producto";
        string Quantity(long value,InventoryDimension dimension)=>dimension==InventoryDimension.Count?$"{value} {(value==1?"pieza":"piezas")}":InventoryDisplay.Format(value,dimension);
        return error.Error switch
        {
            CheckoutInventoryError.InsufficientInventory when error.RequiredAtomic is {} required && error.AvailableAtomic is {} available && error.Dimension is {} dimension
                =>$"No hay suficiente existencia de {product}.\nRequerido: {Quantity(required,dimension)}\nDisponible: {Quantity(available,dimension)}\nLa venta no fue registrada. El carrito permanece intacto.",
            CheckoutInventoryError.InventoryNotInitialized=>$"No se puede completar la venta porque el inventario de {product} aún no tiene saldo inicial. Solicita corrección administrativa.",
            CheckoutInventoryError.InventoryTrackingConfigurationInvalid=>$"No se puede completar la venta porque {product} tiene una configuración de inventario incompleta. Solicita corrección administrativa.",
            CheckoutInventoryError.InventoryItemInactive=>$"No se puede completar la venta porque el artículo de inventario de {product} está inactivo. Solicita revisión administrativa.",
            CheckoutInventoryError.InventoryConcurrencyConflict=>"Las existencias cambiaron o están siendo utilizadas. Revisa y vuelve a intentar. El carrito permanece intacto.",
            CheckoutInventoryError.InventoryConsumptionOverflow=>"No se pudo calcular correctamente el consumo de inventario. La venta no fue registrada.",
            _=>error.Message
        };
    }
}
