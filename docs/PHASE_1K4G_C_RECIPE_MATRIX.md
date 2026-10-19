# FASE 1K.4G-C-C-A — Matriz producto → receta

Fecha de corte: 18/10/2026. Catálogo de referencia: copia aislada `work/phase1k4g-c-b-review-20261018`, consultada en modo de sólo lectura. Contiene 36 productos activos. La matriz no publica fórmulas ni perfiles y no autoriza movimientos de inventario.

## Criterio de clasificación

- **COMPATIBLE DESPUÉS DE RESOLVER DATOS:** el modelo vigente puede representar la estructura, pero faltan rendimientos exactos, cantidades, unidades o políticas COUNT.
- **REQUIERE EXTENSIÓN:** una carga fiel exige una capacidad que el modelo actual no tiene, como fórmula derivada o cantidad fija por tanda.
- **SIN RECETA / REVENTA:** se compra terminado. Puede vincularse posteriormente a inventario de producto terminado sin perfil de producción.
- **PENDIENTE:** no existe información suficiente para diseñar una receta.

En el estado actual hay **0 productos listos para automatización**: la copia C-B contiene cero fórmulas, versiones, perfiles e ítems terminados vinculados. Aun después de capturar esos registros, cualquier cantidad aproximada, rendimiento inconsistente o política pendiente debe seguir bloqueando el consumo automático.

## Matriz de los 36 productos activos

