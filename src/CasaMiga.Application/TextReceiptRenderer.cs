using System.Globalization;
using System.Text;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed class TextReceiptRenderer(int columns = 48)
{
    public int Columns { get; } = columns is >= 32 and <= 64 ? columns : throw new ArgumentOutOfRangeException(nameof(columns));

    public string Render(ReceiptDocument receipt, ReceiptCopyKind copyKind = ReceiptCopyKind.Original)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var lines = new List<string>();
        AddCentered(lines, "Hecho en:");
        AddCentered(lines, receipt.Business.DisplayName);
        AddOptionalCentered(lines, receipt.Business.Address);
        AddOptionalCentered(lines, receipt.Business.Phone);
        AddOptionalCentered(lines, receipt.Business.TaxId);
        AddOptionalCentered(lines, receipt.Business.Contact);
        if (copyKind != ReceiptCopyKind.Original) AddCentered(lines, "*** REIMPRESIÓN ***");
        lines.Add(new string('-', Columns));
        AddWrapped(lines, HumanSaleNumber(receipt.Folio));
        AddWrapped(lines, $"Fecha: {receipt.ConfirmedAt:dd/MM/yyyy · HH:mm}");
        AddWrapped(lines, $"Cajero: {receipt.CashierName}");
        lines.Add(new string('-', Columns));
        foreach (var item in receipt.Items)
        {
            AddWrapped(lines, item.ProductName);
            var left = $"{item.Quantity} x {Money(item.UnitPrice)}";
            lines.Add(Pair(left, Money(item.LineTotal)));
        }
        lines.Add(new string('-', Columns));
        lines.Add(Pair("Subtotal:", Money(receipt.Subtotal)));
        lines.Add(Pair("TOTAL:", Money(receipt.Total)));
        lines.Add(Pair("Pago:", Payment(receipt.PaymentMethod)));
        lines.Add(Pair("Recibido:", Money(receipt.Received)));
        lines.Add(Pair("Cambio:", Money(receipt.Change)));
        lines.Add(new string('-', Columns));
        AddCentered(lines, receipt.Business.ClosingMessage);
        return string.Join("\n", lines) + "\n";
    }

    private static string Money(Money value) => "$" + (value.Cents / 100m).ToString("N2", CultureInfo.GetCultureInfo("es-MX"));
    private static string Payment(string method)=>string.Equals(method,"CASH",StringComparison.Ordinal)?"Efectivo":method;
    private static string HumanSaleNumber(string folio)
    {
        var separator=folio.LastIndexOf('-');var token=separator>=0?folio[(separator+1)..]:folio;
        if(!long.TryParse(token,NumberStyles.None,CultureInfo.InvariantCulture,out var sequence)||sequence<1)
            throw new InvalidDataException("El folio histórico no contiene una secuencia de venta válida.");
        return $"Venta #{sequence:D6}";
    }
    private string Pair(string left,string right)
    {
        if (right.Length >= Columns) return right[^Columns..];
        var available=Columns-right.Length-1;
        if(left.Length>available)left=left[..Math.Max(0,available)];
        return left+new string(' ',Columns-left.Length-right.Length)+right;
    }
    private void AddOptionalCentered(List<string> lines,string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return;
        foreach(var paragraph in value.Replace("\r","").Split('\n'))
            if(!string.IsNullOrWhiteSpace(paragraph))AddCentered(lines,paragraph.Trim());
    }
    private void AddCentered(List<string> lines,string value)
    {
        foreach(var part in Wrap(value))lines.Add(part.Length>=Columns?part:new string(' ',(Columns-part.Length)/2)+part);
    }
    private void AddWrapped(List<string> lines,string value)=>lines.AddRange(Wrap(value));
    private IEnumerable<string> Wrap(string value)
    {
        var normalized=value.Replace("\r","").Replace('\n',' ').Trim();
        if(normalized.Length==0){yield return "";yield break;}
        while(normalized.Length>Columns)
        {
            var split=normalized.LastIndexOf(' ',Columns);
            if(split<1)split=Columns;
            yield return normalized[..split].TrimEnd();normalized=normalized[split..].TrimStart();
        }
        yield return normalized;
    }
}
