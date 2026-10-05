using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

/// <summary>
/// Informational projection of the loaded detail. The service revalidates the operation
/// against current storage when it is submitted; this projection never writes inventory.
/// </summary>
public sealed record InventoryOperationPreview(InventoryDimension Dimension, long BeforeAtomic,
    long DeltaAtomic, long AfterAtomic, InventoryCapture Capture)
{
    public static InventoryOperationPreview Calculate(InventoryItemDetail detail, string operation,
        decimal quantity, string unitCode)
    {
        ArgumentNullException.ThrowIfNull(detail);
        if (operation is not ("opening" or "receipt" or "waste" or "count"))
            throw new ArgumentException("Selecciona una operación de inventario válida.", nameof(operation));
        if (!detail.Item.IsActive)
            throw Error(InventoryError.InventoryItemInactive, "El artículo está inactivo.");
        if (!detail.MainLocation.Active)
            throw Error(InventoryError.LocationNotFound, "El almacén está inactivo.");
        if (operation == "opening" && detail.Balance is not null)
            throw Error(InventoryError.AlreadyInitialized, "Este artículo ya tiene saldo inicial.");
        if (operation != "opening" && detail.Balance is null)
            throw Error(InventoryError.NotInitialized, "Este artículo todavía no tiene saldo inicial.");
        if (detail.Balance is { } balance && balance.Dimension != detail.Item.Dimension)
            throw Error(InventoryError.DimensionMismatch, "La dimensión de la existencia no coincide con el artículo.");

        var allowZero = operation is "opening" or "count";
        if (quantity < 0 || (!allowZero && quantity == 0))
            throw Error(InventoryError.InvalidQuantity,
                allowZero ? "Escribe una cantidad igual o mayor que cero." : "Escribe una cantidad mayor que cero.");

        try
        {
            var capture = CaptureQuantity(detail, quantity, unitCode, allowZero);
            var before = new InventoryQuantity(detail.Item.Dimension, detail.Balance?.QuantityAtomic ?? 0);
            if (operation == "opening")
                return new(detail.Item.Dimension, 0, capture.Quantity.AtomicUnits, capture.Quantity.AtomicUnits, capture);
            if (operation == "count" && capture.Quantity == before)
                return new(detail.Item.Dimension, before.AtomicUnits, 0, before.AtomicUnits, capture);

            InventoryDelta delta;
            if (operation == "count")
                delta = capture.Quantity.CompareTo(before) > 0
                    ? InventoryDelta.Increase(capture.Quantity.Subtract(before))
                    : InventoryDelta.Decrease(before.Subtract(capture.Quantity));
            else
                delta = operation == "receipt"
                    ? InventoryDelta.Increase(capture.Quantity)
                    : InventoryDelta.Decrease(capture.Quantity);

            var after = delta.ApplyTo(before);
            return new(detail.Item.Dimension, before.AtomicUnits, delta.AtomicUnits, after.AtomicUnits, capture);
        }
        catch (OverflowException error)
        {
            throw Error(InventoryError.InvalidQuantity, "La cantidad excede el rango permitido.", error);
        }
        catch (ArgumentException error)
        {
            throw Error(InventoryError.InvalidQuantity, error.Message, error);
        }
        catch (InvalidOperationException error)
        {
            throw Error(InventoryError.InsufficientStock, "No hay existencia suficiente para esta merma.", error);
        }
    }

    private static InventoryCapture CaptureQuantity(InventoryItemDetail detail, decimal quantity,
        string unitCode, bool allowZero)
    {
        var standard = InventoryUnits.All.SingleOrDefault(x => string.Equals(x.Code, unitCode, StringComparison.Ordinal));
        if (standard is not null)
        {
            if (standard.Dimension != detail.Item.Dimension)
                throw Error(InventoryError.DimensionMismatch, "La unidad no corresponde a la dimensión del artículo.");
            return InventoryCapture.Standard(quantity, standard);
        }

        var presentation = detail.Units.SingleOrDefault(x => x.IsActive &&
            string.Equals(x.Code, unitCode, StringComparison.Ordinal));
        if (presentation is null)
            throw Error(InventoryError.InvalidUnit, "La presentación no existe o está inactiva.");
        if (quantity != decimal.Truncate(quantity) || quantity > long.MaxValue)
            throw Error(InventoryError.InvalidQuantity, "La presentación se captura en unidades enteras.");
        // Application uses the atomic standard unit for a permitted zero capture.
        if (quantity == 0 && allowZero)
            return InventoryCapture.Standard(0, detail.Item.Dimension switch
            {
                InventoryDimension.Mass => InventoryUnits.Milligram,
                InventoryDimension.Volume => InventoryUnits.Microliter,
                InventoryDimension.Count => InventoryUnits.Piece,
                _ => throw new ArgumentOutOfRangeException(nameof(detail))
            });
        return InventoryCapture.Presentation(checked((long)quantity), presentation);
    }

    private static InventoryApplicationException Error(InventoryError code, string message, Exception? inner = null) =>
        new(code, message, inner);
}
