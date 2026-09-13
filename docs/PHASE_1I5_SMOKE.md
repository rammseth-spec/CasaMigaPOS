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
