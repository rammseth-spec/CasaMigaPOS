# Verificación independiente de QAv1 — 06/10/2026

Clon limpio de GitHub (`6ec695a`): restore `--locked-mode`, Release 0/0, **780/780** .NET y **6/6** API. Revisión de código independiente sin hallazgos críticos/altos. Corregidos: (1) devoluciones de ventas con tarjeta/transferencia indicaban «entrega en efectivo» — ahora la instrucción, nota y confirmaciones dependen del método; (2) respuestas no-JSON del servidor (502, portal Wi-Fi) se tratan como sin conexión y quedan registradas; (3) la sincronización corre fuera del hilo de la interfaz, su tarea de fondo no puede fallar sin control (política de cierre del POS) y el cierre ya no libera el token en uso; (4) la sucursal sólo se renombra si la nube acepta el registro. Riesgos bajos documentados en SYNC_PLAN.md. Suite **784/784** (625 + 159), Release 0/0.

# Instalador de Windows — 06/10/2026

**Implementado; sin firma.** Inno Setup 6: `scripts/build-installer.ps1` publica app y herramienta autocontenidas y genera `CasaMigaPOS-<versión>-setup.exe` + SHA-256. Instala en Program Files o por usuario, actualiza in situ, nunca toca `%LOCALAPPDATA%\CasaMigaPOS`. Probado: instalación/desinstalación silenciosa, herramienta funcional, datos conservados. `reset-admin` muestra el motivo real de rechazo. Ver [INSTALLER.md](INSTALLER.md). Suite **780/780**, Release 0/0.

# Sincronización en línea fases 2–3 — 06/10/2026

**Implementado; API sin desplegar.** Neon `casamiga-pos` con esquema central (`cloud/db/001_init.sql`). API TypeScript en `cloud/` (health, alta de terminal con secreto, recepción idempotente por lote con detección de conflictos), 6/6 pruebas `node:test`. En la app: envío automático en segundo plano, sólo marca lo confirmado, reintentos con espera creciente, llave DPAPI, pantalla Nube y estado en el encabezado. Bloqueo: el conector de Vercel no puede crear proyectos/despliegues en el equipo. Ver [SYNC_PLAN.md](SYNC_PLAN.md). Suite **780/780** (621 + 159), Release 0/0.

# Gestión de usuarios completa — 06/10/2026

**Implementado; smoke manual pendiente.** Pantalla Usuarios: editar nombre y rol (Encargado/Cajero), desactivar/reactivar (no al administrador ni a uno mismo), además de crear y restablecer contraseña. Recuperación del administrador único con `CasaMiga.Maintenance reset-admin <carpeta>`: respaldo y migración como al abrir la app, contraseña temporal mostrada una vez, cambio obligatorio, auditoría `identity.admin.recovered`. Sin migración. Suite **773/773** (614 + 159), Release 0/0.

# Formas de pago — 06/10/2026

**Implementado; smoke manual pendiente.** Migración 020: `payment` admite `CASH`, `CARD`, `TRANSFER` y referencia opcional; tarjeta/transferencia por el total exacto sin cambio. Un método por venta. Esperado de caja sólo cuenta efectivo; devoluciones de ventas no-efectivo no tocan el cajón. Cobro con selector de método; historial, ticket y reportes muestran el método. Ticket de efectivo sin cambios. Ver [PAYMENTS.md](PAYMENTS.md). Suite **770/770** (611 + 159), Release 0/0.

# Reportes y corte del día — 06/10/2026

**Implementado; smoke manual pendiente.** Pantalla **Reportes** (`sales.view`: Administrator, Manager): periodo por días locales (máx. 366), resumen (ventas, vendido, devuelto, neto, ticket promedio), por producto, por cajero y cortes de caja de los turnos abiertos en el periodo (fondo, ventas, devoluciones, esperado, contado, diferencia). Ventas cuentan por fecha de venta y devoluciones por fecha de devolución. Exportación CSV UTF-8 con BOM y neutralización de fórmulas. Sin migración. Suite **761/761** (602 + 159), Release 0/0.

# Cancelaciones y devoluciones — 06/10/2026

**Implementado; smoke manual pendiente.** Migración 019: `sale_refund` / `sale_refund_item` inmutables, permiso `sales.refund` (Administrator, Manager), triggers que validan caja abierta, cantidades acumuladas, reglas de cancelación y efectivo suficiente; `sync_outbox` reconstruida para aceptar `sale_refund` conservando secuencias. Esperado de caja = fondo + ventas − devoluciones. Historial muestra «Devuelto» y la sección de cancelación/devolución. Inventario sin cambios. Política y pendientes en [REFUNDS.md](REFUNDS.md). `Database.LatestVersion` evita fijar el número de esquema en pruebas. Suite **756/756** (597 + 159), Release 0/0.

# Usuarios, contraseñas y sincronización fase 1 — 06/10/2026

**Implementado; smoke manual pendiente. Commit `0c6e2cb` en `QAv1`.**

- **Migración 017** (`must_change_password`, permiso `users.manage` sólo para Administrator). Regla única de contraseña (`PasswordPolicy`): 8–16 caracteres con mayúscula, minúscula, número y carácter especial; aplica a alta, cambio y restablecimiento. El login sigue aceptando contraseñas previas fuera de la regla.
- Un solo administrador: la creación de usuarios sólo permite Manager o Cashier. Pantalla **Usuarios** (Administrator): listar, crear con contraseña temporal y restablecer contraseña; la temporal se muestra una vez y obliga a cambiarla antes de abrir sesión. Login con «¿Olvidaste tu contraseña?» y «Cambiar contraseña».
- Auditoría inmutable: `identity.user.created`, `identity.password.reset`, `identity.password.changed`, `identity.password.change_failed`, `identity.password.help_requested`. Nunca se guarda la contraseña ni la temporal.
- **Migración 018** — fase 1 de [SYNC_PLAN.md](SYNC_PLAN.md): `branch` (una fila inmutable por instalación) y `sync_outbox` llenada por triggers en el mismo commit de caja, conteo, venta y auditoría; respaldo inicial del historial; pendientes no borrables. `SqliteSyncOutbox` lee, marca enviados (todo o nada) y registra fallos. Sin nube aún.
- Suite `scripts/verify.ps1`: **744/744** (585 generales + 159 WPF), Release 0 errores/advertencias. Incluye fallas inyectadas a mitad de 017 y 018 con rollback completo y reintento.
- Roles sin cambios por decisión del usuario. Proveedor de nube pendiente.

## Registro anterior — FASE 1K ABIERTA — 1K.3D, 06/10/2026

**FASE 1K.3D técnicamente completada; smoke manual pendiente.** Producción conserva el motor y los contratos de FASE 1J, pero ahora presenta un flujo operativo en tres pasos: producto/perfil/versión y piezas planeadas; vista previa de preparaciones, insumos, disponibilidad y faltante; y captura del resultado real antes de registrar. La cantidad planeada sigue controlando el consumo y la cantidad real sólo controla la entrada de producto terminado. El botón final se deshabilita si alguna cantidad real no es un entero mayor o igual que cero. ContextHelp R3 explica cantidad planeada, preview y resultado real sin mover el layout.

La fixture aislada `work/phase1k3d-smoke-20261006` contiene exclusivamente datos SMOKE/NO COMERCIAL. Una ejecución automatizada autorizada registró 5 piezas planeadas y 4 reales: consumió 312.5 g de masa, 125 ml de volumen, 6 piezas y 5 g de la alternativa A; agregó 4 piezas terminadas. Un segundo borrador de 1,000 piezas reprodujo un faltante de 1,106 piezas y su confirmación fallida no cambió balances, ledger ni producto terminado. Preview, ventas y caja permanecieron sin mutación; no hubo impresión.

