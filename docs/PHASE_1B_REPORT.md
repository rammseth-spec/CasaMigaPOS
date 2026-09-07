# Reporte final — FASE 1B

Casa Miga POS, 2026-09-06. Incremento completado; detenerse antes de FASE 1C.

## Resumen

Se completó el ciclo apertura → conteo confirmado → resultado → cierre auditado → nueva sesión. Se implementó respaldo completo con identidad terminal y restauración comprobada. No se añadieron ventas, catálogo UI, pagos, impresión, cajón, inventario, movimientos de caja ni reportes generales.

## Cierre de caja

CashSession.Close transforma sólo OPEN, conserva identidad, fondo, propietario y apertura; añade cerrador, UTC, esperado, contado y diferencia. Montos de contado/esperado no negativos e Int64 checked. No hay setters ni reapertura. Tras cerrar puede abrirse otra sesión, con otro ID. Cerrar aplicación o salir del usuario nunca cierra caja automáticamente.

Esperado = fondo + CASH_IN − CASH_OUT + ventas en efectivo − devoluciones en efectivo. Todos los componentes excepto fondo valen cero en 1B; no se simulan ventas. El cálculo está concentrado en Application dentro de la transacción. Diferencia = contado − esperado; cero exacto, positivo sobrante, negativo faltante. Antes de incorporar movimientos se deberá congelar actividad desde conteo o verificar versión del cálculo al confirmar; no requiere cambiar la representación del aggregate.

## Conteo ciego

La pantalla de conteo no muestra esperado, diferencia ni fondo. Como esperado=fondo en esta fase, también se oculta el fondo de la vista de caja abierta: se prioriza la prohibición de revelación indirecta frente al ejemplo de pantalla que pedía mostrarlo. Después de confirmar el conteo aparecen todos los valores.

Continuar guarda cash_count y auditoría antes de mostrar resultado. Volver/Escape retorna a caja abierta pero conserva el conteo; reiniciar tampoco permite editarlo. Retomar devuelve resultado guardado. Confirmar cierre es una segunda acción distinta; Enter en el campo de contado no cierra definitivamente. No hay corrección posterior de conteo en 1B. El usuario puede recordar el fondo que él mismo capturó al abrir; la UI no puede impedir ese conocimiento previo.

## Permisos

Propietario requiere cash.close. Tercero requiere cash.close y cash.close.other; este último se asigna a Administrator/Manager, nunca a Cashier. Se preserva la matriz de 1A: Cashier sólo open/view por defecto; no se amplía automáticamente su autorización de cierre. Los checks revalidan usuario activo y permisos en Application al retomar, contar y cerrar, no sólo en botones.

Un tercero sin permiso recibe mensaje claro; no existe bypass ni forzar cierre. Un tercero autorizado ve la advertencia de sesión ajena. ClosedByOtherUser se deriva de opened_by != closed_by, sin columna redundante. Auditoría conserva los tres actores relevantes: propietario, quien contó y quien cerró.

## Base de datos

Nueva migración 003_cash_closing.sql: permiso cash.close.other; cash_count con FK, conteo no negativo, una declaración por sesión y sólo sobre OPEN; triggers impiden editar/borrar conteos, modificar/borrar sesiones CLOSED y cerrar con importes ausentes o diferencia incoherente. Índices únicos para cash.count/cash.close audit por sesión. Sigue vigente el índice de una sola OPEN por terminal y el trigger de 002 contra reapertura.

Migraciones 001 y 002 idénticas byte por byte a 1A. Probadas base vacía y actualización desde v2 con sesión abierta, seguida de cierre válido. No se fabrican datos financieros de históricos preexistentes.

## Transacciones

SubmitCount: autorizar → validar OPEN/monto → insertar conteo → auditoría → commit. Si falla auditoría no queda conteo que bloquee un reintento.

