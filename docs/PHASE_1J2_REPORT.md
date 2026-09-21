# CASA MIGA POS — REPORTE FINAL FASE 1J.2

Fecha: 2026-09-21. Alcance: migración 013 y persistencia técnica, sin confirmación productiva.

1. **FASE 1J: ABIERTA.**
2. **1J.2: COMPLETADA.**
3. Modelo: Codex; identificador exacto de ejecución no expuesto en esta sesión. La autorización recomienda Astra.
4. HEAD inicial: `05cc19f94b192884114a3da007dcaa72c25f4308`. HEAD de entrega: commit que incorpora este reporte, identificado por el asunto del punto 5 y el reporte de consola final.
5. Commit de entrega: `feat: persist versioned recipes and production history schema`. Incluye producto, pruebas y documentación de 1J.2; sin push.
6. Git: diff revisado y diff --check correcto; entrega en un commit local sobre master. El reporte de consola final registra el hash resultante y comprueba árbol limpio. Sin push ni remoto nuevo.
7. Migración: `013_recipes_and_production.sql`; schema 13; no 014.
8. SHA-256 de 001–013: anexo íntegro y `evidence/phase1j2-migration-hashes.txt`; 001–012 coinciden con la evidencia de 1I.5.
9. Tablas nuevas (13): formula, formula_version, formula_ingredient, formula_alternative_group, formula_alternative_option, production_profile, production_profile_version, profile_formula_requirement, profile_direct_ingredient, production_batch, production_output_plan, production_formula_snapshot, production_input_snapshot.
10. Tabla reconstruida: únicamente inventory_operation, para ampliar CHECK(kind) y agregar origen productivo. Copia temporal transaccional, FKs ON/diferidas y preservación de filas/UUID/hashes/constraints anteriores. No reconstrucción de movimiento, saldo o venta.
11. Formula: UUID/código estable, nombre, UTC, ActiveVersionId nullable y revisión.
12. FormulaVersion: versión UNIQUE por propietario, nombre, rendimiento/dimensión, notas, UTC, DRAFT/PUBLISHED y revisión.
13. ActiveVersion: único puntero, propio y publicado; activar/desactivar exige revisión esperada.
14. FormulaIngredient: FK real a artículo/dimensión, secuencia, cantidad atómica positiva y política.
15. COUNT: EXACT_WHOLE_COUNT, CEILING_WHOLE_COUNT y UNSPECIFIED persistidos. MASS/VOLUME usan NOT_APPLICABLE. No float/double.
16. AlternativeGroup: UUID, secuencia y opciones; cada grupo publicado tiene al menos dos opciones, sin recursión de fórmulas.
17. AlternativeOption: UUID, artículo/dimensión/cantidad/política, secuencia y preference_rank nullable. Admite dimensiones diferentes dentro del mismo grupo; fallback explícito.
18. Inmutabilidad: versiones publicadas, hijos y lotes confirmados protegidos contra modificación/borrado/agregado/reparentado; versiones históricas siguen publicadas aunque cambie el puntero activo.
19. ProductProductionProfile: identidad estable por Product, artículo terminado FINISHED_GOOD/COUNT y puntero activo. No segundo catálogo.
20. ProfileVersion: DRAFT/PUBLISHED, número único, publicación creciente, UTC y revisión; publicación/activación técnicas separadas.
21. FormulaRequirement: referencia a versión exacta y cantidad por pieza; varias preparaciones genéricas sin copiar ingredientes internos.
22. DirectIngredientRequirement: artículo, dimensión, cantidad por pieza y COUNT policy.
23. ProductionBatch: UUID, OperationId raíz UNIQUE, actor, terminal, UTC, notas, contrato/identidad v1, hash y referencias de operaciones futuras.
24. Estados: DRAFT y CONFIRMED. Sólo se expone creación/consulta de DRAFT; no comando de confirmar.
25. Planned outputs: productos/artículos/perfiles exactos, nombres históricos, secuencia y PlannedCount Int64 > 0.
26. Actual outputs: nullable en DRAFT; 0 y mayores al plan se conservan con marca de política pendiente. No regla comercial inventada.
27. Input snapshots: artículo/nombre, cantidad/dimensión, origen, versión exacta, alternativa elegida, política y racional exacto.
28. Formula/rational snapshots: cantidad requerida de preparación y factor reducido Int64 numerator/denominator > 0; sin aproximación decimal.
29. Production OperationIds: raíz y dos campos preparados; derivación determinista y algoritmo canónico pendientes de 1J.3, no se introdujo uno arbitrario.
30. PRODUCTION_CONSUMPTION: tipo persistente preparado, origen recíproco, MAIN, deltas negativos y proyección no negativa.
31. PRODUCTION_OUTPUT: tipo preparado, origen recíproco, MAIN, deltas positivos y artículo terminado COUNT.
32. Batch ↔ operations: FKs reales diferidas, origen compuesto que requiere lote CONFIRMED al commit, IDs recíprocos, mismo actor/terminal y unicidad. No puede comprometerse una operación productiva contra un DRAFT. Confirmar requiere ambas operaciones, snapshots completos/concordantes y auditoría.
33. InventoryBalance: sigue siendo proyección de InventoryMovement; el writer existente conserva autoridad en código de producto.
34. Triggers: ninguno actualiza InventoryBalance; prueba explícita de todos los triggers. La fixture SQL sintética de contratos vive sólo en tests.
35. Idempotencia: UNIQUE(OperationId), hash SHA-256 y versión de contrato preparados; la recuperación por mismo contenido/conflicto/commit incierto NO está implementada.
36. Persistencia: SqliteRecipeStore en Infrastructure (configuración y parcial Batches), sin Application/UI/DI ni permisos nuevos. Un cambio mínimo de lectura reconoce los nombres de movimientos futuros; no se amplió el writer.
37. Concurrencia: BEGIN IMMEDIATE, revisiones CAS, unicidad y orden de publicación; pruebas concurrentes de publicación/activación y reemplazo de draft con un solo ganador.
38. Datos productivos hardcodeados: NO. Sólo fixtures sintéticas de pruebas; no seeds reales ni edición de instalación existente.
39. Backup: mecanismo existente probado con schema 13, configuración, borradores e historial confirmado sintético; también backup schema 12.
40. Restore: destino nuevo; igualdad de snapshots, referencias y reconciliación; backup 12 restaurado y migrado a 13.
41. integrity_check: comprobado en las pruebas de migración/restauración; resultado verificado `ok`.
42. foreign_key_check: comprobado en esas pruebas; resultado verificado 0 filas.
43. Upgrade 12→13: probado con venta, pago, consumo por venta y saldo histórico preservados; instalación nueva sin seeds de recetas.
44. Rollback: fallo inyectado a mitad de reconstrucción y al registrar 013; esquema queda 12, filas previas idénticas, sin tablas parciales, validación y reintento satisfactorios.
45. Focalizadas: 31/31 aprobadas, 0 fallidas/omitidas; pruebas Recipe* abarcan los 35 puntos de aceptación y fronteras adicionales.
46. Suite final: 466/466 aprobadas, 0 fallidas y 0 omitidas. Incluye las 435 pruebas anteriores más 31 casos nuevos.
47. Build: Release del checkpoint final 0 errores, 0 advertencias; scripts/verify.ps1 ejecutado con feed local en modo locked.
48. Migraciones 001–012: intactas por diff y hashes; no cambios retroactivos.
49. Checkout/1I: código de Domain/Application/Presentation intacto; Sale/SaleItem/Payment/ExpectedCash/CashSession no modificados. Tests anteriores sólo ajustan expectativas de schema y un metadato de evidencia 12→13.
50. Producción real: NO se realizó; tampoco se abrió WPF, se generó venta manual, se imprimió o se accionó cajón. El contrato confirmado se valida mediante SQL sintético exclusivo de tests.
51. Riesgos: repositorio técnico sin frontera de permisos/auditoría de usuario; confirmación/stock/recuperación pendientes. No representa una versión lista para producción. La reconstrucción exige backup y migración transaccional normal; nunca edición manual de una base comercial.
52. Escalas no representables: guardan DRAFT con planning_issues_json, outputs y factores exactos, sin inputs redondeados ni movimientos. COUNT sin política o alternativa inelegible también permanece pendiente; overflow impide escribir.
53. Pendientes 1J.3: políticas de mg/µL, actual=0/>planned y lote totalmente cero frente a salida positiva obligatoria; permisos/auditoría; plan/stock/asociaciones autoritativos; hash/IDs deterministas, atomicidad con writer existente y recuperación. Resolver antes del primer movimiento.
54. **1J.3 NO iniciada.** No ConfirmProduction, UI, recetas reales, compras/proveedores, rediseño, fullscreen, nuevos efectos físicos ni otra fase.
55. Próximo paso: revisar/aceptar este checkpoint y autorizar separadamente diseño y ejecución de 1J.3. No continuar automáticamente.
56. Modelo recomendado para 1J.3: **Astra**, por atomicidad, exactitud y recuperación.

