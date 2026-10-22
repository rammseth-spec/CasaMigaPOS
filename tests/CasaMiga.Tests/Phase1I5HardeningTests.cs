using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class Phase1I5HardeningTests:SalesFixture
{
    private static string Snapshot(Database database)
    {
        using var c=database.Open(readOnly:true);using var tx=c.BeginTransaction(deferred:true);
        using var tables=c.CreateCommand();tables.Transaction=tx;tables.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        var names=new List<string>();using(var r=tables.ExecuteReader())while(r.Read())names.Add(r.GetString(0));
        var all=new List<string>();
        foreach(var name in names)
        {
            using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="SELECT * FROM \""+name.Replace("\"","\"\"")+"\"";
            using var r=q.ExecuteReader();var rows=new List<string>();while(r.Read())rows.Add(JsonSerializer.Serialize(Enumerable.Range(0,r.FieldCount).Select(i=>r.IsDBNull(i)?null:r.GetValue(i)).ToArray()));
            all.Add(name+":"+string.Join("\n",rows.Order(StringComparer.Ordinal)));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",all))));
    }
    private sealed class Settings:IPrintingSettingsProvider
    {public PrintingSettingsResult Read()=>new(new(1,true,"SIMULATED",48,"windows-1252","Disabled",false,new BusinessReceiptProfile("SMOKE 1I"),16),null);}
    private sealed class UnknownTransport:IReceiptPrintTransport
    {public PrintSubmissionResult Submit(PrintSubmission submission)=>throw new PrintTransportException("WRITE_PRINTER_5",true);}
    [TestMethod]
    public void EndToEndCashierRecoveryRestartRestoreAndPrintUnknown()
    {
        product=catalog.EditProduct(product.Id,product.CategoryId,"CONCHA-SMOKE-1I","Concha sintética 1I","",new Money(1900),true);
        var inventory=new InventoryService(new SqliteInventoryStore(db,clock),work,user,clock,terminal.Id);
        var item=inventory.CreateInventoryItem("CONCHA-SMOKE-1I","Concha sintética 1I",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        inventory.RegisterOpeningBalance(new(Guid.NewGuid(),item.Id,SqliteInventoryStore.MainLocationId,10,InventoryUnits.Piece.Code));
        inventory.LinkProductToInventoryItem(product.Id,item.Id,1);
        var config=new SaleConsumptionService(work,work,user,clock,terminal.Id);var initial=config.GetStatus();
        Assert.AreEqual(InventorySaleConsumptionState.Disabled,initial.State);Assert.IsTrue(config.Preflight().CanEnable);
        config.Change(ConsumptionAction.Enable,initial);
        var cashierId=Guid.NewGuid();using(var c=db.Open())using(var q=c.CreateCommand())
        {
            q.CommandText="INSERT INTO app_user(id,username,display_name,password_hash,role_name,created_utc) VALUES($id,'SMOKECASHIER','Cashier',$hash,'Cashier',$at)";
            q.Parameters.AddWithValue("$id",cashierId.ToString("D"));q.Parameters.AddWithValue("$hash",new LocalPasswords().Hash(password));q.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));q.ExecuteNonQuery();
        }
        var cashier=new UserSession(new CheckoutJournal(directory));Assert.IsTrue(new IdentityService(work,new LocalPasswords(),clock,cashier).Login("SMOKECASHIER",password));
        foreach(var permission in new[]{"inventory.view","inventory.configure","inventory.adjust","inventory.receive","inventory.waste",SaleConsumptionService.Permission})Assert.IsFalse(work.Read(s=>s.HasPermission(cashierId,permission)));
        var cp=new PosService(work,cashier,terminal.Id);var checkout=new CheckoutService(work,cashier,clock,terminal.Id,new CheckoutJournal(directory),new CheckoutReceiptSnapshot(new Settings(),TimeZoneInfo.Utc));
        (Sale,CheckoutRequest) Sell(int qty)
        {cp.Add(product.Id);for(int i=1;i<qty;i++)cp.Increment(product.Id);var request=checkout.Prepare(new Money(10000));var sale=checkout.Confirm(request);checkout.Acknowledge(sale.OperationId);return(sale,request);}
        var (first,original)=Sell(2);Assert.AreEqual(3800L,first.Total.Cents);Assert.AreEqual(6200L,first.Payment.Change.Cents);
        Assert.AreEqual(8L,Sql("SELECT quantity_atomic FROM inventory_balance"));Assert.AreEqual(-2L,Sql("SELECT delta_atomic FROM inventory_movement WHERE is_opening=0"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));Assert.AreEqual(1L,Sql("SELECT inventory_evaluation_contract_version FROM sale"));
        var history=inventory.MovementHistory(new(item.Id));var consumed=history.Items.Single(x=>x.Kind==InventoryMovementKind.SaleConsumption);Assert.AreEqual("Venta",new CasaMiga.Presentation.InventoryHistoryRowView(consumed).Kind);
        var before=Snapshot(db);Assert.AreEqual(first.Folio,checkout.Confirm(original).Folio);checkout.Acknowledge(first.OperationId);Assert.AreEqual(before,Snapshot(db));
        Assert.ThrowsExactly<CheckoutException>(()=>checkout.Confirm(original with{Received=new Money(10001)}));Assert.AreEqual(before,Snapshot(db));
        inventory.UnlinkProductFromInventoryItem(product.Id);inventory.LinkProductToInventoryItem(product.Id,item.Id,3);config.Change(ConsumptionAction.Pause,config.GetStatus());
        before=Snapshot(db);Assert.AreEqual(first.Id,checkout.Confirm(original).Id);checkout.Acknowledge(first.OperationId);Assert.AreEqual(before,Snapshot(db));
        Sell(1);Assert.AreEqual(8L,Sql("SELECT quantity_atomic FROM inventory_balance"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale_inventory_evaluation WHERE status='DISABLED'"));
        inventory.UnlinkProductFromInventoryItem(product.Id);var paused=config.GetStatus();before=Snapshot(db);
        Assert.AreEqual(ConsumptionError.PreflightBlocked,Assert.ThrowsExactly<ConsumptionException>(()=>config.Change(ConsumptionAction.Resume,paused)).Error);Assert.AreEqual(before,Snapshot(db));
        inventory.LinkProductToInventoryItem(product.Id,item.Id,1);config.Change(ConsumptionAction.Resume,paused);Sell(1);
        Assert.AreEqual(7L,Sql("SELECT quantity_atomic FROM inventory_balance"));Assert.AreEqual(7600L,work.Read(s=>s.CashSalesTotal(cash.Id)).Cents);
        // Desde el 22/10/2026 una venta sin existencia ya no se bloquea (CheckoutInventoryTests.SaleWithoutStockSellsAndLeavesAnAdjustmentWarning).
        before=Snapshot(db);
        var reopened=new Database(Path.Combine(directory,"casamiga.db"));reopened.Initialize();reopened.Validate();Assert.AreEqual(before,Snapshot(reopened));
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var backupHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(backup)));var size=new FileInfo(backup).Length;
        var restoredPath=Path.Combine(directory,"restored");InstallationBackup.Restore(backup,restoredPath);var restored=new Database(Path.Combine(restoredPath,"casamiga.db"));restored.Validate();
        Assert.AreEqual(before,Snapshot(restored));Assert.IsEmpty(new SqliteInventoryStore(restored,clock).Reconcile());
        var rw=new SqliteLocalUnitOfWork(restored);var ru=new UserSession(new CheckoutJournal(restoredPath));Assert.IsTrue(new IdentityService(rw,new LocalPasswords(),clock,ru).Login("SMOKECASHIER",password));
        var recovery=new CheckoutService(rw,ru,clock,terminal.Id,new CheckoutJournal(restoredPath));var afterLogin=Snapshot(restored);
        Assert.AreEqual(first.Folio,recovery.Confirm(original).Folio);recovery.Acknowledge(first.OperationId);Assert.AreEqual(afterLogin,Snapshot(restored));
        // Administrator reprints through a fake transport; UNKNOWN cannot alter money/stock.
        var receipts=new ReceiptService(work,user,clock,terminal.Id,TimeZoneInfo.Utc,new Settings(),new EscPosReceiptRenderer(),new UnknownTransport());
        var receiptHash=Sql("SELECT content_sha256 FROM receipt_snapshot WHERE sale_id='"+first.Id+"'");
        var print=receipts.Reprint(first.Id);Assert.AreEqual(ReceiptPrintState.Unknown,print.State);Assert.AreEqual(ReceiptPrintState.Unknown,receipts.Retry(print.RequestId).State);
        Assert.AreEqual(receiptHash,Sql("SELECT content_sha256 FROM receipt_snapshot WHERE sale_id='"+first.Id+"'"));
        Assert.AreEqual(3L,Sql("SELECT count(*) FROM sale"));Assert.AreEqual(2L,Sql("SELECT count(*) FROM inventory_operation WHERE kind='SALE_CONSUMPTION'"));
        Assert.AreEqual(7L,Sql("SELECT quantity_atomic FROM inventory_balance"));Assert.AreEqual(7600L,work.Read(s=>s.CashSalesTotal(cash.Id)).Cents);
        var closing=new CashClosingService(work,user,clock);var count=closing.SubmitCount(cash.Id,new Money(57600));Assert.AreEqual(57600L,count.Expected.Cents);Assert.AreEqual(0L,count.Difference.Cents);closing.Close(cash.Id);db.Validate();
        if(Environment.GetEnvironmentVariable("CASAMIGA_HARDENING_EVIDENCE_DIR") is {} evidence)
        {
            Directory.CreateDirectory(evidence);File.Copy(backup,Path.Combine(evidence,"phase1i5.cmbackup"),true);
            File.WriteAllText(Path.Combine(evidence,"phase1i5-integral.json"),JsonSerializer.Serialize(new{schema=15,backupBytes=size,backupSha256=backupHash,logicalSourceSha256=before,logicalRestoredSha256=before,firstOperationId=first.OperationId,firstFolio=first.Folio,sales=3,cashAppliedCents=7600,openingCents=50000,expectedCents=57600,differenceCents=0,finalStock=7,integrity="ok",foreignKeyViolations=0,reconciliationDifferences=0,retryNoMutation=true,restoreRecoveryNoMutation=true,printUnknownNoConsumption=true},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
    [TestMethod]
    public void ExactStockCheckoutReachesZero()
    {
        var inventory=new InventoryService(new SqliteInventoryStore(db,clock),work,user,clock,terminal.Id);
        var item=inventory.CreateInventoryItem("EXACT","Exact",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        inventory.RegisterOpeningBalance(new(Guid.NewGuid(),item.Id,SqliteInventoryStore.MainLocationId,2,InventoryUnits.Piece.Code));inventory.LinkProductToInventoryItem(product.Id,item.Id,1);
        var config=new SaleConsumptionService(work,work,user,clock,terminal.Id);config.Change(ConsumptionAction.Enable,config.GetStatus());pos.Add(product.Id);pos.Increment(product.Id);
        var checkout=new CheckoutService(work,user,clock,terminal.Id,new CheckoutJournal(directory));checkout.Confirm(checkout.Prepare(new Money(4000)));
        Assert.AreEqual(0L,Sql("SELECT quantity_atomic FROM inventory_balance"));Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());db.Validate();
    }
}
