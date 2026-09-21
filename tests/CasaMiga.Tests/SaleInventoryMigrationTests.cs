using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class SaleInventoryMigrationTests
{
    private string directory = null!;
    private Database db = null!;
    private readonly Guid actor = Guid.NewGuid();
    private TerminalIdentity terminal = null!;
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026,9,11,20,0,0,TimeSpan.Zero); }
    private readonly Clock clock = new();

    [TestInitialize] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CasaMigaSaleInventoryMigration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        db = new(Path.Combine(directory,"casamiga.db"));
        using(var c=db.Open())
        using(var tx=c.BeginTransaction())
        {
            Execute(c,tx,"CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT");
            var assembly=typeof(Database).Assembly;
            for(int v=1;v<=10;v++)
            {
                using var reader=new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.Contains($".{v:000}_",StringComparison.Ordinal)))!);
                var sql=reader.ReadToEnd(); Execute(c,tx,sql);
                using var cmd=new SqliteCommand("INSERT INTO schema_migration VALUES($v,$hash,$at)",c,tx);
                cmd.Parameters.AddWithValue("$v",v);
                cmd.Parameters.AddWithValue("$hash",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
                cmd.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O")); cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        terminal=TerminalConfiguration.LoadOrCreate(directory);
        new SqliteLocalUnitOfWork(db).Run(s=>{s.AddAdministrator(actor,"ADMIN","Test","synthetic",clock.UtcNow);s.EnsureTerminal(terminal,clock.UtcNow);return true;});
    }
    [TestCleanup] public void Cleanup()=>Directory.Delete(directory,true);
    private static void Execute(SqliteConnection c,SqliteTransaction? tx,string sql)
    { using var cmd=new SqliteCommand(sql,c,tx);cmd.ExecuteNonQuery(); }
    private string Rows(string sql)
    {
        using var c=db.Open();using var cmd=new SqliteCommand(sql,c);using var r=cmd.ExecuteReader();
        var rows=new List<string>();
        while(r.Read()) rows.Add(JsonSerializer.Serialize(Enumerable.Range(0,r.FieldCount).Select(i=>r.IsDBNull(i)?null:r.GetValue(i)).ToArray()));
        return string.Join("\n",rows.Order(StringComparer.Ordinal));
    }
    private Dictionary<string,string> Data()
    {
        using var c=db.Open();using var cmd=new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name<>'schema_migration'",c);
        using var r=cmd.ExecuteReader();var names=new List<string>();while(r.Read())names.Add(r.GetString(0));
        return names.ToDictionary(n=>n,n=>Rows("SELECT * FROM \""+n+"\""));
    }
    private string Objects()=>Rows("SELECT type,name,tbl_name,sql FROM sqlite_master WHERE type IN('index','trigger')");
    private string Fks()=>Rows("SELECT m.name,p.* FROM sqlite_master m JOIN pragma_foreign_key_list(m.name) p WHERE m.type='table'");
    private string Columns()=>Rows("SELECT m.name,p.* FROM sqlite_master m JOIN pragma_table_info(m.name) p WHERE m.type='table' AND m.name<>'schema_migration'");
    private string FkContracts()=>Rows("SELECT m.name,p.\"table\",p.\"from\",p.\"to\",p.on_delete,p.on_update FROM sqlite_master m JOIN pragma_foreign_key_list(m.name) p WHERE m.type='table'");

    private InventoryOperationRequest Seed()
    {
        var store=new SqliteInventoryStore(db,clock);
        var item=new InventoryItem(Guid.NewGuid(),"CANELA","Canela",InventoryItemKind.RawMaterial,InventoryDimension.Mass,true,clock.UtcNow,clock.UtcNow);
        store.AddItem(item);store.AddItemUnit(new(Guid.NewGuid(),item,"SACO","Saco",1000));
        var opening=new InventoryOperationRequest(Guid.NewGuid(),actor,terminal.Id,InventoryMovementKind.OpeningBalance,[InventoryOperationLine.Opening(item.Id,SqliteInventoryStore.MainLocationId,InventoryCapture.Standard(0,InventoryUnits.Gram))]);
        store.Apply(opening);
        foreach(var kind in new[]{InventoryMovementKind.Receipt,InventoryMovementKind.AdjustmentGain,InventoryMovementKind.AdjustmentLoss,InventoryMovementKind.Waste})
        {
            var capture=InventoryCapture.Standard(2,InventoryUnits.Gram);
            var delta=kind is InventoryMovementKind.Receipt or InventoryMovementKind.AdjustmentGain?InventoryDelta.Increase(capture.Quantity):InventoryDelta.Decrease(capture.Quantity);
            store.Apply(new(Guid.NewGuid(),actor,terminal.Id,kind,[InventoryOperationLine.Change(item.Id,SqliteInventoryStore.MainLocationId,delta,capture)],kind==InventoryMovementKind.Waste?"DAMAGED":kind==InventoryMovementKind.Receipt?null:"DATA_CORRECTION"));
        }
        var product=Guid.NewGuid();
        new SqliteLocalUnitOfWork(db).Run(s=>{var cat=new Category(Guid.NewGuid(),"Test","",0,true,clock.UtcNow,clock.UtcNow);s.SaveCategory(cat,true);s.SaveProduct(new(product,cat.Id,null,"Test","",new(100),true,true,clock.UtcNow,clock.UtcNow),true);return true;});
        store.AddProductLink(new(product,item.Id,new(InventoryDimension.Mass,1)));
        new SqliteLocalUnitOfWork(db).Run(s=>{ var cash=CashSession.Open(Guid.NewGuid(),terminal.Id,actor,clock.UtcNow,new Money(0)); s.AddCashSession(cash); s.AddSale(new Sale(Guid.NewGuid(),Guid.NewGuid(),actor,terminal.Id,cash.Id,s.NextSaleSequence(terminal.Id),clock.UtcNow,[new SaleItem(product,"Historical",new Money(100),1)],Guid.NewGuid(),new Money(100))); return true; }); return opening;
    }

    [TestMethod] public void UpgradePreservesAllDataSchemaObjectsAndRecovery()
    {
        var request=Seed();var before=Data();var objects=Objects();var fks=FkContracts();var columns=Columns();
        using(var connection=db.Open())
        using(var transaction=connection.BeginTransaction())
        {
            var assembly=typeof(Database).Assembly;
            using var reader=new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.Contains(".011_",StringComparison.Ordinal)))!);
            var sql=reader.ReadToEnd();Execute(connection,transaction,sql);
            using var command=new SqliteCommand("INSERT INTO schema_migration VALUES(11,$hash,$at)",connection,transaction);
            command.Parameters.AddWithValue("$hash",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
            command.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));command.ExecuteNonQuery();transaction.Commit();
        }
        db.Validate();
        Assert.IsTrue(before.Keys.All(Data().ContainsKey));
        foreach(var pair in before.Where(x=>x.Key is not ("permission" or "role_permission" or "inventory_operation")))Assert.AreEqual(pair.Value,Data()[pair.Key],pair.Key); Assert.AreEqual(before["inventory_operation"].Replace("]",",null]"),Data()["inventory_operation"]);
        Assert.AreEqual("[0]",Rows("SELECT count(*) FROM sale_inventory_evaluation"));
        var afterObjects=Objects().Split('\n');
        foreach(var definition in objects.Split('\n').Where(x=>!x.Contains("inventory_operation_confirm_guard",StringComparison.Ordinal)))
            CollectionAssert.Contains(afterObjects,definition);
        foreach(var definition in fks.Split('\n')) CollectionAssert.Contains(FkContracts().Split('\n'),definition);
        foreach(var definition in columns.Split('\n')) CollectionAssert.Contains(Columns().Split('\n'),definition);
        var store=new SqliteInventoryStore(db,clock);Assert.IsEmpty(store.Reconcile());Assert.IsNotNull(store.Recover(request));store.Apply(request);
        Assert.AreEqual("[11]",Rows("SELECT max(version) FROM schema_migration"));
        using var c=db.Open();Assert.ThrowsExactly<SqliteException>(()=>Execute(c,null,"DELETE FROM inventory_movement"));
        Assert.ThrowsExactly<SqliteException>(()=>Execute(c,null,"UPDATE inventory_operation SET note='tamper'"));
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var target=Path.Combine(directory,"restored");InstallationBackup.Restore(backup,target);
        var restored=new Database(Path.Combine(target,"casamiga.db"));restored.Validate();
        Assert.IsEmpty(new SqliteInventoryStore(restored,clock).Reconcile());
        using var rc=restored.Open();using var q=new SqliteCommand("SELECT max(version) FROM schema_migration",rc);Assert.AreEqual(15L,q.ExecuteScalar());
    }

    [TestMethod] public void FailureAfterRebuildRollsBackDataObjectsAndVersion()
    {
        Seed();
        using(var c=db.Open())Execute(c,null,"CREATE TRIGGER test_reject_v11 BEFORE INSERT ON schema_migration WHEN NEW.version=11 BEGIN SELECT RAISE(ABORT,'injected migration failure'); END;");
        var data=Data();var objects=Objects();var fks=Fks();
        Assert.ThrowsExactly<SqliteException>(db.Initialize);
        Assert.AreEqual("[10]",Rows("SELECT max(version) FROM schema_migration"));
        foreach(var pair in data)Assert.AreEqual(pair.Value,Data()[pair.Key]);
        Assert.AreEqual(objects,Objects());Assert.AreEqual(fks,Fks());db.Validate();
        using(var c=db.Open())Execute(c,null,"DROP TRIGGER test_reject_v11");
        db.Initialize();db.Validate();Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());
    }

    [TestMethod] public void FailureDuringReconstructionRestoresDroppedTriggersAndRows()
    {
        Seed();
        using(var c=db.Open())Execute(c,null,"CREATE TABLE inventory_operation_011(test INTEGER)");
        var data=Data();var objects=Objects();var fks=Fks();
        Assert.ThrowsExactly<SqliteException>(db.Initialize);
        Assert.AreEqual("[10]",Rows("SELECT max(version) FROM schema_migration"));
        foreach(var pair in data)Assert.AreEqual(pair.Value,Data()[pair.Key]);
        Assert.AreEqual(objects,Objects());Assert.AreEqual(fks,Fks());db.Validate();
        using(var c=db.Open())Execute(c,null,"DROP TABLE inventory_operation_011");
        db.Initialize();db.Validate();Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());
    }
}
