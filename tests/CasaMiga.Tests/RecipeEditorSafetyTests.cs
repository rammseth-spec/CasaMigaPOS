using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>
/// Dos riesgos del editor de Recetas, con datos de prueba: (A) un rendimiento que cambia más de 10 veces pide confirmación antes de
/// guardarse (el error típico es escribir gramos con la unidad en kg); (B) «Por medir» impide publicar una versión con datos sin confirmar.
/// </summary>
public sealed partial class RecipePersistenceTests
{
    private static readonly InventoryQuantity Grams1800 = new(InventoryDimension.Mass, 1_800_000);

    // Masa de prueba guardada con un rendimiento y abierta otra vez para editar su borrador.
    private async Task<RecipeViewModel> SavedDough(string yield, string unit, List<string> asked, bool answer = false)
    {
        var vm = RecipeVm(); vm.ConfirmYieldChange = text => { asked.Add(text); return answer; };
        await vm.LoadAsync(); vm.NewFormula(); vm.Code = "PRUEBA-RIESGO"; vm.Name = "Masa de prueba"; await vm.CreateFormulaAsync();
        vm.NewFormulaVersion(); vm.Name = "Tanda"; vm.Yield = yield; vm.YieldUnit = unit;
        vm.AddIngredient(false); vm.Ingredients[0].Item = vm.Items.Single(x => x.Id == flour.Id); vm.Ingredients[0].Quantity = "900"; vm.Ingredients[0].Unit = "g";
        await vm.SaveFormulaVersionAsync(); Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        Assert.IsEmpty(asked, "The first version has no previous yield to compare with.");
        vm.SelectedVersion = vm.Versions.Single(); await vm.EditFormulaVersionAsync(); Assert.IsTrue(vm.IsFormulaEditor, vm.Message);
        return vm;
    }
    private StoredVersion<FormulaVersion> Stored(RecipeViewModel vm) => RecipeApplication().GetFormulaVersion(vm.Versions.Single().Id)!;

    [TestMethod] public async Task AYieldChangeOfTenTimesOrLessSavesWithoutAsking()
    {
        var asked = new List<string>(); var vm = await SavedDough("1800", "g", asked);
        Assert.AreEqual("1.8", vm.Yield); Assert.AreEqual("kg", vm.YieldUnit);
        vm.Yield = "2000"; vm.YieldUnit = "g"; await vm.SaveFormulaVersionAsync();
        Assert.IsTrue(vm.MessageIsSuccess, vm.Message); Assert.IsEmpty(asked, "1,800 g → 2,000 g is not suspicious.");
        Assert.AreEqual(2_000_000L, Stored(vm).Value.BaseYield.AtomicUnits);
        Assert.IsNull(RecipeYieldCheck.Warning(Grams1800, new(InventoryDimension.Mass, 18_000_000)), "Exactly 10 times is still allowed without asking.");
        Assert.IsNull(RecipeYieldCheck.Warning(Grams1800, new(InventoryDimension.Mass, 180_000)), "Exactly 10 times smaller too.");
    }

    [TestMethod] public async Task AYieldChangeOfMoreThanTenTimesAsksAndCancellingSavesNothing()
    {
        var asked = new List<string>(); var vm = await SavedDough("1800", "g", asked, answer: false);
        var before = Stored(vm);
        vm.Yield = "2000"; Assert.AreEqual("kg", vm.YieldUnit, "The unit stayed in kg: the classic slip."); await vm.SaveFormulaVersionAsync();
        Assert.HasCount(1, asked); StringAssert.Contains(asked[0], "Antes: 1,800 g"); StringAssert.Contains(asked[0], "Ahora: 2,000,000 g");
        StringAssert.Contains(asked[0], "error de unidad");
        Assert.IsFalse(vm.MessageIsSuccess); StringAssert.Contains(vm.Message, "No se guardó");
        var after = Stored(vm);
        Assert.AreEqual(1_800_000L, after.Value.BaseYield.AtomicUnits, "Cancelling keeps the stored yield.");
        Assert.AreEqual(before.Revision, after.Revision, "Nothing was written.");
        Assert.IsTrue(vm.IsFormulaEditor, "The editor stays open to fix the value.");
    }

