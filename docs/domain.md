# Dominio y reglas

## Fundación de inventario — FASE 1H.1

`InventoryQuantity` es una magnitud física exacta y no negativa formada por dimensión y unidades atómicas Int64. Masa usa mg, volumen µL y conteo pieza. Las unidades estándar admitidas son mg/g/kg, µL/ml/L y pieza. La entrada decimal sólo es un intermediario exacto: se rechazan pérdida de precisión, dimensión incompatible, resultado negativo y overflow. Cero representa una existencia vacía; factores de presentación y cantidades por unidad comercial deben ser positivos.

`InventoryItem` distingue RAW_MATERIAL, FINISHED_GOOD, PACKAGING y SUPPLY. Su dimensión queda fija desde la creación. No puede desactivarse con saldo distinto de cero; Application deberá aportar ese hecho desde persistencia. `InventoryItemUnit` modela presentaciones específicas del artículo y sólo convierte cantidades enteras positivas. `InventoryLocation.Main` define MAIN / Almacén principal sin impedir ubicaciones futuras.

`ProductInventoryLink` asocia explícitamente un Product con un InventoryItem y una cantidad exacta positiva por unidad comercial. `Product.TrackInventory` sigue siendo sólo intención: no crea la relación ni produce efectos físicos. Los tipos OPENING_BALANCE, RECEIPT, ADJUSTMENT_GAIN, ADJUSTMENT_LOSS y WASTE quedan identificados para 1H; producción, venta, devolución y transferencia permanecen deshabilitados. Motivos OTHER de merma y ajuste requieren observación.

1H.1 no contiene ledger, saldo, persistencia, permisos, UI ni integración con Checkout.

## Ledger persistente — FASE 1H.2

`InventoryDelta` mantiene el signo separado de `InventoryQuantity`, prohíbe cero y `long.MinValue`, compara sólo dentro de su dimensión y aplica resultados con `checked` sin permitir negativos. OPENING_BALANCE usa una línea especial que puede representar cantidad cero; los movimientos ordinarios conservan deltas distintos de cero.

`InventoryOperationRequest` valida tipo, signo, partidas únicas, razón estructurada y límites. Las partidas se ordenan por ubicación y artículo para hash, bloqueo lógico y persistencia. El hash canónico incluye actor, terminal, tipo, motivo, referencia, delta y snapshot de captura.

El agregado persistido pasa PENDING→CONFIRMED dentro de una transacción inmediata. La operación confirmada y sus movimientos quedan inmutables. Correcciones futuras deberán usar operaciones compensatorias. El saldo materializado no es parte autoritativa del dominio y puede compararse contra la suma checked del ledger.

Hecho de negocio: panadería mexicana, una caja inicial y operación offline. No se asumen precios, impuestos, credenciales, unidades fraccionarias de venta ni datos definitivos de productos.

| Área | Entidades y reglas propuestas |
|---|---|
| Catálogo | Product, Category; desactivar en vez de borrar referencias históricas |
| Venta | Sale y SaleItem con nombre, precio, costo, descuentos e impuestos snapshot |
| Pago | Colección Payment, importe aplicado; efectivo recibido/cambio separados; tarjeta externa registrada sin PAN/CVV |
| Caja | CashSession, CashMovement; apertura, fondo, entradas/retiros, conteo y cierre |
| Inventario | InventoryItem, Movement inmutable; suma por unidad base |
| Receta | RecipeVersion y RecipeIngredient; versión publicada inmutable |
| Producción | Batch referencia versión; consumos, salida real y costo en un commit |
| Merma | Waste y motivo explícitos; movimientos vinculados, sin doble contar pérdida de lote |
| Compras | Supplier, Purchase, PurchaseItem; costo/recepción y ledger atómicos |
| Seguridad | User, Permission, RolePermission, AuditEvent; autorización de casos de uso |

