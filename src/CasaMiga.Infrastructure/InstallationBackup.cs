using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CasaMiga.Application;
namespace CasaMiga.Infrastructure;

// Call Create while holding DirectoryLease. Restore only publishes into a new directory.
public sealed class InstallationBackup(string dataDirectory)
{
    private sealed record Manifest(int Version, Guid TerminalId, string DatabaseSha256, string TerminalSha256);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static TerminalIdentity ValidatePair(string directory)
    {
        var terminal=JsonSerializer.Deserialize<TerminalIdentity>(File.ReadAllText(Path.Combine(directory,"terminal.json")))
            ?? throw new InvalidDataException("Falta identidad de terminal.");
        if(terminal.Id==Guid.Empty || string.IsNullOrWhiteSpace(terminal.Name)) throw new InvalidDataException("Terminal inválida.");
        var database=new Database(Path.Combine(directory,"casamiga.db")); database.Validate();
        using var c=database.Open(readOnly:true); using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT name FROM terminal WHERE id=$id"; cmd.Parameters.AddWithValue("$id",terminal.Id.ToString("D"));
        if(!Equals(cmd.ExecuteScalar(),terminal.Name)) throw new InvalidDataException("Terminal y base no corresponden.");
        return terminal;
    }
    public string Create()
    {
        var backups=Path.Combine(dataDirectory,"backups"); Directory.CreateDirectory(backups);
        var temporary=Path.Combine(backups,"package-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
        var target=Path.Combine(backups,$"casamiga-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.cmbackup");
        try
        {
            // SQLite Backup API produces a consistent snapshot, including committed WAL data.
            BackupService.RestoreToNewFile(Path.Combine(dataDirectory,"casamiga.db"),Path.Combine(temporary,"casamiga.db"));
            File.Copy(Path.Combine(dataDirectory,"terminal.json"),Path.Combine(temporary,"terminal.json"));
            var terminal=ValidatePair(temporary);
            var manifest=new Manifest(1,terminal.Id,Hash(Path.Combine(temporary,"casamiga.db")),Hash(Path.Combine(temporary,"terminal.json")));
            File.WriteAllText(Path.Combine(temporary,"manifest.json"),JsonSerializer.Serialize(manifest));
            using(var archive=ZipFile.Open(target+".partial",ZipArchiveMode.Create))
                foreach(var name in new[]{"casamiga.db","terminal.json","manifest.json"}) archive.CreateEntryFromFile(Path.Combine(temporary,name),name);
            File.Move(target+".partial",target,false); return target;
        }
        finally { Directory.Delete(temporary,true); if(File.Exists(target+".partial")) File.Delete(target+".partial"); }
    }
    public string[] History() => Directory.Exists(Path.Combine(dataDirectory,"backups"))
        ? Directory.GetFiles(Path.Combine(dataDirectory,"backups"),"*.cmbackup").OrderDescending().ToArray():[];
    public static void Restore(string backup, string destination)
    {
        if(!Path.IsPathFullyQualified(destination) || destination.StartsWith(@"\\",StringComparison.Ordinal)) throw new ArgumentException("Destino local absoluto requerido.");
        destination=Path.GetFullPath(destination);
        if(Directory.Exists(destination) || File.Exists(destination)) throw new IOException("El destino debe ser nuevo.");
        var parent=Path.GetDirectoryName(destination) ?? throw new ArgumentException("Destino inválido.");
        Directory.CreateDirectory(parent);
        var temporary=Path.Combine(parent,".restore-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
        try
        {
            using(var archive=ZipFile.OpenRead(backup))
            {
                string[] names=["casamiga.db","terminal.json","manifest.json"];
                if(archive.Entries.Count!=3 || names.Any(n=>archive.Entries.Count(e=>e.FullName==n)!=1))
                    throw new InvalidDataException("Contenido de backup inválido.");
                // Fixed names only: no path traversal or arbitrary extraction.
                foreach(var name in names) archive.GetEntry(name)!.ExtractToFile(Path.Combine(temporary,name));
            }
            var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(temporary,"manifest.json")));
            if(manifest is not {Version:1} || manifest.DatabaseSha256!=Hash(Path.Combine(temporary,"casamiga.db"))
                || manifest.TerminalSha256!=Hash(Path.Combine(temporary,"terminal.json"))) throw new InvalidDataException("Backup incompleto o alterado.");
            var terminal=ValidatePair(temporary);
            if(terminal.Id!=manifest.TerminalId) throw new InvalidDataException("Identidad incompatible.");
            // Validate supported migration history on the candidate, never on the original backup.
            new Database(Path.Combine(temporary,"casamiga.db")).Initialize();
            ValidatePair(temporary);
            File.Delete(Path.Combine(temporary,"manifest.json"));
            Directory.Move(temporary,destination);
        }
        finally { if(Directory.Exists(temporary)) Directory.Delete(temporary,true); }
    }
}

