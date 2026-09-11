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
