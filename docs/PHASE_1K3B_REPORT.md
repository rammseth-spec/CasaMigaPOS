# CASA MIGA POS — FASE 1K.3B

Fecha: 04/10/2026. Estado: **FASE 1K.3B técnicamente completada; smoke manual pendiente.** FASE 1K sigue abierta. 1K.3A y R1 fueron aprobadas manualmente por el usuario antes de autorizar esta subfase.

## Baseline y alcance

- Agente: Codex, basado en GPT-6. No se acredita un cambio de modelo o esfuerzo distinto del disponible en la sesión.
- HEAD inicial: `4f20e520d74c6cace8d13b2ad3b04f436fc6eb71`, `fix: reconcile phase 1K.3A manual smoke`.
- Git limpio al iniciar la subfase. Identidad local preservada: `Rammseth` / `rammstudioworkshop@gmail.com`.
- Alcance exclusivo: Presentation, pruebas y documentación de Inventario. No cambia Domain/Application/Infrastructure/SQLite, ni el contrato de las operaciones.

## Comportamiento implementado

1. **Búsqueda en vivo:** código/nombre mediante el contrato existente; debounce 250 ms, consultas en segundo plano y descarte de respuestas obsoletas. No requiere Enter ni Buscar. Los filtros también actualizan el listado. El selector existente y Buscar se conservan.
2. **Limpieza y selección:** × Limpiar aparece con texto, restaura lista y foco. La selección permanece sólo si el artículo sigue en la página. Los estados distinguen búsqueda en curso, sin coincidencias y filtros sin artículos. Volver tras cancelar Nuevo artículo resuelve la búsqueda pendiente; cancelar sin cambiar el filtro conserva paginación.
3. **Cantidades:** «Existencia al comenzar», «Cantidad recibida», «Cantidad de merma» y «Nueva existencia física». Campo y unidad identifican la operación sin «Cantidad absoluta».
4. **Equivalencias:** factor almacenado exacto; por ejemplo `1 Bulto 25 kg = 25 kg`. Presentaciones enteras, unidades g/kg, ml/L y pieza según dimensión. Ningún factor inventado.
5. **Vista previa:** existencia consultada, movimiento con signo y existencia resultante. Conteo muestra el total físico y la diferencia, incluyendo cero/sin cambio. Usa `InventoryCapture`, `InventoryQuantity` e `InventoryDelta` existentes, con decimal de captura y átomos Int64 checked; no float/double persistente.
6. **Validación:** no habilita Confirmar con cantidades vacías, negativas, fraccionarias incompatibles, desbordamientos, precisión inválida, merma superior a existencia o motivos inválidos. La validación autoritativa y atómica del servicio permanece. La preview es informativa y puede cambiar si el inventario cambia antes del guardado.
7. **Confirmaciones:** operación, movimiento firmado, existencia resultante y referencia real cuando existe. Se toma `PersistedInventoryOperation`, no el cálculo previo. La prueba concurrente verifica preview de 75 kg y resultado confirmado de 76 kg. Un refresh fallido tras commit no invita a repetir; resultado incierto conserva bloqueo/recuperación.
8. **Etiquetas:** «Existencia actual» sustituye Saldo MAIN; MAIN sigue interno. Historial: Operación, Movimiento, Existencia anterior/resultante. «Referencia / factura / nota (opcional)» conserva el campo actual; no crea datos nuevos.
9. **Asociaciones:** nombre del producto, SKU y cantidad/unidad. ProductId permanece interno para identidad/desvinculación; se consultan también productos inactivos. Si los permisos impiden obtener el nombre, se muestra «Producto no disponible» sin revelar GUID.
10. **Semántica de asociaciones:** materia prima puede asociarse según la regla existente; se explica que el descuento es directo, no una receta. Requiere producto con control de inventario y consumo activo. Asociar no cambia el flag; no se impone una restricción nueva a Producto terminado.
11. **Fechas:** `dd/MM/yyyy · HH:mm`, hora local; sin offset técnico. UTC y auditoría no cambian.
12. **Ayuda:** ContextHelp desplegable/cerrable para Saldo inicial, Entrada, Merma, Conteo físico y asociaciones. Mantiene jerarquía de artículo, existencia, presentaciones, acciones e historial.
13. **Botones:** selección/permisos/actividad/saldo/reglas reales controlan detalle, edición, vinculación, desvinculación y operaciones. Merma requiere saldo positivo; conteo bloquea inconsistencia. Se mantienen defensas del servicio, estilos disabled legibles, rueda/scroll y prioridad de Esc con popup.

