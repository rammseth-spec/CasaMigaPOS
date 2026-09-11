# FASE 1G Bloque E — Opción 07

Estado: implementación y envío único terminados; validación física humana pendiente. FASE 1G abierta, versión de desarrollo no lista para producción. Modelo: Astra.

HEAD inicial: d6ecc887ee9c766b7006a5ee21aa6f38222f0a05. El commit de este reporte identifica el checkpoint final; sin push.

## Fuente oficial

Leídos íntegramente CODEX_BRAND_REFERENCE.md, VALIDATION_REPORT.md, DEPRECATION_NOTICE.md, PROMPT_PARA_CODEX.txt, PACKAGE_MANIFEST.json y SHA256SUMS.txt. Diez hashes verificados. Único origen persistente: assets/brand/Opcion07/, copia intacta del ZIP Casa_Miga_Logo_Master_Opcion07_Codex_v1.zip.

Referencia maestra SHA-256: 178F8B03EDD649B451FCE9ABFA06CE1C5D893FB96E22AC5A9B93B8C3BEC06D47.
Ticket: assets/brand/Opcion07/assets/Casa_Miga_Logo_Oficial_Opcion07_Ticket_560px_1bit.png.
SHA-256: 06D4ADE3AE160967FCD21E958458C17113BEF3C4A8E03F15D63115530AE821C6.
PNG 560 × 262, grayscale 1-bit no interlazado. Bordes blancos y dos cortes blancos del pan comprobados por pruebas. No escalado, recorte, dithering ni edición del master.

No existían logos integrados: sólo Assets/README.md y cabecera textual temporal. El paquete anterior permanece únicamente en artifacts/brand-official-review, ignorado por Git y no referenciado por el producto. Ninguna referencia ejecutable antigua requirió migración. UI intacta; assets de UI preparados para uso futuro, sin Build Action WPF. El PNG térmico es EmbeddedResource de Infrastructure, nombre lógico CasaMiga.Opcion07.Ticket.png. La selección usa MonochromeLogoPath con identificador versionado Opcion07 + hash, conservado en el snapshot; no lee rutas arbitrarias.

## Raster y composición

Referencia Epson: https://download4.epson.biz/sec_pubs/pos/reference_en/escpos/gs_lv_0.html . GS v 0 es un comando heredado/obsoleto de Epson; su compatibilidad concreta con Qian se evalúa mediante este único envío, no se presupone por SUBMITTED.

Comandos: ESC t 2 (1B 74 02); ESC a 0 (1B 61 00); GS v 0 modo 0, encabezado 1D 76 30 00 48 00 06 01; GS ! 0 para restaurar tamaño; TOTAL con GS ! 1 y restauración GS ! 0; cuatro LF y GS V 1 final.

Bitmap original: 70 bytes/fila × 262 = 18340 bytes. Lienzo enviado: 72 bytes/fila × 262 = 18864 bytes; gráfico completo con encabezado: 18872 bytes. Ocho puntos blancos por lado centran los 560 puntos originales en 576, sin deformación. Bits MSB primero, filas de arriba abajo, 1 imprime negro. El validador trata el bloque gráfico como datos delimitados por longitud y exige correspondencia exacta con el asset aprobado, sin interpretar bytes gráficos como comandos.

Logo sustituye encabezado textual para evitar marca duplicada. Texto: folio TEST-003; fecha 11/09/2026 22:12:07 +00:00; cajero PRUEBA; terminal DESARROLLO; Producto prueba, 1 × $19.00; subtotal/total $19.00; CASH; efectivo $20.00; aplicado $19.00; cambio $1.00; Gracias por su compra. No datos comerciales inventados. PC850, 48 columnas, wrapping, importes a la derecha, TOTAL doble alto y corte parcial conservados. Sin cajón.

## Único envío

PrintRequestId: 3d8ad746-b867-4540-8e42-9eb21e7848f4. JobId: 6. Cola POS-80, USB001, driver POS-80 11.3.0.1. Estado SUBMITTED. Bytes: 19589. SHA-256 payload: E4B48FE42820B44B2A6ACCA9013EAB3579FA4502CFC0BB41BB1004BD5F6C95B8.

Evidencia: docs/evidence/phase1g-block-e-opcion07.jsonl, phase1g-block-e-opcion07.payload.bin y phase1g-block-e-printing.json. No se abrió base comercial ni se crearon Sale, Payment, caja, folio comercial o cambios en ExpectedCash. Mecanismo de un solo intento conservado.

## Verificación

21 pruebas focalizadas aprobadas. scripts/verify.ps1 con SDK local work/dotnet/dotnet.exe y feed local: 191 aprobadas, 0 fallidas, 0 omitidas; Release 0 errores y 0 advertencias. Se conservan las 188 anteriores. git diff --check correcto. El dotnet del PATH no contenía SDK; se utilizó el SDK 10.0.400 existente del proyecto.

Cambios de producto: OfficialReceiptLogo.cs nuevo; EscPosReceiptRenderer.cs; ControlledReceiptPrintTest.cs; CasaMiga.Infrastructure.csproj; OfficialLogoTests.cs nuevo; paquete assets/brand/Opcion07. Documentación: README, CURRENT_STATE, NEXT_STEPS, PHASE_1G_BLOCK_E_CHECKPOINT, PHASE_1G_BLOCK_E_REPORT, Assets/README y evidencia. Domain, Application, migraciones y UI funcional intactos.

## Checkpoint humano

Solicitar foto completa. Confirmar Opción 07 exacta; techo; pan pequeño centrado y dos cortes; ausencia de vapor; espaciado CASA ✦ MIGA; estrella entre palabras; PANADERÍA legible; ausencia de líneas laterales; centrado; tamaño; aspecto profesional; alineación del texto; TOTAL; corte parcial y espacio inferior. No segunda impresión antes de respuesta.

El logo integrado corresponde exclusivamente a la referencia maestra Casa Miga Opción 07: techo lineal simple, pan pequeño con dos cortes blancos, sin vapor, CASA ✦ MIGA con estrella de cuatro puntas entre las palabras y PANADERÍA debajo, sin líneas decorativas laterales.

Pendientes: validación física, datos comerciales definitivos, reimpresión desde Historial y futuros bloques autorizados. No se inició reimpresión UI, cajón, LAN, inventario, rediseño, fullscreen ni otra fase. Recomendación siguiente: SOL para recepción de evidencia/cierre documental; Astra sólo si surge un defecto real del raster o compatibilidad física.
