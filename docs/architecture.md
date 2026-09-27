## Revisión 1J.5

Cambios acotados al refresco en RecipeViewModel y selector en InventoryView/InventoryViewModel mediante InventoryService.List. No SQL desde Presentation ni reglas Domain duplicadas. No cambia Domain, ProductionContract, IDs deterministas, HasPermission ni writer de inventario. Pruebas nuevas verifican stock cambiante mediante InventoryService, nueva sesión y rechazo de otro actor/terminal, y backup/restauración mixtos. Los datos de negocio siguen pasando por Application.

# Estado vigente — servicios de producción 1J.3

ProductionService y RecipeService en Application dependen de puertos propios. SqliteProductionUnitOfWork comparte conexión/transacción con SqliteRecipeStore y el writer existente de inventario. Permisos y auditoría dentro de la transacción; recuperación histórica antes de catálogo vivo. No composición/UI productiva.

Ver ADR-023 y PHASE_1J3_REPORT.md para contrato, evidencia y limitaciones.

## Registro anterior (histórico)

# Estado vigente — persistencia técnica 1J.2

SqliteRecipeStore y su parcial Batches residen exclusivamente en Infrastructure y no se registran en composición/UI. Reutilizan los modelos y planificador puro de Domain 1J.1 sin modificar Domain. Escrituras en BEGIN IMMEDIATE, reemplazos de borrador con revisión esperada, publicación y activación explícitas; lecturas read-only consistentes. Permisos y auditoría de configuración quedan para una futura frontera Application.

El repositorio sólo escribe configuración/snapshots de lotes. No valida stock, no reserva, no confirma producción, no escribe movimientos ni saldos ni dinero. El writer existente conserva la autoridad de InventoryBalance. Sólo se amplía su parser de lectura para dos futuros tipos. El contrato SQL prepara una transacción recíproca batch/consumo/salida con auditoría e inmutabilidad; su ejecución productiva NO está implementada. Ver ADR-022 y PHASE_1J2_REPORT.

## Registro arquitectónico anterior

# Arquitectura

## Inventario cerrado — FASE 1H

La ruta final es Presentation → InventoryService → IInventoryStore/ILocalReadUnitOfWork → SQLite. Application revalida usuario activo y permiso; UI sólo expresa disponibilidad y mantiene Busy/OperationId estable. Escrituras usan transacción inmediata e idempotencia UUID+SHA-256; lecturas usan conexión read-only y transacción deferred. El ledger confirmado es autoritativo y `InventoryBalance` es una proyección con reconciliación sin reparación automática.

Restart y `.cmbackup` preservan artículo, presentación, vínculo, operaciones, movimientos, saldos, permisos, auditoría y terminal. Restore sólo publica en un directorio nuevo después de validar manifiesto, base y terminal. 1H.5 no cambió código ni arquitectura y no agregó esquema. Checkout y el subsistema de tickets siguen independientes de `ProductInventoryLink`; el efecto `SALE_CONSUMPTION` no existe.

## Inventario 1H.2 (registro histórico)

Infrastructure contiene `SqliteInventoryStore`, dueño de la transacción completa y única autoridad de `InventoryBalance`. Domain aporta solicitudes normalizadas y cantidades exactas; todavía no existe un caso de uso Application ni UI.

La ruta de escritura es: hash canónico, BEGIN IMMEDIATE, idempotencia, PENDING, partidas ordenadas, lectura de saldo, cálculo checked, validación no negativa, movimientos, actualización condicionada del saldo, auditoría, CONFIRMED y commit. Ante excepción se abre otra conexión y se consulta OperationId; un CONFIRMED con el mismo hash se devuelve sin repetir.

La reconciliación es una lectura delimitada que suma el ledger y compara la proyección. No corrige automáticamente. SQLite sigue siendo local y no debe compartirse por red.

Monolito modular Windows offline con SQLite local. Dependencias hacia dentro: Presentation → Application → Domain; Infrastructure implementa puertos de Application. Presentation referencia Infrastructure exclusivamente para composición. No hay servidor, ORM, framework CQRS ni repositorios genéricos.

## FASE 1A implementada

