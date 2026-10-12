using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>El catálogo de revisión (docs/catalog): los 26 primeros son idénticos a la tabla autorizada v2 documentada; después van los panes
/// agregados por el usuario (docs/catalog/README.md).</summary>
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

        Assert.HasCount(29, rows, "26 de la tabla v2 + pan de muerto grande, pay de queso y pay de elote (12/10/2026).");
        Assert.HasCount(26, table);
        for (int i = 0; i < 26; i++)
        {
            Assert.AreEqual(table[i].Groups[1].Value, rows[i][0], "id");
            Assert.AreEqual(table[i].Groups[2].Value.Trim(), rows[i][1], "nombre");
            Assert.AreEqual(table[i].Groups[3].Value, rows[i][2], "precio");
            Assert.AreEqual(table[i].Groups[4].Value.Trim(), rows[i][3], "categoría");
        }
        Assert.AreEqual(rows.Count, rows.Select(r => r[1]).Distinct(StringComparer.Ordinal).Count());
        CollectionAssert.AreEqual(Enumerable.Range(1, rows.Count).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray(), rows.Select(r => r[0]).ToArray(), "Ids consecutivos.");
        CollectionAssert.AreEqual(new[] { "Pan de muerto grande,22,Pan dulce", "Pay de queso,22,Pan dulce", "Pay de elote,22,Pan dulce" },
            rows.Skip(26).Select(r => $"{r[1]},{r[2]},{r[3]}").ToArray());
        Assert.IsTrue(rows.All(r => int.Parse(r[2], CultureInfo.InvariantCulture) > 0));
        CollectionAssert.AreEquivalent(new[] { "Pan dulce", "Dona", "Galleta", "Bebida" }, rows.Select(r => r[3]).Distinct().ToArray());
    }
}
