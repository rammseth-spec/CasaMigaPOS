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
    private TimeSpan delay = TimeSpan.FromSeconds(15);

    public SyncViewModel(SyncEngine engine, SqliteSyncOutbox outbox, Guid terminalId, string terminalName, Action<string, string?> log)
    {
        this.engine = engine; this.outbox = outbox; this.terminalId = terminalId; this.terminalName = terminalName; this.log = log;
        try { BranchName = outbox.Branch().Name; } catch (Exception) { BranchName = ""; }
    }

    public SyncStatus Status { get; private set; } = new(SyncState.NotConfigured, 0, null, "");
    public string ApiUrl { get; set; } = DefaultApiUrl;
    public string BranchName { get; set; }
    public bool Busy { get; private set; }
    public bool Ready => !Busy;
    public string Message { get; private set; } = "";
    public string StatusLabel => Status.State switch
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
    public string StatusDetail => Status.Detail +(Status.LastSuccess is { } at ? $"\nÚltimo envío confirmado: {at.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}" : "");
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
                await RunAsync();
                delay = Status.State switch
                {
                    SyncState.Offline => TimeSpan.FromSeconds(Math.Min(300, delay.TotalSeconds * 2)),
                    SyncState.NeedsAttention => TimeSpan.FromMinutes(5),
                    _ => TimeSpan.FromSeconds(15)
                };
                await Task.Delay(delay, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error) { log("sync.loop.failed", error.GetType().Name); }
    }

    /// <summary>Ejecuta un envío fuera del hilo de la interfaz. Nunca lanza excepciones.</summary>
    public async Task RunAsync()
    {
        if (!await gate.WaitAsync(0)) return;
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
        finally { gate.Release(); Notify(); }
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
            delay = TimeSpan.FromSeconds(15);
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
