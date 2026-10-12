# FASE 1K.4G-A — Auditoría maestra de recetas, productos y producción

Fecha de corte: 09/10/2026. Esta subfase es exclusivamente documental. No se insertaron ni modificaron productos, categorías, ingredientes, recetas, perfiles, saldos, lotes, ventas o movimientos. No existe migración 018.

## Checkpoint GitHub previo

El baseline local `5f5da76f2885ce9deaa2e74f3f3f33519eced5ec` se comparó después de `git fetch origin --prune` con `origin/QA` en `30887e85de0c3c1505c67c7c4169092e5e9b2b54`. La relación fue 0 commits exclusivos remotos y seis locales; QA era ancestro directo. Se ejecutó un push normal `HEAD:QA` y `origin/QA` quedó en `5f5da76f2885ce9deaa2e74f3f3f33519eced5ec`. No hubo force, merge, rebase, reset, modificación de `main` ni rama nueva. El commit documental de esta auditoría no se publicará.

## Fuente auditada y clasificación de datos

- Código: Domain, Application, Infrastructure, Presentation y migraciones 009–017 en el HEAD `5f5da76f2885ce9deaa2e74f3f3f33519eced5ec`.
- Datos: `work/phase1k4f-r2-smoke-20261009/casamiga.db`, copia aislada de revisión en schema 17.
- Base protegida: `work/phase1k2c-r2-smoke-20260928/casamiga.db`, schema 16; se leyó únicamente su hash de control.
- **Catálogo actual de revisión:** nombres, categorías, precios, SKU, flags y fotografías presentes en la copia. La auditoría no convierte esos valores en receta ni confirma por sí sola su vigencia comercial.
- **Dato de prueba/revisión:** saldo de Harina, ventas, evaluaciones y cualquier dato operativo del fixture. No se debe copiar a producción sin confirmación humana.
- **Dato técnico:** unidades, dimensiones, reglas de escalado, estados, contratos y restricciones persistidas.
- **Dato pendiente:** toda masa, fórmula, gramaje, rendimiento, extra, reserva, costo, proveedor, sustitución o política comercial no confirmada expresamente.

## Resultado del catálogo

- 31 productos, todos activos, en cinco categorías: Pan Salado (1), Pan dulce (20), Dona (5), Galleta (2) y Bebida (3).
- 26 productos tienen SKU; cinco no lo tienen.
- Fotografías actuales: 3 con procedencia `REAL`, 25 `PROVISIONAL` y 3 sin fotografía.
- Cinco productos tienen `track_inventory=1`, pero no existe ningún `product_inventory_link`; los otros 26 tienen el flag desactivado.
- Fórmulas: 0. Versiones de fórmula: 0. Perfiles: 0. Versiones de perfil: 0. Lotes: 0.
- Recetas completas: 0. Recetas parciales: 0. Productos sin receta operativa: 31.
- La matriz humana completa está en `PHASE_1K4G_PRODUCT_MATRIX.md`. No usa GUID como identificador principal.

## Ingredientes e inventario actuales

Sólo existe un artículo de inventario:

| Artículo | Código | Tipo | Dimensión | Activo | Saldo del fixture | Presentación | Uso en receta/perfil | Datos pendientes |
|---|---|---|---|---|---:|---|---|---|
| Harina | MP-HAR-001 | Materia prima | MASS | Sí | 50 kg, dato de revisión | HAR-BULTO-25KG · Bulto 25 kg · 25 kg | Ninguno | confirmar saldo comercial, costo, proveedor opcional, reserva mínima, lead time y recetas |

El saldo proviene de una operación `OPENING_BALANCE` confirmada por dos bultos. No hay entradas, mermas, consumo de producción, salida terminada ni vínculos de venta en esta copia. No existen campos persistidos para costo, proveedor, reserva mínima o lead time.

## Unidades exactas y redondeo

El núcleo usa `Int64` atómico con suma y multiplicación `checked`:

