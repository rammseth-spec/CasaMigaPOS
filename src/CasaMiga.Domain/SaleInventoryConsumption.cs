namespace CasaMiga.Domain;

public enum SaleInventoryConsumptionOutcome { NoConsumption, ConsumptionRequired }

public enum SaleInventoryPolicyError
{
    InventoryTrackingConfigurationInvalid,
    InventoryNotInitialized,
    InsufficientInventory,
    InventoryItemInactive,
    InventoryConsumptionOverflow
}

public sealed class SaleInventoryPolicyException : Exception
{
    public SaleInventoryPolicyError Error { get; }
    public Guid ProductId { get; }
    public IReadOnlyList<Guid> ProductIds { get; }
    public Guid? InventoryItemId { get; }
    public InventoryDimension? Dimension { get; }
    public long? RequiredAtomic { get; }
    public long? AvailableAtomic { get; }

    internal SaleInventoryPolicyException(SaleInventoryPolicyError error, Guid productId,
        IEnumerable<Guid>? productIds = null, Guid? inventoryItemId = null,
        InventoryDimension? dimension = null, long? requiredAtomic = null,
        long? availableAtomic = null)
        : base(error.ToString())
    {
        Error = error;
        ProductId = productId;
        ProductIds = Array.AsReadOnly((productIds ?? [productId]).Distinct().Order().ToArray());
        InventoryItemId = inventoryItemId;
        Dimension = dimension;
        RequiredAtomic = requiredAtomic;
        AvailableAtomic = availableAtomic;
    }
}

public sealed record ResolvedSaleInventoryLink
{
    public Guid Id { get; }
    public Guid ProductId { get; }
    public Guid InventoryItemId { get; }
    public InventoryDimension Dimension { get; }
    public long QuantityPerProductUnitAtomic { get; }

    public ResolvedSaleInventoryLink(Guid id, Guid productId, Guid inventoryItemId,
        InventoryDimension dimension, long quantityPerProductUnitAtomic)
    {
        if (id == Guid.Empty || productId == Guid.Empty || inventoryItemId == Guid.Empty)
            throw new ArgumentException("La asociación resuelta es inválida.");
        if (!Enum.IsDefined(dimension)) throw new ArgumentOutOfRangeException(nameof(dimension));
        if (quantityPerProductUnitAtomic <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantityPerProductUnitAtomic));
        Id = id;
        ProductId = productId;
        InventoryItemId = inventoryItemId;
        Dimension = dimension;
        QuantityPerProductUnitAtomic = quantityPerProductUnitAtomic;
    }
}

public sealed record SaleInventoryStockState
{
    public Guid InventoryItemId { get; }
    public Guid InventoryLocationId { get; }
    public InventoryDimension Dimension { get; }
    public bool InventoryItemActive { get; }
    public long? AvailableAtomic { get; }
    public bool IsInitialized => AvailableAtomic.HasValue;

    public SaleInventoryStockState(Guid inventoryItemId, Guid inventoryLocationId,
        InventoryDimension dimension, bool inventoryItemActive, long? availableAtomic)
    {
        if (inventoryItemId == Guid.Empty || inventoryLocationId == Guid.Empty)
            throw new ArgumentException("La posición resuelta es inválida.");
        if (!Enum.IsDefined(dimension)) throw new ArgumentOutOfRangeException(nameof(dimension));
        if (availableAtomic < 0) throw new ArgumentOutOfRangeException(nameof(availableAtomic));
        InventoryItemId = inventoryItemId;
        InventoryLocationId = inventoryLocationId;
        Dimension = dimension;
        InventoryItemActive = inventoryItemActive;
        AvailableAtomic = availableAtomic;
    }
}

public sealed record SaleInventoryLineInput
{
    public int Position { get; }
    public Guid ProductId { get; }
    public int SoldQuantity { get; }
    public bool TrackInventory { get; }
    public ResolvedSaleInventoryLink? Link { get; }
    public SaleInventoryStockState? Stock { get; }

    public SaleInventoryLineInput(int position, Guid productId, int soldQuantity,
        bool trackInventory, ResolvedSaleInventoryLink? link = null,
        SaleInventoryStockState? stock = null)
    {
        if (position < 1 || productId == Guid.Empty || soldQuantity < 1)
            throw new ArgumentException("La partida comercial evaluada es inválida.");
        Position = position;
        ProductId = productId;
        SoldQuantity = soldQuantity;
        TrackInventory = trackInventory;
        Link = link;
        Stock = stock;
    }
}