## Archivos

Producto:

- `src/CasaMiga.Presentation/InventoryViewModel.cs`
- `src/CasaMiga.Presentation/InventoryOperationPreview.cs` (nuevo cálculo puro informativo)
- `src/CasaMiga.Presentation/InventoryView.xaml`
- `src/CasaMiga.Presentation/InventoryView.xaml.cs`

Pruebas:

- `tests/CasaMiga.Tests/InventoryViewModelTests.cs`
- `tests/CasaMiga.Tests/CasaMiga.Tests.csproj` (incluye el nuevo helper en las fuentes enlazadas)
- `tests/CasaMiga.PhotoTests/InventoryUsabilityPresentationTests.cs` (nuevo)
- `tests/CasaMiga.PhotoTests/UsabilityPresentationTests.cs` (literal del estado vacío sin búsqueda)

Documentación: `AGENTS.md`, `README.md`, `docs/CURRENT_STATE.md`, `docs/NEXT_STEPS.md`, este reporte y `docs/evidence/phase1k3b-*`. Launcher, settings, base, medios y artefactos locales continúan ignorados por Git.

## Verificación

- Focales definitivas: **142/142**, 107 generales + 35 WPF/Presentation; 0 fallidas/omitidas.
- Casos nuevos: 2 de ViewModel + 21 en WPF/preview. Búsqueda obsoleta, cancelación de debounce, restauración de paginación, WPF real, foco, ausencia de GUID, etiquetas, botones, ayudas, unidades y nueve comparaciones MASS/VOLUME/COUNT contra movimientos de servicios en bases temporales.
- Suite completa `scripts/verify.ps1`: **702/702** (558 generales + 144 fotos/WPF), **0 fallidas / 0 omitidas**. La recompilación final preserva DLL/PDB de Presentation y ambos proyectos de pruebas byte por byte; no se repite una suite idéntica sin causa. Evidencia: [verificación](evidence/phase1k3b-verification.md) y [manifiesto](evidence/phase1k3b-verification.json).
- Release: **0 errores / 0 advertencias**, build separado con `CasaMigaReviewBuild=1K.3B`.
- `git diff --check`: correcto durante la revisión; se repite antes del commit.
- Se corrigió un enlace faltante del helper en el proyecto de pruebas y se actualizó una expectativa literal coherente con el nuevo estado vacío. No se eliminaron verificaciones defensivas ni se relajaron assertions de visibilidad.
- Pruebas de MainWindow con tamaños solicitados 1280×820 y 1920×1080. Capturas pequeñas 1280×820; la captura grande final contiene realmente 1554×882 porque Windows limita la ventana según monitor/DPI. Las imágenes provienen del árbol WPF real, sin alterar bindings para forzar tamaño. No acredita 1920×1080 físico ni múltiples DPI; esa comprobación física queda en el smoke humano. Scroll horizontal/vertical permite acceder al contenido que excede el área disponible.
- Ledger, balance, permisos, crash de presentación, guardado confirmado/refresh fallido, resultado incierto, scroll, Esc/popup y fullscreen se incluyen en regresiones. Las operaciones persistidas de las pruebas usan exclusivamente fixtures temporales.

## Datos protegidos

Base original:

`C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\work\phase1k2c-r2-smoke-20260928\casamiga.db`

SHA-256 inicial y de control antes de la entrega:

`CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`

Se compara nuevamente después de abrir el launcher. Se copió la instalación cerrada, sin sidecars SQLite ni locks, con base/terminal/configuración/medios (59 archivos byte-idénticos). Consulta SQLite exclusivamente sobre copia y en modo lectura: Harina única, 50.000.000 mg = 50 kg; Bulto 25 kg activo, 25.000.000 mg; consumo automático DISABLED. La cuenta de revisión está activa, sin bloqueo y su contraseña fue comprobada offline sin iniciar sesión ni modificar auditoría/credenciales.

