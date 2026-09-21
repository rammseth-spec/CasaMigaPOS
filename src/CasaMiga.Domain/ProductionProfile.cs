namespace CasaMiga.Domain;

public sealed record FormulaRequirement
{
    public Guid FormulaVersionId { get; }
    public InventoryQuantity QuantityPerPiece { get; }
    public FormulaRequirement(Guid formulaVersionId, InventoryQuantity quantityPerPiece)
    {
        FormulaVersionId = ProductionGuard.Id(formulaVersionId);
        QuantityPerPiece = ProductionGuard.Positive(quantityPerPiece, ProductionError.InvalidRecipeQuantity);
    }
}

public sealed record DirectIngredientRequirement
{
    public FormulaIngredient Ingredient { get; }
    public DirectIngredientRequirement(FormulaIngredient ingredient) =>
        Ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
}

public sealed class ProductProductionProfileVersion
{
    public Guid Id { get; }
    public Guid ProfileId { get; }
    public int Version { get; }
    public Guid ProductId { get; }
    public Guid FinishedInventoryItemId { get; }
    public string Name { get; }
    public IReadOnlyList<FormulaRequirement> Formulas { get; }
    public IReadOnlyList<DirectIngredientRequirement> DirectIngredients { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? PublishedAt { get; }
    public ProductionVersionState State => PublishedAt.HasValue ? ProductionVersionState.Published : ProductionVersionState.Draft;

    public ProductProductionProfileVersion(Guid id, Guid profileId, int version, Guid productId,
        Guid finishedInventoryItemId, string name, IEnumerable<FormulaRequirement> formulas,
        IEnumerable<DirectIngredientRequirement> directIngredients, DateTimeOffset createdAt)
    {
        Id = ProductionGuard.Id(id); ProfileId = ProductionGuard.Id(profileId);
        ProductId = ProductionGuard.Id(productId); FinishedInventoryItemId = ProductionGuard.Id(finishedInventoryItemId);
        ProductionGuard.Require(version > 0 && !string.IsNullOrWhiteSpace(name), ProductionError.InvalidVersion);
        Version = version; Name = name.Trim(); CreatedAt = createdAt.ToUniversalTime();
        Formulas = ProductionGuard.Freeze(formulas.OrderBy(x => x.FormulaVersionId));
        DirectIngredients = ProductionGuard.Freeze(directIngredients.OrderBy(x => x.Ingredient.InventoryItemId));
        ProductionGuard.Require(Formulas.Count + DirectIngredients.Count > 0, ProductionError.InvalidRecipeQuantity);
        ProductionGuard.Require(Formulas.Select(x => x.FormulaVersionId).Distinct().Count() == Formulas.Count,
            ProductionError.DuplicateIngredient);
        ProductionGuard.Require(DirectIngredients.Select(x => x.Ingredient.InventoryItemId).Distinct().Count() == DirectIngredients.Count,
            ProductionError.DuplicateIngredient);
    }
    private ProductProductionProfileVersion(ProductProductionProfileVersion draft, DateTimeOffset at)
        : this(draft.Id, draft.ProfileId, draft.Version, draft.ProductId, draft.FinishedInventoryItemId, draft.Name,
            draft.Formulas, draft.DirectIngredients, draft.CreatedAt)
    {
        ProductionGuard.Require(draft.State == ProductionVersionState.Draft && at.ToUniversalTime() >= CreatedAt,
            ProductionError.InvalidVersion);
        PublishedAt = at.ToUniversalTime();
    }
    internal ProductProductionProfileVersion Publish(DateTimeOffset at) => new(this, at);
}

public sealed class ProductProductionProfile
{
    public Guid Id { get; }
    public Guid ProductId { get; }
    public Guid FinishedInventoryItemId { get; }
    public IReadOnlyList<ProductProductionProfileVersion> Versions { get; }
    public Guid? ActiveVersionId { get; }
    public ProductProductionProfile(Guid id, Guid productId, Guid finishedInventoryItemId)
        : this(id, productId, finishedInventoryItemId, [], null) { }
    private ProductProductionProfile(Guid id, Guid productId, Guid itemId,
        IEnumerable<ProductProductionProfileVersion> versions, Guid? active)
    {
        Id = ProductionGuard.Id(id); ProductId = ProductionGuard.Id(productId);
        FinishedInventoryItemId = ProductionGuard.Id(itemId);
        Versions = ProductionGuard.Freeze(versions); ActiveVersionId = active;
    }
    public ProductProductionProfile PublishVersion(ProductProductionProfileVersion draft, DateTimeOffset at)
    {
        ProductionGuard.Require(draft.ProfileId == Id && draft.ProductId == ProductId &&
            draft.FinishedInventoryItemId == FinishedInventoryItemId && !Versions.Any(x => x.Id == draft.Id) &&
            draft.Version > (Versions.Count == 0 ? 0 : Versions.Max(x => x.Version)), ProductionError.InvalidVersion);
        return new(Id, ProductId, FinishedInventoryItemId, Versions.Append(draft.Publish(at)), draft.Id);
    }
    public ProductProductionProfile Deactivate() => new(Id, ProductId, FinishedInventoryItemId, Versions, null);
}
