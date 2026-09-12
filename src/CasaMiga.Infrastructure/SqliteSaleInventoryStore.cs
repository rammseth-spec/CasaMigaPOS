using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;

namespace CasaMiga.Infrastructure;

internal sealed record StoredSaleInventoryEvaluation(Guid Id, Guid SaleId, Guid CheckoutOperationId,
    SaleInventoryEvaluationStatus Status, Guid? InventoryOperationId, int PolicyVersion,
    string EvaluationSha256, DateTimeOffset ConfirmedAt);
internal sealed record StoredConsumptionConfig(InventorySaleConsumptionState State, long Version,
    DateTimeOffset UpdatedAt, Guid? UpdatedBy);

// Technical primitives only. No activation, authorization use case or independent transaction.
internal static class SqliteSaleInventoryStore
{
    internal static StoredConsumptionConfig ReadConfig(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var q = Command(connection, transaction,
            "SELECT state,version,updated_utc,updated_by FROM inventory_sale_consumption_config WHERE id=1");
        using var r = q.ExecuteReader();
        if (!r.Read()) throw new InvalidDataException("Falta configuración de consumo.");
        return new(Enum.Parse<InventorySaleConsumptionState>(r.GetString(0), true), r.GetInt64(1),
            DateTimeOffset.Parse(r.GetString(2), CultureInfo.InvariantCulture), r.IsDBNull(3) ? null : Guid.Parse(r.GetString(3)));
    }

    internal static StoredSaleInventoryEvaluation? Find(SqliteConnection connection,
        SqliteTransaction transaction, Guid checkoutOperationId)
    {
        using var q = Command(connection, transaction,
            "SELECT id,sale_id,status,inventory_operation_id,policy_version,evaluation_sha256,confirmed_utc FROM sale_inventory_evaluation WHERE checkout_operation_id=$id",
            ("$id", checkoutOperationId.ToString("D")));
        using var r = q.ExecuteReader();
        return !r.Read() ? null : new(Guid.Parse(r.GetString(0)), Guid.Parse(r.GetString(1)), checkoutOperationId,
            r.GetString(2) switch { "DISABLED" => SaleInventoryEvaluationStatus.Disabled,
                "NO_CONSUMPTION" => SaleInventoryEvaluationStatus.NoConsumption, "CONSUMED" => SaleInventoryEvaluationStatus.Consumed,
                _ => throw new InvalidDataException("Estado inválido.") },
            r.IsDBNull(3) ? null : Guid.Parse(r.GetString(3)), r.GetInt32(4), r.GetString(5),
            DateTimeOffset.Parse(r.GetString(6), CultureInfo.InvariantCulture));
    }

