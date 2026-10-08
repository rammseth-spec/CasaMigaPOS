using System.Security.Cryptography;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;

if(args.Length!=2)throw new ArgumentException("Uso: FixtureTool <origen protegido> <destino nuevo>");
var source=Path.GetFullPath(args[0]);var target=Path.GetFullPath(args[1]);
if(Directory.Exists(target)||source==target)throw new IOException("El destino debe ser nuevo y aislado.");
var password=Environment.GetEnvironmentVariable("CASAMIGA_FIXTURE_PASSWORD")??throw new InvalidOperationException("Falta credencial de revisión.");
var sourceDb=Path.Combine(source,"casamiga.db");var sourceHash=Hash(sourceDb);
if(sourceHash!="CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D")throw new InvalidDataException("La base protegida no coincide.");
using(var sourceLease=new FileStream(Path.Combine(source,"casamiga.lock"),FileMode.Open,FileAccess.Read,FileShare.None))
{
    if(new FileInfo(sourceDb+"-wal") is { Exists:true,Length:>0 })throw new IOException("El origen tiene WAL pendiente.");
    Directory.CreateDirectory(target);
    foreach(var file in new[]{"casamiga.db","terminal.json","printing.json"})File.Copy(Path.Combine(source,file),Path.Combine(target,file));
    var media=Path.Combine(source,"product-images");
    foreach(var file in Directory.EnumerateFiles(media,"*",SearchOption.AllDirectories))
    {var destination=Path.Combine(target,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(file,destination);}
}
using var lease=new DirectoryLease(target);
var db=new Database(Path.Combine(target,"casamiga.db"));
long Scalar(string sql){using var c=db.Open(true);using var q=c.CreateCommand();q.CommandText=sql;return Convert.ToInt64(q.ExecuteScalar());}
var originalSchema=Scalar("SELECT max(version) FROM schema_migration");
var tables=new[]{"sale","payment","cash_session","cash_count","audit_entry","inventory_operation","inventory_movement","production_batch","receipt_print_request","receipt_print_event"};
var before=tables.ToDictionary(x=>x,x=>Scalar("SELECT count(*) FROM "+x));
db.Initialize();db.Validate();var store=new SqliteLocalUnitOfWork(db);var clock=new SystemClock();var identity=new UserSession();
var login=new IdentityService(store,new LocalPasswords(),clock,identity);
if(!login.Login("catalog-admin",password))throw new InvalidOperationException("Las credenciales no corresponden a la copia.");
var terminal=TerminalConfiguration.LoadOrCreate(target);var cash=new CashService(store,identity,clock);var closing=new CashClosingService(store,identity,clock);
var existing=cash.Current(terminal.Id);
if(existing is not null)
{
    var resume=closing.Resume(existing.Session.Id);
    if(resume is null){var expected=store.Read(s=>existing.Session.OpeningFund+s.CashSalesTotal(existing.Session.Id));closing.SubmitCount(existing.Session.Id,expected);}
    closing.Close(existing.Session.Id,"Preparación sintética de la copia de revisión");
}
var product=store.Read(s=>s.QueryProducts(new(IncludeInactive:false,Page:0)).Items.Select(x=>x.Product).First(x=>x.IsActive));
var scenarios=new List<object>();
for(var i=0;i<28;i++)
{
    var opened=cash.Open(terminal.Id,new Money(50000)).Session;
    store.Run(s=>
    {
        // Fixture only: direct synthetic aggregate avoids checkout/printing/hardware.
        var sale=new Sale(Guid.NewGuid(),Guid.NewGuid(),identity.Current!.Id,terminal.Id,opened.Id,s.NextSaleSequence(terminal.Id),clock.UtcNow,
            [new(product.Id,"SMOKE NO COMERCIAL · Cierre de caja",new Money(125000),1)],Guid.NewGuid(),new Money(150000));
        s.AddSale(sale);return true;
    });
    var counted=(i%3) switch {1=>178000L,2=>170000L,_=>175000L};
    closing.SubmitCount(opened.Id,new Money(counted));
    var closed=closing.Close(opened.Id,counted==175000?null:counted>175000?"SMOKE · sobrante sintético de $30":"SMOKE · faltante sintético de $50");
    scenarios.Add(new{closed.Id,expected=closed.Expected!.Value.Cents,counted,difference=closed.Difference!.Value.Cents,note=closed.DiscrepancyNote});
}
if(cash.Current(terminal.Id) is not null)throw new InvalidOperationException("La fixture debe iniciar sin sesión.");
var printing=new PrintingConfigurationProvider(target).Read();
if(printing.Settings?.OpenCashDrawer!=false)throw new InvalidDataException("Cajón no deshabilitado.");
db.Validate();var after=tables.ToDictionary(x=>x,x=>Scalar("SELECT count(*) FROM "+x));
foreach(var table in new[]{"inventory_operation","inventory_movement","production_batch","receipt_print_request","receipt_print_event"})
    if(before[table]!=after[table])throw new InvalidDataException("Cambió una tabla ajena: "+table);
if(Hash(sourceDb)!=sourceHash)throw new InvalidDataException("Cambió la base protegida.");
var manifest=new{source,target,sourceHash,sourceSchema=originalSchema,schema=Scalar("SELECT max(version) FROM schema_migration"),credentialVerified=true,
    terminal,countsBefore=before,countsAfter=after,scenarios,noOpenSession=true,openCashDrawer=false,printingHash=Hash(Path.Combine(target,"printing.json")),databaseHash=Hash(db.FilePath)};
File.WriteAllText(Path.Combine(target,"cash-fixture-manifest.json"),JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true}));
static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
