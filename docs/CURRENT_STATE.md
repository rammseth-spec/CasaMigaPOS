# Estado actual — FASE 1G BLOQUE A EN CHECKPOINT FÍSICO

Fecha: 2026-09-11. FASE 1F permanece cerrada y su recuperación es `db6422a1d09c36f1ae038c673d90ef9ec6f13ab4`. FASE 1G Bloque A está implementada en `4f6463e` y queda detenida antes del smoke físico. Git local conserva la identidad Rammseth / `rammstudioworkshop\@gmail.com`; no hay remoto ni push.

ReceiptDocument y ReceiptLine son inmutables y viven en Application. El documento se deriva de Sale/SaleItem/Payment, captura nombres de usuario/terminal y perfil comercial, usa hora local con offset y conserva Money Int64. El JSON canónico, versión y SHA-256 se guardan en receipt_snapshot. Requests y eventos append-only distinguen ORIGINAL/RETRY/REPRINT y REQUESTED/SUBMITTED/COMPLETED/FAILED/UNKNOWN.

Migración 008 agrega las tres tablas y `sales.receipt.reprint` sólo para Administrator/Manager. `printing.json` es local, opcional y excluido de backup. Sin configuración válida, el cobro permanece operativo y el ticket se marca como no enviado. Checkout intenta imprimir sólo después de recibir la venta confirmada; SQLite no permanece en transacción durante Winspool.

WindowsRawPrintTransport envía RAW a una cola lógica y devuelve JobId. SerializedReceiptPrintTransport limita a un trabajo dentro de la aplicación. El renderer de Bloque A contiene texto únicamente: no emite selección de codepage, inicialización, corte ni cajón. SUBMITTED significa aceptado por spooler, no papel físicamente confirmado.

Verificación vigente: 182 pruebas aprobadas, 0 fallidas, 0 omitidas; build Release con 0 errores y 0 advertencias. Upgrade 007→008, base nueva, snapshots, permisos, inmutabilidad, concurrencia y backup/restore están cubiertos. El smoke físico Qian no se inició.

`sales.view` se concede por migración 007 solamente a Administrator y Manager. Cashier no lo recibe. Presentation oculta Ventas sin permiso y Application revalida en cada consulta que el usuario actual exista, esté activo y conserve el permiso. Consultar historial no exige caja abierta ni `sales.complete`.

El listado devuelve hasta 50 ventas, `COUNT` y página mediante joins, orden `confirmed_utc DESC, id DESC`, búsqueda exacta con `terminal_id + sequence` y rango de días locales. Desde es inclusivo; Hasta se convierte al inicio UTC del día local siguiente y se usa como límite exclusivo. El detalle carga encabezado/pago/usuario/terminal y después partidas por `position`, sin N+1. Sale, SaleItem y Payment no cambiaron y continúan inmutables.

Las lecturas usan `ILocalReadUnitOfWork`: conexión SQLite read-only y `BeginTransaction(deferred: true)`. Las operaciones comerciales conservan `ILocalUnitOfWork.Run` con transacción inmediata. Una prueba mantiene una lectura abierta y confirma simultáneamente un Checkout real para comprobar que el historial no reserva el writer lock.

Estado de aceptación: **FASE 1F CERRADA**. Smoke WPF sobre instalación sintética: 55 ventas, página 1 de hasta 50, página 2 con 5, orden descendente 55→1, folio exacto, formato inválido rechazado, día local con 55 resultados, detalle de dos partidas y pago CASH, Volver/Limpiar y Ventas ausente para Cashier. SQLite después del recorrido conserva 55 ventas, 110 partidas, 55 pagos y 55 auditorías `sale.completed`; migración 7 y dos roles con `sales.view`.

Verificación final: 170 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores, 0 advertencias. Publicación win-x64 autocontenida en `artifacts/development`. Backup `.cmbackup` y restauración CLI a carpeta nueva tienen el mismo hash lógico `C7E4A5392F31988D6BC7857B664594F5BCD3C9F4FF3D7CA71C299D1CC9423F4F` y el mismo `terminal.json`.

Limitaciones heredadas de 1F: los nombres mostrados en Historial siguen siendo actuales y no existen snapshots retroactivos. Los tickets anteriores a 1G capturan esos nombres al crear su primer snapshot. La zona horaria depende del equipo y LIMIT/OFFSET deberá revisarse con volúmenes mayores. Sigue siendo una versión de desarrollo, sin preparación de producción ni validación física del hardware.