- Domain: Money en centavos Int64 checked y CashSession inmutable; fondo no negativo, estado y metadatos coherentes. Sin reloj del sistema ni dependencias de persistencia/UI.
- Application: IdentityService, CashService, UserSession, IClock, IPasswords y puertos de unidad de trabajo/store. Autoriza usuario activo y permiso dentro de la operación. La UI no entrega un actor arbitrario para abrir caja.
- Infrastructure: PasswordHasher oficial, SystemClock, adaptador SQLite, migraciones, backup, logs, configuración estable de terminal y DirectoryLease.
- Presentation: MainViewModel con estados de bootstrap/login/caja/apertura; code-behind limitado a eventos, contraseñas y foco. Recursos visuales centralizados, sin biblioteca MVVM adicional.

SqliteLocalUnitOfWork abre conexión y transacción inmediata por operación. Store no hace commits. Apertura comprueba permiso, terminal y sesión existente; crea aggregate y auditoría y confirma todo junto. Si algo falla, Dispose revierte. Índice UNIQUE parcial es la última barrera de concurrencia. Bootstrap también une usuario y auditoría. Login persiste fallos/bloqueo/rehash antes de establecer la sesión en memoria.

DirectoryLease impide dos clientes oficiales sobre el mismo directorio. No reemplaza la restricción SQLite ni ofrece coordinación distribuida. El arranque respalda una base existente antes de migrar; exige versiones contiguas y hashes intactos, valida y genera otra copia después. Maintenance coopera con el mismo bloqueo para init/backup/list.

Tiempo de negocio: IClock.UtcNow, inyectado en Application y pasado al dominio. UTC persistido; UI utiliza zona local del sistema y muestra offset explícito. Falta configuración de zona comercial para reportes. Logs técnicos usan su propio timestamp y campos permitidos, nunca mensajes arbitrarios ni credenciales. Fallos de log posteriores al commit no revierten la apertura; falta alertar/retener esos fallos.

## Evolución pendiente

Ventas persistidas, inventario y hardware siguen fuera del alcance actual; catálogo y POS transitorio se describen en 1C/1D más abajo. Una venta futura deberá persistir snapshots, pagos, movimientos, auditoría y trabajo de impresión en un commit, ejecutando hardware después. Dinero recibido/cambio se separará del importe aplicado. No hay servicios físicos activos en esta entrega.

Identidad y permisos: ADR-006. No existe seguridad frente a un administrador Windows con acceso al archivo. Antes de producción: ACL, recuperación de cuentas, restauración administrativa, retención, espacio en disco y pruebas de equipo real. El backup completo de terminal.json queda cubierto en 1B.

## FASE 1B implementada

CashClosingService autoriza cuenta activa, cash.close y adicional cash.close.other si cambia el actor respecto al propietario. Resume sólo devuelve resultado si existe conteo persistido. SubmitCount confirma cash_count y cash.count audit en una transacción inmediata antes de revelar resultados. Close no acepta importes desde UI: carga conteo inmutable, calcula esperado y llama CashSession.Close. Actualiza sólo OPEN y crea cash.close audit en el mismo commit.

Dos transacciones deliberadas: confirmar conteo y confirmar cierre. Entre ellas la sesión sigue OPEN, pero el conteo no se puede editar ni borrar. Volver/reinicio no reinician el conteo. Concurrencia de cierres termina con un solo cambio de estado y una sola auditoría. Finanzas de CLOSED protegidas también por triggers.

Esperado = fondo + entradas − retiros + ventas efectivo − devoluciones efectivo. En 1B todos los componentes salvo fondo son cero. El cálculo se concentra en Application dentro de la unidad de trabajo; el aggregate recibe esperado/contado y calcula diferencia checked. Antes de añadir movimientos se deberá congelar actividad desde conteo o validar versión/snapshot; no hay movimientos implementados ahora.

Presentation añade conteo/resultado/cierre y Escape/Enter. Por prioridad del conteo ciego no muestra fondo en dashboard abierto; no se expone esperado/diferencia hasta guardar conteo. Confirmación final separada; un Enter en el campo de conteo no ejecuta cierre.

