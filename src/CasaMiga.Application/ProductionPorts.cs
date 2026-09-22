using CasaMiga.Domain;
namespace CasaMiga.Application;

public interface IRecipeStore
{
    void CreateFormula(Formula formula,string name,DateTimeOffset at);
    FormulaSummary? GetFormula(Guid id);
    IReadOnlyList<FormulaSummary> ListFormulas(int limit=50,int offset=0);
    void SaveFormulaDraft(FormulaVersion value,long? expectedRevision=null);
    StoredVersion<FormulaVersion>? GetFormulaVersion(Guid id);
    IReadOnlyList<StoredVersion<FormulaVersion>> ListFormulaVersions(Guid id);
    void PublishFormula(Guid id,long expectedRevision,DateTimeOffset at);
    void ActivateFormula(Guid id,Guid? versionId,long expectedRevision);
    void CreateProfile(ProductProductionProfile profile,DateTimeOffset at);
    ProfileSummary? GetProfile(Guid id);
    IReadOnlyList<ProfileSummary> ListProfiles(int limit=50,int offset=0);
    void SaveProfileDraft(ProductProductionProfileVersion value,long? expectedRevision=null);
    StoredVersion<ProductProductionProfileVersion>? GetProfileVersion(Guid id);
    IReadOnlyList<StoredVersion<ProductProductionProfileVersion>> ListProfileVersions(Guid id);
    void PublishProfile(Guid id,long expectedRevision,DateTimeOffset at);
    void ActivateProfile(Guid id,Guid? versionId,long expectedRevision);
}

public sealed record ProductionStock(Guid ItemId,InventoryDimension Dimension,bool Active,long? Available);
public sealed record ProductionShortage(Guid ItemId,long Required,long? Available);
public sealed record RequestedProductionOutput(Guid ProfileVersionId,long PlannedCount);
public sealed record PrepareProductionCommand(IReadOnlyList<RequestedProductionOutput> Outputs,IReadOnlyList<ProductionAlternativeSelection> Selections);
public sealed record CreateProductionCommand(Guid BatchId,Guid OperationId,PrepareProductionCommand Plan,string? Notes=null);
public sealed record ConfirmProductionCommand(Guid BatchId,Guid OperationId,string ExpectedRevision,IReadOnlyList<ActualOutputLine> Outputs);
public sealed record ProductionPreview(ProductionPlanningResult Planning,IReadOnlyList<ProductionStock> Stock,IReadOnlyList<ProductionShortage> Shortages,IReadOnlyList<Guid> InactiveItems)
{
    public bool CanConfirm => Planning.IsResolved && Shortages.Count==0 && InactiveItems.Count==0 && Stock.All(x=>x.Available.HasValue);
}
public enum ProductionFailure { NotFound, InvalidRequest, RevisionConflict, IdempotencyConflict, UnresolvedPlan, InsufficientStock, InactiveItem, NotInitialized, ConcurrencyConflict, RecoveryInconsistent }
public sealed class ProductionException(ProductionFailure code,IReadOnlyList<ProductionShortage>? shortages=null) : Exception(code.ToString())
{
    public ProductionFailure Code { get; }=code;
    public IReadOnlyList<ProductionShortage> Shortages { get; }=shortages??[];
}
public interface IProductionUnitOfWork
{
    T Run<T>(Func<IProductionSession,T> action);
}
public interface IProductionSession
{
    IRecipeStore Recipes { get; }
    bool Authorized(Guid actor,Guid terminal,string permission);
    StoredProductionBatch? Find(Guid batchId);
    StoredProductionBatch? FindOperation(Guid operationId);
    IReadOnlyList<Guid> List(int limit,int offset);
    ProductionStock Stock(Guid item);
    StoredProductionBatch Create(CreateProductionCommand command,Guid actor,Guid terminal,DateTimeOffset at);
    StoredProductionBatch Complete(StoredProductionBatch batch,ConfirmProductionCommand command,string hash,DateTimeOffset at);
    void ValidateRecovery(StoredProductionBatch batch);
    void Audit(Guid actor,Guid terminal,string action,Guid entity,DateTimeOffset at);
}
