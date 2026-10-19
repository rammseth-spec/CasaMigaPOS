using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class TicketsApplicationTests : SalesFixture
{
    private static BusinessReceiptProfile Profile()=>new("CASA MIGA — DATOS SINTÉTICOS","Calle de prueba 1","555 000 0000",null,"@prueba","Gracias por su compra.");
    private static PrintingSettings Settings()=>new(1,true,"QIAN TEST",48,"windows-1252","Disabled",false,Profile(),16);
    private ReceiptService Service(IReceiptPrintTransport transport,IPrintQueueMonitor? queue,IPrintingSettingsProvider? settings=null)
        =>new(work,user,clock,terminal.Id,TimeZoneInfo.Utc,settings??new FixedSettings(new(Settings(),null)),new EscPosReceiptRenderer(),transport,queue);

    [TestMethod] public void RecentActivityListsEveryTicketWithItsLatestStateNewestFirst()
    {
        var sale=Persist();var transport=new Transport();var service=Service(transport,new FakeQueue());
        Assert.AreEqual(ReceiptPrintState.Submitted,service.Reprint(sale.Id).State);
        transport.Failure=new PrintTransportException("OPEN_PRINTER_1801",false);
        Assert.AreEqual(ReceiptPrintState.Failed,service.Reprint(sale.Id).State);

        var activity=service.RecentActivity();
        Assert.HasCount(2,activity);
        Assert.AreEqual(ReceiptPrintState.Failed,activity[0].State);Assert.AreEqual("OPEN_PRINTER_1801",activity[0].ErrorCode);
        Assert.AreEqual(ReceiptPrintState.Submitted,activity[1].State);Assert.AreEqual(41,activity[1].SpoolerJobId);
        Assert.IsTrue(activity.All(a=>a.Kind==ReceiptCopyKind.Reprint&&a.SaleId==sale.Id));
        var detail=work.Run(s=>s.FindSaleDetail(sale.Id))!;
        Assert.AreEqual(DisplayFolio.For(detail.TerminalName,sale.Sequence),activity[0].Folio);
        Assert.IsFalse(string.IsNullOrWhiteSpace(activity[0].RequestedBy));
    }

    [TestMethod] public void TicketsAndQueueRequireTheReprintPermission()
    {
        var queue=new FakeQueue([new(7,"Casa Miga C01-000001","En espera",null,1,0,"cajero")]);var service=Service(new Transport(),queue);
        Sql("UPDATE app_user SET role_name='Cashier'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>service.RecentActivity());
        Assert.ThrowsExactly<AccessDeniedException>(()=>service.PrintQueue());
        Assert.ThrowsExactly<AccessDeniedException>(()=>service.CancelQueueJob(7));
        Assert.ThrowsExactly<AccessDeniedException>(()=>service.ClearQueue());
        Assert.IsEmpty(queue.Cancelled,"A denied user must not reach the Windows queue.");
        Sql("UPDATE app_user SET role_name='Manager'");
        Assert.HasCount(1,service.PrintQueue().Jobs);
    }

    [TestMethod] public void PrintQueueReadsOnlyTheConfiguredQueueAndReportsWhyItIsUnavailable()
    {
        var queue=new FakeQueue([new(3,"Casa Miga C01-000002","Imprimiendo",DateTimeOffset.UtcNow,1,0,"admin")]);
        var snapshot=Service(new Transport(),queue).PrintQueue();
        Assert.IsTrue(snapshot.Available);Assert.AreEqual("QIAN TEST",snapshot.QueueName);Assert.AreEqual("QIAN TEST",queue.LastQueue);Assert.HasCount(1,snapshot.Jobs);

        Assert.AreEqual("CONFIG_MISSING",Service(new Transport(),queue,new FixedSettings(new(null,"CONFIG_MISSING"))).PrintQueue().ErrorCode);
        Assert.AreEqual("QUEUE_UNAVAILABLE",Service(new Transport(),null).PrintQueue().ErrorCode);
        queue.ListFailure=new PrintTransportException("OPEN_PRINTER_1801",false);
        var broken=Service(new Transport(),queue).PrintQueue();
        Assert.IsFalse(broken.Available);Assert.AreEqual("OPEN_PRINTER_1801",broken.ErrorCode);Assert.IsEmpty(broken.Jobs);
    }

    [TestMethod] public void CancellingAndClearingTheQueueIsAuditedAndNeverTouchesSalesOrTickets()
    {
        var sale=Persist();var service=Service(new Transport(),null);
        _=service.Reprint(sale.Id);
        var queue=new FakeQueue([new(1,"a","En espera",null,1,0,null),new(2,"b","Imprimiendo",null,1,0,null),new(3,"c","En espera",null,1,0,null)]);
        service=Service(new Transport(),queue);
        var sales=Sql("SELECT count(*) FROM sale");var requests=Sql("SELECT count(*) FROM receipt_print_request");var events=Sql("SELECT count(*) FROM receipt_print_event");

        service.CancelQueueJob(3);
        CollectionAssert.AreEqual(new[]{3},queue.Cancelled.ToArray());
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='receipt.queue.job_cancelled'"));

        queue.CancelFailureFor=2;// Terminó de imprimirse entre la lectura y la cancelación.
        Assert.AreEqual(1,service.ClearQueue());
        CollectionAssert.AreEqual(new[]{3,1},queue.Cancelled.ToArray());
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='receipt.queue.cleared'"));

        Assert.AreEqual(sales,Sql("SELECT count(*) FROM sale"));
        Assert.AreEqual(requests,Sql("SELECT count(*) FROM receipt_print_request"));
        Assert.AreEqual(events,Sql("SELECT count(*) FROM receipt_print_event"));
        Assert.ThrowsExactly<ArgumentException>(()=>service.CancelQueueJob(0));
        Assert.ThrowsExactly<InvalidOperationException>(()=>Service(new Transport(),null).CancelQueueJob(5));
    }

    private sealed class FixedSettings(PrintingSettingsResult value):IPrintingSettingsProvider
    {public PrintingSettingsResult Read()=>value;}
    private sealed class Transport:IReceiptPrintTransport
    {
        public PrintTransportException? Failure;
        public PrintSubmissionResult Submit(PrintSubmission submission)=>Failure is null?new(41):throw Failure;
    }
    private sealed class FakeQueue(List<PrintQueueJob>? jobs=null):IPrintQueueMonitor
    {
        private readonly List<PrintQueueJob> pending=jobs??[];
        public readonly List<int> Cancelled=[];public string? LastQueue;public PrintTransportException? ListFailure;public int? CancelFailureFor;
        public IReadOnlyList<PrintQueueJob> List(string queueName){LastQueue=queueName;if(ListFailure is not null)throw ListFailure;return pending.ToList();}
        public void Cancel(string queueName,int jobId)
        {
            LastQueue=queueName;
            if(jobId==CancelFailureFor)throw new PrintTransportException("CANCEL_JOB_87",false);
            Cancelled.Add(jobId);pending.RemoveAll(j=>j.JobId==jobId);
        }
    }
}
