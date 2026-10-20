// Recorrido completo de la PRIMERA receta real (polvorón) sobre una COPIA de la caja, nunca sobre la base real.
//   preparar  <copia>                                  deja en borrador la receta del polvorón (100 g de masa de polvorón por pieza)
//   recorrido <copia> <rinde g> <arriba|exacto> <naranja|vainilla> [piezas]
//             con el rendimiento MEDIDO de la media tanda: publica en la copia, pone existencias de prueba, produce y comprueba
//             que se descontaron los ingredientes y entró el pan. La contraseña del administrador de la copia va en CASAMIGA_COPIA_CLAVE.
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;

var mode = args[0]; var folder = Path.GetFullPath(args[1]);
if (folder.Contains(@"local-main-c2fb393\caja", StringComparison.OrdinalIgnoreCase) || folder.Contains(@"CasaMiga-Ventas\", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Esta herramienta sólo trabaja sobre copias, nunca sobre la caja real.");
using var lease = new DirectoryLease(folder);
var db = new Database(Path.Combine(folder, "casamiga.db")); db.Initialize(); db.Validate();
var terminal = TerminalConfiguration.LoadOrCreate(folder);
var clock = new SystemClock(); var user = new UserSession(); var local = new SqliteLocalUnitOfWork(db); var production = new SqliteProductionUnitOfWork(db);
var password = Environment.GetEnvironmentVariable("CASAMIGA_COPIA_CLAVE") ?? throw new InvalidOperationException("Falta CASAMIGA_COPIA_CLAVE.");
if (!new IdentityService(local, new LocalPasswords(), clock, user).Login("admin", password)) throw new InvalidOperationException("No se pudo entrar como admin en la copia.");
var recipes = new RecipeService(production, user, clock, terminal.Id);
var store = new SqliteInventoryStore(db, clock);
var inventory = new InventoryService(store, local, user, clock, terminal.Id);
var producing = new ProductionService(production, user, clock, terminal.Id);

Guid Single(string sql, string value)
{
    using var c = db.Open(true); using var q = c.CreateCommand(); q.CommandText = sql; q.Parameters.AddWithValue("$v", value);
    return Guid.Parse((string)(q.ExecuteScalar() ?? throw new InvalidOperationException($"No existe {value}.")));
}
var product = Single("SELECT id FROM product WHERE sku=$v", "CM-010");
var finished = Single("SELECT id FROM inventory_item WHERE code=$v", "PT-CM-010");
var dough = recipes.ListFormulas(200).Single(f => f.Code == "MASA-POLVORON");
var doughVersion = recipes.ListFormulaVersions(dough.Id).Single(v => v.Value.Version == 1);
var profile = recipes.ListProfiles(200).SingleOrDefault(p => p.ProductId == product);
if (profile is null) { recipes.CreateProfile(new ProductProductionProfile(Guid.NewGuid(), product, finished)); profile = recipes.ListProfiles(200).Single(p => p.ProductId == product); }
var profileVersion = recipes.ListProfileVersions(profile.Id).SingleOrDefault(v => v.Value.Version == 1);
if (profileVersion is null)
{
    recipes.SaveProfileDraft(new ProductProductionProfileVersion(Guid.NewGuid(), profile.Id, 1, product, finished, "Polvorón · 100 g de masa",
        [new FormulaRequirement(doughVersion.Value.Id, InventoryQuantity.From(100, InventoryUnits.Gram))], [], clock.UtcNow));
    profileVersion = recipes.ListProfileVersions(profile.Id).Single();
}
Console.WriteLine($"Receta del polvorón: {profileVersion.Value.State} · 100 g de «{dough.Name}» por pieza · masa {doughVersion.Value.State}, rinde {doughVersion.Value.BaseYield.AtomicUnits / 1000m:0.###} g");
if (mode == "preparar") return 0;
if (mode != "recorrido") throw new ArgumentException("Modo: preparar o recorrido.");

// 1. La masa con el rendimiento medido y las decisiones del panadero; después se publican masa y receta.
var measured = InventoryQuantity.From(decimal.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture), InventoryUnits.Gram);
var countPolicy = args[3] == "arriba" ? IngredientCountPolicy.CeilingWholeCount : IngredientCountPolicy.ExactWholeCount;
var flavor = args[4]; var pieces = args.Length > 5 ? long.Parse(args[5]) : 18;
var draft = doughVersion.Value;
FormulaIngredient Policy(FormulaIngredient i) => i.Quantity.Dimension == InventoryDimension.Count ? new(i.InventoryItemId, i.Quantity, countPolicy) : i;
var groups = draft.Alternatives.Select(g =>
{
    var options = g.Options.Select(o => new IngredientAlternativeOption(o.Id, Policy(o.Ingredient))).ToArray();
    var naranja = options.Single(o => o.Ingredient.Quantity.Dimension == InventoryDimension.Count).Id;
    var vainilla = options.Single(o => o.Ingredient.Quantity.Dimension != InventoryDimension.Count).Id;
    return new IngredientAlternativeGroup(g.Id, options, flavor == "naranja" ? [naranja, vainilla] : [vainilla, naranja]);
}).ToArray();
recipes.SaveFormulaDraft(new FormulaVersion(draft.Id, draft.FormulaId, draft.Version, draft.Name, measured, draft.Ingredients.Select(Policy), groups, draft.CreatedAt,
    $"Rendimiento medido de la media tanda: {measured.AtomicUnits / 1000m:0.###} g. Huevo y naranja: {(countPolicy == IngredientCountPolicy.CeilingWholeCount ? "redondear hacia arriba" : "sólo piezas enteras exactas")}. Sabor del día por omisión: {flavor}.", draft.Preparations), doughVersion.Revision);
