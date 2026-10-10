using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

/// <summary>Textos de Charolas sin WPF (las pruebas los enlazan).</summary>
internal static class TrayText
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");
    internal static string Format(Money value) => (value.Cents / 100m).ToString("C2", Spanish);
    internal static string Day(DateTimeOffset at)
    {
        var text = at.ToLocalTime().ToString("ddd d MMM", Spanish).Replace(".", "");
        return char.ToUpper(text[0], Spanish) + text[1..];
    }
}

/// <summary>Una entrega del periodo (pestaña Entregas, como la web).</summary>
public sealed record PeriodDeliveryView(ConsignmentDelivery Row)
{
    public string Date => TrayText.Day(Row.DeliveredAt) + " " + Row.DeliveredAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    public string Store => Row.StoreName;
    public string Status => Row.State switch { ConsignmentState.Voided => "Anulada", ConsignmentState.Collected => "Recogida", _ => "Por recoger" };
    public int Pieces => Row.Pieces;
    public string SoldReturned => Row.State == ConsignmentState.Collected ? $"{Row.Settlement!.Sold} / {Row.Settlement.Returned}" : "—";
    public string Charged => Row.State == ConsignmentState.Collected ? TrayText.Format(Row.Settlement!.SoldTotal) : "—";
    public string Contents => string.Join(" · ", Row.Items.Select(i => $"{i.Quantity} {i.ProductName}"));
}
/// <summary>Un cobro del periodo (pestaña Cobros, como la web).</summary>
public sealed record PeriodPaymentView(ConsignmentPaymentRow Row)
{
    public string Date => TrayText.Day(Row.Payment.ReceivedAt) + " " + Row.Payment.ReceivedAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    public string Store => Row.StoreName;
    public string Method => Payment.Label(Row.Payment.Method) + (Row.Payment.Reference is { Length: > 0 } r ? $" · Ref. {r}" : "");
    public string Note => Row.Payment.Note.Length > 0 ? Row.Payment.Note : "—";
    public string Amount => TrayText.Format(Row.Payment.Amount);
    public static string Summary(IReadOnlyList<PeriodPaymentView> rows) => rows.Count == 0 ? "" :
        $"{rows.Count} {(rows.Count == 1 ? "cobro" : "cobros")} · {TrayText.Format(new(rows.Sum(p => p.Row.Payment.Amount.Cents)))}";
}
