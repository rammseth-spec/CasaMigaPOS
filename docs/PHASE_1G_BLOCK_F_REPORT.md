# FASE 1G — BLOQUE F — REIMPRESIÓN DESDE HISTORIAL

Fecha: 2026-09-11. Estado definitivo: **BLOQUE F CERRADO**. Recomendación: **FASE 1G CERRADA dentro del alcance autorizado**. El producto continúa como versión de desarrollo y no está listo para producción.

## Resultado funcional

El detalle de Historial incorpora `REIMPRIMIR TICKET` únicamente para un usuario activo con `sales.receipt.reprint`. Administrator y Manager tienen el permiso; Cashier no. Application revalida autorización al solicitar y al persistir el resultado. La vista se deshabilita durante el trabajo y muestra mensajes distintos para SUBMITTED, FAILED y UNKNOWN.

La reimpresión usa el `receipt_snapshot` inmutable. En esta venta anterior a 1G se creó el primer snapshot una sola vez con SHA-256 `F9F178D61DE6BA21D65692EBD51080435D328F3C45043A07C107999B78F91975`; el hash recalculado coincide y el request referencia el mismo valor. No se consultaron catálogo ni precios actuales.

Cada clic aceptado crea un request REPRINT. Un guard por SaleId bloquea dos ejecuciones simultáneas y la UI deshabilita el botón mientras espera. No hay retry automático. FAILED/UNKNOWN sólo permiten un reintento explícito relacionado y UNKNOWN indica revisar físicamente antes de repetir.

## Smoke físico único

El usuario realizó manualmente: login → Ventas → venta existente → Ver detalle → REIMPRIMIR TICKET una vez. Mensaje exacto:

> Ticket aceptado por la cola de impresión. Trabajo 7.

- PrintRequestId: `3ca46975-71af-4643-b80b-ff8290a5d356`.
- SaleId: `904ce0fa-510e-4308-a0ed-b093749dfcda`.
- Folio: `45f13704a99f46d3be6074cf82156181-00000055`.
- kind: `REPRINT`; ParentRequestId: NULL.
- requested_by: `5f316364-9c24-483f-afd3-c0a11d80d11e`, usuario Administrador sintético.
- queueName: `POS-80`.
- Eventos: secuencia 1 REQUESTED; secuencia 2 SUBMITTED con JobId 7 y sin error.
- Auditoría: `receipt.print.requested` y `receipt.print.submitted`.

La fotografía y confirmación del usuario validan: `*** REIMPRESIÓN ***`; logo maestro Opción 07 con techo, pan, dos cortes blancos, estrella y PANADERÍA; ausencia de vapor y líneas laterales; folio, fecha, cajero y terminal; Concha histórica y Bolillo histórico; Subtotal/Total $43.00; CASH; efectivo $93.00; aplicado $43.00; cambio $50.00; importes alineados; TOTAL destacado; `COPIA · NO ABRE CAJÓN`; legibilidad profesional y corte físico correcto.

## Evidencia técnica post-smoke

| Dato | Antes | Después |
|---|---:|---:|
| Sale | 55 | 55 |
| SaleItem | 110 | 110 |
| Payment | 55 | 55 |
| ExpectedCash | $1,852.00 | $1,852.00 |
| receipt_print_request | 0 | 1 |
| receipt_snapshot | 0 | 1 |
| receipt_print_event | 0 | 2 |

La CashSession `a3ed5724-f0c4-4639-ab8c-90b59d915d8f` conserva apertura UTC, fondo $0.00 y estado OPEN; no recibió conteo ni cierre. Sale, SaleItem y Payment son inmutables por esquema y sus conteos/datos coinciden con la venta reimpresa. No hubo otra venta, pago, cambio de folio ni variación de efectivo esperado.

El render determinista del snapshot, sin llamar al transporte, produce 19,787 bytes y SHA-256 `C17C6CA805932AE98032A500B69F035FCE7AA3D6B8317D7537D81E40A95BEB35`. Confirma:

- `ESC t 2` en offset cero, .NET `ibm850`/CP850.
- Logo oficial Opción 07 exacto, 560 × 262 sobre lienzo 576.
- `REIMPRESIÓN` y `COPIA · NO ABRE CAJÓN`.
- Un `GS V 1` al final.
- Cero `ESC p` fuera de los datos raster.
- `openCashDrawer=false`.

El payload real no se persiste después de enviarse; la correlación del trabajo físico se apoya en el snapshot/hash, configuración usada, request/eventos, renderer determinista, mensaje UI y fotografía. SUBMITTED significa aceptación por Winspool, mientras que la salida física y el corte fueron confirmados por el usuario.

Evidencia: `docs/evidence/phase1g-block-f-post-smoke.json`, `phase1g-block-f-render-contract.json` y `phase1g-block-f-human-confirmation.md`.

## E.1, verificación y Git

E.1 queda cerrado: margen superior aceptado; causa predominantemente mecánica; logo Opción 07 intacto; no se recortan ocho filas; backfeed/reverse feed prohibidos sin documentación Qian. No se acepta riesgo mecánico por una mejora máxima aproximada de 1 mm.

Checkpoint inicial: `faa73fa3e87b4f4e689f7e17479833bced43ce42`. Implementación y pruebas: `5bc0ed01fb25e6c8e387d42b7af29221bed2861f`. El hash del commit documental final se informa al cerrar.

Última suite completa válida: 195 aprobadas, 0 fallidas, 0 omitidas. ReceiptTests focalizadas: 22 aprobadas. Build Release: 0 errores, 0 advertencias. Migraciones 001–008 intactas. La prueba automatizada de backup/restore conserva snapshot, request REPRINT y eventos; el smoke no exige otro backup porque sólo agregó trazabilidad cubierta por SQLite y por esa prueba.

## Estado de FASE 1G

FASE 1G puede cerrarse dentro del alcance efectivamente autorizado: ticket post-commit, snapshot, trazabilidad, Winspool RAW por USB, caracteres españoles, logo oficial, corte parcial y reimpresión desde Historial quedaron implementados y validados físicamente.

El cajón permanece explícitamente deshabilitado. Existe el punto de configuración y separación post-commit, pero no se implementará pulso hasta disponer de cajón, modelo, voltaje, conector y comando confirmado. Es soporte futuro pendiente de hardware, no un defecto del flujo de tickets validado. LAN fue declarada validación posterior/opcional y no bloquea el cierre USB.

Pendientes reales para producción: datos comerciales definitivos, hardware de cajón y su futura autorización, posible validación LAN, administración/recuperación de usuarios, zona horaria comercial configurable, política de backup/retención/cifrado, instalador/licencia y pruebas operativas de despliegue. No se inició FASE 1H, inventario, producción, facturación, rediseño, fullscreen ni otra funcionalidad posterior.

Próximo paso recomendado, sin implementarlo: definir el alcance de preparación para producción o, cuando exista hardware, planificar un bloque independiente de cajón con especificación eléctrica/documentación Qian. Modelo recomendado: **SOL** para planificación y tareas acotadas; usar **Astra** sólo si aparece un problema real de driver, pulso eléctrico, Winspool o concurrencia.
