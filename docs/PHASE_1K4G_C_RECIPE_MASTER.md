# FASE 1K.4G-C-C-A — Recetario comercial maestro

Fecha de corte: 18/10/2026. Fuente: datos proporcionados directamente por el usuario que elabora los productos de Casa Miga. Este documento conserva cantidades y unidades originales; no crea recetas en SQLite, no autoriza consumo automático y no convierte datos aproximados o inconsistentes en valores exactos.

## Convenciones

- **CONFIRMADO:** dato proporcionado sin calificativo de aproximación y coherente dentro de esta captura.
- **APROXIMADO:** el usuario lo describió como aproximado, habitual o como rango.
- **PENDIENTE:** falta el dato necesario.
- **INCONSISTENTE:** dos cantidades reportadas no concilian; no se inventa merma, absorción o conversión.
- MASS, VOLUME y COUNT se mantienen separados. No se convierten huevos a masa ni gramos a mililitros.
- Una preparación documentada todavía puede ser inelegible para automatización por rendimiento, política COUNT, aproximaciones o limitaciones del motor.

## Principios operativos

Los ingredientes comprados, las preparaciones base, las preparaciones derivadas, las coberturas/rellenos, los productos terminados y los artículos de reventa son conceptos distintos. La producción debe consumir materias primas una sola vez y sumar únicamente las piezas terminadas realmente obtenidas. La venta sólo debe descontar el artículo terminado cuando su vínculo y el consumo automático estén validados. Merma, degustación y consumo interno son destinos distintos. Los rellenos de crema pastelera y zarzamora, y los chocolates de cobertura, se compran listos; no se inventan fórmulas de fabricación.

## 1. Masa dulce base

Fórmula por 1 kg de harina: harina de trigo 1,000 g; azúcar estándar 200 g; margarina 100 g; manteca 100 g; agua 250 ml; sal 20 g; esencia de vainilla 20 g; huevo 4 piezas; levadura fresca 30 g. Alternativa excluyente: Tradi Pan, 2 sobres de 11 g en lugar de la levadura fresca. La indivisibilidad de los sobres y la política de redondeo quedan PENDIENTES.

Tanda habitual: 4 kg de harina. Medición histórica: 5,750 g de masa cruda con 3 kg de harina; no se extrapola a la tanda de 4 kg. Productos: conchas, rol de canela, bollos, rosquita y masa enriquecida. Estado del rendimiento: **PENDIENTE** para una base general; la medición de 3 kg es evidencia histórica, no rendimiento publicado.

## 2. Costra de concha

Manteca vegetal 350 g; azúcar glass 175 g; azúcar estándar 175 g; harina 630 g. Suma teórica 1,330 g; rendimiento medido 1,312 g; diferencia 18 g **INCONSISTENTE** y sin causa confirmada. Se divide en costra blanca y costra de chocolate; a la parte de chocolate se agregan 25 g de cacao. La división de la preparación y el cacao por pieza están PENDIENTES.

Cada concha usa 80 g de masa dulce y aproximadamente 30 g de costra. Ambas cantidades de costra son **APROXIMADAS**. No se registra una pérdida de 18 g ni se publica una versión hasta conciliar rendimiento y división.

## 3. Rol de canela

Tanda: 1,100 g de masa dulce para 13 roles. Relleno de la tanda: margarina 80 g, manteca 20 g, canela 20 g y azúcar estándar 20 g; total 140 g. El dato por tanda y el rendimiento son **CONFIRMADOS**. La división por pieza no es atómicamente exacta: 1,100 g / 13 y 140 g / 13 producen fracciones de miligramo; no se redondean ni se acumula error por pieza.

## 4. Masa dulce enriquecida

Es una preparación derivada: se toma masa dulce base y se incorporan 80 g de mantequilla por preparación. Distribución habitual: 12 cuernitos de mantequilla de 80 g, 7 daneses de crema de 80 g y 5 cuernitos de almendra/bigotes de 70 g; 24 productos y 1,870 g distribuidos. No se asume que la masa base extraída fue 1,790 g: esa resta no está validada como medición real.

- Cuernito de mantequilla: 80 g de masa enriquecida y barnizado de huevo; sin otro acabado. Huevo PENDIENTE.
- Danés con crema: 80 g de masa enriquecida, 21 g de crema pastelera comprada antes de hornear y barnizado; huevo PENDIENTE.
- Cuernito de almendra/bigote: 70 g de masa enriquecida, 6 g de almendra fileteada y 2 g de azúcar glass.

