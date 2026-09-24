using CasaMiga.Domain;
namespace CasaMiga.Application;

public sealed record StoredVersion<T>(T Value, long Revision);
public sealed record FormulaSummary(Guid Id, string Code, string Name, Guid? ActiveVersionId, long Revision);
public sealed record ProfileSummary(Guid Id, Guid ProductId, Guid FinishedItemId, Guid? ActiveVersionId, long Revision);
public sealed class RecipeConcurrencyException() : InvalidOperationException("La versión cambió o ya no es un borrador.");

public sealed record StoredProductionOutput(Guid ProductId, Guid FinishedItemId, Guid ProfileVersionId,
    string ProductName, string ItemName, string ProfileName, long PlannedCount, long? ActualCount, bool ActualPolicyPending);
public sealed record StoredProductionFormula(Guid FormulaVersionId, string Name, InventoryQuantity Required, ProductionRatio Factor);
public sealed record StoredProductionInput(Guid ItemId, string Name, InventoryQuantity Required,
    Guid? FormulaVersionId, Guid? ProfileVersionId, Guid? GroupId, Guid? OptionId, IngredientCountPolicy Policy, ProductionRatio ExactAtomic);
public sealed record StoredProductionBatch(Guid Id, Guid OperationId, string Status, Guid CreatedBy, Guid TerminalId,
    DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt, string? Notes, int ContractVersion, int IdentityVersion,
    string? RequestSha256, Guid? ConsumptionOperationId, Guid? OutputOperationId, string PlanningIssuesJson,
    IReadOnlyList<StoredProductionOutput> Outputs, IReadOnlyList<StoredProductionFormula> Formulas, IReadOnlyList<StoredProductionInput> Inputs,
    string CreatedByName = "", string TerminalName = "");
