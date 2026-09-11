# Operación de caja, venta y respaldo — FASE 1E

Sólo desarrollo; venta y cobro efectivo implementados en 1E, sin hardware ni uso de producción. Los apartados previos por fase conservan su contexto histórico; el apartado 1E contiene las reglas vigentes.

## Inicio y conteo

Ejecutar dotnet run --project src/CasaMiga.Presentation con SDK .NET 10. CASAMIGA_DATA_DIR permite ruta local absoluta; por defecto LocalAppData/CasaMigaPOS. Para publicación de pruebas se admite development.settings.json junto al ejecutable, IsDevelopment true y DataDirectory absoluto; marca DESARROLLO. No usar carpeta sincronizada ni compartida.

En base nueva, crear primer administrador con contraseña de 12–128 caracteres, sin contraseña predeterminada. Login explícito en cada reinicio. No existe editor de usuarios ni recuperación de contraseña.

Abrir con fondo no negativo. Mientras la caja está abierta, se muestran usuario, terminal y hora; el fondo se oculta porque en 1B equivale al esperado. Cerrar caja → capturar contado → Continuar guarda irrevocablemente el conteo y muestra resultado → Confirmar cierre. Cancelar/Escape antes de Continuar no registra conteo; Volver/Escape después conserva el conteo y regresa a caja abierta. Cerrar caja nuevamente retoma el resultado, sin editar el importe.

No pulsar Continuar hasta terminar el conteo. No existe corrección posterior en 1B; un futuro ajuste tendrá que ser explícito y auditado. Un reinicio entre conteo y cierre recupera el conteo al retomar. Salir del usuario/cerrar ventana no cierra caja.

Propietario: cash.close. Tercero: cash.close y cash.close.other. Administrator y Manager tienen ambos. Cashier conserva open/view de 1A y no recibe automáticamente close ni close.other; un rol autorizado requiere una futura administración controlada. No editar manualmente producción. Los permisos se revalidan en Application al contar, retomar y cerrar. Si se cierra una sesión ajena, la UI lo indica y la auditoría conserva propietario, contador y cerrador.

Fórmula 1B: esperado = fondo; entradas, retiros, ventas y devoluciones son cero. Diferencia = contado − esperado. Cero es caja exacta; positivo sobrante; negativo faltante. Tras cerrar puede abrirse otra sesión con otro ID; la histórica queda CLOSED e inmutable.

## Backup completo

```powershell
dotnet run --project src/CasaMiga.Maintenance -- init C:\CasaMiga-Dev
dotnet run --project src/CasaMiga.Maintenance -- backup C:\CasaMiga-Dev
dotnet run --project src/CasaMiga.Maintenance -- list C:\CasaMiga-Dev
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
