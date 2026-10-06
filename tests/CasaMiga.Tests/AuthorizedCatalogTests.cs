using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>El catálogo de revisión (docs/catalog) debe ser idéntico a la tabla autorizada v2 documentada.</summary>
[TestClass]
public sealed class AuthorizedCatalogTests
{
    private static string Catalog(string file) => Path.Combine(AppContext.BaseDirectory, "Catalog", file);

    [TestMethod]
    public void CsvMatchesTheAuthorizedTableRowByRow()
    {
        var csv = File.ReadAllLines(Catalog("catalogo-autorizado-v2.csv"));
        Assert.AreEqual("id,nombre,precio_mxn,categoria,nota", csv[0]);
        var rows = csv.Skip(1).Where(l => l.Length > 0).Select(l => l.Split(',')).ToList();
        var table = File.ReadAllLines(Catalog("phase1k2c-provisional-map.md"))
            .Select(l => Regex.Match(l, @"^\| (\d+) \| (.+?) \| \$(\d+)\.00 \| (.+?) \|")).Where(m => m.Success).ToList();

        Assert.HasCount(26, rows);
        Assert.HasCount(26, table);
        for (int i = 0; i < 26; i++)
        {
            Assert.AreEqual(table[i].Groups[1].Value, rows[i][0], "id");
            Assert.AreEqual(table[i].Groups[2].Value.Trim(), rows[i][1], "nombre");
            Assert.AreEqual(table[i].Groups[3].Value, rows[i][2], "precio");
            Assert.AreEqual(table[i].Groups[4].Value.Trim(), rows[i][3], "categoría");
        }
        Assert.AreEqual(26, rows.Select(r => r[1]).Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(rows.All(r => int.Parse(r[2], CultureInfo.InvariantCulture) > 0));
        CollectionAssert.AreEquivalent(new[] { "Pan dulce", "Dona", "Galleta", "Bebida" }, rows.Select(r => r[3]).Distinct().ToArray());
    }
}
