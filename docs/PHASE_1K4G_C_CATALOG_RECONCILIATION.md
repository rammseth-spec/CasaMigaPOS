# FASE 1K.4G-C-A — Conciliación del catálogo comercial real

Fecha de corte: 18/10/2026. Esta subfase es exclusivamente documental y de sólo lectura. No se insertaron, actualizaron ni desactivaron productos; no se alteraron ventas, pagos, caja, fotografías, recetas, inventario o saldos; no se ejecutaron migraciones. **1K.4G-C-B no fue iniciada.**

## Resultado ejecutivo

El catálogo maestro aprobado contiene **36 productos comerciales individuales**. La copia que conserva el catálogo de revisión más reciente contiene 31 productos activos. La conciliación arroja:

- **27/36** productos maestros tienen al menos un candidato actual.
- **9/36** no tienen un registro individual equivalente y deben crearse.
- Clasificación de las 36 filas: **14 CONSERVAR**, **11 RENOMBRAR**, **9 AGREGAR**, **1 REVISAR DUPLICADO** y **1 REQUIERE DECISIÓN COMERCIAL**.
- Existen **dos retiros comerciales confirmados**: Puerquito y Coronita de nuez.
- Existe **un registro histórico combinado que debe quedar fuera del catálogo activo** después de crear sus dos reemplazos: Galleta de nuez y coco.
- De los dos registros del bollo de crema, uno deberá quedar activo y el otro desactivarse cuando el usuario confirme el ID canónico y el precio.
- Dona especial sólo se desactivará si el usuario determina que no corresponde a la dona combinada aprobada.
- No hacen falta cambios de schema ni una migración nueva para las altas, renombres, cambios de categoría o desactivaciones. El contrato actual de `CatalogService` ya crea, edita y activa/desactiva productos con autorización y auditoría.

Los precios listados como “actuales” son evidencia del fixture, no una aprobación comercial. El catálogo maestro no incluyó precios. Las fotografías `PROVISIONAL` tampoco certifican que correspondan al producto real.

## Fuentes y bases examinadas

| Fuente | Uso | Schema | SHA-256 observado | Resultado |
|---|---|---:|---|---|
| `work/phase1k2c-r2-smoke-20260928/casamiga.db` | Base protegida de referencia | 16 | `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D` | Intacta; contiene la versión anterior de 28 productos. Sólo lectura. |
| `work/phase1k4f-r2-smoke-20261009/casamiga.db` | Origen del catálogo de revisión más reciente | 17 | `857783634068D89FE920FC422C1E26C5ECE4A29D10902A93AE78391D3B1B1E1F` | 31 productos activos, 20 ventas/46 líneas, 0 fórmulas, 0 perfiles y 0 vínculos producto-inventario. Sólo lectura. |
| `work/phase1k4g-b-r2-smoke-20261011/casamiga.db` | Evidencia cerrada del smoke de producción | 17 | `4AFE8F3F74FE6B6977D899DAC7A8C3B94665B75815540795435209549B7C2FFE` | Contiene únicamente cinco productos sintéticos `DEMO-*`; no es fuente del catálogo comercial y no se reutilizó. Sólo lectura. |

Las consultas SQLite se abrieron mediante URI `mode=ro` y `PRAGMA query_only=ON`. El fixture 1K.4G-B-R2 fue inspeccionado únicamente para descartar que fuese el origen del catálogo; no se ejecutó ninguna operación sobre él.

## Convenciones de la matriz

- **Precio:** valor observado en el catálogo actual. `PENDIENTE` significa que no existe un registro del cual leerlo. Todo precio debe confirmarse antes de C-B.
- **Foto:** `REAL`, `PROVISIONAL` o `SIN FOTO`. No se copiarán fotos entre IDs y no se generarán imágenes artificiales.
- **Inv./receta:** `T` es el flag `track_inventory`; `L` son vínculos `product_inventory_link`; `P` son perfiles `production_profile`.
- **Ventas:** líneas históricas/piezas observadas en el fixture del catálogo. Las ventas conservan snapshots inmutables.
- Las familias del maestro no se convierten automáticamente en categorías nuevas. La columna Categoría usa la categoría técnica propuesta con el catálogo actual.

