using System.Globalization;
using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class SaleConsumptionIdentityTests
{
    [TestMethod]
    public void RfcVectorUsesNetworkByteOrder()
    {
        Assert.AreEqual(Guid.Parse("2ed6657d-e927-568b-95e1-2665a8aea6a2"),
            SaleConsumptionIdentity.UuidV5(Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"), "www.example.com"));
    }

    [TestMethod]
    [DataRow("aabbccdd-1234-5678-9abc-def012345678", "f2e3751e-040d-5020-b932-d14b048158e9")]
    [DataRow("00000000-0000-0000-0000-000000000001", "da60d397-7f5a-5f7f-85b3-0f01bd8a0815")]
    public void VersionOneOperationVectorsAreFixed(string input, string output) =>
        Assert.AreEqual(Guid.Parse(output), SaleConsumptionIdentity.OperationIdV1(Guid.Parse(input)));

    [TestMethod]
    public void OperationIdentityIsCultureAndTextFormatIndependent()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            var root = Guid.Parse("aabbccdd-1234-5678-9abc-def012345678");
            var expected = SaleConsumptionIdentity.OperationIdV1(root);
            foreach (var culture in new[] { "es-MX", "tr-TR", "ar-SA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var format in new[] { "D", "N", "B", "P" })
                    Assert.AreEqual(expected, SaleConsumptionIdentity.OperationIdV1(Guid.Parse(root.ToString(format).ToUpperInvariant())));
            }
            Assert.AreNotEqual(expected, SaleConsumptionIdentity.OperationIdV1(Guid.Parse("aabbccdd-1234-5678-9abc-def012345679")));
            Assert.ThrowsExactly<ArgumentException>(() => SaleConsumptionIdentity.OperationIdV1(Guid.Empty));
        }
        finally { CultureInfo.CurrentCulture = old; }
    }
}