| Categoría | Producto | SKU | ProductId | Base o preparación | Directos/acabados documentados | Rendimiento o gramaje | Estado técnico | Control contra doble consumo |
|---|---|---|---|---|---|---|---|---|
| Bebida | Vaso de café | CM-024 | `63198b97-7df4-4e00-82e6-063a772b5a44` | PENDIENTE | PENDIENTE | PENDIENTE | PENDIENTE | No vincular hasta definir receta, porción y artículo terminado. |
| Bebida | Leche Kirkland entera | CM-025 | `6b11d309-148f-4103-97f8-cf4d2f02168c` | Reventa | Ninguno de producción | Unidad comercial PENDIENTE | SIN RECETA / REVENTA | Descontar sólo el artículo comprado/terminado al vender. |
| Bebida | Leche Kirkland deslactosada light | CM-026 | `c2c87410-7766-4551-87c6-32d205f54c9f` | Reventa | Ninguno de producción | Unidad comercial PENDIENTE | SIN RECETA / REVENTA | Descontar sólo el artículo comprado/terminado al vender. |
| Dona | Dona de azúcar | CM-008 | `77a5b8a8-53f9-42bb-a9ab-c607d0f1054b` | Masa de dona | Azúcar 9 g | Dona 85 g; rendimiento variable | COMPATIBLE DESPUÉS DE RESOLVER DATOS | Base en fórmula compartida; azúcar una vez como directo del perfil. |
| Dona | Dona de chocolate negro | CM-007 | `a792f462-e335-4d8b-a215-37625c36777a` | Masa de dona | Chocolate negro 10 g | Dona 85 g; rendimiento variable | COMPATIBLE DESPUÉS DE RESOLVER DATOS | No duplicar cobertura en la fórmula base. |
| Dona | Dona de chocolate blanco | CM-021 | `a26997fb-46df-4fe9-bd30-173ae115c675` | Masa de dona | Chocolate blanco 10 g | Dona 85 g; rendimiento variable | COMPATIBLE DESPUÉS DE RESOLVER DATOS | No duplicar cobertura en la fórmula base. |
| Dona | Dona negro chispas | CM-030 | `aabb7fc6-23a8-4fcf-bb44-82f97a5dee5e` | Masa de dona | Chocolate negro 10 g + chispas aprox. 3 g | Dona 85 g; rendimiento variable | APROXIMADO; BLOQUEAR | Resolver chispas exactas antes de automatizar. |
| Dona | Dona blanco coco | CM-031 | `b38153df-ee04-4c55-ad82-8ba8714fb97d` | Masa de dona | Chocolate blanco 10 g + coco aprox. 5 g | Dona 85 g; rendimiento variable | APROXIMADO; BLOQUEAR | Resolver coco exacto antes de automatizar. |
| Dona | Dona chocolate nuez | CM-032 | `7e9cb3f7-a40a-45ca-a91c-1df15b7291f9` | Masa de dona | Cobertura 10 g, tipo PENDIENTE + nuez 10 g | Dona 85 g; rendimiento variable | PENDIENTE; BLOQUEAR | Definir chocolate; registrar cobertura y nuez una sola vez. |
| Dona | Dona combinada | CM-033 | `71c324c4-40ff-4181-9079-10565a61f056` | Masa de dona | Chocolate total 10 g; reparto blanco/negro PENDIENTE | Dona 85 g; rendimiento variable | PENDIENTE; BLOQUEAR | No crear dos directos de 10 g; el total conjunto es 10 g. |
| Dona | Dona glaseada | CM-020 | `b19768a9-684c-4d46-878f-fb396194bf88` | Masa de dona + glaseado | Glaseado 15 g; preparación 100 g glass + 10 ml agua | Dona 85 g; rendimiento de glaseado PENDIENTE | COMPATIBLE DESPUÉS DE RESOLVER DATOS | El glaseado debe ser fórmula separada o directos, nunca ambas cosas. |
| Dona | Berlinesa crema | CM-034 | `a31694a4-d410-421b-89f0-de749193be64` | Masa de dona | Crema pastelera comprada aprox. 18 g | Berlinesa 90 g; rendimiento variable | APROXIMADO; BLOQUEAR | La crema comprada es directo, no fórmula fabricada. |
| Dona | Berlinesa zarzamora | CM-035 | `ba082fc9-d81f-4a05-b7ba-88a8778abfff` | Masa de dona | Relleno de zarzamora comprado aprox. 18 g | Berlinesa 90 g; rendimiento variable | APROXIMADO; BLOQUEAR | El relleno comprado es directo, no fórmula fabricada. |
| Galleta | Galleta con chispitas de colores | CM-023 | `644edb70-10c4-4858-bfa9-99c66401bbaa` | Masa de galleta | Chispas 10 g | 100 g; lote reportado 19 piezas + 50 g degustación | INCONSISTENTE; BLOQUEAR | Degustación no debe convertirse en producto vendido ni merma implícita. |
| Galleta | Galleta de nuez | CM-036 | `46496b9b-0da4-42df-a6bb-058a47333ec1` | Masa de galleta | Nuez 10 g; barniz PENDIENTE | 100 g; rendimiento inconsistente | INCONSISTENTE; BLOQUEAR | Nuez sólo como directo; resolver degustación y huevo. |
| Galleta | Galleta de coco | CM-037 | `7592364d-eaff-4222-b5b8-f8705413e097` | Masa de galleta | Coco 10 g | 100 g; rendimiento inconsistente | INCONSISTENTE; BLOQUEAR | Coco sólo como directo; resolver degustación. |
| Galleta | Galleta multigrano | CM-038 | `f532a645-e89a-480d-93d7-d4f2cf3067b8` | Masa de galleta | Mezcla de semillas 10 g, composición PENDIENTE | 100 g; rendimiento inconsistente | PENDIENTE; BLOQUEAR | Crear un insumo verificado para la mezcla o detallar sus componentes, no ambos. |
| Pan Salado | Baguette | CM-029 | `4274e78f-3c15-49c9-977b-f474e9824e8e` | Masa salada | Ajonjolí aprox. 5 g + barniz de huevo PENDIENTE | 100 g; distribución del lote variable | APROXIMADO; BLOQUEAR | Separar baguette vendible del bolillo interno y no asignar sobrante sin medición. |
| Pan Salado | Bolillo | CM-003 | `f9d8569d-765a-4152-bc61-36cc3f88ef24` | Masa salada | Mantequilla aprox. 7 g | 100 g; distribución del lote variable | APROXIMADO; BLOQUEAR | El bolillo pequeño interno no debe sumar existencia vendible. |
| Pan dulce | Concha de vainilla | CM-001 | `e4749ad8-0671-4a90-b616-f096a667af45` | Masa dulce + costra blanca | Costra aprox. 30 g | 80 g masa + costra; costra 1,312/1,330 g inconsistente | APROXIMADO/INCONSISTENTE; BLOQUEAR | Referir ambas fórmulas una vez; no repetir sus ingredientes como directos. |
| Pan dulce | Concha de chocolate | CM-002 | `3ac35bb4-414c-4f90-ad47-65807101f556` | Masa dulce + costra chocolate | Costra aprox. 30 g; cacao 25 g por fracción, reparto PENDIENTE | 80 g masa + costra | APROXIMADO/INCONSISTENTE; BLOQUEAR | Cacao pertenece a la variante de costra, no al perfil y la fórmula simultáneamente. |
| Pan dulce | Rol de canela | CM-006 | `1fde7b16-c2bc-4f2e-ac37-cf7dd2e5404e` | Masa dulce | Relleno por tanda: margarina 80 g, manteca 20 g, canela 20 g, azúcar 20 g | 1,100 g → 13 roles | REQUIERE EXTENSIÓN | Preservar el consumo fijo por tanda; no redondear 1/13 por pieza. |
| Pan dulce | Bollo crema | CM-017 | `8b0711ef-b6e6-47dc-b7f2-01ace22149f2` | Masa dulce | Crema comprada 15 g + glass 3 g + barniz aprox. 1 ml | 80 g masa | APROXIMADO; BLOQUEAR | Crema es directo comprado; resolver barniz exacto. |
| Pan dulce | Bollo zarzamora | CM-019 | `c6ba8dd7-8176-4f6f-80db-69971bb34923` | Masa dulce | Relleno comprado 15 g después de hornear | 85 g masa | COMPATIBLE DESPUÉS DE RESOLVER RENDIMIENTO BASE | Relleno una vez como directo del perfil. |
| Pan dulce | Bollo ajonjolí | CM-028 | `5e9876c0-d589-4f75-91a0-709263e1a8d4` | Masa dulce | Ajonjolí 6 g + barniz; 1 huevo rinde aprox. 35 | 80 g masa | APROXIMADO; BLOQUEAR | No convertir el huevo por pieza hasta medir o adoptar política de tanda. |
| Pan dulce | Rosquita | CM-027 | `14030080-0244-4806-8a04-6499bf96597c` | Masa dulce | Ajonjolí aprox. 1.5 g | 80 g masa | APROXIMADO; BLOQUEAR | Resolver ajonjolí exacto. |
| Pan dulce | Cuernito de mantequilla | CM-004 | `059d9f77-e474-4fc2-bb7a-762a0cb977f2` | Masa enriquecida derivada de masa dulce | Mantequilla fija 80 g por preparación; barniz PENDIENTE | 12 × 80 g dentro de tanda mixta | REQUIERE EXTENSIÓN | No aplanar masa dulce ni repartir los 80 g sin modelo de fórmula derivada/tanda. |
| Pan dulce | Danés | CM-016 | `6578f84a-8568-4772-94be-0dc03688af0a` | Masa enriquecida derivada | Crema comprada 21 g + barniz PENDIENTE | 7 × 80 g dentro de tanda mixta | REQUIERE EXTENSIÓN | No duplicar base enriquecida; crema sólo como directo. |
| Pan dulce | Cuernito especial con almendra | CM-005 | `9d2f58bc-5e76-44c3-9e67-8c46048dd696` | Masa enriquecida derivada | Almendra 6 g + glass 2 g | 5 × 70 g dentro de tanda mixta | REQUIERE EXTENSIÓN | No duplicar base enriquecida; extras sólo como directos. |
| Pan dulce | Polvorón tradicional | CM-010 | `ac1d3c85-b798-4087-90bf-a751bec30f42` | Masa de polvorón | Sabor: ralladura de 2 naranjas o vainilla 60 ml por media tanda | 18 × 100 g; insumos no concilian con salida | INCONSISTENTE; BLOQUEAR | Las opciones de sabor pueden ser alternativas sólo tras definir unidad/rendimiento. |
| Pan dulce | Polvorón de nuez | CM-011 | `8c72c1c6-c3a3-4f3d-bc87-c11a15f6fab3` | Masa de polvorón | Nuez 12 g + barniz; huevo aprox. 60–70 g | 100 g; rendimiento inconsistente | APROXIMADO/INCONSISTENTE; BLOQUEAR | Nuez y barniz una vez; no sumar huevo a la base y al acabado. |
| Pan dulce | Pay | CM-039 | `0364ccd2-6c83-41bf-9a5d-477514dfa7a1` | Relleno de pay + masa de galleta sin hornear | 20 g de masa de galleta por pay | Relleno rinde 15; 300 g de masa de galleta total | COMPATIBLE DESPUÉS DE RESOLVER MASA/RINDE | Dos requisitos de fórmula; no descontar galletas terminadas. |
| Pan dulce | Pan de muerto grande | CM-040 | `a67359c8-e885-4764-8a97-6051cc985fe4` | Masa de muerto | Margarina y azúcar post-horneado PENDIENTES | 120 g armado; lote reportado no concilia | INCONSISTENTE; BLOQUEAR | Resolver salida total y acabados; no inventar merma. |
| Pan dulce | Pan de muerto pequeño | CM-013 | `7119bef9-735a-4170-8f96-8d2962a1fc79` | Masa de muerto | Margarina y azúcar post-horneado PENDIENTES | 64 g armado; lote reportado no concilia | INCONSISTENTE; BLOQUEAR | Resolver salida total y acabados; no inventar merma. |
| Pan dulce | Muffin sencillo | CM-014 | `b8c445c4-6197-4d9b-be8b-1d7f07850bce` | PENDIENTE | PENDIENTE | PENDIENTE | PENDIENTE | No crear fórmula ni vínculo automático. |
| Pan dulce | Muffin de nuez | CM-015 | `8e8fedbe-a1b3-47e4-8709-cbc7f098d3ec` | PENDIENTE | Nuez: cantidad PENDIENTE | PENDIENTE | PENDIENTE | No inferir la base del muffin sencillo ni la nuez. |