    [TestMethod] public async Task ConfirmingASuspiciousYieldSavesExactlyWhatWasTyped()
    {
        var asked = new List<string>(); var vm = await SavedDough("1800", "g", asked, answer: true);
        vm.Yield = "2000"; await vm.SaveFormulaVersionAsync();
        Assert.HasCount(1, asked); Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        Assert.AreEqual(2_000_000_000L, Stored(vm).Value.BaseYield.AtomicUnits, "2,000 kg as typed; the value is never corrected.");
    }

    [TestMethod] public async Task GramsAndKilogramsAreComparedInTheSameUnit()
    {
        var asked = new List<string>(); var vm = await SavedDough("1800", "g", asked, answer: false);
        vm.Yield = "2"; vm.YieldUnit = "kg"; await vm.SaveFormulaVersionAsync();
        Assert.IsEmpty(asked, "1,800 g → 2 kg is 2,000 g: no warning."); Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        vm.SelectedVersion = vm.Versions.Single(); await vm.EditFormulaVersionAsync();
        vm.Yield = "2000000"; vm.YieldUnit = "g"; await vm.SaveFormulaVersionAsync();
        Assert.HasCount(1, asked, "2,000 g → 2,000,000 g asks."); StringAssert.Contains(asked[0], "Antes: 2,000 g"); StringAssert.Contains(asked[0], "unas 1,000 veces más");
        Assert.AreEqual(2_000_000L, Stored(vm).Value.BaseYield.AtomicUnits);

        // Una versión nueva se compara con la última versión de la receta.
        vm.Back(); vm.NewFormulaVersion(); vm.Name = "Tanda 2"; vm.Yield = "2000"; vm.YieldUnit = "kg";
        vm.AddIngredient(false); vm.Ingredients[0].Item = vm.Items.Single(x => x.Id == flour.Id); vm.Ingredients[0].Quantity = "900"; vm.Ingredients[0].Unit = "g";
        await vm.SaveFormulaVersionAsync();
        Assert.HasCount(2, asked); Assert.HasCount(1, vm.Versions, "The new version was not created.");
        StringAssert.Contains(RecipeYieldCheck.Warning(new(InventoryDimension.Volume, 1_000_000), new(InventoryDimension.Volume, 50_000))!, "Ahora: 50 ml (unas 20 veces menos)");
    }

    [TestMethod] public async Task MassVolumeAndPiecesAreNeverComparedByAmount()
    {
        foreach (var next in new InventoryQuantity[] { new(InventoryDimension.Volume, 2_000_000_000), new(InventoryDimension.Count, 18), new(InventoryDimension.Volume, 1_800_000) })
        {
            var text = RecipeYieldCheck.Warning(Grams1800, next)!;
            StringAssert.Contains(text, "no se comparan"); Assert.DoesNotContain("veces", text, "No amount comparison between kinds.");
        }
        StringAssert.Contains(RecipeYieldCheck.Warning(Grams1800, new(InventoryDimension.Count, 18))!, "ahora piezas (18 piezas)");
        Assert.IsNull(RecipeYieldCheck.Warning(new(InventoryDimension.Count, 2), new(InventoryDimension.Count, 18)), "9 times more pieces: no warning.");
        Assert.IsNotNull(RecipeYieldCheck.Warning(new(InventoryDimension.Count, 1), new(InventoryDimension.Count, 13)));

        // Cambiar el tipo en el editor pide confirmación con ese texto; cancelar no guarda.
        var asked = new List<string>(); var vm = await SavedDough("1800", "g", asked, answer: false);
        vm.YieldDimension = InventoryDimension.Volume; vm.Yield = "1800"; vm.YieldUnit = "ml"; await vm.SaveFormulaVersionAsync();
        Assert.HasCount(1, asked); StringAssert.Contains(asked[0], "Cambiaste el tipo de rendimiento");
        Assert.AreEqual(InventoryDimension.Mass, Stored(vm).Value.BaseYield.Dimension);

        // Una preparación que ya usa otra receta no puede cambiar de tipo: se explica en vez de un error técnico.
        Enriched(Item("BUTTER", InventoryDimension.Mass));
        var changed = new FormulaVersion(version.Id, formula.Id, 1, version.Name, new(InventoryDimension.Volume, 100), version.Ingredients, version.Alternatives, at, version.Notes);
        var error = Assert.ThrowsExactly<ArgumentException>(() => recipes.SaveFormulaDraft(changed, 1));
        StringAssert.Contains(error.Message, "ya la usan otras recetas");
    }

