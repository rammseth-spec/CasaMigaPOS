# CASA MIGA POS — REPORTE CORRECTIVO FASE 1K.2C-R2

Fecha: 28/09/2026. FASE 1K ABIERTA. R1 visual aprobado por el usuario; R2 requiere revisión humana propia. No se inicia Pedidos, migración 017 ni instalador.

## Baseline y Git

- HEAD inicial: `ad00cfec3e55ad95f0ab2adf127c29f18f25a58d`, limpio al comenzar.
- HEAD final y commit se consignan en la entrega final y en la copia local del reporte después de registrar este documento, evitando una referencia circular a su propio hash.
- Identidad repo-local verificada: Rammseth / rammstudioworkshop@gmail.com. Sin push ni creación de remoto.
- Schema 16. Migraciones intactas; SHA-256 de 016: `9223CAA06CD02FA32E874CC1516EE0C6DB798CB76E2FBE1BF267BAB9B695ED76`.

## Catálogo y assets

Se verificaron CSV/JSON equivalentes del paquete autorizado v2. Dataset nuevo aislado: **26 Products, 4 categorías, 26 PROVISIONAL, 0 REAL, 0 SIN FOTO**. No se pobló la instalación comercial. Importación por ProductPhotoService; mínimos, procedencia, conservación de originales y backup/restore de 1K.2B intactos.

Se reutiliza el mapeo R1 revisado visualmente antes de la nueva importación: 17 recortes del primer mosaico, 6 del segundo y 3 referencias individuales de bebidas. Son 26 assets de 512 × 512, sin nombres/precios impresos ni otras cards. Etiquetas comerciales de los envases de leche sí forman parte del producto fotografiado. No se utilizó IA generativa. Los recortes se preparan fuera de ProductPhoto Foundation.

Concha vainilla y chocolate tienen fuentes diferenciadas y hashes distintos. Se examinó la referencia individual del danés: contiene panes adyacentes y superpuestos; aislar el pan completo con crop rectangular incluiría fragmentos ajenos. Se conserva el danés limpio del mosaico, evitando inventar zonas o alterar su forma. Las bebidas usan sus referencias individuales.

Mapa y procedencia con SHA-256 de fuente y asset: `evidence/phase1k2c-r2-mapping.json`. Correspondencia comercial completa: `phase1k2c-provisional-map.md`. Contact sheet: `evidence/phase1k2c-r2-contact-sheet.png`. La verificación técnica de hashes/tamaño no constituye OCR: ausencia de rótulos ajenos revisada visualmente en la hoja de contacto.

## POS y administración

- Se conservan cards 184 × 276 DIP, foto 160, nombre hasta dos líneas, precio separado, scroll reiniciado, rail con iconos/tooltips y navegación expandible.
- Se corrigió la precedencia de Visibility del encabezado CONSULTA, que aún podía verse en el rail contraído.
- Carrito con acciones explícitas **Cobrar** y **Cobrar + imprimir ticket**. Ambas abren el mismo diálogo para capturar efectivo; Enter selecciona la acción elegida en el carrito. No se cobra al abrirlo.
- Catálogo: seleccionar producto → botón visible **Administrar foto** → sección **IMAGEN DEL PRODUCTO**, preview 240 × 240 y estado Sin foto/Provisional/Real.
- Agregar/Cambiar: JPEG/PNG → mover/redimensionar cuadro → vista previa del recorte en vivo → aceptar → ProductPhotoService. Se conserva el original y el ProductId.
- Cancelar el selector o crop retorna antes de llamar a importación; verificado por revisión del flujo. La interacción humana de Cancelar queda incluida en el smoke pendiente.
- Quitar foto exige confirmación Sí/No; mantiene Product e historial y refresca el fallback. La caché se invalida tras importación, reemplazo y retirada; POS carga la revisión vigente al volver a entrar.
- Cashier puede ver fotos y vender, pero Application deniega agregar, reemplazar, retirar y reparar fotos. No se altera esa autoridad.
- El crop redondea y limita coordenadas enteras para evitar exceder la imagen un píxel al redimensionar junto al borde; prueba de regresión incluida.

## Checkout, Historial y fallos

Se conserva exactamente CheckoutService. Cobrar confirma Sale/Items/Payment, auditoría y consumo cuando corresponda; no solicita impresión. Cobrar + imprimir espera el commit y usa ReceiptService. Ambas opciones comparten Busy y el diálogo evita aperturas duplicadas. Una confirmación ya completada tampoco acepta un segundo envío con otra opción de impresión.

Evidencia por servicios reales y transporte sólo a archivos: `evidence/phase1k2c-r2-checkout/checkout-evidence.json`.

| Caso | Sale | Historial | Impresión |
|---|---:|---:|---|
| Sin ticket | 1 | 1 | 0 requests en ese punto |
| Con ticket | 1 | 1 | Original posterior al commit |
| Reprint de la anterior | 0 nuevas | misma venta | Nueva solicitud REPRINT |
| Fallo sintético de impresión | 1 | 1 | FAILED; venta conservada |

