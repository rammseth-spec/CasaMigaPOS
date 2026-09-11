# CASA MIGA POS — aceptación visual FASE 1E

Fecha local: 2026-09-10. Estado: **NO CERRADA**, pendiente únicamente de confirmar los pasos visuales manuales indicados abajo. Cobro persistido, reinicio, conteo y cierre WPF comprobados. No hubo cambios funcionales.

## Resultado y participación manual

Se usaron exclusivamente datos sintéticos en work/phase1e-visual-20260910. La herramienta no consiguió escribir en el modal: activar la ventana propietaria quitaba foco. No se demostró bug de aplicación ni se introdujo ningún ajuste para la herramienta. El usuario completó el cobro durante la pausa; se constató la venta al retomar. Codex reinició la publicación, comprobó POS vacío, contó $53.00 y confirmó cierre mediante WPF. No se repitió el cobro.

Se solicitó al usuario confirmar si observó rechazo de $50.00, cambio visible de $47.00 con recibido $100.00, resultado con folio/importes y regreso inmediato al carrito vacío. Esa confirmación aún no está registrada; los datos persistidos no sustituyen la observación visual.

## Los 24 pasos solicitados

| Paso | Resultado observado |
|---|---|
| 1. Arranque publicado | Validado en publicación autocontenida win-x64. |
| 2. Login | Validado con usuario sintético. |
| 3. Caja OPEN | Validado; fondo cero. |
| 4. Entrada al POS | Validada. |
| 5. Dos líneas | 2 Conchas de $19.00 y 3 Bolillos de $5.00. |
| 6. Total visible | $53.00 correcto. |
| 7. Cobrar habilitado | Deshabilitado vacío; habilitado con carrito válido. |
| 8. Abrir diálogo | Validado. |
| 9. Total del diálogo | $53.00. |
| 10. Introducir insuficiente | Pendiente de confirmación manual de $50.00. |
| 11. Rechazar insuficiente | Pendiente de confirmación visual; campo vacío no equivale a esta prueba. |
| 12. Introducir suficiente | Cobro manual con recibido $100.00, comprobado en Payment. |
| 13. Cambio visible | Persistido $47.00; observación visual pendiente de confirmación manual. |
| 14. Confirmar cobro | Ejecutado manualmente; venta confirmada en SQLite. |
| 15. Una sola venta | Exactamente 1 Sale, 2 SaleItem, 1 Payment y 1 sale.completed. |
| 16. Resultado con folio/importes | Valores persistidos correctos; presentación visual pendiente de confirmación manual. |
| 17. Aceptar resultado | Marcador ausente después de intervención manual; falta confirmación explícita del paso visual. |
| 18. Regreso al POS vacío | Validado tras reinicio; regreso inmediato pendiente de confirmación manual. |
| 19. Cerrar/reabrir | Usuario cerró aplicación; Codex reabrió publicación y realizó login. |
| 20. Venta persiste | Mismo SaleId y UUID, sin duplicados tras reinicio. |
| 21. Pending no reaparece | checkout.pending ausente; POS sin aviso de recuperación. |
| 22. ExpectedCash | UI muestra $53.00. |
| 23. Conteo/cierre | Conteo $53.00 y Confirmar cierre mediante WPF; CAJA CERRADA. |
| 24. Esperado/diferencia | Esperado $53.00, contado $53.00, diferencia $0.00 en UI y SQLite. |

## Persistencia y cierre

Folio: 5635c1f408f34bd7974d03423a5a8d40-00000001. SaleId: 25548b79-f8db-4db1-a80f-a7d117203a68. OperationId: e1521c18-008e-4c0d-b377-5b57e887c6e6. Confirmada UTC 2026-09-10T19:54:19.8514937+00:00. Total/aplicado 5300 centavos, recibido 10000 y cambio 4700. Partidas: 3800 + 1500 = 5300. Usuario, terminal y sesión quedan asociados.

Caja 4dcd2c23-f48f-4ceb-a8f4-ffd5e57f84c4 CLOSED, cierre UTC 2026-09-11T00:44:23.9412796+00:00 (10 de septiembre local). Fondo 0 + efectivo aplicado 5300 = esperado 5300; contado 5300; diferencia 0. Auditorías cash.count y cash.close presentes. Aplicación cerrada normalmente al terminar. No abrir otra caja ni repetir esta venta.

## Verificación final posterior al cierre

Clean correcto. scripts/verify.ps1 ejecutó restore locked, build Release y suite completa: **153 aprobadas, 0 fallidas, 0 omitidas; 0 errores y 0 warnings de compilación**. Duración de pruebas: 1 min 1 s. Único aviso del runner: sobrescritura del TRX anterior.

Publicación autocontenida win-x64 completada en artifacts/phase1e-acceptance. Restore posterior devuelve lockfiles al estado del repositorio. Migraciones 001–006 verificadas por SHA-256 contra schema_migration; todas coinciden. Sin diferencias de src/tests/migrations contra 18fd3a8. git diff --check ejecutado antes del commit. No se cambiaron dependencias.

## Evidencia, archivos y Git

Nuevos: docs/evidence/phase1e-visual-restarted-empty.jpg, phase1e-visual-count-exact.jpg, phase1e-visual-closed.jpg y phase1e-final-* (estado SQLite, migraciones, clean, verificación, publicación, restore y hashes de publicación). Los archivos phase1e-visual-state.json y phase1e-visual-pending-20260910.jpg se conservan como evidencia histórica ANTERIOR al cobro.

Documentación actualizada: README.md, docs/CURRENT_STATE.md, docs/NEXT_STEPS.md, docs/PHASE_1E_REPORT.md, docs/PHASE_1E_MANUAL_SMOKE.md, docs/PHASE_1E_VISUAL_ACCEPTANCE.md y docs/FILES.md. Ningún archivo de código, pruebas o migraciones modificado; no hubo correcciones de bugs.

- 92c5ef795b6da8e1cdf7f34ec9fb1b01cce4a5d2: baseline definitivo 1D, 120 pruebas verificadas.
- 18fd3a8e56c79ef7a5710443da09d364f965f6fa: implementación 1E.
- 55eecfb4961f0b0ef83ffce04c53ca735323b3b7: reporte técnico inicial.
- a6431e2008c6d8b4e6d2a168dbce1305b8ac87b7: intento visual anterior e instrucciones manuales.
- El commit que contiene esta revisión registra sólo documentación y evidencia del cobro/reinicio/cierre; su hash se entrega en el reporte final de la conversación.

Sin push, remoto ni elección de licencia. Identidad Git local conservada.

## Pendiente y próximo paso

Obtener confirmación del usuario sobre los pasos visuales manuales. Si no los comprobó, completar sólo esos casos mediante un nuevo recorrido sintético identificado, conservando esta venta y caja cerrada. No declarar los 24 pasos aprobados por inferencia. No se requiere volver a implementar 1E.

No se inició FASE 1F ni stock, descuentos, devoluciones, tarjetas, fiscalidad, clientes, impresión, cajón, rediseño o fullscreen. Persisten límites de desarrollo: no producción, pruebas físicas ni fallo eléctrico certificado. Backup/restauración de 1E ya constan en la evidencia técnica previa; no se repitieron en este bloque. Detener trabajo tras el reporte y resolver únicamente la confirmación visual pendiente.