## Archivos de este bloque

- Migración 013; SqliteRecipeStore.cs y SqliteRecipeStore.Batches.cs; parser de lectura SqliteInventoryStore.cs.
- RecipePersistenceTests.cs, RecipePlanningStorageTests.cs, RecipeContractTests.cs, RecipeUpgradeTests.cs.
- Expectativas de schema: CashIdentityTests, FoundationTests, InventoryNoteMigrationTests, InventoryPersistenceTests, ReceiptTests, SaleInventoryMigrationTests, SalesHistoryTests; metadato schema de Phase1I5HardeningTests.
- README; docs/CURRENT_STATE, NEXT_STEPS, database, architecture, domain, operations, roadmap y FILES.
- ADR-022, este reporte y evidence/phase1j2-*.

## Evidencia y reproducción

`./scripts/verify.ps1 -Dotnet ./work/dotnet/dotnet.exe -PackageSource ./work/nuget-feed`

Focal: `./work/dotnet/dotnet.exe test tests/CasaMiga.Tests -c Release --no-build --no-restore --filter FullyQualifiedName~Recipe`

Las bases/paquetes de backup de tests son temporales y se eliminan al finalizar. No se entrega una base comercial ni credenciales nuevas. Ver logs de verificación y resumen TRX en evidence/phase1j2-*; decisiones técnicas en ADR-022.

