using CasaMiga.Domain;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        public long NextSaleSequence(Guid terminal)
        {
            using var query = Command("SELECT last_value FROM sale_sequence WHERE terminal_id=$terminal", ("$terminal", Id(terminal)));
            var next = checked(Convert.ToInt64(query.ExecuteScalar() ?? 0L) + 1);
            using var update = Command("INSERT INTO sale_sequence VALUES($terminal,$value) ON CONFLICT(terminal_id) DO UPDATE SET last_value=excluded.last_value",
                ("$terminal", Id(terminal)), ("$value", next));
            update.ExecuteNonQuery(); return next;
        }
        public void AddSale(Sale sale)
        {
            int position = 0;
            foreach (var item in sale.Items)
            {
                using var row = Command("INSERT INTO sale_item VALUES($sale,$position,$product,$name,$price,$quantity,$total)",
                    ("$sale", Id(sale.Id)), ("$position", ++position), ("$product", Id(item.ProductId)), ("$name", item.ProductName),
                    ("$price", item.UnitPrice.Cents), ("$quantity", item.Quantity), ("$total", item.LineTotal.Cents));
                row.ExecuteNonQuery();
            }
            using var payment = Command("INSERT INTO payment VALUES($id,$sale,'CASH',$applied,$received,$change)",
                ("$id", Id(sale.Payment.Id)), ("$sale", Id(sale.Id)), ("$applied", sale.Payment.Applied.Cents),
                ("$received", sale.Payment.Received.Cents), ("$change", sale.Payment.Change.Cents));
            payment.ExecuteNonQuery();
            using var header = Command("INSERT INTO sale VALUES($id,$operation,$user,$terminal,$cash,$sequence,$at,$subtotal,$total)",
                ("$id", Id(sale.Id)), ("$operation", Id(sale.OperationId)), ("$user", Id(sale.UserId)), ("$terminal", Id(sale.TerminalId)),
                ("$cash", Id(sale.CashSessionId)), ("$sequence", sale.Sequence), ("$at", At(sale.ConfirmedAt)),
                ("$subtotal", sale.Subtotal.Cents), ("$total", sale.Total.Cents));
            header.ExecuteNonQuery();
        }
        public Sale? FindSaleByOperation(Guid operation)
        {
            using var query = Command("SELECT s.id,s.user_id,s.terminal_id,s.cash_session_id,s.sequence,s.confirmed_utc,p.id,p.received_cents FROM sale s JOIN payment p ON p.sale_id=s.id WHERE s.operation_id=$operation", ("$operation", Id(operation)));
            Guid id, user, terminal, cash, payment; long sequence, received; DateTimeOffset at;
            using (var r = query.ExecuteReader())
            {
                if (!r.Read()) return null;
                id=Guid.Parse(r.GetString(0));user=Guid.Parse(r.GetString(1));terminal=Guid.Parse(r.GetString(2));cash=Guid.Parse(r.GetString(3));
                sequence=r.GetInt64(4);at=Date(r,5)!.Value;payment=Guid.Parse(r.GetString(6));received=r.GetInt64(7);
            }
            using var rows = Command("SELECT product_id,product_name,unit_price_cents,quantity FROM sale_item WHERE sale_id=$id ORDER BY position", ("$id", Id(id)));
            using var reader = rows.ExecuteReader(); var items = new List<SaleItem>();
            while (reader.Read()) items.Add(new(Guid.Parse(reader.GetString(0)),reader.GetString(1),new(reader.GetInt64(2)),reader.GetInt32(3)));
            return new(id,operation,user,terminal,cash,sequence,at,items,payment,new(received));
        }
        public Money CashSalesTotal(Guid session)
        {
            using var query = Command("SELECT COALESCE(SUM(p.applied_cents),0) FROM payment p JOIN sale s ON s.id=p.sale_id WHERE s.cash_session_id=$cash AND p.method='CASH'", ("$cash", Id(session)));
            return new(Convert.ToInt64(query.ExecuteScalar()));
        }
    }
}
