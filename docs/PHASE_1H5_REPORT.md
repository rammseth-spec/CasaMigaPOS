# CASA MIGA POS — REPORTE FINAL Y CIERRE FASE 1H

Fecha: 2026-09-12. Este reporte es autocontenido. El commit que lo contiene fija el nuevo baseline oficial de FASE 1H.

1. **Estado definitivo FASE 1H:** CERRADA.
2. **Estado 1H.5:** completada; hardening y recuperación aprobados sin cambios de producto.
3. **Modelo utilizado:** Codex GPT-6; no fue necesario escalar a Astra.
4. **HEAD inicial/final:** inicial `397258d9b4711fa8f081323f111c6afe181a9ef5`; el final es el commit documental que contiene este reporte y se confirma con `git rev-parse HEAD` al registrarlo.
5. **Commits:** 1H.5 se registra en un commit documental separado; no contiene funcionalidad.
6. **Git final:** se exige `git diff --check` correcto y árbol limpio después del commit; sin remoto, push o licencia.
7. **Suite final:** 290 aprobadas, 0 fallidas, 0 omitidas.
8. **Build Release:** correcto, 0 errores y 0 advertencias.
9. **Migraciones y hashes:** 001–010 intactas; hashes en `evidence/phase1h5-migration-hashes.txt`; 011 ausente.
10. **Domain:** cantidades exactas Int64 en mg/µL/pieza, conversiones checked y reglas 1H estables.
11. **Ledger:** operaciones/movimientos CONFIRMED inmutables; correcciones mediante movimientos trazables.
12. **InventoryBalance:** proyección materializada escrita sólo por el store y condicionada por saldo/versión.
13. **Reconciliación:** cero diferencias válidas tras restart y restore; la divergencia aislada se detecta sin reparar.
14. **Idempotencia:** mismo UUID+contenido se aplica una vez; contenido distinto devuelve IdempotencyConflict; hashes preservados.
15. **Concurrencia:** recibos concurrentes se serializan; pérdidas no producen negativo; versiones obsoletas fallan sin parcialidad.
16. **Atomicidad/rollback:** multipartida inválida, insuficiencia, overflow y excepción previa al commit no dejan persistencia parcial; post-commit incierto recupera la confirmación.
17. **Restart/reopen:** nuevas instancias de Database, sesión y servicios recuperaron artículo, unidad, presentación, link, balances, movimientos, UUID, auditoría y reconciliación.
18. **Backup:** `.cmbackup` sintético creado correctamente, 20,486 bytes, SHA-256 `96064E56A42D3B9BBEDB474D22CF8C63CC3DE3A0746568396012E2D649F54C13`.
19. **Restore:** realizado exclusivamente en `work/phase1h5-restored-20260912-v2`, un directorio nuevo separado.
20. **integrity_check:** `ok`.
21. **foreign_key_check:** 0 violaciones.
22. **Inventory UI:** la UI funcional aprobada en 1H.4 permanece intacta; 1H.5 no cambió WPF.
23. **Unidades operativas:** masa g/kg, volumen ml/L y conteo pieza; mg/µL permanecen técnicos internos.
24. **Opening Balance:** positivo y cero válidos; segunda inicialización rechazada; corrección posterior sólo por ajuste.
25. **Receive:** intención positiva, nota opcional válida, autorización e idempotencia revalidadas.
26. **Waste:** razón estructurada, no negativo, autorización y rollback revalidados.
27. **Physical Count:** calcula gain/loss; NoChange no escribe; usa saldo/versión esperados.
28. **ProductInventoryLink:** link/unlink explícito, una asociación activa y cantidad positiva; no genera movimientos o ventas.
29. **Historial:** paginación, filtros, orden determinista, snapshots de movimiento y lectura read-only revalidados.
30. **Permisos:** Administrator view/configure/receive/adjust/waste; Manager view/receive/waste; Cashier ninguno.
31. **Application authorization:** usuario activo, permiso y terminal se revalidan en cada caso de uso; llamadas directas no autorizadas fallan.
32. **NoChange:** conteo igual devuelve NoChange sin operación, movimiento ni auditoría.
33. **ConcurrencyConflict:** conteo con balance/version intercalados se rechaza sin ajuste parcial.
34. **Evidencia smoke:** `SMOKE-H14` manual aprobado conserva 1 kg + 500 g − 100 g y conteo 1.5 kg, cuatro movimientos, saldo final 1.5 kg y reconciliación consistente. No se repitió porque no cambió producto.
35. **Cero mutación financiera:** snapshot antes/después: 0 Sale, 0 SaleItem, 0 Payment, 0 CashSession, ExpectedCash 0 y secuencia/folios 0.
36. **Checkout intacto:** no cambió y no consulta ProductInventoryLink.
37. **Tickets/impresión intactos:** regresión automatizada de ticket original, reimpresión y receipt snapshots permanece verde.
38. **Impresora:** no se utilizó en 1H.5.
39. **Riesgos/limitaciones:** SQLite es local; acceso administrativo al archivo queda fuera de la seguridad de la aplicación; zona horaria comercial, retención/cifrado de backups y pruebas de volumen/operación real siguen pendientes de producción.
40. **Pendientes no bloqueantes:** datos reales, recuperación administrativa de cuentas, políticas operativas, empaquetado/licencia y endurecimiento de despliegue.
41. **SALE_CONSUMPTION:** no implementado.
42. **Recetas:** no implementadas.
43. **Producción:** no implementada.
44. **Rediseño:** no iniciado.
45. **Instalador:** no iniciado.
46. **Decisión explícita:** FASE 1H CERRADA; no quedan pendientes bloqueantes dentro del alcance aprobado.
47. **Nuevo baseline oficial:** el commit documental de cierre que contiene este reporte; su hash se entrega en el reporte final posterior al commit.
48. **Próxima fase propuesta, sin implementarla:** planificar la integración inventario–venta y su política comercial/técnica antes de autorizar cualquier `SALE_CONSUMPTION`.
49. **Modelo recomendado para planificarla:** Sol; Astra sólo si aparece un defecto complejo real de concurrencia, recuperación, corrupción o arquitectura.

El sistema continúa siendo una versión de desarrollo y no está listo para producción. No se inició ninguna fase posterior.
