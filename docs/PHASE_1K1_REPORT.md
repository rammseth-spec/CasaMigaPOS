# FASE 1K.1 — shell visual profesional

Estado: **CERRADA Y APROBADA** el 27/09/2026. Implementación, verificación automatizada y revisión visual completadas. 1K.2 no inició.

Corrección tras nueva evidencia humana: el setter global de `TextBlock.Foreground` anulaba el blanco heredado de los botones. Se retiró ese setter; el color general continúa heredándose de Window. `scripts/verify-theme.ps1` carga el tema real en WPF, materializa los controles, comprueba el Foreground de los TextBlocks generados y exporta una imagen. Primario y destructivo: #FFFFFFFF; secundario: #FF4B2E19; navegación: #FFFDF8EF. Imagen renderizada inspeccionada con texto legible. El acceso incorpora composición de dos columnas con panel verde, mensaje de bienvenida y formulario separado.

## Alcance implementado

- Shell WPF con sidebar, header, contenido compartido y pie de contexto.
- Navegación agrupada en Operación, Gestión y Consulta, respetando los permisos existentes.
- Login separado del shell; logout visible y separado de las acciones operativas.
- Recursos semánticos y estilos reutilizables para jerarquía tipográfica, botones, inputs, cards, DataGrid, selección, estados y foco.
- Contraste corregido en todos los botones: la plantilla transmite el `Foreground`, incluidos hover y disabled.
- Comportamiento adaptable mediante Grid, tamaños estrella, mínimos 960 × 640 y ScrollViewer; F11 y botón visible alternan pantalla completa sin cambiar los flujos operativos.
- Icono multirresolución del programa derivado del maestro oficial.
- Identidad oficial Variante 04 y paleta oficial. Hoja de Aguacate BEREL 405N usa `#4A5D4E`, equivalencia digital aproximada autorizada por el usuario.

## Identidad y control del activo

El SVG maestro conserva SHA-256 `7E67467CCF778EF40C40E8D15716AD550E1DA3A11C7D592D57578531A8228423`, `viewBox` 1540 × 893, un path a contornos y color `#4B2E19`. El derivado PNG transparente HD mide 3080 × 1786 y conserva SHA-256 `A1858372624CDDC87091A3DCFCBC5FA5E21F8C2621C5D5C6E2ACEE6D363B06CC`. Ambas instancias WPF usan `Stretch=Uniform`, escalado `HighQuality` y ajuste a píxeles del dispositivo. El icono ICO multirresolución sobre Fondo Natural conserva SHA-256 `A1C759AF8A61B098FBE1F5EF45CAF410FCC0419491C7E2B1085B1AB9217CFD58`. Presentation no referencia activos de Opción 07.

Después de la primera revisión visual, el usuario amplió expresamente el alcance para incluir pantalla completa. F11 y el botón del header entran y salen del modo sin bordes; Esc sale cuando no hay un flujo de caja que deba atender primero.

## Límites

No cambian lógica de negocio, servicios, persistencia, esquema, migraciones, impresión ni recovery. No se implementan instalador, persistencia de imágenes ni fotografías de producto. 1K.2 queda reservada para fotografías reales de Casa Miga asociadas a Product.

## Verificación

Revisión adicional: alias `StaticResource` del diccionario sustituidos por pinceles explícitos, conservando colores centralizados; comprobación de carga del BAML compilado y construcción de MainWindow aprobadas. Smoke directo del acceso en la aplicación: panel verde visible y botón Entrar con texto blanco. El usuario aprobó el resultado visual final después de revisar el shell autenticado y el menú corregido.

- Focal shell/identidad/navegación: 2/2.
- Suite completa: 535/535; 0 fallidas; 0 omitidas.
- Release: 0 errores; 0 advertencias.
- Smoke humano: aprobado el 27/09/2026.

Revisión adicional del menú (27/09/2026): logo ajustado al espacio interior sin ancho fijo que lo recorte; iconos vectoriales por módulo; grupos tipográficos y pestañas internas de Catálogo/Recetas diferenciados. Plantilla propia de navegación con hover suave y sin cambio de aspecto al deshabilitarse durante cargas. Contenedor de contenido limitado al ancho disponible y encabezado en dos filas para evitar recortes.

Se inspeccionaron Caja, Catálogo, Inventario, Recetas, Producción e Historial en la sesión de prueba, sin registrar operaciones. Después de corregir se renderizó el shell WPF a 960 × 820 y 1280 × 820 y se revisaron ambas imágenes. `verify-theme.ps1` comprueba texto blanco, pestaña seleccionada verde/blanca y estabilidad del color del menú al deshabilitarse. Los permisos permanecen cubiertos por la suite existente; este cierre no afirma operaciones reales ni smoke transaccional de todos los roles.
