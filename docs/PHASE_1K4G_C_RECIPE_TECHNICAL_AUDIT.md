# FASE 1K.4G-C-C-A — Auditoría técnica del recetario

Fecha: 18/10/2026. Alcance: análisis de sólo lectura del modelo de dominio, casos de uso, migración vigente y catálogo C-B. No se crearon recetas, perfiles, ítems, vínculos ni movimientos.

## Estado observado

La copia aislada C-B contiene 36 productos activos, pero las tablas `formula`, `formula_version`, `production_profile`, `production_profile_version`, `profile_formula_requirement` y `profile_direct_ingredient` están vacías. No existen lotes de producción ni vínculos producto–inventario. Sólo hay un InventoryItem heredado. Por tanto, esta auditoría describe compatibilidad estructural; no afirma que el catálogo esté listo para producir.

La evidencia de código revisada fue `ProductionFormula.cs`, `ProductionProfile.cs`, `ProductionScaling.cs`, `ProductionPlan.cs`, `Inventory.cs`, `ProductionService.cs`, `RecipeService.cs`, `ProductionPorts.cs`, la migración 013 y ADR-021/022/023. Las conclusiones siguientes derivan de esos contratos vigentes y no de un modelo propuesto.

## A. ¿Qué representa una fórmula?

`FormulaVersion` representa una preparación con rendimiento base fijo en MASS, VOLUME o COUNT. Sus ingredientes físicos son cantidades exactas; puede contener grupos de alternativas excluyentes. La versión publicada es inmutable. El modelo sirve para masa dulce, masa salada, masa de dona, costra, glaseado, polvorón, galleta, pay y pan de muerto cuando se conozcan rendimientos exactos.

## B. ¿Puede una fórmula consumir otra fórmula?

> **Actualización 20/10/2026:** sí, desde la migración 031 (`formula_preparation`). Una versión puede usar otras versiones ya preparadas; producir las consolida una sola vez en orden de dependencias, rechaza ciclos y sólo publica cuando las usadas ya están publicadas. Lo que sigue describe el estado del 18/10/2026.

No. Los ingredientes de una fórmula son InventoryItems y no hay relación Formula→Formula. Esto bloquea una representación fiel de **masa enriquecida = masa dulce ya preparada + 80 g de mantequilla por tanda**. Aplanar los ingredientes de masa dulce dentro de la enriquecida duplicaría mantenimiento y puede duplicar consumo cuando la tanda comparte masa con otros productos. Se requiere una extensión explícita para preparaciones derivadas o un diseño de subpreparaciones antes de C-C-B.

## C. ¿Cómo se relaciona un producto con preparaciones e ingredientes?

Una `ProductProductionProfileVersion` puede exigir una o varias FormulaVersions mediante `FormulaRequirement.QuantityPerPiece` y añadir ingredientes directos por pieza. Esto permite, por ejemplo, representar un pay con relleno de pay y masa de galleta como dos requisitos, o una concha con masa dulce y costra, siempre que cantidades y rendimientos sean exactos. Los rellenos y coberturas comprados deben ser ingredientes directos, no fórmulas ficticias.

## D. ¿Se consolidan fórmulas compartidas en una producción multiproducto?

Sí. `ProductionPlan` agrupa la demanda total por FormulaVersion antes de escalarla una sola vez. Así, varios productos que usan la misma masa pueden compartir el cálculo sin repetir ingredientes. Los directos se calculan por línea de producto y después se agregan por InventoryItem.

## E. ¿Cómo escala el motor las cantidades?

MASS y VOLUME se manejan en unidades atómicas exactas (miligramos y microlitros). Si el cociente no es representable de forma exacta, el plan falla con `NonRepresentableScale`; no redondea silenciosamente. COUNT exige una política explícita `ExactWholeCount` o `CeilingWholeCount`. Por ello 1,100 g / 13 roles y los sobres indivisibles de Tradi Pan no deben aproximarse durante la carga.

## F. ¿Soporta cantidades fijas por tanda?

> **Actualización 20/10/2026:** se representan como una preparación que rinde piezas (la tanda de 13 roles = 1,100 g de masa dulce + 140 g de relleno) y que se produce en tandas completas. No se reparte ni se redondea por pieza. Lo que sigue describe el estado del 18/10/2026.