## Matriz de los 36 productos aprobados

| # | Producto aprobado | Categoría propuesta | Registro actual / ProductId | SKU | Precio actual | Estado y foto | Inv./receta; ventas | Clasificación | Acción técnica propuesta |
|---:|---|---|---|---|---:|---|---|---|---|
| 1 | Concha de vainilla | Pan dulce | Concha de vainilla · `e4749ad8-0671-4a90-b616-f096a667af45` | CM-001 | $13.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 7/10 | CONSERVAR | Preservar ID/SKU; confirmar precio y sustituir foto por una real cuando se entregue. |
| 2 | Concha de chocolate | Pan dulce | Concha de chocolate · `3ac35bb4-414c-4f90-ad47-65807101f556` | CM-002 | $13.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 3/6 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. |
| 3 | Rol de canela | Pan dulce | Rol de canela · `1fde7b16-c2bc-4f2e-ac37-cf7dd2e5404e` | CM-006 | $15.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 2/2 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. |
| 4 | Danés con crema pastelera | Pan dulce | Danés con crema pastelera · `6578f84a-8568-4772-94be-0dc03688af0a` | CM-016 | $15.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 0/0 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. |
| 5 | Cuernito de mantequilla | Pan dulce | Cuernito de mantequilla · `059d9f77-e474-4fc2-bb7a-762a0cb977f2` | CM-004 | $13.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 0/0 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. |
| 6 | Cuernito de almendra (bigote) | Pan dulce | Cuernito especial con almendra · `9d2f58bc-5e76-44c3-9e67-8c46048dd696` | CM-005 | $15.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 1/1 | RENOMBRAR | Renombrar el mismo ID; conservar historial. No crear “Bigote” aparte. |
| 7 | Rosquita de ajonjolí | Pan dulce | No existe | PENDIENTE | PENDIENTE | SIN FOTO | T pendiente; L=0, P=0; 0/0 | AGREGAR | Crear un producto individual cuando se confirmen precio, SKU y flag de inventario. |
| 8 | Bollo relleno de crema pastelera | Pan dulce | Candidato A: `8b0711ef-b6e6-47dc-b7f2-01ace22149f2`; candidato B: `60ae6ccc-bd2e-47cb-aa51-f73d9fd68a05` | CM-017 / CM-018 | $15.00 / $16.00 | Ambos activos · PROVISIONAL | Ambos T=No, L=0, P=0; A 1/2, B 1/2 | REVISAR DUPLICADO | Recomendación: conservar A por coincidir con el nombre maestro y desactivar B, sin mover ventas. Requiere confirmar ID, precio y foto canónicos. |
| 9 | Bollo relleno de zarzamora | Pan dulce | Bollo relleno de zarzamora · `c6ba8dd7-8176-4f6f-80db-69971bb34923` | CM-019 | $16.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 3/4 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. |
| 10 | Bollo con ajonjolí | Pan dulce | No existe | PENDIENTE | PENDIENTE | SIN FOTO | T pendiente; L=0, P=0; 0/0 | AGREGAR | Crear producto individual tras confirmar datos comerciales. |
| 11 | Bolillo de mantequilla | Pan Salado | Bolillo mantequilla · `f9d8569d-765a-4152-bc61-36cc3f88ef24` (hoy en Pan dulce) | CM-003 | $8.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 7/15 | RENOMBRAR | Mantener ID/SKU, añadir “de” y mover a Pan Salado si el usuario confirma que las familias maestras son categorías operativas. |
| 12 | Baguette con ajonjolí | Pan Salado | Baguette · `4274e78f-3c15-49c9-977b-f474e9824e8e` | PENDIENTE | $9.00 | Activo · SIN FOTO | T=Sí, L=0, P=0; 1/2 | RENOMBRAR | Mantener ID, renombrar; confirmar SKU, precio, foto y si se conserva T=Sí. |
| 13 | Dona de azúcar | Dona | Dona de azúcar · `77a5b8a8-53f9-42bb-a9ab-c607d0f1054b` | CM-008 | $13.00 | Activo · REAL | T=No, L=0, P=0; 1/1 | CONSERVAR | Preservar ID/SKU y foto real; confirmar precio. |
| 14 | Dona de chocolate negro | Dona | Dona de chocolate · `a792f462-e335-4d8b-a215-37625c36777a` | CM-007 | $13.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 1/1 | RENOMBRAR | Mantener ID/SKU y precisar “negro”; confirmar precio y foto. |
| 15 | Dona de chocolate negro con chispas | Dona | No existe | PENDIENTE | PENDIENTE | SIN FOTO | T pendiente; L=0, P=0; 0/0 | AGREGAR | Crear variante individual; no reutilizar otra dona. |
| 16 | Dona de chocolate blanco | Dona | Dona sencilla de chocolate blanco · `a26997fb-46df-4fe9-bd30-173ae115c675` | CM-021 | $13.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 2/2 | RENOMBRAR | Mantener ID/SKU y adoptar nombre maestro; confirmar precio y foto. |
| 17 | Dona de chocolate blanco con coco | Dona | No existe | PENDIENTE | PENDIENTE | SIN FOTO | T pendiente; L=0, P=0; 0/0 | AGREGAR | Crear variante individual. |
| 18 | Dona de chocolate con nuez | Dona | No existe | PENDIENTE | PENDIENTE | SIN FOTO | T pendiente; L=0, P=0; 0/0 | AGREGAR | Crear variante individual. |
| 19 | Dona combinada de chocolate negro y blanco | Dona | Posible candidato: Dona especial · `61dc450a-7887-41c7-b2f1-2fed64e40255` | CM-009 | $15.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 2/3 | REQUIERE DECISIÓN COMERCIAL | La descripción histórica abarca coco, nuez, más chocolate y otras coberturas; no prueba equivalencia. Confirmar si se renombra este ID o se crea uno nuevo y se desactiva Dona especial. |
| 20 | Dona glaseada | Dona | Dona glaseada · `b19768a9-684c-4d46-878f-fb396194bf88` | CM-020 | $13.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 2/3 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. |
| 21 | Berlinesa rellena de crema pastelera | Dona | No existe | PENDIENTE | PENDIENTE | SIN FOTO | T pendiente; L=0, P=0; 0/0 | AGREGAR | Crear producto individual. |
| 22 | Berlinesa rellena de zarzamora | Dona | No existe | PENDIENTE | PENDIENTE | SIN FOTO | T pendiente; L=0, P=0; 0/0 | AGREGAR | Crear producto individual. |
| 23 | Polvorón tradicional | Pan dulce | Polvorón · `ac1d3c85-b798-4087-90bf-a751bec30f42` | CM-010 | $13.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 0/0 | RENOMBRAR | Mantener ID/SKU y precisar “tradicional”; confirmar precio y foto. |
| 24 | Polvorón de nuez | Pan dulce | Polvorón de nuez · `8c72c1c6-c3a3-4f3d-bc87-c11a15f6fab3` | CM-011 | $15.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 1/2 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. |
| 25 | Galleta de nuez | Galleta | No existe como producto individual; antecedente combinado `4f0e16b4-2cb3-40e8-84fe-57094ce1cbe7` | PENDIENTE | PENDIENTE | SIN FOTO propia | T pendiente; L=0, P=0; 0/0 propia | AGREGAR | Crear nuevo ID. No reutilizar el ID, SKU, precio o foto del combinado. |
| 26 | Galleta de coco | Galleta | No existe como producto individual; mismo antecedente combinado | PENDIENTE | PENDIENTE | SIN FOTO propia | T pendiente; L=0, P=0; 0/0 propia | AGREGAR | Crear nuevo ID. No reutilizar el combinado. |
| 27 | Galleta con chispitas de colores | Galleta | Galleta con chispitas de colores · `644edb70-10c4-4858-bfa9-99c66401bbaa` | CM-023 | $13.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 1/1 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. |
| 28 | Galleta multigrano | Galleta | GALLETA MULTIGRANO · `f532a645-e89a-480d-93d7-d4f2cf3067b8` (hoy en Pan dulce) | PENDIENTE | $15.00 | Activo · REAL | T=Sí, L=0, P=0; 1/1 | RENOMBRAR | Normalizar nombre, mover a Galleta, conservar foto real; confirmar SKU, precio y T=Sí. |
| 29 | Pay de queso individual | Pan dulce | Pay de queso · `0364ccd2-6c83-41bf-9a5d-477514dfa7a1` | PENDIENTE | $22.00 | Activo · SIN FOTO | T=Sí, L=0, P=0; 1/2 | RENOMBRAR | Mantener ID y añadir “individual”; confirmar SKU, precio, foto y T=Sí. |
| 30 | Pan de muerto pequeño | Pan dulce | Pan de muerto · `7119bef9-735a-4170-8f96-8d2962a1fc79` | CM-013 | $15.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 1/3 | RENOMBRAR | Mantener ID/SKU y precisar tamaño pequeño; confirmar precio y foto. |
| 31 | Pan de muerto grande | Pan dulce | Pan de Muerto Grande · `a67359c8-e885-4764-8a97-6051cc985fe4` | PENDIENTE | $22.00 | Activo · SIN FOTO | T=Sí, L=0, P=0; 2/3 | RENOMBRAR | Mantener ID, normalizar capitalización; confirmar SKU, precio, foto y T=Sí. |
| 32 | Muffin de nuez | Pan dulce | Muffin de nuez · `8e8fedbe-a1b3-47e4-8709-cbc7f098d3ec` | CM-015 | $15.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 0/0 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. La receta sigue pendiente. |
| 33 | Muffin sencillo | Pan dulce | Muffin · `b8c445c4-6197-4d9b-be8b-1d7f07850bce` | CM-014 | $13.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 0/0 | RENOMBRAR | Mantener ID/SKU y precisar “sencillo”; confirmar precio y foto. La receta sigue pendiente. |
| 34 | Vaso de café | Bebida | Vaso de café · `63198b97-7df4-4e00-82e6-063a772b5a44` | CM-024 | $20.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 1/1 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto. |
| 35 | Leche Kirkland entera | Bebida | Leche Kirkland entera · `6b11d309-148f-4103-97f8-cf4d2f02168c` | CM-025 | $28.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 1/1 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto real del producto vendido. |
| 36 | Leche Kirkland deslactosada light | Bebida | Leche Kirkland deslactosada light · `c2c87410-7766-4551-87c6-32d205f54c9f` | CM-026 | $28.00 | Activo · PROVISIONAL | T=No, L=0, P=0; 1/1 | CONSERVAR | Preservar ID/SKU; confirmar precio y foto real del producto vendido. |

