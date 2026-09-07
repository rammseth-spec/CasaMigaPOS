using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class SaleTests
{
    private static Sale Create(IEnumerable<SaleItem> items, long received = 10000) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            DateTimeOffset.UtcNow, items, Guid.NewGuid(), new(received));
    [TestMethod] public void ExactCashTotalsAndSnapshots()
    {
        var items = new List<SaleItem> { new(Guid.NewGuid(), "Concha", new(1900), 2), new(Guid.NewGuid(), "Bolillo", new(500), 3) };
        var sale = Create(items); items.Clear();
        Assert.AreEqual(2, sale.Items.Count); Assert.AreEqual(5300L, sale.Total.Cents);
        Assert.AreEqual(5300L, sale.Payment.Applied.Cents); Assert.AreEqual(4700L, sale.Payment.Change.Cents);
        Assert.AreEqual("CASH", sale.Payment.Method); Assert.AreEqual(TimeSpan.Zero, sale.ConfirmedAt.Offset);
    }
    [TestMethod] public void EmptyAndDuplicateLinesRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Create([]));
        var line = new SaleItem(Guid.NewGuid(), "Concha", new(1), 1);
        Assert.ThrowsExactly<ArgumentException>(() => Create([line, line]));
    }
    [TestMethod][DataRow(0)][DataRow(-1)] public void NonpositiveQuantityRejected(int quantity) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SaleItem(Guid.NewGuid(), "Pan", new(1), quantity));
    [TestMethod] public void NegativePriceAndInsufficientReceivedRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SaleItem(Guid.NewGuid(), "Pan", new(-1), 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create([new(Guid.NewGuid(), "Pan", new(500), 1)],499));
    }
    [TestMethod] public void ZeroPriceAndExactPaymentAllowed()
    {
        Assert.AreEqual(0L, Create([new(Guid.NewGuid(), "Pan", new(0), 1)],0).Total.Cents);
        Assert.AreEqual(0L, Create([new(Guid.NewGuid(), "Pan", new(500), 1)],500).Payment.Change.Cents);
    }
    [TestMethod] public void LineAndAggregateOverflowRejected()
    {
        Assert.ThrowsExactly<OverflowException>(() => new SaleItem(Guid.NewGuid(), "Pan", new(long.MaxValue), 2));
        Assert.ThrowsExactly<OverflowException>(() => Create([new(Guid.NewGuid(), "A", new(long.MaxValue), 1),new(Guid.NewGuid(), "B", new(1),1)],long.MaxValue));
    }
}
