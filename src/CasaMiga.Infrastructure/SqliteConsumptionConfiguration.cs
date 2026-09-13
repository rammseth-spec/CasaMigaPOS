using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        public ConsumptionStatus GetConsumptionStatus()
        {var c=SqliteSaleInventoryStore.ReadConfig(connection,transaction);return new(c.State,c.Version,c.UpdatedAt,c.UpdatedBy);}
        public void UpdateConsumption(ConsumptionStatus expected,ConsumptionStatus next)
        {
            using var q=Command("UPDATE inventory_sale_consumption_config SET state=$state,version=$version,updated_utc=$at,updated_by=$actor WHERE id=1 AND state=$old AND version=$v",
                ("$state",next.State.ToString().ToUpperInvariant()),("$version",next.Version),("$at",At(next.UpdatedUtc)),("$actor",Id(next.UpdatedBy!.Value)),
                ("$old",expected.State.ToString().ToUpperInvariant()),("$v",expected.Version));
            if(q.ExecuteNonQuery()!=1)throw new ConsumptionException(ConsumptionError.ConcurrencyConflict);
        }
        public ConsumptionPreflight CheckConsumption(DateTimeOffset at)
        {
            var issues=new List<PreflightIssue>();
            foreach(var difference in SqliteInventoryStore.Reconcile(connection,transaction))
                issues.Add(new(PreflightCode.ReconciliationMismatch,InventoryItemId:difference.InventoryItemId,
                    LedgerAtomic:difference.LedgerQuantityAtomic,BalanceAtomic:difference.BalanceQuantityAtomic));
            using var main=Command("SELECT count(*) FROM inventory_location WHERE id=$id AND code='MAIN' AND active=1",("$id",Id(SqliteInventoryStore.MainLocationId)));
            var mainValid=Equals(main.ExecuteScalar(),1L);
            // No paging or category filter: every active product is checked, including hidden categories.
            using var q=Command("""
                SELECT p.id,p.name,p.track_inventory,l.id,l.inventory_item_id,l.dimension,l.quantity_per_product_unit_atomic,
                       i.name,i.active,i.dimension,b.quantity_atomic,b.dimension,
                       (SELECT count(*) FROM inventory_movement m JOIN inventory_operation o ON o.id=m.operation_id
                        WHERE m.item_id=i.id AND m.location_id=$main AND m.is_opening=1 AND o.kind='OPENING_BALANCE' AND o.state='CONFIRMED')
                FROM product p LEFT JOIN product_inventory_link l ON l.product_id=p.id AND l.active=1
                LEFT JOIN inventory_item i ON i.id=l.inventory_item_id
                LEFT JOIN inventory_balance b ON b.item_id=i.id AND b.location_id=$main
                WHERE p.active=1 ORDER BY p.id,l.id
                """,("$main",Id(SqliteInventoryStore.MainLocationId)));
            using var r=q.ExecuteReader();var seen=new HashSet<Guid>();
            while(r.Read())
            {
                var product=Guid.Parse(r.GetString(0));var name=r.GetString(1);var tracked=r.GetInt32(2)==1;
                Guid? item=r.IsDBNull(4)?null:Guid.Parse(r.GetString(4));var itemName=r.IsDBNull(7)?null:r.GetString(7);
                void Add(PreflightCode code)=>issues.Add(new(code,product,name,item,itemName));
                if(!seen.Add(product))Add(PreflightCode.InvalidLink);
                if(r.IsDBNull(3)){if(tracked)Add(PreflightCode.TrackedProductWithoutInventoryLink);continue;}
                if(!tracked){Add(PreflightCode.UnexpectedInventoryLink);continue;}
                if(r.IsDBNull(7)){Add(PreflightCode.InventoryItemMissing);continue;}
                if(r.GetInt32(8)!=1)Add(PreflightCode.InventoryItemInactive);
                if(r.GetString(5)!=r.GetString(9) || (!r.IsDBNull(11) && r.GetString(11)!=r.GetString(9)))Add(PreflightCode.DimensionMismatch);
                if(r.GetInt64(6)<=0)Add(PreflightCode.InvalidQuantity);
                if(!mainValid)Add(PreflightCode.MainInvalid);
                if(r.IsDBNull(10) || r.GetInt64(12)!=1)Add(PreflightCode.NotInitialized);
            }
            return new(GetConsumptionStatus(),at,issues.AsReadOnly());
        }
    }
}
