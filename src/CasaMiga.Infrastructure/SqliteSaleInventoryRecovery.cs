using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;
namespace CasaMiga.Infrastructure;

// Property names/order are the persisted hash V1 wire contract; do not rename.
internal sealed record InventorySnapshotV1(int position,Guid productId,int soldQuantity,bool trackInventory,
    Guid? linkId,Guid? itemId,Guid? locationId,string? dimension,long? factor,long consumedAtomic);
internal static partial class SqliteSaleInventoryStore
{
    internal static void ValidateRecovery(SqliteConnection c,SqliteTransaction tx,Sale sale,Guid evaluationId)
    {
        static void Require(bool condition){if(!condition)throw new InvalidDataException("Inventory recovery inconsistent.");}
        var e=Find(c,tx,sale.OperationId);
        Require(e is not null && e.Id==evaluationId && e.SaleId==sale.Id && e.PolicyVersion==1 && e.ConfirmedAt==sale.ConfirmedAt);
        using(var q=Command(c,tx,"SELECT identity_version FROM sale_inventory_evaluation WHERE id=$id",("$id",evaluationId.ToString("D"))))
            Require(Equals(q.ExecuteScalar(),1L));
        var lines=new List<InventorySnapshotV1>();
        using(var q=Command(c,tx,"SELECT position,product_id,sold_quantity,track_inventory,link_id,item_id,location_id,dimension,quantity_per_product_unit_atomic,total_consumed_atomic,sale_id,policy_version FROM sale_inventory_line_snapshot WHERE evaluation_id=$id ORDER BY position",("$id",evaluationId.ToString("D"))))
        using(var r=q.ExecuteReader())while(r.Read())
        {
            Require(r.GetString(10)==sale.Id.ToString("D") && r.GetInt32(11)==1 && r.GetInt32(3) is 0 or 1);
            Guid? G(int n)=>r.IsDBNull(n)?null:Guid.Parse(r.GetString(n));
            lines.Add(new(r.GetInt32(0),Guid.Parse(r.GetString(1)),r.GetInt32(2),r.GetInt32(3)==1,G(4),G(5),G(6),
                r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetInt64(8),r.GetInt64(9)));
        }
        var status=e!.Status switch {SaleInventoryEvaluationStatus.Disabled=>"DISABLED",SaleInventoryEvaluationStatus.NoConsumption=>"NO_CONSUMPTION",SaleInventoryEvaluationStatus.Consumed=>"CONSUMED",_=>throw new InvalidDataException()};
        Require(EvaluationHash(sale,status,status=="DISABLED"?null:lines.ToArray())==e.EvaluationSha256);
        Require(status=="DISABLED"?lines.Count==0:lines.Count==sale.Items.Count);
        var totals=new Dictionary<(Guid Item,Guid Location,string Dimension),long>();
        for(int i=0;i<lines.Count;i++)
        {
            var l=lines[i];var item=sale.Items[i];
            Require(l.position==i+1 && l.productId==item.ProductId && l.soldQuantity==item.Quantity);
            if(!l.trackInventory)
            {Require(l.linkId is null && l.itemId is null && l.locationId is null && l.dimension is null && l.factor is null && l.consumedAtomic==0);continue;}
            Require(l.linkId is not null && l.itemId is not null && l.locationId==SqliteInventoryStore.MainLocationId
                && l.dimension is "MASS" or "VOLUME" or "COUNT" && l.factor>0 && l.consumedAtomic>0);
            Require(checked(l.soldQuantity*l.factor!.Value)==l.consumedAtomic);
            var key=(l.itemId!.Value,l.locationId!.Value,l.dimension!);
            totals[key]=checked(totals.GetValueOrDefault(key)+l.consumedAtomic);
        }
        using(var q=Command(c,tx,"SELECT count(*) FROM inventory_operation WHERE sale_evaluation_id=$id",("$id",evaluationId.ToString("D"))))
            Require(Equals(q.ExecuteScalar(),status=="CONSUMED"?1L:0L));
        if(status!="CONSUMED"){Require(e.InventoryOperationId is null && totals.Count==0);return;}
        Require(totals.Count>0 && e.InventoryOperationId==SaleConsumptionIdentity.OperationIdV1(sale.OperationId));
        var operationId=e.InventoryOperationId!.Value;
        using(var q=Command(c,tx,"SELECT kind,state,actor_id,terminal_id,sale_evaluation_id,expected_line_count,confirmed_utc,request_sha256,reason_code,note,external_reference FROM inventory_operation WHERE id=$id",("$id",operationId.ToString("D"))))
        using(var r=q.ExecuteReader())
        {
            Require(r.Read());Require(r.GetString(0)=="SALE_CONSUMPTION" && r.GetString(1)=="CONFIRMED"
                && r.GetString(2)==sale.UserId.ToString("D") && r.GetString(3)==sale.TerminalId.ToString("D")
                && r.GetString(4)==evaluationId.ToString("D") && r.GetInt32(5)==totals.Count
                && DateTimeOffset.Parse(r.GetString(6),CultureInfo.InvariantCulture)==sale.ConfirmedAt
                && r.IsDBNull(8) && r.IsDBNull(9) && r.IsDBNull(10));
            var hash=r.GetString(7);r.Close();
            var movements=new List<(Guid Item,Guid Location,string Dimension,long Delta,string Unit,string Captured,long Factor)>();
            using var m=Command(c,tx,"SELECT item_id,location_id,dimension,delta_atomic,is_opening,balance_before_atomic,balance_after_atomic,capture_unit_code,captured_quantity,capture_atomic_factor,sequence FROM inventory_movement WHERE operation_id=$id ORDER BY sequence",("$id",operationId.ToString("D")));
            using var mr=m.ExecuteReader();int sequence=0;
            while(mr.Read())
            {
                var item=Guid.Parse(mr.GetString(0));var location=Guid.Parse(mr.GetString(1));var dimension=mr.GetString(2);var delta=mr.GetInt64(3);
                Require(totals.Remove((item,location,dimension),out var amount) && delta<0 && checked(-delta)==amount
                    && mr.GetInt32(4)==0 && mr.GetInt64(5)>=0 && mr.GetInt64(6)>=0
                    && checked(mr.GetInt64(5)+delta)==mr.GetInt64(6) && mr.GetInt32(10)==++sequence);
                movements.Add((item,location,dimension,delta,mr.GetString(7),mr.GetString(8),mr.GetInt64(9)));
            }
            Require(totals.Count==0 && movements.Count>0);
            // Same canonical request hash V1 as the inventory writer; only persisted facts.
            var hashLines=movements.OrderBy(x=>x.Location).ThenBy(x=>x.Item).Select(x=>new {itemId=x.Item.ToString("D"),locationId=x.Location.ToString("D"),dimension=x.Dimension,isOpening=false,deltaAtomic=x.Delta,captureUnitCode=x.Unit,capturedQuantity=x.Captured,captureAtomicFactor=x.Factor}).ToArray();
            Require(hash==SqliteInventoryStore.CanonicalRequestHash(operationId,sale.UserId,sale.TerminalId,"SALE_CONSUMPTION",null,null,null,hashLines));
        }
    }
}
