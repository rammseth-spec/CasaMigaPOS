namespace CasaMiga.Infrastructure;

public sealed class BackupService(Database source, string directory)
{
    public string Create()
    {
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, $"casamiga-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.db");
        string temporary = target + ".partial";
        try
        {
            using (var input = source.Open(readOnly: true))
            using (var output = new Database(temporary).Open()) input.BackupDatabase(output);
            new Database(temporary).Validate();
            File.Move(temporary, target, overwrite: false);
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public string[] History() => Directory.Exists(directory)
        ? Directory.GetFiles(directory, "casamiga-*.db").OrderDescending().ToArray() : [];

    // Restore to a NEW path only; activation over production is deliberately separate.
    public static void RestoreToNewFile(string backup, string destination)
    {
        new Database(backup).Validate();
        using (var reservation = new FileStream(destination, FileMode.CreateNew, FileAccess.Write)) { }
        try
        {
            using (var input = new Database(backup).Open(readOnly: true))
            using (var output = new Database(destination).Open()) input.BackupDatabase(output);
            new Database(destination).Validate();
        }
        catch { File.Delete(destination); throw; }
    }
}
