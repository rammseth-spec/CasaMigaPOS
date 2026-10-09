# FASE 1K.4D — Caja, nota de discrepancia e histórico

Fecha: 08/10/2026. Estado: técnicamente completada; smoke humano pendiente. La autorización estructural sustituye la detención de la auditoría previa. No hay autorización de push ni de fase siguiente.

## Contrato implementado

`CashSession.DiscrepancyNote` / `cash_session.discrepancy_note` es la fuente primaria de la nota. `CashSession.Close` normaliza con Trim, transforma whitespace-only a null, admite hasta 500 caracteres normalizados y exige una nota no vacía cuando contado − esperado es distinto de cero. `Restore` conserva cierres anteriores sin nota; no aplica retrospectivamente la nueva obligatoriedad.

La migración `017_cash_discrepancy_note.sql` agrega:

```sql
-- Existing closed sessions deliberately retain NULL; no financial data is rewritten.
ALTER TABLE cash_session ADD COLUMN discrepancy_note TEXT NULL
 CHECK(discrepancy_note IS NULL OR (length(discrepancy_note) BETWEEN 1 AND 500 AND discrepancy_note=trim(discrepancy_note)));
-- closed_no_update from 003 covers ALL columns, including discrepancy_note.
CREATE TRIGGER cash_close_note_required BEFORE UPDATE ON cash_session
WHEN OLD.state='OPEN' AND NEW.state='CLOSED' AND NEW.difference_cents<>0
 AND (NEW.discrepancy_note IS NULL OR length(trim(NEW.discrepancy_note))=0)
BEGIN SELECT RAISE(ABORT,'Cash discrepancy requires a note'); END;
```

Además crea `cash_close_note_required`, que rechaza la transición OPEN → CLOSED con diferencia y sin nota. `closed_no_update` de 003 ya protege TODAS las columnas, incluida la nueva; no se modifica ese trigger ni ninguna migración aplicada. La nota, estado, responsable, UTC, importes y auditoría se escriben en la misma transacción inmediata de `CashClosingService.Close`. El fallo de auditoría revierte el UPDATE completo. Conteo y cierre siguen siendo dos confirmaciones separadas.

Fórmula real conservada: **esperado = fondo inicial + SUM(payment.applied_cents de pagos CASH de esa sesión)**. Recibido y cambio no se suman otra vez. **Diferencia = contado − esperado**, usando Money/Int64 en centavos checked. No existen entradas, retiros o CashMovement y no se agregaron.

Casos probados con fondo $500 y aplicado CASH $1,250: esperado $1,750; contado $1,750 → sin diferencia; $1,780 → sobrante $30; $1,700 → faltante $50. Los dos últimos requieren nota. Para el desglose histórico, ventas = esperado persistido − fondo persistido; no se reconsulta ni recalcula el cierre a partir de ventas.

## Histórico y UI

`CashHistoryService` usa `ILocalReadUnitOfWork` y los nuevos contratos `CashClosureHistoryQuery`, `CashClosureHistoryRow`, `CashClosureHistoryPage`, `QueryCashClosures` y `FindCashClosure`. Revalida usuario activo y `cash.view` en cada consulta. No agrega permisos ni amplía `cash.close.other`. El terminal se fija al construir el servicio.

Sólo devuelve CLOSED de esa terminal. SQL obtiene el total real y hasta 25 filas por página, en la misma transacción de lectura. Orden: fecha real de cierre descendente (`julianday`), con ID como desempate determinista interno. Los joins resuelven caja, abridor, cerrador y contador; el fallback es «No disponible». No hay N+1 ni SQL en Presentation. Valores financieros históricos ausentes se muestran como «No disponible»; no se inventan ceros.

Caja muestra estado, terminal, responsable, apertura local y fondo inicial. Se preservan reglas de apertura. El esperado y diferencia sólo aparecen tras persistir el conteo ciego; una reanudación muestra el conteo ya confirmado e inmutable. El resumen previo al cierre muestra fondo + ventas = esperado, contado, diferencia y editor de nota. El botón de cierre definitivo se habilita cuando la nota requerida es válida. El servicio valida nuevamente, aunque se omita la UI.

