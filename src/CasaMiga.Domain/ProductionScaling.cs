namespace CasaMiga.Domain;

public enum ProductionError
{
    InvalidRecipeQuantity, InvalidFormulaYield, DimensionMismatch, InvalidAlternative,
    AlternativeNotEligible, NonRepresentableScale, Overflow, DuplicateIngredient,
    DuplicateOutput, InvalidActualOutput, InvalidVersion, ConflictingDefinition,
    CountPolicyRequired,
    /// <summary>Una preparación termina usándose a sí misma a través de otras preparaciones.</summary>
    PreparationCycle,
    /// <summary>La versión está marcada «Por medir»: tiene datos pendientes de confirmar.</summary>
    PendingMeasure
}

public sealed class ProductionDomainException(ProductionError code, Guid? subjectId = null)
    : Exception(code.ToString())
{
    public ProductionError Code { get; } = code;
    public Guid? SubjectId { get; } = subjectId;
}

internal static class ProductionGuard
{
    internal static void Require(bool condition, ProductionError code, Guid? id = null)
    {
        if (!condition) throw new ProductionDomainException(code, id);
    }
    internal static Guid Id(Guid id)
    {
        Require(id != Guid.Empty, ProductionError.InvalidVersion);
        return id;
    }
    internal static InventoryQuantity Positive(InventoryQuantity quantity, ProductionError code)
    {
        Require(Enum.IsDefined(quantity.Dimension) && quantity.AtomicUnits > 0, code);
        return quantity;
    }
    internal static long Checked(Func<long> calculation)
    {
        try { return calculation(); }
        catch (OverflowException) { throw new ProductionDomainException(ProductionError.Overflow); }
    }
    internal static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}

/// <summary>Positive reduced rational. No rounded decimal factor is introduced.</summary>
public sealed record ProductionRatio
{
    public long Numerator { get; }
    public long Denominator { get; }
    public ProductionRatio(long numerator, long denominator)
    {
        ProductionGuard.Require(numerator > 0 && denominator > 0, ProductionError.InvalidRecipeQuantity);
        var gcd = Gcd(numerator, denominator);
        Numerator = numerator / gcd;
        Denominator = denominator / gcd;
    }
    internal static long Gcd(long a, long b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }
    public ProductionRatio Multiply(long value)
    {
        ProductionGuard.Require(value > 0, ProductionError.InvalidRecipeQuantity);
        var gcd = Gcd(value, Denominator);
        return new(ProductionGuard.Checked(() => checked(Numerator * (value / gcd))), Denominator / gcd);
    }
}

public enum IngredientCountPolicy { NotApplicable, ExactWholeCount, CeilingWholeCount, Unspecified }

public sealed record IngredientScaleResult(ProductionRatio ExactAtomic, InventoryQuantity? Quantity,
    ProductionError? Error)
{
    public bool IsEligible => Error is null;
}

public static class ProductionScaling
{
    public static ProductionRatio Factor(InventoryQuantity required, InventoryQuantity yield)
    {
        ProductionGuard.Positive(required, ProductionError.InvalidRecipeQuantity);
        ProductionGuard.Positive(yield, ProductionError.InvalidFormulaYield);
        ProductionGuard.Require(required.Dimension == yield.Dimension, ProductionError.DimensionMismatch);
        return new(required.AtomicUnits, yield.AtomicUnits);
    }

    public static IngredientScaleResult Scale(InventoryQuantity quantity, ProductionRatio factor,
        IngredientCountPolicy policy)
    {
        ProductionGuard.Positive(quantity, ProductionError.InvalidRecipeQuantity);
        ValidatePolicy(quantity.Dimension, policy);
        var exact = factor.Multiply(quantity.AtomicUnits);
        if (quantity.Dimension == InventoryDimension.Count && policy == IngredientCountPolicy.Unspecified)
            return new(exact, null, ProductionError.CountPolicyRequired);
        if (exact.Denominator == 1)
            return new(exact, new(quantity.Dimension, exact.Numerator), null);
        if (quantity.Dimension != InventoryDimension.Count)
            return new(exact, null, ProductionError.NonRepresentableScale);
        if (policy == IngredientCountPolicy.ExactWholeCount)
            return new(exact, null, ProductionError.AlternativeNotEligible);
        var whole = ProductionGuard.Checked(() => checked(exact.Numerator / exact.Denominator + 1));
        return new(exact, new(InventoryDimension.Count, whole), null);
    }

    internal static void ValidatePolicy(InventoryDimension dimension, IngredientCountPolicy policy)
    {
        ProductionGuard.Require(Enum.IsDefined(policy), ProductionError.InvalidRecipeQuantity);
        ProductionGuard.Require(dimension == InventoryDimension.Count
            ? policy != IngredientCountPolicy.NotApplicable
            : policy == IngredientCountPolicy.NotApplicable, ProductionError.DimensionMismatch);
    }
}
