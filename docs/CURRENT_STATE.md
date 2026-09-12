# Continuidad — FASE 1H.4 completada y aprobada

FASE 1H permanece abierta. 1H.4 está completada y su smoke WPF fue aprobado manualmente por el usuario. UI implementada en `845b8a8`, binding WPF corregido en `dd7d5b5` y unidades operativas exactas/validación de Manager en `9b7b6d6`.

Administrator conserva acceso completo; Manager tiene view/receive/waste y carece de configure/adjust; Cashier no accede. La UI ofrece g/kg, ml/L y pieza; mg/µL permanecen internos. `SMOKE-H14` quedó en 1.5 kg, con cuatro movimientos y reconciliación exacta. Ver [reporte de aceptación](PHASE_1H4_SMOKE_CORRECTION.md) y [evidencia](evidence/phase1h4-smoke-correction.txt).

Verificación vigente: 290 pruebas aprobadas, 0 fallidas, 0 omitidas; Release con 0 errores y 0 advertencias. Migraciones 001–010, Domain, ledger, InventoryBalance, Checkout, Sale, Payment, ExpectedCash, tickets e impresión permanecen intactos. El sistema sigue siendo una versión de desarrollo no lista para producción. 1H.5 no inició.

## Registro anterior

# Continuidad — FASE 1H.3 completada

1H.3 está completada técnicamente en el commit `9e96edc2a4652487adce9a4ad2693ff70a3d218d`. FASE 1H permanece abierta y 1H.4 no inició. Existen contratos y servicios Application para saldo inicial, recepción, merma, ajuste a conteo físico, configuración de artículos/presentaciones/asociaciones y consultas paginadas. Presentation no cambió y aún no hay UI de inventario.

Domain ahora coincide con el esquema 010: RECEIPT admite observación opcional normalizada, rechaza whitespace y más de 500 caracteres, y nunca admite reason_code. OPENING_BALANCE conserva razón y observación NULL.

Los comandos de movimiento reciben un OperationId estable y una intención positiva/absoluta; no exponen deltas arbitrarios. El conteo calcula gain/loss en Application. Infrastructure verifica saldo y versión esperados dentro de su transacción inmediata; una intercalación devuelve ConcurrencyConflict sin insertar el ajuste. Un retry del mismo conteo recupera la operación confirmada y contenido distinto produce IdempotencyConflict. NoChange no escribe operación, movimiento ni auditoría.

Administrator posee los cinco permisos; Manager sólo view/receive/waste; Cashier ninguno. Configuración y auditoría se confirman juntas. Las consultas abren SQLite read-only con transacción deferred, LIMIT/OFFSET acotados, orden estable y joins agregados sin N+1.

Verificación final: 52 pruebas focalizadas y 265 completas aprobadas, 0 fallidas y 0 omitidas; Release con 0 errores y 0 advertencias. Migraciones 001–010 intactas. Checkout, Sale, Payment, ExpectedCash, tickets e impresión permanecen intactos. Ver [reporte](PHASE_1H3_REPORT.md), [ADR](decisions/ADR-016-inventory-application.md) y evidencia.

## Registro histórico anterior

# Continuidad — corrección 1H.2A completada / esquema 010

La migración 010 corrige exclusivamente el CHECK de RECEIPT: reason_code NULL y note opcional (NULL o texto de 1–500 caracteres tras trim). 001–009 permanecen intactas. Commit técnico: `76e654ce9b56132ff64248953931c95190858311`; baseline anterior: `6afd1419423b8640f2a9357957894c0e164a80bb`.

Reconstrucción transaccional de inventory_operation e inventory_movement con foreign_keys ON, sin escrituras al balance. Se preservan datos, columnas, índices, triggers y FKs. Pruebas cubren upgrade con operaciones, opening cero, idempotencia, reconciliación, backup/restauración y rollback posterior a la reconstrucción sin registrar versión 10.

Verificación final: 254 pruebas aprobadas, 0 fallidas y 0 omitidas; Release con 0 errores y 0 advertencias. Ver [reporte](PHASE_1H2A_REPORT.md) y evidencia asociada.

