using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

/// <summary>Un pan en la cuadrícula de lotes: foto, nombre y cuántas piezas hay.</summary>
public sealed class BatchProductCard(ProductRow row, long? stock) : INotifyPropertyChanged
{
    private ProductPhotoVisual photo = new(0, null, null, "Sin foto", null, null);
    private long? stock = stock;
    public ProductRow Row => row;
    public Guid Id => row.Product.Id;
    public string Name => row.Product.Name;
    public bool HasPhoto => photo.HasPhoto;
    public bool IsFallback => photo.IsFallback;
    public System.Windows.Media.Imaging.BitmapSource? Photo => photo.Image;
    public long? Stock { get => stock; set { stock = value; PropertyChanged?.Invoke(this, new(null)); } }
    public string StockText => stock is { } s ? $"Hay {s} {(s == 1 ? "pieza" : "piezas")}" : "Sin lotes capturados";
    public void SetPhoto(ProductPhotoVisual value) { photo = value; PropertyChanged?.Invoke(this, new(null)); }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Producción por lotes (pedido del dueño, 22/10/2026): la misma cuadrícula de panes que Venta; al tocar uno se escribe cuántas
/// piezas salieron y se suman a su existencia. No descuenta insumos mientras las recetas estén «Por medir». La primera vez crea
/// (o reutiliza el PT-código existente) el artículo terminado del pan y lo vincula 1 pieza por pieza, para que la venta lo descuente.
/// </summary>
public sealed class BatchEntryViewModel(InventoryService inventory, CatalogService catalog, IProductPhotoUiGateway photos, Action<string, string?> log) : INotifyPropertyChanged
{
    private BatchProductCard? selected; private string pieces = ""; private CancellationTokenSource photoLoad = new();
    public IReadOnlyList<BatchProductCard> Products { get; private set; } = [];
    public BatchProductCard? Selected { get => selected; private set { selected = value; Notify(); } }
    public bool IsEntering => Selected is not null;
    public string Pieces { get => pieces; set { pieces = value; Notify(); } }
    public bool Busy { get; private set; }
    public string Message { get; private set; } = ""; public bool MessageIsSuccess { get; private set; }
    public bool CanCapture => inventory.Can("inventory.receive") && inventory.Can("inventory.configure");

    public async Task LoadAsync()
    {
        if (Busy) return; Busy = true; Notify();
        try
        {
            var products = new List<ProductRow>();
            for (int page = 0; ; page++) { var result = await Task.Run(() => catalog.Products(new(IncludeInactive: false, Page: page))); products.AddRange(result.Items); if (products.Count >= result.Total || result.Items.Count == 0) break; }
            var items = await Task.Run(FinishedItems);
            Products = products.OrderBy(p => p.Product.Name, StringComparer.CurrentCulture)
                .Select(p => new BatchProductCard(p, items.TryGetValue(Code(p.Product), out var row) && row.HasProductLink ? row.MainBalanceAtomic : null)).ToArray();
            Notify();
            photoLoad.Cancel(); photoLoad = new(); var token = photoLoad.Token;
            foreach (var card in Products) { if (token.IsCancellationRequested) break; card.SetPhoto(await photos.LoadAsync(card.Id, token)); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is AccessDeniedException or InventoryApplicationException or CatalogException)
        { Message = "No se pudieron cargar los panes para capturar lotes."; log("production.batch.load_failed", error.GetType().Name); }
        finally { Busy = false; Notify(); }
    }

    public void Choose(Guid productId) { Selected = Products.FirstOrDefault(p => p.Id == productId); Pieces = ""; Message = ""; Notify(); }
    public void Cancel() { Selected = null; Pieces = ""; Notify(); }

    public async Task SaveAsync()
    {
        if (Busy || Selected is not { } card) return;
        if (!int.TryParse(Pieces.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count is < 1 or > 10_000)
        { Message = "Escribe cuántas piezas salieron: un número entero de 1 a 10,000."; MessageIsSuccess = false; Notify(); return; }
        Busy = true; Message = ""; Notify();
        try
        {
            var stock = await Task.Run(() => Capture(card.Row, count));
            card.Stock = stock; Selected = null; Pieces = "";
            MessageIsSuccess = true; Message = $"Listo: se sumaron {count} {(count == 1 ? "pieza" : "piezas")} de {card.Name}. Ahora hay {stock}.";
            log("production.batch.captured", null);
        }
        catch (AccessDeniedException) { Message = "No tienes permiso para capturar lotes."; }
        catch (Exception error) when (error is InventoryApplicationException or CatalogException or ArgumentException)
        { Message = $"No se capturó el lote: {error.Message}"; log("production.batch.failed", error.GetType().Name); }
        finally { Busy = false; Notify(); }
    }

    private long Capture(ProductRow row, int count)
    {
        var product = row.Product; var code = Code(product);
        var item = FinishedItems().GetValueOrDefault(code);
        var itemId = item?.InventoryItemId ?? inventory.CreateInventoryItem(code, product.Name, InventoryItemKind.FinishedGood, InventoryDimension.Count).Id;
        if (item?.MainBalanceAtomic is null)
            inventory.RegisterOpeningBalance(new(Guid.NewGuid(), itemId, ProductionContract.Main, 0, InventoryUnits.Piece.Code));
        if (item is not { HasProductLink: true }) inventory.LinkProductToInventoryItem(product.Id, itemId, 1);
        // Con el vínculo listo se marca el pan para descontar en la venta (sin vínculo, marcarlo bloquearía el cobro).
        if (!product.TrackInventory)
            catalog.EditProduct(product.Id, product.CategoryId, product.Sku, product.Name, product.Description, product.SalePrice, true);
        inventory.ReceiveInventory(new(Guid.NewGuid(), itemId, ProductionContract.Main, count, InventoryUnits.Piece.Code, "LOTE", "Producción: piezas que salieron del lote"));
        return inventory.Detail(itemId).Balance?.QuantityAtomic ?? count;
    }

    private Dictionary<string, InventoryListRow> FinishedItems()
    {
        var rows = new List<InventoryListRow>();
        for (int page = 0; ; page++) { var result = inventory.List(new(Kind: InventoryItemKind.FinishedGood, Active: true, Page: page, PageSize: 100)); rows.AddRange(result.Items); if (rows.Count >= result.Total || result.Items.Count == 0) break; }
        return rows.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }
    /// <summary>El artículo terminado de un pan: «PT-» + su código (así se dieron de alta en la caja: PT-CM-001…).</summary>
    public static string Code(Product product) => "PT-" + (string.IsNullOrWhiteSpace(product.Sku) ? product.Id.ToString("N")[..12].ToUpperInvariant() : product.Sku.Trim().ToUpperInvariant());

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new(null));
}
