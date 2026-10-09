using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using CasaMiga.Domain;

namespace CasaMiga.Infrastructure;

public sealed class Database(string path)
{
    public string FilePath { get; } = Path.GetFullPath(path);

    /// <summary>Versión de esquema que alcanza <see cref="Initialize"/>: una por migración embebida.</summary>
    public static int LatestVersion { get; } = typeof(Database).Assembly.GetManifestResourceNames().Count(n => n.EndsWith(".sql", StringComparison.Ordinal));

    /// <summary>
    /// Rama QA (fase 1K.4D): la nota de discrepancia del corte se aplicó como migración 17 en bases de esa rama. Al juntar las
    /// ramas, esa misma migración (idéntica byte a byte) es la 26 y la 17 es la de contraseñas temporales. Una base con la 17
    /// de QA se reconoce por su huella: se registra como 26 y se aplican 17–25, todo en la misma transacción.
    /// </summary>
    internal const string QaCashDiscrepancyNoteHash = "FFFD6B9273CB924968F60DB3BFBFBFD7B10377052861F7FB87CEB922617EBDA3";
    internal const int QaCashDiscrepancyNoteVersion = 26;

    public SqliteConnection Open(bool readOnly = false)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = FilePath, ForeignKeys = true, Pooling = false, DefaultTimeout = 5,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate
        }.ToString());
        try
        {
            connection.Open();
            connection.CreateCollation("CATALOG_KEY_V1",(x,y)=>StringComparer.Ordinal.Compare(CatalogText.Key(x),CatalogText.Key(y)));
            connection.CreateFunction<string?,string>("catalog_key_v1",CatalogText.Key,isDeterministic:true);
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA busy_timeout=5000; PRAGMA synchronous=FULL;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var connection = Open();
        using (var wal = connection.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode=WAL;";
            if (!string.Equals(wal.ExecuteScalar()?.ToString(), "wal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("No se pudo activar WAL.");
        }
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "CREATE TABLE IF NOT EXISTS schema_migration(version INTEGER PRIMARY KEY, sha256 TEXT NOT NULL, applied_utc TEXT NOT NULL) STRICT;";
        command.ExecuteNonQuery();
        var assembly = typeof(Database).Assembly;
        var scripts = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql", StringComparison.Ordinal)).Order().ToArray();
        for (int i = 0; i < scripts.Length; i++)
            if (!scripts[i].Contains($".{i + 1:000}_", StringComparison.Ordinal))
                throw new InvalidOperationException("Secuencia de migraciones inválida.");
        command.CommandText = "SELECT version FROM schema_migration ORDER BY version";
        using (var versions = command.ExecuteReader())
        {
            int expected = 1;
            while (versions.Read())
                if (versions.GetInt32(0) != expected++) throw new InvalidOperationException("Historial de migraciones incompleto.");
        }
        command.CommandText = "SELECT COALESCE(MAX(version),0) FROM schema_migration";
        if (Convert.ToInt64(command.ExecuteScalar()) > scripts.Length)
            throw new InvalidOperationException("La base requiere una versión más reciente de la aplicación.");
        for (int i = 0; i < scripts.Length; i++)
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(scripts[i])!);
            var sql = reader.ReadToEnd();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            command.Parameters.Clear();
            command.CommandText = "SELECT sha256 FROM schema_migration WHERE version=$v";
            command.Parameters.AddWithValue("$v", i + 1);
            var existing = command.ExecuteScalar();
            if (existing is not null && i + 1 == 17 && Equals(existing, QaCashDiscrepancyNoteHash))
            {
                command.Parameters.Clear();
                command.CommandText = "SELECT count(*) FROM schema_migration WHERE version>17";
                if (Convert.ToInt64(command.ExecuteScalar()) != 0) throw new InvalidOperationException("Historial de migraciones incompleto.");
                command.CommandText = "UPDATE schema_migration SET version=$v WHERE version=17";
                command.Parameters.AddWithValue("$v", QaCashDiscrepancyNoteVersion);
                command.ExecuteNonQuery();
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$v", i + 1);
                existing = null;
            }
            if (existing is not null)
            {
                if (!Equals(existing, hash)) throw new InvalidOperationException("Migración histórica modificada.");
                continue;
            }
            if(i+1==4)
            {
                // Fail explicitly before batched DDL; never silently reconcile legacy commercial data.
                command.CommandText="SELECT EXISTS(SELECT 1 FROM category GROUP BY catalog_key_v1(name) HAVING count(*)>1) OR EXISTS(SELECT 1 FROM product WHERE sku IS NOT NULL GROUP BY catalog_key_v1(sku) HAVING count(*)>1) OR EXISTS(SELECT 1 FROM product p JOIN category c ON c.id=p.category_id WHERE p.active=1 AND c.active=0)";
                if(Convert.ToInt64(command.ExecuteScalar())!=0)throw new InvalidOperationException("El catálogo previo tiene nombres/SKU duplicados o productos activos en categorías inactivas. Requiere revisión antes de migrar.");
            }
            command.CommandText = sql;
            command.ExecuteNonQuery();
            if(i+1==4)
            {
                command.CommandText="SELECT count(*) FROM sqlite_master WHERE name IN ('ux_category_name','ux_product_sku_normalized','category_active_children','product_active_category_insert','product_active_category_update','category_keep_history','product_keep_history','ix_category_order','ix_product_active_name')";
                if(Convert.ToInt64(command.ExecuteScalar())!=9)throw new InvalidOperationException("La migración de catálogo no creó todas sus restricciones.");
            }
            if(i+1==9)
            {
                command.CommandText="SELECT count(*) FROM sqlite_master WHERE name IN ('measurement_unit','inventory_item','inventory_item_unit','inventory_location','inventory_operation','inventory_movement','inventory_balance','product_inventory_link','ux_inventory_opening','ux_product_inventory_active','inventory_operation_confirm_guard','inventory_movement_pending_only','inventory_movement_confirmed_no_update','inventory_movement_confirmed_no_delete')";
                if(Convert.ToInt64(command.ExecuteScalar())!=14)throw new InvalidOperationException("La migración de inventario no creó todas sus estructuras críticas.");
                command.CommandText="SELECT (SELECT count(*) FROM measurement_unit)=7 AND (SELECT count(*) FROM inventory_location WHERE code='MAIN' AND name='Almacén principal')=1 AND (SELECT count(*) FROM permission WHERE name LIKE 'inventory.%')=5 AND (SELECT count(*) FROM role_permission WHERE permission_name LIKE 'inventory.%')=8";
                if(Convert.ToInt64(command.ExecuteScalar())!=1)throw new InvalidOperationException("La migración de inventario no creó sus seeds completos.");
            }
            if (i + 1 == 16)
            {
                command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name IN ('product_photo_asset','product_photo','product_photo_asset_no_delete','product_photo_asset_immutable','product_photo_no_delete','product_photo_revision')";
                if (Convert.ToInt64(command.ExecuteScalar()) != 6) throw new InvalidOperationException("La migración de fotos no creó sus restricciones.");
            }
            if (i + 1 == 17)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM pragma_table_info('app_user') WHERE name='must_change_password')=1 AND (SELECT count(*) FROM role_permission WHERE permission_name='users.manage' AND role_name='Administrator')=1";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de usuarios no creó sus estructuras.");
            }
            if (i + 1 == 18)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM branch)=1 AND (SELECT count(*) FROM sqlite_master WHERE name IN ('sync_outbox','ix_sync_outbox_pending','sync_outbox_no_delete_pending','sync_cash_session_insert','sync_cash_session_update','sync_cash_count_insert','sync_sale_insert','sync_audit_entry_insert','branch_no_delete','branch_id_immutable'))=10";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de sincronización no creó sus estructuras.");
            }
            if (i + 1 == 19)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM sqlite_master WHERE name IN ('sale_refund','sale_refund_item','sale_refund_item_valid','sale_refund_complete','sale_refund_no_update','sale_refund_no_delete','sale_refund_item_no_update','sale_refund_item_no_delete','ux_audit_sale_refund','sync_outbox','ix_sync_outbox_pending','sync_outbox_no_delete_pending','sync_cash_session_insert','sync_cash_session_update','sync_cash_count_insert','sync_sale_insert','sync_sale_refund_insert','sync_audit_entry_insert'))=18"
                    + " AND (SELECT count(*) FROM role_permission WHERE permission_name='sales.refund')=2";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de devoluciones no creó sus estructuras.");
            }
            if (i + 1 == 20)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM pragma_table_info('payment') WHERE name='reference')=1 AND (SELECT count(*) FROM sqlite_master WHERE name IN ('payment','payment_no_update','payment_no_delete','payment_no_append','sale_refund_complete','sale_complete'))=6 AND (SELECT count(*) FROM sqlite_master WHERE name='payment_020')=0";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de formas de pago no creó sus estructuras.");
            }
            if (i + 1 == 21)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM sqlite_master WHERE name IN ('consignment_price','consignment_store','ux_consignment_store_name','consignment_store_no_delete',"
                    + "'consignment_template_item','consignment_delivery','consignment_delivery_item','consignment_delivery_item_valid','consignment_delivery_complete','consignment_void',"
                    + "'consignment_settlement','consignment_settlement_item','consignment_settlement_item_valid','consignment_settlement_complete','consignment_void_valid',"
                    + "'consignment_payment','consignment_delivery_no_update','consignment_delivery_no_delete','consignment_payment_no_update','consignment_payment_no_delete'))=20"
                    + " AND (SELECT count(*) FROM role_permission WHERE permission_name LIKE 'consignment.%')=4";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de charolas no creó sus estructuras.");
            }
            if (i + 1 == 22)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM sqlite_master WHERE name IN ('cash_movement','ix_cash_movement_session','cash_movement_valid','cash_movement_no_update','cash_movement_no_delete','ux_audit_cash_movement','sale_refund_complete'))=7"
                    + " AND (SELECT count(*) FROM role_permission WHERE permission_name='cash.movement')=3 AND (SELECT count(*) FROM role_permission WHERE permission_name='cash.withdrawal')=2";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de movimientos de caja no creó sus estructuras.");
            }
            if (i + 1 == 23)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM sqlite_master WHERE type='trigger' AND name LIKE 'sync\\_%' ESCAPE '\\')=66"
                    + " AND (SELECT count(*) FROM sqlite_master WHERE name IN ('sync_outbox','ix_sync_outbox_pending'))=2 AND (SELECT count(*) FROM sqlite_master WHERE name='sync_outbox_v23')=0";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de sincronización de la versión web no creó sus estructuras.");
            }
            if (i + 1 == 26)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM pragma_table_info('cash_session') WHERE name='discrepancy_note')=1 AND (SELECT count(*) FROM sqlite_master WHERE name='cash_close_note_required')=1";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de nota de discrepancia no creó sus estructuras.");
            }
            if (i + 1 == 25)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM sqlite_master WHERE name IN ('inventory_item_cost','sync_inventory_cost_insert','sync_inventory_cost_update'))=3";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de costos de insumos no creó sus estructuras.");
            }
            if (i + 1 == 24)
            {
                command.CommandText = "SELECT (SELECT count(*) FROM sqlite_master WHERE name IN ('cloud_command','ix_cloud_command_unreported','cloud_command_no_delete','cloud_command_result_immutable'))=4";
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La migración de cambios de la versión web no creó sus estructuras.");
            }
            command.CommandText = "INSERT INTO schema_migration VALUES($v,$hash,$at)";
            command.Parameters.AddWithValue("$hash", hash);
            command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void Validate()
    {
        using var connection = Open(readOnly: true);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        using (var rows = command.ExecuteReader())
        {
            if (!rows.Read() || rows.GetString(0) != "ok" || rows.Read())
                throw new InvalidDataException("Falló la integridad de la base.");
        }
        command.CommandText = "PRAGMA foreign_key_check";
        using var foreignKeys = command.ExecuteReader();
        if (foreignKeys.Read()) throw new InvalidDataException("Referencias inválidas.");
    }
}
