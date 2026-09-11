# Próximos pasos — validación humana FASE 1G Bloque D

Actualización 2026-09-11: **FASE 1G ABIERTA; ENVÍO ÚNICO PC850/TABLA 2 TERMINADO, VALIDACIÓN HUMANA PENDIENTE**. Implementación Bloque D: `283cfa4`.

Estado verificable: migraciones 001–008 sin cambios; 188 pruebas aprobadas, ninguna fallida u omitida; build Release sin errores ni advertencias. Winspool aceptó un único payload PC850 de 889 bytes en POS-80 como JobId 5. `ESC t 2` ocupa los tres primeros bytes; un solo `GS V 01` ocupa los tres últimos; no hay cajón.

Siguiente acción: el usuario debe fotografiar el ticket completo y responder las diez preguntas de `PHASE_1G_BLOCK_D_REPORT.md`. No probar otra tabla ni imprimir de nuevo antes de esa evidencia. No avanzar a logo, cajón, LAN o reimpresión UI.

Antes de producción continúan pendientes datos reales, logo monocromático aprobado, administración/recuperación de usuarios, zona horaria comercial configurable, retención/segunda copia/cifrado, pruebas físicas, licencia y distribución. FASE 1G no está cerrada.
