# FASE 1K.3C-R2 — ContextHelp flotante y Escape real

Fecha: 05/10/2026
Estado: **técnicamente completada; smoke visual/manual pendiente**
Modelo utilizado: Codex, GPT-6
HEAD inicial: `7da1d7cbe85b2449e426d6b5db4b23a2c8a2daf7`
Git inicial: limpio

## Diagnóstico

R1 usaba un `Expander`; al expandirse, su contenido formaba parte del árbol de medida y aumentaba la altura de la sección. Eso empujaba campos y tablas y añadía scroll. Su prueba de Escape elevaba el evento directamente sobre la raíz `ContextHelp`, con lo que ejecutaba el handler local sin reproducir el foco cotidiano ni el orden de `MainWindow`. En uso real, el foco permanecía en el botón o un editor cercano y el shell procesaba Escape antes de que esa raíz lo recibiera. Por eso la prueba anterior era un falso positivo.

## Solución

`ContextHelp` conserva el botón no modal y renderiza su contenido en un `Popup` WPF transparente. El Popup queda fuera del flujo de layout; abrirlo o cerrarlo no altera `ActualHeight` ni la posición Y de controles vecinos. La tarjeta mide su contenido con Wrap, utiliza hasta 520 DIP y baja hasta 280 DIP sólo cuando el viewport lo requiere. Su posición se calcula en DIP respecto de la ventana: se limita al margen derecho y, si no cabe abajo, se coloca arriba. Los cambios de tamaño, ubicación y F11 recalculan la posición.

Una lista de referencias débiles conserva la semántica de varias ayudas y permite cerrar primero la abierta más recientemente. Cada componente escucha `PreviewKeyDown` de su ventana incluso cuando el foco está en un control vecino; el contenido del Popup también recibe Escape en su árbol visual separado. `MainWindow` da prioridad a ContextHelp antes de desplegables, fullscreen y navegación. Cerrar desde el Popup devuelve foco al botón; desde otro control conserva el foco operativo.

Las ayudas de Ingredientes directos permanecen junto al título de la sección. Las ayudas de Política de cantidad están ahora dentro del encabezado de esa columna en ingredientes de receta, alternativas e ingredientes directos de perfil. Las etiquetas accesibles identifican Rendimiento, Alternativas, Preparaciones, Ingredientes directos, Política y Versiones. El texto operativo aprobado no cambió.

## Pruebas

- La prueba WPF materializa ventanas de 1280×820 y 1920×1080, abre con el botón real y cierra con el mismo botón.
- Escape real se eleva desde el botón, un `TextBox` vecino, el contenido del Popup y un editor real de RecipeView.
- Se confirma que RecipeView continúa en modo editor y que rendimiento/nombre del borrador permanecen intactos.
- MainWindow real confirma que Escape cierra ContextHelp antes de salir de F11 o abandonar el editor.
- Se comparan posición Y y altura del contenedor antes/después: no hay desplazamiento.
- Se comprueban ancho 420–520 cuando existe espacio, Wrap y límites superior, derecho e inferior del viewport.
- Se comprueba la asociación de dos ayudas de Ingredientes directos y tres encabezados de Política de cantidad sin duplicarlas junto al título.
- Focales: **46/46**, más repetición final de teclado/layout **3/3**; 0 fallidas, 0 omitidas.
- Suite completa: **713/713** = 558 generales + 155 WPF; 0 fallidas, 0 omitidas.
- Release general y build identificado R2: **0 errores, 0 advertencias**.
- `git diff --check`: correcto.

## Archivos versionados

- `src/CasaMiga.Presentation/ContextHelp.xaml`
- `src/CasaMiga.Presentation/ContextHelp.xaml.cs`
- `src/CasaMiga.Presentation/MainWindow.xaml.cs`
- `src/CasaMiga.Presentation/RecipeView.xaml`
- `tests/CasaMiga.PhotoTests/InventoryUsabilityPresentationTests.cs`
- `tests/CasaMiga.PhotoTests/PopupEscapePresentationTests.cs`
- `tests/CasaMiga.PhotoTests/RecipeUsabilityPresentationTests.cs`
- `tests/CasaMiga.PhotoTests/UsabilityPresentationTests.cs`
- `docs/CURRENT_STATE.md`
- `docs/NEXT_STEPS.md`
- `docs/PHASE_1K3C_R2_REPORT.md`

Domain, Application, Infrastructure, SQLite y migraciones no cambiaron. Schema 16; no existe migración 017. No se modificaron recetas/perfiles persistidos, filtros, dimensiones, unidades, políticas, alternativas, versionado, producción, ventas, caja, inventario o impresión.

## Datos y build de revisión

- Base protegida: `work/phase1k2c-r2-smoke-20260928/casamiga.db`.
- SHA-256 inicial: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
- Copia aislada R2: `work/phase1k3c-r2-smoke-20261005`, tomada de la fixture sintética R1 cerrada y verificada.
- Integridad `ok`, 0 claves foráneas inválidas, schema 16, recetas `SMOKE-*` publicadas/borrador, perfil sintético, alternativas, preparaciones y tres dimensiones; 0 saldos y 0 movimientos `SMOKE-*`.
- Credencial `catalog-admin` validada en lectura contra la copia exacta; hash de la copia idéntico antes/después.
- Impresión local conservada: POS-80, 48 columnas, ibm850, tabla 2, Partial, cajón desactivado. No se envió ningún trabajo.
- EXE: `src/CasaMiga.Presentation/bin/Phase1K3C-R2/CasaMiga.Presentation.exe`.
- Launcher: `work/Abrir-Casa-Miga-1K3C-R2.cmd`.
- Settings local: `src/CasaMiga.Presentation/bin/Phase1K3C-R2/development.settings.json` apunta exclusivamente a la copia R2 y activa desarrollo a 1280×820.
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R2`.
- El launcher limpia sólo la variable heredada en su proceso y apunta exactamente al EXE R2. No depende del acceso anclado.

## Checklist manual R2

1. Login → Recetas → `SMOKE-REC-001` → Versión 2 → Editar borrador.
2. Abrir Rendimiento; confirmar popover compacto sin movimiento y cerrarlo con Esc sin salir del editor.
3. Abrir Alternativas; confirmar que la tabla no se desplaza.
4. Revisar la asociación separada de Ingredientes directos y Política de cantidad; cancelar.
5. En Perfiles de producto, Versión 2, revisar Preparaciones e Ingredientes directos/Política; confirmar popover, Esc y posición; cancelar.

Si pasa este recorrido, 1K.3C puede cerrarse formalmente. No se inició 1K.3D ni Pedidos; no hubo migración 017, recetas reales, operaciones comerciales o impresión.
