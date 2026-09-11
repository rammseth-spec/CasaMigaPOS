using System.Text;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Infrastructure;

public sealed record ControlledReceiptPrintTestResult(
    Guid PrintRequestId,
    DateTimeOffset RequestedAt,
    string QueueName,
    ReceiptPrintState State,
    int? JobId,
    int ByteCount,
    string ReceiptText,
    string? ErrorCode,
    string TracePath);

/// <summary>
/// Development-only physical print probe. It never opens the commercial database
/// and therefore cannot create a sale, payment, cash movement, or receipt snapshot.
/// </summary>
public sealed class ControlledReceiptPrintTest(
    IPrintingSettingsProvider configuration,
    IReceiptPayloadRenderer renderer,
    IReceiptPrintTransport transport,
    IClock clock,
    string tracePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ControlledReceiptPrintTestResult Run()
    {
        var configured = configuration.Read();
        if (!configured.Available || configured.Settings is not { } settings)
            throw new InvalidOperationException($"Configuracion de impresion no disponible: {configured.ErrorCode ?? "UNKNOWN"}.");
        if (settings.Columns != 48 || !string.Equals(settings.EncodingName, "us-ascii", StringComparison.OrdinalIgnoreCase)
            || settings.CutMode != "Disabled" || settings.OpenCashDrawer)
            throw new InvalidOperationException("La prueba fisica exige 48 columnas, ASCII, corte deshabilitado y cajon cerrado.");
        if (!string.Equals(settings.Business.DisplayName, "PRUEBA CASA MIGA", StringComparison.Ordinal))
            throw new InvalidOperationException("La prueba fisica exige el perfil PRUEBA CASA MIGA.");

        var requestedAt = clock.UtcNow;
        var requestId = Guid.NewGuid();
        var document = Document(settings.Business, requestedAt);
        var payload = renderer.Render(document, ReceiptCopyKind.Original, settings);
        ValidateSafePayload(payload);
        var text = Encoding.ASCII.GetString(payload);

        AppendTrace(new(requestId, requestedAt, settings.QueueName, "REQUESTED", null, payload.Length, null));
        try
        {
            var submitted = transport.Submit(new(settings.QueueName, $"Casa Miga PRUEBA {requestId:N}", payload));
            AppendTrace(new(requestId, clock.UtcNow, settings.QueueName, "SUBMITTED", submitted.JobId, payload.Length, null));
            return new(requestId, requestedAt, settings.QueueName, ReceiptPrintState.Submitted, submitted.JobId,
                payload.Length, text, null, Path.GetFullPath(tracePath));
        }
        catch (PrintTransportException error)
        {
            var state = error.OutcomeUncertain ? ReceiptPrintState.Unknown : ReceiptPrintState.Failed;
            AppendTrace(new(requestId, clock.UtcNow, settings.QueueName, state.ToString().ToUpperInvariant(), null, payload.Length, error.ErrorCode));
            return new(requestId, requestedAt, settings.QueueName, state, null, payload.Length, text, error.ErrorCode, Path.GetFullPath(tracePath));
        }
        catch (Exception error)
        {
            var code = "PRINT_TEST_" + new string(error.GetType().Name.Where(char.IsAsciiLetterOrDigit).Take(40).ToArray()).ToUpperInvariant();
            AppendTrace(new(requestId, clock.UtcNow, settings.QueueName, "FAILED", null, payload.Length, code));
            return new(requestId, requestedAt, settings.QueueName, ReceiptPrintState.Failed, null, payload.Length, text, code, Path.GetFullPath(tracePath));
        }
    }

    private static ReceiptDocument Document(BusinessReceiptProfile business, DateTimeOffset timestamp)
    {
        var total = new Money(1900);
        return new(ReceiptDocument.CurrentVersion, "TEST-001", timestamp,
            Guid.Parse("11111111-1111-1111-1111-111111111111"), "PRUEBA",
            Guid.Parse("22222222-2222-2222-2222-222222222222"), "DESARROLLO",
            [new ReceiptLine(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Producto prueba", total, 1, total)],
            total, total, "CASH", total, new Money(2000), new Money(100), business);
    }

    private static void ValidateSafePayload(byte[] payload)
    {
        if (payload.Length == 0 || payload.Any(value => value > 0x7f || value is 0x1b or 0x1d))
            throw new InvalidOperationException("El ticket de prueba no es ASCII seguro o contiene ESC/GS.");
    }

    private void AppendTrace(ControlledPrintTrace entry)
    {
        var fullPath = Path.GetFullPath(tracePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
        using var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read,
            4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(true);
    }

    private sealed record ControlledPrintTrace(Guid PrintRequestId, DateTimeOffset Timestamp, string QueueName,
        string State, int? JobId, int ByteCount, string? ErrorCode);
}
