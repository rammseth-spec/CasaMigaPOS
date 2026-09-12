using System.Text;

namespace CasaMiga.Domain;

public enum InventoryDimension { Mass, Volume, Count }
public enum InventoryItemKind { RawMaterial, FinishedGood, Packaging, Supply }
public enum InventoryMovementKind
{
    OpeningBalance,
    Receipt,
    AdjustmentGain,
    AdjustmentLoss,
    Waste,
    ProductionConsumption,
    ProductionOutput,
    SaleConsumption,
    Return,
    Transfer
}
public enum WasteReason { Damaged, Expired, ProductionError, Spill, Other }
public enum AdjustmentReason { PhysicalCountVariance, DataCorrection, Other }

public static class InventoryMovementKinds
{
    public static bool IsEnabledInPhase1H(InventoryMovementKind kind) => kind is
        InventoryMovementKind.OpeningBalance or InventoryMovementKind.Receipt or
        InventoryMovementKind.AdjustmentGain or InventoryMovementKind.AdjustmentLoss or
        InventoryMovementKind.Waste;
}

public sealed record InventoryUnit
{
    public string Code { get; }
    public string Symbol { get; }
    public InventoryDimension Dimension { get; }
    public long AtomicUnitsPerUnit { get; }

    internal InventoryUnit(string code, string symbol, InventoryDimension dimension, long atomicUnitsPerUnit)
    {
        Code = code;
        Symbol = symbol;
        Dimension = dimension;
        AtomicUnitsPerUnit = atomicUnitsPerUnit;
    }
}

public static class InventoryUnits
{
    public static InventoryUnit Milligram { get; } = new("mg", "mg", InventoryDimension.Mass, 1);
    public static InventoryUnit Gram { get; } = new("g", "g", InventoryDimension.Mass, 1_000);
    public static InventoryUnit Kilogram { get; } = new("kg", "kg", InventoryDimension.Mass, 1_000_000);
    public static InventoryUnit Microliter { get; } = new("uL", "µL", InventoryDimension.Volume, 1);
    public static InventoryUnit Milliliter { get; } = new("ml", "ml", InventoryDimension.Volume, 1_000);
    public static InventoryUnit Liter { get; } = new("L", "L", InventoryDimension.Volume, 1_000_000);
    public static InventoryUnit Piece { get; } = new("piece", "pieza", InventoryDimension.Count, 1);

    private static readonly InventoryUnit[] units =
        [Milligram, Gram, Kilogram, Microliter, Milliliter, Liter, Piece];
    private static readonly IReadOnlyDictionary<string, InventoryUnit> byCode =
        units.ToDictionary(unit => unit.Code, StringComparer.Ordinal);

    public static IReadOnlyCollection<InventoryUnit> All => units;
    public static InventoryUnit FromCode(string code) => byCode.TryGetValue(code, out var unit)
        ? unit
        : throw new ArgumentException("Unidad de inventario desconocida.", nameof(code));
}

