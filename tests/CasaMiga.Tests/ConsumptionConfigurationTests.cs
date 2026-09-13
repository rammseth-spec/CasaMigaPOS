using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class ConsumptionConfigurationTests:SalesFixture
{
    private SaleConsumptionService Service(ILocalUnitOfWork? writer=null)=>new(writer??work,work,user,clock,terminal.Id);
    private Guid Seed(long? quantity=0,bool link=true)
    {
        var item=new InventoryItem(Guid.NewGuid(),"I14","Artículo",InventoryItemKind.RawMaterial,InventoryDimension.Count,true,clock.UtcNow,clock.UtcNow);
        var inventory=new SqliteInventoryStore(db,clock);inventory.AddItem(item);
        if(quantity is {} q)inventory.Apply(new(Guid.NewGuid(),user.Current!.Id,terminal.Id,InventoryMovementKind.OpeningBalance,[InventoryOperationLine.Opening(item.Id,SqliteInventoryStore.MainLocationId,InventoryCapture.Standard(q,InventoryUnits.Piece))]));
        if(link)inventory.AddProductLink(new(product.Id,item.Id,new(InventoryDimension.Count,1)));
        return item.Id;
    }
    [TestMethod] [DataRow("valid")] [DataRow("untracked")] [DataRow("inactiveProduct")] [DataRow("unlinkedRaw")]
    public void ValidAndUnrelatedConfigurationCanEnable(string scenario)
    {
        if(scenario=="valid")Seed();
        if(scenario=="untracked")Sql("UPDATE product SET track_inventory=0");
        if(scenario=="inactiveProduct")Sql("UPDATE product SET active=0");
        if(scenario=="unlinkedRaw"){Sql("UPDATE product SET track_inventory=0");Seed(null,false);}
        var s=Service();var check=s.Preflight();Assert.IsTrue(check.CanEnable);Assert.AreEqual(1L,check.CurrentVersion);
        var result=s.Change(ConsumptionAction.Enable,check.Status);Assert.AreEqual(InventorySaleConsumptionState.Enabled,result.Status.State);
    }
    [TestMethod] [DataRow("missing",PreflightCode.TrackedProductWithoutInventoryLink)]
    [DataRow("unexpected",PreflightCode.UnexpectedInventoryLink)] [DataRow("inactive",PreflightCode.InventoryItemInactive)]
    [DataRow("uninitialized",PreflightCode.NotInitialized)] [DataRow("reconcile",PreflightCode.ReconciliationMismatch)]
    [DataRow("main",PreflightCode.MainInvalid)] [DataRow("quantity",PreflightCode.InvalidQuantity)]
    public void BlockersAreStructuredAndActivationHasNoWrites(string scenario,PreflightCode code)
    {
        if(scenario!="missing")Seed(scenario=="uninitialized"?null:0);
        if(scenario=="unexpected")Sql("UPDATE product SET track_inventory=0");
        if(scenario=="inactive")Sql("UPDATE inventory_item SET active=0");
        if(scenario=="reconcile")Sql("UPDATE inventory_balance SET quantity_atomic=1");
        if(scenario=="main")Sql("UPDATE inventory_location SET active=0");
        if(scenario=="quantity")Sql("PRAGMA ignore_check_constraints=ON; UPDATE product_inventory_link SET quantity_per_product_unit_atomic=0");
        var s=Service();var before=History();var check=s.Preflight();Assert.IsFalse(check.CanEnable);
        Assert.IsTrue(check.Problems.Any(x=>x.Code==code));
        var error=Assert.ThrowsExactly<ConsumptionException>(()=>s.Change(ConsumptionAction.Enable,check.Status));
        Assert.AreEqual(ConsumptionError.PreflightBlocked,error.Error);Assert.AreEqual(check.Status,s.GetStatus());Assert.AreEqual(before,History());
    }
    [TestMethod]
    public void AllProductsNotJustPageAndMultipleBlockersAreChecked()
    {
        Sql("UPDATE product SET active=0");
        for(int i=0;i<55;i++)catalog.CreateProduct(product.CategoryId,"PREFLIGHT-"+i,"Producto "+i,"",new Money(1));
        var result=Service().Preflight();Assert.AreEqual(55,result.Problems.Count);Assert.IsTrue(result.Problems.All(p=>p.ProductId is not null && p.ProductName is not null));
    }
    [TestMethod]
    public void TransitionsNoChangeAndStaleVersionHaveExplicitSemantics()
    {
        Seed();var s=Service();var initial=s.GetStatus();
        Assert.AreEqual(ConsumptionError.InvalidTransition,Assert.ThrowsExactly<ConsumptionException>(()=>s.Change(ConsumptionAction.Pause,initial)).Error);
        Assert.ThrowsExactly<ConsumptionException>(()=>s.Change((ConsumptionAction)99,initial));
        var active=s.Change(ConsumptionAction.Enable,initial).Status;
        Assert.IsTrue(s.Change(ConsumptionAction.Enable,active).NoChange);Assert.IsTrue(s.Change(ConsumptionAction.Resume,active).NoChange);
        Assert.AreEqual(ConsumptionError.ConcurrencyConflict,Assert.ThrowsExactly<ConsumptionException>(()=>s.Change(ConsumptionAction.Enable,initial)).Error);
        var paused=s.Change(ConsumptionAction.Pause,active).Status;Assert.IsTrue(s.Change(ConsumptionAction.Pause,paused).NoChange);
        Assert.ThrowsExactly<ConsumptionException>(()=>s.Change(ConsumptionAction.Enable,paused));
        var resumed=s.Change(ConsumptionAction.Resume,paused).Status;Assert.AreEqual(4L,resumed.Version);
        Assert.AreEqual(3L,Sql("SELECT count(*) FROM audit_entry WHERE action LIKE 'inventory.sale_consumption.%'"));
    }
    [TestMethod]
    public void InformationalGreenIsNotAuthorizationAndResumeChecksAgain()
    {
        Seed();var s=Service();var green=s.Preflight();Sql("UPDATE product_inventory_link SET active=0");
        Assert.ThrowsExactly<ConsumptionException>(()=>s.Change(ConsumptionAction.Enable,green.Status));
        Sql("UPDATE product_inventory_link SET active=1");var active=s.Change(ConsumptionAction.Enable,green.Status).Status;
        Sql("UPDATE product_inventory_link SET active=0");var paused=s.Change(ConsumptionAction.Pause,active).Status;
        Assert.ThrowsExactly<ConsumptionException>(()=>s.Change(ConsumptionAction.Resume,paused));
        Assert.AreEqual(paused,s.GetStatus());
    }
    [TestMethod] [DataRow("Administrator",true,true)] [DataRow("Manager",true,false)] [DataRow("Cashier",false,false)]
    public async Task PermissionsAreRevalidatedAndUiHidesControls(string role,bool view,bool configure)
    {
        Seed();Sql($"UPDATE app_user SET role_name='{role}'");var s=Service();Assert.AreEqual(configure,s.CanConfigure());
        if(!view)Assert.ThrowsExactly<AccessDeniedException>(()=>s.GetStatus());else Assert.IsNotNull(s.GetStatus());
        if(!configure)
        {
            Assert.ThrowsExactly<AccessDeniedException>(()=>s.Preflight());
            foreach(var action in Enum.GetValues<ConsumptionAction>())Assert.ThrowsExactly<AccessDeniedException>(()=>s.Change(action,new(InventorySaleConsumptionState.Disabled,1,clock.UtcNow,null)));
        }
        var vm=new ConsumptionViewModel(s);await vm.LoadAsync();Assert.AreEqual(configure,vm.CanConfigure);Assert.AreEqual(configure,vm.CanEnable);
        if(configure)
        {
            await vm.ChangeAsync(ConsumptionAction.Enable,false);Assert.AreEqual(1L,s.GetStatus().Version);
            Sql("DELETE FROM role_permission WHERE permission_name='inventory.sale_consumption.configure'");
            await vm.ChangeAsync(ConsumptionAction.Enable,true);Assert.IsFalse(vm.CanConfigure);Assert.AreEqual(1L,s.GetStatus().Version);
        }
    }
    [TestMethod] [DataRow("inactive")] [DataRow("terminal")] [DataRow("session")]
    public void InvalidContextCannotConfigure(string fault)
    {
        var s=fault=="terminal"?new SaleConsumptionService(work,work,user,clock,Guid.NewGuid()):fault=="session"?new SaleConsumptionService(work,work,new UserSession(),clock,terminal.Id):Service();
        if(fault=="inactive")Sql("UPDATE app_user SET active=0");
        Assert.ThrowsExactly<AccessDeniedException>(()=>s.Preflight());Assert.IsFalse(s.CanConfigure());
    }
    [TestMethod]
    public async Task ConcurrentAdministratorsOneCommitsOtherConflicts()
    {
        Seed();var s=Service();var initial=s.GetStatus();var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Attempt(){await gate.Task;try{s.Change(ConsumptionAction.Enable,initial);return true;}catch(ConsumptionException e){Assert.AreEqual(ConsumptionError.ConcurrencyConflict,e.Error);return false;}}
        var a=Task.Run(Attempt);var b=Task.Run(Attempt);gate.SetResult();var results=await Task.WhenAll(a,b);Assert.AreEqual(1,results.Count(x=>x));Assert.AreEqual(2L,s.GetStatus().Version);
    }
    private sealed class BeforeCommit(ILocalUnitOfWork inner):ILocalUnitOfWork
    {public T Run<T>(Func<ILocalStore,T> action)=>inner.Run<T>(s=>{_ = action(s);throw new IOException("injected");});}
    [TestMethod] [DataRow("beforeUpdate")] [DataRow("afterUpdate")] [DataRow("audit")] [DataRow("commit")]
    public void ActivationRollsBackAtEveryBoundary(string fault)
    {
        Seed();var s=Service(fault=="commit"?new BeforeCommit(work):null);var before=History();var initial=s.GetStatus();
        if(fault!="commit")Sql(fault=="audit"?"CREATE TRIGGER inject AFTER INSERT ON audit_entry BEGIN SELECT RAISE(ABORT,'test'); END":$"CREATE TRIGGER inject {(fault=="beforeUpdate"?"BEFORE":"AFTER")} UPDATE ON inventory_sale_consumption_config BEGIN SELECT RAISE(ABORT,'test'); END");
        Assert.Throws<Exception>(()=>s.Change(ConsumptionAction.Enable,initial));Assert.AreEqual(initial,s.GetStatus());Assert.AreEqual(before,History());
    }
    [TestMethod]
    public void StateChangesOnlyWriteConfigAndTransitionAudit()
    {
        Seed(5);Persist();var before=History(false);var s=Service();
        var enabled=s.Change(ConsumptionAction.Enable,s.GetStatus()).Status;
        var paused=s.Change(ConsumptionAction.Pause,enabled).Status;s.Change(ConsumptionAction.Resume,paused);
        Assert.AreEqual(before,History(false));
        Assert.AreEqual(user.Current!.Id.ToString("D"),Sql("SELECT actor_id FROM audit_entry WHERE action='inventory.sale_consumption.enabled'"));
        Assert.AreEqual(terminal.Id.ToString("D"),Sql("SELECT terminal_id FROM audit_entry WHERE action='inventory.sale_consumption.enabled'"));
        using var details=JsonDocument.Parse((string)Sql("SELECT details_json FROM audit_entry WHERE action='inventory.sale_consumption.enabled'")!);
        Assert.AreEqual(1L,details.RootElement.GetProperty("versionBefore").GetInt64());Assert.AreEqual(2L,details.RootElement.GetProperty("versionAfter").GetInt64());
    }
    private string History(bool includeAudit=true)
    {
        var rows=new List<string>();foreach(var table in new[]{"sale","sale_item","payment","sale_inventory_evaluation","sale_inventory_line_snapshot","inventory_operation","inventory_movement","inventory_balance","audit_entry"})
        {if(!includeAudit && table=="audit_entry")continue;using var c=db.Open();using var q=c.CreateCommand();q.CommandText="SELECT * FROM "+table;using var r=q.ExecuteReader();while(r.Read())rows.Add(table+JsonSerializer.Serialize(Enumerable.Range(0,r.FieldCount).Select(i=>r.GetValue(i)).ToArray()));}
        return string.Join("\n",rows);
    }
    [TestMethod]
    public void PauseSaleResumeSaleAndBackupKeepHistoryAndCash()
    {
        Seed(5);var s=Service();var active=s.Change(ConsumptionAction.Enable,s.GetStatus()).Status;
        var checkout=new CheckoutService(work,user,clock,terminal.Id,new CheckoutJournal(directory));
        Sale Sell(){pos.Add(product.Id);var request=checkout.Prepare(new Money(2000));var sale=checkout.Confirm(request);checkout.Acknowledge(sale.OperationId);return sale;}
        Sell();var paused=s.Change(ConsumptionAction.Pause,active).Status;Sell();Assert.AreEqual(4L,Sql("SELECT quantity_atomic FROM inventory_balance"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale_inventory_evaluation WHERE status='DISABLED'"));
        s.Change(ConsumptionAction.Resume,paused);Sell();Assert.AreEqual(3L,Sql("SELECT quantity_atomic FROM inventory_balance"));Assert.AreEqual(5700L,work.Run(x=>x.CashSalesTotal(cash.Id)).Cents);
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();var target=Path.Combine(directory,"restored");InstallationBackup.Restore(backup,target);
        var restored=new Database(Path.Combine(target,"casamiga.db"));restored.Validate();Assert.AreEqual(s.GetStatus(),new SqliteLocalUnitOfWork(restored).Read(x=>x.GetConsumptionStatus()));Assert.IsEmpty(new SqliteInventoryStore(restored,clock).Reconcile());
    }
    [TestMethod] [DataRow(CheckoutInventoryError.InsufficientInventory)] [DataRow(CheckoutInventoryError.InventoryNotInitialized)]
    [DataRow(CheckoutInventoryError.InventoryTrackingConfigurationInvalid)] [DataRow(CheckoutInventoryError.InventoryItemInactive)]
    [DataRow(CheckoutInventoryError.InventoryConcurrencyConflict)] [DataRow(CheckoutInventoryError.InventoryConsumptionOverflow)]
    public void CheckoutMessagesAreFriendly(CheckoutInventoryError error)
    {
        var text=CheckoutInventoryMessages.Format(new(error,product.Id,required:6,available:4,dimension:InventoryDimension.Count),"Concha");
        Assert.IsFalse(text.Contains("SQL"));Assert.IsFalse(text.Contains("Int64"));Assert.IsFalse(text.Contains(product.Id.ToString()));
        if(error==CheckoutInventoryError.InsufficientInventory){StringAssert.Contains(text,"Concha");StringAssert.Contains(text,"6 piezas");StringAssert.Contains(text,"4 piezas");}
    }
    [TestMethod] [DataRow(InventoryDimension.Mass,"2 kg","500 g")] [DataRow(InventoryDimension.Volume,"2 L","500 ml")]
    public void CheckoutUsesOperationalUnits(InventoryDimension dimension,string required,string available)
    {
        var text=CheckoutInventoryMessages.Format(new(CheckoutInventoryError.InsufficientInventory,required:2000000,available:500000,dimension:dimension),"Producto");
        StringAssert.Contains(text,required);StringAssert.Contains(text,available);
    }
}