| Dimensión | Unidad atómica | Unidades visibles admitidas |
|---|---|---|
| MASS | mg | mg, g = 1,000 mg, kg = 1,000,000 mg |
| VOLUME | µL (`uL` en persistencia) | µL, ml = 1,000 µL, L = 1,000,000 µL |
| COUNT | piece | pieza = 1 |

Las escalas usan una razón racional reducida; no introducen decimales redondeados. MASS y VOLUME deben terminar exactamente en unidades atómicas: una fracción de mg o µL produce `NonRepresentableScale`. COUNT exige política explícita:

- `ExactWholeCount`: sólo acepta un entero exacto.
- `CeilingWholeCount`: redondea hacia arriba a pieza completa.
- `Unspecified`: bloquea la planificación con `CountPolicyRequired`.

### Huevo y otros indivisibles

La arquitectura puede representar huevo como un artículo COUNT y aplicar `ExactWholeCount` o `CeilingWholeCount`. No existe una política comercial configurada ni conversión automática huevo↔peso. **PENDIENTE DE USUARIO:** decidir por receta si sólo se aceptan huevos enteros exactos, si se redondea hacia arriba o si el ingrediente se controla por MASS mediante un artículo distinto.

### Levadura fresca frente a seca/Tradi Pan

Las alternativas están soportadas mediante un grupo versionado con opciones, cantidades explícitas y orden de preferencia. Esto puede representar levadura fresca frente a seca si existen ambos artículos y el usuario entrega las cantidades equivalentes por rendimiento base. No existe esa configuración. El motor no deduce equivalencias por nombre ni porcentaje.

## Mapa de receta y masa base

La estructura separa correctamente:

1. `Formula` y `FormulaVersion`: una preparación o masa base versionada, con rendimiento MASS/VOLUME/COUNT, ingredientes y alternativas.
2. `ProductProductionProfileVersion`: asocia un producto y un artículo terminado con una o varias fórmulas por pieza y con ingredientes directos por pieza.
3. `ProductionPlan`: suma por `FormulaVersionId` las necesidades de varios productos, escala cada fórmula una sola vez y consolida ingredientes.
4. Snapshots del lote: fijan nombres, cantidades, versiones y elecciones para que la historia no cambie si después se edita catálogo o receta.

Por ello, varios productos pueden compartir una masa sin duplicarla. Por ejemplo conceptual, A y B pueden requerir la misma versión de Masa X y el plan agrega ambas necesidades antes de escalar. En el fixture actual no hay ninguna masa base ni asociación real.

## Plan multiproducto actual

### Ya existe

- UI para varias salidas con producto/perfil/versión y cantidad planeada.
- Agregación de una fórmula compartida entre productos.
- Ingredientes directos por producto para rellenos, coberturas o decoraciones.
- Alternativas explícitas con orden y validación de elegibilidad.
- Vista previa de preparaciones, insumos, disponible, faltante, inactivo y saldo no inicializado.
- Detección de cantidades duplicadas, dimensiones incompatibles, overflow y escalas no representables.
- Creación de borrador con snapshots inmutables.
- Confirmación idempotente y recuperable dentro de una transacción.

### Falta para operar con Casa Miga

- Fórmulas base publicadas y activadas.
- Ítems de inventario para todos los ingredientes y productos terminados.
- Perfiles publicados/activos por producto.
- Gramaje o cantidad de masa por pieza, extras y rendimiento real.
- Política de COUNT para huevo, sobres y otros indivisibles.
- Existencias reales iniciales.
- Vínculos producto→inventario terminado para ventas.
- Reservas mínimas y estados OK/BAJO/CRÍTICO.
- Datos de costos.
- Destinos trazables de muestra/cortesía y uso interno.

## Planear, revisar y confirmar

`PrepareProduction` calcula requisitos y lee stock; no escribe ni reserva. El editor previo permanece transitorio. `CreateProduction` guarda un lote `DRAFT` y sus snapshots, pero tampoco mueve inventario. El lote persistido queda fijado: no se edita; para cambiar cantidades se crea uno nuevo.

