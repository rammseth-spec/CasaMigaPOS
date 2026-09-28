# Product Photo Foundation — contrato 1K.2B

Estado: implementado y verificado; UI/fotos de catálogo final reservadas para 1K.2C. Fuente normativa: autorización 1K.2B y adenda de `PHASE_1K2A_DESIGN.md`. No cambian Domain, precio, cart, Checkout, Sales, Cash, Inventory, Recipes, Production ni Printing.

## Modelo y almacenamiento

Product tiene cero o una foto vigente. `product_photo_asset` conserva metadata histórica; `product_photo` selecciona la actual y mantiene revisión monotónica incluso al quitarla. FK compuesta impide cruzar ProductId/PhotoId. Migration016 agrega sólo esas dos tablas y cuatro triggers, sin permisos nuevos. Original/crop/procedencia no se modifican; reparar sólo actualiza hash/longitud del derivado.

Metadata: GUID, ProductId, PROVISIONAL/REAL, nombre informativo sin ruta (1–255 caracteres), SHA-256/longitud/formato real/ancho/alto del original, orientación1–8, crop X/Y/Edge, versión layout/pipeline1, hash/longitud/lado del thumbnail, JPEG quality85, timestampUTC/actor/terminal. PROVISIONAL→REAL crea un nuevo asset sobre el mismo ProductId; no cambia venta ni precio.

```text
DataDirectory/
  casamiga.db
  terminal.json
  product-images/
    v1/<PhotoId GUID N>/original.jpg|original.png
    v1/<PhotoId GUID N>/pos-v1-<SHA256>.jpg
    .staging/<GUID>/...
```

PhotoId deriva del OperationId generado por la aplicación. Las rutas se construyen desde identidad/formato/hash, no desde nombres personales/productos/origen. GUIDs se almacenan en DB con formato D; nombres de carpeta en N minúsculas. Se rechazan enlaces/reparse points en ancestros. Datos fuera del ejecutable. No deduplicación entre Products.

Retención: original actual byte por byte. Reemplazar/quitar elimina el bundle anterior sólo después del commit y comprobando que no está activo. Metadata/auditoría se conserva; rollback de fotografías retiradas no está implementado. Si limpieza falla por IO/permisos, no se convierte un commit exitoso en fallo aparente: se reintenta por conciliación.

## API y autorización

Componer `ProductPhotoService(repository, files, processor, UserSession, IClock, terminalId)` con `SqliteProductPhotoStore(Database)` y `ProductPhotoFileStore(DataDirectory)` de la misma instalación; codec `WpfProductPhotoProcessor`. Mantener `DirectoryLease` durante toda la operación. El codec recibe bytes y devuelve bytes/metadata: no abre ni escribe archivos. No existe acceso directo desde Presentation a SQLite/filesystem para este flujo.

| Método | Contrato |
|---|---|
| Current(ProductId) | Metadata vigente y revisión; catalog.view, usuario activo y terminal existente. |
| ReadThumbnail(ProductId) | Bytes JPEG verificados o null sin asociación; catalog.view. Faltante/corrupto produce error controlable; futura UI deberá usar fallback. |
| Import(PhotoImport, Stream) | Set si no hay foto o replace si la hay; products.edit, expectedRevision y OperationId; procedencia y filename obligatorios. |
| Remove(ProductId, revision, OperationId) | Puntero null, revisión+1, audit y limpieza postcommit; no borra Product. |
| Recover(ProductId, OperationId) | Resultado durable del actor/terminal original o null; products.edit; no requiere archivo de origen. No implica que ese resultado siga siendo la selección actual. |
| Repair(ProductId, revision, OperationId) | Regenera sólo derivado desde original con hash válido, crop y pipeline registrados; products.edit y CAS. |
| Reconcile(remove=false) | Vista previa de huérfanos; remove=true limpia candidatos de al menos24h; products.edit, gate y referencias actuales. |

Administrator/Manager pueden modificar según permisos actuales; Cashier consulta. Product inactivo sigue editable fotográficamente, sin reactivarlo. Revocación de permisos durante decode se revalida dentro de la transacción que publica asociación. Operaciones idempotentes comparan actor, terminal, producto y fingerprint de intención (request+hash original); reutilizar OperationId con otro payload falla. Auditoría `product.photo.set/replace/remove/repair` comparte commit con selección; registra metadata/IDs/hashes, nunca bytes ni ruta fuente.

## Procesamiento

- Sólo JPEG/PNG, magic y decoder nativo WIC. Un frame; PNG animado rechazado por acTL; CRC/estructura PNG verificados. JPEG exige firma y terminación, header de decoder y decodificación completa. Extensión informativa no autoriza formato.
- Original máximo25MiB, lado12000px, área48MP. Dimensiones comprobadas antes de materializar raster. Crop entero cuadrado >=256px y dentro de la imagen **después** de orientación EXIF; incluye giros y reflejos1–8.
- Convierte perfil disponible a sRGB; perfil no soportado/error de decoder rechaza. No se certifican todos los perfiles ICC posibles. No filtros estéticos, IA ni reconstrucción.
- Thumbnail JPEG Q85, lado min(512,crop.Edge), sin upscale. Escalado por promedio de área, alfa sobre Fondo Natural RGB253/248/239. Frame nuevo sin EXIF/GPS. Original conserva absolutamente sus bytes/metadata.
- Reabre derivado para verificar formato/lado; hash/longitud se verifican también al escribir. Thumbnail <=2MiB. Cuota conservadora de activos5GiB (reserva2MiB por nuevo derivado), hasta10000 asociaciones activas.
- Sin caché de bitmaps en1K.2B. Read verifica puntero/hash actual bajo gate, por lo que reemplazo/quitar no reutiliza bytes viejos. La futura caché POS necesitará clave PhotoId/hash y presupuesto64MiB de píxeles.

