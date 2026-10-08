using System.Text.Json;
using CasaMiga.Domain;

namespace CasaMiga.Application;

/// <summary>
/// Costo de referencia de un insumo para estimar el costo de cada pan desde su receta (docs/PROFIT.md).
/// Es una estimación para reportes: no cambia inventario, ventas ni caja.
/// </summary>
public sealed class IngredientCostService(ILocalUnitOfWork database, UserSession identity, IClock clock, Guid terminalId)
{
    public const string Permission = "inventory.configure";

    /// <param name="perAtomic">Unidades atómicas que cubre el costo (mg, µL o piezas), p. ej. 1 000 000 para 1 kg.</param>
    public void SetCost(Guid itemId, Money cost, long perAtomic, string unitLabel)
    {
        unitLabel = unitLabel.Trim();
        if (cost.Cents is < 0 or > 100_000_000) throw new ArgumentException("El costo debe estar entre $0 y $1,000,000.");
        if (perAtomic is < 1 or > 1_000_000_000_000) throw new ArgumentException("Cantidad de referencia inválida.");
        if (unitLabel.Length is < 1 or > 40) throw new ArgumentException("Escribe la unidad (hasta 40 caracteres).");
        database.Run(store =>
        {
            var actor = identity.Current is { } current && store.FindUser(current.Id) is { Active: true } user && store.HasPermission(user.Id, Permission)
                ? user : throw new AccessDeniedException();
            var at = clock.UtcNow;
            if (!store.SetInventoryItemCost(itemId, cost.Cents, perAtomic, unitLabel, at, actor.Id)) throw new ArgumentException("El insumo ya no existe.");
            store.AddAudit(new(Guid.NewGuid(), actor.Id, terminalId, "inventory.cost.set", itemId, at,
                JsonSerializer.Serialize(new { costCents = cost.Cents, perAtomic, unitLabel })));
            return true;
        });
    }
}
