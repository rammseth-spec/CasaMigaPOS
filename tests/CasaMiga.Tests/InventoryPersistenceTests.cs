using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class InventoryPersistenceTests
{
    private string root = null!;
    private string directory = null!;
    private Database database = null!;
    private SqliteInventoryStore inventory = null!;
    private Guid actor;
    private TerminalIdentity terminal = null!;
    private TestClock clock = null!;
    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 11, 18, 0, 0, TimeSpan.Zero);
    }

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "CasaMigaTests", Guid.NewGuid().ToString("N"));
        directory = Path.Combine(root, "source");
        Directory.CreateDirectory(directory);
        database = new(Path.Combine(directory, "casamiga.db"));
        database.Initialize();
        var work = new SqliteLocalUnitOfWork(database);
        clock = new();
        var session = new UserSession();
        var identity = new IdentityService(work, new LocalPasswords(), clock, session);
        var password = Guid.NewGuid().ToString("N");
        identity.Bootstrap("admin", "Inventario", password);
        Assert.IsTrue(identity.Login("admin", password));
        actor = session.Current!.Id;
        terminal = TerminalConfiguration.LoadOrCreate(directory);
        work.Run(store => { store.EnsureTerminal(terminal, clock.UtcNow); return true; });
        inventory = new(database, clock);
    }

    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    private InventoryItem Item(string code = "FLOUR", InventoryDimension dimension = InventoryDimension.Mass,
        InventoryItemKind kind = InventoryItemKind.RawMaterial)
    {
        var item = new InventoryItem(Guid.NewGuid(), code, code, kind, dimension, true, clock.UtcNow, clock.UtcNow);
        inventory.AddItem(item);
        return item;
    }

    private static InventoryCapture Capture(decimal amount, InventoryUnit unit) => InventoryCapture.Standard(amount, unit);
    private InventoryOperationRequest Opening(InventoryItem item, decimal amount, InventoryUnit unit, Guid? operation = null) =>
        new(operation ?? Guid.NewGuid(), actor, terminal.Id, InventoryMovementKind.OpeningBalance,
            [InventoryOperationLine.Opening(item.Id, SqliteInventoryStore.MainLocationId, Capture(amount, unit))]);
    private InventoryOperationRequest Change(InventoryItem item, InventoryMovementKind kind, decimal amount,
        InventoryUnit unit, string? reason = null, string? note = null, Guid? operation = null)
    {
        var capture = Capture(amount, unit);
        var delta = kind is InventoryMovementKind.Receipt or InventoryMovementKind.AdjustmentGain
            ? InventoryDelta.Increase(capture.Quantity) : InventoryDelta.Decrease(capture.Quantity);
        return new(operation ?? Guid.NewGuid(), actor, terminal.Id, kind,
            [InventoryOperationLine.Change(item.Id, SqliteInventoryStore.MainLocationId, delta, capture)], reason, note);
    }

    private object? Scalar(string sql)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    [TestMethod]
    public void SchemaNineSeedsUnitsLocationAndApprovedPermissions()
    {
        Assert.AreEqual(9L, Scalar("SELECT count(*) FROM schema_migration"));
        Assert.AreEqual(7L, Scalar("SELECT count(*) FROM measurement_unit"));
        Assert.AreEqual("Almacén principal", Scalar("SELECT name FROM inventory_location WHERE code='MAIN'"));
        Assert.AreEqual(5L, Scalar("SELECT count(*) FROM permission WHERE name LIKE 'inventory.%'"));
        Assert.AreEqual(5L, Scalar("SELECT count(*) FROM role_permission WHERE role_name='Administrator' AND permission_name LIKE 'inventory.%'"));
        Assert.AreEqual(3L, Scalar("SELECT count(*) FROM role_permission WHERE role_name='Manager' AND permission_name LIKE 'inventory.%'"));
        Assert.AreEqual(0L, Scalar("SELECT count(*) FROM role_permission WHERE role_name='Cashier' AND permission_name LIKE 'inventory.%'"));
        Assert.AreEqual(8L, Scalar("SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN('measurement_unit','inventory_item','inventory_item_unit','inventory_location','inventory_operation','inventory_movement','inventory_balance','product_inventory_link')"));
        Assert.AreEqual(0L, Scalar("SELECT count(*) FROM sqlite_master WHERE type='trigger' AND instr(upper(sql),'UPDATE INVENTORY_BALANCE')>0"));
        database.Validate();
        Assert.ThrowsExactly<SqliteException>(() => Scalar("DELETE FROM measurement_unit WHERE code='kg'"));
    }

    [TestMethod]
    public void PersistenceRevalidatesApprovedRolePermissions()
    {
        var item = Item();
        Scalar("UPDATE app_user SET role_name='Manager'");
        Assert.ThrowsExactly<InventoryPersistenceException>(() =>
            new SqliteInventoryStore(database, clock).Apply(Opening(item, 0m, InventoryUnits.Gram)));
        Scalar("UPDATE app_user SET role_name='Administrator'");
        inventory.Apply(Opening(item, 10m, InventoryUnits.Gram));
        Scalar("UPDATE app_user SET role_name='Manager'");
        inventory.Apply(Change(item, InventoryMovementKind.Receipt, 1m, InventoryUnits.Gram));
        inventory.Apply(Change(item, InventoryMovementKind.Waste, 1m, InventoryUnits.Gram, "DAMAGED"));
        Assert.ThrowsExactly<InventoryPersistenceException>(() =>
            inventory.Apply(Change(item, InventoryMovementKind.AdjustmentGain, 1m, InventoryUnits.Gram, "DATA_CORRECTION")));
        Scalar("UPDATE app_user SET role_name='Cashier'");
        Assert.ThrowsExactly<InventoryPersistenceException>(() =>
            inventory.Apply(Change(item, InventoryMovementKind.Receipt, 1m, InventoryUnits.Gram)));
    }

    [TestMethod]
    public void PositivePositiveAndZeroOpeningBalancesAreAuditableInitializations()
    {
        var flour = Item();
        var cinnamon = Item("CINNAMON");
        var positive = inventory.Apply(Opening(flour, 25m, InventoryUnits.Kilogram));
        var zero = inventory.Apply(Opening(cinnamon, 0m, InventoryUnits.Gram));
        Assert.AreEqual(actor, positive.ActorId);
        Assert.AreEqual(terminal.Id, positive.TerminalId);
        Assert.AreEqual(clock.UtcNow, positive.ConfirmedAt);
        Assert.AreEqual(25_000_000L, positive.Movements.Single().DeltaAtomic);
        Assert.IsTrue(zero.Movements.Single().IsOpening);
        Assert.AreEqual(0L, zero.Movements.Single().DeltaAtomic);
        Assert.AreEqual(0L, inventory.FindBalance(cinnamon.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.AreEqual(2L, Scalar("SELECT count(*) FROM audit_entry WHERE action='inventory.operation.confirmed'"));
    }

    [TestMethod]
    public void SecondOpeningIsRejectedWithoutPartialOperation()
    {
        var item = Item();
        inventory.Apply(Opening(item, 1m, InventoryUnits.Kilogram));
        Assert.ThrowsExactly<InventoryStockException>(() => inventory.Apply(Opening(item, 2m, InventoryUnits.Kilogram)));
        Assert.AreEqual(1L, Scalar("SELECT count(*) FROM inventory_operation"));
        Assert.AreEqual(1_000_000L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
    }

    [TestMethod]
    public void ReceiptGainLossAndWasteApplyExactSignedDeltas()
    {
        var item = Item();
        inventory.Apply(Opening(item, 10m, InventoryUnits.Kilogram));
        inventory.Apply(Change(item, InventoryMovementKind.Receipt, 2m, InventoryUnits.Kilogram));
        inventory.Apply(Change(item, InventoryMovementKind.AdjustmentGain, 500m, InventoryUnits.Gram, "DATA_CORRECTION"));
        inventory.Apply(Change(item, InventoryMovementKind.AdjustmentLoss, 250m, InventoryUnits.Gram, "PHYSICAL_COUNT_VARIANCE"));
        var waste = inventory.Apply(Change(item, InventoryMovementKind.Waste, 1.250m, InventoryUnits.Kilogram, "SPILL"));
        Assert.AreEqual(-1_250_000L, waste.Movements.Single().DeltaAtomic);
        Assert.AreEqual(11_000_000L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.AreEqual("1.25", waste.Movements.Single().CapturedQuantity);
        Assert.AreEqual(1_000_000L, waste.Movements.Single().CaptureAtomicFactor);
    }

    [TestMethod]
    public void NegativeStockRollsBackWholeMultipartOperation()
    {
        var first = Item("FIRST");
        var second = Item("SECOND");
        inventory.Apply(Opening(first, 10m, InventoryUnits.Gram));
        inventory.Apply(Opening(second, 10m, InventoryUnits.Gram));
        var one = Capture(5m, InventoryUnits.Gram);
        var two = Capture(20m, InventoryUnits.Gram);
        var request = new InventoryOperationRequest(Guid.NewGuid(), actor, terminal.Id, InventoryMovementKind.AdjustmentLoss,
        [
            InventoryOperationLine.Change(first.Id, SqliteInventoryStore.MainLocationId, InventoryDelta.Decrease(one.Quantity), one),
            InventoryOperationLine.Change(second.Id, SqliteInventoryStore.MainLocationId, InventoryDelta.Decrease(two.Quantity), two)
        ], "DATA_CORRECTION");
        Assert.ThrowsExactly<InventoryStockException>(() => inventory.Apply(request));
        Assert.AreEqual(10_000L, inventory.FindBalance(first.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.AreEqual(10_000L, inventory.FindBalance(second.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.AreEqual(0L, Scalar($"SELECT count(*) FROM inventory_operation WHERE id='{request.OperationId:D}'"));
    }

    [TestMethod]
    public void SameRequestIsIdempotentAndDifferentContentConflicts()
    {
        var item = Item();
        inventory.Apply(Opening(item, 0m, InventoryUnits.Gram));
        var operation = Guid.NewGuid();
        var request = Change(item, InventoryMovementKind.Receipt, 1m, InventoryUnits.Kilogram, operation: operation);
        var first = inventory.Apply(request);
        var retry = inventory.Apply(request);
        Assert.AreEqual(first.OperationId, retry.OperationId);
        Assert.AreEqual(first.RequestSha256, retry.RequestSha256);
        Assert.AreEqual(first.Movements.Single(), retry.Movements.Single());
        Assert.AreEqual(1_000_000L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        var conflict = Change(item, InventoryMovementKind.Receipt, 2m, InventoryUnits.Kilogram, operation: operation);
        Assert.ThrowsExactly<InventoryIdempotencyConflictException>(() => inventory.Apply(conflict));
        Assert.AreEqual(1L, Scalar($"SELECT count(*) FROM inventory_movement WHERE operation_id='{operation:D}'"));
    }

    [TestMethod]
    public void RequestHashUsesDeterministicLineOrder()
    {
        var first = Item("FIRST");
        var second = Item("SECOND");
        var operation = Guid.NewGuid();
        var firstLine = InventoryOperationLine.Opening(first.Id, SqliteInventoryStore.MainLocationId,
            Capture(1m, InventoryUnits.Gram));
        var secondLine = InventoryOperationLine.Opening(second.Id, SqliteInventoryStore.MainLocationId,
            Capture(2m, InventoryUnits.Gram));
        var forward = new InventoryOperationRequest(operation, actor, terminal.Id,
            InventoryMovementKind.OpeningBalance, [firstLine, secondLine]);
        var reverse = new InventoryOperationRequest(operation, actor, terminal.Id,
            InventoryMovementKind.OpeningBalance, [secondLine, firstLine]);
        Assert.AreEqual(SqliteInventoryStore.RequestHash(forward), SqliteInventoryStore.RequestHash(reverse));
        var stored = inventory.Apply(reverse);
        Assert.IsTrue(stored.Movements[0].InventoryItemId.CompareTo(stored.Movements[1].InventoryItemId) < 0);
    }

    [TestMethod]
    public void RecoveryReadsConfirmedResultUsingCanonicalHash()
    {
        var item = Item();
        var request = Opening(item, 3.250m, InventoryUnits.Kilogram);
        var confirmed = inventory.Apply(request);
        var recovered = new SqliteInventoryStore(new Database(database.FilePath), clock).Recover(request);
        Assert.IsNotNull(recovered);
        Assert.AreEqual(confirmed.RequestSha256, recovered.RequestSha256);
        Assert.AreEqual(64, recovered.RequestSha256.Length);
        Assert.AreEqual(3_250_000L, recovered.Movements.Single().BalanceAfterAtomic);
    }

    [TestMethod]
    public void ExceptionAfterCommitRecoversWithoutApplyingTwice()
    {
        var item = Item();
        var request = Opening(item, 2m, InventoryUnits.Gram);
        var uncertain = new SqliteInventoryStore(database, clock, () => throw new IOException("Respuesta perdida"));
        var recovered = uncertain.Apply(request);
        Assert.AreEqual(request.OperationId, recovered.OperationId);
        Assert.AreEqual(2_000L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.AreEqual(1L, Scalar($"SELECT count(*) FROM inventory_movement WHERE operation_id='{request.OperationId:D}'"));
    }

    [TestMethod]
    public void BalanceUsesConditionalVersionAndUpdatesOnce()
    {
        var item = Item();
        inventory.Apply(Opening(item, 0m, InventoryUnits.Gram));
        var request = Change(item, InventoryMovementKind.Receipt, 1m, InventoryUnits.Gram);
        inventory.Apply(request);
        inventory.Apply(request);
        var balance = inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!;
        Assert.AreEqual(1_000L, balance.QuantityAtomic);
        Assert.AreEqual(2L, balance.Version);
    }

    [TestMethod]
    public void ConfirmedLedgerAndOperationAreImmutable()
    {
        var item = Item();
        var operation = inventory.Apply(Opening(item, 1m, InventoryUnits.Gram));
        Assert.ThrowsExactly<SqliteException>(() => Scalar($"UPDATE inventory_movement SET delta_atomic=2 WHERE operation_id='{operation.OperationId:D}'"));
        Assert.ThrowsExactly<SqliteException>(() => Scalar($"DELETE FROM inventory_movement WHERE operation_id='{operation.OperationId:D}'"));
        Assert.ThrowsExactly<SqliteException>(() => Scalar($"UPDATE inventory_operation SET note='tamper' WHERE id='{operation.OperationId:D}'"));
        Assert.ThrowsExactly<SqliteException>(() => Scalar($"DELETE FROM inventory_operation WHERE id='{operation.OperationId:D}'"));
    }

    [TestMethod]
    public void ReconciliationReportsProjectionDivergenceWithoutRepair()
    {
        var item = Item();
        inventory.Apply(Opening(item, 5m, InventoryUnits.Gram));
        Assert.IsEmpty(inventory.Reconcile());
        Scalar("UPDATE inventory_balance SET quantity_atomic=6000");
        var difference = inventory.Reconcile().Single();
        Assert.AreEqual(5_000L, difference.LedgerQuantityAtomic);
        Assert.AreEqual(6_000L, difference.BalanceQuantityAtomic);
        Assert.AreEqual(6_000L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
    }

    [TestMethod]
    public async Task ConcurrentReceiptsBothCommitWithDeterministicBalance()
    {
        var item = Item();
        inventory.Apply(Opening(item, 0m, InventoryUnits.Gram));
        using var gate = new ManualResetEventSlim(false);
        Task Apply(decimal grams) => Task.Run(() =>
        {
            gate.Wait();
            new SqliteInventoryStore(new Database(database.FilePath), clock)
                .Apply(Change(item, InventoryMovementKind.Receipt, grams, InventoryUnits.Gram));
        });
        var first = Apply(5m);
        var second = Apply(7m);
        gate.Set();
        await Task.WhenAll(first, second);
        Assert.AreEqual(12_000L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.IsEmpty(inventory.Reconcile());
    }

    [TestMethod]
    public async Task CompetingLossesCannotProduceNegativeStock()
    {
        var item = Item();
        inventory.Apply(Opening(item, 10m, InventoryUnits.Gram));
        using var gate = new ManualResetEventSlim(false);
        Task<bool> Apply() => Task.Run(() =>
        {
            gate.Wait();
            try
            {
                new SqliteInventoryStore(new Database(database.FilePath), clock)
                    .Apply(Change(item, InventoryMovementKind.Waste, 7m, InventoryUnits.Gram, "DAMAGED"));
                return true;
            }
            catch (InventoryStockException) { return false; }
        });
        var first = Apply();
        var second = Apply();
        gate.Set();
        var results = await Task.WhenAll(first, second);
        Assert.AreEqual(1, results.Count(x => x));
        Assert.AreEqual(3_000L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
    }

    [TestMethod]
    public async Task ConcurrentSameOperationAppliesOnceAndConflictIsRejected()
    {
        var item = Item();
        inventory.Apply(Opening(item, 0m, InventoryUnits.Gram));
        var operation = Guid.NewGuid();
        var request = Change(item, InventoryMovementKind.Receipt, 2m, InventoryUnits.Gram, operation: operation);
        using var gate = new ManualResetEventSlim(false);
        Task<PersistedInventoryOperation> Apply() => Task.Run(() =>
        {
            gate.Wait();
            return new SqliteInventoryStore(new Database(database.FilePath), clock).Apply(request);
        });
        var first = Apply();
        var second = Apply();
        gate.Set();
        await Task.WhenAll(first, second);
        Assert.AreEqual(2_000L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.AreEqual(1L, Scalar($"SELECT count(*) FROM inventory_operation WHERE id='{operation:D}'"));
        Assert.ThrowsExactly<InventoryIdempotencyConflictException>(() =>
            inventory.Apply(Change(item, InventoryMovementKind.Receipt, 3m, InventoryUnits.Gram, operation: operation)));
    }

    [TestMethod]
    public async Task ConcurrentDifferentContentWithSameOperationHasOneWinner()
    {
        var item = Item();
        inventory.Apply(Opening(item, 0m, InventoryUnits.Gram));
        var operation = Guid.NewGuid();
        var one = Change(item, InventoryMovementKind.Receipt, 1m, InventoryUnits.Gram, operation: operation);
        var two = Change(item, InventoryMovementKind.Receipt, 2m, InventoryUnits.Gram, operation: operation);
        using var gate = new ManualResetEventSlim(false);
        Task<bool> Apply(InventoryOperationRequest request) => Task.Run(() =>
        {
            gate.Wait();
            try
            {
                new SqliteInventoryStore(new Database(database.FilePath), clock).Apply(request);
                return true;
            }
            catch (InventoryIdempotencyConflictException) { return false; }
        });
        var first = Apply(one);
        var second = Apply(two);
        gate.Set();
        var results = await Task.WhenAll(first, second);
        Assert.AreEqual(1, results.Count(x => x));
        Assert.AreEqual(1L, Scalar($"SELECT count(*) FROM inventory_operation WHERE id='{operation:D}'"));
        Assert.IsTrue(inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic is 1_000 or 2_000);
    }

    [TestMethod]
    public void ItemPresentationPersistsAndConvertsWholePackages()
    {
        var item = Item();
        var sack = new InventoryItemUnit(Guid.NewGuid(), item, "SACK25", "Saco de 25 kg", 25_000_000);
        inventory.AddItemUnit(sack);
        var capture = InventoryCapture.Presentation(2, sack);
        var request = new InventoryOperationRequest(Guid.NewGuid(), actor, terminal.Id,
            InventoryMovementKind.OpeningBalance,
            [InventoryOperationLine.Opening(item.Id, SqliteInventoryStore.MainLocationId, capture)]);
        inventory.Apply(request);
        Assert.AreEqual(50_000_000L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.AreEqual("2", Scalar("SELECT captured_quantity FROM inventory_movement"));
        Assert.AreEqual(25_000_000L, Scalar("SELECT capture_atomic_factor FROM inventory_movement"));
    }

    [TestMethod]
    public void QuantityOverflowRollsBackOperation()
    {
        var item = Item();
        inventory.Apply(new InventoryOperationRequest(Guid.NewGuid(), actor, terminal.Id,
            InventoryMovementKind.OpeningBalance,
            [InventoryOperationLine.Opening(item.Id, SqliteInventoryStore.MainLocationId,
                InventoryCapture.Standard(long.MaxValue, InventoryUnits.Milligram))]));
        var request = Change(item, InventoryMovementKind.Receipt, 1m, InventoryUnits.Milligram);
        Assert.ThrowsExactly<InventoryStockException>(() => inventory.Apply(request));
        Assert.AreEqual(long.MaxValue, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.AreEqual(0L, Scalar($"SELECT count(*) FROM inventory_operation WHERE id='{request.OperationId:D}'"));
    }

    [TestMethod]
    public void ProductLinkPersistsWithoutChangingCheckoutSchemaOrSales()
    {
        var item = Item("CONCHA", InventoryDimension.Count, InventoryItemKind.FinishedGood);
        var product = Guid.NewGuid();
        using (var connection = database.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO category(id,name,active) VALUES('category','Pan dulce',1); INSERT INTO product(id,name,category_id,price_cents,track_inventory,active,created_utc,modified_utc) VALUES($id,'Concha','category',1900,1,1,$at,$at)";
            command.Parameters.AddWithValue("$id", product.ToString("D"));
            command.Parameters.AddWithValue("$at", clock.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
        inventory.AddProductLink(new(product, item.Id, InventoryQuantity.From(1m, InventoryUnits.Piece)));
        Assert.AreEqual(1L, Scalar("SELECT quantity_per_product_unit_atomic FROM product_inventory_link"));
        Assert.AreEqual(0L, Scalar("SELECT count(*) FROM sale"));
        Assert.AreEqual(0L, Scalar("SELECT count(*) FROM inventory_movement"));
    }

    [TestMethod]
    public void InstallationBackupRestoresLedgerBalanceAndIdempotency()
    {
        var item = Item();
        var unit = new InventoryItemUnit(Guid.NewGuid(), item, "SACK25", "Saco de 25 kg", 25_000_000);
        inventory.AddItemUnit(unit);
        var product = Guid.NewGuid();
        using (var connection = database.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO category(id,name,active) VALUES('backup-category','Pan',1); INSERT INTO product(id,name,category_id,price_cents,track_inventory,active,created_utc,modified_utc) VALUES($id,'Harina','backup-category',0,1,1,$at,$at)";
            command.Parameters.AddWithValue("$id", product.ToString("D"));
            command.Parameters.AddWithValue("$at", clock.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
        inventory.AddProductLink(new(product, item.Id, InventoryQuantity.From(1m, InventoryUnits.Gram)));
        var request = Opening(item, 1m, InventoryUnits.Kilogram);
        inventory.Apply(request);
        string package;
        using (var lease = new DirectoryLease(directory)) package = new InstallationBackup(directory).Create();
        var restoredDirectory = Path.Combine(root, "restored");
        InstallationBackup.Restore(package, restoredDirectory);
        var restoredDatabase = new Database(Path.Combine(restoredDirectory, "casamiga.db"));
        var restored = new SqliteInventoryStore(restoredDatabase, clock);
        Assert.AreEqual(1_000_000L, restored.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.IsNotNull(restored.Recover(request));
        Assert.IsEmpty(restored.Reconcile());
        using var restoredConnection = restoredDatabase.Open(readOnly: true);
        using var restoredCommand = restoredConnection.CreateCommand();
        restoredCommand.CommandText = "SELECT (SELECT count(*) FROM measurement_unit)=7 AND (SELECT count(*) FROM inventory_item)=1 AND (SELECT count(*) FROM inventory_item_unit)=1 AND (SELECT count(*) FROM inventory_location)=1 AND (SELECT count(*) FROM product_inventory_link)=1";
        Assert.AreEqual(1L, restoredCommand.ExecuteScalar());
    }

    [TestMethod]
    public void UpgradeFromEightPreservesExistingDataAndFailedNineDoesNotRegister()
    {
        var upgrade = VersionEight(Path.Combine(root, "upgrade.db"));
        using (var connection = upgrade.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO category(id,name,active) VALUES('existing','Existente',1)";
            command.ExecuteNonQuery();
        }
        upgrade.Initialize();
        using (var connection = upgrade.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT count(*) FROM schema_migration";
            Assert.AreEqual(9L, command.ExecuteScalar());
            command.CommandText = "SELECT name FROM category WHERE id='existing'";
            Assert.AreEqual("Existente", command.ExecuteScalar());
        }

        var failed = VersionEight(Path.Combine(root, "failed.db"));
        using (var connection = failed.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE measurement_unit(conflict TEXT)";
            command.ExecuteNonQuery();
        }
        Assert.ThrowsExactly<SqliteException>(failed.Initialize);
        using var failedConnection = failed.Open();
        using var failedCommand = failedConnection.CreateCommand();
        failedCommand.CommandText = "SELECT count(*) FROM schema_migration";
        Assert.AreEqual(8L, failedCommand.ExecuteScalar());
    }

    private static Database VersionEight(string path)
    {
        var previous = new Database(path);
        var assembly = typeof(Database).Assembly;
        using var connection = previous.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT";
        command.ExecuteNonQuery();
        for (int version = 1; version <= 8; version++)
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(
                assembly.GetManifestResourceNames().Single(name => name.Contains($".{version:000}_", StringComparison.Ordinal)))!);
            var sql = reader.ReadToEnd();
            command.Parameters.Clear();
            command.CommandText = sql;
            command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO schema_migration VALUES($version,$hash,$at)";
            command.Parameters.AddWithValue("$version", version);
            command.Parameters.AddWithValue("$hash", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
            command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
        return previous;
    }
}
