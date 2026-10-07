using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        // ---------- Tiendas ----------
        private const string StoreColumns = "id,name,contact,phone,address,active";
        private static ConsignmentStore ReadStore(SqliteDataReader r) =>
            new(Guid.Parse(r.GetString(0)), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt64(5) == 1);

        public IReadOnlyList<ConsignmentStore> ListConsignmentStores(bool includeInactive)
        {
            using var query = Command($"SELECT {StoreColumns} FROM consignment_store" + (includeInactive ? "" : " WHERE active=1") + " ORDER BY name COLLATE NOCASE,id");
            using var r = query.ExecuteReader();
            var stores = new List<ConsignmentStore>();
            while (r.Read()) stores.Add(ReadStore(r));
            return stores;
        }

        public ConsignmentStore? FindConsignmentStore(Guid id)
        {
            using var query = Command($"SELECT {StoreColumns} FROM consignment_store WHERE id=$id", ("$id", Id(id)));
            using var r = query.ExecuteReader();
            return r.Read() ? ReadStore(r) : null;
        }

        public bool ConsignmentStoreNameTaken(string name, Guid? except)
        {
            using var query = Command("SELECT count(*) FROM consignment_store WHERE catalog_key_v1(name)=catalog_key_v1($name) AND ($id IS NULL OR id<>$id)",
                ("$name", name), ("$id", except is { } e ? Id(e) : null));
            return Convert.ToInt64(query.ExecuteScalar()) > 0;
        }

        public void SaveConsignmentStore(ConsignmentStore store, bool create, DateTimeOffset at)
        {
            var values = new (string, object?)[] { ("$id", Id(store.Id)), ("$name", store.Name), ("$contact", store.Contact), ("$phone", store.Phone),
                ("$address", store.Address), ("$active", store.Active ? 1 : 0), ("$at", At(at)) };
            using var write = Command(create
                ? "INSERT INTO consignment_store(id,name,contact,phone,address,active,created_utc,modified_utc) VALUES($id,$name,$contact,$phone,$address,$active,$at,$at)"
                : "UPDATE consignment_store SET name=$name,contact=$contact,phone=$phone,address=$address,active=$active,modified_utc=$at WHERE id=$id", values);
            if (write.ExecuteNonQuery() != 1) throw new InvalidOperationException("No se guardó la tienda.");
        }

        public IReadOnlyList<ConsignmentTemplateLine> ConsignmentTemplate(Guid storeId)
        {
            using var query = Command("SELECT t.product_id,p.name,t.quantity FROM consignment_template_item t JOIN product p ON p.id=t.product_id"
                + " WHERE t.store_id=$store AND p.active=1 ORDER BY p.name COLLATE NOCASE", ("$store", Id(storeId)));
            using var r = query.ExecuteReader();
            var lines = new List<ConsignmentTemplateLine>();
            while (r.Read()) lines.Add(new(Guid.Parse(r.GetString(0)), r.GetString(1), r.GetInt32(2)));
            return lines;
        }

        public void ReplaceConsignmentTemplate(Guid storeId, IReadOnlyList<ConsignmentLine> lines)
        {
            using (var clear = Command("DELETE FROM consignment_template_item WHERE store_id=$store", ("$store", Id(storeId)))) clear.ExecuteNonQuery();
            foreach (var line in lines)
            {
                using var add = Command("INSERT INTO consignment_template_item(store_id,product_id,quantity) VALUES($store,$product,$quantity)",
                    ("$store", Id(storeId)), ("$product", Id(line.ProductId)), ("$quantity", line.Quantity));
                add.ExecuteNonQuery();
            }
        }

        // ---------- Precios de charola ----------
        public IReadOnlyList<ConsignmentPrice> ConsignmentPrices()
        {
            using var query = Command("SELECT p.id,p.name,c.name,p.price_cents,cp.price_cents,p.sku FROM product p JOIN category c ON c.id=p.category_id"
                + " LEFT JOIN consignment_price cp ON cp.product_id=p.id WHERE p.active=1 ORDER BY c.name COLLATE NOCASE,p.name COLLATE NOCASE");
            using var r = query.ExecuteReader();
            var prices = new List<ConsignmentPrice>();
            while (r.Read())
                prices.Add(new(Guid.Parse(r.GetString(0)), r.GetString(1), r.GetString(2), new(r.GetInt64(3)), r.IsDBNull(4) ? null : new Money(r.GetInt64(4)), r.IsDBNull(5) ? null : r.GetString(5)));
            return prices;
        }

        public Money? FindConsignmentPrice(Guid productId)
        {
            using var query = Command("SELECT price_cents FROM consignment_price WHERE product_id=$product", ("$product", Id(productId)));
            return query.ExecuteScalar() is long cents ? new Money(cents) : null;
        }

        public void SetConsignmentPrice(Guid productId, Money? price, DateTimeOffset at)
        {
            using var write = price is { } value
                ? Command("INSERT INTO consignment_price(product_id,price_cents,modified_utc) VALUES($product,$price,$at)"
                    + " ON CONFLICT(product_id) DO UPDATE SET price_cents=excluded.price_cents,modified_utc=excluded.modified_utc",
                    ("$product", Id(productId)), ("$price", value.Cents), ("$at", At(at)))
                : Command("DELETE FROM consignment_price WHERE product_id=$product", ("$product", Id(productId)));
            write.ExecuteNonQuery();
        }

        // ---------- Entregas, recolecciones y anulaciones ----------
        public void AddConsignmentDelivery(ConsignmentDelivery delivery)
        {
            // Partidas primero: el encabezado valida que la entrega esté completa (ver 021).
            foreach (var item in delivery.Items)
            {
                using var add = Command("INSERT INTO consignment_delivery_item(delivery_id,position,product_id,product_name,unit_price_cents,retail_price_cents,quantity)"
                    + " VALUES($delivery,$position,$product,$name,$price,$retail,$quantity)",
                    ("$delivery", Id(delivery.Id)), ("$position", item.Position), ("$product", Id(item.ProductId)), ("$name", item.ProductName),
                    ("$price", item.UnitPrice.Cents), ("$retail", item.RetailPrice.Cents), ("$quantity", item.Quantity));
                add.ExecuteNonQuery();
            }
            using var header = Command("INSERT INTO consignment_delivery(id,operation_id,store_id,terminal_id,delivered_by,delivered_utc,note)"
                + " VALUES($id,$operation,$store,$terminal,$by,$at,$note)",
                ("$id", Id(delivery.Id)), ("$operation", Id(delivery.OperationId)), ("$store", Id(delivery.StoreId)), ("$terminal", Id(delivery.TerminalId)),
                ("$by", Id(delivery.DeliveredBy)), ("$at", At(delivery.DeliveredAt)), ("$note", delivery.Note));
            header.ExecuteNonQuery();
        }

        public void AddConsignmentSettlement(ConsignmentSettlement settlement)
        {
            foreach (var item in settlement.Items)
            {
                using var add = Command("INSERT INTO consignment_settlement_item(delivery_id,position,sold,returned,line_total_cents) VALUES($delivery,$position,$sold,$returned,$total)",
                    ("$delivery", Id(settlement.DeliveryId)), ("$position", item.Position), ("$sold", item.Sold), ("$returned", item.Returned), ("$total", item.LineTotal.Cents));
                add.ExecuteNonQuery();
            }
            using var header = Command("INSERT INTO consignment_settlement(delivery_id,operation_id,collected_by,collected_utc,sold_cents,note) VALUES($delivery,$operation,$by,$at,$sold,$note)",
                ("$delivery", Id(settlement.DeliveryId)), ("$operation", Id(settlement.OperationId)), ("$by", Id(settlement.CollectedBy)),
                ("$at", At(settlement.CollectedAt)), ("$sold", settlement.SoldTotal.Cents), ("$note", settlement.Note));
            header.ExecuteNonQuery();
        }

        public void AddConsignmentVoid(ConsignmentVoid cancellation)
        {
            using var add = Command("INSERT INTO consignment_void(delivery_id,operation_id,voided_by,voided_utc,reason) VALUES($delivery,$operation,$by,$at,$reason)",
                ("$delivery", Id(cancellation.DeliveryId)), ("$operation", Id(cancellation.OperationId)), ("$by", Id(cancellation.VoidedBy)),
                ("$at", At(cancellation.VoidedAt)), ("$reason", cancellation.Reason));
            add.ExecuteNonQuery();
        }

        public ConsignmentDelivery? FindConsignmentDelivery(Guid id) => Deliveries("d.id=$key", ("$key", Id(id))).SingleOrDefault();
        public ConsignmentDelivery? FindConsignmentDeliveryByOperation(Guid operation) => Deliveries("d.operation_id=$key", ("$key", Id(operation))).SingleOrDefault();
        public ConsignmentDelivery? FindConsignmentDeliveryBySettlement(Guid operation) => Deliveries("st.operation_id=$key", ("$key", Id(operation))).SingleOrDefault();

        public IReadOnlyList<ConsignmentDelivery> QueryConsignmentDeliveries(DateTimeOffset? fromUtc, DateTimeOffset? untilUtc, Guid? storeId, bool openOnly) =>
            Deliveries("($from IS NULL OR d.delivered_utc>=$from) AND ($until IS NULL OR d.delivered_utc<$until) AND ($store IS NULL OR d.store_id=$store)"
                + (openOnly ? " AND st.delivery_id IS NULL AND v.delivery_id IS NULL" : ""),
                ("$from", fromUtc is { } f ? At(f) : null), ("$until", untilUtc is { } u ? At(u) : null), ("$store", storeId is { } s ? Id(s) : null));

        public DateTimeOffset? LastConsignmentDelivery(Guid storeId)
        {
            using var query = Command("SELECT max(d.delivered_utc) FROM consignment_delivery d LEFT JOIN consignment_void v ON v.delivery_id=d.id"
                + " WHERE d.store_id=$store AND v.delivery_id IS NULL", ("$store", Id(storeId)));
            using var r = query.ExecuteReader();
            return r.Read() ? Date(r, 0) : null;
        }

        private const string DeliveryJoins = " FROM consignment_delivery d JOIN consignment_store s ON s.id=d.store_id JOIN app_user u ON u.id=d.delivered_by"
            + " LEFT JOIN consignment_settlement st ON st.delivery_id=d.id LEFT JOIN consignment_void v ON v.delivery_id=d.id";

        private List<ConsignmentDelivery> Deliveries(string where, params (string, object?)[] parameters)
        {
            var headers = new List<(Guid Id, Guid Operation, Guid Store, string StoreName, Guid Terminal, Guid By, string ByName, DateTimeOffset At, string Note,
                ConsignmentSettlement? Settlement, ConsignmentVoid? Void)>();
            using (var query = Command("SELECT d.id,d.operation_id,d.store_id,s.name,d.terminal_id,d.delivered_by,u.display_name,d.delivered_utc,d.note,"
                + "st.operation_id,st.collected_by,st.collected_utc,st.sold_cents,st.note,v.operation_id,v.voided_by,v.voided_utc,v.reason"
                + DeliveryJoins + " WHERE " + where + " ORDER BY d.delivered_utc,d.id", parameters))
            using (var r = query.ExecuteReader())
                while (r.Read())
                {
                    var id = Guid.Parse(r.GetString(0));
                    var settlement = r.IsDBNull(9) ? null : new ConsignmentSettlement(id, Guid.Parse(r.GetString(9)), Guid.Parse(r.GetString(10)), Date(r, 11)!.Value,
                        new(r.GetInt64(12)), r.GetString(13), []);
                    var cancellation = r.IsDBNull(14) ? null : new ConsignmentVoid(id, Guid.Parse(r.GetString(14)), Guid.Parse(r.GetString(15)), Date(r, 16)!.Value, r.GetString(17));
                    headers.Add((id, Guid.Parse(r.GetString(1)), Guid.Parse(r.GetString(2)), r.GetString(3), Guid.Parse(r.GetString(4)), Guid.Parse(r.GetString(5)),
                        r.GetString(6), Date(r, 7)!.Value, r.GetString(8), settlement, cancellation));
                }
            if (headers.Count == 0) return [];

            var items = new Dictionary<Guid, List<ConsignmentDeliveryItem>>();
            using (var query = Command("SELECT i.delivery_id,i.position,i.product_id,i.product_name,i.unit_price_cents,i.retail_price_cents,i.quantity"
                + " FROM consignment_delivery_item i JOIN consignment_delivery d ON d.id=i.delivery_id"
                + " LEFT JOIN consignment_settlement st ON st.delivery_id=d.id LEFT JOIN consignment_void v ON v.delivery_id=d.id"
                + " WHERE " + where + " ORDER BY i.delivery_id,i.position", parameters))
            using (var r = query.ExecuteReader())
                while (r.Read())
                {
                    var delivery = Guid.Parse(r.GetString(0));
                    if (!items.TryGetValue(delivery, out var list)) items[delivery] = list = [];
                    list.Add(new(r.GetInt32(1), Guid.Parse(r.GetString(2)), r.GetString(3), new(r.GetInt64(4)), new(r.GetInt64(5)), r.GetInt32(6)));
                }

            var settled = new Dictionary<Guid, List<ConsignmentSettlementItem>>();
            using (var query = Command("SELECT si.delivery_id,si.position,si.sold,si.returned,si.line_total_cents"
                + " FROM consignment_settlement_item si JOIN consignment_delivery d ON d.id=si.delivery_id"
                + " JOIN consignment_settlement st ON st.delivery_id=d.id LEFT JOIN consignment_void v ON v.delivery_id=d.id"
                + " WHERE " + where + " ORDER BY si.delivery_id,si.position", parameters))
            using (var r = query.ExecuteReader())
                while (r.Read())
                {
                    var delivery = Guid.Parse(r.GetString(0));
                    if (!settled.TryGetValue(delivery, out var list)) settled[delivery] = list = [];
                    list.Add(new(r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), new(r.GetInt64(4))));
                }

            return headers.Select(h => new ConsignmentDelivery(h.Id, h.Operation, h.Store, h.StoreName, h.Terminal, h.By, h.ByName, h.At, h.Note,
                items.GetValueOrDefault(h.Id) ?? [], h.Settlement is null ? null : h.Settlement with { Items = settled.GetValueOrDefault(h.Id) ?? [] }, h.Void)).ToList();
        }

        // ---------- Cobros ----------
        public void AddConsignmentPayment(ConsignmentPayment payment)
        {
            using var add = Command("INSERT INTO consignment_payment(id,operation_id,store_id,received_by,received_utc,amount_cents,method,reference,note)"
                + " VALUES($id,$operation,$store,$by,$at,$amount,$method,$reference,$note)",
                ("$id", Id(payment.Id)), ("$operation", Id(payment.OperationId)), ("$store", Id(payment.StoreId)), ("$by", Id(payment.ReceivedBy)),
                ("$at", At(payment.ReceivedAt)), ("$amount", payment.Amount.Cents), ("$method", Payment.Code(payment.Method)), ("$reference", payment.Reference), ("$note", payment.Note));
            add.ExecuteNonQuery();
        }

        public ConsignmentPayment? FindConsignmentPaymentByOperation(Guid operation) => Payments("operation_id=$key", ("$key", Id(operation))).SingleOrDefault();

        public IReadOnlyList<ConsignmentPayment> QueryConsignmentPayments(DateTimeOffset? fromUtc, DateTimeOffset? untilUtc, Guid? storeId) =>
            Payments("($from IS NULL OR received_utc>=$from) AND ($until IS NULL OR received_utc<$until) AND ($store IS NULL OR store_id=$store)",
                ("$from", fromUtc is { } f ? At(f) : null), ("$until", untilUtc is { } u ? At(u) : null), ("$store", storeId is { } s ? Id(s) : null));

        private List<ConsignmentPayment> Payments(string where, params (string, object?)[] parameters)
        {
            using var query = Command("SELECT id,operation_id,store_id,received_by,received_utc,amount_cents,method,reference,note FROM consignment_payment WHERE "
                + where + " ORDER BY received_utc,id", parameters);
            using var r = query.ExecuteReader();
            var payments = new List<ConsignmentPayment>();
            while (r.Read())
                payments.Add(new(Guid.Parse(r.GetString(0)), Guid.Parse(r.GetString(1)), Guid.Parse(r.GetString(2)), Guid.Parse(r.GetString(3)), Date(r, 4)!.Value,
                    new(r.GetInt64(5)), Payment.Parse(r.GetString(6)), r.IsDBNull(7) ? null : r.GetString(7), r.GetString(8)));
            return payments;
        }

        public Money ConsignmentBalance(Guid storeId)
        {
            using var query = Command("SELECT COALESCE((SELECT SUM(st.sold_cents) FROM consignment_settlement st JOIN consignment_delivery d ON d.id=st.delivery_id WHERE d.store_id=$store),0)"
                + "-COALESCE((SELECT SUM(amount_cents) FROM consignment_payment WHERE store_id=$store),0)", ("$store", Id(storeId)));
            return new(Convert.ToInt64(query.ExecuteScalar()));
        }
    }
}
