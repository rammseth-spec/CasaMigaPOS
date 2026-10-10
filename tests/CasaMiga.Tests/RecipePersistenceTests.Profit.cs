using CasaMiga.Application;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>«Ganancia» en Reportes de la caja (D1) con el costo de referencia de Inventario (D2): igual que Reportes → Ganancia en la web.</summary>
public sealed partial class RecipePersistenceTests
{
    private ProfitService Profits => new(work, new SqliteProductionUnitOfWork(db), user, TimeZoneInfo.Utc);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [TestMethod]
    public void ProfitUsesTheRecipeAndTheReferenceCostOfEachIngredient()
    {
        Publish();
        Persist(); // 2 piezas a $19.00
        var before = Profits.Profit(Today, Today);
        var pending = before.Costs.Single(c => c.ProductId == product.Id);
        Assert.IsNull(pending.Cost);
        Assert.AreEqual("FLOUR, PACKET, TOPPING, WATER", pending.Pending, "Only the preferred alternative (PACKET) is needed, not FRESH.");
        Assert.AreEqual(0, before.Counter.CoveredRevenue.Cents, "Without cost the sale does not enter the profit.");
        Assert.AreEqual(3800, before.Counter.Revenue.Cents);

        var costs = new IngredientCostService(work, user, clock, terminal.Id);
        costs.SetCost(flour.Id, new(10), 1, "mg");     // 10 ¢ por mg
        costs.SetCost(water.Id, new(2), 1, "µL");      // 2 ¢ por µL
        costs.SetCost(packet.Id, new(100), 1, "pieza");
        costs.SetCost(topping.Id, new(50), 1, "pieza");
        Assert.AreEqual(100L, costs.Find(packet.Id)!.CostCents);
        Assert.IsNull(costs.Find(fresh.Id), "Without a cost there is nothing to show.");

        var report = Profits.Profit(Today, Today);
        // Preparación: (60 mg × 10 + 20 µL × 2 + 1 paquete × 100) / 100 mg = 7.4 ¢ por mg; 25 mg por pieza + 1 cobertura × 50 = 235 ¢.
        Assert.AreEqual(235, report.Costs.Single(c => c.ProductId == product.Id).Cost!.Value.Cents);
        Assert.AreEqual("Completo", report.Costs.Single(c => c.ProductId == product.Id).Pending);
        var row = report.Products.Single();
        Assert.AreEqual(2, row.CounterPieces);
        Assert.AreEqual(3800 - 470, row.CounterProfit!.Value.Cents);
        Assert.AreEqual(1900 - 235, row.PerPieceCounter!.Value.Cents);
        Assert.AreEqual(3330, report.Counter.Profit.Cents);
        Assert.AreEqual(3330m / 3800m, report.Counter.Margin);
        Assert.AreEqual(0, report.Trays.Pieces);
    }

    [TestMethod]
    public async Task TheReportsScreenShowsProfitOnlyToWhoSeesSalesAndRecipes()
    {
        Publish(); Persist();
        var model = new ReportsViewModel(new ReportService(work, user, TimeZoneInfo.Local), (_, _) => { }, new ProfitService(work, new SqliteProductionUnitOfWork(db), user, TimeZoneInfo.Local));
        await model.LoadAsync();
        Assert.IsTrue(model.CanViewProfit);
        await model.ShowAsync(ReportTab.Profit);
        Assert.IsTrue(model.HasProfit);
        Assert.AreEqual("—", model.CounterProfit, "No costs yet.");
        StringAssert.Contains(model.MissingCosts, "1 productos sin costo");
        StringAssert.StartsWith(model.Csv()!, "Ganancia,");
        StringAssert.StartsWith(model.SuggestedFileName, "CasaMiga-ganancia-");
        var cashier = new ReportsViewModel(new ReportService(work, user, TimeZoneInfo.Local), (_, _) => { });
        await cashier.LoadAsync();
        Assert.IsFalse(cashier.CanViewProfit, "Without the profit service the tab does not appear.");
    }
}
