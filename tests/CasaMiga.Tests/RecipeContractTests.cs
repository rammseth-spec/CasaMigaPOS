using System.Globalization;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

public sealed partial class RecipePersistenceTests
{
    // Synthetic SQL contract fixture only. There is deliberately no equivalent product command/writer.
    private Guid HistoricalContract(bool sealBatch=true)
    {
        Publish();var id=Draft(2);var batch=recipes.GetProductionBatch(id)!;
        foreach(var input in batch.Inputs)
            inventory.RegisterOpeningBalance(new(Guid.NewGuid(),input.ItemId,SqliteInventoryStore.MainLocationId,10000,
                input.Required.Dimension switch{InventoryDimension.Mass=>"mg",InventoryDimension.Volume=>"uL",_=>"piece"}));
        inventory.RegisterOpeningBalance(new(Guid.NewGuid(),finished.Id,SqliteInventoryStore.MainLocationId,0,"piece"));
        using var c=db.Open();using var t=c.BeginTransaction();
        void E(string sql,params object?[] args)
        {
            using var q=c.CreateCommand();q.Transaction=t;q.CommandText=sql;
            for(int i=0;i<args.Length;i++)q.Parameters.AddWithValue("$"+i,args[i] is Guid g?g.ToString("D"):args[i]??DBNull.Value);
            q.ExecuteNonQuery();
        }
        var consume=Guid.NewGuid();var output=Guid.NewGuid();var hash=new string('A',64);
        E("UPDATE production_batch SET consumption_operation_id=$1,output_operation_id=$2,request_sha256=$3 WHERE id=$0",id,consume,output,hash);
        Operation(consume,"PRODUCTION_CONSUMPTION",batch.Inputs.Count);
        int sequence=0;
        foreach(var line in batch.Inputs)Movement(consume,++sequence,line.ItemId,line.Required.Dimension,-line.Required.AtomicUnits,10000);
        Audit("inventory.operation.confirmed",consume);E("UPDATE inventory_operation SET state='CONFIRMED',confirmed_utc=$1 WHERE id=$0",consume,at.ToString("O"));
        Operation(output,"PRODUCTION_OUTPUT",1);Movement(output,1,finished.Id,InventoryDimension.Count,2,0);
        Audit("inventory.operation.confirmed",output);E("UPDATE inventory_operation SET state='CONFIRMED',confirmed_utc=$1 WHERE id=$0",output,at.ToString("O"));
        if(sealBatch){Audit("production.confirmed",id);E("UPDATE production_batch SET status='CONFIRMED',confirmed_utc=$1 WHERE id=$0",id,at.AddMinutes(3).ToString("O"));}
        t.Commit();return id;
        void Operation(Guid operation,string kind,int count)=>E("INSERT INTO inventory_operation(id,request_sha256,kind,state,actor_id,terminal_id,expected_line_count,created_utc,production_batch_id,production_batch_status) VALUES($0,$1,$2,'PENDING',$3,$4,$5,$6,$7,'CONFIRMED')",operation,hash,kind,user.Current!.Id,terminal.Id,count,at.ToString("O"),id);
        void Audit(string action,Guid entity)=>E("INSERT INTO audit_entry VALUES($0,$1,$2,$3,$4,$5,'{}')",Guid.NewGuid(),user.Current!.Id,terminal.Id,action,entity,at.ToString("O"));
        void Movement(Guid operation,int seq,Guid item,InventoryDimension dimension,long delta,long before)
        {
            E("INSERT INTO inventory_movement VALUES($0,$1,$2,$3,$4,$5,0,$6,$7,$8,$9,$10,1)",Guid.NewGuid(),operation,seq,item,SqliteInventoryStore.MainLocationId,dimension.ToString().ToUpperInvariant(),delta,before,checked(before+delta),dimension switch{InventoryDimension.Mass=>"mg",InventoryDimension.Volume=>"uL",_=>"piece"},Math.Abs(delta).ToString(CultureInfo.InvariantCulture));
            E("UPDATE inventory_balance SET quantity_atomic=$1,version=version+1,updated_utc=$2 WHERE item_id=$0",item,checked(before+delta),at.ToString("O"));
        }
    }
    [TestMethod] public void ConfirmedSyntheticContractIsImmutableAndRestorable()
    {
        var id=HistoricalContract();var batch=recipes.GetProductionBatch(id)!;Assert.AreEqual("CONFIRMED",batch.Status);
        var statements=new[]{
            $"UPDATE production_batch SET notes='changed' WHERE id='{id}'",$"DELETE FROM production_batch WHERE id='{id}'",
            $"UPDATE production_output_plan SET actual_count=1 WHERE batch_id='{id}'",$"DELETE FROM production_output_plan WHERE batch_id='{id}'",
            $"UPDATE production_input_snapshot SET item_name='changed' WHERE batch_id='{id}'",$"DELETE FROM production_input_snapshot WHERE batch_id='{id}'",
            $"UPDATE production_formula_snapshot SET formula_name='changed' WHERE batch_id='{id}'",$"DELETE FROM production_formula_snapshot WHERE batch_id='{id}'",
            $"UPDATE inventory_operation SET production_batch_id=NULL WHERE id='{batch.ConsumptionOperationId}'",
            $"DELETE FROM inventory_movement WHERE operation_id='{batch.ConsumptionOperationId}'",
            $"DELETE FROM inventory_item WHERE id='{flour.Id}'",$"DELETE FROM product WHERE id='{product.Id}'"};
        foreach(var sql in statements)Assert.ThrowsExactly<SqliteException>(()=>Sql(sql),sql);
        var draft=Draft();Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_output_plan SET batch_id='{draft}' WHERE batch_id='{id}'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_input_snapshot SET batch_id='{id}',sequence=99 WHERE batch_id='{draft}'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE production_batch SET consumption_operation_id='{batch.ConsumptionOperationId}' WHERE id='{draft}'"));
        Assert.IsEmpty(new SqliteInventoryStore(db,clock).Reconcile());db.Validate();
        var history=inventory.MovementHistory(new(finished.Id));Assert.AreEqual(1,history.Items.Count(x=>x.Kind==InventoryMovementKind.ProductionOutput));
        string backup;using(var lease=new DirectoryLease(directory))backup=new InstallationBackup(directory).Create();
        var destination=Path.Combine(directory,"contract-restored");InstallationBackup.Restore(backup,destination);
        var restored=new Database(Path.Combine(destination,"casamiga.db"));restored.Validate();
        var restoredBatch=new SqliteRecipeStore(restored).GetProductionBatch(id)!;Assert.AreEqual("CONFIRMED",restoredBatch.Status);
        CollectionAssert.AreEqual(batch.Inputs.ToArray(),restoredBatch.Inputs.ToArray());
        Assert.IsEmpty(new SqliteInventoryStore(restored,clock).Reconcile());
    }
    [TestMethod] public void ProductionOperationsCannotCommitAgainstUnconfirmedBatch()
    {
        Assert.ThrowsExactly<SqliteException>(()=>HistoricalContract(sealBatch:false));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind LIKE 'PRODUCTION_%'"));
        Assert.AreEqual("DRAFT",Sql("SELECT status FROM production_batch"));
        Assert.AreEqual(0L,Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{finished.Id}'"));db.Validate();
    }
    [TestMethod] public void ProductionOriginAndNullStateCannotBypassReciprocalContract()
    {
        Publish();var id=Draft(2);var op=Guid.NewGuid();var other=Guid.NewGuid();
        using var c=db.Open();using var t=c.BeginTransaction();using var q=c.CreateCommand();q.Transaction=t;
        q.CommandText="UPDATE production_batch SET consumption_operation_id=$op,output_operation_id=$other WHERE id=$id";
        q.Parameters.AddWithValue("$op",op.ToString());q.Parameters.AddWithValue("$other",other.ToString());q.Parameters.AddWithValue("$id",id.ToString());q.ExecuteNonQuery();
        q.CommandText="INSERT INTO inventory_operation(id,request_sha256,kind,state,actor_id,terminal_id,expected_line_count,created_utc,production_batch_id,production_batch_status) VALUES($op,$hash,'PRODUCTION_CONSUMPTION','PENDING',$actor,$terminal,1,$at,$id,NULL)";
        q.Parameters.AddWithValue("$hash",new string('A',64));q.Parameters.AddWithValue("$actor",user.Current!.Id.ToString());q.Parameters.AddWithValue("$terminal",terminal.Id.ToString());q.Parameters.AddWithValue("$at",at.ToString("O"));
        Assert.ThrowsExactly<SqliteException>(()=>q.ExecuteNonQuery());
        q.CommandText=q.CommandText.Replace("$id,NULL","$id,'CONFIRMED'");q.Parameters["$op"].Value=Guid.NewGuid().ToString();
        Assert.ThrowsExactly<SqliteException>(()=>q.ExecuteNonQuery());t.Rollback();db.Validate();
    }
}
