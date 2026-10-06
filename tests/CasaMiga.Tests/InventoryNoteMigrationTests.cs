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
public sealed class InventoryNoteMigrationTests
{
    private string directory = null!;
    private Database db = null!;
    private readonly Guid actor = Guid.NewGuid();
    private TerminalIdentity terminal = null!;
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026,9,11,20,0,0,TimeSpan.Zero); }
    private readonly Clock clock = new();

    [TestInitialize] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CasaMigaNoteMigration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        db = new(Path.Combine(directory,"casamiga.db"));
        using(var c=db.Open())
        using(var tx=c.BeginTransaction())
        {
            Execute(c,tx,"CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT");
            var assembly=typeof(Database).Assembly;
            for(int v=1;v<=9;v++)
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

    [TestMethod]
    [DataRow("RECEIPT",null,null,true)]
    [DataRow("RECEIPT",null,"Entrega parcial",true)]
    [DataRow("RECEIPT","DAMAGED",null,false)]
    [DataRow("RECEIPT","DATA_CORRECTION",null,false)]
    [DataRow("RECEIPT",null," ",false)]
    [DataRow("OPENING_BALANCE",null,null,true)]
    [DataRow("OPENING_BALANCE",null,"No permitido",false)]
    [DataRow("WASTE","DAMAGED",null,true)]
    [DataRow("WASTE","OTHER",null,false)]
    [DataRow("WASTE","OTHER","Detalle",true)]
    [DataRow("WASTE",null,null,false)]
    [DataRow("ADJUSTMENT_GAIN","DATA_CORRECTION",null,true)]
    [DataRow("ADJUSTMENT_LOSS","PHYSICAL_COUNT_VARIANCE",null,true)]
    [DataRow("ADJUSTMENT_GAIN","OTHER",null,false)]
    [DataRow("ADJUSTMENT_LOSS","OTHER","Detalle",true)]
    [DataRow("ADJUSTMENT_LOSS",null,null,false)]
    public void CorrectedCheckPreservesReasonMatrix(string kind,string? reason,string? note,bool valid)
    {
        db.Initialize();
        using var c=db.Open();using var tx=c.BeginTransaction();
        using var cmd=new SqliteCommand("INSERT INTO inventory_operation(id,request_sha256,kind,state,actor_id,terminal_id,reason_code,note,expected_line_count,created_utc) VALUES($id,$hash,$kind,'PENDING',$actor,$terminal,$reason,$note,1,$at)",c,tx);
        cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString("D"));cmd.Parameters.AddWithValue("$hash",new string('A',64));
        cmd.Parameters.AddWithValue("$kind",kind);cmd.Parameters.AddWithValue("$actor",actor.ToString("D"));cmd.Parameters.AddWithValue("$terminal",terminal.Id.ToString("D"));
        cmd.Parameters.AddWithValue("$reason",(object?)reason??DBNull.Value);cmd.Parameters.AddWithValue("$note",(object?)note??DBNull.Value);cmd.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));
        if(valid)Assert.AreEqual(1,cmd.ExecuteNonQuery());else Assert.ThrowsExactly<SqliteException>(()=>cmd.ExecuteNonQuery());
        // A pending probe is never committed.
    }

    private void ApplyTenOnly()
    {
        using var c=db.Open(); using var tx=c.BeginTransaction();
        var assembly=typeof(Database).Assembly;
        using var reader=new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.Contains(".010_",StringComparison.Ordinal)))!);
        var sql=reader.ReadToEnd(); Execute(c,tx,sql);
        using var cmd=new SqliteCommand("INSERT INTO schema_migration VALUES(10,$hash,$at)",c,tx);
        cmd.Parameters.AddWithValue("$hash",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
        cmd.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O")); cmd.ExecuteNonQuery();tx.Commit();
    }

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
        return opening;
    }

    [TestMethod] public void UpgradePreservesAllDataSchemaObjectsAndRecovery()
    {
        var request=Seed();var before=Data();var objects=Objects();var fks=Fks();var columns=Columns();
        ApplyTenOnly();db.Validate();
        CollectionAssert.AreEquivalent(before.Keys.ToArray(),Data().Keys.ToArray());
        foreach(var pair in before)Assert.AreEqual(pair.Value,Data()[pair.Key],pair.Key);
        Assert.AreEqual(objects,Objects());Assert.AreEqual(fks,Fks());Assert.AreEqual(columns,Columns());
        var store=new SqliteInventoryStore(db,clock);Assert.IsEmpty(store.Reconcile());Assert.IsNotNull(store.Recover(request));store.Apply(request);
        Assert.AreEqual("[10]",Rows("SELECT max(version) FROM schema_migration"));
        using var c=db.Open();Assert.ThrowsExactly<SqliteException>(()=>Execute(c,null,"DELETE FROM inventory_movement"));
        Assert.ThrowsExactly<SqliteException>(()=>Execute(c,null,"UPDATE inventory_operation SET note='tamper'"));
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var target=Path.Combine(directory,"restored");InstallationBackup.Restore(backup,target);
        var restored=new Database(Path.Combine(target,"casamiga.db"));restored.Validate();
        Assert.IsEmpty(new SqliteInventoryStore(restored,clock).Reconcile());
        using var rc=restored.Open();using var q=new SqliteCommand("SELECT max(version) FROM schema_migration",rc);Assert.AreEqual((long)Database.LatestVersion,q.ExecuteScalar());
    }

    [TestMethod] public void FailureAfterRebuildRollsBackDataObjectsAndVersion()
    {
        Seed();
        using(var c=db.Open())Execute(c,null,"CREATE TRIGGER test_reject_v10 BEFORE INSERT ON schema_migration WHEN NEW.version=10 BEGIN SELECT RAISE(ABORT,'injected migration failure'); END;");
        var data=Data();var objects=Objects();var fks=Fks();
        Assert.ThrowsExactly<SqliteException>(db.Initialize);
        Assert.AreEqual("[9]",Rows("SELECT max(version) FROM schema_migration"));
        foreach(var pair in data)Assert.AreEqual(pair.Value,Data()[pair.Key]);
        Assert.AreEqual(objects,Objects());Assert.AreEqual(fks,Fks());db.Validate();
        using(var c=db.Open())Execute(c,null,"DROP TRIGGER test_reject_v10");
        ApplyTenOnly();db.Validate();Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());
    }
}
