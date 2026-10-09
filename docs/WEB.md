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
- Cinco intentos fallidos bloquean ese usuario 15 minutos en la web (la caja no se afecta). Cada intento se cuenta antes de revisar la contraseña, así que muchos intentos simultáneos tampoco pasan de cinco.
- Los envíos (entrar, cambios) sólo se aceptan desde la propia página (origen y `application/json`); la cookie además es `SameSite=Strict`.
- Con contraseña temporal no se entra: primero se cambia en la caja.
- Sesión: cookie firmada (HttpOnly, Secure, SameSite=Strict) de 12 h. Cada consulta revalida al usuario contra lo último que subió la caja: si se desactiva, pierde el permiso o cambia su contraseña, la sesión termina.
- Alcance: el administrador ve todas las sucursales; el encargado, la suya; el cajero entra **sólo a Vender** (y Catálogo para consultar; no ve Caja porque el conteo es ciego). Un rol sin `sales.view` ni `pos.use` no entra. Cada pantalla pide el mismo permiso que en la caja.

## Pantallas

**Vender** (punto de venta con fotos, cobro, ticket y corte; ver [POS_WEB.md](POS_WEB.md)), Resumen, Ventas (búsqueda por folio C01-001061 o producto, forma de pago, detalle del ticket con devoluciones), Reportes (comparativo contra el periodo anterior, ventas por hora, ganancia, control de caja, producción sugerida), Caja (cortes con su resultado o el esperado del turno abierto; gastos, retiros y entradas), Charolas (ruta, análisis por tienda, tiendas con saldo, entregas, cobros), Catálogo (fotos, precios, precio de charola), Inventario (con costo de referencia por insumo), Recetas, Producción, Usuarios (sólo administrador) y Cajas (última conexión y cambios enviados con su estado por caja).

Las fotos de los panes salen en todo el panel, como en la app: más vendidos, ticket de la venta, charolas (ruta, entregar, recoger, entregas, análisis), ganancia, producción sugerida y recetas. Se buscan por producto y, en ventas viejas, por nombre.

Responsiva: menú lateral en computadora, riel de íconos en tablet (≤1100 px) y menú desplegable en celular (≤720 px), donde las tablas se vuelven tarjetas y los diálogos hojas inferiores.

## Tiempo real

- La caja sube lo pendiente y baja los cambios de la web **cada 60 segundos**; tocar la píldora «Nube» de la barra superior sincroniza al momento (cualquier usuario).
- La web se actualiza sola cada 60 s con la página visible (sin parpadear ni perder la posición; se pausa mientras escribes o con un formulario abierto) y tiene el botón «Actualizado hh:mm ↻» para hacerlo al momento.

## Respaldos

Cada caja sube su base cada hora (si cambió) y sus fotos una vez; la nube se copia cada noche; todo cifrado y 30 días. Pantalla **Respaldos** (administrador) y restauración con `CasaMiga.Maintenance restore-cloud`. Ver [BACKUPS.md](BACKUPS.md).

## Ganancia y análisis de charolas

Reportes → Ganancia compara mostrador contra charolas con el costo de cada pan calculado desde su receta; Charolas → Análisis da por tienda la frecuencia de pedido, los días habituales, el porcentaje vendido y el pan que más se vende allí. Ver [PROFIT.md](PROFIT.md).

## Ruta del repartidor (Charolas → Ruta)

Pensada para celular: una tarjeta por tienda activa con teléfono, saldo, última entrega, charolas por recoger y botones grandes.
- **Entregar charola**: viene con la charola habitual; contadores − / + por pan; total de piezas e importe.
- **Recoger**: por cada pan, cuántos regresan (los demás se vendieron); calcula el cargo y propone cobrarlo; forma de pago; o **anular** la charola con motivo.
- **Cobrar**: hasta el saldo de la tienda.
- La caja lo aplica con la **hora en que se hizo en la ruta** (nunca en el futuro) y con el identificador del cambio como operación, así que reintentar no duplica. Mientras la caja no lo aplica, la tarjeta muestra «enviada · esperando a la caja», y no deja recoger o anular dos veces la misma charola desde dos celulares.
- Por ahora lo usa el administrador (una sucursal); no hay rol de repartidor.

## Cambios desde la web