Focales: 10/10 de Producción y 15/15 WPF/ContextHelp. Suite completa `scripts/verify.ps1`: **718/718** (559 generales + 159 WPF), 0 fallidas/omitidas. Release: 0 errores/advertencias. Schema 16, sin 017; Domain/Application/Infrastructure y migraciones intactos. Base protegida SHA-256 inicial/final `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. Revisión: `src/CasaMiga.Presentation/bin/Phase1K3D/`, `work/Abrir-Casa-Miga-1K3D.cmd`, DataDirectory `work/phase1k3d-smoke-20261006`, título `Casa Miga POS · Caja · DESARROLLO · 1K.3D`. Ver `PHASE_1K3D_REPORT.md`.

## Registro anterior — 1K.3C-R3

**FASE 1K.3C técnicamente completada; pendiente un smoke final corto de R3.** El `Popup` de R2 podía crear una superficie/HWND independiente y conservar z-order fuera del ciclo visual del shell. `ContextHelp` usa ahora un `Adorner` dentro del árbol visual y del mismo HWND de `MainWindow`; por construcción no puede quedar como ventana flotante sobre otra aplicación. Abrirlo sigue sin participar en el layout base.

La ayuda se retira al pulsar Esc o el mismo `?`, al minimizar, perder activación/Alt+Tab, ocultar o descargar la vista, cerrar la ventana y cambiar F11/tamaño. Restaurar o reactivar nunca la abre automáticamente; puede abrirse manualmente después. Se conservan ancho máximo 520 DIP, Wrap, límites del viewport, prioridad de Escape, contenido aprobado y asociaciones de Ingredientes directos/Política de cantidad.

Pruebas focales WPF **46/46** y suite completa `scripts/verify.ps1` **716/716** (558 generales + 158 WPF), sin fallos ni omisiones. Release y build `Phase1K3C-R3`: 0 errores/advertencias; diff check correcto. La regresión `ContextHelpDoesNotRemainVisibleWhenOwnerWindowLosesActivationOrIsMinimized` materializa dos ventanas y cubre 1280×820/1920×1080, mismo HWND, minimizar/restaurar, desactivar/reactivar, reapertura, Escape y ausencia de layout shift.

Revisión: `work/phase1k3c-r3-smoke-20261006`, build `src/CasaMiga.Presentation/bin/Phase1K3C-R3/`, launcher `work/Abrir-Casa-Miga-1K3C-R3.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R3`. Schema 16, sin 017; base protegida SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`; Domain/Application/Infrastructure, migraciones, datos y configuración física intactos. Ver `PHASE_1K3C_R3_REPORT.md`. No iniciar 1K.3D ni Pedidos.

## Registro anterior — 1K.3C-R2

**FASE 1K.3C-R2 técnicamente completada; smoke visual/manual pendiente.** `ContextHelp` usa ahora un `Popup` flotante anclado al botón: abrirlo no participa en la medición del formulario ni desplaza campos o tablas. El ancho se limita entre el espacio disponible y 520 DIP, el texto conserva Wrap, el eje horizontal se ajusta al borde derecho y el panel cambia arriba cuando no cabe debajo. Escape se atiende desde la ventana real y desde el árbol separado del Popup; cierra primero la ayuda más reciente, conserva editor/borrador y mantiene la prioridad antes de ComboBox, fullscreen y navegación.

La prueba R1 era un falso positivo porque elevaba Escape directamente sobre la raíz `ContextHelp`; no reproducía el foco real en botón/campo ni la interceptación previa de `MainWindow`. R2 materializa Window/RecipeView/MainWindow, abre mediante el botón, prueba foco en botón, editor y Popup, verifica F11 y compara geometría antes/después. Las ayudas de ingredientes quedan junto al título y las de Política de cantidad junto al encabezado de esa columna en Recetas, alternativas y perfiles. Contenido aprobado y reglas funcionales intactos.

Focales definitivas: **46/46** compartidas y **3/3** de teclado/layout final, 0 fallidas/omitidas. Suite completa `scripts/verify.ps1`: **713/713** (558 generales + 155 WPF), Release y build `Phase1K3C-R2` con 0 errores/advertencias; diff check correcto. Revisión: `work/phase1k3c-r2-smoke-20261005`, build `src/CasaMiga.Presentation/bin/Phase1K3C-R2/`, launcher `work/Abrir-Casa-Miga-1K3C-R2.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R2`. Schema 16, sin 017; base protegida SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`; capas protegidas, operaciones e impresión intactas. Ver `PHASE_1K3C_R2_REPORT.md`. No iniciar 1K.3D.

## Registro anterior — 1K.3C-R1

**FASE 1K.3C-R1 técnicamente completada; smoke manual R1 pendiente.** El filtro de ingredientes mantiene una colección estable por fila y actualiza candidatos en sitio; así los `ComboBox` materializados conservan el ingrediente asignado, su ID, cantidad, unidad y política durante `SIN-RESULTADOS-999`, `SMOKE-MASA-001` y × Limpiar. Cancelar y reabrir recupera el borrador original. El catálogo filtrado sigue separado y buscar no crea filas automáticamente.

La columna visible ahora se llama «Política de cantidad». ContextHelp admite hasta 520 DIP, se contrae al contenedor, conserva Wrap y Esc, y ofrece instrucciones operativas sintéticas para Rendimiento, política, ingredientes directos, alternativas, preparaciones y versiones. Focales definitivas: **142/142** Recetas/reglas y **41/41** WPF, 0 fallidas/omitidas. Suite completa `scripts/verify.ps1`: **711/711** (558 generales + 153 WPF), Release y build `Phase1K3C-R1` con 0 errores/advertencias; diff check correcto.

Revisión R1: `work/phase1k3c-r1-smoke-20261005`, build `src/CasaMiga.Presentation/bin/Phase1K3C-R1/`, launcher `work/Abrir-Casa-Miga-1K3C-R1.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R1`. La copia contiene fixtures sintéticas de receta, tres dimensiones, alternativas, preparación y perfil; schema 16, integridad correcta y cero saldos/movimientos `SMOKE-*`. Base protegida SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. Domain/Application/Infrastructure/SQLite/migraciones intactos; sin 017, datos reales, operaciones o impresión. Ver `PHASE_1K3C_R1_REPORT.md`. No iniciar 1K.3D.

## Registro anterior — 1K.3C

**FASE 1K.3C técnicamente completada; smoke manual pendiente.** El usuario aprobó manualmente 1K.3B antes de autorizar esta subfase. La entrega mejora exclusivamente Recetas y perfiles productivos en Presentation: búsqueda incremental de ingredientes por código/nombre con 250 ms y × Limpiar; selección válida preservada; unidades guiadas por Masa/Volumen/Piezas; políticas de piezas; versión automática; estados Borrador/Publicada/Activa; grupos y opciones de alternativas separados; preparaciones e ingredientes directos diferenciados; ayudas ContextHelp y botones inválidos deshabilitados. La persistencia y las reglas existentes permanecen intactas.

Focales: **109/109** Recetas/persistencia y **38/38** WPF compartidas, más **6/6** WPF nuevas de Recetas; suite completa `scripts/verify.ps1`: **708/708** (558 generales + 150 WPF), 0 fallidas/omitidas. Release y build `Phase1K3C`: 0 errores/advertencias. Sólo Presentation, pruebas y documentación; Domain/Application/Infrastructure y migraciones intactos. Schema 16, sin 017. Ver `PHASE_1K3C_REPORT.md` y `evidence/phase1k3c-verification.md`.

Revisión aislada: `work/phase1k3c-smoke-20261005`, build `src/CasaMiga.Presentation/bin/Phase1K3C/`, launcher `work/Abrir-Casa-Miga-1K3C.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.3C`. Base protegida SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`; Qian POS-80/48/ibm850/tabla 2/Partial/sin cajón conservada, sin impresión. La copia inicia sin recetas, versiones ni perfiles. No se cargaron datos comerciales. FASE 1K permanece abierta; no iniciar 1K.3D.

## Registro anterior — 1K.3B, aprobado por el usuario

**FASE 1K.3B aprobada manualmente por el usuario antes de autorizar 1K.3C.** El registro técnico original partió del baseline limpio `4f20e520d74c6cace8d13b2ad3b04f436fc6eb71`.

Sólo Presentation, pruebas y documentación: búsqueda código/nombre con debounce de 250 ms, cancelación/versionado y selección válida; × Limpiar con foco; equivalencias reales; preview exacto de saldo inicial/entrada/merma/conteo usando los tipos Domain existentes; confirmación basada en el resultado persistido. Etiquetas humanas, fecha local, producto/SKU en asociaciones y ContextHelp. Conservados permisos, recuperación, scroll y prioridad Esc/ComboBox. Sin cambios en Domain/Application/Infrastructure ni migraciones.

