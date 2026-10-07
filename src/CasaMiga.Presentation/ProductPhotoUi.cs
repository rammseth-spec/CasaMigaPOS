using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using CasaMiga.Application;

namespace CasaMiga.Presentation;

public sealed record ProductPhotoVisual(long Revision, Guid? PhotoId, string? Hash, string Status,
    BitmapSource? Image, string? Error)
{
    public bool HasPhoto => Image is not null;
    public bool IsFallback => Image is null;
}

public interface IProductPhotoUiGateway
{
    Task<ProductPhotoVisual> LoadAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductPhotoVisual> ImportAsync(Guid productId, long revision, string originalName,
        string provenance, PhotoCrop crop, byte[] original, CancellationToken cancellationToken = default);
    Task<ProductPhotoVisual> RemoveAsync(Guid productId, long revision, CancellationToken cancellationToken = default);
}

public sealed class BoundedProductPhotoCache
{
    public const long DefaultBudget = 64L * 1024 * 1024;
    private sealed record Entry(Guid ProductId, BitmapSource Image, long Bytes);
    private readonly long budget;
    private readonly Dictionary<string, LinkedListNode<(string Key, Entry Value)>> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, Entry Value)> lru = new();
    private readonly object sync = new();
    private long used;
    public BoundedProductPhotoCache(long budgetBytes = DefaultBudget)
    {
        if (budgetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
        budget = budgetBytes;
    }
    public long BudgetBytes => budget;
    public long UsedBytes { get { lock (sync) return used; } }
    public int Count { get { lock (sync) return entries.Count; } }
    public bool TryGet(string key, out BitmapSource? image)
    {
        lock (sync)
        {
            if (!entries.TryGetValue(key, out var node)) { image = null; return false; }
            lru.Remove(node); lru.AddFirst(node); image = node.Value.Value.Image; return true;
        }
    }
    public void Add(Guid productId, string key, BitmapSource image)
    {
        var bytes = checked((long)image.PixelWidth * image.PixelHeight * 4);
        lock (sync)
        {
            if (entries.Remove(key, out var existing)) { lru.Remove(existing); used -= existing.Value.Value.Bytes; }
            if (bytes > budget) return;
            var node = lru.AddFirst((key, new(productId, image, bytes))); entries.Add(key, node); used += bytes;
            while (used > budget && lru.Last is { } tail)
            { entries.Remove(tail.Value.Key); used -= tail.Value.Value.Bytes; lru.RemoveLast(); }
        }
    }
    public void Invalidate(Guid productId)
    {
        lock (sync)
        {
            foreach (var item in lru.Where(item => item.Value.ProductId == productId).ToArray())
            {
                var found = entries[item.Key]; entries.Remove(item.Key); lru.Remove(found); used -= item.Value.Bytes;
            }
        }
    }
}

public sealed class ProductPhotoUiGateway(ProductPhotoService service, BoundedProductPhotoCache cache) : IProductPhotoUiGateway
{
    private readonly SemaphoreSlim reads = new(4, 4);
    public async Task<ProductPhotoVisual> LoadAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await reads.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() => Load(productId), cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is ProductPhotoException or IOException or UnauthorizedAccessException or InvalidDataException)
        { return new(0, null, null, "Foto no disponible", null, "No se pudo cargar la foto."); }
        finally { reads.Release(); }
    }
    private ProductPhotoVisual Load(Guid productId)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var selection = service.Current(productId);
            if (selection.Photo is not { } photo) return new(selection.Revision, null, null, "Sin foto", null, null);
            var key = $"{photo.Id:N}:{photo.ThumbnailSha256}";
            if (cache.TryGet(key, out var cached)) return new(selection.Revision, photo.Id, photo.ThumbnailSha256,
                photo.Provenance == "REAL" ? "Foto real" : "Foto provisional", cached, null);
            byte[]? bytes;
            try{bytes=service.ReadThumbnail(productId);}
            catch(Exception error) when(error is ProductPhotoException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return new(selection.Revision,photo.Id,photo.ThumbnailSha256,"Foto no disponible",null,
                    "El archivo falta o está dañado; puedes reemplazarlo o quitarlo.");
            }
            var verified = service.Current(productId);
            if (bytes is null || verified.Photo?.Id != photo.Id) continue;
            var image = Decode(bytes); cache.Add(productId, key, image);
            return new(verified.Revision, photo.Id, photo.ThumbnailSha256,
                photo.Provenance == "REAL" ? "Foto real" : "Foto provisional", image, null);
        }
        return new(0, null, null, "Foto actualizada", null, "La foto cambió mientras se cargaba.");
    }
    public static BitmapSource Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    }
    public async Task<ProductPhotoVisual> ImportAsync(Guid productId, long revision, string originalName,
        string provenance, PhotoCrop crop, byte[] original, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            using var input = new MemoryStream(original, writable: false);
            service.Import(new(productId, revision, Guid.NewGuid(), originalName, provenance, crop), input);
        }, cancellationToken);
        cache.Invalidate(productId); return await LoadAsync(productId, cancellationToken);
    }
    public async Task<ProductPhotoVisual> RemoveAsync(Guid productId, long revision, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => service.Remove(productId, revision, Guid.NewGuid()), cancellationToken);
        cache.Invalidate(productId); return await LoadAsync(productId, cancellationToken);
    }
    public static PhotoCrop CenteredCrop(int width,int height)
    {
        var edge=Math.Min(width,height);if(edge<256)throw new ProductPhotoException("La foto necesita al menos 256 píxeles por lado.");
        return new((width-edge)/2,(height-edge)/2,edge);
    }
}

public sealed class ProductCardViewModel(CasaMiga.Application.ProductRow row) : INotifyPropertyChanged
{
    private ProductPhotoVisual photo = new(0, null, null, "Sin foto", null, null);
    public Guid Id => row.Product.Id;
    public string Name => row.Name;
    public string Price => row.Price;
    public string CategoryName => row.CategoryName;
    public bool IsActive => row.Product.IsActive;
    public bool HasPhoto => photo.HasPhoto;
    public bool IsFallback => photo.IsFallback;
    public BitmapSource? Photo => photo.Image;
    public string PhotoStatus => photo.Status;
    public string? PhotoError => photo.Error;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void SetPhoto(ProductPhotoVisual value)
    {
        photo = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
    private int cartQuantity;
    /// <summary>Piezas de este pan en el ticket actual: la tarjeta del Punto de venta muestra la burbuja.</summary>
    public int CartQuantity
    {
        get => cartQuantity;
        set { if (cartQuantity == value) return; cartQuantity = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); }
    }
    public bool InCart => cartQuantity > 0;
}
