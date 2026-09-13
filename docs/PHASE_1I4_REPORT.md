# CASA MIGA POS — REPORTE DE CHECKPOINT FASE 1I.4

1. FASE 1I ABIERTA.
2. 1I.4 implementada y verificada técnicamente; NO APROBADA todavía, smoke humano pendiente.
3. Modelo: Codex basado en GPT-6; variante exacta no expuesta.
4. HEAD inicial c0bae99ba4302443260c0b0c2551eac01748971f. El commit que incorpora este reporte identifica el nuevo checkpoint (git log -1 --format=%H -- docs/PHASE_1I4_REPORT.md).
5. Commit previsto: feat: add safe inventory consumption activation and preflight. Implementación, pruebas y continuidad juntas.
6. Git: revisión y diff --check antes de commit; verificar árbol limpio después. Sin push ni remoto nuevo.
7. Nuevos: SaleConsumptionService.cs, SqliteConsumptionConfiguration.cs, ConsumptionViewModel.cs, CheckoutInventoryMessages.cs, ConsumptionConfigurationTests.cs, ADR-020, PHASE_1I4_REPORT.md, PHASE_1I4_SMOKE.md, phase1i4-verification.txt.
8. Modificados: LocalPorts.cs, SqliteInventoryStore.cs, CheckoutViewModel.cs, InventoryView.xaml/.cs, InventoryViewModel.cs, MainViewModel.cs, csproj de pruebas; README/CURRENT_STATE/NEXT_STEPS/FILES/architecture/domain/database/operations/roadmap. Domain ejecutable y CheckoutService intactos.
9. Servicios Application: GetStatus, Preflight, Change(Enable/Pause/Resume), CanConfigure. Contratos tipados para estado, problemas, resultado y errores.
10. Preflight único compartido: mismo CheckConsumption para consulta informativa y habilitación.
11. Enable/Resume: BEGIN IMMEDIATE → autorización → expected state/version → preflight → UPDATE condicional → auditoría → COMMIT. No usa verde previo ni retry invisible.
12. Reconciliación: algoritmo existente reutilizado sobre transacción del llamador; divergencias ledger/balance bloquean sin reparación. Incluye todas las posiciones existentes.
13. Revisa TODOS los productos activos, sin paginación ni filtro de categorías; prueba con 55 productos bloqueados.
14. TrackInventory=false sin link válido; true sin link o false con link bloquean. Unicidad y compatibilidad se verifican.
15. InventoryItem: existente, activo y dimensión compatible.
16. MAIN: válida/activa, balance existente y una apertura confirmada.
17. Saldo cero inicializado: válido, probado y preparado para smoke.
18. Productos inactivos no bloquean por configuración incompleta. Reactivación/cambio posterior puede ser bloqueado por Checkout, sin desactivar globalmente.
19. Items sin producto no requieren link/apertura; no se permite una divergencia ledger/balance existente. Materias primas sin asociación probadas.
20. Resultado: CanEnable, CurrentState, CurrentVersion, CheckedUtc, lista con códigos/contexto/identidades/nombres cuando corresponden y cantidades de reconciliación.
21. Feature flag existente; no se modificó esquema ni se habilitó instalación real.
22. DISABLED: nunca activada bajo operación normal; no se vuelve a este estado desde Application.
23. ENABLED: ventas futuras evalúan/consumen conforme a 1I.3.
24. PAUSED: suspensión explícita, ventas futuras se registran sin consumo.
25. Transiciones: DISABLED→ENABLED, ENABLED→PAUSED, PAUSED→ENABLED. Resume repite preflight; Pause no lo necesita.
26. Se rechazan Pause desde DISABLED, Enable desde PAUSED y acciones inválidas. No hay acción para volver a DISABLED.
27. Versión y estado esperado se verifican primero. Obsoletos generan ConcurrencyConflict; con expectativas vigentes, destino ya aplicado devuelve NoChange, sin auditoría/incremento.
28. Autorización revalidada dentro de transacción: sesión, usuario activo, terminal y permiso específico.
29. Administrator: controla mediante inventory.sale_consumption.configure.
30. Manager: inventory.view permite estado; no controles administrativos ni preflight.
31. Cashier: sin acceso a módulo ni acciones nuevas.
32. Auditoría: inventory.sale_consumption.enabled/paused/resumed con actor, terminal, UTC, estados/versiones antes/después. Fallo revierte config; NoChange y preflight fallido no generan ruido.
33. UI: bloque mínimo en InventoryView con estado, explicación, fecha, actualización y acciones válidas; bloqueadores en lista.
34. Activar/Reanudar: confirmación humana explícita sobre descuento automático, No por defecto.
35. Pausar: confirmación explícita advierte que ventas continúan sin descontar inventario.
36. Checkout: formatter de errores estructurados, nombre capturado en el carrito, sin SQL/stack ni acciones de override. DISABLED/PAUSED sin popup por venta.
37. InsufficientInventory: producto, requerido, disponible, venta no registrada y carrito intacto.
38. InventoryNotInitialized: indica falta de saldo inicial y corrección administrativa.
39. ConfigurationInvalid: configuración incompleta, sin acceso de corrección para Cashier.
40. ItemInactive: mensaje comprensible de artículo inactivo.
41. ConcurrencyConflict: revisar/reintentar, sin retry invisible. Overflow: mensaje controlado sin Int64.
42. Unidades: InventoryDisplay aprobado (g/kg, ml/L); pieza/piezas. Sin float/double.
43. Venta PAUSED: v1, evaluación DISABLED, stock intacto y efectivo normal, probado.
44. Venta tras Resume: vuelve a consumir exactamente una vez, probado.
45. Atomicidad: fallos antes/durante UPDATE, auditoría y antes de commit revierten config/version; historia sin cambios. Prueba de transiciones confirma sólo config/auditoría.
46. Focalizadas: checkpoint ampliado 92/92, 0 fallidas/omitidas. Incluye nuevos servicios, UI de inventario y regresiones checkout. La prueba adicional de inmutabilidad de historia pasó en suite completa.
47. Suite scripts/verify.ps1: 399 aprobadas, 0 fallidas, 0 omitidas; 35 casos adicionales frente a baseline 364. Duración 3 m 3 s.
48. Restore y Release correctos: 0 errores/advertencias. Publicación local artifacts/phase1i4-smoke correcta.
49. Smoke WPF: preparado, NO ejecutado/aceptado por humano en este checkpoint. Guía PHASE_1I4_SMOKE.md, 14 comprobaciones.
50. Intervención requerida: Administrator (verificar/activar/pausar/reanudar), Manager (sólo lectura), Cashier (sin acceso) y caso inválido con mensaje de bloqueo. Ninguna observación manual inventada.
51. Migraciones 001–012 intactas frente a c0bae99; hashes conservan evidencia phase1i3-migration-hashes.txt.
52. Migración 013 ausente.
53. No impresión física, ni datos reales, ni ventas/caja en preparación de smoke. Tests usan bases temporales y transportes simulados.
54. Riesgos: desarrollo, NO listo para producción; preflight no bloquea cambios posteriores de catálogo (Checkout revalida). UI pendiente de aceptación humana. NoChange requiere expectativas vigentes; tras conflicto se debe actualizar.
55. Pendiente 1I.5: smoke integral de venta, recuperación, backup y cierre, bajo nueva autorización. No sustituido por smoke administrativo.
56. 1I.5 NO iniciada. Tampoco stock bajo, override, recetas, producción, compras, proveedores, lotes, caducidad, devoluciones, transferencias, rediseño, fullscreen, LAN, instalador o cajón.
57. Próximo paso: completar exclusivamente smoke humano 1I.4 y registrar aceptación/reporte final; detenerse después.
58. Modelo recomendado para 1I.5: Astra por validación integral de transacción/recuperación.

Evidencia automática: TestResults/phase1i4-focused.trx, TestResults/foundation.trx y work/phase1i4-verify.log (locales); resumen versionado docs/evidence/phase1i4-verification.txt. Diseño y decisiones: ADR-020.