## Publicación, fallos y concurrencia

Gate por raíz normalizada compartido entre threads/instancias; DirectoryLease existente excluye procesos externos. Orden obligatorio gate→DB. Import mantiene gate durante decode y publicación (prioriza integridad, serializa importaciones); no mantiene transacción SQLite durante procesamiento de imagen.

1. Autorizar y leer stream con límite; calcular fingerprint.
2. Bajo gate, resolver retry; procesar totalmente.
3. Staging único, originales/derivados cerrados con Flush(true), verificar hashes.
4. Mover bundle completo al directorio nuevo del mismo volumen. Retry previo al commit acepta sólo bundle idéntico.
5. Transacción: reautorizar, comprobar revisión, insertar metadata, cambiar selección y audit; commit.
6. Retirar bundle anterior si ya no está activo.

Antes del commit, fallo conserva asociación anterior. Después de publicación y rollback puede quedar huérfano; no se borra suponiendo que el commit falló. Resultado incierto se consulta por Recover/OperationId; retry no duplica audit ni asociación. No existe ACID conjunta entre disco y SQLite: corte eléctrico/antivirus/disco pueden producir media perdida; hashes permiten detectarla, no reconstruir original ausente.

Conciliación bajo gate obtiene referencias autoritativas de DB y sólo considera carpetas GUID no activas en v1/staging, y derivados antiguos de carpetas activas. Antigüedad mínima24h evita purga reciente; ninguna operación activa puede ser escaneada en paralelo. Si DB no se puede consultar, no limpia. No seguimiento de enlaces. La memoria del gate no contiene imágenes.

Repair es explícito. Puede reponer el mismo archivo si sus bytes regenerados coinciden con hash registrado; si cambian, publica nombre por nuevo hash antes de actualizar descriptor/audit. Nunca acepta archivo corrupto recalculándole hash. Derivados antiguos se concilian después; backup enumera sólo el descriptor actual. Read no repara automáticamente y original ausente no se sustituye por thumbnail.

## Backup/restore

`InstallationBackup.Create` conserva v1 para schema<16; schema16 produce v2. SnapshotSQLite→lista de asociaciones de ese snapshot→copia/verificación de originales y thumbnails activos, bajo gate. El archivo final sólo aparece después de extraer y validar el `.partial` generado.

v2: Version, TerminalId, SchemaVersion, Files[{Path,Bytes,Sha256,Role}]; roles database/terminal/original/thumbnail. Contiene casamiga.db, terminal.json, manifest.json y dos archivos por foto vigente. DB conserva toda metadata/audit; excluye binarios retirados, staging, orphans, backups anidados, paths fuente y credenciales en texto.

Límites: manifiesto4MiB,20003 entradas, media5GiB, total16GiB, original25MiB, thumbnail2MiB, terminal64KiB. Rutas con whitelist por rol, GUID y hash; rechaza duplicados incluso case-insensitive, `..`, `.`, backslashes, drive/UNC, absolutos, ADS y segmentos inválidos. Extracción streaming con longitud acotada, nunca ExtractAll; hash individual y referencias desdeDB deben coincidir exactamente. Los hashes detectan corrupción, no autentican el paquete frente a un atacante que pueda reemplazar todos sus componentes.

Restorev1 conserva compatibilidad y migra candidato a16 sin inventar fotos. Restorev2 verifica manifiesto/entradas/DB/FK/terminal/schema/referencias/media. Publicación por Directory.Move sólo a destino nuevo, nunca sobre instalación activa. Migration.Initialize valida historia soportada en candidato; no modifica backup original. Arranque15→16 conserva prebackupv1 y postbackupv2.

No había modo emergency en arquitectura existente: no se implementa formato nuevo ni restore degradado. Media faltante/corrupta falla de forma explícita. El arranque puede detenerse en su respaldo obligatorio; consultar recuperación en operations.md. `.db` aislado no es backup completo de una instalación con fotos.

## Evidencia y límites

621/621 pruebas completas (535 existentes+86 fotos), sin omitidas; Release0/0. Smoke técnico sintético: import→replace→remove→reimport→backupv2→restorev2, original exacto y asociación conservada. También v1restore, límites/EXIF/alfa, rollback/orphans, commit incierto, permisos y cuatro carreras requeridas.

Sin fotos comerciales importadas, sin UI final ni caché, sin paquete de26 productos, sin venta real. No certifica nitidez de fotografías reales, todo ICC, Windows10/LTSC, disco lleno físico, cortes eléctricos ni rendimiento del futuro grid. Pruebas de fallos son inyecciones controladas. Próxima fase1K.2C requiere autorización.
