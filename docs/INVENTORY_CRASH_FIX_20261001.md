# CASA MIGA POS — corrección bloqueante de Inventario

Fecha local: 01/10/2026, America/Mexico_City. FASE 1K continúa abierta. Modelo de la sesión: Codex basado en GPT-6; no se afirma un cambio a Astra. Se utilizaron revisiones paralelas para contrastar WPF, pruebas y persistencia.

**Estado final: corrección de Inventario validada por el usuario.** El 01/10/2026 confirmó «todo correcto» y pidió el reporte. Esta aceptación corresponde al defecto corregido; no cierra FASE 1K ni equivale a aprobación global de R2 o del checkpoint físico Qian. La actualización posterior a esa aceptación es exclusivamente documental; no repite suites ni modifica código, pruebas o migraciones.

**Checkpoint Git autorizado, 01/10/2026.** El usuario confirmó expresamente detalle de Harina sin cierre, presentación Bulto 25 kg persistida y Activa, sin saldo inicial ni entradas/mermas/conteos/asociaciones. Se revisó el diff completo, incluidos archivos nuevos: todo corresponde al bugfix, manejo seguro, regresión, reproducción y documentación. Los hashes de los seis archivos de código/pruebas/probe coinciden con la versión verificada; suite 653/653 y Release 0/0 reutilizados. Un único commit `fix: prevent inventory detail crash after presentation load` incluye esta actualización, con padre `353c7e271dfbdfe66b2e2dc8bc6c28e46914a971`; su SHA final se informa en conversación. Sin push, migraciones ni operaciones comerciales. Es seguro reanudar la revisión manual en «Saldo inicial» de Harina con la copia R2 corregida. El estado sin commit descrito debajo corresponde a la entrega anterior a esta autorización.

## Base y alcance

HEAD inicial y final: `353c7e271dfbdfe66b2e2dc8bc6c28e46914a971`. La corrección queda en el árbol de trabajo, sin commit, push ni remoto nuevo. El estado inicial estaba limpio. Cambios de producto limitados a `InventoryView.xaml` e `InventoryViewModel.cs`, más pruebas, herramienta de reproducción y documentación. Domain, Application, Infrastructure, migraciones y reglas de negocio intactos. Schema final 16, ninguna migración 017.

## Causa demostrada y referencias

Ambos informes originales se localizaron en `work/phase1k2c-r2-smoke-20260928/diagnostics/`:

| Referencia | Timestamp UTC | Excepción registrada |
|---|---|---|
| `6f7cd578043e483cbc8bbcb033a0de3c` | 2026-10-01 06:11:58.2001037 | `System.InvalidOperationException`, HResult -2146233079 |
| `c51af0e89fc342c3971dab013731feb6` | 2026-10-01 06:17:31.6938625 | `System.InvalidOperationException`, HResult -2146233079 |

Los logs sanitizados registran tipo y frames; deliberadamente no guardan mensajes ni argumentos. El mensaje exacto se obtuvo reproduciendo WPF con la copia aislada y con las pruebas de regresión:

> Un enlace TwoWay u OneWayToSource no puede funcionar en la propiedad de sólo lectura "IsActive" de tipo "CasaMiga.Presentation.InventoryUnitView".

Stack común: `PropertyPathWorker.CheckReadOnly` → `ReplaceItem` → `BindingExpression.Activate` → `AttachToContext` → `DataBindEngine.Run/OnLayoutUpdated` → `ContextLayoutManager.UpdateLayout`.

Hay **una sola causa raíz**: la columna `DataGridCheckBoxColumn` «Activa» usaba `{Binding IsActive}` con modo predeterminado bidireccional. `InventoryUnitView.IsActive` sólo tiene getter. El `IsReadOnly` general del DataGrid no cambia el modo por defecto de `CheckBox.IsChecked`. La excepción aparece cuando WPF materializa la fila durante layout; ocurre después de los awaits y del catch del ViewModel. El handler global la registra y termina el proceso, conforme a su política de fallo fatal.

**Primer cierre:** `AddInventoryItemUnit` insertó presentación y auditoría dentro de `SqliteInventoryStore.Configure`, confirmó la transacción y retornó. La UI terminó su recarga y notificó. El layout posterior falló al materializar la casilla. Auditoría de presentación: 06:11:58.0932328; `inventory.ui.completed`: .1665686; fatal: .2001037. Por eso el guardado persistió.

