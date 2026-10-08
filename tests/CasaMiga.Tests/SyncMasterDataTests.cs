using System.Text.Json.Nodes;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>Migración 023: la versión web recibe catálogo, usuarios, permisos, movimientos de caja y charolas.</summary>
[TestClass]
public sealed class SyncMasterDataTests : SalesFixture
{
    private SqliteSyncOutbox Outbox => new(db);
    private long Count(string entity, string? id = null) =>
        Convert.ToInt64(Sql($"SELECT count(*) FROM sync_outbox WHERE entity='{entity}'" + (id is null ? "" : $" AND entity_id='{id}'")));
    private JsonObject Latest(string entity, string id) =>
        Outbox.PendingEnvelopes(500, int.MaxValue).Last(e => e.Entry.Entity == entity && e.Entry.EntityId == id).Payload;

    [TestMethod]
    public void CatalogChangesAreQueuedWithTheirCurrentContent()
    {
        Assert.AreEqual(1L, Count("category", product.CategoryId.ToString()));
        Assert.AreEqual(1L, Count("product", product.Id.ToString()));
        catalog.EditProduct(product.Id, product.CategoryId, product.Sku, "Concha de vainilla", "", new Money(2100), product.TrackInventory);
        Assert.AreEqual(2L, Count("product", product.Id.ToString()), "Each change queues the product again.");

        var payload = Latest("product", product.Id.ToString());
        Assert.AreEqual("Concha de vainilla", (string?)payload["product"]!["name"]);
        Assert.AreEqual(2100L, (long)payload["product"]!["price_cents"]!, "The content is read when sending: the latest price travels.");
        Assert.IsNotNull(payload["inventoryLinks"]);
    }

    [TestMethod]
    public void UsersTravelWithTheirHashButSigningInDoesNotQueue()
    {
        var identity = new IdentityService(work, new LocalPasswords(), clock, user);
        var adminId = user.Current!.Id.ToString();
        var before = Count("app_user", adminId);
        for (var i = 0; i < 3; i++) Assert.IsTrue(identity.Login("admin", password));
        Assert.IsFalse(identity.Login("admin", "Incorrecta#2026"));
        Assert.IsTrue(identity.Login("admin", password));
        Assert.AreEqual(before, Count("app_user", adminId), "Logins, failures and lockout counters stay local.");

        var temporary = identity.CreateUser("cajera", "Cajera Centro", "Cashier");
        var cashier = (string)Sql("SELECT id FROM app_user WHERE username='CAJERA'")!;
        identity.ChangePassword("cajera", temporary, "Centro#2026ab");
        Assert.IsTrue(identity.Login("admin", password));
        Assert.AreEqual(2L, Count("app_user", cashier), "Created, then the definitive password.");
        var payload = Latest("app_user", cashier);
        Assert.AreEqual("CAJERA", (string?)payload["username"]);
        Assert.AreEqual("Cashier", (string?)payload["role_name"]);
        Assert.AreEqual(0L, (long)payload["must_change_password"]!);
        StringAssert.StartsWith((string?)payload["password_hash"], "AQAAAAIAA");
        Assert.IsNull(payload["failed_attempts"]); Assert.IsNull(payload["locked_until_utc"]);

        var roles = Latest("role_permission", "all")["rows"]!.AsArray();
        Assert.AreEqual(Convert.ToInt64(Sql("SELECT count(*) FROM role_permission")), roles.Count);
        var permissionsBefore = Count("role_permission");
        Sql("DELETE FROM role_permission WHERE role_name='Cashier' AND permission_name='cash.movement'");
        Assert.AreEqual(permissionsBefore + 1, Count("role_permission"));
    }

    [TestMethod]
    public void CashMovementsAndConsignmentAreQueued()
    {
        var movement = new CashMovementService(work, user, clock, terminal.Id).Record(new(Guid.NewGuid(), CashMovementKind.Deposit, null, new Money(5000), "Cambio"));
        Assert.AreEqual(5000L, (long)Latest("cash_movement", movement.Id.ToString())["amount_cents"]!);

        var trays = new ConsignmentService(work, user, clock, terminal.Id, TimeZoneInfo.Utc);
        trays.SetPrice(product.Id, new Money(1500));
        var store = trays.SaveStore(null, "Abarrotes Lupita", "Lupita", "555", "Centro");
        trays.SaveTemplate(store.Id, [new(product.Id, 6)]);
        var payload = Latest("consignment_store", store.Id.ToString());
        Assert.AreEqual("Abarrotes Lupita", (string?)payload["store"]!["name"]);
        Assert.AreEqual(6L, (long)payload["template"]![0]!["quantity"]!);

        trays.SetPrice(product.Id, null);
        var removed = Outbox.PendingEnvelopes(500, int.MaxValue).Last(e => e.Entry.Entity == "consignment_price");
        Assert.AreEqual("delete", removed.Entry.Change);
        Assert.IsTrue((bool)removed.Payload["deleted"]!, "A removed tray price travels as a deletion mark.");
    }

    [TestMethod]
    public void BatchesStopBeforeTheByteLimitButAlwaysCarryOne()
    {
        var all = Outbox.PendingEnvelopes(500, int.MaxValue);
        Assert.IsTrue(all.Count > 3);
        Assert.HasCount(1, Outbox.PendingEnvelopes(500, 1), "One entry even if it alone exceeds the limit.");
        var limit = all.Take(3).Sum(e => e.Payload.ToJsonString().Length);
        Assert.HasCount(3, Outbox.PendingEnvelopes(500, limit));
    }

    [TestMethod]
    public void QueueRejectsUnknownEntitiesAndChanges()
    {
        Assert.ThrowsExactly<SqliteException>(() => Sql("INSERT INTO sync_outbox(entity,entity_id,change) VALUES('secret','x','insert')"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("INSERT INTO sync_outbox(entity,entity_id,change) VALUES('product','x','truncate')"));
    }
}

/// <summary>Contraseñas temporales creadas en la web: la nube genera el hash y la caja lo acepta (cloud/lib/auth.ts).</summary>
[TestClass]
public sealed class CloudPasswordCompatibilityTests
{
    // Generado con identityHash('Prueba#2026nube') de la nube (PBKDF2-SHA512, 210 000 iteraciones, sal de 16 bytes).
    private const string CloudHash = "AQAAAAIAAzRQAAAAEBxRae3FEw6FtrMg/PLCO2PyRY0QCpPaCsZdt3OpOY7AbpYhUQCF95JqER4xcPLzdA==";

    [TestMethod]
    public void HashCreatedByTheCloudIsAcceptedByTheTerminal()
    {
        Assert.AreEqual(PasswordCheck.Valid, new LocalPasswords().Verify(CloudHash, "Prueba#2026nube"));
        Assert.AreEqual(PasswordCheck.Failed, new LocalPasswords().Verify(CloudHash, "Prueba#2026Nube"));
    }
}
