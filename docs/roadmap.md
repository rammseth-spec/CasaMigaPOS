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
| 1E: venta persistida y cobro efectivo | Autorizada e implementada: 153 pruebas, recuperación y backup/restore; smoke visual final pendiente. |
| Posteriores a 1E | Sin iniciar; requieren alcance y autorización nuevos. |

## Actualización FASE 1B

1A aprobada. 1B implementada y verificada: 49 tests, cierre ciego y por tercero autorizado, persistencia A–D y backup completo restaurado con terminal. Detenerse aquí.

Actualización 1C: 1B aprobada; catálogo implementado, migración 004, smoke y backup/restore verificados. Detenerse al entregar reporte 1C. El resto del roadmap permanece como propuesta.


## Actualización FASE 1D (histórica)

1C aprobada. 1D implementa captura fatal, POS y carrito transitorio; 120 pruebas y smoke A–H, publicación y respaldo. Cart is transient and not a sale. Detenerse al entregar el reporte; 1E requiere autorización y definición comercial nuevas. Las notas de detención 1A–1C anteriores son históricas, no el alcance vigente.
