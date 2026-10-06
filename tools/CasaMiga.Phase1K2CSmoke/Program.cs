using System.Globalization;
using System.IO;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualBasic.FileIO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if(args.Length>=3&&args[0]=="seed-review")return SeedReview.Run(args[1..]);
if(args.Length==3&&args[0]=="inventory-detail-probe")return InventoryDetailProbe.Run(args[1],args[2]);
if(args.Length==4&&args[0]=="checkout-evidence")return await R2CheckoutEvidence.Run(args[1],args[2],args[3]);
if(args.Length==4&&args[0]=="render-ui")return R2VisualEvidence.Run(args[1],args[2],args[3]);

if(args.Length==3&&args[0]=="verify")
{
    var verifyTarget=Path.GetFullPath(args[1]);using var verifyLease=new DirectoryLease(verifyTarget);
    var verifyDatabase=new Database(Path.Combine(verifyTarget,"casamiga.db"));verifyDatabase.Validate();
    var verifyClock=new SystemClock();var verifyTerminal=TerminalConfiguration.LoadOrCreate(verifyTarget);var verifySession=new UserSession(new CheckoutJournal(verifyTarget));
    var verifyIdentity=new IdentityService(new SqliteLocalUnitOfWork(verifyDatabase),new LocalPasswords(),verifyClock,verifySession);
    if(!verifyIdentity.Login("catalog-admin",args[2]))throw new InvalidOperationException("Credencial smoke inválida.");
    var verifyCatalog=new CatalogService(new SqliteLocalUnitOfWork(verifyDatabase),verifySession,verifyClock,verifyTerminal.Id,true);
    var verifyProducts=verifyCatalog.Products(new("",null,true,0));var verifyPhotos=new SqliteProductPhotoStore(verifyDatabase).Active();
    Console.WriteLine(JsonSerializer.Serialize(new{Products=verifyProducts.Total,Categories=verifyCatalog.Categories(true).Count,Photos=verifyPhotos.Count,Provisional=verifyPhotos.Count(p=>p.Provenance=="PROVISIONAL"),Real=verifyPhotos.Count(p=>p.Provenance=="REAL"),WithoutPhoto=verifyProducts.Total-verifyPhotos.Count},new JsonSerializerOptions{WriteIndented=true}));return 0;
}
if (args.Length != 5 || args[0] != "prepare")
{
    Console.Error.WriteLine("Uso: prepare <paquete extraído> <assets preparados nuevos> <dataset nuevo> <contraseña smoke> | verify <directorio> <contraseña smoke> | seed-review <catálogo csv> <dataset nuevo> [--photos <carpeta>] [--demo-days N]"); return 2;
}
var package=Path.GetFullPath(args[1]);var prepared=Path.GetFullPath(args[2]);var target=Path.GetFullPath(args[3]);var password=args[4];
if(!Directory.Exists(package)||!Path.IsPathFullyQualified(target)||target.StartsWith(@"\\",StringComparison.Ordinal))throw new ArgumentException("Rutas locales requeridas.");
if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any())throw new InvalidOperationException("El dataset de smoke requiere una carpeta nueva y vacía.");
if(Directory.Exists(prepared)&&Directory.EnumerateFileSystemEntries(prepared).Any())throw new InvalidOperationException("La carpeta de assets preparados debe ser nueva y vacía.");
var rows=ReadAndValidate(package);Directory.CreateDirectory(target);Directory.CreateDirectory(prepared);
Exception? preparationError=null;var preparationThread=new Thread(()=>{try{PrepareAssets(package,prepared,rows);}catch(Exception error){preparationError=error;}});preparationThread.SetApartmentState(ApartmentState.STA);preparationThread.Start();preparationThread.Join();if(preparationError is not null)throw new InvalidOperationException("No se pudieron preparar los assets provisionales.",preparationError);
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
foreach(var row in rows)
{
    var product=products[row.Name];var name=$"{row.Id:00}.png";using var input=File.OpenRead(Path.Combine(prepared,name));
    photos.Import(new(product.Id,0,Guid.NewGuid(),name,"PROVISIONAL",new PhotoCrop(0,0,512)),input);
}
using var schemaConnection=database.Open(true);using var schemaCommand=schemaConnection.CreateCommand();schemaCommand.CommandText="SELECT MAX(version) FROM schema_migration";var schema=Convert.ToInt32(schemaCommand.ExecuteScalar(),CultureInfo.InvariantCulture);
var result=new{Directory=target,PreparedAssets=prepared,User="catalog-admin",Products=products.Count,Categories=categories.Count,Provisional=rows.Count,Real=0,WithoutPhoto=0,Schema=schema};
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
static void PrepareAssets(string package,string output,IReadOnlyList<Row> rows)
{
    var sources=new[]{
        new AssetSource(1,"referencia_visual_catalogo.png",new(12,152,218,200)),new AssetSource(2,"referencia_visual_catalogo.png",new(245,152,232,200)),
        new AssetSource(3,"referencia_visual_catalogo.png",new(498,152,288,196)),new AssetSource(4,"referencia_visual_catalogo.png",new(806,152,281,203)),
        new AssetSource(5,"referencia_visual_catalogo.png",new(1106,152,255,205)),new AssetSource(6,"referencia_visual_catalogo.png",new(11,480,220,199)),
        new AssetSource(7,"referencia_visual_catalogo.png",new(251,480,206,196)),new AssetSource(8,"referencia_visual_catalogo.png",new(477,480,201,196)),
        new AssetSource(9,"referencia_visual_catalogo.png",new(702,481,200,195)),new AssetSource(10,"referencia_visual_catalogo.png",new(921,481,209,196)),
        new AssetSource(11,"referencia_visual_catalogo.png",new(1148,480,212,197)),new AssetSource(12,"referencia_visual_catalogo.png",new(12,790,209,201)),
        new AssetSource(13,"referencia_visual_catalogo.png",new(239,790,210,201)),new AssetSource(14,"referencia_visual_catalogo.png",new(469,790,208,199)),
        new AssetSource(15,"referencia_visual_catalogo.png",new(698,790,204,199)),new AssetSource(16,"referencia_visual_catalogo.png",new(923,790,211,201)),
        new AssetSource(17,"referencia_visual_catalogo.png",new(1150,790,210,201)),new AssetSource(18,"referencia_visual_catalogo_con_bebidas.png",new(204,686,180,145)),
        new AssetSource(19,"referencia_visual_catalogo_con_bebidas.png",new(395,686,178,145)),new AssetSource(20,"referencia_visual_catalogo_con_bebidas.png",new(963,95,178,166)),
        new AssetSource(21,"referencia_visual_catalogo_con_bebidas.png",new(1341,95,181,166)),new AssetSource(22,"referencia_visual_catalogo_con_bebidas.png",new(584,686,178,145)),
        new AssetSource(23,"referencia_visual_catalogo_con_bebidas.png",new(773,686,180,145)),new AssetSource(24,"imagen_vaso_cafe_referencia.png",new(165,105,460,460)),
        new AssetSource(25,"imagen_leche_kirkland_entera_referencia.png",new(190,110,430,430)),new AssetSource(26,"imagen_leche_kirkland_deslactosada_light_referencia.png",new(190,110,430,430))};
    if(sources.Length!=26||rows.Select(r=>r.Id).Except(sources.Select(s=>s.Id)).Any())throw new InvalidDataException("El mapeo provisional no cubre exactamente los 26 productos.");
    var mapping=new List<object>();
    foreach(var source in sources)
    {
        var input=Path.Combine(package,source.File);var decoder=BitmapDecoder.Create(new Uri(input),BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var frame=decoder.Frames.Single();
        if(source.Crop.X<0||source.Crop.Y<0||source.Crop.X+source.Crop.Width>frame.PixelWidth||source.Crop.Y+source.Crop.Height>frame.PixelHeight)throw new InvalidDataException($"Crop inválido: {source.File}");
        var crop=new CroppedBitmap(frame,source.Crop);var side=480d;var scale=Math.Min(side/crop.PixelWidth,side/crop.PixelHeight);var width=crop.PixelWidth*scale;var height=crop.PixelHeight*scale;
        var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(253,248,239)),null,new Rect(0,0,512,512));drawing.DrawImage(crop,new Rect((512-width)/2,(512-height)/2,width,height));}
        var target=new RenderTargetBitmap(512,512,96,96,PixelFormats.Pbgra32);target.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));var file=$"{source.Id:00}.png";using(var stream=File.Create(Path.Combine(output,file)))encoder.Save(stream);
        var row=rows.Single(r=>r.Id==source.Id);mapping.Add(new{row.Id,Product=row.Name,Source=source.File,Crop=new{source.Crop.X,source.Crop.Y,source.Crop.Width,source.Crop.Height},Asset=file,Provenance="PROVISIONAL"});
    }
    File.WriteAllText(Path.Combine(output,"mapping.json"),JsonSerializer.Serialize(mapping,new JsonSerializerOptions{WriteIndented=true}));
}
sealed record Row(int Id,string Name,int PriceMxn,string Category,string Note);
sealed record AssetSource(int Id,string File,Int32Rect Crop);
