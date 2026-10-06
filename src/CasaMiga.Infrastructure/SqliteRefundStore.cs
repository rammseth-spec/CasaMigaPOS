using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        private bool? hasRefunds;
        // Bases previas a la migración 019 (upgrades/restauraciones en curso) no tienen devoluciones.
        private bool HasRefunds()
        {
            if (hasRefunds is null)
            {
                using var probe = Command("SELECT count(*) FROM sqlite_master WHERE type='table' AND name='sale_refund'");
                hasRefunds = Convert.ToInt64(probe.ExecuteScalar()) == 1;
            }
            return hasRefunds.Value;
        }

        public Money CashRefundsTotal(Guid session)
        {
            if (!HasRefunds()) return new(0);
            using var query = Command("SELECT COALESCE(SUM(r.total_cents),0) FROM sale_refund r JOIN payment p ON p.sale_id=r.sale_id WHERE r.cash_session_id=$cash AND p.method='CASH'", ("$cash", Id(session)));
            return new(Convert.ToInt64(query.ExecuteScalar()));
        }

        public void AddRefund(SaleRefundRecord refund)
        {
            foreach (var item in refund.Items)
            {
                using var row = Command("INSERT INTO sale_refund_item VALUES($refund,$sale,$position,$quantity,$total)",
                    ("$refund", Id(refund.Id)), ("$sale", Id(refund.SaleId)), ("$position", item.Position), ("$quantity", item.Quantity), ("$total", item.LineTotal.Cents));
                row.ExecuteNonQuery();
            }
            using var header = Command("INSERT INTO sale_refund VALUES($id,$operation,$sale,$kind,$reason,$user,$terminal,$cash,$at,$total)",
                ("$id", Id(refund.Id)), ("$operation", Id(refund.OperationId)), ("$sale", Id(refund.SaleId)),
                ("$kind", refund.Kind == RefundKind.Cancellation ? "CANCELLATION" : "RETURN"), ("$reason", refund.Reason),
                ("$user", Id(refund.UserId)), ("$terminal", Id(refund.TerminalId)), ("$cash", Id(refund.CashSessionId)),
                ("$at", At(refund.RefundedAt)), ("$total", refund.Total.Cents));
            header.ExecuteNonQuery();
        }

        public IReadOnlyList<SaleRefundRecord> ListRefunds(Guid saleId) => !HasRefunds() ? [] : ReadRefunds("r.sale_id=$key", Id(saleId));

        public SaleRefundRecord? FindRefundByOperation(Guid operation) => !HasRefunds() ? null : ReadRefunds("r.operation_id=$key", Id(operation)).SingleOrDefault();

        private List<SaleRefundRecord> ReadRefunds(string predicate, string key)
        {
            var headers = new List<(Guid Id, Guid Operation, Guid Sale, RefundKind Kind, string Reason, Guid User, string UserName, Guid Terminal, Guid Cash, DateTimeOffset At, long Total)>();
            using (var query = Command("SELECT r.id,r.operation_id,r.sale_id,r.kind,r.reason,r.user_id,u.display_name,r.terminal_id,r.cash_session_id,r.refunded_utc,r.total_cents"
                + " FROM sale_refund r JOIN app_user u ON u.id=r.user_id WHERE " + predicate + " ORDER BY r.refunded_utc,r.id", ("$key", key)))
            using (var r = query.ExecuteReader())
                while (r.Read())
                    headers.Add((Guid.Parse(r.GetString(0)), Guid.Parse(r.GetString(1)), Guid.Parse(r.GetString(2)),
                        r.GetString(3) == "CANCELLATION" ? RefundKind.Cancellation : RefundKind.Return, r.GetString(4),
                        Guid.Parse(r.GetString(5)), r.GetString(6), Guid.Parse(r.GetString(7)), Guid.Parse(r.GetString(8)), Date(r, 9)!.Value, r.GetInt64(10)));
            var result = new List<SaleRefundRecord>();
            foreach (var h in headers)
            {
                using var rows = Command("SELECT ri.position,i.product_name,i.unit_price_cents,ri.quantity,ri.line_total_cents FROM sale_refund_item ri"
                    + " JOIN sale_item i ON i.sale_id=ri.sale_id AND i.position=ri.position WHERE ri.refund_id=$id ORDER BY ri.position", ("$id", Id(h.Id)));
                using var r = rows.ExecuteReader();
                var items = new List<SaleRefundItem>();
                while (r.Read()) items.Add(new(r.GetInt32(0), r.GetString(1), new(r.GetInt64(2)), r.GetInt32(3), new(r.GetInt64(4))));
                if (items.Sum(i => i.LineTotal.Cents) != h.Total) throw new InvalidDataException("La devolución histórica no es coherente.");
                result.Add(new(h.Id, h.Operation, h.Sale, h.Kind, h.Reason, h.User, h.UserName, h.Terminal, h.Cash, h.At, new(h.Total), items));
            }
            return result;
        }
    }
}
