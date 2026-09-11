# ADR-013 — Transporte RAW mediante spooler Windows

Fecha: 2026-09-11. Estado: aceptada para FASE 1G Bloque A; validación física pendiente.

## Contexto

La Qian QOP-T80UL-RI-02 declara USB, LAN, 72 mm/576 puntos, driver Windows y compatibilidad ESC/POS. El manual público no publica los bytes de selección de página, corte ni pulso de cajón. Abrir USB o puerto 9100 directamente duplicaría conectividad y diagnóstico que Windows ya ofrece.

Microsoft documenta trabajos RAW mediante OpenPrinter, StartDocPrinter, StartPagePrinter, WritePrinter, EndPagePrinter y EndDocPrinter. Estas llamadas pueden bloquear, por lo que no deben ejecutarse en el hilo WPF. La aceptación del spooler no demuestra que el papel salió.

Fuentes: [manual oficial Qian](https://manuales.qian.mx/QOP-T80UL-RI.pdf), [impresión directa Win32](https://learn.microsoft.com/en-us/windows/win32/printdocs/sending-data-directly-to-a-printer), [spooler Windows](https://learn.microsoft.com/en-us/windows/win32/printdocs/print-spooler) y [StartDocPrinter](https://learn.microsoft.com/en-us/windows/win32/printdocs/startdocprinter).

## Decisión

Infrastructure implementa `WindowsRawPrintTransport`. Recibe un nombre lógico de cola, nombre de trabajo y bytes. Devuelve el JobId de StartDocPrinter. Una falla antes de obtener JobId es FAILED; una falla después de iniciar el documento es UNKNOWN. Los códigos técnicos se reducen a una lista de caracteres segura y 64 posiciones.

`SerializedReceiptPrintTransport` garantiza un solo envío activo dentro de la aplicación. Presentation ejecuta el servicio mediante `Task.Run`, fuera del hilo de UI. `printing.json` se lee sólo cuando se intenta imprimir; si falta o es inválido, la venta y el resto del POS siguen disponibles.

En Bloque A, `EscPosReceiptRenderer` sólo convierte el texto determinista a la codificación configurada. No emite ESC, GS, selección de página, inicialización, corte ni cajón. `cutMode` debe ser Disabled y `openCashDrawer` false; cualquier otra configuración se rechaza.

## Consecuencias

Windows controla el puerto USB y más adelante podrá controlar una cola LAN sin cambiar Application. La cola puede aceptar un trabajo con hardware desconectado; por ello el estado visible es «aceptado por la cola», no «impreso físicamente». La configuración de codepage, corte parcial y cajón permanece bloqueada hasta documentación y smoke físicos.
