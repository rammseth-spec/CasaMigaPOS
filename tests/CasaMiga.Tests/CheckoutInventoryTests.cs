using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class CheckoutInventoryTests : SalesFixture
{
    private CheckoutService Service(ILocalUnitOfWork? unit=null,UserSession? session=null,ICheckoutJournal? journal=null,
        ICheckoutReceiptSnapshot? receipt=null)=>new(unit??work,session??user,clock,terminal.Id,journal??new CheckoutJournal(directory),receipt);
    private void Enable()=>Sql($"UPDATE inventory_sale_consumption_config SET state='ENABLED',version=version+1,updated_by='{user.Current!.Id}'");
    private Guid Seed(long? quantity=10,long factor=1)
    {
        var inventory=new SqliteInventoryStore(db,clock);var item=new InventoryItem(Guid.NewGuid(),"TEST-I13","Test",InventoryItemKind.FinishedGood,InventoryDimension.Count,true,clock.UtcNow,clock.UtcNow);
        inventory.AddItem(item);
        if(quantity is { } q)inventory.Apply(new(Guid.NewGuid(),user.Current!.Id,terminal.Id,InventoryMovementKind.OpeningBalance,[InventoryOperationLine.Opening(item.Id,SqliteInventoryStore.MainLocationId,InventoryCapture.Standard(q,InventoryUnits.Piece))]));
        inventory.AddProductLink(new(product.Id,item.Id,new(InventoryDimension.Count,factor)));
        Sql("UPDATE product SET track_inventory=1");return item.Id;
    }
    private CheckoutRequest Request(CheckoutService service,int quantity=1)
    {pos.Add(product.Id);for(int i=1;i<quantity;i++)pos.Increment(product.Id);return service.Prepare(new Money(100000));}
    private void NoSale()
    {
        foreach(var table in new[]{"sale","sale_item","payment","sale_sequence","sale_inventory_evaluation","sale_inventory_line_snapshot","receipt_snapshot"})Assert.AreEqual(0L,Sql($"SELECT count(*) FROM {table}"),table);
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind='SALE_CONSUMPTION'"));
        Assert.AreEqual(0L,work.Run(s=>s.CashSalesTotal(cash.Id)).Cents);
    }
    [TestMethod] [DataRow("DISABLED")] [DataRow("PAUSED")] [DataRow("ENABLED")]
    public void EveryNewCheckoutHasContractAndFlagEvaluation(string state)
    {
        Sql("UPDATE product SET track_inventory=0");
        if(state!="DISABLED")Enable();
        if(state=="PAUSED")Sql($"UPDATE inventory_sale_consumption_config SET state='PAUSED',version=3,updated_by='{user.Current!.Id}'");
        var service=Service();var request=Request(service);var sale=service.Confirm(request);
        Assert.AreEqual(1L,Sql("SELECT inventory_evaluation_contract_version FROM sale"));
        Assert.AreEqual(sale.OperationId.ToString("D"),Sql("SELECT inventory_evaluation_id FROM sale"));
        Assert.AreEqual(state=="ENABLED"?"NO_CONSUMPTION":"DISABLED",Sql("SELECT status FROM sale_inventory_evaluation"));
        Assert.AreEqual(state=="ENABLED"?1L:0L,Sql("SELECT count(*) FROM sale_inventory_line_snapshot"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_movement"));
        Assert.AreEqual(sale.Id,service.Confirm(request).Id);service.Acknowledge(sale.OperationId);
        db.Validate();
    }
    [TestMethod]
    public void ConsumedRetryUsesHistoryAfterCatalogStockAndFlagChange()
    {
        var item=Seed(10,2);Enable();var service=Service();var request=Request(service,2);var sale=service.Confirm(request);
        Assert.AreEqual(6L,Sql("SELECT quantity_atomic FROM inventory_balance"));
        service.Acknowledge(sale.OperationId);
        Sql("UPDATE product_inventory_link SET quantity_per_product_unit_atomic=100,active=0; UPDATE product SET track_inventory=0,active=0");
        Sql($"UPDATE inventory_sale_consumption_config SET state='PAUSED',version=3,updated_by='{user.Current!.Id}'");
        var inv=new SqliteInventoryStore(db,clock);var cap=InventoryCapture.Standard(6,InventoryUnits.Piece);
        inv.Apply(new(Guid.NewGuid(),user.Current!.Id,terminal.Id,InventoryMovementKind.Waste,[InventoryOperationLine.Change(item,SqliteInventoryStore.MainLocationId,InventoryDelta.Decrease(cap.Quantity),cap)],"DAMAGED"));
        Sql("UPDATE inventory_item SET active=0");
        Assert.AreEqual(sale.Id,service.Confirm(request).Id);Assert.AreEqual(0L,Sql("SELECT quantity_atomic FROM inventory_balance"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM inventory_operation WHERE kind='SALE_CONSUMPTION'"));
        Assert.AreEqual(3800L,work.Run(s=>s.CashSalesTotal(cash.Id)).Cents);
        Assert.ThrowsExactly<CheckoutException>(()=>service.Confirm(request with {Received=new Money(100001)}));
        db.Validate();Assert.IsEmpty(inv.Reconcile());
    }
    [TestMethod] [DataRow("missing",CheckoutInventoryError.InventoryTrackingConfigurationInvalid)]
    [DataRow("untracked",CheckoutInventoryError.InventoryTrackingConfigurationInvalid)]
    [DataRow("inactive",CheckoutInventoryError.InventoryItemInactive)]
    [DataRow("uninitialized",CheckoutInventoryError.InventoryNotInitialized)]
    [DataRow("insufficient",CheckoutInventoryError.InsufficientInventory)]
    [DataRow("overflow",CheckoutInventoryError.InventoryConsumptionOverflow)]
    public void PolicyBlocksWholeCheckoutAndKeepsCart(string fault,CheckoutInventoryError expected)
    {
        if(fault=="missing")Sql("UPDATE product SET track_inventory=1");
        else Seed(fault=="uninitialized"?null:fault is "inactive" or "insufficient"?0:10,fault=="overflow"?long.MaxValue:1);
        if(fault=="untracked")Sql("UPDATE product SET track_inventory=0");
        if(fault=="inactive")Sql("UPDATE inventory_item SET active=0");
        Enable();var service=Service();var request=Request(service,2);
        var error=Assert.ThrowsExactly<CheckoutInventoryException>(()=>service.Confirm(request));Assert.AreEqual(expected,error.Error);
        if(fault=="insufficient"){Assert.AreEqual(2L,error.RequiredAtomic);Assert.AreEqual(0L,error.AvailableAtomic);Assert.AreEqual(product.Id,error.ProductId);Assert.AreEqual(InventoryDimension.Count,error.Dimension);}
        NoSale();Assert.AreSame(request.Cart,user.Cart);Assert.IsFalse(service.HasPending);
    }
    [TestMethod]
    public void FailedAttemptCanRetryAfterReceiptOrFlagChange()
    {
        var item=Seed(0);Enable();var service=Service();var request=Request(service);
        Assert.ThrowsExactly<CheckoutInventoryException>(()=>service.Confirm(request));NoSale();
        var capture=InventoryCapture.Standard(2,InventoryUnits.Piece);
        new SqliteInventoryStore(db,clock).Apply(new(Guid.NewGuid(),user.Current!.Id,terminal.Id,InventoryMovementKind.Receipt,[InventoryOperationLine.Change(item,SqliteInventoryStore.MainLocationId,InventoryDelta.Increase(capture.Quantity),capture)]));
        Assert.AreEqual(1L,service.Confirm(request).Sequence);service.Acknowledge(request.OperationId);
        var next=Request(service,2);Assert.ThrowsExactly<CheckoutInventoryException>(()=>service.Confirm(next));
        Sql($"UPDATE inventory_sale_consumption_config SET state='PAUSED',version=3,updated_by='{user.Current!.Id}'");
        Assert.AreEqual(2L,service.Confirm(next).Sequence);
        Assert.AreEqual("DISABLED",Sql("SELECT status FROM sale_inventory_evaluation WHERE checkout_operation_id='"+next.OperationId+"'"));
        Assert.AreEqual(1L,Sql("SELECT quantity_atomic FROM inventory_balance"));
    }
    [TestMethod]
    public void CashierConsumesWithoutAdministrativePermissionsAndNeedsSalesComplete()
    {
        Seed();Enable();Sql("UPDATE app_user SET role_name='Cashier'");
        Assert.IsFalse(work.Run(s=>s.HasPermission(user.Current!.Id,"inventory.adjust")));
        var service=Service();var request=Request(service);service.Confirm(request);service.Acknowledge(request.OperationId);
        Assert.AreEqual(user.Current!.Id.ToString("D"),Sql("SELECT actor_id FROM inventory_operation WHERE kind='SALE_CONSUMPTION'"));
        Sql("DELETE FROM role_permission WHERE role_name='Cashier' AND permission_name='sales.complete'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>Request(service));
    }
    [TestMethod]
    public void TwoProductsAggregateOneMovement()
    {
        var item=Seed(10,2);var second=catalog.CreateProduct(product.CategoryId,"P2","Other","",new Money(100));
        new SqliteInventoryStore(db,clock).AddProductLink(new(second.Id,item,new(InventoryDimension.Count,3)));
        Sql("UPDATE product SET track_inventory=1");Enable();var service=Service();pos.Add(product.Id);pos.Add(second.Id);
        service.Confirm(service.Prepare(new Money(3000)));
        Assert.AreEqual(2L,Sql("SELECT count(*) FROM sale_inventory_line_snapshot"));
        Assert.AreEqual(-5L,Sql("SELECT delta_atomic FROM inventory_movement WHERE is_opening=0"));
        Assert.AreEqual(5L,Sql("SELECT quantity_atomic FROM inventory_balance"));
    }
    private sealed class Journal:ICheckoutJournal
    {private Guid? value;public Guid? Read()=>value;public void Save(Guid id)=>value=id;public void Clear(Guid id)=>value=null;}
    private sealed class Settings:IPrintingSettingsProvider
    {public PrintingSettingsResult Read()=>new(new(1,true,"TEST",48,"windows-1252","Disabled",false,new BusinessReceiptProfile("TEST"),16),null);}
    private sealed class FailingTransport(Action observe):IReceiptPrintTransport
    {
        public int Calls;
        public PrintSubmissionResult Submit(PrintSubmission submission)
        {Calls++;observe();throw new PrintTransportException("OPEN_PRINTER_1801",false);}
    }
    [TestMethod] [DataRow(false)] [DataRow(true)]
    public void ReceiptSnapshotIsAtomicAndPrintFailureNeverConsumesAgain(bool failSnapshot)
    {
        Seed();Enable();var settings=new Settings();
        var service=Service(receipt:new CheckoutReceiptSnapshot(settings,TimeZoneInfo.Utc));var request=Request(service);
        if(failSnapshot)
        {
            Sql("CREATE TRIGGER inject_snapshot AFTER INSERT ON receipt_snapshot BEGIN SELECT RAISE(ABORT,'test'); END");
            Assert.ThrowsExactly<CheckoutException>(()=>service.Confirm(request));NoSale();
            Assert.AreEqual(10L,Sql("SELECT quantity_atomic FROM inventory_balance"));Assert.AreSame(request.Cart,user.Cart);return;
        }
        var sale=service.Confirm(request);Assert.AreEqual(1L,Sql("SELECT count(*) FROM receipt_snapshot"));
        var hash=Sql("SELECT content_sha256 FROM receipt_snapshot");
        var transport=new FailingTransport(()=>Assert.AreEqual(sale.Id,work.Run(s=>s.FindSaleByOperation(request.OperationId))!.Id));
        Assert.AreEqual(0,transport.Calls);
        var receipts=new ReceiptService(work,user,clock,terminal.Id,TimeZoneInfo.Utc,settings,new EscPosReceiptRenderer(),transport);
        Assert.AreEqual(ReceiptPrintState.Failed,receipts.PrintOriginal(sale).State);
        Assert.AreEqual(ReceiptPrintState.Failed,receipts.Reprint(sale.Id).State);
        Assert.AreEqual(2,transport.Calls);Assert.AreEqual(hash,Sql("SELECT content_sha256 FROM receipt_snapshot"));
        Assert.AreEqual(9L,Sql("SELECT quantity_atomic FROM inventory_balance"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale_inventory_evaluation"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM inventory_operation WHERE kind='SALE_CONSUMPTION'"));
        Assert.AreEqual(1900L,work.Run(s=>s.CashSalesTotal(cash.Id)).Cents);
    }
    [TestMethod]
    public async Task ConcurrentCheckoutsOnlyOneConsumesFourOfFive()
    {
        Seed(5);Enable();var j2=new Journal();var other=LoginAgain(j2);var otherPos=new PosService(work,other,terminal.Id);
        var first=Service();var a=Request(first,4);otherPos.Add(product.Id);for(int i=1;i<4;i++)otherPos.Increment(product.Id);
        var second=Service(session:other,journal:j2);var b=second.Prepare(new Money(100000));
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Run(CheckoutService s,CheckoutRequest r){await gate.Task;try{s.Confirm(r);return true;}catch(CheckoutInventoryException e){Assert.AreEqual(CheckoutInventoryError.InsufficientInventory,e.Error);return false;}}
        var t1=Task.Run(()=>Run(first,a));var t2=Task.Run(()=>Run(second,b));gate.SetResult();var results=await Task.WhenAll(t1,t2);
        Assert.AreEqual(1,results.Count(x=>x));Assert.AreEqual(1L,Sql("SELECT quantity_atomic FROM inventory_balance"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM payment"));
    }
    [TestMethod] [DataRow("sale","INSERT")] [DataRow("sale_item","INSERT")] [DataRow("payment","INSERT")]
    [DataRow("sale_inventory_evaluation","INSERT")] [DataRow("sale_inventory_line_snapshot","INSERT")]
    [DataRow("inventory_operation","INSERT")] [DataRow("inventory_movement","INSERT")] [DataRow("inventory_balance","UPDATE")]
    [DataRow("audit_entry","INSERT")]
    public void CrossRollbackAfterEachWrite(string table,string action)
    {
        Seed();Enable();var service=Service();var request=Request(service);
        Sql($"CREATE TRIGGER inject_failure AFTER {action} ON {table} BEGIN SELECT RAISE(ABORT,'test'); END");
        try{service.Confirm(request);Assert.Fail("Expected rollback");}catch(CheckoutException){}
        NoSale();Assert.AreEqual(10L,Sql("SELECT quantity_atomic FROM inventory_balance"));Assert.AreSame(request.Cart,user.Cart);
    }
    private sealed class FaultUnit(ILocalUnitOfWork inner,bool after):ILocalUnitOfWork
    {
        public bool Armed=true;
        public T Run<T>(Func<ILocalStore,T> action)
        {
            var result=inner.Run(s=>{var value=action(s);if(Armed && !after && value is Sale){Armed=false;throw new IOException("before commit");}return value;});
            if(Armed && after && result is Sale){Armed=false;throw new IOException("after commit");}return result;
        }
    }
    [TestMethod] [DataRow(false)] [DataRow(true)]
    public void PreCommitRollsBackAndLostCommitResponseRecovers(bool after)
    {
        Seed();Enable();var service=Service(new FaultUnit(work,after));var request=Request(service);
        if(!after){Assert.ThrowsExactly<CheckoutException>(()=>service.Confirm(request));NoSale();}
        var sale=service.Confirm(request);Assert.AreEqual(1L,sale.Sequence);Assert.AreEqual(9L,Sql("SELECT quantity_atomic FROM inventory_balance"));
        Assert.AreEqual(sale.Id,service.Resolve()!.Id);Assert.AreEqual(1900L,work.Run(s=>s.CashSalesTotal(cash.Id)).Cents);
    }
    [TestMethod] [DataRow("evaluation")] [DataRow("hash")] [DataRow("snapshot")] [DataRow("operation")]
    public void CorruptedRecoveryIsNeverRepaired(string corruption)
    {
        Seed();Enable();var service=Service();var request=Request(service);var sale=service.Confirm(request);
        using(var c=db.Open())using(var q=c.CreateCommand())
        {
            q.CommandText=corruption switch
            {
                "evaluation"=>"PRAGMA foreign_keys=OFF; DROP TRIGGER sale_inventory_evaluation_no_delete; DELETE FROM sale_inventory_evaluation",
                "hash"=>"DROP TRIGGER sale_inventory_evaluation_no_update; UPDATE sale_inventory_evaluation SET evaluation_sha256='"+new string('A',64)+"'",
                "snapshot"=>"DROP TRIGGER sale_inventory_snapshot_no_delete; DELETE FROM sale_inventory_line_snapshot",
                _=>"DROP TRIGGER inventory_operation_confirmed_no_update; UPDATE inventory_operation SET kind='RECEIPT' WHERE kind='SALE_CONSUMPTION'"
            };q.ExecuteNonQuery();
        }
        var error=Assert.ThrowsExactly<CheckoutInventoryException>(()=>service.Confirm(request));Assert.AreEqual(CheckoutInventoryError.InventoryRecoveryInconsistent,error.Error);
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));Assert.AreEqual(9L,Sql("SELECT quantity_atomic FROM inventory_balance"));Assert.IsTrue(service.HasPending);
    }
    [TestMethod]
    public void ConsumedBackupRestorePreservesContractAndRecovery()
    {
        Seed();Enable();var service=Service();var request=Request(service);var sale=service.Confirm(request);
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var target=Path.Combine(directory,"restore");InstallationBackup.Restore(backup,target);var restored=new Database(Path.Combine(target,"casamiga.db"));restored.Validate();
        var rw=new SqliteLocalUnitOfWork(restored);rw.Run(s=>{var found=s.FindSaleByOperation(request.OperationId)!;s.ValidateSaleInventoryRecovery(found);Assert.AreEqual(sale.Id,found.Id);return true;});
        Assert.IsEmpty(new SqliteInventoryStore(restored,clock).Reconcile());Assert.AreEqual(1900L,rw.Run(s=>s.CashSalesTotal(cash.Id)).Cents);
    }
}
