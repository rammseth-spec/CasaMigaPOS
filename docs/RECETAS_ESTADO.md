# Recetas reales de Casa Miga — estado frente al recetario maestro

Fecha: 20/10/2026. Compara el recetario maestro (`docs/PHASE_1K4G_C_RECIPE_MASTER.md`, datos de quien elabora el pan, 18/10/2026) con lo capturado en la base de la caja real (`C:\Users\ramms\CasaMiga-Revision\local-main-c2fb393\caja\casamiga.db`, consultada en su copia restaurada de prueba). Lo que falta llenar está en `docs/RECETARIO_PARA_COMPLETAR.md`.

**En la caja real hay hoy:** 10 preparaciones en **borrador** (ninguna publicada), **0 recetas de pan**, 34 insumos y 31 panes terminados dados de alta, **0 existencias**, **0 producciones** y el descuento por venta **apagado**. Nada se cambió en esa base.

## A. Completas que pueden validarse

Datos del recetario sin "aproximadamente", "pendiente" ni diferencias. Cada una espera **una sola confirmación tuya** antes de publicarse.

| Receta | Lo confirmado | Confirmación que falta |
|---|---|---|
| Relleno de rol de canela (`RELLENO-ROL`) | Por tanda: margarina 80 g, manteca 20 g, canela 20 g, azúcar 20 g = 140 g | Qué manteca es (el borrador puso **manteca vegetal** sin confirmarlo) |
| Tanda de rol de canela (13 roles) | 1,100 g de masa dulce + 140 g de relleno → 13 roles | Ahora se puede capturar tal cual (tanda de 13 piezas), sin repartir por pieza. Se publica cuando la masa dulce y el relleno estén publicados |
| Relleno de pay de queso (`RELLENO-PAY`) | Queso crema 360 g, leche condensada 250 g, huevo 3, leche evaporada 200 ml, vainilla 10 ml, fécula 15 g → 15 pays | Cómo contar el huevo si se hacen menos de 15 pays (sólo tandas que den huevos enteros, o redondear hacia arriba) |
| Leche Kirkland entera y deslactosada light | Reventa: no llevan receta | Sólo su artículo terminado y el vínculo para la venta, cuando se autorice el descuento por venta |

## B. Bloqueadas por datos que debes confirmar

| Preparación | Qué falta | Panes que bloquea |
|---|---|---|
| Masa dulce base | **Rendimiento** de la tanda que se va a usar: 5,750 g es una medición histórica con 3 kg de harina y la tanda habitual es de 4 kg sin medir. **Vainilla**: el recetario dice 20 g por kilo y el borrador de la caja la pasó a **60 ml** (conversión no aprobada; hay que corregir el borrador al decidir la unidad). Qué manteca es. Sobres de Tradi Pan como piezas | Conchas (2), rol de canela, bollos de crema, zarzamora y ajonjolí, rosquita, y todo lo de masa enriquecida |
| Masa enriquecida | **Cuánta masa dulce se toma** por preparación (no se supone 1,790 g); 80 g de mantequilla y 1,870 g repartidos sí están confirmados. Huevo del barnizado | Cuernito de mantequilla, danés con crema, cuernito de almendra |
| Costra de concha | 1,312 g medidos contra 1,330 g de ingredientes; reparto blanca/chocolate; cacao por pieza; ≈30 g por concha | Conchas |
| Masa salada | Rendimiento medido; ≈5 g de ajonjolí en baguette, ≈7 g de mantequilla en bolillo; barnizado | Baguette, bolillo |
| Masa de donas y berlinesas | Rendimiento (lo del 14/10 es una sola observación); chispas ≈3 g, coco ≈5 g, rellenos ≈18 g; chocolate de la dona con nuez; reparto de la combinada; aceite de fritura | 12 donas y berlinesas |
| Glaseado | Rendimiento real: 100 g de azúcar + 10 ml de agua **no** confirman 110 g (el borrador tiene 110 g provisional) | Dona glaseada |
| Polvorón | Rendimiento (18 × 100 g no cuadra con los ingredientes); huevo del barnizado | Polvorón, polvorón de nuez |
| Galleta | Rendimiento (19 × 100 g + 50 g no cuadra); huevo de la masa; barnizado; mezcla multigrano | 4 galletas y el pay de queso (lleva 20 g de masa de galleta) |
| Pan de muerto | Rendimiento (15 × 120 g + 19 × 64 g no cuadra); acabado de margarina y azúcar | Pan de muerto chico y grande |
| Sin receta | Dona especial, muffin, muffin de nuez, vaso de café, bisquet, pay de elote, galleta de nuez y coco, panes de muerto de masa madre (2) | Esos 9 productos |