La dependencia masa dulce → masa enriquecida → productos es obligatoria para evitar duplicar ingredientes. El motor actual no admite fórmula→fórmula; véase la auditoría técnica.

> **Actualización 20/10/2026:** la caja ya admite una preparación dentro de otra (migración 031). La masa enriquecida se representa como masa dulce + 80 g de mantequilla, sin repartos por pieza; sólo falta el dato medido de la masa dulce que se toma. Ver `docs/RECETAS_ESTADO.md`.

## 5. Bollos y rosquita

- Bollo de crema: masa dulce 80 g, crema pastelera comprada 15 g antes de hornear, azúcar glass 3 g y aproximadamente 1 ml de barnizado de huevo. Es el mismo producto que el antiguo CM-018, ya inactivo.
- Bollo de zarzamora: masa dulce 85 g y relleno comprado de zarzamora 15 g después de hornear.
- Bollo con ajonjolí: masa dulce 80 g, ajonjolí 6 g y barnizado. Un huevo alcanza para unos 35 bollos: **APROXIMADO**.
- Rosquita de ajonjolí: masa dulce 80 g y aproximadamente 1.5 g de ajonjolí.

Las masas y rellenos indicados sin “aproximadamente” están CONFIRMADOS. Barnizados y ajonjolí de rosquita no son elegibles como consumo exacto todavía.

## 6. Masa salada

Por 1 kg de harina: harina 1,000 g; azúcar estándar 40 g; sal 20 g; levadura fresca 30 g; aceite vegetal 50 ml; agua 550 ml. Alternativa excluyente: levadura seca 10 g en lugar de 30 g de fresca. Tanda habitual: 3 kg de harina.

Producción habitual: 30 baguettes y 15 bolillos de mantequilla, ambos con 100 g de masa cruda por pieza. Baguette: aproximadamente 5 g de ajonjolí y barnizado de huevo PENDIENTE. Bolillo: aproximadamente 7 g de mantequilla antes de hornear. Se prepara además un bolillo pequeño de consumo interno cuyo peso no está medido. La mezcla de salidas puede variar. El rendimiento exacto de masa y el sobrante permanecen PENDIENTES; no se clasifican como merma.

## 7. Masa de donas y berlinesas

Por 1 kg de harina: harina 1,000 g; agua 400 ml; huevo 4 piezas; azúcar estándar 200 g; sal 10 g; levadura fresca 40 g; esencia de vainilla 50 ml; manteca vegetal 100 g. Tanda habitual: 2 kg de harina. Dona: 85 g de masa cruda. Berlinesa: 90 g.

Producción histórica del 14/10/2026: 44 donas, 4 berlinesas de zarzamora y 60 g de masa cruda sobrante. Es una observación concreta, no rendimiento invariable. La reutilización ambiental del sobrante no se automatiza ni se recomienda: contiene ingredientes perecederos y requiere validación sanitaria.

Acabados por pieza:

| Variedad | Acabado |
|---|---|
| Dona de azúcar | azúcar estándar 9 g |
| Dona de chocolate negro | chocolate negro comprado 10 g |
| Dona de chocolate blanco | chocolate blanco comprado 10 g |
| Dona negra con chispas | chocolate negro 10 g + chispas aproximadamente 3 g |
| Dona blanca con coco | chocolate blanco 10 g + coco aproximadamente 5 g |
| Dona con nuez | chocolate de cobertura 10 g + nuez 10 g; tipo de chocolate PENDIENTE |
| Dona combinada | 10 g totales de chocolate; reparto negro/blanco PENDIENTE |
| Dona glaseada | glaseado 15 g |
| Berlinesa de crema | relleno comprado aproximadamente 18 g, después de freír |
| Berlinesa de zarzamora | relleno comprado aproximadamente 18 g, después de freír |

Glaseado: azúcar glass 100 g y agua 10 ml. Su rendimiento real está PENDIENTE; no se declara 110 g por suma de unidades distintas. Aceite de fritura: carga aproximada de 1 L, reutilizada varios días y sustituida semanalmente. No se descuenta 1 L por tanda; falta política de carga, reposición y descarte.

## 8. Polvorones

Fórmula completa: harina 1,800 g; manteca vegetal 1,000 g; azúcar estándar 1,000 g; huevo 2 piezas; Royal/polvo para hornear 80 g. Media tanda habitual: harina 900 g; manteca 500 g; azúcar 500 g; huevo 1 pieza; Royal 40 g.

