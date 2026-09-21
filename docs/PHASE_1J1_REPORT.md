# CASA MIGA POS — FASE 1J.1

FASE 1J ABIERTA únicamente con Domain 1J.1. 1J.2 no iniciada. Baseline de entrada: 7cc103c864812d1b8586921bad45323608fdc4d7. El commit que incorpora este reporte fija el checkpoint recuperable. Modelo de trabajo: Astra.

## Resultado

Se implementan Formula/FormulaVersion/FormulaIngredient, alternativas, ProductProductionProfile versionado, requisitos de fórmulas y componentes directos, ProductionPlan multi-output y ProductionActualOutput independiente del consumo preparado. Ver ADR-021 para invariantes y límites.

ProductionRatio usa Int64 reducido, cancelación de factores y checked. MASS/VOLUME no representables retornan error estructurado; COUNT aplica ExactWholeCount, CeilingWholeCount o regla Unspecified pendiente por línea. Fallback sólo configurado y trazable; la selección explícita no se sustituye. Sin nombres de ingredientes en la lógica.

Las versiones son inmutables, con estados Draft/Published y puntero activo único en el agregado. Publicar otra conserva la anterior. Fórmulas auxiliares no requieren existencia intermedia; fórmulas no admiten otras fórmulas. El plan agrega por versión antes de escalar, conserva referencias históricas y ordena por identidad. No contiene stock ni movimientos persistentes.

Actual cero/exceso se representa y marca pendiente de política; no se exige motivo ni se aprueba la confirmación futura. Datos incompletos de cobertura, dona, polvorón, rellenos y toppings permanecen pendientes. No se generaron seeds.

## Archivos

Nuevos de producto: src/CasaMiga.Domain/ProductionScaling.cs, ProductionFormula.cs, ProductionProfile.cs y ProductionPlan.cs.

Nuevo de pruebas: tests/CasaMiga.Tests/ProductionDomainTests.cs. No se modifica producto preexistente. Documentación: este reporte, ADR-021, evidencia, README, CURRENT_STATE, NEXT_STEPS, domain, architecture, roadmap y FILES.

## Verificación

Primer checkpoint: 28 pruebas nuevas aprobadas. Checkpoint ampliado: 53 focalizadas aprobadas (33 nuevas + 20 InventoryDomainTests). Resultado oficial completo en evidence/phase1j1-verification.txt. No declarar pruebas manuales WPF: no se necesita ni implementa UI en esta subfase.

## Protecciones y límites

Sin lectura de SQLite desde el nuevo Domain; las pruebas nuevas son puras. La suite existente sí verifica su infraestructura con bases temporales. No se accedió a instalaciones comerciales ni se movió inventario. No se cambia schema 12, migraciones 001–012, Checkout, Sale, Payment, ExpectedCash, SALE_CONSUMPTION, tickets o impresión. No permisos ni UI. No 013.

El riesgo operativo principal sigue siendo la no representabilidad de ciertas escalas reales en mg/µL; esta fase la reporta sin resolverla por aproximación. Los racionales fuera del rango Int64 producen Overflow. Activación concurrente, validación de artículos y existencia, persistencia del snapshot multi-output, contratos de recuperación y reglas finales de rendimiento cero/exceso son decisiones posteriores. No costos ni compras.

Próximo paso: revisar 1J.1 y autorizar separadamente una propuesta concreta de 1J.2 basada en fórmulas compartidas, sin reutilizar el diseño monoproducto anterior. Modelo recomendado: Astra por constraints, versión activa, contratos históricos y migración segura. Detenerse; no iniciar 1J.2 automáticamente. Sistema de desarrollo, no listo para producción.

Verificación final completada: 53/53 focalizadas; 435/435 suite oficial, 0 fallidas/omitidas; Release 0 errores/advertencias. Migraciones 001–012 verificadas por hash, 013 ausente. 1J.1 COMPLETADA; FASE 1J ABIERTA; 1J.2 NO iniciada.
