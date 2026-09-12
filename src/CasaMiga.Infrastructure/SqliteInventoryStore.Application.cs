using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;

namespace CasaMiga.Infrastructure;

public sealed partial class SqliteInventoryStore
{
    public InventoryItem? FindItem(Guid id)
    {
        using var connection = database.Open(readOnly: true);
        return ReadItem(connection, null, id);
    }

    public InventoryItemUnit? FindItemUnit(Guid itemId, string code)
    {
        using var connection = database.Open(readOnly: true);
        var item = ReadItem(connection, null, itemId);
        if (item is null) return null;
        using var command = Command(connection, null,
            "SELECT id,code,name,atomic_units_per_whole_unit,active FROM inventory_item_unit WHERE item_id=$item AND catalog_key_v1(code)=catalog_key_v1($code) AND active=1",
            ("$item", Id(itemId)), ("$code", code));
        using var row = command.ExecuteReader();
        return row.Read() ? new InventoryItemUnit(Guid.Parse(row.GetString(0)), item, row.GetString(1),
            row.GetString(2), row.GetInt64(3), row.GetInt32(4) == 1) : null;
    }

    public InventoryLocationView? FindLocation(Guid id)
    {
        using var connection = database.Open(readOnly: true);
        using var command = Command(connection, null,
            "SELECT code,name,active FROM inventory_location WHERE id=$id", ("$id", Id(id)));
        using var row = command.ExecuteReader();
        return row.Read() ? new(id, row.GetString(0), row.GetString(1), row.GetInt32(2) == 1) : null;
    }

    public void CreateItem(InventoryItem item, AuditEntry audit) => Configure(audit, (connection, transaction) =>
    {
        using var command = Command(connection, transaction,
            "INSERT INTO inventory_item(id,code,name,kind,dimension,active,created_utc,modified_utc) VALUES($id,$code,$name,$kind,$dimension,$active,$created,$modified)",
            ("$id", Id(item.Id)), ("$code", item.Code), ("$name", item.Name), ("$kind", ItemKind(item.Kind)),
            ("$dimension", Dimension(item.Dimension)), ("$active", item.IsActive ? 1 : 0),
            ("$created", At(item.CreatedAt)), ("$modified", At(item.UpdatedAt)));
        command.ExecuteNonQuery();
    });

    public void UpdateItem(InventoryItem item, AuditEntry audit) => Configure(audit, (connection, transaction) =>
    {
        using var command = Command(connection, transaction,
            "UPDATE inventory_item SET name=$name,kind=$kind,dimension=$dimension,active=$active,modified_utc=$modified WHERE id=$id",
            ("$id", Id(item.Id)), ("$name", item.Name), ("$kind", ItemKind(item.Kind)),
            ("$dimension", Dimension(item.Dimension)), ("$active", item.IsActive ? 1 : 0), ("$modified", At(item.UpdatedAt)));
        if (command.ExecuteNonQuery() != 1)
            throw new InventoryStoreException(InventoryError.InventoryItemNotFound, "Artículo inexistente.");
    });

    public void AddItemUnit(InventoryItemUnit unit, AuditEntry audit) => Configure(audit, (connection, transaction) =>
    {
        using var command = Command(connection, transaction,
            "INSERT INTO inventory_item_unit(id,item_id,code,name,dimension,atomic_units_per_whole_unit,active) VALUES($id,$item,$code,$name,$dimension,$factor,$active)",
            ("$id", Id(unit.Id)), ("$item", Id(unit.InventoryItemId)), ("$code", unit.Code),
            ("$name", unit.Name), ("$dimension", Dimension(unit.Dimension)),
            ("$factor", unit.AtomicUnitsPerWholeUnit), ("$active", unit.IsActive ? 1 : 0));
        command.ExecuteNonQuery();
    });

    public InventoryProductLinkView? FindActiveProductLink(Guid productId)
    {
        using var connection = database.Open(readOnly: true);
        return ReadProductLink(connection, null, productId);
    }

