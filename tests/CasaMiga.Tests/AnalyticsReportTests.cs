using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class AnalyticsTests
{
    // Hora del centro de México (UTC-6, sin horario de verano): las 21:00 locales ya son el día siguiente en UTC.
    private static readonly TimeZoneInfo Mexico = TimeZoneInfo.CreateCustomTimeZone("CasaMiga-Test-MX", TimeSpan.FromHours(-6), "MX", "MX");
    private static readonly Guid Concha = Guid.NewGuid(), Bolillo = Guid.NewGuid();
    private static DateTimeOffset Local(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, TimeSpan.FromHours(-6));

    [TestMethod]
    public void ForecastAveragesSameWeekdayOnOpenDaysAndAddsMargin()
    {
        var target = new DateOnly(2026, 10, 14); // miércoles
        SaleLineFact[] lines =
        [
            new(Local(10, 7, 9), Concha, "Concha", 6), new(Local(10, 7, 21), Concha, "Concha", 4), // miércoles pasado: 10, la de las 21:00 es 8-oct en UTC
            new(Local(10, 7, 12), Bolillo, "Bolillo", 4),
            new(Local(9, 30, 8), Concha, "Concha vieja", 6),                                     // hace dos miércoles
            new(Local(10, 13, 10), Concha, "Concha", 100),                                      // martes: no cuenta
            new(Local(9, 16, 10), Concha, "Concha", 50),                                        // hace 4 semanas: sí cuenta
        ];
        var forecast = Analytics.Forecast(target, 4, lines, Mexico);
        Assert.AreEqual(3, forecast.WeeksWithData);
        var concha = forecast.Rows[0];
        Assert.AreEqual("Concha", concha.ProductName, "Usa el nombre más reciente.");
        Assert.AreEqual(10L, concha.LastQuantity);
        Assert.AreEqual(22.0m, concha.Average);               // (10 + 6 + 50) / 3 días abiertos
        Assert.AreEqual(25, concha.Suggested);                // 22 × 1.10 = 24.2 → 25
        Assert.AreEqual(3, concha.DaysWithSales);
        var bolillo = forecast.Rows[1];
        Assert.AreEqual(1.3m, bolillo.Average);               // 4 / 3 días: un día sin bolillo cuenta como cero
        Assert.AreEqual(2, bolillo.Suggested);
        Assert.AreEqual(27L, forecast.SuggestedTotal);
        Assert.IsEmpty(Analytics.Forecast(target.AddDays(1), 4, lines, Mexico).Rows, "Ningún jueves con venta.");
    }

    [TestMethod]
    public void HeatmapGroupsByLocalWeekdayAndHourAndFindsPeak()
    {
        SaleFact[] sales =
        [
            new(Local(10, 5, 10, 5), 1900), new(Local(10, 5, 10, 50), 3800), new(Local(10, 12, 10, 20), 1900), // lunes 10:00 ×3
            new(Local(10, 10, 8, 0), 5000),                                                                    // sábado 8:00
            new(Local(10, 6, 6, 30), 1000),                                                                    // martes 6:30 amplía el rango
            new(Local(10, 11, 21, 15), 700),                                                                   // domingo 21:15 (lunes en UTC)
        ];
        var map = Analytics.Heatmap(new(2026, 10, 5), new(2026, 10, 12), sales, Mexico);
        Assert.AreEqual(6, map.FirstHour); Assert.AreEqual(21, map.LastHour);
        Assert.HasCount(7, map.Rows);
        Assert.AreEqual(DayOfWeek.Monday, map.Rows[0].Day);
        var monday = map.Rows[0].Cells.Single(c => c.Hour == 10);
        Assert.AreEqual(3, monday.Sales); Assert.AreEqual(7600L, monday.Amount.Cents);
        Assert.AreEqual(1, map.Rows[6].Cells.Single(c => c.Hour == 21).Sales, "Domingo por la noche sigue siendo domingo local.");
        Assert.AreEqual((DayOfWeek.Monday, 10, 3), map.Peak);
        Assert.AreEqual(6, map.TotalSales); Assert.AreEqual(3, map.MaxSales);
        Assert.AreEqual(3, map.ByHour.Single(c => c.Hour == 10).Sales);
        var empty = Analytics.Heatmap(new(2026, 10, 5), new(2026, 10, 5), [], Mexico);
        Assert.AreEqual(7, empty.FirstHour); Assert.AreEqual(20, empty.LastHour); Assert.IsNull(empty.Peak); Assert.AreEqual(0, empty.TotalSales);
    }

    [TestMethod]
    public void ComparisonMatchesProductsAcrossPeriodsAndHandlesMissingBase()
    {
        var now = Data(new ProductReportRow(Concha, "Concha", 4, new(7600), 1, new(1900)), new ProductReportRow(Guid.NewGuid(), "Rol de canela", 2, new(5000), 0, new(0)));
        var before = Data(new ProductReportRow(Concha, "Concha", 2, new(3800), 0, new(0)), new ProductReportRow(Bolillo, "Bolillo", 5, new(2500), 0, new(0)));
        var rows = Analytics.CompareProducts(now, before);
        Assert.HasCount(3, rows);
        var concha = rows.Single(r => r.ProductName == "Concha");
        Assert.AreEqual(3L, concha.Quantity); Assert.AreEqual(2L, concha.PreviousQuantity);
        Assert.AreEqual(50.0m, concha.QuantityChange); Assert.AreEqual(50.0m, concha.NetChange);
        var bolillo = rows.Single(r => r.ProductName == "Bolillo");
        Assert.AreEqual(0L, bolillo.Quantity); Assert.AreEqual(-100.0m, bolillo.QuantityChange);
        Assert.IsNull(rows.Single(r => r.ProductName == "Rol de canela").QuantityChange, "Sin base no hay porcentaje.");
        Assert.AreEqual("Concha", rows[0].ProductName, "Ordena por neto del periodo actual.");
        Assert.AreEqual(12.5m, Analytics.Change(45, 40));
        Assert.IsNull(Analytics.Change(3, 0));

        static SalesReportData Data(params ProductReportRow[] products) => new(1, new(products.Sum(p => p.Sold.Cents)), new(products.Sum(p => p.Refunded.Cents)), products, [], []);
    }

    [TestMethod]
    public void CashControlSplitsShortagesOveragesAndGroupsRefundReasons()
    {
        ShiftFact[] shifts = [new("Ana", true, -2000), new("Ana", true, 0), new("Ana", false, null), new("Luis", true, 500)];
        RefundFact[] refunds = [new("Pan aplastado", 1900, "Ana"), new(" pan aplastado ", 3800, "Luis"), new("Error de captura", 5000, "Ana"), new("Cobro doble", 1000, "Beto")];
        var report = Analytics.CashControl(new(2026, 10, 1), new(2026, 10, 7), shifts, refunds);
        CollectionAssert.AreEqual(new[] { "Ana", "Luis", "Beto" }, report.Cashiers.Select(c => c.Name).ToArray(), "Primero quien tiene más faltante; luego más devuelto.");
        var ana = report.Cashiers[0];
        Assert.AreEqual(3, ana.Shifts); Assert.AreEqual(2, ana.ClosedShifts); Assert.AreEqual(1, ana.ShiftsWithDifference);
        Assert.AreEqual(2000L, ana.Shortages.Cents); Assert.AreEqual(0L, ana.Overages.Cents); Assert.AreEqual(-2000L, ana.Net.Cents);
        Assert.AreEqual(2, ana.Refunds); Assert.AreEqual(6900L, ana.Refunded.Cents);
        var luis = report.Cashiers[1];
        Assert.AreEqual(500L, luis.Overages.Cents); Assert.AreEqual(1, luis.ShiftsWithDifference); Assert.AreEqual(3800L, luis.Refunded.Cents);
        Assert.AreEqual(0, report.Cashiers[2].Shifts, "Quien sólo tiene devoluciones también aparece.");
        var crushed = report.Reasons[0];
        Assert.AreEqual("Pan aplastado", crushed.Reason); Assert.AreEqual(2, crushed.Count); Assert.AreEqual(5700L, crushed.Amount.Cents);
        Assert.HasCount(3, report.Reasons);
    }
}

