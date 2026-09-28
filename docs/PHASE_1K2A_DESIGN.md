# CASA MIGA POS — DISEÑO FASE 1K.2A

Fecha: 27/09/2026. Estado: análisis y propuesta terminados, **pendiente autorización de implementación**. FASE 1K abierta; 1K.1 cerrada; 1K.2 en diseño, no implementada; 1K.2B no iniciada.

Modelo utilizado: Codex basado en GPT-6; la sesión no expone un identificador verificable de variante. La autorización recomienda Astra; no se afirma un cambio de modelo realizado por herramientas.

HEAD inicial y final de este trabajo: `58bdcbd34692e14ced3f85fdeb82fca5d6b8b794`. Schema actual 15. No se crea 016, no se modifica producto ni se aplica DDL. Los bloques SQL de este documento son una propuesta. Se deja documentación y una prueba técnica no productiva sin commit para respetar «NO hacer commit de una arquitectura no autorizada». Identidad repo-local comprobada: Rammseth / rammstudioworkshop@gmail.com. Sin push ni remoto nuevo.

## 1. Recomendación ejecutiva

Usar **ProductPhoto**, una foto principal opcional por Product, con originales y recortes inmutables por revisión y un puntero actual transaccional. El derivado puede repararse con publicación de archivo nuevo y metadata auditada. Guardar los bytes fuera de SQLite, dentro del directorio de datos de la instalación; SQLite guarda asociación, hashes, dimensiones, recorte, versión del procesamiento y auditoría. Una nueva migración 016 **sí es necesaria para esta arquitectura**, pero se propone únicamente.

Conservar cada original confirmado byte por byte. Crear un único derivado cuadrado JPEG Q85, de hasta 512 × 512, para POS. Aceptar inicialmente originales JPEG/PNG. La elección 512/Q85 es recomendación de ingeniería sustentada por área de card, memoria y la prueba aislada; la aceptación de nitidez fotográfica requiere muestras reales. No persistir una tercera resolución de preview en V1.

Evolucionar `.cmbackup` a manifiesto v2 con DB, terminal y todos los archivos registrados, incluidos originales retirados. Conservar lectura de paquetes v1. Publicar archivos antes del commit de su referencia; nunca sobrescribir una foto vigente. Caché de bitmaps con presupuesto, carga visible y paginación de 50 existente. ProductCard llama exactamente al agregado vigente.

## 2. Arquitectura actual inspeccionada

| Fuente actual | Hallazgo | Consecuencia |
|---|---|---|
| `Domain/Catalog.cs`, `migrations/001_foundation.sql`, `004_catalog.sql` | Product tiene GUID, CategoryId, precio Money, Active; no foto. Productos se desactivan, no se borran. | Asociación por Product.Id, independiente de receta/insumo; sin precio duplicado. |
| `Application/CatalogService.cs`, `Infrastructure/SqliteCatalogStore.cs` | Listado ProductRow con CategoryName; búsqueda nombre/SKU normalizada; página de 50; ambos activos para POS. | Conservar filtros/orden y cargar metadata de fotos por lote de IDs. |
| `Application/PosService.cs`, `Presentation/PosView.xaml.cs` | ProductClick → AddAsync → PosService.Add → Cart.Add; revalida actor, permisos, caja, producto y categoría. | No escribir otro cart ni checkout. Foto fallida no cambia disponibilidad comercial. |
| `Presentation/PosView.xaml` | ItemsControl/WrapPanel dentro de ScrollViewer, carrito fijo 410 DIP. | Hoy no hay virtualización real; 50 cards acotadas. Ajustar sólo layout interno del POS futuro. |
| `CatalogViewModel.cs`, migración 004 | `products.edit` existe para Administrator y Manager; Cashier sólo consulta. | Cambiar/quitar foto reutiliza products.edit; recipe.configure no aplica. |
| `FoundationOptions`, `App.xaml.cs`, `TerminalConfiguration.cs` | Raíz local configurable y DirectoryLease exclusivo durante la vida de la app. | La app y CLI no operan la misma instalación a la vez; añadir coordinación interna de media, no un segundo lock global. |
| `InstallationBackup.cs`, ADR-007 | ZIP de exactamente 3 entradas, manifest v1, snapshot SQLite y publicación a carpeta nueva en restore. | No se pueden añadir fotos al ZIP actual sin cambiar contrato/lector. |
| `FoundationRuntime.Start`, `Database.Initialize` | Backup antes y después de migrar; validación, historial y hashes; DDL en transacción. | Proteger downgrade, respaldos incompletos y arranque con media perdida; ver sección 9. |
| `SqliteLocalUnitOfWork.RunTechnical` | Permite usar Store de permisos/auditoría y SQL técnico en la misma transacción. | Puerto específico de fotos puede reutilizarlo sin ampliar reglas de checkout. |

Capas futuras: Application declara DTO/puertos y ProductPhotoService; Infrastructure implementa metadata SQLite y almacén de archivos; Presentation implementa el codec WPF y UI. Domain/Product permanece sin dependencia de WPF/archivos y no necesita alterarse. La composición actual está en MainViewModel/Presentation. Maintenance puede respaldar bytes y validar hashes sin cargar WPF.

## 3. Identidad, relación y ubicación física

No habrá DAM genérico, galería, fotos de Formula, FormulaVersion, ProductionProfile ni InventoryItem. Cada importación confirmada crea PhotoId GUID propio, perteneciente a un Product. La foto anterior se conserva como revisión retirada; el POS sólo consulta la actual. Una misma foto de origen en dos productos crea dos activos, sin deduplicación compartida que complique borrado/propiedad.

```text
<FoundationOptions.DataDirectory>/
  casamiga.db
  terminal.json
  product-images/
    v1/
      <photoId en GUID N minúsculas>/
        original.jpg             # o original.png según tipo real validado
        pos-v1-<sha256>.jpg      # cuadrado JPEG; nombre derivado del hash registrado
    .staging/<operationId>/      # nunca visible ni incluido en backup completo
    .quarantine/<operationId>/   # fallos/no confirmados; nunca borrado de activos vivos
  backups/*.cmbackup
```

Se prefiere un bundle por PhotoId a carpetas Originals/Thumbnails separadas: permite mover el directorio completo y verificar una unidad de publicación. No guardar rutas absolutas, rutas elegidas por el operador ni URI. La ruta relativa se **deriva**, no se acepta como entrada: PhotoId + versión de layout + formato enumerado. Nombre original personal no necesario en la DB ni en logs.

En DB usar GUID D por compatibilidad existente; convertir a GUID N únicamente para el directorio. Resolver con GetFullPath bajo raíz conocida, separación final y comparación Windows ordinal sin distinguir mayúsculas; comprobar también reparse points en cada ancestro. Rechazar UNC, rutas de dispositivo, ADS, enlaces/junctions y rutas que escapen; no confiar sólo en StartsWith. Probar raíces con espacios/Unicode y longitudes altas. La fuente se copia, nunca se mueve ni se borra de la carpeta personal.