## Productos faltantes exactos

Los siguientes nueve productos no existen como identidad individual y deben crearse en C-B sólo después de recibir sus datos comerciales:

1. Rosquita de ajonjolí.
2. Bollo con ajonjolí.
3. Dona de chocolate negro con chispas.
4. Dona de chocolate blanco con coco.
5. Dona de chocolate con nuez.
6. Berlinesa rellena de crema pastelera.
7. Berlinesa rellena de zarzamora.
8. Galleta de nuez.
9. Galleta de coco.

La dona combinada no se cuenta aquí porque tiene a Dona especial como candidato no confirmado. Si el usuario rechaza esa equivalencia, también deberá crearse y el total de altas será diez.

## Equivalencias, duplicados y desactivaciones

### Bollo de crema

- `CM-017`, ID `8b0711ef-b6e6-47dc-b7f2-01ace22149f2`, nombre maestro exacto, $15.00, foto provisional, una línea/dos piezas históricas.
- `CM-018`, ID `60ae6ccc-bd2e-47cb-aa51-f73d9fd68a05`, “por la mitad”, $16.00, foto provisional, una línea/dos piezas históricas.
- Recomendación técnica: conservar `CM-017` y desactivar `CM-018`, pero sólo después de confirmar que $15.00 es el precio vigente y que la foto de `CM-017` es la correcta. No mover, reescribir ni sumar ventas entre IDs.

