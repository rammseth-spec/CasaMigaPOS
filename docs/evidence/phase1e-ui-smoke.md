# Smoke WPF de FASE 1E — intento inicial y cierre posterior

2026-09-07, publicación autocontenida win-x64, instalación sintética work/phase1e-ui restaurada de un paquete 1D y migrada a v6. No se alteró la instalación 1D original.

Observado mediante control de Windows y capturas de la aplicación:

- Login y caja abierta recuperada; POS en ventana de desarrollo 1366×768 DIP.
- Carrito vacío: Cobrar deshabilitado.
- Dos conchas a $19.00 y tres bolillos a $5.00: dos líneas, total $53.00.
- Cobrar abre captura de efectivo de 640 DIP; total $53.00, recibido vacío, cambio — y Confirmar cobro deshabilitado. Controles visibles sin rediseño.
- Volver al POS conserva las dos líneas y $53.00; no se confirmó venta por UI.
- Cierre normal pide descarte del carrito transitorio; se descartó sólo el carrito sintético y la aplicación cerró.

No completado: introducir importe insuficiente/suficiente, confirmar, observar recibo y cierre después de vender. La herramienta de control activaba la ventana propietaria al enviar texto y la entrada modal seguía vacía. set_value reportó elemento no disponible a pesar de aparecer en el árbol de accesibilidad. El teclado auxiliar tampoco resolvió la entrada. No se cambió el producto para eludir la herramienta.

Esta sección conserva el límite exacto del intento del 2026-09-07. El recorrido posterior del 2026-09-10 sí quedó completo: el usuario confirmó que $50.00 no permitió cobrar, $100.00 mostró cambio $47.00, el resultado fue correcto y se aceptó, y el POS regresó con carrito vacío/total $0.00. La evidencia técnica confirmó una sola venta de $53.00, reinicio sin checkout.pending y caja CLOSED con esperado/contado $53.00 y diferencia cero. Consultar phase1e-final-state.json y PHASE_1E_VISUAL_ACCEPTANCE.md.
