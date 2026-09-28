# CASA MIGA POS — REPORTE CORRECTIVO FASE 1K.2C-R1

Fecha: 28/09/2026

Estado: **implementación correctiva terminada; aprobación visual humana pendiente**.
FASE 1K permanece abierta. No se inicia Pedidos ni otra subfase.

## Control de baseline

- HEAD inicial: `37617a8ca41ed322fd2bf594a2d0d542974aee90`.
- HEAD final: commit que incorpora este reporte; el SHA exacto se entrega en la conversación para evitar autorreferencia.
- Git inicial: limpio.
- Schema: 16.
- Migration 016: sin cambios; SHA-256 `9223CAA06CD02FA32E874CC1516EE0C6DB798CB76E2FBE1BF267BAB9B695ED76`.
- Domain/Application/Infrastructure, backup/restore, inventario, recetas y producción: sin cambios por esta corrección visual, salvo uso de contratos públicos existentes desde Presentation/tests.

## Resultado de la corrección

El smoke ya no muestra 22 fallbacks. Se revisaron los dos mosaicos complementarios y las referencias individuales autorizadas. La herramienta controlada produce 26 imágenes 512×512 mediante crop y escalado técnico de alta calidad, conserva proporción, rellena con Fondo Natural y luego importa cada derivado como `PROVISIONAL` por ProductPhotoService. No usa IA, Internet, stock ni cambia toppings o forma del producto.

Resultado verificado del dataset aislado:

- Products: 26.
- Categorías: 4.
- PROVISIONAL: 26.
- REAL: 0.
- SIN FOTO: 0.
- Usuario smoke: `catalog-admin`.
- Dataset: `work/phase1k2c-r1-smoke-20260928`.
- Assets: `work/phase1k2c-r1-prepared-assets`.

## Tabla de 26 Products

