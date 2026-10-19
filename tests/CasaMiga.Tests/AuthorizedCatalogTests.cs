using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>El catálogo de revisión (docs/catalog): los 26 primeros son idénticos a la tabla autorizada v2 documentada; después van los panes
/// agregados por el usuario (docs/catalog/README.md). Cada producto tiene su foto provisional en assets/catalog-provisional-ia.</summary>
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

        Assert.HasCount(42, rows, "26 de la tabla v2 + pan de muerto grande, pay de queso, pay de elote y bisquet (12/10/2026), baguette (15/10/2026) y las 11 altas del catálogo comercial de QA 1K.4G-C-B (19/10/2026).");
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
        CollectionAssert.AreEqual(new[] { "Pan de muerto grande,22,Pan dulce", "Pay de queso,22,Pays", "Pay de elote,22,Pays", "Bisquet,13,Pan dulce", "Baguette,9,Pan salado",
            "Rosquita de ajonjolí,13,Pan dulce", "Bollo con ajonjolí,13,Pan dulce", "Dona de chocolate negro con chispas,15,Dona", "Dona de chocolate blanco con coco,15,Dona", "Dona de chocolate con nuez,15,Dona", "Dona combinada de chocolate negro y blanco,15,Dona", "Berlinesa rellena de crema pastelera,15,Dona", "Berlinesa rellena de zarzamora,16,Dona", "Galleta de nuez,15,Galleta", "Galleta de coco,15,Galleta", "Galleta multigrano,15,Galleta" },
            rows.Skip(26).Select(r => $"{r[1]},{r[2]},{r[3]}").ToArray());
        Assert.IsTrue(rows.All(r => int.Parse(r[2], CultureInfo.InvariantCulture) > 0));
        // La siembra crea las categorías en el orden en que aparecen: «Pays» (12/10/2026) y «Pan salado» (15/10/2026) quedan al final.
        CollectionAssert.AreEqual(new[] { "Pan dulce", "Dona", "Galleta", "Bebida", "Pays", "Pan salado" }, rows.Select(r => r[3]).Distinct().ToArray());
    }

    [TestMethod]
    public void EveryProductHasAProvisionalPhotoRecordedInTheManifest()
    {
        var folder = Catalog("photos");
        var rows = File.ReadAllLines(Catalog("catalogo-autorizado-v2.csv")).Skip(1).Where(l => l.Length > 0).Select(l => l.Split(',')).ToList();
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        var entries = manifest.RootElement.EnumerateArray().ToDictionary(e => e.GetProperty("sku").GetString()!, e => e);
        Assert.HasCount(rows.Count, entries, "Una foto provisional por producto del catálogo.");
        foreach (var row in rows)
        {
            var sku = $"CM-{int.Parse(row[0], CultureInfo.InvariantCulture):000}";
            var entry = entries[sku];
            Assert.AreEqual("PROVISIONAL", entry.GetProperty("provenance").GetString(), sku);
            var file = Path.Combine(folder, entry.GetProperty("file").GetString()!);
            Assert.AreEqual(entry.GetProperty("sha256").GetString()!.ToLowerInvariant(), Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))), sku);
        }
    }
}