recipes.PublishFormula(draft.Id, recipes.GetFormulaVersion(draft.Id)!.Revision);
recipes.ActivateFormula(dough.Id, draft.Id, recipes.GetFormula(dough.Id)!.Revision);
recipes.PublishProfile(profileVersion.Value.Id, recipes.GetProfileVersion(profileVersion.Value.Id)!.Revision);
recipes.ActivateProfile(profile.Id, profileVersion.Value.Id, recipes.GetProfile(profile.Id)!.Revision);

// 2. Existencias de PRUEBA, marcadas en su referencia; sólo existen en esta copia.
var published = recipes.GetFormulaVersion(draft.Id)!.Value;
var used = published.Ingredients.Select(i => i.InventoryItemId).Concat(published.Alternatives.SelectMany(g => g.Options).Select(o => o.Ingredient.InventoryItemId)).ToArray();
foreach (var item in used.Append(finished))
{
    var row = store.FindItem(item)!;
    var (amount, unit) = row.Dimension switch { InventoryDimension.Count => (item == finished ? 0m : 30m, "piece"), InventoryDimension.Volume => (1000m, "ml"), _ => (25000m, "g") };
    inventory.RegisterOpeningBalance(new(Guid.NewGuid(), item, InventoryLocationIds.Main, amount, unit, "PRUEBA · recorrido del polvorón en copia"));
}
long Stock(Guid item) => store.FindBalance(item, InventoryLocationIds.Main)?.QuantityAtomic ?? 0;
var before = used.Append(finished).ToDictionary(x => x, Stock);

// 3. Producción: preparar, borrador y confirmar con las piezas obtenidas.
var plan = new PrepareProductionCommand([new RequestedProductionOutput(profileVersion.Value.Id, pieces)], []);
var preview = producing.PrepareProduction(plan);
if (!preview.Planning.IsResolved) { Console.WriteLine("La receta no se puede calcular exacta: " + string.Join(", ", preview.Planning.Issues.Select(i => i.Code))); return 2; }
var batch = producing.CreateProduction(new CreateProductionCommand(Guid.NewGuid(), Guid.NewGuid(), plan, "PRUEBA · recorrido del polvorón en copia"));
var done = producing.ConfirmProduction(new ConfirmProductionCommand(batch.Id, batch.OperationId, ProductionContract.Revision(batch), [new ActualOutputLine(product, pieces)]));

// 4. Comprobación: cada ingrediente bajó exactamente lo planeado y entraron las piezas.
Console.WriteLine($"Producción {done.Status}: {pieces} polvorones con {preview.Planning.RequiredFormulas.Single().Required.AtomicUnits / 1000m:0.###} g de masa.");
var ok = true;
foreach (var need in preview.Planning.Plan!.Ingredients)
{
    var row = store.FindItem(need.InventoryItemId)!; var change = before[need.InventoryItemId] - Stock(need.InventoryItemId);
    ok &= change == need.Quantity.AtomicUnits;
    Console.WriteLine($"  {row.Name}: se descontó {InventoryDisplay(change, row.Dimension)} (planeado {InventoryDisplay(need.Quantity.AtomicUnits, row.Dimension)})");
}
var entered = Stock(finished) - before[finished]; ok &= entered == pieces;
Console.WriteLine($"  Polvorón terminado: entraron {entered} piezas.");
Console.WriteLine(ok ? "RECORRIDO COMPLETO: correcto." : "RECORRIDO COMPLETO: NO cuadra.");
return ok ? 0 : 1;

static string InventoryDisplay(long atomic, InventoryDimension dimension) => dimension switch
{
    InventoryDimension.Mass => $"{atomic / 1000m:0.###} g", InventoryDimension.Volume => $"{atomic / 1000m:0.###} ml", _ => $"{atomic} pz"
};
