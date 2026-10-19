# FASE 1K.4G-C-B — Catálogo comercial aprobado

Fecha: 18/10/2026. Estado: **implementación técnica completada en copia aislada; smoke humano de Catálogo y POS pendiente**. No se inició 1K.4G-C-C. Rama `master`; HEAD inicial `dc30ddf2686561c191fd15ecb9fa9365dce4ce46`. Este checkpoint cambia datos de una copia local de desarrollo y documentación; no cambia código de producto, pruebas ni migraciones y no se publicó en GitHub.

## Procedencia y protección

| Instalación | Función | SHA-256 de `casamiga.db` |
|---|---|---|
| `work/phase1k4f-r2-smoke-20261009` | Origen autorizado con 31 productos activos, schema 17 | `857783634068D89FE920FC422C1E26C5ECE4A29D10902A93AE78391D3B1B1E1F` |
| `work/phase1k4g-c-b-review-20261018` | Copia aislada de revisión, schema 17 | Antes `BFCA70166ABD09E929BB28CC85C7FACF768C5DE85B371CDDF28FA3CFF7199344`; después `F16ED3CFA7CDF94B0DC20B8E790DCC3B27A69F065A1FAB35BCE122050B282576` |
| `work/phase1k4g-b-r2-smoke-20261011` | Fixture cerrada de producción/merma; no utilizada | `4AFE8F3F74FE6B6977D899DAC7A8C3B94665B75815540795435209549B7C2FFE` |
| `work/phase1k2c-r2-smoke-20260928` | Base protegida; no utilizada | `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D` |

El origen se consultó en modo SQLite `mode=ro`; `integrity_check=ok`, `foreign_key_check=0`, 31 activos. La copia se obtuvo con la API de backup de SQLite para respetar WAL. Se copiaron `terminal.json`, `printing.json` y 56 archivos de imagen, comparando sus hashes. No se copiaron lock, WAL ni SHM. El catálogo más reciente de 1K.4G-B-R2 sólo tenía cinco productos sintéticos y no era un origen comercial adecuado. No se tocó la base predeterminada del equipo.

La aplicación de cambios usó exclusivamente `CatalogService.CreateProduct`, `EditProduct` y `SetProductActive`, tras preflight de identidades, códigos y precios. El manifiesto local está en `work/phase1k4g-c-b-review-20261018/catalog-change-manifest.json`; las instantáneas y hashes por tabla están en `source-before.json`, `destination-before.json` y `destination-after.json` de la misma carpeta. Estos archivos contienen evidencia local ignorada por Git.

## Resultado comercial

La copia tiene **41 identidades históricas: exactamente 36 activas y 5 inactivas**. Diez productos recibieron un ProductId nuevo; once productos se editaron conservando ProductId; cinco se desactivaron conservando ProductId y su historial. El resto se conservó. Las cinco categorías existentes permanecen: Pan dulce, Pan Salado, Dona, Galleta y Bebida. No existe SKU activo duplicado ni nombre comercial activo duplicado. Los SKU `CM-027` a `CM-040` se asignaron sin colisionar ni reutilizar códigos de inactivos; los códigos existentes válidos se conservaron. `CORONITA DE NUEZ` permanece inactiva con SKU nulo histórico.

En esta tabla **C** = precio confirmado explícitamente para C-B; **H** = precio heredado del origen, aún sujeto a revisión comercial. Las fotografías `PROVISIONAL` no certifican correspondencia con el producto real.

