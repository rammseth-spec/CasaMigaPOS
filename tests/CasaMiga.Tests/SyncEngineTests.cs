using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class SyncEngineTests : SalesFixture
{
    private const string Api = "https://sync.example.test/";

    private sealed class FakeTransport : ISyncTransport
    {
        public List<IReadOnlyList<SyncEnvelope>> Batches { get; } = [];
        public Func<IReadOnlyList<SyncEnvelope>, SyncBatchResult>? Respond { get; set; }
        public Exception? Fail { get; set; }
        public string? LastKey { get; private set; }
        public Task<string> RegisterAsync(Uri api, string secret, BranchIdentity branch, Guid terminalId, string terminalName, CancellationToken cancel)
            => secret == "secreto-correcto" ? Task.FromResult("K" + new string('k', 42)) : throw new SyncUnauthorizedException("El secreto de alta no es correcto.");
        public Task<SyncBatchResult> SendAsync(Uri api, string key, Guid branchId, IReadOnlyList<SyncEnvelope> batch, CancellationToken cancel)
        {
            LastKey = key;
            if (Fail is { } error) throw error;
            Batches.Add(batch);
            return Task.FromResult(Respond?.Invoke(batch) ?? new SyncBatchResult(batch.Select(b => b.Entry.Sequence).ToArray(), []));
        }
    }

    private (SyncEngine Engine, FakeTransport Transport, SqliteSyncOutbox Outbox) Engine()
    {
        var transport = new FakeTransport(); var outbox = new SqliteSyncOutbox(db);
        return (new SyncEngine(outbox, transport, directory), transport, outbox);
    }
    private async Task<(SyncEngine, FakeTransport, SqliteSyncOutbox)> Registered()
    {
        var parts = Engine();
        await parts.Engine.RegisterAsync(Api, "secreto-correcto", "Sucursal Centro", terminal.Id, terminal.Name);
        return parts;
    }

    [TestMethod]
    public async Task UnregisteredTerminalKeepsQueueAndReportsNotConfigured()
    {
        Persist();
        var (engine, transport, outbox) = Engine();
        var status = await engine.RunOnceAsync();
        Assert.AreEqual(SyncState.NotConfigured, status.State);
        Assert.IsEmpty(transport.Batches);
        Assert.IsTrue(outbox.PendingCount() > 0);
    }

    [TestMethod]
    public async Task RegistrationStoresProtectedKeyAndRenamesBranch()
    {
        var (engine, transport, outbox) = Engine();
        await Assert.ThrowsExactlyAsync<SyncUnauthorizedException>(() => engine.RegisterAsync(Api, "mal", "Centro", terminal.Id, terminal.Name));
        Assert.IsNull(SyncSettings.Load(directory));
        Assert.AreEqual("Sucursal principal", outbox.Branch().Name, "A rejected registration leaves the local branch name untouched.");
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => engine.RegisterAsync("http://inseguro.example/", "secreto-correcto", "Centro", terminal.Id, terminal.Name));

        await engine.RegisterAsync(Api, "secreto-correcto", "Sucursal Centro", terminal.Id, terminal.Name);
        Assert.AreEqual("Sucursal Centro", outbox.Branch().Name);
        var raw = File.ReadAllText(Path.Combine(directory, SyncSettings.FileName));
        Assert.DoesNotContain("kkkkkkkkkk", raw, "The terminal key is never stored in plain text.");
        Assert.AreEqual("K" + new string('k', 42), SyncSettings.Load(directory)!.TerminalKey());
    }

    [TestMethod]
    public async Task SendsWholeQueueInOrderedBatchesWithFullPayloads()
    {
        var sale = Persist();
        for (int i = 0; i < 120; i++) work.Run(s => { s.AddAudit(new(Guid.NewGuid(), user.Current!.Id, terminal.Id, "test.bulk", Guid.NewGuid(), clock.UtcNow, "{}")); return true; });
        new RefundService(work, user, clock, terminal.Id).Return(sale.Id, [new(1, 1)], "Devolución");
        var (engine, transport, outbox) = await Registered();

        var status = await engine.RunOnceAsync();
        Assert.AreEqual(SyncState.UpToDate, status.State);
        Assert.AreEqual(0L, outbox.PendingCount());
        Assert.IsTrue(transport.Batches.Count >= 2 && transport.Batches.All(b => b.Count <= SyncEngine.BatchSize));
        var all = transport.Batches.SelectMany(b => b).ToList();
        CollectionAssert.AreEqual(all.Select(e => e.Entry.Sequence).Order().ToArray(), all.Select(e => e.Entry.Sequence).ToArray(), "Dependencies keep queue order.");

        var salePayload = all.Single(e => e.Entry.Entity == "sale").Payload;
        Assert.AreEqual(sale.Id.ToString("D"), (string?)salePayload["sale"]!["id"]);
        Assert.AreEqual(1, salePayload["items"]!.AsArray().Count);
        Assert.AreEqual("CASH", (string?)salePayload["payment"]!["method"]);
        var refund = all.Single(e => e.Entry.Entity == "sale_refund").Payload;
        Assert.AreEqual(1L, (long)refund["items"]![0]!["quantity"]!);
        // La versión web acepta la misma contraseña de la caja: sólo sale el hash PBKDF2 (formato Identity V3), nunca la contraseña,
        // ni intentos fallidos o bloqueos, ni la llave de la terminal.
        var users = all.Where(e => e.Entry.Entity == "app_user").Select(e => e.Payload).ToList();
        Assert.IsNotEmpty(users);
        Assert.IsTrue(users.All(u => ((string?)u["password_hash"])!.StartsWith("AQAAAAIAA", StringComparison.Ordinal)), "PBKDF2-HMAC-SHA512 hash only.");
        Assert.IsTrue(users.All(u => u["failed_attempts"] is null && u["locked_until_utc"] is null));
        Assert.IsFalse(all.Any(e => e.Payload.ToJsonString().Contains(password, StringComparison.Ordinal)), "The password itself never leaves the terminal.");
        Assert.IsFalse(all.Any(e => e.Payload.ToJsonString().Contains(new string('k', 42), StringComparison.Ordinal)), "Nor the terminal key.");
        Assert.AreEqual("K" + new string('k', 42), transport.LastKey);
    }

    [TestMethod]
    public async Task NetworkFailureKeepsEverythingPendingAndRetriesLater()
    {
        Persist();
        var (engine, transport, outbox) = await Registered();
        var pending = outbox.PendingCount();
        transport.Fail = new HttpRequestException("sin internet");
        var status = await engine.RunOnceAsync();
        Assert.AreEqual(SyncState.Offline, status.State);
        Assert.AreEqual(pending, outbox.PendingCount());
        Assert.AreEqual(1, outbox.Pending(1)[0].Attempts);

        transport.Fail = null;
        Assert.AreEqual(SyncState.UpToDate, (await engine.RunOnceAsync()).State);
        Assert.AreEqual(0L, outbox.PendingCount());
    }

    [TestMethod]
    public async Task ConflictsStopSendingAndStayPending()
    {
        Persist();
        var (engine, transport, outbox) = await Registered();
        long conflicted = 0;
        transport.Respond = batch => { conflicted = batch[1].Entry.Sequence; return new(batch.Where(b => b.Entry.Sequence != conflicted).Select(b => b.Entry.Sequence).ToArray(), [conflicted]); };
        var status = await engine.RunOnceAsync();
        Assert.AreEqual(SyncState.NeedsAttention, status.State);
        Assert.AreEqual(1L, outbox.PendingCount());
        Assert.AreEqual(conflicted, outbox.Pending(1)[0].Sequence);
    }

    [TestMethod]
    public async Task RejectedTerminalNeedsAttentionAndIgnoresUnknownConfirmations()
    {
        Persist();
        var (engine, transport, outbox) = await Registered();
        transport.Fail = new SyncUnauthorizedException("revocada");
        Assert.AreEqual(SyncState.NeedsAttention, (await engine.RunOnceAsync()).State);

        transport.Fail = null;
        transport.Respond = batch => new([999_999], []);
        Assert.AreEqual(SyncState.NeedsAttention, (await engine.RunOnceAsync()).State, "Confirmations for sequences that were not sent are ignored.");
        Assert.IsTrue(outbox.PendingCount() > 0);
    }

    [TestMethod]
    public async Task HttpTransportSendsBearerKeyAndProtocolShape()
    {
        var sale = Persist();
        var handler = new RecordingHandler();
        var transport = new HttpSyncTransport(new HttpClient(handler));
        var outbox = new SqliteSyncOutbox(db);
        var batch = outbox.PendingEnvelopes(200);
        var key = new string('a', 43);
        var result = await transport.SendAsync(new Uri(Api), key, outbox.Branch().Id, batch, default);

        Assert.AreEqual("Bearer " + key, handler.Authorization);
        Assert.AreEqual("https://sync.example.test/api/sync/batch", handler.Url);
        var body = JsonNode.Parse(handler.Body!)!;
        Assert.AreEqual(outbox.Branch().Id.ToString("D"), (string?)body["branchId"]);
        Assert.AreEqual(batch.Count, body["entries"]!.AsArray().Count);
        Assert.IsTrue(body["entries"]!.AsArray().Any(e => (string?)e!["entity"] == "sale" && (string?)e["entityId"] == sale.Id.ToString("D")));
        CollectionAssert.AreEqual(batch.Select(b => b.Entry.Sequence).ToArray(), result.Accepted.ToArray());

        handler.Status = HttpStatusCode.Unauthorized;
        await Assert.ThrowsExactlyAsync<SyncUnauthorizedException>(() => transport.SendAsync(new Uri(Api), key, Guid.NewGuid(), batch, default));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadGateway, DisplayName = "502 con página HTML del gateway")]
    [DataRow(HttpStatusCode.OK, DisplayName = "200 con portal cautivo de Wi-Fi")]
    public async Task NonJsonServerResponsesCountAsOfflineAndKeepQueue(HttpStatusCode status)
    {
        Persist();
        var outbox = new SqliteSyncOutbox(db);
        SyncSettings.Create(new Uri(Api), outbox.Branch().Id, new string('a', 43)).Save(directory);
        var engine = new SyncEngine(outbox, new HttpSyncTransport(new HttpClient(new StaticHandler(status, "<html>Iniciar sesión en la red</html>", "text/html"))), directory);
        var pending = outbox.PendingCount();

        var result = await engine.RunOnceAsync();
        Assert.AreEqual(SyncState.Offline, result.State);
        Assert.AreEqual(pending, outbox.PendingCount(), "Nothing is marked sent without a valid confirmation.");
        Assert.AreEqual(1, outbox.Pending(1)[0].Attempts, "The failure is recorded for diagnosis.");
    }

    [TestMethod]
    public async Task RegisteringAgainstAWebsiteThatIsNotTheApiExplainsTheProblem()
    {
        var transport = new HttpSyncTransport(new HttpClient(new StaticHandler(HttpStatusCode.NotFound, "<html>404</html>", "text/html")));
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            transport.RegisterAsync(new Uri(Api), "secreto", new BranchIdentity(Guid.NewGuid(), "Centro"), Guid.NewGuid(), "Caja", default));
        StringAssert.Contains(error.Message, "404");
        var ok = new HttpSyncTransport(new HttpClient(new StaticHandler(HttpStatusCode.OK, "<html>sitio</html>", "text/html")));
        error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ok.RegisterAsync(new Uri(Api), "secreto", new BranchIdentity(Guid.NewGuid(), "Centro"), Guid.NewGuid(), "Caja", default));
        StringAssert.Contains(error.Message, "no corresponde");
    }

    private sealed class StaticHandler(HttpStatusCode status, string body, string mediaType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization, Url, Body;
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Authorization = request.Headers.Authorization?.ToString(); Url = request.RequestUri!.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancel);
            var seqs = JsonNode.Parse(Body)!["entries"]!.AsArray().Select(e => (long)e!["seq"]!).ToArray();
            return new HttpResponseMessage(Status) { Content = new StringContent(new JsonObject { ["accepted"] = new JsonArray(seqs.Select(s => (JsonNode)s).ToArray()), ["conflicts"] = new JsonArray() }.ToJsonString(), Encoding.UTF8, "application/json") };
        }
    }
}