public readonly record struct InventoryQuantity : IComparable<InventoryQuantity>
{
    public InventoryDimension Dimension { get; }
    public long AtomicUnits { get; }
    public bool IsZero => AtomicUnits == 0;

    public InventoryQuantity(InventoryDimension dimension, long atomicUnits)
    {
        ValidateDimension(dimension);
        if (atomicUnits < 0) throw new ArgumentOutOfRangeException(nameof(atomicUnits), "La cantidad no puede ser negativa.");
        Dimension = dimension;
        AtomicUnits = atomicUnits;
    }

    public static InventoryQuantity From(decimal amount, InventoryUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "La cantidad no puede ser negativa.");
        var atomic = checked(amount * unit.AtomicUnitsPerUnit);
        if (atomic != decimal.Truncate(atomic))
            throw new ArgumentException("La cantidad no puede representarse exactamente en la unidad atómica.", nameof(amount));
        return new InventoryQuantity(unit.Dimension, checked((long)atomic));
    }

    public decimal In(InventoryUnit unit)
    {
        RequireDimension(unit);
        return AtomicUnits / (decimal)unit.AtomicUnitsPerUnit;
    }

    public InventoryQuantity Add(InventoryQuantity other)
    {
        RequireDimension(other.Dimension);
        return new(Dimension, checked(AtomicUnits + other.AtomicUnits));
    }

    public InventoryQuantity Subtract(InventoryQuantity other)
    {
        RequireDimension(other.Dimension);
        if (other.AtomicUnits > AtomicUnits)
            throw new ArgumentOutOfRangeException(nameof(other), "La cantidad resultante no puede ser negativa.");
        return new(Dimension, checked(AtomicUnits - other.AtomicUnits));
    }

    public InventoryQuantity Multiply(long factor)
    {
        if (factor < 0) throw new ArgumentOutOfRangeException(nameof(factor));
        return new(Dimension, checked(AtomicUnits * factor));
    }

    public int CompareTo(InventoryQuantity other)
    {
        RequireDimension(other.Dimension);
        return AtomicUnits.CompareTo(other.AtomicUnits);
    }

    private void RequireDimension(InventoryDimension dimension)
    {
        ValidateDimension(dimension);
        if (Dimension != dimension) throw new ArgumentException("Las dimensiones de inventario no son compatibles.");
    }

    private void RequireDimension(InventoryUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        RequireDimension(unit.Dimension);
    }

    private static void ValidateDimension(InventoryDimension dimension)
    {
        if (!Enum.IsDefined(dimension)) throw new ArgumentOutOfRangeException(nameof(dimension));
    }
}

public sealed record InventoryItem
{
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public InventoryItemKind Kind { get; }
    public InventoryDimension Dimension { get; }
    public bool IsActive { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public InventoryItem(Guid id, string code, string name, InventoryItemKind kind,
        InventoryDimension dimension, bool active, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Artículo de inventario inválido.", nameof(id));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(dimension)) throw new ArgumentOutOfRangeException(nameof(dimension));
        var created = createdAt.ToUniversalTime();
        var updated = updatedAt.ToUniversalTime();
        if (updated < created) throw new ArgumentException("La actualización no puede preceder a la creación.", nameof(updatedAt));
        Id = id;
        Code = InventoryText.Code(code, 80);
        Name = InventoryText.Name(name, 160);
        Kind = kind;
        Dimension = dimension;
        IsActive = active;
        CreatedAt = created;
        UpdatedAt = updated;
    }

    public InventoryItem Rename(string name, DateTimeOffset at) =>
        new(Id, Code, name, Kind, Dimension, IsActive, CreatedAt, RequireNotBeforeUpdate(at));

    public InventoryItem Reconfigure(InventoryItemKind kind, InventoryDimension dimension, DateTimeOffset at)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(dimension)) throw new ArgumentOutOfRangeException(nameof(dimension));
        if (dimension != Dimension)
            throw new InvalidOperationException("La dimensión no puede cambiar después de crear el artículo.");
        return new(Id, Code, Name, kind, Dimension, IsActive, CreatedAt, RequireNotBeforeUpdate(at));
    }

    public InventoryItem Deactivate(bool hasNonZeroBalance, DateTimeOffset at)
    {
        if (hasNonZeroBalance) throw new InvalidOperationException("No se puede desactivar un artículo con existencia.");
        return new(Id, Code, Name, Kind, Dimension, false, CreatedAt, RequireNotBeforeUpdate(at));
    }

    private DateTimeOffset RequireNotBeforeUpdate(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        if (utc < UpdatedAt) throw new ArgumentException("La actualización no puede retroceder en el tiempo.", nameof(at));
        return utc;
    }
}

public sealed record InventoryItemUnit
{
    public Guid Id { get; }
    public Guid InventoryItemId { get; }
    public string Code { get; }
    public string Name { get; }
    public InventoryDimension Dimension { get; }
    public long AtomicUnitsPerWholeUnit { get; }
    public bool IsActive { get; }

