using System.ComponentModel;
using System.Globalization;
using CasaMiga.Infrastructure;

namespace CasaMiga.Presentation;

/// <summary>Envío automático en segundo plano y registro de la terminal. Nunca bloquea ventas: sólo lee la cola y marca lo confirmado.</summary>
public sealed class SyncViewModel : INotifyPropertyChanged, IDisposable
{
    public const string DefaultApiUrl = "https://casamiga-pos-api.vercel.app/";
    private readonly SyncEngine engine;
    private readonly SqliteSyncOutbox outbox;
    private readonly Guid terminalId;
    private readonly string terminalName;
    private readonly Action<string, string?> log;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stop = new();
    /// <summary>
    /// Cada cuánto sube lo pendiente y baja los cambios de la web: 5 s para que la base de la nube esté siempre al día (decisión del usuario,
    /// 09/10/2026; antes 60 s). Sin nada pendiente no sube nada: sólo pregunta por cambios de la web. «Sincronizar ahora» no espera.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private TimeSpan delay = Interval;

    public SyncViewModel(SyncEngine engine, SqliteSyncOutbox outbox, Guid terminalId, string terminalName, Action<string, string?> log)
    {
        this.engine = engine; this.outbox = outbox; this.terminalId = terminalId; this.terminalName = terminalName; this.log = log;
        try { BranchName = outbox.Branch().Name; } catch (Exception) { BranchName = ""; }
        try { if (engine.RegisteredApi is { } api) ApiUrl = api.ToString(); } catch (Exception) { } // sync.json ilegible: se queda la dirección predeterminada.
    }

