# Recetario Casa Miga — hoja de trabajo para completarlo

Fecha: 19/10/2026. Fuente: recetario comercial que dio quien elabora el pan (18/10/2026, `docs/PHASE_1K4G_C_RECIPE_MASTER.md`), su matriz y auditoría técnica (1K.4G-C), la lista de insumos preparada en 1K.4G-C-C-B1 y el catálogo autorizado de `main` (44 panes, `docs/catalog/catalogo-autorizado-v2.csv`).

**Para qué sirve:** tener en un solo lugar todo lo que la caja necesita para calcular producción, descontar inventario y sacar el costo y la ganancia de cada pan. Se trabaja **en orden, paso por paso**: llenar los espacios `____`, marcar las casillas `[ ]` y, cuando un paso quede completo, pasar al siguiente. Nada de esto se publica en la caja hasta el paso 7; mientras tanto todo se carga como **borrador**.

## Cómo leer los datos

| Marca | Significa |
|---|---|
| ✅ | Confirmado por quien hace el pan |
| ≈ | Aproximado ("más o menos", "como", un rango) — hay que medirlo |
| ❓ | Falta el dato |
| ⚠️ | Dos datos no cuadran — hay que volver a medir o explicar la diferencia |

Reglas que no cambian: las cantidades se escriben en **gramos (g)**, **mililitros (ml)** o **piezas**, y no se mezclan (un huevo no se convierte a gramos, ni gramos a mililitros). Los rellenos de crema pastelera y zarzamora y los chocolates de cobertura **se compran listos**: van como insumo, no como receta.

## Estado general

| Situación | Panes |
|---|---:|
| Con receta documentada que hay que completar o medir (paso 3) | 31 |
| Sin receta todavía: muffins, café, pay de elote, bisquet y los dos panes de muerto de masa madre (paso 4.1) | 7 |
| Reventa, sin receta: leches Kirkland (paso 4.2) | 2 |
| Retirados en el catálogo comercial de QA; decidir si se desactivan (paso 4.3) | 4 |
| **Total del catálogo** | **44** |

Hoy ningún pan está listo para que la caja calcule su consumo solo: faltan rendimientos medidos, costos y cantidades exactas.

---

## Paso 0 — Decisiones generales (contestar primero)

Estas respuestas cambian cómo se capturan varias recetas a la vez.

- [ ] **Huevo:** ¿se cuenta por **pieza** o por **peso** (huevo líquido)? → ____
- [ ] **Huevo en masa vs barnizado:** ¿cuánto huevo va al barnizado de cada familia? (bollos, cuernitos, daneses, baguette, galletas, polvorón de nuez) → ____
- [ ] **Si una receta pide medio huevo:** ¿se redondea hacia arriba (1 pieza) o se exige número entero exacto? → ____
- [ ] **Tradi Pan:** ¿se controla por **sobre de 11 g** (pieza) o por gramos? ¿cuántos sobres equivalen a 30 g de levadura fresca? → ____
- [ ] **Esencia de vainilla:** se anota en **g** (masa dulce, 20 g) y en **ml** (donas, galletas, polvorón, pay). Elegir **una sola** unidad: ____ (y, si es ml, cuántos ml son los 20 g de la masa dulce: ____)
- [ ] **Manteca:** en masa dulce, galleta y relleno de rol dice "manteca". ¿Es **manteca vegetal** (la misma de donas y costra) u otra? → ____
- [ ] **Aceite de fritura** (donas y berlinesas): carga de ≈1 L que se usa varios días y se cambia cada semana. ¿Cómo se descuenta? (por tanda ____ ml / por semana / no se descuenta) → ____
- [ ] **Sobrantes de masa:** ¿se tiran, se refrigeran o se reprocesan? (la masa con huevo no debe quedarse a temperatura ambiente) → ____
- [ ] **Degustación y consumo interno:** la galleta pequeña de 50 g y el bolillo pequeño interno, ¿se registran como degustación/consumo interno o no se registran? → ____
- [ ] **Merma:** ¿quién la registra y con qué motivo? → ____

---

## Paso 1 — Insumos: confirmar la lista y poner su costo

Sin costo por insumo la caja no puede calcular el costo ni la ganancia de ningún pan. Poner el **precio de compra** y la presentación (bulto, caja, litro…).

### 1.1 Insumos ya preparados (31)

