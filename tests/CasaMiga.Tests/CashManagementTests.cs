using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class CashManagementTests : SalesFixture
{
    private CashClosingService Closing => new(work,user,clock);
    private CashHistoryService History => new(work,user,terminal.Id);
    private void AddAppliedCash() => work.Run(store=>
    {
        var sale=new Sale(Guid.NewGuid(),Guid.NewGuid(),user.Current!.Id,terminal.Id,cash.Id,store.NextSaleSequence(terminal.Id),clock.UtcNow,
            [new(product.Id,product.Name,new Money(125000),1)],Guid.NewGuid(),new Money(150000));
        store.AddSale(sale);return true;
    });

    [TestMethod,DataRow(175000L,0L),DataRow(178000L,3000L),DataRow(170000L,-5000L)]
    public void FinancialCasesPersistNoteAndAppliedCashOnly(long counted,long difference)
    {
        AddAppliedCash();Assert.IsNull(Closing.Resume(cash.Id));
        var result=Closing.SubmitCount(cash.Id,new(counted));Assert.AreEqual(175000L,result.Expected.Cents);Assert.AreEqual(difference,result.Difference.Cents);
        var closed=Closing.Close(cash.Id,difference==0?null:"  Conteo sintético  ");
        var row=History.Detail(closed.Id)!;
        Assert.AreEqual(175000L,row.Expected!.Value.Cents);Assert.AreEqual(counted,row.Counted!.Value.Cents);Assert.AreEqual(difference,row.Difference!.Value.Cents);
        Assert.AreEqual(difference==0?null:"Conteo sintético",row.DiscrepancyNote);
        Assert.AreEqual(125000L,(row.Expected.Value-row.OpeningFund).Cents);
        Assert.AreEqual(row.DiscrepancyNote,work.Read(s=>s.FindSession(cash.Id))!.DiscrepancyNote);
        Assert.ThrowsExactly<SqliteException>(()=>Sql("UPDATE cash_session SET discrepancy_note='alterada' WHERE state='CLOSED'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("DELETE FROM cash_session WHERE state='CLOSED'"));
        Assert.ThrowsExactly<CashClosingException>(()=>Closing.Close(cash.Id,"segundo intento"));
    }

    [TestMethod,DataRow(null),DataRow(""),DataRow(" \t\r\n ")]
    public void DiscrepancyRejectsMissingOrWhitespaceNoteAtomically(string? note)
    {
        Closing.SubmitCount(cash.Id,new Money(49900));
        Assert.ThrowsExactly<ArgumentException>(()=>Closing.Close(cash.Id,note));
        Assert.AreEqual(CashState.Open,work.Read(s=>s.FindSession(cash.Id))!.State);
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM audit_entry WHERE action='cash.close'"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM cash_count"));
    }
    [TestMethod] public void NoteLimitAndWhitespaceNormalization()
    {
        Closing.SubmitCount(cash.Id,new Money(50000));
        Assert.ThrowsExactly<ArgumentException>(()=>Closing.Close(cash.Id,new string('x',501)));
        Assert.IsNull(Closing.Close(cash.Id," \t ").DiscrepancyNote);
        var next=new CashService(work,user,clock).Open(terminal.Id,new Money(0));
        Closing.SubmitCount(next.Session.Id,new Money(1));
        Assert.AreEqual(500,Closing.Close(next.Session.Id," "+new string('x',500)+" ").DiscrepancyNote!.Length);
    }
    [TestMethod] public void FailedAuditRollsBackNoteAndAllCloseFields()
    {
        Closing.SubmitCount(cash.Id,new Money(49900));
        Sql("CREATE TRIGGER reject_close BEFORE INSERT ON audit_entry WHEN NEW.action='cash.close' BEGIN SELECT RAISE(ABORT,'fault'); END");
        Assert.ThrowsExactly<SqliteException>(()=>Closing.Close(cash.Id,"Diferencia verificada"));
        var session=work.Read(s=>s.FindSession(cash.Id))!;
        Assert.AreEqual(CashState.Open,session.State);Assert.IsNull(session.DiscrepancyNote);Assert.IsNull(session.ClosedAt);Assert.IsNull(session.Expected);Assert.IsNull(session.Counted);Assert.IsNull(session.ClosedBy);
        Sql("DROP TRIGGER reject_close");Assert.AreEqual("Diferencia verificada",Closing.Close(cash.Id,"Diferencia verificada").DiscrepancyNote);
    }
    [TestMethod] public async Task IndependentIdentitiesRaceToCloseWithExactlyOneNoteAndAudit()
    {
        Closing.SubmitCount(cash.Id,new Money(49900));
        var other=new UserSession();Assert.IsTrue(new IdentityService(work,new LocalPasswords(),clock,other).Login("admin",password));
        using var gate=new ManualResetEventSlim(false);
        Task<bool> Attempt(UserSession session,string note)=>Task.Run(()=>{gate.Wait();try{new CashClosingService(new SqliteLocalUnitOfWork(db),session,clock).Close(cash.Id,note);return true;}catch(CashClosingException){return false;}});
        var a=Attempt(user,"Primero");var b=Attempt(other,"Segundo");gate.Set();var results=await Task.WhenAll(a,b);
        Assert.AreEqual(1,results.Count(x=>x));Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='cash.close'"));
        var stored=work.Read(s=>s.FindSession(cash.Id))!;
        Assert.AreEqual(results[0]?"Primero":"Segundo",stored.DiscrepancyNote);
    }
    [TestMethod] public void HistoryIsPagedTerminalScopedReadOnlyAndUsesCashView()
    {
        Assert.AreEqual(0,History.Search(new()).Total);Assert.IsNull(History.Detail(cash.Id));
        Closing.SubmitCount(cash.Id,new Money(50000));Closing.Close(cash.Id);
        for(var i=0;i<27;i++){var next=new CashService(work,user,clock).Open(terminal.Id,new Money(0)).Session;Closing.SubmitCount(next.Id,new Money(0));Closing.Close(next.Id);}
        var first=History.Search(new());var second=History.Search(new(1));Assert.AreEqual(28,first.Total);Assert.AreEqual(25,first.Items.Count);Assert.AreEqual(3,second.Items.Count);Assert.AreEqual(2,first.TotalPages);
        var all=first.Items.Concat(second.Items).ToArray();Assert.AreEqual(28,all.Select(x=>x.Id).Distinct().Count());Assert.IsTrue(all.Zip(all.Skip(1)).All(x=>x.First.ClosedAt>=x.Second.ClosedAt));
        Assert.IsTrue(all.All(x=>x.TerminalName==terminal.Name&&x.OpenedByName=="Prueba ventas"&&x.ClosedByName=="Prueba ventas"&&x.CountedByName=="Prueba ventas"));
        Assert.AreEqual(0,History.Search(new(2)).Items.Count);
        var otherTerminal=new TerminalIdentity(Guid.NewGuid(),"Otra caja");work.Run(s=>{s.EnsureTerminal(otherTerminal,clock.UtcNow);return true;});
        var otherHistory=new CashHistoryService(work,user,otherTerminal.Id);Assert.AreEqual(0,otherHistory.Search(new()).Total);Assert.IsNull(otherHistory.Detail(cash.Id));
        var audits=Sql("SELECT count(*) FROM audit_entry");History.Search(new());History.Detail(cash.Id);Assert.AreEqual(audits,Sql("SELECT count(*) FROM audit_entry"));
        Sql("UPDATE app_user SET role_name='Cashier'");Assert.AreEqual(28,History.Search(new()).Total);
        Assert.ThrowsExactly<AccessDeniedException>(()=>Closing.Resume(new CashService(work,user,clock).Open(terminal.Id,new Money(0)).Session.Id));
        Sql("DELETE FROM role_permission WHERE role_name='Cashier' AND permission_name='cash.view'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>History.Search(new()));Assert.ThrowsExactly<AccessDeniedException>(()=>History.Detail(cash.Id));
    }
    [TestMethod] public void DiscrepancyNoteMigrationPreservesLegacyClosuresAndData()
    {
        var legacy=new Database(Path.Combine(directory,"legacy16.db"));SaleContractMigrationTests.InitializeThrough(legacy,16);
        var lw=new SqliteLocalUnitOfWork(legacy);var actor=Guid.NewGuid();var closedId=Guid.NewGuid();
        lw.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);s.AddAdministrator(actor,"LEGACY","Histórico",new LocalPasswords().Hash(password),clock.UtcNow);return true;});
        using(var c=legacy.Open())using(var q=c.CreateCommand())
        {
            q.CommandText="INSERT INTO cash_session(id,terminal_id,opened_by,opened_utc,opening_cents,state,closed_by,closed_utc,expected_cents,counted_cents,difference_cents) VALUES($id,$t,$u,'2026-01-01T10:00:00Z',50000,'CLOSED',$u,'2026-01-01T11:00:00Z',175000,170000,-5000)";
            q.Parameters.AddWithValue("$id",closedId.ToString());q.Parameters.AddWithValue("$t",terminal.Id.ToString());q.Parameters.AddWithValue("$u",actor.ToString());q.ExecuteNonQuery();
        }
        var before=lw.Read(s=>s.FindSession(closedId))!;legacy.Initialize();legacy.Initialize();legacy.Validate();
        var after=lw.Read(s=>s.FindSession(closedId))!;
        Assert.AreEqual(before.Expected,after.Expected);Assert.AreEqual(before.Counted,after.Counted);Assert.AreEqual(before.Difference,after.Difference);Assert.AreEqual(before.ClosedAt,after.ClosedAt);Assert.IsNull(after.DiscrepancyNote);
        using var connection=legacy.Open(true);using var query=connection.CreateCommand();query.CommandText="SELECT max(version) FROM schema_migration";Assert.AreEqual((long)Database.LatestVersion,query.ExecuteScalar());
        query.CommandText=$"SELECT count(*) FROM schema_migration WHERE version>{Database.LatestVersion}";Assert.AreEqual(0L,query.ExecuteScalar());
        var row=lw.Read(s=>s.FindCashClosure(terminal.Id,closedId))!;Assert.AreEqual("No disponible",row.CountedByName);Assert.IsNull(row.DiscrepancyNote);
        Assert.AreEqual((long)Database.LatestVersion,Sql("SELECT max(version) FROM schema_migration"));
    }
}
