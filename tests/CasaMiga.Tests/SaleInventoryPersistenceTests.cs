using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class SaleInventoryPersistenceTests : SalesFixture
{
    private InventoryItem SeedItem(long quantity = 20)
    {
        var store = new SqliteInventoryStore(db, clock);
        var item = new InventoryItem(Guid.NewGuid(), "CONSUME", "Consumption", InventoryItemKind.FinishedGood,
            InventoryDimension.Count, true, clock.UtcNow, clock.UtcNow);
        store.AddItem(item);
        store.Apply(new(Guid.NewGuid(), user.Current!.Id, terminal.Id, InventoryMovementKind.OpeningBalance,
            [InventoryOperationLine.Opening(item.Id, SqliteInventoryStore.MainLocationId,
                InventoryCapture.Standard(quantity, InventoryUnits.Piece))]));
        store.AddProductLink(new(product.Id, item.Id, new(InventoryDimension.Count, 3)));
        return item;
    }
    private SaleInventoryConsumptionPlan Plan(InventoryItem item, Sale sale, long factor = 3)
    {
        var link = Guid.Parse((string)Sql($"SELECT id FROM product_inventory_link WHERE product_id='{product.Id}'")!);
        return SaleInventoryConsumptionPolicy.Evaluate(sale.Items.Select((p, i) =>
            p.ProductId == product.Id ? new SaleInventoryLineInput(i + 1, p.ProductId, p.Quantity, true,
                new(link, p.ProductId, item.Id, item.Dimension, factor),
                new(item.Id, SqliteInventoryStore.MainLocationId, item.Dimension, true, 20))
            : new SaleInventoryLineInput(i + 1, p.ProductId, p.Quantity, false)));
    }
    private static void Execute(SqliteConnection c, SqliteTransaction tx, string sql)
    { using var q = new SqliteCommand(sql, c, tx); q.ExecuteNonQuery(); }

    [TestMethod]
    public void NewSchemaDefaultsAndPermissionAreCorrectAndNormalSalesDoNotConsume()
    {
        Assert.AreEqual(11L, Sql("SELECT max(version) FROM schema_migration"));
        work.RunTechnical((s, c, tx) =>
        {
            var config = SqliteSaleInventoryStore.ReadConfig(c, tx);
            Assert.AreEqual(InventorySaleConsumptionState.Disabled, config.State);
            Assert.AreEqual(1L, config.Version); Assert.IsNull(config.UpdatedBy); return true;
        });
        Assert.AreEqual("Administrator", Sql("SELECT role_name FROM role_permission WHERE permission_name='inventory.sale_consumption.configure'"));
        Persist();
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale_inventory_evaluation"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_operation WHERE kind='SALE_CONSUMPTION'"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sqlite_master WHERE type='trigger' AND lower(sql) LIKE '%update inventory_balance%'"));
    }

    [TestMethod]
    public void DisabledAndNoConsumptionHaveDistinctSnapshotSemantics()
    {
        work.RunTechnical((s, c, tx) =>
        {
            var sale = Candidate(s, Guid.NewGuid()); s.AddSale(sale);
            var result = SqliteSaleInventoryStore.Save(c, tx, sale, null);
            Assert.AreEqual(SaleInventoryEvaluationStatus.Disabled, result.Status); Assert.IsNull(result.InventoryOperationId);
            return true;
        });
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale_inventory_line_snapshot"));
        work.RunTechnical((s, c, tx) =>
        {
            var sale = Candidate(s, Guid.NewGuid()); s.AddSale(sale);
            var plan = SaleInventoryConsumptionPolicy.Evaluate([new(1, product.Id, 2, false)]);
            var result = SqliteSaleInventoryStore.Save(c, tx, sale, plan);
            Assert.AreEqual(SaleInventoryEvaluationStatus.NoConsumption, result.Status); Assert.IsNull(result.InventoryOperationId);
            Assert.AreEqual(result, SqliteSaleInventoryStore.Save(c, tx, sale, plan));
            return true;
        });
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM sale_inventory_line_snapshot WHERE track_inventory=0 AND link_id IS NULL"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM inventory_movement")); db.Validate();
    }

    [TestMethod]
    public void ConsumedIsAtomicImmutableIdempotentAndRestorable()
    {
        var item = SeedItem();
        Sale sale = null!; SaleInventoryConsumptionPlan plan = null!;
        work.RunTechnical((s, c, tx) =>
        {
            sale = Candidate(s, Guid.NewGuid()); s.AddSale(sale); plan = Plan(item, sale);
            var evaluation = SqliteSaleInventoryStore.Save(c, tx, sale, plan);
            Assert.AreEqual(SaleInventoryEvaluationStatus.Consumed, evaluation.Status);
            Assert.AreEqual(SaleConsumptionIdentity.OperationIdV1(sale.OperationId), evaluation.InventoryOperationId);
            return true;
        });
        work.RunTechnical((s, c, tx) =>
        {
            var first = SqliteSaleInventoryStore.Find(c, tx, sale.OperationId);
            Assert.AreEqual(first, SqliteSaleInventoryStore.Save(c, tx, sale, plan));
            Assert.ThrowsExactly<InventoryIdempotencyConflictException>(() => SqliteSaleInventoryStore.Save(c, tx, sale, Plan(item, sale, 2)));
            return true;
        });
        var inventory = new SqliteInventoryStore(db, clock);
        Assert.AreEqual(14L, inventory.FindBalance(item.Id, SqliteInventoryStore.MainLocationId)!.QuantityAtomic);
        Assert.IsEmpty(inventory.Reconcile());
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_operation WHERE kind='SALE_CONSUMPTION'"));
        Assert.AreEqual(-6L, Sql("SELECT delta_atomic FROM inventory_movement WHERE is_opening=0"));
        foreach (var table in new[] { "sale_inventory_evaluation", "sale_inventory_line_snapshot" })
        {
            Assert.ThrowsExactly<SqliteException>(() => Sql($"DELETE FROM {table}"));
            Assert.ThrowsExactly<SqliteException>(() => Sql($"UPDATE {table} SET policy_version=policy_version"));
        }
        Sql("UPDATE product_inventory_link SET quantity_per_product_unit_atomic=9,active=0");
        Assert.AreEqual(3L, Sql("SELECT quantity_per_product_unit_atomic FROM sale_inventory_line_snapshot"));
        db.Validate();
        string backup; using (var lease = new DirectoryLease(directory)) backup = new InstallationBackup(directory).Create();
        var target = Path.Combine(directory, "restored"); InstallationBackup.Restore(backup, target);
        var restored = new Database(Path.Combine(target, "casamiga.db")); restored.Validate();
        Assert.IsEmpty(new SqliteInventoryStore(restored, clock).Reconcile());
        new SqliteLocalUnitOfWork(restored).RunTechnical((s, c, tx) =>
        {
            Assert.AreEqual(SaleInventoryEvaluationStatus.Consumed, SqliteSaleInventoryStore.Find(c, tx, sale.OperationId)!.Status);
            Assert.AreEqual(InventorySaleConsumptionState.Disabled, SqliteSaleInventoryStore.ReadConfig(c, tx).State);
            Assert.AreEqual(SaleInventoryEvaluationStatus.Consumed, SqliteSaleInventoryStore.Save(c, tx, sale, plan).Status);
            return true;
        });
    }

    [TestMethod]
    [DataRow("sale_inventory_evaluation", "INSERT")]
    [DataRow("sale_inventory_line_snapshot", "INSERT")]
    [DataRow("inventory_operation", "INSERT")]
    [DataRow("inventory_movement", "INSERT")]
    [DataRow("inventory_balance", "UPDATE")]
    [DataRow("audit_entry", "INSERT")]
    public void FailureAtEachTechnicalWriteRollsBackWholeCommercialTransaction(string table, string action)
    {
        var item = SeedItem();
        Sql($"CREATE TRIGGER injected_failure BEFORE {action} ON {table} BEGIN SELECT RAISE(ABORT,'test failure'); END");
        Assert.ThrowsExactly<SqliteException>(() => work.RunTechnical((s, c, tx) =>
        {
            var sale = Candidate(s, Guid.NewGuid()); s.AddSale(sale);
            SqliteSaleInventoryStore.Save(c, tx, sale, Plan(item, sale)); return true;
        }));
        foreach (var name in new[] { "sale", "sale_item", "payment", "sale_sequence", "sale_inventory_evaluation", "sale_inventory_line_snapshot" })
            Assert.AreEqual(0L, Sql($"SELECT count(*) FROM {name}"), name);
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_operation"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_movement"));
        Assert.AreEqual(20L, Sql("SELECT quantity_atomic FROM inventory_balance"));
        Assert.IsEmpty(new SqliteInventoryStore(db, clock).Reconcile()); db.Validate();
    }

    [TestMethod]
    public void MixedSaleAggregatesTwoProductsAndPreservesAllSnapshotsForCashier()
    {
        var item = SeedItem();
        var second = catalog.CreateProduct(product.CategoryId, "P2", "Otro", "", new Money(100));
        var untracked = catalog.CreateProduct(product.CategoryId, "P3", "Servicio", "", new Money(100));
        new SqliteInventoryStore(db, clock).AddProductLink(new(second.Id, item.Id, new(InventoryDimension.Count, 2)));
        var cashier = Guid.NewGuid();
        Sql($"INSERT INTO app_user(id,username,display_name,password_hash,role_name,created_utc) VALUES('{cashier}','CASH','Cashier','synthetic','Cashier','{clock.UtcNow:O}')");
        work.RunTechnical((s, c, tx) =>
        {
            Assert.IsFalse(s.HasPermission(cashier, "inventory.adjust"));
            Assert.IsFalse(s.HasPermission(cashier, "inventory.receive"));
            var sale = new Sale(Guid.NewGuid(), Guid.NewGuid(), cashier, terminal.Id, cash.Id,
                s.NextSaleSequence(terminal.Id), clock.UtcNow,
                [new(product.Id, product.Name, new Money(100), 2), new(second.Id, second.Name, new Money(100), 3),
                 new(untracked.Id, untracked.Name, new Money(100), 1)], Guid.NewGuid(), new Money(1000));
            s.AddSale(sale);
            var inputs = new List<SaleInventoryLineInput>();
            for (int i = 0; i < 2; i++)
            {
                var p = sale.Items[i];
                using var q = new SqliteCommand("SELECT id,quantity_per_product_unit_atomic FROM product_inventory_link WHERE product_id=$id", c, tx);
                q.Parameters.AddWithValue("$id", p.ProductId.ToString("D")); using var r = q.ExecuteReader(); Assert.IsTrue(r.Read());
                inputs.Add(new(i + 1, p.ProductId, p.Quantity, true,
                    new(Guid.Parse(r.GetString(0)), p.ProductId, item.Id, item.Dimension, r.GetInt64(1)),
                    new(item.Id, SqliteInventoryStore.MainLocationId, item.Dimension, true, 20)));
            }
            inputs.Add(new(3, untracked.Id, 1, false));
            SqliteSaleInventoryStore.Save(c, tx, sale, SaleInventoryConsumptionPolicy.Evaluate(inputs));
            return true;
        });
        Assert.AreEqual(3L, Sql("SELECT count(*) FROM sale_inventory_line_snapshot"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_movement WHERE is_opening=0"));
        Assert.AreEqual(-12L, Sql("SELECT delta_atomic FROM inventory_movement WHERE is_opening=0"));
        Assert.AreEqual(cashier.ToString("D"), Sql("SELECT actor_id FROM inventory_operation WHERE kind='SALE_CONSUMPTION'"));
        Assert.AreEqual(8L, Sql("SELECT quantity_atomic FROM inventory_balance"));
        db.Validate(); Assert.IsEmpty(new SqliteInventoryStore(db, clock).Reconcile());
    }

    [TestMethod]
    public void SnapshotSurvivesLaterProductLinkAndItemChanges()
    {
        var item = SeedItem();
        Sql("UPDATE product_inventory_link SET quantity_per_product_unit_atomic=10");
        work.RunTechnical((s, c, tx) =>
        {
            var sale = Candidate(s, Guid.NewGuid()); s.AddSale(sale);
            SqliteSaleInventoryStore.Save(c, tx, sale, Plan(item, sale, 10)); return true;
        });
        Sql("UPDATE product SET track_inventory=0");
        Sql("UPDATE product_inventory_link SET quantity_per_product_unit_atomic=100,active=0");
        Sql("UPDATE inventory_item SET active=0");
        Assert.AreEqual(20L, Sql("SELECT total_consumed_atomic FROM sale_inventory_line_snapshot"));
        Assert.AreEqual(10L, Sql("SELECT quantity_per_product_unit_atomic FROM sale_inventory_line_snapshot"));
        Assert.AreEqual(1L, Sql("SELECT track_inventory FROM sale_inventory_line_snapshot"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM product_inventory_link"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM product"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM inventory_item"));
    }

    [TestMethod]
    public void StalePlanCannotProduceNegativeStock()
    {
        var item = SeedItem(5);
        Assert.ThrowsExactly<InventoryStockException>(() => work.RunTechnical((s, c, tx) =>
        {
            var sale = Candidate(s, Guid.NewGuid()); s.AddSale(sale);
            SqliteSaleInventoryStore.Save(c, tx, sale, Plan(item, sale)); return true;
        }));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale"));
        Assert.AreEqual(5L, Sql("SELECT quantity_atomic FROM inventory_balance"));
    }

    [TestMethod]
    public void CaughtSnapshotFailureRollsBackSavepointWithoutPartialInventory()
    {
        var item = SeedItem(); var sale = Persist();
        Sql("CREATE TRIGGER injected_failure BEFORE INSERT ON sale_inventory_line_snapshot BEGIN SELECT RAISE(ABORT,'test failure'); END");
        work.RunTechnical((s, c, tx) =>
        {
            Assert.ThrowsExactly<SqliteException>(() => SqliteSaleInventoryStore.Save(c, tx, sale, Plan(item, sale)));
            return true;
        });
        Assert.AreEqual(20L, Sql("SELECT quantity_atomic FROM inventory_balance"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM sale_inventory_evaluation"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM inventory_movement"));
    }

    [TestMethod]
    public void ConfigurationTransitionsAreVersionedAndNeverPauseBeforeActivation()
    {
        Assert.ThrowsExactly<SqliteException>(() => Sql($"UPDATE inventory_sale_consumption_config SET state='PAUSED',version=2,updated_by='{user.Current!.Id}'"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM inventory_sale_consumption_config"));
        foreach (var step in new[] { ("ENABLED", 2), ("PAUSED", 3), ("ENABLED", 4) })
            Sql($"UPDATE inventory_sale_consumption_config SET state='{step.Item1}',version={step.Item2},updated_by='{user.Current!.Id}'");
        Assert.AreEqual(4L, Sql("SELECT version FROM inventory_sale_consumption_config"));
        // This direct SQL probe lives only in an isolated test database, never the application.
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(1L)]
    public void DatabaseRejectsNonNegativeSaleMovement(long delta)
    {
        var item = SeedItem();
        work.RunTechnical((s, c, tx) =>
        {
            var operation = Guid.NewGuid();
            Execute(c, tx, $"INSERT INTO inventory_operation(id,request_sha256,kind,state,actor_id,terminal_id,expected_line_count,created_utc,sale_evaluation_id) VALUES('{operation}','{new string('A',64)}','SALE_CONSUMPTION','PENDING','{user.Current!.Id}','{terminal.Id}',1,'{clock.UtcNow:O}','{Guid.NewGuid()}')");
            Assert.ThrowsExactly<SqliteException>(() => Execute(c, tx,
                $"INSERT INTO inventory_movement VALUES('{Guid.NewGuid()}','{operation}',1,'{item.Id}','{SqliteInventoryStore.MainLocationId}','COUNT',0,{delta},20,{20+delta},'piece','1',1)"));
            Execute(c, tx, $"DELETE FROM inventory_operation WHERE id='{operation}'"); return true;
        });
    }

    [TestMethod]
    public void EvaluationCannotSealIncompleteOrWrongStatusSnapshots()
    {
        var sale = Persist();
        foreach (var status in new[] { "NO_CONSUMPTION", "CONSUMED", "BLOCKED" })
            Assert.ThrowsExactly<SqliteException>(() => Sql($"INSERT INTO sale_inventory_evaluation VALUES('{sale.OperationId}','{sale.Id}','{sale.OperationId}',1,'{status}',NULL,1,'{new string('A',64)}','{sale.ConfirmedAt:O}')"));
        work.RunTechnical((s, c, tx) => { SqliteSaleInventoryStore.Save(c, tx, sale, null); return true; });
        Assert.ThrowsExactly<SqliteException>(() => Sql($"INSERT INTO sale_inventory_line_snapshot VALUES('{sale.OperationId}','{sale.Id}',1,'{product.Id}',2,0,NULL,NULL,NULL,NULL,NULL,0,1)"));
    }

    [TestMethod]
    public void DeferredOriginRejectsOrphanConsumptionAndPublicApplyRemainsClosed()
    {
        var item = SeedItem(); var sale = Persist(); var plan = Plan(item, sale);
        var request = SaleConsumptionOperationFactory.Create(SaleConsumptionIdentity.OperationIdV1(sale.OperationId),
            sale.UserId, sale.TerminalId, plan);
        Assert.ThrowsExactly<InventoryConsistencyException>(() => new SqliteInventoryStore(db, clock).Apply(request));
        Assert.ThrowsExactly<SqliteException>(() => work.RunTechnical((s, c, tx) =>
        {
            SqliteInventoryStore.WriteOperation(c, tx, request, null, clock.UtcNow, sale.OperationId); return true;
        }));
        Assert.AreEqual(20L, Sql("SELECT quantity_atomic FROM inventory_balance"));
    }
}
