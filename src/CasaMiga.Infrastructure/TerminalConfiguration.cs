using System.Text.Json;
using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

public static class TerminalConfiguration
{
    // Called while DirectoryLease is held. Invalid configuration is never silently replaced.
    public static TerminalIdentity LoadOrCreate(string directory)
    {
        string path = Path.Combine(directory, "terminal.json");
        if (File.Exists(path))
        {
            var terminal = JsonSerializer.Deserialize<TerminalIdentity>(File.ReadAllText(path));
            if (terminal is null || terminal.Id == Guid.Empty || string.IsNullOrWhiteSpace(terminal.Name) || terminal.Name.Length > 100)
                throw new InvalidDataException("Configuración de terminal inválida.");
            return terminal;
        }
        var databasePath=Path.Combine(directory,"casamiga.db");
        if(File.Exists(databasePath))
        {
            using var c=new Database(databasePath).Open(readOnly:true); using var cmd=c.CreateCommand();
            cmd.CommandText="SELECT count(*) FROM terminal";
            if(Convert.ToInt64(cmd.ExecuteScalar())>0) throw new InvalidDataException("Falta terminal.json. Restaura un backup completo; no se generará otra terminal.");
        }
        var created = new TerminalIdentity(Guid.NewGuid(), "CASA-MIGA-CAJA-01");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(created));
        File.Move(temporary, path, overwrite: false);
        return created;
    }
}

public sealed class DirectoryLease : IDisposable
{
    private readonly FileStream stream;
    public DirectoryLease(string directory)
    {
        Directory.CreateDirectory(directory);
        stream = new FileStream(Path.Combine(directory, "casamiga.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public void Dispose() => stream.Dispose();
}
