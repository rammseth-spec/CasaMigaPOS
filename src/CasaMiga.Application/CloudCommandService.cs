using System.Text.Json.Nodes;
using CasaMiga.Domain;

namespace CasaMiga.Application;

/// <summary>Cambio hecho en la versión web para esta sucursal (docs/WEB.md).</summary>
public sealed record CloudCommand(Guid Id, long Sequence, string Kind, JsonObject Payload, Guid RequestedById, string RequestedByName, DateTimeOffset? RequestedAt = null);
public sealed record CloudCommandResult(Guid Id, long Sequence, string Status, string? Message)
{
    public bool Applied => Status == "applied";
}

/// <summary>
/// Lo que la caja usa para aplicar inventario, producción y fotos pedidos desde la web (docs/POS_WEB.md, fase 3).
/// Sin esto esos cambios se rechazan con aviso; los demás se aplican igual.
/// </summary>
public sealed record CloudCommandModules(IInventoryStore Inventory, ILocalReadUnitOfWork Security, IProductionUnitOfWork Production, Func<UserSession, ProductPhotoService> Photos);

/// <summary>
/// Aplica en esta caja los cambios hechos en la web, en orden y con las mismas reglas y permisos que en la pantalla:
/// actúa como quien lo pidió si existe aquí; si no (por ejemplo, el administrador de otra sucursal), como el administrador local.
/// Un cambio que las reglas rechazan queda registrado con el motivo; un error inesperado (disco, base) se reintenta después.
/// Aplicar dos veces el mismo cambio no repite nada.
/// </summary>
public sealed class CloudCommandService(ILocalUnitOfWork database, IClock clock, Guid terminalId, TimeZoneInfo timeZone, CloudCommandModules? modules = null)
{
    /// <summary>La operación de una producción pedida desde la web se deriva del cambio: reintentar no crea otra.</summary>
    public static readonly Guid WebProductionNamespace = new("3c0f6f1e-5d2a-5b8e-9a41-7e2b8c9d0f13");

    /// <summary>
    /// Desde dónde pedir cambios. Se relee un tramo ya aplicado porque dos cambios simultáneos en la web pueden hacerse visibles
    /// fuera de orden (el 42 antes que el 41); los ya aplicados se reconocen por su identificador y no se repiten.
    /// </summary>
    public const long Overlap = 20;
    public long LastSequence() => database.Run(store => store.LastCloudCommandSequence());
    public long PullCursor() => Math.Max(0, LastSequence() - Overlap);
    public IReadOnlyList<CloudCommandResult> Unreported() => database.Run(store => store.UnreportedCloudCommands());
    public void MarkReported(IReadOnlyCollection<Guid> ids) => database.Run(store => { store.MarkCloudCommandsReported(ids, clock.UtcNow); return true; });

    public bool IsKnown(Guid id) => database.Run(store => store.FindCloudCommand(id)) is not null;

    public CloudCommandResult Apply(CloudCommand command)
    {
        if (database.Run(store => store.FindCloudCommand(command.Id)) is { } done) return done;
        string? rejection = null;
        try
        {
            var actor = database.Run(store => Actor(store, command.RequestedById)) ?? throw new AccessDeniedException();
            var session = new UserSession();
            session.SignIn(new UserIdentity(actor.Id, actor.DisplayName));
            Execute(command, session);
        }
        catch (Exception error) when (error is AccessDeniedException or ArgumentException or CatalogException or ConsignmentException or InvalidCommandException or KeyNotFoundException
            or ProductPhotoException or ProductionDomainException or ConsumptionException or RecipeConcurrencyException
            or InventoryApplicationException { Error: not (InventoryError.ConcurrencyConflict or InventoryError.InconsistentBalance) }
            or ProductionException { Code: not (ProductionFailure.ConcurrencyConflict or ProductionFailure.RecoveryInconsistent) })
        {
            rejection = Describe(error);
        }
        var result = new CloudCommandResult(command.Id, command.Sequence, rejection is null ? "applied" : "rejected", rejection);
        database.Run(store => { store.AddCloudCommand(result, command.Kind, command.RequestedByName, clock.UtcNow); return true; });
        return result;
    }