### Cuernito de almendra

`CM-005` ya concentra “Cuernito especial con almendra”; tiene una línea/una pieza histórica. Debe conservar su ID y renombrarse a “Cuernito de almendra (bigote)”. No se crea un segundo Bigote.

### Galleta de nuez y coco

`CM-022`, ID `4f0e16b4-2cb3-40e8-84fe-57094ce1cbe7`, tiene $15.00, foto provisional y una línea/una pieza histórica. Como el maestro confirma dos productos separados, se crearán dos IDs nuevos y después se desactivará el combinado. Su venta histórica seguirá apuntando a `CM-022` y conservará el snapshot “Galleta de nuez y coco”. No se copiarán SKU, precio o foto a las nuevas galletas.

### Dona especial

`CM-009`, ID `61dc450a-7887-41c7-b2f1-2fed64e40255`, tiene $15.00, foto provisional y dos líneas/tres piezas históricas. Su descripción menciona coco, nuez, más chocolate y otras coberturas, por lo que no demuestra que sea la combinación negro/blanco. El usuario debe escoger entre:

1. confirmar la equivalencia y renombrar el mismo ID; o
2. crear la dona combinada con ID nuevo y desactivar Dona especial.

### Pan de muerto

- Pequeño: conservar `CM-013`, ID `7119bef9-735a-4170-8f96-8d2962a1fc79`, $15.00, foto provisional y una línea/tres piezas históricas.
- Grande: conservar ID `a67359c8-e885-4764-8a97-6051cc985fe4`, sin SKU, $22.00, sin foto y dos líneas/tres piezas históricas.

