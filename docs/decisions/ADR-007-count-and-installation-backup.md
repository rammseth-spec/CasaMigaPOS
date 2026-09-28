# ADR-007 — Conteo confirmado y respaldo de instalación

Estado: adoptado en FASE 1B, 2026-09-06.

Actualización 1K.2B, 28/09/2026: las decisiones de conteo permanecen intactas. El contrato de backup de tres archivos descrito abajo es v1 histórico. Para schema 16 se adopta v2: manifiesto con lista de archivos, tamaños, SHA-256 y roles; DB/terminal y fotos originales/derivadas **activas** exclusivamente. Gate de media → snapshot SQLite → archivos referenciados por ese snapshot → ZIP temporal → validación real del paquete → publicación final. Retirados y huérfanos quedan excluidos, aunque la metadata/audit histórica sigue en SQLite.

Restore conserva compatibilidad v1, valida v2 y publica únicamente a carpeta nueva. Rutas permitidas cerradas, límites de entrada/paquete, duplicados incluso por mayúsculas rechazados; no usa extracción genérica. Media ausente/alterada falla; no existe modo emergency/degraded en esta versión. Ver `../product-photos.md` y `../PHASE_1K2B_REPORT.md`.

El conteo ciego es un registro durable e inmutable separado del cierre definitivo. Continuar guarda cash_count y su auditoría antes de revelar importes; volver o reiniciar conserva ese conteo. Confirmar cierre realiza una segunda transacción. No aceptar un monto diferente enviado desde la UI después de revelar el esperado. Una corrección futura necesitará un flujo explícito y auditado; no se añade en 1B.

En esta fase esperado = fondo. Por prioridad del requisito de no revelar indirectamente el esperado, la vista de caja abierta y el formulario de conteo ocultan el fondo; aparece en el resultado después del conteo. El operador puede recordar el fondo que él mismo capturó al abrir; ningún software puede evitar ese conocimiento previo. No existe un historial visible que permita consultar importes durante el conteo.

Backup completo: un paquete .cmbackup ZIP con nombres fijos casamiga.db, terminal.json y manifest.json. La base es una instantánea de SQLite Backup API, no una copia arbitraria de archivos WAL. La configuración conserva su separación de la información transaccional. Manifiesto versión 1 con identidad terminal y SHA-256 de base/configuración; detecta corrupción accidental, no autentica frente a un atacante que reemplaza también el manifiesto.

Creación bajo DirectoryLease; publicación por rename del archivo .partial. Restauración extrae sólo tres nombres permitidos en carpeta temporal hermana, valida hashes, integridad, FK, correspondencia terminal/base y migraciones soportadas; después publica el directorio completo por rename. Nunca sobrescribe una instalación existente. No incorpora rutas externas, logs, contraseñas en texto ni configuración de desarrollo. La base sí contiene hashes de credenciales y debe protegerse igual que la instalación.

Los .db históricos se conservan como instantáneas incompletas de base. Cuando una base heredada no tiene configuración, puede generarse uno antes de migrar como protección; no se considera backup completo ni soluciona identidad perdida. Si existe terminal en base pero falta terminal.json, el arranque falla y exige restaurar; no crea silenciosamente otra identidad.
