using System.IO;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

/// <summary>
/// Producción por lotes (pedido del dueño, 22/10/2026), con datos de prueba: tocar un pan, escribir las piezas que salieron y que
/// la venta las descuente; si se vende sin existencia, la venta pasa y queda el ajuste «Venta sin existencia».
/// </summary>
[TestClass, DoNotParallelize]
public sealed class BatchEntryTests
{
    [TestMethod]
    public async Task ABatchAddsPiecesTheSaleDeductsThemAndSellingWithoutStockNeverBlocks()
    {
        var root = Path.Combine(Path.GetTempPath(), "CasaMigaLotes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var database = new Database(Path.Combine(root, "casamiga.db")); database.Initialize();
            var work = new SqliteLocalUnitOfWork(database); var user = new UserSession(new CheckoutJournal(root)); var clock = new SystemClock();
            var terminal = TerminalConfiguration.LoadOrCreate(root); work.Run(s => { s.EnsureTerminal(terminal, clock.UtcNow); return true; });
            var identity = new IdentityService(work, new LocalPasswords(), clock, user); var password = "Aa1!" + Guid.NewGuid().ToString("N")[..12];
            identity.Bootstrap("admin", "Administrador de prueba", password); Assert.IsTrue(identity.Login("admin", password));
            var catalog = new CatalogService(work, user, clock, terminal.Id);
            var category = catalog.CreateCategory("Pan dulce");
            var concha = catalog.CreateProduct(category.Id, "CM-001", "Concha de prueba", "", new Money(1300), trackInventory: false);
            catalog.CreateProduct(category.Id, "CM-002", "Dona de prueba", "", new Money(1300), trackInventory: false);
            var inventory = new InventoryService(new SqliteInventoryStore(database, clock), work, user, clock, terminal.Id);
            // Como en la caja real: el artículo terminado PT-CM-001 ya existe, sin vínculo ni saldo.
            var existing = inventory.CreateInventoryItem("PT-CM-001", "Concha de prueba", InventoryItemKind.FinishedGood, InventoryDimension.Count);
            work.Run(s => { s.AddCashSession(CashSession.Open(Guid.NewGuid(), terminal.Id, user.Current!.Id, clock.UtcNow, new Money(0))); return true; });

            var lotes = new BatchEntryViewModel(inventory, catalog, new NoPhotos(), (_, _) => { });
            Assert.IsTrue(lotes.CanCapture);
            await lotes.LoadAsync();
            CollectionAssert.AreEqual(new[] { "Concha de prueba", "Dona de prueba" }, lotes.Products.Select(p => p.Name).ToArray());
            Assert.AreEqual("Sin lotes capturados", lotes.Products[0].StockText);

            lotes.Choose(concha.Id); lotes.Pieces = "abc"; await lotes.SaveAsync();
            Assert.IsFalse(lotes.MessageIsSuccess); StringAssert.Contains(lotes.Message, "número entero");
            lotes.Pieces = "24"; await lotes.SaveAsync();
            Assert.IsTrue(lotes.MessageIsSuccess, lotes.Message); StringAssert.Contains(lotes.Message, "Ahora hay 24");
            Assert.AreEqual("Hay 24 piezas", lotes.Products[0].StockText);
            Assert.HasCount(1, inventory.List(new(Search: "PT-CM-001", Kind: InventoryItemKind.FinishedGood)).Items, "The existing PT item is reused, not duplicated.");
            Assert.AreEqual(24L, inventory.Detail(existing.Id).Balance!.QuantityAtomic);
            Assert.IsTrue(catalog.Products(new(Search: "CM-001")).Items.Single().Product.TrackInventory, "The bread is now tracked for the sale.");
            lotes.Choose(concha.Id); lotes.Pieces = "6"; await lotes.SaveAsync();
            Assert.AreEqual(30L, inventory.Detail(existing.Id).Balance!.QuantityAtomic);

            // La venta descuenta (con el descuento por venta encendido).
            var config = new SaleConsumptionService(work, work, user, clock, terminal.Id);
            Assert.IsTrue(config.Preflight().CanEnable, "A captured bread leaves a valid configuration.");
            config.Change(ConsumptionAction.Enable, config.GetStatus());
            var pos = new PosService(work, user, terminal.Id); var checkout = new CheckoutService(work, user, clock, terminal.Id, new CheckoutJournal(root));
            Sale Sell(int qty) { pos.Add(concha.Id); for (int i = 1; i < qty; i++) pos.Increment(concha.Id); var request = checkout.Prepare(new Money(100000)); var sale = checkout.Confirm(request); checkout.Acknowledge(sale.OperationId); return sale; }
            Sell(2); Assert.AreEqual(28L, inventory.Detail(existing.Id).Balance!.QuantityAtomic);
            Sell(30);
            Assert.AreEqual(0L, inventory.Detail(existing.Id).Balance!.QuantityAtomic, "Selling more than there is never blocks and never goes negative.");
            var warning = inventory.MovementHistory(new(existing.Id)).Items.Single(m => m.Kind == InventoryMovementKind.AdjustmentGain);
            Assert.AreEqual(2L, warning.DeltaAtomic); StringAssert.Contains(warning.Note!, "Venta sin existencia");

            // Un pan sin lote capturado se vende como siempre, sin descontar.
            var dona = lotes.Products[1].Id; pos.Add(dona); checkout.Acknowledge(checkout.Confirm(checkout.Prepare(new Money(1300))).OperationId);
            using (var connection = database.Open(true)) using (var q = connection.CreateCommand())
            { q.CommandText = "SELECT count(*) FROM sale"; Assert.AreEqual(3L, (long)q.ExecuteScalar()!); }
            database.Validate();
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    private sealed class NoPhotos : IProductPhotoUiGateway
    {
        public Task<ProductPhotoVisual> LoadAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(new ProductPhotoVisual(0, null, null, "Sin foto", null, null));
        public Task<ProductPhotoVisual> ImportAsync(Guid productId, long revision, string originalName, string provenance, PhotoCrop crop, byte[] original, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductPhotoVisual> RemoveAsync(Guid productId, long revision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
