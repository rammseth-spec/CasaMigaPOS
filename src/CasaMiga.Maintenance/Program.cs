using System.Text.Json;
using System.Text.Json.Serialization;
using CasaMiga.Infrastructure;

// Development/maintenance entry point; never activate restored files over live data.
try
{
    if (args.Length < 2)
        throw new ArgumentException("Uso: init|backup|list|print-test|reset-admin <directorio absoluto>; restore-installation <cmbackup> <directorio nuevo>; restore <db legado> <archivo nuevo>; export-sync <directorio de copia> <archivo json>.");
    if (args[0] == "print-test" && args.Length == 2 && Path.IsPathFullyQualified(args[1]) && !args[1].StartsWith(@"\\", StringComparison.Ordinal))
    {
        var test = new ControlledReceiptPrintTest(
            new PrintingConfigurationProvider(args[1]),
            new EscPosReceiptRenderer(),
            new SerializedReceiptPrintTransport(new WindowsRawPrintTransport()),
            new SystemClock(),
            Path.Combine(args[1], "physical-print-test.jsonl"));
        var result = test.Run();
        var output = new JsonSerializerOptions { WriteIndented = true };
        output.Converters.Add(new JsonStringEnumConverter());
        Console.WriteLine(JsonSerializer.Serialize(result, output));
        return result.State == CasaMiga.Application.ReceiptPrintState.Submitted ? 0 : 2;
    }
    if (args[0] == "export-sync" && args.Length == 3 && Path.IsPathFullyQualified(args[1]))
    {
        // Sólo desarrollo: vuelca la cola pendiente de una copia (ya migrada) para revisar la versión web sin tocar la nube. No marca nada como enviado.
        using var lease = new DirectoryLease(args[1]);
        Console.WriteLine(new FoundationRuntime(new FoundationOptions(args[1], IsDevelopment: true)).Start());
        var outbox = new SqliteSyncOutbox(new Database(Path.Combine(args[1], "casamiga.db")));
        var terminal = TerminalConfiguration.LoadOrCreate(args[1]);
        var entries = new List<object>(); long after = 0;
        while (outbox.PendingEnvelopes(500, int.MaxValue, after) is { Count: > 0 } batch)
        {
            entries.AddRange(batch.Select(e => (object)new { seq = e.Entry.Sequence, entity = e.Entry.Entity, entityId = e.Entry.EntityId, change = e.Entry.Change,
                enqueuedAt = e.Entry.EnqueuedAt.ToUniversalTime().ToString("O"), payload = e.Payload }));
            after = batch[^1].Entry.Sequence;
        }
        var branch = outbox.Branch();
        File.WriteAllText(args[2], JsonSerializer.Serialize(new { branch = new { id = branch.Id, name = branch.Name }, terminal = new { id = terminal.Id, name = terminal.Name }, entries }));
        Console.WriteLine($"{entries.Count} registros exportados a {args[2]}.");
        return 0;
    }
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
            case "reset-admin":
            {
                // Respaldo + migración igual que al abrir la app; después contraseña temporal auditada.
                Console.WriteLine(new FoundationRuntime(new FoundationOptions(args[1])).Start());
                var identity = new CasaMiga.Application.IdentityService(new SqliteLocalUnitOfWork(database), new LocalPasswords(), new SystemClock(), new CasaMiga.Application.UserSession());
                var (username, temporary) = identity.RecoverAdministrator();
                Console.WriteLine($"Administrador: {username}");
                Console.WriteLine($"Contraseña temporal: {temporary}");
                Console.WriteLine("Se muestra sólo esta vez. Al iniciar sesión deberá crearse una contraseña nueva.");
                break;
            }
            default: throw new ArgumentException("Comando no reconocido.");
        }
    }
    else throw new ArgumentException("Argumentos inválidos.");
    return 0;
}
catch (InvalidOperationException error) when (args.Length > 0 && args[0] == "reset-admin")
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
catch (Exception error)
{
    Console.Error.WriteLine($"No se completó la operación ({error.GetType().Name}). Revisa comando, archivos, permisos y espacio. No sobrescribas la base existente.");
    return 1;
}
