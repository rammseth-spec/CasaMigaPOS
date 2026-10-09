# CASA MIGA POS — FASE 1K.4E-R1 — REPORTE TÉCNICO

Fecha: 08/10/2026. Estado: corrección técnica completada; smoke físico humano pendiente.

## Diagnóstico

No existía una ruta antigua, otro renderer ni una configuración desactualizada en la fixture. Original y reimpresión recorren `TextReceiptRenderer` y después `EscPosReceiptRenderer`; la reimpresión sólo agrega su marca. La discrepancia provenía de la composición implementada en 1K.4E: `Business.Contact` se agregaba en la cabecera, no había separador entre TOTAL y pago y se emitían sólo cuatro LF antes del corte. Además, las pruebas comprobaban contenido/comandos, pero no toda la secuencia física. La fixture sí usó la configuración actual.

## Resultado funcional y visual

1. Modelo usado: GPT-6.
2. HEAD inicial: `180134c96f8aeb57ea2e0bc20466a38f31499abc`.
3. Causa exacta: orden del renderer real y cobertura insuficiente del orden físico, no hardware ni ruta alterna.
4. Ruta original: `TextReceiptRenderer.Render(..., Original)` → `EscPosReceiptRenderer.Render`.
5. Ruta reimpresión: el mismo par con `ReceiptCopyKind.Reprint`.
6. Renderer compartido: confirmado por código y pruebas sobre ambos tipos.
7. Orden anterior: encabezado/logo/dirección/redes/marca/venta/productos/totales/pago/cierre/corazón/corte.
8. Orden R1: Hecho en/logo/dirección/separador/marca opcional/venta/fecha/cajero/separador/productos/separador/Subtotal/TOTAL/separador/pago/recibido/cambio/separador/Síguenos/iconos/cuentas/agradecimiento/corazón/feed/corte.
9. «Hecho en:» es texto normal y centrado antes del logo.
10. Logo anterior: marca de 490 puntos en lienzo 576×300.
11. Logo R1: marca de 446 puntos en lienzo 576×275, reducción de 9.0%.
12. Proporción: escala uniforme desde el derivado oficial Variante 04, sin recorte, estiramiento o redibujo.
13. Dirección: cuatro líneas; `C.P. 38000 · Celaya, Gto.` cabe en una línea.
14. Teléfono null/vacío: no imprime línea ni hueco.
15. REIMPRESIÓN: después del primer separador y antes de Venta; el original no deja hueco.
16. Venta: `Venta #000017`, sin GUID.
17. Fecha: `dd/MM/yyyy · HH:mm` local.
18. Cajero: nombre humano.
19. Productos: snapshot histórico, nombre y línea de cantidad/precio/importe.
20. Nombre largo: ancho de 31 columnas y separación entre productos; el caso aprobado queda en dos líneas.
21. Subtotal: tamaño normal y alineado.
22. TOTAL: doble altura y restauración inmediata.
23. Pago: `Pago: Efectivo`, separado de TOTAL.
24. Recibido: importe alineado.
25. Cambio: importe alineado.
26. Redes: movidas al pie. Por instrucción posterior del usuario, se imprime «Síguenos en:», un raster 576×40 con los logotipos de Facebook e Instagram alineados sobre sus cuentas y sólo `Casa Miga   @casamiga126`; no se imprimen los nombres de las plataformas.
27. Agradecimiento: texto CP850 centrado después de las cuentas.
28. Corazón: raster 1-bit centrado y separado.
29. Feed final: seis LF; sin backfeed.
30. Corte: un GS V 1 parcial al final.
31. Codificación: `ibm850`/CP850 conservada.
32. Tabla: una selección ESC t 2.
33. Cajón: `openCashDrawer=false`, cero ESC p.
34. Orden probado: prueba explícita para original y reimpresión, incluido raster social.
35. Focales: 30/30, sin fallas ni omisiones.
36. Suite total: 763/763 (576 generales + 187 WPF/Presentation), sin fallas ni omisiones.
37. Release: 0 errores, 0 advertencias.
38. `git diff --check`: correcto antes del commit.
39. Base protegida: schema 16 y SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, intacta.
40. Migraciones: 017 intacta; no existe 018.
41. Impresión automática: ninguna; los payloads se generaron fuera de línea.
42. EXE: `src/CasaMiga.Presentation/bin/Phase1K4E-R1/CasaMiga.Presentation.exe`.
43. Launcher: `work/Abrir-Casa-Miga-1K4E-R1.cmd`.
44. DataDirectory: `work/phase1k4e-r1-smoke-20261008`.
45. Título: `Casa Miga POS · Caja · DESARROLLO · 1K.4E-R1`.
46. Commit previsto: `fix: align physical receipt layout for phase 1K.4E`.
47. HEAD final: corresponde al commit local que contiene este reporte y se entrega en la conversación.
48. Git: debe quedar limpio después del commit.
49. Publicación: sin push ni PR.
50. Prueba física: pendiente de ejecución y aprobación humana.
51. Aceptación física: comprobar orden, nitidez/tamaño de Variante 04, iconos sociales reconocibles, cuentas, nombre largo, TOTAL, pago, agradecimiento/corazón, espacio al corte, marca de reimpresión, ausencia de cajón y ausencia de mutaciones.

## Evidencia aislada

La fixture `work/phase1k4e-r1-smoke-20261008` contiene la venta sintética #000017 por $72.00, recibido $100.00 y cambio $28.00. Original: 23,853 bytes, SHA-256 `50DE3C3831D9F11B1A0CA41F6993123A45B49EE7784AD300EC989BBAA5C1D0EB`. Reimpresión: 23,888 bytes, SHA-256 `58A496A312948C5D00BFEC055BF9F8AD1CEA051255DAD8067DA2FF552DDC3BFA`. Los conteos protegidos permanecieron iguales antes/después y no se invocó transporte de impresión.
