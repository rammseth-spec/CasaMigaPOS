# FASE 1K.4G-B-R2 — sistema global de tablas y legibilidad responsive

Fecha: 11/10/2026

Estado: implementación y verificación técnica completadas; smoke humano de merma pendiente

HEAD inicial: `2e38047173b48c135296163cf8900d40a84948c8`

## Resultado

Presentation dispone ahora de un estándar común para tablas WPF. Los encabezados admiten wrapping y padding uniforme; el texto humano se muestra completo en filas automáticas; cantidades y piezas se centran; importes se alinean a la derecha; y las columnas combinan `*`, mínimos seguros y anchos acotados según el tipo de dato. Las estructuras operativas fijas impiden reordenar columnas y conservan el foco de teclado visible.

La selección abandona el azul intenso nativo y usa un estado cálido coherente con Casa Miga, con contraste legible. El cambio se implementó exclusivamente mediante recursos de Presentation. Los tooltips siguen siendo complementarios y no sustituyen información primaria visible.

## Recursos compartidos

Se amplió el sistema existente de `Resources/Theme.xaml`; no se creó un tema paralelo. Los recursos nuevos son:

- `CasaMigaTableHeaderStyle`
- `CasaMigaTableTextHeaderStyle`
- `CasaMigaTableCenteredHeaderStyle`
- `CasaMigaTableRowStyle`
- `CasaMigaTableCellStyle`
- `CasaMigaTableTextStyle`
- `CasaMigaTableCenteredTextStyle`
- `CasaMigaTableMoneyTextStyle`
- `CasaMigaTableNumericEditorStyle`

El encabezado base conserva contenido enriquecido para que la ayuda contextual de Recetas siga funcionando. Los estilos implícitos de `DataGridColumnHeader`, `DataGridRow` y `DataGridCell` aplican mínimos y estados comunes sin duplicarlos en cada vista.

## Inventario de tablas auditadas

| Vista | Propósito | Composición revisada | Problema | Estrategia final |
|---|---|---|---|---|
| Catálogo | Categorías y productos | código, nombre, precio, estado | anchos rígidos y texto largo | columnas flexibles, wrapping e importes a la derecha |
| Inventario principal | Existencias | código, nombre, tipo, existencia, unidad, estado, producto | columnas humanas comprimidas | star sizing, mínimos seguros y separación clara |
| Inventario / Presentaciones | Presentaciones del ítem | código, nombre, contenido, activa | rigidez y encabezados inconsistentes | texto flexible, cantidad centrada y filas auto |
| Inventario / Asociaciones | Producto → inventario | producto, SKU, cantidad por venta | desperdicio de ancho | nombre flexible, SKU acotado y cantidad centrada |
| Inventario / Historial | Trazabilidad de ledger | fecha, operación, movimiento, existencia, usuario, detalle | ocho columnas, cortes y scroll horizontal | seis columnas; existencia y detalle agrupados sin perder valores |
| Inventario / Selector | Selección de artículo | código, nombre, tipo, saldo, unidad, estado, producto | mezcla de anchos rígidos | mismo estándar responsive del inventario principal |
| Recetas | Lista y composición | recetas, versiones, ingredientes, alternativas, perfiles y preparaciones | encabezados largos y texto humano | wrapping, filas auto y ayuda enriquecida preservada |
| Producción | Planeación y ejecución | selección, plan, fórmulas, necesidades, borradores, historial y resultado real | estilos locales y columnas rígidas | recursos comunes y columnas flexibles con mínimos |
| Caja | Cierres anteriores | cierre, caja, responsable, esperado, contado, diferencia | responsables e importes comprimidos | texto flexible e importes a la derecha |
| Historial de ventas | Lista y detalle | venta, fecha, total, pago, cajero, terminal | truncamiento potencial | columnas de identidad flexibles, dinero a la derecha |
| Reporte diario | Productos más vendidos | producto, piezas, ventas | alineación inconsistente | producto flexible, piezas centradas, ventas a la derecha |

## Historial de movimientos de Inventario

La composición final contiene seis columnas:

1. Fecha
2. Operación
3. Movimiento
4. Existencia
5. Usuario
6. Detalle