Venta PENDING→COMPLETED al confirmar pago local. PENDING→CANCELLED sin simular ingreso. Una cancelación posterior a cobro requiere operación compensatoria autorizada; COMPLETED→REFUNDED sólo cuando la devolución total es válida. Diseñar devolución parcial mediante entidad Refund/RefundItem y límites acumulados; el estado no sustituye el registro de devoluciones. No borrar ventas completadas.

Corte propuesto: esperado = fondo + efectivo neto aplicado en ventas − devoluciones efectivo + entradas − retiros. Diferencia = contado − esperado. Tarjeta se informa aparte. Definir tickets netos/brutos, ticket promedio y tratamiento de cancelaciones antes de cerrar el caso de uso.

Pendientes del propietario para FASE 1: precios definitivos, política de descuentos/devoluciones, impuestos con asesoría apropiada, cierre ciego o visible, venta sin stock, zona horaria comercial y roles operativos. No bloquean esta fundación.

## Reglas implementadas en FASE 1A

CashSession tiene identidad, terminal, propietario, UTC, fondo y estado inmutables. Fondo cero permitido y negativo rechazado; importes siguen ADR-003. Application impide apertura sin cash.open y consulta sin cash.view, revalidando cuenta activa. Una terminal admite una sola OPEN; otras identidades de terminal se tratan independientemente en persistencia, sin implementar red. Sesión y audit_entry se confirman juntas. Cerrar la aplicación no cierra caja; reinicio exige login y recupera la sesión existente.

CashMovement y el cálculo de cierre del apartado anterior siguen propuestos. Los campos de cierre existen como preparación, sin caso de uso ni UI de cierre. Roles iniciales y permisos están definidos en ADR-006; políticas de cierre deberán acordarse para 1B.

## Reglas de cierre implementadas — FASE 1B

C1–C14 protegidas entre dominio, Application y persistencia. CashSession.Close sólo transforma OPEN, exige actor válido, timestamp no anterior a apertura y montos no negativos. Devuelve nuevo aggregate CLOSED con diferencia checked; no modifica el original. El historial mantiene propietario y cerrador distintos cuando corresponde. Propietario necesita cash.close; tercero necesita además cash.close.other.

cash_count representa la declaración confirmada de efectivo, no un borrador editable ni cierre definitivo. Se conserva tras volver/reiniciar; la confirmación final sigue siendo necesaria. En esta fase esperado=fondo y diferencia=contado−esperado. No se incorporan movimientos ni ventas ficticias. Corrección de conteo posterior se deja para un caso auditado futuro.

## Catálogo implementado — FASE 1C

Category: Id GUID, Name (1–100), Description opcional (hasta 1000), SortOrder Int32, IsActive, CreatedAt/UpdatedAt UTC. Nombre único conforme ADR-008. Orden por SortOrder, nombre normalizado e ID. Sin eliminación física.

Product: Id/CategoryId GUID, SKU opcional (hasta 80), Name (1–160), Description, SalePrice Money, IsActive, TrackInventory y fechas UTC. Nombres de producto no son claves ni necesariamente únicos. SKU presente sí es único incluso inactivo; nunca se rellena automáticamente. Precio cero permitido; negativos/fractiones de centavo rechazados. $18.00 = 1800 centavos; UI parsea decimal con punto y sin separador de miles.

Producto activo exige categoría válida y activa. Categoría con producto activo rechaza desactivación; primero mover/desactivar hijos. Producto inactivo puede pertenecer a categoría inactiva, pero no reactivarse mientras ella siga inactiva. Reactivación auditada conserva identidad y creación. TrackInventory predeterminado true es sólo metadato, sin stock ni movimientos.

CatalogService revalida usuario activo y permiso en cada operación. Administrator/Manager: catalog.view, categories.create/edit/deactivate, products.create/edit/deactivate. Cashier: catalog.view. El permiso deactivate también autoriza reactivar; editar no cambia estado por debajo de esa autorización.

