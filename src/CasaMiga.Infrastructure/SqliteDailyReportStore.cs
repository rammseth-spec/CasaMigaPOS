using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        public DailyReportSnapshot QueryDailyReport(DateTimeOffset fromUtc, DateTimeOffset untilUtc)
        {
            (string Name, object? Value)[] bounds =
            {
                ("$from", (object)At(fromUtc)),
                ("$until", (object)At(untilUtc))
            };

            long sales;
            int tickets;
            long pieces;
            using (var totals = Command(
                "SELECT COUNT(DISTINCT s.id),COALESCE(SUM(i.quantity),0) " +
                "FROM sale s JOIN payment p ON p.sale_id=s.id AND p.method='CASH' " +
                "LEFT JOIN sale_item i ON i.sale_id=s.id " +
                "WHERE s.confirmed_utc >= $from AND s.confirmed_utc < $until", bounds))
            using (var reader = totals.ExecuteReader())
            {
                if (!reader.Read()) throw new InvalidDataException("No se pudo calcular el reporte diario.");
                tickets = reader.GetInt32(0);
                pieces = reader.GetInt64(1);
            }
            using (var financial = Command(
                "SELECT COALESCE(SUM(p.applied_cents),0),COUNT(*) FROM sale s " +
                "JOIN payment p ON p.sale_id=s.id AND p.method='CASH' " +
                "WHERE s.confirmed_utc >= $from AND s.confirmed_utc < $until", bounds))
            using (var reader = financial.ExecuteReader())
            {
                if (!reader.Read()) throw new InvalidDataException("No se pudo calcular el total diario.");
                sales = reader.GetInt64(0);
                if (reader.GetInt32(1) != tickets) throw new InvalidDataException("El conteo financiero no es coherente.");
            }

            var products = new List<DailyProductSummary>();
            using (var top = Command(
                "SELECT i.product_name,SUM(i.quantity),SUM(i.line_total_cents) FROM sale s " +
                "JOIN payment p ON p.sale_id=s.id AND p.method='CASH' JOIN sale_item i ON i.sale_id=s.id " +
                "WHERE s.confirmed_utc >= $from AND s.confirmed_utc < $until " +
                "GROUP BY i.product_id,i.product_name ORDER BY SUM(i.quantity) DESC,SUM(i.line_total_cents) DESC,i.product_name COLLATE NOCASE ASC,i.product_id ASC LIMIT 5", bounds))
            using (var reader = top.ExecuteReader())
                while (reader.Read()) products.Add(new(reader.GetString(0), reader.GetInt64(1), new(reader.GetInt64(2))));

            var sessions = new List<DailyCashSessionSummary>();
            using (var cash = Command(
                "SELECT c.id,COALESCE(NULLIF(trim(t.name),''),'No disponible')," +
                "COALESCE(NULLIF(trim(o.display_name),''),'No disponible')," +
                "NULLIF(trim(COALESCE(x.display_name,'')),''),c.opened_utc,c.closed_utc,c.state,c.opening_cents," +
                "COALESCE((SELECT SUM(p.applied_cents) FROM sale s JOIN payment p ON p.sale_id=s.id AND p.method='CASH' WHERE s.cash_session_id=c.id),0)," +
                "c.expected_cents,c.counted_cents,c.difference_cents,c.discrepancy_note " +
                "FROM cash_session c LEFT JOIN terminal t ON t.id=c.terminal_id " +
                "LEFT JOIN app_user o ON o.id=c.opened_by LEFT JOIN app_user x ON x.id=c.closed_by " +
                "WHERE c.opened_utc < $until AND (c.closed_utc IS NULL OR c.closed_utc >= $from) " +
                "ORDER BY c.opened_utc ASC,c.id ASC", bounds))
            using (var reader = cash.ExecuteReader())
            {
                while (reader.Read())
                {
                    var isOpen = string.Equals(reader.GetString(6), "OPEN", StringComparison.Ordinal);
                    var opening = new Money(reader.GetInt64(7));
                    var cashSales = new Money(reader.GetInt64(8));
                    sessions.Add(new(
                        Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3), Date(reader, 4)!.Value, Date(reader, 5), isOpen,
                        opening, cashSales,
                        isOpen ? opening + cashSales : OptionalMoney(reader, 9),
                        OptionalMoney(reader, 10), OptionalMoney(reader, 11),
                        reader.IsDBNull(12) ? null : reader.GetString(12)));
                }
            }

            return new(new Money(sales), tickets, pieces, products.AsReadOnly(), sessions.AsReadOnly());
        }

        private static Money? OptionalMoney(Microsoft.Data.Sqlite.SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal)
            ? null
            : new Money(reader.GetInt64(ordinal));
    }
}
