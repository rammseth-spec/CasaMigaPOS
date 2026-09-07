# Qian QOP-T80UL-RI-02

Consulta: 2026-09-05. No se ha conectado ni enviado comandos a hardware.

Fuente oficial: https://fichas.qian.mx/QOP-T80UL-RI-02.pdf

**Inconsistencia:** la URL contiene T80UL, pero el documento dice QOP-T80BL-RI-02. No se considera confirmación de la unidad del usuario. La ficha describe 80 mm, USB+LAN, ESC/POS, corte, Windows y salida de cajón 24 V/1 A; no aporta una validación específica de Windows 11, comandos precisos, pinout ni temporización.

Pendiente: etiqueta/modelo/revisión de equipo real; manual exacto, driver oficial firmado Windows 11, compatibilidad eléctrica y de conector del cajón. No extrapolar pinout ni pulsos desde otra impresora ESC/POS. No asumir que conector similar implica compatibilidad.

Prueba FASE 2 mediante adaptadores: texto CASA MIGA / PANADERÍA TRADICIONAL MEXICANA / PRUEBA CASA MIGA POS / Concha vainilla / Bolillo / TOTAL / PAN DE FAMILIA. Luego acentos, ancho efectivo, logo oficial, QR, corte y, con cajón compatible, pulso documentado. Probar USB y LAN por separado, papel agotado, impresora apagada, desconexión, reimpresión y reinicio entre commit/envío. Registrar firmware, driver, transporte, resultado y evidencia física.

La API Winspool RAW permite enviar lenguaje de impresora, pero no valida que la Qian entienda todos los comandos Epson. [WritePrinter](https://learn.microsoft.com/en-us/windows/win32/printdocs/writeprinter). Corte y apertura siguen pendientes de documentación y equipo.
