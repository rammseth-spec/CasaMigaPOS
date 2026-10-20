namespace CasaMiga.Domain;

public enum ProductionVersionState { Draft, Published }

public sealed record FormulaIngredient
{
    public Guid InventoryItemId { get; }
    public InventoryQuantity Quantity { get; }
    public IngredientCountPolicy CountPolicy { get; }
    public FormulaIngredient(Guid inventoryItemId, InventoryQuantity quantity, IngredientCountPolicy policy)
    {
        InventoryItemId = ProductionGuard.Id(inventoryItemId);
        Quantity = ProductionGuard.Positive(quantity, ProductionError.InvalidRecipeQuantity);
        ProductionScaling.ValidatePolicy(quantity.Dimension, policy);
        CountPolicy = policy;
    }
}

/// <summary>
/// Otra preparación que esta preparación usa ya hecha, como la masa dulce dentro de la masa enriquecida. La cantidad va en la
/// dimensión del rendimiento de esa preparación; producir consume sus ingredientes una sola vez, junto con los demás usos de esa masa.
/// </summary>
public sealed record FormulaPreparation
{
    public Guid FormulaVersionId { get; }
    public InventoryQuantity Quantity { get; }
    public FormulaPreparation(Guid formulaVersionId, InventoryQuantity quantity)
    {
        FormulaVersionId = ProductionGuard.Id(formulaVersionId);
        Quantity = ProductionGuard.Positive(quantity, ProductionError.InvalidRecipeQuantity);
    }
}

public sealed record IngredientAlternativeOption
{
    public Guid Id { get; }
    public FormulaIngredient Ingredient { get; }
    public IngredientAlternativeOption(Guid id, FormulaIngredient ingredient)
    {
        Id = ProductionGuard.Id(id);
        Ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
    }
}

/// <summary>Fallback is opt-in through an ordered list of option IDs, never inferred from names.</summary>
public sealed class IngredientAlternativeGroup
{
    public Guid Id { get; }
    public IReadOnlyList<IngredientAlternativeOption> Options { get; }
    public IReadOnlyList<Guid> PreferenceOrder { get; }
    public IngredientAlternativeGroup(Guid id, IEnumerable<IngredientAlternativeOption> options,
        IEnumerable<Guid>? preferenceOrder = null)
    {
        Id = ProductionGuard.Id(id);
        Options = ProductionGuard.Freeze(options.OrderBy(x => x.Id));
        PreferenceOrder = ProductionGuard.Freeze(preferenceOrder ?? []);
        ProductionGuard.Require(Options.Count >= 2 && Options.Select(x => x.Id).Distinct().Count() == Options.Count,
            ProductionError.InvalidAlternative, id);
        ProductionGuard.Require(PreferenceOrder.Distinct().Count() == PreferenceOrder.Count &&
            PreferenceOrder.All(x => Options.Any(o => o.Id == x)), ProductionError.InvalidAlternative, id);
    }
}

public sealed class FormulaVersion
{
    public Guid Id { get; }
    public Guid FormulaId { get; }
    public int Version { get; }
    public string Name { get; }
    public InventoryQuantity BaseYield { get; }
    public IReadOnlyList<FormulaIngredient> Ingredients { get; }
    public IReadOnlyList<IngredientAlternativeGroup> Alternatives { get; }
    public IReadOnlyList<FormulaPreparation> Preparations { get; }
    public string? Notes { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? PublishedAt { get; }
    public ProductionVersionState State => PublishedAt.HasValue ? ProductionVersionState.Published : ProductionVersionState.Draft;

    public FormulaVersion(Guid id, Guid formulaId, int version, string name, InventoryQuantity baseYield,
        IEnumerable<FormulaIngredient> ingredients, IEnumerable<IngredientAlternativeGroup> alternatives,
        DateTimeOffset createdAt, string? notes = null, IEnumerable<FormulaPreparation>? preparations = null)
    {
        Id = ProductionGuard.Id(id); FormulaId = ProductionGuard.Id(formulaId);
        ProductionGuard.Require(version > 0 && !string.IsNullOrWhiteSpace(name), ProductionError.InvalidVersion);
        Version = version; Name = name.Trim();
        BaseYield = ProductionGuard.Positive(baseYield, ProductionError.InvalidFormulaYield);
        Ingredients = ProductionGuard.Freeze(ingredients.OrderBy(x => x.InventoryItemId));
        Alternatives = ProductionGuard.Freeze(alternatives.OrderBy(x => x.Id));
        Preparations = ProductionGuard.Freeze((preparations ?? []).OrderBy(x => x.FormulaVersionId));
        ProductionGuard.Require(Ingredients.Count + Alternatives.Count + Preparations.Count > 0, ProductionError.InvalidRecipeQuantity);
        ProductionGuard.Require(Preparations.Select(x => x.FormulaVersionId).Distinct().Count() == Preparations.Count,
            ProductionError.DuplicateIngredient);
        ProductionGuard.Require(Preparations.All(x => x.FormulaVersionId != Id), ProductionError.PreparationCycle, Id);
        ProductionGuard.Require(Alternatives.Select(x => x.Id).Distinct().Count() == Alternatives.Count,
            ProductionError.InvalidAlternative);
        var ids = Ingredients.Select(x => x.InventoryItemId)
            .Concat(Alternatives.SelectMany(x => x.Options).Select(x => x.Ingredient.InventoryItemId)).ToArray();
        ProductionGuard.Require(ids.Distinct().Count() == ids.Length, ProductionError.DuplicateIngredient);
        CreatedAt = createdAt.ToUniversalTime(); Notes = notes;
    }
    private FormulaVersion(FormulaVersion draft, DateTimeOffset at)
        : this(draft.Id, draft.FormulaId, draft.Version, draft.Name, draft.BaseYield, draft.Ingredients,
            draft.Alternatives, draft.CreatedAt, draft.Notes, draft.Preparations)
    {
        ProductionGuard.Require(at.ToUniversalTime() >= CreatedAt && draft.State == ProductionVersionState.Draft,
            ProductionError.InvalidVersion);
        PublishedAt = at.ToUniversalTime();
    }
    internal FormulaVersion Publish(DateTimeOffset at) => new(this, at);
}

/// <summary>Immutable aggregate. Published content stays historical; activation is an aggregate pointer.</summary>
public sealed class Formula
{
    public Guid Id { get; }
    public string Code { get; }
    public IReadOnlyList<FormulaVersion> Versions { get; }
    public Guid? ActiveVersionId { get; }
    public Formula(Guid id, string code) : this(id, code, [], null) { }
    private Formula(Guid id, string code, IEnumerable<FormulaVersion> versions, Guid? active)
    {
        Id = ProductionGuard.Id(id);
        ProductionGuard.Require(!string.IsNullOrWhiteSpace(code), ProductionError.InvalidVersion);
        Code = code.Trim().ToUpperInvariant(); Versions = ProductionGuard.Freeze(versions); ActiveVersionId = active;
    }
    public Formula PublishVersion(FormulaVersion draft, DateTimeOffset at)
    {
        ProductionGuard.Require(draft.FormulaId == Id && !Versions.Any(x => x.Id == draft.Id) &&
            draft.Version > (Versions.Count == 0 ? 0 : Versions.Max(x => x.Version)), ProductionError.InvalidVersion);
        return new(Id, Code, Versions.Append(draft.Publish(at)), draft.Id);
    }
    public Formula Deactivate() => new(Id, Code, Versions, null);
}
