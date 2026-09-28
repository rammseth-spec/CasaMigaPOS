using System.Globalization;
using System.Text.Json;
using CasaMiga.Application;
using Microsoft.Data.Sqlite;

namespace CasaMiga.Infrastructure;

public sealed class SqliteProductPhotoStore(Database database) : IProductPhotoRepository
{
    public T Run<T>(Func<IProductPhotoTransaction, T> operation) => new SqliteLocalUnitOfWork(database)
        .RunTechnical((local, connection, transaction) => operation(new Transaction(local, connection, transaction)));
    public IReadOnlyList<PhotoAsset> Active() => Active(database);
    internal static IReadOnlyList<PhotoAsset> Active(Database db)
    {
        using var connection = db.Open(true);
        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='product_photo'";
        if (Convert.ToInt64(exists.ExecuteScalar()) == 0) return [];
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT a.* FROM product_photo_asset a JOIN product_photo p ON p.current_photo_id=a.id AND p.product_id=a.product_id";
        using var reader = cmd.ExecuteReader();
        var assets = new List<PhotoAsset>();
        while (reader.Read()) assets.Add(Read(reader));
        return assets;
    }
    private static PhotoAsset Read(SqliteDataReader r)
    {
        string Text(string name) => r.GetString(r.GetOrdinal(name));
        int Int(string name) => r.GetInt32(r.GetOrdinal(name));
        long Long(string name) => r.GetInt64(r.GetOrdinal(name));
        return new(Guid.Parse(r.GetString(r.GetOrdinal("id"))),
        Guid.Parse(r.GetString(r.GetOrdinal("product_id"))), Text("provenance"), Text("original_name"), Text("original_format"),
        Text("original_sha256"), Long("original_bytes"), Int("original_width"), Int("original_height"), Int("orientation"),
        new(Int("crop_x"), Int("crop_y"), Int("crop_edge")), Text("thumbnail_sha256"), Long("thumbnail_bytes"), Int("thumbnail_edge"),
        DateTimeOffset.Parse(Text("created_utc"), CultureInfo.InvariantCulture), Guid.Parse(Text("created_by")), Guid.Parse(Text("terminal_id")));
    }
    private sealed class Transaction(ILocalStore local, SqliteConnection connection, SqliteTransaction transaction) : IProductPhotoTransaction
    {
        public ILocalStore Local => local;
        private SqliteCommand Command(string sql, params (string Key, object? Value)[] parameters)
        {
            var cmd = connection.CreateCommand(); cmd.Transaction = transaction; cmd.CommandText = sql;
            foreach (var (key, value) in parameters) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
            return cmd;
        }
        public PhotoSelection Current(Guid product)
        {
            using var cmd = Command("SELECT revision,current_photo_id FROM product_photo WHERE product_id=$id", ("$id", product.ToString("D")));
            long revision; string? photoId;
            using (var r = cmd.ExecuteReader())
            { if (!r.Read()) return new(product, 0, null); revision = r.GetInt64(0); photoId = r.IsDBNull(1) ? null : r.GetString(1); }
            if (photoId is null) return new(product, revision, null);
            using var asset = Command("SELECT * FROM product_photo_asset WHERE id=$id", ("$id", photoId));
            using var reader = asset.ExecuteReader();
            if (!reader.Read()) throw new InvalidDataException("Asociación de foto inválida.");
            return new(product, revision, Read(reader));
        }
        public PhotoChange? Outcome(Guid operation)
        {
            using var cmd = Command("SELECT action,details_json FROM audit_entry WHERE id=$id", ("$id", operation.ToString("D")));
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            if (!r.GetString(0).StartsWith("product.photo.", StringComparison.Ordinal)) throw new ProductPhotoException("OperationId ya utilizado.");
            return JsonSerializer.Deserialize<PhotoChange>(r.GetString(1)) ?? throw new InvalidDataException("Auditoría inválida.");
        }
        public void Save(PhotoAsset a)
        {
            using var cmd = Command("""
                INSERT INTO product_photo_asset(id,product_id,layout_version,provenance,original_name,original_format,
                original_sha256,original_bytes,original_width,original_height,orientation,crop_x,crop_y,crop_edge,pipeline_version,
                thumbnail_sha256,thumbnail_bytes,thumbnail_edge,jpeg_quality,created_utc,created_by,terminal_id)
                VALUES($id,$product,1,$provenance,$name,$format,$hash,$bytes,$width,$height,$orientation,$x,$y,$edge,1,$thumb,$thumbBytes,$thumbEdge,85,$at,$actor,$terminal)
                """, ("$id", a.Id.ToString("D")), ("$product", a.ProductId.ToString("D")), ("$provenance", a.Provenance),
                ("$name", a.OriginalName), ("$format", a.Format), ("$hash", a.OriginalSha256), ("$bytes", a.OriginalBytes),
                ("$width", a.Width), ("$height", a.Height), ("$orientation", a.Orientation), ("$x", a.Crop.X), ("$y", a.Crop.Y), ("$edge", a.Crop.Edge),
                ("$thumb", a.ThumbnailSha256), ("$thumbBytes", a.ThumbnailBytes), ("$thumbEdge", a.ThumbnailEdge),
                ("$at", a.CreatedAt.ToUniversalTime().ToString("O")), ("$actor", a.CreatedBy.ToString("D")), ("$terminal", a.TerminalId.ToString("D")));
            cmd.ExecuteNonQuery();
        }
        public void Select(PhotoChange c, DateTimeOffset at)
        {
            using var cmd = Command("""
                INSERT INTO product_photo(product_id,current_photo_id,revision,last_operation_id,modified_utc,modified_by,terminal_id)
                VALUES($product,$photo,$revision,$operation,$at,$actor,$terminal)
                ON CONFLICT(product_id) DO UPDATE SET current_photo_id=excluded.current_photo_id,revision=excluded.revision,
                last_operation_id=excluded.last_operation_id,modified_utc=excluded.modified_utc,modified_by=excluded.modified_by,terminal_id=excluded.terminal_id
                """, ("$product", c.ProductId.ToString("D")), ("$photo", c.Result.Photo?.Id.ToString("D")), ("$revision", c.Result.Revision),
                ("$operation", c.OperationId.ToString("D")), ("$at", at.ToUniversalTime().ToString("O")), ("$actor", c.Actor.ToString("D")), ("$terminal", c.Terminal.ToString("D")));
            cmd.ExecuteNonQuery();
            Local.AddAudit(new(c.OperationId, c.Actor, c.Terminal, "product.photo." + c.Kind, c.ProductId, at, JsonSerializer.Serialize(c)));
        }
        public void Repair(PhotoAsset asset)
        {
            using var cmd = Command("UPDATE product_photo_asset SET thumbnail_sha256=$hash,thumbnail_bytes=$bytes WHERE id=$id",
                ("$hash", asset.ThumbnailSha256), ("$bytes", asset.ThumbnailBytes), ("$id", asset.Id.ToString("D")));
            if (cmd.ExecuteNonQuery() != 1) throw new InvalidDataException("Foto inexistente.");
        }
    }
}
