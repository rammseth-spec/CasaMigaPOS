using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

/// <summary>Configuración de sincronización por instalación (sync.json). La llave de la terminal se guarda cifrada con DPAPI.</summary>
public sealed record SyncSettings(Uri ApiUrl, Guid BranchId, string ProtectedKey)
{
    public const string FileName = "sync.json";

    public static SyncSettings? Load(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, FileName);
        if (!File.Exists(path)) return null;
        var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path)) ?? throw new InvalidDataException("sync.json inválido.");
        return new(ValidateUrl(stored.ApiUrl), stored.BranchId, stored.ProtectedKey);
    }

    public void Save(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, FileName);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new Stored(ApiUrl.ToString(), BranchId, ProtectedKey)));
        File.Move(temporary, path, overwrite: true);
    }

    public static SyncSettings Create(Uri apiUrl, Guid branchId, string terminalKey) => new(apiUrl, branchId, Dpapi.Protect(terminalKey));
    public string TerminalKey() => Dpapi.Unprotect(ProtectedKey);

    public static Uri ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) throw new ArgumentException("La dirección del servidor no es válida.");
        var local = uri.IsLoopback;
        if (uri.Scheme != Uri.UriSchemeHttps && !(local && uri.Scheme == Uri.UriSchemeHttp))
            throw new ArgumentException("La dirección del servidor debe usar https://.");
        return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
    }

    private sealed record Stored(string ApiUrl, Guid BranchId, string ProtectedKey);
}

/// <summary>DPAPI (usuario actual) sin dependencias adicionales.</summary>
internal static class Dpapi
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CasaMiga.Sync.v1");

    public static string Protect(string secret) => Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(secret), protect: true));
    public static string Unprotect(string value) => Encoding.UTF8.GetString(Transform(Convert.FromBase64String(value), protect: false));

    private static byte[] Transform(byte[] input, bool protect)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("La llave de sincronización requiere Windows.");
        var data = Pin(input); var entropy = Pin(Entropy);
        try
        {
            var ok = protect
                ? CryptProtectData(ref data.Blob, "CasaMiga", ref entropy.Blob, IntPtr.Zero, IntPtr.Zero, 0x1, out var output)
                : CryptUnprotectData(ref data.Blob, IntPtr.Zero, ref entropy.Blob, IntPtr.Zero, IntPtr.Zero, 0x1, out output);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally { LocalFree(output.Data); }
        }
        finally { data.Handle.Free(); entropy.Handle.Free(); }
    }

    private static (GCHandle Handle, DataBlob Blob) Pin(byte[] bytes)
    {
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        return (handle, new DataBlob { Size = bytes.Length, Data = handle.AddrOfPinnedObject() });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { public int Size; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob dataIn, string description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);
}

public sealed record SyncBatchResult(IReadOnlyList<long> Accepted, IReadOnlyList<long> Conflicts);
public sealed class SyncUnauthorizedException(string message) : Exception(message);

public interface ISyncTransport
{
    Task<string> RegisterAsync(Uri api, string enrollmentSecret, BranchIdentity branch, Guid terminalId, string terminalName, CancellationToken cancel);
    Task<SyncBatchResult> SendAsync(Uri api, string terminalKey, Guid branchId, IReadOnlyList<SyncEnvelope> batch, CancellationToken cancel);
    /// <summary>Cambios hechos en la versión web para la sucursal, posteriores al cursor.</summary>
    Task<IReadOnlyList<CloudCommand>> PullCommandsAsync(Uri api, string terminalKey, long after, CancellationToken cancel) => Task.FromResult<IReadOnlyList<CloudCommand>>([]);
    Task ReportCommandsAsync(Uri api, string terminalKey, IReadOnlyList<CloudCommandResult> results, CancellationToken cancel) => Task.CompletedTask;
}

