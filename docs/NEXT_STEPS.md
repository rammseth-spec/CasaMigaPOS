# Próximo paso — smoke manual de 1K.3C-R2

Usar únicamente `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R2`, abierta desde `work/Abrir-Casa-Miga-1K3C-R2.cmd`, con DataDirectory `work/phase1k3c-r2-smoke-20261005`. No usar el acceso anclado ni builds anteriores.

1. Login → Recetas → `SMOKE-REC-001` → Versión 2 → Editar borrador.
2. Abrir Rendimiento: confirmar que flota, no mueve campos y es compacto; pulsar Esc y confirmar que cierra sin salir del editor.
3. Abrir Alternativas: confirmar que la tabla no se desplaza.
4. Abrir Ingredientes directos y Política de cantidad: confirmar que cada `?` está junto a su concepto; cancelar.
5. Perfiles de producto → Versión 2: revisar Preparaciones por pieza y después Ingredientes directos/Política; confirmar popover, Esc y colocación; cancelar.

Si todo pasa, 1K.3C puede cerrarse formalmente. No guardar cambios, crear datos reales, operar ventas/inventario/caja/producción ni imprimir. No iniciar 1K.3D, Pedidos, migración 017 o instalador.

## Registro anterior — smoke manual de 1K.3C-R1

Usar únicamente la instancia `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R1`, abierta desde `work/Abrir-Casa-Miga-1K3C-R1.cmd`, con DataDirectory aislado `work/phase1k3c-r1-smoke-20261005`. No usar el acceso anclado ni el build 1K.3C anterior.

1. Login → Recetas → `SMOKE-REC-001` → Versión 2 → Editar borrador; confirmar COUNT/VOLUME/MASS visibles.
2. Buscar `SIN-RESULTADOS-999`; confirmar el mensaje y, sin limpiar, que las tres filas, cantidades, unidades y políticas siguen visibles.
3. Buscar `SMOKE-MASA-001`; confirmar que MASS es candidato y COUNT/VOLUME siguen visibles. Pulsar × Limpiar y confirmar todo intacto.
4. Confirmar el encabezado «Política de cantidad».
5. Abrir las ayudas de Rendimiento, Política de cantidad, Ingredientes directos y Grupos de alternativas; revisar ancho compacto, instrucciones y ejemplo sintético.
6. En Perfiles de producto, abrir la ayuda de Preparaciones y revisar el mismo criterio.
7. Cancelar, reabrir Versión 2 y confirmar contenido original intacto. Continuar después con el smoke pendiente de Perfiles de producto.

No guardar cambios, crear datos reales, operar ventas/inventario/caja/producción ni imprimir. No iniciar 1K.3D, Pedidos, carga real, migración 017 o instalador. FASE 1K.3C-R1 queda técnicamente completada, no aprobada ni cerrada, hasta recibir el smoke del usuario.

## Registro anterior — smoke manual de 1K.3C

1K.3B fue aprobada manualmente por el usuario. **1K.3C está técnicamente completada; su smoke manual sigue pendiente.** Usar únicamente la instancia `Casa Miga POS · Caja · DESARROLLO · 1K.3C`, abierta desde `work/Abrir-Casa-Miga-1K3C.cmd`, con DataDirectory `work/phase1k3c-smoke-20261005`. No depender del acceso anclado.

Revisión segura, sin guardar:

1. Recetas: comprobar lista vacía, Ver detalle deshabilitado y Nueva receta.
2. Crear el formulario de nueva receta sólo para revisar campos y botón deshabilitado; no confirmar la creación. Cancelar.
3. En una versión ya disponible en un fixture futuro, buscar `Harina` o `MP-HAR-001` sin Enter; comprobar × Limpiar y el mensaje sin coincidencias. En esta copia no existen recetas: no crear una para forzar el recorrido.
4. Comprobar Masa/Volumen/Piezas y que sus unidades sean g/kg, ml/L o pieza sin cruces.
5. Revisar ayudas de Rendimiento, Política, Alternativas, Preparaciones y publicación.
6. Revisar que Nuevo grupo de alternativas y Agregar opción al grupo sean acciones distintas, y que Quitar/Guardar/Publicar/Activar dependan de selección/validez.
7. Comprobar scroll a 1280×820, rueda sobre tablas/selectores, Esc con ComboBox abierto y F11/Esc normal.

No guardar recetas, versiones, perfiles, asociaciones ni operaciones. No cargar Masa dulce, Donas, Polvorón u otros datos reales. No 1K.3D, producción real, ventas, impresión, migración 017 o instalador. Tras el smoke, esperar confirmación explícita del usuario.

