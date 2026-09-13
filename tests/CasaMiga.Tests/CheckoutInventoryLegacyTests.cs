using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class CheckoutInventoryLegacyTests:SalesFixture
{
    protected override int? FixtureSchemaVersion=>11;
    [TestMethod]
    public void MigratedLegacyRetryDoesNotEvaluateCurrentInvalidConfiguration()
    {
        var sale=Persist();db.Initialize();
        Sql($"UPDATE inventory_sale_consumption_config SET state='ENABLED',version=2,updated_by='{user.Current!.Id}'");
        // Tracked product has no link: a new checkout would fail, legacy must recover.
        pos.Add(product.Id);pos.Increment(product.Id);
        var service=new CheckoutService(work,user,clock,terminal.Id,new CasaMiga.Infrastructure.CheckoutJournal(directory));
        var request=service.Prepare(new Money(5000)) with {OperationId=sale.OperationId};
        Assert.AreEqual(sale.Id,service.Confirm(request).Id);
        Assert.AreEqual(0L,Sql("SELECT inventory_evaluation_contract_version FROM sale"));
        Assert.AreEqual(DBNull.Value,Sql("SELECT inventory_evaluation_id FROM sale"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM sale_inventory_evaluation"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_movement"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));service.Acknowledge(sale.OperationId);db.Validate();
    }
}
