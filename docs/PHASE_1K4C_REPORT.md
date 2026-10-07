# FASE 1K.4C — reporte técnico

1. **Modelo utilizado:** GPT-6 (Codex).
2. **HEAD inicial:** `01383e900f09311f9322782bb0ce6d5891dc7cb0`.
3. **Git inicial:** rama `master`, árbol limpio; `origin/QA` en `0d9953669ad7d0eceef50c38cec8f1a52c7fa96a` y sin push.
4. **Contrato Application modificado:** `SalesHistoryQuery`, `SalesHistoryFilter` y `SalesHistoryRow` en `SalesHistoryService.cs`.
5. **Campo opcional agregado:** `Guid? CashSessionId = null`; `SalesHistoryMode.CashSession` identifica la intención explícita.
6. **Compatibilidad null:** `null` conserva la consulta histórica completa, sus filtros previos y la paginación de 50.
7. **Query Infrastructure:** `SqliteSaleStore.QuerySales` incorpora el filtro opcional sin cambiar escrituras.
8. **Predicado conceptual:** `s.cash_session_id = $cash`, con parámetro UUID normalizado por el store.
9. **Antes de LIMIT:** el predicado forma el mismo `WHERE` usado por `count(*)` y por la selección paginada, antes de `ORDER BY`, `LIMIT` y `OFFSET`.
10. **Paginación:** regresión con 60 ventas más recientes fuera de la sesión y 57 dentro; la sesión produce páginas 50 + 7 completas y exclusivas.
11. **Sesión real:** Presentation entrega `opened?.Session.Id`, la sesión abierta que ya mantiene `MainViewModel`.
12. **Sin sesión:** no inventa UUID; muestra `No hay una sesión de caja abierta.` y cero resultados.
13. **Venta humana:** formato `Venta #000005`.
14. **Fuente:** `Sale.Sequence`, correlativo persistido leído de `sale.sequence`.
15. **Unicidad:** la secuencia es por terminal, no global.
16. **Desambiguación:** listado y detalle muestran `Nombre de caja · Venta #NNNNNN`.
17. **GUID ocultos:** SaleId, PaymentId, CashSessionId, TerminalId, UserId y ProductId no se presentan al operador.
18. **Pago:** `CASH` se localiza como `Efectivo`.
19. **Aplicado:** se retiró de la vista operativa.
20. **Fechas:** UTC persiste intacto y la UI convierte con la zona local a `dd/MM/yyyy · HH:mm`.
21. **Hoy:** botón rápido calcula el día local actual.
22. **Ayer:** botón rápido calcula el día local anterior.
23. **Rango:** DatePicker Desde/Hasta reutiliza el contrato validado.
24. **Día final:** Application mantiene el límite superior UTC exclusivo del día siguiente.
25. **Sesión actual:** botón dedicado consulta exclusivamente el CashSessionId abierto.
26. **Resumen:** muestra sólo el total fiable devuelto por la consulta (`N ventas`); no inventa piezas ni importe a partir de una página parcial.
27. **Listado:** Venta, fecha/hora, total, pago, responsable y caja; sin columnas técnicas ni scroll horizontal en los tamaños probados.
28. **Detalle:** encabezado humano, contexto local, partidas históricas y resumen financiero estructurado.
29. **Productos:** usa snapshots de nombre y precio guardados en cada SaleItem.
30. **Cantidades:** singular/plural `1 pieza` / `N piezas`.
31. **Subtotal:** visible y formateado en MXN.
32. **Total:** visible y formateado en MXN.
33. **Recibido:** visible desde el Payment histórico.
34. **Cambio:** visible desde el Payment histórico.
35. **Reimpresión:** botón y flujo existentes preservados; no se ejecutaron.
36. **Permisos:** `sales.view` y `sales.receipt.reprint` continúan revalidados por Application.
37. **No mutación:** consultas y apertura de detalle conservan sale, payment, audit, cash e inventario; pruebas comparan conteos.
38. **Vacíos:** existen mensajes distintos para historial vacío, periodo sin resultados, sesión sin ventas y ausencia de sesión abierta.
39. **1280×820:** listado y detalle materializados sin scroll horizontal.
40. **1920×1080:** listado y detalle materializados sin scroll horizontal.
41. **F11:** regresiones existentes de `PopupEscapePresentationTests` conservan entrada/salida y prioridad de Escape.
42. **Archivos modificados:** seis de producto, dos suites de pruebas y documentación; lista exacta en el commit.
43. **Pruebas focales:** `SalesHistoryTests`, `SalesHistoryUsabilityTests` y `PopupEscapePresentationTests`.
44. **Resultado focal:** 26/26 aprobadas (18 generales + 8 WPF), 0 fallidas y 0 omitidas sobre Release.
45. **Suite completa:** `scripts/verify.ps1` sobre el código definitivo.
46. **Resultado:** 737/737 (561 generales + 176 WPF/Presentation), 0 fallidas y 0 omitidas.
47. **Release:** `Phase1K4C`, 0 errores y 0 advertencias.
48. **git diff --check:** correcto.
49. **Hash protegido inicial:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
50. **Hash protegido final:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
51. **Igualdad:** idénticos; la base protegida no fue abierta para escritura.
52. **Schema:** 16 en la copia aislada validada.
53. **Migración 017:** no existe.
54. **Domain:** sin cambios.
55. **Application:** sólo añade el filtro opcional, expone Sequence en la fila y valida combinaciones de consulta.
56. **Infrastructure:** sólo añade el predicado de lectura y materializa Sequence ya persistido.
57. **Ventas nuevas:** ninguna en datos protegidos o comerciales; la fixture aislada agregó 117 ventas `SMOKE` por herramienta, nunca desde la UI.
58. **Caja:** no hubo apertura, conteo o cierre sobre datos protegidos; la fixture crea una sesión auxiliar aislada para probar paginación.
59. **Impresión:** no se envió ningún trabajo ni se invocó reimpresión.
60. **EXE:** `src/CasaMiga.Presentation/bin/Phase1K4C/CasaMiga.Presentation.exe`.
61. **Launcher:** `work/Abrir-Casa-Miga-1K4C.cmd`.
62. **DataDirectory:** `work/phase1k4c-smoke-20261007`.
63. **Título:** `Casa Miga POS · Caja · DESARROLLO · 1K.4C`.
64. **Launcher → EXE:** apunta sólo a Phase1K4C, limpia `CASAMIGA_DATA_DIR` y usa el runtime local del proyecto.
65. **Apertura automática:** se realiza tras commit, Git limpio y validación final del artefacto.
66. **Estado:** se deja en login, sin autenticación ni operaciones; proceso/título/lease se registran en la evidencia local final.
67. **Commit local:** `feat: improve sales history usability for phase 1K.4C`.
68. **HEAD final:** se informa en la conversación tras crear el único commit, evitando referencia circular en este archivo.
69. **Git final:** debe quedar limpio tras el commit.
70. **Push:** no se realiza.
71. **Próximo GitHub:** después de 1K.4D, conforme a la autorización.
72. **Riesgos:** falta el smoke humano; el resumen deliberadamente no agrega piezas/importe porque el contrato paginado no entrega agregados completos.
73. **Checklist manual:** login; Historial; Todos/Hoy/Ayer/Rango/Sesión actual; páginas 1 y 2 de sesión; abrir detalle; revisar Venta, caja, fechas, cantidades, total/recibido/cambio; comprobar botón de reimpresión sin pulsarlo; revisar 1280×820, 1920×1080 y F11; salir sin operar.
74. **Siguiente fase:** 1K.4D no se inicia automáticamente.

## Datos para revisión

- Usuario: `catalog-admin`
- Contraseña: `Phase1K2C-R2!2026`