## Registro anterior — smoke manual de 1K.3B

1K.3A, R1 y 1K.3B fueron aprobadas por el usuario antes de 1K.3C. Focales 142/142, suite completa 702/702, Release 0 errores/advertencias. Ver `PHASE_1K3B_REPORT.md` para resultados y limitaciones.

Usar únicamente la nueva instancia identificada `Casa Miga POS · Caja · DESARROLLO · 1K.3B`, abierta automáticamente desde `work/Abrir-Casa-Miga-1K3B.cmd` al terminar la entrega. Carpeta aislada: `work/phase1k3b-smoke-20261004`. No depender del acceso anclado; las credenciales se entregan en conversación.

Revisión segura, sin guardar ni confirmar:

1. Escribir Harina o MP-HAR-001 y comprobar búsqueda automática; × Limpiar restaura lista/foco. Probar texto sin coincidencias.
2. Abrir Harina: 50 kg, Bulto 25 kg activo y equivalencia 1 Bulto 25 kg = 25 kg. No repetir Saldo inicial.
3. Entrada: 1 Bulto 25 kg → +25 kg → 75 kg. Abrir/cerrar ayuda y cancelar.
4. Merma: 2 kg → −2 kg → 48 kg. Cancelar.
5. Conteo físico: 48 kg → diferencia −2 kg → 48 kg. Cancelar. Una cantidad vacía/incorrecta no permite confirmar.
6. Revisar etiquetas, fechas locales sin zona, historial con signos y columnas humanas. Asociaciones muestran Producto/SKU, nunca ProductId; si Harina no tiene asociaciones, conservarla así.
7. Comprobar botones, desplazamiento, ComboBox cerrado/rueda, Esc con desplegable abierto y F11, sin registrar operaciones.

Después del smoke, esperar autorización. No 1K.3C/1K.3D, Pedidos, recetas/perfiles reales, producción, ventas, impresión, activación de consumo ni migración 017. FASE 1K sigue abierta.

## Registro anterior — smoke manual de 1K.3A-R1

**Corrección técnica completada; smoke manual R1 pendiente.** Usar la instancia que Codex deja abierta al terminar, identificada `Casa Miga POS · Caja · DESARROLLO · 1K.3A-R1`. No depender del acceso anclado. Launcher específico: `work/Abrir-Casa-Miga-1K3A-R1.cmd`; DataDirectory aislado: `work/phase1k3a-r1-smoke-20261003`. La base R2 original debe permanecer byte-idéntica.

Repetir únicamente las partes fallidas, sin guardar operaciones:

1. Inventario → Harina → Editar → Tipo abierto → Esc cierra sólo el desplegable, sin abandonar editor ni salir de fullscreen. Después comprobar Esc normal y F11. Volver sin guardar.
2. Recetas → texto «Puedes crear y editar recetas y perfiles», estado vacío y Ver detalle deshabilitado sin selección.
3. Nueva receta → Dimensión muestra Masa/Volumen/Piezas. Cancelar.
4. Producción → Nueva producción → estado vacío sin perfiles; Agregar/Quitar/Calcular/Crear deshabilitados, incluso Quitar con la única fila seleccionada. Cancelar.

Harina sigue con **50 kg** y presentación HAR-BULTO-25KG de 25 kg activa. No repetir Saldo inicial, cargar recetas/perfiles, crear producción, vender ni imprimir. Suite 679/679, focales 18/18, Release 0/0. Leer `PHASE_1K3A_R1_REPORT.md`, `evidence/phase1k3a-r1-verification.md` y la nueva regla permanente `review-builds.md`. La comparación de binarios no identifica con certeza qué versión abrió el usuario anteriormente.

Detenerse tras entrega R1. No iniciar 1K.3B ni declarar aprobación humana hasta confirmación explícita. Para una siguiente subfase se requiere nueva autorización.

## Registro anterior — smoke de 1K.3A

1K.3A implementada y verificada: 672/672, Release 0/0, schema 16 intacto. Leer `PHASE_1K3A_REPORT.md`, que conserva completo el backlog observado y el checklist. Abrir `work/Abrir-Casa-Miga-1K3A.cmd` para revisar la build equivalente a R2. Comprobar rueda sobre página/tablas vacías/tablas con filas, ComboBox cerrado y abierto, teclado/barras/touchpad, etiquetas españolas, ausencia de OperationId, estados vacíos y botones deshabilitados. No guardar ni confirmar operaciones durante esta revisión.

**Harina ya tiene 50 kg. No repetir Saldo inicial.** HAR-BULTO-25KG sigue activa y equivale a 25 kg. El saldo real fue capturado por el usuario antes de 1K.3A; el trabajo de esta subfase no modificó la base original.

