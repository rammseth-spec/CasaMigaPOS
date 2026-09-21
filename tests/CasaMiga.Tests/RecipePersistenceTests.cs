using CasaMiga.Domain;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class RecipeMigrationTests : SalesFixture
{
    protected override int? FixtureSchemaVersion => 12;

    [TestMethod]
    public void ThirteenPreservesExistingSaleAndLedger()
    {
        product = catalog.EditProduct(product.Id,product.CategoryId,"P1","Pan","",new Money(1900),true);
        var inventory = new InventoryService(new SqliteInventoryStore(db,clock),work,user,clock,terminal.Id);
        var item = inventory.CreateInventoryItem("FIN","Terminado",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        inventory.RegisterOpeningBalance(new(Guid.NewGuid(),item.Id,SqliteInventoryStore.MainLocationId,10,"piece"));
        inventory.LinkProductToInventoryItem(product.Id,item.Id,1);
        var config = new SaleConsumptionService(work,work,user,clock,terminal.Id);
        config.Change(ConsumptionAction.Enable,config.GetStatus());
        var sale = Persist();
        db.Initialize(); db.Validate();
        Assert.AreEqual(13L, Sql("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(sale.Total.Cents, Sql("SELECT total_cents FROM sale"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM formula"));
        Assert.AreEqual(8L, Sql("SELECT quantity_atomic FROM inventory_balance"));
        Assert.AreEqual(2L, Sql("SELECT count(*) FROM inventory_operation"));
    }
}

[TestClass]
public sealed partial class RecipePersistenceTests : SalesFixture
{
    private SqliteRecipeStore recipes = null!;
    private InventoryService inventory = null!;
    private InventoryItem flour = null!, water = null!, fresh = null!, packet = null!, topping = null!, finished = null!;
    private Formula formula = null!;
    private FormulaVersion version = null!;
    private ProductProductionProfile profile = null!;
    private ProductProductionProfileVersion profileVersion = null!;
    private readonly DateTimeOffset at = new(2026,9,20,12,0,0,TimeSpan.Zero);
    [TestInitialize]
    public void RecipesSetup()
    {
        recipes=new(db); inventory=new(new SqliteInventoryStore(db,clock),work,user,clock,terminal.Id);
        flour=Item("FLOUR",InventoryDimension.Mass);water=Item("WATER",InventoryDimension.Volume);
        fresh=Item("FRESH",InventoryDimension.Mass);packet=Item("PACKET",InventoryDimension.Count);topping=Item("TOPPING",InventoryDimension.Count);
        finished=inventory.CreateInventoryItem("FINISHED","Finished",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        formula=new(Guid.NewGuid(),"SHARED");recipes.CreateFormula(formula,"Shared",at);
        var a=new IngredientAlternativeOption(Guid.NewGuid(),new(fresh.Id,new(InventoryDimension.Mass,10),IngredientCountPolicy.NotApplicable));
        var b=new IngredientAlternativeOption(Guid.NewGuid(),new(packet.Id,new(InventoryDimension.Count,1),IngredientCountPolicy.CeilingWholeCount));
        version=new(Guid.NewGuid(),formula.Id,1,"Formula v1",new(InventoryDimension.Mass,100),
            [new(flour.Id,new(InventoryDimension.Mass,60),IngredientCountPolicy.NotApplicable),new(water.Id,new(InventoryDimension.Volume,20),IngredientCountPolicy.NotApplicable)],
            [new(Guid.NewGuid(),[a,b],[b.Id,a.Id])],at,"technical notes");
        recipes.SaveFormulaDraft(version);
        profile=new(Guid.NewGuid(),product.Id,finished.Id);recipes.CreateProfile(profile,at);
        profileVersion=new(Guid.NewGuid(),profile.Id,1,product.Id,finished.Id,"Profile v1",
            [new(version.Id,new(InventoryDimension.Mass,25))],[new(new(topping.Id,new(InventoryDimension.Count,1),IngredientCountPolicy.ExactWholeCount))],at);
        recipes.SaveProfileDraft(profileVersion);
    }
    private InventoryItem Item(string code,InventoryDimension dimension)=>inventory.CreateInventoryItem(code,code,InventoryItemKind.RawMaterial,dimension);
    private void Publish()
    {
        recipes.PublishFormula(version.Id,1,at.AddMinutes(1));recipes.ActivateFormula(formula.Id,version.Id,1);
        recipes.PublishProfile(profileVersion.Id,1,at.AddMinutes(1));recipes.ActivateProfile(profile.Id,profileVersion.Id,1);
    }
    private Guid Draft(long? actual=null,Guid? operation=null)
    {
        var id=Guid.NewGuid();var published=recipes.GetProfileVersion(profileVersion.Id)!.Value;
        recipes.CreateProductionDraft(id,operation??Guid.NewGuid(),user.Current!.Id,terminal.Id,at.AddMinutes(2),[new(published,2)],
            actualCounts:actual.HasValue?new Dictionary<Guid,long>{{product.Id,actual.Value}}:null,notes:"Draft snapshot");return id;
    }
    [TestMethod] public void RoundTripDraftAndPublishedConfiguration()
    {
        var read=recipes.GetFormulaVersion(version.Id)!;
        Assert.AreEqual(1L,read.Revision);Assert.AreEqual(ProductionVersionState.Draft,read.Value.State);
        Assert.AreEqual(version.BaseYield,read.Value.BaseYield);Assert.AreEqual(version.Notes,read.Value.Notes);
        CollectionAssert.AreEqual(version.Ingredients.ToArray(),read.Value.Ingredients.ToArray());
        CollectionAssert.AreEqual(version.Alternatives[0].PreferenceOrder.ToArray(),read.Value.Alternatives[0].PreferenceOrder.ToArray());
        CollectionAssert.AreEqual(version.Alternatives[0].Options.ToArray(),read.Value.Alternatives[0].Options.ToArray());
        Assert.AreEqual(2,read.Value.Alternatives[0].Options.Select(o=>o.Ingredient.Quantity.Dimension).Distinct().Count());
        Publish();
        Assert.AreEqual(ProductionVersionState.Published,recipes.GetFormulaVersion(version.Id)!.Value.State);
        Assert.AreEqual(at.AddMinutes(1),recipes.GetFormulaVersion(version.Id)!.Value.PublishedAt);
        Assert.AreEqual(version.Id,recipes.ListFormulas().Single().ActiveVersionId);
        Assert.AreEqual(profileVersion.Id,recipes.ListProfiles().Single().ActiveVersionId);
        var p=recipes.GetProfileVersion(profileVersion.Id)!.Value;
        CollectionAssert.AreEqual(profileVersion.Formulas.ToArray(),p.Formulas.ToArray());
        CollectionAssert.AreEqual(profileVersion.DirectIngredients.ToArray(),p.DirectIngredients.ToArray());
        Assert.AreEqual(1,recipes.ListFormulaVersions(formula.Id).Count);Assert.AreEqual(1,recipes.ListProfileVersions(profile.Id).Count);
        db.Validate();
    }
    [TestMethod] public void DraftReplacementUsesRevisionAndRollsBackChildrenOnFailure()
    {
        var changed=new FormulaVersion(version.Id,formula.Id,1,"Changed",version.BaseYield,version.Ingredients,version.Alternatives,at);
        recipes.SaveFormulaDraft(changed,1);Assert.AreEqual("Changed",recipes.GetFormulaVersion(version.Id)!.Value.Name);
        Assert.ThrowsExactly<RecipeConcurrencyException>(()=>recipes.SaveFormulaDraft(version,1));
        var invalid=new FormulaVersion(version.Id,formula.Id,1,"Invalid",version.BaseYield,[new(Guid.NewGuid(),new(InventoryDimension.Mass,5),IngredientCountPolicy.NotApplicable)],[],at);
        Assert.ThrowsExactly<SqliteException>(()=>recipes.SaveFormulaDraft(invalid,2));
        Assert.AreEqual(2L,recipes.GetFormulaVersion(version.Id)!.Revision);Assert.AreEqual(2,recipes.GetFormulaVersion(version.Id)!.Value.Ingredients.Count);
        recipes.SaveProfileDraft(profileVersion,1);Assert.ThrowsExactly<RecipeConcurrencyException>(()=>recipes.SaveProfileDraft(profileVersion,1));
    }
    [TestMethod] public void DuplicateVersionAndWrongActiveOwnerAreRejected()
    {
        var duplicate=new FormulaVersion(Guid.NewGuid(),formula.Id,1,"Duplicate",version.BaseYield,version.Ingredients,[],at);
        Assert.ThrowsExactly<SqliteException>(()=>recipes.SaveFormulaDraft(duplicate));
        Assert.ThrowsExactly<SqliteException>(()=>recipes.ActivateFormula(formula.Id,version.Id,1));
        Assert.ThrowsExactly<SqliteException>(()=>recipes.ActivateProfile(profile.Id,profileVersion.Id,1));
        var other=new Formula(Guid.NewGuid(),"OTHER");recipes.CreateFormula(other,"Other",at);Publish();
        Assert.ThrowsExactly<SqliteException>(()=>recipes.ActivateFormula(other.Id,version.Id,1));
        recipes.ActivateFormula(formula.Id,null,2);Assert.IsNull(recipes.ListFormulas().Single(x=>x.Id==formula.Id).ActiveVersionId);
    }
    [TestMethod] public void ProfileCannotPublishDraftFormulaOrWrongDimension()
    {
        Assert.ThrowsExactly<SqliteException>(()=>recipes.PublishProfile(profileVersion.Id,1,at.AddMinutes(1)));
        Assert.AreEqual(ProductionVersionState.Draft,recipes.GetProfileVersion(profileVersion.Id)!.Value.State);
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE profile_formula_requirement SET dimension='VOLUME' WHERE profile_version_id='{profileVersion.Id}'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_profile SET finished_item_id='{flour.Id}' WHERE id='{profile.Id}'"));
    }
    [TestMethod] public void PublishedContentAndChildrenCannotBeMutatedOrReparented()
    {
        Publish();
        var sqls=new[]{
            $"UPDATE formula_version SET name='x' WHERE id='{version.Id}'",$"DELETE FROM formula_version WHERE id='{version.Id}'",
            $"DELETE FROM formula_ingredient WHERE version_id='{version.Id}'",$"UPDATE formula_ingredient SET quantity_atomic=1 WHERE version_id='{version.Id}'",
            $"DELETE FROM formula_alternative_option WHERE version_id='{version.Id}'",$"UPDATE formula_alternative_option SET preference_rank=NULL WHERE version_id='{version.Id}'",
            $"DELETE FROM formula_alternative_group WHERE version_id='{version.Id}'",$"UPDATE formula_alternative_group SET name='x' WHERE version_id='{version.Id}'",
            $"UPDATE production_profile_version SET name='x' WHERE id='{profileVersion.Id}'",$"DELETE FROM production_profile_version WHERE id='{profileVersion.Id}'",
            $"UPDATE profile_formula_requirement SET quantity_atomic=1 WHERE profile_version_id='{profileVersion.Id}'",$"DELETE FROM profile_formula_requirement WHERE profile_version_id='{profileVersion.Id}'",
            $"UPDATE profile_direct_ingredient SET quantity_atomic=2 WHERE profile_version_id='{profileVersion.Id}'",$"DELETE FROM profile_direct_ingredient WHERE profile_version_id='{profileVersion.Id}'",
            $"INSERT INTO formula_ingredient VALUES('{version.Id}',9,'{topping.Id}','COUNT',1,'EXACT_WHOLE_COUNT')"};
        foreach(var sql in sqls)Assert.ThrowsExactly<SqliteException>(()=>Sql(sql),sql);
        var draft=new FormulaVersion(Guid.NewGuid(),formula.Id,2,"v2",version.BaseYield,version.Ingredients,[],at);recipes.SaveFormulaDraft(draft);
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE formula_ingredient SET version_id='{draft.Id}' WHERE version_id='{version.Id}'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE formula_ingredient SET version_id='{version.Id}',sequence=99 WHERE version_id='{draft.Id}'"));
    }
    [TestMethod] public void HistoricalVersionReferencesSurviveActivationChange()
    {
        Publish();var batch=Draft();
        var second=new FormulaVersion(Guid.NewGuid(),formula.Id,2,"v2",new(InventoryDimension.Mass,200),version.Ingredients,[],at);
        recipes.SaveFormulaDraft(second);recipes.PublishFormula(second.Id,1,at);recipes.ActivateFormula(formula.Id,second.Id,2);
        var stored=recipes.GetProductionBatch(batch)!;
        Assert.AreEqual(version.Id,stored.Formulas.Single().FormulaVersionId);Assert.AreEqual("Formula v1",stored.Formulas.Single().Name);
        Assert.AreEqual(version.Id,recipes.GetProfileVersion(profileVersion.Id)!.Value.Formulas.Single().FormulaVersionId);
        Assert.AreEqual(50L,stored.Formulas.Single().Required.AtomicUnits);Assert.AreEqual(new ProductionRatio(1,2),stored.Formulas.Single().Factor);
    }
    [TestMethod] public async Task ConcurrentActivationAndDraftEditsHaveOneWinner()
    {
        Publish();var outcomes=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>
        {try{new SqliteRecipeStore(db).ActivateFormula(formula.Id,null,2);return true;}catch(RecipeConcurrencyException){return false;}})));
        Assert.AreEqual(1,outcomes.Count(x=>x));
        var draft=new FormulaVersion(Guid.NewGuid(),formula.Id,2,"v2",version.BaseYield,version.Ingredients,[],at);recipes.SaveFormulaDraft(draft);
        outcomes=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>
        {try{new SqliteRecipeStore(db).SaveFormulaDraft(draft,1);return true;}catch(RecipeConcurrencyException){return false;}})));
        Assert.AreEqual(1,outcomes.Count(x=>x));Assert.AreEqual(2L,recipes.GetFormulaVersion(draft.Id)!.Revision);
    }
    [TestMethod] public void CountUnspecifiedIsStoredButCannotBecomeResolvedProduction()
    {
        var f=new FormulaVersion(Guid.NewGuid(),formula.Id,2,"Count pending",new(InventoryDimension.Mass,100),
            [new(packet.Id,new(InventoryDimension.Count,1),IngredientCountPolicy.Unspecified)],[],at);
        recipes.SaveFormulaDraft(f);recipes.PublishFormula(f.Id,1,at);
        Assert.AreEqual(IngredientCountPolicy.Unspecified,recipes.GetFormulaVersion(f.Id)!.Value.Ingredients.Single().CountPolicy);
        var p=new ProductProductionProfileVersion(Guid.NewGuid(),profile.Id,2,product.Id,finished.Id,"Pending",[new(f.Id,new(InventoryDimension.Mass,100))],[],at);
        recipes.SaveProfileDraft(p);recipes.PublishProfile(p.Id,1,at);
        var draftId=Guid.NewGuid(); recipes.CreateProductionDraft(draftId,Guid.NewGuid(),user.Current!.Id,terminal.Id,at,[new(recipes.GetProfileVersion(p.Id)!.Value,1)]); var stored=recipes.GetProductionBatch(draftId)!; var issues=System.Text.Json.JsonSerializer.Deserialize<ProductionIssue[]>(stored.PlanningIssuesJson)!;
        Assert.AreEqual(ProductionError.CountPolicyRequired,issues.Single().Code);Assert.AreEqual("DRAFT",stored.Status);Assert.IsEmpty(stored.Inputs);
    }
    [TestMethod] public void NonRepresentableMassPersistsUnresolvedDraftWithoutRoundedInputs()
    {
        var f=new FormulaVersion(Guid.NewGuid(),formula.Id,2,"Fraction",new(InventoryDimension.Mass,3),[new(flour.Id,new(InventoryDimension.Mass,1),IngredientCountPolicy.NotApplicable)],[],at);
        recipes.SaveFormulaDraft(f);recipes.PublishFormula(f.Id,1,at);
        var p=new ProductProductionProfileVersion(Guid.NewGuid(),profile.Id,2,product.Id,finished.Id,"Fraction",[new(f.Id,new(InventoryDimension.Mass,1))],[],at);
        recipes.SaveProfileDraft(p);recipes.PublishProfile(p.Id,1,at);
        var draftId=Guid.NewGuid(); recipes.CreateProductionDraft(draftId,Guid.NewGuid(),user.Current!.Id,terminal.Id,at,[new(recipes.GetProfileVersion(p.Id)!.Value,1)]); var stored=recipes.GetProductionBatch(draftId)!; var issues=System.Text.Json.JsonSerializer.Deserialize<ProductionIssue[]>(stored.PlanningIssuesJson)!;
        Assert.AreEqual(ProductionError.NonRepresentableScale,issues.Single().Code);Assert.AreEqual("DRAFT",stored.Status);Assert.IsEmpty(stored.Inputs);Assert.AreEqual(new ProductionRatio(1,3),stored.Formulas.Single().Factor);Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation"));
    }
    [TestMethod]
    [DataRow(-1L)][DataRow(0L)][DataRow(1L)][DataRow(3L)]
    public void DraftRetainsActualSeparatelyAndNeverTouchesLedger(long actual)
    {
        Publish();var id=Draft(actual<0?null:actual);var batch=recipes.GetProductionBatch(id)!;
        Assert.AreEqual("DRAFT",batch.Status);Assert.AreEqual(2L,batch.Outputs.Single().PlannedCount);
        Assert.AreEqual(actual<0?null:(long?)actual,batch.Outputs.Single().ActualCount);
        Assert.AreEqual(actual==0||actual>2,batch.Outputs.Single().ActualPolicyPending);
        Assert.IsNull(batch.ConsumptionOperationId);Assert.IsNull(batch.OutputOperationId);Assert.IsNull(batch.ConfirmedAt);
        Assert.AreEqual(4,batch.Inputs.Count);Assert.AreEqual(1L,batch.Inputs.Single(x=>x.ItemId==packet.Id).Required.AtomicUnits);
        Assert.AreEqual(new ProductionRatio(1,2),batch.Inputs.Single(x=>x.ItemId==packet.Id).ExactAtomic);
        Assert.IsNotNull(batch.Inputs.Single(x=>x.ItemId==packet.Id).OptionId);
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation"));Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_movement"));Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_balance"));
        Assert.AreEqual(id,recipes.ListProductionBatchIds().Single());db.Validate();
    }
    [TestMethod] public void StableOperationIdCannotDuplicateAndFailedDraftIsAtomic()
    {
        Publish();var key=Guid.NewGuid();Draft(operation:key);
        Assert.ThrowsExactly<SqliteException>(()=>Draft(operation:key));Assert.AreEqual(1L,Sql("SELECT count(*) FROM production_batch"));
        Sql("CREATE TRIGGER inject_input_failure BEFORE INSERT ON production_input_snapshot BEGIN SELECT RAISE(ABORT,'injected'); END");
        Assert.ThrowsExactly<SqliteException>(()=>Draft());Assert.AreEqual(1L,Sql("SELECT count(*) FROM production_batch"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM production_output_plan"));
    }
    [TestMethod] public void IncompleteBatchAndOrphanReferencesAreRejected()
    {
        Publish();var id=Draft(2);
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_batch SET status='CONFIRMED',confirmed_utc='{at:O}',request_sha256='{new string('A',64)}',consumption_operation_id='{Guid.NewGuid()}',output_operation_id='{Guid.NewGuid()}' WHERE id='{id}'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_batch SET consumption_operation_id='{Guid.NewGuid()}' WHERE id='{id}'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_formula_snapshot SET denominator=2,numerator=2 WHERE batch_id='{id}'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_input_snapshot SET option_id='{Guid.NewGuid()}' WHERE batch_id='{id}' AND option_id IS NOT NULL"));
        Assert.AreEqual("DRAFT",recipes.GetProductionBatch(id)!.Status);db.Validate();
    }
    [TestMethod] public void SnapshotNamesSurviveCatalogRenameAndBackupRestore()
    {
        Publish();var id=Draft(0);var original=recipes.GetProductionBatch(id)!;
        catalog.EditProduct(product.Id,product.CategoryId,"P1","Renamed","",new Money(2200),false);
        Assert.AreEqual(original.Outputs.Single().ProductName,recipes.GetProductionBatch(id)!.Outputs.Single().ProductName);
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var destination=Path.Combine(directory,"restored");InstallationBackup.Restore(backup,destination);
        var restored=new Database(Path.Combine(destination,"casamiga.db"));restored.Initialize();restored.Validate();
        var actual=new SqliteRecipeStore(restored).GetProductionBatch(id)!;
        CollectionAssert.AreEqual(original.Outputs.ToArray(),actual.Outputs.ToArray());CollectionAssert.AreEqual(original.Inputs.ToArray(),actual.Inputs.ToArray());CollectionAssert.AreEqual(original.Formulas.ToArray(),actual.Formulas.ToArray());
        using var c=restored.Open();using var q=c.CreateCommand();q.CommandText="PRAGMA integrity_check";Assert.AreEqual("ok",q.ExecuteScalar());q.CommandText="PRAGMA foreign_key_check";Assert.IsNull(q.ExecuteScalar());
    }
}
