using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>Recetas desde la web como la pantalla de la caja (docs/PLAN_COMPLETAR.md, W5): crear, borradores, publicar y activar.</summary>
[TestClass]
public sealed class CloudRecipeCommandTests : SalesFixture
{
    private long sequence;
    private CloudCommandService Commands => new(work, clock, terminal.Id, TimeZoneInfo.Utc,
        new CloudCommandModules(new SqliteInventoryStore(db, clock), work, new SqliteProductionUnitOfWork(db), _ => throw new NotSupportedException()));
    private CloudCommand Command(string kind, JsonObject payload, Guid? by = null) => new(Guid.NewGuid(), ++sequence, kind, payload, by ?? user.Current!.Id, "Administrador web");
    private static void Ok(CloudCommandResult result) => Assert.IsTrue(result.Applied, result.Message);
    private RecipeService Recipes => new(new SqliteProductionUnitOfWork(db), user, clock, terminal.Id);
    private long Count(string sql) => Convert.ToInt64(Sql(sql));
    private InventoryService Inventory => new(new SqliteInventoryStore(db, clock), work, user, clock, terminal.Id);
    private static JsonObject Line(Guid item, long atomic, string policy = "NotApplicable") => new() { ["itemId"] = item.ToString(), ["atomic"] = atomic, ["policy"] = policy };

