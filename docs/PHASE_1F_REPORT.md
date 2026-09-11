# CASA MIGA POS — REPORTE FINAL FASE 1F

Fecha de cierre: 2026-09-11. Estado definitivo: **FASE 1F CERRADA**. Baseline aprobado de FASE 1E: `d4118144dd6f19bff885ced15a3f6098c24f7c29`. Commit de implementación 1F: `b957190` (`feat: add read-only sales history`).

## Implementación y subfases

1. Contrato de consulta en Application: filtros, páginas, filas, detalle, validación y autorización.
2. Migración 007 y consultas SQLite acotadas.
3. Ruta específica de lectura read-only/deferred y prueba de concurrencia contra Checkout.
4. UI Ventas con listado, filtros, paginación, detalle y navegación.
5. Pruebas, smoke WPF, backup/restauración, publicación y continuidad.

No se cambió la semántica de Sale, SaleItem, Payment, Checkout ni ExpectedCash. El historial no permite editar, borrar, cancelar, devolver ni cambiar datos financieros.

## Permisos

Se creó `sales.view`:

- Administrator: sí.
- Manager: sí.
- Cashier: no.

Presentation oculta Ventas al cajero. Application vuelve a comprobar usuario existente, activo y permiso vigente en `CanView`, `Search` y `Detail`. Las pruebas cubren los tres roles, usuario desactivado después del login, revocación posterior al login, caja cerrada y ausencia de `sales.complete`.

## Migración 007

`007_sales_history.sql` sólo inserta el permiso, lo asigna a Administrator/Manager y crea `ix_sale_history` sobre `sale(confirmed_utc DESC,id DESC)`. El folio no se duplicó: se sigue formando desde `terminal_id + sequence`.

Los SHA-256 de 001–006 coinciden con los valores históricos y además se prueban desde los recursos ensamblados. Upgrade v6→v7 conserva una venta existente y añade exactamente el permiso/roles/índice. Evidencia: `docs/evidence/phase1f-migrations.json`.

## Arquitectura de lectura y bloqueo

`ILocalReadUnitOfWork` abre `Database.Open(readOnly: true)` y `BeginTransaction(deferred: true)`. Una lectura obtiene un snapshot consistente sin iniciar anticipadamente una transacción de escritura. `ILocalUnitOfWork.Run` conserva `BeginTransaction(deferred: false)` para operaciones comerciales; su semántica no se cambió.

La prueba `DeferredReadSnapshotDoesNotReserveWriterLockAgainstCheckout` mantiene una consulta abierta, ejecuta un Checkout real desde otra conexión y exige que el writer complete antes de liberar la lectura. Después confirma que el snapshot leído era coherente y que la nueva venta aparece en la siguiente consulta. Decisión y fuentes oficiales: `docs/decisions/ADR-011-sales-history-read-path.md`.

## Consultas, paginación y filtros

Listado por página:

1. `COUNT(*)` con filtro.
2. Encabezados con joins de payment, app_user y terminal.

El tamaño fijo es 50, con LIMIT/OFFSET y orden `confirmed_utc DESC,id DESC`. No se carga todo el historial ni se consulta usuario/terminal por fila. El folio exige formato canónico y busca exactamente terminal/secuencia. Los modos folio y fechas son excluyentes.

El rango recibe `DateOnly` en Presentation/Application. Desde es inicio local inclusivo. Hasta agrega un día en calendario local, convierte ese límite a UTC y consulta con `< UntilUtc`. Infrastructure recibe únicamente límites UTC. Rangos invertidos, formatos inválidos y desbordes se rechazan.

Detalle ejecuta una consulta de encabezado/pago/usuario/terminal y una de partidas ordenadas por `position`. Reconstruye el agregado para revalidar Money, línea, subtotal, total, aplicado, recibido y cambio. Usa snapshots de `sale_item`. Muestra nombre actual de usuario/terminal y sus IDs históricos, según la decisión aprobada.

## UI y smoke WPF

La navegación incorpora Ventas sin rediseño. El listado muestra folio, fecha local con offset, total MXN, CASH, cajero actual, terminal actual y CashSessionId. Permite folio exacto, Desde/Hasta, Limpiar, Anterior/Siguiente, total de resultados y Ver detalle. El detalle muestra SaleId/UserId/TerminalId/CashSessionId, partidas, snapshots, subtotal, total, aplicado, recibido, cambio y Volver.

Smoke completo sobre 55 ventas sintéticas, sin crear operaciones durante la consulta:

