using System.Text.Json;

namespace CasaMiga.Infrastructure;

public sealed record FoundationOptions(string DataDirectory, bool IsDevelopment = false, bool DiagnosticCrashOnStartup = false, int PreviewWidth = 0, int PreviewHeight = 0)
{
    public static FoundationOptions Load()
    {
        var configured = Environment.GetEnvironmentVariable("CASAMIGA_DATA_DIR");
        bool development = false;
        FoundationOptions? preview = null;
        var developmentFile = Path.Combine(AppContext.BaseDirectory, "development.settings.json");
        if (configured is null && File.Exists(developmentFile))
        {
            var settings = JsonSerializer.Deserialize<FoundationOptions>(File.ReadAllText(developmentFile));
            if (settings is not { IsDevelopment: true }) throw new InvalidDataException("Configuración de desarrollo inválida.");
            configured = settings.DataDirectory; development = true; preview = settings;
        }
        var directory = configured ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CasaMigaPOS");
        if (!Path.IsPathFullyQualified(directory) || directory.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("Se requiere una ruta local absoluta.");
        return new(directory, development, preview?.DiagnosticCrashOnStartup ?? false, preview?.PreviewWidth ?? 0, preview?.PreviewHeight ?? 0);
    }
}

public sealed class FoundationRuntime(FoundationOptions options)
{
    public string Start()
    {
        Directory.CreateDirectory(options.DataDirectory);
        var database = new Database(Path.Combine(options.DataDirectory, "casamiga.db"));
        // Caller holds DirectoryLease; preserve the previous schema before any upgrade.
        if (File.Exists(database.FilePath))
        {
            if(File.Exists(Path.Combine(options.DataDirectory,"terminal.json"))) new InstallationBackup(options.DataDirectory).Create();
            else new BackupService(database,Path.Combine(options.DataDirectory,"backups")).Create(); // Legacy safety snapshot, not a complete installation backup.
        }
        try { database.Initialize(); }
        catch (Exception error) { TryLog("migration.failed", error.GetType().Name); throw; }
        database.Validate();
        var terminal=TerminalConfiguration.LoadOrCreate(options.DataDirectory);
        new SqliteLocalUnitOfWork(database).Run(store => {store.EnsureTerminal(terminal,DateTimeOffset.UtcNow); return true;});
        var backup = new InstallationBackup(options.DataDirectory).Create();
        Log("foundation.ready");
        return backup;
    }

    public void TryLog(string eventName, string? errorType = null)
    {
        try { Log(eventName, errorType); }
        catch (IOException) { /* Technical logging cannot undo a committed business operation. */ }
        catch (UnauthorizedAccessException) { }
    }

    public void Log(string eventName, string? errorType = null)
    {
        Directory.CreateDirectory(options.DataDirectory);
        // Allowlisted fields: never serialize arbitrary exception messages or credentials.
        var json = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, eventName, errorType });
        File.AppendAllText(Path.Combine(options.DataDirectory, $"events-{DateTime.UtcNow:yyyyMMdd}.jsonl"), json + Environment.NewLine);
    }
}


