using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;

// Explicit development command. Uses real services and a file-only transport, never a printer.
internal static class R2CheckoutEvidence
{
    public static async Task<int> Run(string directory,string password,string output)
    {
        directory=Path.GetFullPath(directory);output=Path.GetFullPath(output);
        using var lease=new DirectoryLease(directory);
        var database=new Database(Path.Combine(directory,"casamiga.db"));database.Validate();
        var work=new SqliteLocalUnitOfWork(database);var clock=new SystemClock();var terminal=TerminalConfiguration.LoadOrCreate(directory);
        var journal=new CheckoutJournal(directory);var session=new UserSession(journal);
        var identity=new IdentityService(work,new LocalPasswords(),clock,session);
        if(!identity.Login("catalog-admin",password))throw new InvalidOperationException("Credencial smoke inválida.");
        long Count(string table)
        {
            using var connection=database.Open(true);using var command=connection.CreateCommand();
            command.CommandText=$"SELECT count(*) FROM {table}";return (long)command.ExecuteScalar()!;
        }
        if(Count("sale")!=0)throw new InvalidOperationException("La evidencia requiere un dataset nuevo sin ventas.");
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new InvalidOperationException("La carpeta de evidencia debe estar vacía.");
        Directory.CreateDirectory(output);
        var product=new CatalogService(work,session,clock,terminal.Id,true).Products(new("Concha de vainilla",null,true,0)).Items.Single().Product;
        var pos=new PosService(work,session,terminal.Id);var settings=new Settings();
        var transport=new FileTransport(work,output);var receipts=new ReceiptService(work,session,clock,terminal.Id,TimeZoneInfo.Local,settings,new EscPosReceiptRenderer(),transport);
        var checkout=new CheckoutService(work,session,clock,terminal.Id,journal,new CheckoutReceiptSnapshot(settings,TimeZoneInfo.Local));
        var cases=new List<object>();
        foreach(var mode in new[]{"sin-ticket","con-ticket","fallo-impresion"})
        {
            pos.Add(product.Id);transport.Fail=mode=="fallo-impresion";transport.Name=mode;
            var vm=new CheckoutViewModel(checkout,receipts,product.SalePrice,false){Received="20.00"};
            await vm.ConfirmAsync(mode!="sin-ticket");
            var sale=vm.Result??throw new InvalidOperationException(vm.Message);
            var history=new SalesHistoryService(work,session,TimeZoneInfo.Local).Search(new(SalesHistoryMode.Folio,sale.Folio));
            if(history.Total!=1||history.Items.Single().SaleId!=sale.Id)throw new InvalidOperationException("Venta ausente del Historial.");
            if(mode=="sin-ticket"&&Count("receipt_print_request")!=0)throw new InvalidOperationException("Cobrar generó impresión.");
            cases.Add(new{Mode=mode,SaleId=sale.Id,sale.Folio,HistoryMatches=history.Total,PrintState=vm.PrintResult?.State.ToString(),vm.PrintMessage});
            if(!await vm.LeaveAsync()||session.Cart is {IsEmpty:false})throw new InvalidOperationException("Carrito retenido tras venta confirmada.");
            if(mode=="con-ticket")
            {
                transport.Name="reprint";var before=Count("inventory_movement");var sales=Count("sale");var payments=Count("payment");
                if(receipts.Reprint(sale.Id).State!=ReceiptPrintState.Submitted)throw new InvalidOperationException("Reprint falló.");
                if(Count("inventory_movement")!=before||Count("sale")!=sales||Count("payment")!=payments)throw new InvalidOperationException("Reprint mutó el estado comercial.");
            }
        }
        if(Count("sale")!=3||Count("payment")!=3||Count("receipt_print_request")!=3)throw new InvalidOperationException("Conteo inesperado.");
        var result=new{PhysicalPrints=0,Transport="file-only",Sales=Count("sale"),Payments=Count("payment"),History=new SalesHistoryService(work,session,TimeZoneInfo.Local).Search(new()).Total,PrintRequests=Count("receipt_print_request"),transport.CommittedBeforeEverySubmission,Logo=OfficialReceiptLogo.AssetId,LogoWidth=OfficialReceiptLogo.Width,LogoHeight=OfficialReceiptLogo.Height,Cases=cases};
        var json=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});File.WriteAllText(Path.Combine(output,"checkout-evidence.json"),json);Console.WriteLine(json);return 0;
    }
    private sealed class Settings:IPrintingSettingsProvider
    {
        public PrintingSettingsResult Read()=>new(new(1,true,"FILE-ONLY",48,"ibm850","Partial",false,new("CASA MIGA",ClosingMessage:"PRUEBA CONTROLADA - Gracias por su compra",MonochromeLogoPath:OfficialReceiptLogo.AssetId),2),null);
    }
    private sealed class FileTransport(SqliteLocalUnitOfWork work,string output):IReceiptPrintTransport
    {
        public bool Fail;public string Name="";public bool CommittedBeforeEverySubmission=true;
        public PrintSubmissionResult Submit(PrintSubmission submission)
        {
            // A separate read connection sees only committed sales.
            var history=work.Read(s=>s.QuerySales(new(null,null,null,null,0)));
            // El ticket (y el nombre del trabajo) lleva el folio visible, p. ej. C01-000001.
            CommittedBeforeEverySubmission &= history.Items.Any(sale=>submission.JobName.Contains(CasaMiga.Domain.DisplayFolio.FromInternal(sale.Folio,sale.TerminalName),StringComparison.Ordinal));
            if(!CommittedBeforeEverySubmission)throw new InvalidOperationException("Impresión anterior al commit.");
            File.WriteAllBytes(Path.Combine(output,Name+".bin"),submission.Payload);
            File.WriteAllText(Path.Combine(output,Name+".sha256"),Convert.ToHexString(SHA256.HashData(submission.Payload)));
            if(Fail)throw new PrintTransportException("SYNTHETIC_OFFLINE",false);
            return new(1);
        }
    }
}