Se preserva Busy contra reentrada, unicidad de sesión abierta, conteo único, UPDATE condicionado a OPEN, transacciones inmediatas, auditorías únicas y bloqueo de ventas posteriores al conteo. Cierres anteriores y detalle son de sólo lectura. Escape conserva prioridad de ayudas/popups y fullscreen, y después cierra el detalle de Caja. F11 permanece. No se implementan denominaciones.

## Validación y límites

- Baseline: `86a7d22a69e1e4b884acce02a2f17e4d315dc501`, master, Git limpio; identidad local Rammseth / rammstudioworkshop@gmail.com.
- Focales generales: 76/76; migración, Cash, histórico, permisos, atomicidad y concurrencia.
- Focales WPF: 3/3; flujo real de MainViewModel/MainWindow, conteo ciego, notas, cierre, detalle, permisos, 1280×820, 1920×1080, F11/Escape y ausencia de impresión.
- Suite completa: **757/757** (572 generales + 185 WPF/Presentation), 0 fallidas y 0 omitidas.
- Release identificado Phase1K4D: cero errores y cero advertencias.
- `git diff --check`: correcto durante revisión; se repite antes del commit.
- Pruebas existentes de schema actualizado pasan de esperar 16 a esperar 17; no se cambia su intención. Dos fixtures POS y dos escenarios de cierre reciben notas sintéticas explícitas para cumplir la regla nueva.
- Cambios limitados a contrato de Caja, migración 017, histórico, UI de Caja, pruebas, herramienta y documentación. No hay migración 018, cambios de ticket, fotos, inventario, recetas, producción, carrito o historial de ventas.

La nueva cobertura incluye upgrade real 16→17, preservación de cierre antiguo con diferencia y note NULL, notas normalizadas, límite, rechazadas, nota+auditoría atómicas, inmutabilidad SQL, doble cierre, carrera con dos identidades independientes, páginas 25+3, permiso cash.view de cajero y aislamiento por terminal. No se declara aprobación manual.

## Fixture y build de revisión

Raíz del repositorio: `C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1`.

- EXE: `src/CasaMiga.Presentation/bin/Phase1K4D/CasaMiga.Presentation.exe`.
- Launcher: `work/Abrir-Casa-Miga-1K4D.cmd`.
- DataDirectory: `work/phase1k4d-smoke-20261008`.
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.4D`.
- El launcher limpia CASAMIGA_DATA_DIR en su entorno, fija DOTNET_ROOT local y ejecuta ese EXE. `development.settings.json` apunta exclusivamente a la copia.
- Fixture: schema 17, 30 sesiones cerradas, 28 ventas sintéticas nuevas, sin sesión abierta. Histórico permite páginas 25+5.
- Las credenciales proporcionadas por el usuario se verificaron en la copia; no se almacenan aquí.
- Base protegida: `work/phase1k2c-r2-smoke-20260928/casamiga.db`, schema 16. SHA-256 inicial/final `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
- Configuración de impresión copiada byte a byte, SHA-256 `88843EAF9F452FD94C7CDEA77A71657923DD3E123D082DB9EEEDEB9277464461`; openCashDrawer=false. Ninguna llamada al transporte ni hardware.
- Inventario: 1 operación/1 movimiento antes y después. Producción: 0 antes y después. Impresión: 17 solicitudes/34 eventos antes y después. Son registros heredados de la copia; no se crearon trabajos.

Capturas WPF y logs: `work/phase1k4d-evidence/`. Manifiesto de fixture: `work/phase1k4d-smoke-20261008/cash-fixture-manifest.json`. El manifiesto final local vincula hashes de fuentes, binarios, launcher y commit; los binarios fueron compilados antes del commit autorizado. No confundir metadata del commit padre con fuentes antiguas.

## Smoke manual pendiente

