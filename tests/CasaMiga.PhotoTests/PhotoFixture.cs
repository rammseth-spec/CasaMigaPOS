using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;

namespace CasaMiga.PhotoTests;

internal sealed class PhotoFixture : IDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "CasaMigaPhotoTests", Guid.NewGuid().ToString("N"));
    internal string DirectoryPath => Path.Combine(Root, "instalación pruebas");
    internal Database Db { get; }
    internal SqliteLocalUnitOfWork Work { get; }
    internal UserSession User { get; } = new();
    internal string TestPassword { get; } = Guid.NewGuid().ToString("N") + "!Aa";
    internal TerminalIdentity Terminal { get; }
    internal CatalogService Catalog { get; }
    internal Product Product { get; }
    internal ProductPhotoFileStore Files { get; }
    internal SqliteProductPhotoStore Repository { get; }
    internal ProductPhotoService Service { get; }
    internal TestClock Clock { get; } = new();
    internal sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow; }
    internal PhotoFixture()
    {
        Directory.CreateDirectory(DirectoryPath);
        Db = new(Path.Combine(DirectoryPath, "casamiga.db")); Db.Initialize(); Work = new(Db);
        Terminal = TerminalConfiguration.LoadOrCreate(DirectoryPath);
        Work.Run(s => { s.EnsureTerminal(Terminal, Clock.UtcNow); return true; });
        var identity = new IdentityService(Work, new LocalPasswords(), Clock, User);
        identity.Bootstrap("photo-admin", "Synthetic photo administrator", TestPassword); identity.Login("photo-admin", TestPassword);
        Catalog = new(Work, User, Clock, Terminal.Id);
        Product = Catalog.CreateProduct(Catalog.CreateCategory("PHOTO FIXTURES").Id, null, "Synthetic chart", "Not a commercial photograph", new Money(1800));
        Files = new(DirectoryPath); Repository = new(Db); Service = WithProcessor(new WpfProductPhotoProcessor());
    }
    internal ProductPhotoService WithProcessor(IProductPhotoProcessor processor) => new(Repository, Files, processor, User, Clock, Terminal.Id);
    internal PhotoImport Request(long revision = 0, string provenance = "PROVISIONAL", Guid? operation = null, int edge = 512) =>
        new(Product.Id, revision, operation ?? Guid.NewGuid(), "technical-chart.png", provenance, new(0, 0, edge));
    internal PhotoSelection Import(long revision = 0, string provenance = "PROVISIONAL", byte[]? image = null) => Service.Import(Request(revision, provenance), new MemoryStream(image ?? Chart()));
    internal object? Sql(string sql) { using var c = Db.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = sql; return cmd.ExecuteScalar(); }
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static byte[] Chart(string format = "png", int width = 512, int height = 512, byte alpha = 255)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            int i = (y * width + x) * 4;
            pixels[i] = (byte)(y < height / 2 ? 20 : 220); pixels[i + 1] = 30;
            pixels[i + 2] = (byte)(x < width / 2 ? 240 : 40); pixels[i + 3] = alpha;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4); bitmap.Freeze();
        BitmapEncoder encoder = format == "jpeg" ? new JpegBitmapEncoder { QualityLevel = 95 } : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }
    internal static byte[] WithOrientation(byte[] jpeg, int orientation)
    {
        byte[] app = [0xFF,0xE1,0,34, 69,120,105,102,0,0, 73,73,42,0,8,0,0,0, 1,0, 0x12,1,3,0,1,0,0,0,(byte)orientation,0,0,0, 0,0,0,0];
        return [.. jpeg.AsSpan(0, 2), .. app, .. jpeg.AsSpan(2)];
    }
    internal static byte[] Pixels(byte[] image, out int width, out int height)
    {
        using var stream = new MemoryStream(image);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        width = frame.PixelWidth; height = frame.PixelHeight;
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[width * height * 4]; converted.CopyPixels(pixels, width * 4, 0); return pixels;
    }
    internal static void BuildLegacy(string directory, int version)
    {
        Directory.CreateDirectory(directory); var db = new Database(Path.Combine(directory, "casamiga.db"));
        using var c = db.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT"; cmd.ExecuteNonQuery();
        var assembly = typeof(Database).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(x => x.EndsWith(".sql", StringComparison.Ordinal)).Order(StringComparer.Ordinal).Take(version))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!); var sql = reader.ReadToEnd();
            cmd.CommandText = sql; cmd.ExecuteNonQuery(); cmd.CommandText = "INSERT INTO schema_migration VALUES((SELECT count(*)+1 FROM schema_migration),$hash,$at)";
            cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$hash", Hash(Encoding.UTF8.GetBytes(sql))); cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O")); cmd.ExecuteNonQuery(); cmd.Parameters.Clear();
        }
        var terminal = TerminalConfiguration.LoadOrCreate(directory);
        new SqliteLocalUnitOfWork(db).Run(s => { s.EnsureTerminal(terminal, DateTimeOffset.UtcNow); return true; });
    }
    public void Dispose() => Directory.Delete(Root, true);
}