Focales definitivas **142/142** (107 generales + 35 WPF/Presentation); suite completa `scripts/verify.ps1` **702/702** (558 + 144), 0 fallidas/omitidas; Release 0 errores/advertencias. Recompilación de reconciliación: Presentation y ambos proyectos de pruebas conservan DLL/PDB byte-idénticos a los verificados. Diff check correcto. Ver `evidence/phase1k3b-verification.md` y manifiesto de hashes. Copia aislada `work/phase1k3b-smoke-20261004`, build `src/CasaMiga.Presentation/bin/Phase1K3B/`, launcher `work/Abrir-Casa-Miga-1K3B.cmd`, título de revisión `Casa Miga POS · Caja · DESARROLLO · 1K.3B`. Apertura al terminar pruebas, documentación y commit; dejar login sin autenticar. Commit único autorizado `feat: improve inventory usability for phase 1K.3B`; HEAD final y observación del arranque en el reporte de conversación. Sin push.

Base original SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D` conservada. Harina MP-HAR-001 50 kg; Bulto 25 kg activo. Consumo automático DISABLED; Qian POS-80/48/ibm850/tabla 2/Partial/sin cajón intacta. Schema 16, sin 017, sin operaciones reales ni trabajos de impresión. Ver `PHASE_1K3B_REPORT.md`. No iniciar 1K.3C/1K.3D ni cerrar globalmente FASE 1K.

## Registro anterior — 1K.3A-R1, 03/10/2026

**Corrección técnica completada; smoke manual R1 pendiente.** Baseline limpio `6bf3b5256290bc053003b8b5f67e2a4a6d1148bc`. Esc con ComboBox abierto se atiende antes de fullscreen/navegación, conservando editor, selección y F11. Los hallazgos 2–7 ya se renderizan correctamente en la DLL Phase1K3A conservada; nuevas pruebas sobre MainWindow real lo demuestran. R2 falla esas pruebas, pero no existe evidencia suficiente para identificar qué proceso se usó en el smoke histórico. Se corrigió también el launcher de desarrollo que ocultaba IsDevelopment al establecer CASAMIGA_DATA_DIR.

Focales **18/18**; suite completa scripts/verify.ps1 **679/679** (556 + 123), 0 fallidas/omitidas; Release 0 errores/advertencias. Nuevas pruebas de popup Esc, fullscreen/F11, texto/estado vacío/botones y dimensiones visibles con SelectedValue enum. Scroll/rueda aprobados conservados y regresados. Ver `PHASE_1K3A_R1_REPORT.md` y `evidence/phase1k3a-r1-verification.md`.

Entrega separada en `src/CasaMiga.Presentation/bin/Phase1K3A-R1/`, launcher `work/Abrir-Casa-Miga-1K3A-R1.cmd`, título `DESARROLLO · 1K.3A-R1`. DataDirectory aislado `work/phase1k3a-r1-smoke-20261003`, copiado coherentemente de la base protegida. La original mantiene SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, Harina 50 kg y Bulto 25 kg activo. Schema 16 intacto, sin 017; Domain/Application/Infrastructure intactos; impresión física/configuración sin cambios.

Regla permanente registrada en AGENTS.md y `review-builds.md`: verificar y abrir automáticamente el launcher exacto al terminar el checkpoint; dejar la aplicación en acceso, sin login automatizado ni depender del anclado de Windows. La observación final de apertura se informa en conversación/reporte local posterior al commit único autorizado `fix: reconcile phase 1K.3A manual smoke`. No push. No operaciones sobre datos reales. No iniciar 1K.3B; esperar el smoke manual.

## Registro anterior — 1K.3A, 01/10/2026

Baseline limpio `6942c061bf6a68a65d42f57db5b00fbe567d1548`. Base transversal de usabilidad limitada a Presentation: Masa/Volumen/Piezas y motivos/movimientos localizados, scroll anidado opt-in en Inventario/Recetas/Producción, OperationId oculto pero conservado internamente, estados vacíos, acciones inválidas deshabilitadas y componente ContextHelp reutilizable. No rediseño completo ni ayudas específicas. Ver `PHASE_1K3A_REPORT.md` y evidencia `phase1k3a-*`.

Focales 52/52 + 11/11; `scripts/verify.ps1` completo 672/672 (556 + 116), 0 fallidas/omitidas; Release 0 errores/advertencias; diff check correcto. Domain/Application/Infrastructure/migraciones intactos; schema 16. Un único commit autorizado `feat: improve global usability for phase 1K.3A`; SHA final en conversación. Sin push.

**Estado real actualizado por el usuario antes de 1K.3A:** Harina MP-HAR-001 tiene 50 kg, con HAR-BULTO-25KG de 25 kg activa. Los registros históricos siguientes que dicen «sin saldo inicial» corresponden al checkpoint anterior y ya no describen el saldo actual. Copia aislada de sólo lectura confirma los 50 kg; hash de la base original idéntico al inicio/final. Ninguna operación comercial, carga real o impresión ejecutada en esta subfase.

Build de revisión preparada en `src/CasaMiga.Presentation/bin/Phase1K3A`, con configuración local equivalente a R2 y lanzador `work/Abrir-Casa-Miga-1K3A.cmd`. No se inició contra la base original. Smoke humano, touchpad físico y revisión visual pendientes. Mantener backlog 1K.3B Inventario / 1K.3C Recetas / 1K.3D Producción y carga real. Detenerse tras reporte; no iniciar la siguiente subfase.

## Registro anterior — corrección bloqueante de Inventario

Checkpoint Git autorizado el 01/10/2026: un único commit `fix: prevent inventory detail crash after presentation load`, cuyo padre es `353c7e271dfbdfe66b2e2dc8bc6c28e46914a971`. Esta actualización documental queda incluida en ese checkpoint; su SHA final se entrega en conversación. Revisión completa de alcance y hashes de los seis archivos de código/pruebas/probe coincidentes con los verificados: se reutiliza la suite 653/653 y Release 0/0. Migraciones intactas, schema 16, sin push ni operaciones sobre datos. El usuario confirmó detalle sin crash, presentación 25 kg persistida/Activa y Harina sin saldo inicial; puede reanudar la revisión manual en «Saldo inicial». FASE 1K permanece abierta. Los párrafos siguientes registran el estado anterior al checkpoint.

Baseline y HEAD conservado `353c7e271dfbdfe66b2e2dc8bc6c28e46914a971`. Ambos incidentes `6f7cd578043e483cbc8bbcb033a0de3c` / `c51af0e89fc342c3971dab013731feb6` reproducidos en WPF real: CheckBox TwoWay contra InventoryUnitView.IsActive getter-only. Binding OneWay y columna de sólo lectura corregidos. Manejo de fallos de consulta/refresh confirmado/resultado incierto en InventoryViewModel, sin cambiar handler fatal global ni capas de negocio. Harina y presentación únicas/activas, 25 kg, cero balances/movimientos/links; auditoría y comparación con backups confirman caja/ventas intactas. Base original sin mutaciones por herramientas; copia aislada para reproducción. Ver `INVENTORY_CRASH_FIX_20261001.md` y evidencia `inventory-crash-20261001-*` para pruebas y estado final de compilación. Sin commit/push/migración, schema 16. La corrección queda sin commit para revisión.

Verificación terminada: focal 38/38; suite completa 653/653 (545 generales + 108 fotos/Presentation), Release 0 errores/0 advertencias, diff check correcto. R2 recompilada tras cerrar el usuario la aplicación; DLL idéntica a la verificada. Consulta WPF sobre copia actual: 12 aperturas en dos procesos, 25 kg/Activa marcada/sin saldo inicial. SHA-256 de la base original conservado durante la intervención. El 01/10/2026 el usuario confirmó «todo correcto» y pidió el reporte: corrección de Inventario validada por el usuario. Esta aceptación no cierra FASE 1K ni acredita el smoke global R2 o la impresión física Qian. Cierre documental sin repetir suites ni modificar código, pruebas o migraciones; sin commit/push.

## Estado previo — 1K.2C-R2

28/09/2026. Baseline `ad00cfec3e55ad95f0ab2adf127c29f18f25a58d`; aprobación visual R1 recibida. R2 mantiene 26 Products/4 categorías, 26 PROVISIONAL/0 REAL/0 SIN FOTO, cards y rail aprobados. Botón Administrar foto visible, preview vivo del crop y ambas acciones en el carrito. Variante 04 térmica 576×341/1-bit embebida para original y reprint del perfil Qian 48 columnas, incluso snapshots con identificador viejo. Snapshot/venta/pago/inventario inmutables. Evidencia por servicios: 3 ventas, 3 Historial, 3 requests, cero impresiones físicas. Ver `PHASE_1K2C_R2_REPORT.md`, `printing-variant04.md` y `evidence/phase1k2c-r2-verification.txt`.

Validación Release aislada mediante `scripts/verify.ps1 -OutputPath bin/R2/` porque la aplicación R1 bloqueaba la salida habitual (ya cerrada). Render WPF y raster revisados; la aprobación humana R2 y el ticket físico Qian siguen pendientes. La revisión usa exclusivamente la compilación R2 y su dataset aislado. Schema 16 y hash 016 intactos. Pedido futuro requiere entidad previa al cobro; ver `orders-future-requirements.md`. No 017, no instalador, no cierre global.

## Registro anterior — R1

28/09/2026. La corrección R1 revoca la aprobación visual declarada por error en el reporte anterior. El dataset aislado se regeneró desde CSV/JSON y las dos fuentes mosaico: 26 Products, 4 categorías, 26 PROVISIONAL, 0 REAL y 0 SIN FOTO. Los recortes se preparan fuera de ProductPhoto Foundation con interpolación técnica de alta calidad y después ingresan por ProductPhotoService sin relajar el mínimo de crop. POS usa rail compacto, header operativo, catálogo 70/30 aproximado, cards centradas en foto y carrito persistente en pantalla. Tras la primera revisión humana, las cards pasan a 276 DIP con filas reservadas para nombre/precio y el catálogo vuelve al inicio al entrar; el rail contraído oculta etiquetas, grupos y versión, centra iconos, elimina scrollbar y expone tooltips. Checkout ofrece cobrar con o sin impresión; ambas rutas persisten primero y comparten bloqueo contra doble envío. La administración expone IMAGEN DEL PRODUCTO y crop arrastrable/redimensionable. Migration 016, backup/restore, inventario, recetas y producción permanecen intactos. Automatización: 535/535 regresión + 103/103 foto/Presentation = 638/638; Release limpio. El smoke requiere nueva aprobación explícita del usuario antes de cerrar visualmente 1K.2C. Ver `PHASE_1K2C_R1_REPORT.md`.

## Registro anterior — 1K.2B y corrección de acceso

Corrección de acceso solicitada el 28/09/2026, posterior al cierre: formulario de primer administrador compacto en dos columnas, logo proporcionado y composición centrada en la altura disponible con scroll de respaldo. El ajuste posterior de login amplía el bloque a un máximo de 1160 DIP y equilibra márgenes superior e inferior. Las contraseñas ya no se borran al fallar validación; al crear la cuenta se confirma el éxito, se conserva el usuario en login y se enfoca contraseña. No cambia IdentityService ni la política de 12–128 caracteres. La captura del usuario correspondía a una instalación sin administrador, no al backup sintético; las credenciales photo-admin publicadas previamente sólo sirven en ese backup restaurado. Ver `evidence/access-layout-20260928.md`.

28/09/2026. 1K.1 cerrada; 1K.2A aprobada y checkpoint `089066c04b8a9f69aa6a4468af6f8d146036d576`; 1K.2B implementada/verificada. 1K.2 sigue abierta; 1K.2C no iniciada. Baseline de producto `58bdcbd34692e14ced3f85fdeb82fca5d6b8b794`.

Implementación en `e872479fafd0389dbc32508f0a632ef331799bb9`; cierre documental separado. HEAD exacto del cierre se informa en conversación y copia local del reporte final, evitando referencia circular en su propio commit.

Schema 16 con `016_product_photos.sql`; 001–015 SHA-256 intactos. ProductPhotoService, puertos, almacén SQLite/archivos, codec WPF, respaldo v2 y restore v1/v2. Una asociación opcional, PROVISIONAL/REAL, nombre informativo sin ruta; original byte por byte, EXIF/crop reproducible y thumbnail JPEG Q85 hasta 512. Retirados se limpian tras commit, metadata/audit permanece. Sin caché de bitmaps ni UI final. Ver `product-photos.md` y `PHASE_1K2B_REPORT.md`.

Verificación final `scripts/verify.ps1`: 621/621 (535 existentes + 86 nuevas), 0 fallidas/omitidas; Release 0 warnings/errores. Smoke aislado import/replace/remove/reimport/backup/restore completo, sin ventas reales ni catálogo del negocio. Artefacto local bajo `work/phase1k2b-smoke`; evidencia portable bajo `docs/evidence/phase1k2b-*`. La aplicación del usuario fue cerrada por él para liberar build; no se arrancó contra sus datos ni se migró esa instalación.

Límites: normal backup/restore falla con media ausente/corrupta; no existe modo emergency. Repair explícito exige original válido y products.edit. Windows 10/LTSC y calidad con fotografías reales no certificados. Detenerse: siguiente fase requiere autorización.

## Registro anterior — diseño 1K.2A

Baseline intacto: `58bdcbd34692e14ced3f85fdeb82fca5d6b8b794`. 1K.1 sigue cerrada. 1K.2 está en diseño; no implementada. 1K.2B no inició. Schema actual 15; 001–015 intactas, sin 016.

Propuesta principal: ProductPhoto opcional por Product, originales retenidos en bundles locales portables, metadata/puntero SQLite, derivado JPEG Q85 hasta 512 cuadrado y caché acotada. Se propone 016 y backup v2 con lectura v1, más recuperación de media faltante explícita; nada de esto se ha aplicado. Ver `PHASE_1K2A_DESIGN.md` para DDL propuesto, protocolo de publicación/recuperación, ProductCard, permisos y 36 pruebas futuras.

Evidencia ejecutada: probe WPF aislado con carta técnica no fotográfica, 512/768 JPEG/PNG, carga y liberación de handles; ver `evidence/phase1k2a-image-probe-results.md`. Calidad con fotos reales, rendimiento del POS y Windows 10/LTSC siguen pendientes. 535/535 y Release 0/0 son evidencia previa de 1K.1; no se repitió suite por cambios documentales. src/tests/migrations permanecen intactos. Documentación/probe sin commit mientras se autoriza arquitectura; no push.

## Registro anterior — 1K.1 CLOSED

El usuario aprobó visualmente la versión final el 27/09/2026. El menú muestra el logo completo con margen interior, iconos vectoriales, jerarquía de grupos y pestañas, hover independiente sin cambio visual al bloquear navegación, contenido ajustado al ancho y encabezado adaptable. El shell WPF también fue renderizado y revisado a 960 y 1280 píxeles.

Revisión anterior: eliminada la imposición global de Foreground en TextBlock que seguía oscureciendo botones. Verificación del árbol visual WPF e imagen renderizada confirma blanco en botones primarios. Acceso rediseñado en dos columnas con panel Hoja de Aguacate y formulario claro.

Baseline de entrada: `152d0ecc03911c71863de8ffac21e668516885f6`. 1K.1 reemplaza la navegación superior por un shell WPF con sidebar, header, área de contenido y pie de estado, manteniendo ViewModels y servicios existentes. La visibilidad usa los permisos ya calculados; Application continúa como autoridad. Login, logout, caja y navegación conservan sus handlers.

Identidad: maestro oficial `assets/brand/Variant04/CasaMiga_LOGO_OFICIAL_VECTOR.svg`, VARIANTE 04 — SONRISA DELGADA, derivado PNG transparente HD 3080 × 1786 e icono ICO multirresolución para WPF. Paleta: Pan Artesanal `#4B2E19`, Fondo Natural `#FDF8EF`, Trigo Suave `#A67C52`; Hoja de Aguacate BEREL 405N usa `#4A5D4E`, equivalencia digital aproximada autorizada por el usuario. Presentation no referencia Opción 07. La revisión visual originó corrección global de contraste de botones, navegación verde y fullscreen automático tras login, expresamente autorizados.

