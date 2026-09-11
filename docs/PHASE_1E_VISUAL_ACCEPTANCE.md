# CASA MIGA POS — aceptación visual FASE 1E

Fecha local: 2026-09-10. Estado definitivo: **FASE 1E — CERRADA**. Los 24 pasos del smoke WPF están completos mediante observación manual, control de la aplicación y evidencia técnica. No hubo cambios funcionales.

## Resultado y participación manual

Se usaron exclusivamente datos sintéticos en work/phase1e-visual-20260910. La herramienta no consiguió escribir en el modal: activar la ventana propietaria quitaba foco. No se demostró bug de aplicación ni se introdujo ningún ajuste para la herramienta. El usuario completó el cobro durante la pausa; se constató la venta al retomar. Codex reinició la publicación, comprobó POS vacío, contó $53.00 y confirmó cierre mediante WPF. No se repitió el cobro.

El usuario confirmó explícitamente que $50.00 no permitió confirmar, $100.00 mostró $47.00 de cambio, el resultado se mostró correctamente y se aceptó/cerró normalmente, y el regreso al POS dejó el carrito vacío con total $0.00. Esta observación manual se correlacionó con la evidencia SQLite, sin repetir la operación comercial.

Clasificación usada: **Manual** significa observación confirmada por el usuario; **Técnico** significa control WPF por Codex o evidencia verificable de SQLite/archivos; **Mixto** significa que el comportamiento visual manual y el estado técnico persistido se corroboran entre sí.

## Los 24 pasos solicitados

| Paso | Tipo | Resultado observado |
|---|---|---|
| 1. Arranque publicado | Técnico | Publicación autocontenida win-x64 inició correctamente. |
| 2. Login | Técnico | Usuario sintético autenticado mediante WPF. |
| 3. Caja OPEN | Técnico | Caja abierta con fondo cero, comprobada en UI y SQLite. |
| 4. Entrada al POS | Técnico | Navegación al POS validada mediante WPF. |
| 5. Dos líneas | Técnico | 2 Conchas de $19.00 y 3 Bolillos de $5.00. |
| 6. Total visible | Técnico | Total $53.00 correcto. |
| 7. Cobrar habilitado | Técnico | Deshabilitado con carrito vacío y habilitado con carrito válido. |
| 8. Abrir diálogo | Técnico | Diálogo modal abierto correctamente. |
| 9. Total del diálogo | Técnico | Total $53.00 mostrado. |
| 10. Introducir insuficiente | Manual | Usuario introdujo $50.00. |
| 11. Rechazar insuficiente | Manual | El sistema no permitió confirmar el cobro. |
| 12. Introducir suficiente | Mixto | Usuario introdujo $100.00; Payment conserva received_cents=10000. |
| 13. Cambio visible | Mixto | Usuario observó $47.00; Payment conserva change_cents=4700. |
| 14. Confirmar cobro | Mixto | Confirmación manual correlacionada con una Sale persistida. |
| 15. Una sola venta | Técnico | Exactamente 1 Sale, 2 SaleItem, 1 Payment y 1 sale.completed. |
| 16. Folio/total/recibido/cambio | Mixto | Resultado visual correcto; SQLite conserva folio, $53.00, $100.00 y $47.00. |
| 17. Aceptar resultado | Mixto | Usuario lo aceptó/cerró normalmente; checkout.pending quedó ausente. |
| 18. POS vacío después de cobrar | Mixto | Usuario observó carrito vacío y $0.00; Codex volvió a comprobarlo tras reinicio. |
| 19. Cerrar/reabrir | Mixto | Usuario cerró; Codex reabrió la publicación y realizó login. |
| 20. Venta persiste | Técnico | Mismo SaleId y OperationId, sin duplicados después del reinicio. |
| 21. Pending no reaparece | Técnico | checkout.pending ausente y POS sin aviso de recuperación. |
| 22. ExpectedCash | Técnico | UI y SQLite muestran $53.00. |
| 23. Conteo/cierre | Técnico | Conteo $53.00 y cierre confirmados mediante WPF; caja CLOSED. |
| 24. Esperado/diferencia | Técnico | Esperado $53.00, contado $53.00 y diferencia $0.00 en UI y SQLite. |

## Persistencia y cierre

