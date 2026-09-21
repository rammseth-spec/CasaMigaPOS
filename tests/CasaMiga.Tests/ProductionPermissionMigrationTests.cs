using CasaMiga.Application;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

public sealed partial class RecipePersistenceTests
{
    private static readonly string[] ProductionPermissions=["recipe.view","recipe.configure","production.view","production.create","production.confirm"];
    private void AssertProductionPermissionMatrix(Database database)
    {
        var security=new SqliteLocalUnitOfWork(database);
        using var c=database.Open();using var q=c.CreateCommand();
        q.CommandText="SELECT id,role_name FROM app_user";
        var users=new List<(Guid Id,string Role)>();using(var r=q.ExecuteReader())while(r.Read())users.Add((Guid.Parse(r.GetString(0)),r.GetString(1)));
        foreach(var role in new[]{"Administrator","Manager","Cashier"})
        {
            var actor=users.Single(x=>x.Role==role).Id;
            foreach(var permission in ProductionPermissions)
                Assert.AreEqual(role=="Administrator" || role=="Manager" && permission!="recipe.configure",
                    security.Read(s=>s.HasPermission(actor,permission)),role+" / "+permission);
            Assert.IsFalse(security.Read(s=>s.HasPermission(actor,"production.unknown")));
        }
        q.CommandText="SELECT count(*) FROM permission WHERE name IN ('recipe.view','recipe.configure','production.view','production.create','production.confirm')";
        Assert.AreEqual(5L,q.ExecuteScalar());
        q.CommandText="SELECT count(*) FROM role_permission WHERE permission_name IN ('recipe.view','recipe.configure','production.view','production.create','production.confirm')";
        Assert.AreEqual(9L,q.ExecuteScalar());
        q.CommandText="SELECT max(version) FROM schema_migration";Assert.AreEqual(15L,q.ExecuteScalar());
        database.Validate();
    }
    private void AddPermissionTestUsers()
    {
        using var c=db.Open();using var q=c.CreateCommand();
        foreach(var role in new[]{"Manager","Cashier"})
        {
            q.Parameters.Clear();q.CommandText="INSERT INTO app_user(id,username,display_name,password_hash,role_name,created_utc) SELECT $id,$role,$role,password_hash,$role,created_utc FROM app_user WHERE role_name='Administrator'";
            q.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());q.Parameters.AddWithValue("$role",role);q.ExecuteNonQuery();
        }
    }
    [TestMethod] public void PermissionFifteenFreshRealAuthorizationAndRestore()
    {
        AddPermissionTestUsers();AssertProductionPermissionMatrix(db);var rows=AllRows(true);
        db.Initialize();Assert.AreEqual(rows,AllRows(true));AssertProductionPermissionMatrix(db);
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var target=Path.Combine(directory,"permissions-restored");InstallationBackup.Restore(backup,target);
        var restored=new Database(Path.Combine(target,"casamiga.db"));restored.Initialize();AssertProductionPermissionMatrix(restored);
        Sql("UPDATE app_user SET active=0 WHERE role_name='Manager'");
        var manager=Guid.Parse((string)Sql("SELECT id FROM app_user WHERE role_name='Manager'")!);
        foreach(var permission in ProductionPermissions)Assert.IsFalse(work.Read(s=>s.HasPermission(manager,permission)));
    }
    [TestMethod]
    [DataRow("upgrade")] [DataRow("permission")] [DataRow("assignment")] [DataRow("registration")]
    public void PermissionFifteenUpgradeAndRollbackPreserveAllHistory(string failure)
    {
        var old=new RecipePersistenceTests{schemaOverride=14};old.Setup();
        try
        {
            old.RecipesSetup();old.AddPermissionTestUsers();old.HistoricalContract();old.Draft();var sale=old.Persist();
            old.work.Run(s=>{
                var document=ReceiptDocument.FromSale(s.FindSaleDetail(sale.Id)!,new("Synthetic","Address","Phone",null,null,"Thanks"),TimeZoneInfo.Utc);
                var json=ReceiptDocumentCodec.Serialize(document);
                s.AddReceiptSnapshot(new(sale.Id,document.Version,json,ReceiptDocumentCodec.Hash(json),sale.ConfirmedAt));return true;
            });
            var before=old.AllRows(true);var preserved=old.AllRows(excludeProductionPermissions:true);
            if(failure!="upgrade")
            {
                var trigger=failure switch {
                    "permission"=>"BEFORE INSERT ON permission WHEN NEW.name='production.view'",
                    "assignment"=>"BEFORE INSERT ON role_permission WHEN NEW.role_name='Manager' AND NEW.permission_name='production.create'",
                    _=>"BEFORE INSERT ON schema_migration WHEN NEW.version=15"};
                old.Sql("CREATE TRIGGER inject_permission_failure "+trigger+" BEGIN SELECT RAISE(ABORT,'injected'); END");
                Assert.ThrowsExactly<SqliteException>(()=>old.db.Initialize());
                Assert.AreEqual(before,old.AllRows(true));Assert.AreEqual(14L,old.Sql("SELECT max(version) FROM schema_migration"));
                old.db.Validate();old.Sql("DROP TRIGGER inject_permission_failure");
            }
            old.db.Initialize();old.AssertProductionPermissionMatrix(old.db);
            Assert.AreEqual(preserved,old.AllRows(excludeProductionPermissions:true));
            var after=old.AllRows(true);old.db.Initialize();Assert.AreEqual(after,old.AllRows(true));
        }
        finally{old.Cleanup();}
    }
}
