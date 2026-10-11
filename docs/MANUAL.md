# Manual de Casa Miga POS

Para el personal de la panadería: **cajero**, **encargado** y **administrador**. Sirve para la caja de escritorio y para la versión web (celular, tableta o computadora). Si algo de la pantalla no coincide con este manual, avisa al administrador.

## 1. Lo básico

### Quién puede hacer qué

| | Cajero | Encargado | Administrador |
|---|---|---|---|
| Vender, abrir caja, registrar gastos y entradas | ✔ | ✔ | ✔ |
| Contar y cerrar la caja, retiros, cancelar o devolver ventas | | ✔ | ✔ |
| Reportes, historial de ventas, charolas, catálogo, recetas, producción | | ✔ | ✔ |
| Entradas y mermas de inventario | | ✔ | ✔ |
| Conteo físico de inventario, configurar inventario y recetas | | | ✔ |
| Usuarios, nube, respaldos y cajas web | | | ✔ |

### Entrar

1. Escribe tu **usuario** y tu **contraseña**. Son los mismos en la caja y en la web.
2. Si te dieron una **contraseña temporal**, el sistema te pide crear una nueva al entrar. Debe tener de 8 a 16 caracteres, con mayúscula, minúscula, número y un símbolo (por ejemplo `#` o `!`). La lista en pantalla te dice qué falta.
3. Para salir, toca **Salir** (abajo en el menú). Siempre sal al terminar tu turno.

¿Olvidaste tu contraseña? Pide al administrador que la restablezca (Usuarios → Restablecer contraseña). Te dará una temporal en persona.

### El menú

Venta · Caja · Charolas · Ventas · Reportes · Catálogo · Inventario · Recetas · Producción · Usuarios · Nube. Sólo ves lo que tu puesto permite. Arriba siempre ves si la **caja está abierta**, el estado de la **nube** y quién está usando el sistema.

## 2. Cajero

### Abrir la caja (al empezar el turno)

1. Entra a **Caja** → **Abrir caja**.
2. Escribe el **fondo inicial** (el efectivo con el que empiezas, por ejemplo `500.00`; puede ser `0`).
3. Toca **Confirmar apertura**. Ya puedes vender.

### Vender

1. En **Venta**, toca la tarjeta de cada pan. Cada toque suma una pieza. Usa las categorías de arriba o **Buscar pan o SKU** para encontrarlo más rápido.
2. En el **Ticket** (derecha) revisa piezas y total. Puedes cambiar la cantidad o quitar un pan. **Limpiar** vacía el ticket (te pide confirmar).
3. Toca **Cobrar** (o **Cobrar + imprimir ticket** si el cliente quiere ticket).
4. Elige la forma de pago:
   - **Efectivo**: escribe lo que te dieron; el sistema calcula el **cambio**. Confirma sólo después de recibir el dinero.
   - **Tarjeta** o **Transferencia**: se cobra el total exacto. Puedes anotar la referencia (últimos 4 dígitos o folio). Confirma sólo cuando la terminal o la transferencia muestren el pago **aprobado**.
5. Aparece **Venta confirmada** con su **folio** (por ejemplo `C01-000123`). Toca **Volver al POS** para la siguiente venta.

Si se cae la red a media venta en la web y no sabes si se cobró, vuelve a tocar **Cobrar**: si ya se había registrado, **no se duplica**.

### Gastos y entradas de efectivo

En **Caja**, registra lo que sale o entra del cajón durante el turno:
- **Gasto** (insumos, gas, limpieza, transporte, mantenimiento u otro; «Otro» pide una nota).
- **Entrada** (efectivo que se agrega).

No se borran: si te equivocaste, registra el movimiento contrario. Los **retiros** los hace el encargado.

### Al terminar tu turno

Avisa al encargado para el **conteo y cierre**. El cajero no ve el «esperado»: el conteo es ciego para que nadie cuente pensando en la cifra.

## 3. Encargado

### Contar y cerrar la caja

1. En **Caja** → **Cerrar caja**.
2. Cuenta el efectivo del cajón y escribe el total en **Efectivo contado**. Toca **Continuar**. **Después ya no se puede cambiar el conteo.**
3. Aparece el **Resultado de caja**: fondo, ventas en efectivo, gastos, retiros, entradas, devoluciones, **esperado** y **diferencia** (exacto, sobrante o faltante).
4. Si hay diferencia, escribe una **nota** que la explique (es obligatoria).
5. Toca **Confirmar cierre**. El cierre es definitivo y queda en **Cierres anteriores**.

### Retiros

En **Caja** registra un **Retiro** cuando saques efectivo del cajón (por ejemplo, para depositar). No se puede sacar más efectivo del que debería haber.

### Ventas: buscar, reimprimir, cancelar o devolver

En **Ventas**:
- Busca por **folio** (`C01-001061` o sólo `1061`), por **días**, con **Hoy**, **Ayer** o **Sesión actual**, y filtra por **forma de pago** y **producto**.
- Elige una venta → **Ver detalle**: partidas, pago, quién cobró.
- **Reimprimir ticket** (sale como copia).
- **Cancelar la venta completa** o **devolver piezas**: escribe el motivo y confirma. El sistema te dice cómo regresar el dinero (efectivo del cajón, reverso en terminal o transferencia). Sólo con la caja abierta y antes del conteo. No se puede deshacer.

### Reportes

