# Recetario en borrador y carga de la caja por la web — 19/10/2026

- `docs/catalog/recetario-borrador.json`: el recetario de `docs/RECETARIO_PARA_COMPLETAR.md` como datos (34 insumos, 10 preparaciones y 31 panes), con los rendimientos reportados marcados «por medir». La masa enriquecida se reparte por pieza (masa dulce + mantequilla) mientras la caja no admite una preparación dentro de otra.
- `cloud/scripts/cargar-caja.ts` (`npm run cargar -- <web> [--enviar] [--esperar]`): manda a una caja, por la web, el catálogo autorizado con sus fotos provisionales, los insumos, las preparaciones en borrador y, cuando estén publicadas, lo de cada pan en borrador. Compara contra lo que la caja ya tiene y lo que ya va en la fila (`/api/web/commands?pendientes=1`, sin el tope de 100), así que no duplica. `scripts/run-local.ps1` lo corre al final (`-SinCarga` lo omite): la caja de prueba en Windows ya no arranca sin catálogo ni fotos.
- La demo de la web trae el recetario en borrador (Recetas y perfiles de los 31 panes).
- Corrección: `/api/web/command` aceptaba cuerpos de hasta 50 KB y una foto de producto (hasta 900 KB desde Catálogo) no cabía; ahora hasta 1.5 MB.
- Pruebas: `cloud/test/carga.test.ts` (2): todo el flujo contra la validación de la web y una foto real por HTTP. Nube 75/75.

# Integración de QA en main — 19/10/2026

Se combinó en main lo nuevo de QA: 1K.4G-B (planeación de producción y lectura del resultado), 1K.4G-B-R2 (tablas), 1K.4G-C (conciliación del catálogo comercial y recetario comercial maestro), 1K.4G-C-C-B1 (preparación de inventario), R1 (acomodo) y 1K.4G-C-C-B2 (usabilidad de producción). Igual que el 12/10, se conserva el diseño de main: Catálogo, Recetas, Inventario, Historial de ventas, Punto de venta, Cobro, Cierres, Reporte diario y la ventana principal quedan como en main. No se adoptan R1 (menú que se contrae, encabezado compacto y desplazamiento propio de cada vista) ni el sistema de tablas de R2 (renglones de 48 px, cebra y cuadrícula). Sí entra Producción completa (B, B-R1 y B2): en la ventana de main ocupa la altura visible, y sus tablas usan el diseño de main con renglones que crecen. Los estilos con nombre de R2 (`CasaMigaTable*`) existen en el tema sobre las tablas de main. No se incluyen las pruebas de diseño de R1 y R2 (`AdaptiveLayoutPresentationTests`, `TableReadabilityPresentationTests` y sus cambios en pruebas existentes).

El recetario (`PHASE_1K4G_C_RECIPE_MASTER.md`, `PHASE_1K4G_C_RECIPE_MATRIX.md` y `PHASE_1K4G_C_RECIPE_TECHNICAL_AUDIT.md`) queda como documentación: no carga recetas en la caja y la auditoría reporta 0 productos listos para consumo automático. La conciliación de 1K.4G-C describe 36 productos en la copia de revisión de QA; el catálogo autorizado del repo (`docs/catalog`, 31 productos) no cambia hasta que el usuario lo decida. Pendiente: CI de Windows del PR y prueba humana de Producción.

# FASE 1K.4G-C-C-B2 — APROBADA TÉCNICAMENTE Y CERRADA CON COMMIT LOCAL, 19/10/2026

Fixture sintética nueva `work/phase1k4g-c-c-b2-demo-20261019` (DEMO - NO COMERCIAL, schema 17, 36 productos, 48 artículos, 7 producciones confirmadas y 3 borradores; sin ventas, caja ni impresión). Validación funcional en copia scratch 34/34 sin defectos del motor. Producción mejoró lista adaptable, columnas, filas acotadas, plan inicial vacío y explicaciones de acciones deshabilitadas, sólo en Presentation. `scripts/verify.ps1` **806/806**, Release 0 errores / 0 advertencias. Bases protegidas sin cambios. Reinicio del desplazamiento comprobado automáticamente sobre copia. Checkpoint local en la rama `phase/1k4g-c-c-b2-production-ux`; sin push ni merge a `master`. Pendientes no bloqueantes y prueba física de DPI en el [reporte B2](PHASE_1K4G_C_C_B2_REPORT.md).

## Registro anterior — 1K.4G-C-C-B1-R1

# FASE 1K.4G-C-C-B1-R1 — LAYOUT ADAPTABLE COMPLETADO TÉCNICAMENTE, SMOKE HUMANO PENDIENTE, 19/10/2026


Inventario, Catálogo, Recetas y Producción usan listas principales que crecen con el espacio disponible; POS evita recortar nombre y precio, y la barra lateral/encabezado/acceso se adaptan a ventanas estrechas. Caja e Historial permiten desplazar columnas cuando el ancho físico no alcanza; el reporte diario aprovecha mejor pantallas grandes. Los formularios largos conservan scroll. No se alteraron flujos comerciales, datos, migraciones ni impresión. La copia de revisión R1 parte de B1 con SHA-256 idéntico y credencial comprobada en lectura. Véase [reporte R1](PHASE_1K4G_C_C_B1_R1_LAYOUT_REPORT.md). Falta aprobación visual humana; no iniciar C-C-B2 ni hacer push.

## Registro anterior — 1K.4G-C-C-B1

# FASE 1K.4G-C-C-B1 COMPLETADA TÉCNICAMENTE — SMOKE HUMANO PENDIENTE, 18/10/2026

La copia aislada `work/phase1k4g-c-c-b1-review-20261018` deriva de C-B mediante backup SQLite/WAL seguro. Contiene 31 materias primas (Harina heredada y 30 nuevas) y 36 artículos terminados COUNT vinculados documentalmente a los 36 ProductId activos. No se crearon `ProductInventoryLink`, fórmulas, perfiles, lotes, saldos o movimientos nuevos. Vainilla y mezcla multigrano siguen pendientes; el consumo automático permanece `DISABLED`.