Verificación automatizada: 535/535 pruebas, 0 fallidas, 0 omitidas; Release 0 errores/advertencias; focal 1K.1 2/2; tema WPF materializado y validado. Schema 15; migraciones 001–015 coinciden con los hashes del cierre 1J.5; no existe 016. El smoke fue visual y no realizó operaciones financieras, de inventario o producción. FASE 1K.1 está cerrada; 1K.2 no inició.

## Registro anterior

# FASE 1J CLOSED — 1J.5 completada

Baseline oficial: el commit único que incorpora este cierre, hijo directo de `13da5007eac455e7f88a993b85527b9e848a7d4f`; su SHA exacto se entrega en el reporte final de conversación. No se inserta el hash propio dentro del commit para evitar una referencia circular.

Schema 15; 001–015 intactas, sin 016. 533/533 pruebas; 0 fallidas/omitidas; Release 0 errores/advertencias. Recetas versionadas, perfiles productivos, multi-output, Planned/Actual, zero-output, validación de stock, idempotencia/recuperación, backup/restore y permisos verificados. Smoke humano aprobado en autorización final. UI funcional con refresco y selector de inventario.

Restricciones aceptadas: plan DRAFT no editable; confirmación por creador y terminal original; COUNT requiere pieza, sin alias pza. Desarrollo, NO V1 completa ni production ready. Ver PHASE_1J5_REPORT.md y PHASE_1J5_SMOKE.md.