Después del smoke y de una nueva autorización: FASE 1K.3B, usabilidad específica de Inventario (búsqueda/limpieza, cantidades/equivalencias, vista previa y ayudas), conservando idempotencia y datos existentes. Modelo recomendado GPT-6 Astra, Ultra. 1K.3C y 1K.3D permanecen posteriores; no cargar ingredientes/recetas/perfiles reales, crear producciones o activar consumo automático. FASE 1K continúa abierta. Detenerse después del reporte y commit único autorizado de 1K.3A; no push.

## Registro anterior — checkpoint de Inventario

El usuario autorizó el 01/10/2026 un único commit del bugfix ya verificado y validado manualmente. El checkpoint incluye código, regresión, reproducción, evidencia y documentación asociada; sin cambios funcionales posteriores, migraciones ni push. Revisión manual siguiente: Inventario → `MP-HAR-001` → Ver detalle → «Saldo inicial», en la copia R2 corregida. La revisión y eventual captura corresponden al usuario; este checkpoint no ejecuta ni inicializa inventario. Detenerse después del reporte. FASE 1K sigue abierta y las mejoras UX continúan pendientes.

## Registro previo al checkpoint Git

El 01/10/2026 el usuario confirmó «todo correcto» tras revisar la corrección y solicitó el reporte. Se registra esa aceptación para este defecto de Inventario; FASE 1K, el smoke global R2 y el checkpoint físico Qian no se cierran con ella. Entregar reporte y detenerse. No realizar commit/push ni iniciar mejoras pendientes automáticamente.

Referencia operativa: leer `INVENTORY_CRASH_FIX_20261001.md` y su evidencia. La compilación corregida es `src/CasaMiga.Presentation/bin/R2/CasaMiga.Presentation.exe`, actualizada y verificada (653/653). El detalle se consulta en Inventario → buscar `MP-HAR-001` → Ver detalle: `HAR-BULTO-25KG`, 25 kg, Activa marcada y sin saldo inicial. No recrear Harina/presentación ni inicializar stock para comprobar la corrección. Ante «Operación confirmada» con fallo de consulta, buscar nuevamente sin repetir guardado. Ante resultado incierto, no repetir: reiniciar y revisar artículo/historial antes de continuar.

Pendientes UX sólo documentados: búsqueda en vivo, X, ayuda ?, renombrar Saldo MAIN, etiquetas, estados vacíos y rediseño. No empezar esas mejoras, Recetas/Producción, fase siguiente o instalador. FASE 1K continúa abierta. Modelo recomendado Astra para revisión del cierre; detenerse tras reporte.

## Próximo paso anterior — smoke humano de 1K.2C-R2

Revisar R2 en `work/phase1k2c-r2-smoke-20260928` con la compilación Release de `src/CasaMiga.Presentation/bin/R2`. Credenciales en conversación. Validar catálogo completo, cards, Administrar foto → agregar/cambiar/quitar, preview y Cancelar; Cobrar sin ticket → Historial. Luego, con Qian disponible, una venta sintética mediante Cobrar + imprimir y una reimpresión desde Historial: logo Variante 04, centrado, nitidez, mismo folio, marca REIMPRESIÓN y corte. No se realizaron impresiones físicas automáticas. No considerar R2 aprobado por las capturas automatizadas.

La aplicación anterior ya está cerrada. R2 está preparada en salida separada y con su propio dataset para revisión. Leer `PHASE_1K2C_R2_REPORT.md`, `printing-variant04.md` y la evidencia de verificación.

Pedidos: sólo requisitos, en `orders-future-requirements.md`. Diseñar Order persistente anterior al cobro, pedidos diarios/plantillas, vínculo a Sale y políticas pendientes. Preguntar si alguna tienda recibe pan y paga después. No implementar crédito, reservas, Pedidos, migración 017 o instalador. FASE 1K continúa abierta; detenerse después del reporte.

## Registro anterior — R1

Revisar el smoke aislado preparado con 26 fotos PROVISIONAL, cards de 184 x 276 DIP con nombre/precio completos, rail POS contraído sólo con iconos centrados, carrito, ambos botones de cobro, administración de imagen y crop visual. Confirmar especialmente que al entrar al POS el catálogo comienza arriba y que expandir/contraer navegación no deja textos ni scrollbar recortados. Leer `PHASE_1K2C_R1_REPORT.md`, `product-photo-ui.md` y `phase1k2c-provisional-map.md`. No declarar aprobación visual hasta recibir confirmación explícita del usuario. FASE 1K permanece abierta; no cargar la instalación comercial, iniciar instalador ni cambiar 1K.2B.

