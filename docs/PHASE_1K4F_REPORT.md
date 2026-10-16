# CASA MIGA POS — FASE 1K.4F — REPORTE TÉCNICO

Fecha: 09/10/2026. Estado: implementación técnica completada; smoke visual humano pendiente. Baseline: `f7e8912a4f88384c0f74545c72b8a1535ebfe5c9`, working tree inicialmente limpio.

## Alcance y decisión arquitectónica

La auditoría confirmó que `MainWindow` ya es el shell único, F11 pertenece a esa ventana y los módulos viven en el mismo árbol visual. Se añadió exclusivamente una superficie Presentation: no hay nueva Window, Popup principal, proceso, servicio en segundo plano ni acceso a SQLite. `Domain`, `Application`, `Infrastructure`, migraciones y esquema de negocio no cambiaron.

Los datos del Centro operativo se guardan en `DataDirectory/operations-hub.json`, contrato JSON versión 1 y reemplazo atómico mediante archivo temporal. Sobreviven navegación, cierre de sesión y reinicio de la aplicación. La auditoría de `InstallationBackup` confirma que el backup v2 permite `casamiga.db`, `terminal.json` y medios activos en `product-images`; `operations-hub.json` no forma parte del `.cmbackup` ni de restore. Incluirlo requeriría cambiar la arquitectura de backup, por lo que se documenta y difiere. No fue necesario escalar a migración 018 ni a cambios Application/Infrastructure.

## Header D y Login opción 3

El encabezado muestra `HH:mm` y fecha local abreviada sin segundos. Toda la superficie es un botón accesible «Centro operativo», junto a F11. Sin actividad queda limpia; con timers muestra cantidad y restante más cercano; con vencimientos combina de forma acotada activos/listos y prioriza el conteo urgente. Nunca expone nombres largos en el header. Usa un icono de reloj de la familia vectorial nueva y los recursos existentes `SurfaceBrush`, `BorderBrush`, `AvocadoLeafBrush`, `PrimaryBrush` y tipografía Segoe UI.

El Login opción 3 conserva logo, tarjeta y bloque institucional. Dentro de la tarjeta, los campos permanecen en la columna principal y un panel verde integrado a la derecha muestra únicamente hora y fecha. Antes de iniciar sesión no se renderizan nombres de timers, notas, recordatorios, conteos ni estados. El ancho de acceso permanece cómodo a 1280×820 y la composición queda centrada a 1920×1080.

## Panel 4, temporizadores y recordatorios

Panel 4 mide 420 DIP, se ancla a la derecha dentro del Grid raíz y comparte el HWND de `MainWindow`. El fondo modal y la superficie se cierran por botón, backdrop o Escape. F11 cierra primero el panel y luego cambia el modo; Deactivated y Minimized también lo cierran. Restaurar no crea superficies huérfanas.

Cada temporizador conserva duración original, remaining pausado, deadline UTC y estado Running/Paused/Ready. El restante se calcula contra el deadline; no existe `remaining--` como autoridad. Pausar captura el restante real, reanudar calcula un deadline nuevo, reiniciar vuelve a la duración original, finalizar muestra `¡Listo!` sin MessageBox y Atender lo deja pausado y listo para reutilizar. Se admiten 24 registros, muy por encima del mínimo de seis, y presets 5/10/15/30/45/60.

El selector compacto ofrece 12 categorías: Horno, Fermentación, Masa, Enfriado, Pedido, Entrega, Recordatorio, Reloj, Urgente, Compra/insumo, Limpieza y Café/bebida. Es una sola familia de línea limpia construida con `Geometry`/`Path` local, monocromática, sin emoji, fuente externa ni paquete de iconos. La categoría se persiste y aparece a la izquierda del nombre. Sólo existen prioridades Normal y Urgente; Urgente usa una etiqueta y borde de acento controlado, sin pintar de rojo la tarjeta completa.

Un solo `DispatcherTimer` de baja prioridad pertenece a `MainWindow` y alimenta reloj, timers y recordatorios. El tick normal no persiste datos, no emite `PropertyChanged(null)` y notifica sólo propiedades que cambiaron. No invoca `MainViewModel.Notify`, navegación, catálogo, POS, caché de fotos ni servicios. La persistencia sólo ocurre en mutaciones y transiciones a Ready.

Notas y recordatorios usan tarjetas compactas: texto de hasta 280 caracteres, recordatorio opcional `dd/MM/yyyy` + `HH:mm`, edición, eliminación y atención. Comparten iconografía General, Recordatorio, Pedido, Compra/insumo, Producción, Limpieza, Entrega y Urgente; General es el valor predeterminado. Sin fecha muestra «Sin recordatorio»; al vencer muestra «¡Recordatorio listo!» y alimenta el indicador del header. Editar conserva/cambia icono y prioridad. Un recordatorio listo puede posponerse exactamente 5, 10 o 15 minutos sin convertir la superficie en calendario.