public sealed class HttpSyncTransport(HttpClient http) : ISyncTransport
{
    public async Task<string> RegisterAsync(Uri api, string enrollmentSecret, BranchIdentity branch, Guid terminalId, string terminalName, CancellationToken cancel)
    {
        using var response = await http.PostAsJsonAsync(new Uri(api, "api/terminals/register"), new
        {
            enrollmentSecret, branch = new { id = branch.Id, name = branch.Name }, terminal = new { id = terminalId, name = terminalName }
        }, cancel);
        if (response.StatusCode == HttpStatusCode.Forbidden) throw new SyncUnauthorizedException("El secreto de alta no es correcto.");
        var body = await ReadBody(response, cancel);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException((string?)body?["error"] ?? $"El servidor respondió {(int)response.StatusCode}. Revisa la dirección.");
        return (string?)body?["terminalKey"] ?? throw new InvalidOperationException("La dirección no corresponde a un servidor de Casa Miga.");
    }

    public async Task<SyncBatchResult> SendAsync(Uri api, string terminalKey, Guid branchId, IReadOnlyList<SyncEnvelope> batch, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(api, "api/sync/batch"))
        {
            Content = JsonContent.Create(new
            {
                branchId,
                entries = batch.Select(e => new
                {
                    seq = e.Entry.Sequence, entity = e.Entry.Entity, entityId = e.Entry.EntityId, change = e.Entry.Change,
                    enqueuedAt = e.Entry.EnqueuedAt.ToUniversalTime().ToString("O"), payload = e.Payload
                })
            })
        };
        request.Headers.Authorization = new("Bearer", terminalKey);
        using var response = await http.SendAsync(request, cancel);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new SyncUnauthorizedException("El servidor rechazó la terminal. Vuelve a registrarla.");
        var body = await ReadBody(response, cancel);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
            throw new HttpRequestException((string?)body?["error"] ?? $"El servidor respondió {(int)response.StatusCode}.");
        // Un 200 que no es JSON (p. ej. portal de una red Wi-Fi) no confirma nada: se trata como sin conexión.
        if (body is null) throw new HttpRequestException("Respuesta no válida del servidor.");
        static long[] Read(JsonNode? node) => node is JsonArray a ? a.Select(x => x!.GetValue<long>()).ToArray() : [];
        return new(Read(body["accepted"]), Read(body["conflicts"]));
    }

    public async Task<IReadOnlyList<CloudCommand>> PullCommandsAsync(Uri api, string terminalKey, long after, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(api, $"api/sync/commands?after={after}"));
        request.Headers.Authorization = new("Bearer", terminalKey);
        using var response = await http.SendAsync(request, cancel);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new SyncUnauthorizedException("El servidor rechazó la terminal. Vuelve a registrarla.");
        var body = await ReadBody(response, cancel);
        if (!response.IsSuccessStatusCode || body?["commands"] is not JsonArray list) throw new HttpRequestException((string?)body?["error"] ?? "Respuesta no válida del servidor.");
        return list.OfType<JsonObject>().Select(c => new CloudCommand(
            Guid.Parse((string)c["id"]!), (long)c["seq"]!, (string)c["kind"]!, c["payload"] as JsonObject ?? [],
            Guid.TryParse((string?)c["requestedBy"]?["id"], out var by) ? by : Guid.Empty, (string?)c["requestedBy"]?["name"] ?? "web",
            DateTimeOffset.TryParse((string?)c["requestedAt"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at) ? at : null)).ToList();
    }

    public async Task ReportCommandsAsync(Uri api, string terminalKey, IReadOnlyList<CloudCommandResult> results, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(api, "api/sync/commands"))
        {
            Content = JsonContent.Create(new { results = results.Select(r => new { id = r.Id, status = r.Status, message = r.Message }) })
        };
        request.Headers.Authorization = new("Bearer", terminalKey);
        using var response = await http.SendAsync(request, cancel);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new SyncUnauthorizedException("El servidor rechazó la terminal. Vuelve a registrarla.");
        var body = await ReadBody(response, cancel);
        if (!response.IsSuccessStatusCode || body is null) throw new HttpRequestException((string?)body?["error"] ?? "Respuesta no válida del servidor.");
    }

    /// <summary>Lee el cuerpo sin fallar si no es JSON (páginas de error de un gateway, portales cautivos).</summary>
    private static async Task<JsonObject?> ReadBody(HttpResponseMessage response, CancellationToken cancel)
    {
        var text = await response.Content.ReadAsStringAsync(cancel);
        try { return JsonNode.Parse(text) as JsonObject; }
        catch (JsonException) { return null; }
    }
}