    /// <summary>Quien pidió el cambio si existe aquí; si existe pero está desactivado, nadie (se rechaza); si no existe, el administrador local.</summary>
    private static LocalUser? Actor(ILocalStore store, Guid requestedBy)
    {
        if (store.FindUser(requestedBy) is { } same) return same.Active ? same : null;
        return store.ListUsers().FirstOrDefault(u => u.Role == "Administrator" && u.Active) is { } admin ? store.FindUser(admin.Id) : null;
    }

    private void Execute(CloudCommand command, UserSession session)
    {
        var p = command.Payload;
        var catalog = new CatalogService(database, session, clock, terminalId);
        var identity = new IdentityService(database, new NoPasswords(), clock, session);
        var trays = new ConsignmentService(database, session, clock, terminalId, timeZone);
        // Lo registrado en la ruta conserva la hora en que se hizo en el celular, aunque la caja lo aplique después (sin pasar de ahora).
        var onRoute = new ConsignmentService(database, session, new FixedClock(Earlier(command.RequestedAt, clock.UtcNow)), terminalId, timeZone);
        switch (command.Kind)
        {
            case "category.save":
            {
                var id = Id(p, "id");
                if (database.Run(store => store.FindCategory(id)) is null) catalog.CreateCategory(Text(p, "name"), Text(p, "description"), Int(p, "sortOrder"), id);
                else catalog.EditCategory(id, Text(p, "name"), Text(p, "description"), Int(p, "sortOrder"));
                if (p["active"] is not null) catalog.SetCategoryActive(id, Bool(p, "active"));
                break;
            }
            case "product.save":
            {
                var id = Id(p, "id");
                var sku = p["sku"] is null ? null : Text(p, "sku");
                var price = new Money(Long(p, "priceCents"));
                if (database.Run(store => store.FindProduct(id)) is null)
                    catalog.CreateProduct(Id(p, "categoryId"), sku, Text(p, "name"), Text(p, "description"), price, Bool(p, "trackInventory"), id);
                else catalog.EditProduct(id, Id(p, "categoryId"), sku, Text(p, "name"), Text(p, "description"), price, Bool(p, "trackInventory"));
                break;
            }
            case "product.active": catalog.SetProductActive(Id(p, "id"), Bool(p, "active")); break;
            case "user.create": identity.CreateUserWithHash(Id(p, "id"), Text(p, "username"), Text(p, "displayName"), Text(p, "role"), Text(p, "passwordHash")); break;
            case "user.update": identity.UpdateUser(Id(p, "id"), Text(p, "displayName"), Text(p, "role")); break;
            case "user.active": identity.SetActive(Id(p, "id"), Bool(p, "active")); break;
            case "user.reset_password": identity.ResetPasswordWithHash(Id(p, "id"), Text(p, "passwordHash")); break;
            case "consignment.store.save":
            {
                var id = Id(p, "id");
                var exists = database.Run(store => store.FindConsignmentStore(id)) is not null;
                trays.SaveStore(exists ? id : null, Text(p, "name"), Text(p, "contact"), Text(p, "phone"), Text(p, "address"), exists ? null : id);
                if (p["active"] is not null && database.Run(store => store.FindConsignmentStore(id))!.Active != Bool(p, "active")) trays.SetStoreActive(id, Bool(p, "active"));
                break;
            }
            case "consignment.deliver":
                onRoute.Deliver(Id(p, "storeId"), Lines(p, "lines"), Text(p, "note"), command.Id);
                break;
            case "consignment.collect":
                onRoute.Collect(Id(p, "deliveryId"),
                    (p["returned"] as JsonArray ?? throw new InvalidCommandException()).Select(r => r as JsonObject ?? throw new InvalidCommandException())
                        .ToDictionary(r => Int(r, "position"), r => Int(r, "quantity")),
                    new Money(Long(p, "paidCents")), Method(p), p["reference"] is null ? null : Text(p, "reference"), Text(p, "note"), command.Id);
                break;
            case "consignment.payment":
                onRoute.ReceivePayment(Id(p, "storeId"), new Money(Long(p, "amountCents")), Method(p), p["reference"] is null ? null : Text(p, "reference"), Text(p, "note"), command.Id);
                break;
            case "consignment.void":
                onRoute.Void(Id(p, "deliveryId"), Text(p, "reason"));
                break;
            // «Precios de charola» desde la web; sin precio, el pan ya no se puede poner en charolas. El mismo precio no cambia nada.
            case "consignment.price.save":
                foreach (var price in (p["prices"] as JsonArray ?? throw new InvalidCommandException()).Select(x => x as JsonObject ?? throw new InvalidCommandException()))
                    trays.SetPrice(Id(price, "productId"), price["priceCents"] is null ? null : new Money(Long(price, "priceCents")));
                break;
            case "inventory.cost":
                new IngredientCostService(database, session, clock, terminalId).SetCost(Id(p, "itemId"), new Money(Long(p, "costCents")), Long(p, "perAtomic"), Text(p, "unitLabel"));
                break;
            case "consignment.template.save":
                trays.SaveTemplate(Id(p, "storeId"), (p["lines"] as JsonArray ?? throw new InvalidCommandException())
                    .Select(l => new ConsignmentLine(Id(l as JsonObject ?? throw new InvalidCommandException(), "productId"), Int((JsonObject)l!, "quantity"))).ToList());
                break;
            // Inventario desde la web: misma operación que en la pantalla, con el identificador del cambio (reintentar no la repite).
            case "inventory.receive":
                Inventory(session).ReceiveInventory(new(command.Id, Id(p, "itemId"), Id(p, "locationId"), Quantity(p), Text(p, "unitCode"), Optional(p, "reference"), Optional(p, "note")));
                break;
            case "inventory.waste":
                Inventory(session).RecordWaste(new(command.Id, Id(p, "itemId"), Id(p, "locationId"), Quantity(p), Text(p, "unitCode"), Reason<WasteReason>(p), Optional(p, "note")));
                break;
            case "inventory.count":
            {
                var item = Id(p, "itemId"); var location = Id(p, "locationId");
                var store = Modules.Inventory;
                if (store.FindOperation(command.Id) is not null) break;
                // Sin saldo en esa ubicación, el primer conteo es el saldo inicial (mismo permiso que el ajuste: sólo el administrador).
                if (store.FindBalance(item, location) is null) Inventory(session).RegisterOpeningBalance(new(command.Id, item, location, Quantity(p), Text(p, "unitCode")));
                else Inventory(session).AdjustToPhysicalCount(new(command.Id, item, location, Quantity(p), Text(p, "unitCode"), Reason<AdjustmentReason>(p), Optional(p, "note")));
                break;
            }
            case "inventory.opening":
            {
                if (Modules.Inventory.FindOperation(command.Id) is not null) break;
                Inventory(session).RegisterOpeningBalance(new(command.Id, Id(p, "itemId"), Id(p, "locationId"), Quantity(p), Text(p, "unitCode"), Optional(p, "reference")));
                break;
            }
            // Artículos, presentaciones y asociaciones como en Inventario de la caja; si el cambio ya quedó (reintento), no se repite.
            case "inventory.item.save":
            {
                var id = Id(p, "id");
                if (Modules.Inventory.FindItem(id) is { } item) Inventory(session).UpdateInventoryItemMetadata(id, Text(p, "name"), Choice<InventoryItemKind>(p, "kind"), item.Dimension);
                else Inventory(session).CreateInventoryItem(Text(p, "code"), Text(p, "name"), Choice<InventoryItemKind>(p, "kind"), Choice<InventoryDimension>(p, "dimension"), id);
                break;
            }
            case "inventory.item.active":
                try { Inventory(session).SetInventoryItemActive(Id(p, "id"), Bool(p, "active")); }
                catch (InventoryApplicationException error) when (error.Error == InventoryError.InsufficientStock)
                { throw new ArgumentException("No se puede desactivar un artículo con existencia.", error); }
                break;
            case "inventory.unit.add":
            {
                var item = Id(p, "itemId"); var code = Text(p, "code"); var atomic = Long(p, "atomic");
                if (Modules.Inventory.FindItemUnit(item, code) is { } unit && unit.AtomicUnitsPerWholeUnit == atomic && unit.Name == Text(p, "name").Trim()) break;
                Inventory(session).AddInventoryItemUnit(item, code, Text(p, "name"), atomic);
                break;
            }
            case "inventory.link":
            {
                var product = Id(p, "productId"); var item = Id(p, "itemId"); var atomic = Long(p, "atomic");
                if (Modules.Inventory.FindActiveProductLink(product) is { } link && link.InventoryItemId == item && link.QuantityAtomic == atomic) break;
                Inventory(session).LinkProductToInventoryItem(product, item, atomic);
                break;
            }
            case "inventory.unlink":
            {
                var product = Id(p, "productId");
                if (Modules.Inventory.FindActiveProductLink(product) is null) break;
                Inventory(session).UnlinkProductFromInventoryItem(product);
                break;
            }
            // Consumo automático por ventas: la web manda el estado que vio; si otro lo cambió antes, se rechaza como en la caja.
            case "inventory.consumption":
            {
                var consumption = new SaleConsumptionService(database, Modules.Security, session, clock, terminalId);
                var current = consumption.GetStatus();
                var expected = new ConsumptionStatus(Choice<InventorySaleConsumptionState>(p, "state"), Long(p, "version"), current.UpdatedUtc, current.UpdatedBy);
                consumption.Change(Choice<ConsumptionAction>(p, "action"), expected);
                break;
            }
            case "production.record": RecordProduction(command, session); break;
            // Producción en dos pasos como la pantalla (W6): «Preparar registro de producción» deja el plan fijado como borrador…
            case "production.prepare":
            {
                var production = new ProductionService(Modules.Production, session, clock, terminalId);
                var batchId = Id(p, "batchId");
                var outputs = (p["outputs"] as JsonArray ?? throw new InvalidCommandException()).Select(o => o as JsonObject ?? throw new InvalidCommandException())
                    .Select(o => new RequestedProductionOutput(Id(o, "profileVersionId"), Long(o, "planned"))).ToArray();
                var selections = (p["selections"] as JsonArray ?? []).Select(o => o as JsonObject ?? throw new InvalidCommandException())
                    .Select(o => new ProductionAlternativeSelection(Id(o, "formulaVersionId"), Id(o, "groupId"), Id(o, "optionId"))).ToArray();
                var plan = new PrepareProductionCommand(outputs, selections);
                if (production.GetProduction(batchId) is null)
                {
                    var preview = production.PrepareProduction(plan);
                    if (!preview.CanConfirm)
                        throw new ProductionException(!preview.Planning.IsResolved ? ProductionFailure.UnresolvedPlan : preview.InactiveItems.Count > 0 ? ProductionFailure.InactiveItem
                            : preview.Shortages.Count > 0 ? ProductionFailure.InsufficientStock : ProductionFailure.NotInitialized, preview.Shortages);
                }
                production.CreateProduction(new(batchId, Id(p, "operationId"), plan, Optional(p, "notes")));
                break;
            }
            // …y «Registrar producción» lo confirma con las piezas que salieron (sólo quien lo creó, en esta caja).
            case "production.confirm":
            {
                var production = new ProductionService(Modules.Production, session, clock, terminalId);
                var batch = production.GetProduction(Id(p, "batchId")) ?? throw new ArgumentException("La producción ya no existe.");
                var actual = (p["actuals"] as JsonArray ?? throw new InvalidCommandException()).Select(o => o as JsonObject ?? throw new InvalidCommandException())
                    .Select(o => new ActualOutputLine(Id(o, "productId"), Long(o, "actual"))).ToArray();
                production.ConfirmProduction(new(batch.Id, batch.OperationId, ProductionContract.Revision(batch), actual));
                break;
            }
            case "recipe.formula.create": case "recipe.formula.draft": case "recipe.formula.publish": case "recipe.formula.activate":
            case "recipe.profile.create": case "recipe.profile.draft": case "recipe.profile.publish": case "recipe.profile.activate":
                Recipe(command, new RecipeService(Modules.Production, session, clock, terminalId));
                break;
            case "product.photo":
            {
                var product = Id(p, "productId");
                var photos = Modules.Photos(session);
                if (photos.Recover(product, command.Id) is not null) break;
                byte[] image;
                try { image = Convert.FromBase64String(Text(p, "image")); }
                catch (FormatException) { throw new InvalidCommandException(); }
                var edge = Int(p, "edge");
                // La web manda la foto ya recortada en cuadro (edge × edge); aquí se procesa igual que una importada en la caja.
                var provenance = Optional(p, "provenance") ?? "REAL";
                photos.Import(new PhotoImport(product, photos.Current(product).Revision, command.Id, Text(p, "name"), provenance, new PhotoCrop(0, 0, edge)), new MemoryStream(image, false));
                break;
            }
            case "product.photo.remove":
            {
                var product = Id(p, "productId");
                var photos = Modules.Photos(session);
                if (photos.Recover(product, command.Id) is not null) break;
                var current = photos.Current(product);
                // Sin foto no hay nada que quitar: el cambio queda aplicado sin tocar nada (como «Quitar foto» desactivado en la caja).
                if (current.Photo is not null) photos.Remove(product, current.Revision, command.Id);
                break;
            }
            default: throw new InvalidCommandException("Esta caja no reconoce ese cambio; actualiza Casa Miga.");
        }
    }