| Código | Insumo | Se mide en | Presentación de compra | Costo de compra | Proveedor |
|---|---|---|---|---|---|
| MP-HAR-001 | Harina de trigo | g | Bulto 25 kg | $____ | ____ |
| MP-AZU-EST | Azúcar estándar | g | ____ | $____ | ____ |
| MP-AZU-GLA | Azúcar glass | g | ____ | $____ | ____ |
| MP-MAR-001 | Margarina | g | ____ | $____ | ____ |
| MP-MAN-VEG | Manteca vegetal | g | ____ | $____ | ____ |
| MP-MAN-001 | Mantequilla | g | ____ | $____ | ____ |
| MP-SAL-001 | Sal | g | ____ | $____ | ____ |
| MP-AGU-001 | Agua | ml | (normalmente sin costo) | $____ | — |
| MP-HUE-001 | Huevo | pieza | ____ | $____ | ____ |
| MP-LEV-FRE | Levadura fresca | g | ____ | $____ | ____ |
| MP-TRA-011 | Tradi Pan · sobre de 11 g | pieza | ____ | $____ | ____ |
| MP-LEV-SEC | Levadura seca | g | ____ | $____ | ____ |
| MP-ACE-VEG | Aceite vegetal | ml | ____ | $____ | ____ |
| MP-CAC-001 | Cacao en polvo | g | ____ | $____ | ____ |
| MP-CAN-001 | Canela en polvo | g | ____ | $____ | ____ |
| MP-CHO-NEG | Chocolate negro de cobertura | g | ____ | $____ | ____ |
| MP-CHO-BLA | Chocolate blanco de cobertura | g | ____ | $____ | ____ |
| MP-NUE-001 | Nuez | g | ____ | $____ | ____ |
| MP-ALM-001 | Almendra fileteada | g | ____ | $____ | ____ |
| MP-COC-001 | Coco rallado | g | ____ | $____ | ____ |
| MP-AJO-001 | Ajonjolí | g | ____ | $____ | ____ |
| MP-CRE-PAS | Crema pastelera comprada | g | ____ | $____ | ____ |
| MP-REL-ZAR | Relleno de zarzamora comprado | g | ____ | $____ | ____ |
| MP-CHI-COL | Chispas de colores | g | ____ | $____ | ____ |
| MP-QUE-CRE | Queso crema | g | ____ | $____ | ____ |
| MP-LEC-CON | Leche condensada | g | ____ | $____ | ____ |
| MP-LEC-EVA | Leche evaporada | ml | ____ | $____ | ____ |
| MP-FEC-MAI | Fécula de maíz | g | ____ | $____ | ____ |
| MP-LEC-PRO | Leche (para producción) | ml | ____ | $____ | ____ |
| MP-POL-HOR | Polvo para hornear (Royal) | g | ____ | $____ | ____ |
| MP-NAR-RAL | Naranja (para ralladura) | pieza | ____ | $____ | ____ |

### 1.2 Insumos que faltan dar de alta

- [ ] **Esencia de vainilla** — unidad según paso 0: ____; presentación ____; costo $____
- [ ] **Chispas de chocolate** (dona negra con chispas; hoy solo existen chispas de colores) — ¿son chispas de chocolate? ____; costo $____
- [ ] **Mezcla de semillas** (galleta multigrano) — ¿se compra ya mezclada o se arma? Si se arma, componentes y gramos: ____
- [ ] **Insumos del pay de elote** (paso 4) — ____
- [ ] **Insumos del bisquet** (paso 4) — ____
- [ ] **Insumos de muffins y café** (paso 4) — ____

---

## Paso 2 — Preparaciones base: completar ingredientes y medir cuánto rinden

La caja calcula cada masa a partir de **cuánto rinde una tanda**. Para cada preparación: pesar en la báscula **toda la masa cruda** de una tanda normal, antes de dividirla, y anotarla.

### 2.1 Masa dulce base

Sirve para: conchas, rol de canela, bollos, rosquita y masa enriquecida (cuernitos, daneses, bigotes).