Close: autorizar nuevamente → comprobar OPEN → cargar conteo confirmado → calcular esperado → Domain.Close → UPDATE WHERE state='OPEN' → auditoría → commit. Error de auditoría revierte estado y todas las columnas financieras; el conteo previo permanece como operación ya confirmada. Transacciones SQLite inmediatas y UPDATE condicionado garantizan un solo cierre bajo concurrencia; probado con conexiones reales.

## Auditoría

cash.count registra sesión, actor, terminal, UTC y contado. cash.close registra entidad, terminal, propietario, cerrador, contador/fecha de conteo, fecha final, fondo, esperado, contado, diferencia y indicador explícito de cierre por tercero. Entradas append-only mediante triggers. Logs técnicos separados, sin contraseñas ni hashes; registran arranque, conteo, cierre y códigos/tipos mínimos de rechazo/error. No existe resistencia frente a administrador que modifica el archivo/esquema.

## Backup

InstallationBackup crea .cmbackup con casamiga.db (snapshot de SQLite Backup API), terminal.json y manifest.json (versión, terminal ID y SHA-256 de los dos archivos). Nombres permitidos fijos, sin extracción arbitraria. Creación bajo DirectoryLease, salida .partial y rename final. La primera prueba detectó archivos auxiliares SQLite extra; se corrigió archivando explícitamente los tres componentes.

Restore a directorio nuevo: hashes, integridad/FK, correspondencia terminal/base, versión e historial de migraciones; publica carpeta sólo después de validar. No sobrescribe ni activa sobre instalación existente. El arranque crea backups completos antes/después de migración cuando hay configuración. El .db legado previo sin configuración sigue siendo sólo instantánea de base. Si falta terminal.json y ya existe terminal en DB, se rechaza el arranque para no generar otra identidad silenciosamente.

Prueba automatizada realista: crear terminal e histórico CLOSED → empaquetar → conservar backup fuera de instalación → eliminar entorno sintético original → restaurar → verificar terminal ID y estado/importes históricos. Paquete alterado y destino existente se rechazan. Además, CLI real backup y restore-installation sobre el smoke: terminal, cuatro sesiones CLOSED, conteos y auditoría idénticos al origen.

Los paquetes no están cifrados ni firmados. Hashes detectan corrupción, no autenticidad frente a quien reemplaza el manifiesto. Logs y configuración de desarrollo no forman parte del paquete; la identidad terminal sí. El comando legado restore <db> <archivo nuevo> no constituye restauración completa. Procedimiento en operations.md y decisión en ADR-007.

## Tests

Baseline antes de código: 30/30 aprobadas tras clean, restore y Release. Suite final: 49 totales, 49 aprobadas, 0 fallidas, 0 omitidas; 19 casos añadidos y 30 anteriores conservados. Fixtures/versiones de esquema anteriores adaptados a 003, sin eliminar casos.

Cobertura nueva: exacto/sobrante/faltante/contado cero; negativo y cierre sin conteo rechazados; conteo inmutable/reinicio; permisos propios y ajenos revalidados; tercero auditado; inactivo/sin login; timestamp determinista; rollback de conteo/cierre; doble cierre y concurrencia; persistencia cerrada e inmutabilidad SQL; máximo monetario; backup restaurado tras pérdida de origen; paquete alterado; configuración perdida y upgrade v2.

## Build

Clean y restore completados; build Release: 0 errores y 0 advertencias del compilador. scripts/verify.ps1 ejecutado con restore locked-mode. Único aviso del runner: sobrescritura del TRX previo, sin efecto en resultados. SDK local 10.0.400, runtime 10.0.11; sin dependencias nuevas. Publicación self-contained win-x64 sólo para smoke. Lockfiles regresados al contexto normal y restore locked-mode comprobado después de publicar; versiones iguales a 1A.

## Smoke test

Todos los datos son sintéticos en work/ui-smoke-visible; cuenta de prueba y contraseña efímera de 1A, sin secretos en Git. Casos visuales con WPF real:

| Caso | Fondo | Contado | Resultado |
|---|---:|---:|---|
| A | $500.25 | $500.25 | Diferencia $0.00, cierre correcto. Se reutilizó sesión abierta de 1A, validando además upgrade real. |
| B | $0.00 | $1.00 | SOBRANTE $1.00, cierre correcto. Nueva sesión. |
| C | $10.00 | $9.00 | FALTANTE $1.00, cierre correcto. Nueva sesión. |
| D | $500.25 | $500.25 | Nueva apertura, cerrar app, reabrir/login, recuperar misma hora/identidad/fondo, cerrar, reabrir/login: CAJA CERRADA. |

Estado final leído de DB: cuatro CLOSED, cero OPEN, cuatro cash.close audit; diferencias [0,100,-100,0] centavos. Caso D conserva apertura 06/09/2026 03:48:44 -06:00 tras reinicio. Comparación programática valida ID/terminal/propietario/UTC/fondo; restauración CLI conserva los datos completos. 18 eventos técnicos acumulados del smoke (incluye 1A), sin errores. App quedó cerrada.

## UI

Verificados vista abierta sin importes, conteo vacío sin esperado, resultado y confirmación separada; ratón, Tab, Enter, Escape/Volver, foco visible, botones grandes, contraste y estado Procesando/disabled. Se corrigió mensaje exitoso de cierre a color de éxito. Sin dependencia de hover. Táctil físico pendiente conforme al encargo. No se añadió logo inventado.

## Git

Rama master sin commits, archivos sin seguimiento en el destino nuevo; no se inventó autor ni se publicó remoto. Comparación contra snapshot del estado aprobado de 1A; ninguna fuente previa eliminada, migraciones 001/002 intactas y sin whitespace final en fuentes/documentación.

Principales cambios: CashSession.Close, CashClosingService, puertos/store SQLite, migración 003, InstallationBackup, runtime/configuración, Maintenance, MainViewModel y pantallas WPF, CashClosingTests y fixtures vigentes. Documentación estado/siguientes pasos/modelo/operación/roadmap actualizada. Exclusiones nuevas para .cmbackup, terminal.json, locks y logs; work/artifacts/TestResults siguen ignorados. Inventario en FILES.md y comparación en evidence/phase1b-source-review.json.

## Riesgos

No hay corrección auditada de conteo confirmado, administración de usuarios ni recuperación de acceso. Falta zona comercial configurable, retención/segunda copia/cifrado, activación administrativa de restauraciones, firma e instalador. Logging posterior al commit es best effort. No se probaron corte eléctrico, disco lleno, hardware ni pantalla táctil física. No apto aún para operación comercial: sigue sin ventas/pagos.

Los backups heredados .db no recuperan terminal. El paquete completo tiene integridad comprobable, pero debe protegerse con controles de acceso. No usar simultáneamente original y restaurada como cajas independientes. La política de congelar nuevos movimientos tras contar deberá incorporarse antes de añadir CASH_IN/OUT o ventas.

## Decisiones

ADR-007 documenta conteo durable y paquete de instalación. Sin autorizaciones pendientes para concluir 1B. Se priorizó conteo ciego sobre mostrar fondo antes de contar, dejando explícita esa resolución. No se asignó licencia del producto, identidad Git ni política nueva de close a Cashier.

## Próximo paso

Proponer 1C pequeña: catálogo mínimo administrable con categorías, productos, precio exacto/estado activo, permisos y persistencia; sin carrito, ventas, pagos, hardware ni inventario todavía. Requiere nueva autorización. Detener aquí.

## Evidencia persistente

- evidence/phase1b-baseline-clean.txt y phase1b-baseline.txt.
- evidence/phase1b-clean.txt, phase1b-restore.txt, phase1b-verification.txt y phase1b-publish.txt.
- evidence/phase1b-smoke-and-restore.json, phase1b-logs.json y phase1b-cli.txt.
- evidence/phase1b-source-review.json.