FASE 1H permanece ABIERTA. 1H.3 NO inició. El contrato Domain existente todavía rechaza observación en RECEIPT; su alineación corresponde a 1H.3 antes de exponer el caso de uso. Checkout, tickets, impresión y todo src permanecieron intactos. Versión de desarrollo, no lista para producción.

## Registro histórico anterior
# Continuidad — FASE 1H iniciada, subfase 1H.2 completada

1H.1 y 1H.2 están completadas. El commit `a8fcfb2` agrega migración 009, persistencia del catálogo de inventario, ledger inmutable, saldo materializado reconstruible, operaciones multipartida, idempotencia por UUID+SHA-256, recuperación de commit incierto, reconciliación y pruebas concurrentes.

`SqliteInventoryStore` es la única autoridad que escribe `inventory_balance` dentro de una transacción inmediata. Usa actualización condicionada por saldo y versión. Los triggers sólo protegen estructuras y estados; ninguno calcula ni actualiza balances. `inventory_movement` es la fuente histórica.

OPENING_BALANCE admite cero mediante una línea explícita `is_opening=1, delta_atomic=0`, por lo que distingue una posición inicializada en cero de una aún no inicializada. Los demás movimientos prohíben delta cero. Stock negativo y overflow revierten la operación completa.

La migración 009 crea ocho tablas, siete unidades estándar, MAIN / Almacén principal y cinco permisos. Administrator recibe cinco; Manager recibe view/receive/waste; Cashier ninguno. PENDING sólo vive dentro de la transacción normal del store y se transforma a CONFIRMED antes del commit.

Verificación: 41 pruebas focalizadas y 236 pruebas completas aprobadas, 0 fallidas y 0 omitidas. Build Release: 0 errores y 0 advertencias. Migraciones 001–008 intactas. Checkout, ventas, caja, ExpectedCash, tickets e impresión no cambiaron. No existe UI ni servicio Application operativo de inventario; 1H.3 no se inició.

## Registro histórico anterior

# Continuidad — FASE 1H iniciada, subfase 1H.1 completada

FASE 1H comenzó únicamente con 1H.1. El commit `3ed510c` agrega el modelo puro de Domain para cantidades físicas, dimensiones, unidades estándar, artículos inventariables, presentaciones enteras específicas, ubicación principal, razones estructuradas y asociación explícita Product–InventoryItem. `InventoryQuantity` usa Int64 en mg, µL o pieza; rechaza negativos, pérdida de precisión, dimensiones incompatibles y overflow. Cero es válido como saldo, mientras presentaciones y asociaciones exigen cantidades positivas.

No existe aún migración 009, ledger persistido, `InventoryBalance`, servicios Application, permisos SQLite ni UI de inventario. `Product.TrackInventory` no crea artículos, asociaciones o movimientos. Checkout, ventas, caja, ExpectedCash y tickets no cambiaron. Las migraciones 001–008 permanecen intactas.

Verificación de 1H.1: 18 pruebas focalizadas aprobadas. Suite completa: 213 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores y 0 advertencias. FASE 1G permanece cerrada y el producto continúa siendo una versión de desarrollo no lista para producción.

## Registro histórico anterior

# Continuidad — FASE 1G cerrada

FASE 1G CERRADA dentro del alcance autorizado. Bloque F cerró la reimpresión desde Historial para Administrator y Manager mediante `sales.receipt.reprint`; Cashier no recibe el permiso. Application revalida usuario activo y permiso, reutiliza el `receipt_snapshot` inmutable o crea el primero una sola vez para ventas anteriores a 1G. Cada acción válida crea un request `REPRINT`, conserva trazabilidad append-only y usa los estados REQUESTED/SUBMITTED/COMPLETED/FAILED/UNKNOWN.

El ticket muestra `REIMPRESIÓN`, conserva logo Opción 07, CP850/ESC t 2, 48 columnas, TOTAL, GS V 1 y `openCashDrawer=false`. El botón se deshabilita durante la solicitud; FAILED/UNKNOWN permiten un reintento explícito relacionado y nunca automático. Un guard por SaleId bloquea dos ejecuciones simultáneas dentro del proceso. Sale, SaleItem, Payment, folio, caja y ExpectedCash permanecen inmutables.