| Ingrediente (por 1 kg de harina) | Cantidad | Estado |
|---|---:|---|
| Harina de trigo | 1,000 g | ✅ |
| Azúcar estándar | 200 g | ✅ |
| Margarina | 100 g | ✅ |
| Manteca (¿vegetal? paso 0) | 100 g | ✅ cantidad · ❓ tipo |
| Agua | 250 ml | ✅ |
| Sal | 20 g | ✅ |
| Esencia de vainilla | 20 g | ✅ cantidad · ❓ unidad (paso 0) |
| Huevo | 4 piezas | ✅ |
| Levadura fresca **o** Tradi Pan | 30 g **o** 2 sobres de 11 g | ✅ · ❓ política de sobres |

- Tanda habitual: **4 kg de harina** ✅
- Dato histórico: con 3 kg de harina salieron 5,750 g de masa cruda (no se usa para 4 kg sin medir).
- [ ] **Medir:** masa cruda total de una tanda de 4 kg de harina = ____ g

### 2.2 Costra de concha (blanca y de chocolate)

| Ingrediente (por tanda) | Cantidad | Estado |
|---|---:|---|
| Manteca vegetal | 350 g | ✅ |
| Azúcar glass | 175 g | ✅ |
| Azúcar estándar | 175 g | ✅ |
| Harina | 630 g | ✅ |
| Cacao (solo a la parte de chocolate) | 25 g | ✅ |

- ⚠️ Los ingredientes suman 1,330 g y se midieron 1,312 g (18 g de diferencia).
- [ ] Explicar o volver a medir: rendimiento real de la costra = ____ g
- [ ] ¿Cuánto de la tanda se hace blanca y cuánto de chocolate? blanca ____ g / chocolate ____ g
- [ ] Costra por concha (hoy ≈30 g): ____ g

### 2.3 Relleno de rol de canela (por tanda de 13 roles)

| Ingrediente | Cantidad | Estado |
|---|---:|---|
| Margarina | 80 g | ✅ |
| Manteca (¿vegetal?) | 20 g | ✅ cantidad · ❓ tipo |
| Canela en polvo | 20 g | ✅ |
| Azúcar estándar | 20 g | ✅ |
| **Total** | **140 g** | ✅ |

- Tanda: 1,100 g de masa dulce → 13 roles ✅
- La caja no maneja cantidades fijas por tanda (ver paso 5). [ ] ¿Aceptan anotarlo por pieza redondeado? (≈84.6 g de masa y ≈10.8 g de relleno por rol) → ____

### 2.4 Masa dulce enriquecida

Se toma masa dulce ya hecha y se le agregan **80 g de mantequilla por preparación** ✅.

- Reparto habitual ✅: 12 cuernitos de mantequilla × 80 g, 7 daneses × 80 g, 5 cuernitos de almendra (bigote) × 70 g = 1,870 g.
- [ ] **Medir:** ¿cuánta masa dulce se toma para cada preparación de masa enriquecida? ____ g
- La caja no maneja "masa hecha con otra masa" (ver paso 5).

### 2.5 Masa salada

Sirve para: baguette y bolillo de mantequilla.

| Ingrediente (por 1 kg de harina) | Cantidad | Estado |
|---|---:|---|
| Harina | 1,000 g | ✅ |
| Azúcar estándar | 40 g | ✅ |
| Sal | 20 g | ✅ |
| Levadura fresca **o** levadura seca | 30 g **o** 10 g | ✅ |
| Aceite vegetal | 50 ml | ✅ |
| Agua | 550 ml | ✅ |

- Tanda habitual: **3 kg de harina** ✅. Salen normalmente 30 baguettes y 15 bolillos de 100 g (4,500 g), más un bolillo pequeño de consumo interno sin pesar.
- [ ] **Medir:** masa cruda total de una tanda de 3 kg = ____ g
- [ ] Peso del bolillo pequeño interno = ____ g

### 2.6 Masa de donas y berlinesas

| Ingrediente (por 1 kg de harina) | Cantidad | Estado |
|---|---:|---|
| Harina | 1,000 g | ✅ |
| Agua | 400 ml | ✅ |
| Huevo | 4 piezas | ✅ |
| Azúcar estándar | 200 g | ✅ |
| Sal | 10 g | ✅ |
| Levadura fresca | 40 g | ✅ |
| Esencia de vainilla | 50 ml | ✅ |
| Manteca vegetal | 100 g | ✅ |

