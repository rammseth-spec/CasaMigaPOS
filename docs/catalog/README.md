# Catálogo autorizado v2 (datos de revisión)

`catalogo-autorizado-v2.csv` reproduce, fila por fila, la tabla de [phase1k2c-provisional-map.md](../phase1k2c-provisional-map.md): 26 productos con nombre, precio y categoría del catálogo autorizado v2. La prueba `AuthorizedCatalogTests` impide que ambos difieran. Los mosaicos de fotos originales nunca se versionaron.

**Agregados por el usuario (12/10/2026):** 27 Pan de muerto grande ($22, «Pan dulce»), 28 Pay de queso ($22) y 29 Pay de elote ($22), estos dos en la categoría nueva «Pays» (la quinta, al final), y 30 Bisquet ($13, «Pan dulce»). **15/10/2026:** 31 Baguette ($9) en la categoría nueva «Pan salado» (la sexta, al final), como en la copia de revisión de 1K.4G. **19/10/2026:** 32–42, las 11 altas del catálogo comercial aprobado en QA (1K.4G-C-B): Rosquita de ajonjolí y Bollo con ajonjolí ($13, «Pan dulce»); Dona de chocolate negro con chispas, Dona de chocolate blanco con coco, Dona de chocolate con nuez, Dona combinada de chocolate negro y blanco y Berlinesa rellena de crema pastelera ($15, «Dona»), Berlinesa rellena de zarzamora ($16, «Dona»); Galleta de nuez, Galleta de coco y Galleta multigrano ($15, «Galleta»). Sus SKU siguen el id de esta tabla (CM-032 … CM-042), no los de la copia de revisión de QA. 43 Pan de muerto chico de masa madre ($18) y 44 Pan de muerto grande de masa madre ($25), en «Pan dulce»; sus fotos muestran la miga abierta para que se note la masa madre. **05/11/2026:** 45 Lima ($13), 46 Submarino relleno ($15) y 47 Campechana ($15), en «Pan dulce»; la 34 pasa a «Dona de chocolate negro con chispas de colores» con foto nueva (sus chispas son de colores, no de chocolate). **25/11/2026:** 48 Taco de piña, 49 Bigote de chocolate y 50 Quesadilla ($15, «Pan dulce»), 51 Bolillo integral ($9) y 52 Baguette integral ($10), en «Pan salado»; sus fotos (`gpt_image_2_6`) siguen el mismo estilo. 53–61: los 9 panes que el dueño dio de alta a mano en la caja (Baguette de ajonjolí y ajo, Concha de muerto, Orejita, Pan de muerto escolar, Pan de muerto C, Pan de muerto especial de chocolate, Pan de muerto relleno y Telera de ajonjolí chica y grande), con su precio de la caja, foto de IA y, en `renombres.json`, su nombre anterior para que la caja los renombre en vez de duplicarlos. Sus fotos provisionales `CM-027.jpg` … `CM-061.jpg` se generaron igual que las otras 26. `AuthorizedCatalogTests` compara los 26 primeros con la tabla v2, fija estos treinta y cinco y comprueba que cada producto tenga su foto registrada en el manifiesto con el SHA-256 correcto.

## Fotos provisionales generadas con IA

`assets/catalog-provisional-ia/CM-001.jpg` … `CM-052.jpg` (768 × 768, ~5.4 MB en total) son ilustraciones generadas con IA a partir del nombre de cada producto, para que la caja se vea completa durante la revisión. **No son fotos de los productos reales**: entran con procedencia `PROVISIONAL` y deben reemplazarse por fotos reales antes de producción. `manifest.json` registra SKU, modelo, identificador de generación y SHA-256 de cada archivo.

## Preparar una copia de revisión

Siempre en una carpeta **nueva** (nunca sobre una instalación existente):

```powershell
dotnet run --project tools/CasaMiga.Phase1K2CSmoke -- seed-review docs/catalog/catalogo-autorizado-v2.csv C:\ruta\nueva\vacia --photos assets\catalog-provisional-ia --demo-days 7
```

- Siempre crea `admin` (contraseña generada) y `cajero1` (contraseña temporal, debe cambiarla al entrar), 6 categorías y 61 productos auditados.
- `--photos <carpeta>`: importa `CM-NNN.jpg` por el servicio normal de fotos, con procedencia `PROVISIONAL`.
- `--demo-days N` (1–60): ventas de prueba de los últimos N días hechas con los servicios reales (turnos de caja por día, efectivo/tarjeta/transferencia, una cancelación y devoluciones, cortes con algún faltante/sobrante). Crea los cajeros de prueba `ana` y `luis` y, para Charolas, precios de charola (80 % del de mostrador, sin bebidas), 3 tiendas con su charola habitual, entregas diarias, recolecciones con piezas que regresan para Piedra y cobros (una tienda paga completo, otra a veces deja saldo y otra paga cada semana); la charola de ayer queda en la tienda. Es determinista: la misma N produce los mismos datos.

Las credenciales se imprimen una sola vez; no guardarlas en documentos versionados. Herramienta de desarrollo: no se instala en las cajas. Desde Claude Code desktop usar una carpeta fuera de AppData (ver docs/review-builds.md). Para entrar sin escribir credenciales en una copia de desarrollo, crear `dev-autologin.json` en la carpeta de datos.
