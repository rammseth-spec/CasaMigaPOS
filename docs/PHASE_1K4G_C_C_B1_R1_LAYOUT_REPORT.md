# FASE 1K.4G-C-C-B1-R1 — auditoría y optimización de layout

Fecha: 19/10/2026. Modelo: Codex basado en GPT-6. Estado: implementación técnica; la aprobación visual humana sigue pendiente. Rama: `master`. HEAD de partida: `b4417f45dc9a0fd38942045c466d79e5d6f06719`. El commit local de cierre y su HEAD final se entregan en la conversación de cierre. Sin push, merge, cambio de rama ni inicio de C-C-B2.

## Alcance y diagnóstico

Se revisaron el shell y acceso, Inventario, Catálogo, POS, Caja, Recetas, Producción, Historial de ventas, Reporte diario, Centro operativo y el diálogo de cobro. La causa principal del espacio vacío era medir listas dentro de `StackPanel`/`ScrollViewer` no acotados y luego limitar `DataGrid` a 190, 310 o 360 DIP. En un monitor alto la tabla seguía pequeña mientras la vista dejaba superficie libre. En ancho reducido, varios grids impedían el desplazamiento horizontal aun cuando los mínimos de sus columnas ya no cabían.

| Vista | Situación encontrada | Corrección R1 |
| --- | --- | --- |
| Inventario | Tabla principal de 310 DIP; tarjeta de consumo extensa; filtros y selección encima de la lista | `Grid` Auto/*/Auto, tabla que crece y conserva paginación; tarjeta que envuelve su contenido; búsqueda y selección accesibles; scroll horizontal de tabla sólo si el ancho lo exige. |
| Catálogo | Categorías y productos de 190 DIP | Listas en filas `*`, botones y paginación abajo; editores con scroll propio; columnas desplazables en ancho insuficiente. |
| POS | Tarjeta de 276 DIP y nombre limitado; algunas descripciones y precios se recortaban; carrito con renglón fijo | Foto a 132 DIP sin distorsión; nombre con alto automático, precio separado, tarjeta con mínimo de 250 DIP; nombre del carrito con alto automático; columna de productos admite 320 DIP en ventana compacta. |
| Caja | El historial ya usaba una fila `*`; dashboard limitado a 1120 DIP | Se retiró el tope del dashboard y se habilitó desplazamiento horizontal en el historial cuando sus columnas no caben. |
| Recetas | Listado principal sin crecimiento útil dentro del shell | Lista acotada en fila `*`; edición y detalles largos con desplazamiento vertical independiente. Las tablas pequeñas de formularios mantienen sus alturas deliberadas. |
| Producción | Borradores e historial a 360 DIP | Ambas listas crecen en fila `*`; formularios y detalle conservan desplazamiento propio; sus tablas pueden mostrar todas las columnas mediante scroll si hace falta. |
| Historial de ventas | Ya tenía lista adaptable, pero columnas podían quedar inaccesibles en ancho estrecho | Contenedor acotado por el shell y scroll horizontal local en lista y partidas. |
| Reporte diario | Tope de ancho de 1480 DIP en monitor amplio | Límite ampliado a 1800 DIP. Tablas y gráfica resumidas conservan alturas acotadas porque no son listas largas. |
| Centro operativo | Panel lateral con contenido desplazable existente | Sin cambio funcional ni de anchura: su estructura ya acota el contenido. |
| Acceso y diálogo de cobro | Acceso de dos columnas y diálogo alto podían exceder pantallas con escala 150 % | Acceso de una columna a menos de 1100 DIP, centrado; diálogo con altura máxima de pantalla y scroll interno. |

El shell separa listas principales de su `ScrollViewer` genérico para entregarles un alto medible. Sólo en ventanas cortas el contenedor exterior desplaza el módulo completo. El encabezado pasa sus acciones a una segunda línea en el modo compacto; el menú reduce márgenes y ancho. El mínimo de ventana baja a 820×440 DIP. En POS con barra lateral expandida y ventana compacta se reservan 180 DIP para navegación, 320 para productos y 300 para carrito; al contraer la barra se conservan 84 DIP. No se cambió F11/Esc ni las acciones de navegación.

Se preservaron `DataGrid` virtualizados, estilos compartidos, fotos existentes, proporción del logo oficial, colores, foco y acciones. No se cambiaron cantidad de registros por página, precio, SKU, producto, inventario, receta, venta, caja, impresión o contratos de aplicación. `MainWindow.xaml.cs` sólo propaga el ancho disponible al viewmodel para elegir la presentación compacta.

## Validación y límites

La prueba WPF nueva mide Inventario, Catálogo, Recetas y Producción a 1100×850 y 1100×560 DIP: comprueba que las listas principales superan sus antiguos límites, conservan un mínimo útil en ventana baja y mantienen virtualización. También verifica el ancho efectivo de 853 DIP, equivalente a 1280 px a 150 %, y que las dos columnas del POS caben en 624 DIP con la barra lateral compacta expandida. Las pruebas existentes de búsqueda, carrito, tablas, ventas, caja y navegación se conservan.

`scripts/verify.ps1` finalizó correctamente: restore conforme al lock, Release sin errores ni advertencias y **802/802 pruebas** (584 generales + 218 WPF), sin omitidas. Focales finales de layout, tablas e Inventario: 26/26. Durante el ajuste se corrigieron aserciones que codificaban los antiguos 960×640 DIP y 276 DIP; una prueba asíncrona de búsqueda agotó su plazo de 8 s una vez bajo carga simultánea, pasó focalmente y en la suite final con un plazo de 20 s. No cambió la lógica de búsqueda.

El smoke visual físico **no está aprobado** todavía. No se declara haber examinado visualmente 1280×720, 1280×820, 1366×768, 1920×1080 o 3440×1440, ni las escalas 100/125/150 % en un monitor real desde una sesión iniciada. En ventanas bajas puede aparecer scroll vertical; en tablas anchas puede aparecer scroll horizontal local. Ambas barras son deliberadas para evitar pérdida de controles o columnas. Deben revisarse especialmente acceso, barra lateral expandida/contraída, POS con búsqueda activa, paginación, editor largo, F11 y Escape.

## Datos y entrega aislada

Base protegida: `work/phase1k2c-r2-smoke-20260928/casamiga.db`, SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. Origen B1: `work/phase1k4g-c-c-b1-review-20261018/casamiga.db`, SHA-256 `B40CCD0C50024B433F698953875F5D08A5F85356B70FE91511B2AA3B41F87CAF`. Copia R1: `work/phase1k4g-c-c-b1-r1-review-20261019/casamiga.db`, inicialmente idéntica por SHA-256 al origen B1. No se copiaron WAL/SHM/lock. Schema B1: 17; no se crea migración 018. La copia conserva 67 artículos (31 materias primas y 36 terminados), 36 productos activos, cinco históricos inactivos y consumo automático `DISABLED`. No se hicieron movimientos, ventas, cobros, impresiones ni cambios de saldo. Las credenciales de revisión se verificaron contra la copia R1 en lectura y se entregan sólo en la conversación, no en Git.

Salida identificada: `src/CasaMiga.Presentation/bin/Phase1K4G-C-C-B1-R1/CasaMiga.Presentation.exe`, SHA-256 `60E5E7D4FAEF18E5878256FC8DACFA7B691E899853225575F93ABD108E942CA5`. DLL de Presentation: `src/CasaMiga.Presentation/bin/Phase1K4G-C-C-B1-R1/CasaMiga.Presentation.dll`, SHA-256 `5E9E6CFDC252DC236918C0EB401DDADAEC981FCD52BAFA2D`. Launcher: `work/Abrir-Casa-Miga-1K4G-C-C-B1-R1.cmd`. DataDirectory: `work/phase1k4g-c-c-b1-r1-review-20261019`. `development.settings.json` local fija este directorio y `IsDevelopment=true`; el launcher limpia `CASAMIGA_DATA_DIR` y usa el runtime del proyecto. La identidad esperada del título es `Casa Miga POS · Caja · DESARROLLO · 1K.4G-C-C-B1-R1`; comprobar proceso/título al abrir.

## Smoke humano recomendado y continuidad

1. Abrir sólo el launcher R1 y confirmar título de desarrollo. Iniciar sesión manualmente con las credenciales entregadas en la conversación.
2. Revisar Inventario con 67 filas, filtros, selección rápida, paginación y detalle; maximizar, restaurar y probar F11/Esc. La tabla debe aprovechar la altura sin ocultar los controles.
3. Revisar Catálogo, Recetas, Producción, Historial y Caja; comparar pantalla alta y baja, y comprobar que un scroll local permite alcanzar la última columna en ancho estrecho.
4. En POS revisar tarjetas con nombres largos y precios, carrito, barra lateral expandida/contraída, categorías, búsqueda y botón de cobro visible. No confirmar cobros ni imprimir.
5. Revisar acceso y diálogo de cobro sólo visualmente. No crear usuario ni confirmar una operación.

No iniciar C-C-B2 hasta aprobación humana y resolución de los bloqueos del recetario maestro. Modelo recomendado para C-C-B2: GPT-6.1 Sol, esfuerzo alto, sobre otra copia aislada. La base original, B1 y evidencia histórica permanecen fuera de alcance.