`Existencia` presenta el valor anterior y el resultante como `0 pieza → 2 piezas` o su unidad humana correspondiente. `Detalle` presenta motivo y referencia en líneas independientes. Las propiedades derivadas `BalanceChange` y `Detail` viven en el ViewModel de Presentation; no modifican contratos ni persistencia. En el smoke se leyó completa la salida de producción del 11/10/2026 a las 02:18, con `+2 pieza`, usuario `Administrador catálogo` y trazabilidad completa, sin scroll horizontal.

## Producción y demás módulos

La tabla `Resultado real obtenido` conserva la corrección R1: Producto, Piezas planeadas, Piezas reales y Comparación, con mínimos seguros, números centrados y comparación con wrapping. El mismo estándar se aplicó a selección de productos, plan actual, fórmulas/preparaciones, necesidades, borradores e historial.

Recetas conserva encabezados enriquecidos y ContextHelp. Historial de ventas conserva `Venta #`, fecha, cajero, total y paginación. Reporte diario conserva su diseño aprobado y sólo normaliza `Productos más vendidos`. Caja conserva conteo ciego y lógica de cierres. POS, ticket Qian y Centro operativo no recibieron cambios funcionales.

## Responsive y revisión visual

Las pruebas WPF renderizan explícitamente las superficies 1280×820, 1920×1080 y 3440×1440, además de verificar composición, mínimos, wrapping, alineaciones y ausencia de scroll horizontal como solución de diseño. El smoke visual vivo cubrió una ventana de clase 1280 y F11 en monitor 1920×1080. En ambos se revisaron las tablas principales de Caja, Inventario, detalle de movimientos, Producción, Recetas e Historial de ventas. La validación 3440×1440 se realizó con render WPF explícito, porque el equipo de revisión no dispone de un monitor físico con esa resolución.

No se observó clipping de datos críticos, botones fuera de pantalla ni scroll horizontal global. En ultrawide, el contenido mantiene la estructura de la vista y evita anchos rígidos que generen columnas estrechas.

## Datos y seguridad

La copia de revisión es `work/phase1k4g-b-r2-smoke-20261011`. Conserva la producción DEMO registrada:

- Terminado sintético A: 2 piezas
- Terminado sintético B: 2 piezas
- Harina sintética: 9.58 kg
- Huevo sintético: 28 piezas
- Agua sintética: 4.86 L

La auditoría posterior al smoke confirmó schema 17, cero ventas, cero solicitudes de impresión y ausencia de migración 018. No se ejecutó merma, venta, impresión ni cajón. La base protegida `work/phase1k2c-r2-smoke-20260928/casamiga.db` conservó antes y después SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.

## Verificación

- Focales de tablas/Presentation: 14/14.
- Regresión puntual posterior al ajuste de encabezados enriquecidos: 3/3.
- Suite general: 584/584.
- Suite WPF/Presentation: 215/215.
- Total: 799/799.
- Fallidas: 0.
- Omitidas: 0.
- Release `src/CasaMiga.Presentation/bin/Phase1K4G-B-R2/`: 0 errores, 0 advertencias.
- `git diff --check`: correcto.
- Schema: 17.
- Migración 018: no existe.

## Build de revisión

- EXE: `src/CasaMiga.Presentation/bin/Phase1K4G-B-R2/CasaMiga.Presentation.exe`
- SHA-256 EXE: `4E438B2C96DC011EA4E0CB4ED7A67A6CD3412A01628D113A37C55714118CB7A8`
- DLL: `src/CasaMiga.Presentation/bin/Phase1K4G-B-R2/CasaMiga.Presentation.dll`
- SHA-256 DLL: `F1163ECB523FE60CAAD083E323A8A5500A5DA1B8AFCC7DAC7E6F42B2B25C21C6`
- Launcher: `work/Abrir-Casa-Miga-1K4G-B-R2.cmd`
- SHA-256 launcher: `368F1ADBE134519DC0B682FB7DE0F6E46DCDC65A6F8E6BE494D69945A7F909D9`
- DataDirectory: `work/phase1k4g-b-r2-smoke-20261011`
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.4G-B-R2`

## Alcance

Los cambios se limitan a Presentation/WPF, pruebas relacionadas y documentación. Domain, Application, Infrastructure, cálculos, persistencia, migraciones, impresión y datos comerciales permanecen intactos. No se inició 1K.4G-C, 1K.4H-B, Premium UI ni 1K.5. El siguiente smoke debe continuar manualmente con la merma desde el detalle de Terminado sintético A; esta fase no la ejecutó.
