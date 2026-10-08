using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

/// <summary>Formato de la nube (cloud/lib/backup.ts): "CMB1" + nonce (12) + etiqueta (16) + datos, AES-256-GCM.</summary>
public static class BackupCrypto
{
    private static readonly byte[] Magic = "CMB1"u8.ToArray();

    public static byte[] Encrypt(byte[] key, ReadOnlySpan<byte> plain)
    {
        var sealedBytes = new byte[32 + plain.Length];
        Magic.CopyTo(sealedBytes, 0);
        RandomNumberGenerator.Fill(sealedBytes.AsSpan(4, 12));
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(sealedBytes.AsSpan(4, 12), plain, sealedBytes.AsSpan(32), sealedBytes.AsSpan(16, 16));
        return sealedBytes;
    }

    public static byte[] Decrypt(byte[] key, ReadOnlySpan<byte> sealedBytes)
    {
        if (sealedBytes.Length < 32 || !sealedBytes[..4].SequenceEqual(Magic)) throw new InvalidDataException("Respaldo con formato desconocido.");
        var plain = new byte[sealedBytes.Length - 32];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(sealedBytes.Slice(4, 12), sealedBytes[32..], sealedBytes.Slice(16, 16), plain);
        return plain;
    }

    public static string Sha256(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data));
}

public sealed record CloudMediaEntry(string Path, string Role, string Sha256, long Bytes);
public sealed record CloudBackupResult(bool Uploaded, long DatabaseBytes, int PhotosUploaded);

/// <summary>Lo que la caja necesita de la nube para respaldarse (cloud/lib/backupRoutes.ts).</summary>
public interface ICloudBackupTransport
{
    Task<byte[]> KeyAsync(CancellationToken cancel);
    Task<string?> LastDatabaseShaAsync(CancellationToken cancel);
    Task<IReadOnlyList<string>> MissingAsync(IReadOnlyCollection<string> shas, CancellationToken cancel);
    Task<Guid> BeginAsync(string kind, string sha256, long bytes, int parts, CancellationToken cancel);
    Task PutPartAsync(Guid objectId, int index, ReadOnlyMemory<byte> data, CancellationToken cancel);
    Task CompleteAsync(Guid objectId, CancellationToken cancel);
    Task<Guid> CreateSetAsync(Guid databaseObject, string terminalJson, int schemaVersion, IReadOnlyList<CloudMediaEntry> media, CancellationToken cancel);
}

public sealed class HttpCloudBackupTransport(HttpClient http, Uri api, string terminalKey) : ICloudBackupTransport
{
    private async Task<JsonObject> Send(HttpMethod method, string path, HttpContent? content, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(method, new Uri(api, path)) { Content = content };
        request.Headers.Authorization = new("Bearer", terminalKey);
        using var response = await http.SendAsync(request, cancel);
        var text = await response.Content.ReadAsStringAsync(cancel);
        JsonObject? body = null;
        try { body = JsonNode.Parse(text) as JsonObject; } catch (JsonException) { }
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new SyncUnauthorizedException("El servidor rechazó la terminal. Vuelve a registrarla.");
        if (!response.IsSuccessStatusCode || body is null) throw new HttpRequestException((string?)body?["error"] ?? $"El servidor respondió {(int)response.StatusCode}.");
        return body;
    }

    public async Task<byte[]> KeyAsync(CancellationToken cancel) => Convert.FromBase64String((string)(await Send(HttpMethod.Get, "api/backup/key", null, cancel))["key"]!);
    public async Task<string?> LastDatabaseShaAsync(CancellationToken cancel) => (string?)(await Send(HttpMethod.Get, "api/backup/state", null, cancel))["databaseSha256"];
    public async Task<IReadOnlyList<string>> MissingAsync(IReadOnlyCollection<string> shas, CancellationToken cancel) =>
        ((await Send(HttpMethod.Post, "api/backup/missing", JsonContent.Create(new { shas }), cancel))["missing"] as JsonArray ?? []).Select(x => (string)x!).ToList();
    public async Task<Guid> BeginAsync(string kind, string sha256, long bytes, int parts, CancellationToken cancel) =>
        Guid.Parse((string)(await Send(HttpMethod.Post, "api/backup/object", JsonContent.Create(new { kind, sha256, bytes, parts }), cancel))["id"]!);
    public async Task PutPartAsync(Guid objectId, int index, ReadOnlyMemory<byte> data, CancellationToken cancel)
    {
        var content = new ReadOnlyMemoryContent(data); content.Headers.ContentType = new("application/octet-stream");
        await Send(HttpMethod.Put, $"api/backup/part?object={objectId}&index={index}", content, cancel);
    }
    public Task CompleteAsync(Guid objectId, CancellationToken cancel) => Send(HttpMethod.Post, $"api/backup/complete?object={objectId}", JsonContent.Create(new { }), cancel);
    public async Task<Guid> CreateSetAsync(Guid databaseObject, string terminalJson, int schemaVersion, IReadOnlyList<CloudMediaEntry> media, CancellationToken cancel) =>
        Guid.Parse((string)(await Send(HttpMethod.Post, "api/backup/set", JsonContent.Create(new
        {
            databaseObject, terminalJson, schemaVersion, media = media.Select(m => new { path = m.Path, role = m.Role, sha256 = m.Sha256, bytes = m.Bytes })
        }), cancel))["id"]!);
}

