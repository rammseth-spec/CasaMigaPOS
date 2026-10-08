# Ganancia y análisis de charolas

Decisión del usuario (08/10/2026): el costo de cada pan se **calcula desde las recetas**. Se ve en la web: **Reportes → Ganancia** y **Charolas → Análisis**. Son estimaciones para decidir precios y charolas, no contabilidad.

## De dónde sale el costo

1. **Costo de cada insumo** (migración 025, tabla `inventory_item_cost`): precio de compra de referencia, por ejemplo $28.50 por kg. Se captura en la web, **Inventario → tocar el insumo** (permiso `inventory.configure`: administrador); la caja lo aplica como cualquier otro cambio de la web (`inventory.cost`, `IngredientCostService`, auditoría `inventory.cost.set`) y lo sube junto con el insumo. No mueve existencias.
2. **Preparación** (fórmula publicada): ingredientes + la alternativa preferida de cada grupo (menor `preference_rank`), entre lo que rinde. Resultado: costo por mg, µL o pieza de la preparación.
3. **Pan** (perfil de producción publicado del producto): ingredientes directos + la cantidad de cada preparación que lleva una pieza.

Si un pan no tiene receta publicada, o alguno de sus insumos no tiene costo, queda **sin costo**: aparece con sus piezas y su venta, pero no entra en la ganancia, y la tabla «Costo por pieza» dice qué falta (la receta o qué insumos).

Ejemplo (prueba `cloud/test/analytics.test.ts`): masa con 600 g de harina a $20/kg, 200 g de azúcar a $30/kg y 4 huevos a $3 rinde 1 kg → $30/kg. Una concha con 10 g de azúcar y 60 g de masa cuesta $0.30 + $1.80 = **$2.10**.

## Política de redondeo

Los costos por unidad atómica son fraccionarios (centavos por mg). Se calcula en coma flotante y **se redondea una sola vez: el costo por pieza al centavo** (mitad hacia arriba). Todos los totales son costo por pieza × piezas, en centavos enteros, así que suman igual en cualquier orden. El porcentaje de margen se redondea al entero sólo al mostrarlo.

## Ganancia (Reportes → Ganancia)

Del periodo y la sucursal elegidos:

- **Mostrador**: piezas vendidas menos devueltas (las devoluciones del mismo periodo, partida por partida) × (precio cobrado − costo).
- **Charolas**: lo cobrado de las charolas **recogidas** en el periodo − costo de **todo lo que se entregó**: las piezas que regresan también costaron. Se muestra aparte cuánto regresó y cuánto costó.
- **Margen** = ganancia ÷ venta de los panes con costo. Si sólo algunos panes tienen costo, la tarjeta dice «Venta con costo $X de $Y».
- **Por pan**: piezas e ingreso en mostrador y en charolas, ganancia por pieza en cada canal, piezas que regresaron y ganancia total.

Permisos: `recipe.view` y `sales.view` (administrador y encargado).

## Análisis de charolas (Charolas → Análisis)

Por tienda, en el periodo elegido: número de charolas, **cada cuántos días** pide (promedio entre entregas), **qué días suele** pedir, cobrado, piezas vendidas / regresadas con el porcentaje vendido, última entrega y **lo que más se vende allí** (hasta 8 panes, con lo que regresó de cada uno). Las charolas anuladas no cuentan. Permiso `consignment.view`.

El pan más vendido de la tienda (mostrador) sigue en Reportes → Comparativo y en Producción sugerida.

## Pendiente

- El costo se captura sólo desde la web; la caja lo guarda pero no tiene pantalla para editarlo.
- No incluye mano de obra, gas, renta ni empaque salvo que el empaque esté en la receta como insumo.
- Un costo nuevo cambia también la ganancia de periodos anteriores (no se guarda historial de costos).
