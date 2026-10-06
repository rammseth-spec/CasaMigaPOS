using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass]
public sealed class ProductPhotoTests
{
    private PhotoFixture f = null!;
    [TestInitialize] public void Setup() => f = new();
    [TestCleanup] public void Cleanup() => f.Dispose();
    [TestMethod] public void FreshSchema16AndNoAssociation()
    { Assert.AreEqual((long)Database.LatestVersion, f.Sql("SELECT max(version) FROM schema_migration")); Assert.IsNull(f.Service.Current(f.Product.Id).Photo); }
    [TestMethod] public void Upgrade15To16PreservesMigrationHashes()
    {
        var path = Path.Combine(f.Root, "legacy"); PhotoFixture.BuildLegacy(path, 15); var db = new Database(Path.Combine(path, "casamiga.db"));
        string Hashes() { using var c = db.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT group_concat(sha256) FROM schema_migration WHERE version<=15"; return (string)q.ExecuteScalar()!; }
        var before = Hashes(); db.Initialize(); db.Validate(); Assert.AreEqual(before, Hashes());
        using var connection = db.Open(); using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT max(version) FROM schema_migration"; Assert.AreEqual((long)Database.LatestVersion, cmd.ExecuteScalar());
    }
    [TestMethod][DataRow("jpeg", "PROVISIONAL")][DataRow("png", "REAL")]
    public void ImportPreservesOriginalAndCropAndProduces512(string format, string provenance)
    {
        var original = PhotoFixture.Chart(format, 768, 768); var request = f.Request(provenance: provenance) with { Crop = new(128, 128, 600) };
        var selected = f.Service.Import(request, new MemoryStream(original)); var a = selected.Photo!;
        Assert.AreEqual(f.Product.Id, a.ProductId); Assert.AreEqual(provenance, a.Provenance); Assert.AreEqual(format, a.Format);
        CollectionAssert.AreEqual(original, f.Files.ReadOriginal(a)); Assert.AreEqual(PhotoFixture.Hash(original), a.OriginalSha256); Assert.AreEqual(request.Crop, a.Crop);
        PhotoFixture.Pixels(f.Service.ReadThumbnail(f.Product.Id)!, out int w, out int h); Assert.AreEqual(512, w); Assert.AreEqual(w, h);
        Assert.AreEqual(f.Product, f.Catalog.Products(new()).Items.Single().Product);
    }
    [TestMethod] public void NoUpscale()
    {
        var selection = f.Service.Import(f.Request(edge: 256), new MemoryStream(PhotoFixture.Chart(width: 256, height: 256)));
        Assert.AreEqual(256, selection.Photo!.ThumbnailEdge);
        PhotoFixture.Pixels(f.Service.ReadThumbnail(f.Product.Id)!, out int w, out _); Assert.AreEqual(256, w);
    }
    [TestMethod][DataRow(1)][DataRow(2)][DataRow(3)][DataRow(4)][DataRow(5)][DataRow(6)][DataRow(7)][DataRow(8)]
    public void ExifOrientationIsAppliedBeforeCrop(int orientation)
    {
        var processed = new WpfProductPhotoProcessor().Process(PhotoFixture.WithOrientation(PhotoFixture.Chart("jpeg"), orientation), new(0, 0, 512));
        Assert.AreEqual(orientation, processed.Orientation);
        var pixels = PhotoFixture.Pixels(processed.Thumbnail, out int w, out _); int i = (32 * w + 32) * 4;
        bool left = orientation is 1 or 4 or 5 or 6, top = orientation is 1 or 2 or 5 or 8;
        Assert.IsTrue(Math.Abs(pixels[i + 2] - (left ? 240 : 40)) < 12, "Red orientation channel");
        Assert.IsTrue(Math.Abs(pixels[i] - (top ? 20 : 220)) < 12, "Blue orientation channel");
    }
    [TestMethod] public void TransparentPngUsesNaturalBackground()
    {
        var result = new WpfProductPhotoProcessor().Process(PhotoFixture.Chart(alpha: 0), new(0, 0, 512));
        var pixels = PhotoFixture.Pixels(result.Thumbnail, out _, out _);
        Assert.IsTrue(Math.Abs(pixels[0] - 239) < 3); Assert.IsTrue(Math.Abs(pixels[1] - 248) < 3); Assert.IsTrue(Math.Abs(pixels[2] - 253) < 3);
    }
    [TestMethod][DataRow("unsupported")][DataRow("jpeg")][DataRow("png")]
    public void InvalidAndCorruptFormatsNeverAssociate(string kind)
    {
        var bytes = kind == "unsupported" ? new byte[] { 1, 2, 3, 4 } : PhotoFixture.Chart(kind)[..40];
        Assert.ThrowsExactly<ProductPhotoException>(() => f.Import(image: bytes)); Assert.IsNull(f.Service.Current(f.Product.Id).Photo);
    }
    [TestMethod] public void OversizeRejectedBeforeCodec()
    { Assert.ThrowsExactly<ProductPhotoException>(() => f.Import(image: new byte[ProductPhotoService.MaxOriginalBytes + 1])); Assert.IsNull(f.Service.Current(f.Product.Id).Photo); }
    [TestMethod] public void NonexistentProductRejected()
    { Assert.ThrowsExactly<ProductPhotoException>(() => f.Service.Import(f.Request() with { ProductId = Guid.NewGuid() }, new MemoryStream(PhotoFixture.Chart()))); }
    [TestMethod] public void InactiveProductCanHavePhotoWithoutReactivation()
    { f.Catalog.SetProductActive(f.Product.Id, false); f.Import(); Assert.AreEqual(0, f.Catalog.ActiveProducts().Total); Assert.IsNotNull(f.Service.Current(f.Product.Id).Photo); }
    [TestMethod][DataRow("UNKNOWN")][DataRow("")]
    public void ProvenanceRequired(string value) => Assert.ThrowsExactly<ArgumentException>(() => f.Import(provenance: value));
    [TestMethod][DataRow("C:\\private\\photo.png")][DataRow("../photo.png")][DataRow("photo:stream")]
    public void OriginalNameCannotStorePath(string value) => Assert.ThrowsExactly<ArgumentException>(() => f.Service.Import(f.Request() with { OriginalName = value }, new MemoryStream(PhotoFixture.Chart())));
    [TestMethod] public void ReplaceAndRemoveRetireOnlyUnreferencedBinariesAndAudit()
    {
        var first = f.Import(); var second = f.Import(1, "REAL");
        Assert.AreNotEqual(first.Photo!.Id, second.Photo!.Id); Assert.AreEqual(first.ProductId, second.ProductId);
        Assert.IsFalse(File.Exists(Path.Combine(f.DirectoryPath, ProductPhotoFileStore.OriginalPath(first.Photo))));
        Assert.IsTrue(File.Exists(Path.Combine(f.DirectoryPath, ProductPhotoFileStore.OriginalPath(second.Photo))));
        var removed = f.Service.Remove(f.Product.Id, 2, Guid.NewGuid()); Assert.IsNull(removed.Photo); Assert.IsNull(f.Service.ReadThumbnail(f.Product.Id));
        Assert.IsFalse(File.Exists(Path.Combine(f.DirectoryPath, ProductPhotoFileStore.OriginalPath(second.Photo))));
        Assert.AreEqual(2L, f.Sql("SELECT count(*) FROM product_photo_asset"));
        foreach (var kind in new[] { "set", "replace", "remove" }) Assert.AreEqual(1L, f.Sql($"SELECT count(*) FROM audit_entry WHERE action='product.photo.{kind}'"));
    }
    [TestMethod] public void RetryReturnsDurableOutcomeEvenAfterLaterRemove()
    {
        f.Import(); var request = f.Request(1); var bytes = PhotoFixture.Chart(); var replaced = f.Service.Import(request, new MemoryStream(bytes));
        var operation = Guid.NewGuid(); var removed = f.Service.Remove(f.Product.Id, 2, operation);
        Assert.AreEqual(replaced, f.Service.Import(request, new MemoryStream(bytes))); Assert.AreEqual(removed, f.Service.Remove(f.Product.Id, 2, operation));
        Assert.IsNull(f.Service.Current(f.Product.Id).Photo); Assert.AreEqual(3L, f.Sql("SELECT count(*) FROM audit_entry WHERE action LIKE 'product.photo.%'"));
        Assert.ThrowsExactly<ProductPhotoException>(() => f.Service.Import(request with { Provenance = "REAL" }, new MemoryStream(bytes)));
    }
    [TestMethod] public void ViewAndEditPermissionsAreRevalidated()
    {
        f.Sql("UPDATE app_user SET role_name='Cashier'"); Assert.IsNull(f.Service.Current(f.Product.Id).Photo);
        Assert.ThrowsExactly<AccessDeniedException>(() => f.Import());
        f.Sql("UPDATE app_user SET active=0"); Assert.ThrowsExactly<AccessDeniedException>(() => f.Service.Current(f.Product.Id));
    }
    private sealed class CallbackProcessor(Action callback) : IProductPhotoProcessor
    { public ProcessedPhoto Process(byte[] bytes, PhotoCrop crop) { var result = new WpfProductPhotoProcessor().Process(bytes, crop); callback(); return result; } }
    [TestMethod] public void PermissionRevocationBetweenDecodeAndCommitLeavesNoAssociation()
    {
        var service = f.WithProcessor(new CallbackProcessor(() => f.Sql("UPDATE app_user SET active=0")));
        Assert.ThrowsExactly<AccessDeniedException>(() => service.Import(f.Request(), new MemoryStream(PhotoFixture.Chart())));
        Assert.AreEqual(0L, f.Sql("SELECT count(*) FROM product_photo")); Assert.AreEqual(0L, f.Sql("SELECT count(*) FROM product_photo_asset"));
    }
    [TestMethod] public void AuditFailureRollsBackPointerAndOrphanCanBeReconciled()
    {
        var original = f.Import(); f.Sql("CREATE TRIGGER fail_photo BEFORE INSERT ON audit_entry WHEN NEW.action LIKE 'product.photo.%' BEGIN SELECT RAISE(ABORT,'injected'); END");
        var request = f.Request(1);
        Assert.ThrowsExactly<SqliteException>(() => f.Service.Import(request, new MemoryStream(PhotoFixture.Chart())));
        Assert.AreEqual(original, f.Service.Current(f.Product.Id)); Assert.IsTrue(Directory.Exists(Path.Combine(f.DirectoryPath, "product-images", "v1", request.OperationId.ToString("N"))));
        Assert.AreEqual(0, f.Service.Reconcile().Count); f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2);
        Assert.AreEqual(1, f.Service.Reconcile().Count); Assert.AreEqual(1, f.Service.Reconcile(true).Count); Assert.AreEqual(0, f.Service.Reconcile().Count);
        CollectionAssert.AreEqual(PhotoFixture.Chart(), f.Files.ReadOriginal(original.Photo!));
    }
    [TestMethod] public void RetryAfterPublishedFilesAndDbFailureIsSafe()
    {
        var request = f.Request(); var bytes = PhotoFixture.Chart();
        f.Sql("CREATE TRIGGER fail_photo BEFORE INSERT ON audit_entry WHEN NEW.action LIKE 'product.photo.%' BEGIN SELECT RAISE(ABORT,'injected'); END");
        Assert.ThrowsExactly<SqliteException>(() => f.Service.Import(request, new MemoryStream(bytes))); f.Sql("DROP TRIGGER fail_photo");
        Assert.AreEqual(request.OperationId, f.Service.Import(request, new MemoryStream(bytes)).Photo!.Id);
    }
    [TestMethod] public void ProcessingFailureLeavesNoPublishedAsset()
    {
        var service = f.WithProcessor(new CallbackProcessor(() => throw new IOException("injected")));
        Assert.ThrowsExactly<IOException>(() => service.Import(f.Request(), new MemoryStream(PhotoFixture.Chart())));
        Assert.AreEqual(0L, f.Sql("SELECT count(*) FROM product_photo_asset")); Assert.IsFalse(Directory.Exists(Path.Combine(f.DirectoryPath, "product-images")));
    }
    [TestMethod][DataRow(false)][DataRow(true)]
    public void ControlledRepairRestoresMissingOrCorruptThumbnail(bool corrupt)
    {
        var original = f.Import(); var path = Path.Combine(f.DirectoryPath, ProductPhotoFileStore.ThumbnailPath(original.Photo!));
        if (corrupt) File.WriteAllText(path, "corrupt"); else File.Delete(path);
        Assert.ThrowsExactly<InvalidDataException>(() => f.Service.ReadThumbnail(f.Product.Id));
        var result = f.Service.Repair(f.Product.Id, 1, Guid.NewGuid());
        Assert.AreEqual(original.Photo!.OriginalSha256, result.Photo!.OriginalSha256); Assert.IsNotNull(f.Service.ReadThumbnail(f.Product.Id));
        CollectionAssert.AreEqual(PhotoFixture.Chart(), f.Files.ReadOriginal(result.Photo));
    }
    [TestMethod] public void RepairRejectsCorruptOriginal()
    { var selected = f.Import(); File.WriteAllText(Path.Combine(f.DirectoryPath, ProductPhotoFileStore.OriginalPath(selected.Photo!)), "bad"); Assert.ThrowsExactly<InvalidDataException>(() => f.Service.Repair(f.Product.Id, 1, Guid.NewGuid())); }
    [TestMethod][DataRow(false)][DataRow(true)]
    public async Task ConcurrentMutationsHaveOneWinner(bool remove)
    {
        f.Import(); var bytes = PhotoFixture.Chart(); using var start = new ManualResetEventSlim();
        Task<bool> Attempt(bool second) => Task.Run(() => { start.Wait(); try { if (remove && second) f.Service.Remove(f.Product.Id, 1, Guid.NewGuid()); else f.Import(1, image: bytes); return true; } catch (ProductPhotoException) { return false; } });
        var a = Attempt(false); var b = Attempt(true); start.Set(); var outcomes = await Task.WhenAll(a, b);
        Assert.AreEqual(1, outcomes.Count(x => x)); Assert.AreEqual(2L, f.Service.Current(f.Product.Id).Revision);
        if (f.Service.Current(f.Product.Id).Photo is {} photo) Assert.IsTrue(f.Files.ReadThumbnail(photo).Length > 0);
    }
    [TestMethod] public async Task ReadVersusReplaceReturnsCompleteBytes()
    {
        var first = f.Import(); var before = f.Files.ReadThumbnail(first.Photo!);
        var replacing = Task.Run(() => f.Import(1, image: PhotoFixture.Chart(alpha: 0)));
        var read = await Task.Run(() => f.Service.ReadThumbnail(f.Product.Id)!); var after = await replacing;
        Assert.IsTrue(PhotoFixture.Hash(read) == first.Photo!.ThumbnailSha256 || PhotoFixture.Hash(read) == after.Photo!.ThumbnailSha256);
        Assert.AreEqual(after.Photo!.ThumbnailSha256, PhotoFixture.Hash(f.Service.ReadThumbnail(f.Product.Id)!));
    }
    [TestMethod] public void BackupV2SmokeIncludesOnlyActiveFilesAndRestores()
    {
        f.Import(); f.Import(1, "REAL"); f.Service.Remove(f.Product.Id, 2, Guid.NewGuid()); var current = f.Import(3);
        var orphan = Path.Combine(f.DirectoryPath, "product-images", "v1", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(orphan); File.WriteAllText(Path.Combine(orphan, "original.png"), "orphan");
        var backup = new InstallationBackup(f.DirectoryPath).Create();
        using (var zip = ZipFile.OpenRead(backup))
        {
            Assert.AreEqual(5, zip.Entries.Count); using var stream = zip.GetEntry("manifest.json")!.Open(); using var manifest = JsonDocument.Parse(stream);
            Assert.AreEqual(2, manifest.RootElement.GetProperty("Version").GetInt32());
            foreach (var entry in manifest.RootElement.GetProperty("Files").EnumerateArray())
            {
                var zipped = zip.GetEntry(entry.GetProperty("Path").GetString()!)!; using var input = zipped.Open();
                Assert.AreEqual(entry.GetProperty("Bytes").GetInt64(), zipped.Length); Assert.AreEqual(entry.GetProperty("Sha256").GetString(), Convert.ToHexString(SHA256.HashData(input)));
            }
        }
        var restored = Path.Combine(f.Root, "restored"); InstallationBackup.Restore(backup, restored);
        var repository = new SqliteProductPhotoStore(new Database(Path.Combine(restored, "casamiga.db"))); Assert.AreEqual(current.Photo, repository.Active().Single());
        CollectionAssert.AreEqual(PhotoFixture.Chart(), new ProductPhotoFileStore(restored).ReadOriginal(repository.Active().Single()));
    }
    [TestMethod] public void LegacyV1RestoresAndUpgradesWithoutPhotos()
    {
        var legacy = Path.Combine(f.Root, "legacy"); PhotoFixture.BuildLegacy(legacy, 15); var backup = new InstallationBackup(legacy).Create();
        using (var zip = ZipFile.OpenRead(backup)) Assert.AreEqual(3, zip.Entries.Count);
        var restored = Path.Combine(f.Root, "restored"); InstallationBackup.Restore(backup, restored);
        var db = new Database(Path.Combine(restored, "casamiga.db")); db.Validate(); Assert.AreEqual(0, new SqliteProductPhotoStore(db).Active().Count);
    }
    [TestMethod] public async Task ConcurrentBackupAndReplacementRestoresOneConsistentSnapshot()
    {
        var before = f.Import(); var backupTask = Task.Run(() => new InstallationBackup(f.DirectoryPath).Create()); var replacement = Task.Run(() => f.Import(1, "REAL"));
        var backup = await backupTask; var after = await replacement; var restored = Path.Combine(f.Root, "restored"); InstallationBackup.Restore(backup, restored);
        var asset = new SqliteProductPhotoStore(new Database(Path.Combine(restored, "casamiga.db"))).Active().Single();
        Assert.IsTrue(asset.Id == before.Photo!.Id || asset.Id == after.Photo!.Id); Assert.IsTrue(new ProductPhotoFileStore(restored).ReadOriginal(asset).Length > 0);
    }
    [TestMethod][DataRow(false)][DataRow(true)]
    public void CorruptOrMissingMediaRejectsRestoreWithoutDestination(bool missing)
    {
        var a = f.Import().Photo!; var backup = new InstallationBackup(f.DirectoryPath).Create();
        using (var zip = ZipFile.Open(backup, ZipArchiveMode.Update))
        { var path = ProductPhotoFileStore.ThumbnailPath(a); zip.GetEntry(path)!.Delete(); if (!missing) { using var writer = new StreamWriter(zip.CreateEntry(path).Open()); writer.Write("bad"); } }
        var destination = Path.Combine(f.Root, "restore"); Assert.ThrowsExactly<InvalidDataException>(() => InstallationBackup.Restore(backup, destination)); Assert.IsFalse(Directory.Exists(destination));
    }
    [TestMethod][DataRow("../escape")][DataRow("/absolute")][DataRow("C:/escape")][DataRow("\\\\server\\share")][DataRow("product-images/../escape")][DataRow("product-images\\..\\escape")][DataRow("product-images/x:ads")][DataRow("product-images/./escape")]
    public void TraversalEntriesAreRejected(string path)
    {
        var backup = new InstallationBackup(f.DirectoryPath).Create(); using (var zip = ZipFile.Open(backup, ZipArchiveMode.Update)) zip.CreateEntry(path);
        var destination = Path.Combine(f.Root, "restore"); Assert.ThrowsExactly<InvalidDataException>(() => InstallationBackup.Restore(backup, destination)); Assert.IsFalse(Directory.Exists(destination));
    }
    [TestMethod] public void MissingMediaPreventsPublishingNormalBackup()
    {
        var photo = f.Import().Photo!; File.Delete(Path.Combine(f.DirectoryPath, ProductPhotoFileStore.OriginalPath(photo)));
        Assert.ThrowsExactly<InvalidDataException>(() => new InstallationBackup(f.DirectoryPath).Create()); Assert.AreEqual(0, new InstallationBackup(f.DirectoryPath).History().Length);
    }
}
