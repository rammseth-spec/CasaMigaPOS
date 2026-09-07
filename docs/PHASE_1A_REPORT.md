# Reporte final — Casa Miga POS FASE 1A

Fecha: 2026-09-06. Alcance completado; detenerse antes de FASE 1B. Código en C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1.

## Resumen

Implementados bootstrap controlado del primer administrador, login local, roles/permisos, terminal estable, apertura de caja, auditoría atómica y recuperación tras reinicio. UI WPF cálida con identidad textual temporal. No se añadieron ventas, pagos, productos en UI, inventario, hardware ni cierre.

## Arquitectura

Domain incorpora CashSession inmutable y conserva Money. Application incorpora IdentityService, CashService, UserSession, IClock, IPasswords y puertos de almacenamiento. Infrastructure implementa unidad de trabajo SQLite, PasswordHasher, reloj, configuración de terminal y bloqueo cooperativo por directorio. Presentation usa MainViewModel y recursos compartidos; code-behind gestiona eventos, foco y PasswordBox. No se añadió framework MVVM ni servidor web.

## Base de datos

Nueva migración 002_identity_cash.sql: roles, permisos, asignaciones, usuarios, cash_session y audit_entry. FKs, tablas STRICT, checks monetarios/estado, índices y triggers. La 001 quedó idéntica byte por byte. Probadas creación desde vacío y actualización desde v1 preservando datos. El runner exige secuencia contigua y hashes originales; arranque respalda antes de migrar y después de validar.

## Seguridad

PasswordHasher de Microsoft.Extensions.Identity.Core 10.0.11 (MIT), IdentityV3: PBKDF2-HMAC-SHA512 con 210000 iteraciones, sal aleatoria de 128 bits y subclave de 256 bits. API oficial para verificar y renovar hash. Contraseña inicial elegida por usuario (12–128 caracteres), sin predeterminada; bootstrap único con auditoría. Cinco fallos bloquean cuenta un minuto; respuesta genérica y dummy hash para usuario inexistente. Sesión autenticada sólo en memoria.

Application consulta usuario activo y permiso dentro de cada operación protegida. Administrator y Manager reciben los cuatro permisos; Cashier recibe cash.open y cash.view. cash.close y cash.manual_open_future son sólo datos reservados; no ejecutan cierre ni hardware. No hay editor de usuarios ni recuperación de contraseña. ADR-006 recoge fuentes, licencia y límites.

## Caja

Cada sesión guarda GUID, terminal, usuario de apertura, UTC, fondo Int64 en centavos y estado. Cero permitido; negativo rechazado por dominio. Campos de cierre/futuro esperado/contado/diferencia preparados y nullable. Índice UNIQUE por terminal WHERE state='OPEN' garantiza una sola apertura vigente, también con escritores concurrentes. Trigger rechaza CLOSED→OPEN; el aggregate no ofrece setters/reapertura. Otras terminales se prueban sólo como identidades independientes en almacenamiento, sin red ni modo multiterminal.

## Transacciones

Apertura: permiso/cuenta, terminal, sesión existente, dominio, INSERT sesión, INSERT auditoría y commit en una transacción inmediata. Cualquier fallo crítico revierte todo. Prueba con fallo inyectado de auditoría deja cero sesiones parciales. Bootstrap une usuario/auditoría. Login confirma bloqueo/rehash antes de establecer identidad en memoria. Logs técnicos posteriores al commit son best effort; no revierten una apertura ya confirmada.

## UI

Pantallas: configuración inicial, login, caja cerrada, captura de fondo con cancelar/confirmar y caja abierta. Muestra propietario, terminal, hora con offset y fondo MXN. Botones/entradas >=52 DIP, recursos centralizados, foco visible, Tab/Enter y scroll. Placeholder textual identificado; estructura Assets preparada para logo oficial. Se corrigió contraste de etiquetas de botones durante el smoke. Validación visual con ratón/teclado; panel táctil físico pendiente.

## Tests

Baseline previo: 11/11 correctas. Final: 30/30 correctas, 0 fallidas, 0 omitidas. Cobertura: apertura válida/cero/negativa, permisos revocados, usuario inactivo/no autenticado, terminal desconocida/diferente, duplicado, índice SQL directo, escritores concurrentes, rollback, timestamp determinista, reinicio lógico, hashing/login/bloqueo, bootstrap único, auditoría inmutable y upgrade. Se mantienen pruebas de dinero, migraciones, integridad y backups de FASE 0.

