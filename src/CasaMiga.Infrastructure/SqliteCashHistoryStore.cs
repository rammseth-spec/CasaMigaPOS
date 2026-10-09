using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;

namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        private const string ClosureSelect = "SELECT c.id,COALESCE(NULLIF(trim(t.name),''),'No disponible'),c.opened_utc," +
            "COALESCE(NULLIF(trim(o.display_name),''),'No disponible'),c.opening_cents,c.closed_utc," +
            "COALESCE(NULLIF(trim(u.display_name),''),'No disponible'),COALESCE(NULLIF(trim(n.display_name),''),'No disponible')," +
            "c.expected_cents,c.counted_cents,c.difference_cents,c.discrepancy_note FROM cash_session c " +
            "LEFT JOIN terminal t ON t.id=c.terminal_id LEFT JOIN app_user o ON o.id=c.opened_by " +
            "LEFT JOIN app_user u ON u.id=c.closed_by LEFT JOIN cash_count cc ON cc.session_id=c.id " +
            "LEFT JOIN app_user n ON n.id=cc.counted_by WHERE c.state='CLOSED' AND c.terminal_id=$terminal ";

        private static CashClosureHistoryRow Closure(SqliteDataReader r) => new(Guid.Parse(r.GetString(0)),r.GetString(1),Date(r,2)!.Value,
            r.GetString(3),new Money(r.GetInt64(4)),Date(r,5)!.Value,r.GetString(6),r.GetString(7),
            r.IsDBNull(8)?null:new Money(r.GetInt64(8)),r.IsDBNull(9)?null:new Money(r.GetInt64(9)),
            r.IsDBNull(10)?null:new Money(r.GetInt64(10)),r.IsDBNull(11)?null:r.GetString(11));

        public CashClosureHistoryPage QueryCashClosures(Guid terminal, int page)
        {
            const int size=25;
            using var count=Command("SELECT count(*) FROM cash_session WHERE state='CLOSED' AND terminal_id=$terminal",("$terminal",Id(terminal)));
            var total=Convert.ToInt32(count.ExecuteScalar());
            using var query=Command(ClosureSelect+"ORDER BY julianday(c.closed_utc) DESC,c.id DESC LIMIT $limit OFFSET $offset",
                ("$terminal",Id(terminal)),("$limit",size),("$offset",checked(page*size)));
            using var r=query.ExecuteReader();var rows=new List<CashClosureHistoryRow>();
            while(r.Read())rows.Add(Closure(r));
            return new(rows.AsReadOnly(),total,page,size);
        }
        public CashClosureHistoryRow? FindCashClosure(Guid terminal, Guid id)
        {
            using var query=Command(ClosureSelect+"AND c.id=$id",("$terminal",Id(terminal)),("$id",Id(id)));
            using var r=query.ExecuteReader();return r.Read()?Closure(r):null;
        }
    }
}