Origen C-B SHA-256 `F16ED3CFA7CDF94B0DC20B8E790DCC3B27A69F065A1FAB35BCE122050B282576`; destino B1 `B40CCD0C50024B433F698953875F5D08A5F85356B70FE91511B2AA3B41F87CAF`; base protegida `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. `integrity_check=ok`, 0 FK, schema 17, sin migración 018. Suite oficial **799/799** y Release/build B1 **0 errores, 0 advertencias**. Build `src/CasaMiga.Presentation/bin/Phase1K4G-C-C-B1/`; launcher `work/Abrir-Casa-Miga-1K4G-C-C-B1.cmd`. Ver [reporte B1](PHASE_1K4G_C_C_B1_REPORT.md). No push ni inicio de B2.

## Registro anterior — 1K.4G-C-C-A

# FASE 1K.4G-C-C-A COMPLETADA DOCUMENTALMENTE, 18/10/2026

El recetario comercial maestro conserva las fórmulas y prácticas aportadas por el usuario sin convertir aproximaciones en datos exactos. La matriz cubre los 36 productos activos de la copia C-B con SKU, ProductId, preparación, directos, rendimiento, bloqueo técnico y control contra doble consumo. La auditoría confirma que el motor actual consolida fórmulas compartidas, múltiples requisitos y salidas reales, pero no soporta fórmulas derivadas, requisitos fijos por tanda, rendimiento intermedio variable ni destinos operativos específicos para degustación o consumo interno.

No se crearon Formula, FormulaVersion, ProductionProfile, InventoryItem, ProductInventoryLink ni movimientos. C-C-B no inició. No cambiaron código, pruebas, migraciones o bases. La suite 799/799 y el Release limpio de C-B se conservan como último baseline funcional; no se repitieron por tratarse de un cierre exclusivamente documental. Ver [recetario maestro](PHASE_1K4G_C_RECIPE_MASTER.md), [matriz](PHASE_1K4G_C_RECIPE_MATRIX.md) y [auditoría](PHASE_1K4G_C_RECIPE_TECHNICAL_AUDIT.md).

## Registro anterior — 1K.4G-C-B

# FASE 1K.4G-C-B CERRADA TRAS REVISIÓN HUMANA, 18/10/2026

Con las decisiones comerciales aprobadas, la copia `work/phase1k4g-c-b-review-20261018` contiene 36 productos activos y cinco históricos inactivos. Se crearon diez, se editaron once conservando ID/SKU existente cuando procedía y se desactivaron cinco sin borrar ventas. Se asignaron códigos únicos CM-027–CM-040 a nuevas identidades y a cuatro productos existentes sin SKU. Los precios confirmados para C-B se aplicaron; los otros 24 se conservaron del origen y requieren revisión comercial. El origen 1K.4F-R2, la base protegida y la evidencia cerrada 1K.4G-B-R2 conservan sus SHA-256.

En la copia únicamente cambiaron `product` y `audit_entry`; ventas, pagos, caja, recibos, inventario, recetas, vínculos, fotos y archivos de configuración permanecieron iguales por digest. `integrity_check=ok`, 0 violaciones FK, schema 17; sin migración 018. `scripts/verify.ps1`: **799/799**, sin fallas/omisiones; Release sin errores ni warnings. Build/launcher identificados `Phase1K4G-C-B`, DataDirectory exclusivo, título de desarrollo. La revisión visual humana fue aprobada antes de autorizar C-C-A. Ver [reporte C-B](PHASE_1K4G_C_B_REPORT.md). No se hizo push.

## Registro anterior — 1K.4G-C-A

# FASE 1K.4G-C-A COMPLETADA DOCUMENTALMENTE — DECISIONES COMERCIALES PENDIENTES, 18/10/2026

La conciliación del catálogo maestro de 36 productos contra el catálogo de revisión de 31 productos encontró 27 candidatos y nueve altas inequívocas. La matriz clasifica 14 CONSERVAR, 11 RENOMBRAR, nueve AGREGAR, un REVISAR DUPLICADO y un REQUIERE DECISIÓN COMERCIAL. Puerquito y Coronita se retiran; la Galleta de nuez y coco se sustituirá por dos identidades nuevas; el bollo duplicado y Dona especial requieren decisión antes de C-B.

La revisión fue de sólo lectura sobre la base protegida schema 16, el catálogo 1K.4F-R2 schema 17 y la evidencia cerrada 1K.4G-B-R2. La base protegida conserva SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. No se modificaron bases, código, pruebas o migraciones; no se inició C-B/C-C. Ver [PHASE_1K4G_C_CATALOG_RECONCILIATION.md](PHASE_1K4G_C_CATALOG_RECONCILIATION.md).

## Registro anterior — 1K.4G-B-R2

# FASE 1K.4G-B-R2 CERRADA — APROBADA CON OBSERVACIONES NO BLOQUEANTES, 18/10/2026

Presentation cuenta con un sistema global reusable de tablas: encabezados con wrapping, texto humano completo, filas automáticas, mínimos seguros, cantidades centradas, importes a la derecha, selección Casa Miga y foco de teclado visible. Se auditaron Catálogo, Inventario, asociaciones, movimientos, Recetas, Producción, Caja, Historial de ventas y Reporte diario. El historial de movimientos se recompuso en Fecha, Operación, Movimiento, Existencia, Usuario y Detalle, sin perder valores ni unidades.

Suite oficial **799/799** (584 general + 215 WPF), 0 fallidas/omitidas; Release `src/CasaMiga.Presentation/bin/Phase1K4G-B-R2/` con 0 errores/advertencias. El smoke humano posterior confirmó producción A=2/B=2, venta de una pieza de cada producto y merma única de A; saldos finales A=0, B=1, Harina=9.58 kg, Huevo=28 y Agua=4.86 L. La auditoría read-only verificó integridad SQLite/FK/saldos, una sola operación WASTE y ausencia de efectos financieros o consumos duplicados. La fixture `work/phase1k4g-b-r2-smoke-20261011`, schema 17, quedó cerrada como evidencia; la base protegida permaneció intacta y no existe migración 018. Ver [PHASE_1K4G_B_R2_REPORT.md](PHASE_1K4G_B_R2_REPORT.md). No push/PR ni inicio de C-B/1K.4H-B.

## Registro anterior — 1K.4G-B-R1

# FASE 1K.4G-B-R1 TÉCNICAMENTE COMPLETADA — SMOKE HUMANO PENDIENTE, 11/10/2026

La tabla **Resultado real obtenido** conserva completos Producto, Piezas planeadas, Piezas reales y Comparación. Producto y Comparación distribuyen el espacio de forma flexible; las columnas numéricas tienen mínimos seguros, encabezados y valores centrados, editor con padding/foco nativo y filas automáticas desde 48 DIP. La comparación admite wrapping sin perder información. Resize y reorder están bloqueados únicamente en esta tabla.

La corrección se limita a Presentation/WPF y pruebas. Focales: 78/78 generales de Producción y 3/3 WPF de Producción. Suite oficial **797/797** (584 general + 213 WPF), 0 fallidas/omitidas; Release `src/CasaMiga.Presentation/bin/Phase1K4G-B-R1/` con 0 errores/advertencias. Fixture schema 17 reutilizada sin registrar el borrador, sin migración 018, sin push y sin PR. Ver [PHASE_1K4G_B_R1_REPORT.md](PHASE_1K4G_B_R1_REPORT.md).

## Registro anterior — 1K.4G-B

# FASE 1K.4G-B TÉCNICAMENTE COMPLETADA — SMOKE HUMANO PENDIENTE, 10/10/2026

Producción ahora permite buscar todos los productos activos, agregarlos a un plan multiproducto y comprender su preparación mediante estados derivados `LISTO PARA PRODUCIR`, `RECETA INCOMPLETA` y `SIN RECETA`. Los productos bloqueantes permanecen visibles y explican la configuración faltante. El flujo reutiliza la consolidación exacta de fórmulas compartidas, preview sin escritura, borrador inmutable, resultado real por producto y confirmación atómica de consumo más salida terminada. Se añadió merma mínima de producto terminado sobre el ledger `WASTE` existente, con motivo, nota y protección contra saldo negativo.

Fixture aislada `work/phase1k4g-b-smoke-20261010`, schema 17, con A+B compartiendo preparación, C con faltante, producto sin receta, perfil incompleto, MASS/VOLUME/COUNT, salidas vinculadas y borrador para resultado real menor. Focales de producción 78/78, focales WPF 3/3 y suite oficial **796/796** (584 general + 212 WPF), 0 fallidas/omitidas. Release `src/CasaMiga.Presentation/bin/Phase1K4G-B/`: 0 errores/advertencias. No migración 018, no datos comerciales, no impresión y Reporte Diario intacto. Ver [PHASE_1K4G_B_REPORT.md](PHASE_1K4G_B_REPORT.md).

## Registro anterior — 1K.4H-A-R1

# Integración de QA en main — 12/10/2026

Se combinó QA (1K.4E ticket físico, 1K.4F encabezado y Centro operativo, 1K.4G auditoría de recetas y producción, 1K.4H-A reporte diario) en main por PR. A petición del usuario se conserva el diseño de QAv1/main: acceso con la hora grande (opción B) y barra superior del rediseño A. Las funciones de QA viven dentro de él: el reloj y el aviso del Centro operativo son píldoras de la barra (`OperationsButton`, `OperationsStatusButton`), «Diario» en el riel abre el Reporte diario y pantalla completa sigue siendo el botón de ícono (`FullScreenButton`). El ticket conserva el formato físico de 1K.4E; con tarjeta o transferencia imprime «Pago: Tarjeta» o «Pago: Transferencia» y «Pagado:», sin Recibido ni Cambio. Las pruebas de QA se adaptaron a ese diseño. Pendiente: CI de Windows del PR y prueba humana del ticket y del encabezado.

# FASE 1K.4H-A-R1 TÉCNICAMENTE COMPLETADA — SMOKE HUMANO PENDIENTE, 10/10/2026

R1 refina el Reporte Diario sin cambiar su naturaleza read-only. Ventas por hora usa una timeline acotada con `payment.applied_cents`, convierte timestamps mediante la zona horaria inyectada y agrupa en memoria; muestra primera/última venta y hora pico con desempate por importe, tickets y hora temprana. Caja comunica apertura, cierre, duración, responsable y múltiples sesiones sin alterar métricas. La UI conserva cuatro KPI, una sola frase resumen, tabla histórica con encabezado humano y estados futuros deliberados.

El botón global ahora dice `Pantalla completa · F11` o `Salir de pantalla completa · F11`, cambia icono/ayuda accesible inmediatamente y conserva clic, F11 y Escape. Revisión explícita: 1280×820, 1920×1080, 3440×1440 y F11, sin clipping ni scroll horizontal global; el reporte limita su ancho a 1480 DIP.

Focales generales 7/7; focales WPF 6/6 y regresión WPF ampliada 23/23. Suite oficial final **794/794** (582 general + 212 WPF), 0 fallidas/omitidas; Release 0 errores/advertencias. Base protegida intacta con SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`; schema 17 sólo en fixture, sin migración 018. Build `src/CasaMiga.Presentation/bin/Phase1K4H-A-R1/`, launcher `work/Abrir-Casa-Miga-1K4H-A-R1.cmd`, DataDirectory `work/phase1k4h-a-r1-smoke-20261010`. Ver [PHASE_1K4H_A_R1_REPORT.md](PHASE_1K4H_A_R1_REPORT.md). No push; smoke humano pendiente.

## Registro anterior — 1K.4H-A

Casa Miga incorpora **Reporte diario** de sólo lectura con fecha local, cuatro KPIs, top 5 por snapshots históricos, resumen consolidado de caja y detalle de múltiples sesiones. Usa `payment.applied_cents`, límites `[inicio local, día siguiente)` convertidos a UTC, `sales.view` + `cash.view` y consultas SQL agregadas sin N+1. Categorías se omiten porque no están snapshotteadas. Producción, materias primas, preparación de mañana y merma distinguen dato no disponible de cero real.

Fixture aislada `work/phase1k4h-a-smoke-20261009`, schema 17, escenarios A–G y credencial verificada. Focales de datos 4/4, regresión ventas/caja/ticket 79/79, focales WPF 4/4 y regresión WPF 27/27. Suite final **791/791** (581 general + 210 WPF), 0 fallidas/omitidas; Release 0 errores/advertencias. El build identificado, hashes y commit local se registran en [PHASE_1K4H_A_REPORT.md](PHASE_1K4H_A_REPORT.md). No migración 018, no push, ticket/impresión/operations hub y recetas comerciales intactos.

