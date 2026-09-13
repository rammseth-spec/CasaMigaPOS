using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        private bool HasInventoryContract()
        {
            using var q=Command("SELECT count(*) FROM pragma_table_info('sale') WHERE name='inventory_evaluation_contract_version'");
            return Convert.ToInt64(q.ExecuteScalar())==1;
        }
        private void CompleteInventory(Sale sale)
        {
            try
            {
                SaleInventoryConsumptionPlan? plan=null;
                if(SqliteSaleInventoryStore.ReadConfig(connection,transaction).State==InventorySaleConsumptionState.Enabled)
                {
                    var inputs=new List<SaleInventoryLineInput>();
                    for(int i=0;i<sale.Items.Count;i++)
                    {
                        var product=FindProduct(sale.Items[i].ProductId) ?? throw new InvalidDataException();
                        using var q=Command("SELECT l.id,l.inventory_item_id,l.dimension,l.quantity_per_product_unit_atomic,i.active,b.quantity_atomic FROM product_inventory_link l JOIN inventory_item i ON i.id=l.inventory_item_id LEFT JOIN inventory_balance b ON b.item_id=i.id AND b.location_id=$main WHERE l.product_id=$product AND l.active=1",
                            ("$main",Id(SqliteInventoryStore.MainLocationId)),("$product",Id(product.Id)));
                        using var r=q.ExecuteReader();
                        ResolvedSaleInventoryLink? link=null;SaleInventoryStockState? stock=null;
                        if(r.Read())
                        {
                            var item=Guid.Parse(r.GetString(1));var dimension=Enum.Parse<InventoryDimension>(r.GetString(2),true);
                            link=new(Guid.Parse(r.GetString(0)),product.Id,item,dimension,r.GetInt64(3));
                            stock=new(item,SqliteInventoryStore.MainLocationId,dimension,r.GetInt32(4)==1,r.IsDBNull(5)?null:r.GetInt64(5));
                            if(r.Read())throw new InvalidDataException();
                        }
                        inputs.Add(new(i+1,product.Id,sale.Items[i].Quantity,product.TrackInventory,link,stock));
                    }
                    plan=SaleInventoryConsumptionPolicy.Evaluate(inputs);
                }
                SqliteSaleInventoryStore.Save(connection,transaction,sale,plan);
            }
            catch(SaleInventoryPolicyException e)
            {throw new CheckoutInventoryException(Enum.Parse<CheckoutInventoryError>(e.Error.ToString()),e.ProductId,e.InventoryItemId,e.RequiredAtomic,e.AvailableAtomic,e.Dimension);}
            catch(SqliteException e) when(e.SqliteErrorCode is 5 or 6)
            {throw new CheckoutInventoryException(CheckoutInventoryError.InventoryConcurrencyConflict);}
            catch(InventoryStoreException e) when(e.Error==InventoryError.ConcurrencyConflict)
            {throw new CheckoutInventoryException(CheckoutInventoryError.InventoryConcurrencyConflict);}
            catch(Exception e) when(e is not CheckoutException)
            {throw new CheckoutInventoryException(CheckoutInventoryError.InventoryTrackingConfigurationInvalid);}
        }
        public void ValidateSaleInventoryRecovery(Sale sale)
        {
            try
            {
                if(!HasInventoryContract())return; // Only older schema fixtures/legacy databases.
                using var q=Command("SELECT inventory_evaluation_contract_version,inventory_evaluation_id FROM sale WHERE id=$id",("$id",Id(sale.Id)));
                using var r=q.ExecuteReader();if(!r.Read())throw new InvalidDataException();
                var version=r.GetInt32(0);var id=r.IsDBNull(1)?(Guid?)null:Guid.Parse(r.GetString(1));
                if(version==0 && id is null)return;
                if(version!=1 || id is null)throw new InvalidDataException();
                r.Close();
                SqliteSaleInventoryStore.ValidateRecovery(connection,transaction,sale,id.Value);
            }
            catch(Exception e) when(e is not CheckoutInventoryException)
            {throw new CheckoutInventoryException(CheckoutInventoryError.InventoryRecoveryInconsistent);}
        }
    }
}
