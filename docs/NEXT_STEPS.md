# Próximos pasos

Actualización 2026-09-11: **FASE 1F CERRADA**. Punto de partida: baseline 1E `d4118144dd6f19bff885ced15a3f6098c24f7c29`, implementación 1F `b957190279fb984abbe00ac8e022f33acfa47909` y evidencia/cierre `912cf5232ae54971827b3e337bcb72cd1e05c4bb`.

Estado verificable: migraciones 001–007; bytes 001–006 comprobados contra sus hashes históricos; 170 pruebas aprobadas, ninguna fallida u omitida; build Release sin errores ni advertencias; smoke WPF y backup/restore completos; aplicación cerrada. Publicación de desarrollo disponible en `artifacts/development`.

No hay FASE 1G autorizada. No modificar migraciones aplicadas ni datos comerciales manualmente. Mantener las ventas/pagos/partidas inmutables y usar reversos trazables cuando una fase futura defina cancelaciones o devoluciones. Conservar `ILocalUnitOfWork.Run` inmediato para escrituras y la ruta read-only/deferred para historial.

Próximo paso recomendado, sin implementarlo: definir y autorizar formalmente el siguiente incremento comercial. Antes de producción siguen pendientes administración/recuperación de usuarios, zona horaria comercial configurable, reglas de cancelación/devolución, retención/segunda copia/cifrado, activación operativa de restore, pruebas físicas, licencia y distribución. No iniciar inventario, impresión, cajón, rediseño o fullscreen sin alcance explícito.
