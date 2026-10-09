using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>Producción registrada desde la web (docs/POS_WEB.md, fase 3): la caja la prepara, la fija y la confirma con lo que salió.</summary>
public sealed partial class RecipePersistenceTests
{
    private CloudCommandService WebCommands => new(work, clock, terminal.Id, TimeZoneInfo.Utc,
        new CloudCommandModules(new SqliteInventoryStore(db, clock), work, new SqliteProductionUnitOfWork(db), _ => throw new NotSupportedException()));
    private CloudCommand WebProduction(long planned, long actual) => new(Guid.NewGuid(), 1, "production.record", new JsonObject
    {
        ["outputs"] = new JsonArray(new JsonObject { ["profileVersionId"] = profileVersion.Id.ToString(), ["planned"] = planned, ["actual"] = actual }),
        ["notes"] = "Turno de la mañana",
    }, user.Current!.Id, "Encargado web");

    [TestMethod]
    public void ProductionFromTheWebIsRegisteredOnceWithWhatCameOut()
    {
        ProductionSetup();
        var command = WebProduction(2, 3);
        var result = WebCommands.Apply(command);
        Assert.IsTrue(result.Applied, result.Message);
        var batch = Production().GetProduction(command.Id)!;
        Assert.AreEqual("CONFIRMED", batch.Status);
        Assert.AreEqual("Turno de la mañana", batch.Notes);
        Assert.AreEqual(3L, Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{finished.Id}'"), "The finished goods are what came out, not what was planned.");
        foreach (var input in batch.Inputs.GroupBy(x => x.ItemId))
            Assert.AreEqual(10000 - input.Sum(x => x.Required.AtomicUnits), Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{input.Key}'"));
        var before = AllRows(true);
        Assert.AreEqual(command.Id, WebCommands.Apply(command).Id);
        Assert.AreEqual(before, AllRows(true), "Applying it again changes nothing.");
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM production_batch"));
    }

    [TestMethod]
    public void ProductionWithoutStockIsRejectedWithoutLeavingADraft()
    {
        ProductionSetup(stock: 1);
        var result = WebCommands.Apply(WebProduction(2, 2));
        Assert.AreEqual("rejected", result.Status);
        StringAssert.StartsWith(result.Message, "No hay existencia suficiente para registrar esta producción. Falta: ");
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM production_batch"));
        Assert.AreEqual("rejected", WebCommands.Apply(new(Guid.NewGuid(), 2, "production.record", new JsonObject { ["outputs"] = new JsonArray() }, user.Current!.Id, "Encargado web")).Status);
    }
}
