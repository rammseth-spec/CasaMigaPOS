# Punto de venta en la web (Vender)

Decisión del usuario (08/10/2026): **POS web completo**, para vender y cobrar desde Mac, iPad o celular, no sólo consultar. Se construye por fases.

## Copia de la caja de escritorio (09/10/2026)

Pedido del usuario: «una copia tal cual de POS en Web» — primero **el mismo diseño** de la caja (rediseño A de QAv1) y después una web **independiente** de la caja. Se mantienen HTML/CSS/JavaScript y TypeScript (React o React Native sólo si hace falta). Lo que cambió:

| Caja de escritorio | Web |
|---|---|
| Riel blanco de 96 px: Venta, Caja, Charolas · Ventas, Reportes · Catálogo, Inventario, Recetas, Producción, Usuarios, Nube · Salir | Igual (mismos íconos, nombres, grupos y orden). En celular, menú desplegable. **Resumen** pasó a ser la primera pestaña de Reportes y **Cajas/Respaldos** viven en **Nube**; las direcciones viejas (`#/resumen`, `#/cajas`, `#/respaldos`) redirigen. |
| Barra superior: título y subtítulo, «● Caja abierta», «● Nube: …», ficha del usuario (iniciales, rol · terminal) y pantalla completa | Igual; «Nube» indica si la web leyó la nube y al tocarla actualiza. |
| **Punto de venta** (PosView): buscador con lupa + «Buscar», píldoras de categoría, tarjetas de 236 px con foto y contador, 50 por página con Anterior/Siguiente, «Usuario · Terminal»; a la derecha el **Ticket** con «Limpiar», renglones con foto, − cantidad + y quitar, Subtotal, Total grande, **Cobrar $…** y **Cobrar + imprimir ticket** | Igual. Con la caja cerrada (o ya contada) Venta manda a Caja, como en escritorio. En tableta vertical y celular el ticket se abre desde la barra de abajo. |
| **Cobro** (CheckoutDialog): total grande, forma de pago segmentada, efectivo recibido y cambio, referencia, instrucciones; Volver al POS · Cobrar · Cobrar + imprimir ticket; resultado «Venta confirmada / Folio / Total / Recibido / Cambio» | Igual (sin los atajos de billetes de antes). La impresión es la del navegador. |
| **Tu caja**: CAJA ABIERTA/CERRADA, punto de color, «Abierta por / Hora de apertura» con importes ocultos hasta el conteo ciego, Abrir caja / Cerrar caja (rojo); **Movimientos de efectivo** (Gasto/Entrada/Retiro segmentado, categorías, importe, nota, «En este turno»); **Abrir caja**, **Conteo de caja** y **Resultado de caja** (Fondo, Gastos, Esperado, Contado, «SOBRANTE/FALTANTE», nota de discrepancia obligatoria, Confirmar cierre) | Igual. Sólo en la web: elegir o crear la caja web del dispositivo, «Cambiar de caja» y «Ventas de este turno» (tickets, reimpresión, cancelaciones y devoluciones). Para encargados, debajo: totales del periodo, **Cierres anteriores** de todas las cajas y gastos/retiros/entradas. |
| **Historial de ventas** (SalesHistoryView): folio, días, Hoy/Ayer/Sesión actual, detalle con folio interno, reimpresión, cancelar/devolver | Igual; además forma de pago y búsqueda por producto. |
| **Reportes** (ReportsView): Resumen, Comparativo, Ventas por hora, Control de caja, Producción sugerida, Exportar CSV | Igual, con los mismos cálculos (redondeo bancario, canceladas fuera, pronóstico +10 %). Ganancia es pestaña extra. |
| **Catálogo** (CatalogView + PhotoCropDialog): pestañas Productos/Categorías, tabla con búsqueda, filtro, inactivos y 50 por página; Nuevo producto, Administrar foto, Editar, Activar/desactivar; editor con «IMAGEN DEL PRODUCTO» (origen real/provisional, Agregar/Cambiar con recorte, Quitar foto) y «+ Nueva categoría» sin salir del producto | Igual. La foto se recorta en el navegador (JPEG ≤1024 px) y la caja la procesa como una importada; la categoría nueva se puede usar antes de que la caja la aplique (va antes en la fila). |
| **Inventario** (InventoryView): consumo automático por ventas, búsqueda con Tipo/Estado, tabla de 50, detalle con presentaciones, asociaciones e historial; editores de artículo, operación con vista previa, presentación y asociación | Igual. Todo se manda a la caja como cambio (`inventory.*`) y la existencia cambia cuando la caja lo sube. Extra de la web: Costo de referencia y «Enviado a la caja». |
| **Recetas** (RecipeView): pestañas Recetas / Perfiles de producto, versiones (Borrador, Publicada, Activa) con Crear nueva versión, Editar borrador, Publicar y Activar; editor con rendimiento, ingredientes directos, grupos de alternativas por preferencia, preparaciones publicadas y política de cantidad | Igual. Se manda a la caja (`recipe.*`): la caja numera la versión, revisa artículos y preparaciones y rechaza una revisión vieja como en la pantalla. Una receta o perfil recién mandado ya acepta su primer borrador. |