- Tanda habitual: **2 kg de harina** ✅. Dona 85 g, berlinesa 90 g ✅.
- Dato del 14/10/2026: 44 donas + 4 berlinesas + 60 g de sobrante (4,160 g); es una observación, no un rendimiento fijo.
- [ ] **Medir:** masa cruda total de una tanda de 2 kg = ____ g

### 2.7 Glaseado (dona glaseada)

| Ingrediente | Cantidad | Estado |
|---|---:|---|
| Azúcar glass | 100 g | ✅ |
| Agua | 10 ml | ✅ |

- [ ] **Medir:** glaseado que rinde esa preparación = ____ g (no se suman g + ml)

### 2.8 Masa de polvorón

| Ingrediente | Tanda completa | Media tanda (la habitual) | Estado |
|---|---:|---:|---|
| Harina | 1,800 g | 900 g | ✅ |
| Manteca vegetal | 1,000 g | 500 g | ✅ |
| Azúcar estándar | 1,000 g | 500 g | ✅ |
| Huevo | 2 piezas | 1 pieza | ✅ |
| Royal | 80 g | 40 g | ✅ |
| Sabor: ralladura de naranja **o** vainilla | — | 2 naranjas **o** 60 ml | ✅ |

- Rendimiento reportado: 18 polvorones de 100 g usando toda la preparación. ⚠️ No cuadra con los ingredientes.
- [ ] **Medir:** masa cruda total de una media tanda = ____ g
- [ ] ¿El polvorón tradicional lleva siempre el mismo sabor o cambia por día? ____

### 2.9 Masa de galleta

Sirve para: galletas y la base del pay de queso.

| Ingrediente | Tanda completa | Media tanda (la habitual) | Estado |
|---|---:|---:|---|
| Harina | 1,800 g | 900 g | ✅ |
| Azúcar estándar | 1,000 g | 500 g | ✅ |
| Manteca (¿vegetal?) | 500 g | 250 g | ✅ cantidad · ❓ tipo |
| Margarina | 500 g | 250 g | ✅ |
| Huevo | 3 piezas | 1 pieza (sobra un poco) | ✅ / ≈ |
| Esencia de vainilla | 50 ml | 25 ml | ✅ |
| Royal | 100 g | 50 g | ✅ |

- Rendimiento reportado: 19 galletas de 100 g + 1 pequeña de 50 g para degustación (1,950 g). ⚠️ No cuadra exacto con los ingredientes.
- [ ] **Medir:** masa cruda total de una media tanda = ____ g
- [ ] Huevo exacto de la media tanda (paso 0) = ____

### 2.10 Relleno de pay de queso (por tanda de 15 pays)

| Ingrediente | Cantidad | Estado |
|---|---:|---|
| Queso crema | 360 g | ✅ |
| Leche condensada | 250 g | ✅ |
| Huevo | 3 piezas | ✅ |
| Leche evaporada | 200 ml | ✅ |
| Esencia de vainilla | 10 ml | ✅ |
| Fécula de maíz | 15 g | ✅ |

- Rinde **15 pays** ✅. Cada pay lleva además 20 g de masa de galleta cruda (300 g por tanda) ✅.

### 2.11 Masa de pan de muerto

| Ingrediente (por 1 kg de harina) | Cantidad | Estado |
|---|---:|---|
| Harina | 1,000 g | ✅ |
| Leche | 400 ml | ✅ |
| Azúcar estándar | 250 g | ✅ |
| Margarina | 150 g | ✅ |
| Levadura fresca | 40 g | ✅ |
| Huevo | 4 piezas | ✅ |
| Sal | 10 g | ✅ |

- Tanda habitual: **2 kg de harina** ✅. Reportado: 15 grandes (120 g) + 19 pequeños (64 g) = 3,016 g. ⚠️ No cuadra con los ingredientes.
- [ ] **Medir:** masa cruda total de una tanda de 2 kg = ____ g
- [ ] Acabado después de enfriar, por pieza: margarina ____ g y azúcar ____ g (grande) / ____ g y ____ g (pequeño)

---

## Paso 3 — Cada pan: qué lleva por pieza

"Masa" = gramos de masa cruda por pieza. "Extras" = lo que se agrega solo a ese pan. Completar lo que falta en la última columna.

### 3.1 Pan dulce (masa dulce y masa enriquecida)