    private CloudCommandModules Modules => modules ?? throw new InvalidCommandException("Esta caja no puede aplicar ese cambio; actualiza Casa Miga.");
    private InventoryService Inventory(UserSession session) => new(Modules.Inventory, Modules.Security, session, clock, terminalId);

    /// <summary>
    /// Producción hecha: se prepara con las versiones publicadas que eligió la web, se fija y se registra con lo que salió, como en la pantalla
    /// (borrador y confirmación). Si falta existencia o algo está inactivo se rechaza antes de crear el borrador.
    /// </summary>
    private void RecordProduction(CloudCommand command, UserSession session)
    {
        var p = command.Payload;
        var outputs = (p["outputs"] as JsonArray ?? throw new InvalidCommandException()).Select(o => o as JsonObject ?? throw new InvalidCommandException())
            .Select(o => (Version: Id(o, "profileVersionId"), Planned: Long(o, "planned"), Actual: Long(o, "actual"))).ToArray();
        if (outputs.Length is 0 or > 20 || outputs.Select(o => o.Version).Distinct().Count() != outputs.Length || outputs.Any(o => o.Planned <= 0 || o.Actual < 0))
            throw new InvalidCommandException();
        var production = new ProductionService(Modules.Production, session, clock, terminalId);
        var plan = new PrepareProductionCommand(outputs.Select(o => new RequestedProductionOutput(o.Version, o.Planned)).ToArray(), []);
        var operation = SaleConsumptionIdentity.UuidV5(WebProductionNamespace, command.Id.ToString("D"));
        if (production.GetProduction(command.Id) is null)
        {
            var preview = production.PrepareProduction(plan);
            if (!preview.CanConfirm)
                throw new ProductionException(!preview.Planning.IsResolved ? ProductionFailure.UnresolvedPlan : preview.InactiveItems.Count > 0 ? ProductionFailure.InactiveItem
                    : preview.Shortages.Count > 0 ? ProductionFailure.InsufficientStock : ProductionFailure.NotInitialized, preview.Shortages);
        }
        var batch = production.CreateProduction(new(command.Id, operation, plan, Optional(p, "notes")));
        if (batch.Status == "CONFIRMED") return;
        var actual = batch.Outputs.Select(o => new ActualOutputLine(o.ProductId, outputs.Single(x => x.Version == o.ProfileVersionId).Actual)).ToArray();
        production.ConfirmProduction(new(batch.Id, batch.OperationId, ProductionContract.Revision(batch), actual));
    }

