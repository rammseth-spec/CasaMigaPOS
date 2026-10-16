using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class Phase1K1ShellTests
{
    private static string Presentation(string path) => Path.Combine(AppContext.BaseDirectory, "Presentation", path);

    [TestMethod]
    public void ShellAndThemeAreWellFormedAndUseCentralResources()
    {
        var app = File.ReadAllText(Presentation("App.xaml"));
        var shell = File.ReadAllText(Presentation("MainWindow.xaml"));
        var shellCode = File.ReadAllText(Presentation("MainWindow.xaml.cs"));
        var pos = File.ReadAllText(Presentation("PosView.xaml"));
        var catalog = File.ReadAllText(Presentation("CatalogView.xaml"));
        var crop = File.ReadAllText(Presentation("PhotoCropDialog.xaml"));
        var theme = File.ReadAllText(Presentation(Path.Combine("Resources", "Theme.xaml")));
        _ = XDocument.Parse(app); _ = XDocument.Parse(shell); _ = XDocument.Parse(pos); _ = XDocument.Parse(catalog); _ = XDocument.Parse(crop); _ = XDocument.Parse(theme);

        StringAssert.Contains(app, "Resources/Theme.xaml");
        foreach (var resource in new[] { "BackgroundBrush", "SurfaceBrush", "SurfaceSecondaryBrush", "PrimaryBrush",
                     "PrimaryHoverBrush", "TextPrimaryBrush", "TextSecondaryBrush", "BorderBrush", "SuccessBrush",
                     "WarningBrush", "ErrorBrush", "DisabledBrush", "WheatBrush", "SidebarNavButton", "CardStyle", "StatusPill",
                     "CasaMigaArtisanBread", "CasaMigaNaturalBackground", "CasaMigaSoftWheat", "CasaMigaAvocadoLeafGreen" })
            StringAssert.Contains(theme, $"x:Key=\"{resource}\"");

        // Rediseño A (07-10-2026): riel de íconos con nombres cortos; el nombre completo queda en AutomationProperties.Name.
        foreach (var section in new[] { "Ir a Punto de venta", "Ir a Caja", "Ir a Charolas", "Ir a Historial de ventas", "Ir a Reportes", "Ir a Reporte diario", "Ir a Catálogo",
                     "CurrentUserName", "CurrentRoleLabel", "CurrentTerminalName", "IsSignedOut", "IsNotPos" })
            StringAssert.Contains(shell, section);
        Assert.AreEqual(1, Count(shell, "AutomationProperties.Name=\"Salir de mi usuario\""));
        Assert.AreEqual(0, Count(pos, "Salir de usuario"));
        StringAssert.Contains(shell, "CanViewInventory"); StringAssert.Contains(shell, "CanViewRecipes");
        StringAssert.Contains(shell, "CanViewProduction"); StringAssert.Contains(shell, "CanViewSales");
        StringAssert.Contains(shell, "MinWidth=\"960\""); StringAssert.Contains(shell, "MinHeight=\"640\"");
        StringAssert.Contains(theme, "#4B2E19"); StringAssert.Contains(theme, "#FDF8EF"); StringAssert.Contains(theme, "#A67C52");
        StringAssert.Contains(theme, "BEREL 405N");
        StringAssert.Contains(theme, "#4A5D4E");
        StringAssert.Contains(theme, "AvocadoLeafBrush");
        Assert.IsFalse(shell.Contains("Opcion07", StringComparison.Ordinal));
        Assert.AreEqual(2, Count(shell, "CasaMigaLogo.png"));
        Assert.AreEqual(2, Count(shell, "Stretch=\"Uniform\""));
        Assert.AreEqual(2, Count(shell, "RenderOptions.BitmapScalingMode=\"HighQuality\""));
        StringAssert.Contains(shell, "Assets/CasaMiga.ico");
        StringAssert.Contains(shell, "FullScreenButton");
        StringAssert.Contains(shell, "Pantalla completa · F11");
        StringAssert.Contains(shellCode, "Key.F11");
        StringAssert.Contains(shellCode, "ToggleFullScreen");
        StringAssert.Contains(shellCode, "if (Model.CanNavigate) EnterFullScreen()");
        StringAssert.Contains(shellCode, "ExitFullScreen();LoginPassword.Clear();Model.Logout()");
        StringAssert.Contains(shellCode, "WindowStyle.None");
        StringAssert.Contains(theme, "TextElement.Foreground=\"{TemplateBinding Foreground}\"");
        StringAssert.Contains(pos,"ProductTile");StringAssert.Contains(pos,"{Binding Photo}");StringAssert.Contains(pos,"{Binding IsFallback");
        Assert.IsFalse(pos.Contains("{Binding CategoryName}",StringComparison.Ordinal));StringAssert.Contains(pos,"{Binding Price}");StringAssert.Contains(pos,"CommandParameter=\"{Binding Id}\"");
        StringAssert.Contains(pos,"ProductCatalogScroll");StringAssert.Contains(pos,"Width=\"7*\"");StringAssert.Contains(pos,"Width=\"3*\"");
        StringAssert.Contains(pos,"local:TileGrid");StringAssert.Contains(pos,"{Binding CartQuantity}");StringAssert.Contains(pos,"IsVisibleChanged=\"PosVisibilityChanged\"");
        Assert.IsFalse(pos.Contains("PhotoStatus",StringComparison.Ordinal));
        StringAssert.Contains(catalog,"IMAGEN DEL PRODUCTO");StringAssert.Contains(catalog,"CurrentPhotoStatus");StringAssert.Contains(catalog,"Foto provisional");StringAssert.Contains(catalog,"Foto real");
        StringAssert.Contains(catalog,"ChangePhotoClick");StringAssert.Contains(catalog,"RemovePhotoClick");
        StringAssert.Contains(crop,"CropRectangle");StringAssert.Contains(crop,"OverlayMouseDown");StringAssert.Contains(crop,"ResizeHandle");StringAssert.Contains(crop,"Ajuste avanzado por coordenadas");
        StringAssert.Contains(shell,"SidebarNavButton");StringAssert.Contains(shell,"CashStatusLabel");StringAssert.Contains(shell,"UserInitials");StringAssert.Contains(shell,"StatusPill");
        StringAssert.Contains(theme,"x:Name=\"NavLabel\"");StringAssert.Contains(theme,"x:Name=\"NavIcon\"");StringAssert.Contains(theme,"x:Key=\"ProductTile\"");StringAssert.Contains(theme,"TargetType=\"ScrollBar\"");
        var smoke=File.ReadAllText(Presentation("Phase1K2CSmoke.Program.cs"));
        Assert.AreEqual(26,Count(smoke,"new AssetSource("));StringAssert.Contains(smoke,"mapping.json");StringAssert.Contains(smoke,"Provenance=\"PROVISIONAL\"");
    }

    [TestMethod]
    public void ShellUsesDerivativeOfAuthorizedVariant04Master()
    {
        var masterBytes = File.ReadAllBytes(Presentation(Path.Combine("Assets", "CasaMiga_LOGO_OFICIAL_VECTOR.svg")));
        var master = System.Text.Encoding.UTF8.GetString(masterBytes);
        var derivative = File.ReadAllBytes(Presentation(Path.Combine("Assets", "CasaMigaLogo.png")));

        StringAssert.Contains(master, "viewBox=\"0 0 1540 893\"");
        StringAssert.Contains(master, "fill=\"#4B2E19\"");
        Assert.AreEqual(1, Count(master, "<path "));
        Assert.AreEqual("7E67467CCF778EF40C40E8D15716AD550E1DA3A11C7D592D57578531A8228423",
            Convert.ToHexString(SHA256.HashData(masterBytes)));
        Assert.AreEqual("A1858372624CDDC87091A3DCFCBC5FA5E21F8C2621C5D5C6E2ACEE6D363B06CC",
            Convert.ToHexString(SHA256.HashData(derivative)));
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, derivative[..8]);
        Assert.AreEqual(3080, BinaryPrimitives.ReadInt32BigEndian(derivative.AsSpan(16, 4)));
        Assert.AreEqual(1786, BinaryPrimitives.ReadInt32BigEndian(derivative.AsSpan(20, 4)));

        var icon = File.ReadAllBytes(Presentation(Path.Combine("Assets", "CasaMiga.ico")));
        Assert.AreEqual("A1C759AF8A61B098FBE1F5EF45CAF410FCC0419491C7E2B1085B1AB9217CFD58",
            Convert.ToHexString(SHA256.HashData(icon)));
        CollectionAssert.AreEqual(new byte[] { 0, 0, 1, 0 }, icon[..4]);
    }

    private static int Count(string value, string find)
    {
        var count = 0; var index = 0;
        while ((index = value.IndexOf(find, index, StringComparison.Ordinal)) >= 0) { count++; index += find.Length; }
        return count;
    }
}
