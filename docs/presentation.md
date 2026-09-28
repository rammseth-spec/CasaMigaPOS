# Presentation vigente

WPF conserva el shell 1K.1, fullscreen F11/Escape y el maestro visual Variante 04. El acceso se centra según la altura disponible y mantiene scroll de respaldo.

FASE 1K.2C integra fotografías exclusivamente mediante `IProductPhotoUiGateway` sobre `ProductPhotoService`. POS usa tarjetas de 194 × 292 DIP, imagen 1:1 de 168 DIP, nombre hasta dos líneas, categoría y precio. La tarjeta completa es Button, por lo que mouse, touch, Enter y Space usan el handler `ProductClick` existente. El carrito permanece en columna fija y el catálogo pagina de 50.

La caché visual es LRU de 64 MiB de píxeles; las imágenes se decodifican con `OnLoad`, se congelan y no mantienen streams. Cuatro cargas concurrentes evitan bloquear el dispatcher. Replace/Remove invalida por Product.

Catálogo muestra foto/procedencia sólo en editor administrativo. `PhotoCropDialog` propone square centrado y permite X/Y/lado. La selección de archivos ocurre en UI; escritura, metadata, autorización y CAS permanecen en Application/Infrastructure 1K.2B.

Validación responsive requerida: ventana 960×820 (una columna de cards junto al carrito), 1280×820 (dos o más según DPI) y 1920×1080 (varias cards). El WrapPanel no se declara virtualizado; paginación, carga async y caché acotan trabajo.
