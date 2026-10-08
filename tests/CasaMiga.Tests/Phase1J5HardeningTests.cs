using System.Security.Cryptography;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

public sealed partial class RecipePersistenceTests
{
    [TestMethod]
    public async Task Phase1J5StockChangesAfterPreviewShowsEveryShortageWithoutPartialWrites()
    {
        ProductionSetup();
        var vm=ProductionVm();await vm.LoadAsync();vm.NewProduction();
        vm.Plan[0].Profile=vm.Profiles.Single(x=>x.VersionId==profileVersion.Id);
        vm.Plan[0].Planned="2";await vm.PreviewAsync();
        Assert.IsTrue(vm.PreviewConfirmable,vm.PreviewState);
        await vm.CreateDraftAsync();Assert.IsTrue(vm.DetailIsDraft,vm.Message);
        foreach(var item in new[]{flour,water})
            inventory.AdjustToPhysicalCount(new(Guid.NewGuid(),item.Id,ProductionContract.Main,0,
                item.Dimension==InventoryDimension.Mass?"mg":"uL",AdjustmentReason.DataCorrection));
        var before=AllRows(true);
        await vm.ConfirmAsync();
        Assert.IsTrue(vm.DetailIsDraft);Assert.IsFalse(vm.MessageIsSuccess);
        StringAssert.Contains(vm.Message,flour.Name);StringAssert.Contains(vm.Message,water.Name);
        Assert.AreEqual(before,AllRows(true));
        Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());db.Validate();
    }

    [TestMethod]
    public void Phase1J5NewSessionContinuesDraftButOtherActorOrTerminalCannotConfirm()
    {
        ProductionSetup();var (batch,request)=ProductionDraft();
        var restartedDb=new Database(Path.Combine(directory,"casamiga.db"));
        var freshUser=new UserSession();
        Assert.IsTrue(new IdentityService(new SqliteLocalUnitOfWork(restartedDb),new LocalPasswords(),clock,freshUser).Login("admin",password));
        var restarted=new ProductionService(new SqliteProductionUnitOfWork(restartedDb),freshUser,clock,terminal.Id);
        var persisted=restarted.GetProduction(batch.Id)!;
        Assert.AreEqual(ProductionContract.Revision(batch),ProductionContract.Revision(persisted));
        var otherId=Guid.NewGuid();
        // Controlled fixture account, with real persisted production.confirm permission.
        Sql($"INSERT INTO app_user(id,username,display_name,password_hash,role_name,created_utc) SELECT '{otherId}','OTHER','Other manager',password_hash,'Manager',created_utc FROM app_user WHERE id='{user.Current!.Id}'");
        var otherUser=new UserSession();
        Assert.IsTrue(new IdentityService(work,new LocalPasswords(),clock,otherUser).Login("OTHER",password));
        var otherTerminal=new TerminalIdentity(Guid.NewGuid(),"Synthetic second terminal");
        work.Run(s=>{s.EnsureTerminal(otherTerminal,clock.UtcNow);return true;});
        var before=AllRows(true);
        foreach(var candidate in new[]{
            new ProductionService(new SqliteProductionUnitOfWork(restartedDb),otherUser,clock,terminal.Id),
            new ProductionService(new SqliteProductionUnitOfWork(restartedDb),freshUser,clock,otherTerminal.Id)})
            Assert.AreEqual(ProductionFailure.IdempotencyConflict,
                Assert.ThrowsExactly<ProductionException>(()=>candidate.ConfirmProduction(request)).Code);
        Assert.AreEqual(before,AllRows(true));
        var confirmed=restarted.ConfirmProduction(request);
        freshUser.SignOut();Assert.IsTrue(new IdentityService(work,new LocalPasswords(),clock,freshUser).Login("admin",password));
        Assert.AreEqual(confirmed.RequestSha256,restarted.ConfirmProduction(request).RequestSha256);
        Assert.HasCount(1,restarted.ListProductions());db.Validate();
    }

    [TestMethod]
    public void Phase1J5MixedBackupRestoresDraftPositiveZeroHistoryAndPermissions()
    {
        ProductionSetup();
        var (_,positiveRequest)=ProductionDraft(3);var positive=Production().ConfirmProduction(positiveRequest);
        var (_,zeroRequest)=ProductionDraft(0);var zero=Production().ConfirmProduction(zeroRequest);
        var (draft,_)=ProductionDraft();
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var bytes=File.ReadAllBytes(backup);Assert.IsTrue(bytes.Length>0);
        Console.WriteLine($"1J.5 backup: {Path.GetFileName(backup)}; bytes={bytes.Length}; SHA256={Convert.ToHexString(SHA256.HashData(bytes))}");
        var target=Path.Combine(directory,"mixed-restored");InstallationBackup.Restore(backup,target);
        var restored=new Database(Path.Combine(target,"casamiga.db"));restored.Validate();
        Assert.AreEqual(terminal.Id,TerminalConfiguration.LoadOrCreate(target).Id);
        var restoredUser=new UserSession();var restoredWork=new SqliteLocalUnitOfWork(restored);
        Assert.IsTrue(new IdentityService(restoredWork,new LocalPasswords(),clock,restoredUser).Login("admin",password));
        var production=new ProductionService(new SqliteProductionUnitOfWork(restored),restoredUser,clock,terminal.Id);
        var recipe=new RecipeService(new SqliteProductionUnitOfWork(restored),restoredUser,clock,terminal.Id);
        Assert.AreEqual(version.Id,recipe.GetFormula(formula.Id)!.ActiveVersionId);
        Assert.AreEqual(profileVersion.Id,recipe.GetProfile(profile.Id)!.ActiveVersionId);
        Assert.AreEqual(ProductionContract.Revision(draft),ProductionContract.Revision(production.GetProduction(draft.Id)!));
        Assert.AreEqual(positive.RequestSha256,production.ConfirmProduction(positiveRequest).RequestSha256);
        Assert.AreEqual(zero.RequestSha256,production.ConfirmProduction(zeroRequest).RequestSha256);
        Assert.IsNull(production.GetProduction(zero.Id)!.OutputOperationId);
        Assert.HasCount(3,production.ListProductions());
        Assert.IsEmpty(new SqliteInventoryStore(restored,clock).Reconcile());
        using var connection=restored.Open();using var query=connection.CreateCommand();
        query.CommandText="PRAGMA integrity_check";Assert.AreEqual("ok",query.ExecuteScalar());
        query.CommandText="PRAGMA foreign_key_check";Assert.IsNull(query.ExecuteScalar());
        query.CommandText="SELECT count(*) FROM audit_entry WHERE action='production.confirmed'";Assert.AreEqual(2L,query.ExecuteScalar());
        query.CommandText="SELECT max(version) FROM schema_migration";Assert.AreEqual(17L,query.ExecuteScalar());
        foreach(var role in new[]{"Administrator","Manager","Cashier"})
        {
            query.CommandText=$"UPDATE app_user SET role_name='{role}' WHERE id='{restoredUser.Current!.Id}'";query.ExecuteNonQuery();
            foreach(var permission in new[]{"recipe.view","recipe.configure","production.view","production.create","production.confirm"})
                Assert.AreEqual(role=="Administrator" || role=="Manager"&&permission!="recipe.configure",
                    restoredWork.Run(s=>s.HasPermission(restoredUser.Current!.Id,permission)),$"{role}: {permission}");
            if(role=="Cashier")Assert.ThrowsExactly<AccessDeniedException>(()=>production.ListProductions());
            else Assert.HasCount(3,production.ListProductions());
        }
    }
}
