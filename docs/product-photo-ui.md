# UI de fotografía de producto — FASE 1K.2C

## POS

El POS presenta `ProductCard` completa y accionable por mouse, touch y teclado. Cada tarjeta obtiene nombre y precio desde `Product`; la imagen nunca aporta datos comerciales. Usa el thumbnail JPEG vigente de 1K.2B en aspecto 1:1. La categoría se elige arriba y no se repite en cada card. Sin foto o ante archivo faltante/corrupto muestra un placeholder neutral con nombre y precio. El cajero no ve `PROVISIONAL/REAL`.

Categoría y búsqueda compacta por nombre/SKU conviven con la paginación existente de 50. “Todos” no obliga a filtrar. Hacer click o activar la tarjeta con teclado reutiliza `ProductClick → PosViewModel.AddAsync → PosService.Add`; carrito y snapshot de precio permanecen intactos. Al entrar, buscar, cambiar categoría o página, el catálogo vuelve al inicio. El layout reserva aproximadamente 70% al catálogo y 30% al carrito, con rail de navegación de 84 DIP expandible.

El diálogo de cobro ofrece `Cobrar` y `Cobrar + imprimir ticket`. Ambos llaman el mismo CheckoutService y comparten `Busy`; la venta, partidas y pago se confirman antes de cualquier impresión y siempre quedan en Historial. La primera ruta no invoca ReceiptService. La segunda invoca el pipeline existente después del commit. Un fallo de impresión conserva la venta y dirige a reimpresión desde Historial.

## Carga y caché

`ProductPhotoUiGateway` llama `ProductPhotoService.Current/ReadThumbnail`; no conoce SQLite ni rutas. Carga hasta cuatro thumbnails en paralelo fuera del hilo UI. `BitmapImage` usa `OnLoad`, se congela y el stream se cierra antes de publicar la imagen. La caché LRU utiliza clave `PhotoId:ThumbnailSha256`, contabiliza `ancho × alto × 4` y no excede 64 MiB. Replace/Remove invalida todas las entradas del Product antes de recargar. Navegar de vuelta puede reutilizar la imagen vigente sin retener archivos abiertos.

Un thumbnail ausente/corrupto produce fallback y conserva revisión/PhotoId en administración, de modo que el usuario autorizado aún puede reemplazar o quitar la asociación.

## Catálogo administrativo

Al editar un Product se muestra la sección `IMAGEN DEL PRODUCTO`, preview de 240×240 y estado explícito: Foto real, Foto provisional, Sin foto o Foto no disponible. `products.edit` gobierna Agregar/Cambiar/Quitar; Presentation no amplía permisos. Un Product nuevo debe guardarse antes de asociar foto.

Agregar/cambiar abre selector JPEG/PNG, luego un diálogo de crop cuadrado. El default es el mayor cuadrado centrado; el usuario arrastra el cuadro y su esquina inferior derecha para mover/redimensionar. Las coordenadas quedan en un panel avanzado secundario. El mínimo sigue siendo 256 px. Confirmar llama Import/Replace del servicio con expected revision y OperationId nuevo. Cancelar no modifica nada. Quitar solicita confirmación y llama Remove; conserva Product, precio e historia. Error precommit conserva la foto anterior.

El operador elige Foto real o Foto provisional. El paquete autorizado v2 sólo se cargó como PROVISIONAL; no se deriva procedencia desde nombres de archivo.

## Dataset y revisión

`tools/CasaMiga.Phase1K2CSmoke` exige carpetas nuevas, compara CSV/JSON exactamente, prepara 26 PNG de 512×512 con crop/resize técnico y crea productos/categorías mediante `CatalogService`. Importa por `ProductPhotoService`, abre una caja sintética en cero y nunca toca la instalación predeterminada. Dataset: `work/phase1k2c-r1-smoke-20260928`, usuario `catalog-admin`; la contraseña se entrega fuera del repositorio.

Mapa revisable: `phase1k2c-provisional-map.md`. Resultado: 26 Products, 4 categorías, 26 PROVISIONAL, 0 REAL y 0 fallback. La preparación ocurre antes del pipeline interactivo y no relaja ProductPhoto Foundation.

## Límites

No se modifican migration 016, backup v2, restore, storage root, provenance, hashes, recovery, permisos ni reglas comerciales. No hay virtualización declarada: se conserva paginación de 50 y WrapPanel; para 50–100 productos el coste de imagen está limitado por carga concurrente, caché y página. La revisión humana del smoke R1 permanece pendiente; no se declara aprobación visual. La instalación comercial no fue poblada.
