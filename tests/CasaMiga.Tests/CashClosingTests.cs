using System.IO.Compression;
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
public sealed class CashClosingTests
{
    private string root=null!,directory=null!,password=null!;
    private Database db=null!;
    private SqliteLocalUnitOfWork work=null!;
    private UserSession user=null!;
    private TerminalIdentity terminal=null!;
    private CashService cash=null!;
    private CashClosingService closing=null!;
    private TestClock clock=null!;
    private static readonly LocalPasswords Passwords=new();
    private sealed class TestClock:IClock {public DateTimeOffset UtcNow {get;set;}=new(2026,9,6,12,0,0,TimeSpan.Zero);}
    [TestInitialize] public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"CasaMigaTests",Guid.NewGuid().ToString("N"));
        directory=Path.Combine(root,"installation"); Directory.CreateDirectory(directory);
        db=new(Path.Combine(directory,"casamiga.db")); db.Initialize();
        terminal=TerminalConfiguration.LoadOrCreate(directory); work=new(db); clock=new();
        work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
        user=new(); password=Guid.NewGuid().ToString("N");
        var identity=new IdentityService(work,Passwords,clock,user); identity.Bootstrap("owner","Propietario",password);
        Assert.IsTrue(identity.Login("owner",password)); cash=new(work,user,clock); closing=new(work,user,clock);
    }
    [TestCleanup] public void Cleanup()=>Directory.Delete(root,true);
    private object? Sql(string sql)
    {using var c=db.Open();using var cmd=c.CreateCommand();cmd.CommandText=sql;return cmd.ExecuteScalar();}
    private Guid Open(long fund=50025)=>cash.Open(terminal.Id,new Money(fund)).Session.Id;
    private void OtherUser()
    {
        using var c=db.Open();using var cmd=c.CreateCommand();
        cmd.CommandText="INSERT INTO app_user(id,username,display_name,password_hash,role_name,created_utc) VALUES($id,'OTHER','Otro',$hash,'Manager',$at)";
        cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString("D"));cmd.Parameters.AddWithValue("$hash",Passwords.Hash(password));cmd.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));cmd.ExecuteNonQuery();
        Assert.IsTrue(new IdentityService(work,Passwords,clock,user).Login("other",password));
    }
    [TestMethod]
    [DataRow(50025L,0L)] [DataRow(50125L,100L)] [DataRow(49925L,-100L)] [DataRow(0L,-50025L)]
    public void CloseCalculatesExactSurplusShortageAndZeroCount(long counted,long difference)
    {
        var id=Open();var owner=user.Current!.Id;
        Assert.IsNull(closing.Resume(id));
        var result=closing.SubmitCount(id,new Money(counted));
        Assert.AreEqual(50025L,result.Expected.Cents);Assert.AreEqual(difference,result.Difference.Cents);
        Assert.AreEqual(CashState.Open,work.Run(s=>s.FindSession(id))!.State);
        clock.UtcNow=clock.UtcNow.AddHours(1);var closed=closing.Close(id);
        Assert.AreEqual(CashState.Closed,closed.State);Assert.AreEqual(owner,closed.OpenedBy);Assert.AreEqual(owner,closed.ClosedBy);
        Assert.AreEqual(clock.UtcNow,closed.ClosedAt);Assert.AreEqual(difference,closed.Difference!.Value.Cents);
        Assert.IsFalse(closed.ClosedByOtherUser);Assert.IsNull(cash.Current(terminal.Id));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='cash.close'"));
        Assert.AreEqual(clock.UtcNow.ToString("O"),Sql("SELECT occurred_utc FROM audit_entry WHERE action='cash.close'"));
    }
    [TestMethod] public void NegativeOrUnconfirmedCountCannotClose()
    {
        var id=Open();Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>closing.SubmitCount(id,new Money(-1)));
        Assert.ThrowsExactly<CashClosingException>(()=>closing.Close(id));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM cash_count"));Assert.IsNotNull(cash.Current(terminal.Id));
    }
    [TestMethod] public void CountCannotBeEditedAfterResultEvenAfterRestart()
    {
        var id=Open();closing.SubmitCount(id,new Money(40000));
        var restarted=new CashClosingService(new SqliteLocalUnitOfWork(new Database(db.FilePath)),user,clock);
        Assert.AreEqual(40000L,restarted.Resume(id)!.Count.Counted.Cents);
        Assert.ThrowsExactly<CashClosingException>(()=>restarted.SubmitCount(id,new Money(50025)));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("UPDATE cash_count SET counted_cents=50025"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("DELETE FROM cash_count"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='cash.count'"));
    }
    [TestMethod] public void OwnerPermissionIsRevalidatedAtCountAndFinalClose()
    {
        var id=Open();Sql("DELETE FROM role_permission WHERE role_name='Administrator' AND permission_name='cash.close'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>closing.SubmitCount(id,new Money(0)));
        Sql("INSERT INTO role_permission VALUES('Administrator','cash.close')");closing.SubmitCount(id,new Money(0));
        Sql("DELETE FROM role_permission WHERE role_name='Administrator' AND permission_name='cash.close'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>closing.Close(id));Assert.IsNotNull(cash.Current(terminal.Id));
    }
    [TestMethod] public void OtherUserRequiresAdditionalPermissionAndIsExplicitlyAudited()
    {
        var id=Open();var owner=user.Current!.Id;OtherUser();
        Sql("DELETE FROM role_permission WHERE role_name='Manager' AND permission_name='cash.close.other'");
        Assert.ThrowsExactly<CashClosingException>(()=>closing.SubmitCount(id,new Money(50025)));
        Sql("INSERT INTO role_permission VALUES('Manager','cash.close.other')");closing.SubmitCount(id,new Money(50025));
        Sql("DELETE FROM role_permission WHERE role_name='Manager' AND permission_name='cash.close.other'");
        Assert.ThrowsExactly<CashClosingException>(()=>closing.Close(id));
        Sql("INSERT INTO role_permission VALUES('Manager','cash.close.other')");var closed=closing.Close(id);
        Assert.AreEqual(owner,closed.OpenedBy);Assert.AreEqual(user.Current!.Id,closed.ClosedBy);Assert.IsTrue(closed.ClosedByOtherUser);
        using var json=JsonDocument.Parse((string)Sql("SELECT details_json FROM audit_entry WHERE action='cash.close'")!);
        Assert.IsTrue(json.RootElement.GetProperty("closedByOtherUser").GetBoolean());
        Assert.AreEqual(owner,json.RootElement.GetProperty("openedBy").GetGuid());
        Assert.AreEqual(user.Current.Id,json.RootElement.GetProperty("closedBy").GetGuid());
    }
    [TestMethod] public void OtherPermissionDoesNotReplaceBasePermission()
    {
        var id=Open();OtherUser();Sql("DELETE FROM role_permission WHERE role_name='Manager' AND permission_name='cash.close'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>closing.SubmitCount(id,new Money(0)));
    }
    [TestMethod] public void InactiveOrSignedOutActorCannotClose()
    {
        var id=Open();closing.SubmitCount(id,new Money(0));Sql("UPDATE app_user SET active=0");
        Assert.ThrowsExactly<AccessDeniedException>(()=>closing.Close(id));user.SignOut();
        Assert.ThrowsExactly<AccessDeniedException>(()=>closing.Close(id));
    }
    [TestMethod] public void AuditFailureRollsBackEntireCloseAndAllowsRetry()
    {
        var id=Open();closing.SubmitCount(id,new Money(50025));
        Sql("CREATE TRIGGER test_close_failure BEFORE INSERT ON audit_entry WHEN NEW.action='cash.close' BEGIN SELECT RAISE(ABORT,'fault'); END");
        Assert.ThrowsExactly<SqliteException>(()=>closing.Close(id));
        var stored=work.Run(s=>s.FindSession(id))!;Assert.AreEqual(CashState.Open,stored.State);Assert.IsNull(stored.ClosedAt);Assert.IsNull(stored.Expected);
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM audit_entry WHERE action='cash.close'"));
        Sql("DROP TRIGGER test_close_failure");Assert.AreEqual(CashState.Closed,closing.Close(id).State);
    }
    [TestMethod] public void CountAuditFailureDoesNotLockAnUncommittedCount()
    {
        var id=Open();Sql("CREATE TRIGGER test_count_failure BEFORE INSERT ON audit_entry WHEN NEW.action='cash.count' BEGIN SELECT RAISE(ABORT,'fault'); END");
        Assert.ThrowsExactly<SqliteException>(()=>closing.SubmitCount(id,new Money(0)));
        Assert.IsNull(closing.Resume(id));Assert.AreEqual(0L,Sql("SELECT count(*) FROM cash_count"));
    }
    [TestMethod] public void ClosedSessionPersistsAndCannotChangeOrCloseAgain()
    {
        var id=Open();closing.SubmitCount(id,new Money(50025));var original=closing.Close(id);
        user.SignOut();var next=new SqliteLocalUnitOfWork(new Database(db.FilePath));
        Assert.IsTrue(new IdentityService(next,Passwords,clock,user).Login("owner",password));
        Assert.IsNull(new CashService(next,user,clock).Current(terminal.Id));
        var historical=next.Run(s=>s.FindSession(id))!;Assert.AreEqual(CashState.Closed,historical.State);Assert.AreEqual(original.ClosedAt,historical.ClosedAt);
        Assert.ThrowsExactly<CashClosingException>(()=>closing.Close(id));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("UPDATE cash_session SET state='OPEN'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("UPDATE cash_session SET counted_cents=9"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("DELETE FROM cash_session"));
        Assert.AreNotEqual(id,Open());
    }
    [TestMethod] public async Task ConcurrentClosuresCommitOnlyOneAudit()
    {
        var id=Open();closing.SubmitCount(id,new Money(50025));using var gate=new ManualResetEventSlim(false);
        Task<bool> Attempt()=>Task.Run(()=>{gate.Wait();try{new CashClosingService(new SqliteLocalUnitOfWork(db),user,clock).Close(id);return true;}catch(CashClosingException){return false;}});
        var a=Attempt();var b=Attempt();gate.Set();var results=await Task.WhenAll(a,b);
        Assert.AreEqual(1,results.Count(x=>x));Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='cash.close'"));
    }
    [TestMethod] public void MaximumMoneyRemainsExactAndDomainRejectsInvalidTransition()
    {
        var id=Open(long.MaxValue);closing.SubmitCount(id,new Money(0));var closed=closing.Close(id);
        Assert.AreEqual(-long.MaxValue,closed.Difference!.Value.Cents);
        Assert.ThrowsExactly<InvalidOperationException>(()=>closed.Close(user.Current!.Id,clock.UtcNow,new Money(0),new Money(0)));
        Assert.ThrowsExactly<ArgumentException>(()=>Money.FromPesos(1.001m));
    }
    [TestMethod] public void CompleteBackupRestoresTerminalAndHistoricalSessionAfterOriginalLoss()
    {
        var id=Open();closing.SubmitCount(id,new Money(50025));closing.Close(id);
        string package;using(var lease=new DirectoryLease(directory)) package=new InstallationBackup(directory).Create();
        var preserved=Path.Combine(root,"saved.cmbackup");File.Copy(package,preserved);
        Directory.Delete(directory,true);
        InstallationBackup.Restore(preserved,directory);
        Assert.AreEqual(terminal,TerminalConfiguration.LoadOrCreate(directory));
        var restored=new SqliteLocalUnitOfWork(new Database(Path.Combine(directory,"casamiga.db")));
        var session=restored.Run(s=>s.FindSession(id))!;Assert.AreEqual(CashState.Closed,session.State);Assert.AreEqual(50025L,session.Counted!.Value.Cents);
        Assert.IsNull(restored.Run(s=>s.FindOpenSession(terminal.Id)));
        Assert.ThrowsExactly<IOException>(()=>InstallationBackup.Restore(preserved,directory));
    }
    [TestMethod] public void AlteredBackupIsRejectedWithoutPublishingDestination()
    {
        Open();string package;using(var lease=new DirectoryLease(directory))package=new InstallationBackup(directory).Create();
        using(var zip=ZipFile.Open(package,ZipArchiveMode.Update))
        {zip.GetEntry("terminal.json")!.Delete();using var writer=new StreamWriter(zip.CreateEntry("terminal.json").Open());writer.Write("{}");}
        var target=Path.Combine(root,"restored");Assert.ThrowsExactly<InvalidDataException>(()=>InstallationBackup.Restore(package,target));Assert.IsFalse(Directory.Exists(target));
    }
    [TestMethod] public void MissingTerminalConfigurationFailsInsteadOfCreatingNewIdentity()
    {
        Open();File.Delete(Path.Combine(directory,"terminal.json"));
        Assert.ThrowsExactly<InvalidDataException>(()=>TerminalConfiguration.LoadOrCreate(directory));
    }
    [TestMethod] public void VersionTwoUpgradePreservesOpenSessionAndOriginalMigrationHashes()
    {
        var previous=new Database(Path.Combine(root,"v2.db"));var assembly=typeof(Database).Assembly;
        using(var c=previous.Open())
        {
            using var cmd=c.CreateCommand();cmd.CommandText="CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT";cmd.ExecuteNonQuery();
            for(int i=1;i<=2;i++)
            {
                using var reader=new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.Contains($".{i:000}_",StringComparison.Ordinal)))!);
                var sql=reader.ReadToEnd();cmd.CommandText=sql;cmd.ExecuteNonQuery();cmd.CommandText="INSERT INTO schema_migration VALUES($v,$hash,$at)";
                cmd.Parameters.Clear();cmd.Parameters.AddWithValue("$v",i);cmd.Parameters.AddWithValue("$hash",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));cmd.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));cmd.ExecuteNonQuery();
            }
        }
        var oldWork=new SqliteLocalUnitOfWork(previous);oldWork.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);s.AddAdministrator(user.Current!.Id,"OLD","Old",Passwords.Hash(password),clock.UtcNow);return true;});
        var oldCash=new CashService(oldWork,user,clock);var id=oldCash.Open(terminal.Id,new Money(50025)).Session.Id;
        previous.Initialize();previous.Validate();Assert.AreEqual(id,oldCash.Current(terminal.Id)!.Session.Id);
        new CashClosingService(oldWork,user,clock).SubmitCount(id,new Money(50025));
        Assert.AreEqual(CashState.Closed,new CashClosingService(oldWork,user,clock).Close(id).State);
    }
}