| Cambio | Permiso | En la caja |
|---|---|---|
| Producto (alta, nombre, SKU, categoría, precio, descripción, inventario) | `products.create` / `products.edit` | `CatalogService` con el mismo identificador en todas las cajas |
| Activar/desactivar producto | `products.deactivate` | `SetProductActive` |
| Categoría | `categories.*` | `CreateCategory` / `EditCategory` / `SetCategoryActive` |
| Usuario: alta, nombre/rol, activar, restablecer contraseña | `users.manage` (administrador) | `CreateUserWithHash` / `UpdateUser` / `SetActive` / `ResetPasswordWithHash`; la contraseña temporal la genera la nube, se muestra una vez y debe cambiarse al entrar. El administrador no se edita desde la web. |
| Tienda de charolas y charola habitual | `consignment.manage` | `SaveStore` / `SetStoreActive` / `SaveTemplate` |
| Ruta: entrega, recolección (con cobro), cobro, anulación | `consignment.manage` | `Deliver` / `Collect` / `ReceivePayment` / `Void` con la hora de la ruta |
| Costo de referencia de un insumo | `inventory.configure` (administrador) | `IngredientCostService.SetCost` (migración 025); no mueve existencias |

La caja actúa como quien pidió el cambio si ese usuario existe en ella (con sus permisos locales); si existe pero está desactivado, lo rechaza; si no existe (por ejemplo, el administrador de otra sucursal), actúa como el administrador local. La caja relee los últimos 20 cambios en cada sincronización, porque dos guardados simultáneos en la web pueden hacerse visibles fuera de orden; los ya aplicados no se repiten. Un cambio que las reglas de la caja rechazan queda «Rechazado» con el motivo en Cajas → Cambios; un error inesperado se reintenta en la siguiente sincronización.

## Publicar

Ver [SYNC_PLAN.md](SYNC_PLAN.md#publicar-en-vercel). Variables: `DATABASE_URL` (Neon), `ENROLLMENT_SECRET` (alta de cajas, 24+ caracteres), `SESSION_SECRET` (firma de sesiones web, 32+ caracteres), `BACKUP_SECRET` (cifrado de respaldos, 32+), `CRON_SECRET` (16+), almacén Blob conectado (`BLOB_READ_WRITE_TOKEN`) y opcional `PANEL_TIME_ZONE` (por omisión `America/Mexico_City`).

Revisión local sin Neon ni Vercel:

1. `CasaMiga.Maintenance export-sync <copia de datos> <archivo.json>` (aplica las migraciones a esa copia y vuelca su cola sin marcar nada como enviado).
2. Desde `cloud/`: `npm run dev:web -- <archivo.json>` y abrir `http://localhost:4173`. Con `ENROLLMENT_SECRET` definido, una copia de la app puede registrarse contra `http://localhost:4173` y probar el ciclo completo (subir, ver, editar en la web, aplicar en la caja).

Este servidor de prueba **no pide contraseña** (decisión del usuario, 08/10/2026): entra solo con el último usuario elegido y, con «Salir», se elige otro (administrador, encargado o cajero). Para pedirla: `--con-login`. Empieza con una caja web lista (`CAJA-WEB-02`). Sólo existe en `scripts/dev-web.ts`, que no se publica (`.vercelignore`): la nube en Vercel siempre pide contraseña. Mientras corre, cualquiera en la misma red Wi-Fi puede entrar; úsalo sólo con datos de prueba.

El código de la nube es TypeScript con importaciones `.js` (estilo NodeNext): Vercel lo compila al publicar; localmente las pruebas y el servidor corren con `tsx`.

## Pendiente

- Varias cajas en una sucursal comparten sucursal pero no catálogo: cada una tiene su base; los cambios de la web llegan a todas, pero altas hechas en una caja no bajan a las demás (fase 4 completa).
- Recetas: sólo consulta. Inventario (entradas, mermas, conteos), producción y fotos ya se piden desde la web y los aplica la caja ([POS_WEB.md](POS_WEB.md), fase 3).
- El administrador de cualquier sucursal entra como administrador de todas (decisión «un solo administrador», SYNC_PLAN). Con varias sucursales conviene que sea la misma persona.
- Si dos personas guardan el mismo producto en la web en el mismo instante, puede quedar el primero de los dos guardados (caso extremo; se corrige guardando otra vez).
- Estado más reciente: dentro de una misma caja manda el orden de su cola (aunque su reloj se atrase); entre cajas distintas, la hora en que se encoló.
