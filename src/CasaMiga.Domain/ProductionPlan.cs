namespace CasaMiga.Domain;

public sealed record PlannedOutputLine
{
    public ProductProductionProfileVersion Profile { get; }
    public Guid ProductId => Profile.ProductId;
    public Guid FinishedInventoryItemId => Profile.FinishedInventoryItemId;
    public long PlannedCount { get; }
    public PlannedOutputLine(ProductProductionProfileVersion profile, long plannedCount)
    {
        ProductionGuard.Require(profile.State == ProductionVersionState.Published, ProductionError.InvalidVersion, profile.Id);
        ProductionGuard.Require(plannedCount > 0, ProductionError.InvalidRecipeQuantity);
        Profile = profile; PlannedCount = plannedCount;
    }
}

public sealed record ProductionAlternativeSelection(Guid FormulaVersionId, Guid GroupId, Guid OptionId);
public sealed record ProductionIssue(ProductionError Code, Guid SubjectId, Guid? FormulaVersionId = null,
    Guid? GroupId = null, Guid? OptionId = null, ProductionRatio? ExactAtomic = null);
public sealed record ScaledIngredient(Guid InventoryItemId, InventoryQuantity Quantity, ProductionRatio ExactAtomic,
    IngredientCountPolicy CountPolicy, Guid? GroupId = null, Guid? OptionId = null);
public sealed record PlannedIngredientTotal(Guid InventoryItemId, InventoryQuantity Quantity);
public sealed record PlannedFormulaRequirement(Guid FormulaVersionId, InventoryQuantity Required);

public sealed class PlannedFormula
{
    public FormulaVersion Version { get; }
    public InventoryQuantity Required { get; }
    public ProductionRatio Factor { get; }
    public IReadOnlyList<ScaledIngredient> Ingredients { get; }
    internal PlannedFormula(FormulaVersion version, InventoryQuantity required, ProductionRatio factor,
        IEnumerable<ScaledIngredient> ingredients)
    {
        Version = version; Required = required; Factor = factor;
        Ingredients = ProductionGuard.Freeze(ingredients.OrderBy(x => x.InventoryItemId));
    }
}

public sealed class ProductionPlanningResult
{
    public ProductionPlan? Plan { get; }
    public IReadOnlyList<ProductionIssue> Issues { get; }
    // Includes rejected preferred options when an explicitly configured fallback succeeds.
    public IReadOnlyList<ProductionIssue> IneligibleOptions { get; }
    public IReadOnlyList<PlannedFormulaRequirement> RequiredFormulas { get; }
    public bool IsResolved => Plan is not null;
    internal ProductionPlanningResult(ProductionPlan? plan, IEnumerable<ProductionIssue> issues,
        IEnumerable<ProductionIssue> ineligible, IEnumerable<PlannedFormulaRequirement>? required = null)
    {
        Plan = plan; Issues = ProductionGuard.Freeze(issues); IneligibleOptions = ProductionGuard.Freeze(ineligible);
        RequiredFormulas = ProductionGuard.Freeze(required ?? []);
    }
}

/// <summary>Pure requirements snapshot; not a stock reservation, persisted batch or ledger operation.</summary>
public sealed class ProductionPlan
{
    public IReadOnlyList<PlannedOutputLine> Outputs { get; }
    public IReadOnlyList<PlannedFormula> Formulas { get; }
    public IReadOnlyList<PlannedIngredientTotal> Ingredients { get; }
    private ProductionPlan(IEnumerable<PlannedOutputLine> outputs, IEnumerable<PlannedFormula> formulas,
        IEnumerable<PlannedIngredientTotal> ingredients)
    {
        Outputs = ProductionGuard.Freeze(outputs); Formulas = ProductionGuard.Freeze(formulas);
        Ingredients = ProductionGuard.Freeze(ingredients);
    }

    public static ProductionPlanningResult Calculate(IEnumerable<PlannedOutputLine> outputs,
        IEnumerable<FormulaVersion> versions, IEnumerable<ProductionAlternativeSelection>? selections = null)
    {
        try { return CalculateCore(outputs, versions, selections ?? []); }
        catch (ProductionDomainException error)
        {
            return new(null, [new(error.Code, error.SubjectId ?? Guid.Empty)], []);
        }
    }

