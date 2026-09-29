using System.IO;
using System.IO.Compression;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass]
public sealed class PhotoRecoveryTests
{
    private sealed class UncertainRepository(IProductPhotoRepository inner) : IProductPhotoRepository
    {
        public bool Armed { get; set; } = true;
        public T Run<T>(Func<IProductPhotoTransaction, T> operation)
        {
            var result = inner.Run(operation);
            if (Armed && result is PhotoSelection { Revision: > 0 }) { Armed = false; throw new IOException("Simulated lost commit response"); }
            return result;
        }
        public IReadOnlyList<PhotoAsset> Active() => inner.Active();
    }
    [TestMethod] public void LostCommitResponseRetryRecoversOneAuditedAssociation()
    {
        using var f = new PhotoFixture(); var repository = new UncertainRepository(f.Repository);
        var service = new ProductPhotoService(repository, f.Files, new WpfProductPhotoProcessor(), f.User, f.Clock, f.Terminal.Id);
        var request = f.Request(); var original = PhotoFixture.Chart();
        Assert.ThrowsExactly<IOException>(() => service.Import(request, new MemoryStream(original)));
        Assert.AreEqual(request.OperationId, service.Recover(f.Product.Id, request.OperationId)!.Result.Photo!.Id);
        var recovered = service.Import(request, new MemoryStream(original)); Assert.AreEqual(request.OperationId, recovered.Photo!.Id);
        Assert.AreEqual(1L, f.Sql("SELECT count(*) FROM product_photo_asset")); Assert.AreEqual(1L, f.Sql("SELECT count(*) FROM audit_entry WHERE action='product.photo.set'"));
        CollectionAssert.AreEqual(original, f.Files.ReadOriginal(recovered.Photo));
    }
    [TestMethod] public void StartupKeepsV1PreMigrationAndV2PostMigrationBackups()
    {
        using var f = new PhotoFixture(); var directory = Path.Combine(f.Root, "upgrade-startup"); PhotoFixture.BuildLegacy(directory, 15);
        using var lease = new DirectoryLease(directory); new FoundationRuntime(new(directory)).Start();
        var versions = new List<int>();
        foreach (var backup in new InstallationBackup(directory).History())
        { using var zip = ZipFile.OpenRead(backup); using var input = zip.GetEntry("manifest.json")!.Open(); using var json = System.Text.Json.JsonDocument.Parse(input); versions.Add(json.RootElement.GetProperty("Version").GetInt32()); }
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, versions);
    }
    private sealed class PausedProcessor(ManualResetEventSlim entered, ManualResetEventSlim resume) : IProductPhotoProcessor
    {
        public ProcessedPhoto Process(byte[] bytes, PhotoCrop crop)
        { entered.Set(); if (!resume.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException(); return new WpfProductPhotoProcessor().Process(bytes, crop); }
    }
    [TestMethod] public async Task CleanupCannotRaceAnActivePublisher()
    {
        using var f = new PhotoFixture(); using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        var service = f.WithProcessor(new PausedProcessor(entered, resume)); var bytes = PhotoFixture.Chart();
        var importing = Task.Run(() => service.Import(f.Request(), new MemoryStream(bytes)));
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2);
        var cleanup = Task.Run(() => f.Service.Reconcile(true));
        resume.Set(); var selected = await importing; await cleanup;
        Assert.AreEqual(selected.Photo, f.Service.Current(f.Product.Id).Photo); CollectionAssert.AreEqual(bytes, f.Files.ReadOriginal(selected.Photo!));
    }
    [TestMethod] public void OldDerivedFilesCleanedWithoutTouchingCurrentOriginal()
    {
        using var f = new PhotoFixture(); var photo = f.Import().Photo!;
        var old = Path.Combine(f.DirectoryPath, $"product-images/v1/{photo.Id:N}/pos-v1-{new string('A', 64)}.jpg"); File.WriteAllText(old, "retired derivation");
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2); Assert.AreEqual(1, f.Service.Reconcile(true).Count);
        Assert.IsFalse(File.Exists(old)); Assert.IsTrue(f.Files.ReadThumbnail(photo).Length > 0); Assert.IsTrue(f.Files.ReadOriginal(photo).Length > 0);
    }
    [TestMethod][DataRow("manifest.json")][DataRow("MANIFEST.JSON")]
    public void DuplicateZipEntriesRejected(string name)
    {
        using var f = new PhotoFixture(); var backup = new InstallationBackup(f.DirectoryPath).Create();
        using (var zip = ZipFile.Open(backup, ZipArchiveMode.Update)) zip.CreateEntry(name);
        Assert.ThrowsExactly<InvalidDataException>(() => InstallationBackup.Restore(backup, Path.Combine(f.Root, "restore")));
    }
    [TestMethod] public void FailedStagingPublicationLeavesDatabaseUntouched()
    {
        using var f = new PhotoFixture(); Directory.CreateDirectory(Path.Combine(f.DirectoryPath, "product-images"));
        File.WriteAllText(Path.Combine(f.DirectoryPath, "product-images", ".staging"), "simulated storage conflict");
        Assert.ThrowsExactly<IOException>(() => f.Import()); Assert.AreEqual(0L, f.Sql("SELECT count(*) FROM product_photo_asset")); Assert.IsNull(f.Service.Current(f.Product.Id).Photo);
    }
    [TestMethod] public void ManagerMayEditCashierMayOnlyRead()
    {
        using var f = new PhotoFixture(); f.Sql("UPDATE app_user SET role_name='Manager'"); var photo = f.Import().Photo!;
        f.Sql("UPDATE app_user SET role_name='Cashier'"); Assert.AreEqual(photo, f.Service.Current(f.Product.Id).Photo); Assert.IsTrue(f.Service.ReadThumbnail(f.Product.Id)!.Length > 0);
        Assert.ThrowsExactly<AccessDeniedException>(() => f.Import(1));
        Assert.ThrowsExactly<AccessDeniedException>(() => f.Service.Remove(f.Product.Id, 1, Guid.NewGuid()));
        Assert.ThrowsExactly<AccessDeniedException>(() => f.Service.Repair(f.Product.Id, 1, Guid.NewGuid()));
    }
    [TestMethod] public void ReopeningRepositoryKeepsPhotoAndOriginalIndependentOfSource()
    {
        using var f = new PhotoFixture(); var original = PhotoFixture.Chart(); var source = Path.Combine(f.Root, "import.png"); File.WriteAllBytes(source, original);
        PhotoSelection selection; using (var input = File.OpenRead(source)) selection = f.Service.Import(f.Request(), input); File.Delete(source);
        var reloaded = new SqliteProductPhotoStore(new Database(Path.Combine(f.DirectoryPath, "casamiga.db"))).Active().Single();
        Assert.AreEqual(selection.Photo, reloaded); CollectionAssert.AreEqual(original, new ProductPhotoFileStore(f.DirectoryPath).ReadOriginal(reloaded));
    }
}
