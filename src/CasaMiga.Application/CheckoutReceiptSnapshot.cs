using CasaMiga.Domain;
namespace CasaMiga.Application;

public interface ICheckoutReceiptSnapshot
{
    void Capture(ILocalStore store, Sale sale);
}

// Only captures data on the caller's transaction; never renders or prints.
public sealed class CheckoutReceiptSnapshot(IPrintingSettingsProvider settings, TimeZoneInfo timeZone) : ICheckoutReceiptSnapshot
{
    public void Capture(ILocalStore store,Sale sale)
    {
        var config=settings.Read();
        if(!config.Available)return; // Preserve installations without configured printing.
        var detail=store.FindSaleDetail(sale.Id) ?? throw new InvalidOperationException("Venta no disponible.");
        var document=ReceiptDocument.FromSale(detail,config.Settings!.Business,timeZone);
        var json=ReceiptDocumentCodec.Serialize(document);
        store.AddReceiptSnapshot(new(sale.Id,document.Version,json,ReceiptDocumentCodec.Hash(json),sale.ConfirmedAt));
    }
}
