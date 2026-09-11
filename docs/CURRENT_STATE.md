# Estado actual — FASE 1G BLOQUE E, LOGO OFICIAL REQUERIDO

Fecha: 2026-09-11. FASE 1F permanece cerrada. El usuario confirmó físicamente el Bloque D completo: caracteres españoles, legibilidad, tamaños, alineación, ausencia de basura y corte automático parcial correctos. Bloque D implementa selección estricta de codepage en `283cfa4`; el baseline queda fijado en .NET `ibm850`/CP850, `ESC t 2` y `GS V 1`. Git local conserva la identidad Rammseth / `rammstudioworkshop\@gmail.com`; no hay remoto ni push.

Bloque D envió exactamente un ticket sintético: .NET `ibm850`, ESC/POS tabla 2, selección `1B 74 02` una vez en offset cero. Request `957c1076-cd21-4ceb-b609-63b437a6c643`, JobId 5, SUBMITTED, 889 bytes y SHA-256 `83CC7BA9E0339E608891C2B3908021A847053B40B5A3F722C4F4AF44F101321D`. La fotografía `evidence/phase1g-block-d-physical.png` y la confirmación del usuario validan la salida física y el corte parcial.

Bloque C envió exactamente un ticket sintético a POS-80: request `6df7d54d-a095-48e3-9223-dfc09b5bdf51`, JobId 4, 770 bytes, estado SUBMITTED y SHA-256 `792FEFF59A9921E17AD77DFDD03D3B7D65447150B58ED195C159CF443DECB757`. Contiene encabezado 2×2, TOTAL doble alto, dos restauraciones, cuatro LF y un único corte parcial final. No contiene pulso de cajón. Su jerarquía visual fue aprobada y el corte parcial quedó confirmado con el Bloque D.

Windows confirmó `POS-80`, USB001, driver POS-80 11.3.0.1 y estado Normal. El trabajo `7ba5b12f-2482-457d-abf4-725181b25d60` fue aceptado por Winspool con JobId 3 y estado técnico SUBMITTED; fueron 762 bytes ASCII sin ESC, GS, corte ni cajón. Esto no confirma salida física. No se abrió la base comercial ni se crearon venta, pago, caja o cambio en ExpectedCash.

ReceiptDocument y ReceiptLine son inmutables y viven en Application. El documento se deriva de Sale/SaleItem/Payment, captura nombres de usuario/terminal y perfil comercial, usa hora local con offset y conserva Money Int64. El JSON canónico, versión y SHA-256 se guardan en receipt_snapshot. Requests y eventos append-only distinguen ORIGINAL/RETRY/REPRINT y REQUESTED/SUBMITTED/COMPLETED/FAILED/UNKNOWN.

Migración 008 agrega las tres tablas y `sales.receipt.reprint` sólo para Administrator/Manager. `printing.json` es local, opcional y excluido de backup. Sin configuración válida, el cobro permanece operativo y el ticket se marca como no enviado. Checkout intenta imprimir sólo después de recibir la venta confirmada; SQLite no permanece en transacción durante Winspool.

WindowsRawPrintTransport envía RAW a una cola lógica y devuelve JobId. SerializedReceiptPrintTransport limita a un trabajo dentro de la aplicación. El renderer de Bloque A contiene texto únicamente: no emite selección de codepage, inicialización, corte ni cajón. SUBMITTED significa aceptado por spooler, no papel físicamente confirmado.

Verificación vigente: 188 pruebas aprobadas, 0 fallidas, 0 omitidas; build Release con 0 errores y 0 advertencias. El mecanismo controlado no abre la base comercial y no cambia venta, pago, caja ni ExpectedCash.

`sales.view` se concede por migración 007 solamente a Administrator y Manager. Cashier no lo recibe. Presentation oculta Ventas sin permiso y Application revalida en cada consulta que el usuario actual exista, esté activo y conserve el permiso. Consultar historial no exige caja abierta ni `sales.complete`.

El listado devuelve hasta 50 ventas, `COUNT` y página mediante joins, orden `confirmed_utc DESC, id DESC`, búsqueda exacta con `terminal_id + sequence` y rango de días locales. Desde es inclusivo; Hasta se convierte al inicio UTC del día local siguiente y se usa como límite exclusivo. El detalle carga encabezado/pago/usuario/terminal y después partidas por `position`, sin N+1. Sale, SaleItem y Payment no cambiaron y continúan inmutables.

Las lecturas usan `ILocalReadUnitOfWork`: conexión SQLite read-only y `BeginTransaction(deferred: true)`. Las operaciones comerciales conservan `ILocalUnitOfWork.Run` con transacción inmediata. Una prueba mantiene una lectura abierta y confirma simultáneamente un Checkout real para comprobar que el historial no reserva el writer lock.

Estado de aceptación: **FASE 1F CERRADA**. Smoke WPF sobre instalación sintética: 55 ventas, página 1 de hasta 50, página 2 con 5, orden descendente 55→1, folio exacto, formato inválido rechazado, día local con 55 resultados, detalle de dos partidas y pago CASH, Volver/Limpiar y Ventas ausente para Cashier. SQLite después del recorrido conserva 55 ventas, 110 partidas, 55 pagos y 55 auditorías `sale.completed`; migración 7 y dos roles con `sales.view`.

Verificación final: 170 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores, 0 advertencias. Publicación win-x64 autocontenida en `artifacts/development`. Backup `.cmbackup` y restauración CLI a carpeta nueva tienen el mismo hash lógico `C7E4A5392F31988D6BC7857B664594F5BCD3C9F4FF3D7CA71C299D1CC9423F4F` y el mismo `terminal.json`.

Bloque E fue revisado sólo hasta su precondición. La búsqueda dirigida encontró `src/CasaMiga.Presentation/Assets/README.md`, que declara que no existe un logotipo oficial y prohíbe generarlo o redibujarlo. No se modificó código, pruebas, migraciones ni configuración y no se imprimió otro ticket. Para continuar se necesita el archivo oficial aprobado, preferiblemente PNG monocromático con transparencia, alta resolución y proporciones originales.

Limitaciones heredadas de 1F: los nombres mostrados en Historial siguen siendo actuales y no existen snapshots retroactivos. Los tickets anteriores a 1G capturan esos nombres al crear su primer snapshot. La zona horaria depende del equipo y LIMIT/OFFSET deberá revisarse con volúmenes mayores. Sigue siendo una versión de desarrollo y no está lista para producción.