    /// <summary>
    /// Recetas y perfiles de producto como en la pantalla Recetas (docs/PLAN_COMPLETAR.md, W5): crear, guardar borradores, publicar y activar.
    /// La caja pone el número de versión y lee la dimensión de cada artículo o preparación; lo que ya quedó (reintento) no se repite.
    /// Lo que la base rechazaría (código repetido, artículo inexistente) se revisa antes, para que el cambio se rechace con su motivo.
    /// </summary>
    private void Recipe(CloudCommand command, RecipeService recipes)
    {
        var p = command.Payload;
        long? Revision() => p["revision"] is null ? null : Long(p, "revision");
        FormulaIngredient Line(JsonObject line)
        {
            var item = Modules.Inventory.FindItem(Id(line, "itemId"));
            if (item is not { IsActive: true }) throw new ArgumentException("Un ingrediente ya no existe o está inactivo en Inventario.");
            return new(item.Id, new(item.Dimension, Long(line, "atomic")), Choice<IngredientCountPolicy>(line, "policy"));
        }
        IEnumerable<JsonObject> Rows(JsonObject source, string name) => (source[name] as JsonArray ?? throw new InvalidCommandException())
            .Select(x => x as JsonObject ?? throw new InvalidCommandException());
        switch (command.Kind)
        {
            case "recipe.formula.create":
            {
                var id = Id(p, "id");
                if (recipes.GetFormula(id) is not null) break;
                var code = Text(p, "code");
                if (All(recipes.ListFormulas).Any(f => CatalogText.Key(f.Code) == CatalogText.Key(code))) throw new ArgumentException("Ya existe una receta con ese código.");
                recipes.CreateFormula(new Formula(id, code), Text(p, "name"));
                break;
            }
            case "recipe.formula.draft":
            {
                var formula = recipes.GetFormula(Id(p, "formulaId")) ?? throw new ArgumentException("La receta ya no existe.");
                var id = Id(p, "versionId"); var revision = Revision();
                var stored = recipes.GetFormulaVersion(id);
                if (revision is null && stored is not null) break;
                if (revision is not null && (stored is null || stored.Value.FormulaId != formula.Id || stored.Value.State != ProductionVersionState.Draft))
                    throw new RecipeConcurrencyException();
                var number = stored?.Value.Version ?? recipes.ListFormulaVersions(formula.Id).Select(v => v.Value.Version).DefaultIfEmpty(0).Max() + 1;
                var yield = p["yield"] as JsonObject ?? throw new InvalidCommandException();
                var groups = (p["groups"] as JsonArray ?? throw new InvalidCommandException()).Select(g => g as JsonArray ?? throw new InvalidCommandException())
                    .Select(options => options.Select(o => o as JsonObject ?? throw new InvalidCommandException()).Select(o => new IngredientAlternativeOption(Id(o, "id"), Line(o))).ToArray())
                    .Select(options => new IngredientAlternativeGroup(Guid.NewGuid(), options, options.Select(o => o.Id))).ToArray();
                recipes.SaveFormulaDraft(new FormulaVersion(id, formula.Id, number, Text(p, "name"), new(Choice<InventoryDimension>(yield, "dimension"), Long(yield, "atomic")),
                    Rows(p, "ingredients").Select(Line).ToArray(), groups, stored?.Value.CreatedAt ?? clock.UtcNow, Optional(p, "notes")), revision);
                break;
            }
            case "recipe.formula.publish":
            {
                var stored = recipes.GetFormulaVersion(Id(p, "versionId")) ?? throw new ArgumentException("La versión ya no existe.");
                if (stored.Value.State == ProductionVersionState.Published) break;
                recipes.PublishFormula(stored.Value.Id, Long(p, "revision"));
                break;
            }
            case "recipe.formula.activate":
            {
                var formula = recipes.GetFormula(Id(p, "formulaId")) ?? throw new ArgumentException("La receta ya no existe.");
                var version = Id(p, "versionId");
                if (formula.ActiveVersionId == version) break;
                if (recipes.GetFormulaVersion(version) is not { Value.State: ProductionVersionState.Published } stored || stored.Value.FormulaId != formula.Id)
                    throw new ArgumentException("Publica la versión antes de activarla.");
                recipes.ActivateFormula(formula.Id, version, Long(p, "revision"));
                break;
            }
            case "recipe.profile.create":
            {
                var id = Id(p, "id");
                if (recipes.GetProfile(id) is not null) break;
                var product = Id(p, "productId"); var item = Modules.Inventory.FindItem(Id(p, "finishedItemId"));
                if (database.Run(store => store.FindProduct(product)) is not { IsActive: true }) throw new ArgumentException("El producto ya no existe o está inactivo.");
                if (item is not { IsActive: true, Kind: InventoryItemKind.FinishedGood, Dimension: InventoryDimension.Count })
                    throw new ArgumentException("El artículo terminado debe ser un Producto terminado activo que se cuenta en piezas.");
                if (All(recipes.ListProfiles).Any(x => x.ProductId == product)) throw new ArgumentException("Ese producto ya tiene un perfil productivo.");
                recipes.CreateProfile(new ProductProductionProfile(id, product, item.Id));
                break;
            }
            case "recipe.profile.draft":
            {
                var profile = recipes.GetProfile(Id(p, "profileId")) ?? throw new ArgumentException("El perfil ya no existe.");
                var id = Id(p, "versionId"); var revision = Revision();
                var stored = recipes.GetProfileVersion(id);
                if (revision is null && stored is not null) break;
                if (revision is not null && (stored is null || stored.Value.ProfileId != profile.Id || stored.Value.State != ProductionVersionState.Draft))
                    throw new RecipeConcurrencyException();
                var number = stored?.Value.Version ?? recipes.ListProfileVersions(profile.Id).Select(v => v.Value.Version).DefaultIfEmpty(0).Max() + 1;
                var formulas = Rows(p, "formulas").Select(f =>
                {
                    var version = recipes.GetFormulaVersion(Id(f, "formulaVersionId"));
                    if (version is not { Value.State: ProductionVersionState.Published }) throw new ArgumentException("Una preparación ya no está publicada.");
                    return new FormulaRequirement(version.Value.Id, new(version.Value.BaseYield.Dimension, Long(f, "atomic")));
                }).ToArray();
                recipes.SaveProfileDraft(new ProductProductionProfileVersion(id, profile.Id, number, profile.ProductId, profile.FinishedItemId, Text(p, "name"), formulas,
                    Rows(p, "direct").Select(d => new DirectIngredientRequirement(Line(d))).ToArray(), stored?.Value.CreatedAt ?? clock.UtcNow), revision);
                break;
            }
            case "recipe.profile.publish":
            {
                var stored = recipes.GetProfileVersion(Id(p, "versionId")) ?? throw new ArgumentException("La versión ya no existe.");
                if (stored.Value.State == ProductionVersionState.Published) break;
                recipes.PublishProfile(stored.Value.Id, Long(p, "revision"));
                break;
            }
            case "recipe.profile.activate":
            {
                var profile = recipes.GetProfile(Id(p, "profileId")) ?? throw new ArgumentException("El perfil ya no existe.");
                var version = Id(p, "versionId");
                if (profile.ActiveVersionId == version) break;
                if (recipes.GetProfileVersion(version) is not { Value.State: ProductionVersionState.Published } stored || stored.Value.ProfileId != profile.Id)
                    throw new ArgumentException("Publica la versión antes de activarla.");
                recipes.ActivateProfile(profile.Id, version, Long(p, "revision"));
                break;
            }
        }
    }

