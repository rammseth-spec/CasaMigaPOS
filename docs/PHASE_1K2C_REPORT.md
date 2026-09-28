# CASA MIGA POS — REPORTE FINAL FASE 1K.2C

Fecha: 28/09/2026. Documento autocontenido. La implementación se confirma por el commit de producto indicado en el cierre; el SHA exacto del commit documental final se entrega en la conversación para evitar una referencia circular.

## Estado y Git

1. **FASE 1K:** permanece ABIERTA hasta autorización de cierre formal separada.
2. **FASE 1K.2:** objetivo funcional de catálogo fotográfico completado; no se inicia trabajo posterior.
3. **FASE 1K.2C:** COMPLETADA Y APROBADA VISUALMENTE; el usuario solicitó el reporte final después de la revisión preparada.
4. **Modelo utilizado:** Sol.
5. **HEAD inicial:** `95e9d9c12c44c3d956bfd11fc00ce8a5bf3615a9`.
6. **HEAD final:** el SHA exacto se entrega en la conversación tras crear el commit documental.
7. **Commits:** un commit de producto y, si aplica, un commit documental de cierre; ambos SHA se entregan en la conversación.
8. **Git final:** limpio, sin push y sin remoto agregado; identidad repo-local Rammseth / rammstudioworkshop@gmail.com.
9. **Schema:** 16.
10. **Migration 016:** SHA-256 `9223CAA06CD02FA32E874CC1516EE0C6DB798CB76E2FBE1BF267BAB9B695ED76`; 001–015 y 016 sin cambios.

## POS fotográfico

11. **ProductCard:** componente reutilizable WPF de 194 × 292 DIP; foto 1:1 de 168 DIP, nombre, categoría y precio.
12. **Grid:** WrapPanel paginado; no se declara virtualizado. Mantiene catálogo y carrito en la misma experiencia.
13. **Foto principal:** usa únicamente el thumbnail JPEG generado por 1K.2B, con `UniformToFill`; nunca carga el original en el grid.
14. **Fallback:** placeholder gráfico neutral, sin logo ni foto genérica de pan; conserva nombre y precio.
15. **PROVISIONAL:** visible como fotografía en POS sin mostrar la procedencia al cajero.
16. **REAL:** soportada por el mismo pipeline; el dataset no recibió una foto real autorizada.
17. **Caché:** LRU acotada por clave `PhotoId:ThumbnailSha256`; hit reutiliza el mismo BitmapSource.
18. **Memoria:** presupuesto de 64 MiB de píxeles (`ancho × alto × 4`), con expulsión LRU.
19. **Async loading:** máximo cuatro lecturas concurrentes fuera del dispatcher; BitmapImage `OnLoad`, frozen y stream cerrado.
20. **Categorías:** provienen de Category; “Todos” y selección actual son visibles, con Hoja de Aguacate como acento.
21. **Search:** conserva nombre/SKU y convive con categoría.
22. **Pagination:** conserva páginas de 50, con Anterior/Siguiente.
23. **Card click:** toda la tarjeta ejecuta `ProductClick → AddAsync → PosService.Add`.
24. **Keyboard:** Button permite Enter/Space y foco visible; también funciona con mouse y touch.
25. **Cart integration:** carrito, cantidades, total, clear y checkout vigentes; no existe carrito alternativo.
26. **Price source:** siempre `Product.SalePrice`; la imagen, filename y metadata no aportan precio.
27. **Product inactive:** la card expone `IsActive` y queda deshabilitada/atenuada; el POS vigente consulta activos.

## Administración de fotografías

28. **Admin photo UI:** editor de Product muestra preview, estado y acciones Agregar/cambiar/Quitar.
29. **Crop UI:** diálogo cuadrado con preview, X, Y y tamaño; mínimo 256 px; original intacto.
30. **Add photo:** selector JPEG/PNG → crop → `ProductPhotoService.Import`.
31. **Replace photo:** expected revision + OperationId; invalida caché y actualiza card sin cambiar ProductId ni datos comerciales.
32. **Remove photo:** confirmación → `ProductPhotoService.Remove`; vuelve a fallback sin borrar Product.
33. **Permissions:** Presentation usa la matriz vigente y Application revalida `products.edit`.
34. **Administrator:** consulta y gestiona fotos con sus permisos existentes.
35. **Manager:** conserva exactamente su comportamiento según `products.edit`; no se amplían roles.
36. **Cashier:** puede ver thumbnails en POS y vender productos permitidos; no puede cambiar/quitar fotos.

## Catálogo controlado