    [TestMethod] public async Task InvalidOrNonPositiveYieldsAreRefusedWithAMessageAndSaveNothing()
    {
        var asked = new List<string>(); var vm = await SavedDough("1800", "g", asked, answer: true);
        var before = Stored(vm);
        foreach (var bad in new[] { "abc", "1,800", "", "0", "-5" })
        {
            vm.Yield = bad; vm.YieldUnit = "g";
            Assert.IsFalse(vm.CanSaveFormulaDraft, $"«{bad}» keeps the save button off.");
            await vm.SaveFormulaVersionAsync();
            Assert.IsFalse(vm.MessageIsSuccess, bad); StringAssert.Contains(vm.Message, "mayor que cero", bad);
        }
        Assert.IsEmpty(asked, "An invalid value is refused before any comparison.");
        Assert.AreEqual(before.Revision, Stored(vm).Revision); Assert.AreEqual(1_800_000L, Stored(vm).Value.BaseYield.AtomicUnits);
        Assert.IsNull(RecipeYieldCheck.Warning(null, Grams1800), "Without a previous yield nothing is compared.");
        Assert.IsNull(RecipeYieldCheck.Warning(default(InventoryQuantity), Grams1800), "A non-positive previous yield is not a valid reference.");
        Assert.ThrowsExactly<ProductionDomainException>(() => new FormulaVersion(Guid.NewGuid(), formula.Id, 9, "Cero", new(InventoryDimension.Mass, 0), version.Ingredients, [], at));
    }

    [TestMethod] public async Task AYieldAboveTheCasaMigaMaximumAsksEvenOnTheFirstVersion()
    {
        Assert.IsNull(RecipeYieldCheck.Warning(null, new(InventoryDimension.Mass, 20_000_000)), "Exactly 20 kg is allowed.");
        StringAssert.Contains(RecipeYieldCheck.Warning(null, new(InventoryDimension.Mass, 20_001_000))!, "máximo que rinde una receta en Casa Miga: 20,000 g");
        Assert.IsNull(RecipeYieldCheck.Warning(null, new(InventoryDimension.Volume, 50_000_000)), "Exactly 50 L is allowed.");
        StringAssert.Contains(RecipeYieldCheck.Warning(null, new(InventoryDimension.Volume, 50_001_000))!, "50,000 ml");
        Assert.IsNull(RecipeYieldCheck.Warning(null, new(InventoryDimension.Count, 500)), "Exactly 500 pieces is allowed.");
        StringAssert.Contains(RecipeYieldCheck.Warning(null, new(InventoryDimension.Count, 501))!, "500 piezas");
        var both = RecipeYieldCheck.Warning(Grams1800, new(InventoryDimension.Mass, 2_000_000_000))!;
        StringAssert.Contains(both, "máximo"); StringAssert.Contains(both, "Antes: 1,800 g"); StringAssert.Contains(both, "¿Guardar de todos modos?");
        Assert.HasCount(2, both.Split("¿Guardar"), "One question for both reasons.");

        // Primera versión de una receta: no hay con qué comparar, pero el máximo sí avisa; cancelar no crea nada y confirmar guarda lo escrito.
        var asked = new List<string>(); var answer = false;
        var vm = RecipeVm(); vm.ConfirmYieldChange = text => { asked.Add(text); return answer; };
        await vm.LoadAsync(); vm.NewFormula(); vm.Code = "PRUEBA-MAXIMO"; vm.Name = "Máximo de prueba"; await vm.CreateFormulaAsync();
        vm.NewFormulaVersion(); vm.Name = "Tanda"; vm.Yield = "25"; vm.YieldUnit = "kg";
        vm.AddIngredient(false); vm.Ingredients[0].Item = vm.Items.Single(x => x.Id == flour.Id); vm.Ingredients[0].Quantity = "900"; vm.Ingredients[0].Unit = "g";
        await vm.SaveFormulaVersionAsync();
        Assert.HasCount(1, asked); StringAssert.Contains(asked[0], "25,000 g"); Assert.IsEmpty(vm.Versions, "Cancelling creates nothing.");
        answer = true; await vm.SaveFormulaVersionAsync();
        Assert.IsTrue(vm.MessageIsSuccess, vm.Message); Assert.AreEqual(25_000_000L, Stored(vm).Value.BaseYield.AtomicUnits, "25 kg as typed.");
    }

