# FASE 1D — Reporte de entrega

Fecha: 2026-09-07. Alcance: pantalla POS, catálogo operativo, carrito en memoria y captura global de errores. FASE 1C aprobada. No se inició 1E.

## Resumen

Se implementó el flujo login → caja OPEN → POS → agregar/modificar/eliminar/limpiar carrito. El baseline de 82 pruebas pasó antes de modificar código. La entrega final pasa 120 pruebas y compila/publica Release sin errores ni advertencias de compilación.

**Cart is transient and not a sale.** No existe venta comercial persistida, cobro, Payment, ticket, movimientos de inventario ni hardware nuevo.

## Captura global de errores

Implementada primero, tras consultar documentación oficial de .NET/WPF. App instala DispatcherUnhandledException, AppDomain.CurrentDomain.UnhandledException y TaskScheduler.UnobservedTaskException antes de inicializar la aplicación.

UnhandledFailureDiagnostics produce un archivo independiente con ID, UTC, origen, tipo, HResult, hasta 16 métodos y cuatro tipos internos. Omite Message, ToString, Data, argumentos, rutas de fuentes, contraseñas y hashes. Es best effort y no toma bloqueos de negocio.

Dispatcher/startup: mensaje fijo comprensible, referencia y terminación con código 1; ningún diálogo normal de carrito puede cancelar esa salida. AppDomain registra y deja terminar al runtime, sin esperar Dispatcher. Tarea no observada: registra y termina según política estricta; la detección puede retrasarse hasta GC. No se intenta continuar tras estado desconocido.

Prueba real aislada: error Dispatcher deliberado, diálogo visible, proceso terminado tras aceptar y diagnóstico sin el marcador secreto. ID 8f6bef070d4a415fb274195f77e625cd, UTC 2026-09-07T04:57:42.3373276Z. Cinco pruebas del componente. Política y fuentes oficiales: [ADR-009](decisions/ADR-009-unhandled-failure-policy.md). Evidencia: [fallo controlado](evidence/phase1d-fatal-smoke.json).

## Pantalla POS

PosView/PosViewModel separados del catálogo administrativo. Categorías horizontales, productos grandes, carrito lateral con controles por partida y totales fijos. Navegación a Caja/Catálogo/Salir. Aviso explícito de carrito temporal y precios DEMO. No se muestra Cobrar.

## Catálogo operativo

Sólo categorías activas con al menos un producto activo. Productos requieren actividad propia y de categoría en SQL/Application. Categorías: SortOrder, nombre normalizado, ID. Productos: nombre normalizado e ID, páginas de 50. Búsqueda por nombre/SKU; Enter aplica.

Consulta reutiliza QueryProducts y agrega ListPosCategories con EXISTS. Prueba con 2,002 productos y espía de llamadas verifica una consulta de categorías y una consulta de página por carga, sin FindProduct por botón. No es un benchmark de hardware ni promesa de latencia. No se agregó un orden POS especulativo.

## Carrito

Cart/CartLine inmutables, sin persistencia. Cada línea conserva ProductId, ProductName, UnitPrice, Quantity y LineTotal. Producto repetido incrementa la misma línea. Cantidad entera positiva; desde 1, disminuir se rechaza y la UI ofrece Eliminar.

Nombre/precio se capturan al agregar la primera vez y se conservan aunque cambie el catálogo. Agregar desde catálogo vuelve a comprobar actividad; modificar cantidades de una línea existente conserva su snapshot incluso si luego se desactivó. La futura conversión a venta deberá revalidar comerciabilidad bajo una política aprobada en 1E.

Limpiar requiere confirmación. Cierre normal de aplicación con partidas pide descarte; un fallo fatal pierde el carrito. No hay recuperación de carrito tras reinicio ni inclusión en backups.

## Money/totales

Centavos Int64 y operaciones checked; sin float/double. LineTotal=UnitPrice×Quantity; Subtotal=ΣLineTotal; Total preliminar=Subtotal. Cero permitido, negativos rechazados. Overflow de cantidad, multiplicación y suma probado; no publica estado parcial.