    /// <summary>Todas las recetas o perfiles, de 200 en 200 (el máximo por consulta).</summary>
    private static List<T> All<T>(Func<int, int, IReadOnlyList<T>> page)
    {
        var all = new List<T>();
        for (var offset = 0; ; offset += 200) { var rows = page(200, offset); all.AddRange(rows); if (rows.Count < 200) return all; }
    }

    /// <summary>El motivo que ve la web cuando la caja rechaza un cambio.</summary>
    private string Describe(Exception error) => error switch
    {
        ProductionException { Code: ProductionFailure.InsufficientStock } e => "No hay existencia suficiente para registrar esta producción." +
            (e.Shortages.Count == 0 ? "" : " Falta: " + string.Join(", ", e.Shortages.Select(s => modules?.Inventory.FindItem(s.ItemId)?.Name ?? "un insumo")) + "."),
        ProductionException { Code: ProductionFailure.InactiveItem } => "Un insumo o producto terminado ya no está activo.",
        ProductionException { Code: ProductionFailure.NotInitialized } => "Un insumo o producto terminado todavía no tiene saldo inicial (haz el conteo inicial en Inventario).",
        ProductionException { Code: ProductionFailure.UnresolvedPlan } => "La receta tiene problemas pendientes y no puede registrarse.",
        ProductionException { Code: ProductionFailure.NotFound } => "La receta ya no existe o cambió de versión; actualiza la página.",
        ProductionException { Code: ProductionFailure.IdempotencyConflict } => "Sólo el usuario y terminal que crearon este borrador pueden registrarlo.",
        ProductionException { Code: ProductionFailure.RevisionConflict } => "La producción cambió o ya fue registrada desde otra operación. Actualiza la información.",
        ProductionException { Code: ProductionFailure.InvalidRequest } => "Captura las piezas reales de cada producto del plan.",
        ProductionException => "No se pudo registrar la producción con esos datos.",
        ProductionDomainException e => RecipeProblems.Describe(e),
        RecipeConcurrencyException => "La receta cambió. Actualiza la información antes de continuar.",
        ConsumptionException { Preflight: { } check } => "Corrige antes de activar o reanudar: " + string.Join(" ", check.Problems.Take(5).Select(ConsumptionProblems.Describe)) +
            (check.Problems.Count > 5 ? $" Y {check.Problems.Count - 5} más." : ""),
        ConsumptionException { Error: ConsumptionError.ConcurrencyConflict } => "Otro administrador cambió el estado del consumo automático. Actualiza y revisa antes de intentar nuevamente.",
        ConsumptionException => "Este cambio no está disponible desde el estado actual del consumo automático.",
        _ => error.Message,
    };


