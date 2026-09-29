# Impresión — identidad Variante 04 (R2)

El renderer del perfil aprobado de 48 columnas / 576 puntos usa siempre el recurso embebido `CasaMiga.Variant04.Ticket.png`, tanto en original como en reimpresión. No carga archivos arbitrarios indicados por el perfil. El campo histórico MonochromeLogoPath se conserva en snapshots inmutables, pero ya no selecciona la identidad gráfica en esa impresora. Un snapshot antiguo que contenga Opcion07 se imprime con el activo actual, sin reescribir snapshot, hash, Sale, Payment o inventario.

Fuente: `assets/brand/Variant04/CasaMiga_LOGO_OFICIAL_VECTOR.svg`, hash `7E67467CCF778EF40C40E8D15716AD550E1DA3A11C7D592D57578531A8228423`. Derivación reproducible con `scripts/prepare-ticket-logo.py` a partir del PNG HD transparente ya verificado contra ese maestro (3080 × 1786; hash `A1858372624CDDC87091A3DCFCBC5FA5E21F8C2621C5D5C6E2ACEE6D363B06CC`). No dibujo, fuentes ni generación de imágenes.

Salida: `CasaMiga_Ticket_Termico_576px_1bit.png`, 576 × 341, grayscale 1 bit. Marca escalada a 560 × 325 y centrada con 8 puntos de margen por lado. Error de aspecto por redondeo a puntos enteros inferior a 0.1%. Hash `288A871404AFDEAE88A8FC9F1970BF4E6AE5D7AFBCA2E2774603EE65FD16A4DE`.

El decodificador comprueba hash, tamaño, profundidad y formato; transmite los píxeles sin remuestreo. GS v 0 declara 72 bytes/fila y 341 filas. CP850/ESC t 2, un corte parcial al final, request/event, reintentos y REIMPRESIÓN conservan sus contratos. La validación del probe omite sólo el bloque raster conocido y delimitado: los bytes de imagen no se interpretan como órdenes ESC/POS. No se modifica driver ni spooler.

Los perfiles genéricos de anchos distintos de 48 columnas conservan el encabezado textual previo; no representan la Qian aprobada. El smoke físico se prepara exclusivamente con 48 columnas / 576 puntos. El recurso Opción 07 no se incorpora al ensamblado. Los archivos históricos de 1G permanecen como evidencia, no como activos del pipeline.

Evidencia sin hardware: `evidence/phase1k2c-r2-checkout/` contiene payloads originales, reprint, hashes y PNGs interpretados desde esos bytes. Ambos rasters coinciden píxel a píxel con el PNG oficial. La tipografía del PNG de revisión es una aproximación de pantalla, no una certificación de la tipografía de la impresora.

Prueba humana pendiente: una venta sintética con Cobrar + imprimir ticket y después una reimpresión desde Historial. Revisar Variante 04, centrado, nitidez, folio/importe, marca REIMPRESIÓN, ausencia de duplicación y corte. No se declara éxito físico por la aceptación del spooler ni por los PNGs.
