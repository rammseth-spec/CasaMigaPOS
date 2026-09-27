# CASA MIGA POS — REPORTE FINAL DE CIERRE FASE 1J

1. Estado FASE 1J: CLOSED.

2. Estado 1J.5: completada.

3. Modelo: agente Codex; identificador exacto no verificado.

4. HEAD inicial: 13da5007eac455e7f88a993b85527b9e848a7d4f.

5. HEAD final: commit único que contiene este reporte; SHA exacto comunicado tras commit. No hash autorreferente dentro del commit.

6. Commit: feat: harden recipes and production and close phase 1J; identidad repo-local Rammseth / rammstudioworkshop@gmail.com. Sin push ni remoto nuevo.

7. Git: cierre condicionado a status limpio después del commit; resultado exacto comunicado al usuario.

8. Schema: 15.

9. Hashes 001–015: ver tabla completa al final; comparados con blobs del baseline.

10. No migration 016.

11. Diff revisado: cuatro archivos Presentation, dos archivos de tests y documentación/evidencia. Sin Domain/Application/Infrastructure/migrations modificados, SQL en Presentation, bypass de HasPermission, cambio de writer, contrato canónico o UUIDv5.

12. Correcciones finales: refresco Formula/Profile y selector InventoryItem. Revisión de cierre no cambió código funcional.

13. Refresh: dos regresiones reproducidas fallando antes y aprobadas después; listas reflejan publicación/activación.

14. Selector: nombre y código, consulta paginada vía InventoryService, filtros de tipo/estado, selección y búsqueda; usuario confirmó lista visible.

15. COUNT: pieza oficial, sin alias pza ni modificación de unidades internas.

16. Planned/Actual: A2/B3 con reales A1/B4 probado manualmente; consumo conserva plan y salida usa real.

17. Multi-output: dos productos, prueba manual y automatizada.

18. Zero-output: 0/0 con advertencia y consumo sin salida; cero parcial automatizado.

19. Stock después de preview: reducción legítima con Conteo físico; revalidación al confirmar.

20. Faltantes: 325 g masa y 65 ml líquido mostrados juntos. Rechazo conserva DRAFT; test verifica ausencia de escrituras parciales.

21. Restart DRAFT: plan A8/B3 recuperado manualmente; copia restaurada conserva DRAFT A2/B3.

22. Restart CONFIRMED: cuatro confirmados consultados en copia restaurada.

23. Creador/terminal: restricción conservada, aceptada; sin transferencia de operador.

24. Mismo usuario: nueva sesión confirma y recupera hash idéntico, automatizado.

25. Otro usuario/terminal: IdempotencyConflict, sin escrituras, automatizado.

26. Recuperación histórica: snapshots conservados, pruebas automatizadas y consulta humana.

27. FormulaVersion recovery: pruebas existentes aprobadas, contrato sin cambios.

28. ProfileVersion recovery: pruebas existentes aprobadas, contrato sin cambios.

29. Commit incierto: pruebas de recuperación existentes aprobadas.

30. Idempotencia: reintento no duplica efectos, pruebas aprobadas.

31. Conflicto de idempotencia: probado con rechazo.

32. Concurrencia por stock: pruebas existentes aprobadas.

33. Concurrencia mismo batch: pruebas existentes aprobadas.

34. Backup: casamiga-20260927T010800533Z-60e47a15fa8e4758a88cdd48ca09a08f.cmbackup.

35. Backup tamaño: 52941 bytes, conservado bajo work/phase1j5-smoke-20260926/backups (ignorado por Git).

36. Backup SHA256: 133150088D72ED854DA5CB5B50F5CF5AAC3161F3814BEE3C325F40BDCA9B5DBF.

37. Restore: work/phase1j5-restored-20260926; carpeta nueva, validación nativa y equivalencia de todas las tablas antes de abrir UI.

38. Recetas restauradas: filas idénticas; configuración activa comprobada en test mixto.

39. Perfiles restaurados: filas idénticas; configuración activa comprobada en test mixto.

40. Positivos restaurados: historial manual y tests aprobados.

41. Zero-output restaurado: lote 18:50 conservado; test confirma ausencia de OutputOperationId.

42. Auditoría restaurada: 50 filas idénticas antes del login; test verifica eventos de confirmación.

43. Permisos restaurados: tablas idénticas, matriz automatizada y Cashier manual.

44. InventoryBalance restaurado: seis filas idénticas al origen.

45. Reconciliación: pruebas con SqliteInventoryStore.Reconcile aprobadas; inventario consistente observado en conteos manuales.

46. integrity_check: ok en test mixto; Restore invoca Database.Validate nativo.

47. foreign_key_check: sin violaciones en test mixto y validación nativa.

48. Administrator: configuración/permisos automatizados, conteos físicos manuales.

49. Manager: producción, historial, continuidad y rechazo por faltantes observados.

50. Cashier: copia restaurada sólo Caja/Catálogo/POS; Application deniega producción en test.

51. Smoke: PHASE_1J5_SMOKE.md; aprobado por autorización final, sin repetición innecesaria ni certificación de hardware.

52. Focalizadas finales: 33/33 servicios/hardening/permisos y 6/6 UI, 39/39 en total; 0 fallidas/omitidas. Evidencia final separada; corrida anterior 36/36 conservada.

53. Suite final: scripts/verify.ps1 aprobado; 533/533, 0 fallidas/omitidas, duración 4 min 47 s. Evidencia: evidence/phase1j5-final-verification.txt.

54. Release final: compilación aprobada con 0 errores y 0 advertencias. El runner avisó que sobrescribió el TRX existente; no es advertencia de compilación.

55. git diff --check: aprobado antes del commit, sin errores.

56. Git limpio: verificar después de commit, sin modificar archivos posteriores.

57. POS: regresión automatizada de suite; no venta física nueva en este cierre.

58. Inventario: regresión automatizada y selector/conteos manuales.

59. Ventas: regresión automatizada aprobada.

60. Caja: regresión automatizada aprobada.

61. Impresión: regresión automatizada; hardware no repetido.

62. RecipeService: suite aprobada, reglas intactas.

63. ProductionService: suite aprobada, atomicidad/recovery intactos.

64. No datos reales hardcodeados ni semillas productivas; fixtures sintéticos aislados.

65. Pendientes reales: cocoa concha, rendimiento cobertura, relleno rol, Procali por dona, azúcar dona, toppings dona/polvorón, cuernito relleno, bolillo y puerquito.

66. Restricciones: DRAFT no editable, crear otro plan con control para evitar duplicación; confirmación ligada a creador/terminal; COUNT pieza; sin rediseño/fullscreen/instalador.

67. Riesgos: desarrollo, no production ready ni V1 completa; matriz Windows y hardware final pendientes; lista de inventario carga todas las opciones paginadas, sin prueba de catálogo masivo.

68. FASE 1J CLOSED al superar gates de validación y commit limpio; no bloqueo crítico encontrado en revisión.

69. Baseline oficial: commit único de cierre hijo de 13da5007eac455e7f88a993b85527b9e848a7d4f, SHA en entrega final.

70. Próximo paso: sólo proponer y autorizar alcance separado; no iniciar recetas reales, rediseño, fullscreen ni instalador.

71. Modelo recomendado: Astra para planificar/revisar siguiente alcance.

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
014_allow_zero_output_production_batches.sql 0CCA38796AB9070B4DE4AE5310226221E5D6C76E9BA2ABB871B63F6E3ACE0928
015_recipe_and_production_permissions.sql 447E655117D12CF58D70F38BCE89B462B3C8CD2A80895EEFD8BF97741461AA9F
```
