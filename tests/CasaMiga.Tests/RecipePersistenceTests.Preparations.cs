using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>Preparaciones dentro de preparaciones guardadas en SQLite (migración 031), con cantidades de prueba.</summary>
public sealed partial class RecipePersistenceTests
{
    private (Formula Owner, FormulaVersion Version) Enriched(InventoryItem butter, long preparationAtomic = 50)
    {
        var owner = new Formula(Guid.NewGuid(), "ENRICHED"); recipes.CreateFormula(owner, "Enriched", at);
        var value = new FormulaVersion(Guid.NewGuid(), owner.Id, 1, "Enriched v1", new(InventoryDimension.Mass, 100),
            [new(butter.Id, new(InventoryDimension.Mass, 50), IngredientCountPolicy.NotApplicable)], [], at, null,
            [new(version.Id, new(InventoryDimension.Mass, preparationAtomic))]);
        recipes.SaveFormulaDraft(value);
        return (owner, value);
    }

    [TestMethod] public void APreparationInsideAnotherIsSavedAndPublishedOnlyAfterTheOneItUses()
    {
        var butter = Item("BUTTER", InventoryDimension.Mass);
        var (owner, enriched) = Enriched(butter);
        var read = recipes.GetFormulaVersion(enriched.Id)!.Value;
        CollectionAssert.AreEqual(enriched.Preparations.ToArray(), read.Preparations.ToArray());

        var error = Assert.ThrowsExactly<ArgumentException>(() => recipes.PublishFormula(enriched.Id, 1, at.AddMinutes(1)));
        StringAssert.Contains(error.Message, "Shared · versión 1");
        Assert.ThrowsExactly<SqliteException>(() => Sql($"UPDATE formula_version SET state='PUBLISHED',published_utc='{at:O}' WHERE id='{enriched.Id}'"),
            "The database itself refuses to publish a recipe whose preparation is still a draft.");

        recipes.PublishFormula(version.Id, 1, at.AddMinutes(1));
        recipes.PublishFormula(enriched.Id, 1, at.AddMinutes(2));
        Assert.AreEqual(ProductionVersionState.Published, recipes.GetFormulaVersion(enriched.Id)!.Value.State);
        Assert.ThrowsExactly<SqliteException>(() => Sql($"DELETE FROM formula_preparation WHERE version_id='{enriched.Id}'"), "Published preparations are sealed.");
        Assert.ThrowsExactly<SqliteException>(() => Sql($"UPDATE formula_preparation SET quantity_atomic=1 WHERE version_id='{enriched.Id}'"));
        Assert.IsTrue((long)Sql($"SELECT count(*) FROM sync_outbox WHERE entity='formula' AND entity_id='{owner.Id}'")! >= 2, "Its preparations travel to the cloud with the recipe.");
        db.Validate();
    }

    [TestMethod] public void ADraftEditReplacesItsPreparationsAndAPreparationMustMatchItsYieldDimension()
    {
        var butter = Item("BUTTER", InventoryDimension.Mass);
        var (owner, enriched) = Enriched(butter);
        var edited = new FormulaVersion(enriched.Id, owner.Id, 1, "Enriched v1", enriched.BaseYield, enriched.Ingredients, [], at, null,
            [new(version.Id, new(InventoryDimension.Mass, 60))]);
        recipes.SaveFormulaDraft(edited, 1);
        Assert.AreEqual(60L, recipes.GetFormulaVersion(enriched.Id)!.Value.Preparations.Single().Quantity.AtomicUnits);
        var wrongDimension = new FormulaVersion(enriched.Id, owner.Id, 1, "Enriched v1", enriched.BaseYield, enriched.Ingredients, [], at, null,
            [new(version.Id, new(InventoryDimension.Volume, 60))]);
        Assert.ThrowsExactly<SqliteException>(() => recipes.SaveFormulaDraft(wrongDimension, 2));
        Assert.AreEqual(60L, recipes.GetFormulaVersion(enriched.Id)!.Value.Preparations.Single().Quantity.AtomicUnits, "A rejected edit keeps the previous preparations.");
    }

    [TestMethod] public void AProductionDraftConsumesTheIngredientsOfTheInnerPreparationOnce()
    {
        var butter = Item("BUTTER", InventoryDimension.Mass);
        var (_, enriched) = Enriched(butter);
        recipes.PublishFormula(version.Id, 1, at.AddMinutes(1));
        recipes.PublishFormula(enriched.Id, 1, at.AddMinutes(1));
        var cuernito = new ProductProductionProfileVersion(Guid.NewGuid(), profile.Id, 2, product.Id, finished.Id, "Profile v2",
            [new(enriched.Id, new(InventoryDimension.Mass, 100))], [], at);
        recipes.SaveProfileDraft(cuernito); recipes.PublishProfile(cuernito.Id, 1, at.AddMinutes(1));

        var id = Guid.NewGuid();
        recipes.CreateProductionDraft(id, Guid.NewGuid(), user.Current!.Id, terminal.Id, at.AddMinutes(2), [new(recipes.GetProfileVersion(cuernito.Id)!.Value, 2)]);
        var batch = recipes.GetProductionBatch(id)!;
        Assert.AreEqual("[]", batch.PlanningIssuesJson);
        // 2 piezas × 100 mg de masa enriquecida = 200 mg → 100 mg de butter y 100 mg de la masa de la prueba (rinde 100 mg).
        Assert.AreEqual(100L, batch.Inputs.Single(x => x.ItemId == butter.Id).Required.AtomicUnits);
        Assert.AreEqual(60L, batch.Inputs.Single(x => x.ItemId == flour.Id).Required.AtomicUnits);
        Assert.AreEqual(20L, batch.Inputs.Single(x => x.ItemId == water.Id).Required.AtomicUnits);
        Assert.AreEqual(version.Id, batch.Inputs.Single(x => x.ItemId == flour.Id).FormulaVersionId);
        CollectionAssert.AreEquivalent(new[] { version.Id, enriched.Id }, batch.Formulas.Select(x => x.FormulaVersionId).ToArray());
        Assert.AreEqual(1, batch.Inputs.Count(x => x.ItemId == flour.Id), "The flour of the inner dough appears once.");
    }
}
