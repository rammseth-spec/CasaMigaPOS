# FASE 1G — Checkpoint físico Qian USB

Estado final: Bloques B–F confirmados. FASE 1G cerrada dentro del alcance autorizado. El smoke final de reimpresión produjo PrintRequestId `3ca46975-71af-4643-b80b-ff8290a5d356`, JobId 7 y estado SUBMITTED; la fotografía validó REIMPRESIÓN, logo Opción 07, español, contenido histórico, cero cajón y corte parcial. No repetir.

Resultado recibido: Qian QOP-T80UL-RI-02 por USB001; cola `POS-80`; driver `POS-80 11.3.0.1`; autoprueba y página Windows con salida física. Casa Miga envió después un único ticket ASCII: PrintRequestId `7ba5b12f-2482-457d-abf4-725181b25d60`, JobId 3, estado técnico SUBMITTED, 762 bytes. Ver `PHASE_1G_BLOCK_B_REPORT.md`.

Bloque C: PrintRequestId `6df7d54d-a095-48e3-9223-dfc09b5bdf51`, JobId 4, SUBMITTED, 770 bytes; encabezado 2×2, TOTAL doble alto, cuatro LF y un solo `GS V 01` al final. Ver `PHASE_1G_BLOCK_C_REPORT.md`.

Bloque D: PrintRequestId `957c1076-cd21-4ceb-b609-63b437a6c643`, JobId 5, SUBMITTED, 889 bytes; `ESC t 2` al inicio y cuerpo PC850. La fotografía `evidence/phase1g-block-d-physical.png` confirma los caracteres y la presentación; el usuario confirmó también el corte automático parcial. Ver `PHASE_1G_BLOCK_D_REPORT.md`.

## Preparación exacta

1. Confirma en la etiqueta inferior que el modelo sea `QOP-T80UL-RI-02`. Anota cualquier revisión, serie o firmware visible.
2. Con la impresora apagada, coloca un rollo térmico de 80 mm con la cara térmica en la orientación indicada por Qian y cierra la tapa.
3. Conecta el adaptador original de corriente. Conecta el cable USB directamente al PC de pruebas, sin hub inicialmente.
4. Para la autoprueba, apaga la impresora. Mantén presionado FEED y enciéndela; suelta FEED cuando se encienda el indicador señalado en el manual y espera la hoja de autoprueba.
5. Conserva o fotografía la autoprueba completa. Deben registrarse modelo, firmware, interfaces, ancho/dots, página de caracteres y cualquier configuración de cutter/drawer que aparezca.
6. Instala el driver entregado por Qian para impresora térmica de 80 mm. No descargues drivers de sitios de terceros.
7. En Windows abre **Configuración → Bluetooth y dispositivos → Impresoras y escáneres**. Selecciona la Qian y abre sus propiedades.
8. En **Puertos**, confirma el puerto USB detectado por el instalador. No selecciones TCP/IP ni abras el puerto 9100 en este bloque.
9. En **Configuración del dispositivo**, anota sin cambiar todavía las opciones disponibles para método de impresión, página de caracteres, cortador y cajón.
10. Copia exactamente el nombre de la cola mostrado por Windows, incluidos espacios, mayúsculas y sufijos.
11. Desde **Propiedades de impresora → General**, pulsa **Imprimir página de prueba**.
12. Confirma si Windows marca el trabajo como completado y si la hoja salió completa y legible.

## Resultado que debe devolverse

- Nombre exacto de la cola Windows.
- Windows 10 u 11, edición y arquitectura x64.
- Versión/nombre del driver si aparece.
- Puerto USB seleccionado.
- Resultado de autoprueba.
- Resultado de página de prueba Windows.
- Configuración visible de página de caracteres.
- Opciones visibles de corte completo/parcial.
- Opción visible de cajón y su estado, sin activarla.
- Cualquier error mostrado por Windows o por los indicadores.
- Fotos de autoprueba, página Windows y pantallas de configuración si hace falta interpretar valores.

Después de recopilar esta información, detenerse. No probar corte, cajón ni un ticket desde Casa Miga POS hasta nueva autorización.
