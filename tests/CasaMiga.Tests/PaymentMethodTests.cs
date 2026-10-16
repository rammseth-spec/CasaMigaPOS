using System.Security.Cryptography;
using System.Text;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class PaymentMethodTests : SalesFixture
{
    private CheckoutService Checkout() => new(work, user, clock, terminal.Id, new CheckoutJournal(directory));
    private Sale Sell(PaymentMethod method, string? reference = null, long received = 0)
    {
        var service = Checkout();
        pos.Add(product.Id); pos.Increment(product.Id);                      // 2 conchas = $38
        var sale = service.Confirm(service.Prepare(new Money(received), method, reference));
        service.Acknowledge(sale.OperationId);
        return sale;
    }
    private Money Expected() => work.Run(s => s.FindSession(cash.Id)!.OpeningFund + s.CashSalesTotal(cash.Id) - s.CashRefundsTotal(cash.Id));

    [TestMethod]
    [DataRow(PaymentMethod.Card, "4242")]
    [DataRow(PaymentMethod.Transfer, null)]
    public void NonCashSaleIsExactDoesNotEnterDrawerAndRoundTrips(PaymentMethod method, string? reference)
    {
        var sale = Sell(method, reference, received: 999999);
        Assert.AreEqual(method, sale.Payment.Kind);
        Assert.AreEqual(3800L, sale.Payment.Received.Cents, "Non-cash is charged for the exact total.");
        Assert.AreEqual(0L, sale.Payment.Change.Cents);
        Assert.AreEqual(50000L, Expected().Cents, "Only cash enters the drawer.");

        var stored = work.Run(s => s.FindSaleDetail(sale.Id))!.Sale.Payment;
        Assert.AreEqual(method, stored.Kind); Assert.AreEqual(reference, stored.Reference);
        Assert.AreEqual(Payment.Code(method), Sql($"SELECT method FROM payment WHERE sale_id='{sale.Id}'"));
    }

    [TestMethod]
    public void CashSalesKeepWorkingAsBefore()
    {
        var sale = Sell(PaymentMethod.Cash, received: 5000);
        Assert.AreEqual(1200L, sale.Payment.Change.Cents);
        Assert.AreEqual(53800L, Expected().Cents);
        Assert.AreEqual(DBNull.Value, Sql($"SELECT reference FROM payment WHERE sale_id='{sale.Id}'"));
    }

    [TestMethod]
    public void InvalidPaymentsAreRejectedByDomainAndDatabase()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Payment(Guid.NewGuid(), new(100), new(100), PaymentMethod.Cash, "ref"));
        Assert.ThrowsExactly<ArgumentException>(() => new Payment(Guid.NewGuid(), new(100), new(200), PaymentMethod.Card));
        Assert.ThrowsExactly<ArgumentException>(() => new Payment(Guid.NewGuid(), new(100), new(100), PaymentMethod.Card, new string('x', 41)));
        var sale = Sell(PaymentMethod.Card);
        Assert.ThrowsExactly<SqliteException>(() => Sql($"UPDATE payment SET method='CASH' WHERE sale_id='{sale.Id}'"));
        using var c = db.Open(); using var q = c.CreateCommand();
        q.CommandText = $"INSERT INTO payment(id,sale_id,method,applied_cents,received_cents,change_cents) VALUES('{Guid.NewGuid()}','{Guid.NewGuid()}','CHEQUE',1,1,0)";
        Assert.ThrowsExactly<SqliteException>(() => q.ExecuteNonQuery());
        q.CommandText = $"INSERT INTO payment(id,sale_id,method,applied_cents,received_cents,change_cents) VALUES('{Guid.NewGuid()}','{Guid.NewGuid()}','CARD',100,200,100)";
        Assert.ThrowsExactly<SqliteException>(() => q.ExecuteNonQuery());
    }

    [TestMethod]
    public void NonCashRefundsDoNotNeedOrTouchDrawerCash()
    {
        var sale = Sell(PaymentMethod.Card, "1234");
        var closing = new CashClosingService(work, user, clock);
        closing.SubmitCount(cash.Id, Expected()); closing.Close(cash.Id);
        var next = new CashService(work, user, clock).Open(terminal.Id, new Money(0)).Session;

        var refund = new RefundService(work, user, clock, terminal.Id).Return(sale.Id, [new(1, 2)], "Cargo duplicado");
        Assert.AreEqual(3800L, refund.Total.Cents);
        Assert.AreEqual(0L, work.Run(s => s.CashRefundsTotal(next.Id)).Cents, "A card refund goes back to the card, not out of the drawer.");
    }

    [TestMethod]
    public void ReceiptsShowMethodAndKeepCashLayout()
    {
        var card = Sell(PaymentMethod.Card, "4242");
        var cashSale = Sell(PaymentMethod.Cash, received: 5000);
        var profile = new BusinessReceiptProfile("Casa Miga", null, null, null, null, "Gracias", null);
        string Render(Sale sale) => string.Join("\n", new TextReceiptRenderer().Render(
            ReceiptDocument.FromSale(work.Run(s => s.FindSaleDetail(sale.Id))!, profile, TimeZoneInfo.Utc), ReceiptCopyKind.Original));
        // Mismo formato del ticket físico (1K.4E): «Pago: …»; tarjeta y transferencia sólo dicen lo pagado.
        var cardText = Render(card);
        StringAssert.Contains(cardText, "Pago: Tarjeta"); StringAssert.Contains(cardText, "Pagado:");
        Assert.DoesNotContain("Recibido", cardText); Assert.DoesNotContain("Cambio", cardText);
        var transferText = Render(Sell(PaymentMethod.Transfer));
        StringAssert.Contains(transferText, "Pago: Transferencia"); Assert.DoesNotContain("Cambio", transferText);
        var cashText = Render(cashSale);
        StringAssert.Contains(cashText, "Pago: Efectivo"); StringAssert.Contains(cashText, "Recibido:"); StringAssert.Contains(cashText, "Cambio:");
        Assert.DoesNotContain("CASH", cashText); Assert.DoesNotContain("Pagado", cashText);
    }

    [TestMethod]
    public void ReportsBreakDownByMethod()
    {
        Sell(PaymentMethod.Cash, received: 3800); Sell(PaymentMethod.Card); Sell(PaymentMethod.Transfer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var report = new ReportService(work, user, TimeZoneInfo.Utc).Sales(today, today);
        CollectionAssert.AreEqual(new[] { PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Transfer }, report.Data.ByMethod.Select(m => m.Method).ToArray());
        Assert.IsTrue(report.Data.ByMethod.All(m => m.Sales == 1 && m.Sold.Cents == 3800));
        Assert.AreEqual(50000 + 3800, report.Data.Sessions.Single().Expected.Cents);
    }

    [TestMethod]
    [DataRow("midway", DisplayName = "falla a mitad del script")]
    [DataRow("registration", DisplayName = "falla al registrar migración")]
    public void Migration20PreservesPaymentsAndRollsBackOnFailure(string mode)
    {
        var path = Path.Combine(directory, "v19.db");
        SaleContractMigrationTests.InitializeThrough(new Database(path), 19);
        var old = new Database(path);
        var oldWork = new SqliteLocalUnitOfWork(old); var session = new UserSession();
        var identity = new IdentityService(oldWork, new LocalPasswords(), clock, session);
        identity.Bootstrap("admin", "Administrador", password); Assert.IsTrue(identity.Login("admin", password));
        oldWork.Run(s => { s.EnsureTerminal(terminal, clock.UtcNow); return true; });
        var shift = new CashService(oldWork, session, clock).Open(terminal.Id, new Money(0)).Session;
        var oldCatalog = new CatalogService(oldWork, session, clock, terminal.Id);
        var item = oldCatalog.CreateProduct(oldCatalog.CreateCategory("Pan").Id, "X1", "Bolillo", "", new Money(300));
        var sold = oldWork.Run(s => { var sale = new Sale(Guid.NewGuid(), Guid.NewGuid(), session.Current!.Id, terminal.Id, shift.Id, s.NextSaleSequence(terminal.Id), clock.UtcNow,
            [new(item.Id, item.Name, item.SalePrice, 2)], Guid.NewGuid(), new(1000)); s.AddSale(sale); return sale; });
        string Payments() { using var c = old.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT group_concat(id||method||applied_cents||received_cents||change_cents,'|') FROM payment"; return (string)q.ExecuteScalar()!; }
        long Scalar(string sql) { using var c = old.Open(); using var q = c.CreateCommand(); q.CommandText = sql; return Convert.ToInt64(q.ExecuteScalar()); }
        void Exec(string sql) { using var c = old.Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
        var before = Payments();

        Exec(mode == "midway" ? "CREATE TABLE payment_020(x)" : "CREATE TRIGGER inject_020 BEFORE INSERT ON schema_migration WHEN NEW.version=20 BEGIN SELECT RAISE(ABORT,'injected'); END");
        Assert.ThrowsExactly<SqliteException>(() => old.Initialize());
        Exec(mode == "midway" ? "DROP TABLE payment_020" : "DROP TRIGGER inject_020");
        Assert.AreEqual(19L, Scalar("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(0L, Scalar("SELECT count(*) FROM pragma_table_info('payment') WHERE name='reference'"));
        Assert.AreEqual(1L, Scalar("SELECT count(*) FROM sqlite_master WHERE name='payment_no_update'"));
        Assert.AreEqual(before, Payments());

        old.Initialize(); old.Validate();
        Assert.AreEqual((long)Database.LatestVersion, Scalar("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(before, Payments(), "Every historical payment survives the rebuild.");
        Assert.AreEqual(PaymentMethod.Cash, oldWork.Run(s => s.FindSaleDetail(sold.Id))!.Sale.Payment.Kind);
        Assert.ThrowsExactly<SqliteException>(() => Exec($"DELETE FROM payment WHERE sale_id='{sold.Id}'"), "Immutability triggers are recreated.");
        Assert.AreEqual(300L, new RefundService(oldWork, session, clock, terminal.Id).Return(sold.Id, [new(1, 1)], "Tras migrar").Total.Cents);
        Assert.AreEqual(300L, oldWork.Run(s => s.CashRefundsTotal(shift.Id)).Cents);
    }
}