public sealed record SaleInventoryConsumptionLine
{
    public int Position { get; }
    public Guid ProductId { get; }
    public int SoldQuantity { get; }
    public bool TrackInventory { get; }
    public SaleInventoryConsumptionOutcome Outcome { get; }
    public Guid? ProductInventoryLinkId { get; }
    public Guid? InventoryItemId { get; }
    public Guid? InventoryLocationId { get; }
    public InventoryDimension? Dimension { get; }
    public long? QuantityPerProductUnitAtomic { get; }
    public long TotalConsumedAtomic { get; }

    internal SaleInventoryConsumptionLine(SaleInventoryLineInput input,
        SaleInventoryConsumptionOutcome outcome, long consumedAtomic = 0)
    {
        Position = input.Position;
        ProductId = input.ProductId;
        SoldQuantity = input.SoldQuantity;
        TrackInventory = input.TrackInventory;
        Outcome = outcome;
        ProductInventoryLinkId = input.Link?.Id;
        InventoryItemId = input.Link?.InventoryItemId;
        InventoryLocationId = input.Stock?.InventoryLocationId;
        Dimension = input.Link?.Dimension;
        QuantityPerProductUnitAtomic = input.Link?.QuantityPerProductUnitAtomic;
        TotalConsumedAtomic = consumedAtomic;
    }
}

public sealed record SaleInventoryConsumptionAggregate
{
    public Guid InventoryItemId { get; }
    public Guid InventoryLocationId { get; }
    public InventoryDimension Dimension { get; }
    public long TotalConsumedAtomic { get; }
    public long DeltaAtomic => checked(-TotalConsumedAtomic);

    public SaleInventoryConsumptionAggregate(Guid inventoryItemId, Guid inventoryLocationId,
        InventoryDimension dimension, long totalConsumedAtomic)
    {
        if (inventoryItemId == Guid.Empty || inventoryLocationId == Guid.Empty)
            throw new ArgumentException("El agregado de consumo es inválido.");
        if (!Enum.IsDefined(dimension)) throw new ArgumentOutOfRangeException(nameof(dimension));
        if (totalConsumedAtomic <= 0) throw new ArgumentOutOfRangeException(nameof(totalConsumedAtomic));
        InventoryItemId = inventoryItemId;
        InventoryLocationId = inventoryLocationId;
        Dimension = dimension;
        TotalConsumedAtomic = totalConsumedAtomic;
    }
}

public sealed class SaleInventoryConsumptionPlan
{
    public SaleInventoryConsumptionOutcome Outcome { get; }
    public IReadOnlyList<SaleInventoryConsumptionLine> Lines { get; }
    public IReadOnlyList<SaleInventoryConsumptionAggregate> Aggregates { get; }
    public bool RequiresConsumption => Outcome == SaleInventoryConsumptionOutcome.ConsumptionRequired;

    internal SaleInventoryConsumptionPlan(IEnumerable<SaleInventoryConsumptionLine> lines,
        IEnumerable<SaleInventoryConsumptionAggregate> aggregates)
    {
        Lines = Array.AsReadOnly(lines.OrderBy(x => x.Position).ToArray());
        Aggregates = Array.AsReadOnly(aggregates
            .OrderBy(x => x.InventoryLocationId).ThenBy(x => x.InventoryItemId).ToArray());
        Outcome = Aggregates.Count == 0
            ? SaleInventoryConsumptionOutcome.NoConsumption
            : SaleInventoryConsumptionOutcome.ConsumptionRequired;
    }
}

