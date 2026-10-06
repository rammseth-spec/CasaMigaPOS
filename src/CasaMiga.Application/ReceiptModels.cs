using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed record BusinessReceiptProfile(
    string DisplayName,
    string? Address = null,
    string? Phone = null,
    string? TaxId = null,
    string? Contact = null,
    string ClosingMessage = "Gracias por su compra.",
    string? MonochromeLogoPath = null);

public sealed record ReceiptLine
{
    public Guid ProductId { get; }
    public string ProductName { get; }
    public Money UnitPrice { get; }
    public int Quantity { get; }
    public Money LineTotal { get; }
    [JsonConstructor]
    public ReceiptLine(Guid productId,string productName,Money unitPrice,int quantity,Money lineTotal)
    {
        if(productId==Guid.Empty || string.IsNullOrWhiteSpace(productName) || unitPrice.Cents<0 || quantity<1
            || lineTotal.Cents<0 || unitPrice.Multiply(quantity)!=lineTotal)throw new ArgumentException("Partida de ticket inválida.");
        ProductId=productId;ProductName=productName.Trim();UnitPrice=unitPrice;Quantity=quantity;LineTotal=lineTotal;
    }
}

public sealed class ReceiptDocument
{
    public const int CurrentVersion = 1;
    public int Version { get; }
    public string Folio { get; }
    public DateTimeOffset ConfirmedAt { get; }
    public Guid UserId { get; }
    public string CashierName { get; }
    public Guid TerminalId { get; }
    public string TerminalName { get; }
    public IReadOnlyList<ReceiptLine> Items { get; }
    public Money Subtotal { get; }
    public Money Total { get; }
    public string PaymentMethod { get; }
    public Money Applied { get; }
    public Money Received { get; }
    public Money Change { get; }
    public BusinessReceiptProfile Business { get; }

    [JsonIgnore]
    public string Sha256 => ReceiptDocumentCodec.Hash(ReceiptDocumentCodec.Serialize(this));

    [JsonConstructor]
    public ReceiptDocument(int version, string folio, DateTimeOffset confirmedAt, Guid userId, string cashierName,
        Guid terminalId, string terminalName, IReadOnlyList<ReceiptLine> items, Money subtotal, Money total,
        string paymentMethod, Money applied, Money received, Money change, BusinessReceiptProfile business)
    {
        ArgumentNullException.ThrowIfNull(items);ArgumentNullException.ThrowIfNull(business);
        if (version != CurrentVersion || string.IsNullOrWhiteSpace(folio) || folio.Length>80 || confirmedAt==default || userId == Guid.Empty || terminalId == Guid.Empty
            || string.IsNullOrWhiteSpace(cashierName) || string.IsNullOrWhiteSpace(terminalName) || items.Count == 0
            || paymentMethod is not ("CASH" or "CARD" or "TRANSFER") || string.IsNullOrWhiteSpace(business.DisplayName)
            || string.IsNullOrWhiteSpace(business.ClosingMessage)) throw new ArgumentException("Datos de ticket inválidos.");
        var copy = items.ToArray();
        if(copy.Select(item=>item.ProductId).Distinct().Count()!=copy.Length)throw new ArgumentException("Las partidas de ticket deben ser únicas.");
        var calculated = new Money(0);
        foreach (var item in copy)
        {
            if (item.ProductId == Guid.Empty || string.IsNullOrWhiteSpace(item.ProductName) || item.UnitPrice.Cents<0 || item.Quantity < 1
                || item.LineTotal.Cents<0 || item.LineTotal != item.UnitPrice.Multiply(item.Quantity)) throw new ArgumentException("Partida de ticket inválida.");
            calculated += item.LineTotal;
        }
        if (subtotal.Cents<0 || received.Cents<0 || change.Cents<0 || calculated != subtotal || total != subtotal || applied != total || applied.ChangeFrom(received) != change
            || (paymentMethod != "CASH" && received != applied))
            throw new ArgumentException("Importes de ticket incoherentes.");
        Version = version; Folio = folio.Trim(); ConfirmedAt = confirmedAt; UserId = userId; CashierName = cashierName.Trim();
        TerminalId = terminalId; TerminalName = terminalName.Trim(); Items = Array.AsReadOnly(copy); Subtotal = subtotal; Total = total;
        PaymentMethod = paymentMethod; Applied = applied; Received = received; Change = change; Business = ValidateProfile(business);
    }

    private static BusinessReceiptProfile ValidateProfile(BusinessReceiptProfile profile)
    {
        static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var name = profile.DisplayName.Trim(); var closing = profile.ClosingMessage.Trim();
        if (name.Length > 120 || closing.Length > 500 || new[] { profile.Address, profile.Phone, profile.TaxId, profile.Contact, profile.MonochromeLogoPath }
            .Any(value => value?.Length > 500)) throw new ArgumentException("Configuración comercial demasiado larga.");
        return profile with { DisplayName = name, Address = Optional(profile.Address), Phone = Optional(profile.Phone),
            TaxId = Optional(profile.TaxId), Contact = Optional(profile.Contact), ClosingMessage = closing,
            MonochromeLogoPath = Optional(profile.MonochromeLogoPath) };
    }

    public static ReceiptDocument FromSale(SalesHistoryDetail detail, BusinessReceiptProfile profile, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(detail); ArgumentNullException.ThrowIfNull(timeZone);
        var sale = detail.Sale;
        return new(CurrentVersion, sale.Folio, TimeZoneInfo.ConvertTime(sale.ConfirmedAt, timeZone), sale.UserId,
            detail.CashierName, sale.TerminalId, detail.TerminalName,
            sale.Items.Select(item => new ReceiptLine(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity, item.LineTotal)).ToArray(),
            sale.Subtotal, sale.Total, sale.Payment.Method, sale.Payment.Applied, sale.Payment.Received, sale.Payment.Change, profile);
    }
}

public static class ReceiptDocumentCodec
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string Serialize(ReceiptDocument document) => JsonSerializer.Serialize(document, Options);
    public static ReceiptDocument Deserialize(string json)
        => JsonSerializer.Deserialize<ReceiptDocument>(json, Options) ?? throw new InvalidDataException("Snapshot de ticket inválido.");
    public static string Hash(string canonicalJson) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
}

public enum ReceiptCopyKind { Original, Retry, Reprint }
public enum ReceiptPrintState { Requested, Submitted, Completed, Failed, Unknown }
public sealed record ReceiptSnapshot(Guid SaleId, int SchemaVersion, string ContentJson, string Sha256, DateTimeOffset CreatedAt);
public sealed record ReceiptPrintRequest(Guid Id, Guid SaleId, ReceiptCopyKind Kind, Guid? ParentRequestId,
    Guid RequestedBy, DateTimeOffset RequestedAt, string? QueueName, string? SnapshotSha256);
public sealed record ReceiptPrintEvent(Guid Id, Guid RequestId, ReceiptPrintState State, DateTimeOffset OccurredAt,
    int? SpoolerJobId = null, string? ErrorCode = null);
public sealed record ReceiptPrintResult(Guid RequestId, ReceiptPrintState State, string Message, int? SpoolerJobId = null);
