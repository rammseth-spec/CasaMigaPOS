using System.IO;
using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

/// <summary>Foto subida desde la web (docs/POS_WEB.md, fase 3): la caja la procesa igual que una importada aquí y no la repite.</summary>
[TestClass]
public sealed class WebPhotoCommandTests
{
    private PhotoFixture f = null!;
    [TestInitialize] public void Setup() => f = new();
    [TestCleanup] public void Cleanup() => f.Dispose();

    private CloudCommandService Commands => new(f.Work, f.Clock, f.Terminal.Id, TimeZoneInfo.Utc, new CloudCommandModules(new SqliteInventoryStore(f.Db, f.Clock), f.Work,
        new SqliteProductionUnitOfWork(f.Db), session => new ProductPhotoService(f.Repository, f.Files, new WpfProductPhotoProcessor(), session, f.Clock, f.Terminal.Id)));
    private long sequence;
    private CloudCommand Photo(byte[] image, int edge, Guid? product = null) => new(Guid.NewGuid(), ++sequence, "product.photo",
        new JsonObject { ["productId"] = (product ?? f.Product.Id).ToString(), ["image"] = Convert.ToBase64String(image), ["edge"] = edge, ["name"] = "web.jpg" }, f.User.Current!.Id, "Encargado web");

    [TestMethod]
    public void APhotoFromTheWebReplacesTheCurrentOneOnce()
    {
        f.Import();
        var command = Photo(PhotoFixture.Chart("jpeg", 768, 768), 768);
        var result = Commands.Apply(command);
        Assert.IsTrue(result.Applied, result.Message);
        var current = f.Service.Current(f.Product.Id);
        Assert.AreEqual(2L, current.Revision, "It replaces whatever photo the till had.");
        Assert.AreEqual("REAL", current.Photo!.Provenance);
        Assert.AreEqual(command.Id, current.Photo.Id);
        Assert.AreEqual(512, current.Photo.ThumbnailEdge);
        Assert.AreEqual(command.Id, Commands.Apply(command).Id);
        Assert.AreEqual(2L, f.Service.Current(f.Product.Id).Revision, "Applying it again changes nothing.");
    }

    [TestMethod]
    public void ProvenanceAndRemovalFromTheWebWorkLikeTheCatalogScreen()
    {
        var provisional = Photo(PhotoFixture.Chart("jpeg", 512, 512), 512);
        provisional.Payload["provenance"] = "PROVISIONAL";
        Assert.IsTrue(Commands.Apply(provisional).Applied);
        Assert.AreEqual("PROVISIONAL", f.Service.Current(f.Product.Id).Photo!.Provenance);

        var remove = new CloudCommand(Guid.NewGuid(), ++sequence, "product.photo.remove", new JsonObject { ["productId"] = f.Product.Id.ToString() }, f.User.Current!.Id, "Encargado web");
        Assert.IsTrue(Commands.Apply(remove).Applied);
        var after = f.Service.Current(f.Product.Id);
        Assert.IsNull(after.Photo, "«Quitar foto» leaves the product without a photo.");
        Assert.AreEqual(remove.Id, Commands.Apply(remove).Id);
        Assert.AreEqual(after.Revision, f.Service.Current(f.Product.Id).Revision, "Applying it again changes nothing.");
        Assert.IsTrue(Commands.Apply(new CloudCommand(Guid.NewGuid(), ++sequence, "product.photo.remove", new JsonObject { ["productId"] = f.Product.Id.ToString() }, f.User.Current!.Id, "Encargado web")).Applied,
            "Removing when there is no photo is a no-op.");
    }

    [TestMethod]
    public void BrokenImagesOrUnknownProductsAreRejectedWithAReason()
    {
        var broken = Commands.Apply(Photo([1, 2, 3, 4], 512));
        Assert.AreEqual("rejected", broken.Status);
        StringAssert.Contains(broken.Message, "JPEG");
        Assert.AreEqual("rejected", Commands.Apply(Photo(PhotoFixture.Chart("jpeg", 512, 512), 512, Guid.NewGuid())).Status);
        Assert.AreEqual("rejected", Commands.Apply(Photo(PhotoFixture.Chart("jpeg", 512, 512), 900)).Status, "The crop must fit the picture.");
        Assert.IsNull(f.Service.Current(f.Product.Id).Photo);
    }
}
