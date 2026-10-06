# Catálogo autorizado v2 (datos de revisión)

`catalogo-autorizado-v2.csv` reproduce, fila por fila, la tabla de [phase1k2c-provisional-map.md](../phase1k2c-provisional-map.md): 26 productos con nombre, precio y categoría del catálogo autorizado v2. La prueba `AuthorizedCatalogTests` impide que ambos difieran. Los mosaicos de fotos originales nunca se versionaron.

## Fotos provisionales generadas con IA

`assets/catalog-provisional-ia/CM-001.jpg` … `CM-026.jpg` (768 × 768, ~2 MB en total) son ilustraciones generadas con IA a partir del nombre de cada producto, para que la caja se vea completa durante la revisión. **No son fotos de los productos reales**: entran con procedencia `PROVISIONAL` y deben reemplazarse por fotos reales antes de producción. `manifest.json` registra SKU, modelo, identificador de generación y SHA-256 de cada archivo.

## Preparar una copia de revisión

Siempre en una carpeta **nueva** (nunca sobre una instalación existente):

```powershell
dotnet run --project tools/CasaMiga.Phase1K2CSmoke -- seed-review docs/catalog/catalogo-autorizado-v2.csv C:\ruta\nueva\vacia --photos assets\catalog-provisional-ia --demo-days 7
```

- Siempre crea `admin` (contraseña generada) y `cajero1` (contraseña temporal, debe cambiarla al entrar), 4 categorías y 26 productos auditados.
- `--photos <carpeta>`: importa `CM-NNN.jpg` por el servicio normal de fotos, con procedencia `PROVISIONAL`.
- `--demo-days N` (1–60): ventas de prueba de los últimos N días hechas con los servicios reales (turnos de caja por día, efectivo/tarjeta/transferencia, una cancelación y devoluciones, cortes con algún faltante/sobrante). Crea los cajeros de prueba `ana` y `luis`. Es determinista: la misma N produce los mismos datos.

Las credenciales se imprimen una sola vez; no guardarlas en documentos versionados. Herramienta de desarrollo: no se instala en las cajas. Desde Claude Code desktop usar una carpeta fuera de AppData (ver docs/review-builds.md). Para entrar sin escribir credenciales en una copia de desarrollo, crear `dev-autologin.json` en la carpeta de datos.