## Registro anterior — 1K.4G-A

# FASE 1K.4G-A — AUDITORÍA MAESTRA COMPLETADA, 09/10/2026

El checkpoint aprobado previo se publicó mediante avance normal en `origin/QA`: `30887e85de0c3c1505c67c7c4169092e5e9b2b54` → `5f5da76f2885ce9deaa2e74f3f3f33519eced5ec`, sin commits remotos exclusivos, force, merge, rebase, reset ni rama nueva. La auditoría posterior permanece local y exclusivamente documental.

La copia aislada vigente contiene 31 productos activos en cinco categorías, pero **cero fórmulas, cero perfiles de producción, cero ítems terminados vinculados y cero lotes**. Sólo existe Harina como materia prima; su saldo de 50 kg y presentación Bulto 25 kg pertenecen a la copia de revisión y no se declaran comerciales. Los 31 productos quedan `FALTA INFORMACIÓN`; 0 completos y 0 parciales.

La arquitectura sí soporta fórmula base compartida, ingredientes directos, alternativas, agregación multiproducto, escala exacta MASS/VOLUME/COUNT, preview sin reserva, borrador con snapshots y confirmación atómica de consumo más salida real. Producción puede sumar producto terminado y el consumo por ventas puede restarlo, pero la copia no tiene perfiles, ítems terminados o `product_inventory_link`, y el flag de consumo está `DISABLED`.

Documentos: `PHASE_1K4G_AUDIT.md`, `PHASE_1K4G_PRODUCT_MATRIX.md` y `PHASE_1K4G_RECIPE_QUESTIONNAIRE.md`. Verificación completa: **783/783** (577 generales + 206 WPF), 0 fallidas/omitidas; Release 0 errores/advertencias. Base protegida schema 16 y SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`; fixture schema 17. No se cargaron recetas ni datos comerciales, no se creó migración 018 y no se inició 1K.4G-B/1K.4H. El siguiente paso es obtener respuestas reales producto por producto y por masa.

## Registro anterior — 1K.4F-R2 + 1K.4E-R2

# FASES 1K.4F-R2 + 1K.4E-R2 TÉCNICAMENTE COMPLETADAS, 09/10/2026

El pase controlado concluyó en dos correcciones independientes. El commit A separa en el header estado real de caja, resumen contextual, reloj clickable y F11. El commit B usa `Business.Phone` para imprimir «Pedidos por WhatsApp» y `461 276 7429`, con un raster local 1-bit de teléfono en círculo de 32×32, después de la dirección y antes del primer separador. Original y reimpresión recorren el mismo renderer; Variante 04 permanece intacta a 446 puntos.

Focales A **21/21** y B **27/27**. Suite conjunta `scripts/verify.ps1`: **783/783** (577 generales + 206 WPF/Presentation), 0 fallidas y 0 omitidas; Release 0 errores/advertencias. Payloads fuera de línea: original 24,063 bytes, SHA-256 `0C32856DBC6D8F24B2612A63941F9AB68527FB67115C952650160E1DA3629B4A`; reimpresión 24,098 bytes, SHA-256 `47B73172C36A03B9231256C21B76A92233C19D399605E6172FB358D4F7B43699`. Un ESC t 2, un logo, un icono de pedidos, un raster social, un corazón, un GS V 1, seis LF y cero ESC p. Ningún trabajo se envió a POS-80.

Revisión conjunta: `src/CasaMiga.Presentation/bin/Phase1K4F-R2/`, launcher `work/Abrir-Casa-Miga-1K4F-R2.cmd`, DataDirectory `work/phase1k4f-r2-smoke-20261009`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4F-R2`. Base protegida schema 16 y SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`; fixture schema 17; sin migración 018. Smoke humano de header y prueba física del ticket pendientes. Sin push/PR; no iniciar 1K.4G/1K.5.

## Registro de bloque A — 1K.4F-R2

El encabezado operativo separa ahora cuatro piezas compactas: estado real de caja, resumen contextual del Centro operativo, reloj/fecha y acceso F11. El estado usa `MainViewModel.HasOpenCash`, que se actualiza desde `CashService.Current` y las operaciones reales de apertura/cierre; no existe estado financiero duplicado. El resumen desaparece sin actividad y prioriza urgente, listo y activo. El reloj conserva `HH:mm`, fecha compacta y abre el panel; F11 mantiene un target de 40 DIP, texto visible y ayuda accesible.

El bloque no cambió la altura del header ni la lógica del Centro operativo R1. Pruebas focales: **21/21** (19 de Centro operativo/modelo/WPF y dos del shell), sin fallas ni omisiones. Se verificaron caja abierta/cerrada, ausencia/actividad/listo/urgente/combinación, click del reloj, F11, 1280×820, 1920×1080 y 3440×1440, además de que los ticks no notifican ni recrean POS/Catálogo. Ver `PHASE_1K4F_R2_REPORT.md`. Continúa el bloque B independiente de 1K.4E-R2 antes del build conjunto; sin push.

## Registro anterior — 1K.4F-R1

# FASE 1K.4F-R1 TÉCNICAMENTE COMPLETADA — SMOKE VISUAL HUMANO PENDIENTE, 09/10/2026

R1 convierte el Centro operativo en una vista de trabajo primero. Los editores de temporizador y nota aparecen colapsados; `+ Nuevo`/`+ Nueva` los abren dentro de la misma sección, Cancelar descarta sin crear y una creación o guardado válido vuelve automáticamente a la lista. Editar una nota reutiliza el editor con el rótulo «Editar nota». Las tarjetas reducen moderadamente padding y separación; a 1280×820 las pruebas WPF confirman al menos tres tarjetas de temporizador completas con los editores cerrados.

Un recordatorio listo muestra Atender, Posponer, Editar y Eliminar. Posponer revela inline únicamente 5/10/15 minutos, cierra tras elegir y también responde a Escape. Escape cancela primero el editor o desplegable transitorio y después cierra el panel; F11, Alt+Tab y minimizar conservan la semántica previa. Header y login permanecen intactos para evitar regresiones visuales. Alertas `¡Listo!`, doble pulso/micro-shake de 600 ms, sonido, taskbar, iconos y Normal/Urgente no cambiaron.

La persistencia se auditó sobre `work/phase1k4f-r1-smoke-20261009`: nota simple, recordatorio urgente y timer activo se recargaron desde `operations-hub.json`; tras un minuto simulado el timer siguió Running con 2,640 segundos, consistente con su deadline UTC. El archivo continúa fuera de `.cmbackup`; backup/restore no se modificó. Focales **16/16**; suite **779/779** (576 generales + 203 WPF/Presentation), 0 fallidas y 0 omitidas. Release identificado 0 errores/advertencias. Base protegida schema 16 y SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`; fixture schema 17; no existe migración 018. Impresión 1K.4E-R1, Qian, `printing.json`, cajón y datos comerciales permanecen intactos. Ver `PHASE_1K4F_R1_REPORT.md` y `evidence/phase1k4f-r1-verification.md`.

Build de revisión: `src/CasaMiga.Presentation/bin/Phase1K4F-R1/`; launcher `work/Abrir-Casa-Miga-1K4F-R1.cmd`; título `Casa Miga POS · Caja · DESARROLLO · 1K.4F-R1`. No push. No iniciar 1K.4G ni 1K.5.

## Registro anterior — 1K.4F

# FASE 1K.4F TÉCNICAMENTE COMPLETADA — SUSTITUIDA POR R1, 09/10/2026

Casa Miga incorpora un Centro operativo en Presentation sin migraciones ni cambios de negocio. El Header D muestra hora local `HH:mm`, fecha abreviada y un indicador acotado de activos/listos/urgentes. El botón completo abre el Panel 4 de 420 DIP dentro del mismo árbol visual y HWND; no existe Popup o Window principal adicional. Escape lo cierra antes de salir de F11, y desactivación/minimización también lo cierran para evitar superficies huérfanas.

Los temporizadores usan deadline UTC, remaining real al pausar, nuevo deadline al reanudar y un único `DispatcherTimer` de presentación. El tick notifica únicamente propiedades nombradas, no llama `PropertyChanged(null)`, no guarda cada segundo y no toca Catálogo, POS, fotos ni Views. Se admiten múltiples timers, presets 5/10/15/30/45/60, pausa, reanudación, reinicio, eliminación y atención. Timers y notas comparten iconografía vectorial local, prioridad Normal/Urgente y persistencia retrocompatible; recordatorios listos pueden posponerse 5/10/15 minutos.

Un vencimiento conserva `¡Listo!` y genera una sola alerta agrupada por tick: doble pulso + micro-shake de 600 ms en Header D, `SystemSounds.Asterisk` una vez y, sin foco/minimizado, tres destellos discretos de barra de tareas mediante `FlashWindowEx` sin restaurar ni robar foco. No hay MessageBox, loop ni animación continua.

La persistencia local versionada está en `DataDirectory/operations-hub.json`, con escritura atómica; sobrevive navegación y reinicio. La auditoría de `InstallationBackup` confirma que `.cmbackup` sólo incluye DB, terminal y fotos activas, por lo que este archivo no se respalda ni restaura. Integrarlo requeriría un cambio estructural posterior. Login opción 3 muestra únicamente hora y fecha junto a los campos; nunca expone temporizadores, notas ni recordatorios antes de autenticar.