Smoke físico final: exactamente una reimpresión de una venta sintética existente. PrintRequestId `3ca46975-71af-4643-b80b-ff8290a5d356`, SaleId `904ce0fa-510e-4308-a0ed-b093749dfcda`, JobId 7, REQUESTED→SUBMITTED. La fotografía confirmó REIMPRESIÓN, logo, español, valores históricos, corte parcial y ausencia de apertura de cajón. SQLite conservó 55 ventas, 110 partidas, 55 pagos y ExpectedCash $1,852.00; existe un solo request.

Verificación vigente: 195 pruebas aprobadas, 0 fallidas, 0 omitidas; Release con 0 errores y 0 advertencias. Bloque E.1 cerrado: margen superior aceptado, causa predominantemente mecánica, logo sin recorte y backfeed prohibido sin documentación específica Qian. Cajón deshabilitado pendiente de hardware; LAN opcional. El sistema no está listo para producción y no se inició otra fase.

## Registro histórico anterior

# Continuidad — FASE 1G Bloque E

FASE 1G ABIERTA; Bloque E validado físicamente. Logo Opción 07, texto, importes, TOTAL y corte parcial fueron aprobados. La única observación es exceso de blanco antes del logo. Bloque E.1 determinó que el PNG aporta 8 filas/1.00 mm y Casa Miga no emite feed previo; la causa dominante es mecánica. No imprimir de nuevo. Ver PHASE_1G_BLOCK_E1_TOP_MARGIN_REPORT.md. 191 pruebas aprobadas; Release sin errores ni advertencias.

Baseline Bloque D: CP850, ESC t 2, GS V 1. Fuente única: assets/brand/Opcion07. Paquetes anteriores deprecados. Sistema de desarrollo, no listo para producción. No se inició ninguna fase posterior.

## Registro histórico anterior, sustituido por el estado superior

# Estado actual — FASE 1G BLOQUE E, LOGO OFICIAL REQUERIDO

Fecha: 2026-09-11. FASE 1F permanece cerrada. El usuario confirmó físicamente el Bloque D completo: caracteres españoles, legibilidad, tamaños, alineación, ausencia de basura y corte automático parcial correctos. Bloque D implementa selección estricta de codepage en `283cfa4`; el baseline queda fijado en .NET `ibm850`/CP850, `ESC t 2` y `GS V 1`. Git local conserva la identidad Rammseth / `rammstudioworkshop\@gmail.com`; no hay remoto ni push.

Bloque D envió exactamente un ticket sintético: .NET `ibm850`, ESC/POS tabla 2, selección `1B 74 02` una vez en offset cero. Request `957c1076-cd21-4ceb-b609-63b437a6c643`, JobId 5, SUBMITTED, 889 bytes y SHA-256 `83CC7BA9E0339E608891C2B3908021A847053B40B5A3F722C4F4AF44F101321D`. La fotografía `evidence/phase1g-block-d-physical.png` y la confirmación del usuario validan la salida física y el corte parcial.

Bloque C envió exactamente un ticket sintético a POS-80: request `6df7d54d-a095-48e3-9223-dfc09b5bdf51`, JobId 4, 770 bytes, estado SUBMITTED y SHA-256 `792FEFF59A9921E17AD77DFDD03D3B7D65447150B58ED195C159CF443DECB757`. Contiene encabezado 2×2, TOTAL doble alto, dos restauraciones, cuatro LF y un único corte parcial final. No contiene pulso de cajón. Su jerarquía visual fue aprobada y el corte parcial quedó confirmado con el Bloque D.

Windows confirmó `POS-80`, USB001, driver POS-80 11.3.0.1 y estado Normal. El trabajo `7ba5b12f-2482-457d-abf4-725181b25d60` fue aceptado por Winspool con JobId 3 y estado técnico SUBMITTED; fueron 762 bytes ASCII sin ESC, GS, corte ni cajón. Esto no confirma salida física. No se abrió la base comercial ni se crearon venta, pago, caja o cambio en ExpectedCash.

