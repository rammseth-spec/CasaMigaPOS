using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class ConsignmentTests : SalesFixture
{
    private ConsignmentService Trays => new(work, user, clock, terminal.Id, TimeZoneInfo.Utc);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private Product bolillo = null!;
    private ConsignmentStore lupita = null!;

    [TestInitialize]
    public void Prepare()
    {
        bolillo = catalog.CreateProduct(catalog.CreateCategory("Salado").Id, "P2", "Bolillo", "", new Money(500));
        Trays.SetPrice(product.Id, new Money(1500));     // concha: $19 en mostrador, $15 en charola
        Trays.SetPrice(bolillo.Id, new Money(400));      // bolillo: $5 en mostrador, $4 en charola
        lupita = Trays.SaveStore(null, "Abarrotes Lupita", "Lupita", "555 123 4567", "Av. Juárez 10");
    }

    private ConsignmentDelivery DeliverDozen() => Trays.Deliver(lupita.Id, [new(product.Id, 8), new(bolillo.Id, 4)], "");

    [TestMethod]
    public void PricesListRetailAndTrayPriceAndStoreNamesAreUnique()
    {
        var concha = Trays.Prices().Single(p => p.ProductId == product.Id);
        Assert.AreEqual(1900L, concha.RetailPrice.Cents); Assert.AreEqual(1500L, concha.Price!.Value.Cents);
        Trays.SetPrice(product.Id, null);
        Assert.IsNull(Trays.Prices().Single(p => p.ProductId == product.Id).Price);
        Assert.ThrowsExactly<ConsignmentException>(() => Trays.SaveStore(null, "  abarrotes LUPITA ", "", "", ""));
        Assert.ThrowsExactly<ArgumentException>(() => Trays.SaveStore(null, " ", "", "", ""));
        var renamed = Trays.SaveStore(lupita.Id, "Abarrotes Lupita Centro", "Lupita", "", "");
        Assert.AreEqual("Abarrotes Lupita Centro", Trays.Stores().Single().Name);
        Assert.AreEqual(lupita.Id, renamed.Id);
    }

    [TestMethod]
    public void DeliveryNeedsTrayPriceActiveStoreAndPieces()
    {
        Trays.SetPrice(bolillo.Id, null);
        var missing = Assert.ThrowsExactly<ConsignmentException>(() => DeliverDozen());
        StringAssert.Contains(missing.Message, "Bolillo no tiene precio de charola");
        Assert.ThrowsExactly<ArgumentException>(() => Trays.Deliver(lupita.Id, [new(product.Id, 0)], ""));
        Assert.ThrowsExactly<ArgumentException>(() => Trays.Deliver(lupita.Id, [new(product.Id, 1000)], ""));
        Trays.SetStoreActive(lupita.Id, false);
        Assert.ThrowsExactly<ConsignmentException>(() => Trays.Deliver(lupita.Id, [new(product.Id, 12)], ""));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM consignment_delivery"));
    }

    [TestMethod]
    public void CollectingChargesSoldPiecesAndKeepsTheBalance()
    {
        var tray = DeliverDozen();
        Assert.AreEqual(12, tray.Pieces); Assert.AreEqual(8 * 1500 + 4 * 400, tray.Value.Cents);
        var route = Trays.Route().Single();
        Assert.AreEqual(tray.Id, route.AtStore.Single().Id); Assert.AreEqual(0L, route.Balance.Cents);

        // Regresan 2 conchas: se cobran 6 conchas y 4 bolillos = $106; pagan $50 en el momento.
        var collected = Trays.Collect(tray.Id, new Dictionary<int, int> { [1] = 2 }, new Money(5000), PaymentMethod.Cash, null, "");
        Assert.AreEqual(ConsignmentState.Collected, collected.Delivery.State);
        Assert.AreEqual(10600L, collected.Delivery.Settlement!.SoldTotal.Cents);
        Assert.AreEqual(2, collected.Delivery.Settlement.Returned);
        Assert.AreEqual(5600L, collected.Balance.Cents);
        Assert.IsEmpty(Trays.Route().Single().AtStore);

        var account = Trays.Account(lupita.Id);
        Assert.AreEqual(5600L, account.Balance.Cents); Assert.AreEqual(10600L, account.Charged.Cents); Assert.AreEqual(5000L, account.Paid.Cents);
        Assert.ThrowsExactly<ConsignmentException>(() => Trays.ReceivePayment(lupita.Id, new Money(5700), PaymentMethod.Cash, null, ""));
        Trays.ReceivePayment(lupita.Id, new Money(5600), PaymentMethod.Transfer, "SPEI 123", "");
        Assert.AreEqual(0L, Trays.Account(lupita.Id).Balance.Cents);
        StringAssert.Contains(Assert.ThrowsExactly<ConsignmentException>(() => Trays.ReceivePayment(lupita.Id, new Money(100), PaymentMethod.Cash, null, "")).Message, "no tiene saldo");
    }

    [TestMethod]
    public void TrayCanBeCollectedOnceAndOnlyUncollectedTraysAreVoided()
    {
        var tray = DeliverDozen();
        Assert.ThrowsExactly<ArgumentException>(() => Trays.Collect(tray.Id, new Dictionary<int, int> { [1] = 9 }, new Money(0), PaymentMethod.Cash, null, ""));
        Assert.ThrowsExactly<ConsignmentException>(() => Trays.Collect(tray.Id, new Dictionary<int, int>(), new Money(30000), PaymentMethod.Cash, null, ""),
            "No se cobra más de lo que la tienda debe.");
        Trays.Collect(tray.Id, new Dictionary<int, int>(), new Money(0), PaymentMethod.Cash, null, "");
        Assert.ThrowsExactly<ConsignmentException>(() => Trays.Collect(tray.Id, new Dictionary<int, int>(), new Money(0), PaymentMethod.Cash, null, ""));
        Assert.ThrowsExactly<ConsignmentException>(() => Trays.Void(tray.Id, "Se capturó mal"));

        var wrong = Trays.Deliver(lupita.Id, [new(product.Id, 15)], "");
        var voided = Trays.Void(wrong.Id, "Se capturó mal");
        Assert.AreEqual(ConsignmentState.Voided, voided.State);
        Assert.IsEmpty(Trays.Route().Single().AtStore);
        Assert.ThrowsExactly<ConsignmentException>(() => Trays.Collect(wrong.Id, new Dictionary<int, int>(), new Money(0), PaymentMethod.Cash, null, ""));
        Assert.ThrowsExactly<ArgumentException>(() => Trays.Void(DeliverDozen().Id, "x"), "El motivo es obligatorio.");
    }

    [TestMethod]
    public void StoreWithTraysCannotBeDeactivatedAndTemplateIsKept()
    {
        Trays.SaveTemplate(lupita.Id, [new(product.Id, 8), new(bolillo.Id, 4), new(bolillo.Id, 3)]);
        var template = Trays.Template(lupita.Id);
        Assert.AreEqual(7, template.Single(l => l.ProductId == bolillo.Id).Quantity, "Las líneas del mismo pan se suman.");
        Assert.AreEqual(15, Trays.Route().Single().TemplatePieces);
        DeliverDozen();
        Assert.ThrowsExactly<ConsignmentException>(() => Trays.SetStoreActive(lupita.Id, false));
    }

    [TestMethod]
    public void TrayMoneyNeverTouchesTheCashDrawer()
    {
        Persist();
        var expected = work.Run(s => s.CashSalesTotal(cash.Id));
        var tray = DeliverDozen();
        Trays.Collect(tray.Id, new Dictionary<int, int>(), new Money(12000), PaymentMethod.Cash, null, "");
        Assert.AreEqual(expected, work.Run(s => s.CashSalesTotal(cash.Id)));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM sale"), "Recoger una charola no crea ventas de mostrador.");
    }

    [TestMethod]
    public void ReportShowsSellThroughPiedraAndWhatStaysWithTheStores()
    {
        var tray = DeliverDozen();
        Trays.Collect(tray.Id, new Dictionary<int, int> { [1] = 2 }, new Money(5000), PaymentMethod.Cash, null, "");
        Trays.Deliver(lupita.Id, [new(bolillo.Id, 15)], "");                         // sigue en la tienda
        Trays.Void(Trays.Deliver(lupita.Id, [new(product.Id, 3)], "").Id, "Error de captura");

        var report = Trays.Report(Today, Today);
        var store = report.Stores.Single();
        Assert.AreEqual(2, store.Trays, "La anulada no cuenta.");
        Assert.AreEqual(27, store.Delivered); Assert.AreEqual(10, store.Sold); Assert.AreEqual(2, store.Returned); Assert.AreEqual(15, store.AtStore);
        Assert.AreEqual(83.3m, store.SellThrough);
        Assert.AreEqual(10600L, store.Charged.Cents);
        Assert.AreEqual(6 * 1900 + 4 * 500, store.RetailValue.Cents);
        Assert.AreEqual(2800L, store.LeftToStores.Cents, "En mostrador esas piezas habrían dejado $28 más.");
        Assert.AreEqual(5000L, store.Paid.Cents); Assert.AreEqual(5600L, store.Balance.Cents);
        var concha = report.Products.Single(p => p.ProductId == product.Id);
        Assert.AreEqual(8, concha.Delivered); Assert.AreEqual(6, concha.Sold); Assert.AreEqual(2, concha.Returned); Assert.AreEqual(75.0m, concha.SellThrough);
        Assert.AreEqual(2, report.Returned, "Piezas que regresan para Piedra.");
        Assert.IsEmpty(Trays.Report(Today.AddDays(1), Today.AddDays(1)).Stores);
    }

    [TestMethod]
    public void RecordsAreImmutableAndPermissionsApply()
    {
        var tray = DeliverDozen();
        Trays.Collect(tray.Id, new Dictionary<int, int>(), new Money(1000), PaymentMethod.Card, "4321", "");
        Assert.ThrowsExactly<SqliteException>(() => Sql("UPDATE consignment_delivery SET note='x'"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM consignment_settlement"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("UPDATE consignment_payment SET amount_cents=1"));
        Assert.ThrowsExactly<SqliteException>(() => Sql("DELETE FROM consignment_store"));
        Assert.AreEqual(3L, Sql("SELECT count(*) FROM audit_entry WHERE action IN ('consignment.delivered','consignment.collected','consignment.payment')"));

        Sql("UPDATE app_user SET role_name='Cashier'");
        Assert.IsFalse(Trays.CanView()); Assert.IsFalse(Trays.CanManage());
        Assert.ThrowsExactly<AccessDeniedException>(() => Trays.Route());
        Assert.ThrowsExactly<AccessDeniedException>(() => DeliverDozen());
    }

    [TestMethod]
    public void DatabaseRejectsInconsistentSettlements()
    {
        var tray = DeliverDozen();
        // Vendidas + regresadas debe ser igual a lo entregado.
        Assert.ThrowsExactly<SqliteException>(() => Sql($"INSERT INTO consignment_settlement_item VALUES('{tray.Id}',1,5,2,7500)"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM consignment_settlement_item"));
    }
}