## 4. Metadata y propuesta exacta de schema 016

Dos tablas nuevas. `product_photo_asset` guarda originales/recortes confirmados inmutables y el descriptor reparable del thumbnail; `product_photo` guarda la selección actual y un contador monotónico, incluso cuando se quita la foto. Esto evita el problema ABA (quitar/agregar/quitar y aceptar una edición obsoleta). Foto principal 0..1, originales retenidos 0..N; no cambia la tabla product ni sus constructores.

DDL propuesto, **NO aplicado ni creado como archivo de migración**:

```sql
CREATE TABLE product_photo_asset (
  id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
  product_id TEXT NOT NULL REFERENCES product(id) ON DELETE RESTRICT,
  layout_version INTEGER NOT NULL CHECK(layout_version=1),
  original_format TEXT NOT NULL CHECK(original_format IN ('jpeg','png')),
  original_sha256 TEXT NOT NULL CHECK(length(original_sha256)=64 AND original_sha256 NOT GLOB '*[^0-9A-F]*'),
  original_bytes INTEGER NOT NULL CHECK(original_bytes BETWEEN 1 AND 26214400),
  original_width INTEGER NOT NULL CHECK(original_width BETWEEN 1 AND 12000),
  original_height INTEGER NOT NULL CHECK(original_height BETWEEN 1 AND 12000),
  orientation INTEGER NOT NULL CHECK(orientation BETWEEN 1 AND 8),
  crop_x INTEGER NOT NULL CHECK(crop_x>=0),
  crop_y INTEGER NOT NULL CHECK(crop_y>=0),
  crop_edge INTEGER NOT NULL CHECK(crop_edge>=256),
  pipeline_version INTEGER NOT NULL CHECK(pipeline_version=1),
  thumbnail_sha256 TEXT NOT NULL CHECK(length(thumbnail_sha256)=64 AND thumbnail_sha256 NOT GLOB '*[^0-9A-F]*'),
  thumbnail_bytes INTEGER NOT NULL CHECK(thumbnail_bytes BETWEEN 1 AND 2097152),
  thumbnail_edge INTEGER NOT NULL CHECK(thumbnail_edge BETWEEN 256 AND 512),
  jpeg_quality INTEGER NOT NULL CHECK(jpeg_quality=85),
  created_utc TEXT NOT NULL,
  created_by TEXT NOT NULL REFERENCES app_user(id) ON DELETE RESTRICT,
  terminal_id TEXT NOT NULL REFERENCES terminal(id) ON DELETE RESTRICT,
  UNIQUE(product_id,id),
  CHECK(original_width*original_height<=48000000),
  CHECK(crop_x+crop_edge<=CASE WHEN orientation IN (5,6,7,8) THEN original_height ELSE original_width END),
  CHECK(crop_y+crop_edge<=CASE WHEN orientation IN (5,6,7,8) THEN original_width ELSE original_height END),
  CHECK(thumbnail_edge=CASE WHEN crop_edge<512 THEN crop_edge ELSE 512 END)
) STRICT;

CREATE TABLE product_photo (
  product_id TEXT PRIMARY KEY NOT NULL REFERENCES product(id) ON DELETE RESTRICT,
  current_photo_id TEXT,
  revision INTEGER NOT NULL CHECK(revision>=1),
  last_operation_id TEXT NOT NULL UNIQUE CHECK(length(last_operation_id)=36),
  modified_utc TEXT NOT NULL,
  modified_by TEXT NOT NULL REFERENCES app_user(id) ON DELETE RESTRICT,
  terminal_id TEXT NOT NULL REFERENCES terminal(id) ON DELETE RESTRICT,
  FOREIGN KEY(product_id,current_photo_id)
    REFERENCES product_photo_asset(product_id,id) ON DELETE RESTRICT
) STRICT;

CREATE TRIGGER product_photo_asset_original_immutable BEFORE UPDATE ON product_photo_asset
WHEN NEW.id<>OLD.id OR NEW.product_id<>OLD.product_id
  OR NEW.layout_version<>OLD.layout_version OR NEW.original_format<>OLD.original_format
  OR NEW.original_sha256<>OLD.original_sha256 OR NEW.original_bytes<>OLD.original_bytes
  OR NEW.original_width<>OLD.original_width OR NEW.original_height<>OLD.original_height
  OR NEW.orientation<>OLD.orientation OR NEW.crop_x<>OLD.crop_x OR NEW.crop_y<>OLD.crop_y
  OR NEW.crop_edge<>OLD.crop_edge OR NEW.pipeline_version<>OLD.pipeline_version
  OR NEW.thumbnail_edge<>OLD.thumbnail_edge OR NEW.jpeg_quality<>OLD.jpeg_quality
  OR NEW.created_utc<>OLD.created_utc OR NEW.created_by<>OLD.created_by
  OR NEW.terminal_id<>OLD.terminal_id
BEGIN SELECT RAISE(ABORT,'Original and crop revision are immutable'); END;
CREATE TRIGGER product_photo_asset_no_delete BEFORE DELETE ON product_photo_asset
BEGIN SELECT RAISE(ABORT,'Photo original retention requires explicit policy'); END;
CREATE TRIGGER product_photo_no_delete BEFORE DELETE ON product_photo
BEGIN SELECT RAISE(ABORT,'Clear the current photo and advance revision'); END;
CREATE TRIGGER product_photo_revision_guard BEFORE UPDATE ON product_photo
WHEN NEW.product_id<>OLD.product_id OR NEW.revision<>OLD.revision+1
BEGIN SELECT RAISE(ABORT,'Invalid photo revision transition'); END;
```

Índices: PK/UNIQUE anteriores cubren búsqueda por producto/PhotoId, propiedad compuesta y OperationId actual. No índice por hash ni paths (no hay búsqueda/deduplicación por ellos); el índice UNIQUE(product_id,id) también sirve para revisiones del producto. No seeds ni permisos nuevos. Un Product sin fila equivale a revision=0 y foto ausente. `current_photo_id=NULL` deja el producto intacto y mantiene la revisión.

Application valida GUID no vacíos/canónicos, UTC, límite checked del contador, correspondencia archivo/metadata y actor. SQLite valida tipos, rangos, propietario y FKs; no puede garantizar existencia de archivos. El hash no autentica frente a alteraciones coordinadas de DB/archivos por un atacante local.

Auditoría existente: `product.photo.replace` / `product.photo.remove`, entity_id=ProductId, con operationId, expectedRevision, resultingRevision, beforePhotoId/afterPhotoId y hashes; actor/terminal/UTC en las columnas existentes. Insertar junto con puntero y asset en la **misma** transacción. Reparar derivado usa `product.photo.thumbnail.repair`, PhotoId y hashes anterior/nuevo; sólo puede modificar thumbnail_sha256/thumbnail_bytes, con CAS sobre hash anterior y auditoría atómica. No almacenar precio, nombre del pan, categoría o bytes en metadata de imagen.

