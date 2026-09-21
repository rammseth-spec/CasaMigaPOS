# CASA MIGA POS — REPORTE FINAL CORRECCIÓN 1J.3B

FASE 1J ABIERTA. 1J.3 funcional PENDIENTE. Corrección limitada al aprovisionamiento versionado de permisos; no se reanuda automáticamente 1J.3 ni se inicia 1J.4. Versión de desarrollo, NO lista para producción.

Baseline exacto: a76951f86eaf0a9810fd701273358bea25f7dec2, schema 14, Git limpio. El commit que contiene este reporte constituye el checkpoint 1J.3B y lleva el asunto `fix: provision recipe and production permissions`; su hash final se entrega en conversación. Identidad repo-local conservada: Rammseth / rammstudioworkshop@gmail.com. Sin push ni remoto configurado.

Modelo utilizado: Codex basado en GPT-6; identificador comercial del runtime no confirmado. Astra es la recomendación explícita del usuario para esta microfase y la reanudación futura de 1J.3.

## Cambio

015_recipe_and_production_permissions.sql, schema 15. SHA-256: 447E655117D12CF58D70F38BCE89B462B3C8CD2A80895EEFD8BF97741461AA9F. Migraciones 001–014 intactas, verificadas contra phase1j3a-migration-hashes.txt. No existe 016.

La migración agrega exactamente cinco filas a permission y nueve a role_permission, siguiendo las migraciones 002, 005, 006, 008, 009 y 011. Las identidades persistentes son nombres TEXT: permission.name y app_role.name; no hay GUIDs, IDs numéricos, descriptions ni timestamps que inventar. No crea roles ni modifica tablas, constraints, triggers o datos comerciales.

| Permiso | Administrator | Manager | Cashier |
|---|---|---|---|
| recipe.view | Sí | Sí | No |
| recipe.configure | Sí | No | No |
| production.view | Sí | Sí | No |
| production.create | Sí | Sí | No |
| production.confirm | Sí | Sí | No |

Administrator recibe cinco asociaciones y Manager cuatro. Cashier ninguna. Manager no recibe recipe.configure. Las asignaciones previas ajenas a estos códigos se preservan.

HasPermission permanece intacto: consulta asignaciones persistidas y usuario activo. No hay bypass Administrator, autorización implícita ni aprovisionamiento paralelo al startup. Unknown permission devuelve false; usuario Manager inactivo tampoco accede. Las pruebas usan SqliteLocalUnitOfWork.Read y HasPermission reales, además de verificar las cinco filas y nueve asociaciones.

## Atomicidad y validación

Database.Initialize ejecuta SQL y registro de migración en la transacción existente. No se utiliza INSERT OR IGNORE para ocultar inconsistencias. Una segunda inicialización reconoce la migración registrada y no duplica datos.

Fresh 001→015 y upgrade 14→15 cubiertos. La comparación de todas las filas históricas excluye exclusivamente los cinco permisos nuevos y sus asignaciones en el upgrade exitoso; conserva usuarios, roles, caja, ventas, pagos, inventario, recetas/perfiles, batches DRAFT/CONFIRMED, snapshots, operaciones, auditoría y receipt_snapshot. El test previo de upgrade de producción conserva asimismo el consumo por venta.

Fallos inyectados durante permisos, asignaciones y registro de versión revierten todas las filas: schema permanece 14, no hay aprovisionamiento parcial y el reintento funciona. Rollback compara todas las filas, incluyendo schema_migration y permisos. Database.Validate exige integrity_check=ok y foreign_key_check sin filas. Backup/restauración a carpeta nueva preserva matriz, schema y resultados de HasPermission; nueva inicialización no duplica registros. Sólo se usaron bases temporales de pruebas.

Verificación final: 8/8 focalizadas, 491/491 suite completa mediante scripts/verify.ps1, 0 fallidas y 0 omitidas. Release: 0 errores/advertencias de compilación. El runner sólo avisó de sobrescritura del TRX anterior. La primera suite detectó una expectativa antigua de 22 permisos; se corrigió a 27 y se repitió íntegramente la verificación, con resultado satisfactorio. git diff --check y staged diff correctos. Evidencia: phase1j3b-focused.txt, phase1j3b-verification.txt y phase1j3b-migration-hashes.txt.

## Archivos

- migrations/015_recipe_and_production_permissions.sql.
- tests/CasaMiga.Tests/ProductionPermissionMigrationTests.cs: cinco casos nuevos parametrizados/agrupados.
- Tests de schema existente: conteo total de permisos 22→27 y expectativas 14→15 en CashIdentityTests, FoundationTests, InventoryNoteMigrationTests, InventoryPersistenceTests, Phase1I5HardeningTests (metadato), ReceiptTests, RecipePersistenceTests, RecipeUpgradeTests, SaleInventoryMigrationTests, SalesHistoryTests y ZeroOutputMigrationTests. Este último comparte la comparación histórica ajustada a los permisos nuevos; no altera la lógica del producto.
- README, CURRENT_STATE, NEXT_STEPS, database, roadmap, FILES, este reporte y evidence/phase1j3b-*.

src permanece intacto. Ningún PrepareProduction, ConfirmProduction, servicio de recetas/producción, stock validation, hash, IDs, movimientos, recovery, concurrencia, auditoría funcional nueva o UI implementado. El contrato zero-output de 014 se conserva. No instalación productiva modificada.

Pendiente: revisar esta corrección y autorizar explícitamente la reanudación funcional de 1J.3. Aprovisionar permisos no implementa ni valida los futuros servicios que los consumirán. No se declara FASE 1J cerrada. Detenerse en este checkpoint.