En **Reportes** elige el periodo (Hoy, Ayer, Últimos 7 días, Este mes o fechas) y la pestaña:
- **Resumen**: ventas, vendido, devuelto, neto, ticket promedio, por día, por pan, por cajero y por forma de pago.
- **Comparativo**: contra el periodo anterior de la misma duración.
- **Ventas por hora**: qué días y horas se vende más.
- **Control de caja**: diferencias por persona y motivos de devolución.
- **Producción**: piezas sugeridas para un día (promedio del mismo día de semanas anteriores + 10 %).
- **Ganancia**: mostrador contra charolas con el costo de cada pan desde su receta. Si falta el costo de un insumo, el pan sale «sin costo».

**Exportar CSV** guarda el reporte para Excel. Los reportes de la caja incluyen también lo que vendieron las cajas web de la sucursal.

### Charolas (venta en tiendas)

- **Ruta del día**: cada tienda con su charola y lo que debe.
  - **Entregar**: la charola habitual llega precargada. Ajusta con las tarjetas de pan y confirma.
  - **Recoger**: toca la foto de cada pieza que **regresó**. El sistema cobra lo vendido. Puedes cobrar en ese momento (todo o parte). Una charola mal capturada se puede **anular** con motivo, sólo si no se ha recogido.
  - **Cobrar**: pagos de saldo pendiente.
- **Tiendas**: datos de contacto, charola habitual y estado de cuenta.
- **Precios de charola**: precio para tiendas de cada pan. Sin precio, el pan no se puede poner en charolas.
- **Reporte**, **Entregas** y **Cobros**: lo del periodo, con % de venta por tienda y por pan.

### Catálogo

- **Nuevo producto** / **Editar producto**: nombre, categoría (puedes crear una sin salir), precio, SKU y foto.
- **Activar / desactivar**: un pan desactivado ya no aparece en Venta, pero sus ventas se conservan.
- **Categorías**: orden en que aparecen en Venta.

### Inventario, recetas y producción

- **Inventario**: **Entrada** (lo que llega), **Merma** (con motivo). El **conteo físico** lo hace el administrador.
- **Recetas**: fórmulas (preparaciones) y perfiles de cada pan, con versiones. Sólo el administrador las configura.
- **Producción**: plan de piezas → **Calcular necesidades** (dice si falta algo) → **Preparar registro** → al terminar, **Registrar producción** con lo que realmente salió.

## 4. Administrador

### Usuarios

- **Nuevo usuario**: usuario, nombre y rol (Cajero o Encargado). Se genera una **contraseña temporal** que sólo se muestra una vez: entrégala en persona.
- **Restablecer contraseña**, **Desactivar** o **Reactivar** a quien lo necesite. Un usuario desactivado ya no puede entrar.
- Si el administrador olvida su contraseña, se recupera en la computadora de la caja con: `CasaMiga.Maintenance reset-admin <carpeta de datos>`.

### Nube

- La caja sube lo que registra **cada 5 segundos** cuando hay Internet y baja lo que se cambió en la web. Si no hay Internet, sigue vendiendo y sube todo al regresar.
- **Respaldos**: la caja se respalda sola cada hora (sólo si hubo cambios). Se conservan 30 días. **Respaldar ahora** hace uno al momento.
- **Cajas web**: cada tableta o celular que vende por la web es una caja con sus propios folios (`C02-…`, `C03-…`). Créalas en Venta (web) → **+ Nueva caja web**.

### Restaurar una caja (si se descompone la computadora)

En la computadora nueva, con Casa Miga cerrado: `CasaMiga.Maintenance restore-cloud https://<sitio> <usuario administrador> <carpeta nueva>`. Pide la contraseña, baja el último respaldo, lo verifica y lo deja listo. Después abre Casa Miga con esa carpeta y registra la caja de nuevo en **Nube**. Detalle en [BACKUPS.md](BACKUPS.md).

## 5. La versión web

- Se usa con el mismo usuario y contraseña. **Necesita Internet** todo el tiempo.
- Lo que cambias en la web en **catálogo, usuarios y tiendas** se ve al momento con la marca **«Por confirmar»** hasta que la caja lo recibe. Si la caja lo rechaza, regresa como estaba y el motivo aparece en Nube → Cajas y cambios.
- Inventario, producción, recetas y la ruta de charolas se **envían a la caja** y se ven en cuanto la caja los aplica.
- El ticket se imprime con la impresión del navegador; la web no abre el cajón.

## 6. Si algo sale mal

| Pasa esto | Haz esto |
|---|---|
| Aviso naranja «tiene la caja abierta y no se conecta» (web, encargado) | La computadora de la caja está sin Internet o apagada a media jornada. Revisa la red y que Casa Miga siga abierto; al volver sube todo solo. |
| «Nube: sin conexión» | Sigue trabajando en la caja; sube todo al volver el Internet. En la web, espera a que regrese la red. |
| No puedo entrar | Revisa mayúsculas. Tras varios intentos se bloquea un rato. Pide al administrador que restablezca tu contraseña. |
| Me equivoqué en una venta | El encargado la cancela o devuelve piezas desde **Ventas** (con la caja abierta y antes del conteo). |
| Me equivoqué en un gasto o entrada | Registra el movimiento contrario con una nota. |
| La diferencia de caja no cuadra | Revisa gastos, retiros y devoluciones del turno; escribe la nota y avisa al administrador. |
| Un cambio de la web dice «Rechazado» | Lee el motivo en Nube → Cajas y cambios, corrige y vuelve a mandarlo. |
