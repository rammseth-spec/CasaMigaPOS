## Validación 1J.5 completada

Dataset aislado work/phase1j5-smoke-20260926 restaurado desde backup real de datos sintéticos. No confundir cerrar aplicación con cerrar caja. El relevo de producción entre usuarios no está implementado: el creador debe confirmar desde la terminal original. Si se crea otro plan, controlar operativamente que el borrador anterior no se confirme también. Smoke manual aprobado; FASE 1J CLOSED. Copia restaurada: work/phase1j5-restored-20260926. Evidencia persistente: evidence/phase1j5-persistent-restore.json.

# Operación vigente — UI funcional 1J.4

Administrator puede configurar fórmulas y perfiles desde **Recetas**; Manager sólo consulta. Las versiones publicadas no se editan: se crea una versión DRAFT, se publica y luego se activa. Usar cantidades operativas exactas (`g/kg`, `ml/L`, `pieza`).

Administrator y Manager pueden crear una **Producción**: agregan productos/cantidades, calculan necesidades, revisan existencias y crean el DRAFT. El preview no reserva. El DRAFT queda bloqueado y la confirmación sigue ligada al creador y terminal. Capturar un real entero no negativo por producto y revisar la advertencia si todos son cero. Ante resultado incierto, actualizar/consultar historial conservando la identidad en vez de crear otro lote. Cashier no tiene acceso.

El historial es read-only. No editar SQLite, saldos, snapshots ni estados manualmente. Para pruebas usar `CASAMIGA_DATA_DIR` aislado; no introducir recetas reales sin autorización y mediciones aprobadas. Ver `recipe-production-ui.md`, `PHASE_1J4_REPORT.md` y `PHASE_1J4_SMOKE.md`.

## Registro anterior (histórico)

# Estado vigente — servicios de producción 1J.3

Producción disponible mediante servicios Application, todavía sin UI. No editar SQL de instalaciones reales. Conservar OperationId raíz y ExpectedRevision para reintentos; ante resultado incierto consultar/reintentar la misma identidad. Lote ligado a creador/terminal, MAIN e inventarios inicializados. Cambiar plan/alternativas requiere nuevo DRAFT. Backup/restauración existentes; no se abre POS ni imprime en esta subfase.

Ver ADR-023 y PHASE_1J3_REPORT.md para contrato, evidencia y limitaciones.

## Registro anterior (histórico)

# Operación vigente — almacenamiento 1J.2, sin flujo productivo

No hay pantalla de fórmulas/producción ni permisos nuevos. No cargar recetas reales mediante SQL ni utilizar el repositorio técnico como servicio autorizado de usuario. El POS, Checkout, cierre de caja, impresión y consumo por venta mantienen su comportamiento anterior.

El arranque aplicará 013 por el mecanismo normal de backup/migración/validación cuando se ejecute una instalación; en esta entrega sólo se usaron bases sintéticas de tests. Backup y restauración existentes funcionan con schema 13 y conservan configuración, drafts y contratos históricos. Se probó también restaurar backup 12 y migrarlo. Restaurar a directorio nuevo; validar integridad y FKs, no editar producción. Los tests eliminan sus directorios temporales; la evidencia persistente registra resultados, no una base comercial.

Para verificar esta entrega: scripts/verify.ps1; pruebas Recipe* para persistencia/migración/contrato y regresiones existentes de 1I. No se necesita smoke WPF ni impresora. Ver PHASE_1J2_REPORT y ADR-022. 1J.3 requiere autorización separada.

## Instrucciones de etapas anteriores

# Operación de caja, venta y respaldo — FASE 1E

## Inventario — cierre FASE 1H

Antes de operar, iniciar sesión y abrir Inventario con un rol autorizado. Administrator puede configurar, registrar saldo inicial, entrada, merma y conteo; Manager puede consultar, recibir y registrar merma; Cashier no accede. Las unidades operativas son g/kg, ml/L y pieza. Un reintento debe conservar el mismo OperationId y contenido; contenido distinto con el mismo UUID es conflicto.

Para diagnóstico, usar historial y reconciliación. Una diferencia se reporta y bloquea ajustes dependientes; nunca se repara automáticamente ni se edita SQLite. El backup de instalación se crea con la aplicación cerrada y un lease exclusivo. Restaurar siempre a un directorio nuevo, ejecutar validación, `integrity_check`, `foreign_key_check` y reconciliación, y conservar el origen hasta aceptar la copia. No activar origen y restauración simultáneamente.

