# Evidencia — FASE 1K.4F-R1

Fecha: 09/10/2026. Baseline: `68a16f170614ec2f88aa8b447d7aafb6613be46d`.

## Verificación funcional

- Focales OperationCenter: **16/16**, 0 fallidas, 0 omitidas: 11 de estado/persistencia y cinco WPF.
- Editores colapsados, apertura, cancelación, creación/guardado con autocierre, edición, Posponer transitorio y prioridad de Escape cubiertos.
- Panel dentro del árbol de `MainWindow`; cero `Popup` principal.
- Responsive comprobado a 1280×820 y 1920×1080; a 1280×820 caben al menos tres tarjetas de temporizador completas con editores cerrados.
- Suite general: **576/576**. Suite WPF/Presentation: **203/203**. Total: **779/779**, 0 fallidas, 0 omitidas.
- Release y build identificado `CasaMigaReviewBuild=1K.4F-R1`: 0 errores y 0 advertencias.

## Persistencia observada

Sobre `work/phase1k4f-r1-smoke-20261009` se creó con el contrato real una nota simple, un recordatorio urgente y un temporizador activo de 45 minutos. Una segunda instancia del ViewModel recargó el mismo `operations-hub.json` un minuto después: timer Running, 2,640 segundos restantes, nota presente y recordatorio presente. Esto confirma cálculo por deadline y no por decremento persistido. Navegar o cerrar el panel no elimina los objetos.

`operations-hub.json` no está incluido por `InstallationBackup`: `.cmbackup` conserva DB, terminal y fotos activas. R1 no cambia backup/restore; estos datos operativos locales deben considerarse fuera de respaldo.

## Integridad

- Base protegida antes/después: SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
- Schema protegido 16; fixture aislada 17; sin migración 018.
- Sin cambios en Domain, Application, Infrastructure, migraciones, ticket, renderer, Qian, ESC/POS, `printing.json` o cajón.
- Sin impresión, apertura de cajón ni operación comercial.
- Header, login, iconografía, prioridades y alerta agrupada permanecen bajo regresión.
