using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

public sealed partial class RecipePersistenceTests
{
    [TestMethod]
    [DataRow(InventoryDimension.Mass)] [DataRow(InventoryDimension.Volume)]
    public void ProductionServiceNonRepresentableRemainsDraft(InventoryDimension dimension)
    {
        var item=dimension==InventoryDimension.Mass?flour:water;var app=RecipeApplication();
        var fv=new FormulaVersion(Guid.NewGuid(),formula.Id,2,"Fraction",version.BaseYield,[new(item.Id,new(dimension,1),IngredientCountPolicy.NotApplicable)],[],at);
        app.SaveFormulaDraft(fv);app.PublishFormula(fv.Id,1);
        var pv=new ProductProductionProfileVersion(Guid.NewGuid(),profile.Id,2,product.Id,finished.Id,"Fraction",[new(fv.Id,new(InventoryDimension.Mass,25))],[],at);
        app.SaveProfileDraft(pv);app.PublishProfile(pv.Id,1);
        var plan=new PrepareProductionCommand([new(pv.Id,2)],[]);var preview=Production().PrepareProduction(plan);
        Assert.IsFalse(preview.CanConfirm);var issue=preview.Planning.Issues.Single();Assert.AreEqual(ProductionError.NonRepresentableScale,issue.Code);
        Assert.AreEqual(item.Id,issue.SubjectId);Assert.AreEqual(fv.Id,issue.FormulaVersionId);Assert.AreEqual(1L,issue.ExactAtomic!.Numerator);Assert.AreEqual(2L,issue.ExactAtomic.Denominator);
        var batch=Production().CreateProduction(new(Guid.NewGuid(),Guid.NewGuid(),plan));var before=AllRows(true);
        Assert.AreEqual(ProductionFailure.UnresolvedPlan,Assert.ThrowsExactly<ProductionException>(()=>Production().ConfirmProduction(new(batch.Id,batch.OperationId,ProductionContract.Revision(batch),[new(product.Id,2)]))).Code);
        Assert.AreEqual(before,AllRows(true));Assert.AreEqual("DRAFT",Production().GetProduction(batch.Id)!.Status);
    }

    [TestMethod] public void ProductionServiceAlternativeFallbackAndExplicitRejection()
    {
        ProductionSetup();var app=RecipeApplication();
        var whole=new IngredientAlternativeOption(Guid.NewGuid(),new(packet.Id,new(InventoryDimension.Count,1),IngredientCountPolicy.ExactWholeCount));
        var mass=new IngredientAlternativeOption(Guid.NewGuid(),new(fresh.Id,new(InventoryDimension.Mass,10),IngredientCountPolicy.NotApplicable));
        var group=new IngredientAlternativeGroup(Guid.NewGuid(),[whole,mass],[whole.Id,mass.Id]);
        var fv=new FormulaVersion(Guid.NewGuid(),formula.Id,2,"Fallback",version.BaseYield,version.Ingredients,[group],at);
        app.SaveFormulaDraft(fv);app.PublishFormula(fv.Id,1);
        var pv=new ProductProductionProfileVersion(Guid.NewGuid(),profile.Id,2,product.Id,finished.Id,"Fallback",[new(fv.Id,new(InventoryDimension.Mass,25))],[],at);
        app.SaveProfileDraft(pv);app.PublishProfile(pv.Id,1);
        var plan=new PrepareProductionCommand([new(pv.Id,2)],[]);var preview=Production().PrepareProduction(plan);Assert.IsTrue(preview.CanConfirm);Assert.IsNotEmpty(preview.Planning.IneligibleOptions);
        var command=new CreateProductionCommand(Guid.NewGuid(),Guid.NewGuid(),plan);var batch=Production().CreateProduction(command);
        Assert.AreEqual(mass.Id,batch.Inputs.Single(x=>x.GroupId==group.Id).OptionId);
        var before=AllRows(true);Assert.AreEqual(batch.Id,Production().CreateProduction(command).Id);Assert.AreEqual(before,AllRows(true));
        Production().ConfirmProduction(new(batch.Id,batch.OperationId,ProductionContract.Revision(batch),[new(product.Id,2)]));
        var explicitPlan=plan with{Selections=[new(fv.Id,group.Id,whole.Id)]};Assert.IsFalse(Production().PrepareProduction(explicitPlan).CanConfirm);
        var rejectedCommand=new CreateProductionCommand(Guid.NewGuid(),Guid.NewGuid(),explicitPlan);
        var rejected=Production().CreateProduction(rejectedCommand);Assert.IsEmpty(rejected.Inputs);
        Assert.AreEqual(rejected.Id,Production().CreateProduction(rejectedCommand).Id);
        Assert.AreEqual(ProductionFailure.UnresolvedPlan,Assert.ThrowsExactly<ProductionException>(()=>Production().ConfirmProduction(new(rejected.Id,rejected.OperationId,ProductionContract.Revision(rejected),[new(product.Id,2)]))).Code);
    }