El smoke WPF aprobado `SMOKE-H14` sigue siendo la evidencia humana vigente: opening 1 kg, entrada 500 g, merma 100 g, conteo 1.5 kg, cuatro movimientos y saldo reconciliado 1.5 kg. 1H.5 no cambió producto, por lo que no se repitió. La validación sintética automatizada de cierre ejercitó restart y restore sin ventas ni impresora; consultar `PHASE_1H5_REPORT.md`.

Sólo desarrollo; venta y cobro efectivo implementados en 1E, sin hardware ni uso de producción. Los apartados previos por fase conservan su contexto histórico; el apartado 1E contiene las reglas vigentes.

## Inicio y conteo

Ejecutar dotnet run --project src/CasaMiga.Presentation con SDK .NET 10. CASAMIGA_DATA_DIR permite ruta local absoluta; por defecto LocalAppData/CasaMigaPOS. Para publicación de pruebas se admite development.settings.json junto al ejecutable, IsDevelopment true y DataDirectory absoluto; marca DESARROLLO. No usar carpeta sincronizada ni compartida.

En base nueva, crear primer administrador con contraseña de 12–128 caracteres, sin contraseña predeterminada. Login explícito en cada reinicio. No existe editor de usuarios ni recuperación de contraseña.

Abrir con fondo no negativo. Mientras la caja está abierta, se muestran usuario, terminal y hora; el fondo se oculta porque en 1B equivale al esperado. Cerrar caja → capturar contado → Continuar guarda irrevocablemente el conteo y muestra resultado → Confirmar cierre. Cancelar/Escape antes de Continuar no registra conteo; Volver/Escape después conserva el conteo y regresa a caja abierta. Cerrar caja nuevamente retoma el resultado, sin editar el importe.

No pulsar Continuar hasta terminar el conteo. No existe corrección posterior en 1B; un futuro ajuste tendrá que ser explícito y auditado. Un reinicio entre conteo y cierre recupera el conteo al retomar. Salir del usuario/cerrar ventana no cierra caja.

Propietario: cash.close. Tercero: cash.close y cash.close.other. Administrator y Manager tienen ambos. Cashier conserva open/view de 1A y no recibe automáticamente close ni close.other; un rol autorizado requiere una futura administración controlada. No editar manualmente producción. Los permisos se revalidan en Application al contar, retomar y cerrar. Si se cierra una sesión ajena, la UI lo indica y la auditoría conserva propietario, contador y cerrador.

Fórmula 1B: esperado = fondo; entradas, retiros, ventas y devoluciones son cero. Diferencia = contado − esperado. Cero es caja exacta; positivo sobrante; negativo faltante. Tras cerrar puede abrirse otra sesión con otro ID; la histórica queda CLOSED e inmutable.

## Fotografías 1K.2B — operación vigente

### UI 1K.2C

En Punto de venta, seleccionar una tarjeta completa agrega el producto mediante el carrito existente. Las fotos cargan en segundo plano; un placeholder “Sin foto” es normal y no bloquea venta. Categoría y búsqueda pueden combinarse. La procedencia no se muestra al cajero.

En Catálogo, editar un producto muestra Foto real, Foto provisional, Sin foto o Foto no disponible. Sólo quien conserve `products.edit` puede agregar/cambiar/quitar. Elegir archivo abre crop cuadrado; cancelar no modifica. Al reemplazar se conserva ProductId, precio, historial, receta e inventario. Quitar foto no elimina el producto. No mover archivos bajo `product-images` manualmente.

El generador `tools/CasaMiga.Phase1K2CSmoke` es sólo para carpeta nueva y catálogo controlado. Compara CSV/JSON y se detiene ante discrepancia. No usarlo contra `%LOCALAPPDATA%/CasaMigaPOS` ni una instalación del negocio. El mapa vigente está en `phase1k2c-provisional-map.md`.

Schema 16 incorpora originales/derivados administrados bajo `DataDirectory/product-images/v1/<PhotoId>/`. No moverlos manualmente, no guardar fotos junto al ejecutable ni cambiar filas SQLite para corregir asociaciones. Producto inactivo puede administrarse con products.edit sin reactivarlo. `PROVISIONAL` y `REAL` son procedencia; nunca duplicar Product por cambiar a foto real.

