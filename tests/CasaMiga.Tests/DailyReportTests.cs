using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class DailyReportTests
{
    [TestMethod]
    public void LocalDayAggregatesAppliedPaymentsPiecesAverageSnapshotsAndStableTopFive()
    {
        using var fixture = new Fixture();
        var session = fixture.Open(fixture.Terminal, fixture.FromUtc.AddMinutes(-30), new Money(0));
        var alpha = fixture.Product("A", "Alpha actual", 1000);
        var beta = fixture.Product("B", "Beta actual", 1500);
        var zeta = fixture.Product("Z", "Zeta actual", 1000);

        fixture.Sale(session, fixture.FromUtc.AddSeconds(-1), [(alpha, "Antes", 1, 1000)]);
        fixture.Sale(session, fixture.FromUtc, [(zeta, "Zeta histórico", 2, 1000)]);
        fixture.Sale(session, fixture.FromUtc.AddHours(4), [(alpha, "Alpha histórico", 2, 1000)]);
        fixture.Sale(session, fixture.FromUtc.AddHours(5), [(beta, "Beta histórico", 2, 1500)]);
        fixture.Sale(session, fixture.UntilUtc.AddTicks(-1), [(alpha, "Alpha histórico", 1, 1000)]);
        fixture.Sale(session, fixture.UntilUtc, [(beta, "Después", 1, 1500)]);
        fixture.Catalog.EditProduct(alpha.Id, alpha.CategoryId, alpha.Sku, "Nombre actual modificado", "", new Money(9900), alpha.TrackInventory);

        var report = fixture.Service.Read(fixture.Day);

        Assert.AreEqual(fixture.FromUtc, report.FromUtc);
        Assert.AreEqual(fixture.UntilUtc, report.UntilUtc);
        Assert.AreEqual(8000L, report.Sales.Cents);
        Assert.AreEqual(4, report.Tickets);
        Assert.AreEqual(7L, report.Pieces);
        Assert.AreEqual(2000L, report.AverageTicket.Cents);
        Assert.HasCount(24, report.HourlySales);
        Assert.AreEqual(0, report.HourlySales[0].Hour);
        Assert.AreEqual(23, report.HourlySales[^1].Hour);
        Assert.AreEqual(0, report.HourlySales[1].Tickets);
        Assert.AreEqual(fixture.FromUtc, report.FirstSaleAt);
        Assert.AreEqual(fixture.UntilUtc.AddTicks(-1), report.LastSaleAt);
        Assert.AreEqual(5, report.PeakHour!.Hour);
        Assert.AreEqual(3000L, report.PeakHour.Sales.Cents);
        CollectionAssert.AreEqual(
            new[] { "Alpha histórico", "Beta histórico", "Zeta histórico" },
            report.TopProducts.Select(x => x.ProductName).ToArray());
        CollectionAssert.AreEqual(new long[] { 3, 2, 2 }, report.TopProducts.Select(x => x.Pieces).ToArray());
        CollectionAssert.AreEqual(new long[] { 3000, 3000, 2000 }, report.TopProducts.Select(x => x.Revenue.Cents).ToArray());
        Assert.IsLessThanOrEqualTo(5, report.TopProducts.Count);
    }

    [TestMethod]
    public void CardAndTransferSalesCountAsSalesButNeverAsDrawerCash()
    {
        using var fixture = new Fixture();
        var product = fixture.Product("MIX", "Concha", 1300);
        var session = fixture.Open(fixture.Terminal, fixture.FromUtc.AddHours(1), new Money(10000));
        fixture.Sale(session, fixture.FromUtc.AddHours(2), [(product, "Concha", 2, 1300)]);
        fixture.Sale(session, fixture.FromUtc.AddHours(3), [(product, "Concha", 1, 1300)], method: PaymentMethod.Card);
        fixture.Sale(session, fixture.FromUtc.AddHours(4), [(product, "Concha", 3, 1300)], method: PaymentMethod.Transfer);

        var report = fixture.Service.Read(fixture.Day);
        Assert.AreEqual(7800L, report.Sales.Cents);
        Assert.AreEqual(3, report.Tickets);
        Assert.AreEqual(6L, report.Pieces);
        Assert.AreEqual(6L, report.TopProducts.Single().Pieces);
        var drawer = report.CashSessions.Single();
        Assert.AreEqual(2600L, drawer.CashSales.Cents, "Card and transfer never enter the drawer.");
        Assert.AreEqual(12600L, drawer.Expected!.Value.Cents);
    }

    [TestMethod]
    public void EmptyDayReturnsRealZeroesAndExactCentAverageUsesDecimalRounding()
    {
        using var fixture = new Fixture();
        var empty = fixture.Service.Read(fixture.Day.AddDays(-1));
        Assert.AreEqual(0L, empty.Sales.Cents);
        Assert.AreEqual(0, empty.Tickets);
        Assert.AreEqual(0L, empty.Pieces);
        Assert.AreEqual(0L, empty.AverageTicket.Cents);
        Assert.HasCount(0, empty.TopProducts);
        Assert.HasCount(0, empty.HourlySales);
        Assert.IsNull(empty.FirstSaleAt);
        Assert.IsNull(empty.LastSaleAt);
        Assert.IsNull(empty.PeakHour);

        var session = fixture.Open(fixture.Terminal, fixture.FromUtc, new Money(0));
        var product = fixture.Product("ROUND", "Redondeo", 100);
        fixture.Sale(session, fixture.FromUtc.AddHours(1), [(product, "Redondeo", 1, 100)]);
        fixture.Sale(session, fixture.FromUtc.AddHours(2), [(product, "Redondeo", 1, 101)]);
        var report = fixture.Service.Read(fixture.Day);
        Assert.AreEqual(201L, report.Sales.Cents);
        Assert.AreEqual(101L, report.AverageTicket.Cents, "$2.01 / 2 rounds to the nearest cent without double.");
    }

    [TestMethod]
    public void HourlySalesUseLocalTimeAndPeakUsesAmountTicketsThenEarlierHour()
    {
        using var fixture = new Fixture();
        var session = fixture.Open(fixture.Terminal, fixture.FromUtc, new Money(0));
        var product = fixture.Product("HOUR", "Venta por hora", 100);
        fixture.Sale(session, fixture.FromUtc.AddHours(8).AddMinutes(14), [(product, "Venta por hora", 1, 1000)]);
        fixture.Sale(session, fixture.FromUtc.AddHours(9).AddMinutes(5), [(product, "Venta por hora", 1, 400)]);
        fixture.Sale(session, fixture.FromUtc.AddHours(9).AddMinutes(55), [(product, "Venta por hora", 1, 600)]);
        fixture.Sale(session, fixture.FromUtc.AddHours(10).AddMinutes(10), [(product, "Venta por hora", 1, 500)]);
        fixture.Sale(session, fixture.FromUtc.AddHours(10).AddMinutes(40), [(product, "Venta por hora", 1, 500)]);

        var report = fixture.Service.Read(fixture.Day);

        CollectionAssert.AreEqual(new[] { 8, 9, 10 }, report.HourlySales.Select(x => x.Hour).ToArray());
        Assert.AreEqual(2, report.HourlySales.Single(x => x.Hour == 9).Tickets);
        Assert.AreEqual(1000L, report.HourlySales.Single(x => x.Hour == 9).Sales.Cents);
        Assert.AreEqual(8, report.FirstSaleAt!.Value.Hour);
        Assert.AreEqual(10, report.LastSaleAt!.Value.Hour);
        Assert.AreEqual(9, report.PeakHour!.Hour, "Amount ties, then ticket count ties, then the earliest hour wins.");
    }

    [TestMethod]
    public void CashSummariesSupportOpenExactDiscrepancyAndMultipleSessions()
    {
        using var fixture = new Fixture();
        var product = fixture.Product("CASH", "Producto caja", 1900);

        var exact = fixture.Open(fixture.Terminal, fixture.FromUtc.AddHours(1), new Money(50000));
        fixture.Sale(exact, fixture.FromUtc.AddHours(1.5), [(product, "Producto caja", 1, 1900)]);
        fixture.Close(exact, fixture.FromUtc.AddHours(2), new Money(51900));

        var shortage = fixture.Open(fixture.Terminal, fixture.FromUtc.AddHours(3), new Money(0));
        fixture.Sale(shortage, fixture.FromUtc.AddHours(3.5), [(product, "Producto caja", 1, 1900)]);
        fixture.Close(shortage, fixture.FromUtc.AddHours(4), new Money(1800), "Faltante verificado");

        var surplus = fixture.Open(fixture.Terminal, fixture.FromUtc.AddHours(4.25), new Money(0));
        fixture.Sale(surplus, fixture.FromUtc.AddHours(4.5), [(product, "Producto caja", 1, 1900)]);
        fixture.Close(surplus, fixture.FromUtc.AddHours(4.75), new Money(2000), "Sobrante verificado");

        var secondTerminal = fixture.TerminalNamed("CAJA DOS");
        var open = fixture.Open(secondTerminal, fixture.FromUtc.AddHours(5), new Money(10000));
        fixture.Sale(open, fixture.FromUtc.AddHours(5.5), [(product, "Producto caja", 1, 1900)], secondTerminal);

        var sessions = fixture.Service.Read(fixture.Day).CashSessions;
        Assert.HasCount(4, sessions);
        var exactRow = sessions.Single(x => x.Id == exact.Id);
        Assert.IsFalse(exactRow.IsOpen); Assert.AreEqual(50000L, exactRow.OpeningFund.Cents);
        Assert.AreEqual(1900L, exactRow.CashSales.Cents); Assert.AreEqual(51900L, exactRow.Expected!.Value.Cents);
        Assert.AreEqual(51900L, exactRow.Counted!.Value.Cents); Assert.AreEqual(0L, exactRow.Difference!.Value.Cents);
        var shortageRow = sessions.Single(x => x.Id == shortage.Id);
        Assert.AreEqual(-100L, shortageRow.Difference!.Value.Cents); Assert.AreEqual("Faltante verificado", shortageRow.DiscrepancyNote);
        var surplusRow = sessions.Single(x => x.Id == surplus.Id);
        Assert.AreEqual(100L, surplusRow.Difference!.Value.Cents); Assert.AreEqual("Sobrante verificado", surplusRow.DiscrepancyNote);
        var openRow = sessions.Single(x => x.Id == open.Id);
        Assert.IsTrue(openRow.IsOpen); Assert.AreEqual(11900L, openRow.Expected!.Value.Cents);
        Assert.IsNull(openRow.Counted); Assert.IsNull(openRow.Difference); Assert.IsNull(openRow.ClosedAt);
    }

    [TestMethod]
    public void ReportRequiresBothReadPermissionsAndNeverMutatesOrCountsReprints()
    {
        using var fixture = new Fixture();
        var session = fixture.Open(fixture.Terminal, fixture.FromUtc, new Money(0));
        var product = fixture.Product("SAFE", "Histórico seguro", 1300);
        var sale = fixture.Sale(session, fixture.FromUtc.AddHours(1), [(product, "Histórico seguro", 2, 1300)]);
        var before = fixture.Counts();
        var first = fixture.Service.Read(fixture.Day);
        var afterRead = fixture.Counts();
        Assert.AreEqual(before, afterRead, "Reading the report must not mutate any operational or technical table.");
        fixture.InsertReprint(sale.Id);
        var second = fixture.Service.Read(fixture.Day);
        var after = fixture.Counts();

        Assert.AreEqual(first.Sales, second.Sales);
        Assert.AreEqual(first.Tickets, second.Tickets);
        Assert.AreEqual(first.Pieces, second.Pieces);
        CollectionAssert.AreEqual(first.HourlySales.ToArray(), second.HourlySales.ToArray());
        Assert.AreEqual(before.Sales, after.Sales); Assert.AreEqual(before.Payments, after.Payments);
        Assert.AreEqual(before.CashSessions, after.CashSessions); Assert.AreEqual(before.Audits, after.Audits);
        Assert.AreEqual(before.PrintRequests + 1, after.PrintRequests);
        Assert.AreEqual(before.PrintEvents + 1, after.PrintEvents);

        fixture.Sql("DELETE FROM role_permission WHERE role_name='Administrator' AND permission_name='cash.view'");
        Assert.IsFalse(fixture.Service.CanView());
        Assert.ThrowsExactly<AccessDeniedException>(() => fixture.Service.Read(fixture.Day));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "CasaMigaDailyReport", Guid.NewGuid().ToString("N"));
        private readonly Database database;
        private readonly SqliteLocalUnitOfWork work;
        private readonly UserSession user = new();
        private readonly FixedClock clock;
        private readonly Category category;
        public DateOnly Day { get; } = new(2026, 10, 9);
        public DateTimeOffset FromUtc { get; } = new(2026, 10, 9, 6, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UntilUtc { get; } = new(2026, 10, 10, 6, 0, 0, TimeSpan.Zero);
        public TerminalIdentity Terminal { get; }
        public CatalogService Catalog { get; }
        public DailyReportService Service { get; }

        public Fixture()
        {
            Directory.CreateDirectory(root);
            database = new(Path.Combine(root, "casamiga.db")); database.Initialize(); work = new(database);
            clock = new(new(2026, 10, 9, 18, 0, 0, TimeSpan.Zero));
            Terminal = TerminalNamed("CAJA UNO");
            var identity = new IdentityService(work, new LocalPasswords(), clock, user);
            var password = Guid.NewGuid().ToString("N") + "!Aa";
            identity.Bootstrap("report-admin", "Responsable reporte", password);
            Assert.IsTrue(identity.Login("report-admin", password));
            Catalog = new(work, user, clock, Terminal.Id);
            category = Catalog.CreateCategory("Categoría actual");
            var zone = TimeZoneInfo.CreateCustomTimeZone("CasaMigaDaily", TimeSpan.FromHours(-6), "CasaMigaDaily", "CasaMigaDaily");
            Service = new(work, user, zone, clock);
        }

        public TerminalIdentity TerminalNamed(string name)
        {
            var terminal = new TerminalIdentity(Guid.NewGuid(), name);
            work.Run(store => { store.EnsureTerminal(terminal, clock.UtcNow); return true; });
            return terminal;
        }
        public Product Product(string sku, string name, long cents) => Catalog.CreateProduct(category.Id, sku, name, "", new Money(cents));
        public CashSession Open(TerminalIdentity terminal, DateTimeOffset at, Money fund)
        {
            var session = CashSession.Open(Guid.NewGuid(), terminal.Id, user.Current!.Id, at, fund);
            work.Run(store => { store.AddCashSession(session); return true; }); return session;
        }
        public Sale Sale(CashSession session, DateTimeOffset at, (Product Product, string Name, int Quantity, long Price)[] lines, TerminalIdentity? saleTerminal = null,
            PaymentMethod method = PaymentMethod.Cash)
            => work.Run(store =>
            {
                var terminal = saleTerminal ?? Terminal;
                var sale = new Sale(Guid.NewGuid(), Guid.NewGuid(), user.Current!.Id, terminal.Id, session.Id,
                    store.NextSaleSequence(terminal.Id), at,
                    lines.Select(x => new SaleItem(x.Product.Id, x.Name, new Money(x.Price), x.Quantity)),
                    Guid.NewGuid(), new Money(lines.Sum(x => checked(x.Price * x.Quantity))), method);
                store.AddSale(sale); return sale;
            });
        public void Close(CashSession session, DateTimeOffset at, Money counted, string? note = null) => work.Run(store =>
        {
            var expected = session.OpeningFund + store.CashSalesTotal(session.Id);
            store.CloseSession(session.Close(user.Current!.Id, at, expected, counted, note)); return true;
        });
        public void InsertReprint(Guid saleId)
        {
            var request = Guid.NewGuid(); var at = clock.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            using var connection = database.Open(); using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO receipt_print_request(id,sale_id,kind,parent_request_id,requested_by,requested_utc,queue_name,snapshot_sha256) VALUES($id,$sale,'REPRINT',NULL,$user,$at,NULL,NULL);" +
                "INSERT INTO receipt_print_event(id,request_id,sequence,state,occurred_utc,spooler_job_id,error_code) VALUES($event,$id,1,'REQUESTED',$at,NULL,NULL)";
            command.Parameters.AddWithValue("$id", request.ToString("D")); command.Parameters.AddWithValue("$sale", saleId.ToString("D"));
            command.Parameters.AddWithValue("$user", user.Current!.Id.ToString("D")); command.Parameters.AddWithValue("$at", at);
            command.Parameters.AddWithValue("$event", Guid.NewGuid().ToString("D")); command.ExecuteNonQuery();
        }
        public (long Sales, long Payments, long CashSessions, long Audits, long PrintRequests, long PrintEvents) Counts() =>
            (Scalar("sale"), Scalar("payment"), Scalar("cash_session"), Scalar("audit_entry"), Scalar("receipt_print_request"), Scalar("receipt_print_event"));
        private long Scalar(string table)
        {
            using var connection = database.Open(true); using var command = connection.CreateCommand();
            command.CommandText = $"SELECT count(*) FROM {table}"; return (long)command.ExecuteScalar()!;
        }
        public void Sql(string sql) { using var connection = database.Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
        public void Dispose() => Directory.Delete(root, true);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
}