Fixture aislada `work/phase1k4f-smoke-20261008`; build `src/CasaMiga.Presentation/bin/Phase1K4F/`; launcher `work/Abrir-Casa-Miga-1K4F.cmd`; título `Casa Miga POS · Caja · DESARROLLO · 1K.4F`. Focales **13/13**; suite completa **776/776** (576 generales + 200 WPF/Presentation), 0 fallidas y 0 omitidas. Release: 0 errores y 0 advertencias. Base protegida schema 16 y SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, sin cambios; sólo la fixture avanza a schema 17. No existe migración 018. Impresión 1K.4E-R1, Qian, `printing.json`, cajón y datos comerciales permanecen intactos. Ver `PHASE_1K4F_REPORT.md` y `evidence/phase1k4f-annex-verification.md`.

## Registro anterior — 1K.4E-R1

# FASE 1K.4E-R1 TÉCNICAMENTE COMPLETADA — SMOKE FÍSICO PENDIENTE, 08/10/2026

La discrepancia física de 1K.4E provenía de la composición real compartida: `TextReceiptRenderer` colocaba las redes en la cabecera, carecía de separador entre TOTAL y pago y dejaba sólo cuatro LF finales; las pruebas verificaban presencia, no el orden físico completo. No existía un renderer alterno ni una configuración vieja en la fixture. R1 conserva el pipeline validado y ordena explícitamente original/reimpresión: «Hecho en:», logo, dirección, separador, marca opcional, venta, productos, totales, pago, pie y corte.

Variante 04 se redujo de 490 a 446 puntos (9.0%), sin deformación. Los nombres de producto usan hasta 31 columnas y tienen respiración vertical; TOTAL conserva doble altura y restaura el tamaño. Se agregaron separadores antes del pago y del pie, seis LF finales, corazón y corte parcial. Por instrucción posterior del usuario, el pie dice «Síguenos en:», imprime un raster 1-bit con los logotipos de Facebook e Instagram y después sólo `Casa Miga   @casamiga126`; las palabras de las plataformas ya no se imprimen.

Fixture aislada `work/phase1k4e-r1-smoke-20261008`, schema 17, venta `#000017`, total $72, recibido $100 y cambio $28. Payloads fuera de línea: original 23,853 bytes, SHA-256 `50DE3C3831D9F11B1A0CA41F6993123A45B49EE7784AD300EC989BBAA5C1D0EB`; reimpresión 23,888 bytes, SHA-256 `58A496A312948C5D00BFEC055BF9F8AD1CEA051255DAD8067DA2FF552DDC3BFA`. Auditoría: un logo, un raster social, un corazón, un corte, cero ESC p. Focales 30/30; suite completa 763/763 (576 generales + 187 WPF), sin fallas/omisiones; Release 0 errores/advertencias. Base protegida intacta, schema 16 y hash `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. Sin migración 018 ni impresión automática. Ver `PHASE_1K4E_R1_REPORT.md`.

## Registro anterior — 1K.4E

# FASE 1K.4E TÉCNICAMENTE COMPLETADA — SMOKE FÍSICO NO APROBADO, 08/10/2026

El ticket Qian definitivo conserva ESC/POS RAW, 48 columnas, CP850/ESC t 2 y corte parcial GS V 1. Usa Variante 04 a 490 puntos de ancho (12.5% menor que R2), «Hecho en:», dirección oficial, `Venta #NNNNNN`, fecha local, cajero humano, snapshots históricos, importes humanos, redes, mensaje final y corazón raster. `REIMPRESIÓN` queda visible antes de la venta; no crea venta/pago, no mueve caja/inventario y no abre cajón. El teléfono es configuración opcional: null/vacío no agrega línea.

Fixture aislada `work/phase1k4e-smoke-20261008`, schema 17, venta sintética `#000017`, total $72, recibido $100, cambio $28 y caja abierta para revisión. Sus payloads original/reimpresión fueron generados fuera de línea; ninguno fue enviado a `POS-80`. Focales 28/28; suite completa 761/761 (574 generales + 187 WPF), sin fallas ni omisiones; Release 0 errores/advertencias. La base protegida permanece schema 16 y SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. Sin migración 018. Ver `PHASE_1K4E_REPORT.md`, `printing-variant04.md` y `evidence/phase1k4e-byte-audit.json`.

# FASE 1K.4D-R1 CERRADA Y APROBADA — 08/10/2026

# Catálogo y celular — 12/10/2026

- **Panes nuevos** (pedido del usuario): 27 Pan de muerto grande, 28 Pay de queso y 29 Pay de elote, $22 cada uno, y 30 Bisquet, $13, y (15/10/2026) 31 Baguette, $9, y (19/10/2026) 32–42, las 11 altas del catálogo comercial de QA (rosquita, bollo con ajonjolí, cuatro donas, dos berlinesas y tres galletas), en el catálogo autorizado (`docs/catalog`), la siembra de revisión y la demo de la web local. El pan de muerto grande y el bisquet van en «Pan dulce»; los dos pays, en la categoría nueva «Pays»; la baguette, en la categoría nueva «Pan salado»; las altas de QA, en Pan dulce, Dona y Galleta. Todos tienen foto provisional generada con IA como las otras 26 (`assets/catalog-provisional-ia`, procedencia `PROVISIONAL`; reemplazar por fotos reales antes de producción). En la sucursal real se dan de alta en Catálogo.
- **Web en celular:** las tablas en tarjeta llevan el primer dato como título a todo el ancho (el pan con su foto, el folio, el usuario) y se resalta la tarjeta completa; en Ventas se ocultan terminal, CashSessionId y «Devuelto $0» (siguen en el detalle); búsqueda del historial compacta; los periodos (Hoy, Ayer…) se acomodan en renglones en vez de cortarse; Venta con la caja cerrada avisa por qué lleva a Caja. Revisión: `cloud/scripts/phone-review.ts` (11 pantallas a 375 px, ninguna se desborda).
- **Prueba local:** entrada automática temporal con un usuario fijo por rol (admin, encargado, cajero); `npm run local -- <carpeta> 4173 --sin-contrasena --demo` en Mac/Linux con la demo completa (42 panes con foto, ventas, charolas, inventario y recetas). Ver README.

# Estabilización para el piloto — 10/10/2026

**Implementado y probado.** Pedido del usuario: «arranca con todo en ese orden» (pruebas → documentos → producción → piloto); sin funciones nuevas.

- **CI:** nuevo `ci-cloud.yml` (typecheck, 72 pruebas de la nube, 10 recorridos en navegador); el CI de la caja instala las dependencias de la web para la prueba de punta a punta y anota cada prueba fallida en el correo y en el resumen.
- **Navegador real** (`cloud/test-browser`, playwright-core sobre Edge/Chrome instalado, login real): entrar, vender (efectivo y tarjeta), tarjetas, historial, reportes, catálogo «Por confirmar», usuario nuevo con contraseña temporal, tienda nueva, corte con faltante y celular sin desbordes. Arreglos que salieron: foto de las tarjetas a todo el ancho y avisos que ya no se enciman.
- **Punta a punta** (`CloudEndToEndTests`): la caja real contra la nube local (`cloud/scripts/e2e-server.ts`): alta, subida, login web con la contraseña de la caja, precio visible al momento, venta web, la caja aplica y rechaza, reportes con la venta web, y **simulacro de respaldo y restauración** en carpeta nueva.
- **Volumen** ([VOLUME.md](VOLUME.md)): un año (109,500 ventas). Caja: Resumen de un año 100 s → 1.6 s (los reportes ya no unen las vistas `report_*`; **migración 030** con índices por fecha). Web: **migración de la nube 009** (`cm_utc` e índices) y consultas por rango UTC; Resumen de 92 días 51 s → 9 s en PGlite, pronóstico 24 s → 0.08 s. Ganancia en la web descuenta devoluciones por su fecha, igual que la caja.
- **Aviso en la web** (encargado y administrador): si una caja de escritorio tiene la caja abierta y no se conecta desde hace más de 15 minutos, sale un aviso arriba (`/api/web/alerts`, revisado cada minuto); con la caja cerrada no avisa. Pruebas: nube `alerts.test.ts`, navegador «alert».
- **Ticket de la web igual al de la caja:** logo Variante 04 (el mismo PNG de 576 puntos), folio, fecha con zona, cajero, terminal, TOTAL grande y Efectivo / Aplicado / Cambio. Vista previa sin imprimir: `cloud/scripts/ticket-preview.ts`. Pendiente de decisión: la caja imprime «Pago CASH» en efectivo (formato validado en la impresora física; cambiarlo a «EFECTIVO» requiere un nuevo smoke físico).
- **Instalador 1.0.0** (candidato para el piloto): `artifacts/installer/CasaMigaPOS-1.0.0-setup.exe`, SHA-256 `02D4081A40D8255EB7353A6E63277739A4B57750361B763A7B16C8187F4A5E41`, sin firma. Comprobado: la herramienta publicada crea una base nueva en la versión 30 sin .NET instalado. No se instaló en esta PC (mismo identificador que una instalación real).
- **Documentos:** [MANUAL.md](MANUAL.md) (cajero, encargado, administrador, web), [PILOTO.md](PILOTO.md), [RELEASE_NOTES_1.0.md](RELEASE_NOTES_1.0.md) y [NEXT_STEPS.md](NEXT_STEPS.md) al día.
- Verificación: `verify.ps1` 727 + 210 (1 omitida: volumen, se activa con `CASAMIGA_VOLUME=1`); nube 73 + 11 en navegador.

