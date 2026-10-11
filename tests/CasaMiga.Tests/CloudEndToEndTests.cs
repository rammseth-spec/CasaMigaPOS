using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>
/// De punta a punta: esta caja (SQLite, SyncEngine y HttpSyncTransport reales) contra la nube local (cloud/scripts/e2e-server.ts, PGlite y
/// las mismas rutas que en Vercel) y la web usada por HTTP como el navegador. Necesita Node y cloud/node_modules; sin ellos se omite
/// (en CI corre en el trabajo de la web, donde sí están).
/// </summary>
[TestClass, DoNotParallelize]
public sealed class CloudEndToEndTests : SalesFixture
{
    private const string Secret = "secreto-de-alta-de-la-prueba-e2e";

    private sealed class Cloud : IDisposable
    {
        private readonly Process process;
        public Uri Url { get; }
        public HttpClient Web { get; }
        private Cloud(Process process, Uri url)
        {
            this.process = process; Url = url;
            Web = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = url, Timeout = TimeSpan.FromSeconds(20) };
        }
        public static Cloud? Start()
        {
            var root = AppContext.BaseDirectory;
            while (root is not null && !File.Exists(Path.Combine(root, "CasaMigaPOS.slnx"))) root = Path.GetDirectoryName(root);
            var cloud = root is null ? null : Path.Combine(root, "cloud");
            if (cloud is null || !Directory.Exists(Path.Combine(cloud, "node_modules", "tsx"))) return null;
            Process process;
            try
            {
                process = Process.Start(new ProcessStartInfo("node", ["--import", "tsx", "scripts/e2e-server.ts", Secret])
                    { WorkingDirectory = cloud, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
            }
            catch (System.ComponentModel.Win32Exception) { return null; } // Sin Node.
            var line = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("La nube local no arrancó: " + process.StandardError.ReadToEnd());
            return new Cloud(process, new Uri((string)JsonNode.Parse(line)!["url"]! + "/"));
        }
        public async Task<JsonNode> Get(string op, string query = "")
        {
            using var response = await Web.GetAsync($"api/web/{op}{query}");
            var body = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode, $"{op}: {body}");
            return JsonNode.Parse(body)!;
        }
        public async Task<JsonNode> Post(string op, object body)
        {
            using var response = await Web.PostAsJsonAsync($"api/web/{op}", body);
            var text = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode, $"{op}: {text}");
            return JsonNode.Parse(text)!;
        }
        public void Dispose()
        {
            Web.Dispose();
            try { process.StandardInput.Close(); if (!process.WaitForExit(5000)) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process.Dispose();
        }
    }

    [TestMethod]
    public async Task WebChangesAndWebSalesMeetThisRegisterThroughTheRealCloud()
    {
        using var cloud = Cloud.Start();
        if (cloud is null) { Assert.Inconclusive("Sin Node o sin cloud/node_modules: se omite la prueba de punta a punta."); return; }
        var engine = new SyncEngine(new SqliteSyncOutbox(db), new HttpSyncTransport(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }), directory,
            commands: new CloudCommandService(work, clock, terminal.Id, TimeZoneInfo.Utc), webSales: new SqliteWebSalesMirror(db));

        // 1. Alta de la caja y primera subida: usuarios, permisos, catálogo, turno y una venta del mostrador.
        await engine.RegisterAsync(cloud.Url.ToString(), Secret, "Centro", terminal.Id, terminal.Name);
        Persist();
        Assert.AreEqual(SyncState.UpToDate, (await engine.RunOnceAsync()).State, engine.Status.Detail);

        // 2. La web entra con la misma contraseña de la caja y ve el catálogo que subió.
        var me = await cloud.Post("login", new { username = "admin", password });
        Assert.AreEqual("Administrator", (string)me["role"]!);
        var branch = (string)(await cloud.Get("me"))["branches"]![0]!["id"]!;
        var concha = (await cloud.Get("catalog", $"?branch={branch}"))["products"]!.AsArray().Single(p => (string)p!["id"]! == product.Id.ToString())!;
        Assert.AreEqual(1900, (int)concha["price"]!); Assert.IsFalse((bool)concha["pending"]!);

