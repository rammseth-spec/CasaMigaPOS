using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>Íconos ilustrados del menú lateral (generados con IA, assets/menu-ia/manifest.json): la caja y la web usan los mismos archivos,
/// 128 × 128 con transparencia, y cada botón del menú lleva el suyo en lugar del ícono de trazo.</summary>
[TestClass]
public sealed class MenuIconsTests
{
    private static string Menu(string path) => Path.Combine(AppContext.BaseDirectory, "Menu", path);
    private static string Presentation(string path) => Path.Combine(AppContext.BaseDirectory, "Presentation", path);

    [TestMethod]
    public void EverySidebarButtonUsesAnIllustratedIconSharedWithTheWeb()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Menu("manifest.json")));
        var icons = manifest.RootElement.GetProperty("icons").EnumerateArray().ToList();
        Assert.HasCount(14, icons, "Venta, Caja, Charolas, Ventas, Tickets, Reportes, Diario, Catálogo, Inventario, Recetas, Producción, Usuarios, Nube y Salir.");
        var theme = File.ReadAllText(Presentation(Path.Combine("Resources", "Theme.xaml")));
        var shell = File.ReadAllText(Presentation("MainWindow.xaml"));
        foreach (var icon in icons)
        {
            var file = icon.GetProperty("file").GetString()!;
            var desktop = File.ReadAllBytes(Menu(Path.Combine("caja", file)));
            Assert.AreEqual(icon.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(desktop)), file);
            CollectionAssert.AreEqual(desktop, File.ReadAllBytes(Menu(Path.Combine("web", file))), $"{file}: la caja y la web usan el mismo archivo.");
            // PNG de 128 × 128, RGBA de 8 bits (con transparencia).
            Assert.AreEqual(128, BinaryPrimitives.ReadInt32BigEndian(desktop.AsSpan(16, 4)), file);
            Assert.AreEqual(128, BinaryPrimitives.ReadInt32BigEndian(desktop.AsSpan(20, 4)), file);
            Assert.AreEqual((byte)8, desktop[24], file);
            Assert.AreEqual((byte)6, desktop[25], file);
            var name = Path.GetFileNameWithoutExtension(file);
            var key = "Nav" + char.ToUpperInvariant(name[0]) + name[1..];
            StringAssert.Contains(theme, $"<BitmapImage x:Key=\"{key}\" UriSource=\"/CasaMiga.Presentation;component/Assets/Nav/{file}\"/>");
            StringAssert.Contains(shell, $"Tag=\"{{StaticResource {key}}}\"");
        }
        Assert.AreEqual(icons.Count, Regex.Matches(shell, "Tag=\"\\{StaticResource Nav").Count);
        foreach (var line in shell.Split('\n').Where(l => l.Contains("SidebarNavButton", StringComparison.Ordinal)))
            Assert.IsFalse(line.Contains("Tag=\"{StaticResource Icon", StringComparison.Ordinal), "Botón del menú con ícono de trazo: " + line.Trim());
        StringAssert.Contains(theme, "<Image x:Name=\"NavIcon\" Source=\"{Binding Tag,RelativeSource={RelativeSource TemplatedParent}}\" Width=\"28\" Height=\"28\"");
    }
}
