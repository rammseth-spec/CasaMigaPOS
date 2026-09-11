# FASE 1G — BLOQUE E.1 — INVESTIGACIÓN DEL MARGEN SUPERIOR

Fecha: 2026-09-11. Estado: análisis terminado; ninguna estrategia implementada. FASE 1G permanece abierta. Bloque E está validado físicamente salvo la observación de exceso de blanco antes del logo.

## Git y alcance

- HEAD inicial: `0bef2274bfc8e6a9ac7669bbe47645c4cdf4f664`.
- HEAD final: commit documental de este reporte; el hash exacto se entrega en el cierre del bloque.
- No se imprimió, no se modificó el logo, no se cambió código, renderer, configuración, driver ni ESC/POS.
- Sólo se agregaron o actualizaron documentos de investigación y continuidad.

## Payload real

Se inspeccionó `docs/evidence/phase1g-block-e-opcion07.payload.bin`, PrintRequestId `3d8ad746-b867-4540-8e42-9eb21e7848f4`, JobId 6 y SHA-256 `E4B48FE42820B44B2A6ACCA9013EAB3579FA4502CFC0BB41BB1004BD5F6C95B8`.

- Bytes desde offset 0 hasta antes de `GS v 0`: `1B 74 02 1B 61 00`.
- Offset de `GS v 0`: **6 decimal** (`0x0006`).
- `1B 74 02`: seleccionar PC850 mediante `ESC t 2`.
- `1B 61 00`: alineación izquierda mediante `ESC a 0`.
- Encabezado raster desde offset 6: `1D 76 30 00 48 00 06 01`.
- Datos raster desde offset 14.

Antes del raster no hay `LF 0A`, `CR 0D`, espacios `20`, `ESC d`, `ESC J`, `GS V`, inicialización `ESC @`, saltos, feeds ni comandos verticales. Ambos comandos previos sólo configuran codepage y alineación. Por tanto, **el raster es el primer contenido físicamente imprimible** y Casa Miga POS genera **cero alimentación de papel antes del logo**.

El transporte abre el trabajo como `RAW` y entrega este byte array exacto mediante `WritePrinter`; no aplica el pipeline gráfico del driver. Esto coincide con el procedimiento oficial de Microsoft para enviar control data directamente: <https://learn.microsoft.com/en-us/windows/win32/printdocs/sending-data-directly-to-a-printer>.

## Medición del bitmap

Activo intacto: `assets/brand/Opcion07/assets/Casa_Miga_Logo_Oficial_Opcion07_Ticket_560px_1bit.png`. La medición se hizo sobre el raster real enviado; el renderer conserva todos los píxeles y sólo agrega ocho puntos blancos a cada lado para completar 576.

| Medida | PNG oficial | Raster enviado de 576 puntos |
| --- | ---: | ---: |
| Ancho | 560 px | 576 px |
| Alto | 262 px | 262 px |
| Primera fila negra, índice desde cero | 8 | 8 |
| Última fila negra, índice desde cero | 258 | 258 |
| Blanco superior | 8 px = **1.001 mm** | igual |
| Blanco inferior | 3 px = **0.375 mm** | igual |
| Primer píxel negro X | 11 | 19 |
| Último píxel negro X | 547 | 555 |
| Blanco izquierdo/derecho | 11/12 px | 19/20 px |

Conversión: `píxeles / 203 × 25.4`. Eliminar únicamente las filas completamente blancas superiores reduciría el blanco antes del logo en **8 px, aproximadamente 1.00 mm**. Eliminar también las tres filas blancas inferiores reduciría el largo total en un máximo de **11 px, aproximadamente 1.38 mm**. No se creó tal derivado.

## Margen digital frente a margen mecánico

**Margen digital:** el PNG contiene 8 filas blancas superiores, equivalentes a aproximadamente 1.00 mm. El payload no agrega ninguna línea o alimentación antes de esas filas.

