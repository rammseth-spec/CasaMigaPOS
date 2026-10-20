using System.Globalization;
using System.Text.Json.Nodes;

namespace CasaMiga.Infrastructure;

public sealed record BranchIdentity(Guid Id, string Name);
public sealed record SyncOutboxEntry(long Sequence, string Entity, string EntityId, string Change, DateTimeOffset EnqueuedAt, int Attempts);
public sealed record SyncEnvelope(SyncOutboxEntry Entry, JsonObject Payload);

/// <summary>Cola local de sincronización (docs/SYNC_PLAN.md). Los triggers de la migración 018 la llenan; aquí sólo se lee y se marca.</summary>
public sealed class SqliteSyncOutbox(Database database)
{
    public BranchIdentity Branch()
    {
        using var c = database.Open(readOnly: true); using var q = c.CreateCommand();
        q.CommandText = "SELECT id,name FROM branch";
        using var r = q.ExecuteReader();
        if (!r.Read()) throw new InvalidDataException("La instalación no tiene sucursal.");
        return new(Guid.Parse(r.GetString(0)), r.GetString(1));
    }

    public long PendingCount()
    {
        using var c = database.Open(readOnly: true); using var q = c.CreateCommand();
        q.CommandText = "SELECT count(*) FROM sync_outbox WHERE sent_utc IS NULL";
        return Convert.ToInt64(q.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Pendientes en orden de llegada, que respeta dependencias (caja antes que sus ventas).</summary>
    public IReadOnlyList<SyncOutboxEntry> Pending(int limit, long after = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 500);
        using var c = database.Open(readOnly: true); using var q = c.CreateCommand();
        q.CommandText = "SELECT seq,entity,entity_id,change,enqueued_utc,attempts FROM sync_outbox WHERE sent_utc IS NULL AND seq>$after ORDER BY seq LIMIT $limit";
        q.Parameters.AddWithValue("$limit", limit); q.Parameters.AddWithValue("$after", after);
        using var r = q.ExecuteReader();
        var entries = new List<SyncOutboxEntry>();
        while (r.Read())
            entries.Add(new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), DateTimeOffset.Parse(r.GetString(4), CultureInfo.InvariantCulture), r.GetInt32(5)));
        return entries;
    }

    /// <summary>Marca como enviados sólo los confirmados por la nube; todo o nada.</summary>
    public void MarkSent(IReadOnlyCollection<long> sequences, DateTimeOffset at)
    {
        if (sequences.Count == 0) return;
        using var c = database.Open(); using var t = c.BeginTransaction(); using var q = c.CreateCommand();
        q.Transaction = t;
        q.CommandText = "UPDATE sync_outbox SET sent_utc=$at,last_error=NULL WHERE seq=$seq AND sent_utc IS NULL";
        var seq = q.Parameters.Add("$seq", Microsoft.Data.Sqlite.SqliteType.Integer);
        q.Parameters.AddWithValue("$at", at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        foreach (var value in sequences)
        {
            seq.Value = value;
            if (q.ExecuteNonQuery() != 1) throw new InvalidOperationException($"La entrada {value} no estaba pendiente.");
        }
        t.Commit();
    }

    public void RenameBranch(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 100) throw new ArgumentException("El nombre de la sucursal debe tener de 1 a 100 caracteres.");
        using var c = database.Open(); using var q = c.CreateCommand();
        q.CommandText = "UPDATE branch SET name=$name"; q.Parameters.AddWithValue("$name", name);
        q.ExecuteNonQuery();
    }

    /// <summary>Pendientes con el contenido completo de cada registro, leído en una sola transacción de lectura.
    /// Se corta antes de <paramref name="maxBytes"/> (las fotos pesan) pero siempre lleva al menos uno.</summary>
    public IReadOnlyList<SyncEnvelope> PendingEnvelopes(int limit, int maxBytes = 1_500_000, long after = 0)
    {
        var pending = Pending(limit, after);
        if (pending.Count == 0) return [];
        using var c = database.Open(readOnly: true); using var t = c.BeginTransaction(deferred: true);
        var envelopes = new List<SyncEnvelope>(); var bytes = 0;
        foreach (var entry in pending)
        {
            var payload = Payload(c, t, entry, dataDirectory);
            var size = payload.ToJsonString().Length;
            if (envelopes.Count > 0 && bytes + size > maxBytes) break;
            envelopes.Add(new(entry, payload)); bytes += size;
        }
        return envelopes;
    }

    private readonly string dataDirectory = Path.GetDirectoryName(database.FilePath)!;

    private static JsonObject Payload(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction t, SyncOutboxEntry entry, string dataDirectory)
    {
        JsonObject? One(string sql) => Rows(c, t, sql, entry.EntityId).FirstOrDefault();
        JsonArray Many(string sql) => new(Rows(c, t, sql, entry.EntityId).Select(r => (JsonNode)r).ToArray());
        // Una baja (sólo precios de charola hoy) viaja como marca; la nube la aplica al estado más reciente.
        JsonObject Deleted() => new() { ["deleted"] = true, ["id"] = entry.EntityId };
        if (entry.Change == "delete") return Deleted();
        var payload = entry.Entity switch
        {
            "cash_session" => One("SELECT * FROM cash_session WHERE id=$id"),
            "cash_count" => One("SELECT * FROM cash_count WHERE session_id=$id"),
            "audit_entry" => One("SELECT * FROM audit_entry WHERE id=$id"),
            "sale" => One("SELECT * FROM sale WHERE id=$id") is { } sale
                ? new JsonObject { ["sale"] = sale, ["items"] = Many("SELECT * FROM sale_item WHERE sale_id=$id ORDER BY position"), ["payment"] = One("SELECT * FROM payment WHERE sale_id=$id") }
                : null,
            "sale_refund" => One("SELECT * FROM sale_refund WHERE id=$id") is { } refund
                ? new JsonObject { ["refund"] = refund, ["items"] = Many("SELECT * FROM sale_refund_item WHERE refund_id=$id ORDER BY position") }
                : null,
            // Con el estado del consumo automático por ventas (uno por caja), para Inventario en la web.
            "branch" => One("SELECT id,name,created_utc FROM branch WHERE id=$id") is { } branch
                ? With(branch, "sale_consumption", One("SELECT state,version,updated_utc,updated_by FROM inventory_sale_consumption_config WHERE id=1 AND $id IS NOT NULL"))
                : null,
            // El hash (PBKDF2-SHA512, 210 000 iteraciones) permite entrar a la versión web con la misma contraseña de la caja.
            "app_user" => One("SELECT id,username,display_name,password_hash,role_name,active,must_change_password,created_utc FROM app_user WHERE id=$id"),
            "role_permission" => new JsonObject { ["rows"] = Many("SELECT role_name,permission_name FROM role_permission WHERE $id='all' ORDER BY role_name,permission_name") },
            "category" => One("SELECT * FROM category WHERE id=$id"),
            "product" => One("SELECT * FROM product WHERE id=$id") is { } product
                ? new JsonObject { ["product"] = product, ["inventoryLinks"] = Many("SELECT * FROM product_inventory_link WHERE product_id=$id ORDER BY created_utc,id") }
                : null,
            "product_photo" => One("SELECT * FROM product_photo WHERE product_id=$id") is { } photo
                ? PhotoPayload(photo, One("SELECT a.* FROM product_photo p JOIN product_photo_asset a ON a.id=p.current_photo_id WHERE p.product_id=$id"), dataDirectory)
                : null,
            "cash_movement" => One("SELECT * FROM cash_movement WHERE id=$id"),
            "consignment_store" => One("SELECT * FROM consignment_store WHERE id=$id") is { } store
                ? new JsonObject { ["store"] = store, ["template"] = Many("SELECT * FROM consignment_template_item WHERE store_id=$id ORDER BY product_id") }
                : null,
            // Un alta que se borró antes de enviarse viaja como baja: la cola nunca se atora por un registro que ya no existe.
            "consignment_price" => One("SELECT * FROM consignment_price WHERE product_id=$id") ?? Deleted(),
            "consignment_delivery" => One("SELECT * FROM consignment_delivery WHERE id=$id") is { } delivery
                ? new JsonObject
                {
                    ["delivery"] = delivery, ["items"] = Many("SELECT * FROM consignment_delivery_item WHERE delivery_id=$id ORDER BY position"),
                    ["settlement"] = One("SELECT * FROM consignment_settlement WHERE delivery_id=$id"),
                    ["settlementItems"] = Many("SELECT * FROM consignment_settlement_item WHERE delivery_id=$id ORDER BY position"),
                    ["void"] = One("SELECT * FROM consignment_void WHERE delivery_id=$id"),
                }
                : null,
            "consignment_payment" => One("SELECT * FROM consignment_payment WHERE id=$id"),
            "measurement_unit" => new JsonObject { ["rows"] = Many("SELECT * FROM measurement_unit WHERE $id='all' ORDER BY code") },
            "inventory_location" => One("SELECT * FROM inventory_location WHERE id=$id"),
            "inventory_item" => One("SELECT * FROM inventory_item WHERE id=$id") is { } item
                ? new JsonObject
                {
                    ["item"] = item, ["units"] = Many("SELECT * FROM inventory_item_unit WHERE item_id=$id ORDER BY code"),
                    ["balances"] = Many("SELECT * FROM inventory_balance WHERE item_id=$id ORDER BY location_id"),
                    ["cost"] = One("SELECT cost_cents,per_atomic,unit_label,updated_utc FROM inventory_item_cost WHERE item_id=$id"),
                }
                : null,
            "inventory_operation" => One("SELECT id,kind,state,actor_id,terminal_id,reason_code,note,external_reference,created_utc,confirmed_utc,sale_evaluation_id,production_batch_id,production_batch_status FROM inventory_operation WHERE id=$id") is { } operation
                ? new JsonObject { ["operation"] = operation, ["movements"] = Many("SELECT * FROM inventory_movement WHERE operation_id=$id ORDER BY sequence") }
                : null,
            "formula" => One("SELECT * FROM formula WHERE id=$id") is { } formula
                ? new JsonObject
                {
                    ["formula"] = formula, ["versions"] = Many("SELECT * FROM formula_version WHERE formula_id=$id ORDER BY version"),
                    ["ingredients"] = Many("SELECT i.* FROM formula_ingredient i JOIN formula_version v ON v.id=i.version_id WHERE v.formula_id=$id ORDER BY v.version,i.sequence"),
                    ["groups"] = Many("SELECT g.* FROM formula_alternative_group g JOIN formula_version v ON v.id=g.version_id WHERE v.formula_id=$id ORDER BY v.version,g.sequence"),
                    ["options"] = Many("SELECT o.* FROM formula_alternative_option o JOIN formula_version v ON v.id=o.version_id WHERE v.formula_id=$id ORDER BY v.version,o.group_id,o.sequence"),
                    // Otras preparaciones que usa cada versión (masa dulce dentro de la masa enriquecida).
                    ["preparations"] = Many("SELECT p.* FROM formula_preparation p JOIN formula_version v ON v.id=p.version_id WHERE v.formula_id=$id ORDER BY v.version,p.sequence"),
                }
                : null,
            "production_profile" => One("SELECT * FROM production_profile WHERE id=$id") is { } profile
                ? new JsonObject
                {
                    ["profile"] = profile, ["versions"] = Many("SELECT * FROM production_profile_version WHERE profile_id=$id ORDER BY version"),
                    ["directIngredients"] = Many("SELECT d.* FROM profile_direct_ingredient d JOIN production_profile_version v ON v.id=d.profile_version_id WHERE v.profile_id=$id ORDER BY v.version,d.sequence"),
                    ["formulaRequirements"] = Many("SELECT r.* FROM profile_formula_requirement r JOIN production_profile_version v ON v.id=r.profile_version_id WHERE v.profile_id=$id ORDER BY v.version,r.sequence"),
                }
                : null,
            "production_batch" => One("SELECT id,operation_id,status,created_by,terminal_id,created_utc,confirmed_utc,notes,consumption_operation_id,output_operation_id FROM production_batch WHERE id=$id") is { } batch
                ? new JsonObject
                {
                    ["batch"] = batch, ["outputs"] = Many("SELECT * FROM production_output_plan WHERE batch_id=$id ORDER BY sequence"),
                    ["inputs"] = Many("SELECT * FROM production_input_snapshot WHERE batch_id=$id ORDER BY sequence"),
                    // Preparaciones del plan, para el detalle de la producción en la web.
                    ["formulas"] = Many("SELECT formula_version_id,formula_name,dimension,required_atomic FROM production_formula_snapshot WHERE batch_id=$id ORDER BY formula_name,formula_version_id"),
                }
                : null,
            _ => throw new InvalidDataException($"Entidad de sincronización desconocida: {entry.Entity}.")
        };
        return payload ?? throw new InvalidDataException($"No existe el registro {entry.Entity} {entry.EntityId} de la cola.");
    }

    private static JsonObject With(JsonObject row, string name, JsonNode? value) { row[name] = value; return row; }

    /// <summary>Foto vigente del producto con su miniatura (JPEG de 512 px) en base64; sin foto, sólo la marca.</summary>
    private static JsonObject PhotoPayload(JsonObject photo, JsonObject? asset, string dataDirectory)
    {
        var payload = new JsonObject { ["photo"] = photo, ["asset"] = null, ["thumbnail"] = null };
        if (asset is null) return payload;
        payload["asset"] = new JsonObject
        {
            ["id"] = asset["id"]?.DeepClone(), ["provenance"] = asset["provenance"]?.DeepClone(), ["thumbnail_sha256"] = asset["thumbnail_sha256"]?.DeepClone(),
            ["thumbnail_edge"] = asset["thumbnail_edge"]?.DeepClone(), ["created_utc"] = asset["created_utc"]?.DeepClone(),
        };
        var id = Guid.Parse((string)asset["id"]!); var sha = (string)asset["thumbnail_sha256"]!;
        var path = Path.Combine(dataDirectory, "product-images", "v1", id.ToString("N"), $"pos-v1-{sha}.jpg");
        // Una miniatura ausente o alterada no detiene la sincronización: la web muestra el producto sin foto.
        if (File.Exists(path))
        {
            var bytes = File.ReadAllBytes(path);
            if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) == sha) payload["thumbnail"] = Convert.ToBase64String(bytes);
        }
        return payload;
    }

    private static List<JsonObject> Rows(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction t, string sql, string id)
    {
        using var q = c.CreateCommand(); q.Transaction = t; q.CommandText = sql; q.Parameters.AddWithValue("$id", id);
        using var r = q.ExecuteReader();
        var rows = new List<JsonObject>();
        while (r.Read())
        {
            var row = new JsonObject();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i) switch
                {
                    long n => JsonValue.Create(n), double d => JsonValue.Create(d), string s => JsonValue.Create(s),
                    byte[] b => JsonValue.Create(Convert.ToBase64String(b)), var other => JsonValue.Create(Convert.ToString(other, CultureInfo.InvariantCulture))
                };
            rows.Add(row);
        }
        return rows;
    }

    public void MarkFailed(long sequence, string error)
    {
        using var c = database.Open(); using var q = c.CreateCommand();
        q.CommandText = "UPDATE sync_outbox SET attempts=attempts+1,last_error=$error WHERE seq=$seq AND sent_utc IS NULL";
        q.Parameters.AddWithValue("$seq", sequence);
        q.Parameters.AddWithValue("$error", error.Length > 500 ? error[..500] : error);
        q.ExecuteNonQuery();
    }
}
