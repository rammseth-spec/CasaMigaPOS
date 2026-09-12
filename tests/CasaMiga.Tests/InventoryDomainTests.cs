using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class InventoryDomainTests
{
    [TestMethod]
    public void ApprovedMassConversionsAreExact()
    {
        Assert.AreEqual(25_000_000L, InventoryQuantity.From(25m, InventoryUnits.Kilogram).AtomicUnits);
        Assert.AreEqual(3_250_000L, InventoryQuantity.From(3.250m, InventoryUnits.Kilogram).AtomicUnits);
        Assert.AreEqual(1_000L, InventoryQuantity.From(1m, InventoryUnits.Gram).AtomicUnits);
    }

    [TestMethod]
    public void ApprovedVolumeAndCountConversionsAreExact()
    {
        Assert.AreEqual(1_000_000L, InventoryQuantity.From(1m, InventoryUnits.Liter).AtomicUnits);
        Assert.AreEqual(250_000L, InventoryQuantity.From(250m, InventoryUnits.Milliliter).AtomicUnits);
        Assert.AreEqual(100L, InventoryQuantity.From(100m, InventoryUnits.Piece).AtomicUnits);
    }

    [TestMethod]
    public void FractionNotRepresentableInAtomicUnitIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => InventoryQuantity.From(0.0001m, InventoryUnits.Gram));
        Assert.ThrowsExactly<ArgumentException>(() => InventoryQuantity.From(0.5m, InventoryUnits.Piece));
    }

    [TestMethod]
    public void NegativeAndOverflowingConversionsAreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new InventoryQuantity(InventoryDimension.Mass, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => InventoryQuantity.From(-1m, InventoryUnits.Gram));
        Assert.ThrowsExactly<OverflowException>(() => InventoryQuantity.From(decimal.MaxValue, InventoryUnits.Kilogram));
    }

    [TestMethod]
    public void ZeroIsAnExactBalanceButNotAPositiveBusinessQuantity()
    {
        var zero = InventoryQuantity.From(0m, InventoryUnits.Milligram);
        Assert.IsTrue(zero.IsZero);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ProductInventoryLink(Guid.NewGuid(), Guid.NewGuid(), zero));
    }

    [TestMethod]
    public void ArithmeticIsCheckedAndDimensionSafe()
    {
        var one = new InventoryQuantity(InventoryDimension.Mass, 1);
        Assert.AreEqual(3L, one.Add(new(InventoryDimension.Mass, 2)).AtomicUnits);
        Assert.ThrowsExactly<ArgumentException>(() => one.Add(new(InventoryDimension.Volume, 1)));
        Assert.ThrowsExactly<ArgumentException>(() => one.CompareTo(new(InventoryDimension.Count, 1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => one.Subtract(new(InventoryDimension.Mass, 2)));
        Assert.ThrowsExactly<OverflowException>(() => new InventoryQuantity(InventoryDimension.Mass, long.MaxValue).Add(one));
        Assert.ThrowsExactly<OverflowException>(() => new InventoryQuantity(InventoryDimension.Mass, long.MaxValue).Multiply(2));
    }

    [TestMethod]
    public void SignedDeltaIsExactSeparateAndChecked()
    {
        var quantity = new InventoryQuantity(InventoryDimension.Mass, 10);
        var increase = InventoryDelta.Increase(quantity);
        var decrease = InventoryDelta.Decrease(quantity);
        Assert.AreEqual(10L, increase.AtomicUnits);
        Assert.AreEqual(-10L, decrease.AtomicUnits);
        Assert.IsTrue(decrease.CompareTo(increase) < 0);
        Assert.AreEqual(20L, increase.ApplyTo(quantity).AtomicUnits);
        Assert.AreEqual(0L, decrease.ApplyTo(quantity).AtomicUnits);
        Assert.ThrowsExactly<InvalidOperationException>(() => decrease.ApplyTo(new(InventoryDimension.Mass, 9)));
        Assert.ThrowsExactly<OverflowException>(() =>
            InventoryDelta.Increase(new(InventoryDimension.Mass, long.MaxValue))
                .ApplyTo(new(InventoryDimension.Mass, 1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            InventoryDelta.Increase(new(InventoryDimension.Mass, 0)));
        Assert.ThrowsExactly<ArgumentException>(() =>
            decrease.CompareTo(InventoryDelta.Decrease(new(InventoryDimension.Volume, 10))));
    }

    [TestMethod]
    public void UnitConversionRejectsAnotherDimension()
    {
        var mass = InventoryQuantity.From(1m, InventoryUnits.Kilogram);
        Assert.AreEqual(1m, mass.In(InventoryUnits.Kilogram));
        Assert.ThrowsExactly<ArgumentException>(() => mass.In(InventoryUnits.Liter));
    }

    [TestMethod]
    public void StandardUnitsHaveApprovedAtomicFactors()
    {
        Assert.AreEqual(7, InventoryUnits.All.Count);
        Assert.AreSame(InventoryUnits.Microliter, InventoryUnits.FromCode("uL"));
        Assert.AreEqual(InventoryDimension.Count, InventoryUnits.Piece.Dimension);
        Assert.ThrowsExactly<ArgumentException>(() => InventoryUnits.FromCode("box"));
    }

    [TestMethod]
    public void InventoryItemNormalizesIdentityAndPreservesDimension()
    {
        var created = new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.FromHours(-6));
        var item = new InventoryItem(Guid.NewGuid(), " flour ", " Harina ", InventoryItemKind.RawMaterial,
            InventoryDimension.Mass, true, created, created);
        Assert.AreEqual("FLOUR", item.Code);
        Assert.AreEqual("Harina", item.Name);
        Assert.AreEqual(InventoryDimension.Mass, item.Dimension);
        Assert.AreEqual(TimeSpan.Zero, item.CreatedAt.Offset);
        Assert.AreEqual(InventoryDimension.Mass, item.Rename("Harina de trigo", created.AddMinutes(1)).Dimension);
        Assert.AreEqual(InventoryItemKind.Supply,
            item.Reconfigure(InventoryItemKind.Supply, InventoryDimension.Mass, created.AddMinutes(1)).Kind);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            item.Reconfigure(InventoryItemKind.RawMaterial, InventoryDimension.Volume, created.AddMinutes(1)));
    }

    [TestMethod]
    public void InventoryItemRejectsInvalidIdentityEnumsAndTime()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.ThrowsExactly<ArgumentException>(() => new InventoryItem(Guid.Empty, "A", "A", InventoryItemKind.Supply, InventoryDimension.Count, true, now, now));
        Assert.ThrowsExactly<ArgumentException>(() => new InventoryItem(Guid.NewGuid(), " ", "A", InventoryItemKind.Supply, InventoryDimension.Count, true, now, now));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new InventoryItem(Guid.NewGuid(), "A", "A", (InventoryItemKind)99, InventoryDimension.Count, true, now, now));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new InventoryItem(Guid.NewGuid(), "A", "A", InventoryItemKind.Supply, (InventoryDimension)99, true, now, now));
        Assert.ThrowsExactly<ArgumentException>(() => new InventoryItem(Guid.NewGuid(), "A", "A", InventoryItemKind.Supply, InventoryDimension.Count, true, now, now.AddSeconds(-1)));
    }

    [TestMethod]
    public void ItemWithStockCannotBeDeactivated()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new InventoryItem(Guid.NewGuid(), "FLOUR", "Harina", InventoryItemKind.RawMaterial,
            InventoryDimension.Mass, true, now, now);
        Assert.ThrowsExactly<InvalidOperationException>(() => item.Deactivate(true, now.AddMinutes(1)));
        Assert.IsFalse(item.Deactivate(false, now.AddMinutes(1)).IsActive);
        Assert.IsTrue(item.IsActive);
    }

    [TestMethod]
    public void ItemSpecificPresentationsRequireWholePositiveCounts()
    {
        var now = DateTimeOffset.UtcNow;
        var flour = new InventoryItem(Guid.NewGuid(), "FLOUR", "Harina", InventoryItemKind.RawMaterial,
            InventoryDimension.Mass, true, now, now);
        var sack = new InventoryItemUnit(Guid.NewGuid(), flour, "SACK25", "Saco de 25 kg", 25_000_000);
        Assert.AreEqual(50_000_000L, sack.ConvertWholeUnits(2).AtomicUnits);
        Assert.AreEqual(InventoryDimension.Mass, sack.Dimension);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => sack.ConvertWholeUnits(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => sack.ConvertWholeUnits(-1));
    }

    [TestMethod]
    public void PresentationConversionDetectsOverflow()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new InventoryItem(Guid.NewGuid(), "BOXED", "Caja", InventoryItemKind.Packaging,
            InventoryDimension.Count, true, now, now);
        var box = new InventoryItemUnit(Guid.NewGuid(), item, "BOX", "Caja", long.MaxValue);
        Assert.ThrowsExactly<OverflowException>(() => box.ConvertWholeUnits(2));
    }

    [TestMethod]
    public void MainLocationHasApprovedIdentity()
    {
        var location = InventoryLocation.Main(Guid.NewGuid());
        Assert.AreEqual("MAIN", location.Code);
        Assert.AreEqual("Almacén principal", location.Name);
        Assert.IsTrue(location.IsActive);
    }

    [TestMethod]
    public void ProductInventoryLinkIsExplicitAndPositive()
    {
        var product = Guid.NewGuid();
        var item = Guid.NewGuid();
        var link = new ProductInventoryLink(product, item, InventoryQuantity.From(1m, InventoryUnits.Piece));
        Assert.AreEqual(product, link.ProductId);
        Assert.AreEqual(item, link.InventoryItemId);
        Assert.AreEqual(1L, link.QuantityPerProductUnitAtomic);
        Assert.ThrowsExactly<ArgumentException>(() => new ProductInventoryLink(Guid.Empty, item, link.QuantityPerProductUnit));
    }

    [TestMethod]
    public void TrackInventoryDoesNotCreateAnInventoryAssociation()
    {
        var now = DateTimeOffset.UtcNow;
        var product = new Product(Guid.NewGuid(), Guid.NewGuid(), null, "Concha", "", new Money(1900), true, true, now, now);
        Assert.IsTrue(product.TrackInventory);
        Assert.IsNull(typeof(Product).GetProperty("InventoryItemId"));
    }

    [TestMethod]
    public void OnlyApprovedMovementKindsAreEnabledForPhase1H()
    {
        Assert.IsTrue(InventoryMovementKinds.IsEnabledInPhase1H(InventoryMovementKind.Waste));
        Assert.IsTrue(InventoryMovementKinds.IsEnabledInPhase1H(InventoryMovementKind.OpeningBalance));
        Assert.IsFalse(InventoryMovementKinds.IsEnabledInPhase1H(InventoryMovementKind.SaleConsumption));
        Assert.IsFalse(InventoryMovementKinds.IsEnabledInPhase1H(InventoryMovementKind.ProductionOutput));
        Assert.IsFalse(InventoryMovementKinds.IsEnabledInPhase1H(InventoryMovementKind.Transfer));
    }

    [TestMethod]
    public void StructuredReasonsRequireNoteForOther()
    {
        Assert.AreEqual(WasteReason.Damaged, new WasteReasonSelection(WasteReason.Damaged).Code);
        Assert.AreEqual(AdjustmentReason.PhysicalCountVariance,
            new AdjustmentReasonSelection(AdjustmentReason.PhysicalCountVariance).Code);
        Assert.ThrowsExactly<ArgumentException>(() => new WasteReasonSelection(WasteReason.Other));
        Assert.ThrowsExactly<ArgumentException>(() => new AdjustmentReasonSelection(AdjustmentReason.Other, " "));
        Assert.AreEqual("Detalle", new WasteReasonSelection(WasteReason.Other, " Detalle ").Note);
    }
}