Upgrade: respaldo completo v1 de schema15 antes de 016; crear tablas/triggers sin tocar productos; todos arrancan sin foto; verificar objetos y foreign_key_check antes de registrar hash016. Si falla, rollback por el mecanismo existente, versión15 y backup preservados. No down-migration manual: para volver al binario anterior restaurar paquete anterior en otra carpeta, aceptando que no contiene operaciones posteriores. Binarios schema15 rechazan DB16 y lector v1 rechaza paquetes v2. Restaurar v1 con la aplicación futura valida primero el candidato y luego migra a16, sin inventar fotos.

## 5. Formatos, tamaño y evidencia

| Alternativa | Evaluación | Decisión |
|---|---|---|
| JPEG | Encoder/decoder nativos WPF/WIC, calidad configurable, adecuado a fotos; con pérdida. | Original JPEG permitido, derivado JPEG Q85. |
| PNG | Nativo, sin pérdida, puede incluir alfa; para fotos puede costar más disco. | Original PNG permitido; alfa del derivado se compone sobre Fondo Natural. |
| WebP | No forma parte del conjunto nativo WIC documentado como JPEG/PNG; depender de extensión/codec instalado no es reproducible entre equipos. | No import/export en V1; reevaluar sólo con biblioteca/licencia/despliegue autorizados y benchmark. |

