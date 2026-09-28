# Mapa de fotografías — catálogo autorizado v2 · corrección R1

Autoridad comercial: `catalogo_casa_miga.json` y `catalogo_casa_miga.csv`, validados como equivalentes. Autoridad visual provisional: ambos mosaicos y tres referencias individuales del paquete autorizado. Ninguna imagen aporta nombre, precio o categoría. Todos los derivados son **PROVISIONAL**.

Flujo técnico: fuente autorizada → crop fotográfico sin texto/precio/marco → resize de alta calidad conservando proporción → lienzo 512×512 Fondo Natural → importación por ProductPhotoService con crop 0,0,512. ProductPhoto Foundation y su mínimo de 256 px permanecen intactos.

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

**Totales verificados:** 26 Products, 26 PROVISIONAL, 0 REAL, 0 SIN FOTO.

Control visual: se revisó una hoja de contacto de los 26 derivados. Los crops finales excluyen nombre, precio, marco de tarjeta y fragmentos visibles de tarjetas vecinas; no hay deformación y las conchas de vainilla/chocolate usan imágenes diferentes. Café y leches usan referencias individuales. El danés usa el primer mosaico porque su región representa mejor el producto aislado que la referencia individual disponible.

El dataset se encuentra bajo `work/phase1k2c-r1-smoke-20260928` y los derivados bajo `work/phase1k2c-r1-prepared-assets`; ambos son datos ignorados y aislados. La hoja de contacto portable está en `docs/evidence/phase1k2c-r1-contact-sheet.png`. La instalación comercial no fue modificada.
