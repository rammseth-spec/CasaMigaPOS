# CASA MIGA POS — REPORTE FINAL FASE 1I.2

Fecha: 2026-09-12. El commit que incorpora este reporte es el checkpoint de recuperación; el hash final se entrega después de registrarlo.

1. **FASE 1I:** ABIERTA.
2. **1I.2:** COMPLETADA; persistencia preparada, Checkout no integrado.
3. **Modelo:** GPT-6/Codex de la sesión; sin delegación a modelos más ligeros.
4. **HEAD:** inicial 42d7490cd20fd926f8e3d61830140ba7e8ce2a2d; final identificable con git log -1 -- docs/PHASE_1I2_REPORT.md.
5. **Commit:** feat: prepare sale inventory persistence and schema 11.
6. **Git:** diff revisado; checkpoint acotado, sin push ni remoto. Limpieza posterior comprobada en el reporte conversacional.
7. **Migración:** 011_sale_inventory_consumption.sql.
8. **Hashes:** SHA-256 de 001–011 en [evidencia](evidence/phase1i2-migration-hashes.txt), íntegramente reproducidos abajo.
9. **Reconstrucciones:** inventory_operation e inventory_movement, preservando filas, con FK ON y transacción única.
10. **Tablas nuevas:** sale_inventory_evaluation, sale_inventory_line_snapshot, inventory_sale_consumption_config.
11. **Columnas:** inventory_operation.sale_evaluation_id nullable; evaluación conserva venta/Checkout/policy/status/operación/hash/UTC; snapshots conservan cantidad/link/contexto; singleton conserva state/version/UTC/actor.
12. **Índices:** unicidad SaleId/CheckoutOperationId/InventoryOperationId por constraints; snapshots por evaluation+position (PK), SaleItem, Product y Link; operación por sale_evaluation_id no nulo. Sin índice redundante por estado.
13. **Triggers:** se recrean las protecciones previas; se añade origen, signo MAIN, sellado/completitud, inmutabilidad y transición de configuración.
14. **FKs:** reales, históricas RESTRICT; diferidas sólo para snapshots/origen que se sellan al final; validación FK posterior correcta.
15. **SALE_CONSUMPTION:** persistible sólo por camino interno especializado; Apply público lo rechaza.
16. **Invariantes:** delta negativo/no cero, no opening, MAIN, dimensión coherente, BalanceAfter>=0, operación confirmada inmutable.
17. **Evaluation:** única por Sale y CheckoutOperationId, policy_version=1, identity_version=1, hash y UTC.
18. **Estados:** DISABLED / NO_CONSUMPTION / CONSUMED; BLOCKED rechazado.
19. **Snapshot:** todas las columnas aprobadas, con posición estable FK a SaleItem y ausencia de link representada por NULL.
20. **Política de snapshots:** DISABLED cero; NO_CONSUMPTION todas las líneas no consumibles; CONSUMED todas, consumibles y no inventariables.
21. **Historia:** sin backfill; ventas previas y ventas normales actuales permanecen sin evaluación.
22. **Feature flag:** singleton persistente DISABLED/ENABLED/PAUSED; transiciones versionadas, sin casos de uso de activación.
23. **Estado inicial:** DISABLED, versión 1, actor NULL; nunca habilitado por migración.
24. **Permiso:** inventory.sale_consumption.configure sólo Administrator; Manager/Cashier sin él.
25. **PolicyVersion:** explícita 1 en evaluación y snapshots.
26. **CheckoutOperationId:** FK a sale.operation_id, único, raíz de correlación/recuperación.
27. **InventoryOperationId:** determinista desde CheckoutOperationId.
28. **Algoritmo:** UUIDv5 V1, namespace 82b7820c-b1d4-4cb2-a5a6-19419f756cb9, nombre Guid D minúsculo UTF-8, bytes de red; vectores fijos en ADR-018/pruebas.
29. **Idempotencia:** evaluation_sha256 V1 explícito; repetición idéntica devuelve registro, cambio de contenido falla. Sale no tenía hash persistente; no se modifica ni duplica un hash comercial inexistente. Se conserva request_sha256 de InventoryOperation.
30. **Relación:** evaluación CONSUMED referencia una operación SALE_CONSUMPTION confirmada con actor/terminal de la venta; FK diferida inversa impide huérfanos.
31. **Inmutabilidad:** evaluación, snapshots, operación y movimientos confirmados rechazan cambios/borrado; cambios de catálogo no alteran historia.
32. **Balance:** InventoryMovement autoritativo; InventoryBalance reconstruible; SqliteInventoryStore único escritor.
33. **Triggers y balance:** ninguno contiene UPDATE inventory_balance; probado.
34. **Transacción:** writer recibe conexión/transacción activa; no segunda conexión; Save usa savepoint sin commit propio.
35. **Infrastructure:** WriteOperation compartido, SqliteSaleInventoryStore interno (Save/Find/ReadConfig), RunTechnical interno y visibilidad para pruebas.
36. **Backup/restore:** .cmbackup validado con schema 11, sin cambiar formato; conserva configuración, evaluación, snapshots, ledger, balance e idempotencia.
37. **integrity_check:** ok en original/restaurada.
38. **foreign_key_check:** cero violaciones en original/restaurada.
39. **Reconciliación:** cero diferencias después de consumo, rollback y restore.
40. **Focalizadas:** 31/31 y checkpoint de regresión 52/52; casos adicionales de sellado/signo/reconstrucción incluidos en suite final.
41. **Suite completa:** 325 aprobadas, 0 fallidas, 0 omitidas; scripts/verify.ps1.
42. **Build:** Release, 0 errores y 0 advertencias.
43. **001–010:** byte por byte intactas, diff contra baseline vacío y hashes registrados.
44. **Checkout:** funcionalmente y por archivo intacto; ninguna venta normal consume inventario.
45. **ExpectedCash:** intacto; suite de ventas/cierre aprobada.
46. **Tickets/impresión:** intactos; ninguna prueba física ni uso de impresora.
47. **Riesgos:** versión de desarrollo, no producción; 1I.3 aún debe resolver autorización/revalidación/recovery funcional; 1I.4 preflight/activación/auditoría administrativa. APIs técnicas no deciden el flag.
48. **Pendiente 1I.3:** lectura del flag y resolución coherente en transacción de Checkout, recuperación por OperationId, mensajes de bloqueo y commit comercial único; preservar impresión post-commit.
49. **1I.3:** NO iniciada.
50. **Próximo paso:** revisar este checkpoint y autorizar 1I.3 expresamente.
51. **Modelo recomendado 1I.3:** Astra, por integración transaccional y recuperación Checkout–Inventory.

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
```