| # | Pan | Masa por pieza | Extras por pieza | Falta |
|---:|---|---|---|---|
| 1 | Concha de vainilla | 80 g masa dulce ✅ | costra blanca ≈30 g | [ ] costra exacta ____ g |
| 2 | Concha de chocolate | 80 g masa dulce ✅ | costra de chocolate ≈30 g | [ ] costra exacta ____ g |
| 6 | Rol de canela | 1,100 g / 13 de masa dulce ✅ | relleno 140 g / 13 ✅ | [ ] decisión del paso 2.3 |
| 17 | Bollo relleno de crema pastelera | 80 g masa dulce ✅ | crema pastelera 15 g ✅, azúcar glass 3 g ✅, barnizado ≈1 ml | [ ] barnizado exacto ____ |
| 19 | Bollo relleno de zarzamora | 85 g masa dulce ✅ | relleno de zarzamora 15 g ✅ (después de hornear) | — |
| 32 | Rosquita de ajonjolí | 80 g masa dulce ✅ | ajonjolí ≈1.5 g | [ ] ajonjolí exacto ____ g |
| 33 | Bollo con ajonjolí | 80 g masa dulce ✅ | ajonjolí 6 g ✅, barnizado (1 huevo ≈35 bollos) | [ ] barnizado exacto ____ |
| 4 | Cuernito de mantequilla | 80 g masa enriquecida ✅ | barnizado ❓ | [ ] barnizado ____ |
| 16 | Danés con crema pastelera | 80 g masa enriquecida ✅ | crema pastelera 21 g ✅ (antes de hornear), barnizado ❓ | [ ] barnizado ____ |
| 5 | Cuernito especial con almendra (bigote) | 70 g masa enriquecida ✅ | almendra fileteada 6 g ✅, azúcar glass 2 g ✅ | — |

### 3.2 Pan salado (masa salada)

| # | Pan | Masa por pieza | Extras por pieza | Falta |
|---:|---|---|---|---|
| 31 | Baguette | 100 g ✅ | ajonjolí ≈5 g, barnizado ❓ | [ ] ajonjolí ____ g · [ ] barnizado ____ |
| 3 | Bolillo mantequilla | 100 g ✅ | mantequilla ≈7 g (antes de hornear) | [ ] mantequilla exacta ____ g |

### 3.3 Donas y berlinesas (masa de donas)

| # | Pan | Masa por pieza | Extras por pieza | Falta |
|---:|---|---|---|---|
| 8 | Dona de azúcar | 85 g ✅ | azúcar estándar 9 g ✅ | — |
| 7 | Dona de chocolate (negro) | 85 g ✅ | chocolate negro 10 g ✅ | — |
| 21 | Dona sencilla de chocolate blanco | 85 g ✅ | chocolate blanco 10 g ✅ | — |
| 20 | Dona glaseada | 85 g ✅ | glaseado 15 g ✅ | [ ] rendimiento del glaseado (paso 2.7) |
| 34 | Dona de chocolate negro con chispas | 85 g ✅ | chocolate negro 10 g ✅ + chispas ≈3 g | [ ] chispas exactas ____ g |
| 35 | Dona de chocolate blanco con coco | 85 g ✅ | chocolate blanco 10 g ✅ + coco ≈5 g | [ ] coco exacto ____ g |
| 36 | Dona de chocolate con nuez | 85 g ✅ | chocolate 10 g ✅ + nuez 10 g ✅ | [ ] ¿chocolate negro o blanco? ____ |
| 37 | Dona combinada de chocolate negro y blanco | 85 g ✅ | 10 g de chocolate en total ✅ | [ ] negro ____ g + blanco ____ g |
| 38 | Berlinesa rellena de crema pastelera | 90 g ✅ | crema pastelera ≈18 g (después de freír) | [ ] crema exacta ____ g |
| 39 | Berlinesa rellena de zarzamora | 90 g ✅ | relleno de zarzamora ≈18 g (después de freír) | [ ] relleno exacto ____ g |

Todas: [ ] aceite de fritura según el paso 0.

### 3.4 Polvorones (masa de polvorón)

| # | Pan | Masa por pieza | Extras por pieza | Falta |
|---:|---|---|---|---|
| 10 | Polvorón | 100 g ✅ | sabor del día (paso 2.8) | [ ] rendimiento (paso 2.8) |
| 11 | Polvorón de nuez | 100 g ✅ | nuez 12 g ✅, barnizado (1 huevo ≈60–70 g de barniz) | [ ] barnizado exacto ____ |