/// <summary>
/// Respaldo de esta caja en la nube (docs/BACKUPS.md): copia consistente de la base (sólo si cambió desde el último respaldo),
/// comprimida y cifrada, en partes de 3 MB; las fotos se suben una sola vez (por contenido). Nunca toca la base de trabajo.
/// </summary>
public sealed class CloudBackupService(string dataDirectory, Database database)
{
    public const int PartBytes = 3 * 1024 * 1024;

    public async Task<CloudBackupResult> RunAsync(ICloudBackupTransport transport, CancellationToken cancel = default)
    {
        // Copia temporal fuera de la carpeta de datos: ruta corta (SQLite falla con rutas de más de 260 caracteres)
        // y sin que OneDrive u otra sincronización suba archivos de paso. Se borra al terminar.
        var work = Path.Combine(Path.GetTempPath(), "cm-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var snapshot = new BackupService(database, work).Create();
            var plain = await File.ReadAllBytesAsync(snapshot, cancel);
            var sha = BackupCrypto.Sha256(plain);
            if (await transport.LastDatabaseShaAsync(cancel) == sha) return new(false, plain.Length, 0);

            var key = await transport.KeyAsync(cancel);
            var copy = new Database(snapshot);
            var media = SqliteProductPhotoStore.Active(copy).SelectMany(a => new[]
            {
                new CloudMediaEntry(ProductPhotoFileStore.OriginalPath(a), "original", a.OriginalSha256, a.OriginalBytes),
                new CloudMediaEntry(ProductPhotoFileStore.ThumbnailPath(a), "thumbnail", a.ThumbnailSha256, a.ThumbnailBytes),
            }).OrderBy(m => m.Path, StringComparer.Ordinal).ToList();
            var missing = (await transport.MissingAsync(media.Select(m => m.Sha256).Distinct().ToList(), cancel)).ToHashSet();
            var photos = new ProductPhotoFileStore(dataDirectory);
            var uploaded = 0;
            foreach (var entry in media.Where(m => missing.Contains(m.Sha256)).DistinctBy(m => m.Sha256))
            {
                var path = photos.Resolve(entry.Path);
                ProductPhotoFileStore.Verify(path, entry.Bytes, entry.Sha256);
                await UploadAsync(transport, "photo", entry.Sha256, entry.Bytes, BackupCrypto.Encrypt(key, await File.ReadAllBytesAsync(path, cancel)), cancel);
                uploaded++;
            }

            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(plain);
            var databaseObject = await UploadAsync(transport, "database", sha, plain.Length, BackupCrypto.Encrypt(key, compressed.ToArray()), cancel);
            int schema;
            using (var c = copy.Open(readOnly: true)) { using var q = c.CreateCommand(); q.CommandText = "SELECT max(version) FROM schema_migration"; schema = Convert.ToInt32(q.ExecuteScalar()); }
            await transport.CreateSetAsync(databaseObject, await File.ReadAllTextAsync(Path.Combine(dataDirectory, "terminal.json"), cancel), schema, media, cancel);
            return new(true, plain.Length, uploaded);
        }
        finally { if (Directory.Exists(work)) Directory.Delete(work, true); }
    }

    private static async Task<Guid> UploadAsync(ICloudBackupTransport transport, string kind, string sha, long bytes, byte[] sealedBytes, CancellationToken cancel)
    {
        var parts = Math.Max(1, (sealedBytes.Length + PartBytes - 1) / PartBytes);
        var id = await transport.BeginAsync(kind, sha, bytes, parts, cancel);
        for (var i = 0; i < parts; i++)
            await transport.PutPartAsync(id, i, sealedBytes.AsMemory(i * PartBytes, Math.Min(PartBytes, sealedBytes.Length - i * PartBytes)), cancel);
        await transport.CompleteAsync(id, cancel);
        return id;
    }
}

