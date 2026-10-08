using System.IO;
using System.Security.Cryptography;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

/// <summary>La versión web muestra la miniatura de cada producto: viaja en la cola de sincronización.</summary>
[TestClass]
public sealed class SyncPhotoPayloadTests
{
    [TestMethod]
    public void CurrentPhotoTravelsAsVerifiedThumbnailAndAMissingFileDoesNotBlockTheQueue()
    {
        using var f = new PhotoFixture();
        var photo = f.Import().Photo!;
        var outbox = new SqliteSyncOutbox(f.Db);
        var entry = outbox.PendingEnvelopes(500, int.MaxValue).Last(e => e.Entry.Entity == "product_photo");
        Assert.AreEqual(f.Product.Id.ToString("D"), entry.Entry.EntityId);
        var thumbnail = Convert.FromBase64String((string)entry.Payload["thumbnail"]!);
        Assert.AreEqual(photo.ThumbnailSha256, Convert.ToHexString(SHA256.HashData(thumbnail)));
        Assert.AreEqual(photo.Id.ToString("D"), (string?)entry.Payload["asset"]!["id"]);
        Assert.IsNull(entry.Payload["asset"]!["original_name"], "Only what the web needs: no original file names or sizes.");

        // Miniatura alterada en disco: el producto viaja sin foto en vez de detener la sincronización.
        File.WriteAllBytes(Path.Combine(f.DirectoryPath, ProductPhotoFileStore.ThumbnailPath(photo).Replace('/', Path.DirectorySeparatorChar)), [1, 2, 3]);
        var tampered = outbox.PendingEnvelopes(500, int.MaxValue).Last(e => e.Entry.Entity == "product_photo");
        Assert.IsNull(tampered.Payload["thumbnail"]);
        Assert.IsNotNull(tampered.Payload["photo"]);
    }
}
