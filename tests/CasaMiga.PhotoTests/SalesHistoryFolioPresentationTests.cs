using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass]
public sealed class SalesHistoryFolioPresentationTests
{
    [TestMethod]
    public void SalesHistoryListShowsTheVisibleFolio()
    {
        var terminal = Guid.Parse("b9487c25-5527-40f3-8e5c-ca7307465c4a");
        var row = new SalesHistoryRow(Guid.NewGuid(), $"{terminal:N}-{1061L:D8}", DateTimeOffset.UtcNow, new Money(9400), "CASH",
            Guid.NewGuid(), "Ana", terminal, "CASA-MIGA-CAJA-01", Guid.NewGuid());
        Assert.AreEqual("C01-001061", new SalesHistoryRowView(row).Folio, "La lista muestra el folio corto, no el identificador de la terminal.");
    }
}
