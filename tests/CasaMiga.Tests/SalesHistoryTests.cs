using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class SalesHistoryTests : SalesFixture
{
    private SalesHistoryService History(UserSession? actor = null, TimeZoneInfo? zone = null, ILocalReadUnitOfWork? reader = null)
        => new(reader ?? work, actor ?? user, zone ?? TimeZoneInfo.Utc);

    private Sale PersistAt(DateTimeOffset at, string? name = null, long? price = null)
        => work.Run(store =>
        {
            var sale = new Sale(Guid.NewGuid(), Guid.NewGuid(), user.Current!.Id, terminal.Id, cash.Id,
                store.NextSaleSequence(terminal.Id), at,
                [new SaleItem(product.Id, name ?? product.Name, new Money(price ?? product.SalePrice.Cents), 1)],
                Guid.NewGuid(), new Money(price ?? product.SalePrice.Cents));
            store.AddSale(sale); return sale;
        });

    private UserSession LoginRole(string role, bool active = true)
    {
        var id=Guid.NewGuid();var username=(role+Guid.NewGuid().ToString("N")).ToUpperInvariant();
        using(var connection=db.Open())using(var command=connection.CreateCommand())
        {
            command.CommandText="INSERT INTO app_user(id,username,display_name,password_hash,role_name,active,created_utc) SELECT $id,$name,$display,password_hash,$role,$active,$at FROM app_user WHERE id=$source";
            command.Parameters.AddWithValue("$id",id.ToString("D"));command.Parameters.AddWithValue("$name",username);command.Parameters.AddWithValue("$display",role+" prueba");
            command.Parameters.AddWithValue("$role",role);command.Parameters.AddWithValue("$active",active?1:0);command.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$source",user.Current!.Id.ToString("D"));Assert.AreEqual(1,command.ExecuteNonQuery());
        }
        var session=new UserSession();Assert.AreEqual(active,new IdentityService(work,new LocalPasswords(),clock,session).Login(username,password));return session;
    }

    [TestMethod] public void PermissionIsExplicitAndRevalidated()
    {
        Assert.IsTrue(History().CanView());
        Assert.IsTrue(History(LoginRole("Manager")).CanView());
        var cashier=LoginRole("Cashier");Assert.IsFalse(History(cashier).CanView());
        Assert.ThrowsExactly<AccessDeniedException>(()=>History(cashier).Search(new()));
        Assert.IsFalse(History(LoginRole("Cashier",false)).CanView());
        var deactivated=LoginRole("Manager");
        Sql($"UPDATE app_user SET active=0 WHERE id='{deactivated.Current!.Id:D}'");
        Assert.IsFalse(History(deactivated).CanView());
        Assert.ThrowsExactly<AccessDeniedException>(()=>History(deactivated).Search(new()));
        Sql("DELETE FROM role_permission WHERE role_name='Administrator' AND permission_name='sales.view'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>History().Search(new()));
    }

    [TestMethod] public void ViewingDoesNotRequireOpenCashOrSalesComplete()
    {
        new CashClosingService(work,user,clock).SubmitCount(cash.Id,new Money(50000));
        new CashClosingService(work,user,clock).Close(cash.Id);
        Sql("DELETE FROM role_permission WHERE role_name='Administrator' AND permission_name='sales.complete'");
        Assert.AreEqual(0,History().Search(new()).Total);
    }

    [TestMethod] public void EmptyAndLargeHistoryArePagedWithStableNewestFirstOrder()
    {
        Assert.AreEqual(0,History().Search(new()).Total);
        var at=new DateTimeOffset(2027,1,10,12,0,0,TimeSpan.Zero);var created=new List<Sale>();
        for(int i=0;i<65;i++)created.Add(PersistAt(at.AddSeconds(i)));
        var first=History().Search(new());var second=History().Search(new(Page:1));
        Assert.AreEqual(65,first.Total);Assert.HasCount(50,first.Items);Assert.HasCount(15,second.Items);
        Assert.AreEqual(created[^1].Id,first.Items[0].SaleId);
        Assert.IsFalse(first.Items.Select(x=>x.SaleId).Intersect(second.Items.Select(x=>x.SaleId)).Any());
        Assert.IsTrue(first.Items.Zip(first.Items.Skip(1)).All(pair=>pair.First.ConfirmedAt>=pair.Second.ConfirmedAt));
    }

    [TestMethod] public void ThousandsOfSalesRemainBoundedAndUseOneStorePageCall()
    {
        var at=new DateTimeOffset(2027,2,1,0,0,0,TimeSpan.Zero);
        work.Run(store=>{for(int i=0;i<2000;i++)
        {
            var sale=new Sale(Guid.NewGuid(),Guid.NewGuid(),user.Current!.Id,terminal.Id,cash.Id,store.NextSaleSequence(terminal.Id),at.AddSeconds(i),
                [new SaleItem(product.Id,product.Name,product.SalePrice,1)],Guid.NewGuid(),product.SalePrice);
            store.AddSale(sale);
        }return true;});
        var probe=new ProbeReadUnit(work);var page=History(reader:probe).Search(new(Page:20));
        Assert.AreEqual(2000,page.Total);Assert.HasCount(50,page.Items);
        Assert.AreEqual(1,probe.Calls.GetValueOrDefault(nameof(ILocalStore.QuerySales)));
        Assert.IsFalse(probe.Calls.ContainsKey(nameof(ILocalStore.FindSaleDetail)));
    }

    [TestMethod] public void ExactFolioSearchValidatesAndFindsOnlyTheSale()
    {
        var sale=PersistAt(clock.UtcNow);PersistAt(clock.UtcNow.AddSeconds(1));
        var found=History().Search(new(SalesHistoryMode.Folio,sale.Folio));
        Assert.AreEqual(1,found.Total);Assert.AreEqual(sale.Id,found.Items.Single().SaleId);
        var unknown=$"{terminal.Id:N}-{99999999L:D8}";
        Assert.AreEqual(0,History().Search(new(SalesHistoryMode.Folio,unknown)).Total);
        Assert.ThrowsExactly<SalesHistoryException>(()=>History().Search(new(SalesHistoryMode.Folio,"invalido")));
        Assert.ThrowsExactly<SalesHistoryException>(()=>History().Search(new(SalesHistoryMode.Folio,sale.Folio,new DateOnly(2026,1,1))));
    }

    [TestMethod] public void LocalDateRangeUsesInclusiveStartAndExclusiveNextDay()
    {
        var zone=TimeZoneInfo.CreateCustomTimeZone("HistoryTest",TimeSpan.FromHours(-6),"HistoryTest","HistoryTest");
        var before=PersistAt(new DateTimeOffset(2027,3,10,5,59,59,TimeSpan.Zero));
        var start=PersistAt(new DateTimeOffset(2027,3,10,6,0,0,TimeSpan.Zero));
        var end=PersistAt(new DateTimeOffset(2027,3,11,5,59,59,TimeSpan.Zero));
        var after=PersistAt(new DateTimeOffset(2027,3,11,6,0,0,TimeSpan.Zero));
        var page=History(zone:zone).Search(new(SalesHistoryMode.DateRange,"",new(2027,3,10),new(2027,3,10)));
        CollectionAssert.AreEquivalent(new[]{start.Id,end.Id},page.Items.Select(x=>x.SaleId).ToArray());
        Assert.IsFalse(page.Items.Any(x=>x.SaleId==before.Id||x.SaleId==after.Id));
        Assert.ThrowsExactly<SalesHistoryException>(()=>History(zone:zone).Search(new(SalesHistoryMode.DateRange,"",new(2027,3,11),new(2027,3,10))));
    }

    [TestMethod] public void DetailKeepsSnapshotsAndExactFinancialContext()
    {
        var sale=PersistAt(clock.UtcNow,"Concha original",1900);
        catalog.EditProduct(product.Id,product.CategoryId,product.Sku,"Concha nueva","",new Money(2500),product.TrackInventory);
        var detail=History().Detail(sale.Id)!;
        Assert.AreEqual("Concha original",detail.Sale.Items.Single().ProductName);
        Assert.AreEqual(1900L,detail.Sale.Items.Single().UnitPrice.Cents);Assert.AreEqual(1900L,detail.Sale.Total.Cents);
        Assert.AreEqual("CASH",detail.Sale.Payment.Method);Assert.AreEqual(1900L,detail.Sale.Payment.Applied.Cents);
        Assert.AreEqual(user.Current!.Id,detail.Sale.UserId);Assert.AreEqual(terminal.Id,detail.Sale.TerminalId);Assert.AreEqual(cash.Id,detail.Sale.CashSessionId);
        Assert.AreEqual(user.Current.DisplayName,detail.CashierName);Assert.AreEqual(terminal.Name,detail.TerminalName);
        Assert.IsNull(History().Detail(Guid.NewGuid()));
    }

    [TestMethod] public void ReadsDoNotMutateCommercialOrAuditData()
    {
        var sale=PersistAt(clock.UtcNow);var sales=Convert.ToInt64(Sql("SELECT count(*) FROM sale"));var audits=Convert.ToInt64(Sql("SELECT count(*) FROM audit_entry"));
        _=History().Search(new());_=History().Detail(sale.Id);
        Assert.AreEqual(sales,Sql("SELECT count(*) FROM sale"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM payment"));
        Assert.AreEqual(audits,Sql("SELECT count(*) FROM audit_entry"));Assert.AreEqual("OPEN",Sql("SELECT state FROM cash_session WHERE id='"+cash.Id+"'"));
    }

    [TestMethod] public void DeferredReadSnapshotDoesNotReserveWriterLockAgainstCheckout()
    {
        var initial=PersistAt(clock.UtcNow);var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();
        var readTask=Task.Run(()=>work.Read(store=>{var page=store.QuerySales(new(null,null,null,null,0));entered.Set();release.Wait(TimeSpan.FromSeconds(10));return page;}));
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(3)));
        pos.Add(product.Id);var journal=new CheckoutJournal(directory);var checkout=new CheckoutService(work,user,clock,terminal.Id,journal);var request=checkout.Prepare(new Money(2000));
        try
        {
            var writer=Task.Run(()=>checkout.Confirm(request));
            Assert.IsTrue(writer.Wait(TimeSpan.FromSeconds(3)),"La lectura deferred reservó indebidamente el writer lock.");
            checkout.Acknowledge(writer.Result.OperationId);
        }
        finally { release.Set(); }
        Assert.AreEqual(1,readTask.Result.Total);Assert.AreEqual(initial.Id,readTask.Result.Items.Single().SaleId);
        Assert.AreEqual(2,History().Search(new()).Total);
    }

    [TestMethod] public void BackupRestorePreservesQueryableHistory()
    {
        var sale=PersistAt(clock.UtcNow);var package=new InstallationBackup(directory).Create();var destination=Path.Combine(directory,"history-restored");
        InstallationBackup.Restore(package,destination);var restoredDb=new Database(Path.Combine(destination,"casamiga.db"));var restoredWork=new SqliteLocalUnitOfWork(restoredDb);
        var restoredUser=new UserSession();Assert.IsTrue(new IdentityService(restoredWork,new LocalPasswords(),clock,restoredUser).Login("admin",password));
        var detail=new SalesHistoryService(restoredWork,restoredUser,TimeZoneInfo.Utc).Detail(sale.Id);
        Assert.IsNotNull(detail);Assert.AreEqual(sale.Folio,detail.Sale.Folio);Assert.AreEqual(1900L,detail.Sale.Total.Cents);
    }

    [TestMethod] public void UpgradeFromSixPreservesSaleAndAddsOnlyHistoryPermissionAndIndex()
    {
        var oldDirectory=Path.Combine(directory,"v6");Directory.CreateDirectory(oldDirectory);var oldDb=new Database(Path.Combine(oldDirectory,"casamiga.db"));
        ApplyFirstMigrations(oldDb,6);var oldWork=new SqliteLocalUnitOfWork(oldDb);var oldTerminal=TerminalConfiguration.LoadOrCreate(oldDirectory);oldWork.Run(s=>{s.EnsureTerminal(oldTerminal,clock.UtcNow);return true;});
        var oldUser=new UserSession();var identity=new IdentityService(oldWork,new LocalPasswords(),clock,oldUser);identity.Bootstrap("oldadmin","Anterior",password);Assert.IsTrue(identity.Login("oldadmin",password));
        var oldCatalog=new CatalogService(oldWork,oldUser,clock,oldTerminal.Id);var category=oldCatalog.CreateCategory("Pan");var oldProduct=oldCatalog.CreateProduct(category.Id,null,"Bolillo","",new Money(500));
        var oldCash=new CashService(oldWork,oldUser,clock).Open(oldTerminal.Id,new Money(0)).Session;
        var oldSale=oldWork.Run(s=>{var x=new Sale(Guid.NewGuid(),Guid.NewGuid(),oldUser.Current!.Id,oldTerminal.Id,oldCash.Id,s.NextSaleSequence(oldTerminal.Id),clock.UtcNow,[new SaleItem(oldProduct.Id,oldProduct.Name,oldProduct.SalePrice,2)],Guid.NewGuid(),new Money(1000));s.AddSale(x);return x;});
        oldDb.Initialize();oldDb.Validate();
        Assert.AreEqual(oldSale.Id,new SalesHistoryService(oldWork,oldUser,TimeZoneInfo.Utc).Detail(oldSale.Id)!.Sale.Id);
        using var connection=oldDb.Open();using var command=connection.CreateCommand();
        command.CommandText="SELECT count(*) FROM schema_migration";Assert.AreEqual(15L,command.ExecuteScalar());
        command.CommandText="SELECT count(*) FROM sqlite_master WHERE type='index' AND name='ix_sale_history'";Assert.AreEqual(1L,command.ExecuteScalar());
        command.CommandText="SELECT count(*) FROM role_permission WHERE permission_name='sales.view'";Assert.AreEqual(2L,command.ExecuteScalar());
    }

    [TestMethod]
    [DataRow("001_foundation.sql","67FE636BC15056DB439D15A0CDD73FFD84DA98F5F383FAEF21910F53E191CAD7")]
    [DataRow("002_identity_cash.sql","B8C29E34BFFFDC6EE313756EF4869644A495070C4B096BCDEDAD6C8A8BF7C9E5")]
    [DataRow("003_cash_closing.sql","4A5FD419D95C19CBE7669FCB0C0C825567FA4DDD97478BD9946DDB1E9E13E7E1")]
    [DataRow("004_catalog.sql","7E7F81303AB6FD9DB3E96A3F878AD4FE4B4B94B79FF4C893AF9F9527DAEA1968")]
    [DataRow("005_pos_permission.sql","A335A25AA1F2CC94CED3670FDD4C5E2FCC9AA344141EEA74B06287F357CA167B")]
    [DataRow("006_cash_sales.sql","7EA9A2981B02C7D2A93CC550E5E18D0FA60C63A79C9D1052458E0769DFAE2E0C")]
    public void HistoricalMigrationBytesRemainExact(string file,string expected)
    {
        var assembly=typeof(Database).Assembly;var resource=assembly.GetManifestResourceNames().Single(n=>n.EndsWith(file,StringComparison.Ordinal));
        using var stream=assembly.GetManifestResourceStream(resource)!;using var memory=new MemoryStream();stream.CopyTo(memory);
        Assert.AreEqual(expected,Convert.ToHexString(SHA256.HashData(memory.ToArray())));
    }

    private void ApplyFirstMigrations(Database target,int count)
    {
        var assembly=typeof(Database).Assembly;using var connection=target.Open();using var command=connection.CreateCommand();
        command.CommandText="CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT";command.ExecuteNonQuery();
        foreach(var (resource,index) in assembly.GetManifestResourceNames().Where(n=>n.EndsWith(".sql",StringComparison.Ordinal)).Order().Take(count).Select((name,i)=>(name,i+1)))
        {
            using var reader=new StreamReader(assembly.GetManifestResourceStream(resource)!);var sql=reader.ReadToEnd();command.Parameters.Clear();command.CommandText=sql;command.ExecuteNonQuery();
            command.CommandText="INSERT INTO schema_migration VALUES($version,$hash,$at)";command.Parameters.AddWithValue("$version",index);
            command.Parameters.AddWithValue("$hash",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));command.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O",CultureInfo.InvariantCulture));command.ExecuteNonQuery();
        }
    }

    private sealed class ProbeReadUnit(ILocalReadUnitOfWork inner) : ILocalReadUnitOfWork
    {
        public Dictionary<string,int> Calls {get;}=[];
        public T Read<T>(Func<ILocalStore,T> operation)=>inner.Read(store=>
        {
            var proxy=DispatchProxy.Create<ILocalStore,StoreProbe>();var probe=(StoreProbe)(object)proxy;probe.Inner=store;probe.Calls=Calls;return operation(proxy);
        });
    }

    public class StoreProbe : DispatchProxy
    {
        public ILocalStore Inner {get;set;}=null!;public Dictionary<string,int> Calls {get;set;}=null!;
        protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)
        {
            var method=targetMethod!;Calls[method.Name]=Calls.GetValueOrDefault(method.Name)+1;
            try{return method.Invoke(Inner,args);}catch(TargetInvocationException error){ExceptionDispatchInfo.Capture(error.InnerException!).Throw();throw;}
        }
    }
}