[TestClass]
public sealed class AnalyticsReportServiceTests : SalesFixture
{
    private ReportService Reports => new(work, user, TimeZoneInfo.Utc);
    private RefundService Refunds => new(work, user, clock, terminal.Id);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [TestMethod]
    public void CancelledSalesAreNotDemandNorTraffic()
    {
        Persist(); var cancelled = Persist();
        Refunds.Cancel(cancelled.Id, "Cobro equivocado");
        Assert.AreEqual(1, Reports.Heatmap(Today, Today).TotalSales);
        var forecast = Reports.Forecast(Today.AddDays(7));
        Assert.AreEqual(1, forecast.WeeksWithData);
        var concha = forecast.Rows.Single();
        Assert.AreEqual(2L, concha.LastQuantity); Assert.AreEqual(2.0m, concha.Average); Assert.AreEqual(3, concha.Suggested);
        StringAssert.Contains(ReportService.ToCsv(forecast), "Concha,2,2.0,3,1");
    }

    [TestMethod]
    public void ForecastSkipsInactiveProducts()
    {
        Persist();
        Sql("UPDATE product SET active=0");
        Assert.IsEmpty(Reports.Forecast(Today.AddDays(7)).Rows);
    }

    [TestMethod]
    public void ComparisonUsesThePreviousPeriodOfSameLength()
    {
        Persist();
        var comparison = Reports.Compare(Today.AddDays(1), Today.AddDays(1)); // mañana contra hoy
        Assert.AreEqual(Today, comparison.Previous.From); Assert.AreEqual(Today, comparison.Previous.Through);
        Assert.AreEqual(0, comparison.Current.Data.SaleCount); Assert.AreEqual(1, comparison.Previous.Data.SaleCount);
        Assert.AreEqual(-100.0m, comparison.SalesChange);
        var concha = comparison.Products.Single();
        Assert.AreEqual(0L, concha.Quantity); Assert.AreEqual(2L, concha.PreviousQuantity); Assert.AreEqual(-100.0m, concha.QuantityChange);
        var week = Reports.Compare(Today.AddDays(-6), Today);
        Assert.AreEqual(Today.AddDays(-13), week.Previous.From); Assert.AreEqual(Today.AddDays(-7), week.Previous.Through);
        Assert.IsNull(week.SalesChange);
        StringAssert.Contains(ReportService.ToCsv(comparison), "Ventas,0,1,-100.0");
    }

