# ADR-011 — Ruta de lectura para historial de ventas

Fecha: 2026-09-11. Estado: aceptada en FASE 1F.

## Contexto

`ILocalUnitOfWork.Run` abre una transacción SQLite inmediata para proteger escrituras comerciales. Reutilizarla en Historial reservaría el escritor aunque la operación sólo ejecute `SELECT`, con interferencia innecesaria para Checkout, conteo y cierre. El historial también necesita que su `COUNT` y su página pertenezcan a una lectura consistente.

SQLite documenta que una transacción deferred no inicia como transacción de escritura y que un `SELECT` inicia una lectura; `BEGIN IMMEDIATE`, en cambio, inicia la escritura inmediatamente. Microsoft.Data.Sqlite expone esta elección mediante `BeginTransaction(bool deferred)`. Fuentes: [SQLite Transaction](https://sqlite.org/lang_transaction.html) y [SqliteConnection.BeginTransaction](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.begintransaction?view=msdata-sqlite-10.0.0).

## Decisión

Se introduce `ILocalReadUnitOfWork` como puerto separado. Su implementación abre una conexión `Mode=ReadOnly` y una transacción `BeginTransaction(deferred: true)`. El callback recibe el mismo contrato `ILocalStore`, pero sólo los casos de uso de consulta lo usan. `SalesHistoryService` es el único consumidor inicial.

`ILocalUnitOfWork.Run` no cambia: mantiene conexión normal y transacción inmediata para todas las operaciones que escriben. No se altera la serialización de caja, checkout ni catálogo.

Listado ejecuta dentro de una lectura: un `COUNT` con filtro y una página de encabezados con joins. Detalle ejecuta una consulta de encabezado/pago/usuario/terminal y otra de partidas por `position`. El orden es `confirmed_utc DESC, id DESC`; el tamaño queda fijo en 50.

## Consecuencias

Una lectura sostenida conserva su snapshot y no reserva el writer lock. La prueba de concurrencia mantiene abierto un historial y completa un Checkout real mediante otra conexión dentro del tiempo acotado. La ruta read-only impide convertir accidentalmente esa conexión en escritora.

El contrato `ILocalStore` contiene métodos de lectura y escritura; la protección efectiva para esta ruta es la conexión read-only. Una separación más granular de interfaces puede evaluarse cuando existan más módulos de consulta. SQLite sigue siendo local a una sola instalación y no se admite compartir el archivo por red.
