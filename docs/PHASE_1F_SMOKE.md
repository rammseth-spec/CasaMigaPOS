# FASE 1F — Smoke WPF

Fecha local: 2026-09-10/11. Entorno sintético aislado `work/phase1f-visual-20260910`, publicación win-x64 autocontenida y modo DESARROLLO. Se prepararon 55 ventas antes del recorrido. Durante el smoke no se creó ninguna venta, pago, auditoría ni movimiento de caja.

Clasificación: **M** observado en WPF; **T** comprobado mediante SQLite/pruebas/artefactos; **Mixto** cubierto por ambos.

| # | Paso | Evidencia | Tipo |
|---:|---|---|---|
| 1 | Abrir publicación de desarrollo | Ventana Casa Miga POS visible | M |
| 2 | Iniciar sesión como Administrator | “Administrador sintético” | M |
| 3 | Ver opción Ventas | Botón visible | Mixto |
| 4 | Abrir Historial | Pantalla de solo lectura | M |
| 5 | Mostrar total de resultados | 55 ventas | Mixto |
| 6 | Limitar página a 50 | Etiqueta “hasta 50 por página”; consulta LIMIT 50 | Mixto |
| 7 | Ordenar más reciente primero | secuencia 55 en página 1 | Mixto |
| 8 | Abrir página siguiente | Página 2 visible | M |
| 9 | Segunda página sin solapamiento | 5 ventas, secuencias 5→1 | Mixto |
| 10 | Deshabilitar Siguiente al final | Botón deshabilitado en página 2 | M |
| 11 | Volver a página anterior | Regreso a página 1 | M |
| 12 | Buscar folio exacto | secuencia 55, un resultado | Mixto |
| 13 | Rechazar folio inválido | mensaje de formato visible | Mixto |
| 14 | Capturar Desde local | `2026-09-10` | M |
| 15 | Capturar Hasta local | `2026-09-10` | M |
| 16 | Aplicar rango de día completo | 55 resultados | Mixto |
| 17 | Separar modos de filtro | rango limpia folio; pruebas rechazan combinación | Mixto |
| 18 | Limpiar filtros | vuelve a 55 ventas recientes | M |
| 19 | Seleccionar una venta | venta 55 seleccionada | M |
| 20 | Abrir detalle | contexto e identificadores visibles | Mixto |
| 21 | Mostrar partidas en orden | Concha histórica y Bolillo histórico | Mixto |
| 22 | Mostrar snapshots y Money exacto | $19×2=$38; $5×1=$5; total $43 | Mixto |
| 23 | Mostrar pago completo | CASH, aplicado $43, recibido $93, cambio $50 | Mixto |
| 24 | Volver desde detalle | botón y acción verificados | M |
| 25 | Cerrar sesión Administrator | regreso a login | M |
| 26 | Iniciar sesión como Cashier | “Cajero sintético” | M |
| 27 | Ocultar Ventas para Cashier | sólo Caja, Catálogo y POS | Mixto |
| 28 | Confirmar ausencia de mutación | 55 ventas, 110 partidas, 55 pagos, 55 auditorías | T |
| 29 | Cerrar aplicación | proceso WPF terminado | M |

Capturas: `phase1f-history-page1.png`, `phase1f-history-page2.png`, `phase1f-history-exact-folio.png`, `phase1f-history-invalid-folio.png`, `phase1f-history-local-date.png`, `phase1f-history-detail-top.png`, `phase1f-history-detail-payment.png` y `phase1f-cashier-no-sales.png` en `docs/evidence`.

La fecha de las ventas se presentó con offset `-06:00`. La zona usada es la local del equipo; no existe aún zona horaria comercial configurable. La aplicación quedó cerrada.