### Registros a desactivar

Desactivaciones firmes una vez que C-B sea autorizada:

- Puerquito, `CM-012`, ID `b793c764-ee82-45bb-a041-fa01e477addd`; una línea/una pieza histórica. Corresponde al retiro confirmado “Puerquito de piloncillo”.
- CORONITA DE NUEZ, sin SKU, ID `40b671df-2449-446b-ad34-0f2fdf82403b`; sin ventas, foto REAL. La foto se conserva como metadata histórica del mismo ID.
- Galleta de nuez y coco, `CM-022`, después de crear y validar las dos nuevas galletas.

Desactivaciones condicionadas:

- Uno de `CM-017`/`CM-018`, después de decidir el registro canónico del bollo.
- Dona especial, sólo si no corresponde a la dona combinada aprobada.

La desactivación usa `SetProductActive`; los triggers `product_keep_history` y las FK `RESTRICT` prohíben el borrado. Los tickets y ventas anteriores conservan sus snapshots.

## Pendientes de precio, SKU y fotografía

### Precio

- Nueve altas tienen precio totalmente pendiente.
- El bollo duplicado presenta un conflicto de $15.00 frente a $16.00.
- Dona especial aporta $15.00, pero ese valor no puede heredarse a la dona combinada sin confirmar equivalencia.
- Los otros 25 candidatos tienen un precio actual legible, pero el usuario debe confirmarlo porque la lista maestra aprobó identidades, no precios.

### SKU

Con la estrategia propuesta existen al menos 13 SKU pendientes: nueve altas y cuatro productos conservados/renombrados sin SKU (Baguette, Galleta multigrano, Pay de queso individual y Pan de muerto grande). Serán 14 si la dona combinada requiere un ID nuevo.

Se puede mantener la convención `CM-###`, pero **no se reservan ni asignan números en esta auditoría**. Antes de C-B debe definirse si se continúa la secuencia desde `CM-026`, cómo se documentan huecos y si productos estacionales conservan SKU estable al desactivarse/reactivarse.

### Fotografías

En la proyección que reutiliza un candidato por producto maestro:

- Dos productos aprobados tienen foto `REAL`: Dona de azúcar y Galleta multigrano.
- Veintidós tienen foto `PROVISIONAL` y requieren foto real.
- Doce no tienen foto propia: las nueve altas, Baguette, Pay de queso individual y Pan de muerto grande.
- La foto REAL de Coronita no se reutiliza porque el producto se retira.
- Las fotos del bollo descartado, Galleta de nuez y coco y Dona especial permanecen ligadas a sus IDs históricos. No se copian a identidades nuevas.

