# Plan para completar la caja (C#) y la web — 09/10/2026

Pedido del usuario: «Sigue con Ventas y el resto de pantallas y termina todo… completar la app tanto la App de C# como la de Web con todas las pruebas correspondientes». Diseño: el de la caja de escritorio (QAv1). Lenguajes: los actuales (C#/WPF en la caja; HTML/CSS/JS y TypeScript en la web).

Base: auditoría pantalla por pantalla de la caja contra la web (09/10/2026). Cada fase se cierra con pruebas, `scripts/verify.ps1`, commit y build de revisión.

## Web igual a la caja

| Fase | Qué | Estado |
|---|---|---|
| W1 | Marco, Punto de venta, Cobro, Tu caja | Hecho (1cb85cc) |
| W1b | Historial de ventas (folio, días, Hoy/Ayer/Sesión actual, detalle, reimpresión, cancelar/devolver) | Hecho (9621ba7) |
| W1c | Pendientes chicos: confirmar «Limpiar» del ticket, usuario en cada movimiento de caja, «Ver cierre» con detalle y páginas en Cierres anteriores | Pendiente |
| W2 | Reportes con los mismos cálculos de la caja (Resumen, Comparativo, Ventas por hora, Control de caja, Producción) + Exportar CSV; Ganancia se queda como pestaña extra | Hecho (cifras iguales a la base de la caja: 1061 ventas, $71,583.00, $278.00) |
| W3 | Catálogo: pestañas Productos/Categorías, tabla con páginas, categoría nueva sin salir del producto, origen de foto (real/provisional), Quitar foto, recorte manual | Pendiente |
| W4 | Inventario: consumo automático por ventas, búsqueda y filtros, alta/edición/activar artículos, presentaciones, asociaciones con productos, historial por artículo, vista previa de la operación | Pendiente |
| W5 | Recetas: crear y editar recetas (fórmulas) y perfiles de producto con versiones, borradores, publicar y activar, alternativas | Pendiente |
| W6 | Producción en dos pasos: plan, necesidades y faltantes, alternativas, borrador, registrar con lo que salió, borradores e historial con detalle | Pendiente |
| W7 | Charolas: resumen de ruta, ayuda, entregar como en la caja, recoger con saldo anterior, estado de cuenta, precios de charola, reporte | Pendiente |
| W8 | Usuarios con confirmaciones; cambiar contraseña desde la web; «¿Olvidaste tu contraseña?» | Pendiente |

## Caja igual a la web

| Fase | Qué | Estado |
|---|---|---|
| D1 | Reporte de Ganancia en la caja | Pendiente |
| D2 | Costo de referencia de insumos en Inventario | Pendiente |
| D3 | Charolas: análisis por tienda, entregas y cobros del periodo | Pendiente |
| D4 | Historial de ventas: forma de pago y búsqueda por producto | Pendiente |
| D5 | Las ventas de las cajas web en los reportes de la caja | Pendiente |

## Independencia de la web

| Fase | Qué | Estado |
|---|---|---|
| I1 | Lo que se cambia en la web se ve al momento en la web aunque la caja esté apagada (catálogo, usuarios, tiendas); la caja lo confirma al conectarse y, si lo rechaza, se regresa | Pendiente |

Fuera de alcance por ahora (necesitan decisión o equipo físico): vender sin Internet en la web, impresora térmica y cajón desde el navegador.
