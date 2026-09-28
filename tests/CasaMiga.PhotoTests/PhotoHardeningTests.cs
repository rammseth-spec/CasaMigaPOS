using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass]
public sealed class PhotoHardeningTests
{
    private static byte[] Chunk(string type, byte[] data)
    {
        var bytes = new byte[data.Length + 12]; BinaryPrimitives.WriteInt32BigEndian(bytes, data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(bytes, 4); data.CopyTo(bytes, 8); uint crc = uint.MaxValue;
        foreach (var b in bytes.AsSpan(4, data.Length + 4)) { crc ^= b; for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1; }
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(data.Length + 8), ~crc); return bytes;
    }
    [TestMethod][DataRow(12001, 256)][DataRow(8000, 8000)][DataRow(0, 256)]
    public void PngDimensionsRejectedBeforePixelAllocation(int width, int height)
    {
        byte[] header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height); header[8] = 8; header[9] = 6;
        byte[] image = [137,80,78,71,13,10,26,10, .. Chunk("IHDR", header), .. Chunk("IEND", [])];
        Assert.ThrowsExactly<ProductPhotoException>(() => new WpfProductPhotoProcessor().Process(image, new(0, 0, 256)));
    }
    [TestMethod][DataRow(12001, 256)][DataRow(8000, 8000)]
    public void JpegDimensionsRejectedBeforePixelAllocation(int width, int height)
    {
        var image = PhotoFixture.Chart("jpeg");
        for (var i = 2; i < image.Length - 9; i++) if (image[i] == 255 && image[i + 1] is 0xC0 or 0xC2)
        { BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(i + 5), (ushort)height); BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(i + 7), (ushort)width); break; }
        Assert.ThrowsExactly<ProductPhotoException>(() => new WpfProductPhotoProcessor().Process(image, new(0, 0, 256)));
    }
    [TestMethod] public void AnimatedPngAndCrcCorruptionAreRejected()
    {
        var original = PhotoFixture.Chart(); byte[] animated = [.. original.AsSpan(0, 33), .. Chunk("acTL", new byte[8]), .. original.AsSpan(33)];
        Assert.ThrowsExactly<ProductPhotoException>(() => new WpfProductPhotoProcessor().Process(animated, new(0, 0, 256)));
        original[original.Length / 2] ^= 1;
        Assert.ThrowsExactly<ProductPhotoException>(() => new WpfProductPhotoProcessor().Process(original, new(0, 0, 256)));
    }
    [TestMethod][DataRow(-1, 0, 256)][DataRow(0, -1, 256)][DataRow(0, 0, 255)][DataRow(400, 0, 256)][DataRow(int.MaxValue, 0, 256)]
    public void CropBoundsAreExactAndOverflowSafe(int x, int y, int edge) =>
        Assert.ThrowsExactly<ProductPhotoException>(() => new WpfProductPhotoProcessor().Process(PhotoFixture.Chart(), new(x, y, edge)));
    [TestMethod] public void OrientedRectangularCropUsesNormalizedDimensions()
    {
        var image = PhotoFixture.WithOrientation(PhotoFixture.Chart("jpeg", 768, 512), 6);
        var result = new WpfProductPhotoProcessor().Process(image, new(0, 512, 256));
        Assert.AreEqual(768, result.Width); Assert.AreEqual(512, result.Height); Assert.AreEqual(new PhotoCrop(0, 512, 256), result.Crop);
        Assert.ThrowsExactly<ProductPhotoException>(() => new WpfProductPhotoProcessor().Process(image, new(512, 0, 256)));
    }
    [TestMethod] public void MigrationFailureRollsBackAllPhotoObjects()
    {
        using var f = new PhotoFixture(); var directory = Path.Combine(f.Root, "old"); PhotoFixture.BuildLegacy(directory, 15); var db = new Database(Path.Combine(directory, "casamiga.db"));
        using var c = db.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "CREATE TRIGGER reject16 BEFORE INSERT ON schema_migration WHEN NEW.version=16 BEGIN SELECT RAISE(ABORT,'injected'); END"; cmd.ExecuteNonQuery();
        Assert.ThrowsExactly<SqliteException>(() => db.Initialize()); cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE name LIKE 'product_photo%'"; Assert.AreEqual(0L, cmd.ExecuteScalar());
        cmd.CommandText = "SELECT max(version) FROM schema_migration"; Assert.AreEqual(15L, cmd.ExecuteScalar());
    }
    [TestMethod] public void SqlGuardsProtectOwnershipAndOriginalMetadata()
    {
        using var f = new PhotoFixture(); var photo = f.Import().Photo!;
        foreach (var sql in new[] { "UPDATE product_photo_asset SET provenance='UNKNOWN'", "UPDATE product_photo_asset SET original_sha256=printf('%064d',0)",
            "DELETE FROM product_photo_asset", "DELETE FROM product_photo", "UPDATE product_photo SET revision=revision+2" })
            Assert.ThrowsExactly<SqliteException>(() => f.Sql(sql));
        var other = f.Catalog.CreateProduct(f.Product.CategoryId, null, "Other", "", f.Product.SalePrice);
        Assert.ThrowsExactly<SqliteException>(() => f.Sql($"INSERT INTO product_photo SELECT '{other.Id}',current_photo_id,revision,'{Guid.NewGuid()}',modified_utc,modified_by,terminal_id FROM product_photo"));
        Assert.AreEqual(photo, f.Service.Current(f.Product.Id).Photo);
    }
    [TestMethod] public void StagingCleanupRequiresAgeAndPreservesActiveBundle()
    {
        using var f = new PhotoFixture(); var active = f.Import().Photo!;
        var staged = Path.Combine(f.DirectoryPath, "product-images", ".staging", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staged); File.WriteAllText(Path.Combine(staged, "temp"), "temporary");
        Assert.AreEqual(0, f.Service.Reconcile(true).Count); f.Clock.UtcNow = f.Clock.UtcNow.AddDays(2);
        Assert.AreEqual(1, f.Service.Reconcile(true).Count); Assert.IsFalse(Directory.Exists(staged)); Assert.IsTrue(f.Files.ReadOriginal(active).Length > 0);
    }
    [TestMethod] public void RepairRetryDoesNotIncrementAgain()
    {
        using var f = new PhotoFixture(); f.Import(); var operation = Guid.NewGuid(); var result = f.Service.Repair(f.Product.Id, 1, operation);
        Assert.AreEqual(result, f.Service.Repair(f.Product.Id, 1, operation)); Assert.AreEqual(2L, f.Service.Current(f.Product.Id).Revision);
    }
    [TestMethod][DataRow("hash")][DataRow("size")][DataRow("schema")][DataRow("role")][DataRow("path")][DataRow("null")][DataRow("duplicate")]
    public void ManipulatedManifestRejected(string mode)
    {
        using var f = new PhotoFixture(); f.Import(); var backup = new InstallationBackup(f.DirectoryPath).Create();
        using (var zip = ZipFile.Open(backup, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("manifest.json")!; JsonNode node;
            using (var input = entry.Open()) node = JsonNode.Parse(input)!;
            var files = node["Files"]!.AsArray(); var first = files[0]!;
            switch (mode)
            {
                case "hash": first["Sha256"] = new string('0', 64); break;
                case "size": first["Bytes"] = long.MaxValue; break;
                case "schema": node["SchemaVersion"] = 999; break;
                case "role": first["Role"] = "original"; break;
                case "path": first["Path"] = "../escape"; break;
                case "null": files[0] = null; break;
                case "duplicate": files[1] = first.DeepClone(); break;
            }
            entry.Delete(); using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open()); writer.Write(node.ToJsonString());
        }
        var target = Path.Combine(f.Root, "restore"); Assert.ThrowsExactly<InvalidDataException>(() => InstallationBackup.Restore(backup, target)); Assert.IsFalse(Directory.Exists(target));
    }
    [TestMethod] public void RestoreNeverOverwritesExistingInstallation()
    {
        using var f = new PhotoFixture(); var photo = f.Import().Photo!; var backup = new InstallationBackup(f.DirectoryPath).Create();
        Assert.ThrowsExactly<IOException>(() => InstallationBackup.Restore(backup, f.DirectoryPath)); Assert.AreEqual(photo, f.Service.Current(f.Product.Id).Photo);
    }
    [TestMethod] public void SmokeEvidenceCanBeRetainedWithoutRealCatalog()
    {
        using var f = new PhotoFixture(); var input = PhotoFixture.Chart(); var first = f.Import(image: input);
        CollectionAssert.AreEqual(input, f.Files.ReadOriginal(first.Photo!)); Assert.IsTrue(f.Service.ReadThumbnail(f.Product.Id)!.Length > 0);
        var replaced = f.Import(1, "REAL", PhotoFixture.Chart("jpeg")); Assert.AreNotEqual(first.Photo!.Id, replaced.Photo!.Id);
        Assert.IsNull(f.Service.Remove(f.Product.Id, 2, Guid.NewGuid()).Photo); var final = f.Import(3);
        var backup = new InstallationBackup(f.DirectoryPath).Create(); var restore = Path.Combine(f.Root, "restored"); InstallationBackup.Restore(backup, restore);
        var restored = new SqliteProductPhotoStore(new Database(Path.Combine(restore, "casamiga.db"))).Active().Single();
        CollectionAssert.AreEqual(input, new ProductPhotoFileStore(restore).ReadOriginal(restored)); Assert.AreEqual(final.Photo, restored);
        if (Environment.GetEnvironmentVariable("CASAMIGA_PHOTO_SMOKE_DIR") is {} evidence)
        {
            Directory.CreateDirectory(evidence); var copy = Path.Combine(evidence, Path.GetFileName(backup)); File.Copy(backup, copy);
            File.WriteAllText(Path.Combine(evidence, "credentials.txt"), "Synthetic backup only\nUser: photo-admin\nPassword: " + f.TestPassword);
            using var zip = ZipFile.OpenRead(copy); zip.GetEntry("manifest.json")!.ExtractToFile(Path.Combine(evidence, "manifest.json"), true);
            File.WriteAllText(Path.Combine(evidence, "smoke.json"), JsonSerializer.Serialize(new { schema = 16, synthetic = true, product = f.Product.Id,
                import = first.Photo.Id, replace = replaced.Photo.Id, removed = true, final = final.Photo!.Id, restored = restored.Id,
                file = Path.GetFileName(copy), bytes = new FileInfo(copy).Length, sha256 = PhotoFixture.Hash(File.ReadAllBytes(copy)), realBusinessSales = 0 }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
