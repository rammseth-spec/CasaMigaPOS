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
    public IReadOnlyList<SyncOutboxEntry> Pending(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 500);
        using var c = database.Open(readOnly: true); using var q = c.CreateCommand();
        q.CommandText = "SELECT seq,entity,entity_id,change,enqueued_utc,attempts FROM sync_outbox WHERE sent_utc IS NULL ORDER BY seq LIMIT $limit";
        q.Parameters.AddWithValue("$limit", limit);
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

    /// <summary>Pendientes con el contenido completo de cada registro, leído en una sola transacción de lectura.</summary>
    public IReadOnlyList<SyncEnvelope> PendingEnvelopes(int limit)
    {
        var pending = Pending(limit);
        if (pending.Count == 0) return [];
        using var c = database.Open(readOnly: true); using var t = c.BeginTransaction(deferred: true);
        return pending.Select(entry => new SyncEnvelope(entry, Payload(c, t, entry))).ToList();
    }

    private static JsonObject Payload(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction t, SyncOutboxEntry entry)
    {
        JsonObject? One(string sql) => Rows(c, t, sql, entry.EntityId).FirstOrDefault();
        JsonArray Many(string sql) => new(Rows(c, t, sql, entry.EntityId).Select(r => (JsonNode)r).ToArray());
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
            _ => throw new InvalidDataException($"Entidad de sincronización desconocida: {entry.Entity}.")
        };
        return payload ?? throw new InvalidDataException($"No existe el registro {entry.Entity} {entry.EntityId} de la cola.");
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