`ConfirmProduction` revalida permisos, revisión, configuración activa, saldos y consistencia. En una sola transacción:

1. registra cantidades reales;
2. descuenta los insumos preparados mediante `PRODUCTION_CONSUMPTION`;
3. agrega como `PRODUCTION_OUTPUT` únicamente las piezas reales positivas;
4. confirma el lote y la auditoría.

El consumo se basa en el plan preparado, no en las piezas obtenidas. Un resultado cero consume la preparación y no genera salida terminada. Un resultado mayor que el planeado se conserva con `actual_policy_pending=1`; la arquitectura lo registra, pero la política comercial debe quedar confirmada antes de cargar producción real.

## Producto terminado y venta

El modelo vigente puede representar un producto terminado como `inventory_item` de tipo `FINISHED_GOOD` y dimensión COUNT. El perfil apunta a ese ítem; producción suma piezas reales. Un `product_inventory_link` activo con cantidad por producto permite que una venta reste del mismo ítem cuando el consumo automático está habilitado.

En la copia auditada no hay ítems terminados ni vínculos. El flag global está `DISABLED`; 19 evaluaciones históricas tienen estado `DISABLED`. Por tanto, hoy el catálogo no descuenta producto terminado y ningún producto está listo para planificación automática.

## Merma y otros destinos

Existe `WASTE` con motivos Dañado, Caducado, Error de producción, Derrame y Otro; puede restar un artículo con trazabilidad. Existen ajustes positivos/negativos con Diferencia de conteo físico, Corrección de datos u Otro. Ajuste no debe usarse para ocultar un destino comercial.

No existe soporte persistente y visible para MUESTRA/CORTESÍA, USO INTERNO, DEVOLUCIÓN o REPROCESO. Aunque Domain enumera `Return`, el CHECK vigente de SQLite y la UI no lo habilitan.

Propuesta mínima para 1.0:

- **MERMA:** reutilizar `WASTE` y sus motivos actuales.
- **MUESTRA/CORTESÍA:** salida trazable propia.
- **USO INTERNO:** salida trazable propia.
- **AJUSTE:** conservar exclusivamente para conteo físico/corrección.
- **DEVOLUCIÓN:** diferir hasta definir si es devolución de cliente, proveedor o reverso de venta; cada caso afecta una frontera distinta.
- **REPROCESO:** diferir a 1.1 porque requiere representar trabajo en proceso o transferencia entre estados/lotes, hoy inexistente.

La incorporación de muestra/uso interno exige persistencia nueva o ampliar de forma aditiva el contrato del ledger; probablemente forma parte de una futura migración 018, previa autorización.

## Estados futuros de stock

El preview actual ya conoce `Necesario`, `Disponible` y `Faltante`. No existe reserva mínima. Cuando el usuario proporcione una reserva por ingrediente, el cálculo propuesto es:

- `CRÍTICO`: disponible < necesario.
- `BAJO`: disponible ≥ necesario y disponible − necesario < reserva.
- `OK`: disponible − necesario ≥ reserva.

La reserva debe ser explícita por artículo, en su dimensión atómica, y no inferida en 1.0. Persistirla requerirá una estructura aditiva y, previsiblemente, migración 018.

## Plan de mañana y duplicación

El flujo futuro puede reutilizar el cálculo actual:

`Plan de mañana → productos/cantidades → fórmulas agregadas → insumos consolidados → stock → reserva → faltante`.

Crear o recalcular el preview no muta inventario. Guardar hoy crea un borrador fijado; no existe edición de borrador persistido ni concepto de fecha operativa del plan.

“Duplicar plan anterior” es barato para 1.0 como acción de UI: precargar productos, cantidades y versiones inmutables del lote anterior, generar nuevos BatchId/OperationId y volver a ejecutar el preview. No requiere nueva tabla. Antes de implementarlo debe decidirse si conserva las versiones históricas exactas o propone las versiones activas actuales; la opción segura por defecto es mostrar la diferencia y exigir confirmación.