    [TestMethod] public void ProductionServiceOutputOverflowRollsBackConsumption()
    {
        ProductionSetup();inventory.ReceiveInventory(new(Guid.NewGuid(),finished.Id,ProductionContract.Main,1,"piece"));
        var (_,request)=ProductionDraft(actual:long.MaxValue);var before=AllRows(true);
        Assert.ThrowsExactly<InventoryStockException>(()=>Production().ConfirmProduction(request));
        Assert.AreEqual(before,AllRows(true));Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());
    }

    [TestMethod] public void ProductionServiceThreeOutputsAggregateBeforeCeiling()
    {
        ProductionSetup();var app=RecipeApplication();var outputs=new List<RequestedProductionOutput>{new(profileVersion.Id,1)};
        var actual=new List<ActualOutputLine>{new(product.Id,0)};
        for(int i=1;i<=2;i++)
        {
            var p=catalog.CreateProduct(product.CategoryId,"MULTI"+i,"Multi "+i,"",new Money(1900));
            var item=inventory.CreateInventoryItem("OUTPUT"+i,"Output "+i,InventoryItemKind.FinishedGood,InventoryDimension.Count);
            inventory.RegisterOpeningBalance(new(Guid.NewGuid(),item.Id,ProductionContract.Main,0,"piece"));
            var pr=new ProductProductionProfile(Guid.NewGuid(),p.Id,item.Id);app.CreateProfile(pr);
            var pv=new ProductProductionProfileVersion(Guid.NewGuid(),pr.Id,1,p.Id,item.Id,"Multi",profileVersion.Formulas,profileVersion.DirectIngredients,at);
            app.SaveProfileDraft(pv);app.PublishProfile(pv.Id,1);outputs.Add(new(pv.Id,1));actual.Add(new(p.Id,i));
        }
        var plan=new PrepareProductionCommand(outputs,[]);Assert.IsTrue(Production().PrepareProduction(plan).CanConfirm);
        var batch=Production().CreateProduction(new(Guid.NewGuid(),Guid.NewGuid(),plan));
        Assert.AreEqual(75L,batch.Formulas.Single().Required.AtomicUnits);
        Assert.AreEqual(1L,batch.Inputs.Where(x=>x.ItemId==packet.Id).Sum(x=>x.Required.AtomicUnits));
        Assert.AreEqual(45L,batch.Inputs.Where(x=>x.ItemId==flour.Id).Sum(x=>x.Required.AtomicUnits));
        var confirmed=Production().ConfirmProduction(new(batch.Id,batch.OperationId,ProductionContract.Revision(batch),actual));
        Assert.AreEqual(2L,Sql($"SELECT count(*) FROM inventory_movement WHERE operation_id='{confirmed.OutputOperationId}'"));
        Assert.AreEqual(9997L,Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{topping.Id}'"));
        Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());db.Validate();
    }

    [TestMethod] public void ProductionServicePinnedVersionsSurviveNewActiveVersions()
    {
        ProductionSetup();var (batch,request)=ProductionDraft();var app=RecipeApplication();
        void NewVersions(int number)
        {
            var fv=new FormulaVersion(Guid.NewGuid(),formula.Id,number,"New formula",version.BaseYield,version.Ingredients,[],at);
            app.SaveFormulaDraft(fv);app.PublishFormula(fv.Id,1);app.ActivateFormula(formula.Id,fv.Id,app.GetFormula(formula.Id)!.Revision);
            var pv=new ProductProductionProfileVersion(Guid.NewGuid(),profile.Id,number,product.Id,finished.Id,"New profile",[new(fv.Id,new(InventoryDimension.Mass,100))],[],at);
            app.SaveProfileDraft(pv);app.PublishProfile(pv.Id,1);app.ActivateProfile(profile.Id,pv.Id,app.GetProfile(profile.Id)!.Revision);
        }
        NewVersions(2);var saved=Production().ConfirmProduction(request);
        Assert.AreEqual(version.Id,saved.Formulas.Single().FormulaVersionId);Assert.AreEqual(profileVersion.Id,saved.Outputs.Single().ProfileVersionId);
        NewVersions(3);var before=AllRows(true);Assert.AreEqual(saved.RequestSha256,Production().ConfirmProduction(request).RequestSha256);Assert.AreEqual(before,AllRows(true));
    }

    [TestMethod] public void ProductionServiceRevisionAndActualCoverageFailWithoutWrites()
    {
        ProductionSetup();var (_,request)=ProductionDraft();var before=AllRows(true);
        Assert.AreEqual(ProductionFailure.RevisionConflict,Assert.ThrowsExactly<ProductionException>(()=>Production().ConfirmProduction(request with{ExpectedRevision="stale"})).Code);
        foreach(var values in new ActualOutputLine[][]{[],[new(product.Id,1),new(product.Id,2)],[new(Guid.NewGuid(),2)]})
            Assert.ThrowsExactly<ProductionException>(()=>Production().ConfirmProduction(request with{Outputs=values}));
        Assert.AreEqual(before,AllRows(true));
    }

    [TestMethod] public void ProductionServiceReceiptProductionAndSaleReconcile()
    {
        ProductionSetup();inventory.ReceiveInventory(new(Guid.NewGuid(),flour.Id,ProductionContract.Main,10,"mg"));
        var (_,request)=ProductionDraft(actual:3);Production().ConfirmProduction(request);
        product=catalog.EditProduct(product.Id,product.CategoryId,"P1","Pan","",new Money(1900),true);
        inventory.LinkProductToInventoryItem(product.Id,finished.Id,1);
        var configuration=new SaleConsumptionService(work,work,user,clock,terminal.Id);configuration.Change(ConsumptionAction.Enable,configuration.GetStatus());
        Persist();Assert.AreEqual(1L,Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{finished.Id}'"));
        Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());db.Validate();
    }
}
