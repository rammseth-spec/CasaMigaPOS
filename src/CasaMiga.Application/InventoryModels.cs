using CasaMiga.Domain;

namespace CasaMiga.Application;

public enum InventoryError
{
    Unauthorized, InactiveUser, InvalidUnit, DimensionMismatch, InvalidQuantity, InvalidNote,
    NotInitialized, AlreadyInitialized, InsufficientStock, IdempotencyConflict,
    InventoryItemNotFound, InventoryItemInactive, NoChange, ConcurrencyConflict,
    InconsistentBalance, LocationNotFound, ProductNotFound, Conflict
}

public class InventoryApplicationException(InventoryError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public InventoryError Error { get; } = error;
}

public class InventoryStoreException(InventoryError error, string message, Exception? inner = null)
    : InventoryApplicationException(error, message, inner);

public sealed record PersistedInventoryMovement(Guid Id, int Sequence, Guid InventoryItemId,
    Guid InventoryLocationId, InventoryDimension Dimension, bool IsOpening, long DeltaAtomic,
    long BalanceBeforeAtomic, long BalanceAfterAtomic, string CaptureUnitCode,
    string CapturedQuantity, long CaptureAtomicFactor);

public sealed record PersistedInventoryOperation(Guid OperationId, string RequestSha256,
    InventoryMovementKind Kind, Guid ActorId, Guid TerminalId, string? ReasonCode, string? Note,
    string? ExternalReference, DateTimeOffset ConfirmedAt, IReadOnlyList<PersistedInventoryMovement> Movements);

public sealed record InventoryBalanceRecord(Guid InventoryItemId, Guid InventoryLocationId,
    InventoryDimension Dimension, long QuantityAtomic, long Version, DateTimeOffset UpdatedAt);
public sealed record InventoryBalanceExpectation(Guid InventoryItemId, Guid InventoryLocationId,
    long QuantityAtomic, long Version);

public sealed record InventoryReconciliationDifference(Guid InventoryItemId, Guid InventoryLocationId,
    long LedgerQuantityAtomic, long? BalanceQuantityAtomic);

public sealed record InventoryLocationView(Guid Id, string Code, string Name, bool Active);
public sealed record InventoryProductLinkView(Guid Id, Guid ProductId, Guid InventoryItemId,
    InventoryDimension Dimension, long QuantityAtomic, bool Active);
public sealed record InventoryListQuery(string Search = "", InventoryItemKind? Kind = null,
    bool? Active = true, int Page = 0, int PageSize = 50);
public sealed record InventoryListRow(Guid InventoryItemId, string Code, string Name,
    InventoryItemKind Kind, InventoryDimension Dimension, bool Active, long? MainBalanceAtomic,
    string AtomicUnit, bool HasProductLink);
public sealed record InventoryListPage(IReadOnlyList<InventoryListRow> Items, int Total, int Page, int PageSize);
public sealed record InventoryItemDetail(InventoryItem Item, InventoryLocationView MainLocation,
    InventoryBalanceRecord? Balance, IReadOnlyList<InventoryItemUnit> Units,
    IReadOnlyList<InventoryProductLinkView> ProductLinks, string AtomicUnit);
public sealed record InventoryMovementQuery(Guid? InventoryItemId = null, int Page = 0, int PageSize = 50);
public sealed record InventoryMovementRow(DateTimeOffset OccurredAt, InventoryMovementKind Kind,
    Guid InventoryItemId, string InventoryItemName, Guid LocationId, string LocationName,
    long DeltaAtomic, long BalanceBeforeAtomic, long BalanceAfterAtomic, Guid ActorId,
    string ActorName, Guid TerminalId, string TerminalName, string? ReasonCode, string? Note,
    string? ExternalReference, Guid OperationId);
public sealed record InventoryMovementPage(IReadOnlyList<InventoryMovementRow> Items, int Total, int Page, int PageSize);
public sealed record InventoryReconciliationStatus(Guid InventoryItemId, Guid InventoryLocationId,
    long MaterializedAtomic, long ReconstructedAtomic, bool Consistent);

public sealed record RegisterOpeningBalanceCommand(Guid OperationId, Guid InventoryItemId,
    Guid LocationId, decimal Quantity, string UnitCode, string? ExternalReference = null);
public sealed record ReceiveInventoryCommand(Guid OperationId, Guid InventoryItemId,
    Guid LocationId, decimal Quantity, string UnitCode, string? ExternalReference = null, string? Note = null);
public sealed record RecordWasteCommand(Guid OperationId, Guid InventoryItemId,
    Guid LocationId, decimal Quantity, string UnitCode, WasteReason Reason, string? Note = null);
public sealed record AdjustToPhysicalCountCommand(Guid OperationId, Guid InventoryItemId,
    Guid LocationId, decimal CountedQuantity, string UnitCode, AdjustmentReason Reason, string? Note = null);

public enum InventoryAdjustmentOutcome { Changed, NoChange }
public sealed record InventoryAdjustmentResult(InventoryAdjustmentOutcome Outcome,
    PersistedInventoryOperation? Operation, InventoryBalanceRecord Balance)
{
    public bool Changed => Outcome == InventoryAdjustmentOutcome.Changed;
}

public interface IInventoryStore
{
    InventoryItem? FindItem(Guid id);
    InventoryItemUnit? FindItemUnit(Guid itemId, string code);
    InventoryLocationView? FindLocation(Guid id);
    InventoryBalanceRecord? FindBalance(Guid itemId, Guid locationId);
    PersistedInventoryOperation Apply(InventoryOperationRequest request, InventoryBalanceExpectation? expectation = null);
    PersistedInventoryOperation? FindOperation(Guid operationId);
    PersistedInventoryOperation? Recover(InventoryOperationRequest request);
    IReadOnlyList<InventoryReconciliationDifference> Reconcile();
    void CreateItem(InventoryItem item, AuditEntry audit);
    void UpdateItem(InventoryItem item, AuditEntry audit);
    void AddItemUnit(InventoryItemUnit unit, AuditEntry audit);
    InventoryProductLinkView? FindActiveProductLink(Guid productId);
    void LinkProduct(ProductInventoryLink link, AuditEntry audit);
    void UnlinkProduct(Guid productId, AuditEntry audit);
    InventoryListPage QueryItems(InventoryListQuery query);
    InventoryItemDetail? FindItemDetail(Guid id);
    InventoryMovementPage QueryMovements(InventoryMovementQuery query);
    IReadOnlyList<InventoryReconciliationStatus> QueryReconciliation();
}

public static class InventoryLocationIds
{
    public static readonly Guid Main = Guid.Parse("10000000-0000-0000-0000-000000000001");
}
