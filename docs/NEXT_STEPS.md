# Próximo paso — FASE 1G Bloque F

La implementación y verificación técnica están completas. Ejecutar exactamente un smoke físico desde la UI: iniciar sesión en la instalación sintética `work/phase1f-visual-20260910`, abrir Ventas, elegir una venta existente, Ver detalle y pulsar una sola vez `REIMPRIMIR TICKET`. No crear venta ni abrir caja.

Después del envío registrar el request `REPRINT`, su evento final y JobId; comprobar por SQLite que siguen existiendo 55 ventas, 110 partidas y 55 pagos y que ExpectedCash no cambió. Solicitar al usuario una foto completa para confirmar logo Opción 07, marca REIMPRESIÓN, español, layout, corte parcial y ausencia de apertura de cajón. Detenerse; no iniciar FASE 1H.

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
