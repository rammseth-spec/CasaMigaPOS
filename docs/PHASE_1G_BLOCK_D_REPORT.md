# FASE 1G Bloque D — Codepage y caracteres españoles

Fecha: 2026-09-11. Estado: **BLOQUE D CERRADO Y VALIDADO FÍSICAMENTE; FASE 1G PERMANECE ABIERTA**.

## Git y alcance

- HEAD inicial: `f7aadcce61e1617d004b506f1f86f92e5dcb7e17`.
- Implementación: `283cfa4` (`feat: add controlled PC850 character table selection`).
- El HEAD final corresponde al commit documental posterior al envío.
- Estado Git previsto al cerrar: limpio. Sin push ni remoto.
- No cambiaron tamaños, alineaciones, corte, Sale, Payment, CheckoutService, ExpectedCash, cierre, historial, migraciones ni Presentation.

## Fuentes y decisión

La [ficha oficial Qian](https://manuales.qian.mx/QOP-T80UL-RI-02.pdf) declara compatibilidad Epson ESC/POS y enumera PC850 entre sus juegos extendidos, pero no publica el número de tabla del firmware. Epson documenta [ESC t](https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/esc_lt.html) como `1B 74 n` y asigna `n = 2` a [PC850 Multilingual](https://download4.epson.biz/sec_pubs/pos/reference_en/charcode/page_02.html). Microsoft documenta el registro de [CodePagesEncodingProvider](https://learn.microsoft.com/en-us/dotnet/api/system.text.codepagesencodingprovider?view=net-10.0) para obtener estas codificaciones en .NET.

La única hipótesis probada fue:

- Encoding .NET: `ibm850`, code page 850.
- Tabla ESC/POS: PC850 Multilingual.
- Número: `2`.
- Selección: `ESC t 2`, bytes `1B 74 02`.

La selección aparece exactamente una vez, en offset cero y antes de texto/tamaños. `EncoderFallback.ExceptionFallback` impide sustituciones silenciosas. La impresión física confirmó esta correspondencia; queda como baseline fijo para el equipo probado.

## Envío único

- PrintRequestId: `957c1076-cd21-4ceb-b609-63b437a6c643`.
- REQUESTED: `2026-09-11T20:18:37.9936825+00:00`.
- SUBMITTED: `2026-09-11T20:18:38.1092258+00:00`.
- Queue: `POS-80` por Winspool RAW/USB001.
- JobId: `5`.
- Estado técnico: `SUBMITTED`.
- Bytes: `889`.
- SHA-256: `83CC7BA9E0339E608891C2B3908021A847053B40B5A3F722C4F4AF44F101321D`.
- Bytes exactos: `docs/evidence/phase1g-block-d-payload.hex`.

Mapeo .NET PC850 enviado:

| Texto | Hex |
| --- | --- |
| `á é í ó ú` | `A0 20 82 20 A1 20 A2 20 A3` |
| `Á É Í Ó Ú` | `B5 20 90 20 D6 20 E0 20 E9` |
| `ü Ü` | `81 20 9A` |
| `ñ Ñ` | `A4 20 A5` |
| `¿` | `A8` |
| `¡` | `AD` |
| `$` | `24` |

Se preservaron exactamente un encabezado 2×2, un TOTAL doble alto, dos restauraciones normales, cuatro LF y un `GS V 01` final. No existe `ESC p` ni pulso de cajón.

## Contenido imprimible exacto

```text
    PRUEBA CASA MIGA
                 PRUEBA ESPANOL
                   á é í ó ú
                   Á É Í Ó Ú
  ü Ü | ñ Ñ | ¿Como esta? | ¡Gracias! | $19.00
------------------------------------------------
Folio: TEST-003
Fecha: 11/09/2026 20:18:37 +00:00
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
  PANADERIA CASA MIGA - Gracias por su compra
```

## Pruebas y build

- ReceiptTests focalizadas: 18 aprobadas, 0 fallidas, 0 omitidas.
- Suite completa: 188 aprobadas, 0 fallidas, 0 omitidas.
- Build Release: 0 errores, 0 advertencias.

Las pruebas cubren provider de páginas, encoding válido, representación de todos los caracteres, selección única antes del texto, parejas encoding/tabla estrictas, determinismo, tamaños preservados, un corte final, ausencia de cajón, bloqueo de reenvío y ninguna mutación comercial.

## Validación humana completada

La fotografía preservada en `docs/evidence/phase1g-block-d-physical.png` y la confirmación explícita del usuario registran:

1. `á é í ó ú`: correctos.
2. `ü` y `Ü`: correctos.
3. `ñ` y `Ñ`: correctos.
4. `¿` y `¡`: correctos.
5. `$`: correcto.
6. Texto general: legible.
7. Tamaños: iguales al baseline aprobado.
8. Alineación: conservada.
9. Corte: automático, correcto y parcial.
10. Caracteres basura: ninguno.

El baseline queda fijado en .NET `ibm850`/CP850, `ESC t 2` y `GS V 1`. No se autoriza probar otra tabla ni variar el corte sin una causa técnica nueva y explícita.

Permanecen pendientes el logo oficial, reimpresión UI, cajón y LAN. El Bloque E se detuvo antes de implementar porque el repositorio no contiene un asset identificado como logotipo oficial aprobado.

Se recomienda **SOL** para el siguiente bloque si los caracteres son correctos o requieren un ajuste acotado. Usar **ASTRA** sólo si existe incompatibilidad real entre tabla 2, bytes PC850, interpretación Qian, driver o Winspool.