Microsoft documenta los [codecs nativos WIC](https://learn.microsoft.com/windows/win32/wic/-wic-about-windows-imaging-codec) y el [encoder JPEG con QualityLevel](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.jpegbitmapencoder.qualitylevel?view=windowsdesktop-10.0). Se propone evitar dependencias de codecs de Store en Windows LTSC.

Se ejecutó la prueba reproducible `docs/evidence/phase1k2a-image-probe.ps1`; metodología y todas las cifras en `phase1k2a-image-probe-results.md`. Es una carta de calibración, nunca foto de un producto. Resultados locales JPEG: 512 = 44,809 bytes, mediana 1.647 ms; 768 = 94,549 bytes, mediana 2.879 ms. Memoria BGRA32 teórica: 1 MiB frente a 2.25 MiB por bitmap. PNG fue menor/más rápido en la carta de colores planos: no extrapolar compresión a pan real. No se midió WebP ni disco frío.

Área de imagen propuesta <=232 DIP: a 200% son 464 px, cubiertos por 512. 768 aumenta 2.25 veces los píxeles y sólo aporta detalle útil si el área/DPI lo pide. Seleccionar **un derivado de lado min(512,crop_edge)**, sin ampliar un original pequeño. A 300% una foto de 232 DIP necesita 696 px: falta validar en ese escenario; no se promete nitidez nativa allí. Antes de fijar política final, comparar 3–5 fotos reales en 512/768 a 100/150/200% y, si el equipo lo requiere, 300%. Q85 es objetivo inicial, no medición de aceptación del pan.

| Imágenes retenidas (BGRA32) | 512 | 768 |
|---:|---:|---:|
| 30 | 30MiB | 67.5MiB |
| 50 | 50MiB | 112.5MiB |
| 100 | 100MiB | 225MiB |

Estos son bytes de píxeles, no RAM total/GPU del proceso. Lotes de decodificación secuencial caliente: 30/50/100 a 512 = 64.65/96.93/195.59 ms; a 768 = 93.84/168.52/325.97 ms. No son tiempos garantizados del POS ni un catálogo de 100 fotografías distintas.

## 6. Importación, recorte y publicación

1. Catálogo selecciona Product persistido; comprobar `products.edit`, actor activo y terminal. Para producto nuevo: guardarlo primero, después «Cambiar foto»; no crear un Product temporal para asociar imagen.
2. Elegir archivo local JPEG/PNG, confirmar Product mostrado. Copiar mediante lectura secuencial limitada a staging propio y calcular SHA-256 durante la copia; no conservar dependencia de la ruta personal. Un cambio en la fuente durante la copia se evita con handle sin compartir escritura/borrado; se procesa la copia exacta.
3. Validar tamaño<=25MiB, firma, decoder real y concordancia extensión (.jpg/.jpeg/.png). Rechazar cero bytes, corruptos, formato discrepante, animados/APNG, varios frames y formatos no admitidos (HEIC/WebP/TIFF/SVG/GIF). Comprobar chunks/APNG, no confiar sólo en FrameCount. Dimensiones por lado<=12,000 y total<=48MP antes de pedir el raster completo; crop>=256. Archivos pequeños admitidos con aviso si crop<512.
4. Procesar en un worker STA de imágenes, máximo una importación a la vez y fuera del Dispatcher. Validar encabezados antes de reservar grandes buffers; preview decodificado a máximo1,024px. El límite48MP permite fotos habituales de48MP y acota un planoBGRA a192MB; es presupuesto, no protección perfecta ante codecs. Si la prueba en equipo objetivo no cumple memoria, reducir límite con mensaje explícito, no ignorarlo.
5. Presentar original orientado correctamente, con crop cuadrado centrado como sugerencia, arrastrable y ampliable dentro de sus límites. Guardar crop_x/y/edge enteros en coordenadas del original **después** de EXIF1–8 (incluidos reflejos), nunca en DIP. Preview informa producto y resultado; cancelar borra sólo staging propio y no cambia asociación.
6. Generar desde original validado: orientación → conversión de perfil a sRGB → crop → escala uniforme → BGRA/RGB8 opaco → JPEG Q85. Para PNG transparente componer sobre#FDF8EF (pipelinev1); eliminar EXIF/GPS del derivado, original conserva todos sus bytes/metadata. Perfil corrupto/no soportado: rechazar con mensaje, no alterar colores silenciosamente. Sin filtros estéticos, sharpen creativo, IA, quitar toppings ni deformación.
7. Reabrir derivado, verificar JPEG/dimensiones/límites y calcular hash. Una imagen transformada WPF puede retener el original a través de su grafo: materializar un buffer pequeño independiente antes de salir del worker; liberar stream, decoder y buffers grandes. No cachear TransformedBitmap enlazado al original.
8. Cerrar/flush de archivos; bajo DirectoryLease existente y gate interno de media, mover bundle staging a ruta final **nueva**, mismo volumen. No overwrite, no rutas proporcionadas por usuario, no links. Los archivos están listos antes de publicar su referencia.
9. Transacción breve con autorización nuevamente y CAS de expectedRevision: insertar asset, upsert puntero (sólo si revisión esperada coincide), auditoría y commit. No decodificar ni escribirMB dentro de BEGIN IMMEDIATE. Si se revocó permiso, el producto desapareció o cambió revisión, rollback y conservar foto anterior.
10. Confirmar resultado consultando por OperationId; después notificar a UI e invalidar la selección de caché. Sólo el commit exitoso cambia la foto visible. El staging se limpia; el asset anterior confirmado queda retenido.

No existe transacción ACID conjunta entre SQLite y filesystem. La publicación previa e inmutable evita punteros a archivos a medio escribir en fallos de proceso normales; flush/rename no prometen atomicidad ante cualquier corte eléctrico. Al reabrir, verificar presencia/hash y aplicar recuperación explícita. Ningún fallo de media modifica Cart, Sale, caja o inventario.

## 7. Reemplazo, quitar foto y fallos intermedios

`Replace(ProductId, ExpectedRevision, OperationId, PreparedBundle)` y `Remove(ProductId, ExpectedRevision, OperationId)` son casos de uso separados. OperationId se conserva en UI hasta resolver el resultado; PhotoId=OperationId para importación. Doble click no crea dos revisiones. A igual OperationId, comparar payload/actor/Producto; payload distinto rechaza. QueryOutcome consulta asset/auditoría durable aun si otra edición ya fue posterior; no basta comprobar que sigue siendo la foto actual. Para quitar, guardar OperationId como ID de audit_entry y en puntero, permitiendo consulta por PK existente sin nueva tabla de operaciones. Para reemplazar usar la misma política de ID de auditoría.

| Punto de fallo | Estado y recuperación |
|---|---|
| Validación/codec/cancelación | Original externo intacto; DB y foto actual intactos; staging descartable. |
| Disco lleno/denegación al escribir o mover | No commit; no mostrar éxito. Liberar únicamente temporales de esta operación. |
| Bundle final movido, DB aún no confirmada | Directorio sin fila asset; no visible. Reconciliar con DB antes de mover a quarantine. |
| Rollback confirmado | Foto anterior continúa; activo no confirmado puede retirarse a quarantine. |
| Excepción con commit incierto | No borrar bundle ni iniciar otra intención; QueryOutcome por OperationId. Si DB no se puede leer, conservar ambos y mostrar «verificando cambio». |
| Commit correcto, falla refresco/notificación | Leer puntero al volver a pantalla/reiniciar; PhotoId nuevo evita bitmap viejo. Reintento con la misma intención no duplica cambio. |
| Product inactivo | Se permite gestionar foto con products.edit, pero sigue excluido de venta. |
| Producto inexistente | Rechazo antes y dentro de transacción; FKs impiden referencia huérfana. |

Quitar foto pone current_photo_id=NULL e incrementa revision, con auditoría; no borra Product, original ni derivado confirmado. La interfaz explica «se quitará del producto; el original se conserva». Reemplazar/recortar otra vez crea una revisión nueva; no overwrite. No se construye galería/restauración histórica completa en V1.

Retención: **no eliminación automática de originales confirmados**, ni por reemplazo ni por quitar foto. El backup incluye revisiones retiradas. Límite operativo inicial propuesto:5GiB de media registrada; al alcanzarlo rechazar nuevas importaciones con explicación, sin borrar originales. El usuario puede autorizar posteriormente política de purga/exportación; requiere revisar los triggers propuestos. Huérfanos nunca confirmados: reconciliación bajo lease/gate, cuarentena7d y limpieza de esos temporales después; nunca inferir que un archivo confirmado es basura porque no sea la foto actual. No borrar un bundle si la DB no puede verificarse.

## 8. Integridad y reparación

Al importar, al crear respaldo y al restaurar: hash completo y límites. En el primer uso por sesión: tamaño/hash del derivado antes de decodificar; caché de validación por PhotoId/hash/longitud/fecha local. Una verificación de originales más costosa ocurre en mantenimiento/backup, no en cada card. El sistema no trata las fechas del filesystem como prueba criptográfica.

| Caso | POS | Gestión/recuperación |
|---|---|---|
| Sin asociación | Fallback neutro, nombre/precio | «Agregar foto». |
| Derivado ausente/corrupto/hash distinto | Fallback inmediato, no excepción sin observar | Verificar original; regenerar en staging. Si bytes/hash coinciden, reponer archivo. Si difieren, publicar nuevo archivo nombrado por hash y actualizar sólo descriptor de thumbnail con CAS/auditoría. El original, crop y PhotoId no cambian. |
| Original perdido o hash incorrecto | Puede seguir mostrando derivado validado | Estado de integridad degradada; recuperar byte exacto desde backup o importar nueva foto; no presentar thumbnail como original. |
| Asset sin puntero actual | Nada cambia | Revisión retirada válida; conservar e incluir en backup. |
| Bundle sin asset DB | Nunca mostrarlo | Reconciliar/quarantine, sólo tras resolver resultado de commit. |
| FK incorrecta/DB corrupta | Seguir política actual de integridad DB | No «reparar» base desde fotos ni ocultar fallo de DB. |

La reparación de derivados también funciona para revisiones retiradas, para que su retención no impida recuperar un backup completo. Bajo gate: publicar nuevo archivo completo antes de actualizar su hash/longitud en DB; si el commit es incierto, consultar auditoría por OperationId antes de limpiar. El archivo anterior se retira sólo después de confirmar metadata y ninguna referencia/backup en curso. La caché usa hash y se invalida. Nunca actualizar un hash para aceptar bytes corruptos: el candidato debe regenerarse del original cuyo hash sí coincide y conservar el pipeline/crop registrado.

Los hashes detectan corrupción, no garantizan autenticidad. Logs técnicos con códigos y IDs, sin rutas personales, contraseñas, EXIF completo ni bytes. En POS los errores fotográficos muestran fallback; detalles técnicos quedan en Catálogo/diagnóstico.

## 9. Backup, arranque y restore

### Backup v2 propuesto

Seguir SQLite Backup API y copia de terminal validada. El snapshot de SQLite sólo cubre DB; su asociación con archivos externos es responsabilidad de esta arquitectura, conforme a la [API oficial de respaldo](https://www.sqlite.org/backup.html).

1. DirectoryLease exclusivo de instalación (ya existente); además gate interno para publicación/purga/backup. Importación puede preparar staging sin mantener el gate. Orden fijo: gate de media → transacción DB; nunca inverso. Hoy CLI sólo ejecuta con app cerrada.
2. Tomar snapshot DB mediante BackupDatabase, copiar terminal y validar pareja. Enumerar asset rows **del snapshot**, no del catálogo vivo posterior.
3. Copiar originales y derivados registrados de todos los assets, actuales y retirados; verificar SHA/longitud conforme a snapshot. Gate impide reparar/publicar/purgar mientras se captura y copia. Archivos son inmutables por nombre; excluir derivados viejos no referenciados, staging, quarantine, caches, logs, configuración de desarrollo y backup anidado.
4. Crear manifestv2, ZIP `.partial`, luego rename final. Archivos JPEG/PNG ya comprimidos pueden usar NoCompression; DB puede comprimirse. Comprobar espacio libre con margen para snapshot+paquete. No introducir backups incrementales V1.

Esquema de manifiesto propuesto (nombres nuevos todavía no implementados):

```json
{
  "Version": 2,
  "TerminalId": "<guid>",
  "SchemaVersion": 16,
  "CreatedUtc": "<UTC ISO8601>",
  "MediaLayoutVersion": 1,
  "Files": [
    { "Path": "casamiga.db", "Length": 0, "Sha256": "<64 hex>" },
    { "Path": "terminal.json", "Length": 0, "Sha256": "<64 hex>" },
    { "Path": "product-images/v1/<photoIdN>/original.jpg", "Length": 0, "Sha256": "<64 hex>" },
    { "Path": "product-images/v1/<photoIdN>/pos-v1-<sha256>.jpg", "Length": 0, "Sha256": "<64 hex>" }
  ]
}
```

Los ceros son marcadores del ejemplo; archivos reales deben tener longitudes válidas. El manifiesto no se incluye a sí mismo ni es firma de autenticación. El conjunto exacto de entradas es manifest + Files, sin duplicados ni rutas adicionales; comparar en restauración contra las rutas derivadas de DB, no confiar sólo en manifest. `SchemaVersion` debe coincidir con historial del snapshot.

Un archivo registrado perdido/corrupto implica backup completo fallido: no publicar `.cmbackup` incompleto. Thumbnail reparable se repara con el flujo autorizado antes de reintentar; original irreparable exige recuperación o sustitución explicitada (la revisión perdida seguirá requiriendo reparación mientras esté retenida). Esto implica que la retención de originales necesita diagnóstico visible y respaldo frecuente.

### Efecto en arranque (decisión explícita)

FoundationRuntime hoy crea backup antes/después de migrar y cualquier excepción aborta. No permitir que una foto externa eliminada inutilice indefinidamente el POS en una instalación ya migrada: proponer una ruta específica para **MediaIntegrityException**, sin capturar errores arbitrarios.

En schema16 ya validado, si falta media, conservar snapshot consistente DB+terminal y un manifiesto de emergencia con lista exacta de archivos ausentes en un paquete distinguible `.cmrecovery` (v2, `Complete=false`, `MissingFiles`). No aparece como backup completo ni entra al lector normal `.cmbackup`. Sólo si esa protección mínima y la integridad DB/terminal se completan se permite iniciar con aviso persistente y fallback fotográfico; `Create` manual de backup completo sigue fallando hasta reparar. Restaurar emergencia exigirá comando explícito `restore-recovery` a carpeta nueva, aceptación de fotos faltantes y aviso hasta reparación. Incluir este pequeño alcance en autorización de1K.2B; no ocultarlo como detalle de UI.

Durante un **upgrade**, exigir backup completo del baseline antes de migrar; no avanzar con backup degradado. Si falla DB, terminal, disco o respaldo mínimo, conservar comportamiento de bloqueo actual. No cambiar recovery de ventas/checkout. Evitar duplicar snapshots post-arranque cuando una futura política lo permita queda fuera de esta subfase; actualmente dos backups completos por arranque pueden costar variosGB y debe medirse con fotos reales.

### Restore y compatibilidad

Leer manifest con límite4MiB antes de extraer; rechazar versión desconocida, JSON duplicado/inválido y ZIP con nombres duplicados incluso por diferencias de mayúsculas. No usar ExtractToDirectory sobre entradas arbitrarias. Validar nombres exactos, GUIDN, / como separador de ZIP, ausencia de `..`, barras iniciales, colon, ADS, rutas drive/UNC, trailing dots, links/reparse y entradas inesperadas. Copiar en streams contados a carpeta temporal hermana, con límites por archivo y total; validar tamaño real expandido, CRC del ZIP y SHA. Propuesta inicial: <=20,003 entradas, <=5GiB media y <=16GiB total expandido (DB incluida), con error accionable si se requiere elevar límites. No basar aceptación sólo en ratio de compresión.

Validar DB/FKs, schema soportado, terminal, todos los archivos esperados y ninguna referencia fuera del paquete. Hash del original se compara; derivado tiene además formato/dimensiones válidos. Maintenance no depende de WPF: un validador de encabezados JPEG/PNG acotado verifica dimensiones/frame/signatura, y la app hace decodificación completa antes de mostrar. Paquete con thumbnail corrupto se rechaza como backup completo; no reparar silenciosamente durante restore alterando su hash.

Después validar/migrar candidato, volver a verificarlo y publicar carpeta completa por Directory.Move en mismo volumen; destino debe ser nuevo. Excepción elimina sólo staging validado, original/backup intactos. No activar dos instalaciones con la misma terminal como cajas independientes.

Compatibilidad: lector nuevo acepta v1 (tres entradas, sin fotos; schema<=15, migra después) y v2 completo. Un v1 que declare schema16/referencias fotográficas se rechaza por incompleto. Lector viejo no soporta v2: fallo claro, nunca fallback parcial. `.db` legado sigue sin ser backup de instalación/fotos. `.cmrecovery` requiere ruta explícita, no aceptación silenciosa como completo.

## 10. Caché, carga diferida y memoria

`ProductCardViewModel` compone ProductRow + descriptor opcional; jamás modifica Product ni añade precio a imagen. Después de cada carga de página, obtener descriptores para hasta50 IDs en **una consulta read-only** autorizada, no N consultas. Las cards y texto aparecen primero; error de media no oculta productos válidos.

Servicio de imágenes de Presentation por instalación: clave `(PhotoId, ThumbnailSha256, DecodeBucket)`; buckets256/384/512 según tamaño físico real y DPI, limitados al archivo disponible. LRU con presupuesto64MiB de píxeles decodificados **incluyendo referencias visibles**, compartido por POS/Catálogo; no limitar sólo entradas de diccionario dejando bitmaps retenidos por VMs. Al salir del viewport liberar Image.Source y referencias de VM fuera de margen; invalidar generación/cancelar tareas al cambiar página, categoría, filtro o sesión. Cola visible+una fila de anticipación, máximo2 decodificaciones concurrentes y12 pendientes; evitar duplicar solicitudes por clave (single flight).

BitmapImage desde StreamSource, CacheOption.OnLoad, DecodePixelWidth antes de EndInit; disponer FileStream al terminar, Freeze si CanFreeze y entregar por Dispatcher. Microsoft documenta que [OnLoad permite cerrar el stream](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapimage.cacheoption?view=windowsdesktop-10.0) y que los [Freezable congelados pueden compartirse entre hilos](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/freezable-objects-overview). No usar caché de UriSource susceptible de servir una imagen anterior. No abrir originales en cada card.

La foto no se decodifica en un converter síncrono. Capturar fallos/cancelación de todas las tareas (la política global mata la app ante fallos no observados). Resultados atrasados verifican ProductId/PhotoId y generation de pantalla antes de asignarse. Reemplazo/quitar notifica asociación y desasocia referencias antiguas; revisar versión de metadata al entrar a POS/Catálogo para recuperarse de notificación perdida. Negative cache de30s para falla fotográfica se invalida inmediatamente al reparar.

En esta escala conservar página SQL50 + WrapPanel; **no afirmar virtualización** por activar propiedades sobre él. La carga diferida usa intersección con viewport coalescida al scroll/resize, con suscripciones desmontadas en Unloaded. 100 productos siguen siendo dos páginas. No construir un VirtualizingWrapPanel nuevo por anticipación: Microsoft distingue [virtualización UI y datos](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls), y un panel normal no la aporta. Si las mediciones de50 cards incumplen objetivos, siguiente opción concreta es lista de filas con VirtualizingStackPanel Recycling y subgrid de cards; justificar antes de agregar dependencia externa.

Presupuesto objetivo:64MiB de píxeles + hasta2 buffers transitorios, medir overhead administrado/nativo/GPU; no es garantía de working set total. Preview único hasta1024px, fuera de caché del POS y liberado al cerrar editor. Un original48MP requiere~183MiB por planoBGRA, con posibles copias; importar uno a la vez y medir pico<512MiB adicionales como aceptación inicial en el equipo objetivo. La prueba1K.2A no certifica ese pico ni ausencia global de fugas.

## 11. ProductCard y POS propuestos

Usar tema1K.1, logo Variante04 y colores centralizados. `#4A5D4E` es equivalencia digital **operativa autorizada**, no HEX oficial publicado por Berel. No rediseñar shell ni ampliar fullscreen.

```text
Buscar nombre o SKU [________________] [Buscar]
[Todas] [Category real] [Category real] ...
┌──────────────────────────────┐ ┌─────────────────┐
│ cards de productos           │ │ Tu carrito      │
│ ┌──────────┐ ┌──────────┐     │ │ líneas actuales │
│ │foto real │ │sin foto  │     │ │                 │
│ │cuadrada  │ │neutra    │     │ │ total           │
│ ├──────────┤ ├──────────┤     │ │ [Cobrar]        │
│ │Nombre    │ │Nombre    │     │ └─────────────────┘
│ │Categoría │ │Categoría │     │
│ │$ Precio  │ │$ Precio  │     │
│ └──────────┘ └──────────┘     │
│ Página n     [Anterior][Sig.] │
└──────────────────────────────┘
```

| Elemento/estado | Especificación propuesta |
|---|---|
| Card | Button completo, ancho176–256DIP, separación12DIP, padding12, borde1, radio12; alto uniforme por fila mínimo290DIP, crece con ancho/texto. |
| Foto | Área1:1 (152–232DIP), recorte persistido ya cuadrado; Image Stretch Uniform, sin nuevo crop implícito en UI. |
| Nombre | Siempre visible,18DIP semibold, alineado izquierda, hasta3 líneas reservadas; nombres largos con elipsis y nombre íntegro en AutomationProperties.Name/ayuda. No identificarse sólo por imagen. |
| Precio | 22DIP semibold Pan Artesanal, formato MXN existente de ProductRow; jamás rasterizar en foto ni copiar a metadata. |
| Categoría | 13DIP de texto secundario, proveniente de Category real; ocultable sólo si categoría única ya contextualiza la vista. |
| Normal | Surface clara, texto oscuro, foto dominante; fallback con mismo tamaño para evitar saltos. |
| Hover | Borde verde2DIP dentro de espacio reservado, leve fondo cálido; sin mover/tamaño ni transición que pinte todos los botones. |
| Pressed | Fondo PrimarySoft, sin escala de foto ni sombra decorativa; feedback local del botón. |
| Keyboard focus | Anillo de2DIP contrastante y nombre accesible; Tab sigue orden visual, Enter/Espacio activa una sola vez. |
| Selected | No selección persistente propia de compra: click agrega. Confirmación breve «Agregado» tras éxito; no confundir foco/hover con una línea seleccionada del carrito. |
| Sin foto / error | Placeholder geométrico neutro e indicación «Sin foto»; nombre/precio/categoría completos. Nunca pan genérico, IA, stock ni icono oficial como si fuese producto. |
| Inactivo | Excluido de POS por servicio; en Catálogo se muestra etiqueta «Inactivo», sin posibilidad de agregar. Conservar foto no reactiva Product. |
| Disponibilidad | No inventar «agotado» desde TrackInventory. Mantener semántica actual: disponibilidad sólo de reglas existentes; errores autoritativos siguen viniendo de servicio. |

Categorías: listar Category real a través de ListPosCategories; «Todas» es filtro UI sin crear fila. Usar orden actual, chips táctiles>=44DIP, fila con scroll horizontal y foco visible; no nombres hardcodeados. Conservar búsqueda por nombre/SKU, longitud160, normalización y botón/Enter; cambiar filtro reinicia página. No modificar política de búsqueda por fotos.

Click/touch/teclado de toda la card ejecuta **ProductClick → PosViewModel.AddAsync(ProductId) → PosService.Add**. Mantener CanOperate/Busy y guards existentes; no agregar otra llamada en MouseDown, imagen y texto a la vez. Confirmar producto activo/categoría/permisos/caja sigue a cargo de servicio. El thumbnail no entra al Cart ni SaleItem. Precio mostrado de Product; snapshot en carrito mantiene reglas existentes.

Responsive dentro de PosView: medir ancho de contenido efectivo, no píxeles físicos de monitor. Con contenido >=720 DIP: carrito de 320–360 DIP, separación 16 y área de productos restante; columnas=floor((anchoProductos+12)/(176+12)), al menos 1, card <=256. A 1280 con sidebar 248 y márgenes 48 quedan 984 DIP: carrito 320/separación 16, productos 648, tres cards de 208 DIP. A 960 quedan 664 DIP: cambiar a disposición vertical con catálogo arriba y carrito debajo en filas estrella con mínimo útil/scroll, resumen y Cobrar visibles en la zona de carrito; no comprimir un carrito de 410 y cards a anchos ilegibles. Probar altura 640, fuentes 150/200%, teclado y scroll táctil. A 1920 puede haber 5 cards por fila. No cambiar MinWidth del shell ni navegación fullscreen.

## 12. Catálogo administrativo y permisos

Agregar bloque «Fotografía del producto» al editor de producto persistido: preview, estado «Sin foto»/«Foto disponible»/«Revisar archivo», Cambiar foto y Quitar foto. Guardar cambios comerciales y cambiar foto son operaciones separadas claramente rotuladas; el diálogo muestra el producto persistido y no implica que un precio todavía sin guardar esté confirmado.

Cambiar abre selector, validación, preview/crop y Guardar foto. Mostrar progreso y Cancelar antes de publicación. Quitar confirma asociación a quitar, conserva original y vuelve al fallback. Mensajes concretos: «Selecciona un JPEG o PNG», «Máximo25MB», «No se pudo leer la imagen», «La foto cambió; actualiza antes de guardar», «La foto anterior se conservó». No mostrar SQL, paths o IDs en flujo normal.

Consultar descriptor requiere catalog.view. Cambiar/quitar/recortar requiere products.edit revalidado en Application dentro de transacción; permisos actuales Administrator/Manager habilitados, Cashier sólo consulta. No recipe.configure, ni permiso nuevo ni inferencia por nombre de rol. Inicialmente products.create no autoriza foto por sí solo: crear y luego editar exige products.edit, como ya tienen los roles de gestión. Recuperación de bytes idénticos puede ser técnica bajo gate; reparación que cambia hash/metadata requiere products.edit, también para una revisión retirada. El POS de un cajero muestra fallback hasta esa reparación. Auditar cambios y rechazos de manera compatible con el sistema existente.

## 13. Rendimiento y compatibilidad esperados

Objetivos de aceptación propuestos, **no resultados certificados**: primera página con texto utilizable<500ms, fotos visibles<1s enSSD del equipo objetivo, click con feedback<100ms aparte del servicio existente, scroll p95<33ms por frame, vuelta a categoría con caché sin leer originales. Medir 30/50/100 productos distintos y50 ciclos de cambios de categoría/entrada/salida; después de liberar vistas y estabilizar GC, crecimiento retenido<10MiB respecto al ciclo inicial equivalente. Medir mediana/p95, archivos fríos/calientes,150/200%DPI, disco lento y ausencia de fotos.

No extrapolar la carta técnica al POS. La validación de archivos/fotos reales puede modificar Q85 o elegir768 si se demuestra necesario antes de aprobar implementación. Con512 la app paginada necesita hasta50MiB de payload si materializara toda la página; la caché/lazy evitará hacerlo salvo que sean visibles.100 productos no cargan100originales.

Compatibilidad propuesta: Windows11x64 y Windows10x64/LTSC **según edición/build soportada por .NET10**, no certificación universal de todo Windows10. Consultar [matriz de instalación de Microsoft](https://learn.microsoft.com/dotnet/core/install/windows) y probar el LTSC objetivo, que todavía no se proporcionó. WPF/WIC nativos JPEG/PNG; sin codecs opcionales instalados. Presentation seguirá net10.0-windows, Infrastructure/Maintenance no requieren codec WPF para backup. Futuro self-contained debe incluir runtime Desktop y probar codecs/ICC del SO; self-contained no vuelve autónomo al sistema respecto a Windows.

Datos fuera de Program Files y de la carpeta de binarios. Reinstalar/actualizar no elimina DataDirectory; desinstalador futuro conserva datos por defecto y sólo purga con autorización explícita. Cambio de usuario/equipo: restaurar paquete completo a raíz local nueva, elegir configuración permitida, conservar terminal y activar una sola instalación. No depender de Pictures, OneDrive, junctions, nombres del operador o ruta del ejecutable.

## 14. Pruebas futuras obligatorias (no ejecutadas como producto)

| # | Caso | Resultado verificable |
|---:|---|---|
| 1 | JPEG real | Original/hash idénticos, orientación/crop correctos, derivado legible. |
| 2 | PNG real, alfa/ICC | Original intacto, composición y sRGB correctos, sin metadatos privados en derivado. |
| 3 | Extensión/firma/formato inválido | Rechazo sin cambioDB; HEIC/WebP/APNG/SVG rechazados explícitamente. |
| 4 | Corrupto/truncado | Error controlado, tareas observadas, sin cerrar POS ni dejar referencia. |
| 5 | Foto enorme | Bytes/lados/MP verificados antes de raster; prueba límite y memoria. |
| 6 | Reemplazar | CAS, antes/después auditado, original previo retenido; doble click y reintento idempotentes. |
| 7 | Quitar | Product intacto, punteroNULL, revision+1, fallback, archivos retenidos. |
| 8 | Producto sin foto | Card usable, nombre/precio/categoría; agrega normalmente. |
| 9 | Producto con foto | Coincide ProductId/PhotoId, no se cruza foto al reciclar/refrescar. |
| 10 | Product/categoría inactivo | Fuera de POS y Add rechaza una card obsoleta; Catálogo conserva foto. |
| 11 | Thumbnail | 1:1, lado<=512, no upscale, EXIF1–8, crop bordes; hash/dimensiones. |
| 12 | Invalidation | Cambiar/quitar mientras llega decode antiguo no reaplica foto vieja; memoria acotada. |
| 13 | Reinicio | Puntero/revisión/archivos sobreviven; staging nunca visible. |
| 14 | Archivo faltante | Fallback; original recuperable; arranque degradado media explícito, no DB inválida. |
| 15 | Backup | v2 incluye activos/retirados, snapshot consistente, lease/gate, disco lleno y publicación parcial. |
| 16 | Restore | v1→16 sin foto, v2 con foto; destino nuevo, no cambia backup ni original; emergency sólo explícito. |
| 17 | Hash/integridad | Cambiar bytes, longitud, manifest, eliminar thumb/original y FK inválida detectados. |
| 18 | Catálogo50 | Tiempo, memoria y scroll frío/caliente con50fotos distintas; budget medido. |
| 19 | Catálogo100 | Dos páginas, métricas, no materializa originales/segunda página sin necesidad. |
| 20 | Categorías | Datos reales, orden/filtros activos existentes y estado seleccionado. |
| 21 | Búsqueda | Nombre/SKU/acento/limite160, enter/botón, resultados sin foto y cancelación de decode atrasado. |
| 22 | Agregar card | Mouse/touch/Enter/Espacio agregan una vez por activación; foto no intercepta eventos. |
| 23 | Cart logic único | Ruta PosService.Add y mismas invariantes/checked/snapshot; ninguna alternativa. |
| 24 | Permisos | Administrator/Manager products.edit; Cashier consulta; revocación durante importación rechaza commit. |
| 25 | RegressionPOS | Agregar/cantidad/descartar/checkout incierto, conteo confirmado y caja cerrada sin cambios. |
| 26 | RegressionCatalog | Crear/editar precio/categoría/activar/desactivar y auditoría existentes. |
| 27 | Schema | PK/FK propiedad compuesta, tipo/rangos/crop, punteroNULL, CAS/overflow e inmutabilidad. |
| 28 | Migraciones | 001–015 hashes intactos;016 transaccional, rollback/fallo, upgradev1 y rechazo downgrade. |
| 29 | Reinstalación futura | Diseño de preservaciónDataDirectory y ensayo paquete en máquina limpia; no instalador ahora. |
| 30 | Windows paths | Unicode/espacios, case collisions, long paths, `..`, ADS, UNC, device/reparse/ZIP traversal. |
| 31 | Crash injection | Cada frontera staging/move/commit/notify; incierto recuperado por OperationId y sin borrar original. |
| 32 | Concurrencia | Dos reemplazos misma revisión, replace/remove, backup/publicación, purge/staging; orden de locks. |
| 33 | Memoria/handles | Apertura exclusiva,50ciclos, streams cerrados, tareas canceladas, VMs/BitmapSource no retenidos. |
| 34 | Backup hostile/oversize | Duplicados, ZIP bomb, manifiesto enorme, extras, schema discrepante; ningún archivo fuera de staging. |
| 35 | Responsive/accesibilidad | 960×640/1280×820/1920, DPI100/150/200%, tab/foco/touch, nombres largos y lector de pantalla. |
| 36 | Retención/repair | Retirados incluidos, cuota impide importación sin borrar; derivado regenerado con hash distinto publica archivo nuevo y actualiza descriptor/auditoría por CAS, sin tocar original. |

En implementación ejecutar suite completa, Release, verificación Windows WPF y diff; actualmente 535/535 y Release 0/0 son **evidencia previa de 1K.1**, no una suite repetida en 1K.2A. Este trabajo sólo ejecutó el probe descrito y controles documentales.

Control final de 1K.2A: HEAD sin cambios; diff de src/tests/migrations contra baseline vacío; 15 hashes de migración comparados, 0 discrepancias; 016 ausente; `git diff --check` aprobado para archivos versionados y revisión equivalente de whitespace para documentos nuevos. No se ejecutó la suite completa ni build por ser entrega documental/POC aislada.

## 15. Alternativas descartadas y riesgos

- BLOB para originales: simplifica atomicidad/backup pero aumenta DB, snapshots y tamaño de instalación; no hay ventaja demostrada aquí para variosMB por foto. Archivos exigen protocolo explícito; no se oculta su coste.
- Ruta personal en Product o sidecar JSON como autoridad: no portable, sin FK ni atomicidad con asociación/auditoría. No evita responsablemente016.
- MediaAsset/DAM genérico: galerías, tags y deduplicación global no requeridos. ProductPhoto cubre propiedad y revisión mínima.
- Original único mutable/sólothumbnail: pierde evidencia/original y rompe reintentos; descartado.
- PNG obligado por identidad visual: logo no determina formato fotográfico. WebP dependiente del equipo: descartado para V1.
- 768/varios thumbnails por defecto: más memoria/archivos sin prueba de necesidad de las cards actuales. Preview grande será bajo demanda.
- Cargar originales o caché ilimitada: bloqueos/memoria. Virtualización nueva para50cards: mantener paginación y medir primero.
- Borrar original al quitar foto: contrario a preservación. Retención crece y amplía backups; cuota/alerta y futura política de purga se reconocen explícitamente.
- Persistir estado de stock/precio en foto: duplicaría autoridad. La card respeta servicios actuales.

Riesgos restantes: sin fotos reales no se certifica calidadQ85; Windows10/LTSC objetivo desconocido; almacenamiento original y dos backups por arranque pueden ser costosos; interrupción eléctrica/antivirus puede dejar media faltante; decoderWIC no es sandbox de seguridad; ICC/EXIF y PNG animado exigen pruebas; layout960 puede requerir iteración humana; retención de un original perdido impide backup completo hasta reparar. No se migra impresión: Infrastructure aún tiene recurso histórico de ticketOpción07, fuera del alcance1K.2A; no usarlo en catálogo ni afirmar que desapareció de todo el repositorio.

## 16. Archivos futuros, documentación y siguiente autorización

Implementación propuesta, no cambios realizados ahora:

| Archivo/capa | Cambio futuro |
|---|---|
| `migrations/016_product_photos.sql` | Sólo tras autorización: DDL revisado, constraints y validación de objetos. |
| Application: nuevos `ProductPhotoService.cs`, `ProductPhotoPorts.cs`, `ProductPhotoRecords.cs` | Casos de uso/DTO/puertos, CAS, recuperación y autorización sin WPF. |
| Infrastructure: nuevos `SqliteProductPhotoStore.cs`, `ProductPhotoFileStore.cs` | Metadata y publicación de bundles, coordinación y reconciliación. |
| `Infrastructure/InstallationBackup.cs`, `FoundationRuntime.cs`, `Database.cs` | v1/v2/emergencia, arranque/migración y verificaciones. Mantener reglas comerciales intactas. |
| `Maintenance/Program.cs` | Backup/restore v2 y recuperación explícita; bytes/hashes sin dependencia Desktop. |
| Presentation: nuevos `WpfProductPhotoProcessor.cs`, `ProductImageCache.cs`, `ProductCardViewModel.cs`, `ProductPhotoDialog.xaml/.cs` | Codec/preview, carga acotada, composición visual y crop. |
| `Presentation/PosView.xaml/.cs`, `PosViewModel.cs` | Cards, descriptores batch y lazy; mantener AddAsync→PosService.Add. |
| `Presentation/CatalogView.xaml/.cs`, `CatalogViewModel.cs` | Bloque foto y acciones separadas por permiso. |
| `Presentation/MainViewModel.cs`, `Resources/Theme.xaml` | Composición de puertos/servicios y estilos específicos de cards; shell/fullscreen sin nuevo rediseño. |
| Tests existentes y nuevos ProductPhoto* | Tabla de pruebas anterior; suite original sigue vigente. |

No necesita cambiar Domain/Catalog.cs, PosService.Add, Checkout, ProductionService, RecipeService ni ledger. Si una implementación descubre necesidad de alterar esas fronteras, detener esa decisión y revisar alcance antes.

Entregables actuales: este diseño, evidencia del probe y script no productivo, continuidad en CURRENT_STATE/NEXT_STEPS, nota de arquitectura y nota de backup en operations. No existe docs/presentation.md actualmente: el diseño de ProductCard queda aquí para no duplicar especificación. No se crea ADR adoptado para una propuesta pendiente.

Fotos/datos requeridos para el siguiente paso:3–5 originales reales JPEG/PNG representativos (p.ej. detalles claros/oscuros/toppings) con identificación inequívoca de Product (nombre/SKU o selección en catálogo), luego el resto gradualmente. Preferir>=1024px en lado menor, una pieza principal centrada, margen10–15%, fondo consistente, luz natural/neutral y encuadre uniforme; sin filtros agresivos. Si la cámara entrega HEIC, exportar JPEG/PNG conservando el original externo. No hace falta completar el catálogo para comenzar: fallback soporta mezcla con/sin foto. Confirmar edición/build Windows10/LTSC y DPI/resolución del equipo destino al planificar su validación, sin bloquear este diseño.

Próximo paso exacto: autorizar **1K.2B** con016 propuesta, ProductPhoto, originales retenidos, importación JPEG/PNG, derivado512/Q85, caché64MiB, backupv2 y ruta de emergencia explícita; validar primero las muestras y la propuesta SQL/errores antes de migrar. Modelo recomendado: **Astra** para atomicidad/backup/persistencia y recuperación; UI sigue el sistema1K.1. No implementar ni hacer commit de esta arquitectura hasta autorización. Detenerse al entregar el reporte.