    public SyncStatus Status { get; private set; } = new(SyncState.NotConfigured, 0, null, "");
    public bool IsRegistered { get { try { return engine.RegisteredApi is not null; } catch (Exception) { return false; } } }
    public string ApiUrl { get; set; } = DefaultApiUrl;
    public string BranchName { get; set; }
    public bool Busy { get; private set; }
    /// <summary>Respaldo en la nube: cada hora con la app abierta (decisión del usuario, 08/10/2026), sólo si la base cambió.</summary>
    public static readonly TimeSpan BackupInterval = TimeSpan.FromHours(1);
    private DateTimeOffset? lastBackupAttempt;
    private bool lastBackupFailed;
    public string BackupLabel { get; private set; } = "Respaldo en la nube: pendiente (se hace cada hora con la app abierta).";
    public bool BackingUp { get; private set; }
    /// <summary>Envío en curso (automático o «sincronizar ahora»).</summary>
    public bool Syncing { get; private set; }
    public bool Ready => !Busy;
    public string Message { get; private set; } = "";
    public string StatusLabel => Syncing ? "Nube: sincronizando…" : Status.State switch
    {
        SyncState.UpToDate => "Nube: al día",
        SyncState.Pending => $"Nube: {Status.Pending} pendientes",
        SyncState.Offline => $"Nube: sin conexión · {Status.Pending} pendientes",
        SyncState.NeedsAttention => "Nube: requiere atención",
        _ => "Nube: sin registrar"
    };
    // Punto de color en la barra superior: verde al día, ámbar sin conexión o con algo por revisar, gris sin registrar o pendiente.
    public bool IsUpToDate => Status.State == SyncState.UpToDate;
    public bool NeedsCheck => Status.State is SyncState.Offline or SyncState.NeedsAttention;
    public string StatusDetail => Status.Detail +(Status.LastSuccess is { } at ? $"\nÚltimo envío confirmado: {at.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}" : "")
        + "\nSe sincroniza sola cada 5 segundos; toca la nube para hacerlo ahora.";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify()
    {
        try { System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null))); }
        catch (Exception) { } // Durante el cierre el Dispatcher puede ya no existir.
    }

    public void Start() => _ = Task.Run(LoopAsync);

    // Nunca debe fallar: una tarea en segundo plano con error no observado cierra el POS por política (App.xaml.cs).
    private async Task LoopAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await RunAsync(shown: false);
                // Con todo subido, el respaldo cada hora; si falló, se reintenta a los 15 minutos.
                if (Status.State == SyncState.UpToDate && (lastBackupAttempt is not { } last
                    || DateTimeOffset.UtcNow - last >= (lastBackupFailed ? TimeSpan.FromMinutes(15) : BackupInterval)))
                    await BackupAsync();
                delay = Status.State switch
                {
                    SyncState.Offline => TimeSpan.FromSeconds(Math.Min(300, delay.TotalSeconds * 2)),
                    SyncState.NeedsAttention => TimeSpan.FromMinutes(5),
                    _ => Interval
                };
                await Task.Delay(delay, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error) { log("sync.loop.failed", error.GetType().Name); }
    }

    /// <summary>
    /// Ejecuta un envío fuera del hilo de la interfaz. Nunca lanza excepciones. El envío automático (cada 5 s) no muestra
    /// «sincronizando…» para que la barra superior no parpadee; «Sincronizar ahora» sí.
    /// </summary>
    public async Task RunAsync(bool shown = true)
    {
        if (!await gate.WaitAsync(0)) return;
        Syncing = shown; if (shown) Notify();
        try
        {
            var token = stop.Token;
            Status = await Task.Run(() => engine.RunOnceAsync(token), token);
            if (Status.State is SyncState.NeedsAttention) log("sync.attention", null);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error)
        {
            Status = Status with { State = SyncState.NeedsAttention, Detail = "No se pudo leer la cola de sincronización." };
            log("sync.failed", error.GetType().Name);
        }
        finally { Syncing = false; gate.Release(); Notify(); }
    }

    /// <summary>Respalda ahora en la nube. Nunca lanza excepciones.</summary>
    public async Task BackupAsync()
    {
        if (BackingUp) return;
        BackingUp = true; BackupLabel = "Respaldo en la nube: subiendo…"; Notify();
        lastBackupAttempt = DateTimeOffset.UtcNow;
        try
        {
            var token = stop.Token;
            var result = await Task.Run(() => engine.BackupAsync(token), token);
            var at = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
            BackupLabel = result is null ? "Respaldo en la nube: registra esta terminal para respaldar."
                : result.Uploaded ? $"Respaldo en la nube: {at} · base de {result.DatabaseBytes / 1024:N0} KB" + (result.PhotosUploaded > 0 ? $" y {result.PhotosUploaded} fotos nuevas" : "")
                : $"Respaldo en la nube: {at} · sin cambios desde el anterior";
            lastBackupFailed = false;
            if (result is { Uploaded: true }) log("backup.cloud.uploaded", null);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error)
        {
            lastBackupFailed = true;
            BackupLabel = error is System.Net.Http.HttpRequestException or SyncUnauthorizedException
                ? $"Respaldo en la nube: no se pudo ({error.Message}); se reintenta en 15 minutos."
                : "Respaldo en la nube: no se pudo; se reintenta en 15 minutos.";
            log("backup.cloud.failed", error.GetType().Name);
        }
        finally { BackingUp = false; Notify(); }
    }

    public async Task RegisterAsync(string enrollmentSecret)
    {
        if (Busy) return;
        Busy = true; Message = ""; Notify();
        try
        {
            string url = ApiUrl, branch = BranchName;
            BranchName = await Task.Run(() => engine.RegisterAsync(url, enrollmentSecret, branch, terminalId, terminalName));
            log("sync.registered", null);
            Message = "Terminal registrada. La información pendiente se enviará automáticamente.";
            delay = Interval;
            _ = RunAsync();
        }
        catch (ArgumentException error) { Message = error.Message; }
        catch (SyncUnauthorizedException error) { Message = error.Message; }
        catch (InvalidOperationException error) { Message = error.Message; }
        catch (System.Net.Http.HttpRequestException) { Message = "No se pudo conectar con el servidor. Revisa la dirección e Internet."; }
        catch (Exception error) { Message = "No se pudo registrar la terminal."; log("sync.register.failed", error.GetType().Name); }
        finally { Busy = false; Notify(); }
    }

    // Sólo cancela: el ciclo puede seguir leyendo stop.Token mientras termina.
    public void Dispose() => stop.Cancel();
}
