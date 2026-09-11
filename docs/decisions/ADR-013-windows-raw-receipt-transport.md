# ADR-013 — Transporte RAW mediante spooler Windows

Fecha: 2026-09-11. Estado: aceptada; Bloque B validó transporte físico y Bloque C autoriza tamaño y un corte parcial controlado.

## Contexto

La Qian QOP-T80UL-RI-02 declara USB, LAN, 72 mm/576 puntos, driver Windows y compatibilidad ESC/POS. El manual público no publica los bytes de selección de página, corte ni pulso de cajón. Abrir USB o puerto 9100 directamente duplicaría conectividad y diagnóstico que Windows ya ofrece.

Microsoft documenta trabajos RAW mediante OpenPrinter, StartDocPrinter, StartPagePrinter, WritePrinter, EndPagePrinter y EndDocPrinter. Estas llamadas pueden bloquear, por lo que no deben ejecutarse en el hilo WPF. La aceptación del spooler no demuestra que el papel salió.

Fuentes: [manual oficial Qian](https://manuales.qian.mx/QOP-T80UL-RI.pdf), [impresión directa Win32](https://learn.microsoft.com/en-us/windows/win32/printdocs/sending-data-directly-to-a-printer), [spooler Windows](https://learn.microsoft.com/en-us/windows/win32/printdocs/print-spooler) y [StartDocPrinter](https://learn.microsoft.com/en-us/windows/win32/printdocs/startdocprinter).

## Decisión

Infrastructure implementa `WindowsRawPrintTransport`. Recibe un nombre lógico de cola, nombre de trabajo y bytes. Devuelve el JobId de StartDocPrinter. Una falla antes de obtener JobId es FAILED; una falla después de iniciar el documento es UNKNOWN. Los códigos técnicos se reducen a una lista de caracteres segura y 64 posiciones.

`SerializedReceiptPrintTransport` garantiza un solo envío activo dentro de la aplicación. Presentation ejecuta el servicio mediante `Task.Run`, fuera del hilo de UI. `printing.json` se lee sólo cuando se intenta imprimir; si falta o es inválido, la venta y el resto del POS siguen disponibles.

En Bloque A, `EscPosReceiptRenderer` sólo convirtió texto determinista. Bloque B confirmó físicamente el recorrido RAW sin bytes de control. Para Bloque C, la [ficha oficial Qian QOP-T80UL-RI-02](https://manuales.qian.mx/QOP-T80UL-RI-02.pdf) declara compatibilidad Epson ESC/POS y cortador automático completo/parcial. La referencia oficial Epson documenta [GS !](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_exclamation.html), [GS V](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_cv.html) y [LF](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/lf.html).

Bloque C introduce únicamente estas secuencias:

| Bytes hexadecimales | Propósito |
| --- | --- |
| `1D 21 11` | `GS ! 17`: encabezado a 2× ancho y 2× alto. |
| `1D 21 01` | `GS ! 1`: TOTAL a ancho normal y doble altura. |
| `1D 21 00` | `GS ! 0`: restaurar explícitamente tamaño normal después de cada sección ampliada. |
| `0A 0A 0A 0A` | Cuatro avances de línea después del último texto. |
| `1D 56 01` | `GS V 1`: solicitar un único corte parcial, al final del payload. |

El encabezado 2×2 se compone con un ancho efectivo de `columns / 2`, por lo que en papel configurado a 48 columnas se centra y envuelve a 24 caracteres. TOTAL conserva 48 caracteres efectivos porque sólo duplica altura. `cutMode` acepta únicamente `Disabled` o `Partial`; el corte se agrega sólo con `Partial`. `openCashDrawer` continúa siendo obligatorio en false y no se introduce ninguna secuencia ESC/pulso. Tampoco se introduce selección de codepage ni inicialización.

## Bloque D: hipótesis controlada de codepage

La ficha Qian enumera PC850 entre los juegos extendidos, pero no publica en el documento disponible el número de tabla usado por el firmware. La referencia oficial Epson de [ESC t](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/esc_lt.html) define `1B 74 n` y asigna `n = 2` a PC850 Multilingual. Como Qian declara compatibilidad Epson ESC/POS, Bloque D autoriza una sola prueba de la combinación **.NET `ibm850` (code page 850) + ESC/POS tabla 2 (`1B 74 02`)**. Esta correspondencia sigue siendo una hipótesis hasta observar la impresión; si falla, no se prueba otra tabla automáticamente.

[Microsoft documenta](https://learn.microsoft.com/en-us/dotnet/api/system.text.codepagesencodingprovider?view=net-10.0) que `CodePagesEncodingProvider.Instance` debe registrarse para obtener estas páginas en .NET. El renderer usa `EncoderFallback.ExceptionFallback`: cualquier carácter no representable detiene el envío en vez de sustituirse silenciosamente. `PrintingSettings` conserva juntos nombre de encoding y número ESC/POS, y sólo acepta las parejas documentadas `us-ascii`/sin tabla, `ibm850`/2 y `windows-1252`/16. El comando de selección, cuando existe, aparece exactamente una vez al inicio del payload y antes de cualquier texto.

## Consecuencias

Windows controla el puerto USB y más adelante podrá controlar una cola LAN sin cambiar Application. La cola puede aceptar un trabajo con hardware desconectado; por ello el estado visible es «aceptado por la cola», no «impreso físicamente». El corte parcial debe validarse con exactamente un ticket sintético y observación humana. Codepage, español, logo, cajón y LAN permanecen bloqueados.
