# FASE 1K.4H-A — Reporte Diario Core

Fecha técnica: 09/10/2026

Estado: implementación y focales completas; verificación final registrada al cierre de este documento.

Baseline: `cf1b33ba5c1247542376ef775f2130eaf58838f0` (`master`, limpio).

Publicación: no push, no PR.

## Resultado

Se agregó **Reporte diario** como módulo de consulta con icono vectorial propio. La fecha inicial es el día local actual y usa el mismo `DateEntryControl` enmascarado de Historial (`dd/MM/yyyy` con calendario). La vista muestra cuatro KPIs: ventas, tickets, piezas y ticket promedio; resumen consolidado de caja; sesiones relacionadas con el día; cinco productos más vendidos; una frase humana; y estados explícitos para datos operativos todavía no disponibles.

El reporte es de sólo lectura. `DailyReportService` autoriza con los permisos existentes `sales.view` y `cash.view`, convierte `[inicio local, inicio del día siguiente)` a UTC y ejecuta `ILocalStore.QueryDailyReport` dentro de la misma transacción read-only que revalida la identidad. `SqliteDailyReportStore` usa consultas agregadas y acotadas por fecha; no carga el historial completo ni ejecuta consultas por fila.

## Auditoría de fuentes

- **Venta válida:** el esquema no tiene estados de cancelación o anulación. Cada fila de `sale` es un agregado comercial completado e inmutable; el trigger `sale_complete` exige líneas y un pago cuyo aplicado coincide con el total. El reporte incluye sólo `sale` unida a `payment.method='CASH'`.
- **Importe vendido:** `SUM(payment.applied_cents)`. No se calcula con recibido menos cambio.
- **Tickets:** número de ventas persistidas en el intervalo.
- **Piezas:** `SUM(sale_item.quantity)`.
- **Ticket promedio:** importe aplicado dividido entre tickets, redondeado al centavo con `decimal` y `MidpointRounding.AwayFromZero`; cuando no hay tickets devuelve Money `$0.00` sin división por cero.
- **Productos:** `product_name`, `quantity` y `line_total_cents` de `sale_item`; conserva el snapshot histórico. Orden estable: piezas descendente, importe descendente, nombre ascendente y `product_id` como desempate final. Sólo aparecen productos vendidos y se limita a cinco.
- **Categorías:** se omitieron. La categoría no está snapshotteada en la venta; unir con `product.category_id` usaría el catálogo mutable actual y podría reescribir el pasado.
- **Reimpresión:** `receipt_print_request` y `receipt_print_event` no participan en la consulta. Una prueba inserta una solicitud/evento de reimpresión y confirma que ventas, tickets y piezas no cambian.

## Caja

Se incluyen sesiones que se solapan con el día local: abrieron antes de terminar el día y no cerraron antes de iniciarlo. Esto cubre sesiones nocturnas sin asumir que apertura y cierre suceden en la misma fecha.

Cada sesión muestra terminal, responsable de apertura, responsable de cierre cuando difiere, horas locales, fondo, ventas CASH aplicadas de la sesión, esperado, contado, diferencia y nota. Para una caja abierta, esperado se calcula como fondo más pagos aplicados; contado y diferencia se muestran como **Pendiente**. Para una caja cerrada se respetan `expected_cents`, `counted_cents`, `difference_cents` y `discrepancy_note` persistidos. Un cierre histórico sin dato persistido aparece como **No disponible**, nunca como cero.

Si existen varias sesiones, se consolidan fondo, efectivo vendido, esperado, contado y diferencia cuando todos los datos requeridos existen, y se presenta una lista breve de cada sesión. Una sesión abierta mantiene contado/diferencia consolidados en Pendiente. El estado superior usa `EN CURSO` para el día actual con caja abierta, `CAJA ABIERTA` para otra fecha todavía abierta, `CERRADO` cuando todas las sesiones relacionadas están cerradas y `SIN SESIÓN DE CAJA` cuando no existe una.

## Diferencia entre cero y dato no disponible

- Día consultado sin ventas: ventas `$0.00`, tickets `0`, piezas `0`, promedio `$0.00` y texto «No hubo ventas registradas este día». Son ceros confirmados por consulta.
- Sin sesiones: métricas de caja «No disponible».
- Producción: «No disponible · Completa recetas y perfiles…».
- Materias primas: «Pendiente de configuración»; no se infiere consumo ni existencias.
- Preparación para mañana: «No disponible» hasta completar recetas, inventario y reservas.
- Merma: diferida a 1K.4H-B porque el fixture actual no permite un resumen comercial confiable.

