using System.Security.Cryptography;
using System.Text;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class SyncOutboxTests
{
    private const string Password = "Caja#2026sync";
    private string directory = null!;
    private TestClock clock = null!;
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = new(2026,10,6,15,0,0,TimeSpan.Zero); }

    [TestInitialize] public void Setup() { directory = Path.Combine(Path.GetTempPath(), "CasaMigaTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); clock = new(); }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);

    private sealed record Installation(Database Db, SqliteLocalUnitOfWork Work, UserSession Session, IdentityService Identity, CashService Cash, TerminalIdentity Terminal);

    private Installation Open(Database db)
    {
        var work = new SqliteLocalUnitOfWork(db); var session = new UserSession();
        var identity = new IdentityService(work, new LocalPasswords(), clock, session);
        if (identity.NeedsBootstrap()) identity.Bootstrap("admin", "Administrador", Password);
        Assert.IsTrue(identity.Login("admin", Password));
        var terminal = new TerminalIdentity(Guid.NewGuid(), "CAJA-SYNC");
        work.Run(s => { s.EnsureTerminal(terminal, clock.UtcNow); return true; });
        return new(db, work, session, identity, new CashService(work, session, clock), terminal);
    }

    private static object? Scalar(Database db, string sql) { using var c = db.Open(); using var q = c.CreateCommand(); q.CommandText = sql; return q.ExecuteScalar(); }
    private static void Execute(Database db, string sql) => Scalar(db, sql);
    private static string Outbox(Database db) => (string?)Scalar(db, "SELECT group_concat(entity||':'||change, ',') FROM (SELECT * FROM sync_outbox ORDER BY seq)") ?? "";

    /// <summary>Applies the first <paramref name="count"/> migrations exactly as historical versions would have.</summary>
    private static Database BuildAtVersion(string path, int count)
    {
        var assembly = typeof(Database).Assembly;
        var scripts = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql", StringComparison.Ordinal)).Order().Take(count).ToArray();
        var db = new Database(path);
        using var c = db.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT"; cmd.ExecuteNonQuery();
        for (int i = 0; i < scripts.Length; i++)
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(scripts[i])!);
            var sql = reader.ReadToEnd(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
            cmd.CommandText = "INSERT INTO schema_migration VALUES($v,$h,'utc')"; cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$v", i + 1); cmd.Parameters.AddWithValue("$h", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
            cmd.ExecuteNonQuery(); cmd.Parameters.Clear();
        }
        return db;
    }

    [TestMethod]
    public void FreshInstallationHasExactlyOneImmutableBranch()
    {
        var db = new Database(Path.Combine(directory, "fresh.db")); db.Initialize();
        var branch = new SqliteSyncOutbox(db).Branch();
        Assert.AreEqual("Sucursal principal", branch.Name);
        Assert.AreNotEqual(Guid.Empty, branch.Id);
        Assert.ThrowsExactly<SqliteException>(() => Execute(db, $"INSERT INTO branch(id,name,created_utc) VALUES('{Guid.NewGuid()}','Otra','utc')"));
        Assert.ThrowsExactly<SqliteException>(() => Execute(db, "DELETE FROM branch"));
        Assert.ThrowsExactly<SqliteException>(() => Execute(db, $"UPDATE branch SET id='{Guid.NewGuid()}'"));
        Execute(db, "UPDATE branch SET name='Sucursal Centro'");
        Assert.AreEqual(branch.Id, new SqliteSyncOutbox(db).Branch().Id);
    }

    [TestMethod]
    public void CashLifecycleIsQueuedInSameCommitInDependencyOrder()
    {
        var db = new Database(Path.Combine(directory, "cash.db")); db.Initialize();
        var app = Open(db);
        Assert.AreEqual("audit_entry:insert", Outbox(db), "Bootstrap audit is queued.");
        var opened = app.Cash.Open(app.Terminal.Id, new Money(50000));
        var closing = new CashClosingService(app.Work, app.Session, clock);
        closing.SubmitCount(opened.Session.Id, new Money(50000));
        closing.Close(opened.Session.Id);
        Assert.AreEqual("audit_entry:insert,cash_session:insert,audit_entry:insert,cash_count:insert,audit_entry:insert,cash_session:update,audit_entry:insert", Outbox(db));
        Assert.AreEqual(7L, new SqliteSyncOutbox(db).PendingCount());
    }

    [TestMethod]
    public void RolledBackOperationLeavesNothingQueued()
    {
        var db = new Database(Path.Combine(directory, "rollback.db")); db.Initialize();
        var app = Open(db);
        var before = Outbox(db);
        var actor = app.Session.Current!.Id;
        Assert.ThrowsExactly<InvalidOperationException>(() => app.Work.Run<bool>(s =>
        {
            s.AddAudit(new(Guid.NewGuid(), actor, null, "test.partial", actor, clock.UtcNow, "{}"));
            throw new InvalidOperationException("falla a la mitad");
        }));
        Assert.AreEqual(before, Outbox(db));
        Assert.AreEqual(0L, Scalar(db, "SELECT count(*) FROM audit_entry WHERE action='test.partial'"));
    }

    [TestMethod]
    public void UpgradeQueuesExistingHistoryOnceAndKeepsItIntact()
    {
        var db = BuildAtVersion(Path.Combine(directory, "v17.db"), 17);
        var app = Open(db);
        app.Cash.Open(app.Terminal.Id, new Money(10000));
        var auditsBefore = (long)Scalar(db, "SELECT count(*) FROM audit_entry")!;

        db.Initialize(); db.Validate();
        Assert.AreEqual((long)Database.LatestVersion, Scalar(db, "SELECT max(version) FROM schema_migration"));
        Assert.AreEqual("cash_session:insert,audit_entry:insert,audit_entry:insert", Outbox(db), "Sessions are queued before audit history.");
        Assert.AreEqual(auditsBefore, Scalar(db, "SELECT count(*) FROM audit_entry"));

        db.Initialize();
        Assert.AreEqual(3L, Scalar(db, "SELECT count(*) FROM sync_outbox"), "Re-initializing must not queue history again.");
    }

    [TestMethod]
    [DataRow("midway", DisplayName = "falla a mitad del script")]
    [DataRow("registration", DisplayName = "falla al registrar migración")]
    public void FailedMigration18RollsBackCompletelyAndCanRetry(string mode)
    {
        var db = BuildAtVersion(Path.Combine(directory, "fail.db"), 17);
        Open(db);
        var audits = (long)Scalar(db, "SELECT count(*) FROM audit_entry")!;
        // midway: branch is created and seeded, then CREATE TABLE sync_outbox collides and aborts the batch.
        Execute(db, mode == "midway" ? "CREATE TABLE sync_outbox(x)"
            : "CREATE TRIGGER inject_018 BEFORE INSERT ON schema_migration WHEN NEW.version=18 BEGIN SELECT RAISE(ABORT,'injected'); END");

        Assert.ThrowsExactly<SqliteException>(() => db.Initialize());
        Execute(db, mode == "midway" ? "DROP TABLE sync_outbox" : "DROP TRIGGER inject_018");
        Assert.AreEqual(17L, Scalar(db, "SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(0L, Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name IN ('branch','sync_outbox','sync_cash_session_insert','sync_audit_entry_insert')"), "No 018 object may survive a failed migration.");
        Assert.AreEqual(audits, Scalar(db, "SELECT count(*) FROM audit_entry"));

        db.Initialize(); db.Validate();
        Assert.AreEqual((long)Database.LatestVersion, Scalar(db, "SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(audits, Scalar(db, "SELECT count(*) FROM sync_outbox WHERE entity='audit_entry'"));
    }

    [TestMethod]
    public void SentEntriesLeavePendingQueueAndPendingCannotBeDeleted()
    {
        var db = new Database(Path.Combine(directory, "send.db")); db.Initialize();
        var app = Open(db);
        app.Cash.Open(app.Terminal.Id, new Money(0));
        var outbox = new SqliteSyncOutbox(db);
        var pending = outbox.Pending(500);
        Assert.HasCount(3, pending);
        Assert.IsTrue(pending.Select(p => p.Sequence).SequenceEqual(pending.Select(p => p.Sequence).Order()));

        outbox.MarkFailed(pending[0].Sequence, "sin conexión");
        Assert.AreEqual(1, outbox.Pending(1)[0].Attempts);
        Assert.ThrowsExactly<SqliteException>(() => Execute(db, "DELETE FROM sync_outbox"));

        outbox.MarkSent([pending[0].Sequence, pending[1].Sequence], clock.UtcNow);
        Assert.AreEqual(1L, outbox.PendingCount());
        Assert.ThrowsExactly<InvalidOperationException>(() => outbox.MarkSent([pending[0].Sequence, pending[2].Sequence], clock.UtcNow), "Partial confirmations roll back as a whole.");
        Assert.AreEqual(1L, outbox.PendingCount());
        Execute(db, $"DELETE FROM sync_outbox WHERE seq={pending[0].Sequence}");
    }
}