    [TestMethod]
    public void FormulasFromTheWebGoThroughDraftPublishAndActivate()
    {
        var flour = Inventory.CreateInventoryItem("HAR", "Harina", InventoryItemKind.RawMaterial, InventoryDimension.Mass).Id;
        var butter = Inventory.CreateInventoryItem("MAN", "Mantequilla", InventoryItemKind.RawMaterial, InventoryDimension.Mass).Id;
        var margarine = Inventory.CreateInventoryItem("MAR", "Margarina", InventoryItemKind.RawMaterial, InventoryDimension.Mass).Id;
        var eggs = Inventory.CreateInventoryItem("HUE", "Huevo", InventoryItemKind.RawMaterial, InventoryDimension.Count).Id;
        var formula = Guid.NewGuid();
        var create = Command("recipe.formula.create", new() { ["id"] = formula.ToString(), ["code"] = "masa-dulce", ["name"] = "Masa dulce" });
        Ok(Commands.Apply(create));
        Ok(Commands.Apply(Command("recipe.formula.create", new() { ["id"] = formula.ToString(), ["code"] = "masa-dulce", ["name"] = "Masa dulce" })));
        Assert.AreEqual("MASA-DULCE", Recipes.GetFormula(formula)!.Code, "Same code rules as the screen; sending it again creates nothing.");
        Assert.AreEqual("Ya existe una receta con ese código.", Commands.Apply(Command("recipe.formula.create", new() { ["id"] = Guid.NewGuid().ToString(), ["code"] = "Masa-Dulce", ["name"] = "Otra" })).Message);

        var version = Guid.NewGuid();
        JsonObject Draft(long? revision, long flourAtomic) => new()
        {
            ["formulaId"] = formula.ToString(), ["versionId"] = version.ToString(), ["revision"] = revision, ["name"] = "Base", ["notes"] = "Prueba",
            ["yield"] = new JsonObject { ["dimension"] = "Mass", ["atomic"] = 1_000_000 },
            ["ingredients"] = new JsonArray(Line(flour, flourAtomic), Line(eggs, 3, "ExactWholeCount")),
            ["groups"] = new JsonArray(new JsonArray(
                new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["itemId"] = butter.ToString(), ["atomic"] = 100_000, ["policy"] = "NotApplicable" },
                new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["itemId"] = margarine.ToString(), ["atomic"] = 120_000, ["policy"] = "NotApplicable" })),
        };
        Ok(Commands.Apply(Command("recipe.formula.draft", Draft(null, 500_000))));
        var stored = Recipes.GetFormulaVersion(version)!;
        Assert.AreEqual(1, stored.Value.Version, "The till numbers the version.");
        Assert.AreEqual(2, stored.Value.Alternatives.Single().Options.Count);
        Assert.AreEqual(butter, stored.Value.Alternatives.Single().Options.Single(o => o.Id == stored.Value.Alternatives.Single().PreferenceOrder[0]).Ingredient.InventoryItemId, "Option 1 is the first preference.");

        Ok(Commands.Apply(Command("recipe.formula.draft", Draft(stored.Revision, 600_000))));
        Assert.AreEqual(600_000L, Recipes.GetFormulaVersion(version)!.Value.Ingredients.Single(i => i.InventoryItemId == flour).Quantity.AtomicUnits);
        Assert.AreEqual("La receta cambió. Actualiza la información antes de continuar.", Commands.Apply(Command("recipe.formula.draft", Draft(stored.Revision, 700_000))).Message,
            "An old revision is refused like on the screen.");
        var duplicated = Draft(null, 1); duplicated["versionId"] = Guid.NewGuid().ToString(); duplicated["ingredients"] = new JsonArray(Line(flour, 1), Line(flour, 2));
        Assert.AreEqual("Un mismo artículo no puede repetirse en la receta.", Commands.Apply(Command("recipe.formula.draft", duplicated)).Message);
        var piecesAsMass = Draft(null, 1); piecesAsMass["versionId"] = Guid.NewGuid().ToString(); piecesAsMass["ingredients"] = new JsonArray(Line(eggs, 2));
        Assert.AreEqual("rejected", Commands.Apply(Command("recipe.formula.draft", piecesAsMass)).Status, "Pieces need a piece policy.");

        Assert.AreEqual("Publica la versión antes de activarla.", Commands.Apply(Command("recipe.formula.activate", new() { ["formulaId"] = formula.ToString(), ["versionId"] = version.ToString(), ["revision"] = 1 })).Message);
        var revision = Recipes.GetFormulaVersion(version)!.Revision;
        Ok(Commands.Apply(Command("recipe.formula.publish", new() { ["versionId"] = version.ToString(), ["revision"] = revision })));
        Ok(Commands.Apply(Command("recipe.formula.publish", new() { ["versionId"] = version.ToString(), ["revision"] = revision })));
        Assert.AreEqual(ProductionVersionState.Published, Recipes.GetFormulaVersion(version)!.Value.State);
        Assert.AreEqual(1L, Count($"SELECT count(*) FROM audit_entry WHERE action='recipe.formula.published' AND entity_id='{version}'"), "Publishing twice publishes once.");
        Ok(Commands.Apply(Command("recipe.formula.activate", new() { ["formulaId"] = formula.ToString(), ["versionId"] = version.ToString(), ["revision"] = Recipes.GetFormula(formula)!.Revision })));
        Assert.AreEqual(version, Recipes.GetFormula(formula)!.ActiveVersionId);
        Assert.AreEqual("rejected", Commands.Apply(Command("recipe.formula.draft", Draft(Recipes.GetFormulaVersion(version)!.Revision, 1))).Status, "A published version is never edited.");
    }

    [TestMethod]
    public void ProductProfilesFromTheWebUsePublishedPreparations()
    {
        var flour = Inventory.CreateInventoryItem("HAR", "Harina", InventoryItemKind.RawMaterial, InventoryDimension.Mass).Id;
        var sugar = Inventory.CreateInventoryItem("AZU", "Azúcar", InventoryItemKind.RawMaterial, InventoryDimension.Mass).Id;
        var finished = Inventory.CreateInventoryItem("PT-CON", "Concha terminada", InventoryItemKind.FinishedGood, InventoryDimension.Count).Id;
        var formula = Guid.NewGuid(); var draft = Guid.NewGuid();
        Recipes.CreateFormula(new Formula(formula, "MASA"), "Masa");
        Recipes.SaveFormulaDraft(new FormulaVersion(draft, formula, 1, "Base", new(InventoryDimension.Mass, 1_000_000), [new FormulaIngredient(flour, new(InventoryDimension.Mass, 600_000), IngredientCountPolicy.NotApplicable)], [], clock.UtcNow));

        Assert.AreEqual("El artículo terminado debe ser un Producto terminado activo que se cuenta en piezas.",
            Commands.Apply(Command("recipe.profile.create", new() { ["id"] = Guid.NewGuid().ToString(), ["productId"] = product.Id.ToString(), ["finishedItemId"] = flour.ToString() })).Message);
        var profile = Guid.NewGuid();
        Ok(Commands.Apply(Command("recipe.profile.create", new() { ["id"] = profile.ToString(), ["productId"] = product.Id.ToString(), ["finishedItemId"] = finished.ToString() })));
        Assert.AreEqual("Ese producto ya tiene un perfil productivo.",
            Commands.Apply(Command("recipe.profile.create", new() { ["id"] = Guid.NewGuid().ToString(), ["productId"] = product.Id.ToString(), ["finishedItemId"] = finished.ToString() })).Message);

        var version = Guid.NewGuid();
        JsonObject Draft() => new()
        {
            ["profileId"] = profile.ToString(), ["versionId"] = version.ToString(), ["revision"] = null, ["name"] = "Concha v1",
            ["formulas"] = new JsonArray(new JsonObject { ["formulaVersionId"] = draft.ToString(), ["atomic"] = 60_000 }),
            ["direct"] = new JsonArray(Line(sugar, 15_000)),
        };
        Assert.AreEqual("Una preparación ya no está publicada.", Commands.Apply(Command("recipe.profile.draft", Draft())).Message, "Only published preparations, as on the screen.");
        Recipes.PublishFormula(draft, Recipes.GetFormulaVersion(draft)!.Revision);
        Ok(Commands.Apply(Command("recipe.profile.draft", Draft())));
        var stored = Recipes.GetProfileVersion(version)!;
        Assert.AreEqual(1, stored.Value.Version);
        Assert.AreEqual(60_000L, stored.Value.Formulas.Single().QuantityPerPiece.AtomicUnits);
        Ok(Commands.Apply(Command("recipe.profile.publish", new() { ["versionId"] = version.ToString(), ["revision"] = stored.Revision })));
        Ok(Commands.Apply(Command("recipe.profile.activate", new() { ["profileId"] = profile.ToString(), ["versionId"] = version.ToString(), ["revision"] = Recipes.GetProfile(profile)!.Revision })));
        Assert.AreEqual(version, Recipes.GetProfile(profile)!.ActiveVersionId, "The product can now be produced from the web.");
    }

    [TestMethod]
    public void AManagerCannotConfigureRecipesFromTheWeb()
    {
        var identity = new IdentityService(work, new LocalPasswords(), clock, user);
        var temporary = identity.CreateUser("ana", "Ana", "Manager");
        identity.ChangePassword("ana", temporary, "Ana#2026centro");
        Assert.IsTrue(identity.Login("admin", password));
        var ana = work.Run(s => s.FindUser("ANA"))!.Id;
        Assert.AreEqual("rejected", Commands.Apply(Command("recipe.formula.create", new() { ["id"] = Guid.NewGuid().ToString(), ["code"] = "X", ["name"] = "X" }, ana)).Status);
        Assert.AreEqual(0L, Count("SELECT count(*) FROM formula"));
    }
}