## SHA-256 de migraciones

```text
001_foundation.sql 67FE636BC15056DB439D15A0CDD73FFD84DA98F5F383FAEF21910F53E191CAD7
002_identity_cash.sql B8C29E34BFFFDC6EE313756EF4869644A495070C4B096BCDEDAD6C8A8BF7C9E5
003_cash_closing.sql 4A5FD419D95C19CBE7669FCB0C0C825567FA4DDD97478BD9946DDB1E9E13E7E1
004_catalog.sql 7E7F81303AB6FD9DB3E96A3F878AD4FE4B4B94B79FF4C893AF9F9527DAEA1968
005_pos_permission.sql A335A25AA1F2CC94CED3670FDD4C5E2FCC9AA344141EEA74B06287F357CA167B
006_cash_sales.sql 7EA9A2981B02C7D2A93CC550E5E18D0FA60C63A79C9D1052458E0769DFAE2E0C
007_sales_history.sql 439632CB3B0612DEA8651FA52FE3202F2ADF2D6876AEB68F0E1948985632AB5E
008_receipt_printing.sql B2B395673ACC14493841F001E6E76B248D6AC136632F7F94C190ECB0BB33A6C5
009_inventory_foundation.sql 8679B3154D4EEC966D3E63E8E48610C924597EF5121CA648E34047BC34A37589
010_inventory_receipt_note_fix.sql 6FA06DCD826893576B61AD4F4910CF1753D9640D544FDECF641040802EAC598E
011_sale_inventory_consumption.sql 3B47838DC73CF1EE625CE0A1A0CEDC1373F1CF6E8B28AAEE54E1527419AF9EB1
012_sale_inventory_evaluation_contract.sql 06952781D2960BB511D8180B860257E2150762CAD717B9A8CB2909EEE8A539F6
013_recipes_and_production.sql CAC38A250CAF503A88E79FA07B77A2E2E22A20C3664F7254996323677AB555F1
```
