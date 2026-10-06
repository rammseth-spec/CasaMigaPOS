using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

public abstract class SalesFixture
{
    protected string directory = null!;
    protected Database db = null!;
    protected SqliteLocalUnitOfWork work = null!;
    protected UserSession user = null!;
    protected TerminalIdentity terminal = null!;
    protected CashSession cash = null!;
    protected PosService pos = null!;
    protected CatalogService catalog = null!;
    protected Product product = null!;
    protected string password = null!;
    protected readonly IClock clock = new SystemClock();
    protected virtual int? FixtureSchemaVersion => null;
    [TestInitialize] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"CasaMigaTests",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        db=new(Path.Combine(directory,"casamiga.db")); if(FixtureSchemaVersion is { } v)SaleContractMigrationTests.InitializeThrough(db,v);else db.Initialize();work=new(db);user=new(new CheckoutJournal(directory));
        terminal=TerminalConfiguration.LoadOrCreate(directory);work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
        var identity=new IdentityService(work,new LocalPasswords(),clock,user);password="Aa1!"+Guid.NewGuid().ToString("N")[..12];
        identity.Bootstrap("admin","Prueba ventas",password);Assert.IsTrue(identity.Login("admin",password));
        catalog=new(work,user,clock,terminal.Id);var category=catalog.CreateCategory("Pan");
        product=catalog.CreateProduct(category.Id,"P1","Concha","",new Money(1900));
        cash=new CashService(work,user,clock).Open(terminal.Id,new Money(50000)).Session;
        pos=new(work,user,terminal.Id);
    }
    [TestCleanup] public void Cleanup()=>Directory.Delete(directory,true);
    protected object? Sql(string sql){using var c=db.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    protected Sale Candidate(ILocalStore s, Guid operation) => new(Guid.NewGuid(),operation,user.Current!.Id,terminal.Id,cash.Id,
        s.NextSaleSequence(terminal.Id),clock.UtcNow,[new(product.Id,product.Name,product.SalePrice,2)],Guid.NewGuid(),new(5000));
    protected Sale Persist() => work.Run(s=>{var sale=Candidate(s,Guid.NewGuid());s.AddSale(sale);return sale;});
    protected UserSession LoginAgain(ICheckoutJournal journal)
    {
        var fresh=new UserSession(journal);Assert.IsTrue(new IdentityService(work,new LocalPasswords(),clock,fresh).Login("admin",password));return fresh;
    }
}

[TestClass]
public sealed class SalePersistenceTests : SalesFixture
{
    [TestMethod] public void UpgradeFromFivePreservesExistingCashAndMigrationHashes()
    {
        var old=new Database(Path.Combine(directory,"version-five.db"));var assembly=typeof(Database).Assembly;
        using(var c=old.Open())
        {
            using var q=c.CreateCommand();q.CommandText="CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT";q.ExecuteNonQuery();
            foreach(var resource in assembly.GetManifestResourceNames().Where(n=>n.EndsWith(".sql",StringComparison.Ordinal)).Order().Take(5))
            {
                using var reader=new StreamReader(assembly.GetManifestResourceStream(resource)!);var script=reader.ReadToEnd();
                q.Parameters.Clear();q.CommandText=script;q.ExecuteNonQuery();
                q.CommandText="INSERT INTO schema_migration VALUES((SELECT count(*)+1 FROM schema_migration),$hash,$at)";
                q.Parameters.AddWithValue("$hash",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(script))));
                q.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));q.ExecuteNonQuery();
            }
        }
        var previous=new SqliteLocalUnitOfWork(old);var actor=new UserSession();var login=new IdentityService(previous,new LocalPasswords(),clock,actor);
        login.Bootstrap("upgrade","Upgrade",password);Assert.IsTrue(login.Login("upgrade",password));
        previous.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
        var opened=new CashService(previous,actor,clock).Open(terminal.Id,new Money(125)).Session;
        old.Initialize();old.Initialize();old.Validate();
        Assert.AreEqual(125L,previous.Run(s=>s.FindSession(opened.Id))!.OpeningFund.Cents);
        Assert.AreEqual(0L,previous.Run(s=>s.CashSalesTotal(opened.Id)).Cents);
        Assert.IsTrue(previous.Run(s=>s.HasPermission(actor.Current!.Id,"sales.complete")));
    }
    [TestMethod] public void CompleteAggregateRoundTripsAndSumsAppliedCash()
    {
        var sale=Persist();var loaded=work.Run(s=>s.FindSaleByOperation(sale.OperationId))!;
        Assert.AreEqual(sale.Folio,loaded.Folio);Assert.AreEqual(3800L,loaded.Total.Cents);
        Assert.AreEqual(1200L,loaded.Payment.Change.Cents);Assert.AreEqual(3800L,work.Run(s=>s.CashSalesTotal(cash.Id)).Cents);
    }
    [TestMethod] public void HeaderWithoutChildrenIsRejected()
    {
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"INSERT INTO sale VALUES('{Guid.NewGuid()}','{Guid.NewGuid()}','{user.Current!.Id}','{terminal.Id}','{cash.Id}',1,'{clock.UtcNow:O}',0,0)"));
    }
    [TestMethod] public void OrphanPaymentCannotCommit()
    {
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"INSERT INTO payment(id,sale_id,method,applied_cents,received_cents,change_cents) VALUES('{Guid.NewGuid()}','{Guid.NewGuid()}','CASH',0,0,0)"));
    }
    [TestMethod] public void ConfirmedRowsCannotBeEditedDeletedOrAppended()
    {
        var sale=Persist();
        foreach(var table in new[]{"sale","sale_item","payment"})
        {
            Assert.ThrowsExactly<SqliteException>(()=>Sql($"DELETE FROM {table}"));
            var column=table=="sale"?"total_cents":table=="sale_item"?"quantity":"received_cents";
            Assert.ThrowsExactly<SqliteException>(()=>Sql($"UPDATE {table} SET {column}={column}"));
        }
        Assert.ThrowsExactly<SqliteException>(()=>Sql($"INSERT INTO sale_item VALUES('{sale.Id}',2,'{product.Id}','Otra',0,1,0)"));
    }
    [TestMethod] public void FailureRollsBackRowsAndSequence()
    {
        Assert.ThrowsExactly<InvalidOperationException>(()=>work.Run<int>(s=>{s.AddSale(Candidate(s,Guid.NewGuid()));throw new InvalidOperationException();}));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM sale"));Assert.AreEqual(0L,Sql("SELECT count(*) FROM payment"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM sale_item"));Assert.AreEqual(1L,Persist().Sequence);
    }
    [TestMethod] public void CountConfirmedRejectsPersistingSale()
    {
        new CashClosingService(work,user,clock).SubmitCount(cash.Id,new Money(50000));
        Assert.ThrowsExactly<SqliteException>(()=>Persist());Assert.AreEqual(0L,Sql("SELECT count(*) FROM sale"));
    }
}