Precios de smoke ficticios: Concha vainilla 1900 centavos y Bolillo 500 centavos. No representan precios autorizados del negocio.

## Permisos

Se requieren cuenta activa, catalog.view y pos.use. Migración 005 otorga pos.use a Administrator, Manager y Cashier; Cashier sigue sin permiso de edición de productos. Application valida cada operación; ocultar botones no constituye la autorización.

## Caja/sesión

POS exige terminal válida y caja OPEN sin conteo confirmado. El carrito se asocia a usuario, terminal y sesión de caja. Cambio de usuario/contexto con partidas se rechaza hasta descarte explícito. SignOut/SignIn y CashClosingService protegen la regla fuera de UI.

Logout y comenzar cierre de caja muestran Conservar/Descartar. Conservar cancela; Descartar vacía y permite continuar. Conteo/cierre no recibe importes del carrito. La caja OPEN persiste al cerrar/reabrir la app; el carrito no.

## UI/touch readiness

Controles principales de al menos 52 DIP, productos de 180 DIP de ancho y 112 de alto mínimo, separaciones y recursos centralizados. Diálogo de descarte propio con botones grandes, foco inicial en Conservar y sin aceptar destructivamente por defecto. Totales permanecen visibles; partidas desplazan dentro del panel.

Mouse, Tab, Enter, Escape y foco visible comprobados. Escape en POS enfoca búsqueda; Enter busca; Tab enfoca el botón. Durante smoke se compactaron filas para mostrar dos partidas completas en ventana menor y se corrigió la notificación de Search para ocultar inmediatamente el texto de ayuda. La última publicación se abrió y verificó escribiendo Concha, sin superposición, y aplicando Enter.

Pantalla táctil física y validación exhaustiva con tecnologías de asistencia permanecen pendientes. Placeholder de identidad visual explícito; no se inventó logotipo.

## Resoluciones probadas

- Ventana WPF de **1366×768 DIP**, captura útil aproximada 1354×762: dos partidas, botones, navegación, subtotal y total visibles; scroll interno para listas mayores.
- Monitor **1920×1080 físicos**, configuración verificada visualmente en Windows, escala **125%**, app maximizada: captura lógica 1536×816 por escala/área de trabajo. Dos partidas, controles y total visibles.
- También se intentó ventana 1920×1080 DIP: excede el escritorio a esa escala y se recorta. Se comprobó el monitor mediante maximización; no se cambió resolución ni escala de Windows.

Esto distingue viewport lógico de resolución física. No se certificó un monitor físico 1366×768 ni todas las escalas; la prueba menor fue de ventana. Repetir en el equipo POS físico antes de producción.

## Tests

120 aprobadas, 0 fallidas y 0 omitidas. Se preservan las 82 previas y se agregan 38 casos: carrito, snapshots, cero/negativos/overflow, autorización, actividad, caja/conteo, cambio de usuario/terminal/caja, descarte, no escrituras comerciales, reinicio/backup, catálogo grande y diagnóstico.

[Verificación final](evidence/phase1d-verification.txt); [baseline](evidence/phase1d-baseline.txt). No se afirma ejecución automatizada de todos los posibles fallos del proceso: Dispatcher se comprobó en UI y los orígenes se cubren en el componente testeable.

## Build

SDK 10.0.400, .NET 10.0.11. Clean, restore, build Release, tests y verify.ps1 ejecutados. Publicación autocontenida win-x64 realizada después de la última corrección visual; restore posterior recupera lockfiles originales.

0 errores y 0 warnings de compilación. Aviso real del runner: se sobrescribe foundation.trx existente. Durante desarrollo se corrigió una asignación ignorada de BeginInvoke; no queda advertencia en la entrega. Sin nuevas dependencias, lockfiles iguales al baseline.

[Clean](evidence/phase1d-clean.txt), [restore](evidence/phase1d-restore.txt), [publicación](evidence/phase1d-publish.txt).

