using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class DisplayFolioTests
{
    [TestMethod]
    public void CashRegisterCodeComesFromTheTrailingNumberOfItsName()
    {
        Assert.AreEqual("C01", DisplayFolio.TerminalCode("CASA-MIGA-CAJA-01"));
        Assert.AreEqual("C02", DisplayFolio.TerminalCode("Caja 2"));
        Assert.AreEqual("C12", DisplayFolio.TerminalCode("SUCURSAL-CENTRO-12"));
        Assert.AreEqual("C01", DisplayFolio.TerminalCode("Mostrador"));
        Assert.AreEqual("C01-001061", DisplayFolio.For("CASA-MIGA-CAJA-01", 1061));
        Assert.AreEqual("C01-1234567", DisplayFolio.For("CASA-MIGA-CAJA-01", 1234567), "Más de seis cifras no se recorta.");
        Assert.AreEqual("C01-001061", DisplayFolio.FromInternal("b9487c25552740f38e5cca7307465c4a-00001061", "CASA-MIGA-CAJA-01"));
    }

    [TestMethod]
    public void ParsesTheVisibleFolioWithOrWithoutTheCashRegister()
    {
        Assert.IsTrue(DisplayFolio.TryParse("C01-001061", out var code, out var number)); Assert.AreEqual("C01", code); Assert.AreEqual(1061L, number);
        Assert.IsTrue(DisplayFolio.TryParse(" c1-1061 ", out code, out number)); Assert.AreEqual("C01", code); Assert.AreEqual(1061L, number);
        Assert.IsTrue(DisplayFolio.TryParse("1061", out code, out number)); Assert.IsNull(code); Assert.AreEqual(1061L, number);
        foreach (var invalid in new[] { "", "C01-", "C0-12", "0", "abc", "b9487c25552740f38e5cca7307465c4a-00001061" })
            Assert.IsFalse(DisplayFolio.TryParse(invalid, out _, out _), invalid);
    }
}
