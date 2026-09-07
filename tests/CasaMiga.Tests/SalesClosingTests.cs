using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class SalesClosingTests : SalesFixture
{
    private CheckoutService Checkout()=>new(work,user,clock,terminal.Id,new CheckoutJournal(directory));
    private Sale Commit(CheckoutService service,Money received){var sale=service.Confirm(service.Prepare(received));service.Acknowledge(sale.OperationId);return sale;}
    [TestMethod] public void ExpectedUsesAppliedCashAndSurvivesCountResumeClose()
    {
        var checkout=Checkout();pos.Add(product.Id);Commit(checkout,new Money(10000));
        pos.Add(product.Id);pos.Increment(product.Id);Commit(checkout,new Money(5000));
        var closing=new CashClosingService(work,user,clock);var result=closing.SubmitCount(cash.Id,new Money(55700));
        Assert.AreEqual(55700L,result.Expected.Cents);Assert.AreEqual(0L,result.Difference.Cents);
        Assert.AreEqual(55700L,new CashClosingService(work,user,clock).Resume(cash.Id)!.Expected.Cents);
        Assert.AreEqual(55700L,closing.Close(cash.Id).Expected!.Value.Cents);
    }
    [TestMethod] public void ConfirmedCountRejectsPreviouslyPreparedCheckout()
    {
        var checkout=Checkout();pos.Add(product.Id);var request=checkout.Prepare(new Money(2000));pos.Discard(true);
        new CashClosingService(work,user,clock).SubmitCount(cash.Id,new Money(50000));
        Assert.ThrowsExactly<CheckoutException>(()=>checkout.Confirm(request));Assert.AreEqual(0L,Sql("SELECT count(*) FROM sale"));
    }
    [TestMethod] public void ExpectedOverflowRejectsSaleWithoutLosingCart()
    {
        Sql($"UPDATE cash_session SET opening_cents={long.MaxValue} WHERE id='{cash.Id}'");
        pos.Add(product.Id);var checkout=Checkout();var request=checkout.Prepare(new Money(2000));
        Assert.ThrowsExactly<OverflowException>(()=>checkout.Confirm(request));Assert.AreSame(request.Cart,user.Cart);
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM sale"));
    }
    [TestMethod] public void NewCashDoesNotIncludePreviousCashSales()
    {
        var checkout=Checkout();pos.Add(product.Id);Commit(checkout,new Money(2000));
        var closing=new CashClosingService(work,user,clock);closing.SubmitCount(cash.Id,new Money(51900));closing.Close(cash.Id);
        var next=new CashService(work,user,clock).Open(terminal.Id,new Money(100)).Session;
        Assert.AreEqual(100L,closing.SubmitCount(next.Id,new Money(100)).Expected.Cents);
    }
    [TestMethod] public void BackupRestoresCommittedSalesAndExpectedCash()
    {
        var checkout=Checkout();pos.Add(product.Id);var sale=Commit(checkout,new Money(2000));
        var package=new InstallationBackup(directory).Create();var destination=Path.Combine(directory,"restored");InstallationBackup.Restore(package,destination);
        var restored=new SqliteLocalUnitOfWork(new Database(Path.Combine(destination,"casamiga.db")));
        Assert.AreEqual(sale.Id,restored.Run(s=>s.FindSaleByOperation(sale.OperationId))!.Id);
        Assert.AreEqual(1900L,restored.Run(s=>s.CashSalesTotal(cash.Id)).Cents);
        Assert.IsFalse(File.Exists(Path.Combine(destination,"checkout.pending")));
    }
}
