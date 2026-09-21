using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;
public sealed partial class RecipePersistenceTests
{
    [TestMethod] public void MultiOutputSharesFormulaAndAdditionalPreparationsRemainGeneric()
    {
        Publish();
        var auxiliary=new Formula(Guid.NewGuid(),"AUX");recipes.CreateFormula(auxiliary,"Auxiliary",at);
        var av=new FormulaVersion(Guid.NewGuid(),auxiliary.Id,1,"Aux",new(InventoryDimension.Mass,10),[new(flour.Id,new(InventoryDimension.Mass,5),IngredientCountPolicy.NotApplicable)],[],at);
        recipes.SaveFormulaDraft(av);recipes.PublishFormula(av.Id,1,at);
        var secondProduct=catalog.CreateProduct(product.CategoryId,"P2","Second","",new Money(2500));
        var secondItem=inventory.CreateInventoryItem("FIN2","Second output",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        var secondProfile=new ProductProductionProfile(Guid.NewGuid(),secondProduct.Id,secondItem.Id);recipes.CreateProfile(secondProfile,at);
        var pv=new ProductProductionProfileVersion(Guid.NewGuid(),secondProfile.Id,1,secondProduct.Id,secondItem.Id,"Two preparations",
            [new(version.Id,new(InventoryDimension.Mass,25)),new(av.Id,new(InventoryDimension.Mass,10))],[],at);
        recipes.SaveProfileDraft(pv);recipes.PublishProfile(pv.Id,1,at);
        var id=Guid.NewGuid();recipes.CreateProductionDraft(id,Guid.NewGuid(),user.Current!.Id,terminal.Id,at,
            [new(recipes.GetProfileVersion(profileVersion.Id)!.Value,2),new(recipes.GetProfileVersion(pv.Id)!.Value,2)]);
        var batch=recipes.GetProductionBatch(id)!;Assert.AreEqual(2,batch.Outputs.Count);Assert.AreEqual(2,batch.Formulas.Count);
        Assert.AreEqual(100L,batch.Formulas.Single(x=>x.FormulaVersionId==version.Id).Required.AtomicUnits);
        Assert.AreEqual(70L,batch.Inputs.Where(x=>x.ItemId==flour.Id).Sum(x=>x.Required.AtomicUnits));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_output_plan SET profile_version_id='{pv.Id}' WHERE batch_id='{id}' AND product_id='{product.Id}'"));
        Assert.ThrowsExactly<SqliteException>(()=>recipes.ActivateProfile(secondProfile.Id,profileVersion.Id,1));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation"));db.Validate();
    }
    [TestMethod] public void DirectOnlyDraftAndNoFormulaRecursion()
    {
        var pv=new ProductProductionProfileVersion(Guid.NewGuid(),profile.Id,2,product.Id,finished.Id,"Direct",[],profileVersion.DirectIngredients,at);
        recipes.SaveProfileDraft(pv);recipes.PublishProfile(pv.Id,1,at);
        var id=Guid.NewGuid();recipes.CreateProductionDraft(id,Guid.NewGuid(),user.Current!.Id,terminal.Id,at,[new(recipes.GetProfileVersion(pv.Id)!.Value,3)]);
        var batch=recipes.GetProductionBatch(id)!;Assert.IsEmpty(batch.Formulas);Assert.AreEqual(3L,batch.Inputs.Single().Required.AtomicUnits);
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE formula_ingredient SET item_id='{version.Id}' WHERE version_id='{version.Id}'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE formula_ingredient SET dimension='COUNT',count_policy='NOT_APPLICABLE' WHERE version_id='{version.Id}'"));
    }
    [TestMethod] public void PublishingVersionsMustFollowDomainOrderAndExactRevision()
    {
        var second=new FormulaVersion(Guid.NewGuid(),formula.Id,2,"Second",version.BaseYield,version.Ingredients,[],at);
        recipes.SaveFormulaDraft(second);recipes.PublishFormula(second.Id,1,at);
        Assert.ThrowsExactly<SqliteException>(()=>recipes.PublishFormula(version.Id,1,at));
        Assert.AreEqual(ProductionVersionState.Draft,recipes.GetFormulaVersion(version.Id)!.Value.State);
        var pv=new ProductProductionProfileVersion(Guid.NewGuid(),profile.Id,2,product.Id,finished.Id,"Second",[],profileVersion.DirectIngredients,at);
        recipes.SaveProfileDraft(pv);recipes.PublishProfile(pv.Id,1,at);
        Assert.ThrowsExactly<SqliteException>(()=>recipes.PublishProfile(profileVersion.Id,1,at));
        Assert.AreEqual(ProductionVersionState.Draft,recipes.GetProfileVersion(profileVersion.Id)!.Value.State);
    }
    [TestMethod] public async Task ConcurrentPublicationHasSingleWinner()
    {
        var outcomes=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>
        {
            try{new SqliteRecipeStore(db).PublishFormula(version.Id,1,at);return true;}
            catch(RecipeConcurrencyException){return false;}
            catch(ProductionDomainException e) when(e.Code==ProductionError.InvalidVersion){return false;}
        })));
        Assert.AreEqual(1,outcomes.Count(x=>x));Assert.AreEqual(2L,recipes.GetFormulaVersion(version.Id)!.Revision);
        outcomes=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>
        {
            try{new SqliteRecipeStore(db).PublishProfile(profileVersion.Id,1,at);return true;}
            catch(RecipeConcurrencyException){return false;}
            catch(ProductionDomainException e) when(e.Code==ProductionError.InvalidVersion){return false;}
        })));
        Assert.AreEqual(1,outcomes.Count(x=>x));
        outcomes=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>
        {try{new SqliteRecipeStore(db).ActivateProfile(profile.Id,profileVersion.Id,1);return true;}catch(RecipeConcurrencyException){return false;}})));
        Assert.AreEqual(1,outcomes.Count(x=>x));Assert.AreEqual(profileVersion.Id,recipes.GetProfile(profile.Id)!.ActiveVersionId);
    }
    [TestMethod] public void ReadBoundariesAndOverflowDoNotMutateStorage()
    {
        Assert.IsNull(recipes.GetFormula(Guid.NewGuid()));Assert.IsNull(recipes.GetProfile(Guid.NewGuid()));Assert.IsNull(recipes.GetProductionBatch(Guid.NewGuid()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>recipes.ListFormulas(201));Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>recipes.ListProfiles(offset:-1));
        Publish();var error=Assert.ThrowsExactly<ProductionDomainException>(()=>recipes.CreateProductionDraft(Guid.NewGuid(),Guid.NewGuid(),user.Current!.Id,terminal.Id,at,[new(recipes.GetProfileVersion(profileVersion.Id)!.Value,long.MaxValue)]));
        Assert.AreEqual(ProductionError.Overflow,error.Code);Assert.AreEqual(0L,Sql("SELECT count(*) FROM production_batch"));
    }
}