    public InventoryItemUnit(Guid id, InventoryItem item, string code, string name,
        long atomicUnitsPerWholeUnit, bool active = true)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (id == Guid.Empty) throw new ArgumentException("Presentación inválida.", nameof(id));
        if (atomicUnitsPerWholeUnit <= 0) throw new ArgumentOutOfRangeException(nameof(atomicUnitsPerWholeUnit));
        Id = id;
        InventoryItemId = item.Id;
        Code = InventoryText.Code(code, 80);
        Name = InventoryText.Name(name, 100);
        Dimension = item.Dimension;
        AtomicUnitsPerWholeUnit = atomicUnitsPerWholeUnit;
        IsActive = active;
    }

    public InventoryQuantity ConvertWholeUnits(long wholeUnits)
    {
        if (wholeUnits <= 0) throw new ArgumentOutOfRangeException(nameof(wholeUnits), "La presentación se captura en unidades enteras positivas.");
        return new(Dimension, checked(wholeUnits * AtomicUnitsPerWholeUnit));
    }
}

public sealed record InventoryLocation
{
    public const string MainCode = "MAIN";
    public const string MainName = "Almacén principal";
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public bool IsActive { get; }

    public InventoryLocation(Guid id, string code, string name, bool active = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("Ubicación inválida.", nameof(id));
        Id = id;
        Code = InventoryText.Code(code, 40);
        Name = InventoryText.Name(name, 100);
        IsActive = active;
    }

    public static InventoryLocation Main(Guid id) => new(id, MainCode, MainName);
}

public sealed record ProductInventoryLink
{
    public Guid ProductId { get; }
    public Guid InventoryItemId { get; }
    public InventoryQuantity QuantityPerProductUnit { get; }
    public long QuantityPerProductUnitAtomic => QuantityPerProductUnit.AtomicUnits;

    public ProductInventoryLink(Guid productId, Guid inventoryItemId, InventoryQuantity quantityPerProductUnit)
    {
        if (productId == Guid.Empty || inventoryItemId == Guid.Empty)
            throw new ArgumentException("Asociación producto-inventario inválida.");
        if (quantityPerProductUnit.IsZero)
            throw new ArgumentOutOfRangeException(nameof(quantityPerProductUnit), "La cantidad por unidad comercial debe ser positiva.");
        ProductId = productId;
        InventoryItemId = inventoryItemId;
        QuantityPerProductUnit = quantityPerProductUnit;
    }
}

public readonly record struct WasteReasonSelection
{
    public WasteReason Code { get; }
    public string? Note { get; }
    public WasteReasonSelection(WasteReason code, string? note = null)
    {
        if (!Enum.IsDefined(code)) throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
        Note = InventoryText.OptionalNote(note, code == WasteReason.Other);
    }
}

public readonly record struct AdjustmentReasonSelection
{
    public AdjustmentReason Code { get; }
    public string? Note { get; }
    public AdjustmentReasonSelection(AdjustmentReason code, string? note = null)
    {
        if (!Enum.IsDefined(code)) throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
        Note = InventoryText.OptionalNote(note, code == AdjustmentReason.Other);
    }
}

internal static class InventoryText
{
    public static string Code(string value, int maximum)
    {
        var code = (value ?? "").Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        if (code.Length is 0 || code.Length > maximum) throw new ArgumentException($"El código es obligatorio y admite hasta {maximum} caracteres.");
        return code;
    }

    public static string Name(string value, int maximum)
    {
        var name = (value ?? "").Trim().Normalize(NormalizationForm.FormC);
        if (name.Length is 0 || name.Length > maximum) throw new ArgumentException($"El nombre es obligatorio y admite hasta {maximum} caracteres.");
        return name;
    }

    public static string? OptionalNote(string? value, bool required)
    {
        var note = value?.Trim().Normalize(NormalizationForm.FormC);
        if (note?.Length > 500) throw new ArgumentException("La observación admite hasta 500 caracteres.");
        if (required && string.IsNullOrWhiteSpace(note)) throw new ArgumentException("El motivo OTHER exige una observación.");
        return string.IsNullOrWhiteSpace(note) ? null : note;
    }
}