En 1K.2B se usa API/harness, sin UI temporal ni importación masiva. `ProductPhotoService.Import`, `Remove`, `Recover`, `Repair` y `Reconcile` documentados en `product-photos.md`. Operar con DirectoryLease de instalación. Import y Remove conservan OperationId para resolver respuesta incierta antes de otro intento; `Recover` no necesita archivo de origen. `Repair` explícito requiere original íntegro; no reescribe el original. Reconcile permite vista previa y limpieza de archivos sin referencia de al menos 24 horas, bajo gate. Binarios retirados se limpian después del commit; errores de limpieza se concilian posteriormente.

Backup v2 normal incluye DB, terminal y sólo originales/derivados activos; manifiesto enumera ruta lógica, bytes, hash y rol. Se valida `.partial` antes de publicar `.cmbackup`. Restore admite v1 histórico y v2, sólo hacia directorio nuevo. v1 migra a 16 sin inventar fotos. No se incluyen staging, huérfanos, retirados, contraseñas en texto ni directorios fuente.

Si faltan/corrompen archivos actuales, backup/restore normal falla. No hay modo emergency ni éxito degradado; el arranque también puede detenerse al fallar su respaldo obligatorio. Conservar instalación y último backup íntegro. Si el original es válido, un harness administrativo con identidad autorizada puede ejecutar Repair con la app cerrada/lease adquirido; si el original se perdió, restaurar un backup íntegro a carpeta nueva o importar una sustitución autorizada mediante el servicio. No aceptar un hash nuevo para bytes corruptos ni renombrar un .db como backup completo. Estas acciones no cambian venta/caja/inventario.

Smoke aislado retenido bajo `work/phase1k2b-smoke`; no contiene datos comerciales. No abrir la instalación del negocio sólo para ensayar schema 16. Para repetir smoke: fijar `CASAMIGA_PHOTO_SMOKE_DIR` a una carpeta local de evidencia y ejecutar el test `SmokeEvidenceCanBeRetainedWithoutRealCatalog`; el harness importa/reemplaza/quita/reimporta, respalda y restaura un Product sintético. Credenciales del backup sintético se dejan únicamente en esa carpeta ignorada.

## Propuesta de fotos 1K.2A — histórica, sustituida por la sección anterior

El respaldo vigente sigue siendo v1 con tres entradas. `PHASE_1K2A_DESIGN.md`, sección 9, propone un manifiesto v2 que enumera DB, terminal, originales y thumbnails registrados, con hashes y límites. Requiere autorización posterior: no añadir manualmente fotos a paquetes v1 ni ejecutar DDL propuesto. Restore nuevo conservaría lectura de v1 y publicaría siempre a carpeta nueva. La ruta de emergencia para media perdida está propuesta como `.cmrecovery` explícito; todavía no existe en el programa y no equivale a un backup completo.

## Backup completo

```powershell
dotnet run --project src/CasaMiga.Maintenance -- init C:\CasaMiga-Dev
dotnet run --project src/CasaMiga.Maintenance -- backup C:\CasaMiga-Dev
dotnet run --project src/CasaMiga.Maintenance -- list C:\CasaMiga-Dev
dotnet run --project src/CasaMiga.Maintenance -- reset-admin C:\CasaMiga-Dev
dotnet run --project src/CasaMiga.Maintenance -- restore-installation C:\CasaMiga-Dev\backups\ARCHIVO.cmbackup C:\CasaMiga-Restaurada
```

Cerrar aplicación antes de CLI: ambos cooperan con casamiga.lock. El archivo de lock puede permanecer; no borrarlo para eludir una instancia. backup genera .cmbackup, list lista sólo paquetes completos. Arranque crea paquete completo antes de migrar cuando ya existe terminal.json, y otro después de inicializar/validar. Sin configuración heredada, la instantánea preventiva .db sigue siendo sólo base; el backup final completo requiere terminal válida.

Un backup completo consta del único archivo .cmbackup que contiene casamiga.db, terminal.json y manifest.json. No separar sus componentes. Tiene snapshot SQLite consistente con WAL, hashes y terminal ID comprobados. No incluye logs ni development.settings.json, que no definen identidad de terminal. El paquete no está cifrado: proteger carpeta y segunda copia.

restore-installation exige directorio nuevo, local y absoluto. Comprueba contenido permitido, hashes, integridad/FK, terminal existente en la base e historial de migraciones compatible. Sólo publica el destino completo después de validar. No activa ni reemplaza la instalación actual. Para inspeccionar el candidato, elegir su directorio con CASAMIGA_DATA_DIR e iniciar sesión; nunca operar original y restaurada como si fueran cajas independientes.

