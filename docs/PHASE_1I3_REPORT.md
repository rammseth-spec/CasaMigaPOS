# CASA MIGA POS — REPORTE FINAL FASE 1I.3

Estado: BLOQUEADA / NO COMPLETADA. Implementación detenida antes de modificar producto, conforme a la condición expresa de la autorización. FASE 1I permanece abierta; 1I.2 continúa aprobada.

Baseline: 6aa9ad73b4453e94070d1609febd035cbf425feb. Este checkpoint modifica únicamente documentación. El commit que contiene este reporte identifica el HEAD documental final.

## Bloqueo determinista de recovery

Schema 11 no conserva un marcador independiente que identifique qué versión del flujo Checkout creó cada venta y si esa venta estaba obligada a tener evaluación.

Evidencia revisada:
- migrations/006_cash_sales.sql: sale contiene identidad, OperationId, contexto, secuencia, UTC y totales; no versión de flujo ni obligación de evaluación.
- src/CasaMiga.Application/CheckoutService.cs: el flujo aprobado 1I.2 inserta Sale y auditoría sale.completed, sin evaluación. La auditoría contiene datos comerciales, pero no versión del flujo Checkout.
- tests/CasaMiga.Tests/SaleInventoryPersistenceTests.cs, NewSchemaDefaultsAndPermissionAreCorrectAndNormalSalesDoNotConsume: el contrato aprobado permite persistir una venta con schema 11 y cero evaluaciones.
- migrations/011_sale_inventory_consumption.sql: las versiones de política/identidad están en la evaluación; si ésta falta, tampoco están esas versiones. El singleton registra estado de activación, no adopción de Checkout 1I.3.
- Database.cs: schema_migration.applied_utc indica instalación de schema, no despliegue del flujo 1I.3.
- migrations/008_receipt_printing.sql y ReceiptService.cs: schema_version del receipt identifica formato del ticket, no versión del Checkout ni obligatoriedad de evaluación.
- ADR-018 confirma expresamente ventas normales sin evaluación con schema 11.

Contraejemplo: una venta legítima creada por 1I.2 después de instalar 011 puede no tener evaluación. Una venta del futuro flujo 1I.3 a la que falte su evaluación podría presentar los mismos campos y versiones disponibles. Comparar UTC contra applied_utc de 011 clasificaría incorrectamente ventas legítimas de 1I.2. El flag DISABLED tampoco sirve, pues ambos flujos operan inicialmente en ese estado. UUIDv5 sirve para identificar una operación que existe, no demuestra que una venta sin evaluación debió crearla.

Por tanto no se implementa la regla «sin evaluación = histórica», ni una heurística temporal, ni un nuevo marcador improvisado. La capacidad genérica de guardar JSON en auditoría no equivale a una frontera versionada existente: introducir ese protocolo y resolver la ausencia del marcador requiere una decisión explícita.

## Decisión necesaria (propuesta, no implementada)

Solicitar autorización de una corrección estructural separada que diseñe una marca persistente e inmutable por venta o un contrato equivalente de obligatoriedad de evaluación, establecido atómicamente e independiente de la evaluación. Debe definir identificación segura de historia, nuevas ventas, clientes antiguos y recuperación ante ausencia de datos. Si requiere migración 012, obtener autorización expresa antes de crearla. No basta usar el reloj ni el timestamp de 011.

## Preservación y validación

No se modificaron src, tests ni migrations. No se creó 012. Checkout, ExpectedCash, folios, inventario, impresión y backup mantienen su comportamiento aprobado. No se usaron datos comerciales ni impresora. No se implementaron errores nuevos, recovery, concurrencia, rollback cruzado, preflight ni UI de activación de 1I.3.

Última evidencia técnica disponible: 325 pruebas aprobadas / 0 fallidas / 0 omitidas; Release 0 errores / 0 advertencias, de 1I.2. No se repitió suite/build porque el checkpoint es exclusivamente documental. Se verifica diff y preservación de producto antes del commit. Sin push/remoto/licencia.

Documentos actualizados: CURRENT_STATE, NEXT_STEPS, FILES y este reporte. Modelo: GPT-6/Codex de la sesión. 1I.4 NO iniciada. El sistema sigue siendo una versión de desarrollo no lista para producción.
