using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;

if(args.Length!=2)throw new ArgumentException("Uso: FixtureTool <origen protegido> <destino nuevo>");
var source=Path.GetFullPath(args[0]);var target=Path.GetFullPath(args[1]);
if(Directory.Exists(target)||source==target)throw new IOException("El destino debe ser nuevo y aislado.");
var password=Environment.GetEnvironmentVariable("CASAMIGA_FIXTURE_PASSWORD")??throw new InvalidOperationException("Falta credencial de revisión.");
var sourceDb=Path.Combine(source,"casamiga.db");var sourceHash=Hash(sourceDb);
const string protectedHash="CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D";
if(sourceHash!=protectedHash)throw new InvalidDataException("La base protegida no coincide.");

using(var sourceLease=new FileStream(Path.Combine(source,"casamiga.lock"),FileMode.Open,FileAccess.Read,FileShare.None))
{
    if(new FileInfo(sourceDb+"-wal") is { Exists:true,Length:>0 })throw new IOException("El origen tiene WAL pendiente.");
    Directory.CreateDirectory(target);
    foreach(var file in new[]{"casamiga.db","terminal.json"})File.Copy(Path.Combine(source,file),Path.Combine(target,file));
    var media=Path.Combine(source,"product-images");
    if(Directory.Exists(media))foreach(var file in Directory.EnumerateFiles(media,"*",SearchOption.AllDirectories))
    {var destination=Path.Combine(target,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(file,destination);}
}

var profile=new BusinessReceiptProfile(
    "Casa Miga",
    "Insurgentes 116\nEsquina con Albino García\nBarrio de Santiaguito\nC.P. 38000 · Celaya, Gto.",
    Phone:null,
    TaxId:null,
    Contact:"Facebook: Casa Miga\nInstagram: @casamiga126",
    ClosingMessage:"Gracias por su compra",
    MonochromeLogoPath:null);
var settings=new PrintingSettings(1,true,"POS-80",48,"ibm850","Partial",false,profile,2);
var jsonOptions=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true};
File.WriteAllText(Path.Combine(target,PrintingConfigurationProvider.FileName),JsonSerializer.Serialize(settings,jsonOptions)+Environment.NewLine,new UTF8Encoding(false));

using var lease=new DirectoryLease(target);
var db=new Database(Path.Combine(target,"casamiga.db"));
long Scalar(string sql){using var c=db.Open(true);using var q=c.CreateCommand();q.CommandText=sql;return Convert.ToInt64(q.ExecuteScalar());}
var sourceSchema=Scalar("SELECT max(version) FROM schema_migration");
var guardedTables=new[]{"inventory_operation","inventory_movement","production_batch","receipt_print_request","receipt_print_event"};
var before=guardedTables.ToDictionary(x=>x,x=>Scalar("SELECT count(*) FROM "+x));
db.Initialize();db.Validate();
var unit=new SqliteLocalUnitOfWork(db);var clock=new SystemClock();var session=new UserSession();
var login=new IdentityService(unit,new LocalPasswords(),clock,session);
if(!login.Login("catalog-admin",password))throw new InvalidOperationException("Las credenciales no corresponden a la copia.");
var terminal=TerminalConfiguration.LoadOrCreate(target);var cash=new CashService(unit,session,clock);
var open=cash.Current(terminal.Id)?.Session??cash.Open(terminal.Id,new Money(50000)).Session;
var products=unit.Read(s=>s.QueryProducts(new(IncludeInactive:false,Page:0)).Items.Select(x=>x.Product).Take(3).ToArray());
if(products.Length<3)throw new InvalidDataException("La copia requiere tres productos activos para identidades históricas válidas.");

Sale synthetic=null!;
unit.Run(store=>
{
    var sequence=store.NextSaleSequence(terminal.Id);
    synthetic=new Sale(Guid.NewGuid(),Guid.NewGuid(),session.Current!.Id,terminal.Id,open.Id,sequence,clock.UtcNow,
    [
        new(products[0].Id,"Concha de vainilla",new Money(1300),2),
        new(products[1].Id,"Cuernito especial con almendra y azúcar",new Money(1500),2),
        new(products[2].Id,"Panadería corazón",new Money(1600),1)
    ],Guid.NewGuid(),new Money(10000));
    store.AddSale(synthetic);
    new CheckoutReceiptSnapshot(new PrintingConfigurationProvider(target),TimeZoneInfo.Local).Capture(store,synthetic);
    return true;
});