**Margen físico:** tras el corte anterior, el borde del papel queda en el cortador, mientras que la primera impresión ocurre en el cabezal situado detrás de él. Epson advierte oficialmente que existe una separación entre la posición de corte y la de impresión y que la distancia depende del modelo. Su referencia enumera ejemplos de aproximadamente 6.4, 9.5, 14 y 24.5 mm; esas cifras corresponden a modelos Epson y **no deben atribuirse a la Qian**: <https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_cv.html>.

La documentación pública oficial de la Qian QOP-T80UL-RI-02 confirma 203 DPI, 576 puntos, ESC/POS y cortador completo/parcial, pero no publica la distancia cabezal-cortador, margen superior mínimo, backfeed, presenter ni una función Qian de reducción del margen: <https://manuales.qian.mx/QOP-T80UL-RI-02.pdf>.

Clasificación: **D, combinación de varios factores**, dominada por la mecánica cabezal/cortador. El PNG explica aproximadamente 1 mm; el resto del espacio observado no está en el payload y, por inferencia, procede de la posición física del papel después del corte.

## Paper saving, backfeed y driver

Epson documenta en determinados modelos funciones de corte con retorno a posición de impresión y parámetros de reducción automática del margen superior. También advierte que el margen depende del modelo y que alimentar al menos 1 mm después de ciertos cortes puede evitar alimentación irregular o atasco. `ESC e` realiza retroceso, pero su rango y soporte dependen del modelo: <https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/esc_le.html>. Esto no demuestra que la Qian soporte esas funciones.

Inspección de sólo lectura del driver instalado `POS-80 11.3.0.1`:

- Propiedad visible `PrintTrailingMarginOrNot`: `zjNotPrintTrailingMargin`.
- Valor asociado `TrailingMargin`: `zj30mm`, no aplicado mientras la opción anterior permanezca en “no imprimir margen final”.
- El GPD ofrece márgenes finales de 3 a 36 mm; se refieren al extremo posterior del trabajo, no al espacio antes del logo.
- El GPD declara `PrintableOrigin: (0,0)` y resolución 203 × 203 DPI.
- No aparecen propiedades de `Paper Saving`, `Top Feed`, `Top Margin`, `Feed before print`, `Cutter feed`, `Backfeed` ni `Reduce top margin`.
- El GPD contiene `CmdStartDoc = ESC @` para trabajos procesados por el driver, pero ese byte no está en el trabajo RAW de Casa Miga.

No pudieron verificarse una distancia mecánica exacta de la Qian, firmware, interruptores internos, capacidad de backfeed, paper saving ni opciones ocultas en utilidades externas del fabricante. La documentación oficial disponible tampoco confirma esas funciones para este modelo.

## Riesgos y recomendación

No se deben probar `ESC e`, funciones avanzadas `GS V`, retroceso ni comandos de modelos Epson en la Qian sin documentación específica. Los riesgos incluyen atasco, contacto con el cortador, pérdida de posición, alimentación irregular, desgaste y diferencias de firmware.

Estrategia conservadora recomendada: crear en un bloque posterior un derivado técnico versionado que elimine **sólo las ocho filas superiores totalmente blancas**, conservando los 560 píxeles de ancho, proporción visible y todos los píxeles negros. El ahorro máximo antes del logo será cercano a **1 mm**; no resolverá la separación mecánica dominante. Antes de cualquier intento adicional se debe obtener documentación o utilidad oficial Qian que confirme una función segura de ahorro de margen. Si no existe, aceptar la separación mecánica como límite del equipo.

La estrategia no fue implementada. Próximo paso exacto: autorizar o rechazar el recorte técnico de ocho filas blancas con la expectativa explícita de una mejora máxima aproximada de 1 mm; mantener bloqueado cualquier backfeed. Modelo recomendado: **SOL**, porque el cambio sería pequeño y determinista. Usar Astra sólo si aparece documentación Qian que exija analizar comandos o firmware específicos.