    [TestMethod] public async Task PorMedirIsSavedLoadedAndShownInTheEditorAndTheLists()
    {
        var asked = new List<string>(); var vm = await SavedDough("1800", "g", asked);
        Assert.IsFalse(vm.PendingMeasure);
        vm.PendingMeasure = true; await vm.SaveFormulaVersionAsync(); Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        Assert.IsTrue(Stored(vm).Value.PendingMeasure);
        Assert.AreEqual("Borrador · Por medir", vm.Versions.Single().DisplayState); Assert.AreEqual("Borrador", vm.Versions.Single().State);
        Assert.AreEqual("Sí", vm.Formulas.Single(x => x.Code == "PRUEBA-RIESGO").PendingMeasure);
        Assert.AreEqual("No", vm.Formulas.Single(x => x.Code == "SHARED").PendingMeasure);
        vm.SelectedVersion = vm.Versions.Single(); await vm.EditFormulaVersionAsync(); Assert.IsTrue(vm.PendingMeasure, "The editor shows the mark again.");
        vm.Back(); vm.NewFormulaVersion(); Assert.IsFalse(vm.PendingMeasure, "A new version starts unmarked; the mark is chosen explicitly.");
        Assert.AreEqual(1L, Sql($"SELECT pending_measure FROM formula_version WHERE id='{Stored(vm).Value.Id}'"));
    }

    [TestMethod] public async Task PorMedirBlocksPublishingFromTheScreenWithTheReason()
    {
        var asked = new List<string>(); var vm = await SavedDough("1800", "g", asked);
        vm.PendingMeasure = true; await vm.SaveFormulaVersionAsync();
        vm.SelectedVersion = vm.Versions.Single(); await vm.PublishAsync();
        Assert.IsFalse(vm.MessageIsSuccess); StringAssert.Contains(vm.Message, "marcada «Por medir»"); StringAssert.Contains(vm.Message, "quita la marca");
        Assert.AreEqual(ProductionVersionState.Draft, Stored(vm).Value.State);
    }

    [TestMethod] public void PorMedirIsEnforcedByTheDomainTheServiceAndTheDatabase()
    {
        var marked = new FormulaVersion(version.Id, formula.Id, 1, version.Name, version.BaseYield, version.Ingredients, version.Alternatives, at, version.Notes, null, pendingMeasure: true);
        var domain = Assert.ThrowsExactly<ProductionDomainException>(() => new Formula(formula.Id, "SHARED").PublishVersion(marked, at.AddMinutes(1)));
        Assert.AreEqual(ProductionError.PendingMeasure, domain.Code);
        recipes.SaveFormulaDraft(marked, 1);
        var revision = recipes.GetFormulaVersion(version.Id)!.Revision;
        var service = Assert.ThrowsExactly<ProductionDomainException>(() => RecipeApplication().PublishFormula(version.Id, revision), "The application refuses it without any screen.");
        StringAssert.Contains(RecipeProblems.Describe(service), "«Por medir»");
        Assert.ThrowsExactly<ProductionDomainException>(() => recipes.PublishFormula(version.Id, revision, at.AddMinutes(1)));
        Assert.ThrowsExactly<SqliteException>(() => Sql($"UPDATE formula_version SET state='PUBLISHED',published_utc='{at.AddMinutes(1):O}' WHERE id='{version.Id}'"),
            "The database itself refuses it.");
        Assert.AreEqual(ProductionVersionState.Draft, recipes.GetFormulaVersion(version.Id)!.Value.State);
        Assert.AreEqual(0L, Sql($"SELECT count(*) FROM audit_entry WHERE action='recipe.formula.published' AND entity_id='{version.Id}'"));
    }

