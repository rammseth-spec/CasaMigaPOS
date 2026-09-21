# ADR-022 — Persistencia versionada y contrato histórico de producción

Estado: implementado en 1J.2, exclusivamente almacenamiento. Fecha: 2026-09-21.
Baseline: 05cc19f94b192884114a3da007dcaa72c25f4308. Complementa ADR-021.

## Frontera

Migration 013 prepara fórmulas, perfiles y lotes; no existe ConfirmProduction, servicio Application, permiso nuevo, UI ni registro en composición. SqliteRecipeStore es un repositorio técnico de Infrastructure. Domain 1J.1 permanece intacto. No se consulta stock al guardar un lote, ni se reservan existencias. Ningún método de este repositorio escribe inventory_operation, inventory_movement, inventory_balance, Sale o Payment.

Publicar y activar son dos operaciones técnicas explícitas. Publicar sella una versión; activar/desactivar cambia un único puntero del propietario. Una futura transacción Application podrá agruparlas según el caso de uso autorizado y agregar permisos y auditoría. No se presenta esta API como una frontera de autorización de usuario.

## Configuración histórica

Formula tiene identidad/código estable, nombre y puntero activo nullable. FormulaVersion conserva número UNIQUE por propietario, nombre, rendimiento atómico y dimensión, notas, UTC y revisión. La publicación es creciente por propietario y no puede preceder creación. Se rehidratan modelos Domain mediante sus constructores y publicación, sin setters ni dependencias nuevas en Domain.

Ingredientes y opciones apuntan a InventoryItem mediante FK compuesta con dimensión. No existe FK Formula→Formula; no hay recursión. COUNT guarda EXACT_WHOLE_COUNT, CEILING_WHOLE_COUNT o UNSPECIFIED; MASS/VOLUME requieren NOT_APPLICABLE. UNSPECIFIED puede publicarse como configuración, pero nunca produce un plan resuelto. Cada grupo conserva UUID, secuencia y opciones con dimensiones independientes. preference_rank nullable representa fallback explícito; no se infiere orden por nombre.

ProductProductionProfile fija ProductId y FinishedInventoryItemId (FINISHED_GOOD/COUNT), sin un segundo catálogo. La versión referencia FormulaVersion concreta y cantidad por pieza; preparaciones auxiliares son requisitos adicionales genéricos. Ingredientes directos siguen el mismo contrato dimensional/COUNT.

Published no admite UPDATE/DELETE ni INSERT/UPDATE/DELETE de hijos. Los guards de UPDATE revisan padre anterior y nuevo para impedir reparentar datos históricos. Punteros activos exigen versión publicada del mismo propietario. No hay borrado público. Draft se reemplaza atómicamente con revisión esperada; un fallo de hijo revierte cabecera y todos los hijos. BEGIN IMMEDIATE serializa escritores y la revisión evita sobrescrituras. UNIQUE(owner,version) y publicación creciente cierran las carreras; las lecturas usan conexión read-only y transacción deferred.

## Lotes y snapshots

production_batch conserva OperationId raíz UNIQUE, actor, terminal, UTC, notas, estado DRAFT/CONFIRMED y versiones de contrato/identidad 1. Se reservan hash SHA-256 y UUID de ambas operaciones, sin inventar todavía algoritmo canónico ni derivación determinista. Duplicar OperationId falla; devolver el resultado idempotente según hash y resolver commit incierto pertenece a 1J.3.

production_output_plan fija producto, artículo terminado, versión de perfil y sus nombres; PlannedCount > 0, ActualCount nullable y no negativo. Cero o mayor al plan se guardan con actual_policy_pending=1, sin resolver su aceptación comercial. production_formula_snapshot conserva versión exacta, nombre, cantidad total de preparación y factor racional reducido; production_input_snapshot conserva artículo/nombre, cantidad atómica, origen fórmula o perfil directo, alternativa elegida, política y racional exacto antes de aplicar la política COUNT. Se evita duplicar recetas internas dentro del perfil.