Las 26 imágenes actuales son PROVISIONAL y requieren reemplazo futuro por fotografías reales proporcionadas por Casa Miga. El reemplazo debe usar el flujo administrativo REAL y conservar ProductId, precio, historial, receta e inventario.

Requerimiento futuro formal, no implementado: agregar `Tipo de venta` persistente en Sale con valores **Mostrador**, **Pedido** y **Pedido a tienda**. Pedido/Pedido a tienda debe admitir cliente, tienda o persona de referencia y nota opcional; debe aparecer en Historial, backup/restore, ticket y reimpresión. No debe ser texto temporal de Presentation. Requiere subfase separada con revisión de persistencia y modelo Astra.

## Registro anterior — revisión 1K.2B

Primero revisar la corrección de acceso del 28/09: en instalación nueva crear administrador con contraseña propia de 12–128 caracteres, confirmarla e iniciar sesión. No usar credenciales del backup de smoke en otra instalación. El formulario corregido, con login centrado y márgenes equilibrados, y la compilación Release están preparados para revisión. Esto no inicia 1K.2C.

1K.2B terminó con 621/621 pruebas y Release limpio. Leer `PHASE_1K2B_REPORT.md`, `product-photos.md` y la adenda normativa en `PHASE_1K2A_DESIGN.md`.

No continuar automáticamente. 1K.2C podrá incorporar UI administrativa de importación/crop, ProductCard FOTO + NOMBRE + PRECIO, fallback, caché acotada y carga estructurada explícita del paquete provisional. Mantener ProductId al sustituir PROVISIONAL por REAL. CSV/JSON autorizados serán la autoridad para productos; no inferir datos desde fotos.

Pendiente: aprobación de alcance, fotografías reales, equipo Windows 10/LTSC exacto y validación visual/rendimiento en POS. No se autoriza ahora carga de 26 productos, instalador ni cambios de reglas comerciales. Modelo recomendado: Sol para integración visual sobre estos contratos; Astra si cambia persistencia/backup/recovery.

## Registro histórico — siguiente paso al terminar 1K.2A

Leer `PHASE_1K2A_DESIGN.md`. Autorizar por separado ProductPhoto, DDL 016 propuesto, originales retenidos, JPEG/PNG de entrada, derivado JPEG Q85/512, caché 64 MiB, backup v2 compatible con v1 y recuperación explícita de media perdida. No crear migración ni implementar automáticamente. El diseño y probe quedan sin commit por la restricción expresa de arquitectura no autorizada.

Para validar calidad antes de fijar importación: 3–5 fotografías reales con Product identificado; la falta del resto no bloquea un catálogo mixto con fallback. Confirmar edición/build de Windows 10/LTSC y resolución/DPI para validación en el equipo objetivo. Modelo recomendado para implementación: Astra por atomicidad, persistencia, backup y recuperación.

## Propuesta previa — sustituida por el diseño 1K.2A

1K.1 está aprobada y cerrada. No iniciar 1K.2 automáticamente: preparar un alcance separado y obtener autorización antes de cambiar persistencia, migraciones o catálogo visual.

Requisito formal reservado para 1K.2: el POS usará fotografías reales proporcionadas por Casa Miga, asociadas a Product, con fotografía original, derivado optimizado, tarjeta FOTO + NOMBRE + PRECIO, fallback sin foto, caché/rendimiento, backup/restore y cambio de fotografía. No usar IA, stock, Internet ni productos ajenos. 1K.1 no agrega persistencia ni migración para imágenes.

Modelo recomendado para 1K.2: **Sol** para diseño WPF/POS; detenerse y reconsiderar si el alcance exige persistencia, migración o cambios de negocio.

## Registro anterior

# Próximos trabajos posibles — FASE 1J CLOSED

No iniciar automáticamente ninguna fase. Requieren alcance y autorización separados:

1. Carga autorizada de recetas reales Casa Miga.
2. Completar datos pendientes: cocoa concha, rendimiento cobertura, relleno rol, Procali por dona, azúcar dona, toppings dona, toppings polvorón, cuernito relleno, bolillo y puerquito. No inferir ni seedear.
3. Rediseño visual profesional.
4. Fullscreen/F11.
5. Hardening general V1.
6. Instalador Windows.
7. Validación Windows 10/11.
8. Pruebas físicas finales.

Recomendación: revisar y autorizar el siguiente alcance con Astra. El cierre de recetas/producción no certifica preparación comercial global.

## Registro histórico (sustituido por el estado superior)

# Próximo paso — proponer y autorizar 1J.5 por separado

