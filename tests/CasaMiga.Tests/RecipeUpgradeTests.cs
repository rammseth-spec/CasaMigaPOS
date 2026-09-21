using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class RecipeUpgradeTests : SalesFixture
{
    protected override int? FixtureSchemaVersion => 12;
    private string PreviousRows()
    {
        using var c=db.Open(readOnly:true);using var q=c.CreateCommand();
        var all=new List<string>();
        foreach(var table in new[]{"sale","sale_item","payment","inventory_operation","inventory_movement","inventory_balance","sale_inventory_evaluation","audit_entry","schema_migration"})
        {
            q.CommandText="SELECT * FROM "+table;using var r=q.ExecuteReader();var rows=new List<string>();
            while(r.Read()){var values=new object[r.FieldCount];r.GetValues(values);rows.Add(JsonSerializer.Serialize(values));}
            all.Add(table+string.Join("\n",rows.Order(StringComparer.Ordinal)));
        }
        return string.Join("\n",all);
    }
    private void History()
    {
        product=catalog.EditProduct(product.Id,product.CategoryId,"P1","Pan","",new Money(1900),true);
        var inv=new InventoryService(new SqliteInventoryStore(db,clock),work,user,clock,terminal.Id);
        var item=inv.CreateInventoryItem("HISTORY","History",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        inv.RegisterOpeningBalance(new(Guid.NewGuid(),item.Id,SqliteInventoryStore.MainLocationId,10,"piece"));inv.LinkProductToInventoryItem(product.Id,item.Id,1);
        var config=new SaleConsumptionService(work,work,user,clock,terminal.Id);config.Change(ConsumptionAction.Enable,config.GetStatus());Persist();
    }
    [TestMethod] public void FailureMidThirteenRebuildRollsBackSchemaAndAllPreviousRows()
    {
        History();var before=PreviousRows();
        var assembly=typeof(Database).Assembly;var name=assembly.GetManifestResourceNames().Single(x=>x.Contains(".013_",StringComparison.Ordinal));
        using var reader=new StreamReader(assembly.GetManifestResourceStream(name)!);
        var script=reader.ReadToEnd().Replace("CREATE INDEX ix_inventory_operation_time","SELECT * FROM injected_missing_table; CREATE INDEX ix_inventory_operation_time",StringComparison.Ordinal);
        using(var c=db.Open())using(var t=c.BeginTransaction())using(var q=c.CreateCommand())
        {q.Transaction=t;q.CommandText=script;Assert.ThrowsExactly<SqliteException>(()=>q.ExecuteNonQuery());t.Rollback();}
        Assert.AreEqual(before,PreviousRows());Assert.AreEqual(12L,Sql("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM sqlite_master WHERE name IN('formula','production_batch','operation_013_backup')"));db.Validate();
        db.Initialize();db.Validate();Assert.AreEqual(13L,Sql("SELECT max(version) FROM schema_migration"));
    }
    [TestMethod] public void InitializeFailureAtMigrationRegistrationRollsBackAndCanRetry()
    {
        History();Sql("CREATE TRIGGER inject_013 BEFORE INSERT ON schema_migration WHEN NEW.version=13 BEGIN SELECT RAISE(ABORT,'injected'); END");
        var before=PreviousRows();Assert.ThrowsExactly<SqliteException>(()=>db.Initialize());Assert.AreEqual(before,PreviousRows());
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM sqlite_master WHERE name='formula'"));db.Validate();
        Sql("DROP TRIGGER inject_013");db.Initialize();db.Validate();Assert.AreEqual(8L,Sql("SELECT quantity_atomic FROM inventory_balance"));
    }
    [TestMethod] public void BackupTwelveRestoreThenUpgradePreservesConsumedSale()
    {
        History();var original=(string)Sql("SELECT id FROM sale")!;
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var destination=Path.Combine(directory,"restored12");InstallationBackup.Restore(backup,destination);
        var restored=new Database(Path.Combine(destination,"casamiga.db"));restored.Initialize();restored.Validate();
        using var c=restored.Open(readOnly:true);using var q=c.CreateCommand();
        q.CommandText="SELECT max(version) FROM schema_migration";Assert.AreEqual(13L,q.ExecuteScalar());
        q.CommandText="SELECT id FROM sale";Assert.AreEqual(original,q.ExecuteScalar());
        q.CommandText="SELECT quantity_atomic FROM inventory_balance";Assert.AreEqual(8L,q.ExecuteScalar());
        q.CommandText="SELECT count(*) FROM inventory_operation WHERE kind='SALE_CONSUMPTION'";Assert.AreEqual(1L,q.ExecuteScalar());
        q.CommandText="PRAGMA foreign_key_check";Assert.IsNull(q.ExecuteScalar());
    }
    [TestMethod] public void FreshThirteenHasNoRecipeSeedsOrBalanceWritingTriggers()
    {
        var fresh=new Database(Path.Combine(directory,"fresh.db"));fresh.Initialize();fresh.Validate();
        using var c=fresh.Open(readOnly:true);using var q=c.CreateCommand();
        q.CommandText="SELECT (SELECT count(*) FROM formula)+(SELECT count(*) FROM production_profile)+(SELECT count(*) FROM production_batch)+(SELECT count(*) FROM inventory_item)+(SELECT count(*) FROM product)";Assert.AreEqual(0L,q.ExecuteScalar());
        q.CommandText="SELECT sql FROM sqlite_master WHERE type='trigger'";using var r=q.ExecuteReader();
        while(r.Read())Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(r.GetString(0),@"(?i)\b(?:UPDATE|INSERT\s+INTO|DELETE\s+FROM)\s+inventory_balance\b"));
    }
}
