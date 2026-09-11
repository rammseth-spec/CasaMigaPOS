using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class ReceiptTests : SalesFixture
{
    private static BusinessReceiptProfile Profile()=>new("CASA MIGA — DATOS SINTÉTICOS","Calle de prueba 1","555 000 0000",null,"@prueba","Gracias por su compra.");
    private ReceiptDocument Document()
    {
        var sale=Persist();var detail=work.Run(s=>s.FindSaleDetail(sale.Id))!;
        return ReceiptDocument.FromSale(detail,Profile(),TimeZoneInfo.Utc);
    }
    private static PrintingSettings Settings()=>new(1,true,"QIAN TEST",48,"windows-1252","Disabled",false,Profile());

    [TestMethod] public void ReceiptUsesSaleSnapshotsAndStableCanonicalHash()
    {
        var document=Document();var json=ReceiptDocumentCodec.Serialize(document);var restored=ReceiptDocumentCodec.Deserialize(json);
        Assert.AreEqual(document.Folio,restored.Folio);Assert.AreEqual(product.Name,restored.Items.Single().ProductName);
        Assert.AreEqual(3800L,restored.Total.Cents);Assert.AreEqual(5000L,restored.Received.Cents);Assert.AreEqual(1200L,restored.Change.Cents);
        Assert.AreEqual(document.Sha256,restored.Sha256);Assert.AreEqual(64,document.Sha256.Length);
        catalog.EditProduct(product.Id,product.CategoryId,"P1","Nombre nuevo","",new Money(9900),true);
        Assert.AreEqual("Concha",restored.Items.Single().ProductName);Assert.AreEqual(1900L,restored.Items.Single().UnitPrice.Cents);
    }

    [TestMethod] public void RendererWrapsLongSpanishContentAndMarksCopies()
    {
        var original=Document();var business=original.Business with{DisplayName="Panadería Casa Miga con un nombre comercial deliberadamente largo",ClosingMessage="¡Gracias! Vuelva pronto por más café, piñón y niño."};
        var source=original.Items[0];
        var item=new ReceiptLine(source.ProductId,"Niño envuelto con piñón, café y azúcar de longitud deliberadamente extensa",
            source.UnitPrice,source.Quantity,source.LineTotal);
        var document=new ReceiptDocument(original.Version,original.Folio,original.ConfirmedAt,original.UserId,original.CashierName,
            original.TerminalId,original.TerminalName,[item],original.Subtotal,original.Total,original.PaymentMethod,
            original.Applied,original.Received,original.Change,business);
        var text=new TextReceiptRenderer().Render(document,ReceiptCopyKind.Reprint);
        Assert.Contains("REIMPRESIÓN",text);Assert.Contains("Niño",text);Assert.Contains("piñón",text);Assert.Contains("$38.00",text);
        Assert.IsTrue(text.Split('\n',StringSplitOptions.RemoveEmptyEntries).All(line=>line.Length<=48));
        Assert.AreEqual(text,new TextReceiptRenderer().Render(document,ReceiptCopyKind.Reprint));
    }

    [TestMethod] public void EscPosBlockAContainsTextButNoControlCommands()
    {
        var payload=new EscPosReceiptRenderer().Render(Document(),ReceiptCopyKind.Original,Settings());
        var text=Encoding.GetEncoding(1252).GetString(payload);
        Assert.Contains("CASA MIGA",text);Assert.Contains("Cajero",text);Assert.IsFalse(payload.Contains((byte)0x1B));
        Assert.IsFalse(payload.Contains((byte)0x1D));
    }

    [TestMethod] public void SnapshotRequestsEventsAndPermissionsArePersistedAndImmutable()
    {
        var sale=Persist();var transport=new ProbeTransport(work,sale.OperationId);
        var service=Service(transport);
        var result=service.PrintOriginal(sale);
        Assert.AreEqual(ReceiptPrintState.Submitted,result.State);Assert.AreEqual(41,result.SpoolerJobId);
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM receipt_snapshot"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM receipt_print_request"));
        Assert.AreEqual(2L,Sql("SELECT count(*) FROM receipt_print_event"));Assert.AreEqual(2L,Sql("SELECT count(*) FROM audit_entry WHERE action LIKE 'receipt.print.%'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("UPDATE receipt_snapshot SET content_json=content_json"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("DELETE FROM receipt_print_request"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("UPDATE receipt_print_event SET state=state"));
        Assert.AreEqual(2L,Sql("SELECT count(*) FROM role_permission WHERE permission_name='sales.receipt.reprint'"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM role_permission WHERE permission_name='sales.receipt.reprint' AND role_name='Cashier'"));
        Assert.IsTrue(transport.DatabaseWasAvailable);
    }

    [TestMethod] public void MissingConfigurationDoesNotAffectConfirmedSaleAndCanBeRetried()
    {
        var sale=Persist();var missing=new FixedSettings(new(null,"CONFIG_MISSING"));var transport=new ProbeTransport();
        var service=Service(transport,missing);
        var failed=service.PrintOriginal(sale);
        Assert.AreEqual(ReceiptPrintState.Failed,failed.State);Assert.AreEqual(0,transport.Calls);Assert.AreEqual(1L,Sql("SELECT count(*) FROM sale"));
        missing.Value=new(Settings(),null);var retried=service.Retry(failed.RequestId);
        Assert.AreEqual(ReceiptPrintState.Submitted,retried.State);Assert.AreEqual(1,transport.Calls);
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM receipt_snapshot"));Assert.AreEqual(2L,Sql("SELECT count(*) FROM receipt_print_request"));
    }

    [TestMethod] public void UncertainTransportIsRecordedAndOriginalIsIdempotent()
    {
        var sale=Persist();var transport=new ProbeTransport{Failure=new("WRITE_PRINTER_5",true)};var service=Service(transport);
        var first=service.PrintOriginal(sale);var repeated=service.PrintOriginal(sale);
        Assert.AreEqual(ReceiptPrintState.Unknown,first.State);Assert.AreEqual(first.RequestId,repeated.RequestId);Assert.AreEqual(1,transport.Calls);
        Assert.AreEqual("UNKNOWN",Sql("SELECT state FROM receipt_print_event ORDER BY sequence DESC LIMIT 1"));
    }

    [TestMethod] public void ReprintRequiresDedicatedPermission()
    {
        var sale=Persist();Sql("DELETE FROM role_permission WHERE role_name='Administrator' AND permission_name='sales.receipt.reprint'");
        Assert.ThrowsExactly<AccessDeniedException>(()=>Service(new ProbeTransport()).Reprint(sale.Id));
    }

    [TestMethod] public async Task SerializedTransportNeverRunsJobsConcurrently()
    {
        var inner=new ConcurrentProbeTransport();var serialized=new SerializedReceiptPrintTransport(inner);
        var jobs=Enumerable.Range(0,8).Select(i=>Task.Run(()=>serialized.Submit(new("Q",$"J{i}",[1])))).ToArray();
        await Task.WhenAll(jobs);
        Assert.AreEqual(1,inner.MaximumConcurrent);Assert.AreEqual(8,inner.Calls);
    }

    [TestMethod] public void WindowsRawTransportClassifiesMissingQueueBeforeSubmission()
    {
        if(!OperatingSystem.IsWindows())return;
        var error=Assert.ThrowsExactly<PrintTransportException>(()=>new WindowsRawPrintTransport().Submit(
            new("CASAMIGA-MISSING-"+Guid.NewGuid().ToString("N"),"Prueba",[65])));
        Assert.IsFalse(error.OutcomeUncertain);Assert.StartsWith("OPEN_PRINTER_",error.ErrorCode);
    }

    [TestMethod] public void PrintingConfigurationIsOptionalStrictAndLocal()
    {
        var provider=new PrintingConfigurationProvider(directory);Assert.AreEqual("CONFIG_MISSING",provider.Read().ErrorCode);
        File.WriteAllText(Path.Combine(directory,PrintingConfigurationProvider.FileName),"not-json");Assert.AreEqual("CONFIG_INVALID",provider.Read().ErrorCode);
        var json=JsonSerializer.Serialize(Settings(),new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        File.WriteAllText(Path.Combine(directory,PrintingConfigurationProvider.FileName),json);Assert.IsTrue(provider.Read().Available);
        File.WriteAllText(Path.Combine(directory,PrintingConfigurationProvider.FileName),json.Replace("\"openCashDrawer\":false","\"openCashDrawer\":true"));
        Assert.AreEqual("CONFIG_INVALID",provider.Read().ErrorCode);
    }

    [TestMethod] public void ControlledPhysicalTestIsAsciiSafeSingleSubmissionAndTraced()
    {
        var trace=Path.Combine(directory,"physical-print-test.jsonl");var transport=new ProbeTransport();
        var settings=Settings() with{QueueName="POS-80",EncodingName="us-ascii",Business=new("PRUEBA CASA MIGA","PRUEBA DE IMPRESION",ClosingMessage:"GRACIAS POR SU COMPRA")};
        var result=new ControlledReceiptPrintTest(new FixedSettings(new(settings,null)),new EscPosReceiptRenderer(),transport,clock,trace).Run();
        Assert.AreEqual(ReceiptPrintState.Submitted,result.State);Assert.AreEqual(41,result.JobId);Assert.AreEqual(1,transport.Calls);
        Assert.AreEqual("POS-80",result.QueueName);
        Assert.IsTrue(result.ReceiptText.All(character=>character<=0x7f));Assert.IsFalse(result.ReceiptText.Contains('\u001b'));Assert.IsFalse(result.ReceiptText.Contains('\u001d'));
        Assert.IsTrue(result.ReceiptText.Split("\r\n",StringSplitOptions.RemoveEmptyEntries).All(line=>line.Length<=48));
        var events=File.ReadAllLines(trace);Assert.HasCount(2,events);Assert.Contains("\"state\":\"REQUESTED\"",events[0]);
        Assert.Contains("\"state\":\"SUBMITTED\"",events[1]);Assert.Contains("\"jobId\":41",events[1]);
    }

    [TestMethod] public void ControlledPhysicalTestRequiresEnabledTestProfileBeforeTransport()
    {
        var transport=new ProbeTransport();var disabled=Settings() with{Enabled=false};
        Assert.ThrowsExactly<InvalidOperationException>(()=>new ControlledReceiptPrintTest(
            new FixedSettings(new(disabled,"PRINTING_DISABLED")),new EscPosReceiptRenderer(),transport,clock,Path.Combine(directory,"trace.jsonl")).Run());
        Assert.AreEqual(0,transport.Calls);
    }

    [TestMethod] public void ControlledPhysicalTestRecordsKnownTransportFailureWithoutRetry()
    {
        var transport=new ProbeTransport{Failure=new("OPEN_PRINTER_1801",false)};
        var settings=Settings() with{QueueName="POS-80",EncodingName="us-ascii",Business=new("PRUEBA CASA MIGA",ClosingMessage:"GRACIAS POR SU COMPRA")};
        var trace=Path.Combine(directory,"failed-print.jsonl");
        var result=new ControlledReceiptPrintTest(new FixedSettings(new(settings,null)),new EscPosReceiptRenderer(),transport,clock,trace).Run();
        Assert.AreEqual(ReceiptPrintState.Failed,result.State);Assert.AreEqual(1,transport.Calls);Assert.IsNull(result.JobId);
        Assert.Contains("\"state\":\"FAILED\"",File.ReadAllLines(trace)[1]);Assert.Contains("OPEN_PRINTER_1801",File.ReadAllText(trace));
    }

    [TestMethod] public void UpgradeFromSevenPreservesSaleAndAddsReceiptSchema()
    {
        var path=Path.Combine(directory,"version-seven.db");var old=new Database(path);var assembly=typeof(Database).Assembly;
        using(var connection=old.Open())
        {
            using var command=connection.CreateCommand();
            command.CommandText="CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT";command.ExecuteNonQuery();
            foreach(var resource in assembly.GetManifestResourceNames().Where(n=>n.EndsWith(".sql",StringComparison.Ordinal)).Order().Take(7))
            {
                using var reader=new StreamReader(assembly.GetManifestResourceStream(resource)!);var sql=reader.ReadToEnd();
                command.Parameters.Clear();command.CommandText=sql;command.ExecuteNonQuery();
                command.CommandText="INSERT INTO schema_migration VALUES((SELECT count(*)+1 FROM schema_migration),$hash,$at)";
                command.Parameters.AddWithValue("$hash",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
                command.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));command.ExecuteNonQuery();
            }
        }
        var oldWork=new SqliteLocalUnitOfWork(old);var oldUser=new UserSession();var oldIdentity=new IdentityService(oldWork,new LocalPasswords(),clock,oldUser);
        oldIdentity.Bootstrap("oldadmin","Anterior",password);Assert.IsTrue(oldIdentity.Login("oldadmin",password));
        oldWork.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
        var category=new CatalogService(oldWork,oldUser,clock,terminal.Id).CreateCategory("Anterior");
        var oldProduct=new CatalogService(oldWork,oldUser,clock,terminal.Id).CreateProduct(category.Id,"OLD","Producto anterior","",new Money(125));
        var oldCash=new CashService(oldWork,oldUser,clock).Open(terminal.Id,new Money(0)).Session;
        var oldSale=oldWork.Run(s=>{var candidate=new Sale(Guid.NewGuid(),Guid.NewGuid(),oldUser.Current!.Id,terminal.Id,oldCash.Id,
            s.NextSaleSequence(terminal.Id),clock.UtcNow,[new(oldProduct.Id,oldProduct.Name,oldProduct.SalePrice,1)],Guid.NewGuid(),new Money(200));s.AddSale(candidate);return candidate;});
        old.Initialize();old.Validate();
        using var verified=old.Open();using var query=verified.CreateCommand();
        query.CommandText="SELECT count(*) FROM schema_migration";Assert.AreEqual(8L,query.ExecuteScalar());
        query.CommandText="SELECT count(*) FROM sale WHERE id=$id";query.Parameters.AddWithValue("$id",oldSale.Id.ToString("D"));Assert.AreEqual(1L,query.ExecuteScalar());
        query.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN('receipt_snapshot','receipt_print_request','receipt_print_event')";
        Assert.AreEqual(3L,query.ExecuteScalar());
    }

    [TestMethod] public void InstallationBackupRestoresReceiptSnapshotsAndTrace()
    {
        var sale=Persist();Assert.AreEqual(ReceiptPrintState.Submitted,Service(new ProbeTransport()).PrintOriginal(sale).State);
        var package=new InstallationBackup(directory).Create();
        var restored=Path.Combine(Path.GetDirectoryName(directory)!,"ReceiptRestore-"+Guid.NewGuid().ToString("N"));
        try
        {
            InstallationBackup.Restore(package,restored);
            using var connection=new Database(Path.Combine(restored,"casamiga.db")).Open(readOnly:true);using var command=connection.CreateCommand();
            command.CommandText="SELECT (SELECT count(*) FROM receipt_snapshot),(SELECT count(*) FROM receipt_print_request),(SELECT count(*) FROM receipt_print_event)";
            using var row=command.ExecuteReader();Assert.IsTrue(row.Read());Assert.AreEqual(1L,row.GetInt64(0));Assert.AreEqual(1L,row.GetInt64(1));Assert.AreEqual(2L,row.GetInt64(2));
            Assert.IsFalse(File.Exists(Path.Combine(restored,PrintingConfigurationProvider.FileName)));
        }
        finally{if(Directory.Exists(restored))Directory.Delete(restored,true);}
    }

    private ReceiptService Service(IReceiptPrintTransport transport,IPrintingSettingsProvider? settings=null)
        =>new(work,user,clock,terminal.Id,TimeZoneInfo.Utc,settings??new FixedSettings(new(Settings(),null)),new EscPosReceiptRenderer(),transport);

    private sealed class FixedSettings(PrintingSettingsResult value):IPrintingSettingsProvider
    {public PrintingSettingsResult Value{get;set;}=value;public PrintingSettingsResult Read()=>Value;}
    private sealed class ProbeTransport(SqliteLocalUnitOfWork? database=null,Guid? operation=null):IReceiptPrintTransport
    {
        public int Calls;public bool DatabaseWasAvailable;public PrintTransportException? Failure;
        public PrintSubmissionResult Submit(PrintSubmission submission)
        {
            Interlocked.Increment(ref Calls);
            if(database is not null){DatabaseWasAvailable=database.Run(s=>s.FindSaleByOperation(operation!.Value)) is not null;}
            if(Failure is not null)throw Failure;return new(41);
        }
    }
    private sealed class ConcurrentProbeTransport:IReceiptPrintTransport
    {
        private int active;public int Calls;public int MaximumConcurrent;
        public PrintSubmissionResult Submit(PrintSubmission submission)
        {
            var current=Interlocked.Increment(ref active);Interlocked.Increment(ref Calls);
            int observed;do{observed=MaximumConcurrent;if(observed>=current)break;}while(Interlocked.CompareExchange(ref MaximumConcurrent,current,observed)!=observed);
            Thread.Sleep(15);Interlocked.Decrement(ref active);return new(Calls);
        }
    }
}