# Todas las pantallas de la web iguales a la caja (Catálogo a Usuarios) — 09/10/2026

**Implementado y probado en local.** Pedido del usuario: «Sigue con Ventas y el resto de pantallas y termina todo… tanto la App de C# como la de Web con todas las pruebas correspondientes». Plan por fases en [PLAN_COMPLETAR.md](PLAN_COMPLETAR.md).

- **Catálogo (W3, 24b9178):** pestañas Productos/Categorías, tabla de 50, editor con origen de foto, recorte manual y Quitar foto, «+ Nueva categoría» sin salir del producto. Migración de la nube **007**: el tipo de cambio se valida por formato.
- **Inventario (W4):** copia de InventoryView: consumo automático por ventas (estado, Verificar configuración, Activar/Pausar/Reanudar con confirmación), búsqueda en vivo con Tipo y Estado, lista para elegir, tabla de 50; detalle con existencia, «Inventario consistente» o advertencia, Editar, Activar/Desactivar, Saldo inicial, Entrada, Merma, Conteo físico (con la misma vista previa), presentaciones, asociaciones con productos e historial de 25 por página. Costo de referencia sigue como extra de la web.
- Cambios nuevos que aplica la caja (`CloudCommandService`): `inventory.opening`, `inventory.item.save` (la web fija el id), `inventory.item.active`, `inventory.unit.add`, `inventory.link`, `inventory.unlink` e `inventory.consumption` (manda el estado y la versión que vio; si cambió, se rechaza como en la caja; si la verificación falla, el motivo lista los problemas). Reintentar no repite nada.
- **Migración de la caja 027:** el estado del consumo automático sube dentro de la sucursal (`branch.sale_consumption`). Textos de «Verificar configuración» compartidos (`ConsumptionProblems`).
- **Recetas (W5):** copia de RecipeView con recetas y perfiles de producto: versiones, borradores, publicar y activar; editores con ingredientes directos, grupos de alternativas, preparaciones publicadas y política de cantidad. Cambios `recipe.formula.*` y `recipe.profile.*`: la caja numera la versión, revisa artículos activos, preparaciones publicadas, código y producto sin perfil antes de escribir, y no repite lo ya aplicado. Textos de error compartidos (`RecipeProblems`). Pruebas: caja `CloudRecipeCommandTests` (3), nube `recipes.test.ts` (2).
- **Producción (W6):** copia de ProductionView en dos pasos: plan con piezas, Calcular necesidades (la nube porta ProductionPlan/ProductionScaling con fracciones exactas, piezas hacia arriba y alternativas por preferencia), Preparar registro (`production.prepare`) y Registrar producción con lo que salió (`production.confirm`, sólo quien creó el borrador); borradores e historial con detalle. **Migración 028:** cada producción sube también sus preparaciones (y se reenvían las existentes). Pruebas: caja 2 nuevas en `RecipePersistenceTests.WebProduction`, nube `production.test.ts` (2).
- **Charolas (W7):** copia de ConsignmentView: Ruta del día, Entregar a pantalla completa, Recoger con saldo anterior y anulación, Cobrar, Tiendas con charola habitual y estado de cuenta, Precios de charola (`consignment.price.save`) y Reporte del periodo (la nube porta Summarize y Account de la caja: anuladas fuera, % de venta con redondeo al par). Pruebas: caja `TrayPricesFromTheWebWorkLikeThePricesTab`, nube `trays.test.ts` (4).
- **Usuarios (W8):** copia del panel Usuarios con confirmación al restablecer; en el inicio «Cambiar contraseña» (política de la caja con lista en vivo) y «¿Olvidaste tu contraseña?». La contraseña temporal ya no manda a la caja: abre «Cambiar contraseña». La nueva vale en la web al momento y llega a la caja como `user.password` (sólo el propio usuario; `IdentityService.ChangeOwnPasswordWithHash`).
- **Web independiente (I1):** categorías, productos (alta, edición, activar), usuarios (alta, edición, activar, restablecer contraseña) y tiendas (datos, activar, charola habitual) cambiados en la web **se ven y funcionan en la web al momento**, aunque la caja esté apagada: un usuario creado o restablecido ya entra a la web; un precio nuevo ya cobra en la Venta web. Se marcan «Por confirmar» hasta que la caja responde. La nube escribe en `entity_state` lo que la caja dejará (`cloud/lib/optimistic.ts`, migración de la nube **008**); cuando la caja sube su versión, gana; si la caja lo rechaza, el registro vuelve a lo último que subió la caja (o desaparece si era nuevo) y los cambios que siguen pendientes se vuelven a poner encima. Inventario, producción, recetas y la ruta siguen esperando a la caja. Pruebas: nube `optimistic.test.ts` (5).
- **Ventas web en los reportes de la caja (D5):** nueva ruta de la nube `GET /api/sync/web-sales` (con la llave de la caja; sólo cajas web de su sucursal; cursor por caja web; hasta 500 registros) y en la caja `SqliteWebSalesMirror` + **migración 029** (tablas `web_*` de sólo lectura, sin subir, y vistas `report_*` que suman lo local y lo web). `SyncEngine` la baja después de aplicar los cambios; una nube sin la ruta responde 404 y no pasa nada. Resumen, Comparativo, Ventas por hora, Control de caja (con los turnos web, su esperado y su diferencia) y Ganancia cuentan las ventas web; un cajero web que aún no llega a la caja sale como «Usuario de la web». Pruebas: caja `WebSalesMirrorTests` (5), nube `webSales.test.ts` (2).
- **Charolas en la caja (D3):** pestañas **Entregas** (también anuladas, con estado, vendidas/regreso, cobrado y contenido) y **Cobros** (forma, referencia, nota y total) del periodo del Reporte, hasta 300 de cada una como la web (`ConsignmentService.Period`). Prueba: `ConsignmentTests.DeliveriesAndPaymentsOfThePeriodListEverythingNewestFirst`.
- **Caja igual a la web (D1, D2, D4):** Reportes → **Ganancia** en la caja (`ProfitService`, mismo cálculo que la web: costo por pieza desde la receta activa o la última publicada con la alternativa preferida; en charolas lo que regresa cuenta como costo; CSV «ganancia»; sólo con ver ventas y ver recetas). Inventario → **Costo de referencia** en el detalle del artículo (encargado y administrador). Historial de ventas con **forma de pago** y **producto** (sin distinguir mayúsculas; se combinan con el periodo; la búsqueda por folio los ignora). Pruebas: `RecipePersistenceTests.Profit` (2), `SalesHistoryTests.PaymentMethodAndProductFiltersCombineWithThePeriodBeforePaging`.
- Pruebas: caja `CloudInventoryCommandTests` (3), nube `inventory.test.ts` (4); probado en el navegador con la base de demostración (saldo inicial, presentación, asociación, artículo nuevo, merma con motivo Otro, conteo con diferencia, activar consumo).

# Web igual a la caja de escritorio y sincronización cada 5 segundos — 09/10/2026

**Implementado y probado en local.** Pedidos del usuario: «Haz una copia tal cual de POS en Web» (primero el diseño, después la web independiente; se siguen usando HTML/CSS/JavaScript y TypeScript) y «actualízala para que sea la subida de datos cada 5 seg y la base de datos esté actualizada siempre».

- **Diseño:** marco, Punto de venta, Cobro y Tu caja copiados de la caja (rediseño A de QAv1): riel con los mismos íconos y grupos, barra superior con estado de caja, nube y usuario, tarjetas de 236 px y 50 por página, ticket con Cobrar / Cobrar + imprimir ticket, cobro segmentado, Abrir caja, Conteo y Resultado de caja con nota de discrepancia. Resumen pasó a Reportes y Cajas/Respaldos a Nube (las direcciones viejas redirigen). Detalle en [POS_WEB.md](POS_WEB.md).
- Comparado con capturas de la caja a 1280×820 (Edge sin ventana): mismas medidas (4 columnas de ~181 px con barra de desplazamiento, ticket de ~334 px). Probado en el navegador: venta C02-000001 en efectivo ($54, cambio $46), gasto de gas $50, conteo $510 → esperado $504, «SOBRANTE $6.00», cierre con nota, Venta manda a Caja con la caja cerrada, reabrir. Celular sin desbordes.
- **Sincronización cada 5 s** (antes 60 s): la caja sube lo pendiente y baja los cambios de la web; sin nada pendiente sólo hace una consulta. La vuelta automática no muestra «sincronizando…» para que la barra no parpadee; tocar la nube sí. La web se actualiza sola cada 15 s (antes 60 s).
- Pendiente: Historial de ventas con las acciones de la caja y la web independiente de la caja (decisión de quién manda sobre los datos).

# Punto de venta en la web, fase 3: inventario, producción y fotos — 09/10/2026

**Implementado y probado.** Pedido del usuario: «Termina todo el POS a WEB». Ver [POS_WEB.md](POS_WEB.md).