/// <summary>Restaura una caja desde un respaldo de la nube a una carpeta NUEVA, con las mismas validaciones que un .cmbackup local.</summary>
public static class CloudRestore
{
    /// <param name="detail">Respuesta de /api/web/backup (direcciones de cada parte, huellas y llave).</param>
    public static async Task<string> RebuildAsync(JsonObject detail, Func<string, CancellationToken, Task<byte[]>> download, string destination, CancellationToken cancel = default)
    {
        if ((string?)detail["kind"] != "terminal") throw new InvalidDataException("Ese respaldo no es de una caja.");
        var key = Convert.FromBase64String((string)detail["key"]!);
        destination = Path.GetFullPath(destination);
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("El destino debe ser nuevo.");
        var parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Destino inválido.");
        Directory.CreateDirectory(parent);
        var work = Path.Combine(parent, ".cr-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(work);
        try
        {
            async Task<byte[]> Object(JsonNode? parts)
            {
                using var buffer = new MemoryStream();
                foreach (var part in parts as JsonArray ?? throw new InvalidDataException("Respaldo incompleto."))
                    await buffer.WriteAsync(await download((string)part!["url"]!, cancel), cancel);
                return BackupCrypto.Decrypt(key, buffer.ToArray());
            }
            var database = detail["database"]!;
            using (var input = new GZipStream(new MemoryStream(await Object(database["parts"])), CompressionMode.Decompress))
            using (var output = File.Create(Path.Combine(work, "casamiga.db"))) await input.CopyToAsync(output, cancel);
            if (BackupCrypto.Sha256(await File.ReadAllBytesAsync(Path.Combine(work, "casamiga.db"), cancel)) != (string?)database["sha256"])
                throw new InvalidDataException("La base descargada no corresponde a su huella.");
            await File.WriteAllTextAsync(Path.Combine(work, "terminal.json"), (string?)detail["terminalJson"] ?? throw new InvalidDataException("Falta la identidad de la caja."), cancel);
            foreach (var entry in detail["media"] as JsonArray ?? [])
            {
                var relative = (string)entry!["path"]!;
                if (!ProductPhotoFileStore.SafeRelative(relative)) throw new InvalidDataException("Ruta de foto inválida.");
                var bytes = await Object(entry["parts"]);
                if (BackupCrypto.Sha256(bytes) != (string?)entry["sha256"]) throw new InvalidDataException("Una foto no corresponde a su huella.");
                var path = Path.Combine(work, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, bytes, cancel);
            }
            var package = InstallationBackup.Package(work, Path.Combine(parent, $".cr-{Guid.NewGuid():N}"[..12] + ".cmbackup"));
            try { InstallationBackup.Restore(package, destination); }
            finally { File.Delete(package); }
            return destination;
        }
        finally { if (Directory.Exists(work)) Directory.Delete(work, true); }
    }

    /// <summary>Entra con un usuario administrador de la web, elige el respaldo (el indicado o el más reciente de la caja) y lo restaura.</summary>
    public static async Task<string> FromWebAsync(Uri api, string username, string password, string destination, Guid? backupId, string? terminalName, CancellationToken cancel = default)
    {
        var cookies = new CookieContainer();
        using var http = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { Timeout = TimeSpan.FromMinutes(5) };
        using (var login = await http.PostAsJsonAsync(new Uri(api, "api/web/login"), new { username, password }, cancel))
            if (!login.IsSuccessStatusCode) throw new InvalidOperationException((string?)JsonNode.Parse(await login.Content.ReadAsStringAsync(cancel))?["error"] ?? "No se pudo entrar a la web.");
        if (backupId is null)
        {
            var list = JsonNode.Parse(await http.GetStringAsync(new Uri(api, "api/web/backups"), cancel))!["sets"]!.AsArray()
                .Where(s => (string?)s!["kind"] == "terminal" && (terminalName is null || string.Equals((string?)s["terminal"], terminalName, StringComparison.OrdinalIgnoreCase))).ToList();
            var terminals = list.Select(s => (string?)s!["terminal"]).Distinct().ToList();
            if (list.Count == 0) throw new InvalidOperationException("No hay respaldos de caja en la nube.");
            if (terminals.Count > 1) throw new InvalidOperationException($"Hay respaldos de varias cajas ({string.Join(", ", terminals)}); indica cuál.");
            backupId = Guid.Parse((string)list[0]!["id"]!);
        }
        var detail = JsonNode.Parse(await http.GetStringAsync(new Uri(api, $"api/web/backup?id={backupId}"), cancel)) as JsonObject ?? throw new InvalidDataException("Respuesta inválida.");
        return await RebuildAsync(detail, (url, c) => http.GetByteArrayAsync(url, c), destination, cancel);
    }
}
