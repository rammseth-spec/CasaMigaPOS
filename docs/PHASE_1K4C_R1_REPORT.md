# FASE 1K.4C-R1 — reporte técnico final

1. **Modelo utilizado:** GPT-6 (Codex).
2. **HEAD inicial:** `cb3f55f4ea47372be98d50bf1eb467bfa2b135e9`.
3. **Git inicial:** rama `master`, árbol limpio y sin push.
4. **DatePicker anterior:** el texto automático «Seleccione una fecha» podía partirse y no fijaba el formato operativo.
5. **DatePicker final:** dos controles WPF conservados con ayuda `dd/mm/aaaa` de una línea.
6. **Entrada manual:** se valida antes de aplicar el filtro.
7. **Formato:** exclusivamente `dd/MM/yyyy`; ejemplo válido `07/10/2026`.
8. **Calendario:** el botón y la selección visual del DatePicker permanecen disponibles.
9. **Fecha inválida:** `32/10/2026`, `31/02/2026` y texto muestran retroalimentación humana, no lanzan excepción ni cambian resultados.
10. **Desde > Hasta:** se rechaza con un mensaje específico y conserva el filtro anterior.
11. **Aplicar rango:** ejecuta la consulta sólo cuando ambas fechas son válidas y están ordenadas.
12. **Día final inclusivo:** se preserva el contrato previo de Application; R1 no modifica límites UTC ni consultas.
13. **Layout:** fechas, etiquetas y acción permanecen alineadas en 1280×820 y 1920×1080.
14. **Listado Venta:** muestra exclusivamente `Venta #NNNNNN` desde `Sale.Sequence`.
15. **Columna Caja:** conserva por separado el nombre de terminal.
16. **Redundancia eliminada:** la terminal ya no se concatena dentro de Venta.
17. **Título de detalle:** muestra exclusivamente `Venta #NNNNNN`.
18. **Caja en detalle:** continúa en el contexto junto con fecha y responsable.
19. **Paginación anterior:** sólo indicaba página actual y «hasta 50 por página».
20. **Paginación final:** `Página X de Y · N venta(s) · 50 por página`.
21. **Total:** usa el total real ya devuelto por la consulta, no el número de filas visibles.
22. **X/Y:** el total de páginas se calcula con el tamaño real de página.
23. **Singular/plural:** `1 venta` y `N ventas` se presentan correctamente.
24. **Sesión actual:** la fixture conserva exactamente 57 ventas en esa sesión.
25. **Página 1:** 50 filas, Anterior deshabilitado y Siguiente habilitado.
26. **Página 2:** siete filas, Anterior habilitado y Siguiente deshabilitado.
27. **Vacío:** no presenta una página ficticia y ambos botones quedan deshabilitados.
28. **Hoy/Ayer:** continúan usando fechas locales y pasaron regresión.
29. **Detalle:** snapshots, cantidades, subtotal, total, recibido y cambio pasaron regresión.
30. **Reimpresión:** permiso, botón y servicio permanecen sin cambios; no se invocó.
31. **1280×820:** listado y detalle materializados sin scroll horizontal.
32. **1920×1080:** listado y detalle materializados sin scroll horizontal.
33. **F11:** las regresiones de pantalla completa pasan.
34. **Escape:** conserva prioridad y regreso desde el detalle.
35. **Archivos:** tres de Presentation, una suite WPF y documentación de estado/reporte.
36. **Pruebas focales:** `SalesHistoryTests`, `SalesHistoryUsabilityTests` y `PopupEscapePresentationTests`.
37. **Resultado focal:** 28/28 (18 generales + 10 WPF), 0 fallidas y 0 omitidas.
38. **Suite completa:** `scripts/verify.ps1` sobre el código definitivo.
39. **Resultado completo:** 739/739 (561 generales + 178 WPF/Presentation), 0 fallidas y 0 omitidas.
40. **Release:** Phase1K4C-R1, 0 errores y 0 advertencias.
41. **git diff --check:** correcto.
42. **Hash protegido inicial:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
43. **Hash protegido final:** el mismo valor, verificado antes del checkpoint.
44. **Igualdad:** la base protegida permanece byte-idéntica.
45. **Schema:** 16 en la copia aislada.
46. **Migración 017:** ausente.
47. **Domain:** sin cambios.
48. **Application:** sin cambios.
49. **Infrastructure/SQLite:** sin cambios.
50. **Ventas:** ninguna operación comercial ni escritura de ventas durante R1.
51. **Caja:** ninguna apertura, cierre, conteo o movimiento.
52. **Impresión:** ningún trabajo enviado; la reimpresión no se pulsó.
53. **EXE:** `src/CasaMiga.Presentation/bin/Phase1K4C-R1/CasaMiga.Presentation.exe`.
54. **Launcher:** `work/Abrir-Casa-Miga-1K4C-R1.cmd`.
55. **DataDirectory:** `work/phase1k4c-r1-smoke-20261007`.
56. **Título:** `Casa Miga POS · Caja · DESARROLLO · 1K.4C-R1`.
57. **Launcher → EXE:** apunta únicamente a Phase1K4C-R1, limpia la variable heredada y usa el runtime local.
58. **Apertura automática:** se realiza después del commit y la comprobación de Git limpio.
59. **Estado de la aplicación:** se deja en login, sin autenticar ni operar.
60. **Commit local:** `fix: polish sales history usability for phase 1K.4C`.
61. **HEAD final:** se informa en la conversación después del único commit para evitar una referencia circular.
62. **Git final:** debe quedar limpio tras el commit.
63. **Push:** no se realiza.
64. **GitHub:** el checkpoint remoto permanece previsto después de 1K.4D.
65. **Riesgo pendiente:** falta el smoke humano visual/operativo de R1.
66. **Checklist:** fechas válidas/inválidas, Todos/Hoy/Ayer/Rango/Sesión, páginas 1/2 y 2/2, detalle, reimpresión sin pulsar, 1280×820, 1920×1080, F11 y Escape.
67. **Siguiente fase:** 1K.4D no se inicia automáticamente.

## Datos para revisión

- Usuario: `catalog-admin`
- Contraseña: `Phase1K2C-R2!2026`
