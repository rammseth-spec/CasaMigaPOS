using System.Collections.Concurrent;
using System.Security.Cryptography;
using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

// Installation DirectoryLease is required across processes; this gate coordinates threads within it.
public sealed class ProductPhotoFileStore : IProductPhotoFiles
{
    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly string root;
    public ProductPhotoFileStore(string dataDirectory)
    {
        if (!Path.IsPathFullyQualified(dataDirectory) || dataDirectory.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("Raíz local requerida.");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        CheckAncestors(root);
    }
    public IDisposable Acquire()
    {
        var gate = Gates.GetOrAdd(root, _ => new object()); Monitor.Enter(gate); return new Held(gate);
    }
    private sealed class Held(object gate) : IDisposable { public void Dispose() => Monitor.Exit(gate); }
    internal static void CheckAncestors(string path)
    {
        for (var item = path; item is not null; item = Path.GetDirectoryName(item))
            if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Enlaces no permitidos en almacenamiento de fotos.");
    }
    public static string OriginalPath(PhotoAsset a) => $"product-images/v1/{a.Id:N}/original.{(a.Format == "jpeg" ? "jpg" : "png")}";
    public static string ThumbnailPath(PhotoAsset a) => $"product-images/v1/{a.Id:N}/pos-v1-{a.ThumbnailSha256}.jpg";
    internal string Resolve(string relative)
    {
        if (!SafeRelative(relative)) throw new InvalidDataException("Ruta de media inválida.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta fuera de instalación.");
        CheckAncestors(path); return path;
    }
    internal static bool SafeRelative(string path) => path.Length is > 0 and < 240 && !path.Contains('\\') && !path.Contains(':')
        && !path.StartsWith('/') && path.Split('/').All(p => p.Length > 0 && p is not ("." or "..") && !p.EndsWith('.') && !p.EndsWith(' ') && p.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-'));
    internal static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    internal static void Verify(string path, long bytes, string hash)
    {
        CheckAncestors(path);
        if (!File.Exists(path) || new FileInfo(path).Length != bytes || Hash(path) != hash) throw new InvalidDataException("Fotografía ausente o alterada.");
    }
    private static void Write(string path, byte[] bytes)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes); file.Flush(true);
    }
    public void Publish(PhotoAsset a, byte[] original, byte[] thumbnail)
    {
        using var held = Acquire();
        var staging = Resolve($"product-images/.staging/{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            var originalFile = Path.Combine(staging, Path.GetFileName(OriginalPath(a)));
            var thumbFile = Path.Combine(staging, Path.GetFileName(ThumbnailPath(a)));
            Write(originalFile, original); Write(thumbFile, thumbnail);
            Verify(originalFile, a.OriginalBytes, a.OriginalSha256); Verify(thumbFile, a.ThumbnailBytes, a.ThumbnailSha256);
            var final = Path.GetDirectoryName(Resolve(OriginalPath(a)))!;
            Directory.CreateDirectory(Path.GetDirectoryName(final)!);
            if (Directory.Exists(final))
            {
                // Retry after publication but before DB commit: accept only the identical bundle.
                Verify(Resolve(OriginalPath(a)), a.OriginalBytes, a.OriginalSha256);
                Verify(Resolve(ThumbnailPath(a)), a.ThumbnailBytes, a.ThumbnailSha256);
            }
            else Directory.Move(staging, final);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    public byte[] ReadOriginal(PhotoAsset a) { using var held = Acquire(); var p = Resolve(OriginalPath(a)); Verify(p, a.OriginalBytes, a.OriginalSha256); return File.ReadAllBytes(p); }
    public byte[] ReadThumbnail(PhotoAsset a) { using var held = Acquire(); var p = Resolve(ThumbnailPath(a)); Verify(p, a.ThumbnailBytes, a.ThumbnailSha256); return File.ReadAllBytes(p); }
    public void PublishThumbnail(PhotoAsset a, byte[] bytes)
    {
        using var held = Acquire();
        var path = Resolve(ThumbnailPath(a));
        if (File.Exists(path) && new FileInfo(path).Length == a.ThumbnailBytes && Hash(path) == a.ThumbnailSha256) return;
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try { Write(temp, bytes); Verify(temp, a.ThumbnailBytes, a.ThumbnailSha256); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Retire(PhotoAsset a)
    {
        using var held = Acquire();
        if (SqliteProductPhotoStore.Active(new Database(Path.Combine(root, "casamiga.db"))).Any(x => x.Id == a.Id)) return;
        DeleteTree(Path.GetDirectoryName(Resolve(OriginalPath(a)))!);
    }
    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path)) return;
        CheckAncestors(path);
        // Never follow a junction introduced inside a managed bundle.
        foreach (var item in Directory.EnumerateFileSystemEntries(path))
        {
            CheckAncestors(item);
            if (Directory.Exists(item)) DeleteTree(item); else File.Delete(item);
        }
        Directory.Delete(path);
    }
    public IReadOnlyList<PhotoOrphan> Orphans(IReadOnlyList<PhotoAsset> active, DateTimeOffset olderThan, bool remove)
    {
        using var held = Acquire();
        var result = new List<PhotoOrphan>();
        var ids = active.Select(x => x.Id.ToString("N")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var location in new[] { "product-images/v1", "product-images/.staging" })
        {
            var directory = Resolve(location);
            if (!Directory.Exists(directory)) continue;
            foreach (var candidate in Directory.EnumerateDirectories(directory))
            {
                CheckAncestors(candidate);
                var name = Path.GetFileName(candidate);
                if (!Guid.TryParseExact(name, "N", out _) || (location.EndsWith("v1", StringComparison.Ordinal) && ids.Contains(name))) continue;
                var last = new DateTimeOffset(Directory.GetLastWriteTimeUtc(candidate));
                if (last > olderThan) continue;
                result.Add(new(location + "/" + name, last));
                if (remove) DeleteTree(candidate);
            }
        }
        foreach (var asset in active)
        {
            var directory = Path.GetDirectoryName(Resolve(OriginalPath(asset)))!;
            if (!Directory.Exists(directory)) continue;
            foreach (var candidate in Directory.EnumerateFiles(directory, "pos-v1-*.jpg"))
            {
                CheckAncestors(candidate);
                if (Path.GetFileName(candidate) == Path.GetFileName(ThumbnailPath(asset))) continue;
                var last = new DateTimeOffset(File.GetLastWriteTimeUtc(candidate));
                if (last > olderThan) continue;
                result.Add(new($"product-images/v1/{asset.Id:N}/" + Path.GetFileName(candidate), last));
                if (remove) File.Delete(candidate);
            }
        }
        return result;
    }
}