Totales: 3 ventas, 3 pagos, 3 entradas en Historial, 3 requests (original, reprint y original fallido). Todos los envíos verificaron que la venta concreta ya era visible desde una conexión separada. No se enviaron trabajos físicos. El texto SUBMITTED/Job 1 en esta evidencia procede del transporte simulado, no del spooler Qian.

Ante fallo: «Venta cobrada correctamente. No fue posible imprimir el ticket. Puedes reimprimirlo desde Historial». No se repite checkout ni se restaura el carrito. Reprint preserva inventario, snapshot, venta, pago y total de caja. Pruebas adicionales verifican doble submit y snapshot con referencia gráfica antigua.

## Logo y tickets

Maestro oficial Variante 04 y manual cotejados visualmente. Derivado técnico desde el PNG HD trazable al SVG, sin redibujo ni tipografías: `assets/brand/Variant04/CasaMiga_Ticket_Termico_576px_1bit.png`.

- 576 × 341 px, grayscale 1-bit, 72 bytes por fila.
- Marca 560 × 325, márgenes laterales de 8 puntos, centrado; redondeo de proporción inferior a 0.1%.
- SHA-256: `288A871404AFDEAE88A8FC9F1970BF4E6AE5D7AFBCA2E2774603EE65FD16A4DE`.
- Original y reprint del perfil Qian aprobado de 48 columnas usan exactamente el mismo raster. Comparación píxel a píxel ejecutada.
- Opción 07 retirada del recurso embebido y del pipeline activo. Los archivos históricos permanecen sólo como evidencia; no se reescriben snapshots viejos.
- CP850 / ESC t 2, request/event, marca REIMPRESIÓN y corte parcial conservados. Driver/spooler intactos.
- Perfiles genéricos distintos de 48 columnas conservan su encabezado textual; esta entrega y el smoke Qian usan 48 columnas/576 puntos.
- Evidencia raster original/reprint y payloads reales guardados. El preview de texto usa una fuente de pantalla; no certifica la salida física.

Ver `printing-variant04.md` y `scripts/prepare-ticket-logo.py` para trazabilidad y reproducción.

## Verificación y revisión humana

La evidencia final de suite, Release y diff se registra en `evidence/phase1k2c-r2-verification.txt`. Pruebas focalizadas: 26 de impresión y 28 de fotos/Presentation, correctas. Suite completa: **641/641** (536 generales + 105 fotos/Presentation), cero fallidas/omitidas. Release: **0 errores, 0 advertencias**. `git diff --check` correcto. La suite completa cubre ventas, caja, inventario, recetas, producción, ProductPhoto, backup/restore e impresión.

Smoke renderizado con WPF real y dataset aislado: POS compacto/expandido, 1024 × 768, catálogo, administrador de foto y crop con preview. 26 fotos cargadas. Capturas bajo `evidence/phase1k2c-r2-ui/`, revisadas visualmente por el agente; no equivalen a aceptación humana.

La aplicación anterior mantenía bloqueados los DLL Release; se solicitó cerrarla. La verificación usa `scripts/verify.ps1 -OutputPath bin/R2/`: mismo Release y suites, salida aislada. El nuevo parámetro sólo cambia el directorio de build/test; sin parámetro conserva el comportamiento anterior.

Dataset de revisión: `work/phase1k2c-r2-smoke-20260928`. Contiene las tres ventas sintéticas descritas y la caja abierta para revisión. Credenciales entregadas en conversación, no incorporadas a archivos versionados. Cola POS-80 detectada; disponibilidad física y aprobación humana pendientes. No afirmar ticket físico aprobado.

## Pedidos futuros, límites y próximo paso

Requisitos formales en `orders-future-requirements.md`: entidad persistente Pedido anterior al cobro, folio, tienda/cliente, referencia, productos/cantidades, fechas, nota, estados/auditoría; vínculo OrderId↔SaleId al cobrar mediante Checkout. Pedidos diarios/recurrentes y plantillas habituales registrados. Crear Pedido no crea Sale/Payment ni aumenta ExpectedCash. Reservas de inventario por definir.

Pregunta pendiente: ¿alguna tienda recibe pan y paga otro día? Si sí, evaluar crédito/cuentas por cobrar; no simularlo dejando un Pedido sin cobrar indefinidamente. Futuro ticket de pedido pagado con referencia y Variante 04; comprobante previo PEDIDO — NO PAGADO sin afectar Caja.

Riesgos/pendientes: fotos provisionales deben reemplazarse por reales; falta aceptación humana R2; falta una prueba física original + reprint y validación de corte/nitidez en Qian; Windows 10/LTSC y calidad con fotos reales no certificados.

Próximo paso: revisión humana del catálogo, administración/crop, cobro sin ticket e Historial; después una venta sintética con ticket y su reimpresión si el usuario está disponible. Modelo recomendado Astra para el cierre de impresión y el futuro diseño persistente de Pedidos. Detenerse después de la entrega. FASE 1K permanece abierta.
