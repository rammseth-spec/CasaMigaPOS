using CasaMiga.Infrastructure;

// Development/maintenance entry point; never activate restored files over live data.
try
{
    if (args.Length < 2)
        throw new ArgumentException("Uso: init|backup|list <directorio absoluto>; restore-installation <cmbackup> <directorio nuevo>; restore <db legado> <archivo nuevo>.");
    if (args[0] == "restore-installation" && args.Length == 3)
    {
        InstallationBackup.Restore(args[1],args[2]);
        Console.WriteLine("Instalación restaurada y validada en directorio nuevo, incluida la identidad de terminal.");
    }
    else if (args[0] == "restore" && args.Length == 3)
    {
        BackupService.RestoreToNewFile(args[1], args[2]);
        Console.WriteLine("Restauración validada en archivo nuevo. No se activó sobre la base de trabajo.");
    }
    else if (args.Length == 2 && Path.IsPathFullyQualified(args[1]) && !args[1].StartsWith(@"\\", StringComparison.Ordinal))
    {
        using var lease = new DirectoryLease(args[1]);
        var database = new Database(Path.Combine(args[1], "casamiga.db"));
        var backups = new InstallationBackup(args[1]);
        switch (args[0])
        {
            case "init": Console.WriteLine(new FoundationRuntime(new FoundationOptions(args[1])).Start()); break;
            case "backup": Console.WriteLine(backups.Create()); break;
            case "list": foreach (var file in backups.History()) Console.WriteLine(file); break;
            default: throw new ArgumentException("Comando no reconocido.");
        }
    }
    else throw new ArgumentException("Argumentos inválidos.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"No se completó la operación ({error.GetType().Name}). Revisa comando, archivos, permisos y espacio. No sobrescribas la base existente.");
    return 1;
}
