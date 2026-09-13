# CASA MIGA POS — REPORTE FINAL FASE 1I.3

1. **FASE 1I: ABIERTA.** 1I.1–1I.3 completadas; 1I.4 no iniciada.
2. **1I.3: COMPLETADA.** Checkout atómico con inventario y recovery determinista implementados y verificados.
3. Modelo: Codex basado en GPT-6; la variante comercial exacta no está expuesta al agente.
4. HEAD inicial: bbaf48df8f517a89ebe6f3250822bb7ba88470b7. HEAD final: commit que incorpora este reporte (consultar git log -1 --format=%H -- docs/PHASE_1I3_REPORT.md). Evita autorreferencia imposible del hash dentro de su propio commit.
5. Commit del bloque: feat: integrate atomic checkout inventory with persistent recovery contract. Incluye implementación, pruebas y continuidad; baseline de producto previo 6aa9ad73b4453e94070d1609febd035cbf425feb.
6. Git: diff revisado y git diff --check correcto antes de commit; comprobar árbol limpio después de registrar. Sin push, remoto nuevo ni licencia.
7. Migración 012: 012_sale_inventory_evaluation_contract.sql, aditiva; no reconstruye Sale.
8. Motivo: schema 11 admitía ventas legítimas sin evaluación incluso después de 011; su ausencia no permitía distinguir historia de corrupción futura.
9. Sale recibe inventory_evaluation_contract_version e inventory_evaluation_id.
10. contract_version: INTEGER NOT NULL DEFAULT 0, valores 0/1, inmutable.
11. evaluation_id: TEXT nullable sólo para legacy; FK real a sale_inventory_evaluation.id, único cuando no es NULL.
12. Toda venta existente migra 0/NULL, sin backfill de evaluación ni consumo retroactivo. Evaluaciones técnicas existentes se preservan.
13. Un trigger rechaza cualquier nuevo INSERT legacy; el cliente antiguo falla de forma segura después de schema 12.
14. FK DEFERRABLE INITIALLY DEFERRED obliga a resolver el puntero antes del único commit.
15. CHECK: versión 0/1 y acoplamiento 0/NULL o 1/no-NULL.
16. Triggers nuevos: sale_inventory_contract_required, sale_inventory_contract_owner, sale_inventory_evaluation_contract. sale_no_update/no_delete ya existentes protegen las columnas añadidas.
17. Índice nuevo: ux_sale_inventory_evaluation, único parcial cuando el puntero no es NULL.
18. Rollback 012: fallo inyectado al registrar versión 12 conserva schema 11, datos y ausencia de columnas nuevas. También se prueba rechazo de commit sin evaluación y de dueño incorrecto.
19. SHA-256 001–012: lista completa al final y archivo evidence/phase1i3-migration-hashes.txt.
20. Frontera demostrada: venta schema 11 posterior a 011 migra legacy y recupera aun con ENABLED y configuración actual inválida. Venta v1 sin evaluación se rechaza como inconsistente.
21. Transacción: BEGIN IMMEDIATE; venta/partidas/pago/evaluación/snapshots/operación/movimientos/balance/auditorías y snapshot de ticket configurado dentro del mismo commit. El escritor de ledger/balance existente se reutiliza.
22. Flag: lectura dentro de transacción, inicial DISABLED; no hay servicio/UI de activación ni cambios sobre instalación real.
23. DISABLED: evaluación DISABLED, sin snapshots de inventario ni movimientos.
24. PAUSED: mismo resultado DISABLED, preservando historia; no borra ni modifica consumos previos.
25. NO_CONSUMPTION: ENABLED con todas las líneas sin tracking y sin vínculo; snapshot por cada línea, sin operación de stock.
26. CONSUMED: snapshots completos y una operación agregada por venta, con stock actualizado atómicamente.
27. Evaluation: una por nueva venta, ID igual a CheckoutOperationId según V1, puntero explícito e identidad recíproca verificada.
28. Snapshots: individuales por SaleItem, con tracking/link/item/MAIN/dimensión/factor/cantidad históricos; hash canónico V1.
29. SALE_CONSUMPTION: UUIDv5 V1 determinista; una salida agregada por artículo/ubicación/dimensión, con auditoría inventory.operation.confirmed.
30. Políticas: bloqueo por link/tracking incoherente, artículo inactivo, saldo no inicializado, insuficiencia y overflow. Int64 checked; sin stock negativo ni override. Errores Application estructurados sin SQL visible.
31. Cashier: puede consumir con sales.complete sin inventory.adjust/configure; retirar sales.complete impide checkout. Matriz administrativa preservada.
32. Idempotencia: se busca primero la venta por OperationId y se compara contenido comercial. No duplica venta, pago, folio ni consumo; request diferente se rechaza.
33. Recovery v0: retorna la venta persistida, sin reevaluar inventario ni exigir evaluación histórica.
34. Recovery v1: valida contrato/puntero/versiones/hash/snapshots y, si consumida, UUID, operación confirmada, movimientos y hash. No consulta catálogo, factores, stock o flag actuales.
35. InventoryRecoveryInconsistent: evaluación ausente, hash alterado, snapshot faltante u operación incoherente bloquean recovery sin reparación, recobro ni consumo. Journal pendiente conservado.
36. Commit incierto: excepción posterior al commit recupera exactamente la venta existente; fallo precommit revierte todo y permite retry. Carrito vacío sólo tras resultado confirmado.
37. Concurrencia: stock 5 y dos ventas de 4 dejan una venta y stock 1; la otra falla por insuficiencia. BEGIN IMMEDIATE serializa escritores. Busy/locked se traduce a InventoryConcurrencyConflict.
38. Rollback cruzado: fallos después de escribir venta, partidas, pago, evaluación, snapshots, operación, movimiento, balance y auditoría revierten la operación comercial completa; se prueba también fallo del snapshot del ticket. Carrito y stock previos preservados.
39. ExpectedCash: mantiene apertura + efectivo aplicado. Consumo, retry e impresión no agregan ingresos; verificado con total aplicado 1,900/3,800 centavos en escenarios sintéticos y regresiones de cierre.
40. Folios: secuencia en la misma transacción; fallo no deja consumo de folio, retry devuelve el mismo y la siguiente venta obtiene la secuencia siguiente.
41. Reimpresión: transporte simulado confirma que la venta ya es visible tras commit. Fallos de impresión y reimpresión no duplican consumo ni efectivo; snapshot del ticket permanece igual. No se usó impresora física.
42. Backup/restore: instalación temporal con venta consumida restaura a nuevo directorio; conserva contrato, venta, pago, stock, recovery y efectivo. Regresiones de upgrade/backup previas siguen aprobadas.
43. integrity_check: ok mediante Database.Validate en upgrade y restore sintéticos.
44. foreign_key_check: sin filas mediante Database.Validate.
45. Reconciliación: sin diferencias entre ledger y balance después de consumo/restore.
46. Pruebas focalizadas finales: 42 aprobadas / 0 fallidas / 0 omitidas; TRX local TestResults/phase1i3-focused.trx.
47. Suite completa scripts/verify.ps1: 364 aprobadas / 0 fallidas / 0 omitidas; TRX local TestResults/foundation.trx, duración 2 m 46 s. Se añadieron 39 casos frente a 325 del baseline.
48. Restore locked-mode y build Release: correctos, 0 errores y 0 advertencias. SDK/feed locales; log work/phase1i3-verify.log.
49. Migraciones 001–011: byte a byte intactas respecto del baseline; verificadas con Git y hashes.
50. 013: NO existe ni fue creada.
51. Riesgos: sistema de desarrollo, NO listo para producción. Preflight y activación segura pendientes; flag inicial DISABLED. Una inconsistencia histórica bloquea recovery y requiere diagnóstico, no reparación automática. No smoke físico/WPF nuevo en este bloque; pruebas de impresión con transporte simulado. No se tocó base productiva.
52. 1I.4 NO iniciada. Tampoco recetas, producción, proveedores, devoluciones, overrides, rediseño, fullscreen, LAN, instalador ni cajón.
53. Próximo paso recomendado: revisar/aceptar 1I.3 y autorizar por separado 1I.4 (preflight, activación/pausa/reanudación y auditoría); no implementarlo automáticamente.
54. Modelo recomendado para 1I.4: Astra por precondiciones y límites transaccionales.

## Archivos y punto de recuperación

Inventario de incorporaciones y modificaciones: FILES.md. Diseño: decisions/ADR-019-checkout-inventory-contract.md. Evidencia: evidence/phase1i3-verification.txt. Continuidad actualizada en README, CURRENT_STATE, NEXT_STEPS, architecture, database y roadmap. Domain y migraciones previas no modificados.

El bloqueo previo permanece consultable en bbaf48df8f517a89ebe6f3250822bb7ba88470b7:docs/PHASE_1I3_REPORT.md. La autorización de 012 lo resuelve sin heurísticas ni reconstrucción de Sale. Se detiene el trabajo al terminar el reporte; no hay continuación automática.

## Hashes SHA-256

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
```
