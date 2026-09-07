using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;
[TestClass]
public sealed class CartTests
{
    private static Cart Empty()=>new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());
    [TestMethod]public void NewCartIsEmptyAndNotASale(){var c=Empty();Assert.IsTrue(c.IsEmpty);Assert.AreEqual(0L,c.PreliminaryTotal.Cents);}
    [TestMethod]public void AddCapturesIdentityNameAndExactPrice(){var id=Guid.NewGuid();var c=Empty().Add(id,"Concha",new Money(1800));Assert.AreEqual(id,c.Lines[0].ProductId);Assert.AreEqual("Concha",c.Lines[0].ProductName);Assert.AreEqual(1,c.Lines[0].Quantity);Assert.AreEqual(1800L,c.Subtotal.Cents);}
    [TestMethod]public void RepeatedProductIncrementsWithoutReplacingSnapshot(){var id=Guid.NewGuid();var c=Empty().Add(id,"Concha",new Money(1800)).Add(id,"Nombre nuevo",new Money(1900));Assert.HasCount(1,c.Lines);Assert.AreEqual(2,c.Lines[0].Quantity);Assert.AreEqual("Concha",c.Lines[0].ProductName);Assert.AreEqual(3600L,c.Subtotal.Cents);}
    [TestMethod]public void DifferentProductsHaveSeparateLinesAndCorrectTotals(){var c=Empty().Add(Guid.NewGuid(),"Concha",new Money(1800)).Add(Guid.NewGuid(),"Bolillo",new Money(500));Assert.HasCount(2,c.Lines);Assert.AreEqual(2300L,c.Subtotal.Cents);Assert.AreEqual(c.Subtotal,c.PreliminaryTotal);}
    [TestMethod]public void IncrementAndDecrementProduceNewCart(){var id=Guid.NewGuid();var before=Empty().Add(id,"Concha",new Money(1800));var next=before.Increment(id);Assert.AreEqual(1,before.Lines[0].Quantity);Assert.AreEqual(2,next.Lines[0].Quantity);Assert.AreEqual(1800L,next.Decrement(id).Subtotal.Cents);}
    [TestMethod][DataRow(0)][DataRow(-1)]public void InvalidQuantityCannotEnterCart(int quantity){var id=Guid.NewGuid();var c=Empty().Add(id,"Concha",new Money(1800));Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>c.SetQuantity(id,quantity));Assert.AreEqual(1,c.Lines[0].Quantity);}
    [TestMethod]public void DecrementOneRequiresExplicitRemoval(){var id=Guid.NewGuid();var c=Empty().Add(id,"Concha",new Money(1800));Assert.ThrowsExactly<ArgumentException>(()=>c.Decrement(id));Assert.IsTrue(c.Remove(id).IsEmpty);}
    [TestMethod]public void ClearPreservesContextAndOriginalSnapshot(){var before=Empty().Add(Guid.NewGuid(),"Concha",new Money(1800));var clear=before.Clear();Assert.IsTrue(clear.IsEmpty);Assert.IsFalse(before.IsEmpty);Assert.AreEqual(before.UserId,clear.UserId);Assert.AreEqual(before.CashSessionId,clear.CashSessionId);Assert.AreEqual(before.TerminalId,clear.TerminalId);}
    [TestMethod]public void ZeroPriceIsAllowed(){var id=Guid.NewGuid();var c=Empty().Add(id,"Sin cargo",new Money(0)).Increment(id);Assert.AreEqual(2,c.Lines[0].Quantity);Assert.AreEqual(0L,c.PreliminaryTotal.Cents);}
    [TestMethod]public void MultiplicationOverflowDoesNotMutateCart(){var id=Guid.NewGuid();var c=Empty().Add(id,"Límite",new Money(long.MaxValue));Assert.ThrowsExactly<OverflowException>(()=>c.Increment(id));Assert.AreEqual(long.MaxValue,c.Subtotal.Cents);Assert.AreEqual(1,c.Lines[0].Quantity);}
    [TestMethod]public void SumOverflowDoesNotAddPartialLine(){var c=Empty().Add(Guid.NewGuid(),"Límite",new Money(long.MaxValue));Assert.ThrowsExactly<OverflowException>(()=>c.Add(Guid.NewGuid(),"Otro",new Money(1)));Assert.HasCount(1,c.Lines);}
    [TestMethod]public void QuantityOverflowIsCheckedEvenAtZeroPrice(){var id=Guid.NewGuid();var c=Empty().Add(id,"Cero",new Money(0)).SetQuantity(id,int.MaxValue);Assert.ThrowsExactly<OverflowException>(()=>c.Increment(id));Assert.AreEqual(int.MaxValue,c.Lines[0].Quantity);}
    [TestMethod]public void NegativePriceAndInvalidContextAreRejected(){Assert.ThrowsExactly<ArgumentException>(()=>new Cart(Guid.Empty,Guid.NewGuid(),Guid.NewGuid()));Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>Empty().Add(Guid.NewGuid(),"Inválido",new Money(-1)));}
}
