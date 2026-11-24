using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

/// <summary>Un pan del catálogo de Casa Miga (docs/catalog/catalogo-autorizado-v2.csv) con sus nombres anteriores (renombres.json).</summary>
public sealed record CasaMigaCatalogRow(int Id, string Name, int PriceMxn, string Category, IReadOnlyList<string> Before)
{
    public string Sku => $"CM-{Id:000}";
}

/// <summary>Lo que hizo «Cargar catálogo Casa Miga».</summary>
public sealed record CasaMigaCatalogResult(int Created, int Renamed, int Photos, int Categories)
{
    public string Summary => Created + Renamed + Photos == 0
        ? "El catálogo de Casa Miga ya estaba completo: no hubo cambios."
        : $"Catálogo Casa Miga: {Created} {(Created == 1 ? "pan nuevo" : "panes nuevos")}, {Renamed} {(Renamed == 1 ? "renombrado" : "renombrados")} y {Photos} {(Photos == 1 ? "foto agregada" : "fotos agregadas")}.";
}

/// <summary>
/// Catálogo de Casa Miga que viaja con el programa y el instalador (carpeta catalogo\ junto al .exe): los panes del catálogo autorizado,
/// sus nombres anteriores y sus fotos provisionales hechas con IA (catalogo\fotos\CM-NNN.jpg). Cargarlo agrega los panes que falten con su
/// foto, renombra los que cambiaron de nombre en lugar de dar de alta otro y pone foto a los que no tienen. Nunca borra panes, nunca cambia
/// precios ni categorías de los que ya existen y nunca reemplaza una foto real.
/// </summary>
public static class CasaMigaCatalog
{
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "catalogo");
    public static bool IsAvailable(string folder) => File.Exists(Path.Combine(folder, "catalogo-autorizado-v2.csv"));

    public static IReadOnlyList<CasaMigaCatalogRow> Read(string folder)
    {
        var before = new Dictionary<int, string[]>();
        var renames = Path.Combine(folder, "renombres.json");
        if (File.Exists(renames))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(renames));
            foreach (var entry in document.RootElement.GetProperty("renombres").EnumerateArray())
                before[entry.GetProperty("id").GetInt32()] = entry.GetProperty("antes").EnumerateArray()
                    .Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray();
        }
        var rows = new List<CasaMigaCatalogRow>();
        foreach (var line in File.ReadAllLines(Path.Combine(folder, "catalogo-autorizado-v2.csv")).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            var id = int.Parse(f[0], CultureInfo.InvariantCulture);
            rows.Add(new(id, f[1].Trim(), int.Parse(f[2], CultureInfo.InvariantCulture), f[3].Trim(), before.TryGetValue(id, out var names) ? names : Array.Empty<string>()));
        }
        return rows;
    }

    /// <summary>
    /// Caja recién instalada (pedido del dueño, 24/11/2026): sólo una base sin ningún pan, ni siquiera inactivo, recibe el catálogo sola.
    /// Una base restaurada o que ya tiene panes nunca se toca.
    /// </summary>
    public static bool HasNoProducts(CatalogService catalog) => catalog.Products(new CatalogQuery(IncludeInactive: true)).Total == 0;

    public static async Task<CasaMigaCatalogResult> LoadAsync(CatalogService catalog, IProductPhotoUiGateway photos, string folder)
    {
        var rows = await Task.Run(() => Read(folder));
        var categories = (await Task.Run(() => catalog.Categories(true)))
            .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var products = new List<Product>();
        for (var page = 0; ; page++)
        {
            var current = page;
            var result = await Task.Run(() => catalog.Products(new CatalogQuery("", null, true, current)));
            products.AddRange(result.Items.Select(r => r.Product));
            if (products.Count >= result.Total || result.Items.Count == 0) break;
        }

        int created = 0, renamed = 0, newCategories = 0, added = 0;
        var withPhoto = new List<(Guid Product, CasaMigaCatalogRow Row, bool Renamed)>();
        foreach (var row in rows)
        {
            if (products.FirstOrDefault(p => Same(p.Name, row.Name)) is { } existing) { withPhoto.Add((existing.Id, row, false)); continue; }
            if (products.FirstOrDefault(p => row.Before.Any(b => Same(p.Name, b))) is { } old)
            {
                var updated = await Task.Run(() => catalog.EditProduct(old.Id, old.CategoryId, old.Sku, row.Name, old.Description, old.SalePrice, old.TrackInventory));
                products[products.IndexOf(old)] = updated; renamed++;
                withPhoto.Add((updated.Id, row, true));
                continue;
            }
            if (!categories.TryGetValue(row.Category, out var categoryId))
            {
                var order = categories.Count;
                categoryId = (await Task.Run(() => catalog.CreateCategory(row.Category, "Catálogo Casa Miga", order))).Id;
                categories[row.Category] = categoryId; newCategories++;
            }
            var product = await Task.Run(() => catalog.CreateProduct(categoryId, row.Sku, row.Name, "", new Money(checked(row.PriceMxn * 100L)), false));
            products.Add(product); created++;
            withPhoto.Add((product.Id, row, false));
        }

        foreach (var (product, row, wasRenamed) in withPhoto)
        {
            var file = Path.Combine(folder, "fotos", $"{row.Sku}.jpg");
            if (!File.Exists(file)) continue;
            var current = await photos.LoadAsync(product);
            // Sin foto se agrega la provisional; un pan renombrado cambia la suya sólo si también es provisional. Una foto real nunca se toca.
            if (current.PhotoId is not null && !(wasRenamed && current.Status == "Foto provisional")) continue;
            var bytes = await File.ReadAllBytesAsync(file);
            await photos.ImportAsync(product, current.Revision, Path.GetFileName(file), "PROVISIONAL", new PhotoCrop(0, 0, Edge(bytes)), bytes);
            added++;
        }
        return new(created, renamed, added, newCategories);
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Lado del cuadro que se recorta: las fotos del catálogo ya son cuadradas (768 × 768).</summary>
    private static int Edge(byte[] jpeg)
    {
        using var stream = new MemoryStream(jpeg, false);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        return Math.Min(frame.PixelWidth, frame.PixelHeight);
    }
}