## Registro anterior (histórico)
# FASE 1J ABIERTA — 1J.4 UI funcional completada y aprobada

Baseline de entrada: `a14007d9921e378e57aabd5e68de7e34f7464389`. Recetas y Producción están integradas en WPF sin duplicar reglas críticas: las Views/ViewModels llaman `RecipeService` y `ProductionService`. Administrator consulta y configura; Manager consulta recetas y crea/confirma producción; Cashier no ve los módulos. El smoke sintético humano validó fórmula/versiones, dos perfiles, plan multi-output, preview agregado, borrador recuperado tras reinicio, resultados menor/mayor al plan, confirmación, saldos, historial y restricción de Cashier.

Verificación final: `scripts/verify.ps1` con feed local aprobado; 530/530 pruebas, 0 fallidas y 0 omitidas; Release con 0 errores y 0 advertencias. Schema 15, migraciones 001–015 intactas y sin 016. `integrity_check=ok` y cero violaciones FK en la base de smoke. Desarrollo, NO listo para producción. 1J.5 NO iniciada. Ver `PHASE_1J4_REPORT.md`, `PHASE_1J4_SMOKE.md` y evidencia `phase1j4-*`.

## Registro anterior (histórico)

# FASE 1J ABIERTA — 1J.3 servicios funcionales

1J.3 COMPLETADA: Prepare/Create/ConfirmProduction y RecipeService, permisos persistidos, confirmación atómica, idempotencia y recuperación. Schema 15; migraciones 001–015 intactas, 016 ausente. Baseline: a1e7ef044f215a680526d65b7ea62d575d870b35. Ver ADR-023, PHASE_1J3_REPORT.md y evidencia phase1j3-*.

Verificación final: scripts/verify.ps1 aprobado, 520/520 pruebas, 0 fallidas/omitidas; Release 0 errores/advertencias de compilación. 29/29 focalizadas aprobadas. Desarrollo, NO listo para producción. 1J.4 no iniciada; no UI ni operaciones reales.

## Registro anterior (histórico)

# FASE 1J ABIERTA — corrección 1J.3B

Migración 015 aprovisiona cinco permisos y nueve asignaciones usando permission/role_permission. Schema 15. Administrator 5/5; Manager 4/5 sin recipe.configure; Cashier 0/5. HasPermission y src intactos. Ver PHASE_1J3B_REPORT.md. Verificación: 8/8 focalizadas, 491/491 suite completa, 0 fallidas/omitidas; Release 0 errores/advertencias de compilación.

1J.3 funcional PENDIENTE. No reanudar automáticamente ni iniciar 1J.4. Desarrollo, NO listo para producción. Baseline: a76951f86eaf0a9810fd701273358bea25f7dec2.

## Registro anterior (histórico)

# FASE 1J ABIERTA — corrección 1J.3A

Migración 014: schema 14 admite lotes confirmados con todas las cantidades reales en cero, consumo confirmado obligatorio y ninguna operación de salida. Si existe una cantidad positiva, la salida confirmada es obligatoria. Sólo se reconstruye production_batch; migraciones 001–013 intactas, sin cambios en src. Ver PHASE_1J3A_REPORT.md. Verificación: 51/51 focalizadas, 486/486 suite completa, 0 fallidas/omitidas; Release 0 errores/advertencias de compilación; backup/restore, integridad, FKs y rollback aprobados.

1J.3 funcional sigue PENDIENTE y no se reanuda automáticamente. 1J.4 no iniciada. Desarrollo, NO listo para producción. Baseline de entrada: 2f5e4579255d88b9c85c58bcfa4d6b1107bb63d5.

## Registro anterior (histórico)

# FASE 1J ABIERTA — 1J.2 persistencia de fórmulas, perfiles y lotes

1J.2 COMPLETADA: 466/466 pruebas, 31/31 focalizadas, 0 fallidas/omitidas; Release 0 errores/advertencias. Schema 13; 001–012 intactas. Repositorio técnico Infrastructure para drafts/publicación/activación y consultas. Lotes DRAFT con snapshots exactos; escalas no representables permanecen pendientes sin redondeo. Contrato histórico de ambas operaciones de producción preparado y protegido con FKs diferidas. No ConfirmProduction, stock preflight, Application/UI ni producción real. Ver PHASE_1J2_REPORT.md y ADR-022. Baseline de entrada: 05cc19f94b192884114a3da007dcaa72c25f4308.

1J.3 NO iniciada. Sistema de desarrollo, NO listo para producción. No se abrió el POS ni se modificó una instalación real durante esta subfase.

## Registro anterior (histórico)

# FASE 1J ABIERTA — 1J.1 Domain de fórmulas compartidas

1J.1 COMPLETADA: 435/435 pruebas, 0 fallidas/omitidas, Release 0 errores/advertencias. Incorpora fórmulas y perfiles versionados, alternativas, escalado racional exacto, políticas COUNT por línea, planificación multi-output y cantidades reales separadas. No persiste ni mueve inventario. La autorización supera el diseño monoproducto previo. Ver PHASE_1J1_REPORT.md y ADR-021. Baseline de entrada 7cc103c864812d1b8586921bad45323608fdc4d7. 1J.2 NO iniciada; schema 12 y 001–012 intactas, 013 ausente. Desarrollo, no listo para producción.

## Registro anterior
# FASE 1I — CERRADA

1I.5 completada. Usuario aprobó explícitamente el smoke con «SI» después de las capturas finales: Cashier vendió con consumo ACTIVO, stock 8→6, carrito vacío/$0 e Inventario consistente. Primera venta adicional fue Administrator y se conserva como evidencia, sin borrar ni repetir su intención. Tres ventas manuales: $38 + $19 + $38 = $95 CASH; apertura $0, ExpectedCash calculado $95. Caja sintética permanece OPEN, sin conteo/cierre manual. ENABLED v4, dos consumos -2 y saldo 6; checkout.pending ausente. Consulta final read-only: integrity_check ok, FK 0.

402/402 pruebas aprobadas, 0 fallidas/omitidas; Release 0 errores/advertencias. Backup y restore automatizados verificados separadamente del smoke manual. Schema 12; hashes 001–012 intactos; 013 ausente. Cierre exclusivamente documental tras producto b2eeb2e84952d9a924223285e6e9f24afc77d6a2; no repetir suite sin cambios de producto. El commit de este cierre constituye el baseline oficial recuperable.

Versión de desarrollo, NO lista para producción. Ninguna fase posterior iniciada. No recetas, producción, compras/proveedores, rediseño, fullscreen, instalador, LAN ni nuevos efectos físicos. Próximo paso: proponer alcance de una fase posterior y solicitar autorización; no implementarla. Modelo recomendado para planificar: Astra.