Pendiente de esta copia: Producción, Charolas y Usuarios con todas sus acciones (ver [PLAN_COMPLETAR.md](PLAN_COMPLETAR.md)) y luego la web independiente.

## Fase 1 (lista)

| Qué | Cómo |
|---|---|
| **Caja web** | El administrador la crea en **Caja → + Nueva caja web** (antes en Vender). Cada una tiene su turno, fondo y folios; su número sigue al mayor de la sucursal (`CAJA-WEB-02` → folios `C02-000001…`), así no se confunde con la caja de escritorio (`C01`). Cada dispositivo elige su caja y la recuerda. |
| **Abrir turno** | Fondo inicial (permiso `cash.open`: cajero, encargado, administrador). Un turno abierto por caja. |
| **Vender** | Igual que en la app: buscador, categorías, cuadrícula con fotos, contador por pan, ticket con − / + y quitar, total. En celular y tableta vertical el ticket se abre desde la barra de abajo. |
| **Cobrar** | Efectivo recibido y cambio (los atajos de billetes se quitaron al copiar la caja, 09/10/2026); tarjeta o transferencia por el total exacto con referencia opcional (máx. 40). |
| **Ticket** | En pantalla y **Imprimir ticket** (impresión del navegador, 72 mm): folio, fecha, cajero, caja, partidas, total, pago y cambio. |
| **Corte** | **Corte de caja** (permiso `cash.close`; ajena con `cash.close.other`): conteo ciego, luego esperado / contado / diferencia y **Cerrar turno**. Con faltante o sobrante pide una **nota** (1–500), igual que la caja de escritorio. Después del conteo ya no entran ventas. |
| **Tickets** | Botón **Tickets**: ventas del turno abierto; con un folio (`C02-000015`, o sólo `15` para esta caja) se busca cualquier venta de la sucursal. Cada ticket muestra sus devoluciones. |
| **Reimprimir** | Desde el ticket, con «*** REIMPRESIÓN ***» y «COPIA» (permiso `sales.receipt.reprint`: encargado y administrador, como en la app). |
| **Cancelar o devolver** | Permiso `sales.refund` (encargado y administrador). **Cancelar venta**: completa, del mismo turno y sin devoluciones previas. **Devolver productos**: piezas por partida hasta lo que queda. Motivo obligatorio (3 a 200). La venta original no cambia; se registra la compensación con su bitácora y la app dice qué hacer con el dinero: efectivo sale de la caja (si alcanza el esperado); tarjeta o transferencia se reembolsan fuera de la caja. Las ventas de la caja de escritorio se devuelven en esa caja. |
| **Gasto, entrada o retiro** | Permiso `cash.movement` (cajero, encargado, administrador); **retiro** sólo con `cash.withdrawal` (encargado y administrador). Gasto con categoría (Insumos, Gas, Limpieza, Transporte, Mantenimiento, Otro; «Otro» pide nota). No se saca más efectivo del esperado. Lista «En este turno». Inmutable: un error se corrige con el movimiento contrario. |
| **Quién entra** | Cajero: **Vender** y Catálogo (no ve Caja: muestra el esperado del turno y el conteo es ciego). Encargado y administrador: todo el panel más Vender. |

## Reglas (las mismas de la caja)

- Precio y nombre salen del **catálogo vigente en la nube**, nunca del navegador; sólo productos y categorías activos de la sucursal de la caja. Si un precio cambió después de armar el ticket, el cobro se rechaza con aviso y el ticket se actualiza (nunca se cobra un total distinto al que vio el cajero).
- Partidas únicas, de 1 a 999 piezas, hasta 100 panes distintos; total hasta $1,000,000.
- Efectivo: lo recibido debe alcanzar y no lleva referencia. Tarjeta y transferencia: se cobra exacto.
- **Cada cobro, devolución y movimiento tiene un identificador** (migración de la nube 005): si se cae la red y se vuelve a tocar el botón, devuelve lo ya registrado; nunca lo duplica. Mismo identificador con otro contenido se rechaza.
- Devoluciones y movimientos sólo con el turno abierto y **antes del conteo**, igual que en la app.
- **Una sola sentencia por operación** (`cloud/lib/pos.ts`): actualiza la fila de la caja (`web_till`: consecutivos y turno; sirve de candado y de condición) y escribe el registro inmutable (`sync_event`) y el estado (`entity_state`). Si el turno ya se contó o cerró, no se escribe nada. Dos dispositivos en la misma caja se forman en fila: los folios no se repiten.
- Esperado = fondo + ventas en efectivo (aplicado, sin cambio) − devoluciones de ventas en efectivo + entradas − gastos y retiros (como `CashClosingService`).
- Las ventas, turnos, conteos y la bitácora se guardan **con los mismos formatos que sube la caja de escritorio**, así Resumen, Ventas, Reportes, Ganancia y Caja las cuentan sin cambios. La copia nocturna de la nube las respalda (Respaldos no muestra las cajas web porque no tienen base propia).

