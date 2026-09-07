# Reporte de FASE 0 — Casa Miga POS

Se completó la fundación delimitada para desarrollo. No se avanzó a implementar el POS ni se certifica producción.

## Investigación y stack

Se compararon .NET/WPF y Tauri respecto a Windows 11, offline, SQLite, testing, acceso al hardware, instaladores, actualización, tamaño, mantenimiento, seguridad y crecimiento a PostgreSQL. WinUI se evaluó como alternativa Windows; WinForms, Avalonia y Electron se descartaron preliminarmente por ausencia de ventaja determinante para este alcance. No se hicieron benchmarks comparativos.

Recomendación adoptada: C# / .NET 10 LTS / WPF / Microsoft.Data.Sqlite / SQLite / MSTest. Favorece un lenguaje principal y el ecosistema Windows para una caja local. Tauri es viable y destaca en UI web y empaquetado, pero añade Rust/IPC/WebView2 sin necesidad multiplataforma. WPF no es la recomendación general de Microsoft para toda app nueva: la elección está justificada para este proyecto en ADR-001.

Fuentes oficiales principales:

- [Soporte .NET](https://dotnet.microsoft.com/en-us/platform/support/policy).
- [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/) y [elección Windows](https://learn.microsoft.com/en-us/windows/apps/get-started/).
- [Publicación .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/).
- [Instalador Windows Tauri](https://v2.tauri.app/distribute/windows-installer/), [SQL](https://v2.tauri.app/plugin/sql/) y [seguridad](https://v2.tauri.app/security/capabilities/).
- [Backup SQLite Microsoft](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup), [WAL](https://www.sqlite.org/wal.html) y [claves foráneas](https://www.sqlite.org/foreignkeys.html).
- [Winspool RAW](https://learn.microsoft.com/en-us/windows/win32/printdocs/writeprinter).
- [Ficha Qian con inconsistencia de modelo](https://fichas.qian.mx/QOP-T80UL-RI-02.pdf).
- Licencias/versiones de NuGet, Odoo y uniCenta: enlaces y resultados en research/ y docs/dependencies.md. Floreant devolvió mantenimiento; estudio interno pendiente.

## Arquitectura y archivos

Presentation → Application → Domain; Infrastructure implementa puertos y la composición de Presentation la conecta. Domain no depende de DB ni de dispositivos. La solución añade Maintenance para operaciones técnicas y Tests para reglas/integración.

Se crearon README, AGENTS, configuración Git/editor/SDK/NuGet, solución, seis proyectos, migración SQL 001, cinco ADRs, arquitectura/modelo/dominio/roadmap, operación/dependencias, estado/próximos pasos, investigación de referencias/Qian y evidencia. El requerimiento original se conserva íntegro en USER_REQUIREMENTS.md. Inventario exacto: FILES.md.

SQLite propuesto y creado: tablas STRICT de versiones, terminal, categoría y producto; FK, restricciones, índice categoría y SKU único opcional. Ventas, pagos, caja, producción y ledger están diseñados a nivel inicial, no implementados. Dinero en centavos Int64 con desbordamiento detectado. Costeo fraccionario e impuestos no se inventaron.

## Verificación

Restore con lockfiles correcto. Compilación Release de seis proyectos: **0 errores, 0 advertencias**. **11 tests pasan, 0 omitidos**. La advertencia del runner sobre sobrescribir un TRX previo sólo afecta el reporte de pruebas, no datos ni backups.

Se probaron exactitud/cambio/overflow monetario, restricciones/FK, migración repetida, hash alterado, esquema futuro, rollback de DDL y operación compuesta, respaldo de datos confirmados en WAL, conservación de backups previos, restauración a nuevo archivo, rechazo de backup corrupto y backup/log de arranque. SQLite cargado: 3.53.3, con prueba de versión mínima corregida.

La utilidad CLI además ejecutó init→backup→list→restore con dos copias distintas. No hubo pruebas visuales/táctiles, hardware, instalador, corte de energía o datos reales. La ventana WPF sí compila. Detalle reproducible en operations.md y evidence/.

## Riesgos y asuntos abiertos

- Qian: URL oficial T80UL y contenido T80BL; se necesita confirmar equipo y manual antes de implementar comandos.
- Impresión no es atómica con DB: diseño establece commit primero y resultados físicos desconocidos sin repetición automática de cajón.
- Backup inicial recuperable en pruebas, pero aún sin retención, segunda copia, manifiesto ni activación sobre producción.
- Faltan exclusión de instancias, autenticación/operaciones sensibles y prueba de instalador antes de una caja real.
- SDK ausente y Schannel fallando en el host: resuelto para compilar con SDK local verificado y paquetes NuGet oficiales locales. No se alteraron controles TLS. Auditoría de feed consultada separadamente.
- Git se inicializó en main y configuró localmente; no existe identidad del autor configurada, por lo que no se creó commit ni remoto.

## Autorización y siguiente tarea

No hay aprobación pendiente necesaria para esta fundación: la elección de stack y las piezas pequeñas estaban dentro del encargo. No se eligió licencia del producto, no se incorporó copyleft ni se contrató/publicó ningún servicio. Esas decisiones requerirían autorización si se proponen después.

Se requiere nueva instrucción para avanzar a FASE 1, como solicitaste. Recomiendo comenzar por apertura de caja e identidad/permisos mínimos con aceptación definida y pruebas, manteniendo el avance incremental. Las políticas fiscales y operativas deberán confirmarse antes de codificarlas.