    [TestMethod] public async Task UnmarkingPorMedirAllowsPublishingButNeverPublishes()
    {
        var asked = new List<string>(); var vm = await SavedDough("1800", "g", asked);
        vm.PendingMeasure = true; await vm.SaveFormulaVersionAsync();
        vm.SelectedVersion = vm.Versions.Single(); await vm.EditFormulaVersionAsync(); vm.PendingMeasure = false; await vm.SaveFormulaVersionAsync();
        Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        var unmarked = Stored(vm).Value;
        Assert.IsFalse(unmarked.PendingMeasure); Assert.AreEqual(ProductionVersionState.Draft, unmarked.State, "Unmarking does not publish.");
        Assert.IsNull(Sql($"SELECT published_utc FROM formula_version WHERE id='{unmarked.Id}'") as string);
        Assert.AreEqual("Borrador", vm.Versions.Single().DisplayState);
        vm.SelectedVersion = vm.Versions.Single(); await vm.PublishAsync(); Assert.IsTrue(vm.MessageIsSuccess, vm.Message);
        Assert.AreEqual(ProductionVersionState.Published, Stored(vm).Value.State);
    }

    [TestMethod] public void PublishedVersionsStayImmutableAndNeverPorMedir()
    {
        recipes.PublishFormula(version.Id, 1, at.AddMinutes(1));
        Assert.IsFalse(recipes.GetFormulaVersion(version.Id)!.Value.PendingMeasure);
        Assert.ThrowsExactly<SqliteException>(() => Sql($"UPDATE formula_version SET pending_measure=1 WHERE id='{version.Id}'"), "Published versions are sealed.");
        var marked = new FormulaVersion(version.Id, formula.Id, 1, version.Name, version.BaseYield, version.Ingredients, version.Alternatives, at, version.Notes, null, pendingMeasure: true);
        Assert.ThrowsExactly<RecipeConcurrencyException>(() => recipes.SaveFormulaDraft(marked, recipes.GetFormulaVersion(version.Id)!.Revision), "A published version is never re-saved as a draft.");
        Assert.AreEqual(0L, Sql($"SELECT pending_measure FROM formula_version WHERE id='{version.Id}'"));
        Assert.AreEqual(ProductionVersionState.Published, recipes.GetFormulaVersion(version.Id)!.Value.State);
    }