| Producto activo | SKU | Precio MXN | Base | Foto |
|---|---|---:|:---:|---|
| Concha de vainilla | CM-001 | $13.00 | H | PROVISIONAL |
| Concha de chocolate | CM-002 | $13.00 | H | PROVISIONAL |
| Rol de canela | CM-006 | $15.00 | H | PROVISIONAL |
| Danés con crema pastelera | CM-016 | $15.00 | H | PROVISIONAL |
| Cuernito de mantequilla | CM-004 | $13.00 | H | PROVISIONAL |
| Cuernito de almendra (bigote) | CM-005 | $15.00 | H | PROVISIONAL |
| Rosquita de ajonjolí | CM-027 | $13.00 | C | SIN FOTO |
| Bollo relleno de crema pastelera | CM-017 | $15.00 | C | PROVISIONAL |
| Bollo relleno de zarzamora | CM-019 | $16.00 | C | PROVISIONAL |
| Bollo con ajonjolí | CM-028 | $13.00 | C | SIN FOTO |
| Bolillo de mantequilla | CM-003 | $8.00 | H | PROVISIONAL |
| Baguette con ajonjolí | CM-029 | $9.00 | H | SIN FOTO |
| Dona de azúcar | CM-008 | $13.00 | H | REAL |
| Dona de chocolate negro | CM-007 | $13.00 | H | PROVISIONAL |
| Dona de chocolate negro con chispas | CM-030 | $15.00 | C | SIN FOTO |
| Dona de chocolate blanco | CM-021 | $13.00 | H | PROVISIONAL |
| Dona de chocolate blanco con coco | CM-031 | $15.00 | C | SIN FOTO |
| Dona de chocolate con nuez | CM-032 | $15.00 | C | SIN FOTO |
| Dona combinada de chocolate negro y blanco | CM-033 | $15.00 | C | SIN FOTO |
| Dona glaseada | CM-020 | $13.00 | H | PROVISIONAL |
| Berlinesa rellena de crema pastelera | CM-034 | $15.00 | C | SIN FOTO |
| Berlinesa rellena de zarzamora | CM-035 | $16.00 | C | SIN FOTO |
| Polvorón tradicional | CM-010 | $13.00 | H | PROVISIONAL |
| Polvorón de nuez | CM-011 | $15.00 | H | PROVISIONAL |
| Galleta de nuez | CM-036 | $15.00 | C | SIN FOTO |
| Galleta de coco | CM-037 | $15.00 | C | SIN FOTO |
| Galleta con chispitas de colores | CM-023 | $13.00 | H | PROVISIONAL |
| Galleta multigrano | CM-038 | $15.00 | H | REAL |
| Pay de queso individual | CM-039 | $22.00 | H | SIN FOTO |
| Pan de muerto pequeño | CM-013 | $15.00 | H | PROVISIONAL |
| Pan de muerto grande | CM-040 | $22.00 | H | SIN FOTO |
| Muffin de nuez | CM-015 | $15.00 | H | PROVISIONAL |
| Muffin sencillo | CM-014 | $13.00 | H | PROVISIONAL |
| Vaso de café | CM-024 | $20.00 | H | PROVISIONAL |
| Leche Kirkland entera | CM-025 | $28.00 | H | PROVISIONAL |
| Leche Kirkland deslactosada light | CM-026 | $28.00 | H | PROVISIONAL |

**Altas:** Rosquita de ajonjolí (`14030080-0244-4806-8a04-6499bf96597c`), Bollo con ajonjolí (`5e9876c0-d589-4f75-91a0-709263e1a8d4`), Dona de chocolate negro con chispas (`aabb7fc6-23a8-4fcf-bb44-82f97a5dee5e`), Dona de chocolate blanco con coco (`b38153df-ee04-4c55-ad82-8ba8714fb97d`), Dona de chocolate con nuez (`7e9cb3f7-a40a-45ca-a91c-1df15b7291f9`), Dona combinada de chocolate negro y blanco (`71c324c4-40ff-4181-9079-10565a61f056`), Berlinesa rellena de crema pastelera (`a31694a4-d410-421b-89f0-de749193be64`), Berlinesa rellena de zarzamora (`ba082fc9-d81f-4a05-b7ba-88a8778abfff`), Galleta de nuez (`46496b9b-0da4-42df-a6bb-058a47333ec1`) y Galleta de coco (`7592364d-eaff-4222-b5b8-f8705413e097`). Las diez altas tienen `track_inventory=false`, sin fotografía y sin receta/vínculo.

**Editados con ProductId preservado:** Cuernito especial con almendra → Cuernito de almendra (bigote) (`9d2f58bc-5e76-44c3-9e67-8c46048dd696`); Bolillo mantequilla → Bolillo de mantequilla, categoría Pan Salado (`f9d8569d-765a-4152-bc61-36cc3f88ef24`); Baguette → Baguette con ajonjolí, SKU CM-029 (`4274e78f-3c15-49c9-977b-f474e9824e8e`); Dona de chocolate → Dona de chocolate negro (`a792f462-e335-4d8b-a215-37625c36777a`); Dona sencilla de chocolate blanco → Dona de chocolate blanco (`a26997fb-46df-4fe9-bd30-173ae115c675`); Polvorón → Polvorón tradicional (`ac1d3c85-b798-4087-90bf-a751bec30f42`); GALLETA MULTIGRANO → Galleta multigrano, categoría Galleta y SKU CM-038 (`f532a645-e89a-480d-93d7-d4f2cf3067b8`); Pay de queso → Pay de queso individual, SKU CM-039 (`0364ccd2-6c83-41bf-9a5d-477514dfa7a1`); Pan de muerto → Pan de muerto pequeño (`7119bef9-735a-4170-8f96-8d2962a1fc79`); Pan de Muerto Grande → Pan de muerto grande, SKU CM-040 (`a67359c8-e885-4764-8a97-6051cc985fe4`); Muffin → Muffin sencillo (`b8c445c4-6197-4d9b-be8b-1d7f07850bce`). Los precios heredados y flags de inventario existentes no cambiaron.