    public void LinkProduct(ProductInventoryLink link, AuditEntry audit) => Configure(audit, (connection, transaction) =>
    {
        using (var refs = Command(connection, transaction,
            "SELECT EXISTS(SELECT 1 FROM product WHERE id=$product),EXISTS(SELECT 1 FROM inventory_item WHERE id=$item AND active=1 AND dimension=$dimension),EXISTS(SELECT 1 FROM product_inventory_link WHERE product_id=$product AND active=1)",
            ("$product", Id(link.ProductId)), ("$item", Id(link.InventoryItemId)),
            ("$dimension", Dimension(link.QuantityPerProductUnit.Dimension))))
        using (var row = refs.ExecuteReader())
        {
            row.Read();
            if (row.GetInt32(0) != 1) throw new InventoryStoreException(InventoryError.ProductNotFound, "Producto inexistente.");
            if (row.GetInt32(1) != 1) throw new InventoryStoreException(InventoryError.InventoryItemInactive, "Artículo inexistente, inactivo o incompatible.");
            if (row.GetInt32(2) == 1) throw new InventoryStoreException(InventoryError.Conflict, "El producto ya tiene una asociación activa.");
        }
        using var command = Command(connection, transaction,
            "INSERT INTO product_inventory_link(id,product_id,inventory_item_id,dimension,quantity_per_product_unit_atomic,active,created_utc,modified_utc) VALUES($id,$product,$item,$dimension,$quantity,1,$at,$at)",
            ("$id", Id(audit.Entity)), ("$product", Id(link.ProductId)), ("$item", Id(link.InventoryItemId)),
            ("$dimension", Dimension(link.QuantityPerProductUnit.Dimension)),
            ("$quantity", link.QuantityPerProductUnitAtomic), ("$at", At(audit.At)));
        command.ExecuteNonQuery();
    });

    public void UnlinkProduct(Guid productId, AuditEntry audit) => Configure(audit, (connection, transaction) =>
    {
        using var command = Command(connection, transaction,
            "UPDATE product_inventory_link SET active=0,modified_utc=$at WHERE product_id=$product AND active=1",
            ("$product", Id(productId)), ("$at", At(audit.At)));
        if (command.ExecuteNonQuery() != 1)
            throw new InventoryStoreException(InventoryError.ProductNotFound, "El producto no tiene una asociación activa.");
    });

    public InventoryListPage QueryItems(InventoryListQuery query)
    {
        using var connection = database.Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        const string predicate = "WHERE ($search='' OR instr(catalog_key_v1(i.code),catalog_key_v1($search))>0 OR instr(catalog_key_v1(i.name),catalog_key_v1($search))>0) AND ($kind IS NULL OR i.kind=$kind) AND ($active IS NULL OR i.active=$active)";
        var parameters = new (string, object?)[] { ("$search", query.Search.Trim()),
            ("$kind", query.Kind is { } kind ? ItemKind(kind) : null),
            ("$active", query.Active is { } active ? active ? 1 : 0 : null) };
        int total;
        using (var count = Command(connection, transaction, "SELECT count(*) FROM inventory_item i " + predicate, parameters))
            total = Convert.ToInt32(count.ExecuteScalar());
        var items = new List<InventoryListRow>();
        using (var command = Command(connection, transaction,
            "SELECT i.id,i.code,i.name,i.kind,i.dimension,i.active,b.quantity_atomic,EXISTS(SELECT 1 FROM product_inventory_link l WHERE l.inventory_item_id=i.id AND l.active=1) " +
            "FROM inventory_item i LEFT JOIN inventory_balance b ON b.item_id=i.id AND b.location_id=$main " + predicate +
            " ORDER BY i.name COLLATE CATALOG_KEY_V1,i.id LIMIT $limit OFFSET $offset",
            parameters.Append(("$main", (object?)Id(MainLocationId))).Append(("$limit", query.PageSize)).Append(("$offset", checked(query.Page * query.PageSize))).ToArray()))
        using (var rows = command.ExecuteReader())
            while (rows.Read()) items.Add(new(Guid.Parse(rows.GetString(0)), rows.GetString(1), rows.GetString(2),
                ParseItemKind(rows.GetString(3)), ParseDimension(rows.GetString(4)), rows.GetInt32(5) == 1,
                rows.IsDBNull(6) ? null : rows.GetInt64(6), AtomicUnitCode(ParseDimension(rows.GetString(4))), rows.GetInt32(7) == 1));
        transaction.Commit();
        return new(items.AsReadOnly(), total, query.Page, query.PageSize);
    }