public enum SyncState { NotConfigured, UpToDate, Pending, Offline, NeedsAttention }
public sealed record SyncStatus(SyncState State, long Pending, DateTimeOffset? LastSuccess, string Detail);

/// <summary>
/// Envío de la cola local. Sólo marca como enviado lo que la nube confirma; ante un corte, lo pendiente se conserva y se reintenta.
/// Un conflicto (mismo seq con otro contenido) detiene el envío hasta revisión.
/// </summary>
public sealed class SyncEngine(SqliteSyncOutbox outbox, ISyncTransport transport, string dataDirectory, TimeProvider? time = null, CloudCommandService? commands = null,
    CloudBackupService? backup = null, HttpClient? backupHttp = null)
{
    /// <summary>Dirección con la que se registró esta caja (null si no está registrada).</summary>
    public Uri? RegisteredApi => SyncSettings.Load(dataDirectory)?.ApiUrl;

    /// <summary>Sube el respaldo de esta caja a la nube (sólo si la base cambió). Null si la caja no está registrada o no hay respaldo configurado.</summary>
    public async Task<CloudBackupResult?> BackupAsync(CancellationToken cancel = default)
    {
        if (backup is null || backupHttp is null || SyncSettings.Load(dataDirectory) is not { } settings) return null;
        return await backup.RunAsync(new HttpCloudBackupTransport(backupHttp, settings.ApiUrl, settings.TerminalKey()), cancel);
    }

    public const int BatchSize = 100;
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    public SyncStatus Status { get; private set; } = new(SyncState.NotConfigured, 0, null, "");
    private DateTimeOffset? lastSuccess;

    public async Task<string> RegisterAsync(string apiUrl, string enrollmentSecret, string branchName, Guid terminalId, string terminalName, CancellationToken cancel = default)
    {
        var api = SyncSettings.ValidateUrl(apiUrl);
        if (string.IsNullOrWhiteSpace(enrollmentSecret)) throw new ArgumentException("Escribe el secreto de alta.");
        var name = branchName.Trim();
        if (name.Length is < 1 or > 100) throw new ArgumentException("El nombre de la sucursal debe tener de 1 a 100 caracteres.");
        var branch = outbox.Branch() with { Name = name };
        var key = await transport.RegisterAsync(api, enrollmentSecret.Trim(), branch, terminalId, terminalName, cancel);
        SyncSettings.Create(api, branch.Id, key).Save(dataDirectory);
        outbox.RenameBranch(name);
        Status = Status with { State = SyncState.Pending, Detail = "Terminal registrada." };
        return branch.Name;
    }

    /// <summary>
    /// Envía lotes hasta vaciar la cola o encontrar un problema; con todo enviado, aplica los cambios hechos en la web,
    /// informa el resultado y sube lo que esos cambios produjeron. Devuelve el estado resultante.
    /// </summary>
    public async Task<SyncStatus> RunOnceAsync(CancellationToken cancel = default)
    {
        var settings = SyncSettings.Load(dataDirectory);
        if (settings is null) return Status = new(SyncState.NotConfigured, outbox.PendingCount(), null, "Registra esta terminal para sincronizar.");
        var key = settings.TerminalKey();
        var status = await PushAsync(settings, key, cancel);
        if (status.State != SyncState.UpToDate || commands is null) return status;
        try
        {
            if (await ApplyCommandsAsync(settings, key, cancel)) status = await PushAsync(settings, key, cancel);
            return status;
        }
        catch (SyncUnauthorizedException error)
        {
            return Status = new(SyncState.NeedsAttention, outbox.PendingCount(), lastSuccess, error.Message);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException && !cancel.IsCancellationRequested)
        {
            return Status = new(SyncState.Offline, outbox.PendingCount(), lastSuccess, "Sin conexión con el servidor; se reintentará.");
        }
        catch (Exception error) when (!cancel.IsCancellationRequested)
        {
            return Status = new(SyncState.NeedsAttention, outbox.PendingCount(), lastSuccess, $"No se pudo aplicar un cambio de la versión web ({error.GetType().Name}); se reintentará.");
        }
    }

    /// <summary>Aplica en orden los cambios de la web y reporta cada resultado. Devuelve si alguno cambió datos de esta caja.</summary>
    private async Task<bool> ApplyCommandsAsync(SyncSettings settings, string key, CancellationToken cancel)
    {
        var changed = false;
        await ReportAsync(settings, key, cancel);
        while (true)
        {
            var before = commands!.LastSequence();
            var pulled = await transport.PullCommandsAsync(settings.ApiUrl, key, commands.PullCursor(), cancel);
            foreach (var command in pulled.OrderBy(c => c.Sequence))
            {
                var known = commands.IsKnown(command.Id);
                var result = commands.Apply(command);
                changed |= !known && result.Applied;
            }
            await ReportAsync(settings, key, cancel);
            // Un lote lleno puede traer más; si el cursor no avanzó, ya no hay nada nuevo (evita repetir el mismo tramo).
            if (pulled.Count < 50 || commands.LastSequence() == before) return changed;
        }
    }

    private async Task ReportAsync(SyncSettings settings, string key, CancellationToken cancel)
    {
        var unreported = commands!.Unreported();
        if (unreported.Count == 0) return;
        await transport.ReportCommandsAsync(settings.ApiUrl, key, unreported, cancel);
        commands.MarkReported(unreported.Select(r => r.Id).ToArray());
    }

    private async Task<SyncStatus> PushAsync(SyncSettings settings, string key, CancellationToken cancel)
    {
        while (true)
        {
            var batch = outbox.PendingEnvelopes(BatchSize);
            if (batch.Count == 0) { lastSuccess = clock.GetUtcNow(); return Status = new(SyncState.UpToDate, 0, lastSuccess, "Todo sincronizado."); }
            SyncBatchResult result;
            try { result = await transport.SendAsync(settings.ApiUrl, key, settings.BranchId, batch, cancel); }
            catch (SyncUnauthorizedException error)
            {
                outbox.MarkFailed(batch[0].Entry.Sequence, error.Message);
                return Status = new(SyncState.NeedsAttention, outbox.PendingCount(), lastSuccess, error.Message);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException && !cancel.IsCancellationRequested)
            {
                outbox.MarkFailed(batch[0].Entry.Sequence, error.Message);
                return Status = new(SyncState.Offline, outbox.PendingCount(), lastSuccess, "Sin conexión con el servidor; se reintentará.");
            }
            var sent = batch.Select(b => b.Entry.Sequence).ToHashSet();
            var accepted = result.Accepted.Where(sent.Contains).ToArray();
            outbox.MarkSent(accepted, clock.GetUtcNow());
            if (accepted.Length > 0) lastSuccess = clock.GetUtcNow();
            if (result.Conflicts.Count > 0)
            {
                foreach (var seq in result.Conflicts.Where(sent.Contains)) outbox.MarkFailed(seq, "Conflicto: la nube ya tiene otro registro con esta secuencia.");
                return Status = new(SyncState.NeedsAttention, outbox.PendingCount(), lastSuccess, "La nube reportó registros en conflicto (¿se restauró un respaldo antiguo?). Revisión técnica requerida.");
            }
            if (accepted.Length == 0) return Status = new(SyncState.NeedsAttention, outbox.PendingCount(), lastSuccess, "El servidor no confirmó ningún registro.");
        }
    }
}