## Estrategia de alerta profesional

Cada transición nueva a Ready dispara una sola alerta por tick compartido, incluso si varios timers o recordatorios vencen juntos. El indicador del Header D ejecuta dos pulsos de opacidad y un desplazamiento lateral máximo de 3 DIP durante **600 ms**; no bloquea el dispatcher, no sacude la ventana completa y no continúa animándose. La tarjeta permanece con fondo cálido, borde verde y texto `¡Listo!` hasta Atender o Posponer.

El sonido es `SystemSounds.Asterisk`: recurso local breve de Windows, sin descarga, archivo externo, loop ni dependencia nueva. Si la ventana está minimizada o sin foco, `FlashWindowEx` solicita tres destellos discretos únicamente en el botón de la barra de tareas (`FLASHW_TRAY`); no restaura, activa, trae al frente ni roba foco. La señal pendiente permanece en el header hasta atender. El mismo comportamiento es seguro en F11 y el panel continúa cerrándose en Alt+Tab/minimizar para evitar superficies huérfanas.

## Pruebas y seguridad

Las focales aprobaron **13/13**: reloj/formato local, header limpio/activo/listo/urgente y acotado, seis timers, deadline, pausa/reanudación/reinicio/atención/eliminación, persistencia de icono/prioridad, catálogo vectorial 12+8, recordatorios normal/urgente, Posponer 5/10/15, vencimientos simultáneos con una sola alerta, ausencia de loop, liberación del handler al cerrar, notificaciones nombradas, ausencia de escritura por segundo, panel in-tree, ausencia de Popup principal, microanimación no bloqueante, minimizado sin robo de foco, Escape/F11 y layout 1280×820/1920×1080.

La primera suite completa detectó una aserción histórica que contaba globalmente `Stretch="Uniform"` en `MainWindow`; el icono nuevo elevó el conteo sin afectar funcionalidad. El icono cuadrado pasó a `Stretch="Fill"`, preservando su proporción 1:1, y la regresión focal quedó verde. La ejecución final de `scripts/verify.ps1` aprobó **776/776**: 576 generales y 200 WPF/Presentation, 0 fallidas, 0 omitidas. Release identificado `CasaMigaReviewBuild=1K.4F`: 0 errores y 0 advertencias. La suite existente mantiene cobertura de POS, Caja, Catálogo, Inventario, Recetas, Producción, Historial, navegación, fullscreen y popups secundarios.

El impacto de rendimiento queda acotado a un único tick de baja prioridad. Un segundo inactivo no emite notificaciones; un segundo activo sólo actualiza el restante de timers y el header, no recrea catálogo, fotos, POS ni Views. Persistencia y sonido ocurren sólo al cambiar de estado. Las animaciones son dos timelines de 600 ms reemplazables y los handlers se desuscriben al cerrar la ventana, sin temporizadores o sonidos por alerta individual.

Base protegida inicial/final: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, schema 16. Fixture aislada: `work/phase1k4f-smoke-20261008`, schema 17 tras inicialización normal. No existe migración 018. No se envió impresión, no se abrió cajón y no se ejecutaron operaciones comerciales. Los renderers de ticket, Qian/POS-80, `printing.json`, Variante 04 y 1K.4E-R1 permanecen sin cambios.

## Entrega para revisión

- EXE: `src/CasaMiga.Presentation/bin/Phase1K4F/CasaMiga.Presentation.exe`.
- Launcher: `work/Abrir-Casa-Miga-1K4F.cmd`.
- DataDirectory: `work/phase1k4f-smoke-20261008`.
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.4F`.
- El launcher limpia sólo su `CASAMIGA_DATA_DIR` heredado y ejecuta exclusivamente `CasaMiga.Presentation.dll` 1K.4F mediante `work/.dotnet/dotnet.exe`; la ventana resultante corresponde al EXE de la misma carpeta. `development.settings.json` apunta a la fixture aislada.
- La aplicación se abre automáticamente al terminar el checkpoint y queda en Login, sin introducir credenciales.

## Riesgos y pendientes

El archivo operativo local no se incluye en backup/restore. La comprobación visual humana de transición, densidad, Alt+Tab, minimizar/restaurar y uso prolongado queda pendiente en el build entregado. No se declara aprobación humana con la apertura automática. No se inició 1K.5 y no se hizo push.

Checklist manual detallado: revisar login antes de autenticar; login; Header D; F11; abrir panel; timers simultáneos; pausa/reanudación; navegación; vencimiento e indicador; atender; notas; edición; recordatorio; eliminar; Alt+Tab; minimizar/restaurar; Escape; 1280×820; 1920×1080/F11; ausencia de parpadeo; cerrar sesión. No imprimir.
