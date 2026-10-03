using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass]
public sealed class DevelopmentBuildIdentityTests
{
    [TestMethod]
    public void ReviewIdentityIsOnlyDisplayedInDevelopment()
    {
        const string commercialTitle = "Casa Miga POS · Caja";
        Assert.AreEqual(commercialTitle, DevelopmentBuildIdentity.WindowTitle(commercialTitle, false));
        var expected = commercialTitle + " · DESARROLLO";
        if (!string.IsNullOrWhiteSpace(DevelopmentBuildIdentity.ReviewBuild))
            expected += " · " + DevelopmentBuildIdentity.ReviewBuild;
        Assert.AreEqual(expected, DevelopmentBuildIdentity.WindowTitle(commercialTitle, true));
    }
}