    private static ProductionPlanningResult CalculateCore(IEnumerable<PlannedOutputLine> outputs,
        IEnumerable<FormulaVersion> versions, IEnumerable<ProductionAlternativeSelection> selections)
    {
        var lines = outputs.OrderBy(x => x.ProductId).ToArray();
        ProductionGuard.Require(lines.Length > 0, ProductionError.InvalidRecipeQuantity);
        ProductionGuard.Require(lines.Select(x => x.ProductId).Distinct().Count() == lines.Length &&
            lines.Select(x => x.FinishedInventoryItemId).Distinct().Count() == lines.Length,
            ProductionError.DuplicateOutput);
        ProductionGuard.Require(lines.Select(x => x.Profile.Id).Distinct().Count() == lines.Length &&
            lines.Select(x => x.Profile.ProfileId).Distinct().Count() == lines.Length,
            ProductionError.ConflictingDefinition);
        var definitions = versions.ToArray();
        ProductionGuard.Require(definitions.Select(x => x.Id).Distinct().Count() == definitions.Length &&
            definitions.Select(x => (x.FormulaId, x.Version)).Distinct().Count() == definitions.Length,
            ProductionError.ConflictingDefinition);
        var byId = definitions.ToDictionary(x => x.Id);
        var required = new Dictionary<Guid, InventoryQuantity>();
        var totals = new Dictionary<Guid, InventoryQuantity>();
        var issues = new List<ProductionIssue>();
        var ineligible = new List<ProductionIssue>();
        var formulas = new List<PlannedFormula>();
        foreach (var line in lines)
        {
            foreach (var need in line.Profile.Formulas)
                Add(required, need.FormulaVersionId, Multiply(need.QuantityPerPiece, line.PlannedCount));
            foreach (var direct in line.Profile.DirectIngredients)
            {
                var ingredient = direct.Ingredient;
                var result = ProductionScaling.Scale(ingredient.Quantity, new(line.PlannedCount, 1), ingredient.CountPolicy);
                if (!result.IsEligible) issues.Add(new(result.Error!.Value, ingredient.InventoryItemId,
                    ExactAtomic: result.ExactAtomic));
                else Add(totals, ingredient.InventoryItemId, result.Quantity!.Value);
            }
        }
        var choices = selections.OrderBy(x => x.FormulaVersionId).ThenBy(x => x.GroupId).ToArray();
        ProductionGuard.Require(choices.Select(x => (x.FormulaVersionId, x.GroupId)).Distinct().Count() == choices.Length,
            ProductionError.InvalidAlternative);
        foreach (var choice in choices)
            ProductionGuard.Require(required.ContainsKey(choice.FormulaVersionId) &&
                byId.TryGetValue(choice.FormulaVersionId, out var version) &&
                version.Alternatives.Any(g => g.Id == choice.GroupId && g.Options.Any(o => o.Id == choice.OptionId)),
                ProductionError.InvalidAlternative, choice.GroupId);

        foreach (var requirement in required.OrderBy(x => x.Key))
        {
            ProductionGuard.Require(byId.TryGetValue(requirement.Key, out var version), ProductionError.InvalidVersion, requirement.Key);
            ProductionGuard.Require(version!.State == ProductionVersionState.Published, ProductionError.InvalidVersion, version.Id);
            var factor = ProductionScaling.Factor(requirement.Value, version.BaseYield);
            var scaled = new List<ScaledIngredient>();
            foreach (var ingredient in version.Ingredients)
            {
                var result = ProductionScaling.Scale(ingredient.Quantity, factor, ingredient.CountPolicy);
                if (!result.IsEligible) issues.Add(new(result.Error!.Value, ingredient.InventoryItemId, version.Id,
                    ExactAtomic: result.ExactAtomic));
                else scaled.Add(new(ingredient.InventoryItemId, result.Quantity!.Value, result.ExactAtomic, ingredient.CountPolicy));
            }
            foreach (var group in version.Alternatives)
            {
                var explicitChoice = choices.SingleOrDefault(x => x.FormulaVersionId == version.Id && x.GroupId == group.Id);
                var order = explicitChoice is null ? group.PreferenceOrder : new[] { explicitChoice.OptionId };
                if (order.Count == 0)
                {
                    issues.Add(new(ProductionError.InvalidAlternative, group.Id, version.Id, group.Id));
                    continue;
                }
                bool selected = false;
                foreach (var optionId in order)
                {
                    var ingredient = group.Options.Single(x => x.Id == optionId).Ingredient;
                    var result = ProductionScaling.Scale(ingredient.Quantity, factor, ingredient.CountPolicy);
                    if (!result.IsEligible)
                    {
                        var problem = new ProductionIssue(result.Error!.Value, ingredient.InventoryItemId,
                            version.Id, group.Id, optionId, result.ExactAtomic);
                        ineligible.Add(problem);
                        // An unapproved policy cannot be silently bypassed by fallback.
                        if (result.Error == ProductionError.CountPolicyRequired) { issues.Add(problem); break; }
                        continue;
                    }
                    scaled.Add(new(ingredient.InventoryItemId, result.Quantity!.Value, result.ExactAtomic,
                        ingredient.CountPolicy, group.Id, optionId));
                    selected = true; break;
                }
                if (!selected) issues.Add(new(ProductionError.AlternativeNotEligible, group.Id, version.Id, group.Id));
            }
            foreach (var ingredient in scaled) Add(totals, ingredient.InventoryItemId, ingredient.Quantity);
            formulas.Add(new(version, requirement.Value, factor, scaled));
        }
        // An output cannot also act as a physical ingredient in this non-recursive contract.
        ProductionGuard.Require(!lines.Any(x => totals.ContainsKey(x.FinishedInventoryItemId)),
            ProductionError.ConflictingDefinition);
        var requestedFormulas = required.OrderBy(x => x.Key).Select(x => new PlannedFormulaRequirement(x.Key, x.Value)).ToArray();
        if (issues.Count != 0) return new(null, issues, ineligible, requestedFormulas);
        return new(new(lines, formulas, totals.OrderBy(x => x.Key).Select(x => new PlannedIngredientTotal(x.Key, x.Value))),
            [], ineligible, requestedFormulas);
    }

