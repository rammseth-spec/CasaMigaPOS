# Catálogo autorizado v2 (datos de revisión)

`catalogo-autorizado-v2.csv` reproduce, fila por fila, la tabla de [phase1k2c-provisional-map.md](../phase1k2c-provisional-map.md): 26 productos con nombre, precio y categoría del catálogo autorizado v2. La prueba `AuthorizedCatalogTests` impide que ambos difieran. Las fotos no están: los mosaicos originales nunca se versionaron.

Preparar una copia de revisión **nueva** (nunca sobre una instalación existente):

```powershell
dotnet run --project tools/CasaMiga.Phase1K2CSmoke -- seed-review docs/catalog/catalogo-autorizado-v2.csv C:\ruta\nueva\vacia
```

Crea `admin` (contraseña generada) y `cajero1` (contraseña temporal, debe cambiarla al entrar), 4 categorías y 26 productos auditados. Las credenciales se imprimen una sola vez; no guardarlas en documentos versionados. Herramienta de desarrollo: no se instala en las cajas.
