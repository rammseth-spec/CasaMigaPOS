# CASA MIGA POS — REPORTE CORRECCIÓN 1H.2A / MIGRACIÓN 010

Estado: 1H.2A COMPLETADA; FASE 1H ABIERTA. 1H.3 NO inició. Modelo utilizado: GPT-6 Astra.

## Defecto y alcance

009 agrupaba OPENING_BALANCE y RECEIPT bajo reason_code NULL y note NULL. Esto impedía la observación opcional en RECEIPT. Se añadió migrations/010_inventory_receipt_note_fix.sql; 001–009 permanecen byte por byte intactas. No se modificó src ni se implementó funcionalidad Application, UI, ventas→inventario o hardware.

## Git y recuperación

HEAD inicial limpio: 6afd1419423b8640f2a9357957894c0e164a80bb.
Commit técnico: 76e654ce9b56132ff64248953931c95190858311 (migración y pruebas).
El commit documental que contiene este reporte es el baseline final recomendado; obtener su identificador con git log -1 --format=%H -- docs/PHASE_1H2A_REPORT.md. Su padre es el commit técnico anterior. No se hizo push ni se configuró remoto.

## SQLite y preservación

Dentro de la transacción existente de Database.Initialize, con foreign_keys ON, se reconstruyen inventory_operation e inventory_movement. Una tabla de staging guarda los movimientos, se copia el padre corregido, se elimina primero la hija, se reemplaza el padre y se recrea la hija con sus datos. Se elimina staging y se restauran cuatro índices explícitos y diez triggers, además de los índices automáticos PK/UNIQUE. No hay writable_schema ni edición directa de sqlite_master.

Comparaciones automatizadas verificaron equivalencia lógica de todas las filas de todas las tablas salvo schema_migration: cantidades, hashes, UUIDs, timestamps, actores, terminales, ledger, saldos y ProductInventoryLink. Se compararon columnas/tipos/NOT NULL/default/PK, FKs y las definiciones de todos los índices y triggers. Los demás CHECK y UNIQUE se conservaron al copiar las definiciones de 009.

InventoryBalance no recibe escrituras de la migración; SqliteInventoryStore sigue siendo su única autoridad y ningún trigger lo actualiza. Ledger y operaciones CONFIRMED mantienen su inmutabilidad. Opening cero, opening único, prohibición de stock negativo e idempotencia siguen cubiertos por la suite. Recuperar/reaplicar el mismo request tras migrar conserva idempotencia; reconciliación sin diferencias.

## Reglas finales del esquema

- OPENING_BALANCE: reason_code NULL y note NULL.
- RECEIPT: reason_code NULL; note NULL o length(trim(note)) entre 1 y 500. Rechaza razones de ajuste/merma y observación vacía tras trim.
- WASTE: razón obligatoria DAMAGED, EXPIRED, PRODUCTION_ERROR, SPILL u OTHER.
- ADJUSTMENT_GAIN/LOSS: razón obligatoria PHYSICAL_COUNT_VARIANCE, DATA_CORRECTION u OTHER.
- OTHER exige observación no NULL y válida según el CHECK de note. Las demás observaciones conservan la misma validación.

## Verificación

18 pruebas focalizadas aprobadas: 16 variantes de restricciones y dos pruebas integrales. Upgrade real 009→010 con operaciones existentes, apertura cero, recepción, ambos ajustes, merma, saldo y asociación. Comparación de datos/objetos, inmutabilidad, recuperación, reconciliación y backup .cmbackup/restauración a esquema 10.

Fallo inyectado al insertar schema_migration versión 10, después de reconstruir tablas: rollback completo a versión 9, datos/índices/triggers/FKs preservados; versión 10 no registrada. Tras quitar el fallo, upgrade correcto.

scripts/verify.ps1: 254 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores y 0 advertencias. Se repitió después de normalizar los finales de línea del SQL nuevo, antes de fijar su hash definitivo. Database.Validate ejecutó integrity_check y foreign_key_check en upgrade, rollback y restauración sin errores. git diff --check correcto; src y migraciones 001–009 sin diferencias respecto al HEAD inicial.

Evidencia: evidence/phase1h2a-verification.txt y evidence/phase1h2a-migration-hashes.txt. Este último contiene SHA-256 de 001–010.

## Archivos del bloque

Nueva migración y InventoryNoteMigrationTests.cs. Cinco tests existentes sólo actualizan la versión esperada a 10: CashIdentityTests, FoundationTests, InventoryPersistenceTests, ReceiptTests y SalesHistoryTests. Documentación: README.md, docs/CURRENT_STATE.md, docs/NEXT_STEPS.md, docs/FILES.md, docs/database.md, este reporte y los dos archivos de evidencia.

## Riesgos y continuidad

La reconstrucción requiere espacio temporal y bloqueo transaccional proporcional al ledger; se validó con bases sintéticas, no se manipuló producción. El contrato Domain todavía rechaza note en RECEIPT: se debe alinear en 1H.3 antes de exponer su caso de uso. No se afirma que la observación funcione ya mediante ese contrato.

Checkout, ventas, caja, tickets e impresión intactos. No se usó impresora ni se generaron operaciones comerciales reales. No se inició InventoryService, comandos/queries Application, UI, recetas, producción, compras, proveedores, cajón, LAN, rediseño o instalador. El sistema sigue en desarrollo y no está listo para producción.

Próximo paso recomendado: revisar este cierre y retomar exclusivamente 1H.3 desde el commit documental final, usando SOL; Astra si aparece un defecto técnico complejo. No se inicia en este bloque.