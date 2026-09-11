# Estado actual — FASE 1E

Fecha: 2026-09-10. FASE 1D aprobada, baseline verificable `92c5ef795b6da8e1cdf7f34ec9fb1b01cce4a5d2`: exportado y probado, 120 pruebas. Git local: Rammseth, `rammstudioworkshop\@gmail.com`. Sin remoto.

1E autorizada e implementada en `18fd3a8e56c79ef7a5710443da09d364f965f6fa`: Domain comercial inmutable; migración 006; checkout efectivo atómico con auditoría; identificador único y recuperación durable; ExpectedCash integrado; UI mínima Cobrar. Sin nuevas dependencias, rediseño ni funcionalidades fuera de alcance. 001–005 mantienen sus bytes originales. Verificación final: 153 aprobadas, cero fallidas/omitidas, build sin errores/advertencias.

Cart sigue siendo transitorio. Sólo un commit confirmado crea una venta; un rollback conserva las partidas. El marcador checkout.pending sólo contiene UUID y permanece hasta que el operador acepta el resultado. El reinicio recupera el resultado comercial, no el carrito perdido. Un resultado incierto bloquea nuevas operaciones.

La política aprobada conserva nombre/precio del carrito y revalida producto/categoría activos al cobrar. Esperado = fondo + suma del efectivo aplicado de ventas de esa caja; recibido y cambio no se suman dos veces. No se vende después de confirmar conteo.

Estado de aceptación: NO CERRADA únicamente por confirmación visual manual pendiente. El usuario completó el cobro sintético: una venta de $53.00, dos partidas, un pago de $100.00 con cambio $47.00 y una auditoría. Codex verificó reinicio con POS vacío y sin checkout.pending, realizó conteo $53.00 y confirmó cierre WPF: CLOSED, esperado/contado $53.00 y diferencia $0.00. Suite posterior al cierre: 153 aprobadas, 0 fallidas/omitidas, build sin errores/advertencias; publicación win-x64 correcta. Falta confirmar observación manual del rechazo con $50.00, cambio visible, resultado y retorno inmediato al POS. No repetir esta venta ni reabrir la caja cerrada. Consultar PHASE_1E_VISUAL_ACCEPTANCE.md.

Límites: instalación de desarrollo; no producción, hardware, pantalla táctil física ni fallo eléctrico certificado. Backup incluye ventas y pagos en la base, no carrito ni marcador de intento; restaurar sólo a carpeta nueva y reconciliar el último cobro antes de operar. No modificar manualmente datos comerciales para resolver incidencias.