InstallationBackup empaqueta snapshot SQLite, terminal.json y manifiesto de hashes; creación bajo lease y publicación por rename. Restore valida en carpeta candidata y publica sólo un directorio nuevo. BackupService queda como primitiva de snapshot/compatibilidad con .db anteriores. TerminalConfiguration rechaza falta de configuración si ya existe identidad en la base. ADR-007 y operations.md detallan límites y recuperación.


## FASE 1C implementada

Domain añade Category/Product inmutables y CatalogText sin dependencias. Application incorpora CatalogService y contratos de lectura paginados; revalida permisos en casos de uso, no depende de botones deshabilitados. Infrastructure implementa los nuevos puertos en Store parcial, consultas parametrizadas, índices y triggers. Transacción inmediata contiene cambio y auditoría; si falla cualquier paso se revierte.

Presentation añade CatalogViewModel/CatalogView y navegación Caja/Catálogo. Listas de productos paginadas y grids virtualizados, recursos centralizados, formularios separados de listas. Binding OneWay explícito sobre valores inmutables de categoría. La colección de filtros conserva instancia si no cambia; se limpia estado al entrar con usuario nuevo. No hay librería MVVM nueva.

ActiveProducts y Categories(false) sirven para lectura futura del POS. SaleItem deberá guardar snapshots; imágenes/color/iconos podrán añadirse después sin blobs actuales. Precio histórico permanece en audit_entry, sin módulo comercial separado. ADR-008 define normalización persistente e implicaciones de mantenimiento. Catálogo deja de ser pendiente; venta, inventario y hardware siguen propuestos.

## FASE 1D implementada

Domain añade Cart/CartLine inmutables sin referencia a persistencia. Application incorpora PosService y mantiene el carrito en UserSession, con asociación usuario/terminal/caja. Cada operación revalida contexto y autorización dentro de la unidad de trabajo. El nuevo estado se publica sólo después de validación, cálculo checked y finalización de la unidad de trabajo.

UserSession serializa cambios mediante un lock. PosService y CashClosingService toman primero ese lock y luego la unidad SQLite, evitando inversión del orden. SignOut/SignIn y conteo/cierre impiden saltarse la política de descarte. Descartar permite recuperar una sesión aun si ya perdió autorización; no escribe datos comerciales.

La consulta POS reutiliza QueryProducts con IncludeInactive=false, y ListPosCategories devuelve activas con EXISTS de producto activo. No carga productos individualmente para dibujar botones. PosViewModel/PosView son independientes de administración; la UI consume sólo la página actual de 50 y deja fijo el bloque de totales al desplazar partidas.

Cart is transient and not a sale. No hay serializador/repositorio de carrito, ni venta/pago nuevo. Migración 005 sólo instala pos.use. No hay librerías adicionales.

Captura global instalada en el constructor de App antes de inicializar: DispatcherUnhandledException, AppDomain.UnhandledException y UnobservedTaskException. UnhandledFailureDiagnostics escribe un informe técnico acotado y best effort fuera del bloqueo de negocio. La política estricta termina el proceso en vez de ocultar el fallo; detalles, límites y documentación oficial en ADR-009.

## FASE 1E implementada (vigente)

Sale/SaleItem/Payment viven en Domain; CheckoutService coordina autorización, contexto, snapshot, secuencia y auditoría. Infrastructure añade Store parcial SqliteSaleStore y CheckoutJournal. ILocalStore incorpora NextSaleSequence, AddSale, FindSaleByOperation y CashSalesTotal. No se añaden paquetes ni dependencias de UI al dominio.

Orden de bloqueos: UserSession y después transacción SQLite inmediata. El UUID del intento se escribe y fuerza a disco antes de la transacción comercial. El marcador no almacena importe ni partidas. Confirm encuentra primero un resultado por operación; si existe valida actor/contexto/contenido y devuelve la misma venta sin insertar nada. Si falla Run, una lectura independiente determina si hubo commit. Si tampoco puede leer, conserva el marcador y carrito y exige recuperación; nunca vuelve a cobrar automáticamente.

