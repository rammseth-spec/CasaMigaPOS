using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed record InventoryItemCostRow(Guid ItemId, string Name, long? CostCents, long? PerAtomic, string? UnitLabel);

/// <summary>Costo estimado de una pieza desde su receta publicada (docs/PROFIT.md); null si falta la receta o el costo de algún insumo.</summary>
public sealed record ProductCostRow(Guid ProductId, string Name, Money? Cost, IReadOnlyList<string> Missing, bool HasRecipe)
{
    public string Pending => !HasRecipe ? "Receta" : Missing.Count > 0 ? string.Join(", ", Missing) : "Completo";
}
public sealed record ProfitProductRow(Guid ProductId, string Name, long CounterPieces, Money CounterRevenue, long TrayPieces, Money TrayRevenue, long Returned, Money? UnitCost)
{
    public Money? CounterProfit => UnitCost is { } c ? CounterRevenue - new Money(c.Cents * CounterPieces) : null;
    /// <summary>En charolas, lo que regresa también costó: se cuenta como costo perdido.</summary>
    public Money? TrayProfit => UnitCost is { } c ? TrayRevenue - new Money(c.Cents * (TrayPieces + Returned)) : null;
    public Money? PerPieceCounter => UnitCost is { } c && CounterPieces != 0 ? new Money(ProfitService.Round((double)CounterRevenue.Cents / CounterPieces) - c.Cents) : null;
    public Money? PerPieceTray => UnitCost is { } c && TrayPieces != 0 ? new Money(ProfitService.Round((double)TrayRevenue.Cents / TrayPieces) - c.Cents) : null;
    public Money? Total => UnitCost is null ? null : CounterProfit!.Value + TrayProfit!.Value;
}
/// <summary>Un canal (mostrador o charolas): la ganancia sólo cuenta la venta de productos con costo conocido.</summary>
public sealed record ProfitChannel(long Pieces, Money Revenue, Money CoveredRevenue, Money Cost)
{
    public Money Profit => CoveredRevenue - Cost;
    public decimal? Margin => CoveredRevenue.Cents == 0 ? null : (decimal)Profit.Cents / CoveredRevenue.Cents;
}
public sealed record ProfitReport(DateOnly From, DateOnly Through, ProfitChannel Counter, ProfitChannel Trays, long Returned, Money ReturnedCost,
    IReadOnlyList<ProfitProductRow> Products, IReadOnlyList<ProductCostRow> Costs);

/// <summary>
/// «Ganancia» (D1, igual que Reportes → Ganancia en la web): mostrador contra charolas en el periodo, con el costo de cada pan calculado desde
/// su receta y el costo de referencia de los insumos. Mostrador: lo vendido menos lo devuelto (por fecha, como el resto de los reportes de la caja);
/// charolas: lo cobrado de las recogidas en el periodo. Es una estimación: no cambia nada.
/// </summary>
public sealed class ProfitService(ILocalReadUnitOfWork database, IProductionUnitOfWork production, UserSession identity, TimeZoneInfo timeZone)
{
    public bool CanView() => database.Read(store => identity.Current is { } actor && store.FindUser(actor.Id) is { Active: true }
        && store.HasPermission(actor.Id, "sales.view") && store.HasPermission(actor.Id, "recipe.view"));

    internal static long Round(double value) => (long)Math.Floor(value + 0.5);

    public ProfitReport Profit(DateOnly from, DateOnly through)
    {
        if (from > through) throw new ReportException("La fecha Desde no puede ser posterior a Hasta.");
        if (through.DayNumber - from.DayNumber + 1 > ReportService.MaxDays) throw new ReportException($"El periodo máximo es de {ReportService.MaxDays} días.");
        if (!CanView()) throw new AccessDeniedException();
        var fromUtc = ToUtc(from); var untilUtc = ToUtc(through.AddDays(1));
        var (sales, deliveries, items, products) = database.Read(store =>
        {
            var all = new List<Product>();
            for (var page = 0; ; page++)
            {
                var result = store.QueryProducts(new CatalogQuery(IncludeInactive: true, Page: page));
                all.AddRange(result.Items.Select(x => x.Product));
                if (all.Count >= result.Total || result.Items.Count == 0) break;
            }
            return (store.QuerySalesReport(fromUtc, untilUtc), store.QueryConsignmentDeliveries(null, null, null, openOnly: false), store.InventoryItemCosts(), all);
        });
        var costs = ProductCosts(products, items);
        var unitCost = costs.ToDictionary(c => c.ProductId, c => c.Cost);
        var rows = new Dictionary<Guid, (string Name, long CounterPieces, long CounterRevenue, long TrayPieces, long TrayRevenue, long Returned)>();
        void Add(Guid id, string name, long counterPieces = 0, long counterRevenue = 0, long trayPieces = 0, long trayRevenue = 0, long returned = 0)
        {
            var r = rows.GetValueOrDefault(id, (name, 0, 0, 0, 0, 0));
            rows[id] = (name, r.CounterPieces + counterPieces, r.CounterRevenue + counterRevenue, r.TrayPieces + trayPieces, r.TrayRevenue + trayRevenue, r.Returned + returned);
        }
        foreach (var p in sales.Products) Add(p.ProductId, p.ProductName, p.QuantitySold - p.QuantityRefunded, p.Net.Cents);
        foreach (var d in deliveries.Where(d => d.Settlement is { } s && s.CollectedAt >= fromUtc && s.CollectedAt < untilUtc))
            foreach (var item in d.Items)
                if (d.Settlement!.Items.FirstOrDefault(s => s.Position == item.Position) is { } settled)
                    Add(item.ProductId, item.ProductName, trayPieces: settled.Sold, trayRevenue: settled.LineTotal.Cents, returned: settled.Returned);
        var list = rows.Select(r => new ProfitProductRow(r.Key, r.Value.Name, r.Value.CounterPieces, new(r.Value.CounterRevenue), r.Value.TrayPieces, new(r.Value.TrayRevenue),
                r.Value.Returned, unitCost.GetValueOrDefault(r.Key)))
            .OrderByDescending(p => p.CounterPieces + p.TrayPieces).ThenBy(p => p.Name, StringComparer.CurrentCulture).ToList();
        ProfitChannel Channel(Func<ProfitProductRow, long> pieces, Func<ProfitProductRow, long> revenue, Func<ProfitProductRow, long> cost)
        {
            var withCost = list.Where(p => p.UnitCost is not null).ToList();
            return new(list.Sum(pieces), new(list.Sum(revenue)), new(withCost.Sum(revenue)), new(withCost.Sum(cost)));
        }
        return new ProfitReport(from, through,
            Channel(p => p.CounterPieces, p => p.CounterRevenue.Cents, p => (p.UnitCost?.Cents ?? 0) * p.CounterPieces),
            Channel(p => p.TrayPieces, p => p.TrayRevenue.Cents, p => (p.UnitCost?.Cents ?? 0) * (p.TrayPieces + p.Returned)),
            list.Sum(p => p.Returned), new(list.Sum(p => (p.UnitCost?.Cents ?? 0) * p.Returned)), list,
            costs.OrderBy(c => c.Name, StringComparer.CurrentCulture).ToList());
    }

