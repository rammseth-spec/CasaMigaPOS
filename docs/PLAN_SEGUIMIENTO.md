# Plan acordado y pendientes — seguimiento

Actualizado: 20/10/2026. Compara los compromisos del plan original con lo que hay en `main`. **No cambia el alcance**: nada de esto queda aplazado sin aprobación del usuario.

Fuente del plan: `docs/USER_REQUIREMENTS.md` ("UR", pedido original), `docs/roadmap.md` y las instrucciones de cada fase (`docs/USER_REQUIREMENTS_PHASE_*.md`). Las decisiones posteriores del usuario están en `SYNC_PLAN.md`, `WEB.md`, `POS_WEB.md`, `CONSIGNMENT.md`, `INSTALLER.md` y `BACKUPS.md`.

## Compromisos y estado real

| # | Compromiso original | Estado real en `main` | Trabajo pendiente |
|---|---|---|---|
| 1 | Venta rápida: categorías → productos → ticket → cobro (UR 885–913) | Hecho; en uso con 23 ventas reales | — |
| 2 | La venta registra descuento e impuestos (UR 506–533) | No hay descuentos ni impuestos (las fases 1E y 1F los dejaron fuera) | Decidir si entran |
| 3 | Cancelaciones y devoluciones auditables (UR 506–533, 722–743) | Hechas, con reglas por defecto sin confirmar | Confirmar las reglas; la devolución no regresa el pan al inventario; comprobante impreso |
| 4 | Efectivo y tarjeta; varios pagos en una venta (UR 553–567) | Efectivo, tarjeta y transferencia; un solo pago por venta | Decidir si hacen falta pagos mixtos |
| 5 | Caja: apertura, fondo, entradas y salidas, conteo, cierre y diferencia (UR 571–600) | Hecho | El primer cierre real (la caja del 19/10 sigue abierta) |
| 6 | Corte con desglose (UR 604–620) | En pantalla y en CSV | Corte impreso |
| 7 | Catálogo con historial (UR 330–346) | Hecho; 44 productos | Fotos reales (hoy son ilustraciones de IA) |
| 8 | Inventario sólo por movimientos (UR 438–471) | Motor hecho; 0 existencias en la caja real | Conteo inicial real |
| 9 | Completar la venta crea sus movimientos de inventario (UR 969–985) | Programado y apagado | Encenderlo con recetas aprobadas y conteo real |
| 10 | Recetas versionadas (UR 379–397) | Motor hecho; desde el 20/10 una preparación puede usar otras | 10 preparaciones reales en borrador, 0 publicadas, 0 recetas de pan (`RECETAS_ESTADO.md`) |
| 11 | Producción por lote (planeado, real, merma) que consume insumos y suma pan (UR 401–434) | Motor hecho y probado sólo con DEMO; 0 lotes reales | Depende de las recetas aprobadas |
| 12 | Merma como operación propia con costo estimado (UR 624–647) | Merma hecha, sin costo estimado; sin degustación ni consumo interno | Costo estimado; destinos de degustación y consumo interno |
| 13 | Costos por receta, lote y pieza; compras y proveedores (UR 475–502, 651–684) | Sólo costo de referencia por insumo y reporte de ganancia | Compras, proveedores y costo promedio |
| 14 | Ticket térmico separado del diseño; reimpresión (UR 809–858) | Hecho, con apartado Tickets y cola de impresión | Aprobar en papel el ticket vigente; QR |
| 15 | Cajón: se abre al cobrar en efectivo y hay apertura manual auditada (UR 783–805) | Bloqueado en el código por falta de cajón | Hacerlo cuando haya cajón físico |
| 16 | Roles, permisos y contraseñas (UR 688–718) | Hecho, con alta, restablecimiento y recuperación | En la caja real: cambiar la contraseña del administrador y quitar los usuarios de prueba |
| 17 | Bitácora de auditoría (UR 722–743) | Hecho | — |
| 18 | Respaldos manual y automático con restauración probada (UR 917–931) | Automático al abrir y manual; restauración probada el 20/10 con las 23 ventas | Copia fuera del equipo; limpiar respaldos viejos; incluir la configuración de la impresora |
| 19 | Reportes (UR 1099–1105) | Diario, comparativo, ganancia y CSV | Impresión de reportes |
| 20 | v0.1 sin Internet: abrir → vender → cobrar → imprimir → corte → cajón → cierre (UR 1318–1346) | Todo menos el cajón; falta un cierre real | Cajón y primer cierre real |
| 21 | Instalador (fuera en 1A y 1G; aprobado sin firma el 06/10) | Hecho y sin firma; el candidato es anterior a los cambios del 19 y 20/10 | Generarlo desde `main` y probarlo en Windows 10/11 limpios |
| 22 | Nube y web (el pedido original las excluía; aprobadas del 06 al 09/10) | Hechas; no publicadas | Publicar en Vercel |
| 23 | Pedidos (`orders-future-requirements.md`) | No hecho | Requiere autorización |
| 24 | Charolas (decisión del 06/10) | Hecho | — |

