using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class CheckoutTests : SalesFixture
{
    private CheckoutService Service(ILocalUnitOfWork? unit=null,UserSession? session=null) => new(unit??work,session??user,clock,terminal.Id,new CheckoutJournal(directory));
    private CheckoutRequest Request(CheckoutService service){pos.Add(product.Id);return service.Prepare(new(2000));}
    [TestMethod] public void CommitCreatesSingleAggregateAuditAndClearsCart()
    {
        var service=Service();var request=Request(service);var sale=service.Confirm(request);
        Assert.IsNull(user.Cart);Assert.IsTrue(service.HasPending);service.Acknowledge(sale.OperationId);Assert.IsFalse(service.HasPending);Assert.AreEqual(100L,sale.Payment.Change.Cents);
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale_item"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM payment"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='sale.completed'"));
    }
    [TestMethod] public void RepeatedOperationReturnsOriginalWithoutClearingNewCart()
    {
        var service=Service();var request=Request(service);var original=service.Confirm(request);service.Acknowledge(original.OperationId);pos.Add(product.Id);var newCart=user.Cart;
        Assert.AreEqual(original.Id,service.Confirm(request).Id);Assert.AreSame(newCart,user.Cart);service.Acknowledge(original.OperationId);
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='sale.completed'"));
        Assert.ThrowsExactly<CheckoutException>(()=>service.Confirm(request with {Received=new(3000)}));
        Assert.AreSame(newCart,user.Cart);
    }
    [TestMethod] public void AuditFailureRollsBackAndPreservesCartForRetry()
    {
        var service=Service();var request=Request(service);
        Sql("CREATE TRIGGER fail_sale_audit BEFORE INSERT ON audit_entry WHEN NEW.action='sale.completed' BEGIN SELECT RAISE(ABORT,'test failure'); END");
        Assert.ThrowsExactly<CheckoutException>(()=>service.Confirm(request));
        Assert.AreSame(request.Cart,user.Cart);Assert.IsFalse(service.HasPending);
        Assert.AreEqual(0L,Sql("SELECT (SELECT count(*) FROM sale)+(SELECT count(*) FROM sale_item)+(SELECT count(*) FROM payment)+(SELECT count(*) FROM sale_sequence)"));
        Sql("DROP TRIGGER fail_sale_audit");Assert.AreEqual(1L,service.Confirm(request).Sequence);
    }
    [TestMethod] public void SnapshotsStayStableAndInactiveProductRejectsCheckout()
    {
        var service=Service();var request=Request(service);
        catalog.EditProduct(product.Id,product.CategoryId,product.Sku,"Nuevo nombre","",new Money(3000),product.TrackInventory);
        var sale=service.Confirm(request);service.Acknowledge(sale.OperationId);Assert.AreEqual("Concha",sale.Items[0].ProductName);Assert.AreEqual(1900L,sale.Total.Cents);
        pos.Add(product.Id);var next=service.Prepare(new(4000));catalog.SetProductActive(product.Id,false);
        Assert.ThrowsExactly<CheckoutException>(()=>service.Confirm(next));Assert.AreSame(next.Cart,user.Cart);
    }
    [TestMethod] public void ChangedCartAndRevokedPermissionRejectConfirmation()
    {
        var service=Service();var request=Request(service);pos.Increment(product.Id);
        Assert.ThrowsExactly<CheckoutException>(()=>service.Confirm(request));
        Sql("DELETE FROM role_permission WHERE permission_name='sales.complete'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>service.Prepare(new(5000)));Assert.IsFalse(service.HasPending);
    }
    [TestMethod] public void CommitResponseFailureResolvesWithoutDuplicate()
    {
        var unit=new FaultUnit(work);var service=Service(unit);var request=Request(service);unit.FailAfterCommit=true;
        var sale=service.Confirm(request);Assert.IsNull(user.Cart);Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));
        Assert.AreEqual(sale.Id,service.Confirm(request).Id);
    }
    [TestMethod] public void UncertainCommitBlocksMutationAndRecoversAfterRestart()
    {
        var unit=new FaultUnit(work);var service=Service(unit);var request=Request(service);unit.FailAfterCommit=true;unit.BlockAfterFault=true;
        Assert.ThrowsExactly<CheckoutException>(()=>service.Confirm(request));Assert.IsTrue(service.HasPending);Assert.IsTrue(user.CheckoutPending);
        Assert.ThrowsExactly<CheckoutException>(()=>pos.Increment(product.Id));Assert.ThrowsExactly<CheckoutException>(()=>pos.Discard(true));
        Assert.ThrowsExactly<CheckoutException>(()=>user.SignOut());
        var fresh=Service();var result=fresh.Resolve();Assert.IsNotNull(result);Assert.AreEqual(request.OperationId,result.OperationId);
        Assert.IsTrue(fresh.HasPending);fresh.Acknowledge(result.OperationId);Assert.IsFalse(fresh.HasPending);Assert.IsNull(user.Cart);Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));
    }
    [TestMethod] public void MarkerWithoutCommittedSaleResolvesAsAbsent()
    {
        new CheckoutJournal(directory).Save(Guid.NewGuid());var service=Service();
        Assert.ThrowsExactly<CheckoutException>(()=>service.Prepare(new(0)));Assert.IsNull(service.Resolve());Assert.IsFalse(service.HasPending);
    }
    [TestMethod] public void ConflictingRetryPreservesUncertainReceiptAndOriginalCartReference()
    {
        var unit=new FaultUnit(work);var service=Service(unit);var request=Request(service);
        unit.FailAfterCommit=true;unit.BlockAfterFault=true;
        Assert.ThrowsExactly<CheckoutException>(()=>service.Confirm(request));
        var recovered=Service();
        Assert.ThrowsExactly<CheckoutException>(()=>recovered.Confirm(request with {Received=new(3000)}));
        Assert.IsTrue(recovered.HasPending);Assert.IsTrue(user.CheckoutPending);Assert.AreSame(request.Cart,user.Cart);
        var sale=recovered.Resolve();Assert.IsNotNull(sale);Assert.IsNull(user.Cart);
        Assert.AreEqual(2000L,sale.Payment.Received.Cents);recovered.Acknowledge(sale.OperationId);
        Assert.IsFalse(recovered.HasPending);Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));
    }
    [TestMethod] public void JournalContainsOnlyOperationAndRejectsInvalidMarker()
    {
        var journal=new CheckoutJournal(directory);var id=Guid.NewGuid();journal.Save(id);
        Assert.AreEqual(id.ToString("D"),File.ReadAllText(Path.Combine(directory,"checkout.pending")));
        Assert.ThrowsExactly<InvalidOperationException>(()=>journal.Save(Guid.NewGuid()));
        File.WriteAllText(Path.Combine(directory,"checkout.pending"),"invalid");Assert.ThrowsExactly<InvalidDataException>(()=>journal.Read());
    }
    [TestMethod] public void RestartBeforeReceiptAcknowledgementFindsSaleWithoutCart()
    {
        var service=Service();var sale=service.Confirm(Request(service));
        var journal=new CheckoutJournal(directory);var freshUser=LoginAgain(journal);
        var restarted=new CheckoutService(work,freshUser,clock,terminal.Id,journal);
        Assert.IsNull(freshUser.Cart);Assert.IsTrue(restarted.HasPending);
        Assert.ThrowsExactly<CheckoutException>(()=>new PosService(work,freshUser,terminal.Id).Load());
        Assert.ThrowsExactly<CheckoutException>(()=>new CashClosingService(work,freshUser,clock).SubmitCount(cash.Id,new Money(0)));
        Assert.AreEqual(sale.Id,restarted.Resolve()!.Id);restarted.Acknowledge(sale.OperationId);
        Assert.IsTrue(new PosService(work,freshUser,terminal.Id).Load().Cart.IsEmpty);
    }
    [TestMethod] public void ZeroTotalAndInsufficientCashFollowMoneyPolicy()
    {
        var service=Service();pos.Add(product.Id);var cart=user.Cart;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>service.Prepare(new(1899)));Assert.AreSame(cart,user.Cart);
        pos.Discard(true);catalog.EditProduct(product.Id,product.CategoryId,product.Sku,product.Name,"",new(0),true);
        pos.Add(product.Id);var sale=service.Confirm(service.Prepare(new(0)));
        Assert.AreEqual(0L,sale.Total.Cents);Assert.AreEqual(0L,sale.Payment.Change.Cents);
    }
    [TestMethod] public async Task ConcurrentRetryAcrossSessionsPersistsOnce()
    {
        var ja=new MemoryJournal();var jb=new MemoryJournal();var ua=LoginAgain(ja);var ub=LoginAgain(jb);
        new PosService(work,ua,terminal.Id).Add(product.Id);new PosService(work,ub,terminal.Id).Add(product.Id);
        var a=new CheckoutService(work,ua,clock,terminal.Id,ja);var b=new CheckoutService(work,ub,clock,terminal.Id,jb);
        var ra=a.Prepare(new(2000));var rb=b.Prepare(new(2000)) with {OperationId=ra.OperationId};
        var results=await Task.WhenAll(Task.Run(()=>a.Confirm(ra)),Task.Run(()=>b.Confirm(rb)));
        Assert.AreEqual(results[0].Id,results[1].Id);Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='sale.completed'"));
    }
    [TestMethod] public async Task SaleAndCountRaceProducesConsistentExpectedCash()
    {
        var journal=new MemoryJournal();var seller=LoginAgain(journal);new PosService(work,seller,terminal.Id).Add(product.Id);
        var service=new CheckoutService(work,seller,clock,terminal.Id,journal);var request=service.Prepare(new(2000));
        var saleTask=Task.Run(()=>{try{return service.Confirm(request);}catch(CheckoutException){return null;}});
        var countTask=Task.Run(()=>new CashClosingService(work,user,clock).SubmitCount(cash.Id,new Money(50000)));
        await Task.WhenAll(saleTask,countTask);
        var expected=saleTask.Result is null?50000L:51900L;
        Assert.AreEqual(expected,countTask.Result.Expected.Cents);
        Assert.AreEqual(expected,new CashClosingService(work,user,clock).Resume(cash.Id)!.Expected.Cents);
    }
    private sealed class MemoryJournal : ICheckoutJournal
    {
        private Guid? current;
        public Guid? Read()=>current;
        public void Save(Guid operation){if(current is { } old && old!=operation)throw new InvalidOperationException();current=operation;}
        public void Clear(Guid operation){if(current is { } old && old!=operation)throw new InvalidOperationException();current=null;}
    }
    private sealed class FaultUnit(ILocalUnitOfWork inner) : ILocalUnitOfWork
    {
        public bool FailAfterCommit,BlockAfterFault;private bool faulted;
        public T Run<T>(Func<ILocalStore,T> operation)
        {
            if(faulted && BlockAfterFault)throw new IOException("test unavailable");
            var result=inner.Run(operation);
            if(FailAfterCommit && result is Sale){FailAfterCommit=false;faulted=true;throw new IOException("test lost response");}
            return result;
        }
    }
}