1J.4 está completada y aprobada. Revisar `PHASE_1J4_REPORT.md`, `PHASE_1J4_SMOKE.md` y `recipe-production-ui.md`. No continuar automáticamente.

La propuesta de 1J.5 debe centrarse en hardening operativo: evaluar la restricción creador/terminal del borrador, reintentos y recuperación ante resultado incierto, smoke de faltantes/cambios de stock y zero-output total, backup/restauración del flujo UI y criterios para introducir recetas reales mediante captura autorizada. Mantener schema 15 salvo autorización expresa de una necesidad estructural; no iniciar rediseño, fullscreen, compras/proveedores, impresión de producción ni datos reales.

Modelo recomendado para proponer y ejecutar 1J.5: **Astra** si el alcance incluye recuperación/concurrencia crítica; **Sol** sólo para ajustes UI aislados ya cubiertos por contratos. FASE 1J permanece abierta; desarrollo, NO listo para producción.

## Registro anterior (histórico)

# Próximo paso — revisión de 1J.3 y autorización separada de 1J.4

Revisar PHASE_1J3_REPORT.md, ADR-023 y evidencia phase1j3-*. No iniciar automáticamente UI. Preparar propuesta de interacción para fórmulas/perfiles, alternativas, preview sin reserva, actuals cero/superiores al plan, confirmación y recuperación conservando OperationId/ExpectedRevision.

Límites actuales: MAIN; saldos inicializados; plan DRAFT sin edición funcional; confirmación por creador/terminal; sin transferencia entre usuarios. No datos productivos reales, impresión, rediseño ni fullscreen. FASE 1J ABIERTA, desarrollo NO listo para producción. Astra recomendado para revisar contrato crítico; Sol para UI tras autorización.

## Registro anterior (histórico)

# Próximo paso — revisar corrección 1J.3B

Revisar PHASE_1J3B_REPORT.md y evidencia phase1j3b-*. Permisos aprovisionados mediante migración 015, sin bypass ni inicialización paralela. Autorizar separadamente la reanudación de 1J.3 funcional; los servicios futuros deberán revalidar HasPermission con la matriz aprobada.

1J.3 sigue PENDIENTE; 1J.4 no iniciada. Modelo recomendado para reanudar: Astra, conforme a la autorización. FASE 1J ABIERTA; desarrollo, no listo para producción.

## Registro anterior (histórico)

# Próximo paso — revisar corrección 1J.3A

Revisar PHASE_1J3A_REPORT.md y evidencia phase1j3a-*. El bloqueo de esquema por salida totalmente cero queda corregido en 014. 1J.3 funcional permanece pendiente de autorización de reanudación: no hay PrepareProduction, ConfirmProduction ni nuevos permisos/UI. No iniciar 1J.4 automáticamente.

Modelo recomendado para reanudar 1J.3: Astra. Mantener writer de InventoryBalance único, atomicidad, consumo obligatorio y salida sólo cuando haya cantidades reales positivas. FASE 1J ABIERTA; desarrollo, no listo para producción.

## Registro anterior (histórico)

# Próximo paso — revisar 1J.2 y autorizar 1J.3 separadamente

Revisar PHASE_1J2_REPORT.md, ADR-022 y evidencia phase1j2-*. La persistencia está preparada; no hay comando de confirmar producción. No continuar automáticamente.

Antes del primer movimiento deben aprobarse: política MASS/VOLUME no representable; actual=0 y actual>planned (incluido lote totalmente cero frente a salida positiva obligatoria); contrato canónico/IDs deterministas y recuperación; permisos y auditoría Application; revalidación de configuración, asociaciones comerciales y stock en la misma transacción del writer existente. No inferir recetas reales ni activar datos productivos. UI, impresión, rediseño y fullscreen no pertenecen a este checkpoint.

Modelo recomendado para 1J.3: Astra. FASE 1J ABIERTA; desarrollo, no listo para producción. 1J.3 no iniciada.

## Registro anterior (histórico)

# Próximo paso — revisar 1J.1 y autorizar 1J.2 separadamente

Revisar PHASE_1J1_REPORT.md y ADR-021. Diseñar persistencia para Formula/Version, perfiles, alternativas y contrato multi-output; no volver al modelo Recipe→un producto. Definir constraints de una versión activa, inmutabilidad, snapshots y recuperación antes de escribir migración. No iniciar 1J.2 sin autorización expresa; 013 sigue ausente.

Pendientes comerciales: resolución de escalas MASS/VOLUME no representables, actual=0 y actual>planned, datos faltantes de cobertura/dona/polvorón/toppings. No inferir reglas ni semillas. Permisos y UI sólo propuestos. Modelo recomendado: Astra. FASE 1J ABIERTA; versión de desarrollo, no lista para producción.

