using System.Globalization;

namespace CasaMiga.Infrastructure;

public sealed record BranchIdentity(Guid Id, string Name);
public sealed record SyncOutboxEntry(long Sequence, string Entity, string EntityId, string Change, DateTimeOffset EnqueuedAt, int Attempts);

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

    public void MarkFailed(long sequence, string error)
    {
        using var c = database.Open(); using var q = c.CreateCommand();
        q.CommandText = "UPDATE sync_outbox SET attempts=attempts+1,last_error=$error WHERE seq=$seq AND sent_utc IS NULL";
        q.Parameters.AddWithValue("$seq", sequence);
        q.Parameters.AddWithValue("$error", error.Length > 500 ? error[..500] : error);
        q.ExecuteNonQuery();
    }
}
