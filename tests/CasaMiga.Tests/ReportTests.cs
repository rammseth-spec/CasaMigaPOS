using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class ReportTests : SalesFixture
{
    private ReportService Reports => new(work, user, TimeZoneInfo.Utc);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [TestMethod]
    public void DailyReportAggregatesSalesRefundsProductsCashiersAndShift()
    {
        var first = Persist(); var second = Persist();           // 2 × (2 conchas $19) = $76
        new RefundService(work, user, clock, terminal.Id).Return(first.Id, [new(1, 1)], "Pan aplastado");

        var report = Reports.Sales(Today, Today);
        Assert.AreEqual(2, report.Data.SaleCount);
        Assert.AreEqual(7600L, report.Data.Gross.Cents);
        Assert.AreEqual(1900L, report.Data.Refunded.Cents);
        Assert.AreEqual(5700L, report.Net.Cents);
        Assert.AreEqual(3800L, report.AverageTicket.Cents);

        var concha = report.Data.Products.Single();
        Assert.AreEqual("Concha", concha.ProductName);
        Assert.AreEqual(4L, concha.QuantitySold); Assert.AreEqual(1L, concha.QuantityRefunded); Assert.AreEqual(5700L, concha.Net.Cents);

        var cashier = report.Data.Cashiers.Single();
        Assert.AreEqual(2, cashier.Sales); Assert.AreEqual(7600L, cashier.Sold.Cents);

        var shift = report.Data.Sessions.Single();
        Assert.IsFalse(shift.Closed);
        Assert.AreEqual(7600L, shift.CashSales.Cents); Assert.AreEqual(1900L, shift.Refunds.Cents);
        Assert.AreEqual(50000 + 7600 - 1900, shift.Expected.Cents);
        Assert.IsNull(shift.Counted);
    }

    [TestMethod]
    public void ClosedShiftShowsCountedAndDifference()
    {
        Persist();
        var closing = new CashClosingService(work, user, clock);
        closing.SubmitCount(cash.Id, new Money(53700)); closing.Close(cash.Id, "Sobrante de prueba");
        var shift = Reports.Sales(Today, Today).Data.Sessions.Single();
        Assert.IsTrue(shift.Closed);
        Assert.AreEqual(53700L, shift.Counted!.Value.Cents);
        Assert.AreEqual(-100L, shift.Difference!.Value.Cents);
    }

    [TestMethod]
    public void PeriodsOutsideActivityAreEmpty()
    {
        Persist();
        var report = Reports.Sales(Today.AddDays(1), Today.AddDays(2));
        Assert.AreEqual(0, report.Data.SaleCount);
        Assert.AreEqual(0L, report.AverageTicket.Cents);
        Assert.IsEmpty(report.Data.Products); Assert.IsEmpty(report.Data.Sessions);
    }

    [TestMethod]
    public void RejectsInvalidPeriodsAndUnauthorizedUsers()
    {
        Assert.ThrowsExactly<ReportException>(() => Reports.Sales(Today, Today.AddDays(-1)));
        Assert.ThrowsExactly<ReportException>(() => Reports.Sales(Today.AddDays(-400), Today));
        Sql("UPDATE app_user SET role_name='Cashier'");
        Assert.IsFalse(Reports.CanView());
        Assert.ThrowsExactly<AccessDeniedException>(() => Reports.Sales(Today, Today));
    }

    [TestMethod]
    public void CsvEscapesTextAndNeutralizesFormulas()
    {
        var category = catalog.CreateCategory("Especial");
        var tricky = catalog.CreateProduct(category.Id, "P9", "=HYPERLINK(\"x\"), pan", "", new Money(500));
        work.Run(s =>
        {
            var sale = new Sale(Guid.NewGuid(), Guid.NewGuid(), user.Current!.Id, terminal.Id, cash.Id, s.NextSaleSequence(terminal.Id), clock.UtcNow,
                [new(tricky.Id, tricky.Name, tricky.SalePrice, 1)], Guid.NewGuid(), new(500));
            s.AddSale(sale); return true;
        });
        var csv = ReportService.ToCsv(Reports.Sales(Today, Today), TimeZoneInfo.Utc);
        StringAssert.Contains(csv, "\"'=HYPERLINK(\"\"x\"\"), pan\",1,5.00");
        StringAssert.Contains(csv, "Ventas,Vendido,Devuelto,Neto,Ticket promedio");
        StringAssert.Contains(csv, "1,5.00,0.00,5.00,5.00");
    }
}