## Registro anterior (histórico, sustituido por este cierre)
# Continuidad — 1I.5: smoke parcial verificado; pendiente Cashier con consumo ACTIVO

FASE 1I ABIERTA. Suite completa 402/402; Release 0 errores y 0 advertencias. Backup/restauración, recuperación y reimpresión simulada verificados. Correcciones de presentación: etiqueta Venta y tres mensajes heredados de asociación. Schema 12; 001–012 intactas, sin 013. Ver PHASE_1I5_REPORT.md y PHASE_1I5_SMOKE.md. No fases posteriores. Desarrollo, no listo para producción.

## Registro anterior

# Continuidad — FASE 1I.4: completada, smoke humano confirmado

Baseline de entrada: c0bae99ba4302443260c0b0c2551eac01748971f. 1I.4 incorpora preflight compartido, activación/pausa/reanudación transaccionales, permisos/versionado/auditoría y UI operativa mínima. Schema permanece en 12; migraciones 001–012 intactas, 013 ausente. No se habilitó ninguna instalación real.

FASE 1I ABIERTA. Smoke administrativo WPF 1I.4 completado mediante capturas y confirmación explícita del usuario. No se inició 1I.5. Ver PHASE_1I4_REPORT.md, PHASE_1I4_SMOKE.md y ADR-020. Versión de desarrollo, no lista para producción.

## Registro anterior

# Continuidad — FASE 1I.3 completada

FASE 1I ABIERTA; 1I.1–1I.3 completadas. El desbloqueo autorizado sustituye el checkpoint bbaf48df8f517a89ebe6f3250822bb7ba88470b7. Schema 12 incorpora contrato inmutable 0/1 en Sale: historia 0/NULL; nuevas ventas 1/evaluación obligatoria con FK diferida y protección recíproca. Checkout integra evaluación, consumo, movimientos, balance, auditoría y snapshot de ticket configurado en su único commit.

Recovery valida historia/hash sin recalcular catálogo ni stock; rollback conserva carrito y commit incierto recupera sin duplicar. DISABLED/PAUSED no consumen. Flag inicial DISABLED; no se activó instalación alguna. 1I.4 NO iniciada. No impresión física ni operaciones sobre producción. Versión de desarrollo, no lista para producción.

Verificación final: 42/42 focalizadas; suite completa 364/364, 0 fallidas/omitidas; Release 0 errores/advertencias. Backup/restore, integridad, FKs y reconciliación aprobados. Evidencia y resultados vigentes: [reporte 1I.3](PHASE_1I3_REPORT.md), [ADR-019](decisions/ADR-019-checkout-inventory-contract.md) y evidence/phase1i3-*. El commit que incorpora este reporte fija el checkpoint de producto. El registro de bloqueo siguiente es histórico y quedó resuelto por autorización de 012.

## Registro anterior


# Continuidad — 1I.3 BLOQUEADA antes de implementar

FASE 1I abierta. La revisión autorizada de 1I.3 encontró que schema 11 no distingue de forma determinista una venta legítima 1I.2 sin evaluación de una venta futura obligada a tenerla cuya evaluación falte. Se aplicó la condición expresa de detenerse: no se modificó producto ni migraciones, no se creó 012. Baseline de producto: 6aa9ad73b4453e94070d1609febd035cbf425feb; 325 pruebas aprobadas y Release 0/0 son evidencia previa de 1I.2, no repetida aquí. Ver [reporte de bloqueo](PHASE_1I3_REPORT.md). 1I.3 NO completada; 1I.4 NO iniciada.

## Registro anterior

# Continuidad — FASE 1I.2 completada

FASE 1I permanece ABIERTA; 1I.1 aprobada y 1I.2 completada. Baseline de entrada: 42d7490cd20fd926f8e3d61830140ba7e8ce2a2d. El commit que incorpora PHASE_1I2_REPORT.md fija este checkpoint.

Schema 11 prepara SALE_CONSUMPTION interno, evaluación por venta, snapshots inmutables, UUIDv5 V1 y feature flag inicial DISABLED. SqliteInventoryStore conserva la única escritura de balance y ahora reutiliza su writer en una transacción del llamador. FKs diferidas impiden consumo huérfano. No backfill ni llamadas desde Checkout. No preflight ni activación UI.

Verificación: scripts/verify.ps1, 325 aprobadas / 0 fallidas / 0 omitidas; Release 0 errores / 0 advertencias. Upgrade con historia, rollback durante/después de reconstrucción, atomicidad, idempotencia y backup/restore schema 11 aprobados; integrity_check ok, foreign_key_check vacío, reconciliación sin diferencias. Migraciones 001–010 y Checkout/Application/Presentation/Sale intactos. No impresora. 1I.3 NO inició. Versión de desarrollo, no lista para producción. Ver [reporte](PHASE_1I2_REPORT.md), [ADR-018](decisions/ADR-018-sale-inventory-persistence.md) y evidencia phase1i2-*.

## Registro anterior
# Continuidad — FASE 1I.1 completada

FASE 1I está abierta únicamente con 1I.1 completada. Desde el baseline `71a4f60159ca167cbc316956c848b2082cdf84a5`, Domain incorpora política pura, errores estructurados, snapshots por SaleItem, agregación determinista y factory controlada para preparar `SALE_CONSUMPTION`. El constructor general continúa rechazando ese tipo y el esquema 10 no puede persistirlo.

Las reglas aprobadas bloquean tracking/link incoherente, artículo inactivo, MAIN no inicializada, insuficiencia y overflow. Un producto con TrackInventory=false y sin link produce NoConsumption. La multiplicación y agregación usan Int64 checked; el plan copia valores históricos y no contiene SaleId definitivo.

Verificación: 10 pruebas nuevas, checkpoint ampliado 62/62 y suite completa 300/300; 0 fallidas u omitidas. Release: 0 errores y 0 advertencias. Migraciones 001–010, CheckoutService, Sale, Payment, ExpectedCash, tickets e impresión permanecen intactos; 011 no existe y 1I.2 no inició. Ver [reporte](PHASE_1I1_REPORT.md) y [ADR-017](decisions/ADR-017-sale-inventory-consumption-policy.md).

## Registro anterior

# Continuidad — FASE 1H cerrada

FASE 1H está **CERRADA**. 1H.5 validó sin cambios de producto el reinicio, backup/restore, integridad, foreign keys, reconciliación, permisos, idempotencia, concurrencia, rollback y ausencia de efectos financieros. El baseline de entrada fue `397258d9b4711fa8f081323f111c6afe181a9ef5`; el commit documental que contiene este estado establece el nuevo baseline oficial.

La instalación sintética separada conservó esquema 10, siete unidades, MAIN, un artículo, una presentación, un vínculo, cuatro operaciones y movimientos, saldo 1,500,000 mg, cinco permisos, siete auditorías y sus UUID/hash. El SHA-256 lógico fue `527BB37DB0B63410BF645DE624DDBFA32C866A17E7B7E41DBE8E6E75D20775DB` tanto antes como después del restore; `integrity_check=ok`, cero violaciones FK y cero diferencias de reconciliación. Sale, SaleItem, Payment, CashSession, ExpectedCash y folios permanecieron en cero en esa prueba.

Verificación final: 290 aprobadas, 0 fallidas, 0 omitidas; Release con 0 errores y 0 advertencias. Migraciones 001–010 permanecen intactas y no existe 011. No se repitió el smoke WPF porque no cambió `src`; se conserva la aceptación manual de 1H.4. No se utilizó impresora. `SALE_CONSUMPTION`, recetas, producción y fases posteriores no se iniciaron. El sistema continúa como versión de desarrollo no lista para producción. Ver [reporte](PHASE_1H5_REPORT.md) y [evidencia](evidence/phase1h5-backup-restore.json).

## Registro anterior

# Continuidad — FASE 1H.4 completada y aprobada

FASE 1H permanece abierta. 1H.4 está completada y su smoke WPF fue aprobado manualmente por el usuario. UI implementada en `845b8a8`, binding WPF corregido en `dd7d5b5` y unidades operativas exactas/validación de Manager en `9b7b6d6`.

