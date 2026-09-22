# CASA MIGA POS — REPORTE FASE 1J.3

Fecha: 2026-09-22. Baseline: a1e7ef044f215a680526d65b7ea62d575d870b35. Implementación y verificación mediante Codex; modelo recomendado por autorización: Astra. No se certifica un identificador comercial del runtime que no esté expuesto por la sesión.

## Alcance y estado

FASE 1J ABIERTA. 1J.3 funcional COMPLETADA. Schema 15, sin migración 016. 1J.4 no iniciada; sin UI, impresión, rediseño ni datos productivos. Desarrollo, NO listo para producción. El commit que incorpora este reporte y su evidencia identifica el punto de recuperación final en Git.

## Servicios y política (puntos 11–33)

ProductionService expone Prepare/Create/Get/List/ConfirmProduction. RecipeService expone consultas, creación de propietarios, guardar drafts con revisión, publicar y activar Formula/Profile. Cada frontera verifica permisos persistidos dentro de la transacción. No modifica HasPermission.

Prepare usa versiones publicadas exactas, agrega fórmulas compartidas antes de escalar e inputs físicos por artículo en MAIN. Selección explícita no se sustituye; sin ella usa preferencias persistidas y fallback elegible. COUNT aplica EXACT/CEILING del Domain. MASS/VOLUME no representable devuelve problema con fórmula, artículo y racional; lote permanece DRAFT. Preview no reserva ni escribe inventario. Confirmación relee stock autoritativo y devuelve todos los faltantes; bloquea inactivos y saldos no inicializados.

PlannedCount determina consumo; ActualCount determina salida. Se prueban actual 0, menor, igual y mayor al plan. Todos cero confirma consumo y deja OutputOperationId NULL, sin PRODUCTION_OUTPUT, movimientos cero ni WASTE automático. Multi-output omite sólo las salidas cero. Snapshots conservan versiones, cantidades, alternativas, políticas y factores; no se sustituyen al publicar versiones nuevas.

## Contrato y persistencia (34–54)

Contrato/identidad V1, JSON determinista UTF-8 y SHA-256 hexadecimal mayúsculo, UUIDv5 con namespaces separados para consumo/salida. Orden y campos detallados en ADR-023. ExpectedRevision es token de contenido del plan, no columna numérica inventada. Los nombres y timestamps quedan fuera del hash comercial.

SqliteProductionUnitOfWork usa una transacción inmediata para autorización, recuperación, revisión, stock, snapshots, hash, consumo, salida opcional, ledger, balances, auditoría y CONFIRMED. Reutiliza el único writer SqliteInventoryStore.WriteOperation. Permanece prohibido escribir producción mediante Apply genérico.

Idempotencia devuelve el resultado original sin duplicar lote, operaciones, movimientos o auditoría. Contrato distinto con la misma identidad rechaza. Recuperación de confirmación usa snapshots persistidos antes de catálogo vivo, incluso después de nuevas Formula/Profile activas o artículos desactivados. Valida hash, IDs, origen y movimientos. Error después del commit abre una transacción separada para recuperar; fallo previo revierte todo. Siete puntos de rollback y commit incierto cubiertos. Dos lotes compitiendo por stock no pueden consumir ambos; dos callers del mismo lote recuperan una confirmación.

Auditoría atómica: production.created, production.confirmed y acciones recipe.formula/profile de creación, guardar draft, publicar y activar.

## Permisos (55–58)

Administrator: recipe.view/configure y production.view/create/confirm. Manager: recipe.view y production.view/create/confirm; NO recipe.configure. Cashier: ninguno. Revalidación también en recuperación. Matriz 015 y HasPermission intactos.

## Recuperación y regresiones (59–68)

Backup/restauración mediante InstallationBackup de lotes confirmados con salida positiva y completamente cero. Se vuelve a recuperar la misma identidad en la base restaurada, se valida SQLite/FKs y se reconcilia ledger/saldos. Escenario integrado de saldo inicial, entrada, producción y consumo por venta. Suite completa cubre checkout, consumo por ventas, caja, impresión y autorización; no se realizó smoke físico ni se imprimió.

## Evidencia (69–75)

Resultados definitivos: scripts/verify.ps1 aprobado; 520/520 pruebas, 0 fallidas/omitidas; Release 0 errores y 0 advertencias de compilación. Focalizadas: 29/29, 0 fallidas/omitidas. Ver phase1j3-verification.txt y phase1j3-focused.txt. El aviso de sobrescritura de foundation.trx no es una advertencia de compilación. git diff --check y revisión del diff aprobados. Hashes SHA-256 de las 15 migraciones en phase1j3-migration-hashes.txt. Comparación Git contra baseline confirma 001–015 intactas, Presentation intacta y ausencia de 016. No recetas reales ni semillas productivas.

## Riesgos y continuidad (76–80)

El plan de un DRAFT funcional no se edita: cambiarlo requiere otro lote. Confirmación ligada al creador/terminal conforme al esquema existente; no hay transferencia entre usuarios. Reintentos deben conservar identidad y revisión, nunca crear otra operación por incertidumbre. Lecturas autorizadas usan la unidad de trabajo inmediata; es una aplicación local con escritores serializados, no un servicio multi-terminal distribuido. Persistencia de una caída continua del almacenamiento puede impedir recuperación inmediata, sin autorizar duplicación.

1J.4 pendiente de autorización para diseñar UI y mensajes de errores, alternativas y cantidades reales, conservación de identidad, confirmaciones y smoke sintético. No implementar automáticamente. Modelo recomendado: Astra para revisión de recuperación/atomicidad; Sol puede implementar UI una vez aprobado su contrato. El producto sigue siendo de desarrollo.

## Archivos

Nuevos: Application/ProductionService.cs, RecipeService.cs, ProductionPorts.cs, ProductionRecords.cs, ProductionContract.cs; Infrastructure/SqliteProductionUnitOfWork.cs; tests/ProductionServiceTests.cs y ProductionServicePlanningTests.cs; ADR-023, este reporte y evidencia phase1j3-*.

Modificados: Domain/Inventory.cs; Infrastructure/SqliteInventoryStore.cs, SqliteRecipeStore.cs y SqliteRecipeStore.Batches.cs; tests/RecipePlanningStorageTests.cs (namespace de modelos trasladados a Application); README, CURRENT_STATE, NEXT_STEPS, domain, architecture, database, operations, roadmap y FILES.
