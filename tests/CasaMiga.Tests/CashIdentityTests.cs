using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class CashIdentityTests
{
    private static readonly LocalPasswords Passwords = new();
    private string directory = null!;
    private string password = null!;
    private Database database = null!;
    private SqliteLocalUnitOfWork work = null!;
    private UserSession user = null!;
    private IdentityService identity = null!;
    private CashService cash = null!;
    private TerminalIdentity terminal = null!;
    private TestClock clock = null!;
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = new(2026,9,5,12,30,0,TimeSpan.Zero); }

    [TestInitialize] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CasaMigaTests", Guid.NewGuid().ToString("N"));
        database = new(Path.Combine(directory,"cash.db")); database.Initialize();
        work = new(database); user = new(); clock = new(); password = "Aa1!"+Guid.NewGuid().ToString("N")[..12];
        identity = new(work, Passwords, clock, user);
        identity.Bootstrap("admin", "Encargado de prueba", password);
        Assert.IsTrue(identity.Login("admin", password));
        terminal = new(Guid.NewGuid(), "CAJA-TEST");
        work.Run(store => { store.EnsureTerminal(terminal,clock.UtcNow); return true; });
        cash = new(work,user,clock);
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory,true);
    private object? Scalar(string sql)
    {
        using var c = database.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = sql; return cmd.ExecuteScalar();
    }
    private void Execute(string sql) => Scalar(sql);

    [TestMethod]
    [DataRow(0L)]
    [DataRow(12550L)]
    public void ValidOpeningPersistsExactFundActorAndDeterministicAudit(long cents)
    {
        var opened = cash.Open(terminal.Id,new Money(cents));
        Assert.AreEqual(cents, opened.Session.OpeningFund.Cents);
        Assert.AreEqual(user.Current!.Id, opened.Session.OpenedBy);
        Assert.AreEqual(clock.UtcNow, opened.Session.OpenedAt);
        Assert.AreEqual(opened.Session.Id,cash.Current(terminal.Id)!.Session.Id);
        Assert.AreEqual(1L,Scalar("SELECT count(*) FROM audit_entry WHERE action='cash.open'"));
        Assert.AreEqual(clock.UtcNow.ToString("O"),Scalar("SELECT occurred_utc FROM audit_entry WHERE action='cash.open'"));
    }
    [TestMethod] public void NegativeFundHasNoPartialRecords()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>cash.Open(terminal.Id,new Money(-1)));
        Assert.AreEqual(0L,Scalar("SELECT count(*) FROM cash_session"));
        Assert.AreEqual(0L,Scalar("SELECT count(*) FROM audit_entry WHERE action='cash.open'"));
    }
    [TestMethod] public void PermissionRevocationIsEnforcedAfterLogin()
    {
        Execute("DELETE FROM role_permission WHERE permission_name='cash.open'");
        Assert.IsFalse(cash.CanOpen());
        Assert.ThrowsExactly<AccessDeniedException>(()=>cash.Open(terminal.Id,new Money(0)));
        Assert.AreEqual(0L,Scalar("SELECT count(*) FROM cash_session"));
    }
    [TestMethod] public void ViewPermissionIsAlsoEnforced()
    {
        Execute("DELETE FROM role_permission WHERE permission_name='cash.view'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>cash.Current(terminal.Id));
    }
    [TestMethod] public void UnauthenticatedAndInactiveUsersCannotOpen()
    {
        user.SignOut(); Assert.ThrowsExactly<AccessDeniedException>(()=>cash.Open(terminal.Id,new Money(0)));
        Assert.IsTrue(identity.Login("admin",password)); Execute("UPDATE app_user SET active=0");
        Assert.ThrowsExactly<AccessDeniedException>(()=>cash.Open(terminal.Id,new Money(0)));
        Assert.IsFalse(identity.Login("admin",password)); Assert.IsNull(user.Current);
    }
    [TestMethod] public void SecondOpenIsRejectedButDifferentTerminalCanOpen()
    {
        cash.Open(terminal.Id,new Money(100));
        Assert.ThrowsExactly<CashAlreadyOpenException>(()=>cash.Open(terminal.Id,new Money(200)));
        var other = new TerminalIdentity(Guid.NewGuid(),"CAJA-2");
        work.Run(store=> {store.EnsureTerminal(other,clock.UtcNow); return true;});
        cash.Open(other.Id,new Money(200));
        Assert.AreEqual(2L,Scalar("SELECT count(*) FROM cash_session"));
    }
    [TestMethod] public void UnknownTerminalIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(()=>cash.Open(Guid.NewGuid(),new Money(0)));
        Assert.AreEqual(0L,Scalar("SELECT count(*) FROM cash_session"));
    }
    [TestMethod] public void DatabaseUniqueIndexProtectsAgainstDirectSecondInsert()
    {
        cash.Open(terminal.Id,new Money(100));
        Assert.ThrowsExactly<SqliteException>(()=>Execute("INSERT INTO cash_session(id,terminal_id,opened_by,opened_utc,opening_cents,state) SELECT 'duplicate',terminal_id,opened_by,opened_utc,opening_cents,state FROM cash_session"));
        Assert.AreEqual(1L,Scalar("SELECT count(*) FROM cash_session"));
    }
    [TestMethod] public async Task ConcurrentOpeningsProduceOneSessionAndOneAudit()
    {
        using var gate = new ManualResetEventSlim(false);
        Task<bool> Attempt() => Task.Run(()=> {gate.Wait(); try {new CashService(new SqliteLocalUnitOfWork(database),user,clock).Open(terminal.Id,new Money(0)); return true;} catch(CashAlreadyOpenException){return false;}});
        var first=Attempt(); var second=Attempt(); gate.Set();
        var results=await Task.WhenAll(first,second);
        Assert.AreEqual(1,results.Count(x=>x));
        Assert.AreEqual(1L,Scalar("SELECT count(*) FROM cash_session"));
        Assert.AreEqual(1L,Scalar("SELECT count(*) FROM audit_entry WHERE action='cash.open'"));
    }
    [TestMethod] public void AuditFailureRollsBackCashInsert()
    {
        Execute("CREATE TRIGGER test_audit_failure BEFORE INSERT ON audit_entry WHEN NEW.action='cash.open' BEGIN SELECT RAISE(ABORT,'test fault'); END");
        Assert.ThrowsExactly<SqliteException>(()=>cash.Open(terminal.Id,new Money(100)));
        Assert.AreEqual(0L,Scalar("SELECT count(*) FROM cash_session"));
        Assert.AreEqual(0L,Scalar("SELECT count(*) FROM audit_entry WHERE action='cash.open'"));
    }
    [TestMethod] public void RestartRequiresLoginAndRecoversSameSession()
    {
        var first=cash.Open(terminal.Id,new Money(50000)); user.SignOut();
        var reopened = new Database(database.FilePath); reopened.Initialize();
        var newUser = new UserSession(); var newWork=new SqliteLocalUnitOfWork(reopened);
        var newCash=new CashService(newWork,newUser,clock);
        Assert.ThrowsExactly<AccessDeniedException>(()=>newCash.Current(terminal.Id));
        Assert.IsTrue(new IdentityService(newWork,Passwords,clock,newUser).Login("admin",password));
        var restored=newCash.Current(terminal.Id)!.Session;
        Assert.AreEqual(first.Session.Id,restored.Id); Assert.AreEqual(first.Session.OpenedAt,restored.OpenedAt);
        Assert.AreEqual(50000L,restored.OpeningFund.Cents);
    }
    [TestMethod] public void ClosedSessionCannotBeReopenedEvenWithDirectSql()
    {
        cash.Open(terminal.Id,new Money(0));
        Execute("UPDATE cash_session SET state='CLOSED',closed_by=opened_by,closed_utc=opened_utc,expected_cents=0,counted_cents=0,difference_cents=0");
        Assert.ThrowsExactly<SqliteException>(()=>Execute("UPDATE cash_session SET state='OPEN',closed_by=NULL,closed_utc=NULL"));
        Assert.AreEqual("CLOSED",Scalar("SELECT state FROM cash_session"));
    }
    [TestMethod] public void HashesAreSaltedAndInvalidPasswordsFail()
    {
        string one=Passwords.Hash(password),two=Passwords.Hash(password);
        Assert.AreNotEqual(one,two); Assert.AreNotEqual(password,one);
        Assert.AreEqual(PasswordCheck.Valid,Passwords.Verify(one,password));
        Assert.AreEqual(PasswordCheck.Failed,Passwords.Verify(one,"incorrect"));
        Assert.AreEqual(PasswordCheck.Failed,Passwords.Verify("malformed",password));
        Assert.IsFalse(identity.Login("missing",password)); Assert.IsNull(user.Current);
        Assert.IsFalse(identity.Login("admin","incorrect"));
    }
    [TestMethod] public void LockoutPersistsAndExpiresWithClock()
    {
        for(int i=0;i<5;i++) Assert.IsFalse(identity.Login("admin","incorrect"));
        var restarted=new IdentityService(new SqliteLocalUnitOfWork(database),Passwords,clock,new UserSession());
        Assert.IsFalse(restarted.Login("admin",password));
        clock.UtcNow=clock.UtcNow.AddMinutes(1);
        Assert.IsTrue(restarted.Login("admin",password));
        Assert.AreEqual(0L,Scalar("SELECT failed_attempts FROM app_user"));
    }
    [TestMethod] public void BootstrapIsOneTimeAndRoleMatrixIsExplicit()
    {
        Assert.IsFalse(identity.NeedsBootstrap());
        Assert.ThrowsExactly<InvalidOperationException>(()=>identity.Bootstrap("other","Other",password));
        Assert.AreEqual(1L,Scalar("SELECT count(*) FROM app_user"));
        Assert.AreEqual(3L,Scalar("SELECT count(*) FROM app_role"));
        Assert.AreEqual(33L,Scalar("SELECT count(*) FROM permission"));
        Execute("UPDATE app_user SET role_name='Cashier'");
        Assert.IsTrue(cash.CanOpen());
        Assert.IsFalse(work.Run(s=>s.HasPermission(user.Current!.Id,"cash.close")));
    }
    [TestMethod] public void AuditCannotBeModifiedOrDeleted()
    {
        cash.Open(terminal.Id,new Money(0));
        Assert.ThrowsExactly<SqliteException>(()=>Execute("UPDATE audit_entry SET action='tamper'"));
        Assert.ThrowsExactly<SqliteException>(()=>Execute("DELETE FROM audit_entry"));
    }
    [TestMethod] public void TerminalConfigurationAndLeaseSurviveRestart()
    {
        var first=TerminalConfiguration.LoadOrCreate(directory);
        Assert.AreEqual(first,TerminalConfiguration.LoadOrCreate(directory));
        // Una segunda ventana con la misma carpeta se distingue de una falla de disco: la app sólo muestra la que ya está abierta.
        using(var lease=new DirectoryLease(directory)) Assert.ThrowsExactly<DataDirectoryInUseException>(()=>new DirectoryLease(directory));
        using var next=new DirectoryLease(directory);
    }
    [TestMethod] public void UpgradeFromVersionOnePreservesCatalogAndCreatesIdentity()
    {
        var previous=new Database(Path.Combine(directory,"previous.db"));
        var assembly=typeof(Database).Assembly;
        using var reader=new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith("001_foundation.sql",StringComparison.Ordinal)))!);
        var sql=reader.ReadToEnd(); var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
        using(var c=previous.Open())
        {
            using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.ExecuteNonQuery();
            cmd.CommandText="CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT; INSERT INTO schema_migration VALUES(1,$hash,$at); INSERT INTO category(id,name,active) VALUES('c','Pan dulce',1)";
            cmd.Parameters.AddWithValue("$hash",hash);cmd.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O",CultureInfo.InvariantCulture));cmd.ExecuteNonQuery();
        }
        previous.Initialize(); previous.Validate();
        using var connection=previous.Open();using var query=connection.CreateCommand();
        query.CommandText="SELECT name FROM category WHERE id='c'";Assert.AreEqual("Pan dulce",query.ExecuteScalar());
        query.CommandText="SELECT count(*) FROM schema_migration";Assert.AreEqual((long)Database.LatestVersion,query.ExecuteScalar());
        query.CommandText="SELECT count(*) FROM app_user";Assert.AreEqual(0L,query.ExecuteScalar());
    }
}
