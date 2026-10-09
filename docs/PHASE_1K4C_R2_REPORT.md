# FASE 1K.4C-R2 — reporte técnico final

1. **Modelo:** GPT-6 (Codex).
2. **HEAD inicial:** `50f7349e51688d34615b997228a6243ba0b1032e`.
3. **Git inicial:** `master`, limpio, sin push.
4. **Clipping reproducido:** `07/10/2026` quedaba visualmente truncado; DatePicker 190 px, editor 62 px y viewport 38 px.
5. **Borrado reproducido:** `32/10/2026` permanecía mientras tenía foco y desaparecía al perderlo/aplicar.
6. **Causa raíz:** el botón interno heredaba el ancho mínimo global; después el parser del DatePicker restauraba texto desde `SelectedDate=null`.
7. **Responsable:** template interno `DatePickerTextBox`/`PART_Button` y sincronización `Text`–`SelectedDate` del DatePicker WPF.
8. **Solución:** editor compuesto pequeño, con buffer textual independiente y validación explícita.
9. **Control:** se sustituyó el DatePicker estándar por `DateEntryControl` sólo en Presentation.
10. **Placeholder:** `dd/mm/aaaa`, completo y de una línea.
11. **Ancho:** campo 210 px, calendario 42 px y editor útil mínimo validado de 165 px.
12. **Escritura manual:** se comporta como TextBox normal y conserva los caracteres.
13. **Parsing:** estricto `dd/MM/yyyy`.
14. **Cultura:** `CultureInfo.InvariantCulture`, sin depender de Windows.
15. **Fecha válida:** `07/10/2026` se conserva y aplica.
16. **Fecha inválida:** se rechazan día/mes inexistentes, texto y separadores/formato distintos.
17. **Retención:** el texto inválido permanece hasta que el operador lo corrige.
18. **Feedback:** borde de error y mensaje cercano “Fecha inválida. Usa dd/mm/aaaa.”.
19. **Escritura parcial:** no genera error por carácter; sólo limpia un error previo mientras se edita.
20. **LostFocus:** valida discretamente al abandonar el editor, sin MessageBox.
21. **Aplicar:** consulta sólo con ambas fechas válidas.
22. **Desde > Hasta:** se rechaza con mensaje específico y conserva ambos textos.
23. **Resultados:** el listado y filtro anteriores permanecen ante cualquier rechazo.
24. **Calendario:** botón fijo abre Calendar y devuelve el mismo formato estricto.
25. **Día Hasta:** la semántica existente de día local completo permanece sin cambios.
26. **1280×820:** texto, calendario, tabla y detalle caben sin scroll horizontal.
27. **1920×1080:** mismos criterios verificados.
28. **Todos/Hoy/Ayer:** pasan regresión y actualizan los buffers humanos.
29. **Sesión actual:** 57 ventas, distribuidas en 50 + 7.
30. **Paginación:** `Página 1 de 2` y `Página 2 de 2` conservadas.
31. **F11:** cubierto por regresión de shell, sin cambio funcional.
32. **Escape:** regresa del detalle y conserva la prioridad aprobada.
33. **Archivos:** DateEntryControl, vista/VM/code-behind de Historial, prueba WPF, README, estado, próximos pasos, evidencia y este reporte.
34. **Focales:** SalesHistoryTests, SalesHistoryUsabilityTests y PopupEscapePresentationTests.
35. **Resultado focal:** 29/29, cero fallidas y omitidas.
36. **Suite:** `scripts/verify.ps1` sobre el código definitivo.
37. **Resultado total:** 740/740 (561 generales + 179 WPF), cero fallidas y omitidas.
38. **Release:** compilación general y Phase1K4C-R2, 0 errores y 0 advertencias.
39. **git diff --check:** correcto.
40. **Base protegida antes:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
41. **Base protegida después:** el mismo SHA-256.
42. **Igualdad:** byte-idéntica.
43. **Schema:** 16.
44. **Migración 017:** no existe.
45. **Domain:** sin cambios.
46. **Application:** sin cambios.
47. **Infrastructure:** sin cambios.
48. **Ventas:** no se ejecutaron ventas ni cobros.
49. **Caja:** no se abrió, cerró, contó ni movió.
50. **Impresión:** no se envió ningún trabajo.
51. **EXE:** `src/CasaMiga.Presentation/bin/Phase1K4C-R2/CasaMiga.Presentation.exe`.
52. **Launcher:** `work/Abrir-Casa-Miga-1K4C-R2.cmd`.
53. **DataDirectory:** `work/phase1k4c-r2-smoke-20261007`.
54. **Título:** `Casa Miga POS · Caja · DESARROLLO · 1K.4C-R2`.
55. **Launcher → EXE:** ruta específica R2, variable heredada limpia y runtime local.
56. **Apertura:** el launcher exacto se ejecuta automáticamente tras checkpoint y Git limpio.
57. **Estado de app:** se deja en login, sin autenticar.
58. **Commit:** único commit local `fix: correct sales history date entry for phase 1K.4C`.
59. **HEAD final:** se informa después del commit en la conversación para evitar una referencia circular.
60. **Git final:** debe quedar limpio.
61. **Push:** no se realiza.
62. **GitHub:** el próximo checkpoint permanece después de 1K.4D.
63. **Riesgo:** falta la aprobación humana visual del build R2.
64. **Checklist:** login, Historial, placeholder/texto completo, válido, cinco inválidos, corrección, calendario, rango invertido, resultados, sesión, F11, Escape, no reimprimir y salir.
65. **1K.4D:** no se inicia.

## Datos para revisión

- Usuario: `catalog-admin`
- Contraseña: `Phase1K2C-R2!2026`
