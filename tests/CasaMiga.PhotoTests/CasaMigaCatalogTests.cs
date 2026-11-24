using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

/// <summary>
/// «Cargar catálogo Casa Miga» (pedido del dueño, 13/11/2026): el catálogo y sus fotos provisionales con IA viajan con el programa y el
/// instalador. Agrega los panes que faltan con su foto, renombra los que cambiaron de nombre sin dar de alta otro y nunca toca una foto real.
/// </summary>
[TestClass, DoNotParallelize]
public sealed class CasaMigaCatalogTests
{
    [TestMethod]
    public async Task LoadingTheCatalogAddsMissingBreadsRenamesChangedOnesAndNeverReplacesARealPhoto()
    {
        var root = Path.Combine(Path.GetTempPath(), "CasaMigaCatalogo", Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "catalogo");
        Directory.CreateDirectory(Path.Combine(folder, "fotos"));
        try
        {
            File.WriteAllLines(Path.Combine(folder, "catalogo-autorizado-v2.csv"), new[] { "id,nombre,precio_mxn,categoria,nota", "1,Concha de vainilla,13,Pan dulce,",
                "34,Dona de chocolate negro con chispas de colores,15,Dona,", "45,Lima,13,Pan dulce,", "46,Submarino relleno,15,Pan dulce," });
            File.WriteAllText(Path.Combine(folder, "renombres.json"), "{ \"renombres\": [ { \"id\": 34, \"antes\": [\"Dona de chocolate negro con chispas\"] } ] }");
            foreach (var sku in new[] { "CM-001", "CM-034", "CM-045", "CM-046" }) Jpeg(Path.Combine(folder, "fotos", sku + ".jpg"), 300);
            Assert.IsTrue(CasaMigaCatalog.IsAvailable(folder));
            Assert.AreEqual("Dona de chocolate negro con chispas", CasaMigaCatalog.Read(folder).Single(r => r.Id == 34).Before.Single());

            var database = new Database(Path.Combine(root, "casamiga.db")); database.Initialize();
            var work = new SqliteLocalUnitOfWork(database); var user = new UserSession(new CheckoutJournal(root)); var clock = new SystemClock();
            var terminal = TerminalConfiguration.LoadOrCreate(root); work.Run(s => { s.EnsureTerminal(terminal, clock.UtcNow); return true; });
            var identity = new IdentityService(work, new LocalPasswords(), clock, user); var password = "Aa1!" + Guid.NewGuid().ToString("N")[..12];
            identity.Bootstrap("admin", "Administrador de prueba", password); Assert.IsTrue(identity.Login("admin", password));
            var catalog = new CatalogService(work, user, clock, terminal.Id);
            Assert.IsTrue(CasaMigaCatalog.HasNoProducts(catalog), "Una caja recién instalada recibe el catálogo sola al entrar el administrador.");
            // Como la caja de la tienda: la concha con foto real y la dona con su nombre anterior y una foto provisional.
            var pan = catalog.CreateCategory("Pan dulce"); var donas = catalog.CreateCategory("Dona");
            var concha = catalog.CreateProduct(pan.Id, "CM-001", "Concha de vainilla", "", new Money(1400), trackInventory: false);
            var dona = catalog.CreateProduct(donas.Id, "CM-034", "Dona de chocolate negro con chispas", "", new Money(1500), trackInventory: false);
            var photos = new FakePhotos(); photos.Set(concha.Id, "Foto real"); photos.Set(dona.Id, "Foto provisional");
            Assert.IsFalse(CasaMigaCatalog.HasNoProducts(catalog), "Una base que ya tiene panes (o restaurada) nunca recibe el catálogo sola.");

            var result = await CasaMigaCatalog.LoadAsync(catalog, photos, folder);
            Assert.AreEqual((2, 1, 3, 0), (result.Created, result.Renamed, result.Photos, result.Categories), result.Summary);
            var all = catalog.Products(new(IncludeInactive: true)).Items.Select(r => r.Product).ToList();
            Assert.HasCount(4, all, "The renamed donut is not added again.");
            var renamed = all.Single(p => p.Id == dona.Id);
            Assert.AreEqual("Dona de chocolate negro con chispas de colores", renamed.Name);
            Assert.AreEqual(1500L, renamed.SalePrice.Cents);
            Assert.AreEqual(1400L, all.Single(p => p.Id == concha.Id).SalePrice.Cents, "Prices of existing breads never change.");
            var lima = all.Single(p => p.Name == "Lima"); var submarino = all.Single(p => p.Name == "Submarino relleno");
            Assert.AreEqual("CM-045", lima.Sku); Assert.AreEqual(1300L, lima.SalePrice.Cents); Assert.AreEqual(pan.Id, lima.CategoryId);
            CollectionAssert.AreEquivalent(new[] { dona.Id, lima.Id, submarino.Id }, photos.Imported.Select(x => x.Product).ToArray(), "A real photo is never replaced.");
            Assert.IsTrue(photos.Imported.All(x => x.Provenance == "PROVISIONAL" && x.Edge == 300));

            var again = await CasaMigaCatalog.LoadAsync(catalog, photos, folder);
            Assert.AreEqual(0, again.Created + again.Renamed + again.Photos);
            StringAssert.Contains(again.Summary, "ya estaba completo");
            database.Validate();
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    private static void Jpeg(string path, int edge)
    {
        var pixels = new byte[edge * edge * 3];
        Array.Fill(pixels, (byte)200);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(edge, edge, 96, 96, PixelFormats.Bgr24, null, pixels, edge * 3)));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private sealed class FakePhotos : IProductPhotoUiGateway
    {
        private readonly Dictionary<Guid, string> status = new();
        public List<(Guid Product, string Provenance, int Edge)> Imported { get; } = new();
        public void Set(Guid product, string value) => status[product] = value;
        public Task<ProductPhotoVisual> LoadAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(status.TryGetValue(productId, out var value)
                ? new ProductPhotoVisual(1, Guid.NewGuid(), "prueba", value, null, null)
                : new ProductPhotoVisual(0, null, null, "Sin foto", null, null));
        public Task<ProductPhotoVisual> ImportAsync(Guid productId, long revision, string originalName, string provenance, PhotoCrop crop, byte[] original, CancellationToken cancellationToken = default)
        {
            Imported.Add((productId, provenance, crop.Edge));
            status[productId] = provenance == "REAL" ? "Foto real" : "Foto provisional";
            return LoadAsync(productId, cancellationToken);
        }
        public Task<ProductPhotoVisual> RemoveAsync(Guid productId, long revision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