No. Los requisitos del perfil son por pieza y la fórmula escala proporcionalmente respecto a su rendimiento. No existe un requisito fijo que se aplique una vez por tanda independientemente de las piezas. El relleno del rol (140 g por una tanda de 13) y los 80 g de mantequilla de la masa enriquecida requieren conservarse como cantidades de tanda o contar con una medición exacta compatible; dividir y redondear por pieza sería incorrecto.

## G. ¿Soporta rendimiento variable o mezcla de salidas?

Cada FormulaVersion tiene un `BaseYield` fijo. El motor no registra el rendimiento real de una preparación intermedia ni su sobrante. Sí permite un lote multiproducto con varias salidas terminadas y cantidades reales distintas de las planeadas, pero no explica cómo una masa variable se repartió entre productos. Los datos históricos de dona, salado, polvorón, galleta y muerto deben conciliarse antes de publicar versiones.

## H. ¿Qué ocurre con lo planeado y lo real?

Los ingredientes se determinan con `PlannedCount`; `ActualCount` determina cuántas piezas terminadas entran al inventario. Cero y cantidades mayores o menores están soportadas. La diferencia no crea automáticamente una merma y no debe usarse para ocultar degustación, consumo interno, sobrante o error de medición.

## I. ¿La confirmación es atómica y recuperable?

Sí. El flujo confirma consumo de ingredientes y entradas terminadas dentro de una operación transaccional, conserva snapshots inmutables y usa identificadores/idempotencia para recuperar resultados inciertos. Los efectos físicos ocurren después del commit. Esta propiedad sólo protege datos ya modelados correctamente; no vuelve exacta una receta aproximada.

## J. ¿Cómo deben modelarse reventa y rellenos comprados?

Leches de reventa no necesitan perfil de producción. Pueden tener un InventoryItem de producto terminado y un `ProductInventoryLink` para que la venta descuente unidades cuando el consumo automático esté validado. Crema pastelera, zarzamora y chocolate comprados son materias primas directas en el perfil de los panes correspondientes; no se deben inventar fórmulas de fabricación para ellos.

## K. ¿Cómo se evita el doble consumo?

El perfil consume materias primas durante producción y agrega sólo el InventoryItem terminado. `ProductInventoryLink` debe apuntar a ese mismo terminado para que la venta reste una pieza. Es incorrecto vincular el producto vendible a harina, masa u otro ingrediente crudo; también es incorrecto repetir en directos ingredientes ya incluidos en una fórmula. Para pay se consume masa de galleta cruda como requisito de fórmula, nunca galletas terminadas.

## L. ¿Cómo se tratan aproximaciones e inconsistencias?

El esquema no tiene un indicador estructural de “aproximado”; sólo podría conservarse como texto, lo cual no es suficiente para automatizar. C-C-B debe rechazar la publicación automática de cualquier versión que dependa de una aproximación o diferencia no conciliada. Los 18 g de costra, los 50 g de degustación de galleta, el reparto del lote de dona y la suma del pan de muerto no se convierten en merma implícita.

## M. ¿Qué pasa con unidades incompatibles?

Cada InventoryItem tiene una dimensión inmutable. La esencia de vainilla se reportó en gramos en masa dulce y en mililitros en otras preparaciones. No existe una conversión general segura entre masa y volumen. Debe estandarizarse el método de medición o crear insumos separados sólo si representan presentaciones operativamente distintas y verificadas. Huevos, sobres y piezas permanecen COUNT; no se convierten a gramos sin una política explícita.

## N. ¿Cómo se modelan alternativas?

Los grupos de alternativas excluyentes permiten elegir entre levadura fresca y otra opción y pueden mezclar dimensiones. La preferencia debe ser explícita. Tradi Pan puede ser COUNT por sobres si se confirma esa unidad; el escalamiento debe definir si exige conteo entero exacto o redondeo hacia arriba. La levadura seca de masa salada requiere la misma definición antes de publicarse.

## O. ¿Cómo se tratan aceite reutilizable, sobrantes, degustación y consumo interno?

