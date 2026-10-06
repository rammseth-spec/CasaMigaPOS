# CASA MIGA POS — FASE 1K.4A

**Estado:** técnicamente completada; smoke manual pendiente. Este checkpoint no cierra FASE 1K ni autoriza 1K.4B.

1. **Modelo realmente utilizado:** GPT-6.
2. **HEAD inicial:** `bdb068e2749b10807613ac9a9eb49214f07fce0f`.
3. **Git inicial:** limpio, rama `master`; `origin/QA` coincidía con el baseline. No se hizo push.
4. **Auditoría POS:** la búsqueda sólo se aplicaba mediante botón/Enter; Esc seleccionaba el campo pero no limpiaba; no había estado vacío ni acción × Limpiar. Las tarjetas ya reservaban 160 DIP para imagen, 48 para nombre y 32 para precio.
5. **Auditoría Catálogo:** búsqueda, categoría y Mostrar inactivos dependían de «Buscar / aplicar»; los refresh descartaban siempre la selección; no existía creación de categoría dentro del editor de producto; el editor reiniciaba el origen visible de una foto existente a REAL.
6. **Causa del bug de filtro administrativo:** no existían debounce, cancelación ni versionado de consultas; un cambio rápido podía terminar con una consulta anterior aplicada tarde. Además, reemplazar filtros y limpiar `SelectedProduct` en cada refresh perdía selección aunque el elemento continuara visible.
7. **Solución:** snapshot de consulta, cancelación y revisión monotónica, debounce, restauración de IDs todavía válidos y preservación de producto seleccionado cuando permanece en el resultado.
8. **Búsqueda en vivo:** POS y Catálogo actualizan resultados al escribir; botón y Enter conservan aplicación inmediata explícita.
9. **Debounce real:** 250 ms.
10. **Nombre:** cubierto en POS y Catálogo.
11. **SKU:** cubierto en POS y Catálogo.
12. **× Limpiar:** visible sólo con texto; limpia, refresca y devuelve foco al buscador.
13. **Esc:** con foco en el buscador y texto, limpia sin navegar, salir ni cambiar fullscreen. Popup/editor conservan prioridad; con cuadro limpio la ruta global queda disponible.
14. **Categoría preservada:** buscar y limpiar mantienen la categoría elegida; cambiar categoría actualiza dentro del nuevo contexto.
15. **Sin resultados:** POS muestra «No encontramos productos con esa búsqueda.»; Catálogo distingue búsqueda vacía, categoría sin productos y catálogo vacío.
16. **Tarjetas POS:** se conserva foto dominante y altura estable de imagen/nombre/precio; no se cambió carrito.
17. **Nombres largos:** Wrap con máximo visual de dos líneas (44 DIP) y ellipsis sólo después de ese espacio reservado.
18. **Precio:** mantiene su fila de 32 DIP y permanece materializado aun con nombre largo.
19. **Fotos:** ownership Product, caché, crop, archivos y persistencia de 1K.2 permanecen intactos.
20. **Placeholder:** texto humano «Sin imagen», icono existente y sin rutas técnicas.
21. **Origen de la imagen:** etiqueta visible unificada; conserva códigos internos REAL/PROVISIONAL. Editar una foto provisional ya no la presenta silenciosamente como real.
22. **Crear producto:** conserva validaciones existentes y selecciona la fila creada cuando sigue visible.
23. **Editar producto:** conserva categoría, precio, SKU, descripción, control de inventario y origen visual de foto.
24. **Categoría existente:** selector existente conservado.
25. **Nueva categoría dentro del flujo:** `+ Nueva categoría` abre un editor inline; al crear, conserva el borrador y selecciona la categoría resultante. La unicidad sigue en `CatalogService`/Domain.
26. **Pan salado:** no existe en la base protegida. Se creó exclusivamente en `work/phase1k4a-smoke-20261006`, activa, ID `13117b16-b5da-4618-adbc-330a7a10b782`.
27. **Bolillo:** en la base protegida existe `CM-003 · Bolillo mantequilla`, ID `f9d8569d-765a-4152-bc61-36cc3f88ef24`, en la categoría original. Sólo la copia aislada lo vincula a Pan salado.
28. **Precio local de Bolillo:** ya era 800 centavos / $8 MXN; se conservó.
29. **Precio de tienda:** no se implementó ninguna regla de $7, canal o tienda.
30. **1280×820:** prueba WPF de cards, nombre largo, precio y textos de imagen superada.
31. **1920×1080:** la misma prueba WPF fue superada.
32. **F11:** no se cambió el shell; regresiones de teclado/Popup y ContextHelp superadas. Esc del buscador sólo consume la tecla cuando limpia texto.
33. **Archivos modificados:** seis archivos de Presentation, `PosCatalogUsabilityTests.cs` y los tres documentos exigidos. Ningún archivo de capas protegidas o migraciones.
34. **Pruebas focales:** 13 casos combinados de 1K.4A, Popup/Esc y ContextHelp, más la regresión exacta de Producción 1K.3D; incluyen 6 casos nuevos/variantes 1K.4A.
35. **Resultado focal:** 14/14, 0 fallidas, 0 omitidas.
36. **Suite completa:** `scripts/verify.ps1` con build identificado `CasaMigaReviewBuild=1K.4A`.
37. **Resultado suite:** 725/725 = 560 generales + 165 WPF/Presentation; 0 fallidas, 0 omitidas.
38. **Release:** 0 errores, 0 advertencias; salida separada `src/CasaMiga.Presentation/bin/Phase1K4A/`.
39. **git diff --check:** correcto.
40. **Hash base protegida inicial:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
41. **Hash final:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
42. **Igualdad:** byte-idéntica; la fixture se creó como copia independiente.
43. **Schema:** 16.
44. **Migración 017:** no existe y no se creó.
45. **Domain:** sin cambios.
46. **Application:** sin cambios.
47. **Infrastructure:** sin cambios.
48. **Ventas:** no se confirmó ninguna; conteo de la copia antes/después 16/16.
49. **Caja:** no se abrió/cerró ninguna sesión; conteo 2/2 y sin auditorías nuevas de caja en pruebas focales.
50. **Impresión:** no se envió ningún trabajo; requests de la copia 17/17.
51. **Producción:** no se registró producción; conteo 0/0.
52. **EXE:** `src/CasaMiga.Presentation/bin/Phase1K4A/CasaMiga.Presentation.exe`.
53. **Launcher:** `work/Abrir-Casa-Miga-1K4A.cmd`.
54. **DataDirectory:** `work/phase1k4a-smoke-20261006`.
55. **Título:** `Casa Miga POS · Caja · DESARROLLO · 1K.4A`.
56. **Launcher → EXE:** el launcher limpia `CASAMIGA_DATA_DIR`, usa el runtime local y apunta sólo al EXE Phase1K4A; `development.settings.json` fija la copia indicada.
57. **Apertura automática:** se realiza después del commit y de comprobar lease/rutas.
58. **App abierta en login:** se confirma en el reporte final de conversación tras observar proceso, ruta y título, sin autenticar.
59. **Acceso anclado:** no se usa.
60. **Commit:** único mensaje autorizado `feat: improve pos and catalog usability for phase 1K.4A`.
61. **HEAD final:** se reporta después de crear el checkpoint único, en la conversación.
62. **Git final:** debe quedar limpio; se reporta después del commit.
63. **Riesgos/pendientes:** smoke humano; Pan salado y el vínculo real del bolillo siguen pendientes en la base protegida porque alterar datos comerciales estaba prohibido. La fixture contiene 28 fotos copiadas (25 PROVISIONAL, 3 REAL) y un producto sintético sin imagen. No sustituye fotos reales Casa Miga.
64. **Checklist manual:** login; POS; categoría Pan salado; búsqueda por nombre y SKU; comprobar debounce; × Limpiar y categoría; sin resultados; Esc; nombre largo/precio/placeholder; Catálogo; filtros rápidos y Mostrar inactivos; crear/editar producto sintético; categoría existente y categoría inline; Origen de la imagen; cancelar cambios no deseados; F11; salir. No vender ni imprimir.
65. **Límites confirmados:** no 1K.4B, Historial, Caja, ticket, Pedidos/Tiendas, migración 017 ni push.

## Datos para revisión

Las credenciales verificadas corresponden únicamente a la copia aislada y se entregan en la conversación, no en documentación versionada.