| Id | Product | Precio | Categoría | Fuente | Crop X,Y,W,H | Asset | Estado |
|---:|---|---:|---|---|---|---|---|
| 1 | Concha de vainilla | $13.00 | Pan dulce | `referencia_visual_catalogo.png` | 12,152,218,200 | `01.png` 512×512 | PROVISIONAL |
| 2 | Concha de chocolate | $13.00 | Pan dulce | `referencia_visual_catalogo.png` | 245,152,232,200 | `02.png` 512×512 | PROVISIONAL |
| 3 | Bolillo mantequilla | $8.00 | Pan dulce | `referencia_visual_catalogo.png` | 498,152,288,196 | `03.png` 512×512 | PROVISIONAL |
| 4 | Cuernito de mantequilla | $13.00 | Pan dulce | `referencia_visual_catalogo.png` | 806,152,281,203 | `04.png` 512×512 | PROVISIONAL |
| 5 | Cuernito especial con almendra | $15.00 | Pan dulce | `referencia_visual_catalogo.png` | 1106,152,255,205 | `05.png` 512×512 | PROVISIONAL |
| 6 | Rol de canela | $15.00 | Pan dulce | `referencia_visual_catalogo.png` | 11,480,220,199 | `06.png` 512×512 | PROVISIONAL |
| 7 | Dona de chocolate | $13.00 | Dona | `referencia_visual_catalogo.png` | 251,480,206,196 | `07.png` 512×512 | PROVISIONAL |
| 8 | Dona de azúcar | $13.00 | Dona | `referencia_visual_catalogo.png` | 477,480,201,196 | `08.png` 512×512 | PROVISIONAL |
| 9 | Dona especial | $15.00 | Dona | `referencia_visual_catalogo.png` | 702,481,200,195 | `09.png` 512×512 | PROVISIONAL |
| 10 | Polvorón | $13.00 | Pan dulce | `referencia_visual_catalogo.png` | 921,481,209,196 | `10.png` 512×512 | PROVISIONAL |
| 11 | Polvorón de nuez | $15.00 | Pan dulce | `referencia_visual_catalogo.png` | 1148,480,212,197 | `11.png` 512×512 | PROVISIONAL |
| 12 | Puerquito | $13.00 | Pan dulce | `referencia_visual_catalogo.png` | 12,790,209,201 | `12.png` 512×512 | PROVISIONAL |
| 13 | Pan de muerto | $15.00 | Pan dulce | `referencia_visual_catalogo.png` | 239,790,210,201 | `13.png` 512×512 | PROVISIONAL |
| 14 | Muffin | $13.00 | Pan dulce | `referencia_visual_catalogo.png` | 469,790,208,199 | `14.png` 512×512 | PROVISIONAL |
| 15 | Muffin de nuez | $15.00 | Pan dulce | `referencia_visual_catalogo.png` | 698,790,204,199 | `15.png` 512×512 | PROVISIONAL |
| 16 | Danés con crema pastelera | $15.00 | Pan dulce | `referencia_visual_catalogo.png` | 923,790,211,201 | `16.png` 512×512 | PROVISIONAL |
| 17 | Bollo relleno de crema pastelera | $15.00 | Pan dulce | `referencia_visual_catalogo.png` | 1150,790,210,201 | `17.png` 512×512 | PROVISIONAL |
| 18 | Bollo relleno por la mitad de crema pastelera | $16.00 | Pan dulce | `referencia_visual_catalogo_con_bebidas.png` | 204,686,180,145 | `18.png` 512×512 | PROVISIONAL |
| 19 | Bollo relleno de zarzamora | $16.00 | Pan dulce | `referencia_visual_catalogo_con_bebidas.png` | 395,686,178,145 | `19.png` 512×512 | PROVISIONAL |
| 20 | Dona glaseada | $13.00 | Dona | `referencia_visual_catalogo_con_bebidas.png` | 963,95,178,166 | `20.png` 512×512 | PROVISIONAL |
| 21 | Dona sencilla de chocolate blanco | $13.00 | Dona | `referencia_visual_catalogo_con_bebidas.png` | 1341,95,181,166 | `21.png` 512×512 | PROVISIONAL |
| 22 | Galleta de nuez y coco | $15.00 | Galleta | `referencia_visual_catalogo_con_bebidas.png` | 584,686,178,145 | `22.png` 512×512 | PROVISIONAL |
| 23 | Galleta con chispitas de colores | $13.00 | Galleta | `referencia_visual_catalogo_con_bebidas.png` | 773,686,180,145 | `23.png` 512×512 | PROVISIONAL |
| 24 | Vaso de café | $20.00 | Bebida | `imagen_vaso_cafe_referencia.png` | 165,105,460,460 | `24.png` 512×512 | PROVISIONAL |
| 25 | Leche Kirkland entera | $28.00 | Bebida | `imagen_leche_kirkland_entera_referencia.png` | 190,110,430,430 | `25.png` 512×512 | PROVISIONAL |
| 26 | Leche Kirkland deslactosada light | $28.00 | Bebida | `imagen_leche_kirkland_deslactosada_light_referencia.png` | 190,110,430,430 | `26.png` 512×512 | PROVISIONAL |

El mapa reproducible completo está en `phase1k2c-provisional-map.md` y `evidence/phase1k2c-r1-mapping.json`. La hoja de contacto revisada está en `evidence/phase1k2c-r1-contact-sheet.png`. Los 26 derivados muestran únicamente el producto, sin nombre, precio o marco del mosaico. Concha vainilla/chocolate usan referencias diferenciadas; café y leches usan imágenes individuales. Para Danés se eligió el primer mosaico porque ofrece una representación aislada superior a la referencia individual disponible.

## POS y layout de cajero

- Header permanente corregido a **Punto de venta**; los textos de preparación sólo corresponden al arranque.
- Sidebar POS: rail compacto de 84 DIP con botón de expansión; el shell administrativo conserva 248 DIP y toda su navegación.
- Contenido POS con margen reducido; catálogo/carrito usan proporción aproximada 70/30 con mínimos para 960 DIP.
- ProductCard: 184×276, fotografía 160 DIP como elemento dominante y filas reservadas de 48/32 DIP para nombre/precio; se quitó la categoría repetida de cada card. Esta distribución corrige el recorte observado en nombres de dos líneas y precios.
- Búsqueda compacta por nombre/SKU y categorías rápidas de DB.
- El scroll vuelve arriba al entrar al POS, buscar, cambiar categoría o página; evita reabrir el catálogo en una fila parcial.
- Carrito visible con cantidad, `+`, `−`, eliminar, subtotal y total.

