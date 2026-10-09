using System.Security.Cryptography;
using System.Text;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>
/// Al juntar la rama QA (fase 1K.4D) con QAv1, la nota de discrepancia pasó de la migración 17 a la 26 sin cambiar un byte.
/// Una base creada con la rama QA (17 = nota) debe abrir con todo: 17–25 de QAv1 y su 17 registrada como 26.
/// </summary>
[TestClass]
public sealed class QaBranchMigrationTests
{
    private string directory = null!;

    [TestInitialize] public void Setup() { directory = Path.Combine(Path.GetTempPath(), "CasaMigaQaBranch", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);

    private static string Script(int version)
    {
        var assembly = typeof(Database).Assembly;
        using var reader = new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.Contains($".{version:000}_", StringComparison.Ordinal)))!);
        return reader.ReadToEnd();
    }
    private static string Hash(string sql) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));

    /// <summary>Base como la deja la rama QA: 1–16 comunes y, como 17, la nota de discrepancia (hoy 026).</summary>
    private Database QaBranchDatabase(string seventeenth)
    {
        var db = new Database(Path.Combine(directory, "casamiga.db"));
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        void Run(string sql) { using var cmd = new SqliteCommand(sql, c, tx); cmd.ExecuteNonQuery(); }
        void Record(int version, string sql)
        {
            using var cmd = new SqliteCommand("INSERT INTO schema_migration VALUES($v,$hash,'2026-10-08T00:00:00Z')", c, tx);
            cmd.Parameters.AddWithValue("$v", version); cmd.Parameters.AddWithValue("$hash", Hash(sql)); cmd.ExecuteNonQuery();
        }
        Run("CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT");
        for (var v = 1; v <= 16; v++) { var sql = Script(v); Run(sql); Record(v, sql); }
        Run(seventeenth); Record(17, seventeenth);
        tx.Commit();
        return db;
    }

    private static object? Scalar(Database db, string sql) { using var c = db.Open(); using var cmd = new SqliteCommand(sql, c); return cmd.ExecuteScalar(); }

    [TestMethod]
    public void QaBranchDatabaseUpgradesWithEveryMigration()
    {
        Assert.AreEqual(Database.QaCashDiscrepancyNoteHash, Hash(Script(26)), "026 is QA's 017, byte for byte.");
        var db = QaBranchDatabase(Script(26));
        db.Initialize(); db.Validate();
        Assert.AreEqual((long)Database.LatestVersion, Scalar(db, "SELECT count(*) FROM schema_migration"));
        Assert.AreEqual((long)Database.LatestVersion, Scalar(db, "SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(Database.QaCashDiscrepancyNoteHash, Scalar(db, "SELECT sha256 FROM schema_migration WHERE version=26"));
        Assert.AreEqual(Hash(Script(17)), Scalar(db, "SELECT sha256 FROM schema_migration WHERE version=17"), "17 is now the temporary passwords migration.");
        Assert.AreEqual(1L, Scalar(db, "SELECT count(*) FROM pragma_table_info('cash_session') WHERE name='discrepancy_note'"));
        Assert.AreEqual(1L, Scalar(db, "SELECT count(*) FROM pragma_table_info('app_user') WHERE name='must_change_password'"));
        db.Initialize(); db.Validate();
        Assert.AreEqual((long)Database.LatestVersion, Scalar(db, "SELECT count(*) FROM schema_migration"), "Opening again changes nothing.");
    }

    [TestMethod]
    public void AnUnknownSeventeenthIsStillRejected()
    {
        var db = QaBranchDatabase("CREATE TABLE extra(x INTEGER);");
        Assert.AreEqual("Migración histórica modificada.", Assert.ThrowsExactly<InvalidOperationException>(db.Initialize).Message);
        Assert.AreEqual(17L, Scalar(db, "SELECT max(version) FROM schema_migration"), "Nothing changed: the whole upgrade is one transaction.");
    }
}
