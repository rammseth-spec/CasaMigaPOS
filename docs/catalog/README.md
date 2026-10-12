# Catálogo autorizado v2 (datos de revisión)

`catalogo-autorizado-v2.csv` reproduce, fila por fila, la tabla de [phase1k2c-provisional-map.md](../phase1k2c-provisional-map.md): 26 productos con nombre, precio y categoría del catálogo autorizado v2. La prueba `AuthorizedCatalogTests` impide que ambos difieran. Los mosaicos de fotos originales nunca se versionaron.

**Agregados por el usuario (12/10/2026):** 27 Pan de muerto grande ($22, «Pan dulce»), 28 Pay de queso ($22) y 29 Pay de elote ($22), estos dos en la categoría nueva «Pays» (la quinta, al final). Sus fotos provisionales `CM-027.jpg` … `CM-029.jpg` se generaron igual que las otras 26. `AuthorizedCatalogTests` compara los 26 primeros con la tabla v2, fija estos tres y comprueba que cada producto tenga su foto registrada en el manifiesto con el SHA-256 correcto.

## Fotos provisionales generadas con IA

`assets/catalog-provisional-ia/CM-001.jpg` … `CM-029.jpg` (768 × 768, ~2.3 MB en total) son ilustraciones generadas con IA a partir del nombre de cada producto, para que la caja se vea completa durante la revisión. **No son fotos de los productos reales**: entran con procedencia `PROVISIONAL` y deben reemplazarse por fotos reales antes de producción. `manifest.json` registra SKU, modelo, identificador de generación y SHA-256 de cada archivo.

## Preparar una copia de revisión

Siempre en una carpeta **nueva** (nunca sobre una instalación existente):

```powershell
dotnet run --project tools/CasaMiga.Phase1K2CSmoke -- seed-review docs/catalog/catalogo-autorizado-v2.csv C:\ruta\nueva\vacia --photos assets\catalog-provisional-ia --demo-days 7
```

- Siempre crea `admin` (contraseña generada) y `cajero1` (contraseña temporal, debe cambiarla al entrar), 5 categorías y 29 productos auditados.
- `--photos <carpeta>`: importa `CM-NNN.jpg` por el servicio normal de fotos, con procedencia `PROVISIONAL`.
- `--demo-days N` (1–60): ventas de prueba de los últimos N días hechas con los servicios reales (turnos de caja por día, efectivo/tarjeta/transferencia, una cancelación y devoluciones, cortes con algún faltante/sobrante). Crea los cajeros de prueba `ana` y `luis` y, para Charolas, precios de charola (80 % del de mostrador, sin bebidas), 3 tiendas con su charola habitual, entregas diarias, recolecciones con piezas que regresan para Piedra y cobros (una tienda paga completo, otra a veces deja saldo y otra paga cada semana); la charola de ayer queda en la tienda. Es determinista: la misma N produce los mismos datos.

Las credenciales se imprimen una sola vez; no guardarlas en documentos versionados. Herramienta de desarrollo: no se instala en las cajas. Desde Claude Code desktop usar una carpeta fuera de AppData (ver docs/review-builds.md). Para entrar sin escribir credenciales en una copia de desarrollo, crear `dev-autologin.json` en la carpeta de datos.
