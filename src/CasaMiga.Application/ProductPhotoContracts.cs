namespace CasaMiga.Application;

public sealed class ProductPhotoException(string message) : Exception(message);
public sealed record PhotoCrop(int X, int Y, int Edge);
public sealed record ProcessedPhoto(string Format, int Width, int Height, int Orientation, PhotoCrop Crop, byte[] Thumbnail);
public sealed record PhotoAsset(Guid Id, Guid ProductId, string Provenance, string OriginalName,
    string Format, string OriginalSha256, long OriginalBytes, int Width, int Height, int Orientation,
    PhotoCrop Crop, string ThumbnailSha256, long ThumbnailBytes, int ThumbnailEdge,
    DateTimeOffset CreatedAt, Guid CreatedBy, Guid TerminalId);
public sealed record PhotoSelection(Guid ProductId, long Revision, PhotoAsset? Photo);
public sealed record PhotoChange(Guid OperationId, Guid ProductId, long ExpectedRevision, string Kind,
    string Fingerprint, Guid Actor, Guid Terminal, PhotoSelection Result, Guid? PreviousPhotoId, string? PreviousHash);
public sealed record PhotoImport(Guid ProductId, long ExpectedRevision, Guid OperationId,
    string OriginalName, string Provenance, PhotoCrop Crop);
public sealed record PhotoOrphan(string RelativePath, DateTimeOffset LastWriteUtc);

// Codec owns no paths: streams/bytes only. Implemented by the Windows presentation adapter.
public interface IProductPhotoProcessor { ProcessedPhoto Process(byte[] original, PhotoCrop crop); }
public interface IProductPhotoTransaction
{
    ILocalStore Local { get; }
    PhotoSelection Current(Guid product);
    PhotoChange? Outcome(Guid operation);
    void Save(PhotoAsset asset);
    void Select(PhotoChange change, DateTimeOffset at);
    void Repair(PhotoAsset asset);
}
public interface IProductPhotoRepository
{
    T Run<T>(Func<IProductPhotoTransaction, T> operation);
    IReadOnlyList<PhotoAsset> Active();
}
public interface IProductPhotoFiles
{
    // Common gate for publication, reads, repair, cleanup and installation backup.
    IDisposable Acquire();
    void Publish(PhotoAsset asset, byte[] original, byte[] thumbnail);
    byte[] ReadOriginal(PhotoAsset asset);
    byte[] ReadThumbnail(PhotoAsset asset);
    void PublishThumbnail(PhotoAsset asset, byte[] bytes);
    void Retire(PhotoAsset asset);
    IReadOnlyList<PhotoOrphan> Orphans(IReadOnlyList<PhotoAsset> active, DateTimeOffset olderThan, bool remove);
}
