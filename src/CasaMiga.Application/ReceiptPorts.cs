namespace CasaMiga.Application;

public sealed record PrintingSettings(
    int Version,
    bool Enabled,
    string QueueName,
    int Columns,
    string EncodingName,
    string CutMode,
    bool OpenCashDrawer,
    BusinessReceiptProfile Business,
    int? CharacterTable = null);

public sealed record PrintingSettingsResult(PrintingSettings? Settings, string? ErrorCode)
{
    public bool Available => Settings is { Enabled: true } && ErrorCode is null;
}

public interface IPrintingSettingsProvider { PrintingSettingsResult Read(); }
public interface IReceiptPayloadRenderer
{
    byte[] Render(ReceiptDocument document, ReceiptCopyKind copyKind, PrintingSettings settings);
}
public sealed record PrintSubmission(string QueueName, string JobName, byte[] Payload);
public sealed record PrintSubmissionResult(int JobId);
public interface IReceiptPrintTransport { PrintSubmissionResult Submit(PrintSubmission submission); }

public sealed class PrintTransportException(string errorCode, bool outcomeUncertain, Exception? inner = null)
    : Exception("No se pudo enviar el ticket a la cola de impresión.", inner)
{
    public string ErrorCode { get; } = Normalize(errorCode);
    public bool OutcomeUncertain { get; } = outcomeUncertain;
    private static string Normalize(string value)
    {
        if(string.IsNullOrWhiteSpace(value))return "PRINT_TRANSPORT";
        var normalized=new string(value.Where(character=>char.IsAsciiLetterOrDigit(character) || character=='_').Take(64).ToArray());
        return normalized.Length==0?"PRINT_TRANSPORT":normalized;
    }
}

/// <summary>Un ticket enviado (original, reintento o reimpresión) con su último estado registrado.</summary>
public sealed record ReceiptActivity(Guid RequestId, Guid SaleId, string TerminalName, long Sequence, ReceiptCopyKind Kind,
    DateTimeOffset RequestedAt, string RequestedBy, ReceiptPrintState State, int? SpoolerJobId, string? ErrorCode, long TotalCents = 0)
{
    public string Folio => CasaMiga.Domain.DisplayFolio.For(TerminalName, Sequence);
}

/// <summary>Un trabajo que sigue en la cola de Windows de la impresora de tickets.</summary>
public sealed record PrintQueueJob(int JobId, string Document, string Status, DateTimeOffset? SubmittedAt, int TotalPages, int PagesPrinted, string? User);
public sealed record PrintQueueSnapshot(string? QueueName, IReadOnlyList<PrintQueueJob> Jobs, string? ErrorCode)
{
    public bool Available => QueueName is not null && ErrorCode is null;
}

/// <summary>Lee y cancela trabajos de la cola de impresión de Windows (spooler). No toca ventas ni tickets registrados.</summary>
public interface IPrintQueueMonitor
{
    IReadOnlyList<PrintQueueJob> List(string queueName);
    void Cancel(string queueName, int jobId);
}
