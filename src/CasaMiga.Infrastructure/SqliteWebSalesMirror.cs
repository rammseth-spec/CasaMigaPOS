using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace CasaMiga.Infrastructure;

/// <summary>Lo que registró una caja web (venta, devolución, turno, conteo o movimiento), con el formato que sube la caja de escritorio.</summary>
public sealed record WebSaleEvent(Guid Terminal, string TerminalName, long Sequence, string Entity, string EntityId, string Change, JsonObject Payload);

/// <summary>
/// D5: copia local de sólo lectura de lo que registraron las cajas web de la sucursal (migración 029), para que los reportes de la caja
/// las cuenten igual que la web. Lleva un cursor por caja web; aplicar dos veces lo mismo no cambia nada. Nunca se sube ni se borra.
/// </summary>
public sealed class SqliteWebSalesMirror(Database database)
{
    public IReadOnlyDictionary<Guid, long> Cursors()
    {
        using var c = database.Open(readOnly: true); using var q = c.CreateCommand();
        q.CommandText = "SELECT id,seq FROM web_terminal";
        using var r = q.ExecuteReader();
        var cursors = new Dictionary<Guid, long>();
        while (r.Read()) cursors[Guid.Parse(r.GetString(0))] = r.GetInt64(1);
        return cursors;
    }

    /// <summary>Guarda los registros en una sola transacción y avanza el cursor de cada caja web. Devuelve cuántos registros eran válidos.</summary>
    public int Apply(IReadOnlyList<WebSaleEvent> events)
    {
        if (events.Count == 0) return 0;
        using var c = database.Open(); using var tx = c.BeginTransaction();
        var applied = 0;
        foreach (var e in events.OrderBy(e => e.Terminal).ThenBy(e => e.Sequence))
        {
            Run(c, tx, "INSERT INTO web_terminal(id,name,seq) VALUES($id,$name,$seq) ON CONFLICT(id) DO UPDATE SET name=excluded.name,seq=max(seq,excluded.seq)",
                ("$id", e.Terminal.ToString("D")), ("$name", e.TerminalName), ("$seq", e.Sequence));
            // Un registro mal formado no detiene a los demás ni queda a medias (la nube sólo guarda lo que ella misma validó).
            Run(c, tx, "SAVEPOINT web_event");
            try { if (Store(c, tx, e)) applied++; Run(c, tx, "RELEASE web_event"); }
            catch (Exception error) when (error is InvalidOperationException or FormatException or KeyNotFoundException or ArgumentException
                or SqliteException { SqliteErrorCode: 19 })
            { Run(c, tx, "ROLLBACK TO web_event"); Run(c, tx, "RELEASE web_event"); }
        }
        tx.Commit();
        return applied;
    }

