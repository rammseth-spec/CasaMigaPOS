# CASA MIGA POS — FASE 1K.4F-R1 — REPORTE FINAL

Fecha: 09/10/2026. Modelo usado: GPT-6. Baseline y HEAD inicial: `68a16f170614ec2f88aa8b447d7aafb6613be46d`; Git inicialmente limpio. Estado: corrección técnica completada, smoke visual humano pendiente.

## Resultado

El Centro operativo muestra primero timers, notas y recordatorios. El editor de timer está colapsado por defecto; `+ Nuevo` lo abre inline, Cancelar lo cierra sin crear y una creación válida agrega la tarjeta y lo cierra. El editor de nota sigue la misma regla con `+ Nueva`; Editar reutiliza esa superficie y muestra claramente «Editar nota». Cancelar una edición no muta la nota y guardar la actualiza antes de volver a la lista.

Las tarjetas reducen padding de 12 a 10 DIP y separación de 8 a 7 DIP, conservando icono, nombre, tiempo/`¡Listo!`, estado y acciones con targets de 36 DIP. La prueba WPF a 1280×820 confirma al menos tres tarjetas de temporizador completas con los formularios cerrados; 1920×1080 y el ancho fijo de 420 DIP también quedan contenidos y sin scroll horizontal.

Los recordatorios listos muestran Atender, Posponer, Editar y Eliminar. Posponer revela dentro de la tarjeta únicamente 5, 10 y 15 minutos; elegir una opción la aplica, oculta el selector y actualiza el estado. Sólo un selector de posposición puede quedar abierto. No se introdujo Window, Popup ni diálogo externo.

Escape atiende primero editor de timer, editor de nota o selector de posposición; cuando no existe estado transitorio cierra el panel. F11 conserva su prioridad posterior. Alt+Tab y minimizar continúan cerrando el panel mediante `Deactivated`/`StateChanged`, por lo que no hay superficie huérfana. El estado del contenido persiste porque la vista y el ViewModel permanecen en el shell.

Las alertas no cambiaron: `¡Listo!`, doble pulso y micro-shake de 600 ms, `SystemSounds.Asterisk`, atención de taskbar sin restaurar ni robar foco y una sola alerta agrupada por tick. Se conservaron las 12 categorías de timer, ocho de nota y prioridades Normal/Urgente. Header D y Login quedaron intactos porque ya estaban aprobados y tocarlos introducía riesgo sin aportar al objetivo.

## Persistencia, backup y rendimiento

La ubicación sigue siendo `DataDirectory/operations-hub.json`. El probe sobre la copia R1 creó una nota, un recordatorio urgente y un timer activo de 45 minutos. Al recargar el mismo archivo un minuto después, el timer siguió Running con 2,640 segundos y deadline coherente; nota y recordatorio permanecieron. Los editores y las opciones Posponer son estado de UI y correctamente vuelven cerrados tras reiniciar.

El archivo operativo no forma parte del `.cmbackup`: el mecanismo vigente sólo incorpora DB, terminal y fotos activas. R1 no cambia backup/restore. Persiste la limitación documentada de que timers/notas no se trasladan con un respaldo de instalación.

Permanece un único `DispatcherTimer` de baja prioridad. No se agregó `PropertyChanged(null)`, escritura por segundo, refresco de Catálogo/POS, recarga de fotos ni recreación de Views. Guardado ocurre únicamente al mutar o transicionar a Ready.

## Validación

- Focales: **16/16** (11 de modelo/persistencia + cinco WPF), 0 fallidas/omitidas.
- Suite general: **576/576**.
- Suite WPF/Presentation: **203/203**.
- Total: **779/779**, 0 fallidas, 0 omitidas.
- `scripts/verify.ps1`: Release correcto, 0 errores y 0 advertencias.
- `git diff --check`: correcto.
- Base protegida inicial/final: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, idéntica.
- Schema protegido 16; fixture operacional 17; migración 018 no existe.
- Ticket 1K.4E-R1, renderer/printing, Qian, `printing.json`, ESC/POS y cajón intactos.
- No se imprimió, no se abrió cajón y no hubo venta, caja, inventario, receta o producción.

## Entrega

- EXE: `src/CasaMiga.Presentation/bin/Phase1K4F-R1/CasaMiga.Presentation.exe`.
- DLL emparejada: `src/CasaMiga.Presentation/bin/Phase1K4F-R1/CasaMiga.Presentation.dll`.
- Launcher: `work/Abrir-Casa-Miga-1K4F-R1.cmd`.
- DataDirectory: `work/phase1k4f-r1-smoke-20261009`.
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.4F-R1`.
- El launcher limpia sólo la variable heredada `CASAMIGA_DATA_DIR`, fija `DOTNET_ROOT` al runtime controlado de `work/.dotnet` y ejecuta exclusivamente el EXE de esta entrega.

Commit local autorizado: `fix: refine operations hub UX for phase 1K.4F`. No push ni PR. No se inició 1K.4G ni 1K.5.

## Checklist humano

1. Revisar login e iniciar sesión.
2. Abrir Centro operativo y confirmar ambos editores cerrados.
3. Probar `+ Nuevo`, Cancelar, crear timer y autocierre; crear tres timers y revisar densidad.
4. Terminar uno, revisar `¡Listo!`, Atender, Reiniciar y Eliminar.
5. Probar `+ Nueva`, Cancelar, crear y editar nota.
6. Crear recordatorio, dejarlo vencer, abrir Posponer, revisar 5/10/15 y elegir 5.
7. Revisar alerta conjunta, segundo plano, F11, Escape, Alt+Tab y minimizar/restaurar.
8. Cerrar y reabrir el mismo launcher; comprobar deadline, nota y recordatorio.
9. Observar parpadeos/CPU. No imprimir.

Riesgo pendiente: la aceptación visual y la prueba humana de persistencia siguen pendientes; abrir el build no equivale a aprobarlo.
