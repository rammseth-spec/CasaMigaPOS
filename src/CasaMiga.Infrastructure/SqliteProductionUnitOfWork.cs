using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;
namespace CasaMiga.Infrastructure;

public sealed class SqliteProductionUnitOfWork(Database database,Action<string>? fault=null) : IProductionUnitOfWork
{
    public T Run<T>(Func<IProductionSession,T> action)
    {
        try
        {
            var result=new SqliteLocalUnitOfWork(database).RunTechnical((security,c,t)=>action(new Session(database,security,c,t,fault)));
            fault?.Invoke("after_commit");return result;
        }
        catch(SqliteException e) when(e.SqliteErrorCode is 5 or 6)
        {throw new ProductionException(ProductionFailure.ConcurrencyConflict);}
    }
    private sealed class Session(Database database,ILocalStore security,SqliteConnection c,SqliteTransaction t,Action<string>? fault) : IProductionSession
    {
        private readonly SqliteRecipeStore recipes=new(database,c,t);
        public IRecipeStore Recipes=>recipes;
        public bool Authorized(Guid actor,Guid terminal,string permission)=>security.FindUser(actor) is {Active:true} && security.HasPermission(actor,permission) && security.FindTerminal(terminal) is not null;
        public StoredProductionBatch? Find(Guid id)=>recipes.GetProductionBatch(id);
        public StoredProductionBatch? FindOperation(Guid operation)
        {
            using var q=Command("SELECT id FROM production_batch WHERE operation_id=$0",operation);
            return q.ExecuteScalar() is string id?Find(Guid.Parse(id)):null;
        }
        public IReadOnlyList<Guid> List(int limit,int offset)=>recipes.ListProductionBatchIds(limit,offset);
        public ProductionStock Stock(Guid item)
        {
            using var q=Command("SELECT i.dimension,i.active,b.quantity_atomic FROM inventory_item i LEFT JOIN inventory_balance b ON b.item_id=i.id AND b.location_id=$1 WHERE i.id=$0",item,ProductionContract.Main);
            using var r=q.ExecuteReader();if(!r.Read())throw new ProductionException(ProductionFailure.NotFound);
            return new(item,Enum.Parse<InventoryDimension>(r.GetString(0),true),r.GetInt64(1)==1,r.IsDBNull(2)?null:r.GetInt64(2));
        }
        public StoredProductionBatch Create(CreateProductionCommand command,Guid actor,Guid terminal,DateTimeOffset at)
        {
            var outputs=command.Plan.Outputs.Select(x=>new PlannedOutputLine(recipes.GetProfileVersion(x.ProfileVersionId)?.Value??throw new ProductionException(ProductionFailure.NotFound),x.PlannedCount));
            recipes.CreateProductionDraft(command.BatchId,command.OperationId,actor,terminal,at,outputs,command.Plan.Selections,notes:command.Notes);
            return Find(command.BatchId)!;
        }
        public void Audit(Guid actor,Guid terminal,string action,Guid entity,DateTimeOffset at)=>security.AddAudit(new(Guid.NewGuid(),actor,terminal,action,entity,at,"{}"));
        public StoredProductionBatch Complete(StoredProductionBatch batch,ConfirmProductionCommand command,string hash,DateTimeOffset at)
        {
            if(SqliteInventoryStore.Reconcile(c,t).Count!=0)throw new ProductionException(ProductionFailure.RecoveryInconsistent);
            var consumption=ProductionContract.ConsumptionId(batch.OperationId);
            Guid? output=command.Outputs.Any(x=>x.ActualCount>0)?ProductionContract.OutputId(batch.OperationId):null;
            fault?.Invoke("before_snapshots");
            foreach(var actual in command.Outputs)
                Execute("UPDATE production_output_plan SET actual_count=$2,actual_policy_pending=CASE WHEN $2=0 OR $2>planned_count THEN 1 ELSE 0 END WHERE batch_id=$0 AND product_id=$1",batch.Id,actual.ProductId,actual.ActualCount);
            Execute("UPDATE production_batch SET request_sha256=$1,consumption_operation_id=$2,output_operation_id=$3 WHERE id=$0 AND status='DRAFT'",batch.Id,hash,consumption,output);
            fault?.Invoke("before_consumption");
            SqliteInventoryStore.WriteOperation(c,t,Consumption(batch),null,at,productionBatchId:batch.Id);
            fault?.Invoke("after_consumption");
            if(output.HasValue)
            {
                fault?.Invoke("before_output");
                SqliteInventoryStore.WriteOperation(c,t,Output(batch,command.Outputs),null,at,productionBatchId:batch.Id);
                fault?.Invoke("after_output");
            }
            Audit(batch.CreatedBy,batch.TerminalId,"production.confirmed",batch.Id,at);
            fault?.Invoke("during_audit");fault?.Invoke("before_confirmed");
            Execute("UPDATE production_batch SET status='CONFIRMED',confirmed_utc=$1 WHERE id=$0 AND status='DRAFT'",batch.Id,at.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture));
            return Find(batch.Id)!;
        }
        public void ValidateRecovery(StoredProductionBatch batch)
        {
            if(batch.Outputs.Any(x=>!x.ActualCount.HasValue))throw new ProductionException(ProductionFailure.RecoveryInconsistent);
            var actual=batch.Outputs.Select(x=>new ActualOutputLine(x.ProductId,x.ActualCount!.Value)).ToArray();
            if(batch.RequestSha256!=ProductionContract.Hash(batch,actual) || batch.ConsumptionOperationId!=ProductionContract.ConsumptionId(batch.OperationId) ||
                batch.OutputOperationId!=(actual.Any(x=>x.ActualCount>0)?ProductionContract.OutputId(batch.OperationId):(Guid?)null))throw new ProductionException(ProductionFailure.RecoveryInconsistent);
            Check(Consumption(batch));if(batch.OutputOperationId.HasValue)Check(Output(batch,actual));
            void Check(InventoryOperationRequest request)
            {
                using var origin=Command("SELECT count(*) FROM inventory_operation WHERE id=$0 AND production_batch_id=$1 AND state='CONFIRMED' AND production_batch_status='CONFIRMED'",request.OperationId,batch.Id);
                if(Convert.ToInt64(origin.ExecuteScalar(),CultureInfo.InvariantCulture)!=1)
                    throw new ProductionException(ProductionFailure.RecoveryInconsistent);
                var saved=SqliteInventoryStore.ReadOperation(c,t,request.OperationId);
                if(saved is null || saved.RequestSha256!=SqliteInventoryStore.RequestHash(request) || saved.Movements.Count!=request.Lines.Count)
                    throw new ProductionException(ProductionFailure.RecoveryInconsistent);
                foreach(var line in request.Lines)
                    if(!saved.Movements.Any(m=>m.InventoryItemId==line.InventoryItemId && m.InventoryLocationId==line.InventoryLocationId && m.DeltaAtomic==line.DeltaAtomic))
                        throw new ProductionException(ProductionFailure.RecoveryInconsistent);
            }
        }
        private static InventoryOperationRequest Consumption(StoredProductionBatch batch)=>InventoryOperationRequest.CreateProduction(
            ProductionContract.ConsumptionId(batch.OperationId),batch.CreatedBy,batch.TerminalId,InventoryMovementKind.ProductionConsumption,
            batch.Inputs.GroupBy(x=>new{x.ItemId,x.Required.Dimension}).Select(g=>Line(g.Key.ItemId,g.Key.Dimension,g.Aggregate(0L,(v,x)=>checked(v+x.Required.AtomicUnits)),false)));
        private static InventoryOperationRequest Output(StoredProductionBatch batch,IReadOnlyList<ActualOutputLine> actual)=>InventoryOperationRequest.CreateProduction(
            ProductionContract.OutputId(batch.OperationId),batch.CreatedBy,batch.TerminalId,InventoryMovementKind.ProductionOutput,
            actual.Where(x=>x.ActualCount>0).Select(x=>Line(batch.Outputs.Single(o=>o.ProductId==x.ProductId).FinishedItemId,InventoryDimension.Count,x.ActualCount,true)));
        private static InventoryOperationLine Line(Guid item,InventoryDimension dimension,long quantity,bool output)
        {
            var unit=dimension switch{InventoryDimension.Mass=>InventoryUnits.Milligram,InventoryDimension.Volume=>InventoryUnits.Microliter,_=>InventoryUnits.Piece};
            var capture=InventoryCapture.Standard(quantity,unit);
            return InventoryOperationLine.Change(item,ProductionContract.Main,output?InventoryDelta.Increase(capture.Quantity):InventoryDelta.Decrease(capture.Quantity),capture);
        }
        private SqliteCommand Command(string sql,params object?[] values)
        {
            var q=c.CreateCommand();q.Transaction=t;q.CommandText=sql;
            for(int i=0;i<values.Length;i++)q.Parameters.AddWithValue("$"+i,values[i] is Guid g?g.ToString("D"):values[i]??DBNull.Value);return q;
        }
        private void Execute(string sql,params object?[] values){using var q=Command(sql,values);q.ExecuteNonQuery();}
    }
}
