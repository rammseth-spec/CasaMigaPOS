using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

/// <summary>Rendimiento en computadoras modestas: fotos decodificadas al tamaño en que se ven y modo ligero sin sombras.</summary>
[TestClass]
public sealed class LowEndPerformanceTests
{
    private static byte[] Jpeg(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 7);
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 }; encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }
    [STATestMethod]
    public void PosThumbnailIsDecodedAtDisplaySizeNotFullSize()
    {
        var decoded = ProductPhotoUiGateway.Decode(Jpeg(512, 512), ProductPhotoUiGateway.DefaultDisplayEdge);
        Assert.AreEqual(320, decoded.PixelWidth); Assert.AreEqual(320, decoded.PixelHeight); Assert.IsTrue(decoded.IsFrozen);
        var large = ProductPhotoUiGateway.Decode(Jpeg(768, 768), 320);
        Assert.AreEqual(320, large.PixelWidth);
    }
    [STATestMethod]
    public void DecodeKeepsProportionAndNeverEnlarges()
    {
        var wide = ProductPhotoUiGateway.Decode(Jpeg(600, 400), 320);
        Assert.AreEqual(320, wide.PixelHeight); Assert.AreEqual(480, wide.PixelWidth);
        var tall = ProductPhotoUiGateway.Decode(Jpeg(400, 600), 320);
        Assert.AreEqual(320, tall.PixelWidth); Assert.AreEqual(480, tall.PixelHeight);
        var small = ProductPhotoUiGateway.Decode(Jpeg(256, 256), 320);
        Assert.AreEqual(256, small.PixelWidth);
    }
    [TestMethod]
    public void DisplayEdgeFollowsScreenScale()
    {
        Assert.AreEqual(320, ProductPhotoUiGateway.DisplayEdgeFor(1));
        Assert.AreEqual(400, ProductPhotoUiGateway.DisplayEdgeFor(1.25));
        Assert.AreEqual(480, ProductPhotoUiGateway.DisplayEdgeFor(1.5));
        Assert.AreEqual(320, ProductPhotoUiGateway.DisplayEdgeFor(0.5));
        Assert.AreEqual(320, ProductPhotoUiGateway.DisplayEdgeFor(double.NaN));
        Assert.AreEqual(1280, ProductPhotoUiGateway.DisplayEdgeFor(10));
    }
    [TestMethod]
    public void LightModeTurnsOnWithoutGraphicsAccelerationAndCanBeForced()
    {
        Assert.IsTrue(LightRendering.Decide(0, null)); Assert.IsTrue(LightRendering.Decide(1, ""));
        Assert.IsFalse(LightRendering.Decide(2, null)); Assert.IsFalse(LightRendering.Decide(2, "otro"));
        Assert.IsTrue(LightRendering.Decide(2, "1")); Assert.IsFalse(LightRendering.Decide(0, " 0 "));
    }
    [TestMethod]
    public Task LightModeRemovesThemeShadowsAndMenuFade() => WpfTestHost.OnUi(() =>
    {
        var theme = new ResourceDictionary { Source = new Uri("/CasaMiga.Presentation;component/Resources/Theme.xaml", UriKind.Relative) };
        foreach (var key in LightRendering.ShadowKeys) Assert.IsInstanceOfType<DropShadowEffect>(theme[key], key);
        Assert.AreEqual(PopupAnimation.Fade, theme[LightRendering.PopupAnimationKey]);
        var resources = new ResourceDictionary(); resources.MergedDictionaries.Add(theme);
        var card = new Border { Resources = resources }; card.SetResourceReference(UIElement.EffectProperty, "ShadowDialog");
        var dialog = new Border { Resources = resources, Style = (Style)theme["DialogCard"] };
        var host = new StackPanel { Children = { card, dialog } }; WpfTestHost.Layout(host, 300, 300);
        Assert.IsInstanceOfType<DropShadowEffect>(card.Effect); Assert.IsInstanceOfType<DropShadowEffect>(dialog.Effect);
        Assert.AreNotSame(card.Effect, dialog.Effect, "Cada elemento recibe su propia sombra, como antes con la sombra en línea.");
        LightRendering.ApplyResources(resources);
        Assert.IsNull(card.Effect); Assert.IsNull(dialog.Effect);
        Assert.AreEqual(PopupAnimation.None, resources[LightRendering.PopupAnimationKey]);
    });
    [TestMethod]
    public void ThemeHasNoInlineShadowsOutsideTheLightModeResources()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var presentation = Path.Combine(root, "src", "CasaMiga.Presentation");
        foreach (var file in Directory.EnumerateFiles(presentation, "*.xaml", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(presentation, "*.cs", SearchOption.AllDirectories))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var lines = File.ReadAllLines(file).Where(l => l.Contains("DropShadowEffect") && !l.Contains("x:Key=\"Shadow")).ToArray();
            Assert.AreEqual(0, lines.Length, $"{Path.GetFileName(file)} tiene una sombra que el modo ligero no puede quitar: {string.Join(" | ", lines)}");
        }
    }
}
