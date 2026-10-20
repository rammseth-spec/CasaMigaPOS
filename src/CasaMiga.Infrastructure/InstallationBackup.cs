using System.IO.Compression;
using System.Text.Json;
using CasaMiga.Application;
namespace CasaMiga.Infrastructure;

// Caller holds DirectoryLease. Media gate precedes snapshot/DB access everywhere.
public sealed class InstallationBackup(string dataDirectory)
{
    private sealed record Entry(string Path, long Bytes, string Sha256, string Role);
    private sealed record Manifest(int Version, Guid TerminalId, string? DatabaseSha256 = null,
        string? TerminalSha256 = null, int SchemaVersion = 0, Entry[]? Files = null);
    private const long MaxTotal = 16L * 1024 * 1024 * 1024;
    private const long MaxMedia = 5L * 1024 * 1024 * 1024;
    private const long MaxSettings = 1024 * 1024;
    /// <summary>
    /// Configuración que también se respalda si existe. sync.json no: su llave sólo sirve en este equipo y usuario de Windows,
    /// así que después de restaurar la caja se vuelve a registrar en la nube.
    /// </summary>
    private static readonly string[] SettingsFiles = ["printing.json", "operations-hub.json"];
    private static string Hash(string path) => ProductPhotoFileStore.Hash(path);
    private static TerminalIdentity ValidatePair(string directory)
    {
        var terminal = JsonSerializer.Deserialize<TerminalIdentity>(File.ReadAllText(Path.Combine(directory, "terminal.json")))
            ?? throw new InvalidDataException("Falta identidad de terminal.");
        if (terminal.Id == Guid.Empty || string.IsNullOrWhiteSpace(terminal.Name)) throw new InvalidDataException("Terminal inválida.");
        var database = new Database(Path.Combine(directory, "casamiga.db")); database.Validate();
        using var c = database.Open(true); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT name FROM terminal WHERE id=$id"; cmd.Parameters.AddWithValue("$id", terminal.Id.ToString("D"));
        if (!Equals(cmd.ExecuteScalar(), terminal.Name)) throw new InvalidDataException("Terminal y base no corresponden.");
        return terminal;
    }
    private static int Schema(string directory)
    {
        using var c = new Database(Path.Combine(directory, "casamiga.db")).Open(true); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT max(version) FROM schema_migration"; return Convert.ToInt32(cmd.ExecuteScalar());
    }
    private static Entry Describe(string root, string path, string role) => new(path, new FileInfo(Path.Combine(root, path)).Length, Hash(Path.Combine(root, path)), role);
    private static Entry[] Media(Database db) => SqliteProductPhotoStore.Active(db).SelectMany(a => new[]
    {
        new Entry(ProductPhotoFileStore.OriginalPath(a), a.OriginalBytes, a.OriginalSha256, "original"),
        new Entry(ProductPhotoFileStore.ThumbnailPath(a), a.ThumbnailBytes, a.ThumbnailSha256, "thumbnail")
    }).OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
    public string Create()
    {
        var store = new ProductPhotoFileStore(dataDirectory);
        using var held = store.Acquire();
        var backups = store.Resolve("backups"); Directory.CreateDirectory(backups);
        var temporary = Path.Combine(backups, "package-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
        var target = Path.Combine(backups, $"casamiga-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.cmbackup");
        var validation = temporary + "-check";
        try
        {
            BackupService.RestoreToNewFile(Path.Combine(dataDirectory, "casamiga.db"), Path.Combine(temporary, "casamiga.db"));
            File.Copy(store.Resolve("terminal.json"), Path.Combine(temporary, "terminal.json"));
            var terminal = ValidatePair(temporary); var schema = Schema(temporary);
            Manifest manifest;
            var paths = new List<string> { "casamiga.db", "terminal.json" };
            if (schema < 16) manifest = new(1, terminal.Id, Hash(Path.Combine(temporary, paths[0])), Hash(Path.Combine(temporary, paths[1])));
            else
            {
                var media = Media(new Database(Path.Combine(temporary, "casamiga.db")));
                if (media.Length > 20000 || media.Sum(x => x.Bytes) > MaxMedia) throw new InvalidDataException("Límite de fotografías del backup excedido.");
                foreach (var entry in media)
                {
                    var source = store.Resolve(entry.Path); ProductPhotoFileStore.Verify(source, entry.Bytes, entry.Sha256);
                    var destination = Path.Combine(temporary, entry.Path); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination);
                    paths.Add(entry.Path);
                }
                // Configuración de la caja que no vive en la base: la impresora de tickets y las notas y temporizadores del Centro operativo.
                var settings = new List<Entry>();
                foreach (var name in SettingsFiles)
                {
                    var source = Path.Combine(dataDirectory, name);
                    if (!File.Exists(source)) continue;
                    if (new FileInfo(source).Length is < 1 or > MaxSettings) throw new InvalidDataException($"El archivo de configuración {name} no tiene un tamaño válido.");
                    File.Copy(source, Path.Combine(temporary, name)); paths.Add(name); settings.Add(Describe(temporary, name, "settings"));
                }
                manifest = new(2, terminal.Id, SchemaVersion: schema, Files: [Describe(temporary, "casamiga.db", "database"), Describe(temporary, "terminal.json", "terminal"), .. media, .. settings]);
            }
            File.WriteAllText(Path.Combine(temporary, "manifest.json"), JsonSerializer.Serialize(manifest)); paths.Add("manifest.json");
            using (var archive = ZipFile.Open(target + ".partial", ZipArchiveMode.Create))
                foreach (var path in paths) archive.CreateEntryFromFile(Path.Combine(temporary, path), path);
            Directory.CreateDirectory(validation); ExtractValidated(target + ".partial", validation);
            File.Move(target + ".partial", target, false);
            Prune(backups, DateTime.UtcNow);
            return target;
        }
        finally
        {
            Directory.Delete(temporary, true);
            if (Directory.Exists(validation)) Directory.Delete(validation, true);
            if (File.Exists(target + ".partial")) File.Delete(target + ".partial");
        }
    }
    /// <summary>
    /// Empaqueta como .cmbackup v2 una instalación ya armada en <paramref name="directory"/> (base, terminal.json y fotos con sus rutas).
    /// Sirve para restaurar desde la nube con las mismas validaciones de <see cref="Restore"/>.
    /// </summary>
    public static string Package(string directory, string target)
    {
        var terminal = ValidatePair(directory); var schema = Schema(directory);
        var media = Media(new Database(Path.Combine(directory, "casamiga.db")));
        foreach (var entry in media) ProductPhotoFileStore.Verify(Path.Combine(directory, entry.Path), entry.Bytes, entry.Sha256);
        var manifest = new Manifest(2, terminal.Id, SchemaVersion: schema, Files: [Describe(directory, "casamiga.db", "database"), Describe(directory, "terminal.json", "terminal"), .. media]);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(manifest));
        using (var archive = ZipFile.Open(target, ZipArchiveMode.Create))
            foreach (var path in new[] { "casamiga.db", "terminal.json" }.Concat(media.Select(m => m.Path)).Append("manifest.json"))
                archive.CreateEntryFromFile(Path.Combine(directory, path), path);
        return target;
    }

    /// <summary>Respaldos locales que se conservan siempre (los más recientes), aunque tengan más de <see cref="KeepDays"/> días.</summary>
    public const int KeepAtLeast = 20;
    /// <summary>Días que se conserva un respaldo local; la misma retención que la nube (docs/BACKUPS.md).</summary>
    public const int KeepDays = 30;
    /// <summary>
    /// Limpieza de la carpeta backups: sólo borra respaldos con más de 30 días y nunca los 20 más recientes, así que el único
    /// respaldo existente nunca se borra. Sólo toca archivos con el nombre que pone <see cref="Create"/>; si uno no se puede borrar, se deja.
    /// </summary>
    public static IReadOnlyList<string> Prune(string backups, DateTime nowUtc)
    {
        var dated = Directory.GetFiles(backups, "casamiga-*.cmbackup").Select(path => (Path: path, At: CreatedAt(path)))
            .Where(x => x.At is not null).OrderByDescending(x => x.At).ToArray();
        var removed = new List<string>();
        foreach (var (path, at) in dated.Skip(KeepAtLeast).Where(x => nowUtc - x.At!.Value > TimeSpan.FromDays(KeepDays)))
        {
            try { File.Delete(path); removed.Add(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return removed;
    }
    private static DateTime? CreatedAt(string path)
    {
        var name = Path.GetFileName(path);
        return name.Length > 28 && DateTime.TryParseExact(name.AsSpan(9, 19), "yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var at) ? at : null;
    }

    public string[] History() => Directory.Exists(Path.Combine(dataDirectory, "backups"))
        ? Directory.GetFiles(Path.Combine(dataDirectory, "backups"), "*.cmbackup").OrderDescending().ToArray() : [];
    private static void Extract(ZipArchiveEntry entry, string destination, long maximum)
    {
        if (entry.Length < 0 || entry.Length > maximum) throw new InvalidDataException("Entrada demasiado grande.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input = entry.Open(); using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
        var buffer = new byte[81920]; long count = 0; int read;
        while ((read = input.Read(buffer)) > 0)
        { count = checked(count + read); if (count > maximum || count > entry.Length) throw new InvalidDataException("Longitud ZIP inválida."); output.Write(buffer, 0, read); }
        if (count != entry.Length) throw new InvalidDataException("ZIP truncado.");
        output.Flush(true);
    }
    private static void ExtractValidated(string backup, string directory)
    {
        using var archive = ZipFile.OpenRead(backup);
        if (archive.Entries.Count is < 3 or > 20005 || archive.Entries.Any(e => !ProductPhotoFileStore.SafeRelative(e.FullName))
            || archive.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Count)
            throw new InvalidDataException("Rutas o contenido de backup inválidos.");
        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("Falta manifiesto.");
        Extract(manifestEntry, Path.Combine(directory, "manifest.json"), 4 * 1024 * 1024);
        Manifest manifest;
        try { manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(directory, "manifest.json"))) ?? throw new InvalidDataException("Manifiesto inválido."); }
        catch (JsonException) { throw new InvalidDataException("Manifiesto inválido."); }
        Entry[] entries;
        if (manifest.Version == 1)
        {
            if (archive.Entries.Count != 3) throw new InvalidDataException("Backup v1 inválido.");
            entries = [new("casamiga.db", archive.GetEntry("casamiga.db")?.Length ?? -1, manifest.DatabaseSha256 ?? "", "database"),
                new("terminal.json", archive.GetEntry("terminal.json")?.Length ?? -1, manifest.TerminalSha256 ?? "", "terminal")];
        }
        else if (manifest.Version == 2 && manifest.Files is { Length: >= 2 and <= 20004 }) entries = manifest.Files;
        else throw new InvalidDataException("Versión de backup no admitida.");
        if (entries.Length != archive.Entries.Count - 1
            || entries.Any(e => e is null || e.Path is null || e.Sha256 is null || !AllowedEntry(e) || e.Bytes < 1 || e.Bytes > MaxTotal
                || e.Sha256.Length != 64 || e.Sha256.Any(c => !char.IsAsciiHexDigitUpper(c)))
            || entries.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length
            || entries.Sum(e => e.Bytes) > MaxTotal) throw new InvalidDataException("Lista de archivos inválida.");
        foreach (var entry in entries)
        {
            var item = archive.GetEntry(entry.Path) ?? throw new InvalidDataException("Falta archivo listado.");
            long limit = entry.Role switch { "original" => ProductPhotoService.MaxOriginalBytes, "thumbnail" => 2 * 1024 * 1024, "terminal" => 65536, "database" => MaxTotal, "settings" => MaxSettings, _ => 0 };
            if (item.Length != entry.Bytes || item.Length > limit) throw new InvalidDataException("Tamaño o rol inválido.");
            var path = Path.Combine(directory, entry.Path); Extract(item, path, limit); ProductPhotoFileStore.Verify(path, entry.Bytes, entry.Sha256);
        }
        if (entries.Count(e => e.Path == "casamiga.db" && e.Role == "database") != 1 || entries.Count(e => e.Path == "terminal.json" && e.Role == "terminal") != 1)
            throw new InvalidDataException("Falta base o terminal.");
        if (ValidatePair(directory).Id != manifest.TerminalId) throw new InvalidDataException("Identidad incompatible.");
        var media = Media(new Database(Path.Combine(directory, "casamiga.db")));
        var listed = entries.Where(e => e.Role is "original" or "thumbnail").OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
        if (!media.SequenceEqual(listed) || listed.Sum(e => e.Bytes) > MaxMedia || (manifest.Version == 2 && Schema(directory) != manifest.SchemaVersion))
            throw new InvalidDataException("Referencias de media no corresponden al snapshot.");
        if (manifest.Version == 1 && Schema(directory) >= 16) throw new InvalidDataException("Schema 16 requiere backup v2.");
    }
    private static bool AllowedEntry(Entry e)
    {
        if (e.Role == "database") return e.Path == "casamiga.db";
        if (e.Role == "terminal") return e.Path == "terminal.json";
        if (e.Role == "settings") return SettingsFiles.Contains(e.Path, StringComparer.Ordinal);
        if (!ProductPhotoFileStore.SafeRelative(e.Path)) return false;
        var parts = e.Path.Split('/');
        if (parts.Length != 4 || parts[0] != "product-images" || parts[1] != "v1" || !Guid.TryParseExact(parts[2], "N", out var id)
            || id == Guid.Empty || parts[2] != id.ToString("N")) return false;
        return e.Role == "original" ? parts[3] is "original.jpg" or "original.png" : e.Role == "thumbnail"
            && parts[3].Length == 75 && parts[3].StartsWith("pos-v1-", StringComparison.Ordinal) && parts[3].EndsWith(".jpg", StringComparison.Ordinal)
            && parts[3].AsSpan(7, 64).ToString().All(char.IsAsciiHexDigitUpper);
    }
    public static void Restore(string backup, string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || destination.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("Destino local absoluto requerido.");
        destination = Path.GetFullPath(destination); ProductPhotoFileStore.CheckAncestors(destination);
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("El destino debe ser nuevo.");
        var parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Destino inválido."); Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, ".restore-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
        try
        {
            ExtractValidated(backup, temporary);
            new Database(Path.Combine(temporary, "casamiga.db")).Initialize(); ValidatePair(temporary);
            File.Delete(Path.Combine(temporary, "manifest.json")); Directory.Move(temporary, destination);
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }
}
