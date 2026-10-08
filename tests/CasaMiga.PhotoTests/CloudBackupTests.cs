using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

/// <summary>Respaldo de la caja en la nube y restauración en una carpeta nueva (docs/BACKUPS.md).</summary>
[TestClass]
public sealed class CloudBackupTests
{
    /// <summary>La nube en memoria, con el mismo contrato que cloud/lib/backupRoutes.ts.</summary>
    private sealed class FakeCloud : ICloudBackupTransport
    {
        public readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
        public readonly Dictionary<Guid, (string Kind, string Sha, long Bytes, byte[][] Parts, bool Done)> Objects = [];
        public readonly List<(Guid Database, string Sha, string TerminalJson, int Schema, IReadOnlyList<CloudMediaEntry> Media)> Sets = [];
        public Task<byte[]> KeyAsync(CancellationToken cancel) => Task.FromResult(Key);
        public Task<string?> LastDatabaseShaAsync(CancellationToken cancel) => Task.FromResult(Sets.Count == 0 ? null : Sets[^1].Sha);
        public Task<IReadOnlyList<string>> MissingAsync(IReadOnlyCollection<string> shas, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<string>>(shas.Where(s => !Objects.Values.Any(o => o.Kind == "photo" && o.Done && o.Sha == s)).ToList());
        public Task<Guid> BeginAsync(string kind, string sha256, long bytes, int parts, CancellationToken cancel)
        { var id = Guid.NewGuid(); Objects[id] = (kind, sha256, bytes, new byte[parts][], false); return Task.FromResult(id); }
        public Task PutPartAsync(Guid objectId, int index, ReadOnlyMemory<byte> data, CancellationToken cancel) { Objects[objectId].Parts[index] = data.ToArray(); return Task.CompletedTask; }
        public Task CompleteAsync(Guid objectId, CancellationToken cancel)
        { var o = Objects[objectId]; Assert.IsTrue(o.Parts.All(p => p is not null)); Objects[objectId] = o with { Done = true }; return Task.CompletedTask; }
        public Task<Guid> CreateSetAsync(Guid databaseObject, string terminalJson, int schemaVersion, IReadOnlyList<CloudMediaEntry> media, CancellationToken cancel)
        { Sets.Add((databaseObject, Objects[databaseObject].Sha, terminalJson, schemaVersion, media)); return Task.FromResult(Guid.NewGuid()); }

        /// <summary>Lo que entrega /api/web/backup al administrador para restaurar.</summary>
        public JsonObject Detail(int set = -1)
        {
            var s = Sets[set < 0 ? Sets.Count + set : set];
            JsonArray Parts(Guid id) => new(Objects[id].Parts.Select((_, i) => (JsonNode)new JsonObject { ["url"] = $"mem://{id}/{i}" }).ToArray());
            Guid Photo(string sha) => Objects.First(o => o.Value.Kind == "photo" && o.Value.Done && o.Value.Sha == sha).Key;
            return new()
            {
                ["kind"] = "terminal", ["key"] = Convert.ToBase64String(Key), ["terminalJson"] = s.TerminalJson,
                ["database"] = new JsonObject { ["sha256"] = s.Sha, ["parts"] = Parts(s.Database) },
                ["media"] = new JsonArray(s.Media.Select(m => (JsonNode)new JsonObject { ["path"] = m.Path, ["role"] = m.Role, ["sha256"] = m.Sha256, ["bytes"] = m.Bytes, ["parts"] = Parts(Photo(m.Sha256)) }).ToArray()),
            };
        }
        public Task<byte[]> Download(string url, CancellationToken cancel)
        {
            var parts = url["mem://".Length..].Split('/');
            return Task.FromResult(Objects[Guid.Parse(parts[0])].Parts[int.Parse(parts[1])]);
        }
    }

    [TestMethod]
    public async Task BackupUploadsOnlyWhatChangedAndRestoresAFullInstallation()
    {
        using var f = new PhotoFixture();
        var photo = f.Import().Photo!;
        var cloud = new FakeCloud();
        var backup = new CloudBackupService(f.DirectoryPath, f.Db);

        var first = await backup.RunAsync(cloud);
        Assert.IsTrue(first.Uploaded); Assert.AreEqual(2, first.PhotosUploaded, "Original and thumbnail.");
        Assert.IsFalse(cloud.Objects.Values.Any(o => o.Parts.Any(p => p.AsSpan().IndexOf("SQLite format"u8) >= 0)), "Nothing travels in plain text.");
        Assert.IsFalse((await backup.RunAsync(cloud)).Uploaded, "Without changes nothing is uploaded.");

        f.Catalog.CreateProduct(f.Product.CategoryId, null, "Bolillo", "", new Money(300));
        var second = await backup.RunAsync(cloud);
        Assert.IsTrue(second.Uploaded); Assert.AreEqual(0, second.PhotosUploaded, "Photos already up are not sent again.");
        Assert.HasCount(2, cloud.Sets);
        Assert.IsFalse(Directory.Exists(Path.Combine(f.DirectoryPath, "backups")) && Directory.GetDirectories(Path.Combine(f.DirectoryPath, "backups")).Length > 0, "No temporary copy is left in the data folder.");

        var target = Path.Combine(f.Root, "restaurada");
        await CloudRestore.RebuildAsync(cloud.Detail(), cloud.Download, target);
        var restored = new Database(Path.Combine(target, "casamiga.db"));
        using (var c = restored.Open(readOnly: true)) { using var q = c.CreateCommand(); q.CommandText = "SELECT count(*) FROM product"; Assert.AreEqual(2L, q.ExecuteScalar()); }
        var original = Path.Combine(target, ProductPhotoFileStore.OriginalPath(photo).Replace('/', Path.DirectorySeparatorChar));
        Assert.AreEqual(photo.OriginalSha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))));
        Assert.AreEqual(File.ReadAllText(Path.Combine(f.DirectoryPath, "terminal.json")), File.ReadAllText(Path.Combine(target, "terminal.json")));
        await Assert.ThrowsExactlyAsync<IOException>(() => CloudRestore.RebuildAsync(cloud.Detail(), cloud.Download, target), "Never over an existing folder.");

        // Un respaldo alterado en la nube no se restaura.
        var tampered = cloud.Objects[cloud.Sets[^1].Database].Parts[0];
        tampered[^1] ^= 1;
        await Assert.ThrowsExactlyAsync<AuthenticationTagMismatchException>(() => CloudRestore.RebuildAsync(cloud.Detail(), cloud.Download, Path.Combine(f.Root, "alterada")));
        Assert.IsFalse(Directory.Exists(Path.Combine(f.Root, "alterada")));
    }
}
