# CASA MIGA POS — FASE 1K.4E — REPORTE TÉCNICO

Fecha: 08/10/2026. Estado: implementación técnica completada; smoke físico humano pendiente.

## Resultado

El pipeline existente se conservó: snapshot inmutable al confirmar la venta, composición de texto, render ESC/POS, request/event y transporte RAW. No cambió Sale, Payment, Cash, Domain, SQLite, spooler, encoding o arquitectura de impresión. La reimpresión conserva permisos y usa el snapshot histórico sin reconstruir productos desde el catálogo.

El ticket usa «Hecho en:», logo oficial Variante 04, datos oficiales, folio derivado de la secuencia persistida, fecha local `dd/MM/yyyy · HH:mm`, cajero humano, productos históricos, Subtotal, TOTAL en doble altura con restauración inmediata, `Pago: Efectivo`, Recibido y Cambio. Omite GUID, terminal técnica, `CASH` y `Aplicado`. La reimpresión agrega una marca centrada inequívoca antes de los datos de venta.

Dirección: Insurgentes 116; Esquina con Albino García; Barrio de Santiaguito; C.P. 38000; Celaya, Gto. Redes: Facebook: Casa Miga e Instagram: @casamiga126. Cierre: Gracias por su compra y corazón raster 1-bit. El teléfono oficial sigue pendiente; `phone` null, vacío o whitespace no imprime línea ni espacio extra, y un valor futuro se imprime una sola vez sin migración.

## Identidad y bytes

Logo anterior R2: marca 560 × 325 dentro de lienzo 576 × 341. Logo 1K.4E: marca de 490 puntos de ancho dentro de lienzo 576 × 300, reducción de 12.5%, sin deformación. Archivo `assets/brand/Variant04/CasaMiga_Ticket_Termico_576px_1bit.png`, SHA-256 `37927561B9F1BE2FD839C769CE845C62A6A3573653D9533239A1BC7F7175BCDD`.

Perfil Qian: Qian QOP-T80UL-RI-02, cola `POS-80`, USB001 ya validado, 48 columnas, `ibm850`, ESC t 2, RAW, cuatro LF finales y corte parcial GS V 1. `openCashDrawer=false`; los payloads original y reimpresión contienen cero ESC p. Logo y corazón son los únicos raster; el cuerpo permanece CP850. Evidencia: `docs/evidence/phase1k4e-byte-audit.json`.

## Fixture y seguridad

`tools/CasaMiga.Phase1K4ESmoke` exige el hash protegido, lock exclusivo, ausencia de WAL, destino nuevo y contraseña sólo por variable de proceso. La copia `work/phase1k4e-smoke-20261008` migra de schema 16 a 17, valida `catalog-admin`, conserva caja abierta y agrega una venta sintética #000017 de $72.00, recibido $100.00, cambio $28.00 con snapshot inmutable. Genera original/reimpresión fuera de línea, sin usar checkout, transporte ni cola. Inventario, producción y request/event de impresión conservan sus conteos.

Base protegida inicial/final: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, schema 16, byte-idéntica. Fixture: schema 17. Migración 017 intacta; no existe 018.

## Validación y entrega

Focales de ticket: 28/28, 0 fallidas, 0 omitidas. `scripts/verify.ps1`: 761/761 (574 generales + 187 WPF/Presentation), 0 fallidas y 0 omitidas; Release de solución con 0 errores y 0 advertencias. Build identificado: `src/CasaMiga.Presentation/bin/Phase1K4E/`; launcher `work/Abrir-Casa-Miga-1K4E.cmd`; DataDirectory `work/phase1k4e-smoke-20261008`; título `Casa Miga POS · Caja · DESARROLLO · 1K.4E`. Hashes y trazabilidad están en `docs/evidence/phase1k4e-verification.md`.

El smoke físico debe comprobar logo, dirección, ausencia de teléfono, productos, TOTAL, Efectivo/Recibido/Cambio, redes, cierre, corazón, corte, marca REIMPRESIÓN, ausencia de apertura de cajón y ausencia de mutaciones comerciales. No se declara éxito físico hasta que el usuario imprima voluntariamente. No se inició 1K.4F y no se hizo push.