## Build

Ejecutados clean Release, restore desde paquetes oficiales, scripts/verify.ps1 con restore locked-mode, build Release y suite completa: 0 errores y 0 advertencias del compilador. El test runner avisó únicamente que sobrescribía el TRX previo; no afecta resultados. SDK local 10.0.400 y runtime 10.0.11. Publicación self-contained win-x64 ejecutada para smoke, sin instalador definitivo. Archivos bloqueados de un publish previo se resolvieron cerrando la aplicación; no quedó fallo pendiente.

Evidencia: evidence/phase1a-baseline.txt, phase1a-clean.txt, phase1a-restore.txt y phase1a-verification.txt. Los 36 paquetes resueltos tienen inventario de licencias en phase1a-dependency-licenses.json. Feed oficial NuGet consultado en phase1a-nuget-audit.json: sólo avisos de Newtonsoft.Json y SQLitePCLRaw cuyos rangos no incluyen las versiones resueltas. No es garantía absoluta de seguridad.

## Smoke test

Datos aislados de desarrollo, usuario sintético smoke-admin y contraseña aleatoria efímera no registrada. Bootstrap → login → CAJA CERRADA → fondo -1.00 rechazado → cancelar → abrir con $500.25 → CAJA ABIERTA → cerrar ventana normalmente → abrir aplicación → login requerido → misma CAJA ABIERTA.

Hora visible antes/después: 05/09/2026 22:20:54 -06:00. UTC persistido 2026-09-06T04:20:54.3983689+00:00. Comparación de base tras cierre/reinicio: misma sesión, usuario, terminal, fondo 50025 centavos y auditoría; una sola cash.open. Cuatro eventos técnicos revisados sin errores/contraseñas/hashes. Evidencia: phase1a-restart.json y phase1a-logs.json. La aplicación quedó cerrada al terminar. No se simuló corte eléctrico ni fallo físico.

## Git

Revisados fuentes nuevas/cambiadas contra los 57 archivos indexados de FASE 0: ningún archivo perdido, migración 001 intacta y sin whitespace final en fuentes/documentación. Principales cambios: CashSession, IdentityService, CashService, puertos locales, SqliteLocalUnitOfWork, LocalPasswords, TerminalConfiguration, runner/runtime, Maintenance, WPF/tema/MainViewModel, migración 002, CashIdentityTests, lockfiles y documentación. Inventario y comparación en FILES.md y evidence/phase1a-source-review.json.

El destino tenía .git nuevo: se conservó. Rama master sin commits; archivos sin seguimiento en este repositorio. No se creó commit, no se inventó nombre/email y no hay publicación remota. .gitignore excluye bin/obj, artifacts, TestResults, work, bases y development.settings.json. Las referencias históricas a main/staging en el informe FASE 0 pertenecen al repositorio anterior.

## Riesgos

- terminal.json está fuera del backup SQLite. Conservarlo junto a la base; perderlo puede crear otra identidad y ocultar la sesión asociada a la anterior. Falta restauración administrativa integrada.
- Base/backups sin cifrar, sin protección contra administrador Windows; requerirá ACL, segunda copia y política de retención.
- No hay gestión/recuperación de usuarios, expiración por inactividad ni MFA. Medir latencia de hash en equipo real.
- Zona de UI tomada del sistema con offset visible; falta zona comercial configurable para futuros reportes.
- Logs sin rotación/retención ni alerta por fallos posteriores al commit; bloqueo de directorio cooperativo, no distribuido.
- Pendientes pruebas táctiles físicas, rendimiento, cortes eléctricos, disco lleno, instalador/firma y restauración de producción. La publicación de desarrollo no constituye un POS listo para operación real.

## Decisiones

ADR-006 adoptado dentro de la autorización de 1A; no requiere otro permiso para entregar este incremento. No se eligió licencia del producto ni identidad Git. Para 1B deberán acordarse conteo ciego/visible y quién puede cerrar sesiones de otros usuarios. No se implementaron esas políticas por anticipado.

## Próximo paso

Proponer FASE 1B de cierre básico sin ventas/hardware: cash.close, conteo, esperado igual al fondo mientras no existen movimientos, diferencia, cierre/auditoría atómicos, historial mínimo y nueva apertura posterior. Probar doble cierre, permisos, concurrencia, rollback y reinicio. Esperar autorización antes de iniciar.