### 3.5 Galletas y pay (masa de galleta)

| # | Pan | Masa por pieza | Extras por pieza | Falta |
|---:|---|---|---|---|
| 23 | Galleta con chispitas de colores | 100 g ✅ | chispas de colores 10 g ✅ | [ ] rendimiento (paso 2.9) |
| 40 | Galleta de nuez | 100 g ✅ | nuez 10 g ✅, barnizado ❓ | [ ] barnizado ____ |
| 41 | Galleta de coco | 100 g ✅ | coco 10 g ✅ | [ ] rendimiento (paso 2.9) |
| 42 | Galleta multigrano | 100 g ✅ | mezcla de semillas 10 g ✅ | [ ] composición de la mezcla (paso 1.2) |
| 28 | Pay de queso | 1/15 del relleno de pay ✅ + 20 g de masa de galleta ✅ | — | [ ] ¿el pay de queso del catálogo es el "individual"? ____ |

### 3.6 Pan de muerto (masa de pan de muerto)

| # | Pan | Masa por pieza | Extras por pieza | Falta |
|---:|---|---|---|---|
| 27 | Pan de muerto grande | 120 g (cuerpo 100 g + 2 huesitos de 10 g) ✅ | margarina y azúcar ❓ | [ ] acabado (paso 2.11) |
| 13 | Pan de muerto (pequeño) | 64 g (cuerpo 50 g + 2 huesitos de 7 g) ✅ | margarina y azúcar ❓ | [ ] acabado (paso 2.11) |

---

## Paso 4 — Panes sin receta y decisiones del catálogo

### 4.1 Panes del catálogo que aún no tienen receta

| # | Pan | Qué falta |
|---:|---|---|
| 14 | Muffin | [ ] receta completa (ingredientes y tanda) · [ ] gramos por pieza · [ ] cuántos salen |
| 15 | Muffin de nuez | [ ] ¿misma mezcla que el muffin? ____ · [ ] nuez por pieza ____ g |
| 24 | Vaso de café | [ ] café por vaso ____ g · [ ] agua ____ ml · [ ] vaso, tapa, azúcar o crema ____ |
| 29 | Pay de elote | [ ] receta completa · [ ] ¿lleva base de galleta como el de queso? · [ ] cuántos salen |
| 30 | Bisquet | [ ] receta completa · [ ] gramos por pieza · [ ] cuántos salen |
| 43 | Pan de muerto chico de masa madre | [ ] masa de pan de muerto con masa madre: harina, agua, masa madre y lo demás ____ · [ ] cuánta masa madre por tanda ____ g · [ ] cómo y cada cuándo se alimenta la masa madre ____ · [ ] tiempos de fermentación ____ · [ ] gramos por pieza (¿como el 13, 64 g?) ____ · [ ] cuántos salen |
| 44 | Pan de muerto grande de masa madre | [ ] ¿misma masa que el 43? ____ · [ ] gramos por pieza (¿como el 27, 120 g?) ____ · [ ] cuántos salen |

### 4.2 Reventa (se compran listos, sin receta)

| # | Producto | Falta |
|---:|---|---|
| 25 | Leche Kirkland entera | [ ] presentación que se vende (botella de ____ ml) · [ ] costo $____ |
| 26 | Leche Kirkland deslactosada light | [ ] presentación ____ · [ ] costo $____ |

### 4.3 Decisiones del catálogo (catálogo comercial aprobado en QA, 18/10/2026)

En el catálogo comercial de QA estos 4 panes quedaron **retirados**; en `main` siguen activos:

- [ ] **9 Dona especial** — ¿es la misma que la **37 Dona combinada**? Si sí, desactivar la 9: ____
- [ ] **12 Puerquito** — retirado; ¿se desactiva? ____
- [ ] **18 Bollo relleno por la mitad de crema pastelera** — es el mismo que el 17; ¿se desactiva? ____
- [ ] **22 Galleta de nuez y coco** — se separó en 40 Galleta de nuez y 41 Galleta de coco; ¿se desactiva? ____

Nombres que QA propone; elegir si se cambian en la caja:

| # | Nombre en `main` | Nombre en QA | ¿Cambiar? |
|---:|---|---|---|
| 3 | Bolillo mantequilla (Pan dulce) | Bolillo de mantequilla (Pan Salado) | [ ] |
| 5 | Cuernito especial con almendra | Cuernito de almendra (bigote) | [ ] |
| 7 | Dona de chocolate | Dona de chocolate negro | [ ] |
| 10 | Polvorón | Polvorón tradicional | [ ] |
| 13 | Pan de muerto | Pan de muerto pequeño | [ ] |
| 14 | Muffin | Muffin sencillo | [ ] |
| 21 | Dona sencilla de chocolate blanco | Dona de chocolate blanco | [ ] |
| 28 | Pay de queso | Pay de queso individual | [ ] |
| 31 | Baguette | Baguette con ajonjolí | [ ] |

---

## Paso 5 — Lo que la caja todavía no sabe representar

Para que nada se cuente dos veces, la caja necesita que cada ingrediente se descuente una sola vez. Hoy hay dos casos que no caben tal como se hacen en la panadería:

1. **Masa hecha con otra masa** (masa enriquecida = masa dulce + 80 g de mantequilla). Opciones:
   - [ ] a) Capturar la masa enriquecida con sus propios ingredientes (los de la masa dulce más la mantequilla), con su propio rendimiento medido (paso 2.4).
   - [ ] b) Pedir a desarrollo que la caja permita usar una masa dentro de otra.
2. **Cantidades fijas por tanda** (relleno del rol: 140 g para 13 roles; mantequilla de la masa enriquecida: 80 g por preparación). Opciones:
   - [ ] a) Repartirlas por pieza y aceptar el redondeo (paso 2.3).
   - [ ] b) Pedir a desarrollo cantidades fijas por tanda.

---

## Paso 6 — Cargar el recetario en la caja (como borrador)

Se carga en este orden, porque cada paso necesita que la caja ya tenga el anterior:

1. [ ] **Insumos** que falten (paso 1.2) y **costo** de cada insumo (paso 1.1).
2. [ ] **Preparaciones base** (paso 2) como **borrador**, con su rendimiento medido.
3. [ ] **Producto terminado** de cada pan (artículo de inventario por pieza) y su **perfil de producción** (paso 3) como **borrador**.

Ya está preparado (19/10/2026): el recetario de este documento, con los rendimientos reportados marcados «por medir», está en `docs/catalog/recetario-borrador.json` y se carga **en borrador** con un script que manda los cambios por la web; la caja los aplica al sincronizar:

```bash
cd cloud
npm run cargar -- https://<dirección de la web>              # sólo muestra qué falta
npm run cargar -- https://<dirección de la web> --enviar --esperar
```

- Pide el usuario y la contraseña de un administrador (o `CASAMIGA_USUARIO` / `CASAMIGA_CONTRASENA`).
- Manda, en orden: **catálogo autorizado con sus fotos provisionales** → insumos y un producto terminado por pan → preparaciones en borrador → lo que lleva cada pan en borrador. Sólo manda lo que falta: volver a correrlo no duplica nada.
- Lo de cada pan espera a que sus preparaciones estén **publicadas** (la caja lo exige); volver a correr el script después de publicarlas.
- En la prueba local de Windows (`scripts/run-local.ps1`) corre solo al final; la demo de la web (`npm run local -- … --demo`) ya trae todo en borrador.
- Cuando el paso 2 tenga los rendimientos medidos, se corrige `recetario-borrador.json` y la versión en borrador en la caja.

Mientras algo esté en borrador **no descuenta inventario ni calcula producción**.

## Paso 7 — Revisar y publicar

Por cada familia (empezar por una sola, por ejemplo donas):

- [ ] Hacer una tanda real y registrarla en **Producción** con lo que salió.
- [ ] Comparar lo que la caja calculó de insumos contra lo que realmente se usó.
- [ ] Si cuadra: **publicar** y **activar** la preparación y los perfiles de esa familia.
- [ ] Vincular cada pan con su producto terminado para que **la venta descuente** su existencia.
- [ ] Revisar el **costo por pieza** y la **ganancia** en Reportes.

---

## Seguridad alimentaria

Este recetario no valida vida útil ni procedimientos sanitarios. No guardar a temperatura ambiente masa cruda con huevo para usarla al día siguiente; la reutilización, refrigeración y descarte necesitan una regla sanitaria autorizada.
