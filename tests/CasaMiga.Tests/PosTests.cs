using System.Reflection;
using System.Runtime.ExceptionServices;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;
[TestClass]
public sealed class PosTests
{
    private string directory=null!,password=null!;
    private Database db=null!;
    private SqliteLocalUnitOfWork work=null!;
    private UserSession user=null!;
    private TerminalIdentity terminal=null!;
    private CatalogService catalog=null!;
    private PosService pos=null!;
    private IdentityService login=null!;
    private CashClosingService closing=null!;
    private CashSession cash=null!;
    private Category sweet=null!,salt=null!;
    private Product concha=null!,bolillo=null!;
    private readonly IClock clock=new SystemClock();
    [TestInitialize]public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"CasaMigaTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        db=new(Path.Combine(directory,"casamiga.db"));db.Initialize();work=new(db);user=new();password=Guid.NewGuid().ToString("N");
        terminal=TerminalConfiguration.LoadOrCreate(directory);work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
        login=new(work,new LocalPasswords(),clock,user);login.Bootstrap("admin","Prueba POS",password);Assert.IsTrue(login.Login("admin",password));
        catalog=new(work,user,clock,terminal.Id);sweet=catalog.CreateCategory("Dulce","",2);salt=catalog.CreateCategory("Salado","",1);
        concha=catalog.CreateProduct(sweet.Id,"CON-VAI","Concha","",new Money(1800));bolillo=catalog.CreateProduct(salt.Id,"BOL","Bolillo","",new Money(500));
        cash=new CashService(work,user,clock).Open(terminal.Id,new Money(0)).Session;closing=new(work,user,clock);pos=new(work,user,terminal.Id);
    }
    [TestCleanup]public void Cleanup()=>Directory.Delete(directory,true);
    private object? Sql(string sql){using var c=db.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    [TestMethod]public void CatalogUsesOnlyActiveProductsAndNonemptyActiveCategories()
    {
        catalog.CreateCategory("Vacía");catalog.SetProductActive(bolillo.Id,false);catalog.SetCategoryActive(salt.Id,false);
        var page=pos.Load();Assert.HasCount(1,page.Categories);Assert.AreEqual(sweet.Id,page.Categories[0].Id);Assert.AreEqual(concha.Id,page.Products.Items.Single().Product.Id);
        Assert.ThrowsExactly<PosException>(()=>pos.Add(bolillo.Id));
    }
    [TestMethod]public void CategoriesUseSortOrderAndProductsHaveStableOrder()
    {
        var first=pos.Load();var second=pos.Load();Assert.AreEqual(salt.Id,first.Categories[0].Id);Assert.AreEqual("Bolillo",first.Products.Items[0].Name);
        CollectionAssert.AreEqual(first.Products.Items.Select(p=>p.Product.Id).ToArray(),second.Products.Items.Select(p=>p.Product.Id).ToArray());
    }
    [TestMethod][DataRow("concha")][DataRow(" con-vai ")]public void SearchMatchesNameAndSku(string search){Assert.AreEqual(concha.Id,pos.Load(search).Products.Items.Single().Product.Id);}
    [TestMethod]public void CategoryFilterIsAppliedInQuery(){Assert.AreEqual(bolillo.Id,pos.Load(category:salt.Id).Products.Items.Single().Product.Id);}
    [TestMethod][DataRow("pos.use")][DataRow("catalog.view")]public void RevokedPermissionRejectsReadsAndMutations(string permission)
    {
        pos.Add(concha.Id);Sql($"DELETE FROM role_permission WHERE permission_name='{permission}'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>pos.Load());Assert.ThrowsExactly<AccessDeniedException>(()=>pos.Increment(concha.Id));pos.Discard(true);Assert.IsNull(user.Cart);
    }
    [TestMethod]public void InactiveUserCannotUsePos(){Sql("UPDATE app_user SET active=0");Assert.ThrowsExactly<AccessDeniedException>(()=>pos.Load());Assert.ThrowsExactly<AccessDeniedException>(()=>pos.Add(concha.Id));}
    [TestMethod]public void ClosedCashRejectsPosInApplication(){closing.SubmitCount(cash.Id,new Money(0));closing.Close(cash.Id);Assert.ThrowsExactly<PosException>(()=>pos.Load());Assert.ThrowsExactly<PosException>(()=>pos.Add(concha.Id));}
    [TestMethod]public void ConfirmedCountFreezesPosEvenWhileCashIsOpen(){closing.SubmitCount(cash.Id,new Money(0));Assert.ThrowsExactly<PosException>(()=>pos.Load());Assert.ThrowsExactly<PosException>(()=>pos.Add(concha.Id));}
    [TestMethod]public void CartCannotPassThroughLogoutOrLoginWithoutExplicitDiscard()
    {
        pos.Add(concha.Id);Assert.ThrowsExactly<CartDiscardRequiredException>(user.SignOut);Assert.ThrowsExactly<CartDiscardRequiredException>(()=>login.Login("admin",password));
        Assert.ThrowsExactly<CartDiscardRequiredException>(()=>pos.Discard(false));Assert.IsNotNull(user.Current);
        pos.Discard(true);user.SignOut();Assert.IsNull(user.Cart);Assert.IsTrue(login.Login("admin",password));Assert.IsTrue(pos.Load().Cart.IsEmpty);
    }
    [TestMethod]public void ClosingAndCountingRequireDiscardInApplication()
    {
        pos.Add(concha.Id);Assert.ThrowsExactly<CartDiscardRequiredException>(()=>closing.Resume(cash.Id));Assert.ThrowsExactly<CartDiscardRequiredException>(()=>closing.SubmitCount(cash.Id,new Money(0)));Assert.ThrowsExactly<CartDiscardRequiredException>(()=>closing.Close(cash.Id));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM cash_count"));pos.Discard(true);closing.SubmitCount(cash.Id,new Money(0));Assert.AreEqual(CashState.Closed,closing.Close(cash.Id).State);
    }
    [TestMethod]public void DifferentCashierStartsEmptyAfterExplicitDiscard()
    {
        var admin=user.Current!.Id;var cashier=Guid.NewGuid();
        using(var c=db.Open())using(var q=c.CreateCommand())
        {
            q.CommandText="INSERT INTO app_user(id,username,display_name,password_hash,role_name,created_utc) SELECT $id,'CASHIER','Otro cajero',password_hash,'Cashier',created_utc FROM app_user WHERE id=$admin";
            q.Parameters.AddWithValue("$id",cashier.ToString("D"));q.Parameters.AddWithValue("$admin",admin.ToString("D"));q.ExecuteNonQuery();
        }
        pos.Add(concha.Id);Assert.ThrowsExactly<CartDiscardRequiredException>(()=>login.Login("cashier",password));
        pos.Discard(true);Assert.IsTrue(login.Login("cashier",password));var page=pos.Load();
        Assert.IsTrue(page.Cart.IsEmpty);Assert.AreEqual(cashier,page.Cart.UserId);Assert.AreEqual(cash.Id,page.Cart.CashSessionId);
        Assert.AreEqual(1800L,pos.Add(concha.Id).Subtotal.Cents);
        Assert.ThrowsExactly<AccessDeniedException>(()=>catalog.EditProduct(concha.Id,sweet.Id,null,"No permitido","",new Money(1),true));
    }
    [TestMethod]public void CatalogEditsAndDeactivationDoNotChangeTemporarySnapshot()
    {
        pos.Add(concha.Id);catalog.EditProduct(concha.Id,sweet.Id,"NEW","Nombre nuevo","",new Money(1900),true);var doubled=pos.Add(concha.Id);
        Assert.AreEqual("Concha",doubled.Lines.Single().ProductName);Assert.AreEqual(3600L,doubled.Subtotal.Cents);
        catalog.SetProductActive(concha.Id,false);Assert.IsFalse(pos.Load().Products.Items.Any(p=>p.Product.Id==concha.Id));
        Assert.AreEqual(3600L,user.Cart!.Subtotal.Cents);Assert.ThrowsExactly<PosException>(()=>pos.Add(concha.Id));Assert.AreEqual(5400L,pos.Increment(concha.Id).Subtotal.Cents);
    }
    [TestMethod]public void MutationsAndOverflowDoNotWriteBusinessRows()
    {
        var audits=Sql("SELECT count(*) FROM audit_entry");var high=catalog.CreateProduct(sweet.Id,null,"Límite","",new Money(long.MaxValue));audits=Sql("SELECT count(*) FROM audit_entry");
        pos.Add(high.Id);Assert.ThrowsExactly<OverflowException>(()=>pos.Add(concha.Id));Assert.HasCount(1,user.Cart!.Lines);Assert.AreEqual(audits,Sql("SELECT count(*) FROM audit_entry"));
        pos.Discard(true);pos.Add(concha.Id);pos.Increment(concha.Id);pos.Decrement(concha.Id);pos.Remove(concha.Id);Assert.AreEqual(audits,Sql("SELECT count(*) FROM audit_entry"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('cart','cart_line')"));
        Assert.AreEqual(0L,Sql("SELECT (SELECT count(*) FROM sale)+(SELECT count(*) FROM sale_item)+(SELECT count(*) FROM payment)"));
    }
    [TestMethod]public void RestartAndBackupPreserveCashAndCatalogButNeverCart()
    {
        pos.Add(concha.Id);var archive=new InstallationBackup(directory).Create();var target=Path.Combine(directory,"restored");InstallationBackup.Restore(archive,target);
        var restored=new SqliteLocalUnitOfWork(new Database(Path.Combine(target,"casamiga.db")));var nextUser=new UserSession();Assert.IsTrue(new IdentityService(restored,new LocalPasswords(),clock,nextUser).Login("admin",password));
        var page=new PosService(restored,nextUser,terminal.Id).Load();Assert.IsTrue(page.Cart.IsEmpty);Assert.AreEqual(cash.Id,page.Cart.CashSessionId);Assert.AreEqual(2,page.Products.Total);
    }
    [TestMethod]public void NewCashAndOtherTerminalDoNotInheritCart()
    {
        pos.Add(concha.Id);var other=new TerminalIdentity(Guid.NewGuid(),"Otra");work.Run(s=>{s.EnsureTerminal(other,clock.UtcNow);return true;});new CashService(work,user,clock).Open(other.Id,new Money(0));
        Assert.ThrowsExactly<CartDiscardRequiredException>(()=>new PosService(work,user,other.Id).Load());
        pos.Discard(true);closing.SubmitCount(cash.Id,new Money(0));closing.Close(cash.Id);var next=new CashService(work,user,clock).Open(terminal.Id,new Money(0));
        Assert.AreEqual(next.Session.Id,pos.Load().Cart.CashSessionId);Assert.IsTrue(user.Cart!.IsEmpty);
    }
    [TestMethod]public void PosPermissionAssignedToAllThreeRolesButNotCatalogEditingToCashier()
    {
        Assert.AreEqual(3L,Sql("SELECT count(*) FROM role_permission WHERE permission_name='pos.use'"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM role_permission WHERE role_name='Cashier' AND permission_name='products.edit'"));
    }
    [TestMethod]public void LargeCatalogUsesBoundedSetQueriesWithoutPerButtonLookup()
    {
        using(var c=db.Open())using(var tx=c.BeginTransaction())using(var q=c.CreateCommand())
        {
            q.Transaction=tx;q.CommandText="INSERT INTO product(id,category_id,name,price_cents,active,track_inventory,created_utc,modified_utc) VALUES($id,$cat,$name,1800,1,1,$at,$at)";
            q.Parameters.AddWithValue("$id","");q.Parameters.AddWithValue("$cat",sweet.Id.ToString("D"));q.Parameters.AddWithValue("$name","");q.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));
            for(int i=0;i<2000;i++){q.Parameters["$id"].Value=Guid.NewGuid().ToString("D");q.Parameters["$name"].Value=$"Producto {i:0000}";q.ExecuteNonQuery();}tx.Commit();
        }
        var probe=new ProbeUnit(work);var service=new PosService(probe,user,terminal.Id);var first=service.Load();Assert.AreEqual(2002,first.Products.Total);Assert.HasCount(50,first.Products.Items);
        Assert.AreEqual(1,probe.Calls[nameof(ILocalStore.ListPosCategories)]);Assert.AreEqual(1,probe.Calls[nameof(ILocalStore.QueryProducts)]);Assert.IsFalse(probe.Calls.ContainsKey(nameof(ILocalStore.FindProduct)));
        var next=service.Load(page:1);Assert.IsFalse(first.Products.Items.Select(p=>p.Product.Id).Intersect(next.Products.Items.Select(p=>p.Product.Id)).Any());
    }
    private sealed class ProbeUnit(ILocalUnitOfWork inner):ILocalUnitOfWork
    {
        public Dictionary<string,int> Calls {get;}=[];
        public T Run<T>(Func<ILocalStore,T> operation)=>inner.Run(s=>
        {
            var proxy=DispatchProxy.Create<ILocalStore,StoreProbe>();((StoreProbe)(object)proxy).Inner=s;((StoreProbe)(object)proxy).Calls=Calls;return operation(proxy);
        });
    }
    public class StoreProbe:DispatchProxy
    {
        public ILocalStore Inner {get;set;}=null!;
        public Dictionary<string,int> Calls {get;set;}=null!;
        protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)
        {
            var method=targetMethod!;Calls[method.Name]=Calls.GetValueOrDefault(method.Name)+1;
            try{return method.Invoke(Inner,args);}catch(TargetInvocationException e){ExceptionDispatchInfo.Capture(e.InnerException!).Throw();throw;}
        }
    }
}
