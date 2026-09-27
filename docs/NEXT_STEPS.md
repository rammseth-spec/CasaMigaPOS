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