## Registro anterior
# FASE 1I — CERRADA

1I.5 completada. Usuario aprobó explícitamente el smoke con «SI» después de las capturas finales: Cashier vendió con consumo ACTIVO, stock 8→6, carrito vacío/$0 e Inventario consistente. Primera venta adicional fue Administrator y se conserva como evidencia, sin borrar ni repetir su intención. Tres ventas manuales: $38 + $19 + $38 = $95 CASH; apertura $0, ExpectedCash calculado $95. Caja sintética permanece OPEN, sin conteo/cierre manual. ENABLED v4, dos consumos -2 y saldo 6; checkout.pending ausente. Consulta final read-only: integrity_check ok, FK 0.

402/402 pruebas aprobadas, 0 fallidas/omitidas; Release 0 errores/advertencias. Backup y restore automatizados verificados separadamente del smoke manual. Schema 12; hashes 001–012 intactos; 013 ausente. Cierre exclusivamente documental tras producto b2eeb2e84952d9a924223285e6e9f24afc77d6a2; no repetir suite sin cambios de producto. El commit de este cierre constituye el baseline oficial recuperable.

Versión de desarrollo, NO lista para producción. Ninguna fase posterior iniciada. No recetas, producción, compras/proveedores, rediseño, fullscreen, instalador, LAN ni nuevos efectos físicos. Próximo paso: proponer alcance de una fase posterior y solicitar autorización; no implementarla. Modelo recomendado para planificar: Astra.

## Registro anterior (histórico, sustituido por este cierre)
# Próximo paso — smoke humano 1I.5

Seguir PHASE_1I5_SMOKE.md por bloques cortos, dando siempre usuario y contraseña sintética al solicitar login. Empezar con SMOKEADMIN: saldo 10, verificar configuración, activar. Esperar confirmación antes de vender. No declarar cierre hasta completar aceptación humana y evidencia. Después emitir reporte de los 65 puntos autorizados y detenerse; no iniciar fase posterior.

## Registro anterior

# Próximo paso — revisar 1I.4 y autorizar 1I.5 por separado

1I.4 completada con smoke administrativo humano confirmado. Revisar PHASE_1I4_REPORT.md y ADR-020. No continuar automáticamente: 1I.5 requiere autorización nueva para su smoke integral de venta, recuperación, backup y cierre. Modelo recomendado: Astra. FASE 1I sigue ABIERTA; versión de desarrollo, no lista para producción. Migraciones 001–012 inmutables; no 013.

## Registro anterior

# Próximo paso — revisar 1I.3 y autorizar 1I.4

1I.3 completada. Revisar PHASE_1I3_REPORT.md, ADR-019 y evidencia phase1i3-* antes de autorizar 1I.4 (preflight, activación/pausa/reanudación y auditoría de configuración). No iniciar automáticamente. Modelo recomendado: Astra por validación de precondiciones y límites transaccionales.

Mantener flag DISABLED en instalaciones, migraciones 001–012 inmutables, efectos físicos post-commit y sales.complete como permiso comercial. No habilitar consumo mediante edición manual de la base. No 013, recetas, producción, proveedores, devoluciones, overrides, rediseño, fullscreen, LAN, instalador ni cajón. Sistema de desarrollo, no listo para producción.

## Registro anterior — bloqueo ya resuelto por autorización de 012

# Próximo paso — resolver frontera histórica de recovery

1I.3 está BLOQUEADA. Revisar PHASE_1I3_REPORT.md y autorizar un diseño separado de marca persistente e inmutable de obligatoriedad de evaluación. Si requiere migración 012, autorizarla expresamente. No implementar heurísticas temporales, no asumir que toda ausencia de evaluación es histórica y no continuar 1I.3 hasta resolver esta condición. Mantener 001–011 intactas. 1I.4 no autorizada ni iniciada.

## Registro anterior

# Próximo paso — revisar y autorizar FASE 1I.3

1I.2 completada. No iniciar automáticamente 1I.3. Revisar PHASE_1I2_REPORT.md y ADR-018; proponer/confirmar integración funcional del flag y plan dentro de la transacción de Checkout, autorización sales.complete, recuperación/idempotencia y bloqueo sin efectos parciales. La infraestructura técnica existe, pero Checkout no la llama y el flag está DISABLED.

No tocar migraciones 001–011 una vez aplicadas; cualquier cambio estructural posterior requiere nueva migración. Preservar Sale/Payment/ExpectedCash/folios/tickets y efectos físicos post-commit. Preflight/activación/pausa/reanudación y auditoría de configuración corresponden a 1I.4, no están implementados. Sin recetas, producción, proveedores, devoluciones, overrides, rediseño, fullscreen, LAN, instalador ni cajón.