- Desde la web: **Entrada**, **Merma** y **Conteo** de inventario (el primer conteo de un insumo es su saldo inicial), **Registrar producción** (panes con receta activa, planeadas y las que salieron) y **foto del producto** (recortada en cuadro en el navegador). La nube valida y encola (migración de la nube **006**); la caja de escritorio los aplica con sus reglas y permisos (`CloudCommandService` + `CloudCommandModules`), sin repetir nada si los aplica dos veces. Producción sin existencia se rechaza sin dejar borrador y dice qué falta.
- Inventario y Producción en la web muestran **«Enviado a la caja»** (pendiente / rechazado con motivo).
- Corrección: las cajas web ya no cuentan como cajas que aplican cambios (un cambio podía quedarse en «1 de 2 cajas»). La caja ya no vuelve a bajar lo que reportó y cada lote pesa como máximo ~3 MB.
- Pruebas: nube 46/46 (inventario, producción, fotos, lotes); caja: inventario y permisos (`CloudCommandTests`), producción (`RecipePersistenceTests.WebProduction`), foto (`WebPhotoCommandTests`). Probado en el navegador: conteo de harina 25.5 kg, merma con «Otro» pide observación, entrada de azúcar 10 kg, producción de conchas 24 → 22, foto nueva de un producto, vista de celular.
- Sigue en la caja: recetas (fórmulas y versiones), alta de insumos, impresora térmica y cajón. La web no funciona sin Internet.
- Vercel: el conector de Vercel de esta sesión ve el equipo «Alex Poumian's projects» pero **no tiene permiso para crear proyectos** (403); la publicación queda en manos del usuario (ver mensaje de la sesión).

# Rama QA fusionada en QAv1 (fases 1K.4B–1K.4D) — 09/10/2026

**Fusionada y verificada.** A pedido del usuario se juntó la rama `QA` (9 commits: carrito, historial de ventas, cierre e historial de caja; smoke aprobado por el usuario en esa rama) con QAv1. **Decisión del usuario: se conserva el diseño de QAv1**; lo de QA entra como funciones dentro de ese diseño.

- **Carrito (1K.4B):** operaciones del carrito en fila (sin duplicar ni perder toques), el renglón nuevo se desplaza a la vista, el catálogo no parpadea ni recarga fotos (la foto del ticket reutiliza la de la tarjeta). El ticket es el de QAv1 (renglones con foto, nombre en un renglón); el resumen «N productos · N piezas» queda en el modelo, sin cambiar la pantalla.
- **Historial de ventas (1K.4C):** pantalla de QAv1 (folio corto, fechas, tabla con Devuelto y CashSessionId, detalle en texto, cancelaciones y devoluciones). De QA: atajos **Hoy / Ayer / Sesión actual** (botones secundarios de QAv1 bajo las fechas), fechas en `AAAA-MM-DD` **o** `dd/mm/aaaa`, zona horaria de la caja, mensajes de fecha y resumen del filtro. El control con máscara de QA queda en el código sin usarse.
- **Caja (1K.4D):** con faltante o sobrante el cierre exige **nota de discrepancia** (1–500, también en la base por trigger); **historial de cierres** de la caja con detalle (identificación, resumen y nota); «Cerrar caja» sólo para quien puede. Tablero, textos y resultado del corte de QAv1 («SOBRANTE $…», «Confirmar cierre»); la nota y los cierres anteriores se agregan debajo.
- **Migración:** la nota de QA era su 017; en QAv1 la 017 ya era la de contraseñas temporales. Ahora es la **026** (mismo archivo, byte por byte). `Database.Initialize` reconoce una base de la rama QA por la huella de su 017, la registra como 026 y aplica 17–25 en la misma transacción (`QaBranchMigrationTests`). Una 17 desconocida sigue rechazándose. Una base de la rama QA (versión 17) es la causa más probable del «No se completó la operación» antes del login reportado en la otra Windows; la fusión la abre.
- **Web:** el cierre de una caja web con diferencia también pide la nota; Caja del panel la muestra.
- Pruebas de QA adaptadas al diseño de QAv1 (textos, botones, folio corto, 2 renglones con foto visibles en el ticket a 1280×820, contraseñas de prueba dentro de la política 8–16).

# Punto de venta en la web, fase 2 y fotos en el panel — 09/10/2026

**Implementado y probado en local.** Ver [POS_WEB.md](POS_WEB.md).

- **Tickets** del turno y búsqueda por folio; **reimprimir** (encargado y administrador); **cancelar venta** (mismo turno, sin devoluciones) y **devolver productos** con motivo, con la instrucción de qué hacer con el dinero; **gastos, entradas y retiros** con categoría y nota. Mismas reglas que la caja: sólo antes del conteo, no sacar más efectivo del esperado, retiros sólo encargado/administrador, identificador de operación (migración de la nube 005).
- **Fotos de los panes en todo el panel**: más vendidos, ticket, charolas, análisis, ganancia, producción sugerida y recetas.
- Inventario: el descuento al vender está apagado en la caja; web y escritorio se comportan igual mientras siga así.
- Prueba en el navegador: venta C02-000001 de 2 conchas, devolución de 1 ($13, «Entrega $13.00 en efectivo»), gasto de gas $50; el panel muestra el turno con esperado $463 = 500 + 26 − 13 − 50. Pruebas de la nube 43/43.
- Pull de QAv1: el commit «Charolas» del usuario (README, .gitignore, .gitattributes) no afecta; `cloud/.gitignore` sigue ignorando `node_modules`, `.vercel` y `.env`.

# Punto de venta en la web, fase 1 — 08/10/2026

**Implementado y probado en local.** Ver [POS_WEB.md](POS_WEB.md).

- Decisión del usuario: POS web completo (vender y cobrar desde Mac, iPad o celular), por fases.
- **Cajas web** (migración de la nube 004): cada una con turno, fondo y folios propios (`CAJA-WEB-02` → `C02-000001`); el administrador las crea en Vender.
- **Vender** igual que en la app: categorías, buscador, fotos, ticket con − / +, cobro en efectivo (cambio y atajos), tarjeta o transferencia (exacto, referencia), ticket en pantalla e impresión del navegador; **corte** con conteo ciego y cierre.
- Reglas de la caja en la nube, una sentencia por operación: precio del catálogo vigente, productos y categorías activos, identificador de cobro (reintentar no duplica), sin ventas después del conteo, folios sin repetir con dos dispositivos.
- Las ventas y turnos web se guardan con el formato de la caja de escritorio: Resumen, Ventas, Reportes, Ganancia y Caja los cuentan igual.
- El cajero ya entra a la web, sólo a Vender (y Catálogo de consulta). Caja en la web pide `sales.view`: muestra el esperado del turno abierto y el conteo es ciego.
- Prueba en el navegador con la demo: cajera Ana abrió turno con $500, vendió C02-000001 en efectivo ($42, cambio $58) y C02-000002 con tarjeta ($40, ref. 4601); el encargado contó $540 → esperado $542, diferencia −$2, y cerró. Resumen y Ventas los muestran. Computadora, tableta y celular sin desbordes. Pruebas de la nube 42/42.
- App de escritorio, sólo copias de desarrollo: `dev-autologin.json` acepta `Users`; al salir, la pantalla de acceso ofrece un botón por cuenta (admin, cajeros) para entrar sin teclear, con el login normal. Una instalación normal lo ignora.
- Servidor de prueba (`npm run dev:web`) sin contraseña por decisión del usuario: se elige el usuario al entrar; `--con-login` la pide. La nube publicada siempre pide contraseña.
- Pendiente (siguientes fases): devoluciones y cancelaciones, gastos/retiros/entradas, reimpresión y descuento de inventario desde la web.

# Ganancia desde recetas y análisis de charolas — 08/10/2026

**Implementado y probado en local.** Ver [PROFIT.md](PROFIT.md).

- **Costo de insumos** (migración 025, `inventory_item_cost`): precio de referencia por insumo, capturado en la web (Inventario → tocar el insumo, administrador); la caja lo aplica (`inventory.cost`, auditado) y lo sube con el insumo.
- **Costo por pan** desde el perfil de producción publicado: ingredientes directos + preparaciones (alternativa preferida). Política de redondeo: costo por pieza al centavo; totales = costo por pieza × piezas.
- **Reportes → Ganancia**: mostrador contra charolas (las piezas que regresan cuentan como costo), margen, ganancia por pieza y por pan; avisa qué panes no tienen costo y por qué.
- **Charolas → Análisis**: por tienda, cada cuántos días pide, días habituales, vendido/regresado, cobrado y lo que más se vende allí.
- Corregido en la web: los tipos de insumo se veían como `RAW_MATERIAL` (ahora «Materia prima», etc., como en la caja) y las existencias en cero como «0 mg».
- Prueba en el navegador con la copia demo más una receta de prueba: Concha de vainilla a $4.08 por pieza × 438 piezas = $1,787.04 de costo, $3,906.96 de ganancia (69 %); un costo capturado en la web quedó en Cajas → Cambios como «Costo de insumo · $95.50 por kg». Vista de celular sin desbordes.

# Casa Miga en línea (versión web) — 07/10/2026

**Implementado y probado de punta a punta en local; publicación pendiente** (falta `npx vercel login` del dueño y capturar `DATABASE_URL`, `ENROLLMENT_SECRET` y `SESSION_SECRET`). Ver [WEB.md](WEB.md).