    internal static StoredSaleInventoryEvaluation Save(SqliteConnection connection, SqliteTransaction transaction,
        Sale sale, SaleInventoryConsumptionPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(sale);
        if (transaction.Connection != connection) throw new ArgumentException("La transacción no pertenece a la conexión.");
        var status = plan is null ? "DISABLED" : plan.RequiresConsumption ? "CONSUMED" : "NO_CONSUMPTION";
        var lines = plan?.Lines;
        if (lines is not null && (lines.Count != sale.Items.Count || lines.Where((l, i) =>
            l.Position != i + 1 || l.ProductId != sale.Items[i].ProductId || l.SoldQuantity != sale.Items[i].Quantity).Any()))
            throw new InventoryConsistencyException("El plan no corresponde a la venta completa.");
        if (plan?.Aggregates.Any(x => x.InventoryLocationId != SqliteInventoryStore.MainLocationId) == true)
            throw new InventoryConsistencyException("Sólo MAIN está habilitada.");
        // Sale has no commercial request hash column. Include immutable commercial content in
        // this versioned evaluation hash instead of changing historical sale storage.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            version = 1, policyVersion = SaleConsumptionIdentity.PolicyVersion, status,
            saleId = sale.Id, checkoutOperationId = sale.OperationId, actorId = sale.UserId,
            terminalId = sale.TerminalId, cashSessionId = sale.CashSessionId, sequence = sale.Sequence,
            confirmedUtc = sale.ConfirmedAt.ToString("O", CultureInfo.InvariantCulture),
            receivedCents = sale.Payment.Received.Cents,
            items = sale.Items.Select(x => new { productId = x.ProductId, name = x.ProductName,
                priceCents = x.UnitPrice.Cents, quantity = x.Quantity }).ToArray(),
            lines = lines?.Select(x => new { position = x.Position, productId = x.ProductId,
                soldQuantity = x.SoldQuantity, trackInventory = x.TrackInventory,
                linkId = x.ProductInventoryLinkId, itemId = x.InventoryItemId,
                locationId = x.InventoryLocationId, dimension = x.Dimension?.ToString().ToUpperInvariant(),
                factor = x.QuantityPerProductUnitAtomic, consumedAtomic = x.TotalConsumedAtomic }).ToArray()
        }))));
        var existing = Find(connection, transaction, sale.OperationId);
        if (existing is not null)
        {
            if (existing.SaleId != sale.Id || existing.EvaluationSha256 != hash)
                throw new InventoryIdempotencyConflictException();
            return existing;
        }
        // Savepoint prevents partial technical writes even if a caller catches an exception.
        const string savepoint = "sale_inventory_write";
        transaction.Save(savepoint);
        try
        {
            var id = sale.OperationId;
            Guid? operation = plan?.RequiresConsumption == true ? SaleConsumptionIdentity.OperationIdV1(sale.OperationId) : null;
            if (operation is { } operationId)
            {
                var request = SaleConsumptionOperationFactory.Create(operationId, sale.UserId, sale.TerminalId, plan!);
                SqliteInventoryStore.WriteOperation(connection, transaction, request, null, sale.ConfirmedAt, id);
            }
            if (lines is not null)
                foreach (var line in lines)
                {
                    using var q = Command(connection, transaction,
                        "INSERT INTO sale_inventory_line_snapshot VALUES($evaluation,$sale,$position,$product,$sold,$track,$link,$item,$location,$dimension,$factor,$total,1)",
                        ("$evaluation", id.ToString("D")), ("$sale", sale.Id.ToString("D")), ("$position", line.Position),
                        ("$product", line.ProductId.ToString("D")), ("$sold", line.SoldQuantity), ("$track", line.TrackInventory ? 1 : 0),
                        ("$link", line.ProductInventoryLinkId?.ToString("D")), ("$item", line.InventoryItemId?.ToString("D")),
                        ("$location", line.InventoryLocationId?.ToString("D")), ("$dimension", line.Dimension?.ToString().ToUpperInvariant()),
                        ("$factor", line.QuantityPerProductUnitAtomic), ("$total", line.TotalConsumedAtomic));
                    q.ExecuteNonQuery();
                }
            using (var q = Command(connection, transaction,
                "INSERT INTO sale_inventory_evaluation VALUES($id,$sale,$checkout,1,$status,$operation,1,$hash,$at)",
                ("$id", id.ToString("D")), ("$sale", sale.Id.ToString("D")), ("$checkout", sale.OperationId.ToString("D")),
                ("$status", status), ("$operation", operation?.ToString("D")), ("$hash", hash),
                ("$at", sale.ConfirmedAt.ToString("O", CultureInfo.InvariantCulture)))) q.ExecuteNonQuery();
            var result = Find(connection, transaction, sale.OperationId)!;
            transaction.Release(savepoint);
            return result;
        }
        catch
        {
            transaction.Rollback(savepoint);
            transaction.Release(savepoint);
            throw;
        }
    }

    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction tx, string sql,
        params (string Name, object? Value)[] values)
    {
        var q = new SqliteCommand(sql, c, tx);
        foreach (var (name, value) in values) q.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return q;
    }
}
