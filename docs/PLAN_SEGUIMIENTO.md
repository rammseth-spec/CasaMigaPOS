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
| 10 | Recetas versionadas (UR 379–397) | Motor hecho; desde el 20/10 una preparación puede usar otras. Desde el 21/10, en el código: aviso de rendimiento sospechoso (más de 10 veces) y marca «Por medir», que impide publicar | 10 preparaciones reales en borrador, 0 publicadas, 0 recetas de pan (`RECETAS_ESTADO.md`) |
| 11 | Producción por lote (planeado, real, merma) que consume insumos y suma pan (UR 401–434) | Motor hecho y probado sólo con DEMO; 0 lotes reales | Depende de las recetas aprobadas |
| 12 | Merma como operación propia con costo estimado (UR 624–647) | Merma hecha, sin costo estimado; sin degustación ni consumo interno | Costo estimado; destinos de degustación y consumo interno |
| 13 | Costos por receta, lote y pieza; compras y proveedores (UR 475–502, 651–684) | Sólo costo de referencia por insumo y reporte de ganancia | Compras, proveedores y costo promedio |
| 14 | Ticket térmico separado del diseño; reimpresión (UR 809–858) | Hecho, con apartado Tickets y cola de impresión | Aprobar en papel el ticket vigente; QR |
| 15 | Cajón: se abre al cobrar en efectivo y hay apertura manual auditada (UR 783–805) | Bloqueado en el código por falta de cajón | Hacerlo cuando haya cajón físico |
| 16 | Roles, permisos y contraseñas (UR 688–718) | Hecho, con alta, restablecimiento y recuperación | En la caja real: cambiar la contraseña del administrador y quitar los usuarios de prueba |
| 17 | Bitácora de auditoría (UR 722–743) | Hecho | — |
| 18 | Respaldos manual y automático con restauración probada (UR 917–931) | Automático al abrir y manual; restauración probada el 20/10 con las 23 ventas | Copia fuera del equipo (la limpieza de respaldos viejos y la configuración de la impresora en el respaldo están en `main` desde el 20/10, sin instalar) |
| 19 | Reportes (UR 1099–1105) | Diario, comparativo, ganancia y CSV | Impresión de reportes |
| 20 | v0.1 sin Internet: abrir → vender → cobrar → imprimir → corte → cajón → cierre (UR 1318–1346) | Todo menos el cajón; falta un cierre real | Cajón y primer cierre real |
| 21 | Instalador (fuera en 1A y 1G; aprobado sin firma el 06/10) | 1.0.0 generado desde `c1a84b4` y probado en una instalación aislada el 20/10 (sin firma) | Probarlo en Windows 10/11 limpios y en la computadora de la tienda |
| 22 | Nube y web (el pedido original las excluía; aprobadas del 06 al 09/10) | Hechas; no publicadas | Publicar en Vercel |
| 23 | Pedidos (`orders-future-requirements.md`) | No hecho | Requiere autorización |
| 24 | Charolas (decisión del 06/10) | Hecho | — |

## Documentos que se contradicen

**Corregidas el 20/10/2026:**
- `AGENTS.md`: ahora dice qué tiene `main`, qué no se implementa sin autorización, dónde está la caja real y las reglas de recetas y reportes.
- `NEXT_STEPS.md` y `PILOTO.md`: la regla «no se agregan funciones» quedó anotada como regla del piloto del 10/10, con lo que se autorizó después.
- `README.md`: estado al 20/10, la nube como opcional y no publicada, y la versión como candidata en piloto.
- `RELEASE_NOTES_1.0.md`: el descuento por venta ya no se llama opcional; está apagado y pendiente del plan. Ningún documento aplaza recetas ni inventario a una 1.1.
- `roadmap.md`: tiene un «Estado actual» y la fase 1I queda marcada como cerrada.
- Nube: se registró `decisions/ADR-024-nube-y-version-web.md`.
- Fotos de IA: anotadas en `roadmap.md` y `NEXT_STEPS.md` como provisionales por decisión del usuario, a reemplazar por fotos reales.
- Recetario y motor: los documentos de recetas ya reflejan las preparaciones dentro de otras y las tandas.