Sabor excluyente por media tanda: ralladura de 2 naranjas o esencia de vainilla 60 ml. Rendimiento reportado: 18 polvorones de 100 g de masa cruda, utilizando toda la preparación. Existe una diferencia **INCONSISTENTE** entre ingredientes y masa distribuida; no se inventa pérdida. Polvorón de nuez: la misma masa del sabor del día, nuez 12 g por pieza y barnizado; un huevo alcanza aproximadamente 60–70 piezas.

## 9. Galletas

Fórmula completa: harina 1,800 g; azúcar estándar 1,000 g; manteca 500 g; margarina 500 g; huevo 3 piezas; esencia de vainilla 50 ml; Royal 100 g. Media tanda habitual: harina 900 g; azúcar 500 g; manteca 250 g; margarina 250 g; esencia de vainilla 25 ml; Royal 50 g. El usuario utiliza un huevo y sobra un poco; no se sustituye por 1.5 huevos. Debe distinguirse huevo de masa y barnizado.

Rendimiento reportado: 19 galletas comerciales de 100 g y una pequeña de 50 g, total distribuido 1,950 g. La pequeña se parte para degustación, no es merma. La diferencia contra ingredientes es **INCONSISTENTE**.

Extras: nuez 10 g, coco 10 g, chispitas 10 g o semillas 10 g por pieza, respectivamente. Todas usan barnizado de huevo PENDIENTE. La mezcla de semillas de multigrano está PENDIENTE.

## 10. Pay de queso individual

Por tanda: queso crema 360 g; leche condensada 250 g; huevo 3 piezas; leche evaporada 200 ml; esencia de vainilla 10 ml; fécula de maíz 15 g. Rendimiento: 15 pays. Cada pay usa 20 g de masa cruda de galleta; 300 g en total. No se descuentan galletas terminadas: se consume la preparación compartida de galleta.

## 11. Pan de muerto

Por 1 kg de harina: harina 1,000 g; leche 400 ml; azúcar estándar 250 g; margarina 150 g; levadura fresca 40 g; huevo 4 piezas; sal 10 g. Tanda habitual: 2 kg de harina con ingredientes proporcionales.

Rendimiento reportado: 15 grandes y 19 pequeños. Grande: cuerpo 100 g + dos huesitos de 10 g = 120 g. Pequeño: cuerpo 50 g + dos huesitos de 7 g = 64 g. Total distribuido 3,016 g. La diferencia contra ingredientes es **INCONSISTENTE**; los huesitos usan la misma masa. Acabado posterior al enfriado: margarina y azúcar, cantidades PENDIENTES.

## 12. Productos sin receta y reventa

Muffin sencillo, muffin de nuez y vaso de café: receta/insumos PENDIENTES. Leche Kirkland entera y deslactosada light: productos comprados para reventa, sin receta de fabricación. Puerquito y Coronita de nuez están retirados; no se crean recetas para ellos.

## Pendientes indispensables

1. Rendimiento base medido de masa dulce para la tanda que se automatizará y cantidad real extraída para masa enriquecida.
2. Política COUNT para huevo y sobres de Tradi Pan; confirmar si los sobres se controlan como piezas indivisibles.
3. División blanca/chocolate de la costra, cacao por pieza y explicación o aceptación formal del rendimiento 1,312 g.
4. Rendimiento real del glaseado; tipo de chocolate de dona con nuez y reparto de la combinada.
5. Consumo exacto de huevo en barnizados, masa de galleta y cada familia donde hoy es aproximado o pendiente.
6. Rendimientos medidos de masa salada, polvorón, galleta y pan de muerto; no usar diferencias como merma implícita.
7. Mezcla de semillas multigrano, acabados de pan de muerto, recetas de muffins y componentes del café.
8. Política operativa para aceite de fritura, sobrantes/reproceso, degustación y consumo interno.
9. Resolver que la esencia de vainilla se reporta en gramos para masa dulce y en mililitros para otras fórmulas; un InventoryItem sólo admite una dimensión.

## Seguridad alimentaria

La documentación no valida vida útil ni procedimientos sanitarios. En especial, no se automatiza ni recomienda conservar a temperatura ambiente masa cruda con huevo para usarla al día siguiente. La reutilización, refrigeración, descarte y trazabilidad requieren una política sanitaria autorizada antes de cualquier función operativa.