        // 3. Con la caja «apagada» (sin sincronizar): nuevo precio, una categoría repetida que la caja va a rechazar y una venta web.
        await cloud.Post("command", new { branchId = branch, kind = "product.save", payload = new {
            id = product.Id, categoryId = product.CategoryId, sku = (string?)null, name = product.Name, description = "", priceCents = 2100, trackInventory = true } });
        var duplicate = Guid.NewGuid();
        await cloud.Post("command", new { branchId = branch, kind = "category.save", payload = new { id = duplicate, name = "pan", description = "", sortOrder = 9 } });
        var catalog = await cloud.Get("catalog", $"?branch={branch}");
        concha = catalog["products"]!.AsArray().Single(p => (string)p!["id"]! == product.Id.ToString())!;
        Assert.AreEqual(2100, (int)concha["price"]!, "The new price is visible on the web at once.");
        Assert.IsTrue((bool)concha["pending"]!);
        Assert.IsTrue(catalog["categories"]!.AsArray().Any(c => (string)c!["id"]! == duplicate.ToString()));

        var till = await cloud.Post("pos-till", new { branchId = branch });
        var session = await cloud.Post("pos-open", new { tillId = (string)till["id"]!, openingCents = 0 });
        var sale = await cloud.Post("pos-sale", new { tillId = (string)till["id"]!, sessionId = (string)session["sessionId"]!, operationId = Guid.NewGuid(),
            lines = new[] { new { productId = product.Id, quantity = 2 } }, method = "CASH", receivedCents = 5000 });
        Assert.AreEqual(4200, (int)sale["total"]!, "The web sells at the price it already shows.");
        Assert.AreEqual("C02-000001", (string)sale["folio"]!);

        // 4. La caja se conecta: aplica el precio, rechaza la categoría repetida y baja la venta web para sus reportes.
        Assert.AreEqual(SyncState.UpToDate, (await engine.RunOnceAsync()).State, engine.Status.Detail);
        Assert.AreEqual(2100L, work.Run(s => s.FindProduct(product.Id))!.SalePrice.Cents);
        Assert.IsNull(work.Run(s => s.FindCategory(duplicate)));
        var report = new ReportService(work, user, TimeZoneInfo.Utc).Sales(DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.AreEqual(2, report.Data.SaleCount, "The counter sale and the web sale.");
        Assert.AreEqual(3800 + 4200, report.Data.Gross.Cents);

        // 5. La web queda igual a la caja: precio confirmado, la categoría rechazada desaparece y su motivo queda en los cambios.
        catalog = await cloud.Get("catalog", $"?branch={branch}");
        concha = catalog["products"]!.AsArray().Single(p => (string)p!["id"]! == product.Id.ToString())!;
        Assert.AreEqual(2100, (int)concha["price"]!); Assert.IsFalse((bool)concha["pending"]!);
        Assert.IsFalse(catalog["categories"]!.AsArray().Any(c => (string)c!["id"]! == duplicate.ToString()), "Rejected by the register: it goes back.");
        var commands = (await cloud.Get("commands", $"?branch={branch}")).AsArray();
        Assert.AreEqual("applied", (string)commands.Single(c => (string)c!["kind"]! == "product.save")!["status"]!);
        var rejected = commands.Single(c => (string)c!["kind"]! == "category.save")!;
        Assert.AreEqual("rejected", (string)rejected["status"]!);
        StringAssert.Contains((string)rejected["message"]!, "categor", StringComparison.OrdinalIgnoreCase);

        // 6. Otra vuelta no repite nada.
        await engine.RunOnceAsync();
        Assert.AreEqual(1L, Convert.ToInt64(Sql("SELECT count(*) FROM web_sale")));
    }
}
