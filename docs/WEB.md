# Casa Miga en línea (versión web)

Consulta y algunos cambios desde cualquier navegador (computadora, tablet o celular). Las cajas siguen vendiendo sin Internet: la web muestra lo que ya subieron y los cambios hechos en la web bajan a las cajas cuando se conectan.

Decisiones del usuario (07/10/2026): consultar todo lo que se ve en la caja; editar precios, productos del catálogo, usuarios y tiendas de charolas; cada usuario entra con su cuenta; administrador ve todas las sucursales, encargado sólo la suya, cajero no entra; catálogo con fotos reducidas; responsiva para celular y tablet.

## Cómo fluye la información

| Sentido | Qué | Cómo |
|---|---|---|
| Caja → nube | Ventas, devoluciones, cortes, gastos/retiros/entradas, bitácora, sucursal, usuarios (sólo el hash), permisos, categorías, productos, fotos (miniatura de 512 px), charolas (tiendas, charola habitual, precios, entregas, recolecciones, cobros), inventario, recetas y producción | Cola local `sync_outbox` (migraciones 018, 019 y 023). Cada entrada dice qué registro cambió; el contenido se lee al enviar, así que llega el estado más reciente. Lotes de hasta 1.5 MB. |
| Nube | Historial inmutable (`sync_event`) y último contenido de cada registro (`entity_state`) | `cloud/db/002_web.sql`; las migraciones de la nube se aplican solas al primer acceso, con candado. |
| Nube → caja | Cambios hechos en la web (`branch_command`) | Después de subir lo pendiente, la caja pide los cambios de su sucursal, los aplica en orden con sus propias reglas y permisos, informa el resultado (`command_result`) y sube lo que cambió. Registro local en `cloud_command` (migración 024): aplicar dos veces no repite nada. |

## Acceso

- Usuario y contraseña de la caja. La nube compara contra el hash PBKDF2-SHA512 (210 000 iteraciones, formato de ASP.NET Identity V3) que subió la caja; la contraseña nunca se guarda ni se muestra en la web.
- Cinco intentos fallidos bloquean ese usuario 15 minutos en la web (la caja no se afecta).
- Con contraseña temporal no se entra: primero se cambia en la caja.
- Sesión: cookie firmada (HttpOnly, Secure, SameSite=Strict) de 12 h. Cada consulta revalida al usuario contra lo último que subió la caja: si se desactiva, pierde el permiso o cambia su contraseña, la sesión termina.
- Alcance: el administrador ve todas las sucursales; el encargado, la suya; el cajero (sin `sales.view`) no entra. Cada pantalla pide el mismo permiso que en la caja.

## Pantallas

Resumen, Ventas (búsqueda por folio C01-001061 o producto, forma de pago, detalle del ticket con devoluciones), Reportes (comparativo contra el periodo anterior, ventas por hora, control de caja, producción sugerida), Caja (cortes con su resultado o el esperado del turno abierto; gastos, retiros y entradas), Charolas (tiendas con saldo, entregas, cobros), Catálogo (fotos, precios, precio de charola), Inventario, Recetas, Producción, Usuarios (sólo administrador) y Cajas (última conexión y cambios enviados con su estado por caja).

Responsiva: menú lateral en computadora, riel de íconos en tablet (≤1100 px) y menú desplegable en celular (≤720 px), donde las tablas se vuelven tarjetas y los diálogos hojas inferiores.

## Cambios desde la web

| Cambio | Permiso | En la caja |
|---|---|---|
| Producto (alta, nombre, SKU, categoría, precio, descripción, inventario) | `products.create` / `products.edit` | `CatalogService` con el mismo identificador en todas las cajas |
| Activar/desactivar producto | `products.deactivate` | `SetProductActive` |
| Categoría | `categories.*` | `CreateCategory` / `EditCategory` / `SetCategoryActive` |
| Usuario: alta, nombre/rol, activar, restablecer contraseña | `users.manage` (administrador) | `CreateUserWithHash` / `UpdateUser` / `SetActive` / `ResetPasswordWithHash`; la contraseña temporal la genera la nube, se muestra una vez y debe cambiarse al entrar. El administrador no se edita desde la web. |
| Tienda de charolas y charola habitual | `consignment.manage` | `SaveStore` / `SetStoreActive` / `SaveTemplate` |

La caja actúa como quien pidió el cambio si ese usuario existe en ella (con sus permisos locales); si no (por ejemplo, el administrador de otra sucursal), como el administrador local. Un cambio que las reglas de la caja rechazan queda «Rechazado» con el motivo en Cajas → Cambios; un error inesperado se reintenta en la siguiente sincronización.

## Publicar

Ver [SYNC_PLAN.md](SYNC_PLAN.md#publicar-en-vercel). Variables: `DATABASE_URL` (Neon), `ENROLLMENT_SECRET` (alta de cajas, 24+ caracteres), `SESSION_SECRET` (firma de sesiones web, 32+ caracteres) y opcional `PANEL_TIME_ZONE` (por omisión `America/Mexico_City`).

Revisión local sin Neon ni Vercel:

1. `CasaMiga.Maintenance export-sync <copia de datos> <archivo.json>` (aplica las migraciones a esa copia y vuelca su cola sin marcar nada como enviado).
2. `node scripts/dev-web.ts <archivo.json>` desde `cloud/` y abrir `http://localhost:4173`.

## Pendiente

- Varias cajas en una sucursal comparten sucursal pero no catálogo: cada una tiene su base; los cambios de la web llegan a todas, pero altas hechas en una caja no bajan a las demás (fase 4 completa).
- Inventario, recetas y producción: sólo consulta.
- Fotos: se ven, no se cambian desde la web.