Sólo la confirmación conocida permite vaciar la misma instancia de carrito. No se borra un carrito nuevo al devolver una operación antigua. El marcador permanece hasta Acknowledge después de mostrar el resultado: un reinicio entre commit y pantalla puede recuperarlo. Un reintento conflictivo no borra un marcador anterior. PosService, descarte y conteo/cierre bloquean intentos pendientes también desde Application. Nueva sesión de usuario puede iniciar login para recuperar el UUID persistido; no recupera el carrito transitorio.

CashClosingService suma efectivo aplicado de ventas dentro de su unidad de trabajo. La exclusión de ventas al confirmar conteo y la serialización SQLite mantienen consistente el resultado al competir cobro y conteo. CheckoutDialog/ViewModel agregan captura de recibido, cambio, confirmación explícita y recuperación. El resto del POS conserva comportamiento y recursos visuales aprobados.

## FASE 1F implementada (vigente)

Application añade `SalesHistoryService` y contratos de consulta. Autoriza `sales.view` en cada operación, convierte días de la zona local a límites UTC y mantiene separados los modos folio/rango. No exige caja abierta ni permiso de venta.

Infrastructure implementa `ILocalReadUnitOfWork` mediante conexión read-only y transacción deferred. La ruta comercial inmediata no cambia. El listado usa COUNT más una página de joins; el detalle usa encabezado/pago/contexto más partidas ordenadas. ADR-011 documenta la decisión y la prueba concurrente contra Checkout.

Presentation incorpora Ventas, paginación de 50, folio exacto, rango, detalle y control de visibilidad por permiso. Los nombres de usuario/terminal son actuales y los IDs permanecen históricos. Sale/SaleItem/Payment y ExpectedCash no se modificaron.

## Frontera preparada en 1I.2

SqliteInventoryStore.WriteOperation reutiliza el escritor de ledger/balance sobre una transacción del llamador. SqliteSaleInventoryStore es interno y sella evaluación y snapshots sin abrir conexión ni hacer commit; un savepoint elimina escrituras técnicas parciales ante fallo. RunTechnical permite probar venta/pago y consumo en una misma transacción sin cambiar ILocalUnitOfWork ni CheckoutService. No hay llamadas desde la aplicación, activación ni preflight. El esquema 11 queda DISABLED. ADR-018 fija FKs diferidas, UUIDv5 V1 e idempotencia para 1I.3.

## Integración 1I.3

La frontera preparada de 1I.2 ya está integrada: Checkout usa BEGIN IMMEDIATE y AddSale completa evaluación/consumo sobre la transacción existente. Snapshot de ticket configurado y auditoría comercial preceden al commit; impresión posterior. Contrato persistente 0/1 en Sale separa recovery histórico del obligatorio, sin reevaluar catálogo ni existencias actuales. Ver ADR-019 y PHASE_1I3_REPORT.md. No activación ni preflight 1I.4.

## FASE 1I.4 — smoke humano confirmado

SaleConsumptionService combina ILocalReadUnitOfWork para status/preflight informativo e ILocalUnitOfWork BEGIN IMMEDIATE para transición. CheckConsumption comparte reglas; autorización, expected state/version, preflight, UPDATE y auditoría son atómicos. Reconciliación reutiliza el algoritmo existente con conexión del llamador. Ver ADR-020.

## Cierre FASE 1I / 1I.5

FASE 1I CERRADA tras aprobación humana explícita. Ver PHASE_1I5_REPORT.md y evidencia phase1i5-*. Schema 12, sin migración 013; 402 pruebas aprobadas. Sin cambios arquitectónicos ni política comercial en el cierre. Versión de desarrollo, no lista para producción. No fase posterior iniciada. La caja del smoke permanece OPEN; no usar estos datos sintéticos como instalación comercial.

## FASE 1J.1 — Domain puro

Fórmulas técnicas compartidas sin inventario intermedio obligatorio; FormulaVersion sólo referencia ingredientes físicos. ProductProductionProfileVersion referencia versiones de fórmula y componentes directos. ProductionPlan agrega preparación por versión antes de escalado y COUNT por línea; ProductionActualOutput no recalcula consumo. Ver ADR-021 y PHASE_1J1_REPORT.md. Sin schema, persistencia, UI, permisos ni cambios de 1I. 1J abierta; 1J.2 no iniciada.