**Contradicciones con el catálogo:** el recetario dice que **Puerquito** está retirado y que el bollo relleno por la mitad (CM-018) es el mismo bollo de crema, pero ambos siguen **activos** en la caja. Decide si se desactivan.

## C. Bloqueadas por el programa

| Limitación | Estado |
|---|---|
| Una preparación dentro de otra (masa dulce → masa enriquecida; masa dulce y relleno → tanda de roles) | **Resuelto en `main` el 20/10/2026** (migración 031). Se comprobó con recetas de prueba y con una copia de la caja; todavía no está en la caja real |
| Cantidades fijas por tanda (140 g de relleno para 13 roles) | **Resuelto** con lo anterior: la tanda es una preparación de 13 piezas que se produce en tandas completas; no se reparte ni se redondea por rol |
| Editar un borrador ya guardado en la pantalla Recetas | **Corregido el 20/10/2026**: antes la caja respondía «La receta cambió» al guardar de nuevo un borrador de receta o de perfil |
| Degustación y consumo interno (galleta de 50 g, bolillo interno) | Falta en el programa; necesita tu decisión de cómo se registran (recetario, paso 0) |
| Aceite de fritura que se reutiliza varios días | Falta en el programa; necesita la política de carga, reposición y descarte |
| Vainilla en g y en ml | Cada insumo se mide de una sola forma; necesita tu decisión (una unidad o dos insumos distintos) |

## Primera receta real: polvorón (CM-010)

Es la que menos datos necesita: una sola masa, sin acabados ni barnizado (el barnizado es del polvorón **de nuez**) y con todos sus artículos ya dados de alta en la caja.

**Lo que ya sé (recetario maestro, sección 8):**
- Media tanda, la habitual: harina 900 g, manteca vegetal 500 g, azúcar estándar 500 g, huevo 1 pieza y Royal 40 g.
- Sabor, uno de los dos: ralladura de 2 naranjas **o** esencia de vainilla 60 ml.
- 100 g de masa cruda por polvorón.
- En la caja: la masa de polvorón está en borrador; la receta del pan, en borrador en la copia de revisión; el artículo terminado «Polvorón» (PT-CM-010) existe.

**Lo único que falta (4 datos):**
1. **Peso de la masa cruda de una media tanda**, pesada completa antes de dividirla: ____ g. Hoy sólo está el reporte de 18 × 100 g = 1,800 g, que no cuadra con los ingredientes; no se usa.
2. **Huevo y naranjas cuando se hace menos de una media tanda:** ¿se redondea hacia arriba (por ejemplo, 1 huevo aunque toque medio) o sólo se producen medias tandas completas? → ____
3. **Sabor por omisión:** ¿naranja o vainilla? (el otro queda como alternativa) → ____
4. **¿Cuántos polvorones salen de esa media tanda?** → ____ (si no son peso ÷ 100 g, decir qué pasa con lo que sobra)

**En cuanto lleguen**, la herramienta `tools/recorrido-receta` (`dotnet run --project tools/recorrido-receta -- recorrido <copia> <rinde g> <arriba|exacto> <naranja|vainilla> [piezas]`) corre el recorrido completo en una copia. Hace esto:
- pone el rendimiento medido y publica;
- carga existencias de prueba marcadas «PRUEBA»;
- produce;
- comprueba que se descontó cada ingrediente y entró el pan.

La herramienta ya se probó en una copia desechable con el rendimiento reportado. Ese ensayo no valida la receta y la copia se borró.

## Recorrido completo (preparaciones → pan terminado → producción → consumo → entrada de pan)

**Con recetas reales todavía no:** espera los 4 datos del polvorón. El recorrido está comprobado con recetas de prueba, incluidas la masa enriquecida y la tanda de roles, y la herramienta de arriba ya lo hace completo con la receta del polvorón.