## Productos estacionales

Producto, fórmula y perfil tienen activación separada. Desactivar un producto no borra ventas, snapshots ni lotes; las claves foráneas restringen borrado. Pan de muerto puede desactivarse/reactivarse sin módulo de temporadas. Falta una decisión humana sobre cuándo se muestra y si sus perfiles se desactivan al mismo tiempo.

## Costeo

El catálogo almacena precio de venta, pero inventario/recetas no almacenan costo de compra, costo vigente, historial de costo ni proveedor. El motor sí conserva cantidades exactas, por lo que podrá calcular costo por receta, lote y pieza cuando exista una política decimal y costos trazables.

Datos necesarios para **producir**: receta, rendimiento, perfil, cantidades, unidades, stock y políticas de indivisibles. Datos necesarios sólo para **costear**: contenido de compra, costo, vigencia y política de prorrateo. La ausencia de costos no debe bloquear la producción.

## Persistencia futura y migración 018

Ingresar recetas, perfiles, ingredientes, ítems terminados y vínculos usa schema 13–17 y no requiere migración. Probablemente sí hará falta una migración 018, pero sólo después de respuestas del usuario, para uno o varios de estos requisitos confirmados:

- reserva mínima por artículo;
- costos/presentaciones comerciales y su vigencia;
- motivos formales de muestra/cortesía y uso interno;
- fecha/estado editable de un plan futuro, si se exige editar planes ya guardados.

No se propone SQL todavía. Antes de autorizar 018 se debe escoger el alcance mínimo y evitar mezclar reservas, costeo y destinos si no se implementan juntos.

## Backup y restore

Fórmulas, perfiles, snapshots, lotes y ledger residen en SQLite y ya están cubiertos por `.cmbackup`; existen pruebas de restore para producción. Nuevas tablas/columnas requerirán validación de upgrade, integridad, FK, backup v1/v2 y restore a directorio nuevo. Fotografías siguen bajo el manifiesto v2. `operations-hub.json` continúa fuera de `.cmbackup` y debe resolverse en hardening 1K.5.

## Datos necesarios para el reporte diario 1K.4H

Ya pueden derivarse, una vez configurados datos reales: planeado/real producido, ventas, pagos, existencia y materias primas consumidas. Faltan semánticas formales para muestra/uso interno, reservas y plan de mañana. Merma existente puede reportarse por `WASTE`. El dinero proviene de ventas/pagos y no debe mezclarse con costo estimado.

## Capacidad actual de UI

- **Recetas:** crear fórmula/perfil, guardar borrador, publicar, activar, versionar, agregar ingredientes, alternativas y requisitos directos; no hay importación masiva ni asistente de captura comercial.
- **Producción:** plan multiproducto, preview, faltantes, alternativas, borrador fijado, captura real, confirmación, borradores e historial.
- **Inventario:** artículos, unidades/presentaciones, saldo inicial, entrada, merma, conteo físico e historial.
- **Faltante en UI:** reservas, costos/proveedores, muestra/uso interno, duplicar plan, fecha “mañana”, edición de borrador persistido y estados OK/BAJO/CRÍTICO.

## Siguiente paso recomendado

El usuario debe responder `PHASE_1K4G_RECIPE_QUESTIONNAIRE.md`, comenzando por identificar masas base y productos comprados/terminados. Después se debe reconciliar cada respuesta con `PHASE_1K4G_PRODUCT_MATRIX.md` y proponer 1K.4G-B. No cargar recetas ni crear migración 018 hasta contar con esas respuestas.

## Verificación técnica

- `scripts/verify.ps1`: 577/577 pruebas generales y 206/206 WPF/Presentation; total 783/783, 0 fallidas y 0 omitidas.
- Release: 0 errores y 0 advertencias.
- `git diff --check`: correcto.
- `src`, `tests` y `migrations`: sin cambios.
- Base protegida: schema 16 y SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, idéntico al inicio.
- Fixture auditado: schema 17.
- Migración 018: ausente.
