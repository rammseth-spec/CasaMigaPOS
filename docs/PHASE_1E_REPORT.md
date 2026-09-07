# CASA MIGA POS — Reporte FASE 1E

Fecha: 2026-09-07. Estado: implementación terminada, aceptación visual final pendiente. No se declara cierre total de 1E ni preparación para producción.

## Identidad Git y recuperación de 1D

Configuración exclusivamente local verificada literalmente:

```text
Rammseth
rammstudioworkshop\@gmail.com
```

Baseline definitivo aprobado de 1D: `92c5ef795b6da8e1cdf7f34ec9fb1b01cce4a5d2`. Sin cambios funcionales. Se ejecutó verificación completa antes de registrarlo: 120 aprobadas. Después se exportó el commit a una carpeta nueva, se compararon SHA-256 de las cinco migraciones y se volvió a ejecutar la suite: 120 aprobadas, cero errores/advertencias de compilación.

Detalle de recuperación: el primer commit raíz acb3604 normalizaba finales de línea mixtos en SQL histórico. Los commits 5b98863 y 92c5ef7 corrigieron exclusivamente su almacenamiento Git, usando migrations/*.sql -text y conservando los bytes originales. Usar el baseline definitivo, no el primer commit raíz. No se modificó ninguna migración aplicada ni dato para hacer pasar la validación.

Recuperación sin sobrescribir trabajo actual: `git archive --format=zip --output=work/phase1d-baseline.zip 92c5ef7`; extraer a otra carpeta. Datos se recuperan desde .cmbackup compatible, no desde Git. No ejecutar 1D contra una base migrada a v6. No se configuró remoto, no hubo push ni se eligió licencia.

Commit de implementación 1E: `18fd3a8e56c79ef7a5710443da09d364f965f6fa`. Incluye el código y las pruebas; su mensaje conserva explícitamente el pendiente de smoke visual. Documentación/evidencia se registran después en un commit separado.

## Implementación por subfase

| Subfase | Resultado |
|---|---|
| 1E.1 | Sale, SaleItem y Payment inmutables; cantidades enteras, Money Int64 checked, totales y cambio exactos. 7 pruebas iniciales de dominio aprobadas. |
| 1E.2 | Migración 006, cuatro tablas STRICT, FK, unicidad, integridad de agregado y triggers de inmutabilidad. Persistencia inicial: 6 pruebas; upgrade v5 añadido después. |
| 1E.3 | Checkout atómico, sales.complete, snapshots aprobados, UUID único, marcador durable, lectura de recuperación y aceptación del resultado. 14 pruebas focalizadas finales aprobadas. |
| 1E.4 | ExpectedCash suma efectivo aplicado por caja; conteo confirmado bloquea venta. 5 pruebas focalizadas de cierre/restauración aprobadas. |
| 1E.5 | Cobrar integrado al POS, captura de recibido/cambio, confirmación explícita, folio y recuperación. Smoke WPF parcial; pendiente recorrido completo visual. |

No se implementó inventario/stock, descuentos, promociones, combos, devoluciones, cancelaciones, tarjetas, fiscalidad, clientes, impresora, cajón, nueva identidad, rediseño ni fullscreen. Se preservan las funcionalidades 1A–1D, con cambios necesarios en puertos, guardas del contexto y cálculo de caja. Sin paquetes nuevos ni cambios de lockfiles.

## Semántica comercial e invariantes

El carrito continúa transitorio: abrir Cobrar o preparar el intento no lo convierte en venta. Se conserva ProductId, nombre y precio del carrito; la confirmación revalida que producto y categoría estén activos. No se reemplaza el snapshot por precios actuales.

Sale referencia usuario, terminal y sesión de caja, con UTC y folio por terminal/secuencia. Payment es único, CASH, aplicado=total, recibido>=total y cambio=recibido−total. Subtotal=total=suma checked de líneas. Cero permitido, carrito vacío rechazado. Las entidades no tienen setters; la BD rechaza modificar/borrar datos comerciales o añadir partidas/pagos después de la confirmación. La fecha de pago es la UTC del mismo agregado confirmado.

Secuencia protegida: carrito → preparar UUID → escribir marcador durable → transacción SQLite inmediata [revalidar, asignar folio, insertar partidas/pago/venta/auditoría] → commit confirmado → vaciar sólo ese carrito → mostrar resultado → aceptación elimina marcador.

Si falla antes de commit, rollback conserva carrito y revierte también la secuencia. Si falla al entregar el resultado, una consulta independiente por UUID verifica si existe. Si existe devuelve la venta original; si no existe libera el intento; si no puede consultar conserva marcador y carrito, bloqueando nuevas operaciones. Al reiniciar, el marcador permite recuperar la venta incluso sin carrito. Permanece hasta aceptar el resultado, cubriendo un cierre entre commit y presentación. Un reintento con otro contenido se rechaza; no borra un marcador previo ni duplica venta/auditoría. Devolver una venta antigua no vacía un carrito nuevo.

ExpectedCash = fondo inicial + suma de Payment.Applied de las ventas de esa sesión. Ejemplo: fondo $500, venta $53, recibido $100 y cambio $47 → esperado $553. No se suman $100 y se resta cambio otra vez. Conteo y venta compiten bajo transacciones inmediatas: si el conteo gana, la venta se rechaza; si la venta gana, el esperado la incluye. El conteo sigue ciego, inmutable y separado de confirmar cierre.

## Verificación y evidencia

- Baseline 1D: 120/120, repetido desde exportación Git recuperada.
- Checkpoint inicial 1E: 147/147; refuerzo de recuperación/concurrencia: 152/152.
- Final después de clean, restore locked, build Release y suite completa: **153 aprobadas, 0 fallidas, 0 omitidas; 0 errores y 0 advertencias de compilación**. El runner avisó sólo de sobrescribir el TRX previo.
- Cobertura nueva: inmutabilidad y SQL directo, ausencia de huérfanos/venta parcial, rollback por auditoría, folio, snapshot, permiso revocado, carrito obsoleto, resultado perdido después de commit, bloqueo y reinicio antes de aceptar recibo, marcador inválido, cero/insuficiente, retry concurrente, carrera conteo/venta, overflow del esperado, separación entre cajas y restauración.
- Smoke por servicios reales/SQLite: 2 conchas de $19 + 3 bolillos de $5 = $53; $50 rechazado, $100 aceptado, cambio $47; recuperación con una UserSession nueva, aceptación de resultado y caja cerrada con esperado $53/diferencia $0. No se presenta esta prueba como interacción WPF.
- Backup/restore CLI a directorio nuevo: coinciden seis migraciones, terminal/configuración, categoría, dos productos, caja, conteo, venta, dos partidas, pago, secuencia y ocho auditorías. Integridad/FK e historial validados por Maintenance.
- Publicación autocontenida Release win-x64 actualizada en artifacts/development, con hashes de ejecutable/ensamblados registrados. Publicación local de desarrollo, sin instalador firmado ni publicación remota.
- Revisión del diff: sin cambios en migraciones 001–005 ni lockfiles; ninguna operación float/double/TOTAL en la nueva ruta financiera. git diff --check sin errores de whitespace.

Evidencia persistente en docs/evidence/phase1e-*: baseline y baseline recuperado, dominio/persistencia/checkout/cierre, verificación, clean, publicación, restore, hashes, smoke de servicios, backup/restore CLI y comparación. El detalle visual está en phase1e-ui-smoke.md. ADR-010 documenta las decisiones.

## Smoke WPF y pendiente concreto

Comprobado en instalación sintética separada: arranque/login, caja recuperada, POS sin cambios visuales profesionales, Cobrar deshabilitado con carrito vacío, carrito de dos líneas y total $53, apertura de diálogo con total correcto, confirmación deshabilitada sin recibido y Volver conservando carrito. Se cerró normalmente descartando sólo el carrito de prueba.

No se pudo completar la introducción de recibido en el modal mediante la herramienta de Windows: la activación del propietario quitaba foco y set_value devolvía elemento no disponible. No se registró una venta mediante UI. Falta verificar visualmente recibido insuficiente, cambio, confirmación, recibo, regreso a carrito vacío, reinicio y cierre con la venta. NEXT_STEPS.md contiene el recorrido exacto. No se declara smoke completo ni se solicita una nueva autorización de implementación.

## Archivos principales

Nuevos: Domain/Sale.cs; Application/CheckoutService.cs; Infrastructure/SqliteSaleStore.cs y CheckoutJournal.cs; migrations/006_cash_sales.sql; Presentation/CheckoutViewModel.cs y CheckoutDialog.xaml/.cs; tests SaleTests, SalePersistenceTests, CheckoutTests y SalesClosingTests; ADR-010, este reporte y evidencia 1E.

Integración: LocalPorts, PosService y CashClosingService; MainViewModel/MainWindow y PosView/PosViewModel; expectativas de FoundationTests/CashIdentityTests/PosTests; .gitignore. Documentación: README, AGENTS, CURRENT_STATE, NEXT_STEPS, PHASE_1E_PROGRESS, domain, database, architecture, operations, roadmap y FILES. .gitattributes se corrigió antes, como parte del baseline recuperable 1D.

## Límites y continuidad

La aceptación visual pendiente impide declarar 1E completamente cerrada. No iniciar funcionalidades posteriores. Persisten límites de desarrollo: sin pruebas táctiles físicas, corte eléctrico real, retención/cifrado/segunda copia ni recuperación administrativa de usuarios.

checkout.pending almacena sólo UUID y no viaja en .cmbackup; el backup sí conserva venta/pago. Resolver/aceptar el último intento antes de respaldo operativo. Tras restaurar una copia antigua, conciliar cobros posteriores y no repetir automáticamente una operación. Restauración y resolución de intento son procedimientos diferentes. Un usuario original desactivado/sin permiso requiere un proceso administrativo futuro; no saltar autorización editando SQLite.

El siguiente paso es completar y documentar el smoke WPF pendiente, actualizar aceptación y registrar esa evidencia. El alcance 1E ya está autorizado.
