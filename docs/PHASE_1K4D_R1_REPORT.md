# FASE 1K.4D-R1 — Pulido visual final de Caja

Fecha: 08/10/2026. Estado: implementación visual y verificación técnica completadas; smoke visual pendiente. No hay autorización de push, PR o fase siguiente.

## Alcance y baseline

Baseline `7a2bb9f3e52ad90cb5118e0f27ee2259b49b1ef9`, rama `master`, Git limpio. La funcionalidad 1K.4D ya fue aprobada manualmente. R1 modifica exclusivamente Presentation, pruebas WPF/Presentation y documentación. Domain, Application, Infrastructure, SQLite, migraciones, permisos, fórmulas, concurrencia, impresión y cajón quedan fuera de alcance.

## Histórico

Antes, `CashHistoryView` usaba un `StackPanel` y un `DataGrid Height="210"`; el scroll general del shell impedía aprovechar el alto restante. Ahora el dashboard Caja tiene un panel propio que ocupa el área operativa y el histórico usa un `Grid` con filas `Auto / Auto / * / Auto`. La tabla crece en `*`, usa fila de 36 DIP y encabezado de 40 DIP; el footer permanece en la última fila.

Las pruebas de layout exigen al menos siete filas visibles a 1280×820 y nueve a 1920×1080, ausencia de scroll horizontal y footer dentro del viewport. La consulta permanece en 25 registros por página. En página 2 con tres registros de prueba, la tabla conserva el alto y deja espacio libre sin estirar filas.

## Detalle histórico

Antes, el detalle era un único texto multilínea dentro de una tarjeta vertical y usaba «Cerrar detalle». Ahora contiene tres bloques paralelos: Identificación, Resumen financiero y Nota. Los valores, nombres humanos, signos y fallback «Sin nota registrada» se conservan. La acción se llama «Volver» y ejecuta exactamente `HideCashClosure`; Escape conserva la misma ruta.

## Resultado de caja

El resumen financiero está contenido en una superficie compacta y mantiene Caja, apertura, fondo, ventas CASH aplicadas, esperado, contado y diferencia. La advertencia de cierre definitivo y las acciones permanecen visibles.

Con diferencia cero se colapsa por completo el editor de nota y aparece «Sin diferencia. No se requiere nota». `CanConfirmClose` conserva su comportamiento y permite cerrar sin nota. Con diferencia distinta de cero siguen visibles «Nota de discrepancia · obligatoria», la indicación para habilitar el cierre, textarea, `0/500 caracteres` y botón deshabilitado hasta recibir texto válido. No cambió ninguna regla de negocio.

El conteo continúa siendo ciego: antes de confirmarlo no se muestran esperado, ventas ni diferencia. Los textos humanos permanecen: «Sin diferencia · $0.00», «Sobrante de $30.00 (+)» y «Faltante de $50.00 (−)».

## Pruebas y evidencia

- Focales `CashUsabilityTests`: 5/5. Cubren 1280×820, 1920×1080, tabla dinámica, pie fijo, páginas 1/2 y 2/2, detalle compacto, «Volver», notas históricas presentes/ausentes, resultado exacto sin editor, diferencia con editor obligatorio, contador, habilitación, conteo ciego, F11 y Escape.
- Suite completa `scripts/verify.ps1`: 759/759 (572 generales + 187 WPF/Presentation), 0 fallidas y 0 omitidas.
- Release identificado Phase1K4D-R1: 0 errores y 0 advertencias.
- `git diff --check`: correcto antes del commit.
- Capturas locales: `work/phase1k4d-r1-evidence/`.

Una regeneración adicional de capturas, posterior a la suite, fue anulada por el runner antes de ejecutar pruebas: `vstest` no conectó con `testhost` en 90 ni 180 segundos. No produjo evidencia nueva ni invalida las corridas focal 5/5 y completa 759/759 ya concluidas; no quedaron procesos de prueba activos.

## Datos y seguridad

La base protegida `work/phase1k2c-r2-smoke-20260928/casamiga.db` inició con SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, schema 16. La copia nueva `work/phase1k4d-r1-smoke-20261008` migró aisladamente a schema 17 y contiene 30 cierres, 28 ventas sintéticas preparadas por tooling y ninguna sesión abierta. Inventario, producción y registros de impresión conservaron sus conteos. `printing.json` se copió byte a byte, conserva `openCashDrawer=false`; no se enviaron trabajos ni pulsos.

Migración 017 permanece byte-idéntica al baseline y no existe 018. La contraseña de revisión fue validada en la copia y no se almacena en documentación versionada.

## Build de revisión

- EXE: `src/CasaMiga.Presentation/bin/Phase1K4D-R1/CasaMiga.Presentation.exe`.
- Launcher: `work/Abrir-Casa-Miga-1K4D-R1.cmd`.
- DataDirectory: `work/phase1k4d-r1-smoke-20261008`.
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.4D-R1`.

El launcher limpia `CASAMIGA_DATA_DIR` sólo en su proceso, usa el runtime local y ejecuta el EXE R1. Después de pruebas, documentación, commit local y Git limpio se abre automáticamente y queda en login, sin escribir credenciales ni operar.

## Smoke manual corto

1. Login; Caja cerrada; revisar altura y páginas 1/2 y 2/2.
2. Abrir un cierre, revisar los tres bloques y volver.
3. Abrir una sesión sintética; confirmar conteo exacto y ausencia del editor grande.
4. Revisar un escenario con diferencia: nota visible, cierre deshabilitado sin nota y habilitado con nota válida.
5. Revisar F11 y Escape. No imprimir ni accionar cajón.

Checkpoint local previsto: `fix: polish cash management usability for phase 1K.4D`. GitHub queda pendiente del smoke y aprobación explícita. No iniciar la fase siguiente.