Administrator conserva acceso completo; Manager tiene view/receive/waste y carece de configure/adjust; Cashier no accede. La UI ofrece g/kg, ml/L y pieza; mg/µL permanecen internos. `SMOKE-H14` quedó en 1.5 kg, con cuatro movimientos y reconciliación exacta. Ver [reporte de aceptación](PHASE_1H4_SMOKE_CORRECTION.md) y [evidencia](evidence/phase1h4-smoke-correction.txt).

Verificación vigente: 290 pruebas aprobadas, 0 fallidas, 0 omitidas; Release con 0 errores y 0 advertencias. Migraciones 001–010, Domain, ledger, InventoryBalance, Checkout, Sale, Payment, ExpectedCash, tickets e impresión permanecen intactos. El sistema sigue siendo una versión de desarrollo no lista para producción. 1H.5 no inició.

## Registro anterior

# Continuidad — FASE 1H.3 completada

1H.3 está completada técnicamente en el commit `9e96edc2a4652487adce9a4ad2693ff70a3d218d`. FASE 1H permanece abierta y 1H.4 no inició. Existen contratos y servicios Application para saldo inicial, recepción, merma, ajuste a conteo físico, configuración de artículos/presentaciones/asociaciones y consultas paginadas. Presentation no cambió y aún no hay UI de inventario.

Domain ahora coincide con el esquema 010: RECEIPT admite observación opcional normalizada, rechaza whitespace y más de 500 caracteres, y nunca admite reason_code. OPENING_BALANCE conserva razón y observación NULL.

Los comandos de movimiento reciben un OperationId estable y una intención positiva/absoluta; no exponen deltas arbitrarios. El conteo calcula gain/loss en Application. Infrastructure verifica saldo y versión esperados dentro de su transacción inmediata; una intercalación devuelve ConcurrencyConflict sin insertar el ajuste. Un retry del mismo conteo recupera la operación confirmada y contenido distinto produce IdempotencyConflict. NoChange no escribe operación, movimiento ni auditoría.

Administrator posee los cinco permisos; Manager sólo view/receive/waste; Cashier ninguno. Configuración y auditoría se confirman juntas. Las consultas abren SQLite read-only con transacción deferred, LIMIT/OFFSET acotados, orden estable y joins agregados sin N+1.

Verificación final: 52 pruebas focalizadas y 265 completas aprobadas, 0 fallidas y 0 omitidas; Release con 0 errores y 0 advertencias. Migraciones 001–010 intactas. Checkout, Sale, Payment, ExpectedCash, tickets e impresión permanecen intactos. Ver [reporte](PHASE_1H3_REPORT.md), [ADR](decisions/ADR-016-inventory-application.md) y evidencia.

## Registro histórico anterior

# Continuidad — corrección 1H.2A completada / esquema 010

La migración 010 corrige exclusivamente el CHECK de RECEIPT: reason_code NULL y note opcional (NULL o texto de 1–500 caracteres tras trim). 001–009 permanecen intactas. Commit técnico: `76e654ce9b56132ff64248953931c95190858311`; baseline anterior: `6afd1419423b8640f2a9357957894c0e164a80bb`.

Reconstrucción transaccional de inventory_operation e inventory_movement con foreign_keys ON, sin escrituras al balance. Se preservan datos, columnas, índices, triggers y FKs. Pruebas cubren upgrade con operaciones, opening cero, idempotencia, reconciliación, backup/restauración y rollback posterior a la reconstrucción sin registrar versión 10.

Verificación final: 254 pruebas aprobadas, 0 fallidas y 0 omitidas; Release con 0 errores y 0 advertencias. Ver [reporte](PHASE_1H2A_REPORT.md) y evidencia asociada.

FASE 1H permanece ABIERTA. 1H.3 NO inició. El contrato Domain existente todavía rechaza observación en RECEIPT; su alineación corresponde a 1H.3 antes de exponer el caso de uso. Checkout, tickets, impresión y todo src permanecieron intactos. Versión de desarrollo, no lista para producción.

## Registro histórico anterior
# Continuidad — FASE 1H iniciada, subfase 1H.2 completada

1H.1 y 1H.2 están completadas. El commit `a8fcfb2` agrega migración 009, persistencia del catálogo de inventario, ledger inmutable, saldo materializado reconstruible, operaciones multipartida, idempotencia por UUID+SHA-256, recuperación de commit incierto, reconciliación y pruebas concurrentes.

`SqliteInventoryStore` es la única autoridad que escribe `inventory_balance` dentro de una transacción inmediata. Usa actualización condicionada por saldo y versión. Los triggers sólo protegen estructuras y estados; ninguno calcula ni actualiza balances. `inventory_movement` es la fuente histórica.

OPENING_BALANCE admite cero mediante una línea explícita `is_opening=1, delta_atomic=0`, por lo que distingue una posición inicializada en cero de una aún no inicializada. Los demás movimientos prohíben delta cero. Stock negativo y overflow revierten la operación completa.

La migración 009 crea ocho tablas, siete unidades estándar, MAIN / Almacén principal y cinco permisos. Administrator recibe cinco; Manager recibe view/receive/waste; Cashier ninguno. PENDING sólo vive dentro de la transacción normal del store y se transforma a CONFIRMED antes del commit.

Verificación: 41 pruebas focalizadas y 236 pruebas completas aprobadas, 0 fallidas y 0 omitidas. Build Release: 0 errores y 0 advertencias. Migraciones 001–008 intactas. Checkout, ventas, caja, ExpectedCash, tickets e impresión no cambiaron. No existe UI ni servicio Application operativo de inventario; 1H.3 no se inició.

## Registro histórico anterior

# Continuidad — FASE 1H iniciada, subfase 1H.1 completada

FASE 1H comenzó únicamente con 1H.1. El commit `3ed510c` agrega el modelo puro de Domain para cantidades físicas, dimensiones, unidades estándar, artículos inventariables, presentaciones enteras específicas, ubicación principal, razones estructuradas y asociación explícita Product–InventoryItem. `InventoryQuantity` usa Int64 en mg, µL o pieza; rechaza negativos, pérdida de precisión, dimensiones incompatibles y overflow. Cero es válido como saldo, mientras presentaciones y asociaciones exigen cantidades positivas.

No existe aún migración 009, ledger persistido, `InventoryBalance`, servicios Application, permisos SQLite ni UI de inventario. `Product.TrackInventory` no crea artículos, asociaciones o movimientos. Checkout, ventas, caja, ExpectedCash y tickets no cambiaron. Las migraciones 001–008 permanecen intactas.

Verificación de 1H.1: 18 pruebas focalizadas aprobadas. Suite completa: 213 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores y 0 advertencias. FASE 1G permanece cerrada y el producto continúa siendo una versión de desarrollo no lista para producción.

## Registro histórico anterior

# Continuidad — FASE 1G cerrada

FASE 1G CERRADA dentro del alcance autorizado. Bloque F cerró la reimpresión desde Historial para Administrator y Manager mediante `sales.receipt.reprint`; Cashier no recibe el permiso. Application revalida usuario activo y permiso, reutiliza el `receipt_snapshot` inmutable o crea el primero una sola vez para ventas anteriores a 1G. Cada acción válida crea un request `REPRINT`, conserva trazabilidad append-only y usa los estados REQUESTED/SUBMITTED/COMPLETED/FAILED/UNKNOWN.

El ticket muestra `REIMPRESIÓN`, conserva logo Opción 07, CP850/ESC t 2, 48 columnas, TOTAL, GS V 1 y `openCashDrawer=false`. El botón se deshabilita durante la solicitud; FAILED/UNKNOWN permiten un reintento explícito relacionado y nunca automático. Un guard por SaleId bloquea dos ejecuciones simultáneas dentro del proceso. Sale, SaleItem, Payment, folio, caja y ExpectedCash permanecen inmutables.

Smoke físico final: exactamente una reimpresión de una venta sintética existente. PrintRequestId `3ca46975-71af-4643-b80b-ff8290a5d356`, SaleId `904ce0fa-510e-4308-a0ed-b093749dfcda`, JobId 7, REQUESTED→SUBMITTED. La fotografía confirmó REIMPRESIÓN, logo, español, valores históricos, corte parcial y ausencia de apertura de cajón. SQLite conservó 55 ventas, 110 partidas, 55 pagos y ExpectedCash $1,852.00; existe un solo request.