## Control de fórmula, aproximaciones y elegibilidad

Esta segunda vista completa los campos sin repetir IDs y gramajes de la tabla anterior. “Fórmula prevista” describe el diseño documental y no una FormulaVersion existente.

| SKU | Fórmula prevista | Preparaciones intermedias | Aproximados | Pendientes principales | Validación | Automatización |
|---|---|---|---|---|---|---|
| CM-024 | PENDIENTE | PENDIENTE | Ninguno declarado | Receta, porción, rendimiento e insumos | PENDIENTE | NO ELEGIBLE |
| CM-025 | NO APLICA | Ninguna | Ninguno | Unidad comercial y vínculo terminado | NO APLICA / reventa | NO ELEGIBLE aún |
| CM-026 | NO APLICA | Ninguna | Ninguno | Unidad comercial y vínculo terminado | NO APLICA / reventa | NO ELEGIBLE aún |
| CM-008 | Masa de dona | Masa compartida | Ninguno en acabado | Rendimiento estable y aceite | Parcial | NO ELEGIBLE |
| CM-007 | Masa de dona | Masa compartida | Ninguno en acabado | Rendimiento estable y aceite | Parcial | NO ELEGIBLE |
| CM-021 | Masa de dona | Masa compartida | Ninguno en acabado | Rendimiento estable y aceite | Parcial | NO ELEGIBLE |
| CM-030 | Masa de dona | Masa compartida | Chispas 3 g | Chispas exactas, rendimiento y aceite | APROXIMADO | NO ELEGIBLE |
| CM-031 | Masa de dona | Masa compartida | Coco 5 g | Coco exacto, rendimiento y aceite | APROXIMADO | NO ELEGIBLE |
| CM-032 | Masa de dona | Masa compartida | Ninguno declarado | Tipo de chocolate, rendimiento y aceite | PENDIENTE | NO ELEGIBLE |
| CM-033 | Masa de dona | Masa compartida | Ninguno declarado | Reparto del chocolate, rendimiento y aceite | PENDIENTE | NO ELEGIBLE |
| CM-020 | Masa de dona + glaseado | Glaseado | Ninguno declarado | Rendimiento del glaseado, dona y aceite | Parcial | NO ELEGIBLE |
| CM-034 | Masa de dona | Masa compartida | Crema 18 g | Relleno exacto, rendimiento y aceite | APROXIMADO | NO ELEGIBLE |
| CM-035 | Masa de dona | Masa compartida | Zarzamora 18 g | Relleno exacto, rendimiento y aceite | APROXIMADO | NO ELEGIBLE |
| CM-023 | Masa de galleta | Masa compartida | Ninguno declarado | Conciliar rendimiento y degustación | INCONSISTENTE | NO ELEGIBLE |
| CM-036 | Masa de galleta | Masa compartida | Huevo de barniz 60–70 g | Barniz, rendimiento y degustación | APROXIMADO/INCONSISTENTE | NO ELEGIBLE |
| CM-037 | Masa de galleta | Masa compartida | Ninguno declarado | Rendimiento y degustación | INCONSISTENTE | NO ELEGIBLE |
| CM-038 | Masa de galleta | Masa compartida | Ninguno declarado | Composición de semillas, rendimiento y degustación | PENDIENTE/INCONSISTENTE | NO ELEGIBLE |
| CM-029 | Masa salada | Masa compartida | Ajonjolí 5 g | Barniz, rendimiento y reparto de lote | APROXIMADO | NO ELEGIBLE |
| CM-003 | Masa salada | Masa compartida | Mantequilla 7 g | Rendimiento y bolillo de consumo interno | APROXIMADO | NO ELEGIBLE |
| CM-001 | Masa dulce + costra blanca | Costra de concha | Costra 30 g | División/rendimiento exactos | APROXIMADO/INCONSISTENTE | NO ELEGIBLE |
| CM-002 | Masa dulce + costra chocolate | Costra de concha | Costra 30 g | División, cacao por fracción y rendimiento | APROXIMADO/INCONSISTENTE | NO ELEGIBLE |
| CM-006 | Masa dulce + relleno de rol | Relleno fijo por tanda | Ninguno declarado | Diseño de tanda 1,100 g/13 | CONFIRMADO pero no representable | NO ELEGIBLE |
| CM-017 | Masa dulce | Masa compartida | Barniz 1 ml | Barniz y rendimiento base | APROXIMADO | NO ELEGIBLE |
| CM-019 | Masa dulce | Masa compartida | Ninguno declarado | Rendimiento base | Parcial | NO ELEGIBLE |
| CM-028 | Masa dulce | Masa compartida | Barniz: 1 huevo/35 | Barniz y rendimiento base | APROXIMADO | NO ELEGIBLE |
| CM-027 | Masa dulce | Masa compartida | Ajonjolí 1.5 g | Ajonjolí y rendimiento base | APROXIMADO | NO ELEGIBLE |
| CM-004 | Masa enriquecida | Masa dulce derivada + mantequilla fija | Ninguno declarado | Modelo derivado/tanda y barniz | CONFIRMADO pero no representable | NO ELEGIBLE |
| CM-016 | Masa enriquecida | Masa dulce derivada + mantequilla fija | Ninguno declarado | Modelo derivado/tanda y barniz | CONFIRMADO pero no representable | NO ELEGIBLE |
| CM-005 | Masa enriquecida | Masa dulce derivada + mantequilla fija | Ninguno declarado | Modelo derivado/tanda y rendimiento | CONFIRMADO pero no representable | NO ELEGIBLE |
| CM-010 | Masa de polvorón | Variante de sabor | Ralladura por naranjas | Rendimiento y elección de sabor | INCONSISTENTE | NO ELEGIBLE |
| CM-011 | Masa de polvorón | Variante de sabor | Huevo 60–70 g | Barniz y rendimiento | APROXIMADO/INCONSISTENTE | NO ELEGIBLE |
| CM-039 | Relleno de pay + masa de galleta cruda | Dos preparaciones compartidas | Ninguno declarado | Rendimiento compatible de masa de galleta | Parcial | NO ELEGIBLE |
| CM-040 | Masa de muerto | Masa compartida | Ninguno declarado | Conciliar rendimiento; margarina y azúcar | INCONSISTENTE/PENDIENTE | NO ELEGIBLE |
| CM-013 | Masa de muerto | Masa compartida | Ninguno declarado | Conciliar rendimiento; margarina y azúcar | INCONSISTENTE/PENDIENTE | NO ELEGIBLE |
| CM-014 | PENDIENTE | PENDIENTE | Ninguno declarado | Receta completa y rendimiento | PENDIENTE | NO ELEGIBLE |
| CM-015 | PENDIENTE | PENDIENTE | Ninguno declarado | Receta, nuez y rendimiento | PENDIENTE | NO ELEGIBLE |

## Cobertura del catálogo

- 36/36 productos activos tienen una fila con ProductId y SKU.
- 2 son reventa sin receta; 1 bebida, 2 muffins y varios componentes/acabados permanecen pendientes.
- Ninguna receta está marcada como lista para publicación automática.
- Los datos aproximados se conservaron como tales; las diferencias de rendimiento no se convirtieron en merma.
- Puerquito y Coronita de nuez permanecen históricos/inactivos y quedan fuera de esta matriz activa.

## Regla de publicación futura

Para publicar un perfil deben coincidir exactamente el `FinishedInventoryItemId` del perfil y el `InventoryItemId` vinculado al producto. Producción consume las fórmulas y directos del perfil y suma sólo la salida real; venta descuenta únicamente el terminado. Ningún ingrediente crudo debe vincularse directamente al producto vendible.
