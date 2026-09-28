using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass]
public sealed class ProductPhotoUiTests
{
    private static BitmapSource Image(int side,byte value)
    {
        var pixels=Enumerable.Repeat(value,side*side*4).ToArray();
        var image=BitmapSource.Create(side,side,96,96,PixelFormats.Bgra32,null,pixels,side*4);image.Freeze();return image;
    }
    [STATestMethod]
    public void CacheHitReturnsSameFrozenBitmap()
    {
        var cache=new BoundedProductPhotoCache(4096);var product=Guid.NewGuid();var image=Image(16,12);
        cache.Add(product,"a",image);
        Assert.IsTrue(cache.TryGet("a",out var found));Assert.AreSame(image,found);Assert.IsTrue(found!.IsFrozen);
    }
    [STATestMethod]
    public void CacheIsBoundedAndEvictsLeastRecentlyUsed()
    {
        var cache=new BoundedProductPhotoCache(2048);var product=Guid.NewGuid();
        cache.Add(product,"a",Image(16,1));cache.Add(product,"b",Image(16,2));Assert.IsTrue(cache.TryGet("a",out _));
        cache.Add(product,"c",Image(16,3));
        Assert.IsFalse(cache.TryGet("b",out _));Assert.IsTrue(cache.TryGet("a",out _));Assert.IsTrue(cache.TryGet("c",out _));
        Assert.IsTrue(cache.UsedBytes<=cache.BudgetBytes);
    }
    [STATestMethod]
    public void InvalidateRemovesOnlySelectedProduct()
    {
        var cache=new BoundedProductPhotoCache(4096);var first=Guid.NewGuid();var second=Guid.NewGuid();
        cache.Add(first,"a",Image(8,1));cache.Add(second,"b",Image(8,2));cache.Invalidate(first);
        Assert.IsFalse(cache.TryGet("a",out _));Assert.IsTrue(cache.TryGet("b",out _));
    }
    [STATestMethod]
    public void DecodeLoadsBytesAndDoesNotRetainInputStream()
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Image(32,80)));
        byte[] bytes;using(var output=new MemoryStream()){encoder.Save(output);bytes=output.ToArray();}
        var decoded=ProductPhotoUiGateway.Decode(bytes);
        Array.Fill(bytes,(byte)0);Assert.AreEqual(32,decoded.PixelWidth);Assert.IsTrue(decoded.IsFrozen);
    }
    [STATestMethod]
    public void ProductCardUsesProductNamePriceCategoryAndInactiveState()
    {
        var at=DateTimeOffset.UtcNow;var product=new Product(Guid.NewGuid(),Guid.NewGuid(),"SKU-1","Nombre autorizado","",new Money(1300),false,false,at,at);
        var card=new ProductCardViewModel(new ProductRow(product,"Pan dulce"));
        Assert.AreEqual("Nombre autorizado",card.Name);Assert.AreEqual("$13.00",card.Price);Assert.AreEqual("Pan dulce",card.CategoryName);Assert.IsFalse(card.IsActive);Assert.IsTrue(card.IsFallback);
    }
    [STATestMethod]
    public void ProductCardExposesPhotoWithoutChangingProductFields()
    {
        var at=DateTimeOffset.UtcNow;var product=new Product(Guid.NewGuid(),Guid.NewGuid(),null,"Concha", "",new Money(1500),true,false,at,at);
        var card=new ProductCardViewModel(new ProductRow(product,"Pan dulce"));var image=Image(24,90);
        card.SetPhoto(new(3,Guid.NewGuid(),"HASH","Foto provisional",image,null));
        Assert.IsTrue(card.HasPhoto);Assert.IsFalse(card.IsFallback);Assert.AreSame(image,card.Photo);Assert.AreEqual("Concha",card.Name);Assert.AreEqual("$15.00",card.Price);
    }
    [TestMethod]
    public void CenteredCropUsesLargestSquareAndSupportsCustomCropContract()
    {
        Assert.AreEqual(new PhotoCrop(0,250,1000),ProductPhotoUiGateway.CenteredCrop(1000,1500));
        var custom=new PhotoCrop(50,75,700);Assert.AreEqual(50,custom.X);Assert.AreEqual(75,custom.Y);Assert.AreEqual(700,custom.Edge);
        Assert.ThrowsExactly<ProductPhotoException>(()=>ProductPhotoUiGateway.CenteredCrop(200,600));
    }
    [STATestMethod]
    public void CacheInvalidationMakesReplacementVisible()
    {
        var cache=new BoundedProductPhotoCache(4096);var product=Guid.NewGuid();var old=Image(16,10);var replacement=Image(16,20);
        cache.Add(product,"old",old);cache.Invalidate(product);cache.Add(product,"new",replacement);
        Assert.IsFalse(cache.TryGet("old",out _));Assert.IsTrue(cache.TryGet("new",out var found));Assert.AreSame(replacement,found);
    }
    [TestMethod]
    public void OneHundredCardsKeepAuthoritativeNamesAndPrices()
    {
        var at=DateTimeOffset.UtcNow;var category=Guid.NewGuid();
        var cards=Enumerable.Range(1,100).Select(i=>new ProductCardViewModel(new ProductRow(new Product(Guid.NewGuid(),category,$"SKU-{i}",$"Producto {i}","",new Money(i*100),true,false,at,at),"Categoría"))).ToArray();
        Assert.AreEqual(100,cards.Length);Assert.AreEqual("Producto 100",cards[^1].Name);Assert.AreEqual("$100.00",cards[^1].Price);Assert.IsTrue(cards.All(card=>card.IsFallback));
    }
    [TestMethod]
    public async Task GatewayLoadsOnWorkerThreadCachesAndReleasesThumbnailFile()
    {
        using var fixture=new PhotoFixture();var selection=fixture.Import();var cache=new BoundedProductPhotoCache(4*1024*1024);
        var gateway=new ProductPhotoUiGateway(fixture.Service,cache);
        var first=await gateway.LoadAsync(fixture.Product.Id);var second=await gateway.LoadAsync(fixture.Product.Id);
        Assert.IsTrue(first.HasPhoto);Assert.AreEqual("Foto provisional",first.Status);Assert.AreEqual(selection.Photo!.Id,first.PhotoId);
        Assert.AreSame(first.Image,second.Image);Assert.AreEqual(1,cache.Count);
        var path=Path.Combine(fixture.DirectoryPath,ProductPhotoFileStore.ThumbnailPath(selection.Photo));
        using var exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        Assert.IsTrue(exclusive.Length>0,"BitmapImage.OnLoad debe liberar el archivo derivado.");
    }
    [TestMethod]
    public async Task GatewayReplacementAndRemovalInvalidateCachedRevision()
    {
        using var fixture=new PhotoFixture();var cache=new BoundedProductPhotoCache(4*1024*1024);var gateway=new ProductPhotoUiGateway(fixture.Service,cache);
        var provisional=await gateway.ImportAsync(fixture.Product.Id,0,"technical-chart.png","PROVISIONAL",new(0,0,512),PhotoFixture.Chart());
        var real=await gateway.ImportAsync(fixture.Product.Id,provisional.Revision,"real-chart.png","REAL",new(0,0,512),PhotoFixture.Chart(alpha:0));
        Assert.AreEqual("Foto real",real.Status);Assert.AreNotEqual(provisional.PhotoId,real.PhotoId);Assert.AreEqual(1,cache.Count);
        var removed=await gateway.RemoveAsync(fixture.Product.Id,real.Revision);
        Assert.IsTrue(removed.IsFallback);Assert.AreEqual("Sin foto",removed.Status);Assert.AreEqual(0,cache.Count);
    }
    [TestMethod]
    public async Task MissingThumbnailUsesFallbackButPreservesRevisionForAdminRecovery()
    {
        using var fixture=new PhotoFixture();var selected=fixture.Import();var path=Path.Combine(fixture.DirectoryPath,ProductPhotoFileStore.ThumbnailPath(selected.Photo!));File.Delete(path);
        var gateway=new ProductPhotoUiGateway(fixture.Service,new BoundedProductPhotoCache());var result=await gateway.LoadAsync(fixture.Product.Id);
        Assert.IsTrue(result.IsFallback);Assert.AreEqual(1,result.Revision);Assert.AreEqual(selected.Photo!.Id,result.PhotoId);
        Assert.AreEqual("Foto no disponible",result.Status);Assert.IsFalse(string.IsNullOrWhiteSpace(result.Error));
    }
}