Auditoría before/after conserva nombre, descripción, categoría, SKU, centavos y flags; actor/terminal/UTC e ID de entidad en columnas existentes. Sin cambios efectivos no se generan eventos. No hay tabla adicional de historial; las futuras ventas deberán guardar snapshots independientes del catálogo. Imágenes/iconos/color podrán agregarse después sin blobs ahora.

## Carrito transitorio — FASE 1D

Cart is transient and not a sale. Cart y CartLine son inmutables y sólo existen en memoria. Cart se asocia a UserId, TerminalId y CashSessionId. CartLine conserva ProductId, ProductName, UnitPrice, Quantity y LineTotal; cantidades Int32 enteras >=1. Agregar repetido incrementa la línea existente. Disminuir desde 1 se rechaza; la UI deshabilita «−» y ofrece Eliminar. Clear conserva el contexto del modelo y vacía las líneas.

Money usa centavos Int64 checked en multiplicación y suma. Total preliminar=Subtotal=Σ(UnitPrice×Quantity). Precio cero válido, negativo inválido. Overflow deja intacto el carrito original.

Nombre/precio se capturan al agregar por primera vez. Agregar de nuevo valida actividad actual pero conserva el snapshot anterior; cambios de nombre/precio o desactivación no reescriben partidas existentes. Incrementar/disminuir una partida existente mantiene su snapshot incluso inactiva. Antes de una venta futura será obligatoria la revalidación de comerciabilidad según política de 1E.

PosService exige usuario activo, catalog.view y pos.use, terminal válida, caja OPEN y sin conteo confirmado. Logout/cambio de usuario, conteo/cierre de caja y cambio de contexto no heredan partidas: exigen descarte explícito. Cerrar normalmente la app también pregunta; un fallo fatal termina y pierde el carrito. No existe auditoría comercial ni escritura SQLite por editar el carrito.

## Venta y efectivo implementados — FASE 1E (vigente)

Las propuestas generales anteriores no son estados implementados. No se persiste PENDING: Cart sigue en memoria hasta confirmar. Sale es un agregado inmutable con Id, OperationId, UserId, TerminalId, CashSessionId, Sequence, ConfirmedAt UTC, Items y Payment. Folio = terminal GUID sin guiones + secuencia de al menos ocho dígitos; único por terminal/secuencia, no fiscal. MXN es la política única de Money, no multimoneda.

SaleItem conserva ProductId, nombre vendido, precio unitario, cantidad Int32 y total checked. Nombre y precio permanecen como en el carrito, incluso si cambian en catálogo. Producto y categoría deben seguir activos al cobrar. No hay costo, descuento ni impuesto comercial añadido. Subtotal=Total=suma exacta de líneas. Precio cero y venta total cero permitidos; carrito vacío rechazado.

Payment único por Sale: método CASH, aplicado=Total, recibido>=Total, cambio=recibido−Total. Sale/SaleItem/Payment no tienen setters y SQLite impide actualización, eliminación y anexar partidas a la venta confirmada. La fecha del pago es la ConfirmedAt del agregado, confirmado en la misma transacción.

ExpectedCash vigente = fondo + suma de Payment.Applied de ventas de esa CashSession. Ejemplo: fondo $500 + venta $53, recibido $100 y cambio $47 → esperado $553. No se suma recibido ni se resta cambio de nuevo. No hay ventas después de cash_count confirmado.

## Proyecciones de historial — FASE 1F

FASE 1F no cambia el dominio comercial. `SalesHistoryQuery`, `SalesHistoryRow`, `SalesHistoryPage` y `SalesHistoryDetail` pertenecen a Application porque son proyecciones de consulta. El detalle reconstruye `Sale` para comprobar snapshots y aritmética Money; nunca persiste el agregado reconstruido. Los nombres actuales de usuario/terminal acompañan a UserId/TerminalId sin modificar las ventas históricas.