public sealed partial class RecipePersistenceTests
{
    [TestMethod] public void PublicInventoryWorkflowStillRejectsBothProductionKinds()
    {
        foreach(var kind in new[]{InventoryMovementKind.ProductionConsumption,InventoryMovementKind.ProductionOutput})
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new InventoryOperationRequest(Guid.NewGuid(),user.Current!.Id,terminal.Id,kind,[]));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation"));
    }
}

public sealed partial class RecipePersistenceTests
{
    [TestMethod] public void DraftUsesStoredProfileIdentityAndRejectsForeignActualCount()
    {
        Publish();var forgedProduct=Guid.NewGuid();
        var forgedDraft=new ProductProductionProfileVersion(profileVersion.Id,profile.Id,1,forgedProduct,finished.Id,"Forged",profileVersion.Formulas,profileVersion.DirectIngredients,at);
        var forged=new ProductProductionProfile(profile.Id,forgedProduct,finished.Id).PublishVersion(forgedDraft,at).Versions.Single();
        Assert.ThrowsExactly<ArgumentException>(()=>recipes.CreateProductionDraft(Guid.NewGuid(),Guid.NewGuid(),user.Current!.Id,terminal.Id,at,[new(forged,2)],actualCounts:new Dictionary<Guid,long>{{forgedProduct,1}}));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM production_batch"));
        var id=Guid.NewGuid();recipes.CreateProductionDraft(id,Guid.NewGuid(),user.Current!.Id,terminal.Id,at,[new(forged,2)]);
        Assert.AreEqual(product.Id,recipes.GetProductionBatch(id)!.Outputs.Single().ProductId);
        Assert.AreEqual("Profile v1",recipes.GetProductionBatch(id)!.Outputs.Single().ProfileName);
    }
}