    /// <summary>
    /// Costo por pieza: versión activa del perfil (o la última publicada); ingredientes directos más cada preparación por su costo por unidad de lo que
    /// rinde (ingredientes y la opción preferida de cada grupo de alternativas). Si falta el costo de un insumo, el pan queda sin costo.
    /// </summary>
    private IReadOnlyList<ProductCostRow> ProductCosts(IReadOnlyList<Product> products, IReadOnlyList<InventoryItemCostRow> items)
    {
        var perAtomic = items.Where(i => i.CostCents is not null && i.PerAtomic is > 0).ToDictionary(i => i.ItemId, i => (double)i.CostCents!.Value / i.PerAtomic!.Value);
        var itemName = items.ToDictionary(i => i.ItemId, i => i.Name);
        double LineCost(IEnumerable<FormulaIngredient> lines, ISet<string> missing) => lines.Sum(l =>
        {
            if (perAtomic.TryGetValue(l.InventoryItemId, out var c)) return c * l.Quantity.AtomicUnits;
            missing.Add(itemName.GetValueOrDefault(l.InventoryItemId, "Insumo")); return 0;
        });
        return production.Run(s =>
        {
            var formulaCost = new Dictionary<Guid, (double Cost, HashSet<string> Missing)?>();
            (double Cost, HashSet<string> Missing)? Formula(Guid versionId)
            {
                if (formulaCost.TryGetValue(versionId, out var known)) return known;
                var version = s.Recipes.GetFormulaVersion(versionId)?.Value;
                (double, HashSet<string>)? value = null;
                if (version is not null)
                {
                    var missing = new HashSet<string>();
                    var preferred = version.Alternatives.Select(g => g.Options.First(o => o.Id == (g.PreferenceOrder.Count > 0 ? g.PreferenceOrder[0] : g.Options[0].Id)).Ingredient);
                    value = (LineCost(version.Ingredients.Concat(preferred), missing) / Math.Max(1, version.BaseYield.AtomicUnits), missing);
                }
                formulaCost[versionId] = value;
                return value;
            }
            var profiles = new List<ProfileSummary>();
            for (var offset = 0; ; offset += 200) { var page = s.Recipes.ListProfiles(200, offset); profiles.AddRange(page); if (page.Count < 200) break; }
            var byProduct = profiles.GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.First());
            return products.Select(product =>
            {
                if (!byProduct.TryGetValue(product.Id, out var profile)) return new ProductCostRow(product.Id, product.Name, null, [], false);
                var versions = s.Recipes.ListProfileVersions(profile.Id).Select(v => v.Value).Where(v => v.State == ProductionVersionState.Published).ToList();
                var version = versions.FirstOrDefault(v => v.Id == profile.ActiveVersionId) ?? versions.OrderByDescending(v => v.Version).FirstOrDefault();
                if (version is null) return new ProductCostRow(product.Id, product.Name, null, [], false);
                var missing = new HashSet<string>();
                var total = LineCost(version.DirectIngredients.Select(d => d.Ingredient), missing);
                foreach (var requirement in version.Formulas)
                {
                    if (Formula(requirement.FormulaVersionId) is not { } f) { missing.Add("Preparación"); continue; }
                    missing.UnionWith(f.Missing);
                    total += f.Cost * requirement.QuantityPerPiece.AtomicUnits;
                }
                var sorted = missing.OrderBy(m => m, StringComparer.CurrentCulture).ToList();
                return new ProductCostRow(product.Id, product.Name, sorted.Count > 0 ? null : new Money(Round(total)), sorted, true);
            }).ToList();
        });
    }

    private DateTimeOffset ToUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
    }
}
