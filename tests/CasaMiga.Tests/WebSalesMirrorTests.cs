using System.Net;
using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>D5: lo que vendieron las cajas web de la sucursal entra en los reportes de la caja, igual que en la web.</summary>
[TestClass]
public sealed class WebSalesMirrorTests : SalesFixture
{
    private static readonly Guid Till = Guid.Parse("e1000000-0000-4000-8000-000000000001");
    private static readonly Guid Session = Guid.Parse("e2000000-0000-4000-8000-000000000001");
    private static readonly Guid Stranger = Guid.Parse("e3000000-0000-4000-8000-000000000001");
    private ReportService Reports => new(work, user, TimeZoneInfo.Utc);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private SqliteWebSalesMirror Mirror => new(db);

    private static string At(int seconds) => DateTimeOffset.UtcNow.AddSeconds(seconds - 60).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

    // Mismo formato que escribe cloud/lib/pos.ts (el de la caja de escritorio).
    private List<WebSaleEvent> Events()
    {
        var admin = user.Current!.Id.ToString("D");
        long seq = 0;
        WebSaleEvent E(string entity, string id, JsonObject payload, string change = "insert") => new(Till, "CAJA-WEB-02", seq += 2, entity, id, change, payload);
        JsonObject Sale(string id, int sequence, string by, int quantity, string method, long received, string? reference, int at) => new()
        {
            ["sale"] = new JsonObject { ["id"] = id, ["operation_id"] = Guid.NewGuid().ToString(), ["user_id"] = by, ["terminal_id"] = Till.ToString(), ["cash_session_id"] = Session.ToString(),
                ["sequence"] = sequence, ["confirmed_utc"] = At(at), ["subtotal_cents"] = 1900 * quantity, ["total_cents"] = 1900 * quantity },
            ["items"] = new JsonArray(new JsonObject { ["sale_id"] = id, ["position"] = 1, ["product_id"] = product.Id.ToString(), ["product_name"] = "Concha",
                ["unit_price_cents"] = 1900, ["quantity"] = quantity, ["line_total_cents"] = 1900 * quantity }),
            ["payment"] = new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["sale_id"] = id, ["method"] = method, ["applied_cents"] = 1900 * quantity,
                ["received_cents"] = received, ["change_cents"] = received - 1900 * quantity, ["reference"] = reference },
        };
        var cashSale = "e4000000-0000-4000-8000-000000000001"; var cardSale = "e4000000-0000-4000-8000-000000000002"; var refund = "e5000000-0000-4000-8000-000000000001";
        var opened = new JsonObject { ["id"] = Session.ToString(), ["terminal_id"] = Till.ToString(), ["opened_by"] = admin, ["opened_utc"] = At(0), ["opening_cents"] = 50000, ["state"] = "OPEN",
            ["closed_utc"] = null, ["closed_by"] = null, ["expected_cents"] = null, ["counted_cents"] = null, ["difference_cents"] = null };
        var closed = opened.DeepClone().AsObject();
        closed["state"] = "CLOSED"; closed["closed_utc"] = At(9); closed["closed_by"] = admin; closed["expected_cents"] = 49500; closed["counted_cents"] = 49400; closed["difference_cents"] = -100;
        return [
            E("cash_session", Session.ToString(), opened),
            E("sale", cashSale, Sale(cashSale, 1, admin, 1, "CASH", 2000, null, 1)),
            E("sale", cardSale, Sale(cardSale, 2, Stranger.ToString(), 2, "CARD", 3800, "4601", 2)),
            E("sale_refund", refund, new JsonObject {
                ["refund"] = new JsonObject { ["id"] = refund, ["operation_id"] = Guid.NewGuid().ToString(), ["sale_id"] = cashSale, ["kind"] = "RETURN", ["reason"] = "Cliente lo regresó",
                    ["user_id"] = admin, ["terminal_id"] = Till.ToString(), ["cash_session_id"] = Session.ToString(), ["refunded_utc"] = At(3), ["total_cents"] = 1900 },
                ["items"] = new JsonArray(new JsonObject { ["refund_id"] = refund, ["sale_id"] = cashSale, ["position"] = 1, ["quantity"] = 1, ["line_total_cents"] = 1900 }) }),
            E("cash_movement", "e6000000-0000-4000-8000-000000000001", new JsonObject { ["id"] = "e6000000-0000-4000-8000-000000000001", ["operation_id"] = Guid.NewGuid().ToString(),
                ["cash_session_id"] = Session.ToString(), ["terminal_id"] = Till.ToString(), ["user_id"] = admin, ["kind"] = "EXPENSE", ["category"] = "GAS", ["amount_cents"] = 500, ["note"] = "", ["occurred_utc"] = At(4) }),
            E("cash_count", Session.ToString(), new JsonObject { ["session_id"] = Session.ToString(), ["counted_by"] = admin, ["counted_utc"] = At(5), ["counted_cents"] = 49400 }),
            E("cash_session", Session.ToString(), closed, "update"),
        ];
    }

    [TestMethod]
    public void WebSalesRefundsAndShiftsCountInTheReportsLikeOnTheWeb()
    {
        Persist(); // en esta caja: 2 conchas en efectivo, $38
        var events = Events();
        Assert.AreEqual(7, Mirror.Apply(events));
        Assert.AreEqual(14L, Mirror.Cursors()[Till]);
        Assert.AreEqual(7, Mirror.Apply(events), "Applying again changes nothing.");
        Assert.AreEqual(2L, Convert.ToInt64(Sql("SELECT count(*) FROM web_sale")));
        Assert.AreEqual(0L, Convert.ToInt64(Sql("SELECT count(*) FROM sync_outbox WHERE entity LIKE 'web%' OR entity_id IN (SELECT id FROM web_sale)")), "Nothing goes back up.");

        var report = Reports.Sales(Today, Today);
        Assert.AreEqual(3, report.Data.SaleCount);
        Assert.AreEqual(3800 + 1900 + 3800, report.Data.Gross.Cents);
        Assert.AreEqual(1900L, report.Data.Refunded.Cents);
        var concha = report.Data.Products.Single();
        Assert.AreEqual(5L, concha.QuantitySold); Assert.AreEqual(1L, concha.QuantityRefunded);
        CollectionAssert.AreEquivalent(new[] { "Usuario de la web", report.Data.Cashiers.Single(c => c.UserId == user.Current!.Id).Name },
            report.Data.Cashiers.Select(c => c.Name).ToArray(), "A web cashier not yet in this register still counts.");
        var card = report.Data.Payments!.Single(p => p.Method == PaymentMethod.Card);
        Assert.AreEqual(1, card.Sales); Assert.AreEqual(3800L, card.Sold.Cents);
        var inCash = report.Data.Payments!.Single(p => p.Method == PaymentMethod.Cash);
        Assert.AreEqual(2, inCash.Sales); Assert.AreEqual(1900L, inCash.Refunded.Cents);

        Assert.HasCount(2, report.Data.Sessions);
        var web = report.Data.Sessions.Single(s => s.SessionId == Session);
        Assert.IsTrue(web.Closed);
        Assert.AreEqual(1900L, web.CashSales.Cents, "Card sales do not enter the drawer.");
        Assert.AreEqual(1900L, web.Refunds.Cents); Assert.AreEqual(-500L, web.Movements.Cents);
        Assert.AreEqual(49500L, web.Expected.Cents); Assert.AreEqual(49400L, web.Counted!.Value.Cents); Assert.AreEqual(-100L, web.Difference!.Value.Cents);
        var local = report.Data.Sessions.Single(s => s.SessionId == cash.Id);
        Assert.AreEqual(3800L, local.CashSales.Cents, "The local shift is unchanged.");

        Assert.AreEqual(3, Reports.Compare(Today, Today).CurrentDays!.Sum(d => d.Sales), "Comparativo and Ventas por hora read the same facts.");
    }

    [TestMethod]
    public void ABrokenRecordIsSkippedWholeAndTheCursorStillMoves()
    {
        var events = Events();
        var sale = events[1].Payload.DeepClone().AsObject(); sale.Remove("items");
        Assert.AreEqual(0, Mirror.Apply([events[0] with { Entity = "audit_entry" }, events[1] with { Payload = sale }]));
        Assert.AreEqual(0L, Convert.ToInt64(Sql("SELECT count(*) FROM web_sale")), "No header without its lines.");
        Assert.AreEqual(4L, Mirror.Cursors()[Till]);
        Assert.AreEqual(0, Reports.Sales(Today, Today).Data.SaleCount);
    }

    [TestMethod]
    public void AClosedWebShiftDoesNotReopen()
    {
        var events = Events();
        Mirror.Apply(events);
        Mirror.Apply([events[0]]);
        Assert.AreEqual("CLOSED", Sql($"SELECT state FROM web_cash_session WHERE id='{Session:D}'"));
    }

    private sealed class Transport(List<WebSaleEvent> events) : ISyncTransport
    {
        public List<IReadOnlyDictionary<Guid, long>> Asked { get; } = [];
        public Task<string> RegisterAsync(Uri api, string enrollmentSecret, BranchIdentity branch, Guid terminalId, string terminalName, CancellationToken cancel) => Task.FromResult("K" + new string('k', 42));
        public Task<SyncBatchResult> SendAsync(Uri api, string terminalKey, Guid branchId, IReadOnlyList<SyncEnvelope> batch, CancellationToken cancel) =>
            Task.FromResult(new SyncBatchResult(batch.Select(b => b.Entry.Sequence).ToArray(), []));
        public Task<IReadOnlyList<WebSaleEvent>> PullWebSalesAsync(Uri api, string terminalKey, IReadOnlyDictionary<Guid, long> cursors, CancellationToken cancel)
        {
            Asked.Add(cursors);
            return Task.FromResult<IReadOnlyList<WebSaleEvent>>(events.Where(e => e.Sequence > cursors.GetValueOrDefault(e.Terminal)).ToList());
        }
    }

    [TestMethod]
    public async Task EachSyncRunBringsWhatIsNewFromTheWebTills()
    {
        var events = Events();
        var transport = new Transport(events.Take(3).ToList());
        var engine = new SyncEngine(new SqliteSyncOutbox(db), transport, directory, webSales: Mirror);
        await engine.RegisterAsync("https://sync.example.test/", "secreto-correcto", "Centro", terminal.Id, terminal.Name);
        Assert.AreEqual(SyncState.UpToDate, (await engine.RunOnceAsync()).State);
        Assert.AreEqual(2L, Convert.ToInt64(Sql("SELECT count(*) FROM web_sale")));
        transport = new Transport(events);
        engine = new SyncEngine(new SqliteSyncOutbox(db), transport, directory, webSales: Mirror);
        await engine.RunOnceAsync();
        Assert.AreEqual(6L, transport.Asked.Single()[Till], "Asks only for what comes after its cursor.");
        Assert.AreEqual(1L, Convert.ToInt64(Sql("SELECT count(*) FROM web_sale_refund")));
    }

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    [TestMethod]
    public async Task TheHttpTransportSendsTheCursorsAndToleratesAnOlderCloud()
    {
        var json = $$$"""{"events":[{"terminal":"{{{Till}}}","terminalName":"CAJA-WEB-02","seq":7,"entity":"cash_count","entityId":"{{{Session}}}","change":"insert","payload":{"session_id":"{{{Session}}}","counted_cents":49400}}]}""";
        var handler = new Handler(HttpStatusCode.OK, json);
        var events = await new HttpSyncTransport(new HttpClient(handler)).PullWebSalesAsync(new("https://sync.example.test/"), "key", new Dictionary<Guid, long> { [Till] = 5 }, default);
        Assert.AreEqual($"https://sync.example.test/api/sync/web-sales?after={Till:D}:5", handler.Urls.Single());
        Assert.AreEqual(7L, events.Single().Sequence); Assert.AreEqual("CAJA-WEB-02", events.Single().TerminalName);
        Assert.AreEqual(1, Mirror.Apply(events));
        Assert.AreEqual(49400L, Convert.ToInt64(Sql($"SELECT counted_cents FROM web_cash_count WHERE session_id='{Session:D}'")));

        var old = new Handler(HttpStatusCode.NotFound, """{"error":"No encontrado"}""");
        Assert.IsEmpty(await new HttpSyncTransport(new HttpClient(old)).PullWebSalesAsync(new("https://sync.example.test/"), "key", new Dictionary<Guid, long>(), default));
        Assert.AreEqual("https://sync.example.test/api/sync/web-sales", old.Urls.Single());
    }
}