Base: `cloud/db/004_web_pos.sql` (`terminal.kind`, `web_till`, `web_operation`). Rutas: `GET /api/web/pos`, `POST /api/web/pos-till | pos-open | pos-sale | pos-count | pos-close`. Pruebas: `cloud/test/pos.test.ts`.

## Fase 3: inventario, producción y fotos desde la web

Lo que en la caja se hace en Inventario, Producción y Catálogo → foto también se pide desde la web. Igual que los precios o la ruta, **la nube sólo valida y encola** (`branch_command`, migración de la nube 006) y **la caja de escritorio lo aplica con sus propias reglas y permisos** (`CloudCommandService`), firmado por quien lo pidió. Mientras la caja no se conecta, la web lo muestra en **«Enviado a la caja»** como *Pendiente*; si la caja lo rechaza, ahí mismo sale *Rechazado* con el motivo (también en Cajas → Cambios).

| Qué | Quién | Cómo |
|---|---|---|
| **Entrada** | `inventory.receive` (encargado y administrador) | Insumo, cantidad y unidad (kg, g, L, ml, pieza o la presentación del insumo, como «Bulto 44 kg» en enteros), referencia y observación opcionales. |
| **Merma** | `inventory.waste` (encargado y administrador) | Cantidad, unidad y motivo (dañado, caducado, error de producción, derrame, otro). «Otro» pide observación. |
| **Conteo** | `inventory.adjust` (sólo administrador, como en la caja) | Lo que hay físicamente; la caja calcula la diferencia y registra la ganancia o pérdida con su motivo. **Si el insumo no tiene saldo, el primer conteo es su saldo inicial.** Una entrada o merma sin saldo inicial se rechaza con aviso. |
| **Registrar producción** | `production.create` y `production.confirm` (encargado y administrador) | Panes con receta activa, piezas planeadas y las que salieron. La caja prepara la producción con esa receta, la fija y la confirma: descuenta los insumos y suma lo que salió. Si falta existencia (o algo está inactivo o sin saldo) la rechaza **sin dejar borrador** y dice qué falta. |
| **Foto del producto** | `products.edit` | En el producto, **Agregar/Cambiar foto** (celular: cámara o galería). El navegador la endereza, la recorta al centro en cuadro y la reduce (máx. 1024 px, JPEG < 1 MB); la caja la procesa igual que una importada ahí (original + miniatura de 512) y la sube. Un producto nuevo puede llevar su foto desde el alta. |

Reglas:

- Cantidades como texto decimal (`"2.5"`), hasta 3 decimales y 999,999; deben dar unidades mínimas enteras (2.5 kg sí, 0.5 mg no). La caja vuelve a revisar unidad, dimensión, existencia y permisos.
- **Cada pedido es una operación con el identificador del cambio**: si la caja lo aplica dos veces (corte de luz, red), no se repite nada (inventario: mismo `OperationId`; producción: lote = id del cambio y operación derivada; foto: misma operación).
- La nube no guarda la foto en la lista de cambios que ve la web; a la caja le baja sólo lo que no ha reportado y en lotes de hasta ~3 MB.
- Las cajas web ya no cuentan como «cajas que aplican cambios» (antes un cambio podía quedarse en «1 de 2 cajas»).
- Una caja de escritorio sin actualizar rechaza estos cambios con «actualiza Casa Miga».

Recetas (fórmulas y versiones) siguen configurándose en la caja.

## Diferencias con la caja de escritorio

- Necesita Internet en todo momento (no hay modo sin conexión).
- Imprime con el diálogo del navegador; no abre cajón ni manda ESC/POS.
- Las ventas web **no descuentan inventario** de la caja de escritorio ni aparecen en los reportes de la app de escritorio (sí en todos los de la web).

## Fases

- **Fase 1** (08/10/2026): caja web, turno, venta con fotos, cobro, ticket e impresión, corte.
- **Fase 2** (09/10/2026): tickets, reimpresión, cancelaciones y devoluciones, gastos/entradas/retiros. Pruebas `cloud/test/pos.test.ts` (5).
- **Fase 3** (09/10/2026): entradas, mermas y conteos de inventario, registro de producción y foto del producto desde la web (aplicados por la caja). Pruebas de la nube `cloud/test/commands.test.ts` y de la caja `CloudCommandTests`, `RecipePersistenceTests.WebProduction`, `WebPhotoCommandTests`.
- **Inventario**: hoy el descuento automático de inventario al vender está **apagado** en la caja de escritorio (`inventory_sale_consumption_config` = DISABLED) y ningún producto lleva inventario, así que las ventas web y las de escritorio se comportan igual. Si se enciende, faltará mandar el consumo de las ventas web a la caja de escritorio (un cambio de la web más, como los de la ruta).