    [TestMethod] public void NestedPreparationsKeepWorkingWithPorMedir()
    {
        var (_, enriched) = Enriched(Item("BUTTER", InventoryDimension.Mass));
        var innerMarked = new FormulaVersion(version.Id, formula.Id, 1, version.Name, version.BaseYield, version.Ingredients, version.Alternatives, at, version.Notes, null, pendingMeasure: true);
        recipes.SaveFormulaDraft(innerMarked, 1);
        Assert.AreEqual(ProductionError.PendingMeasure, Assert.ThrowsExactly<ProductionDomainException>(() => recipes.PublishFormula(version.Id, 2, at.AddMinutes(1))).Code);
        StringAssert.Contains(Assert.ThrowsExactly<ArgumentException>(() => recipes.PublishFormula(enriched.Id, 1, at.AddMinutes(1))).Message, "Publica primero las preparaciones",
            "The outer recipe still waits for the inner one.");

        recipes.SaveFormulaDraft(new FormulaVersion(version.Id, formula.Id, 1, version.Name, version.BaseYield, version.Ingredients, version.Alternatives, at, version.Notes), 2);
        recipes.PublishFormula(version.Id, 3, at.AddMinutes(1));
        var outerMarked = new FormulaVersion(enriched.Id, enriched.FormulaId, 1, enriched.Name, enriched.BaseYield, enriched.Ingredients, [], at, null, enriched.Preparations, pendingMeasure: true);
        recipes.SaveFormulaDraft(outerMarked, 1);
        Assert.IsTrue(recipes.GetFormulaVersion(enriched.Id)!.Value.PendingMeasure);
        CollectionAssert.AreEqual(enriched.Preparations.ToArray(), recipes.GetFormulaVersion(enriched.Id)!.Value.Preparations.ToArray(), "Marking keeps the preparations.");
        Assert.ThrowsExactly<ProductionDomainException>(() => recipes.PublishFormula(enriched.Id, 2, at.AddMinutes(2)), "A marked outer recipe is blocked even with its preparation published.");
        recipes.SaveFormulaDraft(new FormulaVersion(enriched.Id, enriched.FormulaId, 1, enriched.Name, enriched.BaseYield, enriched.Ingredients, [], at, null, enriched.Preparations), 2);
        recipes.PublishFormula(enriched.Id, 3, at.AddMinutes(2));
        Assert.AreEqual(ProductionVersionState.Published, recipes.GetFormulaVersion(enriched.Id)!.Value.State);
        db.Validate();
    }
}

/// <summary>La marca «Por medir» desde la web y al actualizar una base de la versión 31 (migración 032), con datos de prueba.</summary>
[TestClass]
public sealed class RecipePendingMeasureMigrationTests : SalesFixture
{
    protected override int? FixtureSchemaVersion => 31;
    private readonly DateTimeOffset at = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [TestMethod] public void ExistingDraftsStartPorMedirAndPublishedVersionsStayAsTheyWere()
    {
        var store = new SqliteRecipeStore(db);
        var inventory = new InventoryService(new SqliteInventoryStore(db, clock), work, user, clock, terminal.Id);
        var flour = inventory.CreateInventoryItem("PRUEBA-HARINA", "Harina de prueba", InventoryItemKind.RawMaterial, InventoryDimension.Mass);
        var formula = new Formula(Guid.NewGuid(), "PRUEBA"); store.CreateFormula(formula, "Prueba", at);
        FormulaVersion Version(int number) => new(Guid.NewGuid(), formula.Id, number, $"V{number}", new(InventoryDimension.Mass, 1_800_000),
            [new(flour.Id, new(InventoryDimension.Mass, 900_000), IngredientCountPolicy.NotApplicable)], [], at);
        var published = Version(1); store.SaveFormulaDraft(published); store.PublishFormula(published.Id, 1, at.AddMinutes(1));
        var draft = Version(2); store.SaveFormulaDraft(draft);
        Assert.IsFalse(store.GetFormulaVersion(draft.Id)!.Value.PendingMeasure, "Version 31 has no mark yet and still reads.");
        var revisions = (published: store.GetFormulaVersion(published.Id)!.Revision, draft: store.GetFormulaVersion(draft.Id)!.Revision);
        long Outbox() => Convert.ToInt64(Sql($"SELECT count(*) FROM sync_outbox WHERE entity='formula' AND entity_id='{formula.Id}'"));
        var queued = Outbox();

        db.Initialize(); db.Validate();
        Assert.AreEqual((long)Database.LatestVersion, Sql("SELECT max(version) FROM schema_migration"));
        var p = store.GetFormulaVersion(published.Id)!; var d = store.GetFormulaVersion(draft.Id)!;
        Assert.IsFalse(p.Value.PendingMeasure); Assert.AreEqual(ProductionVersionState.Published, p.Value.State); Assert.AreEqual(revisions.published, p.Revision);
        Assert.IsTrue(d.Value.PendingMeasure, "An existing draft is never taken as verified."); Assert.AreEqual(revisions.draft, d.Revision);
        Assert.AreEqual(1_800_000L, d.Value.BaseYield.AtomicUnits, "Quantities and units are untouched.");
        Assert.AreEqual(900_000L, d.Value.Ingredients.Single().Quantity.AtomicUnits);
        Assert.ThrowsExactly<ProductionDomainException>(() => store.PublishFormula(draft.Id, d.Revision, at.AddMinutes(2)));
        Assert.IsGreaterThan(queued, Outbox(), "The mark travels to the cloud.");
    }

