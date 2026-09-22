using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

public sealed partial class RecipePersistenceTests
{
    private ProductionService Production(Action<string>? fault=null)=>new(new SqliteProductionUnitOfWork(db,fault),user,clock,terminal.Id);
    private RecipeService RecipeApplication()=>new(new SqliteProductionUnitOfWork(db),user,clock,terminal.Id);
    private PrepareProductionCommand PlanRequest(long count=2)=>new([new(profileVersion.Id,count)],[]);
    private void ProductionSetup(long stock=10000)
    {
        Publish();
        foreach(var item in new[]{flour,water,packet,fresh,topping})
            inventory.RegisterOpeningBalance(new(Guid.NewGuid(),item.Id,ProductionContract.Main,stock,item.Dimension switch{InventoryDimension.Mass=>"mg",InventoryDimension.Volume=>"uL",_=>"piece"}));
        inventory.RegisterOpeningBalance(new(Guid.NewGuid(),finished.Id,ProductionContract.Main,0,"piece"));
    }
    private (StoredProductionBatch Batch,ConfirmProductionCommand Request) ProductionDraft(long actual=2,long planned=2)
    {
        var batch=Production().CreateProduction(new(Guid.NewGuid(),Guid.NewGuid(),PlanRequest(planned)));
        return (batch,new(batch.Id,batch.OperationId,ProductionContract.Revision(batch),[new(product.Id,actual)]));
    }
    [TestMethod]
    [DataRow(0L)] [DataRow(1L)] [DataRow(2L)] [DataRow(3L)]
    public void ProductionServiceActualAndRecoveryAndRestore(long actual)
    {
        ProductionSetup();var (batch,request)=ProductionDraft(actual);var previewBefore=AllRows(true);
        var preview=Production().PrepareProduction(PlanRequest());Assert.IsTrue(preview.CanConfirm);Assert.AreEqual(previewBefore,AllRows(true));
        var saved=Production().ConfirmProduction(request);Assert.AreEqual("CONFIRMED",saved.Status);
        Assert.AreEqual(ProductionContract.ConsumptionId(batch.OperationId),saved.ConsumptionOperationId);
        Assert.AreEqual(actual>0?ProductionContract.OutputId(batch.OperationId):(Guid?)null,saved.OutputOperationId);
        Assert.AreEqual(actual,Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{finished.Id}'"));
        foreach(var input in batch.Inputs.GroupBy(x=>x.ItemId))Assert.AreEqual(10000-input.Sum(x=>x.Required.AtomicUnits),Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{input.Key}'"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='production.confirmed'"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind='WASTE'"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_movement m JOIN inventory_operation o ON o.id=m.operation_id WHERE o.kind LIKE 'PRODUCTION_%' AND m.delta_atomic=0"));
        var before=AllRows(true);Assert.AreEqual(saved.RequestSha256,Production().ConfirmProduction(request).RequestSha256);Assert.AreEqual(before,AllRows(true));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_output_plan SET actual_count=5 WHERE batch_id='{batch.Id}'"));
        inventory.AdjustToPhysicalCount(new(Guid.NewGuid(),flour.Id,ProductionContract.Main,0,"mg",AdjustmentReason.DataCorrection));
        inventory.AdjustToPhysicalCount(new(Guid.NewGuid(),finished.Id,ProductionContract.Main,0,"piece",AdjustmentReason.DataCorrection));
        Sql($"UPDATE inventory_item SET active=0 WHERE id IN ('{flour.Id}','{finished.Id}')");
        before=AllRows(true);Assert.AreEqual(saved.RequestSha256,Production().ConfirmProduction(request).RequestSha256);Assert.AreEqual(before,AllRows(true));
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var target=Path.Combine(directory,"production-restored");InstallationBackup.Restore(backup,target);var restored=new Database(Path.Combine(target,"casamiga.db"));restored.Validate();
        var service=new ProductionService(new SqliteProductionUnitOfWork(restored),user,clock,terminal.Id);
        Assert.AreEqual(saved.RequestSha256,service.ConfirmProduction(request).RequestSha256);
        Assert.IsEmpty(new SqliteInventoryStore(restored,clock).Reconcile());db.Validate();
    }
    [TestMethod]
    [DataRow("before_snapshots")] [DataRow("before_consumption")] [DataRow("after_consumption")]
    [DataRow("before_output")] [DataRow("after_output")] [DataRow("before_confirmed")] [DataRow("during_audit")]
    public void ProductionServiceFaultRollsBackEverything(string point)
    {
        ProductionSetup();var (_,request)=ProductionDraft();var before=AllRows(true);
        Assert.ThrowsExactly<IOException>(()=>Production(p=>{if(p==point)throw new IOException("synthetic");}).ConfirmProduction(request));
        Assert.AreEqual(before,AllRows(true));db.Validate();Assert.AreEqual("CONFIRMED",Production().ConfirmProduction(request).Status);
    }
    [TestMethod] public void ProductionServiceUncertainCommitRecovers()
    {
        ProductionSetup();var (_,request)=ProductionDraft();bool thrown=false;
        var result=Production(point=>{if(point=="after_commit" && !thrown){thrown=true;throw new IOException("commit uncertain");}}).ConfirmProduction(request);
        Assert.IsTrue(thrown);Assert.AreEqual("CONFIRMED",result.Status);
        var before=AllRows(true);Production().ConfirmProduction(request);Assert.AreEqual(before,AllRows(true));
    }
    [TestMethod] public void ProductionServiceStockRecheckAndMultipleShortages()
    {
        ProductionSetup(1);var (_,request)=ProductionDraft();var before=AllRows(true);
        var preview=Production().PrepareProduction(PlanRequest());Assert.IsFalse(preview.CanConfirm);Assert.IsTrue(preview.Shortages.Count>=2);
        var error=Assert.ThrowsExactly<ProductionException>(()=>Production().ConfirmProduction(request));Assert.AreEqual(ProductionFailure.InsufficientStock,error.Code);Assert.IsTrue(error.Shortages.Count>=2);
        Assert.AreEqual(before,AllRows(true));
    }
    [TestMethod]
    [DataRow(true)] [DataRow(false)]
    public void ProductionServiceInactiveBlocksNewConfirmation(bool input)
    {
        ProductionSetup();var (_,request)=ProductionDraft();
        if(input)inventory.AdjustToPhysicalCount(new(Guid.NewGuid(),flour.Id,ProductionContract.Main,0,"mg",AdjustmentReason.DataCorrection));
        Sql($"UPDATE inventory_item SET active=0 WHERE id='{(input?flour.Id:finished.Id)}'");
        var before=AllRows(true);Assert.AreEqual(ProductionFailure.InactiveItem,Assert.ThrowsExactly<ProductionException>(()=>Production().ConfirmProduction(request)).Code);Assert.AreEqual(before,AllRows(true));
    }
    [TestMethod] public void ProductionServiceCanonicalAndConflictingRetry()
    {
        ProductionSetup();var (batch,request)=ProductionDraft();
        var shuffled=batch with{Inputs=batch.Inputs.Reverse().ToArray(),Formulas=batch.Formulas.Reverse().ToArray(),Outputs=batch.Outputs.Reverse().ToArray()};
        Assert.AreEqual(ProductionContract.Hash(batch,request.Outputs),ProductionContract.Hash(shuffled,request.Outputs.Reverse().ToArray()));
        Assert.AreNotEqual(ProductionContract.Hash(batch,request.Outputs),ProductionContract.Hash(batch,[new(product.Id,3)]));
        Assert.AreNotEqual(ProductionContract.ConsumptionId(batch.OperationId),ProductionContract.OutputId(batch.OperationId));
        Assert.AreNotEqual(ProductionContract.ConsumptionId(batch.OperationId),ProductionContract.ConsumptionId(Guid.NewGuid()));
        Production().ConfirmProduction(request);var before=AllRows(true);
        Assert.AreEqual(ProductionFailure.IdempotencyConflict,Assert.ThrowsExactly<ProductionException>(()=>Production().ConfirmProduction(request with{Outputs=[new(product.Id,3)]})).Code);
        Assert.AreEqual(before,AllRows(true));
    }
    [TestMethod]
    [DataRow("Administrator")] [DataRow("Manager")] [DataRow("Cashier")]
    public void ProductionServicePermissionsRevalidated(string role)
    {
        ProductionSetup();Sql($"UPDATE app_user SET role_name='{role}'");
        if(role=="Cashier")
        {
            Assert.ThrowsExactly<AccessDeniedException>(()=>Production().PrepareProduction(PlanRequest()));
            Assert.ThrowsExactly<AccessDeniedException>(()=>ProductionDraft());
            Assert.ThrowsExactly<AccessDeniedException>(()=>Production().ListProductions());
            Assert.ThrowsExactly<AccessDeniedException>(()=>RecipeApplication().ListFormulas());
            Assert.ThrowsExactly<AccessDeniedException>(()=>RecipeApplication().CreateFormula(new(Guid.NewGuid(),"DENIED"),"Denied"));return;
        }
        Assert.IsNotEmpty(RecipeApplication().ListFormulas());var (_,request)=ProductionDraft();
        Assert.AreEqual("CONFIRMED",Production().ConfirmProduction(request).Status);Assert.IsNotEmpty(Production().ListProductions());
        if(role=="Manager")Assert.ThrowsExactly<AccessDeniedException>(()=>RecipeApplication().CreateFormula(new(Guid.NewGuid(),"DENIED"),"Denied"));
        else RecipeApplication().CreateFormula(new(Guid.NewGuid(),"ALLOWED"),"Allowed");
        Sql("UPDATE app_user SET role_name='Cashier'");Assert.ThrowsExactly<AccessDeniedException>(()=>Production().ConfirmProduction(request));
    }
    [TestMethod]
    [DataRow(true)] [DataRow(false)]
    public async Task ProductionServiceConcurrentCallers(bool sameBatch)
    {
        ProductionSetup(210);var (_,a)=ProductionDraft(planned:14);var b=sameBatch?a:ProductionDraft(planned:14).Request;
        using var gate=new Barrier(2);
        Task<bool> Run(ConfirmProductionCommand request)=>Task.Run(()=>{gate.SignalAndWait();try{Production().ConfirmProduction(request);return true;}catch(ProductionException e) when(e.Code is ProductionFailure.InsufficientStock or ProductionFailure.ConcurrencyConflict){return false;}});
        var results=await Task.WhenAll(Run(a),Run(b));Assert.AreEqual(sameBatch?2:1,results.Count(x=>x));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM production_batch WHERE status='CONFIRMED'"));Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());db.Validate();
    }
}