    private static InventoryQuantity Multiply(InventoryQuantity value, long count) =>
        new(value.Dimension, ProductionGuard.Checked(() => checked(value.AtomicUnits * count)));
    private static void Add(Dictionary<Guid, InventoryQuantity> target, Guid id, InventoryQuantity value)
    {
        if (!target.TryGetValue(id, out var before)) { target.Add(id, value); return; }
        ProductionGuard.Require(before.Dimension == value.Dimension, ProductionError.DimensionMismatch, id);
        target[id] = new(value.Dimension, ProductionGuard.Checked(() => checked(before.AtomicUnits + value.AtomicUnits)));
    }
}

public sealed record ActualOutputLine
{
    public Guid ProductId { get; }
    public long ActualCount { get; }
    public ActualOutputLine(Guid productId, long actualCount)
    {
        ProductionGuard.Require(productId != Guid.Empty && actualCount >= 0, ProductionError.InvalidActualOutput);
        ProductId = productId; ActualCount = actualCount;
    }
}

/// <summary>Records obtained quantities without approving future confirmation policy or changing prepared needs.</summary>
public sealed class ProductionActualOutput
{
    public ProductionPlan PreparedPlan { get; }
    public IReadOnlyList<ActualOutputLine> Outputs { get; }
    public IReadOnlyList<Guid> PendingBusinessPolicyProducts { get; }
    public ProductionActualOutput(ProductionPlan preparedPlan, IEnumerable<ActualOutputLine> outputs)
    {
        PreparedPlan = preparedPlan;
        Outputs = ProductionGuard.Freeze(outputs.OrderBy(x => x.ProductId));
        ProductionGuard.Require(Outputs.Count == preparedPlan.Outputs.Count &&
            Outputs.Select(x => x.ProductId).Distinct().Count() == Outputs.Count &&
            Outputs.All(x => preparedPlan.Outputs.Any(p => p.ProductId == x.ProductId)), ProductionError.InvalidActualOutput);
        PendingBusinessPolicyProducts = ProductionGuard.Freeze(Outputs.Where(x => x.ActualCount == 0 ||
            x.ActualCount > preparedPlan.Outputs.Single(p => p.ProductId == x.ProductId).PlannedCount).Select(x => x.ProductId));
    }
}
