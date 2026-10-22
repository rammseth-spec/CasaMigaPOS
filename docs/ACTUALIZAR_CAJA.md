# Actualizar la caja real a la versión 32 de la base, y cómo volver

Ensayado el 22/10/2026 sobre una copia de la carpeta completa de la caja real: 23 ventas, $1,633, caja del 19/10 abierta y 44 productos con foto.

## Aplicado a la caja real el 22/10/2026, con autorización del usuario

- **Programa:** compilado de `main` en `30fad4b` (Release, `Version=1.0.0`, sin etiqueta de revisión ni `development.settings.json`), puesto en `C:\Users\ramms\CasaMiga-Ventas\programa`.
- **Copias de antes, para regresar:**
  - datos: `C:\Users\ramms\CasaMiga-Ventas\respaldos\caja-antes-v32-20261022` (112 archivos, misma huella que la base original);
  - programa anterior: `C:\Users\ramms\CasaMiga-Ventas\programa-4e8ec88-anterior` (55 archivos).
  - El programa que estaba en uso quedó además renombrado como `programa-retirado-20261022`.
- **Respaldo oficial** con la herramienta anterior: `casamiga-20261022T225800727Z-…cmbackup` en la carpeta `backups` de la caja. Al abrir, el programa nuevo hizo además sus respaldos de antes y después.
- **Resultado** al abrir con `Abrir-Casa-Miga.vbs` (sólo lectura para comprobarlo):
  - base en 32; 23 ventas ($1,633) y 23 pagos; la caja del 19/10 sigue abierta;
  - 44 productos activos con 44 fotos y 3 usuarios;
  - 0 movimientos de inventario y el descuento por venta `DISABLED`;
  - las 10 preparaciones como borrador «Por medir»;
  - la pantalla de acceso dice «Aplicación local · Versión 1.0.0» y la nube «sin conexión · 10 pendientes» (los avisos de la marca, que se mandan al conectarse).
- Para regresar, seguir «Cómo volver a la versión anterior» con esas copias.

La caja real no se tocó; antes y después del ensayo su base tiene la misma huella (SHA-256 `3101C0B7507E95B8…`, última escritura 19/10/2026 18:56). La primera versión de este documento (20/10) probó sólo hasta la 31.

## Qué cambia

El programa de `main` agrega dos migraciones:

- **031**: preparaciones dentro de preparaciones (una tabla nueva, vacía, y sus reglas).
- **032**: la marca «Por medir». Las **10 preparaciones en borrador quedan marcadas** y no podrán publicarse hasta quitarles la marca una por una, cuando sus datos estén confirmados.

No modifica ninguna venta, caja, producto, foto, usuario, ticket, inventario ni registro de auditoría. El descuento por venta sigue apagado.

Una vez actualizada, **una versión anterior del programa ya no abre esa base** y no la daña.

## Lo que se comprobó (22/10/2026)

Carpeta del ensayo: `C:\Users\ramms\CasaMiga-Revision\ensayo-v32-20261022`.
- `antes-v30`: copia sin tocar.
- `actualizada`: copia subida a la 32.
- A ambas se les quitó `sync.json` para que el ensayo nunca se conectara a la nube con la identidad de la caja real.

| Prueba | Resultado |
|---|---|
| Copia de la carpeta completa con la caja cerrada (sólo lectura del origen) | Las dos copias tienen la misma huella que la original |
| Arranque del programa de `main` sobre la copia (el mismo camino que al abrir la app) | Respalda antes y después, y sube la base de 30 a 32 |
| Todas las tablas antes y después, renglón por renglón | Iguales, salvo tres diferencias esperadas (detalle abajo) |
| Ventas, pagos, caja y usuarios después de actualizar | 23 ventas, $1,633, la caja del 19/10 sigue abierta, 3 usuarios |
| Programa anterior (`4e8ec88`) sobre la copia ya en 32 | No la abre (muestra «No se completó la operación (código: InvalidOperationException)» y registra `migration.failed`) y la base queda idéntica |
| Programa anterior sobre la copia de antes | Abre normal: «Caja abierta», pantalla de acceso |

**Las tres diferencias esperadas** de la comparación:
- `formula_version`: las 10 versiones ganan la marca «Por medir». Nombre, rendimiento, notas, fechas y revisión quedan idénticos.
- `schema_migration`: los registros de las migraciones 31 y 32.
- `sync_outbox`: 10 avisos para la nube.
- La tabla nueva `formula_preparation` queda vacía.

Antes, la herramienta anterior no podía restaurar un respaldo `.cmbackup` hecho por la versión nueva. Por eso **la forma de volver es la copia de la carpeta completa**.

## Pasos para actualizar (cuando se apruebe)

1. Cerrar la caja (el programa, no el turno) y comprobar que no queda abierta.
2. Sacar una **copia de la carpeta completa** de datos y otra del programa:
   - `C:\Users\ramms\CasaMiga-Revision\local-main-c2fb393\caja` → `C:\Users\ramms\CasaMiga-Ventas\respaldos\caja-antes-v32-<fecha>`;
   - `CasaMiga-Ventas\programa` → `CasaMiga-Ventas\programa-4e8ec88-anterior`.
3. Hacer además un respaldo oficial: `CasaMiga.Maintenance backup <carpeta de datos>`.
4. Poner el programa nuevo, compilado de `main` en Release, en `CasaMiga-Ventas\programa`, sin `development.settings.json`.
5. Abrir la caja con su lanzador. Al arrancar respalda, sube la base a 32 y vuelve a respaldar.
6. Revisar que estén las ventas del día, la caja abierta, el catálogo con fotos y, en Recetas, las 10 preparaciones como «Borrador · Por medir».

## Cómo volver a la versión anterior

1. Cerrar la caja.
2. Renombrar la carpeta de datos actualizada (`caja` → `caja-v32-no-usada`) y poner en su lugar la copia del paso 2 (`caja-antes-v32-<fecha>` → `caja`).
3. Volver a poner el programa anterior (`programa-4e8ec88-anterior` → `programa`).
4. Abrir la caja.

**Importante:** lo que se haya vendido o registrado *después* de actualizar queda en `caja-v32-no-usada` y no aparece en la versión anterior. Por eso conviene actualizar al **inicio** del día, antes de vender, y decidir en ese momento si se queda o se regresa.
