using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        public SalesReportData QuerySalesReport(DateTimeOffset fromUtc, DateTimeOffset untilUtc)
        {
            var range = new (string, object?)[] { ("$from", At(fromUtc)), ("$until", At(untilUtc)) };
            int saleCount; long gross;
            using (var summary = Command("SELECT count(*),COALESCE(SUM(total_cents),0) FROM sale WHERE confirmed_utc>=$from AND confirmed_utc<$until", range))
            using (var r = summary.ExecuteReader()) { r.Read(); saleCount = r.GetInt32(0); gross = r.GetInt64(1); }

            var products = new Dictionary<Guid, (string Name, long Qty, long Sold, long RQty, long Refunded)>();
            using (var sold = Command("SELECT i.product_id,i.product_name,SUM(i.quantity),SUM(i.line_total_cents) FROM sale_item i JOIN sale s ON s.id=i.sale_id"
                + " WHERE s.confirmed_utc>=$from AND s.confirmed_utc<$until GROUP BY i.product_id,i.product_name ORDER BY max(s.confirmed_utc)", range))
            using (var r = sold.ExecuteReader())
                while (r.Read())
                {
                    var id = Guid.Parse(r.GetString(0));
                    var current = products.GetValueOrDefault(id, (r.GetString(1), 0, 0, 0, 0));
                    products[id] = (r.GetString(1), current.Qty + r.GetInt64(2), current.Sold + r.GetInt64(3), 0, 0);
                }

            long refunded = 0;
            if (HasRefunds())
            {
                using (var total = Command("SELECT COALESCE(SUM(total_cents),0) FROM sale_refund WHERE refunded_utc>=$from AND refunded_utc<$until", range))
                    refunded = Convert.ToInt64(total.ExecuteScalar());
                using var back = Command("SELECT i.product_id,i.product_name,SUM(ri.quantity),SUM(ri.line_total_cents) FROM sale_refund_item ri"
                    + " JOIN sale_refund f ON f.id=ri.refund_id JOIN sale_item i ON i.sale_id=ri.sale_id AND i.position=ri.position"
                    + " WHERE f.refunded_utc>=$from AND f.refunded_utc<$until GROUP BY i.product_id,i.product_name", range);
                using var r = back.ExecuteReader();
                while (r.Read())
                {
                    var id = Guid.Parse(r.GetString(0));
                    var current = products.GetValueOrDefault(id, (r.GetString(1), 0, 0, 0, 0));
                    products[id] = (current.Name, current.Qty, current.Sold, current.RQty + r.GetInt64(2), current.Refunded + r.GetInt64(3));
                }
            }

            var cashiers = new List<CashierReportRow>();
            using (var byUser = Command("SELECT s.user_id,u.display_name,count(*),SUM(s.total_cents) FROM sale s JOIN app_user u ON u.id=s.user_id"
                + " WHERE s.confirmed_utc>=$from AND s.confirmed_utc<$until GROUP BY s.user_id,u.display_name ORDER BY SUM(s.total_cents) DESC,u.display_name", range))
            using (var r = byUser.ExecuteReader())
                while (r.Read()) cashiers.Add(new(Guid.Parse(r.GetString(0)), r.GetString(1), r.GetInt32(2), new(r.GetInt64(3))));

            var methods = new Dictionary<string, (int Sales, long Sold, long Refunded)>();
            using (var byMethod = Command("SELECT p.method,count(*),SUM(p.applied_cents) FROM sale s JOIN payment p ON p.sale_id=s.id"
                + " WHERE s.confirmed_utc>=$from AND s.confirmed_utc<$until GROUP BY p.method", range))
            using (var r = byMethod.ExecuteReader())
                while (r.Read()) methods[r.GetString(0)] = (r.GetInt32(1), r.GetInt64(2), 0);
            if (HasRefunds())
            {
                using var back = Command("SELECT p.method,SUM(f.total_cents) FROM sale_refund f JOIN payment p ON p.sale_id=f.sale_id"
                    + " WHERE f.refunded_utc>=$from AND f.refunded_utc<$until GROUP BY p.method", range);
                using var r = back.ExecuteReader();
                while (r.Read()) { var m = methods.GetValueOrDefault(r.GetString(0)); methods[r.GetString(0)] = (m.Sales, m.Sold, m.Refunded + r.GetInt64(1)); }
            }
            var paymentRows = methods.Select(m => new PaymentReportRow(Payment.Parse(m.Key), m.Value.Sales, new(m.Value.Sold), new(m.Value.Refunded)))
                .OrderBy(m => m.Method).ToList();

            var sessions = new List<(Guid Id, string By, DateTimeOffset Opened, DateTimeOffset? Closed, bool IsClosed, long Opening, long? Counted)>();
            using (var shifts = Command("SELECT c.id,u.display_name,c.opened_utc,c.closed_utc,c.state,c.opening_cents,COALESCE(c.counted_cents,n.counted_cents)"
                + " FROM cash_session c JOIN app_user u ON u.id=c.opened_by LEFT JOIN cash_count n ON n.session_id=c.id"
                + " WHERE c.opened_utc>=$from AND c.opened_utc<$until ORDER BY c.opened_utc,c.id", range))
            using (var r = shifts.ExecuteReader())
                while (r.Read())
                    sessions.Add((Guid.Parse(r.GetString(0)), r.GetString(1), Date(r, 2)!.Value, Date(r, 3), r.GetString(4) == "CLOSED", r.GetInt64(5), r.IsDBNull(6) ? null : r.GetInt64(6)));

            var sessionRows = sessions.Select(s =>
            {
                var cashSales = CashSalesTotal(s.Id); var refunds = CashRefundsTotal(s.Id);
                var expected = new Money(s.Opening) + cashSales - refunds;
                return new SessionReportRow(s.Id, s.By, s.Opened, s.Closed, s.IsClosed, new(s.Opening), cashSales, refunds,
                    s.Counted is { } c ? new Money(c) : null, s.Counted is { } k ? new Money(checked(k - expected.Cents)) : null);
            }).ToList();

            var productRows = products.Select(p => new ProductReportRow(p.Key, p.Value.Name, p.Value.Qty, new(p.Value.Sold), p.Value.RQty, new(p.Value.Refunded)))
                .OrderByDescending(p => p.Net.Cents).ThenBy(p => p.ProductName, StringComparer.CurrentCulture).ToList();
            return new(saleCount, new(gross), new(refunded), productRows, cashiers, sessionRows, paymentRows);
        }
    }
}
