using System.Security.Cryptography;
using System.Text;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class RefundTests : SalesFixture
{
    private RefundService Refunds(UserSession? session = null) => new(work, session ?? user, clock, terminal.Id);

    /// <summary>Sale of 3 conchas ($19) and 2 donas ($12) = $81.</summary>
    private Sale TwoLineSale()
    {
        var category = catalog.CreateCategory("Dulce");
        var dona = catalog.CreateProduct(category.Id, "P2", "Dona", "", new Money(1200));
        return work.Run(s =>
        {
            var sale = new Sale(Guid.NewGuid(), Guid.NewGuid(), user.Current!.Id, terminal.Id, cash.Id, s.NextSaleSequence(terminal.Id), clock.UtcNow,
                [new(product.Id, product.Name, product.SalePrice, 3), new(dona.Id, dona.Name, dona.SalePrice, 2)], Guid.NewGuid(), new(10000));
            s.AddSale(sale); return sale;
        });
    }

    private Money Expected(Guid session) => work.Run(s => s.FindSession(session)!.OpeningFund + s.CashSalesTotal(session) - s.CashRefundsTotal(session));
    private CashSession ReopenWithFund(long cents)
    {
        var closing = new CashClosingService(work, user, clock);
        var counted = Expected(cash.Id);
        closing.SubmitCount(cash.Id, counted); closing.Close(cash.Id);
        return new CashService(work, user, clock).Open(terminal.Id, new Money(cents)).Session;
    }

    [TestMethod]
    public void CancellationRefundsWholeSaleInSameShiftAndReducesExpectedCash()
    {
        var sale = Persist();
        Assert.AreEqual(50000 + 3800, Expected(cash.Id).Cents);
        var refund = Refunds().Cancel(sale.Id, "Cobro equivocado");

        Assert.AreEqual(RefundKind.Cancellation, refund.Kind);
        Assert.AreEqual(3800L, refund.Total.Cents);
        Assert.AreEqual(50000L, Expected(cash.Id).Cents, "Cash given back leaves the drawer.");
        Assert.AreEqual(1L, Sql($"SELECT count(*) FROM audit_entry WHERE action='sale.cancelled' AND entity_id='{refund.Id}'"));
        Assert.AreEqual(1L, Sql($"SELECT count(*) FROM sync_outbox WHERE entity='sale_refund' AND entity_id='{refund.Id}'"));
        Assert.AreEqual(3800L, Sql($"SELECT total_cents FROM sale WHERE id='{sale.Id}'"), "The original sale is never modified.");

        var options = Refunds().Options(sale.Id);
        Assert.IsFalse(options.CanCancel); Assert.IsFalse(options.CanReturn);
        Assert.ThrowsExactly<RefundException>(() => Refunds().Cancel(sale.Id, "Otra vez"));
        Assert.ThrowsExactly<RefundException>(() => Refunds().Return(sale.Id, [new(1, 1)], "Otra vez"));
    }

    [TestMethod]
    public void PartialReturnsAccumulateUpToSoldQuantity()
    {
        var sale = TwoLineSale();
        var first = Refunds().Return(sale.Id, [new(1, 2)], "Pan aplastado");
        Assert.AreEqual(3800L, first.Total.Cents);
        var second = Refunds().Return(sale.Id, [new(1, 1), new(2, 1)], "Cliente devolvió");
        Assert.AreEqual(1900L + 1200L, second.Total.Cents);

        var options = Refunds().Options(sale.Id);
        Assert.AreEqual(0, options.Lines[0].Remaining);
        Assert.AreEqual(1, options.Lines[1].Remaining);
        Assert.IsFalse(options.CanCancel, "A sale with previous returns cannot be cancelled.");
        Assert.ThrowsExactly<ArgumentException>(() => Refunds().Return(sale.Id, [new(1, 1)], "Excedente"));
        Assert.ThrowsExactly<ArgumentException>(() => Refunds().Return(sale.Id, [new(2, 2)], "Excedente"));
        Assert.ThrowsExactly<ArgumentException>(() => Refunds().Return(sale.Id, [new(9, 1)], "No existe"));
        Assert.ThrowsExactly<ArgumentException>(() => Refunds().Return(sale.Id, [new(2, 0)], "Vacío"));
        Assert.AreEqual(2, options.Refunds.Count);
        Assert.AreEqual(50000 + 8100 - 6900, Expected(cash.Id).Cents);
    }

    [TestMethod]
    public void DatabaseRejectsOverReturnEvenBypassingTheService()
    {
        var sale = Persist();
        var at = DateTimeOffset.UtcNow.ToString("O");
        Assert.ThrowsExactly<SqliteException>(() => RawRefund(sale, Guid.NewGuid(), 3, 5700, at), "Only 2 were sold.");
        Assert.ThrowsExactly<SqliteException>(() => RawRefund(sale, Guid.NewGuid(), 1, 1, at), "Line total must match the sold price.");
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale_refund"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale_refund_item"));
    }

    private int RawRefund(Sale sale, Guid refundId, int quantity, long cents, string at)
    {
        using var c = db.Open(); using var t = c.BeginTransaction(); using var q = c.CreateCommand(); q.Transaction = t;
        q.CommandText = $"INSERT INTO sale_refund_item VALUES('{refundId}','{sale.Id}',1,{quantity},{cents});"
            + $"INSERT INTO sale_refund VALUES('{refundId}','{Guid.NewGuid()}','{sale.Id}','RETURN','Directo','{user.Current!.Id}','{terminal.Id}','{cash.Id}','{at}',{cents})";
        q.ExecuteNonQuery(); t.Commit(); return 1;
    }

    [TestMethod]
    public void RefundsAreImmutable()
    {
        var sale = Persist();
        Refunds().Return(sale.Id, [new(1, 1)], "Pan duro");
        Assert.ThrowsExactly<SqliteException>(() => Sql("UPDATE sale_refund SET total_cents=1"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM sale_refund"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("UPDATE sale_refund_item SET quantity=2"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM sale_refund_item"));
    }

    [TestMethod]
    public void LaterShiftCanReturnButNotCancelAndCashComesFromCurrentDrawer()
    {
        var sale = Persist();
        var next = ReopenWithFund(10000);
        Assert.IsFalse(Refunds().Options(sale.Id).CanCancel);
        Assert.ThrowsExactly<RefundException>(() => Refunds().Cancel(sale.Id, "Turno anterior"));

        var refund = Refunds().Return(sale.Id, [new(1, 2)], "Turno anterior");
        Assert.AreEqual(next.Id, refund.CashSessionId);
        Assert.AreEqual(10000 - 3800, Expected(next.Id).Cents);
        var closed = work.Run(s => s.FindSession(cash.Id))!;
        Assert.AreEqual(50000 + 3800, closed.Expected!.Value.Cents, "Closed shifts never change.");
    }

    [TestMethod]
    public void RefundCannotExceedCashInDrawer()
    {
        var sale = Persist();
        ReopenWithFund(1000);
        var error = Assert.ThrowsExactly<RefundException>(() => Refunds().Return(sale.Id, [new(1, 1)], "Sin efectivo"));
        StringAssert.Contains(error.Message, "efectivo");
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale_refund"));
    }

    [TestMethod]
    public void ClosingShowsRefundsAndCountsAgainstNetExpected()
    {
        var sale = Persist();
        Refunds().Return(sale.Id, [new(1, 1)], "Devolución");
        var result = new CashClosingService(work, user, clock).SubmitCount(cash.Id, new Money(50000 + 1900));
        Assert.AreEqual(1900L, result.Refunds.Cents);
        Assert.AreEqual(50000 + 1900, result.Expected.Cents);
        Assert.AreEqual(0L, result.Difference.Cents);
        Assert.ThrowsExactly<RefundException>(() => Refunds().Return(sale.Id, [new(1, 1)], "Después del conteo"));
    }

    [TestMethod]
    public void RequiresPermissionOpenCashAndReason()
    {
        var sale = Persist();
        Assert.ThrowsExactly<ArgumentException>(() => Refunds().Cancel(sale.Id, "  "));
        Sql("UPDATE app_user SET role_name='Cashier'");
        Assert.IsFalse(Refunds().CanRefund());
        Assert.ThrowsExactly<AccessDeniedException>(() => Refunds().Cancel(sale.Id, "Cajero"));
        Assert.ThrowsExactly<AccessDeniedException>(() => Refunds().Options(sale.Id));
        Sql("UPDATE app_user SET role_name='Manager'");
        Assert.IsTrue(Refunds().CanRefund());

        var closing = new CashClosingService(work, user, clock);
        closing.SubmitCount(cash.Id, Expected(cash.Id)); closing.Close(cash.Id);
        Assert.AreEqual("Abre la caja de esta terminal para registrar devoluciones.", Refunds().Options(sale.Id).Blocker);
        Assert.ThrowsExactly<RefundException>(() => Refunds().Return(sale.Id, [new(1, 1)], "Sin caja"));
    }

    [TestMethod]
    public void CustomerInstructionOnlySendsCashFromTheDrawerForCashSales()
    {
        var total = new Money(3800);
        StringAssert.Contains(RefundService.CustomerInstruction(PaymentMethod.Cash, total), "en efectivo al cliente");
        foreach (var method in new[] { PaymentMethod.Card, PaymentMethod.Transfer })
        {
            var text = RefundService.CustomerInstruction(method, total);
            Assert.DoesNotContain("en efectivo al cliente", text, $"{method} refunds must never tell the cashier to hand out drawer cash.");
            StringAssert.Contains(text, "No entregues efectivo de la caja");
        }
        StringAssert.Contains(RefundService.CustomerInstruction(PaymentMethod.Card, total), "tarjeta");
        StringAssert.Contains(RefundService.CustomerInstruction(PaymentMethod.Transfer, total), "transferencia");
    }

    [TestMethod]
    public void FailureMidRefundLeavesNoTrace()
    {
        var sale = Persist();
        var outbox = Sql("SELECT count(*) FROM sync_outbox");
        Sql("CREATE TRIGGER inject_refund BEFORE INSERT ON audit_entry WHEN NEW.action='sale.returned' BEGIN SELECT RAISE(ABORT,'injected'); END");
        Assert.ThrowsExactly<SqliteException>(() => Refunds().Return(sale.Id, [new(1, 1)], "Falla a la mitad"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale_refund"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale_refund_item"));
        Assert.AreEqual(outbox, Sql("SELECT count(*) FROM sync_outbox"));
        Assert.AreEqual(53800L, Expected(cash.Id).Cents);

        Sql("DROP TRIGGER inject_refund");
        Assert.AreEqual(1900L, Refunds().Return(sale.Id, [new(1, 1)], "Reintento").Total.Cents);
    }

    [TestMethod]
    public void HistoryListShowsRefundedAmount()
    {
        var sale = Persist();
        Refunds().Return(sale.Id, [new(1, 1)], "Devolución");
        var history = new SalesHistoryService(work, user, TimeZoneInfo.Utc);
        var row = history.Search(new()).Items.Single(r => r.SaleId == sale.Id);
        Assert.AreEqual(1900L, row.Refunded.Cents);
    }

    [TestMethod]
    [DataRow("midway", DisplayName = "falla a mitad del script")]
    [DataRow("registration", DisplayName = "falla al registrar migración")]
    public void Migration19PreservesOutboxAndRollsBackOnFailure(string mode)
    {
        var path = Path.Combine(directory, "v18.db");
        var assembly = typeof(Database).Assembly;
        var scripts = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql", StringComparison.Ordinal)).Order().Take(18).ToArray();
        var old = new Database(path);
        using (var c = old.Open())
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT"; cmd.ExecuteNonQuery();
            for (int i = 0; i < scripts.Length; i++)
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(scripts[i])!);
                var sql = reader.ReadToEnd(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
                cmd.CommandText = "INSERT INTO schema_migration VALUES($v,$h,'utc')"; cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$v", i + 1); cmd.Parameters.AddWithValue("$h", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
                cmd.ExecuteNonQuery(); cmd.Parameters.Clear();
            }
        }
        var oldWork = new SqliteLocalUnitOfWork(old); var session = new UserSession();
        var identity = new IdentityService(oldWork, new LocalPasswords(), clock, session);
        identity.Bootstrap("admin", "Administrador", password); Assert.IsTrue(identity.Login("admin", password));
        oldWork.Run(s => { s.EnsureTerminal(terminal, clock.UtcNow); return true; });
        new CashService(oldWork, session, clock).Open(terminal.Id, new Money(100));
        var outbox = new SqliteSyncOutbox(old);
        outbox.MarkSent([outbox.Pending(1)[0].Sequence], clock.UtcNow);
        string Snapshot() { using var c = old.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT group_concat(seq||entity||entity_id||change||ifnull(sent_utc,'-')||attempts,'|') FROM (SELECT * FROM sync_outbox ORDER BY seq)"; return (string)q.ExecuteScalar()!; }
        long Scalar(string sql) { using var c = old.Open(); using var q = c.CreateCommand(); q.CommandText = sql; return Convert.ToInt64(q.ExecuteScalar()); }
        var before = Snapshot();

        using (var c = old.Open()) { using var q = c.CreateCommand();
            q.CommandText = mode == "midway" ? "CREATE TABLE sync_outbox_v19(x)" : "CREATE TRIGGER inject_019 BEFORE INSERT ON schema_migration WHEN NEW.version=19 BEGIN SELECT RAISE(ABORT,'injected'); END";
            q.ExecuteNonQuery(); }
        Assert.ThrowsExactly<SqliteException>(() => old.Initialize());
        using (var c = old.Open()) { using var q = c.CreateCommand(); q.CommandText = mode == "midway" ? "DROP TABLE sync_outbox_v19" : "DROP TRIGGER inject_019"; q.ExecuteNonQuery(); }
        Assert.AreEqual(18L, Scalar("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(0L, Scalar("SELECT count(*) FROM sqlite_master WHERE name IN ('sale_refund','sync_sale_refund_insert')"));
        Assert.AreEqual(1L, Scalar("SELECT count(*) FROM sqlite_master WHERE name='sync_audit_entry_insert'"), "018 triggers survive a failed 019.");
        Assert.AreEqual(before, Snapshot());

        old.Initialize(); old.Validate();
        Assert.AreEqual((long)Database.LatestVersion, Scalar("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(before, Snapshot(), "Sequences, sent marks and attempts survive the outbox rebuild.");
        var beforeCount = Scalar("SELECT count(*) FROM sync_outbox");
        new CashClosingService(oldWork, session, clock).SubmitCount(oldWork.Run(s => s.FindOpenSession(terminal.Id))!.Session.Id, new Money(100));
        Assert.AreEqual(beforeCount + 2, Scalar("SELECT count(*) FROM sync_outbox"), "Recreated triggers keep queueing.");
        Assert.AreEqual(beforeCount + 2, Scalar("SELECT max(seq) FROM sync_outbox"), "New sequences continue after the preserved ones.");
    }
}