Schema de `schema_migration`: **16**. `PRAGMA user_version` no es el contrato de versión de esta aplicación. Migraciones 001–016 intactas y **sin 017**. Hash 016: `9223CAA06CD02FA32E874CC1516EE0C6DB798CB76E2FBE1BF267BAB9B695ED76`.

Impresión conservada byte por byte: POS-80, 48 columnas, ibm850, characterTable 2, corte Partial y openCashDrawer false. SHA-256 de printing.json: `88843EAF9F452FD94C7CDEA77A71657923DD3E123D082DB9EEEDEB9277464461`. Logo térmico Variante 04 intacto. **Cero trabajos enviados a la impresora.**

**Cero movimientos reales**: no se registraron entradas, mermas, conteos, saldos iniciales, asociaciones, ventas, cajas, recetas ni producciones sobre la instalación protegida ni sobre la copia destinada al usuario.

## Entrega de revisión

EXE:

`C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\src\CasaMiga.Presentation\bin\Phase1K3B\CasaMiga.Presentation.exe`

Launcher:

`C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\work\Abrir-Casa-Miga-1K3B.cmd`

DataDirectory:

`C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\work\phase1k3b-smoke-20261004`

Identificador de título incorporado para comprobar al abrir:

`Casa Miga POS · Caja · DESARROLLO · 1K.3B`

El launcher apunta al EXE anterior, limpia CASAMIGA_DATA_DIR sólo en su entorno y utiliza `development.settings.json` local con IsDevelopment=true y la copia aislada. No sobreescribe R1 ni depende del acceso anclado. Tras terminar validación, documentación y el commit autorizado, se ejecuta ese launcher y se verifica ruta de proceso/título/pantalla de acceso. No se automatiza login ni se escriben credenciales. La observación posterior al commit y el HEAD final se entregan en conversación y `work/phase1k3b-evidence/final-delivery.json` para evitar un SHA circular dentro de su propio commit.

Commit único autorizado: `feat: improve inventory usability for phase 1K.3B`. Sin push, remoto nuevo, cambio de identidad o reescritura de historial. El estado Git final debe quedar limpio; se verifica después del checkpoint.

## Checklist manual seguro

Usar la copia 1K.3B dejada en login y las credenciales incluidas en el reporte de conversación. **No pulsar Confirmar ni guardar durante este smoke.**

1. Inventario: escribir Harina o MP-HAR-001 sin Enter/Buscar; comprobar resultado automático, × Limpiar y foco. Buscar algo inexistente y comprobar el mensaje vacío.
2. Sin selección, Ver detalle deshabilitado; al seleccionar Harina se habilita. Abrir y verificar 50 kg y Bulto 25 kg activo, equivalencia completa. No repetir Saldo inicial.
3. Entrada: escribir 1 y elegir Bulto 25 kg; preview 50 kg / +25 kg / 75 kg. Ver ayuda y referencia opcional. **Cancelar**.
4. Merma: escribir 2 kg; preview 50 kg / −2 kg / 48 kg. Abrir/cerrar ayuda. **Cancelar**.
5. Conteo: escribir 48 kg; registrada 50, física 48, diferencia −2, resultante 48. Cantidad vacía/incorrecta deshabilita Confirmar. **Cancelar**.
6. Historial: fecha local sin offset, signo del movimiento, Existencia anterior/resultante. Desplazarse horizontalmente para las columnas finales.
7. Asociaciones: cabeceras Producto/SKU/Cantidad por venta; sin GUID visible. Harina no tiene asociaciones: conservar estado vacío, no crear una para probar. El caso poblado está cubierto por WPF temporal.
8. Comprobar ayudas discretas, estados/botones, scroll sobre tablas, rueda con ComboBox cerrado y Esc con popup abierto. Después F11/Esc normal. Salir de cualquier editor sin guardar.

Pendiente: aprobación humana de 1K.3B y comprobación física de DPI/touchpad. Preview depende de existencia consultada y el servicio vuelve a validar al confirmar. FASE 1K sigue abierta.

No 1K.3C, no 1K.3D, no Pedidos, no carga de recetas reales, no producción real, no ventas, no impresión, no migración 017 y no cambios fuera de alcance. Detenerse después de esta entrega.