## Documentos que se contradicen

- `AGENTS.md:4` prohíbe cancelaciones, devoluciones, inventario, tarjetas y pantalla completa, pero `main` ya los tiene. Está desactualizado.
- `NEXT_STEPS.md:3` y `PILOTO.md:3` dicen «no se agregan funciones», pero después se agregaron Tickets, cobro rápido, íconos, carga de recetas y ahora preparaciones dentro de preparaciones.
- `README.md` dice a la vez «versión 1.0 candidata… por la nube», «NO lista para producción» y «no hay servicios cloud».
- `RELEASE_NOTES_1.0.md` llama **opcional** al descuento de inventario, mientras el pedido original lo pide al completar cada venta. Tampoco ningún documento aplaza las recetas o el inventario a una 1.1: esa idea fue una sugerencia mía que no aprobaste y la retiro.
- `roadmap.md` marca la fase 1I como abierta en una línea y cerrada en otra, y no menciona la 1.0, la nube ni el piloto.
- La nube se decidió sin el ADR que pide `roadmap.md:56`.
- `roadmap.md:4` y `NEXT_STEPS.md:522` prohíben fotos de IA, pero el catálogo usa fotos provisionales hechas con IA.
- `CURRENT_STATE.md:50` habla de 31 productos autorizados; el catálogo actual tiene 44.
- El recetario da por retirados Puerquito y el bollo CM-018, pero en la caja siguen activos.

## Pendientes en orden

1. **Ventas reales protegidas** — hecho el 20/10: respaldo completo y restauración probada en una carpeta aparte. Falta una copia fuera del equipo (USB o nube).
2. **Cierre de la caja del 19/10** — con tus cantidades reales; no se registra nada sin ellas.
3. **Acceso** — cambiar la contraseña del administrador y desactivar `encargado` y `cajero` en la caja real.
4. **Recetas reales** — datos del recetario (`RECETAS_ESTADO.md` B y `RECETARIO_PARA_COMPLETAR.md` paso 0). Después: publicar en una copia, recorrido completo y aprobación.
5. **Programa para recetas** — degustación y consumo interno; aceite reutilizable. Ambos necesitan tu decisión.
6. **Conteo real de inventario** y, después, encender el descuento por venta.
7. **Instalar en la caja real lo ya verificado**: íconos (`4e8ec88`) y preparaciones dentro de preparaciones. Esto último cambia la base a la versión 31 y requiere tu aprobación.
8. **Arreglos para la entrega**:
   - quitar «Versión de desarrollo» del inicio de sesión y mostrar la versión;
   - limpiar los respaldos viejos y respaldar la configuración de la impresora;
   - evitar que el modo de prueba se cuele en el instalador;
   - escala en pantallas con zoom;
   - «Pago CASH» en el ticket.
9. **Instalador** desde `main`, probado en Windows 10/11 limpios.
10. **Pruebas físicas** — ticket en papel; cancelar un trabajo real de la cola; computadora de la tienda.
11. **Publicar la nube** (Vercel) y revisar el CI de `main`.
12. **Decisiones abiertas** — descuentos e impuestos, pagos mixtos, corte impreso, cajón, compras y proveedores, pedidos, reglas de devolución, fotos reales.