Modelo recomendado 1I.3: Astra por la frontera transaccional y recuperación. Esperar autorización expresa antes de implementarla.

## Registro anterior
# Próximo paso — proponer y autorizar FASE 1I.2

1I.1 está completada. No iniciar automáticamente 1I.2. La siguiente propuesta debe fijar la migración 011, tablas de evaluación/snapshot, estado disabled/enabled/paused, permiso de activación, algoritmo versionado de InventoryOperationId, persistencia sobre la misma transacción de Checkout, triggers, upgrade/rollback y pruebas.

Mantener sin cambios hasta autorización: CheckoutService, ILocalUnitOfWork, SqliteInventoryStore, migraciones 001–010, ventas, pagos, ExpectedCash, tickets e impresión. No habilitar consumo, no crear flag, no consumir ventas históricas y no implementar recetas, producción, devoluciones u override.

Modelo recomendado para 1I.2: Astra por la reconstrucción estructural y la frontera transaccional; detenerse si el diseño no puede garantizar commit único y recuperación idempotente.

## Registro anterior

# Próximo paso — planificar la fase posterior a 1H

FASE 1H está cerrada. No iniciar automáticamente otra fase. La próxima conversación debe proponer alcance, invariantes, riesgos, pruebas y exclusiones, y esperar autorización expresa antes de implementar.

Propuesta recomendada: definir primero la relación entre inventario y venta (`SALE_CONSUMPTION`) como un plan separado, incluyendo política de existencias insuficientes, reversos, recetas/versiones, productos elaborados, atomicidad con Checkout y migración aditiva. El cierre 1H no autoriza ninguna de esas decisiones.

Mantener fuera hasta nueva autorización: consumo automático por ventas, recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, cajón, LAN, rediseño profesional, fullscreen e instalador. Modelo recomendado para planificar: Sol; reservar Astra para un defecto técnico complejo demostrado.

## Registro anterior

# Próximo paso — proponer FASE 1H.5

1H.4 está completada y aprobada. No iniciar automáticamente 1H.5. La siguiente conversación debe proponer su alcance, invariantes, pruebas y exclusiones para autorización expresa.

Mantener fuera hasta autorización: consumo de inventario por ventas, `SALE_CONSUMPTION`, recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, impresión de inventario, cajón, LAN, rediseño profesional, fullscreen e instalador. Modelo recomendado para 1H.5: Sol por continuidad y costo; reservar Astra para un defecto complejo de concurrencia, recuperación o arquitectura.

## Registro anterior

# Próximo paso — proponer y autorizar FASE 1H.4

1H.3 está completada. FASE 1H permanece abierta. No iniciar automáticamente 1H.4.

La siguiente propuesta puede definir una UI WPF mínima para inventario sobre los servicios existentes, sin acceder a SQLite ni enviar deltas arbitrarios. Debe decidir el ciclo de vida del OperationId en formularios/reintentos, preparación y confirmación de movimientos, visualización de unidades atómicas/presentaciones, tratamiento de ConcurrencyConflict y resultado incierto, y aviso de reconciliación inconsistente sin reparación automática.

Mantener fuera consumo por ventas, SALE_CONSUMPTION, recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, impresión, cajón, LAN, rediseño, fullscreen e instalador. Modelo recomendado para proponer 1H.4: SOL; usar Astra sólo ante un defecto técnico complejo.

## Registro histórico anterior

# Próximo paso — retomar 1H.3 después de revisar el cierre 1H.2A

1H.2A está completada. Usar como baseline el commit documental de cierre cuyo padre es `76e654ce9b56132ff64248953931c95190858311`. No iniciar 1H.3 en este bloque.

Antes de exponer RECEIPT en Application, alinear su contrato Domain con el CHECK 010: reason_code NULL y observación opcional válida. Preservar el resto de razones e invariantes. Retomar únicamente el alcance autorizado de 1H.3 tras la revisión del reporte; modelo recomendado: SOL, reservando Astra para defectos complejos.

No activar descuento por venta, recetas, producción, compras, proveedores, UI, hardware ni funcionalidades posteriores.

## Registro histórico anterior
# Próximo paso — proponer y autorizar FASE 1H.3

FASE 1H está abierta y 1H.2 está completada. No iniciar automáticamente 1H.3.