**Inactivos con identidad e historial preservados:** CM-018 Bollo relleno por la mitad de crema pastelera (`60ae6ccc-bd2e-47cb-aa51-f73d9fd68a05`), CM-009 Dona especial (`61dc450a-7887-41c7-b2f1-2fed64e40255`), CM-022 Galleta de nuez y coco (`4f0e16b4-2cb3-40e8-84fe-57094ce1cbe7`), CM-012 Puerquito (`b793c764-ee82-45bb-a041-fa01e477addd`) y CORONITA DE NUEZ (`40b671df-2449-446b-ad34-0f2fdf82403b`). CM-017 continúa activo a $15.00, CM-019 continúa activo a $16.00. La dona combinada tiene ID propio, distinto al de Dona especial.

## Integridad y pruebas

Comparación de digest por fila de las 44 tablas entre `destination-before.json` y `destination-after.json`: **únicamente `product` (31→41 filas) y `audit_entry` (141→167) cambiaron**. Permanecieron iguales `sale` (20), `sale_item` (46), pagos, sesiones y conteos de caja, recibos, inventario, movimientos, recetas, perfiles, vínculos producto-inventario, usuarios y todas las tablas de fotos. Los 56 archivos fotográficos conservaron su hash; `terminal.json` y `printing.json` también. En 36 activos: 2 fotos REAL, 21 PROVISIONAL y 13 SIN FOTO. No se fabricaron ni reasignaron fotos. `integrity_check=ok`, `foreign_key_check=0`, schema 17. No se ejecutó venta, cobro, impresión, producción ni movimiento de inventario.

`scripts/verify.ps1` pasó: **799/799** pruebas (584 generales y 215 WPF), ninguna fallida u omitida. TRX: `TestResults/foundation_net10.0_20261018211746.trx` y `TestResults/foundation_net10.0_20261018211520.trx`. Build Release de la solución y build aislado de Presentation: **0 errores, 0 warnings**. No se agregaron pruebas porque se usaron servicios existentes sin cambios de lógica; la verificación de datos compara identidades y todos los digests no objetivo.

## Revisión manual pendiente

EXE: `src/CasaMiga.Presentation/bin/Phase1K4G-C-B/CasaMiga.Presentation.exe`; DLL SHA-256 `91E042EAF47B9485E1E48414D8A770F81EC910F1A5B0B11C7221E51AEFB76096`; EXE SHA-256 `B5DCB7BCD186AB1FCBA4B11A21B4A09078D105542DBED05480938FF5B8027B29`. Launcher: `work/Abrir-Casa-Miga-1K4G-C-B.cmd`. `development.settings.json` junto al EXE apunta a `C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\work\phase1k4g-c-b-review-20261018`; el launcher limpia `CASAMIGA_DATA_DIR` localmente para asegurar esta ruta. Título esperado: `Casa Miga POS · Caja · DESARROLLO · 1K.4G-C-B`.

El usuario debe iniciar sesión y revisar: Catálogo (36 activos, precios y las diez variedades nuevas), filtro de inactivos (cinco históricos), categorías, fotografías existentes y placeholder de los trece activos sin foto; luego Punto de venta, búsqueda por nombre/SKU, tarjetas y navegación. No confirmar cobros ni imprimir. La accesibilidad visual real queda **pendiente de aprobación humana**; compilar y abrir el launcher no equivalen a esa aprobación.

**Apertura verificada:** el launcher inició el EXE identificado y la ventana principal mostró `Casa Miga POS · Caja · DESARROLLO · 1K.4G-C-B`. Se dejó abierta la primera instancia en acceso, sin iniciar sesión. Una segunda apertura de diagnóstico fue rechazada por `DirectoryLease` al detectar esa primera instancia; su diálogo de error se cerró normalmente, sin forzar el proceso principal. La instantánea read-only `destination-open.json` confirmó **cero cambios en las 44 tablas** respecto de `destination-after.json`, `integrity_check=ok`, 0 violaciones FK y 56 archivos fotográficos. Los tres hashes de origen/base protegida/fixture cerrada se verificaron de nuevo sin cambios. No hubo otra incidencia funcional detectada; la inspección humana de Catálogo/POS sigue pendiente.

Riesgos pendientes: 24 precios heredados requieren aprobación comercial; 21 fotografías provisionales y 13 faltantes entre activos requieren imágenes reales/validación; cuatro productos anteriores mantienen su flag `track_inventory=true` sin nuevos vínculos ni recetas (Baguette con ajonjolí, Galleta multigrano, Pay de queso individual y Pan de muerto grande). No activar consumo automático ni iniciar 1K.4G-C-C hasta autorización separada y revisión de estos datos.
