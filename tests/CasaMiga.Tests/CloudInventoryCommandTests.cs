using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>Inventario desde la web como la pantalla de la caja (docs/PLAN_COMPLETAR.md, W4): artículos, presentaciones, asociaciones y consumo automático.</summary>
[TestClass]
public sealed class CloudInventoryCommandTests : SalesFixture
{
    private long sequence;
    private CloudCommandService Commands => new(work, clock, terminal.Id, TimeZoneInfo.Utc,
        new CloudCommandModules(new SqliteInventoryStore(db, clock), work, new SqliteProductionUnitOfWork(db), _ => throw new NotSupportedException()));
    private CloudCommand Command(string kind, JsonObject payload, Guid? by = null) => new(Guid.NewGuid(), ++sequence, kind, payload, by ?? user.Current!.Id, "Administrador web");
    private static void Ok(CloudCommandResult result) => Assert.IsTrue(result.Applied, result.Message);
    private static readonly string Main = SqliteInventoryStore.MainLocationId.ToString();
    private long Count(string sql) => Convert.ToInt64(Sql(sql));
    private long Balance(Guid item) => Count($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{item}'");
    private JsonObject Item(Guid id, string code, string name, string kind, string dimension) =>
        new() { ["id"] = id.ToString(), ["code"] = code, ["name"] = name, ["kind"] = kind, ["dimension"] = dimension };

    [TestMethod]
    public void ItemsPresentationsAndOpeningFromTheWebWorkLikeTheScreen()
    {
        var butter = Guid.NewGuid();
        var create = Command("inventory.item.save", Item(butter, "MAN", "Mantequilla", "RawMaterial", "Mass"));
        Ok(Commands.Apply(create));
        Ok(Commands.Apply(create));
        Assert.AreEqual(1L, Count($"SELECT count(*) FROM inventory_item WHERE id='{butter}' AND code='MAN' AND kind='RAW_MATERIAL' AND dimension='MASS' AND active=1"),
            "The web chooses the id, so the item it shows is the one the till creates; applying again creates nothing.");
        Assert.AreEqual(1L, Count($"SELECT count(*) FROM audit_entry WHERE action='inventory.item.created' AND entity_id='{butter}'"));

        Ok(Commands.Apply(Command("inventory.item.save", Item(butter, "OTRO", "Mantequilla sin sal", "Supply", "Count"))));
        Assert.AreEqual(1L, Count($"SELECT count(*) FROM inventory_item WHERE id='{butter}' AND code='MAN' AND name='Mantequilla sin sal' AND kind='SUPPLY' AND dimension='MASS'"),
            "Editing keeps the code and the dimension, as on the screen.");
        Assert.AreEqual("rejected", Commands.Apply(Command("inventory.item.save", Item(Guid.NewGuid(), "X", "Malo", "Flour", "Mass"))).Status);

        var block = Command("inventory.unit.add", new() { ["itemId"] = butter.ToString(), ["code"] = "BLQ", ["name"] = "Bloque", ["atomic"] = 500_000 });
        Ok(Commands.Apply(block));
        Ok(Commands.Apply(Command("inventory.unit.add", new() { ["itemId"] = butter.ToString(), ["code"] = "BLQ", ["name"] = "Bloque", ["atomic"] = 500_000 })));
        Assert.AreEqual(1L, Count($"SELECT count(*) FROM inventory_item_unit WHERE item_id='{butter}' AND code='BLQ' AND atomic_units_per_whole_unit=500000"),
            "The same presentation sent twice is added once.");
        Assert.AreEqual("rejected", Commands.Apply(Command("inventory.unit.add", new() { ["itemId"] = butter.ToString(), ["code"] = "BLQ", ["name"] = "Bloque grande", ["atomic"] = 1_000_000 })).Status,
            "A different presentation with a taken code is refused.");

        StringAssert.Contains(Commands.Apply(Command("inventory.receive", new() { ["itemId"] = butter.ToString(), ["locationId"] = Main, ["quantity"] = "1", ["unitCode"] = "kg" })).Message,
            "no tiene saldo inicial");
        var opening = Command("inventory.opening", new() { ["itemId"] = butter.ToString(), ["locationId"] = Main, ["quantity"] = "4", ["unitCode"] = "BLQ", ["reference"] = "Inventario de apertura" });
        Ok(Commands.Apply(opening));
        Assert.AreEqual(2_000_000L, Balance(butter), "Four 500 g blocks.");
        Assert.AreEqual("Inventario de apertura", (string)Sql($"SELECT external_reference FROM inventory_operation WHERE id='{opening.Id}' AND kind='OPENING_BALANCE'")!);
        StringAssert.Contains(Commands.Apply(Command("inventory.opening", new() { ["itemId"] = butter.ToString(), ["locationId"] = Main, ["quantity"] = "1", ["unitCode"] = "kg" })).Message,
            "ya", "The opening balance is registered once.");

        Assert.AreEqual("No se puede desactivar un artículo con existencia.", Commands.Apply(Command("inventory.item.active", new() { ["id"] = butter.ToString(), ["active"] = false })).Message);
        Ok(Commands.Apply(Command("inventory.count", new() { ["itemId"] = butter.ToString(), ["locationId"] = Main, ["quantity"] = "0", ["unitCode"] = "kg", ["reason"] = "PhysicalCountVariance" })));
        Ok(Commands.Apply(Command("inventory.item.active", new() { ["id"] = butter.ToString(), ["active"] = false })));
        Assert.AreEqual(0L, Count($"SELECT active FROM inventory_item WHERE id='{butter}'"));
        Ok(Commands.Apply(Command("inventory.item.active", new() { ["id"] = butter.ToString(), ["active"] = true })));
        Assert.AreEqual(1L, Count($"SELECT active FROM inventory_item WHERE id='{butter}'"));
    }

    [TestMethod]
    public void ProductLinksAndSaleConsumptionFromTheWeb()
    {
        var concha = Guid.NewGuid();
        Ok(Commands.Apply(Command("inventory.item.save", Item(concha, "PT-CON", "Concha terminada", "FinishedGood", "Count"))));
        JsonObject Consumption(string action, string state, long version) => new() { ["action"] = action, ["state"] = state, ["version"] = version };

        var blocked = Commands.Apply(Command("inventory.consumption", Consumption("Enable", "Disabled", 1)));
        Assert.AreEqual("rejected", blocked.Status);
        StringAssert.Contains(blocked.Message, "Concha controla inventario pero no tiene un artículo asociado.", "The same check as «Verificar configuración».");

        var link = Command("inventory.link", new() { ["productId"] = product.Id.ToString(), ["itemId"] = concha.ToString(), ["atomic"] = 1 });
        Ok(Commands.Apply(link));
        Ok(Commands.Apply(Command("inventory.link", new() { ["productId"] = product.Id.ToString(), ["itemId"] = concha.ToString(), ["atomic"] = 1 })));
        Assert.AreEqual(1L, Count($"SELECT count(*) FROM product_inventory_link WHERE product_id='{product.Id}' AND active=1"), "The same link sent twice is created once.");
        Assert.AreEqual("rejected", Commands.Apply(Command("inventory.link", new() { ["productId"] = product.Id.ToString(), ["itemId"] = concha.ToString(), ["atomic"] = 2 })).Status,
            "A product has a single active link; unlink first.");
        StringAssert.Contains(Commands.Apply(Command("inventory.consumption", Consumption("Enable", "Disabled", 1))).Message, "no tiene saldo inicial registrado");

        Ok(Commands.Apply(Command("inventory.opening", new() { ["itemId"] = concha.ToString(), ["locationId"] = Main, ["quantity"] = "30", ["unitCode"] = "piece" })));
        Ok(Commands.Apply(Command("inventory.consumption", Consumption("Enable", "Disabled", 1))));
        Assert.AreEqual("ENABLED", (string)Sql("SELECT state FROM inventory_sale_consumption_config")!);
        StringAssert.Contains(Commands.Apply(Command("inventory.consumption", Consumption("Pause", "Enabled", 1))).Message, "Otro administrador cambió el estado",
            "The web sends the state it saw; if it changed meanwhile, nothing happens.");
        Ok(Commands.Apply(Command("inventory.consumption", Consumption("Pause", "Enabled", 2))));
        Ok(Commands.Apply(Command("inventory.consumption", Consumption("Resume", "Paused", 3))));
        Assert.AreEqual(4L, Count("SELECT version FROM inventory_sale_consumption_config"));
        Assert.AreEqual(1L, Count("SELECT count(*) FROM audit_entry WHERE action='inventory.sale_consumption.paused'"));

        var branch = new SqliteSyncOutbox(db).PendingEnvelopes(500, int.MaxValue).Last(e => e.Entry.Entity == "branch").Payload;
        Assert.AreEqual("ENABLED", (string)branch["sale_consumption"]!["state"]!, "The web sees the state with the branch.");
        Assert.AreEqual(4L, (long)branch["sale_consumption"]!["version"]!);

        Ok(Commands.Apply(Command("inventory.unlink", new() { ["productId"] = product.Id.ToString() })));
        Ok(Commands.Apply(Command("inventory.unlink", new() { ["productId"] = product.Id.ToString() })));
        Assert.AreEqual(0L, Count($"SELECT count(*) FROM product_inventory_link WHERE product_id='{product.Id}' AND active=1"));
        var uploaded = new SqliteSyncOutbox(db).PendingEnvelopes(500, int.MaxValue).Last(e => e.Entry.Entity == "product" && e.Entry.EntityId == product.Id.ToString()).Payload;
        Assert.AreEqual(0L, (long)uploaded["inventoryLinks"]!.AsArray().Last()!["active"]!, "The link goes up inside the product.");
    }

    [TestMethod]
    public void AManagerCannotConfigureInventoryFromTheWeb()
    {
        var identity = new IdentityService(work, new LocalPasswords(), clock, user);
        var temporary = identity.CreateUser("ana", "Ana", "Manager");
        identity.ChangePassword("ana", temporary, "Ana#2026centro");
        Assert.IsTrue(identity.Login("admin", password));
        var ana = work.Run(s => s.FindUser("ANA"))!.Id;
        Assert.AreEqual("rejected", Commands.Apply(Command("inventory.item.save", Item(Guid.NewGuid(), "AZU", "Azúcar", "RawMaterial", "Mass"), ana)).Status);
        Assert.AreEqual("rejected", Commands.Apply(Command("inventory.consumption", new() { ["action"] = "Enable", ["state"] = "Disabled", ["version"] = 1 }, ana)).Status);
        Assert.AreEqual(0L, Count("SELECT count(*) FROM inventory_item"));
        Assert.AreEqual("DISABLED", (string)Sql("SELECT state FROM inventory_sale_consumption_config")!);
    }
}