1. Abrir sólo el launcher 1K4D e iniciar sesión con la cuenta de revisión entregada en la conversación.
2. Caja: comprobar CAJA CERRADA y 30 cierres; avanzar a página 2 y volver. Abrir detalles con esperado $1,750, conteo $1,750/$1,780/$1,700; revisar nota y responsable. No hay edición.
3. Abrir una sesión sintética con fondo 500.00. Ver fondo, responsable y fecha. Iniciar cierre: esperado y diferencia deben seguir ocultos.
4. Contar 500.00 y confirmar conteo. Esta sesión nueva no tiene ventas: fondo $500 + ventas $0 = esperado $500; «Sin diferencia». Cerrar sin nota.
5. Abrir otra sesión con fondo 500.00, contar 530.00: sobrante $30. El cierre exige nota; escribir una observación sintética y confirmar.
6. En una tercera sesión con fondo 500.00, contar 450.00: faltante $50; escribir nota y cerrar. Son escenarios independientes; un conteo confirmado no se puede reescribir.
7. Revisar los tres cierres y sus notas en el histórico. Verificar F11 y Escape, 1280×820 y 1920×1080.
8. No vender desde UI ni imprimir ni usar cajón, inventario o producción. Operar Caja únicamente en esta copia. Las ventas de $1,250 ya están preparadas en los históricos por tooling.

## Cierre Git

Un único commit local autorizado: `feat: complete cash closing and history for phase 1K.4D`. El SHA final se entrega en conversación y manifiesto local después de crearlo. No push ni PR. GitHub permanece pendiente de aprobación humana de 1K.4D. No iniciar la fase siguiente.

## Archivos del checkpoint

- `docs/CURRENT_STATE.md`
- `docs/evidence/phase1k4d-verification.md`
- `docs/NEXT_STEPS.md`
- `docs/PHASE_1K4D_REPORT.md`
- `migrations/017_cash_discrepancy_note.sql`
- `src/CasaMiga.Application/CashClosingService.cs`
- `src/CasaMiga.Application/CashHistoryService.cs`
- `src/CasaMiga.Application/LocalPorts.cs`
- `src/CasaMiga.Domain/CashSession.cs`
- `src/CasaMiga.Infrastructure/SqliteCashHistoryStore.cs`
- `src/CasaMiga.Infrastructure/SqliteLocalUnitOfWork.cs`
- `src/CasaMiga.Presentation/CashHistoryView.xaml`
- `src/CasaMiga.Presentation/CashHistoryView.xaml.cs`
- `src/CasaMiga.Presentation/MainViewModel.Cash.cs`
- `src/CasaMiga.Presentation/MainViewModel.cs`
- `src/CasaMiga.Presentation/MainWindow.xaml`
- `src/CasaMiga.Presentation/MainWindow.xaml.cs`
- `tests/CasaMiga.PhotoTests/CashUsabilityTests.cs`
- `tests/CasaMiga.PhotoTests/ProductPhotoTests.cs`
- `tests/CasaMiga.Tests/CashClosingTests.cs`
- `tests/CasaMiga.Tests/CashIdentityTests.cs`
- `tests/CasaMiga.Tests/CashManagementTests.cs`
- `tests/CasaMiga.Tests/FoundationTests.cs`
- `tests/CasaMiga.Tests/InventoryNoteMigrationTests.cs`
- `tests/CasaMiga.Tests/InventoryPersistenceTests.cs`
- `tests/CasaMiga.Tests/Phase1J5HardeningTests.cs`
- `tests/CasaMiga.Tests/PosTests.cs`
- `tests/CasaMiga.Tests/ProductionPermissionMigrationTests.cs`
- `tests/CasaMiga.Tests/ReceiptTests.cs`
- `tests/CasaMiga.Tests/RecipePersistenceTests.cs`
- `tests/CasaMiga.Tests/RecipeUpgradeTests.cs`
- `tests/CasaMiga.Tests/SaleInventoryMigrationTests.cs`
- `tests/CasaMiga.Tests/SalesHistoryTests.cs`
- `tests/CasaMiga.Tests/ZeroOutputMigrationTests.cs`
- `tools/CasaMiga.Phase1K4DSmoke/FixtureTool.csproj`
- `tools/CasaMiga.Phase1K4DSmoke/packages.lock.json`
- `tools/CasaMiga.Phase1K4DSmoke/Program.cs`
- `tools/CasaMiga.Phase1K4DSmoke/README.md`
