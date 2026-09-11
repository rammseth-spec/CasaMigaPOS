# Estado actual — FASE 1E CERRADA

Fecha: 2026-09-10. FASE 1D aprobada, baseline verificable `92c5ef795b6da8e1cdf7f34ec9fb1b01cce4a5d2`: exportado y probado, 120 pruebas. Git local: Rammseth, `rammstudioworkshop\@gmail.com`. Sin remoto.

1E autorizada e implementada en `18fd3a8e56c79ef7a5710443da09d364f965f6fa`: Domain comercial inmutable; migración 006; checkout efectivo atómico con auditoría; identificador único y recuperación durable; ExpectedCash integrado; UI mínima Cobrar. Sin nuevas dependencias, rediseño ni funcionalidades fuera de alcance. 001–005 mantienen sus bytes originales. Verificación final: 153 aprobadas, cero fallidas/omitidas, build sin errores/advertencias.

Cart sigue siendo transitorio. Sólo un commit confirmado crea una venta; un rollback conserva las partidas. El marcador checkout.pending sólo contiene UUID y permanece hasta que el operador acepta el resultado. El reinicio recupera el resultado comercial, no el carrito perdido. Un resultado incierto bloquea nuevas operaciones.

La política aprobada conserva nombre/precio del carrito y revalida producto/categoría activos al cobrar. Esperado = fondo + suma del efectivo aplicado de ventas de esa caja; recibido y cambio no se suman dos veces. No se vende después de confirmar conteo.

Estado de aceptación: **FASE 1E CERRADA**. El usuario confirmó que $50.00 impidió confirmar, $100.00 mostró $47.00 de cambio, el resultado se presentó y se aceptó normalmente, y el POS regresó con carrito vacío y total $0.00. La evidencia técnica confirma una sola venta de $53.00, dos partidas, un pago CASH y una auditoría; reinicio con venta persistida y sin checkout.pending; caja CLOSED con esperado/contado $53.00 y diferencia $0.00. Los 24 pasos del smoke WPF están completos. Suite posterior al recorrido: 153 aprobadas, 0 fallidas/omitidas, build sin errores/advertencias; publicación win-x64 correcta. Consultar PHASE_1E_VISUAL_ACCEPTANCE.md.

Límites: el sistema continúa como versión de desarrollo y no está listo para producción; no hay certificación de hardware, pantalla táctil física ni fallo eléctrico. Backup incluye ventas y pagos en la base, no carrito ni marcador de intento; restaurar sólo a carpeta nueva y reconciliar el último cobro antes de operar. No modificar manualmente datos comerciales para resolver incidencias. No se inició ninguna fase posterior.
