using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class ProductionDomainTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-09-20T12:00:00Z");
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    private static InventoryQuantity G(decimal value) => InventoryQuantity.From(value, InventoryUnits.Gram);
    private static InventoryQuantity Ml(decimal value) => InventoryQuantity.From(value, InventoryUnits.Milliliter);
    private static InventoryQuantity Piece(long value) => new(InventoryDimension.Count, value);
    private static FormulaIngredient Ingredient(int id, InventoryQuantity quantity,
        IngredientCountPolicy policy = IngredientCountPolicy.NotApplicable) => new(Id(id), quantity, policy);
    private static FormulaVersion Published(InventoryQuantity yield, IEnumerable<FormulaIngredient> ingredients,
        IEnumerable<IngredientAlternativeGroup>? groups = null, int id = 100)
    {
        var formula = new Formula(Id(id), "FORMULA");
        return formula.PublishVersion(new(Id(id + 1), formula.Id, 1, "Preparación de prueba", yield,
            ingredients, groups ?? [], At), At).Versions.Single();
    }
    private static ProductProductionProfileVersion Profile(int id, FormulaVersion? formula = null,
        InventoryQuantity? perPiece = null, IEnumerable<DirectIngredientRequirement>? direct = null)
    {
        var owner = new ProductProductionProfile(Id(id), Id(id + 1), Id(id + 2));
        return owner.PublishVersion(new(Id(id + 3), owner.Id, 1, owner.ProductId, owner.FinishedInventoryItemId,
            "Pan de prueba", formula is null ? [] : [new(formula.Id, perPiece ?? G(85))], direct ?? [], At), At).Versions.Single();
    }
    private static ProductionDomainException Error(ProductionError code, Action action)
    {
        var error = Assert.ThrowsExactly<ProductionDomainException>(action);
        Assert.AreEqual(code, error.Code); return error;
    }

    [TestMethod]
    public void SweetDoughFixturePreservesMixedDimensionsAndExactYield()
    {
        // Full 3 kg flour batch; yield is observed, not 1916.666... per kg.
        var formula = Published(G(5750), [Ingredient(1, G(3000)), Ingredient(2, G(600)),
            Ingredient(3, Ml(750)), Ingredient(4, Piece(12), IngredientCountPolicy.CeilingWholeCount)]);
        Assert.AreEqual(5_750_000L, formula.BaseYield.AtomicUnits);
        Assert.AreEqual(3, formula.Ingredients.Select(x => x.Quantity.Dimension).Distinct().Count());
        Assert.AreEqual(1L, ProductionScaling.Factor(G(5750), formula.BaseYield).Numerator);
    }

    [TestMethod]
    public void PublicationAndNewRevisionDoNotMutatePriorObjectsOrInputLists()
    {
        var ingredients = new List<FormulaIngredient> { Ingredient(1, G(3000)) };
        var owner = new Formula(Id(100), "DULCE");
        var draft = new FormulaVersion(Id(101), owner.Id, 1, "Masa", G(5750), ingredients, [], At);
        ingredients.Clear();
        var first = owner.PublishVersion(draft, At);
        var second = first.PublishVersion(new(Id(102), owner.Id, 2, "Masa nueva", G(5700),
            [Ingredient(1, G(2900))], [], At.AddDays(1)), At.AddDays(1));
        Assert.AreEqual(ProductionVersionState.Draft, draft.State);
        Assert.AreEqual(ProductionVersionState.Published, first.Versions[0].State);
        Assert.AreEqual(3_000_000L, first.Versions[0].Ingredients[0].Quantity.AtomicUnits);
        Assert.AreEqual(Id(101), first.ActiveVersionId);
        Assert.AreEqual(Id(102), second.ActiveVersionId);
        Assert.AreEqual(2, second.Versions.Count);
        Assert.IsNull(second.Deactivate().ActiveVersionId);
        Assert.AreEqual(0, owner.Versions.Count);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<FormulaIngredient>)draft.Ingredients).Clear());
        Error(ProductionError.InvalidVersion, () => second.PublishVersion(draft, At));
    }

    [TestMethod]
    public void VersionOwnershipAndPublicationTimeAreValidated()
    {
        var draft = new FormulaVersion(Id(101), Id(100), 1, "Masa", G(1), [Ingredient(1, G(1))], [], At);
        Error(ProductionError.InvalidVersion, () => new Formula(Id(200), "X").PublishVersion(draft, At));
        Error(ProductionError.InvalidVersion, () => new Formula(Id(100), "X").PublishVersion(draft, At.AddSeconds(-1)));
    }

    [TestMethod]
    public void RationalFactorIsReducedWithoutDecimalApproximation()
    {
        var factor = ProductionScaling.Factor(G(3000), G(5750));
        Assert.AreEqual(12L, factor.Numerator); Assert.AreEqual(23L, factor.Denominator);
        var scaled = ProductionScaling.Scale(G(5750), factor, IngredientCountPolicy.NotApplicable);
        Assert.AreEqual(G(3000), scaled.Quantity);
        Assert.AreEqual(new ProductionRatio(1, 1), new ProductionRatio(1, long.MaxValue).Multiply(long.MaxValue));
    }

    [TestMethod]
    public void RationalAndPlanOverflowAreStructured()
    {
        Error(ProductionError.Overflow, () => new ProductionRatio(long.MaxValue, 1).Multiply(2));
        var formula = Published(G(1), [Ingredient(1, G(1))]);
        var profile = Profile(200, formula, new(InventoryDimension.Mass, long.MaxValue));
        var result = ProductionPlan.Calculate([new(profile, 2)], [formula]);
        Assert.IsNull(result.Plan); Assert.AreEqual(ProductionError.Overflow, result.Issues.Single().Code);
    }

    [TestMethod]
    [DataRow(InventoryDimension.Mass)]
    [DataRow(InventoryDimension.Volume)]
    public void FractionalAtomicMassAndVolumeRequireResolution(InventoryDimension dimension)
    {
        var result = ProductionScaling.Scale(new(dimension, 1), new(1, 3), IngredientCountPolicy.NotApplicable);
        Assert.AreEqual(ProductionError.NonRepresentableScale, result.Error);
        Assert.IsNull(result.Quantity); Assert.AreEqual(new ProductionRatio(1, 3), result.ExactAtomic);
    }

    [TestMethod]
    [DataRow(50, 5)]
    [DataRow(51, 6)]
    [DataRow(52, 6)]
    [DataRow(59, 6)]
    public void EggCeilingIsLinePolicy(int tenths, int expected)
    {
        var result = ProductionScaling.Scale(Piece(1), new(tenths, 10), IngredientCountPolicy.CeilingWholeCount);
        Assert.AreEqual((long)expected, result.Quantity!.Value.AtomicUnits);
    }

    [TestMethod]
    public void DryYeastWholePacketsRejectFractionWithoutCeiling()
    {
        Assert.AreEqual(Piece(4), ProductionScaling.Scale(Piece(2), new(2, 1), IngredientCountPolicy.ExactWholeCount).Quantity);
        var result = ProductionScaling.Scale(Piece(2), new(13, 10), IngredientCountPolicy.ExactWholeCount);
        Assert.AreEqual(new ProductionRatio(13, 5), result.ExactAtomic);
        Assert.AreEqual(ProductionError.AlternativeNotEligible, result.Error);
        Assert.IsNull(result.Quantity);
    }

    private static (FormulaVersion Formula, Guid Group, Guid Dry, Guid Fresh) Yeast(bool preference = true)
    {
        var dry = new IngredientAlternativeOption(Id(11), Ingredient(2, Piece(2), IngredientCountPolicy.ExactWholeCount));
        var fresh = new IngredientAlternativeOption(Id(12), Ingredient(3, G(30)));
        var group = new IngredientAlternativeGroup(Id(10), [dry, fresh], preference ? [dry.Id, fresh.Id] : []);
        return (Published(G(1000), [Ingredient(1, G(1000))], [group]), group.Id, dry.Id, fresh.Id);
    }

    [TestMethod]
    public void ConfiguredFallbackUsesFreshOnlyWhenDryFractionIsIneligible()
    {
        var f = Yeast(); var p = Profile(200, f.Formula, G(1300));
        var result = ProductionPlan.Calculate([new(p, 1)], [f.Formula]);
        Assert.IsTrue(result.IsResolved);
        Assert.AreEqual(G(39), result.Plan!.Ingredients.Single(x => x.InventoryItemId == Id(3)).Quantity);
        Assert.IsFalse(result.Plan.Ingredients.Any(x => x.InventoryItemId == Id(2)));
        Assert.AreEqual(f.Fresh, result.Plan.Formulas.Single().Ingredients.Single(x => x.GroupId == f.Group).OptionId);
        Assert.AreEqual(f.Dry, result.IneligibleOptions.Single().OptionId);
    }

    [TestMethod]
    public void ExplicitSelectionNeverSilentlyFallsBack()
    {
        var f = Yeast(); var p = Profile(200, f.Formula, G(1300));
        var result = ProductionPlan.Calculate([new(p, 1)], [f.Formula], [new(f.Formula.Id, f.Group, f.Dry)]);
        Assert.IsNull(result.Plan);
        Assert.IsTrue(result.Issues.Any(x => x.Code == ProductionError.AlternativeNotEligible));
    }

    [TestMethod]
    public void AlternativesRequireExactlyOneValidChoiceOrConfiguredPreference()
    {
        var f = Yeast(false); var p = Profile(200, f.Formula, G(1000));
        Assert.IsFalse(ProductionPlan.Calculate([new(p, 1)], [f.Formula]).IsResolved);
        Assert.IsTrue(ProductionPlan.Calculate([new(p, 1)], [f.Formula], [new(f.Formula.Id, f.Group, f.Dry)]).IsResolved);
        var both = ProductionPlan.Calculate([new(p, 1)], [f.Formula],
            [new(f.Formula.Id, f.Group, f.Dry), new(f.Formula.Id, f.Group, f.Fresh)]);
        Assert.AreEqual(ProductionError.InvalidAlternative, both.Issues.Single().Code);
        var unknown = ProductionPlan.Calculate([new(p, 1)], [f.Formula], [new(f.Formula.Id, f.Group, Id(999))]);
        Assert.AreEqual(ProductionError.InvalidAlternative, unknown.Issues.Single().Code);
    }

    [TestMethod]
    public void SharedFormulaIsAggregatedBeforeEggCeilingAndDirectIngredientsAreSummed()
    {
        var f = Published(G(5750), [Ingredient(1, G(3000)), Ingredient(2, Piece(12), IngredientCountPolicy.CeilingWholeCount)]);
        var concha = Profile(200, f, G(85));
        var cuerno = Profile(300, f, G(80));
        var bigote = Profile(400, f, G(70), [new(Ingredient(3, G(6))), new(Ingredient(4, G(2)))]);
        // 2550 + 1600 + 700 = 4850g, flour would be fractional mg: resolution is explicit.
        var nonExact = ProductionPlan.Calculate([new(concha, 30), new(cuerno, 20), new(bigote, 10)], [f]);
        Assert.IsFalse(nonExact.IsResolved);
        Assert.AreEqual(G(4850), nonExact.RequiredFormulas.Single().Required);
        Assert.IsTrue(nonExact.Issues.Any(x => x.Code == ProductionError.NonRepresentableScale));
        // A deliberately exact fixture isolates aggregation and single rounding, not a productive recipe.
        var exact = Published(G(1000), [Ingredient(1, G(500)), Ingredient(2, Piece(1), IngredientCountPolicy.CeilingWholeCount)], id: 110);
        concha = Profile(200, exact, G(85), [new(Ingredient(4, G(2)))]);
        cuerno = Profile(300, exact, G(80));
        bigote = Profile(400, exact, G(70), [new(Ingredient(3, G(6))), new(Ingredient(4, G(2)))]);
        var plan = ProductionPlan.Calculate([new(concha, 30), new(cuerno, 20), new(bigote, 10)], [exact]).Plan!;
        Assert.AreEqual(G(4850), plan.Formulas.Single().Required);
        Assert.AreEqual(5L, plan.Ingredients.Single(x => x.InventoryItemId == Id(2)).Quantity.AtomicUnits);
        Assert.AreEqual(G(80), plan.Ingredients.Single(x => x.InventoryItemId == Id(4)).Quantity);
        Assert.AreEqual(G(60), plan.Ingredients.Single(x => x.InventoryItemId == Id(3)).Quantity);
    }

    [TestMethod]
    public void VisualOrderDoesNotChangePlanOrderingOrQuantities()
    {
        var f = Published(G(1000), [Ingredient(1, G(500))]);
        var a = new PlannedOutputLine(Profile(200, f), 30);
        var b = new PlannedOutputLine(Profile(300, f, G(80)), 20);
        var first = ProductionPlan.Calculate([a, b], [f]).Plan!;
        var second = ProductionPlan.Calculate([b, a], [f]).Plan!;
        CollectionAssert.AreEqual(first.Outputs.ToArray(), second.Outputs.ToArray());
        CollectionAssert.AreEqual(first.Ingredients.ToArray(), second.Ingredients.ToArray());
        Assert.AreEqual(first.Formulas.Single().Factor, second.Formulas.Single().Factor);
    }

    [TestMethod]
    public void AuxiliaryPreparationUsesExplicitVersionAndYieldWithoutInventingCoverage()
    {
        var dough = Published(G(1000), [Ingredient(1, G(500))]);
        // Synthetic verified yield, deliberately unrelated to the unresolved 26–27 observation.
        var cover = Published(G(200), [Ingredient(2, G(100)), Ingredient(3, G(100))], id: 110);
        var owner = new ProductProductionProfile(Id(200), Id(201), Id(202));
        var profile = owner.PublishVersion(new(Id(203), owner.Id, 1, owner.ProductId, owner.FinishedInventoryItemId,
            "Concha auxiliar", [new(dough.Id, G(85)), new(cover.Id, G(10))], [], At), At).Versions.Single();
        var plan = ProductionPlan.Calculate([new(profile, 20)], [cover, dough]).Plan!;
        Assert.AreEqual(2, plan.Formulas.Count);
        Assert.AreEqual(G(100), plan.Ingredients.Single(x => x.InventoryItemId == Id(2)).Quantity);
    }

    [TestMethod]
    public void ProfileVersionsKeepPhysicalTargetAndSingleActivePointer()
    {
        var owner = new ProductProductionProfile(Id(200), Id(201), Id(202));
        var direct = new[] { new DirectIngredientRequirement(Ingredient(1, G(1.5m))) };
        var one = new ProductProductionProfileVersion(Id(203), owner.Id, 1, owner.ProductId, owner.FinishedInventoryItemId,
            "Rosquita fixture", [], direct, At);
        var first = owner.PublishVersion(one, At);
        var second = first.PublishVersion(new(Id(204), owner.Id, 2, owner.ProductId, owner.FinishedInventoryItemId,
            "Nueva", [], direct, At), At);
        Assert.AreEqual(Id(203), first.ActiveVersionId); Assert.AreEqual(Id(204), second.ActiveVersionId);
        Assert.IsNull(second.Deactivate().ActiveVersionId);
        Assert.AreEqual(ProductionVersionState.Draft, one.State);
        Assert.AreEqual(2, second.Versions.Count);
        Error(ProductionError.InvalidVersion, () => second.PublishVersion(new(Id(205), owner.Id, 3, owner.ProductId,
            Id(999), "Otro resultado", [], direct, At), At));
    }

    [TestMethod]
    public void ActualOutputDoesNotRecalculatePreparedIngredients()
    {
        var f = Published(G(1000), [Ingredient(1, G(500))]);
        var p = Profile(200, f, G(100));
        var plan = ProductionPlan.Calculate([new(p, 30)], [f]).Plan!;
        var actual = new ProductionActualOutput(plan, [new(p.ProductId, 28)]);
        Assert.AreSame(plan, actual.PreparedPlan);
        Assert.AreEqual(30L, actual.PreparedPlan.Outputs.Single().PlannedCount);
        Assert.AreEqual(28L, actual.Outputs.Single().ActualCount);
        Assert.AreEqual(G(1500), actual.PreparedPlan.Ingredients.Single().Quantity);
        Assert.AreEqual(0, actual.PendingBusinessPolicyProducts.Count);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(31)]
    public void ZeroAndExcessActualAreRepresentedWithPendingPolicy(int actualCount)
    {
        var p = Profile(200, direct: [new(Ingredient(1, G(1)))]);
        var plan = ProductionPlan.Calculate([new(p, 30)], []).Plan!;
        var actual = new ProductionActualOutput(plan, [new(p.ProductId, actualCount)]);
        Assert.AreEqual(p.ProductId, actual.PendingBusinessPolicyProducts.Single());
        Assert.AreEqual(G(30), actual.PreparedPlan.Ingredients.Single().Quantity);
    }

    [TestMethod]
    public void ActualOutputRejectsNegativeDuplicateMissingOrUnknownProducts()
    {
        var p = Profile(200, direct: [new(Ingredient(1, G(1)))]);
        var plan = ProductionPlan.Calculate([new(p, 2)], []).Plan!;
        Error(ProductionError.InvalidActualOutput, () => new ActualOutputLine(p.ProductId, -1));
        Error(ProductionError.InvalidActualOutput, () => new ProductionActualOutput(plan, []));
        Error(ProductionError.InvalidActualOutput, () => new ProductionActualOutput(plan, [new(Id(999), 1)]));
        Error(ProductionError.InvalidActualOutput, () => new ProductionActualOutput(plan, [new(p.ProductId, 1), new(p.ProductId, 1)]));
    }

    [TestMethod]
    public void DuplicateOutputsAndIngredientDimensionsAreRejected()
    {
        var p = Profile(200, direct: [new(Ingredient(1, G(1)))]);
        Assert.AreEqual(ProductionError.DuplicateOutput,
            ProductionPlan.Calculate([new(p, 1), new(p, 2)], []).Issues.Single().Code);
        var other = Profile(300, direct: [new(Ingredient(1, Ml(1)))]);
        Assert.AreEqual(ProductionError.DimensionMismatch,
            ProductionPlan.Calculate([new(p, 1), new(other, 1)], []).Issues.Single().Code);
    }

    [TestMethod]
    public void FormulaRequirementMustMatchYieldDimension()
    {
        var f = Published(G(1000), [Ingredient(1, G(500))]);
        var p = Profile(200, f, Ml(1));
        Assert.AreEqual(ProductionError.DimensionMismatch, ProductionPlan.Calculate([new(p, 1)], [f]).Issues.Single().Code);
        Error(ProductionError.DimensionMismatch, () => Ingredient(2, G(1), IngredientCountPolicy.CeilingWholeCount));
    }

    [TestMethod]
    public void UnknownPolvoronEggPolicyIsNotInferredFromSweetDough()
    {
        var f = Published(G(4000), [Ingredient(1, G(1800)), Ingredient(2, Piece(1), IngredientCountPolicy.Unspecified)]);
        var p = Profile(200, f, G(100));
        var result = ProductionPlan.Calculate([new(p, 20)], [f]);
        Assert.IsNull(result.Plan);
        Assert.AreEqual(ProductionError.CountPolicyRequired, result.Issues.Single().Code);
    }

    [TestMethod]
    public void InvalidYieldDuplicateIngredientsAndMissingVersionsAreRejected()
    {
        Error(ProductionError.InvalidFormulaYield, () => Published(G(0), [Ingredient(1, G(1))]));
        Error(ProductionError.DuplicateIngredient, () => Published(G(1), [Ingredient(1, G(1)), Ingredient(1, G(2))]));
        var f = Published(G(3850), [Ingredient(1, G(2000))]);
        var p = Profile(200, f, G(3850));
        Assert.AreEqual(ProductionError.InvalidVersion, ProductionPlan.Calculate([new(p, 1)], []).Issues.Single().Code);
        Assert.AreEqual(G(2000), ProductionPlan.Calculate([new(p, 1)], [f]).Plan!.Ingredients.Single().Quantity);
        // No individual doughnut weight is deduced from the observed count of 62.
    }

    [TestMethod]
    public void FormulaCannotContainFormulaReferencesAndQuantitiesHaveNoFloatingTypes()
    {
        Assert.IsFalse(typeof(FormulaIngredient).GetProperties().Any(x => x.PropertyType == typeof(FormulaVersion) ||
            x.PropertyType == typeof(FormulaRequirement)));
        foreach (var type in new[] { typeof(InventoryQuantity), typeof(ProductionRatio), typeof(PlannedOutputLine), typeof(ActualOutputLine) })
            Assert.IsFalse(type.GetProperties().Any(x => x.PropertyType == typeof(float) || x.PropertyType == typeof(double)));
        Assert.AreEqual(typeof(Guid), typeof(FormulaIngredient).GetProperty(nameof(FormulaIngredient.InventoryItemId))!.PropertyType);
    }

    [TestMethod]
    public void EligiblePreferredAlternativeIsExclusiveAndItsPolicyIsHistorical()
    {
        var f = Yeast(); var p = Profile(200, f.Formula, G(1000));
        var result = ProductionPlan.Calculate([new(p, 1)], [f.Formula]);
        Assert.AreEqual(0, result.IneligibleOptions.Count);
        Assert.IsFalse(result.Plan!.Ingredients.Any(x => x.InventoryItemId == Id(3)));
        var selected = result.Plan.Formulas.Single().Ingredients.Single(x => x.GroupId.HasValue);
        Assert.AreEqual(f.Dry, selected.OptionId);
        Assert.AreEqual(IngredientCountPolicy.ExactWholeCount, selected.CountPolicy);
        Assert.AreEqual(Piece(2), selected.Quantity);
    }

    [TestMethod]
    public void UnspecifiedPolicyIsNotSilentlyReplacedByFallback()
    {
        var group = new IngredientAlternativeGroup(Id(10),
            [new(Id(11), Ingredient(1, Piece(1), IngredientCountPolicy.Unspecified)),
             new(Id(12), Ingredient(2, G(1)))], [Id(11), Id(12)]);
        var f = Published(G(1), [], [group]);
        var p = Profile(200, f, G(1));
        var result = ProductionPlan.Calculate([new(p, 1)], [f]);
        Assert.IsFalse(result.IsResolved);
        Assert.IsTrue(result.Issues.Any(x => x.Code == ProductionError.CountPolicyRequired));
    }

    [TestMethod]
    public void AggregatedIngredientOverflowDoesNotReturnPartialPlan()
    {
        var a = Profile(200, direct: [new(Ingredient(1, new(InventoryDimension.Mass, long.MaxValue)))]);
        var b = Profile(300, direct: [new(Ingredient(1, G(1)))]);
        var result = ProductionPlan.Calculate([new(a, 1), new(b, 1)], []);
        Assert.IsNull(result.Plan); Assert.AreEqual(ProductionError.Overflow, result.Issues.Single().Code);
    }

    [TestMethod]
    public void DraftsAndConflictingVersionDefinitionsCannotEnterAPlan()
    {
        var p = new ProductProductionProfileVersion(Id(203), Id(200), 1, Id(201), Id(202),
            "Borrador", [], [new(Ingredient(1, G(1)))], At);
        Error(ProductionError.InvalidVersion, () => new PlannedOutputLine(p, 1));
        var f = Published(G(1), [Ingredient(1, G(1))]);
        var published = Profile(200, f, G(1));
        Assert.AreEqual(ProductionError.ConflictingDefinition,
            ProductionPlan.Calculate([new(published, 1)], [f, f]).Issues.Single().Code);
    }

    [TestMethod]
    public void MultipleActualOutputsAreMatchedByProductNotVisualOrder()
    {
        var a = Profile(200, direct: [new(Ingredient(1, G(1)))]);
        var b = Profile(300, direct: [new(Ingredient(2, G(2)))]);
        var plan = ProductionPlan.Calculate([new(b, 20), new(a, 30)], []).Plan!;
        var actual = new ProductionActualOutput(plan, [new(b.ProductId, 18), new(a.ProductId, 28)]);
        Assert.AreEqual(a.ProductId, actual.Outputs[0].ProductId);
        Assert.AreEqual(2, actual.PreparedPlan.Ingredients.Count);
        Assert.AreEqual(G(40), actual.PreparedPlan.Ingredients.Single(x => x.InventoryItemId == Id(2)).Quantity);
    }
}