    public InventoryItemDetail? FindItemDetail(Guid id)
    {
        using var connection = database.Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        var item = ReadItem(connection, transaction, id);
        if (item is null) return null;
        InventoryLocationView main;
        using (var location = Command(connection, transaction, "SELECT code,name,active FROM inventory_location WHERE id=$id", ("$id", Id(MainLocationId))))
        using (var row = location.ExecuteReader()) { row.Read(); main = new(MainLocationId, row.GetString(0), row.GetString(1), row.GetInt32(2) == 1); }
        var balance = ReadBalance(connection, transaction, id, MainLocationId);
        var units = new List<InventoryItemUnit>();
        using (var command = Command(connection, transaction,
            "SELECT id,code,name,atomic_units_per_whole_unit,active FROM inventory_item_unit WHERE item_id=$id ORDER BY active DESC,name COLLATE CATALOG_KEY_V1,id", ("$id", Id(id))))
        using (var rows = command.ExecuteReader())
            while (rows.Read()) units.Add(new(Guid.Parse(rows.GetString(0)), item, rows.GetString(1), rows.GetString(2), rows.GetInt64(3), rows.GetInt32(4) == 1));
        var links = new List<InventoryProductLinkView>();
        using (var command = Command(connection, transaction,
            "SELECT id,product_id,dimension,quantity_per_product_unit_atomic,active FROM product_inventory_link WHERE inventory_item_id=$id ORDER BY active DESC,product_id", ("$id", Id(id))))
        using (var rows = command.ExecuteReader())
            while (rows.Read()) links.Add(new(Guid.Parse(rows.GetString(0)), Guid.Parse(rows.GetString(1)), id,
                ParseDimension(rows.GetString(2)), rows.GetInt64(3), rows.GetInt32(4) == 1));
        transaction.Commit();
        return new(item, main, balance, units.AsReadOnly(), links.AsReadOnly(), AtomicUnitCode(item.Dimension));
    }

    public InventoryMovementPage QueryMovements(InventoryMovementQuery query)
    {
        using var connection = database.Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        const string predicate = "WHERE o.state='CONFIRMED' AND ($item IS NULL OR m.item_id=$item)";
        object? item = query.InventoryItemId is { } id ? Id(id) : null;
        int total;
        using (var count = Command(connection, transaction,
            "SELECT count(*) FROM inventory_movement m JOIN inventory_operation o ON o.id=m.operation_id " + predicate, ("$item", item)))
            total = Convert.ToInt32(count.ExecuteScalar());
        var movements = new List<InventoryMovementRow>();
        using (var command = Command(connection, transaction,
            "SELECT o.confirmed_utc,o.kind,m.item_id,i.name,m.location_id,l.name,m.delta_atomic,m.balance_before_atomic,m.balance_after_atomic,o.actor_id,u.display_name,o.terminal_id,t.name,o.reason_code,o.note,o.external_reference,o.id " +
            "FROM inventory_movement m JOIN inventory_operation o ON o.id=m.operation_id JOIN inventory_item i ON i.id=m.item_id JOIN inventory_location l ON l.id=m.location_id JOIN app_user u ON u.id=o.actor_id JOIN terminal t ON t.id=o.terminal_id " +
            predicate + " ORDER BY o.confirmed_utc DESC,o.id DESC,m.sequence ASC LIMIT $limit OFFSET $offset",
            ("$item", item), ("$limit", query.PageSize), ("$offset", checked(query.Page * query.PageSize))))
        using (var rows = command.ExecuteReader())
            while (rows.Read()) movements.Add(new(ParseAt(rows.GetString(0)), ParseMovementKind(rows.GetString(1)),
                Guid.Parse(rows.GetString(2)), rows.GetString(3), Guid.Parse(rows.GetString(4)), rows.GetString(5),
                rows.GetInt64(6), rows.GetInt64(7), rows.GetInt64(8), Guid.Parse(rows.GetString(9)), rows.GetString(10),
                Guid.Parse(rows.GetString(11)), rows.GetString(12), rows.IsDBNull(13) ? null : rows.GetString(13),
                rows.IsDBNull(14) ? null : rows.GetString(14), rows.IsDBNull(15) ? null : rows.GetString(15), Guid.Parse(rows.GetString(16))));
        transaction.Commit();
        return new(movements.AsReadOnly(), total, query.Page, query.PageSize);
    }

    public IReadOnlyList<InventoryReconciliationStatus> QueryReconciliation()
    {
        using var connection = database.Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        var result = new List<InventoryReconciliationStatus>();
        using var command = Command(connection, transaction,
            "WITH ledger AS (SELECT m.item_id,m.location_id,sum(m.delta_atomic) quantity FROM inventory_movement m JOIN inventory_operation o ON o.id=m.operation_id WHERE o.state='CONFIRMED' GROUP BY m.item_id,m.location_id), positions AS (SELECT item_id,location_id FROM ledger UNION SELECT item_id,location_id FROM inventory_balance) SELECT p.item_id,p.location_id,COALESCE(b.quantity_atomic,0),COALESCE(l.quantity,0) FROM positions p LEFT JOIN inventory_balance b USING(item_id,location_id) LEFT JOIN ledger l USING(item_id,location_id) ORDER BY p.location_id,p.item_id");
        using var rows = command.ExecuteReader();
        while (rows.Read())
        {
            var materialized = rows.GetInt64(2); var reconstructed = rows.GetInt64(3);
            result.Add(new(Guid.Parse(rows.GetString(0)), Guid.Parse(rows.GetString(1)), materialized, reconstructed, materialized == reconstructed));
        }
        transaction.Commit();
        return result.AsReadOnly();
    }

