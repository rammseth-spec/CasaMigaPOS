# ADR-008 — Normalización de catálogo e historial de precio

Estado: implementado en FASE 1C, sujeto a revisión de la fase. Fecha: 2026-09-06.

## Decisión

Category/Product mantienen GUID. El nombre visual conserva mayúsculas/minúsculas y espacios interiores; sólo se recortan extremos y se normaliza Unicode NFC. Para unicidad de categorías y SKU se calcula trim + NFC + ToUpperInvariant, comparación ordinal. Acentos son significativos: Café y Cafe son distintos. SKU opcional: vacío se vuelve NULL; si existe se almacena la clave normalizada, máximo 80 caracteres. No se reutiliza un nombre de categoría o SKU por desactivar el registro.

SQLite registra catalog_key_v1 como función determinista en cada conexión del factory. Índices únicos de expresión la usan para las carreras de escritura; CATALOG_KEY_V1 se usa para orden. No se redefine NOCASE. Microsoft documenta que NOCASE integrado sólo ignora caso ASCII y que LIKE no respeta collations: [Microsoft.Data.Sqlite collations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/collation). SQLite admite funciones deterministas en índices de expresión: [SQLite expression indexes](https://www.sqlite.org/expridx.html).

V1 es contrato persistente: cambiar reglas o actualizar tablas Unicode del runtime requiere revisar compatibilidad y reconstrucción/migración de índices. Herramientas de mantenimiento deben usar Database.Open; una conexión externa sin registro no puede validar/escribir estos índices. No se distribuye SQLite para edición manual.

La migración 004 rechaza duplicados previos y productos activos en categorías inactivas antes del DDL, dentro de su transacción. Verifica nueve índices/triggers antes de registrar versión 4. No corrige automáticamente nombres/precios legados. En pruebas se observó ejecución incompleta del lote ante un conflicto de índice; la comprobación explícita evita declarar aplicada una migración incompleta. No se atribuye una causa del proveedor que no se haya demostrado.

Audit_entry basta para esta fase: evento product.edit con ID, actor, terminal, UTC y snapshots before/after que incluyen priceCents. Cambios y auditoría se confirman juntos; no hay segunda tabla de historial duplicada ni ruido por consultas/no-op. Las futuras ventas deberán guardar snapshot de precio/nombre en SaleItem; no se implementan ahora.

## Políticas de negocio acotadas

Cero pesos es válido técnicamente (p. ej., artículo sin cargo); no se impone un mínimo comercial sin decisión del negocio. Negativos y fracciones de centavo rechazados conforme a ADR-003. Activar producto exige categoría activa. Desactivar categoría con hijos activos se rechaza, sin cascada. Desactivar conserva datos y auditoría; se puede reactivar. TrackInventory es sólo intención futura.

## Coste y alternativas

Se prefiere un contrato Unicode explícito a NOCASE ASCII y comprobaciones sólo en memoria. La búsqueda de subcadena normalizada no usa FTS: paginación SQL de 50, orden estable y prueba con 2,000 registros; no se promete tiempo máximo. La arquitectura puede añadir búsqueda especializada cuando exista evidencia de necesidad.
