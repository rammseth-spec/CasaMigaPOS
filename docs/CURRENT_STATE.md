# Estado actual — FASE 1F CERRADA

Fecha: 2026-09-11. FASE 1E permanece cerrada en `d4118144dd6f19bff885ced15a3f6098c24f7c29`. FASE 1F está registrada en implementación `b957190279fb984abbe00ac8e022f33acfa47909` y cierre documental/evidencia `912cf5232ae54971827b3e337bcb72cd1e05c4bb`. Añade historial y detalle de ventas confirmadas, exclusivamente de lectura. Git local conserva la identidad Rammseth / `rammstudioworkshop\@gmail.com`; no hay remoto ni push.

`sales.view` se concede por migración 007 solamente a Administrator y Manager. Cashier no lo recibe. Presentation oculta Ventas sin permiso y Application revalida en cada consulta que el usuario actual exista, esté activo y conserve el permiso. Consultar historial no exige caja abierta ni `sales.complete`.

El listado devuelve hasta 50 ventas, `COUNT` y página mediante joins, orden `confirmed_utc DESC, id DESC`, búsqueda exacta con `terminal_id + sequence` y rango de días locales. Desde es inclusivo; Hasta se convierte al inicio UTC del día local siguiente y se usa como límite exclusivo. El detalle carga encabezado/pago/usuario/terminal y después partidas por `position`, sin N+1. Sale, SaleItem y Payment no cambiaron y continúan inmutables.

Las lecturas usan `ILocalReadUnitOfWork`: conexión SQLite read-only y `BeginTransaction(deferred: true)`. Las operaciones comerciales conservan `ILocalUnitOfWork.Run` con transacción inmediata. Una prueba mantiene una lectura abierta y confirma simultáneamente un Checkout real para comprobar que el historial no reserva el writer lock.

Estado de aceptación: **FASE 1F CERRADA**. Smoke WPF sobre instalación sintética: 55 ventas, página 1 de hasta 50, página 2 con 5, orden descendente 55→1, folio exacto, formato inválido rechazado, día local con 55 resultados, detalle de dos partidas y pago CASH, Volver/Limpiar y Ventas ausente para Cashier. SQLite después del recorrido conserva 55 ventas, 110 partidas, 55 pagos y 55 auditorías `sale.completed`; migración 7 y dos roles con `sales.view`.

Verificación final: 170 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores, 0 advertencias. Publicación win-x64 autocontenida en `artifacts/development`. Backup `.cmbackup` y restauración CLI a carpeta nueva tienen el mismo hash lógico `C7E4A5392F31988D6BC7857B664594F5BCD3C9F4FF3D7CA71C299D1CC9423F4F` y el mismo `terminal.json`.

Limitaciones: los nombres de usuario y terminal mostrados son actuales; se conservan UserId/TerminalId, pero no se añadieron snapshots retroactivos. La zona horaria depende del equipo; una zona comercial configurable queda pendiente. LIMIT/OFFSET es adecuado para esta fase y deberá revisarse si el volumen real crece mucho. Sigue siendo una versión de desarrollo, sin preparación de producción, hardware ni fase posterior iniciada.
