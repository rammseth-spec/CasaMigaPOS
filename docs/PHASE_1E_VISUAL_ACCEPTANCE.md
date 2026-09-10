# CASA MIGA POS — aceptación visual FASE 1E

Fecha: 2026-09-10. Estado: **NO CERRADA**. La implementación sigue aceptada provisionalmente; falta completar el recorrido WPF desde la captura de efectivo. No se realizó ningún cambio funcional.

## Entorno y resultado exacto

Publicación de desarrollo autocontenida win-x64. Instalación nueva en work/phase1e-visual-20260910, preparada mediante servicios existentes con usuario sintético, una categoría, dos productos, caja OPEN y fondo cero. La preparación no creó ventas. La instalación anterior work/phase1e-ui se conservó y también se comprobó sin ventas ni marcador pendiente.

Se completó login real en WPF y se construyó el carrito mediante botones. Se probaron entrada de teclado, Tab y foco explícito. El login funciona; al escribir en el modal de cobro, la herramienta activa su ventana propietaria y el importe sigue vacío. El inventario de ventanas sólo expone la propietaria como destino. No se atribuye este comportamiento a un defecto demostrado de la aplicación.

Se solicitó intervención manual para introducir 50.00 y después 100.00 sin confirmar todavía. Al registrar esta evidencia no se había recibido resultado manual y el campo continuaba vacío. No se aceptó ni se confirmó un cobro.

## Los 24 pasos solicitados

| Paso | Resultado observado |
|---|---|
| 1. Arranque publicado | Validado: aparece login FASE 1E / DESARROLLO. |
| 2. Login | Validado: usuario sintético autenticado mediante WPF. |
| 3. Caja OPEN | Validado en UI y SQLite; fondo 0. |
| 4. Entrada al POS | Validada. |
| 5. Dos líneas | Validado: 2 Conchas de $19 y 3 Bolillos de $5. |
| 6. Total visible | Validado: $53.00. |
| 7. Cobrar habilitado correctamente | Validado en casos carrito vacío (deshabilitado) y carrito válido (habilitado). |
| 8. Abrir cobro | Validado: diálogo modal visible. |
| 9. Total en diálogo | Validado: $53.00. |
| 10. Efectivo insuficiente | Pendiente: enviar 50.00 no introdujo texto. |
| 11. Impedir venta insuficiente | Pendiente: sólo se observó deshabilitado con campo vacío; no equivale a probar 50.00. |
| 12. Efectivo suficiente | Pendiente de entrada manual. |
| 13. Cambio visible | Pendiente: debe ser $47.00 con recibido $100.00. |
| 14. Confirmar cobro | No ejecutado. |
| 15. Completar una sola vez | No validado en este smoke; SQLite contiene cero ventas. |
| 16. Folio/total/recibido/cambio | Pendiente; no hay recibo de una venta WPF. |
| 17. Aceptar resultado | No ejecutado. |
| 18. POS vacío después de cobrar | Pendiente. |
| 19. Cerrar/reabrir después de cobrar | Pendiente. Los arranques anteriores no sustituyen este paso. |
| 20. Persistencia de la venta | Pendiente de una venta WPF. |
| 21. Marcador aceptado no reaparece | Pendiente. Ahora está ausente porque no hubo cobro. |
| 22. ExpectedCash incorpora venta | Pendiente en WPF; caja aún sin venta. |
| 23. Conteo/cierre sintético | No ejecutado en este recorrido. |
| 24. Esperado/diferencia | Pendiente; columnas de cierre siguen NULL. |

## Evidencia y alcance

phase1e-visual-pending-20260910.jpg registra el diálogo vacío sobre carrito de $53.00. phase1e-visual-state.json contiene la consulta de sólo lectura: 0 ventas, 0 partidas, 0 pagos, 0 auditorías sale.completed, caja OPEN y checkout.pending ausente. No se declara éxito de un paso por estar cubierto por las pruebas de servicios anteriores.

No se demostró bug de aplicación, por lo que no se cambió ningún archivo de src, tests o migrations. No hay hacks, APIs nuevas, cambios de foco/modal/seguridad ni refactors. Migraciones 001–006 deben coincidir con el commit de implementación 18fd3a8. El entorno sintético y las utilidades de preparación viven en work y no forman parte del producto ni del commit.

## Verificación final de este intento

Ejecutados clean, restore locked, build Release y suite completa mediante scripts/verify.ps1. Resultado del 2026-09-10: **153 aprobadas, 0 fallidas, 0 omitidas; 0 errores y 0 advertencias de compilación**. Duración reportada por tests: 2 min 19 s. El runner avisó únicamente de sobrescribir el TRX existente.

Publicación autocontenida win-x64 terminada en artifacts/phase1e-acceptance, separada para no interrumpir la ventana de prueba abierta. Configuración sintética incluida en su carpeta ignorada; no ejecutar ambas publicaciones simultáneamente sobre los mismos datos. Restore posterior dejó lockfiles sin cambios. git diff --check sin errores. Los seis SHA-256 de migraciones coinciden con schema_migration y src/tests/migrations permanecen sin cambios frente al estado inicial del bloque.

Evidencia: phase1e-visual-clean.txt, phase1e-visual-verification.txt, phase1e-visual-publish.txt, phase1e-visual-restore.txt, phase1e-visual-migrations.json y phase1e-visual-publication.json. Esta verificación técnica no se presenta como verificación posterior a un smoke exitoso: el smoke continúa incompleto.

Archivos de este bloque: docs/CURRENT_STATE.md, docs/NEXT_STEPS.md, docs/PHASE_1E_REPORT.md, docs/PHASE_1E_MANUAL_SMOKE.md, docs/PHASE_1E_VISUAL_ACCEPTANCE.md, docs/FILES.md y evidencia docs/evidence/phase1e-visual-*. No se corrigieron bugs ni se modificaron código, pruebas, dependencias o migraciones.

## Git y continuidad

92c5ef795b6da8e1cdf7f34ec9fb1b01cce4a5d2: baseline definitivo 1D. 18fd3a8e56c79ef7a5710443da09d364f965f6fa: implementación 1E. 55eecfb4961f0b0ef83ffce04c53ca735323b3b7: reporte previo con smoke pendiente. Este bloque registra únicamente documentación/evidencia adicional, en un commit separado. Sin push, remoto ni elección de licencia.

Próximo paso: seguir PHASE_1E_MANUAL_SMOKE.md desde la captura preparada, comunicar lo observado y comprobar la venta/cierre mediante sólo lectura. Tras completar todos los pasos, actualizar aceptación y repetir la verificación final posterior al smoke. Hasta entonces 1E permanece NO CERRADA. No se inició 1F ni inventario, impresión, cajón, rediseño, fullscreen o cualquier otra funcionalidad posterior.
