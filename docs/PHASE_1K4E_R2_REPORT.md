# CASA MIGA POS — FASE 1K.4E-R2 — REPORTE TÉCNICO

Fecha: 09/10/2026. Modelo usado: GPT-6. Baseline del bloque B: commit A `d8da59ceca8ca01aad223ad63eea1d35c9dbc1cc`. Estado: implementación y validación técnica completadas; prueba física humana pendiente.

## Contacto y composición

El número oficial se configura mediante `Business.Phone`; el renderer no contiene un teléfono fijo. La fixture usa `461 276 7429`. Cuando existe, `TextReceiptRenderer` produce dos líneas centradas: `Pedidos por WhatsApp` y `461 276 7429`. Si el campo es null, vacío o espacios, no produce texto, icono ni hueco.

El icono es el fallback autorizado de teléfono dentro de círculo. Es un raster local, determinista, monocromático 1-bit de 32×32 puntos, cuatro bytes por fila y comando total de 136 bytes. Se eligió porque no existe un activo maestro local de WhatsApp fiable para impresión térmica; no se recreó su logotipo, no se usó IA, fuente o emoji.

Orden en original y reimpresión: Hecho en → logo oficial → dirección → icono de pedidos → Pedidos por WhatsApp → 461 276 7429 → primer separador → marca REIMPRESIÓN sólo si aplica → Venta → fecha/cajero → productos → Subtotal/TOTAL → pago/recibido/cambio → redes → agradecimiento → corazón → seis LF → corte. Las pruebas verifican explícitamente las posiciones. Variante 04 conserva lienzo 576×275 y marca de 446 puntos; su recurso y geometría no cambiaron. Redes conservan `Síguenos en:`, raster y `Casa Miga   @casamiga126`.

## Bytes y evidencia fuera de línea

Fixture: `work/phase1k4f-r2-smoke-20261009`, schema 17. Original: 24,063 bytes, SHA-256 `0C32856DBC6D8F24B2612A63941F9AB68527FB67115C952650160E1DA3629B4A`. Reimpresión: 24,098 bytes, SHA-256 `47B73172C36A03B9231256C21B76A92233C19D399605E6172FB358D4F7B43699`.

Auditoría original: ESC t 2 = 1; logo Casa Miga = 1; icono pedidos = 1; raster social = 1; corazón = 1; TOTAL doble altura = 1 y restauración normal = 2; GS V 1 = 1; seis LF antes del corte; ESC p = 0. Configuración: POS-80, 48 columnas, ibm850, tabla 2, Partial y `openCashDrawer=false`. El transporte de impresión no fue invocado.

## Validación

- Focales ReceiptTests: 27/27, 0 fallidas/omitidas.
- Suite general: 577/577.
- Suite WPF/Presentation: 206/206.
- Total conjunto: 783/783, 0 fallidas, 0 omitidas.
- Release: 0 errores, 0 advertencias.
- Base protegida: SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, schema 16 e intacta.
- Fixture: schema 17; migración 018 no existe.
- Inventario, producción y solicitudes/eventos de impresión de la copia: conteos iguales antes/después de preparar payloads.
- Cajón: desactivado y cero ESC p.
- Push/PR: ninguno.

Commit local requerido: `fix: add ordering contact to receipt for phase 1K.4E`. La prueba física de original/reimpresión sigue pendiente y requiere acción humana explícita.
