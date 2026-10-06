using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using CasaMiga.Domain;

namespace CasaMiga.Infrastructure;

public sealed class Database(string path)
{
    public string FilePath { get; } = Path.GetFullPath(path);

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