37. **Catálogo estructurado:** `catalogo_casa_miga.json` y `catalogo_casa_miga.csv` del paquete autorizado v2.
38. **CSV/JSON:** comparación exacta de las 26 filas antes de crear datos; discrepancia detiene la utilidad.
39. **Products de smoke:** 26 Products y 4 categorías creados por CatalogService en carpeta nueva.
40. **Products reales importados:** ninguno; la instalación comercial/predeterminada no se abrió ni pobló.
41. **Mapa provisional:** `docs/phase1k2c-provisional-map.md`, con Product, fuente, crop y estado.
42. **PROVISIONAL:** 4.
43. **REAL:** 0, porque no se recibió una fotografía inequívocamente autorizada como REAL.
44. **SIN FOTO:** 22.
45. **Danés:** usa la referencia individual, crop X19/Y320/lado700, PROVISIONAL.
46. **Bebidas:** café y dos leches usan referencias individuales con crops documentados, PROVISIONAL.

## Responsive, smoke y pruebas

47. **Responsive:** catálogo y carrito siguen operables en los tres anchos; se conserva scroll.
48. **Resoluciones:** 960×820 (1 card), 1280×820 (2 cards), 1920×1080 (5 cards), según harness WPF.
49. **Fullscreen regression:** se conserva el comportamiento 1K.1; no se reescribió ni endureció.
50. **Smoke visual:** dataset aislado preparado y abierto; el usuario solicitó el reporte final tras la revisión. No se realizaron operaciones comerciales reales.
51. **Focal tests:** ProductPhotoUiTests 12/12; Phase1K1ShellTests 2/2.
52. **Suite completa:** 633/633, compuesta por CasaMiga.Tests 535/535 y CasaMiga.PhotoTests 98/98; 0 fallidas, 0 omitidas.
53. **Release:** 0 errores, 0 advertencias.
54. **git diff --check:** correcto.
55. **POS regression:** cubierta por suite general, shell XAML y flujo Add vigente.
56. **Catalog regression:** cubierta por suite general y validación estructural del editor.
57. **Product Photo regression:** 98/98 incluye foundation y las 12 pruebas UI nuevas.
58. **Backup regression:** suite completa correcta; backup v2 aislado de 2,583,245 bytes con media activa.
59. **Restore regression:** copia aislada validada con 26 Products, 4 categorías y 4 fotos PROVISIONAL.
60. **Inventory regression:** incluida en CasaMiga.Tests 535/535.
61. **Sales regression:** incluida en CasaMiga.Tests 535/535.
62. **Cash regression:** incluida; dataset abre caja sintética en $0 exclusivamente para POS.
63. **Printing regression:** incluida en CasaMiga.Tests 535/535; no se imprimió físicamente.
64. **Recipe regression:** incluida en CasaMiga.Tests 535/535.
65. **Production regression:** incluida en CasaMiga.Tests 535/535.

## Hallazgos, correcciones y límites

66. **Problemas visuales encontrados:** mosaicos con regiones limpias menores a 256 px o riesgo de texto/producto vecino; por ello no se asignaron 22 imágenes dudosas. Se ajustó el acceso previo para eliminar espacio excesivo y preservar scroll.
67. **Correcciones:** cards profesionales, fallback, categorías seleccionadas, carga diferida, caché, preview/procedencia administrativa, crop, Replace/Remove, login centrado y feedback de creación de administrador.
68. **Riesgos:** no existe certificación de rendimiento en el equipo Windows 10/LTSC final; WrapPanel no virtualiza; calidad final depende de fotos reales; un archivo ausente/corrupto muestra fallback y requiere Replace/Remove/Repair autorizado.
69. **Fotos reales pendientes:** las 26: reemplazar los cuatro PROVISIONAL por REAL y proporcionar REAL para los 22 SIN FOTO; detalle por Product en el mapa.
70. **1K.2B estructural:** sin cambios a migration 016, schema, backup v2, restore, storage root, provenance, hashes, replace/recovery, orphan cleanup, permisos o atomicidad.
71. **Siguiente subfase:** NO iniciada. Instalador NO iniciado. Instalación comercial NO poblada.
72. **Próximo paso recomendado:** autorizar por separado el cierre formal de FASE 1K; después, si se desea, planificar sustitución gradual de fotos reales sin reconstruir Products.
73. **Modelo recomendado:** Sol para cierre documental y sustitución UI acotada; Astra sólo ante un defecto estructural demostrado.

Evidencia: `docs/evidence/phase1k2c-verification.txt`, `docs/evidence/phase1k2c-migration-hashes.txt`, `docs/product-photo-ui.md` y `docs/phase1k2c-provisional-map.md`.
