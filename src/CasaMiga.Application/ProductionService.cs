using CasaMiga.Domain;
namespace CasaMiga.Application;

public sealed class ProductionService(IProductionUnitOfWork work,UserSession user,IClock clock,Guid terminal)
{
    private Guid Authorize(IProductionSession s,string permission)
    {
        var actor=user.Current?.Id??throw new AccessDeniedException();
        if(!s.Authorized(actor,terminal,permission))throw new AccessDeniedException();return actor;
    }
    public ProductionPreview PrepareProduction(PrepareProductionCommand command)
    {
        var request=Freeze(command);
        return work.Run(s=>{Authorize(s,"production.create");return Prepare(s,request);});
    }
    private static PrepareProductionCommand Freeze(PrepareProductionCommand command)=>new(command.Outputs.ToArray(),command.Selections.ToArray());
    private static ProductionPreview Prepare(IProductionSession s,PrepareProductionCommand command)
    {
        var outputs=command.Outputs.Select(x=>new PlannedOutputLine(s.Recipes.GetProfileVersion(x.ProfileVersionId)?.Value??throw new ProductionException(ProductionFailure.NotFound),x.PlannedCount)).ToArray();
        var formulas=outputs.SelectMany(x=>x.Profile.Formulas).Select(x=>x.FormulaVersionId).Distinct()
            .Select(x=>s.Recipes.GetFormulaVersion(x)?.Value??throw new ProductionException(ProductionFailure.NotFound)).ToArray();
        var result=ProductionPlan.Calculate(outputs,formulas,command.Selections);
        var inputs=result.Plan?.Ingredients??[];
        var stock=inputs.Select(x=>x.InventoryItemId).Concat(outputs.Select(x=>x.FinishedInventoryItemId)).Distinct().Select(s.Stock).ToArray();
        var shortages=inputs.Select(x=>new ProductionShortage(x.InventoryItemId,x.Quantity.AtomicUnits,stock.Single(v=>v.ItemId==x.InventoryItemId).Available))
            .Where(x=>x.Available is null || x.Available<x.Required).ToArray();
        return new(result,stock,shortages,stock.Where(x=>!x.Active).Select(x=>x.ItemId).ToArray());
    }
    public StoredProductionBatch CreateProduction(CreateProductionCommand command)
    {
        var request=command with{Plan=Freeze(command.Plan)};
        return work.Run(s=>{
            var actor=Authorize(s,"production.create");
            if(request.BatchId==Guid.Empty || request.OperationId==Guid.Empty)throw new ProductionException(ProductionFailure.InvalidRequest);
            if(s.FindOperation(request.OperationId) is {} previous)
            {
                if(previous.Id!=request.BatchId || previous.CreatedBy!=actor || previous.TerminalId!=terminal || previous.Notes!=request.Notes ||
                    !previous.Outputs.Select(x=>(x.ProfileVersionId,x.PlannedCount)).OrderBy(x=>x.ProfileVersionId).SequenceEqual(request.Plan.Outputs.Select(x=>(x.ProfileVersionId,x.PlannedCount)).OrderBy(x=>x.ProfileVersionId)))
                    throw new ProductionException(ProductionFailure.IdempotencyConflict);
                // Draft creation retries resolve only the same immutable published versions.
                // Compare every selected alternative, including defaults omitted by the caller.
                var planning=Prepare(s,request.Plan).Planning;
                if(System.Text.Json.JsonSerializer.Serialize(planning.Issues)!=previous.PlanningIssuesJson)
                    throw new ProductionException(ProductionFailure.IdempotencyConflict);
                if(planning.Plan is {} plan)
                {
                    var choices=plan.Formulas.SelectMany(f=>f.Ingredients.Where(i=>i.GroupId.HasValue).Select(i=>(FormulaVersionId:f.Version.Id,i.GroupId,i.OptionId))).OrderBy(x=>x.FormulaVersionId).ThenBy(x=>x.GroupId);
                    var frozen=previous.Inputs.Where(i=>i.GroupId.HasValue).Select(i=>(FormulaVersionId:i.FormulaVersionId!.Value,i.GroupId,i.OptionId)).OrderBy(x=>x.FormulaVersionId).ThenBy(x=>x.GroupId);
                    if(!choices.SequenceEqual(frozen))throw new ProductionException(ProductionFailure.IdempotencyConflict);
                }
                return previous;
            }
            var batch=s.Create(request,actor,terminal,clock.UtcNow);
            s.Audit(actor,terminal,"production.created",batch.Id,clock.UtcNow);return batch;
        });
    }
    public StoredProductionBatch? GetProduction(Guid id)=>work.Run(s=>{Authorize(s,"production.view");return s.Find(id);});
    public IReadOnlyList<Guid> ListProductions(int limit=50,int offset=0)=>work.Run(s=>{Authorize(s,"production.view");return s.List(limit,offset);});
    public StoredProductionBatch ConfirmProduction(ConfirmProductionCommand command)
    {
        var request=command with{Outputs=command.Outputs.ToArray()};
        try{return work.Run(s=>Confirm(s,request,false)!);}
        catch(Exception error) when(error is not (AccessDeniedException or ProductionException or ArgumentException or ProductionDomainException))
        {
            // The original transaction has been disposed. Resolve uncertain commit using a separate transaction.
            var recovered=work.Run(s=>Confirm(s,request,true));if(recovered is not null)return recovered;throw;
        }
    }
    private StoredProductionBatch? Confirm(IProductionSession s,ConfirmProductionCommand request,bool recoveryOnly)
    {
        var actor=Authorize(s,"production.confirm");
        var batch=s.FindOperation(request.OperationId)??throw new ProductionException(ProductionFailure.NotFound);
        if(batch.Id!=request.BatchId || batch.CreatedBy!=actor || batch.TerminalId!=terminal)throw new ProductionException(ProductionFailure.IdempotencyConflict);
        if(ProductionContract.Revision(batch)!=request.ExpectedRevision)throw new ProductionException(ProductionFailure.RevisionConflict);
        var hash=ProductionContract.Hash(batch,request.Outputs);
        if(batch.Status=="CONFIRMED")
        {
            if(hash!=batch.RequestSha256)throw new ProductionException(ProductionFailure.IdempotencyConflict);
            s.ValidateRecovery(batch);return batch;
        }
        if(recoveryOnly)return null;
        if(batch.PlanningIssuesJson!="[]" || batch.Inputs.Count==0)throw new ProductionException(ProductionFailure.UnresolvedPlan);
        var totals=batch.Inputs.GroupBy(x=>x.ItemId).Select(g=>new{Id=g.Key,Quantity=g.Aggregate(0L,(sum,x)=>checked(sum+x.Required.AtomicUnits))}).ToArray();
        var stock=totals.Select(x=>x.Id).Concat(batch.Outputs.Select(x=>x.FinishedItemId)).Distinct().ToDictionary(x=>x,s.Stock);
        if(stock.Values.Any(x=>!x.Active))throw new ProductionException(ProductionFailure.InactiveItem);
        var missing=totals.Select(x=>new ProductionShortage(x.Id,x.Quantity,stock[x.Id].Available)).Where(x=>x.Available is null || x.Available<x.Required).ToArray();
        if(missing.Length!=0)throw new ProductionException(ProductionFailure.InsufficientStock,missing);
        if(batch.Outputs.Any(x=>stock[x.FinishedItemId].Available is null))throw new ProductionException(ProductionFailure.NotInitialized);
        return s.Complete(batch,request,hash,clock.UtcNow);
    }
}
