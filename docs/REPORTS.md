# Reportes

Pantalla **Reportes** (permiso `sales.view`: Administrator, Manager). Sólo lectura, sin migraciones. Todos los días y horas son **locales** de la caja; la base guarda UTC. Cada pestaña exporta su propio CSV (UTF-8 con BOM, fórmulas neutralizadas).

| Pestaña | Qué responde | Regla |
|---|---|---|
| Resumen | ¿Cuánto vendimos? | Ventas por fecha de venta; devoluciones por fecha de devolución. Por producto, forma de pago, cajero y cortes de caja de los turnos abiertos en el periodo. |
| Comparativo | ¿Vamos mejor o peor? | Contra el periodo **inmediato anterior de la misma duración**: hoy contra ayer, 7 días contra los 7 previos, del 1 al 6 contra los 6 días anteriores. Cambio % = (actual − anterior) / anterior; sin base (anterior = 0) se muestra «Nuevo». Mismas cifras que Resumen. |
| Ventas por hora | ¿Cuándo hay más gente? | Número de ventas por día de la semana × hora (mínimo de 7:00 a 20:00, se amplía si hay ventas fuera). Excluye ventas canceladas. Con periodos de menos de 7 días se avisa que el patrón semanal está incompleto. |
| Control de caja | ¿Dónde se pierde dinero? | Faltantes y sobrantes de los cortes contados, atribuidos a quien **abrió** la caja. Devoluciones atribuidas a quien **cobró** la venta original (las autoriza un encargado). Motivos agrupados sin distinguir mayúsculas ni espacios. |
| Producción | ¿Cuánto hornear mañana? | Para el día elegido: promedio de piezas del **mismo día de la semana** en las últimas 4 semanas, contando sólo los días en que la sucursal vendió algo (un día cerrado no es demanda cero), más 10 % de margen, redondeado hacia arriba. Sólo productos activos; excluye ventas canceladas pero no devoluciones (la demanda existió). Indica cuántas de las 4 semanas tienen datos. |

**Gráficas** (barras dibujadas en WPF, sin librerías externas): Resumen muestra ventas por día (por semana si el periodo pasa de 31 días), los 10 productos con más venta neta y la mezcla de formas de pago; Comparativo dibuja cada día junto al día equivalente del periodo anterior; Control de caja, los motivos de devolución; Producción, una barra junto a las piezas sugeridas. Al terminar, «Generar» muestra «Actualizado a las hh:mm:ss» para confirmar que corrió aunque las cifras no cambien.

Cálculos en `src/CasaMiga.Application/AnalyticsReports.cs` (funciones puras, probadas sin base de datos) y consultas en `src/CasaMiga.Infrastructure/SqliteReportStore.cs`. Pruebas: `ReportTests`, `AnalyticsTests`, `AnalyticsReportServiceTests`.

Pendiente de decidir con el negocio: margen de seguridad por producto (hoy 10 % fijo para todos), restar mermas registradas en Inventario al pronóstico, y comparar contra «el mismo día de la semana pasada» además del periodo inmediato anterior.
