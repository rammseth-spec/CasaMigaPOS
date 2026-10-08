using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

public sealed partial class RecipePersistenceTests
{
    private int? schemaOverride;
    protected override int? FixtureSchemaVersion => schemaOverride;

    [TestMethod]
    [DataRow(0L,1,false)] [DataRow(-1L,1,false)] [DataRow(3L,1,false)]
    [DataRow(2L,0,false)] [DataRow(2L,2,false)] [DataRow(2L,1,true)]
    public void CorrectiveMovementShapeCannotBeRelaxed(long delta,int count,bool invert)
    {
        Assert.ThrowsExactly<SqliteException>(()=>HistoricalContract(outputDelta:delta,outputLineCount:count,invertConsumption:invert));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind LIKE 'PRODUCTION_%'"));db.Validate();
    }

    [TestMethod]
    [DataRow(0L)] [DataRow(1L)] [DataRow(2L)] [DataRow(3L)]
    public void CorrectiveActualOutputContract(long actual)
    {
        var id=HistoricalContract(actual:actual);var batch=recipes.GetProductionBatch(id)!;
        Assert.AreEqual("CONFIRMED",batch.Status);Assert.IsNotNull(batch.ConsumptionOperationId);
        Assert.AreEqual(actual>0,batch.OutputOperationId.HasValue);
        Assert.AreEqual(actual,Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{finished.Id}'"));
        Assert.AreEqual(actual>0?1L:0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind='PRODUCTION_OUTPUT'"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE expected_line_count=0 OR kind='WASTE'"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_movement m JOIN inventory_operation o ON o.id=m.operation_id WHERE m.delta_atomic=0 AND o.kind LIKE 'PRODUCTION_%'"));
        Assert.AreEqual((long)batch.Inputs.Count,Sql("SELECT count(*) FROM inventory_movement m JOIN inventory_operation o ON o.id=m.operation_id WHERE o.kind='PRODUCTION_CONSUMPTION' AND m.delta_atomic<0"));
        Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());db.Validate();
        foreach(var sql in new[]{ $"UPDATE production_batch SET notes='x' WHERE id='{id}'",$"DELETE FROM production_batch WHERE id='{id}'",$"UPDATE production_output_plan SET actual_count=5 WHERE batch_id='{id}'",$"DELETE FROM production_input_snapshot WHERE batch_id='{id}'"})
            Assert.ThrowsExactly<SqliteException>(()=>Sql(sql));
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var destination=Path.Combine(directory,"zero-restored");InstallationBackup.Restore(backup,destination);
        var restored=new Database(Path.Combine(destination,"casamiga.db"));restored.Initialize();restored.Validate();
        var copy=new SqliteRecipeStore(restored).GetProductionBatch(id)!;
        Assert.AreEqual(batch.OutputOperationId,copy.OutputOperationId);Assert.AreEqual(batch.ConsumptionOperationId,copy.ConsumptionOperationId);
        CollectionAssert.AreEqual(batch.Inputs.ToArray(),copy.Inputs.ToArray());Assert.IsEmpty(new SqliteInventoryStore(restored,clock).Reconcile());
    }

    [TestMethod]
    [DataRow(0L,false,false)] [DataRow(2L,true,false)] [DataRow(2L,false,true)] [DataRow(0L,true,true)]
    public void CorrectiveInvalidOperationPresenceRollsBack(long actual,bool output,bool consumption)
    {
        Assert.ThrowsExactly<SqliteException>(()=>HistoricalContract(actual:actual,includeOutput:output,includeConsumption:consumption));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind LIKE 'PRODUCTION_%'"));
        Assert.AreEqual("DRAFT",Sql("SELECT status FROM production_batch"));db.Validate();
    }

    [TestMethod]
    [DataRow("NULL")] [DataRow("0")]
    public void CorrectiveActualChangedAfterOutputCannotConfirm(string actual)
    {
        Assert.ThrowsExactly<SqliteException>(()=>HistoricalContract(beforeSeal:(c,t,id)=>{
            using var q=c.CreateCommand();q.Transaction=t;q.CommandText=$"UPDATE production_output_plan SET actual_count={actual} WHERE batch_id='{id}'";q.ExecuteNonQuery();
        }));db.Validate();
    }

    [TestMethod] public void CorrectiveDraftRemainsUnresolvedWithoutOperations()
    {
        Publish();var id=Draft();var batch=recipes.GetProductionBatch(id)!;
        Assert.AreEqual("DRAFT",batch.Status);Assert.IsNull(batch.OutputOperationId);Assert.IsNull(batch.ConsumptionOperationId);
        Assert.AreEqual(1L,Sql($"SELECT count(*) FROM production_output_plan WHERE batch_id='{id}' AND actual_count IS NULL"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_output_plan SET actual_count=-1 WHERE batch_id='{id}'"));db.Validate();
    }

    private string AllRows(bool migrations=false, bool excludeProductionPermissions=false)
    {
        using var c=db.Open(readOnly:true);using var q=c.CreateCommand();
        q.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        var tables=new List<string>();using(var r=q.ExecuteReader())while(r.Read())tables.Add(r.GetString(0));
        var result=new List<string>();
        // Historical business snapshot excludes new photo-only tables when comparing pre-photo upgrades.
        foreach(var table in tables.Where(x=>(migrations||x!="schema_migration") && (!excludeProductionPermissions || x is not ("product_photo" or "product_photo_asset"))))
        {
            q.CommandText="SELECT * FROM \""+table+"\"";
            if(excludeProductionPermissions && table is "permission" or "role_permission")
                q.CommandText+=" WHERE "+(table=="permission"?"name":"permission_name")+" NOT IN ('recipe.view','recipe.configure','production.view','production.create','production.confirm')";
            using var r=q.ExecuteReader();var rows=new List<string>();
            while(r.Read())
            {
                var values=new object[r.FieldCount];r.GetValues(values);
                // 017 adds only a nullable note. Include its expected NULL in the legacy
                // snapshot so equality still checks every old value AND the new NULL.
                if(table=="cash_session" && !Enumerable.Range(0,r.FieldCount).Any(i=>r.GetName(i)=="discrepancy_note"))
                    values=[..values,DBNull.Value];
                rows.Add(JsonSerializer.Serialize(values));
            }
            result.Add(table+string.Join("\n",rows.Order(StringComparer.Ordinal)));
        }
        return string.Join("\n",result);
    }

    [TestMethod]
    [DataRow("upgrade")] [DataRow("midway")] [DataRow("registration")]
    public void CorrectiveUpgradePreservesHistoryAndRollback(string mode)
    {
        var old=new RecipePersistenceTests{schemaOverride=13};old.Setup();
        try
        {
            old.RecipesSetup();old.HistoricalContract();old.Draft();
            old.product=old.catalog.EditProduct(old.product.Id,old.product.CategoryId,"P1","Concha","",new Money(1900),true);
            old.inventory.LinkProductToInventoryItem(old.product.Id,old.finished.Id,1);
            var config=new SaleConsumptionService(old.work,old.work,old.user,old.clock,old.terminal.Id);
            config.Change(ConsumptionAction.Enable,config.GetStatus());var sale=old.Persist();
            old.work.Run(s=>{
                var document=ReceiptDocument.FromSale(s.FindSaleDetail(sale.Id)!,new("Synthetic","Address","Phone",null,null,"Thanks"),TimeZoneInfo.Utc);
                var json=ReceiptDocumentCodec.Serialize(document);
                s.AddReceiptSnapshot(new(sale.Id,document.Version,json,ReceiptDocumentCodec.Hash(json),sale.ConfirmedAt));return true;
            });
            Assert.AreEqual(1L,old.Sql("SELECT count(*) FROM inventory_operation WHERE kind='SALE_CONSUMPTION'"));
            var rows=old.AllRows(excludeProductionPermissions:true);
            var before=old.AllRows(true);
            var opSchema=old.Sql("SELECT sql FROM sqlite_master WHERE name='inventory_operation'");
            if(mode=="midway")
            {
                var assembly=typeof(Database).Assembly;var name=assembly.GetManifestResourceNames().Single(x=>x.Contains(".014_",StringComparison.Ordinal));
                using var reader=new StreamReader(assembly.GetManifestResourceStream(name)!);
                var script=reader.ReadToEnd().Replace("DROP TABLE production_batch;","DROP TABLE production_batch; SELECT * FROM injected_missing_table;",StringComparison.Ordinal);
                using(var c=old.db.Open())using(var t=c.BeginTransaction())using(var q=c.CreateCommand())
                {q.Transaction=t;q.CommandText=script;Assert.ThrowsExactly<SqliteException>(()=>q.ExecuteNonQuery());t.Rollback();}
                Assert.AreEqual(before,old.AllRows(true));old.db.Validate();
            }
            if(mode=="registration")
            {
                old.Sql("CREATE TRIGGER inject_014 BEFORE INSERT ON schema_migration WHEN NEW.version=14 BEGIN SELECT RAISE(ABORT,'injected'); END");
                Assert.ThrowsExactly<SqliteException>(()=>old.db.Initialize());Assert.AreEqual(before,old.AllRows(true));old.db.Validate();old.Sql("DROP TRIGGER inject_014");
            }
            old.db.Initialize();old.db.Validate();Assert.AreEqual(rows,old.AllRows(excludeProductionPermissions:true));
            Assert.AreEqual(17L,old.Sql("SELECT max(version) FROM schema_migration"));
            Assert.AreEqual(opSchema,old.Sql("SELECT sql FROM sqlite_master WHERE name='inventory_operation'"));
            Assert.AreEqual(0L,old.Sql("SELECT count(*) FROM sqlite_master WHERE name LIKE '%_014'"));
            Assert.IsEmpty(new SqliteInventoryStore(old.db,old.clock).Reconcile());
        }
        finally{old.Cleanup();}
    }
}
