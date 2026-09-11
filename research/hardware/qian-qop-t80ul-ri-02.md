# Qian QOP-T80UL-RI-02

Consulta ampliada: 2026-09-11. No se ha conectado ni enviado comandos a hardware.

Manual oficial específico: https://manuales.qian.mx/QOP-T80UL-RI.pdf

El manual identifica QOP-T80UL-RI-02, USB+LAN, Ethernet 10/100 puerto 9100, 72 mm, 576 puntos, 203 DPI, ESC/POS, fuentes A/B, páginas PC850/WPC1252, corte completo/parcial y configuración de cutter/drawer mediante driver. Describe autoprueba, instalación del driver, detección del puerto USB y página de prueba Windows. No publica bytes de codepage/corte/cajón, pinout ni pulso.

Ficha secundaria: https://fichas.qian.mx/QOP-T80UL-RI-02.pdf

**Inconsistencia:** la URL contiene T80UL, pero el documento dice QOP-T80BL-RI-02. No se considera confirmación de la unidad del usuario. La ficha describe 80 mm, USB+LAN, ESC/POS, corte, Windows y salida de cajón 24 V/1 A; no aporta una validación específica de Windows 11, comandos precisos, pinout ni temporización.

Pendiente: etiqueta/modelo/revisión de equipo real; manual exacto, driver oficial firmado Windows 11, compatibilidad eléctrica y de conector del cajón. No extrapolar pinout ni pulsos desde otra impresora ESC/POS. No asumir que conector similar implica compatibilidad.

Checkpoint FASE 1G: seguir `docs/PHASE_1G_PHYSICAL_CHECKPOINT.md`. USB precede a LAN. Registrar firmware, driver, cola, puerto, codepage visible y resultados de autoprueba/página Windows. No imprimir desde el POS, cortar ni abrir cajón hasta nueva autorización.

La API Winspool RAW permite enviar lenguaje de impresora, pero no valida que la Qian entienda todos los comandos Epson. [WritePrinter](https://learn.microsoft.com/en-us/windows/win32/printdocs/writeprinter). Corte y apertura siguen pendientes de documentación y equipo.
