# FASE 1C — Catálogo base

Fecha: 2026-09-06. Alcance autorizado por USER_REQUIREMENTS_PHASE_1C.md. FASE 1B aprobada; esta entrega queda lista para revisión. No se inició FASE 1D.

## Resumen

Catálogo administrable de categorías y productos integrado con identidad, caja y respaldo existentes. Se completó el flujo de creación, edición, estado, consulta, cierre/reapertura y restauración. Los datos usados son sintéticos. No es una entrega habilitada para producción.

## Categorías

GUID, nombre, descripción opcional, orden Int32, activo y fechas UTC. Crear, editar y activar/desactivar. Normalización de unicidad: trim, Unicode NFC y mayúscula invariante; se conserva el caso visual. Acentos y espacios interiores significativos. Nombre no vacío hasta 100 caracteres. Unicidad incluye inactivas. Sin borrado físico; desactivar con productos activos se rechaza sin cascada.

## Productos

GUID, categoría GUID, SKU opcional, nombre, descripción, precio Money, activo, TrackInventory y UTC. Nombre hasta 160; SKU hasta 80, vacío NULL y presente normalizado/único incluso inactivo. Producto activo requiere categoría activa. Reactivación auditada. TrackInventory sólo metadato, true inicial para físicos; no hay stock ni movimientos.

Demo controlada: acción explícita sólo desarrollo y catálogo vacío; PAN DULCE/PAN SALADO/BEBIDAS y diez productos pedidos. Todos los precios del seed son $18.00 ficticios y sus descripciones identifican DEMO. Sin seed automático ni precios definitivos.

## Modelo monetario

ADR-003 intacto: MXN en centavos Int64, operaciones checked; decimal sólo para captura/formato. $18.00 corresponde a 1800 centavos. Cero permitido técnicamente para artículos sin cargo, sin imponer un mínimo comercial arbitrario. Negativos y fracciones de centavo rechazados. No floating point.

## Permisos

catalog.view; categories.create, categories.edit, categories.deactivate; products.create, products.edit, products.deactivate. Administrator/Manager reciben los siete; Cashier sólo catalog.view. Application revalida cuenta activa y permiso dentro de cada caso de uso. Editar no altera estado; deactivate autoriza ambos cambios de estado. UI refleja permisos, sin sustituirlos.

## Auditoría

Crear/editar/activar/desactivar generan eventos con actor, terminal, entidad, UTC y before/after. Producto incluye nombre, descripción, CategoryId, SKU, priceCents, activo y TrackInventory. Consultas/no-op no generan ruido. La evidencia real confirma SMOKE-ADMIN cambió Concha vainilla de 1800 a 1900 centavos el 2026-09-06T23:52:16.2503443Z, con terminal e ID de producto.

Audit_entry basta para reconstruir cambios en esta fase; no se duplicó historial. El futuro SaleItem deberá guardar snapshots, sin consultar el precio actual para reinterpretar ventas históricas.

## Base de datos

Migración 004: metadatos de categoría, índices únicos normalizados, orden/filtro, triggers contra borrado y estados incoherentes, siete permisos. 001–003 iguales byte a byte según SHA-256. Base vacía y upgrade desde v3 probados. Preflight rechaza conflictos previos y verifica índices/triggers antes de registrar v4; rollback mantiene v3 ante conflicto.

Database.Open registra catalog_key_v1 determinista y CATALOG_KEY_V1 en cada conexión. El contrato es versionado y no debe cambiar silenciosamente con una actualización de runtime. Fechas de categorías legadas toman la fecha registrada de 001 como referencia, porque no existía creación comercial conocida.

## Transacciones

Se conserva SqliteLocalUnitOfWork con transacción inmediata. Mutación y auditoría se confirman juntas; fallos revierten ambas. Índices únicos protegen carreras, además de reglas de dominio/Application. Probados rollback de categoría, creación/producto/precio y concurrencia de nombre duplicado.

## UI

WPF conserva estilo cálido y recursos centralizados. Navegación sólo Caja/Catálogo, listas/formularios separados, búsqueda nombre/SKU, categoría, inactivos, páginas SQL de 50 y grids virtualizados. Mouse/teclado usados en smoke; táctil físico pendiente. Categorías administrativas muestran también inactivas.