public static class SaleInventoryConsumptionPolicy
{
    public static SaleInventoryConsumptionPlan Evaluate(IEnumerable<SaleInventoryLineInput> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var input = lines.ToArray();
        if (input.Length == 0 || input.Length > 1_000 ||
            input.Select(x => x.Position).Distinct().Count() != input.Length ||
            input.Select(x => x.ProductId).Distinct().Count() != input.Length)
            throw new ArgumentException("La evaluación requiere partidas únicas y no vacías.", nameof(lines));

        var details = new List<SaleInventoryConsumptionLine>(input.Length);
        var availability = new Dictionary<(Guid Location, Guid Item), SaleInventoryStockState>();
        foreach (var line in input.OrderBy(x => x.Position))
        {
            if (line.Link is null)
            {
                if (line.TrackInventory) throw Policy(SaleInventoryPolicyError.InventoryTrackingConfigurationInvalid, line);
                if (line.Stock is not null) throw Policy(SaleInventoryPolicyError.InventoryTrackingConfigurationInvalid, line);
                details.Add(new(line, SaleInventoryConsumptionOutcome.NoConsumption));
                continue;
            }
            if (!line.TrackInventory || line.Link.ProductId != line.ProductId || line.Stock is null ||
                line.Stock.InventoryItemId != line.Link.InventoryItemId || line.Stock.Dimension != line.Link.Dimension)
                throw Policy(SaleInventoryPolicyError.InventoryTrackingConfigurationInvalid, line);
            if (!line.Stock.InventoryItemActive)
                throw Policy(SaleInventoryPolicyError.InventoryItemInactive, line);
            if (!line.Stock.IsInitialized)
                throw Policy(SaleInventoryPolicyError.InventoryNotInitialized, line);

            long consumed;
            try { consumed = checked((long)line.SoldQuantity * line.Link.QuantityPerProductUnitAtomic); }
            catch (OverflowException) { throw Policy(SaleInventoryPolicyError.InventoryConsumptionOverflow, line); }
            details.Add(new(line, SaleInventoryConsumptionOutcome.ConsumptionRequired, consumed));

            var key = (line.Stock.InventoryLocationId, line.Stock.InventoryItemId);
            if (availability.TryGetValue(key, out var previous) &&
                (previous.Dimension != line.Stock.Dimension || previous.AvailableAtomic != line.Stock.AvailableAtomic ||
                 previous.InventoryItemActive != line.Stock.InventoryItemActive))
                throw Policy(SaleInventoryPolicyError.InventoryTrackingConfigurationInvalid, line);
            availability[key] = line.Stock;
        }

        var aggregates = new List<SaleInventoryConsumptionAggregate>();
        foreach (var group in details.Where(x => x.Outcome == SaleInventoryConsumptionOutcome.ConsumptionRequired)
                     .GroupBy(x => (Location: x.InventoryLocationId!.Value, Item: x.InventoryItemId!.Value))
                     .OrderBy(x => x.Key.Location).ThenBy(x => x.Key.Item))
        {
            long total = 0;
            try { foreach (var detail in group) total = checked(total + detail.TotalConsumedAtomic); }
            catch (OverflowException)
            {
                var failed = group.OrderBy(x => x.Position).First();
                throw Policy(SaleInventoryPolicyError.InventoryConsumptionOverflow, failed,
                    group.Select(x => x.ProductId));
            }
            var stock = availability[group.Key];
            if (total > stock.AvailableAtomic!.Value)
            {
                var failed = group.OrderBy(x => x.Position).First();
                throw new SaleInventoryPolicyException(SaleInventoryPolicyError.InsufficientInventory,
                    failed.ProductId, group.Select(x => x.ProductId), group.Key.Item, stock.Dimension,
                    total, stock.AvailableAtomic.Value);
            }
            aggregates.Add(new(group.Key.Item, group.Key.Location, stock.Dimension, total));
        }
        return new(details, aggregates);
    }

    private static SaleInventoryPolicyException Policy(SaleInventoryPolicyError error,
        SaleInventoryLineInput line, IEnumerable<Guid>? products = null) =>
        new(error, line.ProductId, products, line.Link?.InventoryItemId,
            line.Link?.Dimension, availableAtomic: line.Stock?.AvailableAtomic);

    private static SaleInventoryPolicyException Policy(SaleInventoryPolicyError error,
        SaleInventoryConsumptionLine line, IEnumerable<Guid>? products = null) =>
        new(error, line.ProductId, products, line.InventoryItemId, line.Dimension);
}

public static class SaleConsumptionOperationFactory
{
    public static InventoryOperationRequest Create(Guid operationId, Guid actorId, Guid terminalId,
        SaleInventoryConsumptionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.RequiresConsumption) throw new ArgumentException("El plan no requiere consumo.", nameof(plan));
        var lines = plan.Aggregates.Select(aggregate =>
        {
            var unit = aggregate.Dimension switch
            {
                InventoryDimension.Mass => InventoryUnits.Milligram,
                InventoryDimension.Volume => InventoryUnits.Microliter,
                InventoryDimension.Count => InventoryUnits.Piece,
                _ => throw new ArgumentOutOfRangeException(nameof(plan))
            };
            var capture = InventoryCapture.Standard(aggregate.TotalConsumedAtomic, unit);
            return InventoryOperationLine.Change(aggregate.InventoryItemId, aggregate.InventoryLocationId,
                InventoryDelta.Decrease(capture.Quantity), capture);
        });
        return InventoryOperationRequest.CreateSaleConsumption(operationId, actorId, terminalId, lines);
    }
}
