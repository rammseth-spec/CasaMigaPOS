# FASE 1G Bloque C — Legibilidad y primer corte controlado

Fecha: 2026-09-11. Estado actualizado en Bloque D: **jerarquía visual aprobada por el usuario**. Encabezado, cuerpo, TOTAL, alineación, márgenes y distribución fueron satisfactorios. La confirmación explícita del comportamiento exacto del corte parcial continúa pendiente si no se proporcionó por separado.

## Git y alcance

- HEAD inicial: `1aa550d8d5fd651df1b3c292d413e5360adae283`.
- Implementación: `65b78bf` (`feat: add controlled receipt sizing and partial cut`).
- El HEAD final corresponde al commit documental posterior a este envío.
- No se hizo push ni se configuró remoto.
- Estado Git previsto al cerrar el reporte: limpio.
- No cambiaron Sale, SaleItem, Payment, CheckoutService, ExpectedCash, cierre, historial, migraciones ni Presentation.

## Referencias oficiales

La [ficha oficial Qian QOP-T80UL-RI-02](https://manuales.qian.mx/QOP-T80UL-RI-02.pdf) declara compatibilidad Epson ESC/POS y cortador completo/parcial. Se usaron únicamente las referencias oficiales Epson para [GS !](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_exclamation.html), [GS V](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_cv.html) y [LF](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/lf.html).

## Renderer y jerarquía

- `PRUEBA CASA MIGA`: 2× ancho y 2× alto, centrado con ancho efectivo de 24 caracteres.
- Subtítulo, datos, producto, precios, efectivo, cambio y mensaje final: tamaño normal, 48 columnas.
- `TOTAL`: ancho normal y doble altura para mantener la alineación a la derecha.
- Después del encabezado y de TOTAL se restaura explícitamente el tamaño normal.
- Los encabezados largos se envuelven a 24 caracteres efectivos, sin truncamiento silencioso.

Secuencias exactas introducidas:

| Hex | Cantidad | Propósito |
| --- | ---: | --- |
| `1D 21 11` | 1 | `GS ! 17`, tamaño 2×2. |
| `1D 21 01` | 1 | `GS ! 1`, doble altura. |
| `1D 21 00` | 2 | `GS ! 0`, restauración normal. |
| `0A` | 4 al final | Avance de papel antes del cortador. |
| `1D 56 01` | 1 | `GS V 1`, solicitud de corte parcial. Es el último comando. |

No existe `ESC p`, pulso de cajón, segundo corte, inicialización ni selección de codepage.

## Configuración y envío único

La configuración local ignorada usa `POS-80`, 48 columnas, `us-ascii`, `cutMode: Partial`, `openCashDrawer: false` y perfil `PRUEBA CASA MIGA`. La cola estaba Normal en USB001 con driver POS-80 11.3.0.1.

- PrintRequestId: `6df7d54d-a095-48e3-9223-dfc09b5bdf51`.
- REQUESTED: `2026-09-11T19:58:51.2157194+00:00`.
- SUBMITTED: `2026-09-11T19:58:51.3412379+00:00`.
- JobId: `4`.
- Estado técnico: `SUBMITTED`.
- Bytes: `770`.
- SHA-256: `792FEFF59A9921E17AD77DFDD03D3B7D65447150B58ED195C159CF443DECB757`.
- Bytes exactos: `docs/evidence/phase1g-block-c-payload.hex`.

Los últimos siete bytes son exactamente `0A 0A 0A 0A 1D 56 01`. El mecanismo rechaza reutilizar el mismo directorio de intento. `SUBMITTED` significa aceptación por Winspool; todavía no confirma impresión ni corte físicos.

## Contenido imprimible

```text
    PRUEBA CASA MIGA
             PRUEBA TAMANO Y CORTE
------------------------------------------------
Folio: TEST-002
Fecha: 11/09/2026 19:58:51 +00:00
Cajero: PRUEBA
Terminal: DESARROLLO
------------------------------------------------
Producto prueba
1 x $19.00                                $19.00
------------------------------------------------
Subtotal                                  $19.00
TOTAL                                     $19.00
Pago                                        CASH
Efectivo                                  $20.00
Aplicado                                  $19.00
Cambio                                     $1.00
------------------------------------------------
             GRACIAS POR SU COMPRA
```

El primer renglón ocupa como máximo 24 columnas porque se imprime a doble ancho; TOTAL conserva 48 porque sólo duplica altura.

## Pruebas

- Focalizadas ReceiptTests: 17 aprobadas, 0 fallidas, 0 omitidas.
- Suite completa: 187 aprobadas, 0 fallidas, 0 omitidas.
- Build Release: 0 errores, 0 advertencias.

Las pruebas cubren tamaños y restauraciones, wrapping efectivo, un solo corte al final, cuatro avances, ausencia de cajón, determinismo, bloqueo de reenvío y conservación de venta/efectivo esperado.

## Archivos modificados

- `src/CasaMiga.Infrastructure/EscPosReceiptRenderer.cs`.
- `src/CasaMiga.Infrastructure/PrintingConfiguration.cs`.
- `src/CasaMiga.Infrastructure/ControlledReceiptPrintTest.cs`.
- `tests/CasaMiga.Tests/ReceiptTests.cs`.
- `docs/decisions/ADR-013-windows-raw-receipt-transport.md`.
- `README.md`, `docs/CURRENT_STATE.md`, `docs/NEXT_STEPS.md` y documentación/evidencia de Bloques B/C.
- `work/phase1g-block-c/printing.json`, traza y payload binario permanecen ignorados por Git y fuera de backup.

## Validación humana requerida

Se necesita una foto completa del ticket y confirmar:

1. ¿El tamaño de CASA MIGA se ve bien?
2. ¿El cuerpo se lee cómodamente?
3. ¿TOTAL tiene suficiente énfasis?
4. ¿Los productos y precios siguen alineados?
5. ¿Hay algo cortado por la derecha?
6. ¿Los márgenes se ven correctos?
7. ¿La densidad es suficiente?
8. ¿El cortador funcionó?
9. ¿Fue corte parcial?
10. ¿El ticket quedó fácil de retirar?
11. ¿Quedó suficiente papel debajo del último texto?

Permanecen pendientes español/codepage, logo, reimpresión UI, cajón y LAN. No se debe ajustar el renderer ni ejecutar otra impresión antes de recibir la foto.

Se recomienda **SOL** para el siguiente bloque porque corresponde interpretar evidencia visual y realizar cambios acotados. Usar ASTRA sólo si aparece incompatibilidad real de ESC/POS, Winspool, driver, bytes o bloqueo/reinicio de la impresora.