- **Caja → nube** (migración 023): además de ventas y cortes, la caja sube sucursal, usuarios (sólo el hash PBKDF2), permisos, catálogo con fotos (miniatura), gastos/retiros/entradas, charolas, inventario, recetas y producción. Lo existente se encola una vez; iniciar sesión ya no encola al usuario; lotes de 1.5 MB.
- **Nube** (`cloud/db/002_web.sql`, se aplica sola): último estado por registro, bloqueos del acceso web y cola de cambios por sucursal.
- **Web** responsiva (computadora, tablet, celular): entra cada usuario con su cuenta de la caja; administrador todas las sucursales, encargado la suya, cajero no entra. Resumen, Ventas con ticket, Reportes (comparativo, por hora, control de caja, producción sugerida), Caja, Charolas, Catálogo con fotos, Inventario, Recetas, Producción, Usuarios y Cajas.
- **Web → caja** (migración 024): precios, productos, categorías, usuarios (alta con contraseña temporal, rol, activar, restablecer) y tiendas/charola habitual. La caja los aplica en orden con sus propias reglas y permisos, informa «Aplicado» o «Rechazado» con el motivo y sube el resultado.
- Prueba de punta a punta: copia de la app registrada contra la nube local (`scripts/dev-web.ts` con la cola real exportada por `CasaMiga.Maintenance export-sync`): subió 2880 registros, el panel cuadró con la copia demo (1061 ventas, $71,583; charolas por cobrar $1,231), un cambio de precio hecho en la web quedó «Aplicado» y la caja mostró el precio nuevo.
- Corregido: tras reiniciar la app con el conteo ciego ya hecho, la tarjeta de movimientos volvía a aparecer; ahora el turno contado queda congelado.

# Gastos, retiros y entradas de caja — 07/10/2026

**Implementado; smoke manual pendiente.** En **Caja**, con la caja abierta, tarjeta «Movimientos de efectivo» (migración 022): **Gasto** con categoría fija (Insumos, Gas, Limpieza, Transporte, Mantenimiento, Otro con nota obligatoria), **Entrada** (efectivo que entra y no es venta) y **Retiro** (a resguardo; sólo encargado y administrador, el cajero no lo ve). Cambian el efectivo esperado del corte; sólo antes del conteo ciego; nunca se saca más de lo que debe haber, y las devoluciones en efectivo también lo respetan. Registros inmutables con operación idempotente y auditoría `cash.expense`/`cash.withdrawal`/`cash.deposit`; la base repite las reglas. El cierre desglosa gastos, retiros y entradas, y Reportes → Cortes muestra la columna «Mov.» (también en el CSV). Sólo registro, sin comprobante impreso (elección del usuario). Ver [CASH_MOVEMENTS.md](CASH_MOVEMENTS.md).

Probado en pantalla sobre una copia aparte de los datos: gasto «Otro» rechazado sin nota y registrado con nota, entrada, retiro mayor al efectivo rechazado, y el cierre desglosado (fondo $200 − gasto $45.50 + entrada $200 = esperado $354.50). La tarjeta va junto al estado de la caja a 1280 px y se oculta después del conteo ciego.

Build `src/CasaMiga.Presentation/bin/Review-QAv1-5958a90` sobre la copia `demo-charolas-QAv1-7c33343` (ya migrada a 022; la caja de hoy tiene fondo de $200 y sin ventas en efectivo, así que no deja sacar más de eso), launcher `work/Abrir-Casa-Miga-QAv1-5958a90.cmd`, con `dev-autologin.json` (entra como admin). Suite **853/853** (673 + 180), Release 0/0. Credenciales en la conversación.

# Rediseño A «Claro y cálido» y folios cortos — 07/10/2026

**Implementado; smoke manual pendiente.** El usuario eligió la dirección A de la propuesta (lienzo «Rediseño Casa Miga POS» en claude.ai). Tema nuevo en `Resources/Theme.xaml`: fondo cálido claro, superficies blancas con línea fina, Hoja de Aguacate como único color de acción, texto casi negro, letra Segoe UI Variable, botones principal/secundario/discreto/destructivo, campos con anillo de foco, listas desplegables, casillas, opciones, tablas sin cebra, barras de desplazamiento delgadas sin flechas y ayudas emergentes oscuras. Marco: riel blanco de íconos con nombres cortos (el nombre completo sigue en `AutomationProperties.Name`), barra superior con estado de caja, nube y ficha del usuario, sin pie de página; el logo oficial se usa completo (sin recortar el símbolo). Punto de venta con tarjetas que llenan el ancho (`TileGrid`), burbuja de piezas en el ticket, fotos en los renglones del ticket y «Cobrar $X». Charolas: recoger tocando la tarjeta (sin escribir números; «−» corrige) y entregar/charola habitual con − y + sin caja de número. Avisos propios (`AppDialog`) en lugar de `MessageBox`, salvo el cierre por error fatal y el aviso de doble apertura; diálogos de cobro y de descartar como tarjetas. Un solo botón principal por zona en Catálogo, Inventario, Recetas, Producción y Ventas.

**Folio visible C01-001061** (elegido por el usuario): clave de la caja tomada del número al final de su nombre («CASA-MIGA-CAJA-01» → C01) y consecutivo de seis cifras (`DisplayFolio`). Se muestra en el historial, el detalle, la confirmación de cancelación, el aviso de venta cobrada y los tickets nuevos. El folio interno (`Sale.Folio`, terminal + consecutivo) no cambia: sigue en la auditoría, la sincronización y el detalle («Folio interno»). La búsqueda acepta «C01-001061», «1061» o el folio largo de los tickets anteriores; las reimpresiones de ventas viejas conservan el folio con que se imprimieron. Una segunda caja debe llevar otro número al final de su nombre para no repetir la clave.

Build `src/CasaMiga.Presentation/bin/Review-QAv1-38aa9ad` sobre la copia `demo-charolas-QAv1-7c33343`, launcher `work/Abrir-Casa-Miga-QAv1-38aa9ad.cmd`, con `dev-autologin.json` (entra como admin). Suite **834/834** (661 + 173), Release 0/0. Credenciales en la conversación.

# Recoger sin cortes y doble apertura — 07/10/2026

**Implementado; smoke manual pendiente.** Recoger charola: el aviso vacío de la ventana ocupaba 39 px y hacía desplazar la página al tocar una foto; ahora no ocupa lugar. Tarjetas más bajas (dos por renglón a 1280 px; seis panes caben completos) y cobro sin desplazarse: importe y forma de pago en renglones compactos, y «Anular esta charola…» como paso aparte con su motivo (sin MessageBox). Abrir Casa Miga dos veces con la misma carpeta de datos ya no muestra «Cierre por error»: trae al frente la ventana abierta (o avisa que ya está abierta) y sale con código 0 sin registrar incidente.

Build `src/CasaMiga.Presentation/bin/Review-QAv1-d0baaf3` sobre la misma copia `demo-charolas-QAv1-7c33343`, launcher `work/Abrir-Casa-Miga-QAv1-d0baaf3.cmd`, con `dev-autologin.json` (entra como admin). Suite **829/829** (657 + 172), Release 0/0. Credenciales en la conversación.

# Charolas a consignación — 06/10/2026

**Implementado; smoke manual pendiente.** Nueva pantalla **Charolas** (Administrator y Manager) con migración 021: precio de charola por producto, tiendas cliente con charola habitual, entregar, recoger (lo que regresa se aparta para Piedra), cobros por tienda aparte de la caja con saldo pendiente, anulación de charolas sin recoger y reporte (% de venta, Piedra, cargado, pagado, saldo y margen que se queda en tiendas). Registros inmutables y validados por la base; auditoría `consignment.*`. `seed-review --demo-days` ahora también crea 3 tiendas con charolas diarias, recolecciones y cobros. Corregido un enlace de WPF que dejaba vacía la lista de la charola habitual cuando la pantalla nacía sin datos; prueba de regresión con el orden real de arranque. Ver [CONSIGNMENT.md](CONSIGNMENT.md).

Copia de revisión `C:\Users\jalex\CasaMiga-Revision\demo-charolas-QAv1-7c33343` (`--demo-days 28`): 1061 ventas de mostrador y, en Charolas, 3 tiendas, 84 charolas (1092 piezas), 81 recogidas con 904 vendidas y 149 de regreso para Piedra, 57 cobros y $1,231 por cobrar; verificada contra la base en sólo lectura y en pantalla. Build `src/CasaMiga.Presentation/bin/Review-QAv1-2f21688` (entregar y recoger con tarjetas como el Punto de venta; se ajusta a pantalla completa), launcher `work/Abrir-Casa-Miga-QAv1-2f21688.cmd`, con `dev-autologin.json` (entra como admin). Suite **828/828** (657 + 171), Release 0/0. Credenciales en la conversación.

# Gráficas en Reportes y copia con 4 semanas — 06/10/2026

**Implementado; smoke manual pendiente.** El usuario reportó que «Generar» no hacía nada y que no había gráficas. Generar sí recalculaba, pero con el mismo periodo las cifras no cambiaban y no había ninguna señal; ahora muestra «Actualizado a las hh:mm:ss» y «Generando…». Gráficas de barras en Resumen (ventas por día o semana, 10 productos más vendidos, formas de pago), Comparativo (día por día contra el periodo anterior), Control de caja (motivos) y Producción (barra junto a lo sugerido). Tablas ajustadas para no desplazarse a lo ancho a 1280 px; Cortes junta apertura y cierre en «Turno». Ver [REPORTS.md](REPORTS.md).

