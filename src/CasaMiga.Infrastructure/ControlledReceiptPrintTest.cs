using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
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
    string PayloadSha256,
    string PayloadPath,
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
            || settings.CutMode is not ("Disabled" or "Partial") || settings.OpenCashDrawer)
            throw new InvalidOperationException("La prueba fisica exige 48 columnas, ASCII, corte controlado y cajon cerrado.");
        if (!string.Equals(settings.Business.DisplayName, "PRUEBA CASA MIGA", StringComparison.Ordinal))
            throw new InvalidOperationException("La prueba fisica exige el perfil PRUEBA CASA MIGA.");
        var fullTracePath=Path.GetFullPath(tracePath);var payloadPath=fullTracePath+".payload.bin";
        if(File.Exists(fullTracePath) || File.Exists(payloadPath))
            throw new InvalidOperationException("Este directorio ya contiene un intento fisico; usa un directorio nuevo para evitar reenvios.");

        var requestedAt = clock.UtcNow;
        var requestId = Guid.NewGuid();
        var document = Document(settings.Business, requestedAt);
        var payload = renderer.Render(document, ReceiptCopyKind.Original, settings);
        ValidateSafePayload(payload,settings.CutMode);
        var text = PrintableText(payload);
        var hash=Convert.ToHexString(SHA256.HashData(payload));
        WritePayload(payloadPath,payload);

        AppendTrace(new(requestId, requestedAt, settings.QueueName, "REQUESTED", null, payload.Length, hash, null));
        try
        {
            var submitted = transport.Submit(new(settings.QueueName, $"Casa Miga PRUEBA {requestId:N}", payload));
            AppendTrace(new(requestId, clock.UtcNow, settings.QueueName, "SUBMITTED", submitted.JobId, payload.Length, hash, null));
            return new(requestId, requestedAt, settings.QueueName, ReceiptPrintState.Submitted, submitted.JobId,
                payload.Length, text, hash, payloadPath, null, fullTracePath);
        }
        catch (PrintTransportException error)
        {
            var state = error.OutcomeUncertain ? ReceiptPrintState.Unknown : ReceiptPrintState.Failed;
            AppendTrace(new(requestId, clock.UtcNow, settings.QueueName, state.ToString().ToUpperInvariant(), null, payload.Length, hash, error.ErrorCode));
            return new(requestId, requestedAt, settings.QueueName, state, null, payload.Length, text, hash, payloadPath, error.ErrorCode, fullTracePath);
        }
        catch (Exception error)
        {
            var code = "PRINT_TEST_" + new string(error.GetType().Name.Where(char.IsAsciiLetterOrDigit).Take(40).ToArray()).ToUpperInvariant();
            AppendTrace(new(requestId, clock.UtcNow, settings.QueueName, "FAILED", null, payload.Length, hash, code));
            return new(requestId, requestedAt, settings.QueueName, ReceiptPrintState.Failed, null, payload.Length, text, hash, payloadPath, code, fullTracePath);
        }
    }

    private static ReceiptDocument Document(BusinessReceiptProfile business, DateTimeOffset timestamp)
    {
        var total = new Money(1900);
        return new(ReceiptDocument.CurrentVersion, "TEST-002", timestamp,
            Guid.Parse("11111111-1111-1111-1111-111111111111"), "PRUEBA",
            Guid.Parse("22222222-2222-2222-2222-222222222222"), "DESARROLLO",
            [new ReceiptLine(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Producto prueba", total, 1, total)],
            total, total, "CASH", total, new Money(2000), new Money(100), business);
    }

    private static void ValidateSafePayload(byte[] payload,string cutMode)
    {
        if(payload.Length==0 || payload.Any(value=>value>0x7f || value==0x1b))throw Unsafe();
        var cuts=0;
        for(var index=0;index<payload.Length;index++)
        {
            if(payload[index]==0x1d)
            {
                if(index+2>=payload.Length)throw Unsafe();
                var command=payload[index+1];var parameter=payload[index+2];
                if(command==0x21 && parameter is 0x00 or 0x01 or 0x11){index+=2;continue;}
                if(command==0x56 && parameter==0x01 && cutMode=="Partial" && index==payload.Length-3){cuts++;index+=2;continue;}
                throw Unsafe();
            }
            if(payload[index]<0x20 && payload[index] is not (0x0a or 0x0d))throw Unsafe();
        }
        if(cuts!=(cutMode=="Partial"?1:0))throw Unsafe();
    }

    private static InvalidOperationException Unsafe()=>new("El ticket contiene bytes fuera del conjunto ASCII/ESC-POS autorizado.");
    private static string PrintableText(byte[] payload)
    {
        using var text=new MemoryStream();
        for(var index=0;index<payload.Length;index++)
        {
            if(payload[index]==0x1d){index+=2;continue;}
            text.WriteByte(payload[index]);
        }
        return Encoding.ASCII.GetString(text.ToArray()).TrimEnd('\r','\n')+"\r\n";
    }

    private static void WritePayload(string path,byte[] payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);
        stream.Write(payload);stream.Flush(true);
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
        string State, int? JobId, int ByteCount, string PayloadSha256, string? ErrorCode);
}