    private sealed class InvalidCommandException(string message = "Cambio con formato inválido.") : Exception(message);
    private sealed class FixedClock(DateTimeOffset at) : IClock { public DateTimeOffset UtcNow => at; }
    private static DateTimeOffset Earlier(DateTimeOffset? requested, DateTimeOffset now) => requested is { } at && at < now ? at.ToUniversalTime() : now;
    private static PaymentMethod Method(JsonObject p)
    {
        try { return Payment.Parse(Text(p, "method")); }
        catch (InvalidDataException) { throw new InvalidCommandException(); }
    }
    private static List<ConsignmentLine> Lines(JsonObject p, string name) =>
        (p[name] as JsonArray ?? throw new InvalidCommandException())
            .Select(l => l as JsonObject ?? throw new InvalidCommandException())
            .Select(l => new ConsignmentLine(Id(l, "productId"), Int(l, "quantity"))).ToList();
    /// <summary>Los cambios de la web nunca crean contraseñas aquí: ya traen su hash.</summary>
    private sealed class NoPasswords : IPasswords
    {
        public string Hash(string password) => throw new InvalidCommandException();
        public PasswordCheck Verify(string? hash, string password) => PasswordCheck.Failed;
    }

    private static string Text(JsonObject p, string name)
    {
        try { return p[name]?.GetValue<string>() ?? throw new InvalidCommandException(); }
        catch (Exception error) when (error is InvalidOperationException or FormatException) { throw new InvalidCommandException(); }
    }
    // Por texto JSON: igual si el valor llegó por red (JsonElement) o se armó en memoria (int, long, bool).
    private static long Long(JsonObject p, string name) =>
        p[name] is JsonValue value && long.TryParse(value.ToJsonString(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : throw new InvalidCommandException();
    private static int Int(JsonObject p, string name) => Long(p, name) is var v and >= int.MinValue and <= int.MaxValue ? (int)v : throw new InvalidCommandException();
    private static bool Bool(JsonObject p, string name) => (p[name] as JsonValue)?.ToJsonString() switch
    {
        "true" => true, "false" => false, _ => throw new InvalidCommandException(),
    };
    private static Guid Id(JsonObject p, string name) => Guid.TryParse(Text(p, name), out var id) && id != Guid.Empty ? id : throw new InvalidCommandException();
    private static string? Optional(JsonObject p, string name) => p[name] is null ? null : Text(p, name) is { Length: > 0 } value ? value : null;
    /// <summary>Cantidad como texto decimal («2.5»), para no perder exactitud en el camino; la caja la convierte a su unidad mínima.</summary>
    private static decimal Quantity(JsonObject p) =>
        decimal.TryParse(Text(p, "quantity"), System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : throw new InvalidCommandException();
    private static T Reason<T>(JsonObject p) where T : struct, Enum => Choice<T>(p, "reason");
    /// <summary>Valor de una enumeración por su nombre exacto («RawMaterial», «Pause»); nunca por número.</summary>
    private static T Choice<T>(JsonObject p, string name) where T : struct, Enum =>
        Enum.TryParse<T>(Text(p, name), false, out var value) && Enum.IsDefined(value) && !int.TryParse(Text(p, name), out _) ? value : throw new InvalidCommandException();
}
