# ADR-005 — Hardware y efectos posteriores al commit

Estado: puertos mínimos creados; transportes pendientes.

Application define IReceiptPrinter e ICashDrawer; Domain no conoce Qian, USB, TCP ni ESC/POS. TicketData es contrato preliminar de fundación, no el modelo final de ticket: evolucionar a partidas estructuradas antes de FASE 1. ITicketRenderer produce bytes y la impresora los transporta. Reimpresión parte de snapshots, nunca del catálogo vigente.

Decisión de integridad: confirmar venta/pagos/movimientos/auditoría y trabajo de impresión durable en la misma transacción local; luego imprimir, cortar y abrir cajón cuando corresponda. El diagrama del hito que pone registrar venta después de imprimir se interpreta como resultado visible, no como orden de persistencia, porque contradice la confirmación previa requerida en la sección de cajón.

No existe transacción atómica entre SQLite y papel. Tras desconexión o reinicio puede desconocerse si se imprimió/abrió: representar Unknown, ofrecer reimpresión identificada y no repetir automáticamente el pulso del cajón. ACK del spooler no prueba salida física. Apertura manual requiere permiso y auditoría previa persistida. El puerto no sustituye esa validación del caso de uso.

Ruta candidata USB: driver Windows y spooler RAW. Ethernet: transporte a dirección/puerto documentados, sin asumir 9100. WritePrinter es bloqueante y debe correr fuera de UI. No se implementan transportes en FASE 0.

El modelo Qian y pinout/pulso requieren confirmación; consultar research/hardware/qian-qop-t80ul-ri-02.md. No enviar comandos a dispositivos en esta fase.