ReceiptDocument y ReceiptLine son inmutables y viven en Application. El documento se deriva de Sale/SaleItem/Payment, captura nombres de usuario/terminal y perfil comercial, usa hora local con offset y conserva Money Int64. El JSON canónico, versión y SHA-256 se guardan en receipt_snapshot. Requests y eventos append-only distinguen ORIGINAL/RETRY/REPRINT y REQUESTED/SUBMITTED/COMPLETED/FAILED/UNKNOWN.

Migración 008 agrega las tres tablas y `sales.receipt.reprint` sólo para Administrator/Manager. `printing.json` es local, opcional y excluido de backup. Sin configuración válida, el cobro permanece operativo y el ticket se marca como no enviado. Checkout intenta imprimir sólo después de recibir la venta confirmada; SQLite no permanece en transacción durante Winspool.

WindowsRawPrintTransport envía RAW a una cola lógica y devuelve JobId. SerializedReceiptPrintTransport limita a un trabajo dentro de la aplicación. El renderer de Bloque A contiene texto únicamente: no emite selección de codepage, inicialización, corte ni cajón. SUBMITTED significa aceptado por spooler, no papel físicamente confirmado.

Verificación vigente: 188 pruebas aprobadas, 0 fallidas, 0 omitidas; build Release con 0 errores y 0 advertencias. El mecanismo controlado no abre la base comercial y no cambia venta, pago, caja ni ExpectedCash.

`sales.view` se concede por migración 007 solamente a Administrator y Manager. Cashier no lo recibe. Presentation oculta Ventas sin permiso y Application revalida en cada consulta que el usuario actual exista, esté activo y conserve el permiso. Consultar historial no exige caja abierta ni `sales.complete`.

El listado devuelve hasta 50 ventas, `COUNT` y página mediante joins, orden `confirmed_utc DESC, id DESC`, búsqueda exacta con `terminal_id + sequence` y rango de días locales. Desde es inclusivo; Hasta se convierte al inicio UTC del día local siguiente y se usa como límite exclusivo. El detalle carga encabezado/pago/usuario/terminal y después partidas por `position`, sin N+1. Sale, SaleItem y Payment no cambiaron y continúan inmutables.

Las lecturas usan `ILocalReadUnitOfWork`: conexión SQLite read-only y `BeginTransaction(deferred: true)`. Las operaciones comerciales conservan `ILocalUnitOfWork.Run` con transacción inmediata. Una prueba mantiene una lectura abierta y confirma simultáneamente un Checkout real para comprobar que el historial no reserva el writer lock.

Estado de aceptación: **FASE 1F CERRADA**. Smoke WPF sobre instalación sintética: 55 ventas, página 1 de hasta 50, página 2 con 5, orden descendente 55→1, folio exacto, formato inválido rechazado, día local con 55 resultados, detalle de dos partidas y pago CASH, Volver/Limpiar y Ventas ausente para Cashier. SQLite después del recorrido conserva 55 ventas, 110 partidas, 55 pagos y 55 auditorías `sale.completed`; migración 7 y dos roles con `sales.view`.

Verificación final: 170 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores, 0 advertencias. Publicación win-x64 autocontenida en `artifacts/development`. Backup `.cmbackup` y restauración CLI a carpeta nueva tienen el mismo hash lógico `C7E4A5392F31988D6BC7857B664594F5BCD3C9F4FF3D7CA71C299D1CC9423F4F` y el mismo `terminal.json`.

Bloque E fue revisado sólo hasta su precondición. La búsqueda dirigida encontró `src/CasaMiga.Presentation/Assets/README.md`, que declara que no existe un logotipo oficial y prohíbe generarlo o redibujarlo. No se modificó código, pruebas, migraciones ni configuración y no se imprimió otro ticket. Para continuar se necesita el archivo oficial aprobado, preferiblemente PNG monocromático con transparencia, alta resolución y proporciones originales.

Limitaciones heredadas de 1F: los nombres mostrados en Historial siguen siendo actuales y no existen snapshots retroactivos. Los tickets anteriores a 1G capturan esos nombres al crear su primer snapshot. La zona horaria depende del equipo y LIMIT/OFFSET deberá revisarse con volúmenes mayores. Sigue siendo una versión de desarrollo y no está lista para producción.