La prueba automatizada crea sesión histórica CLOSED, respalda, conserva paquete fuera de instalación, elimina el entorno sintético original, restaura y verifica terminal ID, fondo/contado y estado cerrado. También rechaza paquete alterado y destino existente. La CLI se verifica adicionalmente sobre los datos del smoke.

El comando legado restore <archivo.db> <nuevo.db> sigue disponible para diagnóstico de base, pero NO restaura identidad ni se considera restauración completa. La pérdida de terminal.json con base existente falla de forma explícita; restaurar un paquete completo en vez de generar otra terminal.

Pendientes: retención, segunda copia, cifrado, activación administrativa y rollback operativos, firma, espacio en disco y pruebas físicas de corte eléctrico. Los hashes detectan corrupción, no reemplazan autenticidad ni control de acceso.

## UI y diagnóstico

Theme.xaml centraliza colores, foco y controles principales >=52 DIP. Tab/Enter funcionan en entradas y botones; Escape cancela captura o vuelve desde resultado. Confirmación final requiere acción distinta: Enter en conteo no cierra automáticamente. No se depende de hover. Panel táctil físico pendiente.

Logs técnicos permitidos: arranque, errores por tipo, cash.counted, cash.closed y rechazos mínimos. Auditoría comercial vive en la base. No se registran contraseñas, hashes ni mensajes arbitrarios de excepciones. Logging posterior al commit es best effort; faltan retención y alerta por fallos.

scripts/verify.ps1 restaura locked-mode, compila Release y ejecuta tests. En esta sesión SDK 10.0.400 local y caché NuGet oficial en work; no se desactivó TLS ni se agregaron dependencias para 1B.

## Catálogo — FASE 1C

Tras login, Catálogo permite Productos/Categorías. Seleccionar fila antes de editar o activar/desactivar. Buscar/aplicar actualiza nombre/SKU, categoría y Mostrar inactivos; Enter en búsqueda aplica, Escape cancela editor. Precio MXN usa punto decimal; cero válido. Categorías administrativas incluyen inactivas; consultas operativas las excluyen. Para desactivar una categoría, mover o desactivar primero sus productos activos.

Sólo Administrator/Manager tienen edición por defecto; Cashier consulta. Cambiar permisos requiere un flujo administrativo futuro; nunca editar producción directamente. TrackInventory no modifica existencias. Páginas de 50 productos; listas no representan todos los resultados simultáneamente.

En desarrollo aparece banner DEMO y botón Cargar catálogo DEMO. Es acción explícita, exige catálogo vacío y permisos create; crea tres categorías y diez productos, todos a $18.00 ficticios y descripción DEMO. No es catálogo/precio comercial de Casa Miga. Producción no ejecuta ni permite SeedDemo. Para una demostración nueva, usar carpeta de desarrollo aislada; no borrar catálogo existente para cargarla.

Upgrade v3→v4: respaldo antes, preflight, migración y validación, respaldo después. Si hay nombres/SKU duplicados según normalización o activos en categoría inactiva, se detiene sin ajustar datos automáticamente. Conservar respaldo y revisar copia de desarrollo; no cambiar migraciones aplicadas. Database.Open registra catalog_key_v1/CATALOG_KEY_V1; Maintenance usa ese factory para validar/restaurar.

Backup completo incluye catálogo sin cambiar .cmbackup. Restauración CLI de fase 1C verificada: dos categorías, dos productos (uno inactivo), seis auditorías de catálogo y cuatro cajas CLOSED idénticas. No activar original/restaurada a la vez.

Log técnico añade catalog.saved, catalog.authorization.denied y catalog.operation.failed (sólo tipo). Auditoría comercial permanece en SQLite. El smoke reveló un fallo fatal WPF que sólo apareció en Windows .NET Runtime; fue corregido y repetido. Captura de fallos fatales y alerta de logging siguen como mejora de robustez.

## POS y carrito — FASE 1D

Iniciar sesión, abrir/recuperar caja y pulsar POS. Se requiere usuario activo, catalog.view, pos.use y caja OPEN sin conteo confirmado. Administrator, Manager y Cashier reciben pos.use; Cashier continúa sin edición de productos.

Sólo aparecen categorías activas con productos activos y productos comerciables según actividad de producto/categoría. Categoría filtra; Buscar/actualizar aplica nombre/SKU. Enter busca, Escape enfoca y selecciona búsqueda, Tab recorre controles con foco visible. La consulta devuelve páginas de 50, sin consultas por botón.

