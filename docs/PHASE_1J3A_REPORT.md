# CASA MIGA POS — REPORTE FINAL CORRECCIÓN 1J.3A

FASE 1J ABIERTA. Corrección de esquema 1J.3A implementada; 1J.3 funcional PENDIENTE. No reanudar automáticamente. Desarrollo, NO listo para producción.

Baseline: 2f5e4579255d88b9c85c58bcfa4d6b1107bb63d5, schema 13, árbol limpio al inicio. El commit que contiene este reporte es el checkpoint correctivo, con asunto `fix: allow confirmed production with zero output`; su identificador se entrega en el reporte final de conversación. Modelo: Codex basado en GPT-6; no se dispone de confirmación independiente del identificador comercial del runtime. Astra es el modelo recomendado por el usuario para esta corrección y para reanudar 1J.3.

## Cambio y causa

013 exigía output_operation_id para todo lote CONFIRMED. Eso impedía representar ingredientes consumidos cuando ninguna pieza resultó vendible. La única migración nueva es `014_allow_zero_output_production_batches.sql`, schema 14. SHA-256: `0CCA38796AB9070B4DE4AE5310226221E5D6C76E9BA2ABB871B63F6E3ACE0928`. Las trece migraciones previas coinciden byte por byte y en hash con el baseline; no existe 015.

Sólo se reconstruye production_batch. Se ajusta el CHECK de CONFIRMED: consumo obligatorio, salida nullable, y operaciones distintas cuando existe salida. Conserva PK, UNIQUE, FKs diferidas, columnas, identidad, notas, snapshots e índice ix_production_batch_time. No cambia la definición de inventory_operation, movement o balance.

Se recrean los seis triggers pertenecientes a la tabla. Cinco conservan su lógica; production_batch_confirm conserva comprobación de snapshots, consumo, concordancia y auditoría, pero comprueba la operación de salida sólo cuando existe. Se agregan production_batch_output_presence y inventory_production_output_requires_actual. Los triggers de hijos, signos y reciprocidad originales permanecen.

## Invariantes

- CONFIRMED siempre exige ConsumptionOperation confirmada, recíproca y con movimientos negativos concordantes con los ingredientes snapshot.
- Todos los ActualCount deben estar resueltos y ser no negativos.
- Si existe ActualCount > 0, OutputOperation es obligatoria, confirmada y recíproca. Sus movimientos corresponden exactamente a líneas positivas.
- Si todos son cero, OutputOperationId es NULL y no existe PRODUCTION_OUTPUT asociado. No hay movimiento de salida cero, operación vacía ni WASTE automático.
- La presencia se decide con EXISTS, sin sumar ActualCount ni añadir estado redundante.
- expected_line_count continúa entre 1 y 1000. Los signos originales se mantienen. El saldo inicial cero permitido previamente no se altera: la prohibición aquí corresponde a movimientos de producción.
- Lotes confirmados, snapshots, operaciones y movimientos mantienen inmutabilidad. DRAFT admite actual pendiente y no crea operaciones.
- InventoryBalance conserva su autoridad en el writer existente. No hay triggers que escriban balances. Los movimientos/actualizaciones SQL de pruebas son exclusivamente fixtures sintéticos y no constituyen un servicio productivo.

## Reconstrucción segura

La transacción toma copia TEMP de las filas originales, elimina la tabla, crea la nueva estructura, restaura el nombre production_batch y sólo entonces reinserta las filas; restaura índice y triggers. Este orden resuelve los contadores de referencias diferidas de SQLite. Copiar primero a la tabla nueva y después renombrarla produjo un rechazo de commit en la prueba histórica; se descartó ese orden. El ajuste permanece dentro de 014 y no requirió otro cambio de esquema.

foreign_keys permanece habilitado, defer_foreign_keys difiere comprobaciones dentro de la transacción. legacy_alter_table se activa sólo durante el renombrado y se restablece inmediatamente. Un CHECK temporal hace abortar la migración si pragma_foreign_key_check devuelve cualquier violación. El registro de versión y todo el DDL quedan dentro de Database.Initialize. Fallos a mitad de reconstrucción y al registrar versión 14 revierten todos los datos y dejan schema 13, que puede reintentarse.

La comparación de todas las filas antes/después incluye fórmulas, ingredientes, alternativas, perfiles, lote DRAFT, lote CONFIRMED positivo, ventas/partidas/pagos, consumo por venta, inventario, receipt_snapshot, auditoría y configuración. Sólo se excluye schema_migration de la comparación del upgrade exitoso; se incluye al verificar rollback. Database.Initialize verifica también hashes históricos. No se migró ni editó una instalación real.

## Verificación

51/51 pruebas focalizadas aprobadas (20 casos nuevos); suite completa mediante scripts/verify.ps1: 486/486, 0 fallidas y 0 omitidas. Build Release: 0 errores y 0 advertencias de compilación. El runner avisó únicamente de la sobrescritura del TRX anterior. integrity_check=ok, foreign_key_check sin filas y reconciliación sin diferencias en las bases de prueba y restauradas. git diff --check y diff staged correctos. Evidencia: phase1j3a-focused.txt, phase1j3a-verification.txt y phase1j3a-migration-hashes.txt.

Cobertura focal: salidas reales 0/1/2/3 frente a plan 2; consumo y salida obligatorios según caso; rechazo de salida en cero, consumo ausente, cambio de actual a NULL/cero tras crear salida, signo incorrecto, delta cero, cantidades discordantes, expected_line_count cero o discordante; DRAFT nullable y rechazo de negativos; inmutabilidad; recuperación por rollback de operaciones; actualización 13→14 con historia; fallo intermedio y al registrar; backup/restauración de lotes cero y positivos con reconciliación sin diferencias. Las pruebas previas cubren instalación limpia 1→14, FKs recíprocas y sin huérfanos, prohibición de triggers que escriban balances y regresiones de 1I.

## Archivos y continuidad

- migrations/014_allow_zero_output_production_batches.sql.
- tests/CasaMiga.Tests/ZeroOutputMigrationTests.cs y RecipeContractTests.cs.
- Expectativas de schema únicamente: CashIdentityTests, FoundationTests, InventoryNoteMigrationTests, InventoryPersistenceTests, Phase1I5HardeningTests (metadato), ReceiptTests, RecipePersistenceTests, RecipeUpgradeTests, SaleInventoryMigrationTests y SalesHistoryTests.
- README.md; docs/CURRENT_STATE.md, NEXT_STEPS.md, FILES.md, database.md, PHASE_1J3A_REPORT.md; evidence/phase1j3a-*.

src intacto. No Application services, PrepareProduction, ConfirmProduction, hash canónico funcional, IDs deterministas funcionales, stock preflight productivo, concurrencia/recovery funcional, permisos o UI. No 1J.4 ni fase posterior; no operaciones físicas. Sin remoto nuevo ni push. Identidad Git sólo local: Rammseth / rammstudioworkshop@gmail.com.

Pendiente: revisión de este checkpoint y autorización explícita para reanudar 1J.3 funcional, con Astra recomendado. La corrección permite el contrato histórico, pero aún no ofrece un flujo de producción al usuario. No declara FASE 1J cerrada ni aptitud para producción.
