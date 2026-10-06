using System.Security.Cryptography;
using System.Text;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class SaleContractMigrationTests
{
    private string root=null!;
    private Database db=null!;
    internal static void InitializeThrough(Database db, int version)
    {
        using var c=db.Open();using var tx=c.BeginTransaction();
        Execute(c,tx,"CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT");
        var a=typeof(Database).Assembly;
        for(int v=1;v<=version;v++)
        {
            using var r=new StreamReader(a.GetManifestResourceStream(a.GetManifestResourceNames().Single(n=>n.Contains($".{v:000}_",StringComparison.Ordinal)))!);
            var sql=r.ReadToEnd();Execute(c,tx,sql);
            Execute(c,tx,$"INSERT INTO schema_migration VALUES({v},'{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)))}','2000-01-01T00:00:00Z')");
        }
        tx.Commit();
    }
    private static void Execute(SqliteConnection c,SqliteTransaction? tx,string sql)
    {using var q=new SqliteCommand(sql,c,tx);q.ExecuteNonQuery();}
    private object? Scalar(string sql){using var c=db.Open();using var q=new SqliteCommand(sql,c);return q.ExecuteScalar();}
    [TestInitialize] public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"CasaMigaContract",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        db=new(Path.Combine(root,"test.db"));InitializeThrough(db,11);
        using var c=db.Open();using var tx=c.BeginTransaction();
        Execute(c,tx,"INSERT INTO terminal VALUES('t','T','2000-01-01'); INSERT INTO app_user(id,username,display_name,password_hash,role_name,created_utc) VALUES('u','U','U','test','Administrator','2000-01-01'); INSERT INTO cash_session(id,terminal_id,opened_by,opened_utc,opening_cents,state) VALUES('c','t','u','2000-01-01',0,'OPEN'); INSERT INTO category(id,name,active) VALUES('cat','Cat',1); INSERT INTO product(id,name,category_id,price_cents,track_inventory,active,created_utc,modified_utc) VALUES('p','P','cat',100,0,1,'2000','2000'); INSERT INTO sale_item VALUES('legacy',1,'p','P',100,1,100); INSERT INTO payment VALUES('pay','legacy','CASH',100,100,0); INSERT INTO sale VALUES('legacy','op','u','t','c',1,'2026-09-12T00:00:00Z',100,100)");tx.Commit();
    }
    [TestCleanup] public void Cleanup()=>Directory.Delete(root,true);
    private void Insert(SqliteConnection c,SqliteTransaction tx,int version,string? evaluation)
    {
        Execute(c,tx,"INSERT INTO sale_item VALUES('new',1,'p','P',100,1,100); INSERT INTO payment(id,sale_id,method,applied_cents,received_cents,change_cents) VALUES('pay2','new','CASH',100,100,0)");
        Execute(c,tx,$"INSERT INTO sale VALUES('new','op2','u','t','c',2,'2026-09-12T00:00:00Z',100,100,{version},{(evaluation is null?"NULL":"'"+evaluation+"'")})");
    }
    [TestMethod] public void LegacyAfterElevenRemainsLegacyAndColumnsAreImmutable()
    {
        db.Initialize();db.Validate();Assert.AreEqual(0L,Scalar("SELECT inventory_evaluation_contract_version FROM sale"));
        Assert.AreEqual(DBNull.Value,Scalar("SELECT inventory_evaluation_id FROM sale"));
        Assert.AreEqual(0L,Scalar("SELECT count(*) FROM sale_inventory_evaluation"));
        Assert.ThrowsExactly<SqliteException>(()=>Scalar("UPDATE sale SET inventory_evaluation_contract_version=1"));
        Assert.ThrowsExactly<SqliteException>(()=>Scalar("UPDATE sale SET inventory_evaluation_id='x'"));
    }
    [TestMethod] [DataRow(0,null)] [DataRow(1,null)]
    public void NewLegacyAndMissingPointerAreRejected(int version,string? pointer)
    {
        db.Initialize();using var c=db.Open();using var tx=c.BeginTransaction();
        Assert.ThrowsExactly<SqliteException>(()=>Insert(c,tx,version,pointer));
    }
    [TestMethod] public void MissingEvaluationFailsDeferredCommit()
    {
        db.Initialize();using var c=db.Open();using var tx=c.BeginTransaction();Insert(c,tx,1,"e");
        Assert.ThrowsExactly<SqliteException>(tx.Commit);
    }
    [TestMethod] public void CorrectEvaluationCommitsAndWrongOwnerIsRejected()
    {
        db.Initialize();using var c=db.Open();using var tx=c.BeginTransaction();Insert(c,tx,1,"e");
        Assert.ThrowsExactly<SqliteException>(()=>Execute(c,tx,$"INSERT INTO sale_inventory_evaluation VALUES('e','legacy','op',1,'DISABLED',NULL,1,'{new string('A',64)}','2026')"));
        Execute(c,tx,$"INSERT INTO sale_inventory_evaluation VALUES('e','new','op2',1,'DISABLED',NULL,1,'{new string('A',64)}','2026')");
        tx.Commit();db.Validate();
    }
    [TestMethod] public void FailedMigrationRollsBackColumnsDataAndVersion()
    {
        using(var c=db.Open())Execute(c,null,"CREATE TRIGGER fail12 BEFORE INSERT ON schema_migration WHEN NEW.version=12 BEGIN SELECT RAISE(ABORT,'test'); END");
        Assert.ThrowsExactly<SqliteException>(db.Initialize);
        Assert.AreEqual(11L,Scalar("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(0L,Scalar("SELECT count(*) FROM pragma_table_info('sale') WHERE name='inventory_evaluation_contract_version'"));
        Assert.AreEqual(100L,Scalar("SELECT total_cents FROM sale WHERE id='legacy'"));db.Validate();
    }
}