    private void Configure(AuditEntry audit, Action<SqliteConnection, SqliteTransaction> change)
    {
        try
        {
            using var connection = database.Open();
            using var transaction = connection.BeginTransaction(deferred: false);
            EnsureConfigureAccess(connection, transaction, audit);
            change(connection, transaction);
            InsertAudit(connection, transaction, audit);
            transaction.Commit();
        }
        catch (InventoryStoreException) { throw; }
        catch (SqliteException error) when (error.SqliteErrorCode == 19)
            { throw new InventoryStoreException(InventoryError.Conflict, "La configuración entra en conflicto con datos existentes.", error); }
    }

    private static void EnsureConfigureAccess(SqliteConnection connection, SqliteTransaction transaction, AuditEntry audit)
    {
        if (audit.Terminal is not { } terminal) throw new InventoryStoreException(InventoryError.Conflict, "Terminal obligatoria.");
        using var command = Command(connection, transaction,
            "SELECT EXISTS(SELECT 1 FROM app_user u JOIN role_permission p ON p.role_name=u.role_name WHERE u.id=$actor AND u.active=1 AND p.permission_name='inventory.configure'),EXISTS(SELECT 1 FROM terminal WHERE id=$terminal)",
            ("$actor", Id(audit.Actor)), ("$terminal", Id(terminal)));
        using var row = command.ExecuteReader(); row.Read();
        if (row.GetInt32(0) != 1) throw new InventoryStoreException(InventoryError.Unauthorized, "Permiso insuficiente.");
        if (row.GetInt32(1) != 1) throw new InventoryStoreException(InventoryError.Conflict, "Terminal no configurada.");
    }

    private static void InsertAudit(SqliteConnection connection, SqliteTransaction transaction, AuditEntry audit)
    {
        using var command = Command(connection, transaction,
            "INSERT INTO audit_entry VALUES($id,$actor,$terminal,$action,$entity,$at,$details)",
            ("$id", Id(audit.Id)), ("$actor", Id(audit.Actor)),
            ("$terminal", audit.Terminal is { } terminal ? Id(terminal) : null), ("$action", audit.Action),
            ("$entity", Id(audit.Entity)), ("$at", At(audit.At)), ("$details", audit.DetailsJson));
        command.ExecuteNonQuery();
    }

    private static InventoryItem? ReadItem(SqliteConnection connection, SqliteTransaction? transaction, Guid id)
    {
        using var command = Command(connection, transaction,
            "SELECT code,name,kind,dimension,active,created_utc,modified_utc FROM inventory_item WHERE id=$id", ("$id", Id(id)));
        using var row = command.ExecuteReader();
        return row.Read() ? new(id, row.GetString(0), row.GetString(1), ParseItemKind(row.GetString(2)),
            ParseDimension(row.GetString(3)), row.GetInt32(4) == 1, ParseAt(row.GetString(5)), ParseAt(row.GetString(6))) : null;
    }

    private static InventoryProductLinkView? ReadProductLink(SqliteConnection connection, SqliteTransaction? transaction, Guid productId)
    {
        using var command = Command(connection, transaction,
            "SELECT id,inventory_item_id,dimension,quantity_per_product_unit_atomic,active FROM product_inventory_link WHERE product_id=$product AND active=1", ("$product", Id(productId)));
        using var row = command.ExecuteReader();
        return row.Read() ? new(Guid.Parse(row.GetString(0)), productId, Guid.Parse(row.GetString(1)),
            ParseDimension(row.GetString(2)), row.GetInt64(3), row.GetInt32(4) == 1) : null;
    }

    private static InventoryItemKind ParseItemKind(string value) => value switch
    {
        "RAW_MATERIAL" => InventoryItemKind.RawMaterial, "FINISHED_GOOD" => InventoryItemKind.FinishedGood,
        "PACKAGING" => InventoryItemKind.Packaging, "SUPPLY" => InventoryItemKind.Supply,
        _ => throw new InvalidDataException("Tipo de artículo de inventario inválido.")
    };
    private static string AtomicUnitCode(InventoryDimension dimension) => dimension switch
    {
        InventoryDimension.Mass => "mg", InventoryDimension.Volume => "uL", InventoryDimension.Count => "piece",
        _ => throw new ArgumentOutOfRangeException(nameof(dimension))
    };
}