var detail=unit.Read(s=>s.FindSaleDetail(synthetic.Id))??throw new InvalidDataException("No se encontró la venta sintética.");
var snapshot=unit.Read(s=>s.FindReceiptSnapshot(synthetic.Id))??throw new InvalidDataException("No se capturó el snapshot.");
var document=ReceiptDocumentCodec.Deserialize(snapshot.ContentJson);
var renderer=new EscPosReceiptRenderer();
var originalPayload=renderer.Render(document,ReceiptCopyKind.Original,settings);
var reprintPayload=renderer.Render(document,ReceiptCopyKind.Reprint,settings);
File.WriteAllBytes(Path.Combine(target,"ticket-original-NO-ENVIADO.bin"),originalPayload);
File.WriteAllBytes(Path.Combine(target,"ticket-reimpresion-NO-ENVIADO.bin"),reprintPayload);
File.WriteAllText(Path.Combine(target,"ticket-original.txt"),new TextReceiptRenderer(48).Render(document,ReceiptCopyKind.Original),new UTF8Encoding(false));
File.WriteAllText(Path.Combine(target,"ticket-reimpresion.txt"),new TextReceiptRenderer(48).Render(document,ReceiptCopyKind.Reprint),new UTF8Encoding(false));

var printing=new PrintingConfigurationProvider(target).Read();
if(!printing.Available||printing.Settings is not {Enabled:true,QueueName:"POS-80",Columns:48,EncodingName:"ibm850",CharacterTable:2,CutMode:"Partial",OpenCashDrawer:false})
    throw new InvalidDataException("La configuración de impresión final no coincide.");
db.Validate();var after=guardedTables.ToDictionary(x=>x,x=>Scalar("SELECT count(*) FROM "+x));
foreach(var table in guardedTables)if(before[table]!=after[table])throw new InvalidDataException("Cambió una tabla protegida: "+table);
if(Hash(sourceDb)!=sourceHash)throw new InvalidDataException("Cambió la base protegida.");
if(Contains(originalPayload,[0x1b,0x70])||Contains(reprintPayload,[0x1b,0x70]))throw new InvalidDataException("Se detectó un pulso de cajón.");

var manifest=new
{
    source,target,sourceHash,sourceSchema,schema=Scalar("SELECT max(version) FROM schema_migration"),credentialVerified=true,terminal,
    sale=new{synthetic.Id,synthetic.Sequence,synthetic.Folio,totalCents=synthetic.Subtotal.Cents,receivedCents=synthetic.Payment.Received.Cents,changeCents=synthetic.Payment.Change.Cents,
        cashier=detail.CashierName,items=synthetic.Items.Select(x=>new{x.ProductName,x.Quantity,unitPriceCents=x.UnitPrice.Cents,lineTotalCents=x.LineTotal.Cents})},
    snapshot=snapshot.Sha256,printing=new{settings.Enabled,settings.QueueName,settings.Columns,settings.EncodingName,settings.CharacterTable,settings.CutMode,settings.OpenCashDrawer},
    payloads=new{original=new{bytes=originalPayload.Length,sha256=HashBytes(originalPayload)},reprint=new{bytes=reprintPayload.Length,sha256=HashBytes(reprintPayload)}},
    audit=new{codepage=Count(originalPayload,[0x1b,0x74,0x02]),alignLeft=Count(originalPayload,[0x1b,0x61,0x00]),alignCenter=Count(originalPayload,[0x1b,0x61,0x01]),
        logo=Count(originalPayload,OfficialReceiptLogo.Command()),socialLogos=Count(originalPayload,ReceiptSocialRaster.Command()),heart=Count(originalPayload,ReceiptHeartRaster.Command()),totalDoubleHeight=Count(originalPayload,[0x1d,0x21,0x01]),
        normalSize=Count(originalPayload,[0x1d,0x21,0x00]),partialCut=Count(originalPayload,[0x1d,0x56,0x01]),drawerKick=Count(originalPayload,[0x1b,0x70]),finalFeedBytes=6},
    guardedCountsBefore=before,guardedCountsAfter=after,noPhysicalPrintSubmitted=true,protectedHashUnchanged=true,databaseHash=Hash(db.FilePath)
};
File.WriteAllText(Path.Combine(target,"ticket-fixture-manifest.json"),JsonSerializer.Serialize(manifest,jsonOptions)+Environment.NewLine,new UTF8Encoding(false));
Console.WriteLine(JsonSerializer.Serialize(manifest,jsonOptions));

static int Count(byte[] source,byte[] sequence){var count=0;for(var i=0;i<=source.Length-sequence.Length;i++)if(source.AsSpan(i,sequence.Length).SequenceEqual(sequence)){count++;i+=sequence.Length-1;}return count;}
static bool Contains(byte[] source,byte[] sequence)=>Count(source,sequence)>0;
static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static string HashBytes(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
