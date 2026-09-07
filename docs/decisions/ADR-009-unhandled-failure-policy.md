# ADR-009 — Errores no controlados en WPF

Estado: implementado para FASE 1D. Investigación oficial: 2026-09-07.

## Política

Registrar diagnóstico mínimo y terminar. No se intenta convertir un fallo desconocido en una operación exitosa ni guardar un carrito como recuperación financiera.

DispatcherUnhandledException cubre el hilo UI principal; se registra, deshabilita ventana, muestra mensaje fijo con referencia de incidente y termina con código 1. Handled=true sólo reemplaza el procesamiento/diálogo predeterminado; no hay retorno a operación normal. Environment.Exit evita que la confirmación ordinaria de cierre cancele una salida fatal. No se cierra caja ni se generan ventas al salir.

AppDomain.CurrentDomain.UnhandledException registra best effort y deja terminar al runtime; no espera al Dispatcher ni adquiere locks de negocio. Puede ejecutarse en un hilo con locks retenidos. No se promete capturar stack overflow, access violation, pérdida de energía o fallo antes de registrar handlers.

TaskScheduler.UnobservedTaskException no provoca terminación por defecto en .NET moderno. Casa Miga adopta política más estricta: registrar y terminar con código 1 sin esperar a UI. La notificación depende del GC y no sustituye await/catch en tareas normales. No se instala configuración heredada de .NET Framework ni se usa SetObserved para continuar silenciosamente.

Los errores de arranque capturados se registran como Startup y terminan con aviso. Validaciones de negocio conocidas siguen mostrando mensajes recuperables en sus casos de uso. Los handlers no escriben datos comerciales, no crean backups de último momento y no intentan reparar la base.

## Datos técnicos y privacidad

Archivos únicos diagnostics/failure-ID.json: ID aleatorio, UTC, origen, Terminate=true, tipo de excepción, HResult, hasta 16 nombres de métodos/tipos de la traza (sin paths/argumentos) y hasta cuatro tipos internos. Sin Message, ToString, Data, parámetros, nombre de usuario, contraseña, hash ni contenido del carrito. Se usa un archivo por incidente para no compartir locks con el log ordinario.

Si falla el registro se mantiene la terminación. Antes de leer configuración se usa la carpeta diagnóstica predeterminada; después, la de la instalación. No hay garantía de escritura ante disco lleno, permisos, memoria agotada o fallo del sistema. Una referencia emitida no certifica que el archivo se haya escrito. No se añade telemetría remota.

## Verificación

Cinco casos prueban los cuatro orígenes, política de terminación, redacción por lista de campos y fallo de sink. Prueba WPF publicada con DiagnosticCrashOnStartup=true en configuración de desarrollo aislada: excepción deliberada, diálogo comprensible, aplicación deshabilitada, registro Dispatcher con métodos sin mensaje secreto y cierre del proceso al aceptar. No se corrompieron datos.

DiagnosticCrashOnStartup sólo se ejecuta cuando IsDevelopment=true. Esta configuración ignorada en Git no se distribuye como producción. El error se provoca mediante Dispatcher.BeginInvoke después de inicializar, sin catch local, para recorrer el handler real.

## Fuentes oficiales

- [WPF DispatcherUnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-10.0): ámbito de hilo UI y comportamiento Handled.
- [AppDomain.UnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception?view=net-10.0): notificación, terminación y riesgo de locks.
- [TaskScheduler.UnobservedTaskException](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?view=net-10.0): tarea no observada y comportamiento predeterminado moderno.
