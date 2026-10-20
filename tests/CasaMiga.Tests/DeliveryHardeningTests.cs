using CasaMiga.Application;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>Arreglos para la entrega (20/10/2026): configuración dentro del respaldo, limpieza de respaldos viejos y versión visible.</summary>
[TestClass]
public sealed class DeliveryHardeningTests
{
    private string root = null!, directory = null!;
    [TestInitialize] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "CasaMigaTests", Guid.NewGuid().ToString("N"));
        directory = Path.Combine(root, "installation"); Directory.CreateDirectory(directory);
        var db = new Database(Path.Combine(directory, "casamiga.db")); db.Initialize();
        var terminal = TerminalConfiguration.LoadOrCreate(directory);
        new SqliteLocalUnitOfWork(db).Run(s => { s.EnsureTerminal(terminal, DateTimeOffset.UtcNow); return true; });
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod] public void ThePrinterAndOperationCenterSettingsAreBackedUpAndRestoredButTheCloudKeyIsNot()
    {
        File.WriteAllText(Path.Combine(directory, "printing.json"), "{\"QueueName\":\"POS-80\"}");
        File.WriteAllText(Path.Combine(directory, "operations-hub.json"), "{\"notes\":[]}");
        File.WriteAllText(Path.Combine(directory, "sync.json"), "{\"ProtectedKey\":\"local only\"}");
        string backup; using (new DirectoryLease(directory)) backup = new InstallationBackup(directory).Create();
        var restored = Path.Combine(root, "restored");
        InstallationBackup.Restore(backup, restored);
        Assert.AreEqual("{\"QueueName\":\"POS-80\"}", File.ReadAllText(Path.Combine(restored, "printing.json")));
        Assert.AreEqual("{\"notes\":[]}", File.ReadAllText(Path.Combine(restored, "operations-hub.json")));
        Assert.IsFalse(File.Exists(Path.Combine(restored, "sync.json")), "The cloud key only works on this computer; the till registers again.");
    }

    [TestMethod] public void ABackupWithoutSettingsStillWorksAndAnOversizedSettingsFileIsRefused()
    {
        string backup; using (new DirectoryLease(directory)) backup = new InstallationBackup(directory).Create();
        InstallationBackup.Restore(backup, Path.Combine(root, "plain"));
        Assert.IsFalse(File.Exists(Path.Combine(root, "plain", "printing.json")));
        File.WriteAllBytes(Path.Combine(directory, "printing.json"), new byte[1024 * 1024 + 1]);
        using (new DirectoryLease(directory)) Assert.ThrowsExactly<InvalidDataException>(() => new InstallationBackup(directory).Create());
    }

    [TestMethod] public void OldBackupsAreRemovedOnlyAfterThirtyDaysAndTheNewestTwentyAlwaysStay()
    {
        var backups = Path.Combine(root, "backups"); Directory.CreateDirectory(backups);
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        string Make(DateTime at) { var path = Path.Combine(backups, $"casamiga-{at:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.cmbackup"); File.WriteAllText(path, "x"); return path; }
        var recent = Enumerable.Range(0, 3).Select(i => Make(now.AddHours(-i))).ToArray();
        var old = Enumerable.Range(0, 25).Select(i => Make(now.AddDays(-40 - i))).ToArray();
        var foreign = Path.Combine(backups, "copia-manual.cmbackup"); File.WriteAllText(foreign, "x");

        var removed = InstallationBackup.Prune(backups, now);

        Assert.HasCount(8, removed, "28 dated backups: the newest 20 stay, the 8 oldest (all over 30 days) go.");
        CollectionAssert.AreEquivalent(old.Skip(17).ToArray(), removed.ToArray());
        Assert.IsTrue(recent.All(File.Exists));
        Assert.IsTrue(File.Exists(foreign), "Files with another name are never touched.");
        Assert.IsEmpty(InstallationBackup.Prune(backups, now.AddDays(365)), "Twenty backups always stay, however old.");
    }
}