Copia nueva `C:\Users\jalex\CasaMiga-Revision\demo4sem-QAv1-0b9b195` con `--demo-days 28`: 1061 ventas del 08/09 al 05/10 ($71,583), 683 efectivo / 270 tarjeta / 108 transferencia, 1 cancelación y 18 devoluciones, 28 cortes (faltante $20 el 04/10, sobrante $5 el 02/10), 26 fotos `PROVISIONAL`; caja de hoy cerrada. Verificada contra la base en sólo lectura y en pantalla (el mapa de calor de las 4 semanas suma 1060 = 1061 menos la cancelada). Sustituye a `demo-QAv1-447f004`, que se conserva. Build `src/CasaMiga.Presentation/bin/Review-QAv1-29c724f` (título `… DESARROLLO · QAv1-29c724f`), launcher `work/Abrir-Casa-Miga-QAv1-29c724f.cmd`, con `dev-autologin.json` (entra como admin). Credenciales en la conversación.

# Copia de revisión con fotos y ventas de prueba — 06/10/2026

`C:\Users\jalex\CasaMiga-Revision\demo-QAv1-447f004` (fuera de AppData), preparada con `seed-review … --photos assets\catalog-provisional-ia --demo-days 7`: 26 productos con foto `PROVISIONAL`, 267 ventas del 29/09 al 05/10 (efectivo, tarjeta y transferencia), 1 cancelación y 4 devoluciones, 7 cortes (uno con faltante de $20 y otro con sobrante de $5), cajeros `ana` y `luis` además de `admin` y `cajero1`. Para comprobar las fotos en Punto de venta se abrió la caja de hoy como admin con fondo de $500. Build `src/CasaMiga.Presentation/bin/Review-QAv1-3eebacf`, launcher `work/Abrir-Casa-Miga-QAv1-3eebacf.cmd`, con `dev-autologin.json` (entra como admin). Credenciales entregadas en la conversación; no se versionan. Smoke manual pendiente.

# Pantalla de acceso rediseñada — 06/10/2026

**Implementado; smoke manual pendiente.** Opción B: panel café a la izquierda con el logo oficial sobre crema, hora grande, fecha, «Cada día, un buen comienzo.» y fichas de estado (caja abierta/cerrada de esta terminal, nube y nombre de terminal; sin importes ni quién abrió). A la derecha, saludo según la hora («Buenos días/tardes/noches») y el formulario; mismo diseño para primer administrador y cambio de contraseña. Reloj cada 15 s sólo con la sesión cerrada. Probado a 1280×820 y al mínimo 960×640. Suite **813/813** (646 + 167), Release 0/0.

# Reportes analíticos — 06/10/2026

**Implementado; smoke manual pendiente.** Reportes ahora tiene pestañas: Resumen (lo anterior), **Comparativo** contra el periodo anterior de igual duración, **Ventas por hora** (mapa de calor día × hora con hora pico), **Control de caja** (faltantes/sobrantes por quien abrió la caja, devoluciones por quien cobró, motivos) y **Producción** (piezas sugeridas: promedio del mismo día de la semana en 4 semanas + 10 %). Cada pestaña exporta su CSV. Sin migración. Reglas en [REPORTS.md](REPORTS.md). Suite **811/811** (644 + 167), Release 0/0.

# Fotos provisionales y ventas de prueba — 06/10/2026

`seed-review` acepta `--photos assets\catalog-provisional-ia` (26 ilustraciones generadas con IA, procedencia `PROVISIONAL`, a reemplazar por fotos reales) y `--demo-days N` (ventas deterministas con los servicios reales: turnos diarios, efectivo/tarjeta/transferencia, una cancelación, devoluciones y cortes con faltante/sobrante; cajeros `ana` y `luis`). Ver [docs/catalog/README.md](catalog/README.md).

# Copia de revisión con catálogo autorizado — 06/10/2026

Copia aislada `C:\Users\jalex\CasaMiga-Revision\catalogo-QAv1-ad4d70c` (fuera de AppData: Claude Code desktop virtualiza AppData, ver docs/review-builds.md) preparada con `seed-review` desde `docs/catalog/catalogo-autorizado-v2.csv`: esquema 20, `admin` y `cajero1` (temporal), 4 categorías, 26 productos, sin caja abierta ni fotos, con `dev-autologin.json` (entra sola como admin; verificado: evento `development.autologin`). Build `src/CasaMiga.Presentation/bin/Review-QAv1-ad4d70c`, launcher `work/Abrir-Casa-Miga-QAv1-ad4d70c.cmd`, título `Casa Miga POS · Caja · DESARROLLO · QAv1-ad4d70c`. La copia previa en `%LOCALAPPDATA%\CasaMigaPOS-Review-Catalogo` quedó sólo en el almacenamiento virtualizado del agente: no es válida. Credenciales entregadas en la conversación; no se versionan. Smoke manual pendiente.

# Contraseñas en vivo y campos de la marca — 06/10/2026

**Implementado; smoke manual pendiente.** Alta de administrador y cambio de contraseña muestran los requisitos en vivo (gris antes de escribir, verde cumplido, rojo pendiente) y si la confirmación coincide; bordes del campo en verde/rojo; el botón se habilita sólo con todo cumplido y Enter envía. `PasswordPolicy.Evaluate` es la única fuente de la lista y de `Validate`. Todos los campos de contraseña (`PasswordInput`) tienen botón para mostrar/ocultar; al mostrarla, copiar/cortar quedan deshabilitados y sin historial de deshacer. Plantilla de `TextBox`/`PasswordBox` del tema: borde redondeado y, al seleccionar, anillo café con brillo trigo sin mover el diseño; color de validación `ValidBrush` #2E7D32 (no es color de marca). Suite **797/797** (633 + 164), Release 0/0.

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

# FASE 1K ABIERTA — 1K.4A, 06/10/2026

**FASE 1K.4A técnicamente completada; smoke manual pendiente.** POS y Catálogo incorporan búsqueda en vivo por nombre/SKU con debounce de 250 ms, cancelación de consultas obsoletas, × Limpiar, Escape contextual, categoría preservada y estados vacíos humanos. El filtro administrativo conserva selecciones válidas y responde correctamente a cambios rápidos. El editor de producto permite crear una categoría inline sin perder el borrador y presenta `Origen de la imagen` con REAL/PROVISIONAL intactos; el placeholder visible es `Sin imagen`. Cards mantienen foto, dos líneas de nombre y precio reservado.

La base protegida permanece SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. En ella Pan salado no existe; Bolillo mantequilla `CM-003` sí existe a $8 MXN y no fue mutado. La copia aislada `work/phase1k4a-smoke-20261006` agrega Pan salado, vincula allí el bolillo y añade casos sintéticos largo/sin imagen/inactivo. Conserva schema 16, 25 fotos PROVISIONAL, 3 REAL y conteos operativos antes/después. No hubo ventas, caja, inventario, impresión o producción.

Pruebas focales **14/14**, incluida regresión exacta de Producción 1K.3D. Suite completa `scripts/verify.ps1`: **725/725** (560 generales + 165 WPF/Presentation), 0 fallidas/omitidas. Release Phase1K4A: 0 errores/advertencias. Domain/Application/Infrastructure y migraciones intactos; sin 017. Revisión: `src/CasaMiga.Presentation/bin/Phase1K4A/`, `work/Abrir-Casa-Miga-1K4A.cmd`, DataDirectory `work/phase1k4a-smoke-20261006`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4A`. Ver `PHASE_1K4A_REPORT.md`. No iniciar 1K.4B.

## Registro anterior — 1K.3D-R1

**FASE 1K.3D-R1 técnicamente completada; smoke final corto pendiente.** El pulido de Producción corrige pluralización de piezas, presenta MASS/VOLUME en forma compuesta exacta cuando mejora la lectura, amplía la columna Planeado / real, reemplaza el checkbox «Sin producto» por un resultado textual y diferencia claramente el lenguaje de borrador frente al historial confirmado. El plan conserva su inmutabilidad y ahora se explica como «ya quedó fijado». La comparación del resultado real cuantifica piezas de diferencia.

La ayuda de vista previa conserva que calcular no reserva ni descuenta y ahora explica Necesario, Disponible, Faltante y Estado con un ejemplo 500/350/150. La ayuda de Resultado real mantiene su semántica aprobada: el consumo depende de lo planeado y la entrada terminada de lo realmente obtenido, incluido cero. No cambió el motor, la persistencia ni las reglas de Producción.

Pruebas focales: **7/7** generales y **4/4** WPF/ContextHelp. Suite completa `scripts/verify.ps1`: **719/719** (560 generales + 159 WPF), 0 fallidas/omitidas. Release: 0 errores/advertencias. Schema 16, sin 017; Domain/Application/Infrastructure y migraciones intactos. Base protegida SHA-256 inicial/final `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. Revisión: `src/CasaMiga.Presentation/bin/Phase1K3D-R1/`, `work/Abrir-Casa-Miga-1K3D-R1.cmd`, DataDirectory `work/phase1k3d-r1-smoke-20261006`, título `Casa Miga POS · Caja · DESARROLLO · 1K.3D-R1`. Ver `PHASE_1K3D_R1_REPORT.md`.

El baseline real fue `bd6771ec05b35fde505020f872c6e878f09ddfe9`, no el `cfcd80c5...` previsto en la autorización: entre ambos el usuario pidió conectar y subir QA a GitHub, lo que produjo el merge documentado de las historias. R1 no realiza push ni modifica ramas/remotos.

## Registro anterior — 1K.3D

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
