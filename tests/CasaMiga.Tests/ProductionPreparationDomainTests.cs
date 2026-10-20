using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>
/// Preparaciones dentro de preparaciones (migración 031): la forma de la masa enriquecida y de la tanda de roles de Casa Miga,
/// con cantidades de prueba, no las del recetario.
/// </summary>
[TestClass]
public sealed class ProductionPreparationDomainTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-20T12:00:00Z");
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    private static InventoryQuantity G(decimal value) => InventoryQuantity.From(value, InventoryUnits.Gram);
    private static InventoryQuantity Piece(long value) => new(InventoryDimension.Count, value);
    private static FormulaIngredient Ingredient(int id, InventoryQuantity quantity) => new(Id(id), quantity, IngredientCountPolicy.NotApplicable);
    private static FormulaVersion Draft(int id, InventoryQuantity yield, IEnumerable<FormulaIngredient> ingredients, IEnumerable<FormulaPreparation>? preparations = null) =>
        new(Id(id + 1), Id(id), 1, $"Preparación {id}", yield, ingredients, [], At, null, preparations);
    private static FormulaVersion Published(int id, InventoryQuantity yield, IEnumerable<FormulaIngredient> ingredients, IEnumerable<FormulaPreparation>? preparations = null) =>
        new Formula(Id(id), $"F{id}").PublishVersion(Draft(id, yield, ingredients, preparations), At).Versions.Single();
    private static PlannedOutputLine Output(int id, FormulaVersion formula, InventoryQuantity perPiece, long planned, IEnumerable<DirectIngredientRequirement>? direct = null)
    {
        var owner = new ProductProductionProfile(Id(id), Id(id + 1), Id(id + 2));
        var version = owner.PublishVersion(new(Id(id + 3), owner.Id, 1, owner.ProductId, owner.FinishedInventoryItemId, $"Pan {id}",
            [new(formula.Id, perPiece)], direct ?? [], At), At).Versions.Single();
        return new(version, planned);
    }
    private static long Total(ProductionPlan plan, int item) => plan.Ingredients.Single(x => x.InventoryItemId == Id(item)).Quantity.AtomicUnits;

    [TestMethod]
    public void EnrichedDoughUsesSweetDoughOnceTogetherWithTheConchasThatShareIt()
    {
        var sweet = Published(100, G(1000), [Ingredient(1, G(500)), Ingredient(2, G(500))]);
        var enriched = Published(200, G(1000), [Ingredient(3, G(100))], [new(sweet.Id, G(900))]);
        var concha = Output(1000, sweet, G(80), 10);
        var cuernito = Output(2000, enriched, G(80), 10);

        var result = ProductionPlan.Calculate([concha, cuernito], ProductionPlan.RequiredVersions([sweet.Id, enriched.Id], id => id == sweet.Id ? sweet : enriched));

        Assert.IsTrue(result.IsResolved, string.Join(", ", result.Issues.Select(x => x.Code)));
        // 800 g de masa dulce para conchas + 720 g dentro de 800 g de masa enriquecida = 1,520 g, calculada una sola vez.
        Assert.AreEqual(G(1520).AtomicUnits, result.RequiredFormulas.Single(x => x.FormulaVersionId == sweet.Id).Required.AtomicUnits);
        Assert.AreEqual(G(760).AtomicUnits, Total(result.Plan!, 1));
        Assert.AreEqual(G(760).AtomicUnits, Total(result.Plan!, 2));
        Assert.AreEqual(G(80).AtomicUnits, Total(result.Plan!, 3), "The butter of the enriched dough is counted once, not again inside the sweet dough.");
        Assert.AreEqual(2, result.Plan!.Formulas.Count);
    }

    [TestMethod]
    public void ACinnamonRollBatchIsMadeInWholeBatchesWithoutRoundingTheDoughPerRoll()
    {
        var sweet = Published(100, G(1000), [Ingredient(1, G(500))]);
        var filling = Published(300, G(140), [Ingredient(5, G(140))]);
        var batch = Published(400, Piece(13), [], [new(sweet.Id, G(1100)), new(filling.Id, G(140))]);
        var versions = ProductionPlan.RequiredVersions([batch.Id], id => new[] { sweet, filling, batch }.Single(x => x.Id == id));

        var thirteen = ProductionPlan.Calculate([Output(1000, batch, Piece(1), 13)], versions);
        Assert.IsTrue(thirteen.IsResolved);
        Assert.AreEqual(G(550).AtomicUnits, Total(thirteen.Plan!, 1));
        Assert.AreEqual(G(140).AtomicUnits, Total(thirteen.Plan!, 5));
        Assert.IsTrue(ProductionPlan.Calculate([Output(1000, batch, Piece(1), 26)], versions).IsResolved);

        var ten = ProductionPlan.Calculate([Output(1000, batch, Piece(1), 10)], versions);
        Assert.IsFalse(ten.IsResolved, "1,100 g × 10 / 13 is not an exact amount; it is not rounded per roll.");
        Assert.IsTrue(ten.Issues.Any(x => x.Code == ProductionError.NonRepresentableScale && x.SubjectId == sweet.Id && x.FormulaVersionId == batch.Id));
    }

    [TestMethod]
    public void APreparationCannotUseItselfDirectlyOrThroughAnotherPreparation()
    {
        Assert.AreEqual(ProductionError.PreparationCycle, Assert.ThrowsExactly<ProductionDomainException>(() =>
            new FormulaVersion(Id(101), Id(100), 1, "Masa", G(1000), [], [], At, null, [new(Id(101), G(10))])).Code);
        var first = Published(100, G(1000), [Ingredient(1, G(10))], [new(Id(201), G(10))]);
        var second = Published(200, G(1000), [Ingredient(2, G(10))], [new(first.Id, G(10))]);
        var result = ProductionPlan.Calculate([Output(1000, first, G(10), 1)], [first, second]);
        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual(ProductionError.PreparationCycle, result.Issues.Single().Code);
    }

    [TestMethod]
    public void AnUnpublishedOrRepeatedPreparationIsNotUsedToProduce()
    {
        var draftSweet = Draft(100, G(1000), [Ingredient(1, G(500))]);
        var enriched = Published(200, G(1000), [Ingredient(3, G(100))], [new(draftSweet.Id, G(900))]);
        var result = ProductionPlan.Calculate([Output(1000, enriched, G(80), 10)], [enriched, draftSweet]);
        Assert.IsFalse(result.IsResolved);
        Assert.AreEqual(ProductionError.InvalidVersion, result.Issues.Single().Code);
        Assert.AreEqual(ProductionError.DuplicateIngredient, Assert.ThrowsExactly<ProductionDomainException>(() =>
            Draft(300, G(1000), [], [new(draftSweet.Id, G(1)), new(draftSweet.Id, G(2))])).Code);
    }

    [TestMethod]
    public void RequiredVersionsBringsTheNestedPreparationsOnce()
    {
        var sweet = Published(100, G(1000), [Ingredient(1, G(500))]);
        var enriched = Published(200, G(1000), [Ingredient(3, G(100))], [new(sweet.Id, G(900))]);
        var loads = new List<Guid>();
        var versions = ProductionPlan.RequiredVersions([enriched.Id, sweet.Id, enriched.Id], id => { loads.Add(id); return id == sweet.Id ? sweet : enriched; });
        CollectionAssert.AreEquivalent(new[] { sweet.Id, enriched.Id }, versions.Select(x => x.Id).ToArray());
        Assert.AreEqual(2, loads.Count);
    }
}
