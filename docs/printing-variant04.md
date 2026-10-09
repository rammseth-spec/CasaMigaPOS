# Impresión — identidad Variante 04 (1K.4E-R1)

El renderer del perfil aprobado de 48 columnas / 576 puntos usa siempre el recurso embebido `CasaMiga.Variant04.Ticket.png`, tanto en original como en reimpresión. No carga archivos arbitrarios indicados por el perfil. El campo histórico MonochromeLogoPath se conserva en snapshots inmutables, pero ya no selecciona la identidad gráfica en esa impresora. Un snapshot antiguo que contenga Opcion07 se imprime con el activo actual, sin reescribir snapshot, hash, Sale, Payment o inventario.

Fuente: `assets/brand/Variant04/CasaMiga_LOGO_OFICIAL_VECTOR.svg`, hash `7E67467CCF778EF40C40E8D15716AD550E1DA3A11C7D592D57578531A8228423`. Derivación reproducible con `scripts/prepare-ticket-logo.py` a partir del PNG HD transparente ya verificado contra ese maestro (3080 × 1786; hash `A1858372624CDDC87091A3DCFCBC5FA5E21F8C2621C5D5C6E2ACEE6D363B06CC`). No dibujo, fuentes ni generación de imágenes.

Salida R1: `CasaMiga_Ticket_Termico_576px_1bit.png`, lienzo 576 × 275, grayscale 1 bit. Marca escalada a 446 puntos de ancho y centrada con 65 puntos de margen por lado. Es 9.0% menor que la marca impresa en 1K.4E (490 puntos) y conserva proporción y geometría. Hash `EA3258058C9060D85FBAD830C845EE5BD52813F7F9D29BD6EA43FFCD2395AFFD`.

El decodificador comprueba hash, tamaño, profundidad y formato; transmite los píxeles sin remuestreo. El logo usa GS v 0 con 72 bytes/fila y 275 filas; el bloque social usa 72 bytes/fila y 40 filas. CP850/ESC t 2, un corte parcial al final, request/event, reintentos y REIMPRESIÓN conservan sus contratos. La validación del probe omite sólo los bloques raster conocidos y delimitados del logo, los logotipos sociales y el corazón: los bytes de imagen no se interpretan como órdenes ESC/POS. No se modifica driver ni spooler.

El ticket R1 imprime «Hecho en:» centrado antes del logo, dirección oficial, folio humano, fecha local, cajero y etiquetas financieras humanas. Las redes se movieron al pie: la instrucción posterior del usuario reemplazó las palabras Facebook/Instagram por un raster 1-bit determinista con ambos logotipos, precedido por «Síguenos en:» y seguido por `Casa Miga   @casamiga126`. Omite el identificador técnico de terminal y cualquier teléfono null/vacío. `phone` permanece opcional en `printing.json`: cuando exista el número oficial bastará configurarlo. Logo oficial, logotipos sociales y corazón son raster; el resto permanece en CP850.

Los perfiles genéricos de anchos distintos de 48 columnas conservan el encabezado textual previo; no representan la Qian aprobada. El smoke físico se prepara exclusivamente con 48 columnas / 576 puntos. El recurso Opción 07 no se incorpora al ensamblado. Los archivos históricos de 1G permanecen como evidencia, no como activos del pipeline.

Evidencia sin hardware: `evidence/phase1k4e-r1-byte-audit.json` registra los comandos y hashes de payload original/reimpresión generados fuera de línea. Los archivos locales correspondientes quedan en la fixture ignorada por Git. No se enviaron a la cola.

Prueba humana pendiente: una venta sintética con Cobrar + imprimir ticket y después una reimpresión desde Historial. Revisar Variante 04, centrado, nitidez, folio/importe, marca REIMPRESIÓN, ausencia de duplicación y corte. No se declara éxito físico por la aceptación del spooler ni por los PNGs.