La siguiente propuesta debe definir los casos de uso Application para configurar artículos y registrar OPENING_BALANCE, RECEIPT, ADJUSTMENT_GAIN, ADJUSTMENT_LOSS y WASTE. Debe envolver el puerto persistente ya probado, revalidar permisos, conservar motivos estructurados y decidir el flujo de preparación/confirmación y resultado incierto antes de diseñar UI.

No modificar Checkout ni activar SALE_CONSUMPTION. Mantener fuera recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, UI profesional y hardware.

## Registro histórico anterior

# Próximo paso — autorizar FASE 1H.2

FASE 1H está iniciada y 1H.1 está completada. No iniciar automáticamente 1H.2.

La siguiente subfase debe diseñar y autorizar la migración 009, el ledger inmutable y la proyección reconstruible `InventoryBalance`. Antes de escribir persistencia se debe escoger una sola autoridad transaccional para actualizar el saldo: Application/store o un mecanismo controlado de SQLite. No pueden coexistir dos mecanismos independientes.

Mantener fuera de 1H.2: Checkout, descuento por venta, recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, UI y hardware. Preservar migraciones 001–008.

## Registro histórico anterior

# Próximo paso después de FASE 1G

FASE 1G está cerrada dentro del alcance autorizado. No iniciar automáticamente otra fase.

Siguiente acción recomendada: proponer y autorizar un bloque de preparación para producción. Si primero se adquiere el cajón, elaborar un bloque independiente con modelo, voltaje, cableado/conector y comando Qian documentado antes de habilitar cualquier pulso. LAN puede validarse en otro bloque opcional. Mantener `openCashDrawer=false` hasta entonces.

## Registro histórico anterior

# Continuidad — FASE 1G Bloque E

FASE 1G ABIERTA; Bloque E validado físicamente. Bloque E.1 fue sólo análisis: no hubo impresión ni cambio de producto. El raster empieza en offset 6 y no tiene feed previo; el PNG aporta 1.00 mm de blanco superior y el resto observado es principalmente mecánico. Ver PHASE_1G_BLOCK_E1_TOP_MARGIN_REPORT.md.

Siguiente decisión: aceptar el margen mecánico o autorizar en un bloque separado un derivado técnico que quite únicamente ocho filas superiores totalmente blancas, con mejora máxima aproximada de 1 mm. No probar backfeed ni comandos no documentados para la Qian.

Baseline Bloque D: CP850, ESC t 2, GS V 1. Fuente única: assets/brand/Opcion07. Paquetes anteriores deprecados. Sistema de desarrollo, no listo para producción. No se inició ninguna fase posterior.

## Registro histórico anterior, sustituido por el estado superior

# Próximos pasos — proporcionar logo oficial para FASE 1G Bloque E

Actualización 2026-09-11: **FASE 1G ABIERTA; BLOQUE D VALIDADO FÍSICAMENTE; BLOQUE E DETENIDO ANTES DE IMPLEMENTAR POR FALTA DEL LOGO OFICIAL**. Implementación Bloque D: `283cfa4`.

Estado verificable: migraciones 001–008 sin cambios; 188 pruebas aprobadas, ninguna fallida u omitida; build Release sin errores ni advertencias. Winspool aceptó un único payload PC850 de 889 bytes en POS-80 como JobId 5. `ESC t 2` ocupa los tres primeros bytes; un solo `GS V 01` ocupa los tres últimos; no hay cajón. La fotografía y confirmación del usuario validaron caracteres, jerarquía, alineación, legibilidad, ausencia de basura y corte parcial.

Siguiente acción: proporcionar el archivo del logotipo oficial aprobado. Formato preferido: PNG monocromático con fondo transparente, alta resolución y proporciones originales. Debe ser una fuente autorizada y conservar sin reconstrucción la tipografía, estrella, vapor y geometría originales. El diseño permitirá sustituir posteriormente el asset sin cambiar la lógica.

Cuando el asset esté disponible, reanudar el Bloque E desde el checkpoint `PHASE_1G_BLOCK_E_CHECKPOINT.md`. No imprimir otra prueba, modificar el baseline PC850/corte ni avanzar a cajón, LAN o reimpresión UI mientras falta el logo.

Antes de producción continúan pendientes datos reales, logo monocromático aprobado, administración/recuperación de usuarios, zona horaria comercial configurable, retención/segunda copia/cifrado, pruebas físicas, licencia y distribución. FASE 1G no está cerrada.

Checkpoint vigente 1I.5: reabrir instalación existente, login SMOKECASHIER y comprobar venta con consumo ACTIVO; principal anterior fue Administrator. No cerrar fase ni repetir esa intención.

Revisión adicional completada: logo completo, iconos, hover estable, ausencia de cambios visuales al bloquear la navegación y pestañas de Catálogo/Recetas con selección verde.