## Riesgos de integridad histórica

1. **No cambiar ProductId al renombrar.** Las ventas apuntan al ID y guardan además nombre/precio como snapshot; un renombre sólo cambia el catálogo vigente.
2. **No fusionar ventas.** Los dos bollos tienen IDs y referencias históricas separadas. Reasignar `sale_item.product_id` está prohibido por inmutabilidad y alteraría historia financiera.
3. **No borrar.** Productos, ventas, líneas y pagos están protegidos por triggers/FK. La salida correcta es desactivar.
4. **No reutilizar el combinado de galleta.** Convertir su ID en “Galleta de nuez” falsearía el significado del producto vigente y mezclaría su foto/SKU con una venta histórica combinada.
5. **Dona especial es semánticamente ambigua.** Renombrarla sin confirmación podría atribuir tres piezas históricas a una variedad que no fue la vendida.
6. **Snapshot atípico en el fixture.** Una línea asociada al ProductId de `CM-017` conserva el snapshot “Cuernito especial con almendra y azúcar”. No se corregirá: la línea es inmutable y demuestra por qué C-B debe verificar IDs y snapshots por separado.
7. **Precios del fixture no son aprobación.** Cambiar el precio del producto no altera ventas previas, pero sí las ventas nuevas; se requiere confirmación humana.
8. **Fotos por ID.** Renombrar conserva la foto del mismo ID; crear un producto nuevo no debe heredar automáticamente una imagen del producto histórico.
9. **Inventario y recetas.** Los 31 productos del catálogo no tienen vínculos ni perfiles. Algunos tienen `track_inventory=1` sin vínculo. C-B no debe activar consumo ni crear relaciones de inventario/receta.
10. **Origen de datos.** La base protegida tiene 28 productos y debe seguir intacta; el catálogo de 31 proviene de un fixture de revisión. Antes de aplicar C-B se debe declarar explícitamente cuál DataDirectory recibirá los cambios y trabajar primero en una copia aislada.

## Estrategia técnica segura para 1K.4G-C-B

1. Obtener las decisiones comerciales pendientes que aparecen al final de este documento.
2. Identificar y cerrar la instancia objetivo; verificar `DirectoryLease`, launcher, DataDirectory y hash. Crear una copia aislada coherente para el primer pase.
3. Tomar un inventario previo de categorías, productos, fotos, ventas, pagos, caja, inventario, perfiles y hashes.
4. Ejecutar todas las mutaciones mediante los casos de uso existentes, con un usuario autorizado:
   - `EditProduct` para nombre, categoría, SKU, precio y flag de inventario;
   - `CreateProduct` para identidades faltantes;
   - `SetProductActive` para retiros y duplicados.
5. Preservar todos los ProductId que sólo se renombran. Generar un ID nuevo únicamente para cada producto realmente faltante.
6. Crear y verificar primero los reemplazos de la galleta combinada; desactivar el combinado después.
7. Resolver el bollo y Dona especial en operaciones separadas, con evidencia antes/después.
8. No copiar fotos entre ProductId. La carga de fotos reales se realiza por el flujo de foto ya implementado cuando el usuario entregue los archivos.
9. No crear fórmulas, perfiles, ítems terminados, vínculos ni saldos; no cambiar el flag global de consumo por ventas.
10. Verificar el catálogo resultante en una copia, obtener aprobación humana y sólo entonces preparar cualquier aplicación sobre otra DataDirectory autorizada.

No se requiere migración 018: `product`, `category`, auditoría, activación y fotos ya soportan estas operaciones. Tampoco se requiere cambio de código para la conciliación descrita. Si C-B necesita una carga repetible, debe ser una herramienta controlada que invoque Application, no SQL manual ni una migración con datos comerciales.

## Estrategia de pruebas para C-B

### Antes

- Hash, schema, `foreign_key_check`, `integrity_check` y conteos de tablas críticas.
- Snapshot de los 31 productos con ID, SKU, nombre, categoría, precio, activo, foto, `track_inventory`, vínculos, perfiles y referencias de venta.
- Conteos/hashes de `sale`, `sale_item`, `payment`, `cash_session`, ledger, fórmulas, perfiles y fotos.