Verificación vigente: 195 pruebas aprobadas, 0 fallidas, 0 omitidas; Release con 0 errores y 0 advertencias. Bloque E.1 cerrado: margen superior aceptado, causa predominantemente mecánica, logo sin recorte y backfeed prohibido sin documentación específica Qian. Cajón deshabilitado pendiente de hardware; LAN opcional. El sistema no está listo para producción y no se inició otra fase.

## Registro histórico anterior

# Continuidad — FASE 1G Bloque E

FASE 1G ABIERTA; Bloque E validado físicamente. Logo Opción 07, texto, importes, TOTAL y corte parcial fueron aprobados. La única observación es exceso de blanco antes del logo. Bloque E.1 determinó que el PNG aporta 8 filas/1.00 mm y Casa Miga no emite feed previo; la causa dominante es mecánica. No imprimir de nuevo. Ver PHASE_1G_BLOCK_E1_TOP_MARGIN_REPORT.md. 191 pruebas aprobadas; Release sin errores ni advertencias.

Baseline Bloque D: CP850, ESC t 2, GS V 1. Fuente única: assets/brand/Opcion07. Paquetes anteriores deprecados. Sistema de desarrollo, no listo para producción. No se inició ninguna fase posterior.

## Registro histórico anterior, sustituido por el estado superior

# Estado actual — FASE 1G BLOQUE E, LOGO OFICIAL REQUERIDO

Fecha: 2026-09-11. FASE 1F permanece cerrada. El usuario confirmó físicamente el Bloque D completo: caracteres españoles, legibilidad, tamaños, alineación, ausencia de basura y corte automático parcial correctos. Bloque D implementa selección estricta de codepage en `283cfa4`; el baseline queda fijado en .NET `ibm850`/CP850, `ESC t 2` y `GS V 1`. Git local conserva la identidad Rammseth / `rammstudioworkshop\@gmail.com`; no hay remoto ni push.

Bloque D envió exactamente un ticket sintético: .NET `ibm850`, ESC/POS tabla 2, selección `1B 74 02` una vez en offset cero. Request `957c1076-cd21-4ceb-b609-63b437a6c643`, JobId 5, SUBMITTED, 889 bytes y SHA-256 `83CC7BA9E0339E608891C2B3908021A847053B40B5A3F722C4F4AF44F101321D`. La fotografía `evidence/phase1g-block-d-physical.png` y la confirmación del usuario validan la salida física y el corte parcial.

Bloque C envió exactamente un ticket sintético a POS-80: request `6df7d54d-a095-48e3-9223-dfc09b5bdf51`, JobId 4, 770 bytes, estado SUBMITTED y SHA-256 `792FEFF59A9921E17AD77DFDD03D3B7D65447150B58ED195C159CF443DECB757`. Contiene encabezado 2×2, TOTAL doble alto, dos restauraciones, cuatro LF y un único corte parcial final. No contiene pulso de cajón. Su jerarquía visual fue aprobada y el corte parcial quedó confirmado con el Bloque D.

Windows confirmó `POS-80`, USB001, driver POS-80 11.3.0.1 y estado Normal. El trabajo `7ba5b12f-2482-457d-abf4-725181b25d60` fue aceptado por Winspool con JobId 3 y estado técnico SUBMITTED; fueron 762 bytes ASCII sin ESC, GS, corte ni cajón. Esto no confirma salida física. No se abrió la base comercial ni se crearon venta, pago, caja o cambio en ExpectedCash.

ReceiptDocument y ReceiptLine son inmutables y viven en Application. El documento se deriva de Sale/SaleItem/Payment, captura nombres de usuario/terminal y perfil comercial, usa hora local con offset y conserva Money Int64. El JSON canónico, versión y SHA-256 se guardan en receipt_snapshot. Requests y eventos append-only distinguen ORIGINAL/RETRY/REPRINT y REQUESTED/SUBMITTED/COMPLETED/FAILED/UNKNOWN.

Migración 008 agrega las tres tablas y `sales.receipt.reprint` sólo para Administrator/Manager. `printing.json` es local, opcional y excluido de backup. Sin configuración válida, el cobro permanece operativo y el ticket se marca como no enviado. Checkout intenta imprimir sólo después de recibir la venta confirmada; SQLite no permanece en transacción durante Winspool.

WindowsRawPrintTransport envía RAW a una cola lógica y devuelve JobId. SerializedReceiptPrintTransport limita a un trabajo dentro de la aplicación. El renderer de Bloque A contiene texto únicamente: no emite selección de codepage, inicialización, corte ni cajón. SUBMITTED significa aceptado por spooler, no papel físicamente confirmado.

Verificación vigente: 188 pruebas aprobadas, 0 fallidas, 0 omitidas; build Release con 0 errores y 0 advertencias. El mecanismo controlado no abre la base comercial y no cambia venta, pago, caja ni ExpectedCash.

`sales.view` se concede por migración 007 solamente a Administrator y Manager. Cashier no lo recibe. Presentation oculta Ventas sin permiso y Application revalida en cada consulta que el usuario actual exista, esté activo y conserve el permiso. Consultar historial no exige caja abierta ni `sales.complete`.

El listado devuelve hasta 50 ventas, `COUNT` y página mediante joins, orden `confirmed_utc DESC, id DESC`, búsqueda exacta con `terminal_id + sequence` y rango de días locales. Desde es inclusivo; Hasta se convierte al inicio UTC del día local siguiente y se usa como límite exclusivo. El detalle carga encabezado/pago/usuario/terminal y después partidas por `position`, sin N+1. Sale, SaleItem y Payment no cambiaron y continúan inmutables.

Las lecturas usan `ILocalReadUnitOfWork`: conexión SQLite read-only y `BeginTransaction(deferred: true)`. Las operaciones comerciales conservan `ILocalUnitOfWork.Run` con transacción inmediata. Una prueba mantiene una lectura abierta y confirma simultáneamente un Checkout real para comprobar que el historial no reserva el writer lock.

Estado de aceptación: **FASE 1F CERRADA**. Smoke WPF sobre instalación sintética: 55 ventas, página 1 de hasta 50, página 2 con 5, orden descendente 55→1, folio exacto, formato inválido rechazado, día local con 55 resultados, detalle de dos partidas y pago CASH, Volver/Limpiar y Ventas ausente para Cashier. SQLite después del recorrido conserva 55 ventas, 110 partidas, 55 pagos y 55 auditorías `sale.completed`; migración 7 y dos roles con `sales.view`.

Verificación final: 170 aprobadas, 0 fallidas, 0 omitidas. Build Release: 0 errores, 0 advertencias. Publicación win-x64 autocontenida en `artifacts/development`. Backup `.cmbackup` y restauración CLI a carpeta nueva tienen el mismo hash lógico `C7E4A5392F31988D6BC7857B664594F5BCD3C9F4FF3D7CA71C299D1CC9423F4F` y el mismo `terminal.json`.

Bloque E fue revisado sólo hasta su precondición. La búsqueda dirigida encontró `src/CasaMiga.Presentation/Assets/README.md`, que declara que no existe un logotipo oficial y prohíbe generarlo o redibujarlo. No se modificó código, pruebas, migraciones ni configuración y no se imprimió otro ticket. Para continuar se necesita el archivo oficial aprobado, preferiblemente PNG monocromático con transparencia, alta resolución y proporciones originales.

Limitaciones heredadas de 1F: los nombres mostrados en Historial siguen siendo actuales y no existen snapshots retroactivos. Los tickets anteriores a 1G capturan esos nombres al crear su primer snapshot. La zona horaria depende del equipo y LIMIT/OFFSET deberá revisarse con volúmenes mayores. Sigue siendo una versión de desarrollo y no está lista para producción.

Verificación del cierre 1I.4: 399 pruebas aprobadas, 0 fallidas/omitidas; Release 0 errores/advertencias. Subtítulo corregido, publicación artifacts/phase1i4-final. No repetir smoke administrativo ni iniciar 1I.5 sin autorización.

Checkpoint humano 1I.5: ver sección Avance humano de PHASE_1I5_REPORT.md. Dos ventas verificadas; falta Cashier con consumo ACTIVO. Texto heredado corregido; fase abierta.
