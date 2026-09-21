# ADR-021 — Fórmulas compartidas y planificación pura de producción

Estado: implementado exclusivamente en 1J.1. Fecha: 2026-09-20.
Baseline: 7cc103c864812d1b8586921bad45323608fdc4d7. Sustituye la propuesta anterior de una receta monoproducto y COUNT global exacto.

## Frontera del Domain

Formula es una identidad estable de preparación técnica. No requiere InventoryItem ni genera ingreso/salida de masa intermedia. FormulaVersion contiene sólo ingredientes físicos InventoryItem y grupos de alternativas físicas. No tiene referencias a fórmulas: la recursión no forma parte del contrato.

ProductProductionProfile es una identidad estable por ProductId y FinishedInventoryItemId. Su versión contiene FormulaRequirement (versión fijada + cantidad por pieza) y DirectIngredientRequirement. Permite una fórmula base y varias auxiliares sin copiar sus ingredientes al perfil. Outputs son COUNT. No se consulta catálogo, stock ni SQLite; validar tipo/estado real de artículos y asociación comercial corresponde al servicio futuro con datos autoritativos.

## Versiones e inmutabilidad

Versiones Draft y Published, ambas representadas por objetos inmutables. Publicar devuelve un agregado nuevo, conserva el objeto borrador y toda versión anterior, y cambia un único ActiveVersionId. Desactivar deja el puntero nulo. Una versión reemplazada sigue Published histórica pero inactiva; no necesita mutar a Retired. Publicar exige propietario correcto, número creciente, identidad nueva y fecha no anterior a creación. No existe editor de contenido publicado. Las colecciones se copian y exponen mediante envolturas read-only.

La unicidad dentro del agregado es Domain; la unicidad entre escritores/filas deberá establecerse en 1J.2. El plan acepta versiones publicadas fijadas, incluidas históricas, sin resolver automáticamente la versión activa actual. Un futuro caso de uso nuevo debe seleccionar/revalidar la activa; recovery debe conservar la histórica.

## Cantidades, escalado y límites

Reutiliza InventoryQuantity, InventoryDimension e InventoryUnits. No segundo sistema de unidades. ProductionRatio guarda numerador/denominador positivos Int64 reducidos por MCD. La multiplicación cancela factores antes de checked. Si el racional reducido excede Int64 devuelve Overflow; no recurre a aproximación decimal. La escala es preparación requerida / rendimiento base, no una conversión dimensional entre ingredientes y resultado.

El plan primero suma toda demanda de una FormulaVersion, luego escala una vez sus ingredientes. Así el ceiling de huevo se aplica por preparación compartida, no por salida comercial. Versiones distintas no se mezclan. Ingredientes directos se escalan por línea de perfil y luego se suman junto con los ingredientes de fórmulas por InventoryItemId/dimensión. Se rechazan dimensiones incompatibles, duplicados de salida, definiciones conflictivas, ingredientes duplicados en una fórmula y output utilizado simultáneamente como ingrediente físico.

MASS/VOLUME requieren divisibilidad exacta hasta mg/µL. Un residuo retorna NonRepresentableScale y el racional exacto. No se aplica nearest/floor/ceiling ni se aprueba quantum operativo diferente. El resultado no resuelto no contiene ProductionPlan ejecutable; sí conserva RequiredFormulas para explicar la demanda cuando se completó la evaluación de escala.

COUNT lleva política por línea/opción: ExactWholeCount rechaza fracciones; CeilingWholeCount sube a la siguiente pieza; Unspecified retorna CountPolicyRequired incluso si una escala particular es entera. Este último representa una regla comercial aún no validada, no un default silencioso. Polvorón no hereda la regla de huevo de masa dulce.

## Alternativas

IngredientAlternativeGroup contiene opciones de InventoryItem, incluso con dimensiones diferentes entre opciones (fresca MASS/seca COUNT). Se escoge una sola opción por grupo y versión. IDs de opción y grupo son configuración, nunca nombres reconocidos por código. La selección explícita tiene prioridad y no hace fallback. Si falta selección, sólo se usa PreferenceOrder configurado; se permite pasar a la siguiente por no representabilidad. Una política no aprobada u overflow bloquea, no induce sustitución.

El resultado expone IneligibleOptions y el plan conserva opción elegida, grupo, cantidad exacta teórica, política y cantidad resuelta. Esto hace visible 2.6 sobres rechazados y fresca elegida; no consulta disponibilidad ni sustituye por falta de stock. Un sobre completo se representa como artículo COUNT; su masa informativa de 11 g no dispara una conversión automática.

## Plan y resultado real

ProductionPlan es un cálculo puro e inmutable, no ProductionBatch confirmado ni reserva. Conserva outputs planeados, versiones de perfil, versiones de fórmula, factores, ingredientes resueltos y totales deterministas ordenados por identidad. ProductionActualOutput apunta al mismo plan preparado y exige una cantidad no negativa por cada producto. No reescala ingredientes a partir del rendimiento real.

Actual=0 y actual>planned son representables, con PendingBusinessPolicyProducts explícito. No se aprueba su confirmación ni se exige motivo; ambas políticas siguen pendientes. Ningún resultado real crea WASTE automáticamente. Idempotencia persistente, journal, permisos, origen de movimientos y contrato transaccional se definirán después, sin habilitar tipos reservados en schema 12.

## Datos representativos y pendientes

Fixtures: 3000 g harina / 5750 g masa, 2000 g harina / 3850 g masa, 85/80/70 g por producto, almendra 6 g, glass 2 g, ajonjolí 1.5 g, levadura fresca 30 g frente a 2 sobres y huevo con ceiling. Algunos tests usan fórmulas expresamente sintéticas para aislar reglas aritméticas; no son semillas productivas. La combinación 30 conchas + 20 cuernos + 10 bigotes requiere 4850 g y puede dejar harina no representable en mg con el rendimiento observado: se reporta, no se redondea.

Siguen pendientes: cocoa de concha chocolate; rendimiento preciso de cobertura (no fijar 26/27); relleno de rol; cuernito jamón/queso; peso de dona (no fijar 62.1 g); azúcar de dona; Procali chocolate/vainilla; chispas, coco, nuez y rayas de chocolate blanco; toppings y regla de huevo de polvorón; bolillo/puerquito y otros. Son datos de futuras versiones/configuración, no constantes de Domain.

## Fases futuras y exclusiones

1J.2 debe autorizar y diseñar persistencia de agregados, versiones activas, snapshots y contrato multi-output. No se creó 013. 1J.3 resolverá stock/transacción, confirmación, recovery y políticas pendientes. Propuesta de permisos: Administrator configura recetas y produce; Manager consulta recetas y produce; Cashier sin administración de recetas/producción. No se insertaron permisos.

UX futura: elegir cantidades, revisar necesidades, registrar cantidades reales y confirmar; sin mostrar racionales, unidades atómicas, hashes ni ledger. No UI implementada. No costos, compras, proveedores, subrecetas recursivas, datos productivos ni modificación de Checkout/ventas/impresión.