Se corrigieron dos hallazgos visuales: binding TwoWay a Category.IsActive inmutable causaba cierre; ahora OneWay. Se conserva la instancia de filtros cuando no cambia para estabilizar selección al refrescar. Flujo repetido después de corregir.

## Tests

Baseline previo: 49/49. Final: 82 aprobadas, cero fallidas/omitidas. Conserva las anteriores y añade 33 casos de validaciones, normalización Unicode, permisos, auditoría, rollback, estados, búsqueda, persistencia, backup, migraciones y concurrencia. Consulta con 2,000 productos devuelve páginas estables de 50 sin solaparse. No se tomó el tiempo del runner como benchmark de consultas.

Evidencias: phase1c-baseline*.txt, phase1c-verification.txt y CatalogTests.cs. Las adaptaciones de tests anteriores sólo reflejan las nuevas columnas, versión y cantidad de permisos.

## Build

Clean, restore, Release y scripts/verify.ps1 ejecutados después de las correcciones. SDK 10.0.400, paquetes oficiales locales; restore locked-mode correcto, sin dependencias nuevas ni cambios de lockfiles. Cero errores y cero warnings de compilación. Único aviso del runner: sobrescribe foundation.trx existente. Publicación win-x64 autocontenida en artifacts/development para smoke; no despliegue ni instalador de producción.

## Smoke test

A: PAN DULCE y Concha vainilla DEMO $18.00 aparecen. B: edición a $19.00 confirmada en UI/auditoría. C: PAN SALADO y Bolillo DEMO $5.00, filtro muestra sólo Bolillo. D: desactivado desaparece de activos y aparece Inactivo al incluirlos. E: PAN DULCE rechaza desactivar porque contiene Concha activa. F: cerrar/reabrir/login preserva Concha $19 y Bolillo inactivo $5; filtro estable. Aplicación quedó cerrada.

Incidencia inicial WPF corregida y documentada: commit de primera categoría sobrevivió al fallo de render. Windows .NET Runtime capturó excepción; el log técnico de la aplicación no la registró. Ocho eventos técnicos de fase 1C sin errorType ni credenciales; esto no oculta la incidencia inicial.

## Backup/restauración

Formato .cmbackup sin cambios: casamiga.db, terminal.json y manifiesto. Test automatizado de catálogo y CLI real backup/restore-installation sobre smoke. Destino nuevo validado; comparación de categorías, productos, seis auditorías de catálogo, terminal, versiones y cuatro cajas CLOSED coincide exactamente con origen. No se reemplazó instalación existente. Evidencia phase1c-smoke-and-restore.json y phase1c-cli-restore.txt.

## Git

Rama master sin commits; fuentes siguen sin seguimiento como en baseline. Sin autor inventado, commit ni remoto. git diff --check no detecta problemas, pero no cubre fuentes untracked: además se comparó snapshot work/phase1c-before, se revisaron cambios y se comprobaron hashes/espacios finales. Cero archivos originales eliminados, cero dependencias/lockfiles modificados y migraciones 001–003 intactas. Datos, contraseñas, paquetes compilados y respaldos fuera de Git. Avisos CRLF→LF de git diff --no-index reflejan .gitattributes, no warnings de build.

## Riesgos

Pendientes de producción: captura de fallos fatales WPF, recuperación de usuarios, backup con retención/segunda copia/cifrado/activación operativa, pruebas táctiles y cortes físicos. Normalización Unicode persistente requiere compatibilidad al actualizar runtime. Búsqueda substring recorre candidatos, no hay garantía de latencia en equipo final; categorías se cargan completas. No hay protección frente a administrador local que altere el esquema. No existen ventas, pagos, tickets, stock, recetas, impuestos, imágenes ni hardware.

## Decisiones

ADR-008 documenta normalización persistente e historial por auditoría. Se mantiene cero permitido, rechazo de categoría con hijos activos, SKU opcional, desactivación en lugar de borrado, paginación 50 y demo exclusivamente explícita. Sin nuevas dependencias ni cambio de identificadores/representación monetaria/formato de backup.

## Próximo paso recomendado

Revisar y aprobar FASE 1C. Definir y autorizar el alcance de 1D antes de iniciar ventas, snapshots o pagos. El trabajo se detiene aquí.
