# CASA MIGA POS — CHECKPOINT 1I.5

FASE 1I ABIERTA. 1I.5 en hardening final; no cerrada, pendiente de smoke humano final. Baseline 8173390e1ef1069d2a271f132b18b0e335c98935. No fases posteriores ni funcionalidades nuevas.

## Prueba integral añadida

Phase1I5HardeningTests crea producto terminado COUNT, opening 10 y vínculo 1:1. Activa por SaleConsumptionService, vende 2 con Cashier sin permisos inventory.*, confirma stock 8 y $38.00 aplicados. Retry y conflicto se comparan por hash lógico de todas las tablas, incluyendo auditoría y folios, sin mutaciones.

Cambiar vínculo se realiza con Unlink/Link autorizados; pausa por servicio. Recovery de la venta original usa snapshots históricos sin recalcular factor 3 ni flag actual. Venta pausada aumenta efectivo sin consumo. Resume con vínculo eliminado falla sin cambios; reparar por API permite reanudar y vender otra unidad. Resultado automatizado: 3 ventas, $76.00 aplicados, stock 7. Insuficiencia 8 contra 7 bloquea y conserva carrito y hash de datos.

Nuevas instancias Initialize/Validate conservan historia. Backup .cmbackup se restaura en directorio nuevo y se comparan SHA-256 lógicos de todas las tablas. Recovery posterior a restore con sesión nueva recupera folio y no muta datos. Transporte simulado UNKNOWN de reimpresión y retry conserva receipt snapshot, ventas, stock y efectivo. Cierre técnico: apertura $500.00 + ventas $76.00 = ExpectedCash $576.00, contado $576.00, diferencia cero. Todo ocurre en bases de prueba, no en el smoke manual.

Caso separado de stock exacto 2→0 confirma y reconcilia. Se reutilizan regresiones existentes: legacy/v1, evaluación corrupta, commit incierto, rollback por cada escritura, concurrencia 5 con dos ventas de 4, agregación de líneas, NO_CONSUMPTION, permisos, preflight y tickets. No se reemplazan por afirmaciones visuales.

## Defecto limitado encontrado

InventoryViewModel.MovementLabel no contemplaba SaleConsumption y mostraba el enum técnico en el historial. Se añade traducción Venta. Regresión integrada obtiene el movimiento real desde Application y valida su presentación. No cambia política, persistencia, dinero ni stock.

La preparación inicial del test intentó sustituir un vínculo activo con Link; la API lo rechazó correctamente. Se corrigió el test usando Unlink antes de Link. No es defecto de producto.

## Evidencia y continuidad

Pruebas focalizadas previas a la corrección de etiqueta: 98 aprobadas, 0 fallidas/omitidas. Resultados completos y metadata del backup en evidence/phase1i5-verification.txt y evidence/phase1i5-integral.json. Backup binario conservado localmente en work/phase1i5-evidence/phase1i5.cmbackup. Credenciales y bases manuales fuera de Git.

Smoke humano separado preparado según PHASE_1I5_SMOKE.md: DISABLED, 10 piezas, $19.00, caja sin abrir. Ninguna acción manual de 1I.5 se declara realizada todavía. Detenerse al solicitar activación inicial y avanzar sólo con respuestas del usuario. No impresora física, producción, recetas, compras, proveedores, rediseño, fullscreen ni 013. Sistema de desarrollo, no listo para producción.

Después del smoke confirmado: completar las verificaciones de evidencia manual, actualizar cierre y entregar reporte final autocontenido con los 65 puntos solicitados. Mientras falte esa confirmación, FASE 1I permanece ABIERTA.

## Avance humano 2026-09-13 y desviación de rol

Capturas del usuario confirman ACTIVO/saldo 10; venta 00000001 por SMOKEADMIN, 2 piezas/$38, recibido $100/cambio $62; carrito vacío/$0; movimiento Venta -2 (10→8), Inventario consistente; PAUSADO/saldo 8; login SMOKECASHIER con caja existente OPEN; venta 00000002 por $19, recibido $20/cambio $1; PAUSADO/saldo 8 después; reanudación ACTIVO/saldo 8. Usuario confirmó aplicación CERRADA.

Consulta SQLite read-only posterior: dos ventas/pagos, CONSUMED y DISABLED respectivamente, ENABLED versión 4, apertura 0, efectivo aplicado 5700 centavos (ExpectedCash calculable $57), caja OPEN, pending ausente, integrity_check ok y cero violaciones FK. Ver evidence/phase1i5-manual.json. No se ejecutó otra venta ni se cerró caja desde herramientas.

Los pasos 8–10 de la venta principal fueron realizados con Administrator, no Cashier. No se declaran cumplidos en el rol requerido. Pendiente humano: comprobar venta con consumo activo usando SMOKECASHIER, después de reabrir; una venta nueva de 2 unidades dejaría stock 6 y efectivo acumulado $95. No repetir la intención anterior ni crear otra caja. Esperar captura de login antes de instruir el cobro. No cerrar fase todavía.

Defecto visual adicional: texto heredado en InventoryView.xaml afirmaba que la asociación todavía no descontaba. Se reemplaza por explicación condicionada al consumo ACTIVO y commit, con regresión InventoryAssociationHelpExplainsConditionalConsumption. Sin cambios de política ni migraciones.

La regresión detectó una segunda aparición del mismo texto heredado en el formulario Asociar producto; ambas se corrigieron. La primera ejecución de este bloque falló esa regresión; no se presentó como aprobada.

Revisión completa del defecto: tres mensajes (detalle, formulario y confirmación de asociación). Regresión XAML y regresión del flujo SaveLink aprobadas. Verificación final scripts/verify.ps1: 402/402, cero fallidas/omitidas, Release cero errores/advertencias; log work/phase1i5-final-verify-fixed.log. No cambios de migraciones.
