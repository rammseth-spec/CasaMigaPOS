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

    private long webSequence = 100;
    private CloudCommand WebStep(string kind, JsonObject payload, Guid? by = null) => new(Guid.NewGuid(), ++webSequence, kind, payload, by ?? user.Current!.Id, "Encargado web");

    [TestMethod]
    public void ProductionFromTheWebInTwoStepsLikeTheScreen()
    {
        ProductionSetup();
        var batchId = Guid.NewGuid(); var operation = Guid.NewGuid();
        JsonObject Plan() => new()
        {
            ["batchId"] = batchId.ToString(), ["operationId"] = operation.ToString(), ["notes"] = "Turno de la tarde",
            ["outputs"] = new JsonArray(new JsonObject { ["profileVersionId"] = profileVersion.Id.ToString(), ["planned"] = 2 }), ["selections"] = new JsonArray(),
        };
        Assert.IsTrue(WebCommands.Apply(WebStep("production.prepare", Plan())).Applied);
        Assert.IsTrue(WebCommands.Apply(WebStep("production.prepare", Plan())).Applied, "Sending the same plan again returns the same draft.");
        var draft = Production().GetProduction(batchId)!;
        Assert.AreEqual("DRAFT", draft.Status);
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM production_batch"));
        Assert.AreEqual(0L, Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{finished.Id}'"), "Preparing fixes the plan without moving inventory.");
        var payload = new SqliteSyncOutbox(db).PendingEnvelopes(500, int.MaxValue).Last(e => e.Entry.Entity == "production_batch").Payload;
        Assert.AreEqual(draft.Formulas.Count, payload["formulas"]!.AsArray().Count, "The preparations of the plan travel with the batch.");

        JsonObject Actual(long count) => new() { ["batchId"] = batchId.ToString(), ["actuals"] = new JsonArray(new JsonObject { ["productId"] = product.Id.ToString(), ["actual"] = count }) };
        var identity = new IdentityService(work, new LocalPasswords(), clock, user);
        var temporary = identity.CreateUser("ana", "Ana", "Manager");
        identity.ChangePassword("ana", temporary, "Ana#2026centro");
        Assert.IsTrue(identity.Login("admin", password));
        var ana = work.Run(s => s.FindUser("ANA"))!.Id;
        Assert.AreEqual("Sólo el usuario y terminal que crearon este borrador pueden registrarlo.", WebCommands.Apply(WebStep("production.confirm", Actual(3), ana)).Message);

        Assert.IsTrue(WebCommands.Apply(WebStep("production.confirm", Actual(3))).Applied);
        Assert.IsTrue(WebCommands.Apply(WebStep("production.confirm", Actual(3))).Applied, "Registering it again changes nothing.");
        var done = Production().GetProduction(batchId)!;
        Assert.AreEqual("CONFIRMED", done.Status);
        Assert.AreEqual(3L, Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{finished.Id}'"));
        Assert.AreEqual("rejected", WebCommands.Apply(WebStep("production.confirm", Actual(1))).Status, "A registered production cannot change.");
    }

    [TestMethod]
    public void PreparingWithoutStockIsRejectedWithWhatIsMissing()
    {
        ProductionSetup(stock: 1);
        var result = WebCommands.Apply(WebStep("production.prepare", new()
        {
            ["batchId"] = Guid.NewGuid().ToString(), ["operationId"] = Guid.NewGuid().ToString(),
            ["outputs"] = new JsonArray(new JsonObject { ["profileVersionId"] = profileVersion.Id.ToString(), ["planned"] = 2 }), ["selections"] = new JsonArray(),
        }));
        StringAssert.StartsWith(result.Message, "No hay existencia suficiente para registrar esta producción. Falta: ");
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM production_batch"));
    }
}
