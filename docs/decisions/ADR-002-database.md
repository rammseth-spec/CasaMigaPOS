# ADR-002 — SQLite local

Estado: adoptado para la fundación.

SQLite en disco local con Microsoft.Data.Sqlite; `Foreign Keys=True` en cada conexión, WAL, `synchronous=FULL`, espera de bloqueo de 5 segundos y pooling deshabilitado por simplicidad de ciclo de vida. No alojar DB en SMB, OneDrive ni carpeta sincronizada. SQLite permite sólo un escritor; no se presenta como servidor multiterminal. [WAL oficial](https://www.sqlite.org/wal.html), [FK](https://www.sqlite.org/foreignkeys.html).

Migraciones SQL embebidas ordenadas, tabla de versiones con SHA-256 y transacción por actualización. Se rechaza una versión más nueva y un hash diferente. Antes de migrar producción se exigirá backup y mantenimiento exclusivo; la fundación sólo crea bases de desarrollo. No se admiten modificaciones manuales ni downgrade. Extender el runner con validación de secuencia y prueba de migración fallida antes de una segunda migración.

Se elige ADO.NET por controlar SQL y límites de transacción de una pieza pequeña. No se crea repositorio genérico universal. Casos de uso futuros tendrán puertos por operación, implementados en infraestructura. PostgreSQL requerirá un adaptador, migraciones específicas y pruebas de semántica, además de un servicio si hay varias terminales; no basta cambiar una cadena de conexión.

Backup mediante `BackupDatabase`; no copiar sólo el archivo mientras WAL esté activo. [Microsoft](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup).
