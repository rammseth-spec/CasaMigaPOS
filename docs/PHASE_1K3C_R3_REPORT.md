# FASE 1K.3C-R3 — hardening final de ContextHelp

Fecha: 06/10/2026

Estado: **técnicamente completada; smoke final corto pendiente**

Modelo utilizado: Codex, GPT-6

HEAD inicial: `0f0f634d1585f072d03e7370ef3d4068635cda65`

Git inicial: limpio

## Diagnóstico y solución

R2 mostraba `ContextHelp` mediante `Popup`. En WPF, un `Popup` se presenta en una ventana nativa separada del árbol visual del propietario. Aunque R2 corregía el movimiento del formulario y Escape, esa separación permitía que la superficie conservara visibilidad/z-order al minimizar o cambiar a otra aplicación.

R3 reemplaza el `Popup` por un `Adorner` montado en el `AdornerLayer` del contenido de la ventana. La tarjeta pertenece al mismo árbol visual, `PresentationSource` y HWND que `MainWindow`; no existe una ventana auxiliar capaz de permanecer sobre una aplicación externa. Al cerrar, la tarjeta se retira del layer y se desacopla del `VisualCollection`, lo que permite reapertura limpia sin conservar un padre visual anterior.

El control cierra la ayuda en estos casos:

- Escape desde el botón, la tarjeta o un control vecino;
- segundo clic en el mismo botón `?`;
- minimización;
- pérdida de activación, incluido Alt+Tab;
- ocultamiento/descarga de la vista por navegación o salida de sesión;
- cierre de la ventana;
- cambio de tamaño o transición F11, para evitar coordenadas obsoletas.

Restaurar o reactivar conserva la ayuda cerrada. El usuario puede abrirla nuevamente y Escape vuelve a funcionar. `MainWindow` cierra cualquier ayuda antes de alternar fullscreen. Se mantienen el ancho máximo de 520 DIP, Wrap, margen del viewport, colocación arriba cuando no cabe debajo, prioridad de Escape, contenido aprobado y asociaciones visuales de Recetas/Perfiles.

## Pruebas

La regresión WPF `ContextHelpDoesNotRemainVisibleWhenOwnerWindowLosesActivationOrIsMinimized` usa ventanas reales A/B y el botón real. Verifica:

- mismo HWND para ventana y tarjeta;
- no layout shift;
- 1280×820 y 1920×1080;
- minimizar, ocultamiento inmediato, restaurar y permanencia cerrada;
- desactivar mediante otra Window, reactivar y permanencia cerrada;
- reapertura manual y Escape;
- ocultamiento/navegación, cambio de tamaño/F11 y cierre del host.

Resultados:

- focal directa de R3: **11/11**, 0 fallidas, 0 omitidas;
- focal amplia ContextHelp/MainWindow/RecipeView/ProductProfile/Escape/fullscreen/minimize/deactivate/navigation/layout: **46/46**, 0 fallidas, 0 omitidas;
- suite completa: **716/716** = 558 generales + 158 WPF, 0 fallidas, 0 omitidas;
- Release general: 0 errores, 0 advertencias;
- build identificado `Phase1K3C-R3`: 0 errores, 0 advertencias;
- `git diff --check`: correcto.

## Datos y build de revisión

- Base protegida: `work/phase1k2c-r2-smoke-20260928/casamiga.db`.
- SHA-256 inicial y final esperado: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
- Copia R3: `work/phase1k3c-r3-smoke-20261006`.
- SHA-256 de la copia antes/después de la validación de credencial: `6FE685D35E812BC85E953603A2C758E05ECA577D6707D972C052AC9005C73DDB`.
- Integridad: `ok`; claves foráneas: 0; schema: 16.
- Fixture: 2 fórmulas sintéticas, 2 publicadas, 1 borrador, 2 activas; 1 perfil con versión publicada/borrador y activa; 4 alternativas, 4 preparaciones y 2 ingredientes directos; 0 saldos/movimientos `SMOKE-*`.
- Credencial de revisión verificada en lectura sobre la copia exacta; el hash no cambió.
- EXE: `src/CasaMiga.Presentation/bin/Phase1K3C-R3/CasaMiga.Presentation.exe`.
- SHA-256 del EXE: `CCDB2E3D44F8DC7550FE28267C7930CED8709D27E4030201673B9C978EE57517`.
- Launcher: `work/Abrir-Casa-Miga-1K3C-R3.cmd`.
- DataDirectory: `work/phase1k3c-r3-smoke-20261006`.
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R3`.

La configuración local de impresión se conservó (POS-80, 48 columnas, ibm850, tabla 2, corte Partial, cajón desactivado), pero no se envió ningún trabajo. No se utilizaron datos reales ni se ejecutaron operaciones comerciales.

## Alcance versionado

- `src/CasaMiga.Presentation/ContextHelp.xaml`
- `src/CasaMiga.Presentation/ContextHelp.xaml.cs`
- `src/CasaMiga.Presentation/MainWindow.xaml.cs`
- `tests/CasaMiga.PhotoTests/ContextHelpLifecyclePresentationTests.cs`
- adaptación de pruebas existentes de ContextHelp en Inventario, Recetas, Escape y usabilidad;
- `docs/CURRENT_STATE.md`
- `docs/NEXT_STEPS.md`
- este reporte.

No cambiaron Domain, Application, Infrastructure, SQLite ni migraciones. Schema 16 permanece intacto y no existe migración 017. Los textos de ayuda, filtros, dimensiones, unidades, políticas, alternativas, versionado, producción, ventas, caja, inventario y configuración física permanecen sin cambios funcionales. No se inició 1K.3D ni Pedidos.

## Smoke final corto

1. Login → Recetas → `SMOKE-REC-001` → Versión 2 → Editar borrador.
2. Abrir Rendimiento; confirmar overlay y ausencia de layout shift; Escape cierra.
3. F11 → abrir Rendimiento → Escape cierra y conserva fullscreen.
4. Abrir de nuevo → Alt+Tab → la ayuda desaparece; volver y confirmar que sigue cerrada.
5. Abrir de nuevo → minimizar/restaurar → confirmar que sigue cerrada.
6. Revisar Ingredientes directos o Política de cantidad y cancelar sin guardar.

Si este recorrido pasa, FASE 1K.3C puede cerrarse formalmente. Las credenciales se entregan sólo en la conversación, no en documentación versionada.