### Durante

- Una operación de catálogo por producto con auditoría esperada.
- Reintento seguro: ninguna alta duplicada por SKU y ningún producto activo en categoría inactiva.
- Verificación inmediata del ID conservado después de cada renombre.
- Desactivar sólo después de comprobar reemplazo activo cuando corresponda.

### Después

- Exactamente 36 identidades comerciales activas, si todas las decisiones fueron resueltas.
- Cero nombres/SKU duplicados bajo normalización; categorías activas y filtros POS correctos.
- Los nueve o diez IDs nuevos corresponden sólo a altas autorizadas.
- Los IDs renombrados no cambian; ventas, líneas, pagos, caja y tickets históricos permanecen byte/filas equivalentes.
- Puerquito, Coronita, combinado de galleta y el duplicado elegido quedan inactivos; Dona especial según decisión.
- `foreign_key_check` vacío, `integrity_check=ok`, saldos/ledger sin cambios, fórmulas/perfiles/vínculos sin cambios.
- Fotografías siguen ligadas al mismo ProductId; ninguna foto artificial o copiada.
- Pruebas focales de catálogo/POS/historial/fotos/autorización y luego `scripts/verify.ps1`, Release, `git diff --check` y smoke manual en build/DataDirectory identificados.

## Decisiones necesarias antes de C-B

1. Declarar la DataDirectory exacta que se usará como origen autorizado. La base protegida no debe modificarse y la fixture R2 cerrada no se reutiliza.
2. Confirmar si las familias del maestro son sólo agrupaciones documentales o nuevas categorías visibles. Recomendación mínima: conservar cinco categorías y mover Bolillo a Pan Salado y Galleta multigrano a Galleta.
3. Confirmar `CM-017` como bollo canónico y su precio; decidir qué foto representa al producto.
4. Confirmar si Dona especial equivale a Dona combinada de chocolate negro y blanco.
5. Confirmar los nueve precios faltantes y validar los precios actuales de los 27 candidatos.
6. Aprobar el esquema de SKU y asignar los 13 o 14 SKU pendientes.
7. Confirmar `track_inventory` de las altas y de los cuatro candidatos que hoy están en `Sí`. Recomendación: no activar consumo ni vínculos hasta la etapa controlada de recetas/inventario.
8. Proporcionar fotos reales. C-B puede cerrar el catálogo sin ellas, pero debe mantener explícitamente su estado pendiente.
9. Confirmar que Galleta de nuez y coco se desactivará tras crear las dos variantes separadas.

## Verificación de esta subfase

- Rama inicial: `master`.
- HEAD inicial: `8beafeb8dad1103d17cd53bdb003fca8f758c74c`.
- Working tree inicial: limpio.
- Se inspeccionaron `CatalogService`, `SqliteCatalogStore`, migraciones 001/004/006/009/013/016/017 y la documentación 1K.4G pertinente.
- Las tres bases se consultaron en modo de sólo lectura; sus hashes quedaron iguales al terminar las consultas.
- No se repitió la suite 799/799 ni Release porque no cambió código, pruebas o migraciones. La regla de cierre documental exige revisar el diff y confirmar que `src/`, `tests/` y `migrations/` permanezcan intactos.

## Credenciales vigentes de desarrollo

- Usuario: `catalog-admin`
- Contraseña: `Phase1K2C-R2!2026`

Estas credenciales corresponden a las copias de revisión que ya fueron verificadas. No se restablecieron ni se almacenaron en ninguna base nueva.

## Recomendación para continuar

Ejecutar 1K.4G-C-B con **GPT-6 Sol, esfuerzo alto**, una vez contestadas las nueve decisiones. El alcance puede mantenerse en schema 17 y reutilizar servicios existentes; el riesgo principal es la conciliación histórica de IDs, snapshots, precios y fotos, no una carencia del modelo de datos.

**Estado de 1K.4G-C-A: COMPLETADA DOCUMENTALMENTE. 1K.4G-C-B: NO INICIADA Y BLOQUEADA POR DECISIONES COMERCIALES.**