**Siguen abiertas:**
- `CURRENT_STATE.md:50` habla de 31 productos autorizados y el catálogo actual tiene 44. Es una entrada histórica; el catálogo vigente es `docs/catalog/README.md`.
- El recetario da por retirados Puerquito y el bollo CM-018, pero en la caja siguen activos. **Decisión tuya.**

## Pendientes en orden

1. **Ventas reales protegidas** — hecho el 20/10: respaldo completo y restauración probada en una carpeta aparte. Falta una copia fuera del equipo (USB o nube).
2. **Cierre de la caja del 19/10** — con tus cantidades reales; no se registra nada sin ellas.
3. **Acceso** — cambiar la contraseña del administrador y desactivar `encargado` y `cajero` en la caja real.
4. **Recetas reales** — la primera es el **polvorón**: faltan 4 datos (`RECETAS_ESTADO.md`, «Primera receta real»). Con ellos: hacer el recorrido completo en la copia, desde las pantallas (`GUIA_RECORRIDO_POLVORON.md`, la copia ya tiene existencias de prueba) o con la herramienta. Las recetas del rol y del pay ya están en borrador en la copia. Después, el resto del recetario (`RECETAS_ESTADO.md` B y `RECETARIO_PARA_COMPLETAR.md` paso 0) y tu aprobación.
5. **Programa para recetas** — degustación y consumo interno; aceite reutilizable. Ambos necesitan tu decisión.
6. **Conteo real de inventario** y, después, encender el descuento por venta.
7. **Instalar en la caja real lo que está en `main`**: los íconos (`4e8ec88`) ya están instalados desde el 20/10. Faltan las preparaciones dentro de preparaciones y los arreglos del punto 8, que cambian la base a la versión 31, y las dos protecciones del editor de recetas del 21/10 (versión 32: al actualizar, los 10 borradores reales quedan «Por medir»). **Hecho el 22/10 con tu autorización:** la caja real ya corre el programa de `main` (`30fad4b`, versión 1.0.0, base 32), con las ventas, la caja abierta y el catálogo intactos. Las copias para regresar están detalladas en `ACTUALIZAR_CAJA.md`.
8. **Arreglos para la entrega** — hechos en `main` el 20/10, sin instalar todavía en la caja real:
   - el inicio de sesión muestra «Versión 1.0.0» (sólo una copia de prueba dice «Versión de desarrollo»);
   - los respaldos locales llevan la configuración de la impresora y del Centro operativo, y se borran sólo los de más de 30 días, conservando siempre los 20 más recientes;
   - el instalador se detiene si la publicación trae archivos del modo de prueba y nunca los instala;
   - «Pago CASH» ya estaba resuelto: el ticket integrado de QA imprime «Pago: Efectivo»;
   - escala en pantallas con zoom: el programa ya se adapta a la escala de Windows. Sólo falta probarlo en la pantalla de la tienda (punto 10), no hace falta cambiar código.
9. **Instalador** — `CasaMigaPOS-1.0.0-setup.exe` generado el 20/10 desde `c1a84b4` (SHA-256 `B3BD35F4…45CD`) y probado en una instalación aislada: instalar, abrir con datos vacíos y con una copia de la caja, instalar encima y desinstalar sin tocar los datos (`INSTALLER.md`). Falta probarlo en Windows 10/11 limpios y en la computadora de la tienda; va sin firma, así que SmartScreen avisará.
10. **Pruebas físicas** — ticket en papel; cancelar un trabajo real de la cola; computadora de la tienda.
11. **Publicar la nube** (Vercel) y revisar el CI de `main`.
12. **Decisiones abiertas** — descuentos e impuestos, pagos mixtos, corte impreso, cajón, compras y proveedores, pedidos, reglas de devolución, fotos reales.
