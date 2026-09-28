using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CasaMiga.Application;

public sealed class ProductPhotoService(IProductPhotoRepository repository, IProductPhotoFiles files,
    IProductPhotoProcessor processor, UserSession identity, IClock clock, Guid terminal)
{
    public const int MaxOriginalBytes = 25 * 1024 * 1024;
    private Guid Authorize(IProductPhotoTransaction tx, Guid product, string permission)
    {
        if (identity.Current is not {} actor || tx.Local.FindUser(actor.Id) is not { Active: true }
            || !tx.Local.HasPermission(actor.Id, permission)) throw new AccessDeniedException();
        if (tx.Local.FindTerminal(terminal) is null) throw new ProductPhotoException("Terminal no configurada.");
        if (product != Guid.Empty && tx.Local.FindProduct(product) is null) throw new ProductPhotoException("Producto inexistente.");
        return actor.Id;
    }
    public PhotoSelection Current(Guid product) => repository.Run(tx =>
    { Authorize(tx, product, "catalog.view"); return tx.Current(product); });
    // Resolve an uncertain commit without requiring the original source file again.
    public PhotoChange? Recover(Guid product, Guid operation)
    {
        CheckOperation(product, 0, operation);
        return repository.Run(tx =>
        {
            var actor = Authorize(tx, product, "products.edit");
            var outcome = tx.Outcome(operation);
            if (outcome is not null && (outcome.ProductId != product || outcome.Actor != actor || outcome.Terminal != terminal))
                throw new AccessDeniedException();
            return outcome;
        });
    }
    public byte[]? ReadThumbnail(Guid product)
    {
        using var held = files.Acquire();
        var selected = Current(product);
        return selected.Photo is {} photo ? files.ReadThumbnail(photo) : null;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Fingerprint(object value) => Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
    private static void CheckOperation(Guid product, long revision, Guid operation)
    {
        if (product == Guid.Empty || operation == Guid.Empty || revision < 0 || revision == long.MaxValue)
            throw new ArgumentException("Identidad o revisión de fotografía inválida.");
    }
    private static PhotoSelection? Retry(IProductPhotoTransaction tx, Guid operation, Guid product, Guid actor,
        Guid terminal, string fingerprint)
    {
        var prior = tx.Outcome(operation);
        if (prior is null) return null;
        if (prior.ProductId != product || prior.Actor != actor || prior.Terminal != terminal || prior.Fingerprint != fingerprint)
            throw new ProductPhotoException("OperationId ya utilizado por otra intención.");
        return prior.Result;
    }
    public PhotoSelection Import(PhotoImport request, Stream source)
    {
        CheckOperation(request.ProductId, request.ExpectedRevision, request.OperationId);
        if (request.Provenance is not ("REAL" or "PROVISIONAL")) throw new ArgumentException("Procedencia requerida.");
        var name = request.OriginalName;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.IndexOfAny(['/', '\\', ':']) >= 0 || name.Any(char.IsControl))
            throw new ArgumentException("Nombre informativo sin ruta requerido.");
        var actor = repository.Run(tx => Authorize(tx, request.ProductId, "products.edit"));
        using var input = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = source.Read(buffer)) > 0)
        {
            if (input.Length + count > MaxOriginalBytes) throw new ProductPhotoException("Máximo 25 MiB.");
            input.Write(buffer, 0, count);
        }
        var original = input.ToArray();
        var fingerprint = Fingerprint(new { request, hash = Hash(original) });
        using var held = files.Acquire();
        var retry = repository.Run(tx => { if (Authorize(tx, request.ProductId, "products.edit") != actor) throw new AccessDeniedException(); return Retry(tx, request.OperationId, request.ProductId, actor, terminal, fingerprint); });
        if (retry is not null) return retry;
        var active = repository.Active().Where(a => a.ProductId != request.ProductId).ToArray();
        if (active.Length >= 10000 || active.Sum(a => a.OriginalBytes + a.ThumbnailBytes) + original.LongLength + 2097152 > 5L * 1024 * 1024 * 1024)
            throw new ProductPhotoException("Límite de almacenamiento de fotografías alcanzado.");
        var processed = processor.Process(original, request.Crop);
        var asset = new PhotoAsset(request.OperationId, request.ProductId, request.Provenance, name,
            processed.Format, Hash(original), original.LongLength, processed.Width, processed.Height, processed.Orientation,
            processed.Crop, Hash(processed.Thumbnail), processed.Thumbnail.LongLength, Math.Min(512, processed.Crop.Edge), clock.UtcNow, actor, terminal);
        files.Publish(asset, original, processed.Thumbnail);
        PhotoAsset? previous = null;
        var result = repository.Run(tx =>
        {
            if (Authorize(tx, request.ProductId, "products.edit") != actor) throw new AccessDeniedException();
            var current = tx.Current(request.ProductId);
            if (current.Revision != request.ExpectedRevision) throw new ProductPhotoException("La foto cambió; actualiza antes de guardar.");
            previous = current.Photo;
            tx.Save(asset);
            var next = new PhotoSelection(request.ProductId, checked(current.Revision + 1), asset);
            tx.Select(new(request.OperationId, request.ProductId, request.ExpectedRevision,
                current.Photo is null ? "set" : "replace", fingerprint, actor, terminal, next, previous?.Id, previous?.OriginalSha256), clock.UtcNow);
            return next;
        });
        // Cleanup cannot turn an already committed operation into an apparent failure.
        if (previous is not null) TryRetire(previous);
        return result;
    }
    public PhotoSelection Remove(Guid product, long expectedRevision, Guid operation)
    {
        CheckOperation(product, expectedRevision, operation);
        using var held = files.Acquire();
        PhotoAsset? previous = null;
        var fingerprint = Fingerprint(new { product, expectedRevision, operation, kind = "remove" });
        var result = repository.Run(tx =>
        {
            var actor = Authorize(tx, product, "products.edit");
            var retry = Retry(tx, operation, product, actor, terminal, fingerprint);
            if (retry is not null) return retry;
            var current = tx.Current(product);
            if (current.Revision != expectedRevision) throw new ProductPhotoException("La foto cambió; actualiza antes de quitar.");
            previous = current.Photo;
            var next = new PhotoSelection(product, checked(current.Revision + 1), null);
            tx.Select(new(operation, product, expectedRevision, "remove", fingerprint, actor, terminal, next, previous?.Id, previous?.OriginalSha256), clock.UtcNow);
            return next;
        });
        if (previous is not null) TryRetire(previous);
        return result;
    }
    public PhotoSelection Repair(Guid product, long expectedRevision, Guid operation)
    {
        CheckOperation(product, expectedRevision, operation);
        using var held = files.Acquire();
        var fingerprint = Fingerprint(new { product, expectedRevision, operation, kind = "repair" });
        var actor = repository.Run(tx => Authorize(tx, product, "products.edit"));
        var retry = repository.Run(tx => { if (Authorize(tx, product, "products.edit") != actor) throw new AccessDeniedException(); return Retry(tx, operation, product, actor, terminal, fingerprint); });
        if (retry is not null) return retry;
        var current = Current(product);
        if (current.Revision != expectedRevision || current.Photo is not {} photo) throw new ProductPhotoException("Foto vigente requerida.");
        var regenerated = processor.Process(files.ReadOriginal(photo), photo.Crop);
        var repaired = photo with { ThumbnailSha256 = Hash(regenerated.Thumbnail), ThumbnailBytes = regenerated.Thumbnail.LongLength };
        files.PublishThumbnail(repaired, regenerated.Thumbnail);
        return repository.Run(tx =>
        {
            if (Authorize(tx, product, "products.edit") != actor) throw new AccessDeniedException();
            if (tx.Current(product).Revision != expectedRevision) throw new ProductPhotoException("La foto cambió.");
            tx.Repair(repaired);
            var next = new PhotoSelection(product, checked(expectedRevision + 1), repaired);
            tx.Select(new(operation, product, expectedRevision, "repair", fingerprint, actor, terminal, next, photo.Id, photo.OriginalSha256), clock.UtcNow);
            return next;
        });
    }
    public IReadOnlyList<PhotoOrphan> Reconcile(bool remove = false)
    {
        using var held = files.Acquire();
        repository.Run(tx => Authorize(tx, Guid.Empty, "products.edit"));
        return files.Orphans(repository.Active(), clock.UtcNow.AddDays(-1), remove);
    }
    private void TryRetire(PhotoAsset asset)
    {
        try { files.Retire(asset); }
        catch (IOException) { /* Retried by controlled reconciliation. */ }
        catch (UnauthorizedAccessException) { }
    }
}
