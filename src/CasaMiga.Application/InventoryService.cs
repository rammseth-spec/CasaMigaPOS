using System.Text.Json;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed class InventoryService(IInventoryStore inventory, ILocalReadUnitOfWork security,
    UserSession session, IClock clock, Guid terminalId)
{
    public bool Can(string permission) => security.Read(store => session.Current is { } current &&
        store.FindUser(current.Id) is { Active: true } && store.HasPermission(current.Id, permission) &&
        store.FindTerminal(terminalId) is not null);

    public PersistedInventoryOperation RegisterOpeningBalance(RegisterOpeningBalanceCommand command)
    {
        var actor = Authorize("inventory.adjust", administratorOnly: true);
        var (item, capture) = PrepareCapture(command.InventoryItemId, command.LocationId,
            command.Quantity, command.UnitCode, allowZero: true);
        return Apply(new(command.OperationId, actor, terminalId, InventoryMovementKind.OpeningBalance,
            [InventoryOperationLine.Opening(item.Id, command.LocationId, capture)],
            externalReference: command.ExternalReference));
    }

    public PersistedInventoryOperation ReceiveInventory(ReceiveInventoryCommand command)
    {
        var actor = Authorize("inventory.receive");
        var (item, capture) = PrepareCapture(command.InventoryItemId, command.LocationId,
            command.Quantity, command.UnitCode, allowZero: false);
        try
        {
            return Apply(new(command.OperationId, actor, terminalId, InventoryMovementKind.Receipt,
                [InventoryOperationLine.Change(item.Id, command.LocationId,
                    InventoryDelta.Increase(capture.Quantity), capture)], note: command.Note,
                externalReference: command.ExternalReference));
        }
        catch (ArgumentException error) when (error.ParamName == "note" || error.Message.Contains("observación", StringComparison.OrdinalIgnoreCase))
            { throw Error(InventoryError.InvalidNote, error.Message, error); }
    }

    public PersistedInventoryOperation RecordWaste(RecordWasteCommand command)
    {
        var actor = Authorize("inventory.waste");
        var reason = Guard(() => new WasteReasonSelection(command.Reason, command.Note), InventoryError.InvalidNote);
        var (item, capture) = PrepareCapture(command.InventoryItemId, command.LocationId,
            command.Quantity, command.UnitCode, allowZero: false);
        return Apply(new(command.OperationId, actor, terminalId, InventoryMovementKind.Waste,
            [InventoryOperationLine.Change(item.Id, command.LocationId,
                InventoryDelta.Decrease(capture.Quantity), capture)], WasteCode(reason.Code), reason.Note));
    }

    public InventoryAdjustmentResult AdjustToPhysicalCount(AdjustToPhysicalCountCommand command)
    {
        var actor = Authorize("inventory.adjust", administratorOnly: true);
        var reason = Guard(() => new AdjustmentReasonSelection(command.Reason, command.Note), InventoryError.InvalidNote);
        var (item, capture) = PrepareCapture(command.InventoryItemId, command.LocationId,
            command.CountedQuantity, command.UnitCode, allowZero: true);
        if (inventory.FindOperation(command.OperationId) is { } previous)
            return RecoverPhysicalCount(previous, actor, command, capture.Quantity.AtomicUnits, reason);
        EnsureConsistent(item.Id, command.LocationId);
        var current = inventory.FindBalance(item.Id, command.LocationId)
            ?? throw Error(InventoryError.NotInitialized, "La posición no tiene saldo inicial.");
        if (current.Dimension != capture.Quantity.Dimension)
            throw Error(InventoryError.DimensionMismatch, "La dimensión del conteo no coincide con el saldo.");
        if (current.QuantityAtomic == capture.Quantity.AtomicUnits)
            return new(InventoryAdjustmentOutcome.NoChange, null, current);
        var gain = capture.Quantity.AtomicUnits > current.QuantityAtomic;
        var difference = gain
            ? checked(capture.Quantity.AtomicUnits - current.QuantityAtomic)
            : checked(current.QuantityAtomic - capture.Quantity.AtomicUnits);
        var quantity = new InventoryQuantity(item.Dimension, difference);
        var delta = gain ? InventoryDelta.Increase(quantity) : InventoryDelta.Decrease(quantity);
        // The ledger captures the calculated adjustment, expressed exactly in the atomic unit.
        // The caller still supplies only the absolute physical count.
        var deltaCapture = InventoryCapture.Standard(difference, AtomicUnit(item.Dimension));
        var request = new InventoryOperationRequest(command.OperationId, actor, terminalId,
            gain ? InventoryMovementKind.AdjustmentGain : InventoryMovementKind.AdjustmentLoss,
            [InventoryOperationLine.Change(item.Id, command.LocationId, delta, deltaCapture)],
            AdjustmentCode(reason.Code), reason.Note);
        var operation = Apply(request, new(item.Id, command.LocationId, current.QuantityAtomic, current.Version));
        return new(InventoryAdjustmentOutcome.Changed, operation, inventory.FindBalance(item.Id, command.LocationId)
            ?? throw Error(InventoryError.InconsistentBalance, "El saldo confirmado no pudo leerse."));
    }

    public InventoryItem CreateInventoryItem(string code, string name, InventoryItemKind kind,
        InventoryDimension dimension)
    {
        var actor = Authorize("inventory.configure");
        var at = clock.UtcNow.ToUniversalTime();
        var item = Guard(() => new InventoryItem(Guid.NewGuid(), code, name, kind, dimension, true, at, at), InventoryError.Conflict);
        RunStore(() => inventory.CreateItem(item, Audit(actor, "inventory.item.created", item.Id, null, ItemSnapshot(item), at)));
        return item;
    }

    public InventoryItem UpdateInventoryItemMetadata(Guid id, string name, InventoryItemKind kind,
        InventoryDimension dimension)
    {
        var actor = Authorize("inventory.configure");
        var old = RequireItem(id, includeInactive: true);
        var at = clock.UtcNow.ToUniversalTime();
        var next = Guard(() => old.Rename(name, at).Reconfigure(kind, dimension, at), InventoryError.DimensionMismatch);
        if (old.Name == next.Name && old.Kind == next.Kind) return old;
        RunStore(() => inventory.UpdateItem(next, Audit(actor, "inventory.item.updated", id,
            ItemSnapshot(old), ItemSnapshot(next), next.UpdatedAt)));
        return next;
    }

    public InventoryItem SetInventoryItemActive(Guid id, bool active)
    {
        var actor = Authorize("inventory.configure");
        var old = RequireItem(id, includeInactive: true);
        if (old.IsActive == active) return old;
        var at = clock.UtcNow.ToUniversalTime();
        var hasStock = (inventory.FindBalance(id, InventoryLocationIds.Main)?.QuantityAtomic ?? 0) != 0;
        var next = Guard(() => active
            ? new InventoryItem(old.Id, old.Code, old.Name, old.Kind, old.Dimension, true, old.CreatedAt, at)
            : old.Deactivate(hasStock, at), hasStock ? InventoryError.InsufficientStock : InventoryError.Conflict);
        RunStore(() => inventory.UpdateItem(next, Audit(actor, active ? "inventory.item.activated" : "inventory.item.deactivated",
            id, ItemSnapshot(old), ItemSnapshot(next), at)));
        return next;
    }

    public InventoryItemUnit AddInventoryItemUnit(Guid itemId, string code, string name, long atomicUnitsPerWholeUnit)
    {
        var actor = Authorize("inventory.configure");
        var item = RequireItem(itemId);
        var unit = Guard(() => new InventoryItemUnit(Guid.NewGuid(), item, code, name, atomicUnitsPerWholeUnit), InventoryError.InvalidQuantity);
        RunStore(() => inventory.AddItemUnit(unit, Audit(actor, "inventory.unit.added", unit.Id, null,
            new { unit.InventoryItemId, unit.Code, unit.Name, unit.Dimension, unit.AtomicUnitsPerWholeUnit }, clock.UtcNow)));
        return unit;
    }

    public InventoryProductLinkView LinkProductToInventoryItem(Guid productId, Guid itemId, long quantityAtomic)
    {
        var actor = Authorize("inventory.configure");
        var item = RequireItem(itemId);
        var link = Guard(() => new ProductInventoryLink(productId, itemId, new(item.Dimension, quantityAtomic)), InventoryError.InvalidQuantity);
        var linkId = Guid.NewGuid();
        RunStore(() => inventory.LinkProduct(link, Audit(actor, "inventory.product_link.created", linkId, null,
            new { productId, itemId, quantityAtomic, item.Dimension }, clock.UtcNow)));
        return inventory.FindActiveProductLink(productId)
            ?? throw Error(InventoryError.InconsistentBalance, "La asociación creada no pudo leerse.");
    }

    public void UnlinkProductFromInventoryItem(Guid productId)
    {
        var actor = Authorize("inventory.configure");
        var old = inventory.FindActiveProductLink(productId)
            ?? throw Error(InventoryError.ProductNotFound, "El producto no tiene una asociación activa.");
        RunStore(() => inventory.UnlinkProduct(productId, Audit(actor, "inventory.product_link.removed", old.Id,
            old, null, clock.UtcNow)));
    }

    public InventoryListPage List(InventoryListQuery query)
    {
        Authorize("inventory.view"); ValidatePage(query.Page, query.PageSize);
        if (query.Search.Length > 160) throw Error(InventoryError.Conflict, "El filtro admite hasta 160 caracteres.");
        return inventory.QueryItems(query);
    }

    public InventoryItemDetail Detail(Guid id)
    {
        Authorize("inventory.view");
        return inventory.FindItemDetail(id) ?? throw Error(InventoryError.InventoryItemNotFound, "Artículo inexistente.");
    }

    public InventoryMovementPage MovementHistory(InventoryMovementQuery query)
    {
        Authorize("inventory.view"); ValidatePage(query.Page, query.PageSize);
        return inventory.QueryMovements(query);
    }

    public IReadOnlyList<InventoryReconciliationStatus> ReconciliationStatus()
    {
        Authorize("inventory.view");
        return inventory.QueryReconciliation();
    }

    private Guid Authorize(string permission, bool administratorOnly = false) => security.Read(store =>
    {
        if (session.Current is not { } current) throw Error(InventoryError.Unauthorized, "Inicia sesión.");
        var user = store.FindUser(current.Id) ?? throw Error(InventoryError.Unauthorized, "Usuario inexistente.");
        if (!user.Active) throw Error(InventoryError.InactiveUser, "El usuario está inactivo.");
        if (!store.HasPermission(user.Id, permission)) throw Error(InventoryError.Unauthorized, "Permiso insuficiente.");
        if (administratorOnly && !store.HasRole(user.Id, "Administrator"))
            throw Error(InventoryError.Unauthorized, "La operación requiere Administrator.");
        if (store.FindTerminal(terminalId) is null) throw Error(InventoryError.Conflict, "Terminal no configurada.");
        return user.Id;
    });

    private (InventoryItem Item, InventoryCapture Capture) PrepareCapture(Guid itemId, Guid locationId,
        decimal amount, string unitCode, bool allowZero)
    {
        var item = RequireItem(itemId);
        if (inventory.FindLocation(locationId) is not { Active: true })
            throw Error(InventoryError.LocationNotFound, "Ubicación inexistente o inactiva.");
        if (amount < 0 || (!allowZero && amount == 0))
            throw Error(InventoryError.InvalidQuantity, "La cantidad debe ser positiva.");
        try { return (item, Capture(item, amount, unitCode, allowZero)); }
        catch (OverflowException e) { throw Error(InventoryError.InvalidQuantity, "La cantidad excede el rango permitido.", e); }
        catch (ArgumentOutOfRangeException e) { throw Error(InventoryError.InvalidQuantity, e.Message, e); }
        catch (ArgumentException e) { throw Error(InventoryError.InvalidQuantity, e.Message, e); }
    }

    private InventoryCapture Capture(InventoryItem item, decimal amount, string unitCode, bool allowZero)
    {
        if (!allowZero && amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var standard = InventoryUnits.All.SingleOrDefault(x => string.Equals(x.Code, unitCode, StringComparison.Ordinal));
        if (standard is not null)
        {
            if (standard.Dimension != item.Dimension)
                throw Error(InventoryError.DimensionMismatch, "La unidad no corresponde a la dimensión del artículo.");
            return InventoryCapture.Standard(amount, standard);
        }
        var custom = inventory.FindItemUnit(item.Id, unitCode)
            ?? throw Error(InventoryError.InvalidUnit, "Presentación inexistente o inactiva.");
        if (amount != decimal.Truncate(amount) || amount > long.MaxValue)
            throw Error(InventoryError.InvalidQuantity, "La presentación se captura en unidades enteras.");
        if (amount == 0 && allowZero)
            return InventoryCapture.Standard(0, AtomicUnit(item.Dimension));
        return InventoryCapture.Presentation(checked((long)amount), custom);
    }

    private InventoryItem RequireItem(Guid id, bool includeInactive = false)
    {
        var item = inventory.FindItem(id)
            ?? throw Error(InventoryError.InventoryItemNotFound, "Artículo inexistente.");
        if (!includeInactive && !item.IsActive)
            throw Error(InventoryError.InventoryItemInactive, "El artículo está inactivo.");
        return item;
    }

    private void EnsureConsistent(Guid itemId, Guid locationId)
    {
        if (inventory.Reconcile().Any(x => x.InventoryItemId == itemId && x.InventoryLocationId == locationId))
            throw Error(InventoryError.InconsistentBalance, "El saldo no coincide con el ledger.");
    }

    private PersistedInventoryOperation Apply(InventoryOperationRequest request,
        InventoryBalanceExpectation? expectation = null)
    {
        try { return inventory.Apply(request, expectation); }
        catch (InventoryStoreException error) { throw Error(error.Error, error.Message, error); }
        catch (InventoryApplicationException) { throw; }
    }

    private InventoryAdjustmentResult RecoverPhysicalCount(PersistedInventoryOperation previous, Guid actor,
        AdjustToPhysicalCountCommand command, long countedAtomic, AdjustmentReasonSelection reason)
    {
        bool adjustment = previous.Kind is InventoryMovementKind.AdjustmentGain or InventoryMovementKind.AdjustmentLoss;
        var movement = previous.Movements.Count == 1 ? previous.Movements[0] : null;
        if (!adjustment || previous.ActorId != actor || previous.TerminalId != terminalId ||
            previous.ReasonCode != AdjustmentCode(reason.Code) || previous.Note != reason.Note || movement is null ||
            movement.InventoryItemId != command.InventoryItemId || movement.InventoryLocationId != command.LocationId ||
            movement.BalanceAfterAtomic != countedAtomic)
            throw Error(InventoryError.IdempotencyConflict, "El OperationId ya corresponde a otro contenido.");
        var balance = inventory.FindBalance(command.InventoryItemId, command.LocationId)
            ?? throw Error(InventoryError.InconsistentBalance, "El saldo confirmado no pudo leerse.");
        return new(InventoryAdjustmentOutcome.Changed, previous, balance);
    }

    private AuditEntry Audit(Guid actor, string action, Guid entity, object? before, object? after, DateTimeOffset at) =>
        new(Guid.NewGuid(), actor, terminalId, action, entity, at.ToUniversalTime(),
            JsonSerializer.Serialize(new { before, after }));
    private static object ItemSnapshot(InventoryItem item) => new
        { item.Code, item.Name, item.Kind, item.Dimension, item.IsActive };
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 0 || page > 100_000 || pageSize is < 1 or > 100)
            throw Error(InventoryError.Conflict, "Paginación inválida.");
    }
    private static InventoryUnit AtomicUnit(InventoryDimension dimension) => dimension switch
        { InventoryDimension.Mass => InventoryUnits.Milligram, InventoryDimension.Volume => InventoryUnits.Microliter,
          InventoryDimension.Count => InventoryUnits.Piece, _ => throw new ArgumentOutOfRangeException(nameof(dimension)) };
    private static string WasteCode(WasteReason reason) => reason switch
        { WasteReason.Damaged => "DAMAGED", WasteReason.Expired => "EXPIRED", WasteReason.ProductionError => "PRODUCTION_ERROR",
          WasteReason.Spill => "SPILL", WasteReason.Other => "OTHER", _ => throw new ArgumentOutOfRangeException(nameof(reason)) };
    private static string AdjustmentCode(AdjustmentReason reason) => reason switch
        { AdjustmentReason.PhysicalCountVariance => "PHYSICAL_COUNT_VARIANCE", AdjustmentReason.DataCorrection => "DATA_CORRECTION",
          AdjustmentReason.Other => "OTHER", _ => throw new ArgumentOutOfRangeException(nameof(reason)) };
    private static InventoryApplicationException Error(InventoryError code, string message, Exception? inner = null) => new(code, message, inner);
    private static T Guard<T>(Func<T> action, InventoryError code)
    {
        try { return action(); }
        catch (InventoryApplicationException) { throw; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            { throw Error(code, error.Message, error); }
    }
    private static void RunStore(Action action)
    {
        try { action(); }
        catch (InventoryStoreException error) { throw Error(error.Error, error.Message, error); }
    }
}
