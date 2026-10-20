using CasaMiga.Domain;
using CasaMiga.Application;
using System.Text.Json;

namespace CasaMiga.Infrastructure;

public sealed partial class SqliteRecipeStore
{
    /// <summary>Persists a draft from exact published versions. No stock check, reservation, inventory write or confirmation.</summary>
    public void CreateProductionDraft(Guid id, Guid operationId, Guid actorId, Guid terminalId, DateTimeOffset at,
        IEnumerable<PlannedOutputLine> outputs, IEnumerable<ProductionAlternativeSelection>? selections = null,
        IReadOnlyDictionary<Guid,long>? actualCounts = null, string? notes = null)
    {
        if (id==Guid.Empty || operationId==Guid.Empty || actorId==Guid.Empty || terminalId==Guid.Empty)
            throw new ArgumentException("Identidad vacía.");
        var requested = outputs.ToArray();
        var choices = selections?.ToArray() ?? [];
        var actual = actualCounts is null ? new Dictionary<Guid,long>() : new Dictionary<Guid,long>(actualCounts);
        if (actual.Any(x => x.Value<0))
            throw new ArgumentException("Cantidad real inválida o producto ajeno al plan.");
        Write((c,t) =>
        {
            // Reload exact version IDs so caller-supplied mutable definitions cannot forge a historical snapshot.
            var lines = requested.Select(o => new PlannedOutputLine(
                (ReadProfile(c,t,o.Profile.Id) ?? throw new KeyNotFoundException()).Value,o.PlannedCount)).ToArray();
            if (actual.Keys.Any(key => !lines.Any(line => line.ProductId==key)))
                throw new ArgumentException("Cantidad real de un producto ajeno a los perfiles persistidos.");
            var formulas = ProductionPlan.RequiredVersions(lines.SelectMany(o => o.Profile.Formulas).Select(f => f.FormulaVersionId),
                f => (ReadFormula(c,t,f) ?? throw new KeyNotFoundException()).Value);
            var result = ProductionPlan.Calculate(lines,formulas,choices);
            var fatal = result.Issues.FirstOrDefault(i => i.Code is not (ProductionError.NonRepresentableScale or ProductionError.CountPolicyRequired or ProductionError.AlternativeNotEligible));
            if (fatal is not null) throw new ProductionDomainException(fatal.Code,fatal.SubjectId);
            Execute(c,t,"INSERT INTO production_batch(id,operation_id,status,created_by,terminal_id,created_utc,notes,contract_version,identity_version,planning_issues_json) VALUES($0,$1,'DRAFT',$2,$3,$4,$5,1,1,$6)",id,operationId,actorId,terminalId,At(at),notes,JsonSerializer.Serialize(result.Issues));
            int sequence=0;
            foreach (var line in lines.OrderBy(x => x.ProductId))
            {
                var names = Rows(c,t,"SELECT p.name,i.name FROM product p,inventory_item i WHERE p.id=$0 AND i.id=$1",line.ProductId,line.FinishedInventoryItemId).Single();
                long? count=actual.TryGetValue(line.ProductId,out var value) ? value : null;
                Execute(c,t,"INSERT INTO production_output_plan VALUES($0,$1,$2,$3,$4,$5,$6,$7,$8,$9,$10)",
                    id,++sequence,line.ProductId,line.FinishedInventoryItemId,line.Profile.Id,S(names,0),S(names,1),line.Profile.Name,line.PlannedCount,count,
                    count.HasValue && (count==0 || count>line.PlannedCount) ? 1 : 0);
            }
            foreach (var requirement in result.RequiredFormulas)
            {
                var formula=formulas.Single(f=>f.Id==requirement.FormulaVersionId);
                var factor=ProductionScaling.Factor(requirement.Required,formula.BaseYield);
                Execute(c,t,"INSERT INTO production_formula_snapshot VALUES($0,$1,$2,$3,$4,$5,$6)",id,formula.Id,formula.Name,Dimension(requirement.Required.Dimension),requirement.Required.AtomicUnits,factor.Numerator,factor.Denominator);
            }
            // An unresolved plan remains an explicit draft; never invent rounded inputs.
            if (!result.IsResolved) return;
            var plan=result.Plan!;
            sequence=0;
            foreach (var formula in plan.Formulas)
                foreach (var ingredient in formula.Ingredients)
                    Input(ingredient,formula.Version.Id,null);
            foreach (var line in plan.Outputs)
                foreach (var direct in line.Profile.DirectIngredients)
                {
                    var ingredient=direct.Ingredient;
                    var scaled=ProductionScaling.Scale(ingredient.Quantity,new(line.PlannedCount,1),ingredient.CountPolicy);
                    Input(new(ingredient.InventoryItemId,scaled.Quantity!.Value,scaled.ExactAtomic,ingredient.CountPolicy),null,line.Profile.Id);
                }
            void Input(ScaledIngredient line, Guid? formulaId, Guid? profileId)
            {
                var name=Rows(c,t,"SELECT name FROM inventory_item WHERE id=$0",line.InventoryItemId).Single();
                Execute(c,t,"INSERT INTO production_input_snapshot VALUES($0,$1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)",
                    id,++sequence,line.InventoryItemId,S(name,0),Dimension(line.Quantity.Dimension),line.Quantity.AtomicUnits,
                    formulaId.HasValue ? "FORMULA" : "DIRECT",formulaId,profileId,line.GroupId,line.OptionId,Policy(line.CountPolicy),line.ExactAtomic.Numerator,line.ExactAtomic.Denominator);
            }
        });
    }

