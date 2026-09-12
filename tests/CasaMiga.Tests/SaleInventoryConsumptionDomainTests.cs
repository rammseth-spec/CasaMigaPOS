using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class SaleInventoryConsumptionDomainTests
{
    private static readonly Guid Main = Id(900);
    private static Guid Id(int value) => Guid.Parse($"10000000-0000-0000-0000-{value:D12}");

    private static SaleInventoryLineInput Tracked(int position, int product, int item,
        int sold = 1, long factor = 1, long? available = 100, bool active = true,
        InventoryDimension dimension = InventoryDimension.Count, Guid? location = null)
    {
        var productId = Id(product);
        var itemId = Id(item);
        return new(position, productId, sold, true,
            new(Id(product + 500), productId, itemId, dimension, factor),
            new(itemId, location ?? Main, dimension, active, available));
    }

    [TestMethod]
    public void UntrackedProductWithoutLinkProducesNoConsumption()
    {
        var plan = SaleInventoryConsumptionPolicy.Evaluate([new(1, Id(1), 3, false)]);
        Assert.AreEqual(SaleInventoryConsumptionOutcome.NoConsumption, plan.Outcome);
        Assert.IsFalse(plan.RequiresConsumption);
        Assert.IsEmpty(plan.Aggregates);
        var line = plan.Lines.Single();
        Assert.AreEqual(SaleInventoryConsumptionOutcome.NoConsumption, line.Outcome);
        Assert.AreEqual(3, line.SoldQuantity);
        Assert.IsNull(line.ProductInventoryLinkId);
        Assert.AreEqual(0L, line.TotalConsumedAtomic);
    }

    [TestMethod]
    public void TrackingIntentAndLinkMustBeCoherent()
    {
        var missing = Assert.ThrowsExactly<SaleInventoryPolicyException>(() =>
            SaleInventoryConsumptionPolicy.Evaluate([new(1, Id(1), 1, true)]));
        Assert.AreEqual(SaleInventoryPolicyError.InventoryTrackingConfigurationInvalid, missing.Error);
        Assert.AreEqual(Id(1), missing.ProductId);

        var tracked = Tracked(1, 2, 20);
        var incoherent = new SaleInventoryLineInput(tracked.Position, tracked.ProductId,
            tracked.SoldQuantity, false, tracked.Link, tracked.Stock);
        Assert.AreEqual(SaleInventoryPolicyError.InventoryTrackingConfigurationInvalid,
            Assert.ThrowsExactly<SaleInventoryPolicyException>(() =>
                SaleInventoryConsumptionPolicy.Evaluate([incoherent])).Error);
    }

    [TestMethod]
    public void InactiveAndUninitializedPositionsHaveStableErrors()
    {
        var inactive = Assert.ThrowsExactly<SaleInventoryPolicyException>(() =>
            SaleInventoryConsumptionPolicy.Evaluate([Tracked(1, 1, 10, active: false)]));
        Assert.AreEqual(SaleInventoryPolicyError.InventoryItemInactive, inactive.Error);
        Assert.AreEqual(Id(10), inactive.InventoryItemId);

        var uninitialized = Assert.ThrowsExactly<SaleInventoryPolicyException>(() =>
            SaleInventoryConsumptionPolicy.Evaluate([Tracked(1, 1, 10, available: null)]));
        Assert.AreEqual(SaleInventoryPolicyError.InventoryNotInitialized, uninitialized.Error);
        Assert.IsNull(uninitialized.AvailableAtomic);
    }

    [TestMethod]
    public void ExactStockSucceedsAndInsufficientStockIsStructured()
    {
        var exact = SaleInventoryConsumptionPolicy.Evaluate([Tracked(1, 1, 10, sold: 4, factor: 3, available: 12)]);
        Assert.AreEqual(12L, exact.Aggregates.Single().TotalConsumedAtomic);

        var insufficient = Assert.ThrowsExactly<SaleInventoryPolicyException>(() =>
            SaleInventoryConsumptionPolicy.Evaluate([Tracked(1, 1, 10, sold: 5, factor: 3, available: 14)]));
        Assert.AreEqual(SaleInventoryPolicyError.InsufficientInventory, insufficient.Error);
        Assert.AreEqual(Id(1), insufficient.ProductId);
        Assert.AreEqual(Id(10), insufficient.InventoryItemId);
        Assert.AreEqual(InventoryDimension.Count, insufficient.Dimension);
        Assert.AreEqual(15L, insufficient.RequiredAtomic);
        Assert.AreEqual(14L, insufficient.AvailableAtomic);
    }

    [TestMethod]
    public void MultiplicationAndAggregationAreChecked()
    {
        var exact = SaleInventoryConsumptionPolicy.Evaluate([Tracked(1, 1, 10, sold: 12, factor: 6, available: 72)]);
        Assert.AreEqual(72L, exact.Lines.Single().TotalConsumedAtomic);

        var multiplication = Assert.ThrowsExactly<SaleInventoryPolicyException>(() =>
            SaleInventoryConsumptionPolicy.Evaluate([Tracked(1, 1, 10, sold: 2, factor: long.MaxValue, available: long.MaxValue)]));
        Assert.AreEqual(SaleInventoryPolicyError.InventoryConsumptionOverflow, multiplication.Error);

        var half = long.MaxValue / 2 + 1;
        var aggregation = Assert.ThrowsExactly<SaleInventoryPolicyException>(() =>
            SaleInventoryConsumptionPolicy.Evaluate([
                Tracked(1, 1, 10, factor: half, available: long.MaxValue),
                Tracked(2, 2, 10, factor: half, available: long.MaxValue)]));
        Assert.AreEqual(SaleInventoryPolicyError.InventoryConsumptionOverflow, aggregation.Error);
        CollectionAssert.AreEquivalent(new[] { Id(1), Id(2) }, aggregation.ProductIds.ToArray());
    }

    [TestMethod]
    public void SameItemIsAggregatedWhileLineSnapshotsRemainSeparate()
    {
        var untracked = new SaleInventoryLineInput(3, Id(3), 4, false);
        var plan = SaleInventoryConsumptionPolicy.Evaluate([
            Tracked(2, 2, 10, sold: 3, factor: 5, available: 100),
            untracked,
            Tracked(1, 1, 10, sold: 2, factor: 4, available: 100)]);

        Assert.AreEqual(SaleInventoryConsumptionOutcome.ConsumptionRequired, plan.Outcome);
        Assert.AreEqual(3, plan.Lines.Count);
        Assert.AreEqual(23L, plan.Aggregates.Single().TotalConsumedAtomic);
        Assert.AreEqual(-23L, plan.Aggregates.Single().DeltaAtomic);
        Assert.AreEqual(8L, plan.Lines[0].TotalConsumedAtomic);
        Assert.AreEqual(15L, plan.Lines[1].TotalConsumedAtomic);
        Assert.AreEqual(SaleInventoryConsumptionOutcome.NoConsumption, plan.Lines[2].Outcome);
    }

    [TestMethod]
    public void AggregateOrderDoesNotDependOnCartOrder()
    {
        var a = Tracked(1, 1, 30, location: Id(902));
        var b = Tracked(2, 2, 20, location: Id(901));
        var c = Tracked(3, 3, 10, location: Id(901));
        var forward = SaleInventoryConsumptionPolicy.Evaluate([a, b, c]);
        var reverse = SaleInventoryConsumptionPolicy.Evaluate([c, b, a]);
        var expected = forward.Aggregates.Select(x => (x.InventoryLocationId, x.InventoryItemId)).ToArray();
        CollectionAssert.AreEqual(expected, reverse.Aggregates.Select(x => (x.InventoryLocationId, x.InventoryItemId)).ToArray());
        CollectionAssert.AreEqual(new[] { (Id(901), Id(10)), (Id(901), Id(20)), (Id(902), Id(30)) }, expected);
    }

    [TestMethod]
    public void SaleConsumptionCanOnlyBeCreatedFromAValidatedPlan()
    {
        var tracked = Tracked(1, 1, 10, sold: 2, factor: 3, available: 10);
        var plan = SaleInventoryConsumptionPolicy.Evaluate([tracked]);
        var request = SaleConsumptionOperationFactory.Create(Id(700), Id(701), Id(702), plan);
        Assert.AreEqual(InventoryMovementKind.SaleConsumption, request.Kind);
        Assert.AreEqual(-6L, request.Lines.Single().DeltaAtomic);
        Assert.AreEqual("piece", request.Lines.Single().Capture.UnitCode);
        Assert.IsNull(request.ReasonCode);

        var positiveCapture = InventoryCapture.Standard(1m, InventoryUnits.Piece);
        var positive = InventoryOperationLine.Change(Id(10), Main,
            InventoryDelta.Increase(positiveCapture.Quantity), positiveCapture);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new InventoryOperationRequest(
            Id(703), Id(701), Id(702), InventoryMovementKind.SaleConsumption, [positive]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SaleInventoryConsumptionAggregate(Id(10), Main, InventoryDimension.Count, 0));
        var noConsumption = SaleInventoryConsumptionPolicy.Evaluate([new(1, Id(1), 1, false)]);
        Assert.ThrowsExactly<ArgumentException>(() =>
            SaleConsumptionOperationFactory.Create(Id(704), Id(701), Id(702), noConsumption));
    }

    [TestMethod]
    public void PlanCopiesLinkValuesAndCannotAcquireASaleIdentity()
    {
        var input = Tracked(1, 1, 10, sold: 2, factor: 4, available: 20);
        var plan = SaleInventoryConsumptionPolicy.Evaluate([input]);
        input = Tracked(1, 1, 99, sold: 2, factor: 9, available: 20);
        var snapshot = plan.Lines.Single();
        Assert.AreEqual(Id(10), snapshot.InventoryItemId);
        Assert.AreEqual(4L, snapshot.QuantityPerProductUnitAtomic);
        Assert.AreEqual(8L, snapshot.TotalConsumedAtomic);
        Assert.IsNull(typeof(SaleInventoryConsumptionLine).GetProperty("SaleId"));
    }

    [TestMethod]
    public void DomainHasNoSqliteDependencyAndPhase1HGateRemainsClosed()
    {
        Assert.IsFalse(typeof(SaleInventoryConsumptionPlan).Assembly.GetReferencedAssemblies()
            .Any(x => x.Name == "Microsoft.Data.Sqlite"));
        Assert.IsFalse(InventoryMovementKinds.IsEnabledInPhase1H(InventoryMovementKind.SaleConsumption));
    }
}