- Manual: apertura, login, navegación, pantalla, botones, entradas, mensajes, páginas, detalle, Volver/Limpiar y ausencia de Ventas para Cashier.
- Técnico: esquema/roles, límites UTC, consultas acotadas, integridad, hashes, no mutación, concurrencia, backup/restore.
- Mixto: permiso admin/cajero, total 55, máximo 50, orden 55→1, página 2 con 5, folio exacto, folio inválido, filtro local, datos del detalle y ausencia de mutación.

El recorrido paso a paso está en `docs/PHASE_1F_SMOKE.md`; capturas en `docs/evidence/phase1f-*.png`. SQLite posterior conserva 55 ventas, 110 partidas, 55 pagos y 55 auditorías; secuencias 1–55, caja OPEN preexistente y cero operaciones comerciales creadas por el smoke. La aplicación quedó cerrada.

## Pruebas, build y publicación

- SalesHistoryTests focalizadas: 17 aprobadas, 0 fallidas, 0 omitidas.
- Suite final: 170 aprobadas, 0 fallidas, 0 omitidas.
- Build Release: 0 errores, 0 advertencias.
- Restore locked-mode correcto desde caché local oficial; no se añadieron paquetes.
- Publicación Release win-x64 autocontenida en `artifacts/development`; SHA-256 del ejecutable `606CBB86A9239E64189360E19B6E498E69369D18D1AD68DD06DFD023B542D867`.

El aviso de sobrescritura del TRX sólo indica que se reemplazó el archivo de resultado anterior. La publicación sigue marcada como DESARROLLO y no es un instalador de producción.

## Backup y restauración

La CLI creó un `.cmbackup` de la instalación sintética y lo restauró a un directorio nuevo. Origen y restaurado producen el mismo hash lógico de todas las tablas: `C7E4A5392F31988D6BC7857B664594F5BCD3C9F4FF3D7CA71C299D1CC9423F4F`. `terminal.json` coincide en SHA-256. El historial restaurado conserva 55 ventas, 110 partidas, 55 pagos, 55 auditorías y schema v7. Evidencia: `docs/evidence/phase1f-restore.json`.

## Archivos de producto

Nuevos:

- `migrations/007_sales_history.sql`
- `src/CasaMiga.Application/SalesHistoryService.cs`
- `src/CasaMiga.Presentation/SalesHistoryView.xaml`
- `src/CasaMiga.Presentation/SalesHistoryView.xaml.cs`
- `src/CasaMiga.Presentation/SalesHistoryViewModel.cs`
- `tests/CasaMiga.Tests/SalesHistoryTests.cs`

Modificados:

- `src/CasaMiga.Application/LocalPorts.cs`
- `src/CasaMiga.Infrastructure/SqliteLocalUnitOfWork.cs`
- `src/CasaMiga.Infrastructure/SqliteSaleStore.cs`
- `src/CasaMiga.Presentation/MainViewModel.cs`
- `src/CasaMiga.Presentation/MainWindow.xaml`
- `src/CasaMiga.Presentation/MainWindow.xaml.cs`
- `src/CasaMiga.Presentation/PosView.xaml`
- `src/CasaMiga.Presentation/PosView.xaml.cs`
- `tests/CasaMiga.Tests/CashIdentityTests.cs`
- `tests/CasaMiga.Tests/FoundationTests.cs`

La documentación y evidencia de 1F se registran en commits separados del código. `work`, `artifacts` y `TestResults` continúan ignorados.

## Riesgos y límites

- Los nombres mostrados son actuales; no existen snapshots retroactivos de usuario/terminal. Los IDs preservan la referencia histórica.
- La zona horaria es la local del equipo; falta configuración comercial explícita.
- LIMIT/OFFSET puede degradarse con volúmenes mucho mayores; el smoke y la prueba cubren miles, no años de operación real.
- No hay seguridad frente a un administrador Windows con acceso directo al archivo.
- Siguen pendientes pruebas físicas, política de retención/segunda copia/cifrado, activación operativa de restore, recuperación administrativa, licencia y distribución.
- El producto continúa en desarrollo y no está listo para producción.

No se implementaron FASE 1G, cancelaciones, devoluciones, reembolsos, descuentos, promociones, combos, tarjetas, clientes, fiscalidad, facturación, inventario, recetas, producción, impresión, Qian, cajón, rediseño, F11/fullscreen ni ninguna funcionalidad posterior.

Próximo paso recomendado, sin ejecutarlo: definir y autorizar formalmente el alcance de la siguiente fase con una decisión comercial concreta. No iniciar trabajo posterior desde este cierre.