**Segundo cierre:** la consulta leyó esa misma presentación existente y produjo el mismo binding al abrir detalle. `inventory.ui.completed` a 06:17:31.6785366 precede al fatal .6938625. No fue otra escritura ni una falla de conversión, asociaciones o movimientos.

## Datos y atomicidad

Se copió la base con la aplicación cerrada y WAL vacío a `work/inventory-crash-20261001/`. SHA-256 original y copia inicial: `BC58A1D3CA30B266657B42C236D8802BA05FA6B2DE74B77356BD771B9A44125F`. La base original no se abrió mediante herramientas de diagnóstico: consultas y login del probe se ejecutaron exclusivamente sobre la copia. Las pruebas de altas usan otros directorios temporales.

Evidencia detallada: `evidence/inventory-crash-20261001-data.json`.

- `MP-HAR-001`, Harina: exactamente una fila, materia prima, masa, activo. ID `31e0d651-1fa3-4314-a9a3-886f618a8750`.
- `HAR-BULTO-25KG`, Bulto 25 kg: exactamente una fila y ligada al artículo correcto. ID `3796dab2-9068-4602-bc70-bf3c3b90f059`.
- Contenido correcto: **25,000,000 mg atómicos = 25,000 g = 25 kg**. No se confundieron gramos con unidades atómicas.
- Flag persistido de presentación: **active=1**. La casilla aparentemente desmarcada era el binding incompleto antes del crash; no era una desactivación.
- Saldo **SIN INICIALIZAR**, no un saldo inicial de cero: no hay fila de balance ni apertura.
- Cero movimientos, cero asociaciones, cero duplicados y cero presentaciones huérfanas. Claves foráneas correctas. `Database.Validate()` también pasó en la copia con las funciones/collation de la aplicación.
- Consumo automático `DISABLED`, sin cambios.
- Comparación canónica de todas las filas/columnas entre respaldo anterior al primer cierre (00:20:48.181 UTC), respaldo entre cierres (06:15:37.672 UTC) y copia actual: tablas de caja, conteo, ventas, partidas, pagos, secuencia, snapshots/solicitudes/eventos de tickets, operaciones, movimientos, saldos y asociaciones idénticas. 15 ventas, 15 pagos, dos cajas históricas y una OPEN. No hay evidencia de mutación financiera por los cierres; la igualdad de tablas demuestra el mismo estado persistido durante esos checkpoints.

No se eliminó, recreó ni actualizó Harina o su presentación. No se inicializó saldo ni se activó consumo. No se enviaron trabajos a impresora.

## Corrección y recuperación

1. Binding de «Activa» explícito `Mode=OneWay`; columna explícitamente de sólo lectura. Conserva getter, dato y permisos existentes.
2. `InventoryViewModel` distingue el retorno confirmado del servicio respecto al refresco posterior. Si el refresco falla, informa **«Operación confirmada»**, retira el editor y pide consultar el artículo sin repetir el guardado.
3. Error de lectura sin escritura: elimina el detalle parcial, registra el tipo de excepción y permite volver a consultar. También captura fallos del refresco ejecutado después de un conflicto de concurrencia, que antes podían escapar desde el bloque catch.
4. Rechazo conocido de validación/negocio: conserva el flujo de corrección. Excepción inesperada durante una escritura, antes de un retorno confirmado: estado incierto, registra diagnóstico y bloquea acciones de Inventario. No hay retry automático; exige reiniciar y revisar persistencia antes de continuar. Salir/volver a entrar al módulo no borra ese bloqueo.
5. Un editor de alta de artículo/presentación ya finalizado no puede reenviar el guardado con otro clic tardío.

No se desactiva ni se convierte en un catch universal el handler fatal de la aplicación. Los errores de layout de programación no se ocultan: este binding se corrige y queda cubierto por materialización WPF real. Los diagnósticos recuperables usan evento y tipo, sin mensajes que puedan incluir datos sensibles.

