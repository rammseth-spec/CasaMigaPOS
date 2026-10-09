# Impresión — identidad Variante 04 (1K.4E)

El renderer del perfil aprobado de 48 columnas / 576 puntos usa siempre el recurso embebido `CasaMiga.Variant04.Ticket.png`, tanto en original como en reimpresión. No carga archivos arbitrarios indicados por el perfil. El campo histórico MonochromeLogoPath se conserva en snapshots inmutables, pero ya no selecciona la identidad gráfica en esa impresora. Un snapshot antiguo que contenga Opcion07 se imprime con el activo actual, sin reescribir snapshot, hash, Sale, Payment o inventario.

Fuente: `assets/brand/Variant04/CasaMiga_LOGO_OFICIAL_VECTOR.svg`, hash `7E67467CCF778EF40C40E8D15716AD550E1DA3A11C7D592D57578531A8228423`. Derivación reproducible con `scripts/prepare-ticket-logo.py` a partir del PNG HD transparente ya verificado contra ese maestro (3080 × 1786; hash `A1858372624CDDC87091A3DCFCBC5FA5E21F8C2621C5D5C6E2ACEE6D363B06CC`). No dibujo, fuentes ni generación de imágenes.

Salida: `CasaMiga_Ticket_Termico_576px_1bit.png`, lienzo 576 × 300, grayscale 1 bit. Marca escalada a 490 puntos de ancho y centrada con 43 puntos de margen por lado. Es 12.5% menor que la marca R2 de 560 puntos y conserva proporción y geometría. Hash `37927561B9F1BE2FD839C769CE845C62A6A3573653D9533239A1BC7F7175BCDD`.

El decodificador comprueba hash, tamaño, profundidad y formato; transmite los píxeles sin remuestreo. GS v 0 declara 72 bytes/fila y 300 filas. CP850/ESC t 2, un corte parcial al final, request/event, reintentos y REIMPRESIÓN conservan sus contratos. La validación del probe omite sólo los bloques raster conocidos y delimitados del logo y el corazón: los bytes de imagen no se interpretan como órdenes ESC/POS. No se modifica driver ni spooler.

El ticket 1K.4E agrega «Hecho en:», dirección oficial, folio humano, fecha local, cajero, etiquetas financieras humanas, redes sociales y corazón raster. Omite el identificador técnico de terminal y cualquier teléfono null/vacío. `phone` permanece opcional en `printing.json`: cuando exista el número oficial bastará configurarlo. El cuerpo sigue siendo texto CP850; sólo logo y corazón son raster.

Los perfiles genéricos de anchos distintos de 48 columnas conservan el encabezado textual previo; no representan la Qian aprobada. El smoke físico se prepara exclusivamente con 48 columnas / 576 puntos. El recurso Opción 07 no se incorpora al ensamblado. Los archivos históricos de 1G permanecen como evidencia, no como activos del pipeline.

Evidencia sin hardware: `evidence/phase1k4e-byte-audit.json` registra los comandos y hashes de payload original/reimpresión generados fuera de línea. Los archivos locales correspondientes quedan en la fixture ignorada por Git. No se enviaron a la cola.

Prueba humana pendiente: una venta sintética con Cobrar + imprimir ticket y después una reimpresión desde Historial. Revisar Variante 04, centrado, nitidez, folio/importe, marca REIMPRESIÓN, ausencia de duplicación y corte. No se declara éxito físico por la aceptación del spooler ni por los PNGs.
