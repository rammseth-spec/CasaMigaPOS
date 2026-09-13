# Smoke WPF 1I.5 — APROBADO por el usuario

Los 21 pasos quedan completados; la venta requerida por Cashier se realizó al final, después de reanudar, para corregir la desviación inicial de rol sin borrar operaciones.

| Pasos | Evidencia y cobertura |
|---|---|
| 1–3 | Mixta: preparación técnica DISABLED/10, navegación humana; producto preconfigurado, sin configuración manual necesaria. |
| 4–6 | Mixta: activación humana confirmada en captura ACTIVO/10; transición persistida. Preflight cubierto técnicamente; su mensaje previo no tiene captura independiente. |
| 7–10 | Mixta: login SMOKECASHIER/caja abierta, venta 00000003 $38/$100/$62; completado después de reanudación. Captura posterior del carrito vacío bajo Administrator y pending ausente; no se atribuye captura del carrito al Cashier. |
| 11–15 | Mixta: saldo 6, historial Cashier -2 (8→6), consistencia visible y SQLite. |
| 16–17 | Mixta: pausa visible y venta siguiente con evaluación DISABLED. |
| 18 | Mixta: Cashier venta 00000002, $19/$20/$1. |
| 19 | Mixta: saldo 8 después de venta pausada, sin consumo persistido. |
| 20–21 | Mixta: reanudación visible ACTIVO, persistida ENABLED versión 4. |

Intervención humana: capturas aportadas y aprobación final «SI». Insuficiencia no repetida manualmente: cubierta por pruebas automatizadas. Ninguna impresión física. Capturas conservadas en evidence/phase1i5-manual-captures; consulta en evidence/phase1i5-manual-final.json. Total manual $95, saldo final 6. Caja OPEN, sin cierre manual ni conteo; no afirmar diferencia manual cero. Backup/restauración y cierre de caja automatizados pertenecen a otra instalación.

## Guion y checkpoints anteriores (históricos)
# Smoke WPF final 1I — PENDIENTE DE CONFIRMACIÓN HUMANA

Instalación nueva: work/phase1i5-smoke-20260913/valid. Lanzador: work/phase1i5-smoke-20260913/Abrir_smoke_1I5.cmd. Publicación: artifacts/phase1i5-smoke. La contraseña nueva para SMOKEADMIN, SMOKEMANAGER y SMOKECASHIER está en synthetic-credential.txt de esa carpeta; no reutilizar la de 1I.4 ni versionarla. Darla explícitamente al usuario cada vez que deba iniciar sesión.

Preparado: CONCHA-SMOKE-1I, nombre Concha sintética 1I, $19.00 por producto; FinishedGood/Count; 1 producto = 1 pieza; MAIN inicializada con 10 piezas; preflight verde; DISABLED/v1. Sin caja, venta ni pago iniciales. Sin configuración de impresora física. La instalación automatizada de hardening y sus corrupciones/fallos son independientes de ésta.

## Administrator — activación

1. Entrar con SMOKEADMIN, pulsar Inventario.
2. Comprobar DESACTIVADO y fila CONCHA-SMOKE-1I con saldo 10.
3. Producto y artículo ya preparados: no reconfigurar.
4. Pulsar Verificar configuración y observar resultado válido.
5. Pulsar Activar, confirmar Sí.
6. Comprobar ACTIVO.

## Cashier — venta principal

7. Caja → Salir de mi usuario; entrar SMOKECASHIER. Abrir caja con fondo 0.00 (prerrequisito de venta en esta instalación nueva).
8. POS: agregar Concha sintética 1I y ajustar cantidad a 2; total $38.00.
9. Cobrar, recibido 100.00, confirmar; cambio esperado $62.00.
10. Aceptar/cerrar resultado. Carrito vacío. No reintentar impresión: está sin configurar deliberadamente. No recibir dinero real.

## Administrator — stock e historia

11. Salir del Cashier con carrito vacío; entrar SMOKEADMIN → Inventario.
12. Comprobar saldo 8 piezas.
13. Seleccionar artículo → Ver detalle; revisar historial.
14. Debe existir movimiento Venta, -2, antes 10, después 8.
15. Comprobar mensaje Inventario consistente.
16. Volver a lista; Pausar, confirmar Sí.
17. Comprobar PAUSADO.

## Venta durante pausa y reanudación

18. Entrar SMOKECASHIER; usar caja abierta; vender 1 unidad por $19.00, recibido 20.00, cambio $1.00; aceptar resultado.
19. Entrar SMOKEADMIN → Inventario; comprobar que el saldo sigue en 8.
20. Reanudar y confirmar Sí.
21. Comprobar ACTIVO.

Esperados financieros manuales: fondo 0, dos ventas/pagos, efectivo aplicado $57.00. Stock final 8; un consumo -2, la venta pausada no consume. No ejecutar más ventas sin indicación. No cerrar FASE 1I hasta recibir confirmación humana y verificar evidencia.

Se puede comprobar insuficiencia posteriormente con 9 unidades frente a 8, si resulta razonable: requerido/disponible, venta no registrada, carrito preservado; nunca pulsar alternativas de override. Esta comprobación todavía no se declara realizada.

## Avance humano 2026-09-13 y desviación de rol

Capturas del usuario confirman ACTIVO/saldo 10; venta 00000001 por SMOKEADMIN, 2 piezas/$38, recibido $100/cambio $62; carrito vacío/$0; movimiento Venta -2 (10→8), Inventario consistente; PAUSADO/saldo 8; login SMOKECASHIER con caja existente OPEN; venta 00000002 por $19, recibido $20/cambio $1; PAUSADO/saldo 8 después; reanudación ACTIVO/saldo 8. Usuario confirmó aplicación CERRADA.

Consulta SQLite read-only posterior: dos ventas/pagos, CONSUMED y DISABLED respectivamente, ENABLED versión 4, apertura 0, efectivo aplicado 5700 centavos (ExpectedCash calculable $57), caja OPEN, pending ausente, integrity_check ok y cero violaciones FK. Ver evidence/phase1i5-manual.json. No se ejecutó otra venta ni se cerró caja desde herramientas.

Los pasos 8–10 de la venta principal fueron realizados con Administrator, no Cashier. No se declaran cumplidos en el rol requerido. Pendiente humano: comprobar venta con consumo activo usando SMOKECASHIER, después de reabrir; una venta nueva de 2 unidades dejaría stock 6 y efectivo acumulado $95. No repetir la intención anterior ni crear otra caja. Esperar captura de login antes de instruir el cobro. No cerrar fase todavía.

Defecto visual adicional: texto heredado en InventoryView.xaml afirmaba que la asociación todavía no descontaba. Se reemplaza por explicación condicionada al consumo ACTIVO y commit, con regresión InventoryAssociationHelpExplainsConditionalConsumption. Sin cambios de política ni migraciones.