    private static bool Store(SqliteConnection c, SqliteTransaction tx, WebSaleEvent e)
    {
        var terminal = e.Terminal.ToString("D");
        var p = e.Payload;
        switch (e.Entity)
        {
            case "sale":
            {
                var sale = Obj(p, "sale"); var payment = Obj(p, "payment"); var id = Text(sale, "id");
                Run(c, tx, "INSERT OR IGNORE INTO web_sale(id,terminal_id,user_id,cash_session_id,sequence,confirmed_utc,subtotal_cents,total_cents) VALUES($id,$t,$u,$s,$n,$at,$sub,$total)",
                    ("$id", id), ("$t", terminal), ("$u", Text(sale, "user_id")), ("$s", Text(sale, "cash_session_id")), ("$n", Number(sale, "sequence")),
                    ("$at", Utc(sale, "confirmed_utc")), ("$sub", Number(sale, "subtotal_cents")), ("$total", Number(sale, "total_cents")));
                foreach (var item in Items(p))
                    Run(c, tx, "INSERT OR IGNORE INTO web_sale_item(sale_id,position,product_id,product_name,unit_price_cents,quantity,line_total_cents) VALUES($id,$pos,$product,$name,$price,$qty,$line)",
                        ("$id", id), ("$pos", Number(item, "position")), ("$product", Text(item, "product_id")), ("$name", Text(item, "product_name")),
                        ("$price", Number(item, "unit_price_cents")), ("$qty", Number(item, "quantity")), ("$line", Number(item, "line_total_cents")));
                Run(c, tx, "INSERT OR IGNORE INTO web_payment(sale_id,method,applied_cents,received_cents,change_cents,reference) VALUES($id,$m,$applied,$received,$change,$ref)",
                    ("$id", id), ("$m", Optional(payment, "method") ?? "CASH"), ("$applied", Number(payment, "applied_cents")), ("$received", Number(payment, "received_cents")),
                    ("$change", Number(payment, "change_cents")), ("$ref", Optional(payment, "reference")));
                return true;
            }
            case "sale_refund":
            {
                var refund = Obj(p, "refund"); var id = Text(refund, "id");
                Run(c, tx, "INSERT OR IGNORE INTO web_sale_refund(id,sale_id,kind,reason,user_id,terminal_id,cash_session_id,refunded_utc,total_cents) VALUES($id,$sale,$kind,$reason,$u,$t,$s,$at,$total)",
                    ("$id", id), ("$sale", Text(refund, "sale_id")), ("$kind", Text(refund, "kind")), ("$reason", Text(refund, "reason")), ("$u", Text(refund, "user_id")),
                    ("$t", terminal), ("$s", Text(refund, "cash_session_id")), ("$at", Utc(refund, "refunded_utc")), ("$total", Number(refund, "total_cents")));
                foreach (var item in Items(p))
                    Run(c, tx, "INSERT OR IGNORE INTO web_sale_refund_item(refund_id,sale_id,position,quantity,line_total_cents) VALUES($id,$sale,$pos,$qty,$line)",
                        ("$id", id), ("$sale", Text(item, "sale_id")), ("$pos", Number(item, "position")), ("$qty", Number(item, "quantity")), ("$line", Number(item, "line_total_cents")));
                return true;
            }
            case "cash_session":
                // La apertura llega como «insert» y el cierre como «update» del mismo turno; un turno cerrado ya no cambia.
                Run(c, tx, @"INSERT INTO web_cash_session(id,terminal_id,opened_by,opened_utc,opening_cents,state,closed_utc,counted_cents) VALUES($id,$t,$by,$at,$opening,$state,$closed,$counted)
                    ON CONFLICT(id) DO UPDATE SET state=excluded.state,closed_utc=excluded.closed_utc,counted_cents=excluded.counted_cents WHERE web_cash_session.state='OPEN'",
                    ("$id", Text(p, "id")), ("$t", terminal), ("$by", Text(p, "opened_by")), ("$at", Utc(p, "opened_utc")), ("$opening", Number(p, "opening_cents")),
                    ("$state", Text(p, "state")), ("$closed", Optional(p, "closed_utc") is null ? null : Utc(p, "closed_utc")), ("$counted", OptionalNumber(p, "counted_cents")));
                return true;
            case "cash_count":
                Run(c, tx, "INSERT OR IGNORE INTO web_cash_count(session_id,counted_cents) VALUES($id,$counted)", ("$id", Text(p, "session_id")), ("$counted", Number(p, "counted_cents")));
                return true;
            case "cash_movement":
                Run(c, tx, "INSERT OR IGNORE INTO web_cash_movement(id,cash_session_id,kind,amount_cents,occurred_utc) VALUES($id,$s,$kind,$amount,$at)",
                    ("$id", Text(p, "id")), ("$s", Text(p, "cash_session_id")), ("$kind", Text(p, "kind")), ("$amount", Number(p, "amount_cents")), ("$at", Utc(p, "occurred_utc")));
                return true;
            default:
                return false;
        }
    }

    private static void Run(SqliteConnection c, SqliteTransaction tx, string sql, params (string Name, object? Value)[] values)
    {
        using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = sql;
        foreach (var (name, value) in values) q.Parameters.AddWithValue(name, value ?? DBNull.Value);
        q.ExecuteNonQuery();
    }

    private static JsonObject Obj(JsonObject p, string name) => p[name] as JsonObject ?? throw new KeyNotFoundException(name);
    private static IEnumerable<JsonObject> Items(JsonObject p) => (p["items"] as JsonArray ?? throw new KeyNotFoundException("items")).OfType<JsonObject>();
    private static string Text(JsonObject p, string name) => Optional(p, name) is { Length: > 0 } text ? text : throw new KeyNotFoundException(name);
    private static string? Optional(JsonObject p, string name) => p[name] switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonNode n => n.ToJsonString(),
    };
    /// <summary>Mismo formato que guarda la caja ("O" en UTC), para que los periodos comparen igual.</summary>
    private static string Utc(JsonObject p, string name) =>
        DateTimeOffset.Parse(Text(p, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static long Number(JsonObject p, string name) => OptionalNumber(p, name) ?? throw new KeyNotFoundException(name);
    private static long? OptionalNumber(JsonObject p, string name) => p[name] switch
    {
        null => null,
        JsonValue v when v.TryGetValue<long>(out var l) => l,
        JsonValue v when v.TryGetValue<int>(out var i) => i,
        JsonValue v when v.TryGetValue<double>(out var d) && d == Math.Floor(d) => (long)d,
        JsonValue v when v.TryGetValue<string>(out var s) => long.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture),
        _ => throw new FormatException(name),
    };
}
