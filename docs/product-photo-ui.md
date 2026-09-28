# UI de fotografía de producto — FASE 1K.2C

## POS

El POS presenta `ProductCard` completa y accionable por mouse, touch y teclado. Cada tarjeta obtiene nombre, categoría y precio desde `Product`; la imagen nunca aporta datos comerciales. Usa el thumbnail JPEG vigente de 1K.2B en aspecto 1:1. Sin foto o ante archivo faltante/corrupto muestra un placeholder neutral con nombre y precio. El cajero no ve `PROVISIONAL/REAL`.

Categoría y búsqueda nombre/SKU conviven con la paginación existente de 50. “Todos” no obliga a filtrar. Hacer click o activar la tarjeta con teclado reutiliza `ProductClick → PosViewModel.AddAsync → PosService.Add`; carrito, snapshot de precio y checkout permanecen intactos. El POS sólo consulta productos activos; la tarjeta también expone estado deshabilitado para impedir activación accidental si recibe uno inactivo.

## Carga y caché

`ProductPhotoUiGateway` llama `ProductPhotoService.Current/ReadThumbnail`; no conoce SQLite ni rutas. Carga hasta cuatro thumbnails en paralelo fuera del hilo UI. `BitmapImage` usa `OnLoad`, se congela y el stream se cierra antes de publicar la imagen. La caché LRU utiliza clave `PhotoId:ThumbnailSha256`, contabiliza `ancho × alto × 4` y no excede 64 MiB. Replace/Remove invalida todas las entradas del Product antes de recargar. Navegar de vuelta puede reutilizar la imagen vigente sin retener archivos abiertos.

Un thumbnail ausente/corrupto produce fallback y conserva revisión/PhotoId en administración, de modo que el usuario autorizado aún puede reemplazar o quitar la asociación.

## Catálogo administrativo

Al editar un Product se muestra foto actual y estado discreto: Foto real, Foto provisional, Sin foto o Foto no disponible. `products.edit` gobierna Agregar/cambiar/quitar; Presentation no amplía permisos. Un Product nuevo debe guardarse antes de asociar foto.

Agregar/cambiar abre selector JPEG/PNG, luego un diálogo de crop cuadrado. El default es el mayor cuadrado centrado; sliders permiten mover X/Y y ajustar lado, mínimo 256 px. Confirmar llama Import/Replace del servicio con expected revision y OperationId nuevo. Cancelar no modifica nada. Quitar solicita confirmación y llama Remove; conserva Product, precio e historia. Error precommit conserva la foto anterior.

El operador elige Foto real o Foto provisional. El paquete autorizado v2 sólo se cargó como PROVISIONAL; no se deriva procedencia desde nombres de archivo.

## Dataset y revisión

`tools/CasaMiga.Phase1K2CSmoke` exige carpeta nueva, compara CSV/JSON exactamente y crea productos/categorías mediante `CatalogService`. Importa por `ProductPhotoService`, abre una caja sintética en cero y nunca toca la instalación predeterminada. Dataset: `work/phase1k2c-smoke-20260928-v2`, usuario `catalog-admin`; la contraseña se entrega fuera del repositorio.

Mapa revisable: `phase1k2c-provisional-map.md`. Resultado: 26 Products, 4 categorías, 4 PROVISIONAL, 0 REAL y 22 fallback. Los mosaicos no se usaron porque la región limpia de cada tarjeta queda bajo el mínimo de 256 px o arriesga texto/contenido vecino.

## Límites

No se modifican migration 016, backup v2, restore, storage root, provenance, hashes, recovery, permisos ni reglas comerciales. No hay virtualización declarada: se conserva paginación de 50 y WrapPanel; para 50–100 productos el coste de imagen está limitado por carga concurrente, caché y página. El smoke visual fue aceptado por el usuario al solicitar el reporte final el 28/09/2026. La instalación comercial no fue poblada.
