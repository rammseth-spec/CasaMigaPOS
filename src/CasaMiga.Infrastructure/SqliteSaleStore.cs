using CasaMiga.Application;
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
        private bool? hasPaymentReference;
        // Bases previas a la migración 020 sólo admiten efectivo y no tienen referencia.
        private bool HasPaymentReference()
        {
            if (hasPaymentReference is null)
            {
                using var probe = Command("SELECT count(*) FROM pragma_table_info('payment') WHERE name='reference'");
                hasPaymentReference = Convert.ToInt64(probe.ExecuteScalar()) == 1;
            }
            return hasPaymentReference.Value;
        }
        private string PaymentReferenceColumn => HasPaymentReference() ? "p.reference" : "NULL";
        public void AddSale(Sale sale)
        {
            var contract = HasInventoryContract();
            int position = 0;
            foreach (var item in sale.Items)
            {
                using var row = Command("INSERT INTO sale_item VALUES($sale,$position,$product,$name,$price,$quantity,$total)",
                    ("$sale", Id(sale.Id)), ("$position", ++position), ("$product", Id(item.ProductId)), ("$name", item.ProductName),
                    ("$price", item.UnitPrice.Cents), ("$quantity", item.Quantity), ("$total", item.LineTotal.Cents));
                row.ExecuteNonQuery();
            }
            using var payment = Command("INSERT INTO payment(id,sale_id,method,applied_cents,received_cents,change_cents"
                + (HasPaymentReference() ? ",reference) VALUES($id,$sale,$method,$applied,$received,$change,$reference)" : ") VALUES($id,$sale,$method,$applied,$received,$change)"),
                ("$id", Id(sale.Payment.Id)), ("$sale", Id(sale.Id)), ("$method", sale.Payment.Method), ("$applied", sale.Payment.Applied.Cents),
                ("$received", sale.Payment.Received.Cents), ("$change", sale.Payment.Change.Cents), ("$reference", sale.Payment.Reference));
            payment.ExecuteNonQuery();
            using var header = Command("INSERT INTO sale(id,operation_id,user_id,terminal_id,cash_session_id,sequence,confirmed_utc,subtotal_cents,total_cents"
                + (contract ? ",inventory_evaluation_contract_version,inventory_evaluation_id" : "")
                + ") VALUES($id,$operation,$user,$terminal,$cash,$sequence,$at,$subtotal,$total"
                + (contract ? ",1,$operation)" : ")"),
                ("$id", Id(sale.Id)), ("$operation", Id(sale.OperationId)), ("$user", Id(sale.UserId)), ("$terminal", Id(sale.TerminalId)),
                ("$cash", Id(sale.CashSessionId)), ("$sequence", sale.Sequence), ("$at", At(sale.ConfirmedAt)),
                ("$subtotal", sale.Subtotal.Cents), ("$total", sale.Total.Cents));
            header.ExecuteNonQuery();
            if(contract) CompleteInventory(sale);
        }
        public Sale? FindSaleByOperation(Guid operation)
        {
            using var query = Command("SELECT s.id,s.user_id,s.terminal_id,s.cash_session_id,s.sequence,s.confirmed_utc,p.id,p.received_cents,p.method," + PaymentReferenceColumn + " FROM sale s JOIN payment p ON p.sale_id=s.id WHERE s.operation_id=$operation", ("$operation", Id(operation)));
            Guid id, user, terminal, cash, payment; long sequence, received; DateTimeOffset at; PaymentMethod method; string? reference;
            using (var r = query.ExecuteReader())
            {
                if (!r.Read()) return null;
                id=Guid.Parse(r.GetString(0));user=Guid.Parse(r.GetString(1));terminal=Guid.Parse(r.GetString(2));cash=Guid.Parse(r.GetString(3));
                sequence=r.GetInt64(4);at=Date(r,5)!.Value;payment=Guid.Parse(r.GetString(6));received=r.GetInt64(7);
                method=Payment.Parse(r.GetString(8));reference=r.IsDBNull(9)?null:r.GetString(9);
            }
            using var rows = Command("SELECT product_id,product_name,unit_price_cents,quantity FROM sale_item WHERE sale_id=$id ORDER BY position", ("$id", Id(id)));
            using var reader = rows.ExecuteReader(); var items = new List<SaleItem>();
            while (reader.Read()) items.Add(new(Guid.Parse(reader.GetString(0)),reader.GetString(1),new(reader.GetInt64(2)),reader.GetInt32(3)));
            return new(id,operation,user,terminal,cash,sequence,at,items,payment,new(received),method,reference);
        }
        public Money CashSalesTotal(Guid session)
        {
            using var query = Command("SELECT COALESCE(SUM(p.applied_cents),0) FROM payment p JOIN sale s ON s.id=p.sale_id WHERE s.cash_session_id=$cash AND p.method='CASH'", ("$cash", Id(session)));
            return new(Convert.ToInt64(query.ExecuteScalar()));
        }

        public SalesHistoryPage QuerySales(SalesHistoryFilter filter)
        {
            const int pageSize = 50;
            string predicate;
            (string Name, object? Value)[] parameters;
            if (filter.TerminalId is { } terminal && filter.Sequence is { } sequence)
            {
                predicate = " WHERE s.terminal_id=$terminal AND s.sequence=$sequence";
                parameters = [("$terminal", Id(terminal)), ("$sequence", sequence)];
            }
            else if (filter.Sequence is { } visible)
            {
                // Folio visible: el consecutivo; la clave de caja la filtra el servicio.
                predicate = " WHERE s.sequence=$sequence";
                parameters = [("$sequence", visible)];
            }
            else
            {
                var parts = new List<string>();
                var values = new List<(string, object?)>();
                if (filter.FromUtc is { } from) { parts.Add("s.confirmed_utc >= $from"); values.Add(("$from", At(from))); }
                if (filter.UntilUtc is { } until) { parts.Add("s.confirmed_utc < $until"); values.Add(("$until", At(until))); }
                predicate = parts.Count == 0 ? "" : " WHERE " + string.Join(" AND ", parts);
                parameters = values.ToArray();
            }

            using var count = Command("SELECT count(*) FROM sale s" + predicate, parameters);
            var total = Convert.ToInt32(count.ExecuteScalar());
            using var page = Command("SELECT s.id,s.terminal_id,s.sequence,s.confirmed_utc,s.total_cents,p.method,s.user_id,u.display_name,t.name,s.cash_session_id,"
                + (HasRefunds() ? "(SELECT COALESCE(SUM(r.total_cents),0) FROM sale_refund r WHERE r.sale_id=s.id)" : "0")
                + " FROM sale s JOIN payment p ON p.sale_id=s.id JOIN app_user u ON u.id=s.user_id JOIN terminal t ON t.id=s.terminal_id"
                + predicate + " ORDER BY s.confirmed_utc DESC,s.id DESC LIMIT $limit OFFSET $offset", parameters);
            page.Parameters.AddWithValue("$limit", pageSize);
            page.Parameters.AddWithValue("$offset", checked(filter.Page * pageSize));
            using var reader = page.ExecuteReader();
            var items = new List<SalesHistoryRow>();
            while (reader.Read())
            {
                var saleId = Guid.Parse(reader.GetString(0));
                var terminalId = Guid.Parse(reader.GetString(1));
                var rowSequence = reader.GetInt64(2);
                items.Add(new(saleId, $"{terminalId:N}-{rowSequence:D8}", Date(reader, 3)!.Value,
                    new(reader.GetInt64(4)), reader.GetString(5), Guid.Parse(reader.GetString(6)), reader.GetString(7),
                    terminalId, reader.GetString(8), Guid.Parse(reader.GetString(9)), new(reader.GetInt64(10))));
            }
            return new(items, total, filter.Page, pageSize);
        }

        public SalesHistoryDetail? FindSaleDetail(Guid saleId)
        {
            using var query = Command("SELECT s.operation_id,s.user_id,u.display_name,s.terminal_id,t.name,s.cash_session_id,s.sequence,s.confirmed_utc,p.id,p.method,p.applied_cents,p.received_cents,p.change_cents,s.subtotal_cents,s.total_cents," + PaymentReferenceColumn
                + " FROM sale s JOIN payment p ON p.sale_id=s.id JOIN app_user u ON u.id=s.user_id JOIN terminal t ON t.id=s.terminal_id WHERE s.id=$id", ("$id", Id(saleId)));
            Guid operation, user, terminal, cash, payment; long sequence, received, applied, change, subtotal, total;
            DateTimeOffset at; string cashierName, terminalName, method; string? reference;
            using (var reader = query.ExecuteReader())
            {
                if (!reader.Read()) return null;
                operation=Guid.Parse(reader.GetString(0));user=Guid.Parse(reader.GetString(1));cashierName=reader.GetString(2);
                terminal=Guid.Parse(reader.GetString(3));terminalName=reader.GetString(4);cash=Guid.Parse(reader.GetString(5));sequence=reader.GetInt64(6);
                at=Date(reader,7)!.Value;payment=Guid.Parse(reader.GetString(8));method=reader.GetString(9);applied=reader.GetInt64(10);
                received=reader.GetInt64(11);change=reader.GetInt64(12);subtotal=reader.GetInt64(13);total=reader.GetInt64(14);
                reference=reader.IsDBNull(15)?null:reader.GetString(15);
            }
            using var rows = Command("SELECT product_id,product_name,unit_price_cents,quantity,line_total_cents FROM sale_item WHERE sale_id=$id ORDER BY position", ("$id", Id(saleId)));
            using var itemReader = rows.ExecuteReader(); var items = new List<SaleItem>();
            while (itemReader.Read())
            {
                var item = new SaleItem(Guid.Parse(itemReader.GetString(0)), itemReader.GetString(1), new(itemReader.GetInt64(2)), itemReader.GetInt32(3));
                if (item.LineTotal.Cents != itemReader.GetInt64(4)) throw new InvalidDataException("La partida histórica no es coherente.");
                items.Add(item);
            }
            var sale = new Sale(saleId, operation, user, terminal, cash, sequence, at, items, payment, new(received), Payment.Parse(method), reference);
            if (method != sale.Payment.Method || applied != sale.Payment.Applied.Cents || change != sale.Payment.Change.Cents
                || subtotal != sale.Subtotal.Cents || total != sale.Total.Cents)
                throw new InvalidDataException("La venta histórica no es coherente.");
            return new(sale, cashierName, terminalName);
        }
    }
}
