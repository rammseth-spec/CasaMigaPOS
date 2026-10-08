# Casa Miga en línea (CasaMigaPOS_Web)

Versión web de Casa Miga POS: consulta y algunos cambios desde la computadora, el celular o la tablet. Las cajas (app de escritorio CasaMigaPOS) siguen vendiendo sin Internet y sincronizan con esta nube cada 60 segundos.

- **Web responsiva**: menú lateral en computadora, riel de íconos en tablet, menú desplegable y tarjetas en celular.
- **Pantallas**: Resumen, Ventas (ticket), Reportes, Caja, Charolas (incluida la **Ruta** para registrar entregas, recolecciones y cobros desde el celular), Catálogo con fotos, Inventario, Recetas, Producción, Usuarios y Cajas.
- **Acceso**: usuario y contraseña de la caja. Administrador: todas las sucursales; encargado: la suya; cajero: no entra.
- **Cambios** (precios, productos, categorías, usuarios, tiendas, ruta): se validan aquí y cada caja los aplica con sus propias reglas.
- **API de las cajas**: alta de terminal, recepción de lotes, cambios pendientes y **respaldos** (cada hora por caja, copia nocturna de la nube, 30 días; todo cifrado). Restauración: `CasaMiga.Maintenance restore-cloud`.

Detalle completo del funcionamiento: `docs/WEB.md` en el repositorio CasaMigaPOS.

## Estructura

| Carpeta | Qué |
|---|---|
| `public/` | La página (HTML, CSS y JavaScript sin dependencias; CSP estricta). |
| `api/` | Funciones de Vercel: `web/[op]` (la web), `sync/*` y `terminals/register` (las cajas), `health`. |
| `lib/` | Lógica compartida y consultas (TypeScript). |
| `db/` | Migraciones de PostgreSQL; se aplican solas en el primer acceso, con candado. |
| `test/` | Pruebas con PostgreSQL en memoria (PGlite). |
| `scripts/dev-web.ts` | Servidor local sin Neon ni Vercel. |

## Publicar (Vercel + Neon)

1. En Vercel: **Add New → Project → Import** este repositorio (framework: Other; sin build).
2. Variables de entorno (Production):
   - `DATABASE_URL`: cadena de conexión de Neon (consola de Neon → Connect).
   - `ENROLLMENT_SECRET`: 24 caracteres o más; se escribe en la pantalla **Nube** de cada caja para registrarla.
   - `SESSION_SECRET`: 32 caracteres o más; firma las sesiones de la web (cambiarlo cierra todas).
   - `BACKUP_SECRET`: 32 caracteres o más; cifra los respaldos. **Guárdalo también fuera de Vercel**: sin él no se puede restaurar nada.
   - `CRON_SECRET`: 16 caracteres o más; protege la copia nocturna (Vercel Cron lo envía solo).
   - Opcional `PANEL_TIME_ZONE` (por omisión `America/Mexico_City`).
   - En **Storage → Blob**, crea un almacén y conéctalo al proyecto (agrega `BLOB_READ_WRITE_TOKEN`): ahí viven los respaldos cifrados.
3. Deploy y abrir `/api/health` → `{"ok":true,"schema":N}`.
4. En cada caja, como administrador → **Nube**: dirección del sitio, nombre de la sucursal y el secreto de alta.

Los valores secretos nunca se guardan en el repositorio.

## Desarrollo local

```bash
npm install
npm test
npm run typecheck
```

Revisar la web con datos reales sin tocar la nube: exportar la cola de una **copia** de datos de la caja con `CasaMiga.Maintenance export-sync <copia> <archivo.json>` y luego `npm run dev:web -- <archivo.json>` → `http://localhost:4173`. Con `ENROLLMENT_SECRET` definido, una copia de la app puede registrarse contra `http://localhost:4173` y probar el ciclo completo.

TypeScript con importaciones `.js` (NodeNext): Vercel lo compila al publicar; localmente corre con `tsx`.