Pulsar producto agrega/incrementa. «+» y «−» modifican; «−» queda deshabilitado en 1 y Eliminar quita la línea. Limpiar pide confirmación con Conservar/Descartar. Salir del usuario, comenzar cierre de caja o cerrar la aplicación con partidas requieren el mismo descarte explícito. Conservar cancela la acción. Navegar a Caja/Catálogo mantiene el carrito del usuario actual.

Cart is transient and not a sale. Nombre y precio capturados no cambian con el catálogo. El total es preliminar, sin impuestos/descuentos/pagos. El carrito se pierde al reiniciar y nunca se incluye en backups. Cerrar ventana o logout no cierra financieramente la caja.

Diagnósticos fatales: diagnostics/failure-ID.json, política ADR-009. Si aparece cierre por error, anotar referencia, reiniciar e inspeccionar caja antes de continuar; no asumir que una operación previa quedó sin confirmar. El log omite mensajes arbitrarios, hashes y credenciales. La prueba fatal de desarrollo está aislada y no equivale a un fallo comercial.

development.settings.json ignorado permite PreviewWidth/PreviewHeight y DiagnosticCrashOnStartup exclusivamente con IsDevelopment=true. No activar el fallo deliberado fuera de una instalación sintética. Tamaños de WPF están en DIP: 1920×1080 físicos al 125% no equivalen a una ventana de 1920×1080 DIP. Usar maximización para probar el monitor; en 1D se comprobó además ventana de 1366×768 DIP. No se cambió la configuración de Windows.

## Venta y cobro — FASE 1E (vigente)

Las fórmulas sin ventas de 1B y la ausencia de cobro de 1D son históricas. Ahora Cobrar abre captura de efectivo, no persiste todavía. Introducir MXN con punto decimal, máximo dos decimales y sin separadores de miles. Recibido debe cubrir el total. Volver antes de confirmar conserva carrito; confirmar registra venta/partidas/pago/auditoría juntos. Después de éxito se muestra folio, total, recibido y cambio. Volver al POS acepta el resultado y permite continuar. La X/Escape del resultado no lo acepta: usar Volver al POS.

Ante resultado pendiente, no volver a recibir dinero ni borrar checkout.pending. Usar Verificar resultado e iniciar con el mismo usuario. Si existe la venta, se muestra el resultado original; si la consulta confirma ausencia, se libera el intento y se conserva el carrito que todavía exista en memoria. Si la base no responde o el marcador está corrupto, se bloquea operación para revisión técnica. No hay recuperación administrativa implementada para una cuenta original desactivada/sin permiso: no eludir autorización editando datos.

Esperado = fondo + efectivo aplicado por ventas de la caja. $53 vendidos con $100 recibidos y $47 de cambio aumentan esperado en $53. Conteo sigue siendo ciego y definitivo; desde confirmarlo ya no se vende. sales.complete es explícito para los tres roles, adicional a catalog.view y pos.use. Los permisos de cierre previos no cambian.

Backup incluye todas las ventas/pagos y sus snapshots en SQLite. No incluye carrito ni checkout.pending: antes de respaldo operativo aceptar/verificar resultados pendientes y cerrar normalmente. Tras restaurar un backup, revisar el último cobro conocido y conciliar efectivo antes de operar; no reproducir automáticamente un carrito ni asumir que un backup antiguo contiene cobros posteriores. Una restauración no sustituye resolución del intento de la instalación original. Conservar original y paquete; activar sólo un entorno.

La publicación de desarrollo está en artifacts/development. El smoke WPF de 1E quedó completo mediante confirmación manual y evidencia técnica: cobro, resultado, carrito vacío, recuperación y cierre exacto. También se verificaron servicios, SQLite, backup y restore CLI. Consultar PHASE_1E_REPORT.md. Esto sigue siendo una publicación de desarrollo, no de producción.

## Historial — FASE 1F

Ventas está disponible sólo para usuarios con `sales.view` (Administrator/Manager por defecto). Cashier no ve la opción. Las consultas no requieren abrir o cerrar caja y no deben usarse para editar SQLite. El rango usa días locales del equipo; conservar el offset mostrado al investigar una incidencia.

La publicación de desarrollo vigente está en `artifacts/development`. El smoke con 55 ventas comprobó dos páginas, folio, rango, detalle y permiso de cajero. Backup/restore CLI conserva historial y terminal con el mismo hash lógico. Consultar `PHASE_1F_REPORT.md` y `PHASE_1F_SMOKE.md`. La aplicación quedó cerrada y sigue sin estar lista para producción.

## Ticket e impresión — FASE 1G Bloque A