    [TestMethod]
    public void CashControlAttributesShortagesAndRefundsToCashiers()
    {
        var first = Persist(); Persist();
        Refunds.Return(first.Id, [new(1, 1)], "Pan aplastado");
        var closing = new CashClosingService(work, user, clock);
        closing.SubmitCount(cash.Id, new Money(50000 + 7600 - 1900 - 100)); closing.Close(cash.Id);
        var report = Reports.CashControl(Today, Today);
        var cashier = report.Cashiers.Single();
        Assert.AreEqual("Prueba ventas", cashier.Name);
        Assert.AreEqual(1, cashier.ClosedShifts); Assert.AreEqual(1, cashier.ShiftsWithDifference);
        Assert.AreEqual(100L, cashier.Shortages.Cents); Assert.AreEqual(1, cashier.Refunds); Assert.AreEqual(1900L, cashier.Refunded.Cents);
        var reason = report.Reasons.Single();
        Assert.AreEqual("Pan aplastado", reason.Reason); Assert.AreEqual(1900L, reason.Amount.Cents);
        StringAssert.Contains(ReportService.ToCsv(report), "Pan aplastado,1,19.00");
    }

    [TestMethod]
    public void NewReportsRequireSalesPermissionAndValidInput()
    {
        Assert.ThrowsExactly<ReportException>(() => Reports.Forecast(Today, 0));
        Assert.ThrowsExactly<ReportException>(() => Reports.Forecast(Today, ReportService.MaxForecastWeeks + 1));
        Assert.ThrowsExactly<ReportException>(() => Reports.Compare(Today, Today.AddDays(-1)));
        Sql("UPDATE app_user SET role_name='Cashier'");
        Assert.ThrowsExactly<AccessDeniedException>(() => Reports.Compare(Today, Today));
        Assert.ThrowsExactly<AccessDeniedException>(() => Reports.Heatmap(Today, Today));
        Assert.ThrowsExactly<AccessDeniedException>(() => Reports.CashControl(Today, Today));
        Assert.ThrowsExactly<AccessDeniedException>(() => Reports.Forecast(Today));
    }

    [TestMethod]
    public async Task ViewModelFillsEveryTabAndExportsTheVisibleOne()
    {
        Persist();
        var model = new ReportsViewModel(new ReportService(work, user, TimeZoneInfo.Local), (_, _) => { });
        await model.LoadAsync();
        Assert.AreEqual("", model.Message);
        Assert.IsTrue(model.HasReport);
        Assert.HasCount(3, model.ComparisonKpis);
        Assert.HasCount(7, model.HeatRows);
        Assert.IsTrue(model.HasHeatmap);
        StringAssert.Contains(model.HeatPeak, "1 venta");
        Assert.IsTrue(model.HasCashierControl);
        StringAssert.StartsWith(model.Csv()!, "Reporte de ventas");
        await model.ShowAsync(ReportTab.Comparison);
        StringAssert.StartsWith(model.Csv()!, "Comparativo");
        StringAssert.StartsWith(model.SuggestedFileName, "CasaMiga-comparativo-");
        Assert.IsFalse(model.HasComparisonCoverage, "Sin ventas antes no hay comparación que advertir.");
        model.FromSelected = model.ThroughSelected = DateTime.Today.AddDays(1);
        await model.GenerateAsync();                                   // mañana (sin turnos) contra hoy (un turno)
        StringAssert.Contains(model.ComparisonCoverage, "0 turnos de caja y el anterior 1 turno de caja");
        await model.ShowAsync(ReportTab.Heatmap);
        StringAssert.StartsWith(model.Csv()!, "Ventas por hora");
        await model.ShowAsync(ReportTab.CashControl);
        StringAssert.StartsWith(model.Csv()!, "Control de caja");
        model.ForecastSelected = DateTime.Today.AddDays(7);
        await model.ShowAsync(ReportTab.Forecast);
        Assert.IsTrue(model.HasForecast, model.Message);
        Assert.AreEqual(3, model.ForecastRows.Single().Suggested);
        StringAssert.StartsWith(model.Csv()!, "Pronóstico de producción");
        StringAssert.StartsWith(model.SuggestedFileName, "CasaMiga-produccion-");
        model.Forget();
        Assert.IsTrue(model.IsSummary); Assert.IsNull(model.Csv());
    }
}
