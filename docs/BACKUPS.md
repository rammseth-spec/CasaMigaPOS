# Respaldos en la nube

Decisiones del usuario (08/10/2026): cada caja respalda **cada hora con la app abierta**; se conservan **30 días**; la base de la nube se copia **cada noche**.

## Qué se respalda

| Qué | Cuándo | Cómo |
|---|---|---|
| Base de cada caja (ventas, caja, catálogo, charolas, usuarios, inventario, recetas…) | Al abrir la app y luego cada hora, en cuanto todo está sincronizado; **sólo si cambió** desde el respaldo anterior (misma huella SHA-256 = no se sube). Botón «Respaldar ahora» en **Nube**. Si falla, se reintenta a los 15 min. | Copia en vivo con la API de respaldo de SQLite (sin detener la venta), comprimida (gzip) y cifrada; en partes de 3 MB. |
| Fotos de productos (original y miniatura) | Junto con la base | Una sola vez por contenido: si ya están arriba no se vuelven a subir. |
| Identidad de la caja (`terminal.json`) | En cada respaldo | Dentro del registro del respaldo. |
| Base de la nube (lo recibido de las cajas, cambios hechos en la web) | Cada noche a las 3:00 (Vercel Cron, 09:00 UTC) | Tablas como renglones JSON, comprimidas y cifradas. |

La copia temporal de la base se hace en la carpeta temporal del sistema (ruta corta: SQLite no abre rutas de más de 260 caracteres) y se borra al terminar; no entra a la carpeta de datos ni a OneDrive.

## Seguridad

- Todo se cifra **antes** de guardarse: AES-256-GCM, formato `CMB1` + nonce (12) + etiqueta (16) + datos (`BackupCrypto` en la caja, `cloud/lib/backup.ts` en la nube).
- La llave de cada sucursal se deriva de `BACKUP_SECRET` (HKDF-SHA256, `branch:<id>`); la de la copia de la nube, con `cloud`. **Sin `BACKUP_SECRET` no se puede leer ningún respaldo**: guárdalo también fuera de Vercel (gestor de contraseñas).
- Los archivos viven en Vercel Blob con direcciones de sufijo aleatorio; aun si una dirección se filtrara, el contenido está cifrado. Un respaldo alterado no se restaura (la etiqueta GCM falla).
- Cada respaldo deja además un **manifiesto cifrado** junto a sus archivos: si se perdiera la base de la nube, basta Blob + `BACKUP_SECRET` para reconstruir.
- Sólo el administrador ve la pantalla **Respaldos** y obtiene lo necesario para restaurar.

## Cuánto se conserva

Todo lo de las últimas **48 horas**; de ahí a **30 días**, el último respaldo de cada día por caja; siempre el más reciente aunque sea viejo. La tarea nocturna borra lo demás y las fotos que ya ningún respaldo usa (con 2 días de gracia para subidas en curso).

## Restaurar una caja

En la computadora nueva (o en otra carpeta), con Casa Miga cerrado:

```
CasaMiga.Maintenance restore-cloud https://<sitio> <usuario administrador> <carpeta nueva> [identificador o nombre de la caja]
```

Pide la contraseña sin mostrarla, entra a la web como administrador, baja el respaldo más reciente de la caja (o el indicado; con varias cajas hay que indicar cuál), lo descifra, verifica cada huella y lo arma como un `.cmbackup` que pasa por la misma validación que una restauración local (`InstallationBackup.Restore`). Nunca escribe sobre una carpeta existente. Después: abrir Casa Miga con esa carpeta de datos y volver a registrar la caja en **Nube**.

Probado de punta a punta con la nube local: la base restaurada coincide con la original (1061 ventas, 26 productos, 84 charolas, 2 movimientos de caja, versión 24) y las 52 fotos pasan verificación.

## Configuración en Vercel

1. **Storage → Blob → Create** y conectarlo al proyecto (agrega `BLOB_READ_WRITE_TOKEN`).
2. Variables: `BACKUP_SECRET` (32+ caracteres) y `CRON_SECRET` (16+; Vercel lo manda en la tarea nocturna y nadie más puede dispararla).
3. La tarea está en `vercel.json` → `crons`.

Espacio aproximado (copia demo de 4 semanas): base 5 MB → 1 MB comprimida; fotos 2.6 MB una sola vez. Con cambios cada hora en horario de tienda y la regla de 48 h / 30 días: unos 60–80 MB por caja.

## Respaldos locales en la caja (20/10/2026)

- **Cuándo:** al abrir la app (antes y después de migrar) y con `CasaMiga.Maintenance backup <carpeta de datos>`. Quedan en `<carpeta de datos>\backups` como `.cmbackup`.
- **Qué llevan:** la base, la identidad de la caja (`terminal.json`), las fotos de los productos y, si existen, `printing.json` (impresora de tickets) y `operations-hub.json` (notas y temporizadores del Centro operativo).
  - `sync.json` **no** va: su llave sólo funciona en ese equipo y usuario de Windows; después de restaurar se vuelve a registrar la caja en **Nube**.
  - Un respaldo con configuración no lo restaura una versión anterior del programa: usa la herramienta de la misma versión o una más nueva.
- **Cuánto se conserva:** se borran sólo los respaldos con más de **30 días**, y **siempre** se quedan los **20 más recientes**, aunque sean viejos. Así el único respaldo existente nunca se borra. Los archivos con otro nombre (una copia manual, por ejemplo) no se tocan.
- **Fuera del equipo:** los respaldos locales viven en el mismo disco. Hasta publicar la nube, copia la carpeta `backups` a una USB.
