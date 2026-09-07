# Smoke WPF de FASE 1E — parcial

2026-09-07, publicación autocontenida win-x64, instalación sintética work/phase1e-ui restaurada de un paquete 1D y migrada a v6. No se alteró la instalación 1D original.

Observado mediante control de Windows y capturas de la aplicación:

- Login y caja abierta recuperada; POS en ventana de desarrollo 1366×768 DIP.
- Carrito vacío: Cobrar deshabilitado.
- Dos conchas a $19.00 y tres bolillos a $5.00: dos líneas, total $53.00.
- Cobrar abre captura de efectivo de 640 DIP; total $53.00, recibido vacío, cambio — y Confirmar cobro deshabilitado. Controles visibles sin rediseño.
- Volver al POS conserva las dos líneas y $53.00; no se confirmó venta por UI.
- Cierre normal pide descarte del carrito transitorio; se descartó sólo el carrito sintético y la aplicación cerró.

No completado: introducir importe insuficiente/suficiente, confirmar, observar recibo y cierre después de vender. La herramienta de control activaba la ventana propietaria al enviar texto y la entrada modal seguía vacía. set_value reportó elemento no disponible a pesar de aparecer en el árbol de accesibilidad. El teclado auxiliar tampoco resolvió la entrada. No se cambió el producto para eludir la herramienta.

No afirmar que $50.00 fue introducido ni que la venta se cobró por UI. El smoke de servicios separado sí verificó $53/$100/$47, recuperación y cierre exacto, y está identificado expresamente como no UI en phase1e-service-smoke.txt. Sigue pendiente completar el recorrido visual descrito en NEXT_STEPS.md.
