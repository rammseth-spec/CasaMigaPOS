using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class FoundationTests
{
    private string directory = null!;
    private Database database = null!;
    [TestInitialize] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CasaMigaTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        database = new Database(Path.Combine(directory, "test.db"));
        database.Initialize();
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, recursive: true);

    [TestMethod] public void MoneyIsExactAndChangeIsCorrect()
    {
        Assert.AreEqual(12550L, Money.FromPesos(125.50m).Cents);
        Assert.AreEqual(1L, (Money.FromPesos(.03m) - Money.FromPesos(.02m)).Cents);
        Assert.AreEqual(7450L, Money.FromPesos(125.50m).ChangeFrom(Money.FromPesos(200m)).Cents);
        Assert.AreEqual(375L, new Money(125).Multiply(3).Cents);
    }
    [TestMethod] public void InvalidMoneyAndOverflowAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Money.FromPesos(.001m));
        Assert.ThrowsExactly<OverflowException>(() => new Money(long.MaxValue) + new Money(1));
        Assert.ThrowsExactly<OverflowException>(() => new Money(long.MaxValue).Multiply(2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Money(200).ChangeFrom(new Money(100)));
    }
    [TestMethod] public void MigrationIsRepeatableAndDetectsTampering()
    {
        database.Initialize();
        using var c = database.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM schema_migration";
        Assert.AreEqual(12L, cmd.ExecuteScalar());
        cmd.CommandText = "UPDATE schema_migration SET sha256='modified'"; cmd.ExecuteNonQuery();
        Assert.ThrowsExactly<InvalidOperationException>(database.Initialize);
    }
    [TestMethod] public void ConstraintsAndForeignKeysAreEnforced()
    {
        using var c = database.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys"; Assert.AreEqual(1L, cmd.ExecuteScalar());
        cmd.CommandText = "INSERT INTO product VALUES('p',NULL,'Concha','missing','',100,1,1,'utc','utc')";
        Assert.ThrowsExactly<SqliteException>(() => cmd.ExecuteNonQuery());
        cmd.CommandText = "INSERT INTO category(id,name,active) VALUES('c','Pan dulce',1)"; cmd.ExecuteNonQuery();
        cmd.CommandText = "INSERT INTO product VALUES('p',NULL,'Concha','c','',-1,1,1,'utc','utc')";
        Assert.ThrowsExactly<SqliteException>(() => cmd.ExecuteNonQuery());
    }
    [TestMethod] public void NativeSqliteIncludesWalResetFix()
    {
        using var c = database.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT sqlite_version()";
        var version = Version.Parse((string)cmd.ExecuteScalar()!);
        Console.WriteLine($"Native SQLite: {version}");
        Assert.IsTrue(version >= new Version(3, 51, 3), "Se requiere SQLite con corrección WAL-reset.");
    }
    [TestMethod] public void FailedMigrationRollsBackDdlAndVersionRecord()
    {
        var conflicting = new Database(Path.Combine(directory, "conflicting.db"));
        using var c = conflicting.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "CREATE TABLE category(existing_value TEXT)"; cmd.ExecuteNonQuery();
        Assert.ThrowsExactly<SqliteException>(conflicting.Initialize);
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE name IN ('terminal','schema_migration','product')";
        Assert.AreEqual(0L, cmd.ExecuteScalar());
    }
    [TestMethod] public void FutureSchemaIsRejected()
    {
        using var c = database.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO schema_migration VALUES(99,'future','utc')"; cmd.ExecuteNonQuery();
        Assert.ThrowsExactly<InvalidOperationException>(database.Initialize);
    }
    [TestMethod] public void FailedCompoundOperationRollsBack()
    {
        using var c = database.Open();
        using (var tx = c.BeginTransaction())
        {
            using var cmd = c.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO category(id,name,active) VALUES('c','Pan dulce',1)"; cmd.ExecuteNonQuery();
            cmd.CommandText = "INSERT INTO category(id,name,active) VALUES('c','Duplicada',1)";
            Assert.ThrowsExactly<SqliteException>(() => cmd.ExecuteNonQuery());
        }
        using var count = c.CreateCommand(); count.CommandText = "SELECT count(*) FROM category";
        Assert.AreEqual(0L, count.ExecuteScalar());
    }
    [TestMethod] public void BackupRestoresCommittedWalDataAndPreservesExistingFiles()
    {
        using var c = database.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO category(id,name,active) VALUES('c','Pan dulce',1)"; cmd.ExecuteNonQuery();
        var backups = new BackupService(database, Path.Combine(directory, "backups"));
        var first = backups.Create(); var second = backups.Create(); Assert.AreNotEqual(first, second);
        Assert.HasCount(2, backups.History());
        var restored = Path.Combine(directory, "restored.db");
        BackupService.RestoreToNewFile(first, restored);
        using var rc = new Database(restored).Open(); using var query = rc.CreateCommand();
        query.CommandText = "SELECT name FROM category WHERE id='c'"; Assert.AreEqual("Pan dulce", query.ExecuteScalar());
        Assert.ThrowsExactly<IOException>(() => BackupService.RestoreToNewFile(first, restored));
    }
    [TestMethod] public void CorruptBackupIsRejectedWithoutCreatingDestination()
    {
        var corrupt = Path.Combine(directory, "corrupt.db"); File.WriteAllText(corrupt, "not SQLite");
        var destination = Path.Combine(directory, "restored.db");
        Assert.ThrowsExactly<SqliteException>(() => BackupService.RestoreToNewFile(corrupt, destination));
        Assert.IsFalse(File.Exists(destination));
    }
    [TestMethod] public void StartupCreatesAutomaticBackupsAndStructuredLog()
    {
        var runtime = new FoundationRuntime(new FoundationOptions(Path.Combine(directory, "runtime")));
        Assert.IsTrue(File.Exists(runtime.Start())); Assert.IsTrue(File.Exists(runtime.Start()));
        var log = Directory.GetFiles(Path.Combine(directory, "runtime"), "*.jsonl").Single();
        Assert.HasCount(2, File.ReadAllLines(log));
    }
}