El ticket se intenta sólo después del commit de venta. Un fallo, configuración ausente o estado UNKNOWN no cambia venta, pago, carrito ni ExpectedCash. SUBMITTED significa aceptado por la cola Windows; verificar siempre el papel.

La configuración física se guarda como `printing.json` en el directorio de datos y no forma parte de `.cmbackup`. Consultar `docs/printing.example.json`, que está deshabilitado y contiene datos sintéticos. No copiarlo habilitado a una instalación real hasta contar con nombre exacto de cola y datos comerciales aprobados.

Bloque A no envía comandos ESC/POS de selección de página, corte o cajón. Para el checkpoint USB seguir `docs/PHASE_1G_PHYSICAL_CHECKPOINT.md`. No abrir USB ni puerto 9100 directamente.

## Reimpresión desde Historial — FASE 1G Bloque F

Administrator y Manager pueden abrir el detalle de una venta y usar `REIMPRIMIR TICKET`; Cashier no tiene `sales.receipt.reprint`. La aplicación revalida usuario activo y permiso en el momento de la solicitud. El botón permanece deshabilitado mientras el trabajo está activo.

La reimpresión reutiliza el snapshot persistido, se identifica como REIMPRESIÓN y no relee catálogo, precios ni perfil comercial actual. Para una venta anterior a 1G sin snapshot, crea y fija el primero una sola vez. Cada clic aceptado crea un request REPRINT; FAILED o UNKNOWN sólo se reintentan por una acción explícita del operador y conservan la relación con el intento anterior.

SUBMITTED sólo confirma aceptación por Winspool. Ante UNKNOWN revisar primero el papel. Reimprimir nunca crea o modifica venta/pago, no cambia ExpectedCash y nunca emite cajón. La configuración física aprobada sigue siendo POS-80, CP850/ESC t 2, logo Opción 07 intacto, 48 columnas, corte parcial GS V 1 y `openCashDrawer=false`.

Smoke final del Bloque F: un único request REPRINT `3ca46975-71af-4643-b80b-ff8290a5d356`, JobId 7, validado físicamente. No repetirlo. FASE 1G está cerrada en su alcance autorizado; cajón continúa deshabilitado hasta contar con hardware y especificación confirmada, y LAN permanece opcional.

## FASE 1I.4 — smoke humano confirmado

En Inventario, Administrator puede verificar configuración, activar, pausar y reanudar con confirmación. Manager sólo ve estado; Cashier no accede. Un preflight verde es informativo: activar/reanudar lo repite bajo transacción. Pausar permite ventas sin descuento automático, no modifica historia. Ante conflicto actualizar/revisar; ante divergencia corregir por proceso administrativo, sin reparación automática. Cambios posteriores de catálogo pueden bloquear ventas individuales en Checkout. Smoke aislado: PHASE_1I4_SMOKE.md. No habilitar producción.

## Cierre FASE 1I / 1I.5

FASE 1I CERRADA tras aprobación humana explícita. Ver PHASE_1I5_REPORT.md y evidencia phase1i5-*. Schema 12, sin migración 013; 402 pruebas aprobadas. Sin cambios arquitectónicos ni política comercial en el cierre. Versión de desarrollo, no lista para producción. No fase posterior iniciada. La caja del smoke permanece OPEN; no usar estos datos sintéticos como instalación comercial.

Inventario: puede elegirse un artículo en la lista bajo el buscador por nombre y código; la selección ejecuta su búsqueda. Tipo y Estado se aplican al pulsar Buscar. El texto de búsqueda sigue disponible.
# Operación 1K.2C — catálogo visual

El POS carga thumbnails derivados, nunca originales, y presenta foto/fallback, nombre y precio. Categoría y búsqueda pueden combinarse; la página conserva hasta 50 productos. La tarjeta completa agrega por el flujo POS vigente. En Catálogo, un usuario con `products.edit` puede agregar/cambiar/quitar foto, elegir REAL/PROVISIONAL y confirmar un crop cuadrado; guardar primero un Product nuevo. No editar archivos de `product-images` ni SQLite manualmente.

Para pruebas de catálogo usar una carpeta `CASAMIGA_DATA_DIR` aislada. El dataset `work/phase1k2c-smoke-20260928-v2` contiene datos sintéticos/autorizados y no representa la instalación comercial. Las credenciales de smoke no aplican a otra instalación. Consultar `product-photo-ui.md`, `phase1k2c-provisional-map.md` y `PHASE_1K2C_REPORT.md`.