## Smoke test

Aplicación WPF publicada, instalación aislada y usuario/productos sintéticos. Casos:

| Caso | Resultado observado |
|---|---|
| A | Caja abierta con fondo cero; PAN DULCE, Concha x2, una línea y $38.00. |
| B | PAN SALADO, Bolillo x3; dos líneas y $53.00. |
| C | Bolillo x4 → $58.00; disminuir a x3 → $53.00. |
| D | Eliminar Bolillo → Concha x2, subtotal $38.00. |
| E | Limpiar y confirmar → vacío, $0.00. Repetido con el diálogo táctil final. |
| F | POS sin caja abierta rechazado con “Abre una caja antes de usar el POS.”; permanece en Caja. |
| G | Logout/cerrar caja con partidas pide descarte; Conservar cancela. Logout con Descartar vuelve al login; reingreso empieza vacío. |
| H | Cerrar app con Concha, confirmar descarte, reabrir/login/POS: carrito vacío y misma caja OPEN. |

Los casos A–E iniciales se comprobaron también en ventana maximizada; las filas compactadas se verificaron después en 1366×768 DIP. El descarte previo al conteo/cierre se cubre además en tests; no se hizo un cierre financiero nuevo durante G.

Logs de instalación de smoke: seis eventos, ninguno con errorType ni evento failed. Un diagnóstico fatal deliberado existe sólo en la carpeta de prueba de errores; no se oculta como si no hubiera ocurrido. No se encontraron diagnósticos fatales en la instalación del smoke operativo.

## Backup

Formato .cmbackup intacto. CLI crea y restaura a work/phase1d-cli-restored, sin activar ni reemplazar origen. Comparación exacta: 2 categorías, 2 productos, 5 sesiones (4 CLOSED/1 OPEN), 4 conteos, 21 auditorías, 5 migraciones y terminal idénticos. terminal.json coincide.

No existen tablas cart/sales/sale_items/payments. La prueba automatizada de backup parte de un carrito no vacío y confirma que una sesión nueva inicia sin él. [Comparación](evidence/phase1d-smoke-and-restore.json), [CLI backup](evidence/phase1d-cli-backup.txt), [CLI restore](evidence/phase1d-cli-restore.txt).

## Git

master sin commits; user.name/user.email ausentes y sin remoto. No se inventó identidad ni se publicó código. La revisión usa hashes contra work/phase1d-before porque git diff no representa fuentes todavía sin seguimiento. Migraciones 001–004 y seis lockfiles idénticos al baseline. Datos, credenciales, backups y binarios permanecen ignorados.

El propietario debe configurar datos reales:

```powershell
git config user.name "Tu nombre real"
git config user.email "Tu correo real"
```

Después conviene registrar el baseline aprobado 1C y esta fase por separado, conservando el árbol actual. El snapshot disponible no es un commit. [Revisión de fuentes](evidence/phase1d-source-review.json).

## Riesgos

No listo para producción. Pendientes: pantalla táctil física, equipo menor real y otras escalas, recuperación/administración de usuarios, retención/segunda copia/cifrado, restauración operativa y fallos físicos. No hay cobro, stock ni fiscalidad.

El carrito se pierde al terminar el proceso. El diagnóstico no garantiza escritura ante falta de disco, memoria o terminación externa. Los snapshots no constituyen precio/comerciabilidad definitivos de una venta futura. Falta historial Git real.

## Decisiones

Carrito immutable en UserSession; descarte explícito; decremento mínimo 1 con eliminación separada; cero permitido; snapshots estables; categorías sólo con productos activos; búsqueda paginada y orden estable; sin campo especulativo ni auditoría comercial de carrito. Una única migración 005 para permiso pos.use. Política fatal estricta y diagnóstico sin secretos según ADR-009.

## Próximo paso recomendado

Revisar esta entrega, configurar identidad Git real y acordar alcance/reglas de 1E antes de autorizarla. **Detenerse aquí: no se inició venta persistida, pago, cobro, ticket ni hardware.**