    [TestMethod] public void TheWebKeepsTheMarkWhenItDoesNotSendItAndCannotPublishAMarkedVersion()
    {
        db.Initialize();
        var commands = new CloudCommandService(work, clock, terminal.Id, TimeZoneInfo.Utc,
            new CloudCommandModules(new SqliteInventoryStore(db, clock), work, new SqliteProductionUnitOfWork(db), _ => throw new NotSupportedException()));
        var recipes = new RecipeService(new SqliteProductionUnitOfWork(db), user, clock, terminal.Id);
        var inventory = new InventoryService(new SqliteInventoryStore(db, clock), work, user, clock, terminal.Id);
        var flour = inventory.CreateInventoryItem("PRUEBA-HARINA", "Harina de prueba", InventoryItemKind.RawMaterial, InventoryDimension.Mass).Id;
        var formula = new Formula(Guid.NewGuid(), "PRUEBA-WEB"); recipes.CreateFormula(formula, "Web");
        var version = new FormulaVersion(Guid.NewGuid(), formula.Id, 1, "Base", new(InventoryDimension.Mass, 1_000_000),
            [new(flour, new(InventoryDimension.Mass, 500_000), IngredientCountPolicy.NotApplicable)], [], clock.UtcNow, null, null, pendingMeasure: true);
        recipes.SaveFormulaDraft(version);
        long sequence = 0;
        CloudCommand Command(string kind, System.Text.Json.Nodes.JsonObject payload) => new(Guid.NewGuid(), ++sequence, kind, payload, user.Current!.Id, "Administrador web");
        System.Text.Json.Nodes.JsonObject Draft() => new()
        {
            ["formulaId"] = formula.Id.ToString(), ["versionId"] = version.Id.ToString(), ["revision"] = recipes.GetFormulaVersion(version.Id)!.Revision, ["name"] = "Base editada",
            ["yield"] = new System.Text.Json.Nodes.JsonObject { ["dimension"] = "Mass", ["atomic"] = 1_000_000 },
            ["ingredients"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["itemId"] = flour.ToString(), ["atomic"] = 600_000, ["policy"] = "NotApplicable" }),
            ["groups"] = new System.Text.Json.Nodes.JsonArray(),
        };
        Assert.IsTrue(commands.Apply(Command("recipe.formula.draft", Draft())).Applied);
        Assert.IsTrue(recipes.GetFormulaVersion(version.Id)!.Value.PendingMeasure, "An edit from the web that does not mention the mark keeps it.");
        var publish = commands.Apply(Command("recipe.formula.publish", new() { ["versionId"] = version.Id.ToString(), ["revision"] = recipes.GetFormulaVersion(version.Id)!.Revision }));
        Assert.IsFalse(publish.Applied); StringAssert.Contains(publish.Message, "«Por medir»");
        var unmark = Draft(); unmark["pendingMeasure"] = false;
        Assert.IsTrue(commands.Apply(Command("recipe.formula.draft", unmark)).Applied);
        Assert.IsFalse(recipes.GetFormulaVersion(version.Id)!.Value.PendingMeasure);
        Assert.AreEqual(ProductionVersionState.Draft, recipes.GetFormulaVersion(version.Id)!.Value.State, "Unmarking from the web does not publish either.");
    }
}
