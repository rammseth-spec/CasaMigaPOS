using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;

namespace CasaMiga.Infrastructure;

public class InventoryPersistenceException(string message, Exception? inner = null,
    InventoryError error = InventoryError.Conflict) : InventoryStoreException(error, message, inner);
public sealed class InventoryIdempotencyConflictException() : InventoryPersistenceException(
    "El OperationId ya corresponde a otro contenido.", error: InventoryError.IdempotencyConflict);
public sealed class InventoryStockException(string message, Exception? inner = null,
    InventoryError error = InventoryError.InvalidQuantity) : InventoryPersistenceException(message, inner, error);
public sealed class InventoryConsistencyException(string message,
    InventoryError error = InventoryError.InconsistentBalance) : InventoryPersistenceException(message, error: error);

public sealed partial class SqliteInventoryStore(Database database, IClock clock, Action? afterCommit = null) : IInventoryStore
{
    public static readonly Guid MainLocationId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    public void AddItem(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = Command(connection, transaction,
            "INSERT INTO inventory_item(id,code,name,kind,dimension,active,created_utc,modified_utc) VALUES($id,$code,$name,$kind,$dimension,$active,$created,$updated)",
            ("$id", Id(item.Id)), ("$code", item.Code), ("$name", item.Name), ("$kind", ItemKind(item.Kind)),
            ("$dimension", Dimension(item.Dimension)), ("$active", item.IsActive ? 1 : 0),
            ("$created", At(item.CreatedAt)), ("$updated", At(item.UpdatedAt)));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void AddItemUnit(InventoryItemUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = Command(connection, transaction,
            "INSERT INTO inventory_item_unit(id,item_id,code,name,dimension,atomic_units_per_whole_unit,active) VALUES($id,$item,$code,$name,$dimension,$factor,$active)",
            ("$id", Id(unit.Id)), ("$item", Id(unit.InventoryItemId)), ("$code", unit.Code), ("$name", unit.Name),
            ("$dimension", Dimension(unit.Dimension)), ("$factor", unit.AtomicUnitsPerWholeUnit), ("$active", unit.IsActive ? 1 : 0));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void AddProductLink(ProductInventoryLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var at = clock.UtcNow.ToUniversalTime();
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = Command(connection, transaction,
            "INSERT INTO product_inventory_link(id,product_id,inventory_item_id,dimension,quantity_per_product_unit_atomic,active,created_utc,modified_utc) VALUES($id,$product,$item,$dimension,$quantity,1,$at,$at)",
            ("$id", Id(Guid.NewGuid())), ("$product", Id(link.ProductId)), ("$item", Id(link.InventoryItemId)),
            ("$dimension", Dimension(link.QuantityPerProductUnit.Dimension)),
            ("$quantity", link.QuantityPerProductUnitAtomic), ("$at", At(at)));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public PersistedInventoryOperation Apply(InventoryOperationRequest request,
        InventoryBalanceExpectation? expectation = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind is InventoryMovementKind.SaleConsumption or InventoryMovementKind.ProductionConsumption or InventoryMovementKind.ProductionOutput)
            throw new InventoryConsistencyException("Use el camino interno de evaluación de venta.");
        var hash = RequestHash(request);
        try
        {
            using var connection = database.Open();
            using var transaction = connection.BeginTransaction(deferred: false);
            EnsureContext(connection, transaction, request.ActorId, request.TerminalId, request.Kind);
            var existing = ReadOperation(connection, transaction, request.OperationId);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestSha256, hash, StringComparison.Ordinal))
                    throw new InventoryIdempotencyConflictException();
                if (existing.Movements.Count == 0)
                    throw new InventoryConsistencyException("La operación existente no está confirmada.");
                transaction.Commit();
                afterCommit?.Invoke();
                return existing;
            }

            var result = WriteOperation(connection, transaction, request, expectation, clock.UtcNow.ToUniversalTime());
            transaction.Commit();
            afterCommit?.Invoke();
            return result;
        }
        catch
        {
            var recovered = Recover(request);
            if (recovered is not null) return recovered;
            throw;
        }
    }

    // Caller owns commit/rollback; this is the only ledger and balance writer.
    internal static PersistedInventoryOperation WriteOperation(SqliteConnection connection,
        SqliteTransaction transaction, InventoryOperationRequest request,
        InventoryBalanceExpectation? expectation, DateTimeOffset at, Guid? evaluationId = null, Guid? productionBatchId = null)
    {
        if ((request.Kind == InventoryMovementKind.SaleConsumption) != evaluationId.HasValue)
            throw new InventoryConsistencyException("Origen de consumo inválido.");
        if ((request.Kind is InventoryMovementKind.ProductionConsumption or InventoryMovementKind.ProductionOutput) != productionBatchId.HasValue)
            throw new InventoryConsistencyException("Origen de producción inválido.");
        var hash = RequestHash(request);
        if (expectation is not null)
        {
            if (request.Kind is not (InventoryMovementKind.AdjustmentGain or InventoryMovementKind.AdjustmentLoss) ||
                request.Lines.Count != 1 || request.Lines[0].InventoryItemId != expectation.InventoryItemId ||
                request.Lines[0].InventoryLocationId != expectation.InventoryLocationId)
                throw new InventoryConsistencyException("La precondición no corresponde al ajuste físico.");
            var actual = ReadBalance(connection, transaction, expectation.InventoryItemId,
                expectation.InventoryLocationId);
            if (actual is null || actual.QuantityAtomic != expectation.QuantityAtomic ||
                actual.Version != expectation.Version)
                throw new InventoryConsistencyException("El saldo cambió durante el conteo físico.", InventoryError.ConcurrencyConflict);
        }


        using (var pending = Command(connection, transaction,
            "INSERT INTO inventory_operation(id,request_sha256,kind,state,actor_id,terminal_id,reason_code,note,external_reference,expected_line_count,created_utc"
            + (evaluationId.HasValue ? ",sale_evaluation_id" : "") + (productionBatchId.HasValue ? ",production_batch_id,production_batch_status" : "")
            + ") VALUES($id,$hash,$kind,'PENDING',$actor,$terminal,$reason,$note,$reference,$count,$at"
            + (evaluationId.HasValue ? ",$evaluation" : "") + (productionBatchId.HasValue ? ",$batch,'CONFIRMED')" : ")"),
            ("$batch", productionBatchId?.ToString("D")),
            ("$evaluation", evaluationId?.ToString("D")), ("$id", Id(request.OperationId)), ("$hash", hash), ("$kind", MovementKind(request.Kind)),
            ("$actor", Id(request.ActorId)), ("$terminal", Id(request.TerminalId)), ("$reason", request.ReasonCode),
            ("$note", request.Note), ("$reference", request.ExternalReference), ("$count", request.Lines.Count), ("$at", At(at))))
            pending.ExecuteNonQuery();

        var movements = new List<PersistedInventoryMovement>();
        int sequence = 0;
        foreach (var line in request.Lines.OrderBy(x => x.InventoryLocationId).ThenBy(x => x.InventoryItemId))
        {
            ValidateLineReferences(connection, transaction, line);
            var current = ReadBalance(connection, transaction, line.InventoryItemId, line.InventoryLocationId);
            long before;
            long after;
            long version;
            if (line.IsOpening)
            {
                if (current is not null) throw new InventoryStockException("La posición de inventario ya fue inicializada.", error: InventoryError.AlreadyInitialized);
                before = 0;
                after = line.DeltaAtomic;
                version = 1;
            }
            else
            {
                if (current is null) throw new InventoryStockException("La posición de inventario no tiene saldo inicial.", error: InventoryError.NotInitialized);
                if (current.Dimension != line.Dimension)
                    throw new InventoryConsistencyException("La dimensión del saldo no coincide con la partida.");
                before = current.QuantityAtomic;
                try { after = checked(before + line.DeltaAtomic); }
                catch (OverflowException error) { throw new InventoryStockException("La operación excede el rango de cantidad permitido.", error); }
                if (after < 0) throw new InventoryStockException("La operación dejaría existencia negativa.", error: InventoryError.InsufficientStock);
                version = checked(current.Version + 1);
            }

            var movement = new PersistedInventoryMovement(Guid.NewGuid(), ++sequence, line.InventoryItemId,
                line.InventoryLocationId, line.Dimension, line.IsOpening, line.DeltaAtomic, before, after,
                line.Capture.UnitCode, line.Capture.NormalizedQuantity, line.Capture.AtomicUnitsPerUnit);
            InsertMovement(connection, transaction, request.OperationId, movement);
            if (line.IsOpening)
                InsertBalance(connection, transaction, movement, version, at);
            else
                UpdateBalance(connection, transaction, movement, current!.Version, version, at);
            movements.Add(movement);
        }

        using (var audit = Command(connection, transaction,
            "INSERT INTO audit_entry VALUES($id,$actor,$terminal,'inventory.operation.confirmed',$operation,$at,$details)",
            ("$id", Id(Guid.NewGuid())), ("$actor", Id(request.ActorId)), ("$terminal", Id(request.TerminalId)),
            ("$operation", Id(request.OperationId)), ("$at", At(at)),
            ("$details", JsonSerializer.Serialize(new { requestSha256 = hash, kind = MovementKind(request.Kind), lineCount = movements.Count }))))
            audit.ExecuteNonQuery();
        using (var confirm = Command(connection, transaction,
            "UPDATE inventory_operation SET state='CONFIRMED',confirmed_utc=$at WHERE id=$id AND state='PENDING'",
            ("$at", At(at)), ("$id", Id(request.OperationId))))
            if (confirm.ExecuteNonQuery() != 1) throw new InventoryConsistencyException("No se pudo confirmar la operación.");

        return new(request.OperationId, hash, request.Kind, request.ActorId, request.TerminalId,
            request.ReasonCode, request.Note, request.ExternalReference, at, movements.AsReadOnly());
    }
    public PersistedInventoryOperation? Recover(InventoryOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var expectedHash = RequestHash(request);
        using var connection = database.Open(readOnly: true);
        var operation = ReadOperation(connection, null, request.OperationId);
        if (operation is null) return null;
        if (!string.Equals(operation.RequestSha256, expectedHash, StringComparison.Ordinal))
            throw new InventoryIdempotencyConflictException();
        if (operation.Movements.Count == 0)
            throw new InventoryConsistencyException("Se encontró una operación PENDING fuera de su transacción.");
        return operation;
    }

    public PersistedInventoryOperation? FindOperation(Guid operationId)
    {
        using var connection = database.Open(readOnly: true);
        return ReadOperation(connection, null, operationId);
    }

    public InventoryBalanceRecord? FindBalance(Guid itemId, Guid locationId)
    {
        using var connection = database.Open(readOnly: true);
        return ReadBalance(connection, null, itemId, locationId);
    }

    public IReadOnlyList<InventoryReconciliationDifference> Reconcile()
    {
        using var connection = database.Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        return Reconcile(connection,transaction);
    }
    internal static IReadOnlyList<InventoryReconciliationDifference> Reconcile(SqliteConnection connection,SqliteTransaction transaction)
    {
        var ledger = new Dictionary<(Guid Item, Guid Location), long>();
        using (var command = Command(connection, transaction,
            "SELECT m.item_id,m.location_id,m.delta_atomic FROM inventory_movement m JOIN inventory_operation o ON o.id=m.operation_id WHERE o.state='CONFIRMED' ORDER BY m.item_id,m.location_id,o.confirmed_utc,o.id,m.sequence"))
        using (var rows = command.ExecuteReader())
            while (rows.Read())
            {
                var key = (Guid.Parse(rows.GetString(0)), Guid.Parse(rows.GetString(1)));
                ledger[key] = checked(ledger.GetValueOrDefault(key) + rows.GetInt64(2));
            }
        var balances = new Dictionary<(Guid Item, Guid Location), long>();
        using (var command = Command(connection, transaction, "SELECT item_id,location_id,quantity_atomic FROM inventory_balance"))
        using (var rows = command.ExecuteReader())
            while (rows.Read()) balances[(Guid.Parse(rows.GetString(0)), Guid.Parse(rows.GetString(1)))] = rows.GetInt64(2);
        return ledger.Keys.Union(balances.Keys).OrderBy(x => x.Location).ThenBy(x => x.Item)
            .Where(key => !balances.TryGetValue(key, out var balance) || !ledger.TryGetValue(key, out var sum) || balance != sum)
            .Select(key => new InventoryReconciliationDifference(key.Item, key.Location,
                ledger.GetValueOrDefault(key), balances.TryGetValue(key, out var value) ? value : null)).ToArray();
    }

    public static string RequestHash(InventoryOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CanonicalRequestHash(request.OperationId,request.ActorId,request.TerminalId,MovementKind(request.Kind),request.ReasonCode,request.Note,request.ExternalReference,request.Lines.OrderBy(x => x.InventoryLocationId).ThenBy(x => x.InventoryItemId).Select(x => new
            {
                itemId = x.InventoryItemId.ToString("D"),
                locationId = x.InventoryLocationId.ToString("D"),
                dimension = Dimension(x.Dimension),
                isOpening = x.IsOpening,
                deltaAtomic = x.DeltaAtomic,
                captureUnitCode = x.Capture.UnitCode,
                capturedQuantity = x.Capture.NormalizedQuantity,
                captureAtomicFactor = x.Capture.AtomicUnitsPerUnit
            }).ToArray());
    }
    internal static string CanonicalRequestHash(Guid operation,Guid actor,Guid terminal,string kind,
        string? reasonCode,string? note,string? externalReference,object lines)
    {
        var canonical=new {version=1,operationId=operation.ToString("D"),actorId=actor.ToString("D"),
            terminalId=terminal.ToString("D"),kind,reasonCode,note,externalReference,lines};
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
    }
    private static void EnsureContext(SqliteConnection connection, SqliteTransaction transaction, Guid actor,
        Guid terminal, InventoryMovementKind kind)
    {
        var permission = kind switch
        {
            InventoryMovementKind.Receipt => "inventory.receive",
            InventoryMovementKind.Waste => "inventory.waste",
            InventoryMovementKind.OpeningBalance or InventoryMovementKind.AdjustmentGain or
                InventoryMovementKind.AdjustmentLoss => "inventory.adjust",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        using var command = Command(connection, transaction,
            "SELECT EXISTS(SELECT 1 FROM app_user u JOIN role_permission rp ON rp.role_name=u.role_name WHERE u.id=$actor AND u.active=1 AND rp.permission_name=$permission"
            + " AND ($opening=0 OR u.role_name='Administrator')) AND EXISTS(SELECT 1 FROM terminal WHERE id=$terminal)",
            ("$actor", Id(actor)), ("$terminal", Id(terminal)), ("$permission", permission),
            ("$opening", kind == InventoryMovementKind.OpeningBalance ? 1 : 0));
        if (Convert.ToInt64(command.ExecuteScalar()) != 1)
            throw new InventoryPersistenceException("Usuario, terminal y permiso de inventario válidos son obligatorios.");
    }

    private static void ValidateLineReferences(SqliteConnection connection, SqliteTransaction transaction, InventoryOperationLine line)
    {
        using (var item = Command(connection, transaction, "SELECT dimension,active FROM inventory_item WHERE id=$id", ("$id", Id(line.InventoryItemId))))
        using (var row = item.ExecuteReader())
        {
            if (!row.Read() || row.GetInt32(1) != 1) throw new InventoryPersistenceException("El artículo de inventario no existe o está inactivo.");
            if (!string.Equals(row.GetString(0), Dimension(line.Dimension), StringComparison.Ordinal))
                throw new InventoryPersistenceException("La dimensión del artículo no coincide con la partida.");
        }
        using (var location = Command(connection, transaction, "SELECT active FROM inventory_location WHERE id=$id", ("$id", Id(line.InventoryLocationId))))
            if (!Equals(location.ExecuteScalar(), 1L)) throw new InventoryPersistenceException("La ubicación no existe o está inactiva.");
        using var unit = Command(connection, transaction,
            "SELECT dimension,atomic_units_per_unit FROM measurement_unit WHERE code=$code UNION ALL SELECT dimension,atomic_units_per_whole_unit FROM inventory_item_unit WHERE item_id=$item AND code=$code AND active=1",
            ("$code", line.Capture.UnitCode), ("$item", Id(line.InventoryItemId)));
        using var units = unit.ExecuteReader();
        if (!units.Read() || !string.Equals(units.GetString(0), Dimension(line.Dimension), StringComparison.Ordinal)
            || units.GetInt64(1) != line.Capture.AtomicUnitsPerUnit || units.Read())
            throw new InventoryPersistenceException("La unidad de captura no es válida para el artículo.");
    }

    private static void InsertMovement(SqliteConnection connection, SqliteTransaction transaction,
        Guid operationId, PersistedInventoryMovement movement)
    {
        using var command = Command(connection, transaction,
            "INSERT INTO inventory_movement(id,operation_id,sequence,item_id,location_id,dimension,is_opening,delta_atomic,balance_before_atomic,balance_after_atomic,capture_unit_code,captured_quantity,capture_atomic_factor) VALUES($id,$operation,$sequence,$item,$location,$dimension,$opening,$delta,$before,$after,$unit,$captured,$factor)",
            ("$id", Id(movement.Id)), ("$operation", Id(operationId)), ("$sequence", movement.Sequence),
            ("$item", Id(movement.InventoryItemId)), ("$location", Id(movement.InventoryLocationId)),
            ("$dimension", Dimension(movement.Dimension)), ("$opening", movement.IsOpening ? 1 : 0),
            ("$delta", movement.DeltaAtomic), ("$before", movement.BalanceBeforeAtomic),
            ("$after", movement.BalanceAfterAtomic), ("$unit", movement.CaptureUnitCode),
            ("$captured", movement.CapturedQuantity), ("$factor", movement.CaptureAtomicFactor));
        command.ExecuteNonQuery();
    }

    private static void InsertBalance(SqliteConnection connection, SqliteTransaction transaction,
        PersistedInventoryMovement movement, long version, DateTimeOffset at)
    {
        using var command = Command(connection, transaction,
            "INSERT INTO inventory_balance(item_id,location_id,dimension,quantity_atomic,version,updated_utc) VALUES($item,$location,$dimension,$quantity,$version,$at)",
            ("$item", Id(movement.InventoryItemId)), ("$location", Id(movement.InventoryLocationId)),
            ("$dimension", Dimension(movement.Dimension)), ("$quantity", movement.BalanceAfterAtomic),
            ("$version", version), ("$at", At(at)));
        try { command.ExecuteNonQuery(); }
        catch (SqliteException error) when (error.SqliteExtendedErrorCode == 1555 || error.SqliteExtendedErrorCode == 2067)
        { throw new InventoryStockException("La posición de inventario ya fue inicializada.", error: InventoryError.AlreadyInitialized); }
    }

    private static void UpdateBalance(SqliteConnection connection, SqliteTransaction transaction,
        PersistedInventoryMovement movement, long expectedVersion, long nextVersion, DateTimeOffset at)
    {
        using var command = Command(connection, transaction,
            "UPDATE inventory_balance SET quantity_atomic=$after,version=$next,updated_utc=$at WHERE item_id=$item AND location_id=$location AND quantity_atomic=$before AND version=$expected",
            ("$after", movement.BalanceAfterAtomic), ("$next", nextVersion), ("$at", At(at)),
            ("$item", Id(movement.InventoryItemId)), ("$location", Id(movement.InventoryLocationId)),
            ("$before", movement.BalanceBeforeAtomic), ("$expected", expectedVersion));
        if (command.ExecuteNonQuery() != 1)
            throw new InventoryConsistencyException("El saldo cambió durante la operación.", InventoryError.ConcurrencyConflict);
    }

    private static InventoryBalanceRecord? ReadBalance(SqliteConnection connection, SqliteTransaction? transaction,
        Guid itemId, Guid locationId)
    {
        using var command = Command(connection, transaction,
            "SELECT dimension,quantity_atomic,version,updated_utc FROM inventory_balance WHERE item_id=$item AND location_id=$location",
            ("$item", Id(itemId)), ("$location", Id(locationId)));
        using var row = command.ExecuteReader();
        return row.Read() ? new(itemId, locationId, ParseDimension(row.GetString(0)), row.GetInt64(1),
            row.GetInt64(2), ParseAt(row.GetString(3))) : null;
    }

    internal static PersistedInventoryOperation? ReadOperation(SqliteConnection connection,
        SqliteTransaction? transaction, Guid operationId)
    {
        string hash, kind; Guid actor, terminal; string? reason, note, reference; DateTimeOffset? confirmed;
        using (var command = Command(connection, transaction,
            "SELECT request_sha256,kind,state,actor_id,terminal_id,reason_code,note,external_reference,confirmed_utc FROM inventory_operation WHERE id=$id",
            ("$id", Id(operationId))))
        using (var row = command.ExecuteReader())
        {
            if (!row.Read()) return null;
            hash = row.GetString(0); kind = row.GetString(1); actor = Guid.Parse(row.GetString(3)); terminal = Guid.Parse(row.GetString(4));
            reason = row.IsDBNull(5) ? null : row.GetString(5); note = row.IsDBNull(6) ? null : row.GetString(6);
            reference = row.IsDBNull(7) ? null : row.GetString(7);
            confirmed = row.IsDBNull(8) ? null : ParseAt(row.GetString(8));
            if (!string.Equals(row.GetString(2), "CONFIRMED", StringComparison.Ordinal) || confirmed is null)
                return new(operationId, hash, ParseMovementKind(kind), actor, terminal, reason, note, reference,
                    DateTimeOffset.MinValue, Array.Empty<PersistedInventoryMovement>());
        }
        var movements = new List<PersistedInventoryMovement>();
        using (var command = Command(connection, transaction,
            "SELECT id,sequence,item_id,location_id,dimension,is_opening,delta_atomic,balance_before_atomic,balance_after_atomic,capture_unit_code,captured_quantity,capture_atomic_factor FROM inventory_movement WHERE operation_id=$id ORDER BY sequence",
            ("$id", Id(operationId))))
        using (var rows = command.ExecuteReader())
            while (rows.Read())
                movements.Add(new(Guid.Parse(rows.GetString(0)), rows.GetInt32(1), Guid.Parse(rows.GetString(2)),
                    Guid.Parse(rows.GetString(3)), ParseDimension(rows.GetString(4)), rows.GetInt32(5) == 1,
                    rows.GetInt64(6), rows.GetInt64(7), rows.GetInt64(8), rows.GetString(9), rows.GetString(10), rows.GetInt64(11)));
        return new(operationId, hash, ParseMovementKind(kind), actor, terminal, reason, note, reference,
            confirmed.Value, movements.AsReadOnly());
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, params (string Name, object? Value)[] parameters)
    {
        var command = new SqliteCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    private static string Id(Guid id) => id.ToString("D");
    private static string At(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseAt(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).ToUniversalTime();
    private static string Dimension(InventoryDimension dimension) => dimension switch
    {
        InventoryDimension.Mass => "MASS", InventoryDimension.Volume => "VOLUME",
        InventoryDimension.Count => "COUNT", _ => throw new ArgumentOutOfRangeException(nameof(dimension))
    };
    private static InventoryDimension ParseDimension(string value) => value switch
    {
        "MASS" => InventoryDimension.Mass, "VOLUME" => InventoryDimension.Volume,
        "COUNT" => InventoryDimension.Count, _ => throw new InvalidDataException("Dimensión de inventario inválida.")
    };
    private static string ItemKind(InventoryItemKind kind) => kind switch
    {
        InventoryItemKind.RawMaterial => "RAW_MATERIAL", InventoryItemKind.FinishedGood => "FINISHED_GOOD",
        InventoryItemKind.Packaging => "PACKAGING", InventoryItemKind.Supply => "SUPPLY",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static string MovementKind(InventoryMovementKind kind) => kind switch
    {
        InventoryMovementKind.OpeningBalance => "OPENING_BALANCE", InventoryMovementKind.Receipt => "RECEIPT",
        InventoryMovementKind.AdjustmentGain => "ADJUSTMENT_GAIN", InventoryMovementKind.AdjustmentLoss => "ADJUSTMENT_LOSS",
        InventoryMovementKind.Waste => "WASTE", InventoryMovementKind.SaleConsumption => "SALE_CONSUMPTION", InventoryMovementKind.ProductionConsumption => "PRODUCTION_CONSUMPTION", InventoryMovementKind.ProductionOutput => "PRODUCTION_OUTPUT", _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static InventoryMovementKind ParseMovementKind(string value) => value switch
    {
        "OPENING_BALANCE" => InventoryMovementKind.OpeningBalance, "RECEIPT" => InventoryMovementKind.Receipt,
        "ADJUSTMENT_GAIN" => InventoryMovementKind.AdjustmentGain, "ADJUSTMENT_LOSS" => InventoryMovementKind.AdjustmentLoss,
        "WASTE" => InventoryMovementKind.Waste, "SALE_CONSUMPTION" => InventoryMovementKind.SaleConsumption,
        "PRODUCTION_CONSUMPTION" => InventoryMovementKind.ProductionConsumption,
        "PRODUCTION_OUTPUT" => InventoryMovementKind.ProductionOutput,
        _ => throw new InvalidDataException("Tipo de movimiento inválido.")
    };
}