Inventario soporta movimientos trazables y merma (`WASTE`), pero la operación vigente no ofrece semánticas habilitadas para degustación o consumo interno. Cargar 1 L de aceite de fritura por tanda sería falso porque se reutiliza durante varios días y se reemplaza aproximadamente por semana. Debe existir una política operativa separada para consumo/reposición de aceite. Los sobrantes de masa, piezas internas y degustaciones tampoco deben registrarse como venta ni como merma automática; requieren destinos y permisos definidos.

## P. ¿Qué falta antes de implementar C-C-B?

1. Conciliar rendimientos exactos de todas las masas y preparaciones.
2. Resolver gramajes aproximados, barnices, acabados, composición de multigrano, muffins y café.
3. Definir unidad y política COUNT para huevos, sobres y otras piezas indivisibles.
4. Resolver vainilla MASS frente a VOLUME.
5. Definir tratamiento de masa derivada y requisitos fijos por tanda.
6. Definir política de aceite reutilizable, sobrante/reproceso, degustación y consumo interno.
7. Crear el catálogo de InventoryItems con dimensiones y unidades verificadas.
8. Diseñar y revisar fórmulas/perfiles antes de publicar; mantener consumo por ventas desactivado hasta un smoke completo.

## Cambios técnicos candidatos para una fase posterior

> **Actualización 20/10/2026:** las dos primeras viñetas (subpreparaciones y requisitos por tanda) ya están hechas; ver `docs/RECETAS_ESTADO.md`.

Estos cambios no se implementaron y requieren autorización separada:

- Referencia segura a subpreparaciones o fórmulas derivadas, con detección de ciclos y consolidación única.
- Requisitos fijos por tanda además de requisitos proporcionales por pieza.
- Captura del rendimiento real de preparación intermedia y destino explícito de sobrante.
- Estado estructural BORRADOR/APROXIMADO/VALIDADO que impida publicar datos incompletos.
- Operaciones trazables de degustación y consumo interno, separadas de venta y merma.
- Política de insumos reutilizables o consumo por periodo para aceite, sin simular consumo por tanda.

## Propuesta controlada para C-C-B

C-C-B debe comenzar como preparación de datos en copia aislada: inventariar insumos y unidades, cargar únicamente fórmulas exactas y mantener en borrador/bloqueados los perfiles con pendientes. Antes de activar consumo, debe probar producción multiproducto, salida real, merma separada, venta del terminado y ausencia de doble consumo. No debe habilitarse globalmente el consumo por ventas mientras exista un solo vínculo ambiguo.

Plan por etapas verificables:

1. **Ingredientes:** crear en copia aislada InventoryItems verificados, uno por insumo físico y dimensión; resolver primero unidades y alternativas.
2. **Masas/preparaciones:** comenzar por fórmulas proporcionales con rendimiento exacto. La masa salada, dona y masa dulce son candidatas estructurales, pero ninguna se publica hasta medir el rendimiento. Costra, polvorón, galleta y muerto siguen detrás de sus inconsistencias. Masa enriquecida y rol esperaban decisión arquitectónica; desde el 20/10/2026 se representan como preparaciones dentro de otras y sólo esperan datos medidos.
3. **Terminados:** crear InventoryItems COUNT distintos para cada SKU fabricado y para cada artículo de reventa controlado.
4. **Perfiles/catálogo:** asociar fórmulas y directos sin repetir insumos, y hacer coincidir el terminado del perfil con el vínculo del ProductId.
5. **Validación:** ejecutar en fixture copiada previews multiproducto, producción real menor/igual/mayor, recuperación, merma separada y venta; auditar ledger y saldos.
6. **Autorización:** activar descuentos por venta sólo después del smoke de cada vínculo terminado. Muffins y café pueden permanecer sin receta y no bloquean el trabajo de familias verificadas.

## Conclusión

El motor vigente es adecuado para fórmulas proporcionales exactas, alternativas explícitas, múltiples preparaciones por producto y consolidación multiproducto. No representa fielmente preparaciones anidadas, cantidades fijas por tanda, rendimientos intermedios variables ni destinos distintos de venta/merma. El recetario aportado es suficiente para diseñar C-C-B, pero **no para publicar automáticamente ninguna de las 36 identidades activas sin resolver los bloqueos documentados**.
