using System.Globalization;
using System.IO;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualBasic.FileIO;

if(args.Length==3&&args[0]=="verify")
{
    var verifyTarget=Path.GetFullPath(args[1]);using var verifyLease=new DirectoryLease(verifyTarget);
    var verifyDatabase=new Database(Path.Combine(verifyTarget,"casamiga.db"));verifyDatabase.Validate();
    var verifyClock=new SystemClock();var verifyTerminal=TerminalConfiguration.LoadOrCreate(verifyTarget);var verifySession=new UserSession(new CheckoutJournal(verifyTarget));
    var verifyIdentity=new IdentityService(new SqliteLocalUnitOfWork(verifyDatabase),new LocalPasswords(),verifyClock,verifySession);
    if(!verifyIdentity.Login("catalog-admin",args[2]))throw new InvalidOperationException("Credencial smoke inválida.");
    var verifyCatalog=new CatalogService(new SqliteLocalUnitOfWork(verifyDatabase),verifySession,verifyClock,verifyTerminal.Id,true);
    var verifyProducts=verifyCatalog.Products(new("",null,true,0));var verifyPhotos=new SqliteProductPhotoStore(verifyDatabase).Active();
    Console.WriteLine(JsonSerializer.Serialize(new{Products=verifyProducts.Total,Categories=verifyCatalog.Categories(true).Count,Photos=verifyPhotos.Count,Provisional=verifyPhotos.Count(p=>p.Provenance=="PROVISIONAL"),Real=verifyPhotos.Count(p=>p.Provenance=="REAL")},new JsonSerializerOptions{WriteIndented=true}));return 0;
}
if (args.Length != 4 || args[0] != "prepare")
{
    Console.Error.WriteLine("Uso: prepare <paquete extraído> <directorio nuevo> <contraseña smoke> | verify <directorio> <contraseña smoke>"); return 2;
}
var package=Path.GetFullPath(args[1]);var target=Path.GetFullPath(args[2]);var password=args[3];
if(!Directory.Exists(package)||!Path.IsPathFullyQualified(target)||target.StartsWith(@"\\",StringComparison.Ordinal))throw new ArgumentException("Rutas locales requeridas.");
if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any())throw new InvalidOperationException("El dataset de smoke requiere una carpeta nueva y vacía.");
var rows=ReadAndValidate(package);Directory.CreateDirectory(target);
using var lease=new DirectoryLease(target);
var clock=new SystemClock();var options=new FoundationOptions(target,true);new FoundationRuntime(options).Start();
var database=new Database(Path.Combine(target,"casamiga.db"));var unit=new SqliteLocalUnitOfWork(database);
var terminal=TerminalConfiguration.LoadOrCreate(target);var journal=new CheckoutJournal(target);var session=new UserSession(journal);
var identity=new IdentityService(unit,new LocalPasswords(),clock,session);identity.Bootstrap("catalog-admin","Administrador catálogo",password);
if(!identity.Login("catalog-admin",password))throw new InvalidOperationException("No se pudo iniciar el usuario smoke.");
var catalog=new CatalogService(unit,session,clock,terminal.Id,true);
var categories=new Dictionary<string,Guid>(StringComparer.Ordinal);
foreach(var name in rows.Select(row=>row.Category).Distinct(StringComparer.Ordinal))
    categories.Add(name,catalog.CreateCategory(name,"Catálogo autorizado v2 · dataset controlado",categories.Count).Id);
var products=new Dictionary<string,Product>(StringComparer.Ordinal);
foreach(var row in rows)
{
    var product=catalog.CreateProduct(categories[row.Category],$"CM-{row.Id:000}",row.Name,row.Note,new Money(checked(row.PriceMxn*100)),false);
    products.Add(row.Name,product);
}
new CashService(unit,session,clock).Open(terminal.Id,new Money(0));
var photos=new ProductPhotoService(new SqliteProductPhotoStore(database),new ProductPhotoFileStore(target),new WpfProductPhotoProcessor(),session,clock,terminal.Id);
var mapping=new[]{
    new Map("Danés con crema pastelera","referencia_real_danes_crema_pastelera.png",new PhotoCrop(19,320,700)),
    new Map("Vaso de café","imagen_vaso_cafe_referencia.png",new PhotoCrop(165,105,460)),
    new Map("Leche Kirkland entera","imagen_leche_kirkland_entera_referencia.png",new PhotoCrop(190,110,430)),
    new Map("Leche Kirkland deslactosada light","imagen_leche_kirkland_deslactosada_light_referencia.png",new PhotoCrop(190,110,430))
};
foreach(var map in mapping)
{
    var product=products[map.Product];var file=Path.Combine(package,map.File);using var input=File.OpenRead(file);
    photos.Import(new(product.Id,0,Guid.NewGuid(),map.File,"PROVISIONAL",map.Crop),input);
}
using var schemaConnection=database.Open(true);using var schemaCommand=schemaConnection.CreateCommand();schemaCommand.CommandText="SELECT MAX(version) FROM schema_migration";var schema=Convert.ToInt32(schemaCommand.ExecuteScalar(),CultureInfo.InvariantCulture);
var result=new{Directory=target,User="catalog-admin",Products=products.Count,Categories=categories.Count,Provisional=mapping.Length,Real=0,WithoutPhoto=products.Count-mapping.Length,Schema=schema};
Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));return 0;

static IReadOnlyList<Row> ReadAndValidate(string package)
{
    var jsonPath=Path.Combine(package,"catalogo_casa_miga.json");var csvPath=Path.Combine(package,"catalogo_casa_miga.csv");
    using var document=JsonDocument.Parse(File.ReadAllText(jsonPath));
    var json=document.RootElement.GetProperty("productos").EnumerateArray().Select(item=>new Row(
        item.GetProperty("id").GetInt32(),item.GetProperty("nombre").GetString()!,item.GetProperty("precio_mxn").GetInt32(),item.GetProperty("categoria").GetString()!,
        item.TryGetProperty("nota",out var note)?note.GetString()??"":"")).OrderBy(row=>row.Id).ToArray();
    var csv=new List<Row>();using var parser=new TextFieldParser(csvPath){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true};parser.SetDelimiters(",");
    _=parser.ReadFields();while(!parser.EndOfData){var f=parser.ReadFields()!;if(f.All(string.IsNullOrWhiteSpace))continue;csv.Add(new(int.Parse(f[0],CultureInfo.InvariantCulture),f[1],int.Parse(f[2],CultureInfo.InvariantCulture),f[3],f.Length>4?f[4]:""));}
    var orderedCsv=csv.OrderBy(row=>row.Id).ToArray();
    if(json.Length!=26||!json.SequenceEqual(orderedCsv))throw new InvalidDataException("CSV y JSON no coinciden exactamente; carga detenida.");
    if(json.Select(row=>row.Id).Distinct().Count()!=26||json.Select(row=>row.Name).Distinct(StringComparer.Ordinal).Count()!=26)throw new InvalidDataException("Identidades duplicadas.");
    return json;
}
sealed record Row(int Id,string Name,int PriceMxn,string Category,string Note);
sealed record Map(string Product,string File,PhotoCrop Crop);
