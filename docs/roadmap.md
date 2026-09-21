# Checkpoint vigente — FASE 1J

- 1J.1: Domain aprobado y conservado.
- 1J.2: COMPLETADA: persistencia, migración 013, snapshots y contrato histórico; 466/466 pruebas y Release sin errores/advertencias.
- 1J.3: NO iniciada. Proponer y autorizar confirmación atómica, stock, idempotencia/recuperación y políticas pendientes antes del primer movimiento.
- UI de fórmulas/producción, datos productivos, rediseño y fullscreen: fuera de esta entrega.

Desarrollo, NO listo para producción. Ver PHASE_1J2_REPORT.md.

## Roadmap anterior

# Roadmap y aceptación

| Fase | Alcance | Condición de salida |
|---|---|---|
| 0 | Stack, arquitectura, solución, migración, logging, configuración, tests, backup inicial | Build y tests; restauración verificada; límites y siguiente tarea documentados |
| 1 | Catálogo, caja, efectivo/tarjeta, venta y ticket estructurado | Flujo offline probado; rollback/idempotencia/cierre y snapshots; identidad/permisos/auditoría mínimos |
| 2 | Qian real, USB/LAN, impresión, corte y cajón | Ficha exacta; pruebas físicas con fallos/desconexiones y resultado desconocido |
| 3 | Ingredientes, unidades, recetas versionadas, lotes y merma | Trazabilidad de versión y atomicidad de consumo/salida |
| 4 | Compras, proveedores, valoración, inventario y costos | Reconciliación ledger y costos históricos inmutables |
| 5 | Administración, reportes, seguridad y backup avanzados | Restauración operativa ensayada y permisos revisados |
| 6 | Sólo bajo necesidad: servidor/PostgreSQL/multiterminal/sucursales | Nuevo ADR y pruebas de concurrencia; no compartir archivo SQLite |

Decisión explícita de 1E: vender sin inventario dentro del alcance autorizado. TrackInventory continúa como metadato y no comprueba existencias. El ledger deberá definirse en una fase posterior; no afirmar control de stock antes de implementarlo.

v0.1 exige hardware real y corte confiable: FASE 1 sola no cumple el hito. Fuera de alcance: CFDI/SAT, nómina, CRM, web tienda, nube compleja, lealtad, IA y móvil.

Definition of Done por tarea: compila sin nuevas advertencias relevantes; pruebas críticas; manejo de fallos; persistencia consistente; docs actuales; dependencias/licencias justificadas; sin secretos; revisión del cambio. Pruebas UI no sustituyen atomicidad. FASE 0 no se equipara a certificación de producción.

## Incrementos de FASE 1

| Incremento | Estado / aceptación |
|---|---|
| 1A: identidad, permisos y apertura | Implementado: 30 tests, Release sin warnings, flujo visual y recuperación al reiniciar verificados. Detener aquí. |
| 1B: cierre básico | Implementado: conteo ciego, cierre/auditoría atómicos, historial persistido y backup completo restaurado; 49 tests. Sin ventas ni hardware. |
| 1C: catálogo base | Implementado y verificado: categorías/productos, permisos, auditoría, 82 tests y smoke A–F. |
| 1D: POS y carrito transitorio | Implementado: 120 pruebas, smoke A–H, captura fatal, UI y backup. Sin venta persistida ni cobro. |
| 1E: venta persistida y cobro efectivo | **CERRADA**: 153 pruebas, recuperación, backup/restore y smoke WPF de 24 pasos completados. |
| 1F: historial y detalle de ventas | **CERRADA**: permiso, lectura deferred, migración 007, 170 pruebas, smoke y restore. |
| 1G: ticket e impresión | **CERRADA en alcance autorizado**: USB/Winspool, snapshot, CP850, logo, corte y reimpresión validados; 195 pruebas. Cajón diferido por falta de hardware y LAN opcional. |
| 1H: inventario y movimientos | **CERRADA**: Domain exacto, esquema 10, ledger/balance, Application, UI, permisos, smoke, restart y backup/restore; 290 pruebas. Sin consumo por venta. |
| 1I: integración venta–inventario | **ABIERTA, 1I.1–1I.4 completadas**: checkout atómico y recovery, schema 12. Flag inicial DISABLED; 1I.4 completada, smoke humano confirmado. |
| 1I.5 y posteriores | Sin iniciar; requieren alcance y autorización nuevos. |

## Actualización FASE 1B

1A aprobada. 1B implementada y verificada: 49 tests, cierre ciego y por tercero autorizado, persistencia A–D y backup completo restaurado con terminal. Detenerse aquí.

Actualización 1C: 1B aprobada; catálogo implementado, migración 004, smoke y backup/restore verificados. Detenerse al entregar reporte 1C. El resto del roadmap permanece como propuesta.


## Actualización FASE 1D (histórica)

1C aprobada. 1D implementa captura fatal, POS y carrito transitorio; 120 pruebas y smoke A–H, publicación y respaldo. Cart is transient and not a sale. Detenerse al entregar el reporte; 1E requiere autorización y definición comercial nuevas. Las notas de detención 1A–1C anteriores son históricas, no el alcance vigente.

## Cierre FASE 1I / 1I.5

FASE 1I CERRADA tras aprobación humana explícita. Ver PHASE_1I5_REPORT.md y evidencia phase1i5-*. Schema 12, sin migración 013; 402 pruebas aprobadas. Sin cambios arquitectónicos ni política comercial en el cierre. Versión de desarrollo, no lista para producción. No fase posterior iniciada. La caja del smoke permanece OPEN; no usar estos datos sintéticos como instalación comercial.

## FASE 1J.1 — Domain puro

Fórmulas técnicas compartidas sin inventario intermedio obligatorio; FormulaVersion sólo referencia ingredientes físicos. ProductProductionProfileVersion referencia versiones de fórmula y componentes directos. ProductionPlan agrega preparación por versión antes de escalado y COUNT por línea; ProductionActualOutput no recalcula consumo. Ver ADR-021 y PHASE_1J1_REPORT.md. Sin schema, persistencia, UI, permisos ni cambios de 1I. 1J abierta; 1J.2 no iniciada.
