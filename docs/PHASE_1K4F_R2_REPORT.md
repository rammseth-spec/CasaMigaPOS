# CASA MIGA POS — FASE 1K.4F-R2 — BLOQUE A

Fecha: 09/10/2026. Modelo usado: GPT-6. HEAD inicial: `918f24fcf241c186ecd806baa56ebbea47ce5b3c`. Estado: implementación y validación técnica del bloque A completadas; build conjunto y smoke humano pendientes hasta concluir el bloque B.

## Resultado

El header conserva título, subtítulo, usuario y terminal a la izquierda. A la derecha presenta `Caja abierta`/`Caja cerrada`, un resumen del Centro operativo sólo cuando existe actividad, el reloj con fecha y F11. La fuente del estado de caja es `MainViewModel.HasOpenCash`, derivada de la sesión consultada mediante `CashService.Current` y refrescada por los flujos reales de apertura/cierre. No se agregó estado local ni lógica financiera.

El resumen operativo queda separado del reloj, es también acceso al panel y conserva la alerta breve sobre su propia superficie. Sin actividad desaparece por completo. Sus prioridades son urgente, listo y activo; las combinaciones sin urgencia muestran listo y activo, mientras urgencia toma precedencia. El reloj conserva `HH:mm`, fecha `ddd · d MMM` y abre el mismo panel aprobado en R1. F11 se compactó a `F11`, mantiene 40 DIP de alto y expone ayuda/nombre accesible; dentro de pantalla completa la ayuda cambia a «Salir de pantalla completa · F11».

La altura y padding vertical del header no cambiaron: el ajuste aprovecha el espacio horizontal sin comprimir título, subtítulo, chips o targets táctiles. El Centro operativo R1, sus formularios, persistencia, snooze, alertas, sonido y atención de taskbar no cambiaron funcionalmente.

## Validación del bloque

- Caja cerrada y caja abierta real: verificadas.
- Sin timers: resumen oculto.
- Activo, listo, urgente y combinación: verificados.
- Click en reloj: abre el Centro operativo.
- F11: conserva entrada y salida.
- 1280×820, 1920×1080 y 3440×1440: cuatro elementos visibles, dentro del header y sin clipping/overlap.
- Regresión de refresco: un tick conserva las mismas instancias de POS/Catálogo y no emite notificación global del shell.
- Focales: 19/19 de Centro operativo/modelo/WPF y 2/2 del shell; total 21/21, 0 fallidas, 0 omitidas.
- Domain, Application, Infrastructure, SQLite y migraciones: sin cambios en este bloque.
- Push/PR: ninguno.

Commit local requerido: `fix: refine operational header for phase 1K.4F`.