Después de la primera revisión humana se corrigió el rail contraído: los encabezados de grupo, etiquetas y versión se ocultan; cada icono se centra en todo el ancho, el scrollbar lateral desaparece y el nombre completo sigue disponible como tooltip. Expandir recupera el menú administrativo completo.

## Cobro, impresión e Historial

El checkout sigue siendo uno. El diálogo expone dos acciones:

- **Cobrar**: prepara y confirma Sale/Items/Payment; no llama impresión; muestra que la venta quedó en Historial.
- **Cobrar + imprimir ticket**: ejecuta el mismo prepare/confirm; sólo después del commit llama ReceiptService.

Ambas acciones comparten `CheckoutViewModel.Busy`, por lo que iniciar una deshabilita las dos. Si la impresión falla o queda incierta, la venta no se revierte ni se confirma otra vez; el operador recibe: “Venta cobrada correctamente. No fue posible imprimir el ticket. Puedes reimprimirlo desde Historial.” La recuperación de un intento antiguo no imprime automáticamente si no existe intención de impresión vigente.

Pruebas nuevas verifican cobrar sin transporte, cobrar e imprimir, fallo de impresión con Sale persistida y doble envío compartido. Ambos caminos dejan una fila Sale, que es la autoridad de Historial.

## Administración de imagen

Catálogo → Editar producto muestra **IMAGEN DEL PRODUCTO**, preview 240×240, estado Sin foto/Foto provisional/Foto real y acciones directas **Agregar foto** o **Cambiar foto**, más **Quitar foto**. Quitar conserva Product y vuelve al fallback. `products.edit` continúa gobernando todas las mutaciones; Cashier puede ver las cards del POS pero no administrar fotos.

El crop es visual: cuadro superpuesto arrastrable y tirador inferior derecho redimensionable. X/Y/Tamaño quedan dentro de “Ajuste avanzado por coordenadas”. `PhotoCropViewModel` aplica mínimo 256, máximo del bitmap y límites al mover/redimensionar. Replace/Remove invalida la caché compartida y la navegación de regreso recarga la card sin reiniciar la aplicación.

## Pruebas y build

Ejecuciones previas a la verificación final:

- `CasaMiga.Tests`: 535/535, 0 fallidas, 0 omitidas.
- `CasaMiga.PhotoTests`: 103/103, 0 fallidas, 0 omitidas.
- Total: **638/638**.
- Build Release: 0 errores, 0 advertencias.
- Tool prepare: 26/26 PROVISIONAL.
- Tool verify: 26 Products, 4 categorías, 26 Photos, 26 PROVISIONAL, 0 REAL, 0 SIN FOTO.

La evidencia definitiva de `scripts/verify.ps1`, Release y `git diff --check` se registra en `docs/evidence/phase1k2c-r1-verification.txt`.

## Smoke visual

La compilación Release está abierta contra el dataset aislado. Para revisión:

- Usuario: `catalog-admin`
- Contraseña: `Phase1K2C-R1!2026`

Lista de aceptación pendiente: fotografías, nombres, precios, categorías, tamaño/número de columnas, rail compacto/expandible, header, carrito, agregar producto, Cobrar, Cobrar + imprimir, Catálogo administrativo, Agregar/Cambiar/Quitar y drag/resize del crop. La hoja de contacto constituye evidencia del control visual de los 26 assets; las capturas finales de UI y la aprobación pertenecen a la revisión humana.

**No se declara 1K.2C aprobada visualmente.** Esa declaración requiere confirmación explícita del usuario.

## Pendientes

1. Revisión humana del smoke y correcciones que resulten.
2. Sustituir gradualmente las 26 imágenes PROVISIONAL por fotografías REAL de Casa Miga.
3. Validar rendimiento/calidad en el equipo Windows objetivo.
4. Cierre formal posterior de FASE 1K, sólo con autorización separada.

## Pedido/Tienda documentado, no implementado

`NEXT_STEPS.md` reserva una subfase con modelo Astra para persistir `Tipo de venta`: Mostrador, Pedido o Pedido a tienda; referencia de cliente/tienda/persona y nota opcional. Debe formar parte de Sale y sobrevivir Historial, backup/restore, ticket y reimpresión. No se agregó texto temporal, columna, migration ni lógica de pedido en R1.