    public StoredProductionBatch? GetProductionBatch(Guid id) => Read((c,t) =>
    {
        var r=Rows(c,t,"SELECT b.operation_id,b.status,b.created_by,b.terminal_id,b.created_utc,b.confirmed_utc,b.notes,b.contract_version,b.identity_version,b.request_sha256,b.consumption_operation_id,b.output_operation_id,b.planning_issues_json,u.display_name,t.name FROM production_batch b JOIN app_user u ON u.id=b.created_by JOIN terminal t ON t.id=b.terminal_id WHERE b.id=$0",id).SingleOrDefault();
        if (r is null) return null;
        var outputs=Rows(c,t,"SELECT product_id,finished_item_id,profile_version_id,product_name,item_name,profile_name,planned_count,actual_count,actual_policy_pending FROM production_output_plan WHERE batch_id=$0 ORDER BY sequence",id)
            .Select(x => new StoredProductionOutput(G(x,0),G(x,1),G(x,2),S(x,3),S(x,4),S(x,5),L(x,6),x[7] is DBNull ? null : L(x,7),L(x,8)==1)).ToArray();
        var formulas=Rows(c,t,"SELECT formula_version_id,formula_name,dimension,required_atomic,numerator,denominator FROM production_formula_snapshot WHERE batch_id=$0 ORDER BY formula_version_id",id)
            .Select(x => new StoredProductionFormula(G(x,0),S(x,1),Quantity(x,2,3),new(L(x,4),L(x,5)))).ToArray();
        var inputs=Rows(c,t,"SELECT item_id,item_name,dimension,required_atomic,formula_version_id,profile_version_id,group_id,option_id,count_policy,exact_numerator,exact_denominator FROM production_input_snapshot WHERE batch_id=$0 ORDER BY sequence",id)
            .Select(x => new StoredProductionInput(G(x,0),S(x,1),Quantity(x,2,3),NG(x,4),NG(x,5),NG(x,6),NG(x,7),ParsePolicy(S(x,8)),new(L(x,9),L(x,10)))).ToArray();
        return new StoredProductionBatch(id,G(r,0),S(r,1),G(r,2),G(r,3),Date(r,4),r[5] is DBNull ? null : Date(r,5),r[6] as string,
            checked((int)L(r,7)),checked((int)L(r,8)),r[9] as string,NG(r,10),NG(r,11),S(r,12),Array.AsReadOnly(outputs),Array.AsReadOnly(formulas),Array.AsReadOnly(inputs),S(r,13),S(r,14));
    });

    public IReadOnlyList<Guid> ListProductionBatchIds(int limit=50,int offset=0) => Read((c,t) =>
        Rows(c,t,"SELECT id FROM production_batch ORDER BY created_utc DESC,id LIMIT $0 OFFSET $1",Limit(limit),Offset(offset)).Select(x => G(x,0)).ToArray());
}