CreateProductionDraft recarga las versiones publicadas por ID dentro de su transacción y usa sólo el planificador puro de 1J.1. No confía en definiciones suministradas por el llamador. Los snapshots usan nombres de catálogo en ese instante y no cambian al renombrar/activar versiones posteriores.

NonRepresentableScale, CountPolicyRequired y AlternativeNotEligible mantienen un DRAFT con outputs, requerimientos racionales y planning_issues_json, sin inputs redondeados. Overflow/identidades inválidas impiden la escritura. Un lote con problemas pendientes no puede sellarse. No se decide una política de redondeo para mg/µL.

## Ledger y reconstrucción 013

El CHECK de tipos previo exige reconstruir sólo inventory_operation para admitir PRODUCTION_CONSUMPTION y PRODUCTION_OUTPUT. Se preservan todas sus columnas previas, UUID, hashes, relaciones de consumo por venta, filas, índices y triggers anteriores. No se reconstruyen inventory_movement, inventory_balance, Sale ni tablas de evaluación. La copia temporal vive dentro de la transacción de Database.Initialize.

PRAGMA defer_foreign_keys=ON difiere las referencias durante esa reconstrucción; foreign_keys permanece ON. La tabla vuelve al mismo nombre, con referencias salientes reales, antes de commit. Los tests prueban historia 12 con venta/consumo, fallo a mitad de reconstrucción, fallo al registrar 013, rollback íntegro y reintento. No se modifica 001–012, ni se crea 014. La excepción -text de .gitattributes conserva los bytes de SQL aplicados.

production_batch posee FKs diferidas a ambas operaciones; inventory_operation posee FK compuesta diferida (production_batch_id,production_batch_status)→(id,status), con estado de origen requerido CONFIRMED. La inserción exige lote DRAFT recíproco, mismo actor/terminal e ID reservado. Esto permite construir todo dentro de una futura transacción, pero hace imposible comprometer operaciones de producción contra un lote que siga DRAFT. UNIQUE(batch,kind) y UNIQUE en ambos punteros impiden compartir o duplicar operaciones.

Sellar el lote exige outputs finales, inputs y cobertura de requisitos/grupos, ambas operaciones confirmadas, movimientos coherentes con snapshots y auditoría. Los signos, MAIN, salida FINISHED_GOOD/COUNT, cantidades no negativas y la concordancia de proyección se defienden también al confirmar operaciones. El cálculo principal no está en triggers: no se implementa un planificador SQL. El futuro servicio debe revalidar configuración, permisos, plan, stock, política y hash canónico.

InventoryMovement sigue autoritativo. El writer existente sigue siendo la única autoridad de actualización de InventoryBalance en código de producto; no se amplía su API de escritura. Sólo se amplía el parser de lectura para reconocer los dos nombres futuros. Los triggers NUNCA actualizan saldos. La fixture SQL de tests construye un contrato confirmado sintético para probar constraints, inmutabilidad y backup; no es código productivo ni operación sobre una instalación real.

## Límites y continuidad

No hay autorización, auditoría de configuración, editor/actualizador de lotes, confirmación, stock preflight, operación determinista, recuperación por hash ni reversos de producción expuestos. Deben diseñarse en 1J.3. Un resultado totalmente cero se puede almacenar en DRAFT; su eventual confirmación debe resolver la tensión entre ambas operaciones obligatorias y salida positiva, antes del primer movimiento. No se presume una decisión comercial.

Backup/restore usa el mecanismo existente de instalación, con destino nuevo, integridad y FKs; cubre schema 13 con configuración, borradores e historia sintética confirmada, y backup 12 restaurado y migrado. La suite anterior protege Checkout/1I. No se ejecuta una nueva venta o producción manual, smoke WPF, impresión ni cajón.
