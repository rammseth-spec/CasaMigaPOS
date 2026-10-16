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
        AddOrderingContact(lines, receipt.Business.Phone);
        AddOptionalCentered(lines, receipt.Business.TaxId);
        lines.Add(new string('-', Columns));
        if (copyKind != ReceiptCopyKind.Original) AddCentered(lines, "*** REIMPRESIÓN ***");
        AddWrapped(lines, HumanSaleNumber(receipt.Folio));
        AddWrapped(lines, $"Fecha: {receipt.ConfirmedAt:dd/MM/yyyy · HH:mm}");
        AddWrapped(lines, $"Cajero: {receipt.CashierName}");
        lines.Add(new string('-', Columns));
        for(var index=0;index<receipt.Items.Count;index++)
        {
            var item=receipt.Items[index];
            AddWrapped(lines, item.ProductName,Math.Min(31,Columns));
            var left = $"{item.Quantity} x {Money(item.UnitPrice)}";
            lines.Add(Pair(left, Money(item.LineTotal)));
            if(index<receipt.Items.Count-1)lines.Add("");
        }
        lines.Add(new string('-', Columns));
        lines.Add(Pair("Subtotal", Money(receipt.Subtotal)));
        lines.Add(Pair("TOTAL", Money(receipt.Total)));
        lines.Add(new string('-', Columns));
        AddWrapped(lines,$"Pago: {Payment(receipt.PaymentMethod)}");
        if(string.Equals(receipt.PaymentMethod,"CASH",StringComparison.Ordinal))
        {
            lines.Add(Pair("Recibido:", Money(receipt.Received)));
            lines.Add(Pair("Cambio:", Money(receipt.Change)));
        }
        else lines.Add(Pair("Pagado:", Money(receipt.Applied)));
        lines.Add(new string('-', Columns));
        AddContactFooter(lines, receipt.Business.Contact);
        lines.Add("");
        AddCentered(lines, receipt.Business.ClosingMessage);
        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    private static string Money(Money value) => "$" + (value.Cents / 100m).ToString("N2", CultureInfo.GetCultureInfo("es-MX"));
    private static string Payment(string method)=>method switch{"CASH"=>"Efectivo","CARD"=>"Tarjeta","TRANSFER"=>"Transferencia",_=>method};
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
    private void AddOrderingContact(List<string> lines,string? phone)
    {
        if(string.IsNullOrWhiteSpace(phone))return;
        AddCentered(lines,"Pedidos por WhatsApp");
        AddOptionalCentered(lines,phone);
    }
    private void AddContactFooter(List<string> lines,string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return;
        var entries=value.Replace("\r","").Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        var facebook=entries.SingleOrDefault(line=>line.StartsWith("Facebook:",StringComparison.OrdinalIgnoreCase));
        var instagram=entries.SingleOrDefault(line=>line.StartsWith("Instagram:",StringComparison.OrdinalIgnoreCase));
        if(facebook is not null&&instagram is not null&&entries.Length==2)
        {
            AddCentered(lines,"Síguenos en:");
            var facebookAccount=facebook[(facebook.IndexOf(':')+1)..].Trim();
            var instagramAccount=instagram[(instagram.IndexOf(':')+1)..].Trim();
            AddCentered(lines,$"{facebookAccount}   {instagramAccount}");
            return;
        }
        AddOptionalCentered(lines,value);
    }
    private void AddCentered(List<string> lines,string value)
    {
        foreach(var part in Wrap(value))lines.Add(part.Length>=Columns?part:new string(' ',(Columns-part.Length)/2)+part);
    }
    private void AddWrapped(List<string> lines,string value,int? width=null)=>lines.AddRange(Wrap(value,width));
    private IEnumerable<string> Wrap(string value,int? requestedWidth=null)
    {
        var width=requestedWidth??Columns;
        if(width<1||width>Columns)throw new ArgumentOutOfRangeException(nameof(requestedWidth));
        var normalized=value.Replace("\r","").Replace('\n',' ').Trim();
        if(normalized.Length==0){yield return "";yield break;}
        while(normalized.Length>width)
        {
            var split=normalized.LastIndexOf(' ',width);
            if(split<1)split=width;
            yield return normalized[..split].TrimEnd();normalized=normalized[split..].TrimStart();
        }
        yield return normalized;
    }
}
