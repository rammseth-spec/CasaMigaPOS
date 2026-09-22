using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaMiga.Domain;
namespace CasaMiga.Application;

public static class ProductionContract
{
    public const int Version=1;
    public static readonly Guid Main=new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ConsumptionNamespace=new("70de5260-9c49-50ce-a30c-14d27e575228");
    public static readonly Guid OutputNamespace=new("5909cf55-b0cc-51f0-86a0-9a0ad456f250");
    public static Guid ConsumptionId(Guid operation)=>SaleConsumptionIdentity.UuidV5(ConsumptionNamespace,operation.ToString("D"));
    public static Guid OutputId(Guid operation)=>SaleConsumptionIdentity.UuidV5(OutputNamespace,operation.ToString("D"));
    // Content revision is stable across confirmation, excludes actuals, status and generated metadata.
    public static string Revision(StoredProductionBatch batch)=>Hash(batch,batch.Outputs.Select(o=>new ActualOutputLine(o.ProductId,0)).ToArray());
    public static string Hash(StoredProductionBatch batch,IReadOnlyList<ActualOutputLine> actual)
    {
        if(batch.ContractVersion!=Version || batch.IdentityVersion!=Version)
            throw new ProductionException(ProductionFailure.RecoveryInconsistent);
        if(actual.Count!=batch.Outputs.Count || actual.Select(x=>x.ProductId).Distinct().Count()!=actual.Count ||
            actual.Any(x=>!batch.Outputs.Any(o=>o.ProductId==x.ProductId)))throw new ProductionException(ProductionFailure.InvalidRequest);
        var contract=new {
            version=Version,identityVersion=batch.IdentityVersion,batch.Id,batch.OperationId,batch.CreatedBy,batch.TerminalId,location=Main,
            consumptionOperationId=ConsumptionId(batch.OperationId),
            outputOperationId=actual.Any(x=>x.ActualCount>0)?OutputId(batch.OperationId):(Guid?)null,
            outputs=batch.Outputs.OrderBy(x=>x.ProductId).Select(x=>new{x.ProductId,x.FinishedItemId,x.ProfileVersionId,x.PlannedCount,actual=actual.Single(a=>a.ProductId==x.ProductId).ActualCount}),
            formulas=batch.Formulas.OrderBy(x=>x.FormulaVersionId).Select(x=>new{x.FormulaVersionId,dimension=x.Required.Dimension.ToString(),atomic=x.Required.AtomicUnits,x.Factor.Numerator,x.Factor.Denominator}),
            inputs=batch.Inputs.OrderBy(x=>x.FormulaVersionId).ThenBy(x=>x.ProfileVersionId).ThenBy(x=>x.GroupId).ThenBy(x=>x.OptionId).ThenBy(x=>x.ItemId)
                .Select(x=>new{x.ItemId,dimension=x.Required.Dimension.ToString(),atomic=x.Required.AtomicUnits,x.FormulaVersionId,x.ProfileVersionId,x.GroupId,x.OptionId,policy=x.Policy.ToString(),x.ExactAtomic.Numerator,x.ExactAtomic.Denominator})
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(contract))));
    }
}