## Archivos funcionales

### Application

- `src/CasaMiga.Application/DailyReportService.cs`
- `src/CasaMiga.Application/LocalPorts.cs`

### Infrastructure

- `src/CasaMiga.Infrastructure/SqliteDailyReportStore.cs`

### Presentation

- `src/CasaMiga.Presentation/DailyReportViewModel.cs`
- `src/CasaMiga.Presentation/DailyReportView.xaml`
- `src/CasaMiga.Presentation/DailyReportView.xaml.cs`
- `src/CasaMiga.Presentation/MainViewModel.cs`
- `src/CasaMiga.Presentation/MainWindow.xaml`
- `src/CasaMiga.Presentation/MainWindow.xaml.cs`

### Pruebas

- `tests/CasaMiga.Tests/DailyReportTests.cs`
- `tests/CasaMiga.PhotoTests/DailyReportUsabilityTests.cs`

## Cobertura focal

Focal Application/Infrastructure: **4/4**. Regresión ampliada de reporte, ventas, caja, cierre y ticket: **79/79**.

Focal WPF: **4/4**. Regresión WPF de reporte, Historial, Caja, header, Centro operativo y F11: **27/27**.

Se cubren límites de medianoche local inclusivo/exclusivo, agregado financiero, piezas, promedio monetario, snapshots, ranking y desempate, día vacío, caja abierta, cierre exacto, faltante con nota, múltiples sesiones, permisos revalidados, cero mutación, reimpresión invariante, placeholders confiables y layout a 1280×820, 1920×1080 y 3440×1440.

## Fixture de revisión

- DataDirectory: `work/phase1k4h-a-smoke-20261009`
- Schema: 17.
- Manifest local: `work/phase1k4h-a-smoke-20261009/daily-report-fixture-manifest.json`.
- Escenarios: 05/10 sin ventas; 06/10 cierre exacto; 07/10 faltante con nota; 08/10 dos sesiones; 09/10 dos ventas, producto repetido y caja abierta.
- Totales del 09/10: 2 tickets, 5 piezas y $70.00.
- Datos: 6 ventas sintéticas, 6 pagos y 5 sesiones; 0 requests/eventos de impresión.
- Credencial `catalog-admin` verificada durante la creación de la copia.

## Verificación final

- Suite general: **581/581**.
- Suite WPF: **210/210**.
- Total: **791/791**; 0 fallidas, 0 omitidas.
- Release: **0 errores, 0 advertencias**.
- `git diff --check`: correcto.
- Base protegida inicial/final: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D` / `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
- Schema aplicación/fixture: 17. Base protegida: 16. No existe migración 018.
- Recetas comerciales, ticket/ESC-POS/Qian/printing y `operations-hub.json`: sin cambios.

## Entrega manual

- EXE: `src/CasaMiga.Presentation/bin/Phase1K4H-A/CasaMiga.Presentation.exe`.
- Launcher: `work/Abrir-Casa-Miga-1K4H-A.cmd`.
- DataDirectory: `work/phase1k4h-a-smoke-20261009`.
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.4H-A`.
- El launcher limpia `CASAMIGA_DATA_DIR`, usa `development.settings.json` junto al EXE y abre exclusivamente la copia aislada.
- La aplicación se deja en login. No se inicia sesión, no se imprime, no se abre cajón y no se realiza ninguna operación.

## Riesgos y siguiente paso

El índice `ix_sale_history(confirmed_utc DESC,id DESC)` cubre el filtro principal. Caja no tiene índice temporal específico, pero su cardinalidad es de sesiones, no de líneas/ventas, y la consulta se ejecuta una vez; 1K.4H-A no justifica migración 018. Antes de agregar tendencias extensas o reportes multianuales debe medirse el volumen real.

El siguiente paso recomendado, sólo con autorización expresa, es 1K.4H-B para decidir merma y resúmenes productivos cuando existan datos confiables. Modelo recomendado: GPT-6 Sol con esfuerzo alto. No se inicia automáticamente 1K.4H-B ni se retoma 1K.4G-B/C.