Folio: 5635c1f408f34bd7974d03423a5a8d40-00000001. SaleId: 25548b79-f8db-4db1-a80f-a7d117203a68. OperationId: e1521c18-008e-4c0d-b377-5b57e887c6e6. Confirmada UTC 2026-09-10T19:54:19.8514937+00:00. Total/aplicado 5300 centavos, recibido 10000 y cambio 4700. Partidas: 3800 + 1500 = 5300. Usuario, terminal y sesión quedan asociados.

Caja 4dcd2c23-f48f-4ceb-a8f4-ffd5e57f84c4 CLOSED, cierre UTC 2026-09-11T00:44:23.9412796+00:00 (10 de septiembre local). Fondo 0 + efectivo aplicado 5300 = esperado 5300; contado 5300; diferencia 0. Auditorías cash.count y cash.close presentes. Aplicación cerrada normalmente al terminar. No abrir otra caja ni repetir esta venta.

## Verificación final posterior al cierre

Clean correcto. scripts/verify.ps1 ejecutó restore locked, build Release y suite completa: **153 aprobadas, 0 fallidas, 0 omitidas; 0 errores y 0 warnings de compilación**. Duración de pruebas: 1 min 1 s. Único aviso del runner: sobrescritura del TRX anterior.

Publicación autocontenida win-x64 completada en artifacts/phase1e-acceptance. Restore posterior devuelve lockfiles al estado del repositorio. Migraciones 001–006 verificadas por SHA-256 contra schema_migration; todas coinciden. Sin diferencias de src/tests/migrations contra 18fd3a8. git diff --check ejecutado antes del commit. No se cambiaron dependencias.

## Evidencia, archivos y Git

Nuevos: docs/evidence/phase1e-visual-restarted-empty.jpg, phase1e-visual-count-exact.jpg, phase1e-visual-closed.jpg y phase1e-final-* (estado SQLite, migraciones, clean, verificación, publicación, restore y hashes de publicación). Los archivos phase1e-visual-state.json y phase1e-visual-pending-20260910.jpg se conservan como evidencia histórica ANTERIOR al cobro.

Archivos actualizados en el cierre formal: AGENTS.md, README.md, docs/CURRENT_STATE.md, docs/NEXT_STEPS.md, docs/PHASE_1E_REPORT.md, docs/PHASE_1E_PROGRESS.md, docs/PHASE_1E_MANUAL_SMOKE.md, docs/PHASE_1E_VISUAL_ACCEPTANCE.md, docs/FILES.md, docs/operations.md, docs/roadmap.md y docs/evidence/phase1e-ui-smoke.md. Se añadió docs/evidence/phase1e-manual-confirmation.md. Ningún archivo de código, pruebas o migraciones fue modificado; no hubo correcciones de bugs.

- 92c5ef795b6da8e1cdf7f34ec9fb1b01cce4a5d2: baseline definitivo 1D, 120 pruebas verificadas.
- 18fd3a8e56c79ef7a5710443da09d364f965f6fa: implementación 1E.
- 55eecfb4961f0b0ef83ffce04c53ca735323b3b7: reporte técnico inicial.
- a6431e2008c6d8b4e6d2a168dbce1305b8ac87b7: intento visual anterior e instrucciones manuales.
- 728ecfdcb61432967aa0fd604b0a44aa722a0ca0: evidencia del cobro, reinicio y cierre, manteniendo pendientes las observaciones manuales todavía no recibidas en ese momento.
- El commit de cierre formal posterior registra la confirmación manual y el estado CERRADA; su hash se entrega en el reporte final de la conversación.

Sin push, remoto ni elección de licencia. Identidad Git local conservada.

## Cierre y próximo paso

La confirmación manual quedó incorporada y los 24 pasos están completos. No se repitió la venta, no se abrió otra caja y no se requiere trabajo adicional dentro de 1E.

No se inició FASE 1F ni stock, descuentos, devoluciones, tarjetas, fiscalidad, clientes, impresión, cajón, rediseño o fullscreen. Persisten límites de desarrollo: no producción, pruebas físicas ni fallo eléctrico certificado. Backup/restauración de 1E ya constan en la evidencia técnica previa; no se repitieron en este bloque. Próximo paso recomendado, sin implementarlo: definir y autorizar formalmente el alcance de la siguiente fase.
