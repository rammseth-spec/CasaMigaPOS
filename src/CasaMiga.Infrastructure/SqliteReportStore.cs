using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        // D5 (migración 029): los reportes suman lo de esta caja y lo bajado de las cajas web de la sucursal (tablas web_*). Cada consulta corre
        // por separado sobre las tablas locales y las web, con sus índices (migración 030), y se suma al final: unir las vistas report_* en un
        // JOIN hacía que SQLite recorriera todas las ventas (docs/VOLUME.md). Las devoluciones web sólo son de ventas web.
        private bool? hasWebSales;
        private bool HasWebSales()
        {
            if (hasWebSales is null)
            {
                using var query = Command("SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name='web_sale')");
                hasWebSales = Convert.ToInt64(query.ExecuteScalar()) == 1;
            }
            return hasWebSales.Value;
        }

        /// <summary>La consulta sobre las tablas locales y, si existen, también sobre las web, unidas con UNION ALL.</summary>
        private string Both(Func<string, string> query) => HasWebSales() ? $"{query("")} UNION ALL {query("web_")}" : query("");
        private string NotCancelled(string prefix, string sale) => HasRefunds()
            ? $" AND NOT EXISTS(SELECT 1 FROM {prefix}sale_refund f WHERE f.sale_id={sale}.id AND f.kind='CANCELLATION')" : "";

        public SalesReportData QuerySalesReport(DateTimeOffset fromUtc, DateTimeOffset untilUtc)
        {
            var range = new (string, object?)[] { ("$from", At(fromUtc)), ("$until", At(untilUtc)) };
            int saleCount; long gross;
            using (var summary = Command($"SELECT COALESCE(SUM(c),0),COALESCE(SUM(t),0) FROM ({Both(w => $"SELECT count(*) c,COALESCE(SUM(total_cents),0) t FROM {w}sale WHERE confirmed_utc>=$from AND confirmed_utc<$until")})", range))
            using (var r = summary.ExecuteReader()) { r.Read(); saleCount = r.GetInt32(0); gross = r.GetInt64(1); }

            var products = new Dictionary<Guid, (string Name, long Qty, long Sold, long RQty, long Refunded)>();
            using (var sold = Command("SELECT product_id,product_name,SUM(quantity),SUM(line_total_cents) FROM ("
                + Both(w => $"SELECT i.product_id,i.product_name,i.quantity,i.line_total_cents,s.confirmed_utc FROM {w}sale s JOIN {w}sale_item i ON i.sale_id=s.id WHERE s.confirmed_utc>=$from AND s.confirmed_utc<$until")
                + ") GROUP BY product_id,product_name ORDER BY max(confirmed_utc)", range))
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
                using (var total = Command($"SELECT COALESCE(SUM(t),0) FROM ({Both(w => $"SELECT COALESCE(SUM(total_cents),0) t FROM {w}sale_refund WHERE refunded_utc>=$from AND refunded_utc<$until")})", range))
                    refunded = Convert.ToInt64(total.ExecuteScalar());
                using var back = Command("SELECT product_id,product_name,SUM(quantity),SUM(line_total_cents) FROM ("
                    + Both(w => $"SELECT i.product_id,i.product_name,ri.quantity,ri.line_total_cents FROM {w}sale_refund f JOIN {w}sale_refund_item ri ON ri.refund_id=f.id"
                        + $" JOIN {w}sale_item i ON i.sale_id=ri.sale_id AND i.position=ri.position WHERE f.refunded_utc>=$from AND f.refunded_utc<$until")
                    + ") GROUP BY product_id,product_name", range);
                using var r = back.ExecuteReader();
                while (r.Read())
                {
                    var id = Guid.Parse(r.GetString(0));
                    var current = products.GetValueOrDefault(id, (r.GetString(1), 0, 0, 0, 0));
                    products[id] = (current.Name, current.Qty, current.Sold, current.RQty + r.GetInt64(2), current.Refunded + r.GetInt64(3));
                }
            }

            // Quien vendió en la web y todavía no llega a esta caja aparece igual (sin nombre local).
            var cashiers = new List<CashierReportRow>();
            using (var byUser = Command("SELECT x.user_id,COALESCE(u.display_name,'Usuario de la web'),SUM(x.c),SUM(x.t) FROM ("
                + Both(w => $"SELECT user_id,count(*) c,SUM(total_cents) t FROM {w}sale WHERE confirmed_utc>=$from AND confirmed_utc<$until GROUP BY user_id")
                + ") x LEFT JOIN app_user u ON u.id=x.user_id GROUP BY x.user_id,u.display_name ORDER BY SUM(x.t) DESC,u.display_name", range))
            using (var r = byUser.ExecuteReader())
                while (r.Read()) cashiers.Add(new(Guid.Parse(r.GetString(0)), r.GetString(1), r.GetInt32(2), new(r.GetInt64(3))));

            var methods = new Dictionary<string, (int Sales, long Sold, long Refunded)>();
            using (var byMethod = Command("SELECT method,SUM(c),SUM(t) FROM ("
                + Both(w => $"SELECT p.method,count(*) c,SUM(p.applied_cents) t FROM {w}sale s JOIN {w}payment p ON p.sale_id=s.id WHERE s.confirmed_utc>=$from AND s.confirmed_utc<$until GROUP BY p.method")
                + ") GROUP BY method", range))
            using (var r = byMethod.ExecuteReader())
                while (r.Read()) methods[r.GetString(0)] = (r.GetInt32(1), r.GetInt64(2), 0);
            if (HasRefunds())
            {
                using var back = Command("SELECT method,SUM(t) FROM ("
                    + Both(w => $"SELECT p.method,SUM(f.total_cents) t FROM {w}sale_refund f JOIN {w}payment p ON p.sale_id=f.sale_id WHERE f.refunded_utc>=$from AND f.refunded_utc<$until GROUP BY p.method")
                    + ") GROUP BY method", range);
                using var r = back.ExecuteReader();
                while (r.Read()) { var m = methods.GetValueOrDefault(r.GetString(0)); methods[r.GetString(0)] = (m.Sales, m.Sold, m.Refunded + r.GetInt64(1)); }
            }
            var paymentRows = methods.Select(m => new PaymentReportRow(Payment.Parse(m.Key), m.Value.Sales, new(m.Value.Sold), new(m.Value.Refunded)))
                .OrderBy(m => m.Method).ToList();

            var sessions = new List<(Guid Id, string By, DateTimeOffset Opened, DateTimeOffset? Closed, bool IsClosed, long Opening, long? Counted, bool Web)>();
            var shiftSql = "SELECT c.id,u.display_name,c.opened_utc,c.closed_utc,c.state,c.opening_cents,COALESCE(c.counted_cents,n.counted_cents),0"
                + " FROM cash_session c JOIN app_user u ON u.id=c.opened_by LEFT JOIN cash_count n ON n.session_id=c.id WHERE c.opened_utc>=$from AND c.opened_utc<$until";
            if (HasWebSales())
                shiftSql += " UNION ALL SELECT w.id,COALESCE(u.display_name,'Usuario de la web'),w.opened_utc,w.closed_utc,w.state,w.opening_cents,COALESCE(w.counted_cents,k.counted_cents),1"
                    + " FROM web_cash_session w LEFT JOIN app_user u ON u.id=w.opened_by LEFT JOIN web_cash_count k ON k.session_id=w.id WHERE w.opened_utc>=$from AND w.opened_utc<$until";
            using (var shifts = Command(shiftSql + " ORDER BY 3,1", range))
            using (var r = shifts.ExecuteReader())
                while (r.Read())
                    sessions.Add((Guid.Parse(r.GetString(0)), r.GetString(1), Date(r, 2)!.Value, Date(r, 3), r.GetString(4) == "CLOSED", r.GetInt64(5), r.IsDBNull(6) ? null : r.GetInt64(6), r.GetInt64(7) == 1));

            var sessionRows = sessions.Select(s =>
            {
                var (cashSales, refunds, movements) = s.Web ? WebCashTotals(s.Id) : (CashSalesTotal(s.Id), CashRefundsTotal(s.Id), CashMovementTotals(s.Id).Net);
                var expected = new Money(s.Opening) + cashSales - refunds + movements;
                return new SessionReportRow(s.Id, s.By, s.Opened, s.Closed, s.IsClosed, new(s.Opening), cashSales, refunds,
                    s.Counted is { } c ? new Money(c) : null, s.Counted is { } k ? new Money(checked(k - expected.Cents)) : null, movements);
            }).ToList();

            var productRows = products.Select(p => new ProductReportRow(p.Key, p.Value.Name, p.Value.Qty, new(p.Value.Sold), p.Value.RQty, new(p.Value.Refunded)))
                .OrderByDescending(p => p.Net.Cents).ThenBy(p => p.ProductName, StringComparer.CurrentCulture).ToList();
            return new(saleCount, new(gross), new(refunded), productRows, cashiers, sessionRows, paymentRows);
        }

        /// <summary>Lo mismo que CashSalesTotal, CashRefundsTotal y CashMovementTotals, para un turno de una caja web.</summary>
        private (Money CashSales, Money Refunds, Money Movements) WebCashTotals(Guid session)
        {
            using var query = Command("SELECT (SELECT COALESCE(SUM(p.applied_cents),0) FROM web_sale s JOIN web_payment p ON p.sale_id=s.id WHERE s.cash_session_id=$cash AND p.method='CASH'),"
                + "(SELECT COALESCE(SUM(r.total_cents),0) FROM web_sale_refund r JOIN web_payment p ON p.sale_id=r.sale_id WHERE r.cash_session_id=$cash AND p.method='CASH'),"
                + "(SELECT COALESCE(SUM(CASE kind WHEN 'DEPOSIT' THEN amount_cents ELSE -amount_cents END),0) FROM web_cash_movement WHERE cash_session_id=$cash)", ("$cash", Id(session)));
            using var r = query.ExecuteReader();
            r.Read();
            return (new(r.GetInt64(0)), new(r.GetInt64(1)), new(r.GetInt64(2)));
        }

        // Sólo productos activos: el pronóstico es para lo que hoy se sigue horneando.
        public IReadOnlyList<SaleLineFact> QuerySaleLines(DateTimeOffset fromUtc, DateTimeOffset untilUtc)
        {
            using var query = Command("SELECT x.confirmed_utc,x.product_id,p.name,x.quantity FROM ("
                + Both(w => $"SELECT s.confirmed_utc,i.product_id,i.quantity FROM {w}sale s JOIN {w}sale_item i ON i.sale_id=s.id WHERE s.confirmed_utc>=$from AND s.confirmed_utc<$until{NotCancelled(w, "s")}")
                + ") x JOIN product p ON p.id=x.product_id WHERE p.active=1", ("$from", At(fromUtc)), ("$until", At(untilUtc)));
            using var r = query.ExecuteReader();
            var lines = new List<SaleLineFact>();
            while (r.Read()) lines.Add(new(Date(r, 0)!.Value, Guid.Parse(r.GetString(1)), r.GetString(2), r.GetInt32(3)));
            return lines;
        }

        public IReadOnlyList<SaleFact> QuerySaleFacts(DateTimeOffset fromUtc, DateTimeOffset untilUtc)
        {
            using var query = Command(Both(w => $"SELECT s.confirmed_utc,s.total_cents FROM {w}sale s WHERE s.confirmed_utc>=$from AND s.confirmed_utc<$until{NotCancelled(w, "s")}"),
                ("$from", At(fromUtc)), ("$until", At(untilUtc)));
            using var r = query.ExecuteReader();
            var sales = new List<SaleFact>();
            while (r.Read()) sales.Add(new(Date(r, 0)!.Value, r.GetInt64(1)));
            return sales;
        }

        public IReadOnlyList<RefundFact> QueryRefundFacts(DateTimeOffset fromUtc, DateTimeOffset untilUtc)
        {
            if (!HasRefunds()) return [];
            using var query = Command("SELECT x.reason,x.total_cents,COALESCE(u.display_name,'Usuario de la web') FROM ("
                + Both(w => $"SELECT f.reason,f.total_cents,s.user_id,f.refunded_utc FROM {w}sale_refund f JOIN {w}sale s ON s.id=f.sale_id WHERE f.refunded_utc>=$from AND f.refunded_utc<$until")
                + ") x LEFT JOIN app_user u ON u.id=x.user_id ORDER BY x.refunded_utc", ("$from", At(fromUtc)), ("$until", At(untilUtc)));
            using var r = query.ExecuteReader();
            var refunds = new List<RefundFact>();
            while (r.Read()) refunds.Add(new(r.GetString(0), r.GetInt64(1), r.GetString(2)));
            return refunds;
        }
    }
}