Archivos modificados: `src/CasaMiga.Presentation/InventoryView.xaml`, `InventoryViewModel.cs`; `tests/CasaMiga.Tests/InventoryViewModelTests.cs`; `tools/CasaMiga.Phase1K2CSmoke/Program.cs`; `README.md`, `docs/CURRENT_STATE.md`, `docs/NEXT_STEPS.md`, `docs/FILES.md`. Nuevos: `tests/CasaMiga.PhotoTests/InventoryPresentationTests.cs`, `tools/CasaMiga.Phase1K2CSmoke/InventoryDetailProbe.cs`, este reporte y los dos archivos de evidencia `inventory-crash-20261001-data.json` / `inventory-crash-20261001-verification.md`. Son ocho archivos modificados y cinco nuevos, sin stage/commit.

## Verificación

Evidencia ejecutada y resultados finales: `evidence/inventory-crash-20261001-verification.md`.

Focal: **38/38** (35 ViewModel y 3 WPF). Suite completa obligatoria `scripts/verify.ps1`: **653/653** (545 generales + 108 fotos/Presentation), cero fallidas y cero omitidas. Se agregaron 12 casos. Restore locked correcto; Release **0 errores y 0 advertencias**; `git diff --check` correcto. Se revisó el diff y se confirmó ausencia de cambios en las tres capas de negocio y migraciones. El SHA-256 de 016 sigue siendo `9223CAA06CD02FA32E874CC1516EE0C6DB798CB76E2FBE1BF267BAB9B695ED76`.

- Antes del fix: tres casos WPF rojos (activa/inactiva, guardado seguido de layout), y reproducción idéntica al abrir Harina en la copia del dataset actual.
- Después: los tres casos WPF pasan, incluyendo captura de masa, presentación 25000 g, refresco, lista/detalle repetido, nueva conexión/sesión tras reinicio y rechazo de código duplicado. Se comprueba el valor real de `CheckBox.IsChecked` y se preserva toda la fila de caja abierta.
- Pruebas de ViewModel: lectura fallida recuperable, validación, duplicados, fallo de refresco después de commit, fallo antes/después de escritura con resultado incierto, bloqueo de reintentos y conflicto con segundo fallo de lectura. Diagnósticos sin datos sensibles.
- Probe de copia actual: seis aperturas en dos instancias del ViewModel, casilla marcada, 25 kg, cero movimientos/asociaciones y sin saldo inicial. Segundo proceso permite verificar persistencia entre ejecuciones.

## Reanudar y límites

Reanudar sólo con la compilación corregida. Abrir Inventario → buscar `MP-HAR-001` → Ver detalle. Confirmar `HAR-BULTO-25KG`, 25 kg y Activa marcada; volver a lista y abrir detalle otra vez. No volver a crear artículo/presentación ni registrar saldo inicial para verificar esta corrección. La cuenta de revisión se entrega en conversación, no se almacena en este documento.

Tras confirmar el usuario que cerró R2, se actualizó `src/CasaMiga.Presentation/bin/R2/CasaMiga.Presentation.exe` mediante build Release. La DLL Presentation coincide byte por byte con la verificada en `bin/InventoryFix`: SHA-256 `EE073CE0AD850FFF312E20D7B5BCD4A8B917AD723BB2F954E6A50BA58EC66CD6`. Su `development.settings.json` conserva la ruta original de revisión `work/phase1k2c-r2-smoke-20260928`. No se arrancó la aplicación sobre esa base desde herramientas durante esta corrección. Abrir esta copia R2, evitando otros ejecutables antiguos.

La corrección elimina el defecto reproducido y permite reanudar esa consulta con los binarios actualizados. La validación humana de esta corrección fue recibida el 01/10/2026 («todo correcto»). Se registra lo informado por el usuario, sin atribuir a herramientas observación de sus acciones manuales. El manejo de estado incierto es conservador: bloquea Inventario y exige revisión tras reinicio; no implementa una nueva pantalla de recuperación ni reparaciones automáticas. Otras excepciones no previstas aún conservan el cierre global seguro.

Pendientes UX, únicamente documentados: búsqueda en vivo, botón X, ayuda contextual ?, renombrar Saldo MAIN, cambios de etiquetas, estados vacíos y rediseño de formulario. No se implementó ninguno. Sin nueva fase, instalador, Recetas, Producción ni Pedidos. Próximo modelo recomendado: Astra para revisión técnica del cierre y sus límites; detenerse al entregar el reporte.
