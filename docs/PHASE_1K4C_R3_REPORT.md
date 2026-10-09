# FASE 1K.4C-R3 — reporte técnico final

1. **Modelo:** GPT-6 (Codex).
2. **HEAD inicial:** `51651283cb97efe36e1f8f891998ba888a8232b6`.
3. **Git inicial:** `master`, limpio y sin push.
4. **Máscara:** edición basada en máximo ocho dígitos y representación derivada `dd/mm/aaaa`.
5. **Barras:** se insertan automáticamente después del segundo y cuarto dígito.
6. **Entrada:** el teclado directo sólo admite `0`–`9`.
7. **Progresión:** `0`, `07`, `07/1`, `07/10`, `07/10/2`, `07/10/20`, `07/10/202`, `07/10/2026`.
8. **Límite:** ocho dígitos significativos; el exceso no modifica el campo.
9. **Backspace:** elimina dígitos y recompone barras sin dejar estados rotos.
10. **Delete:** elimina el dígito correspondiente y vuelve a aplicar la máscara.
11. **Selección/Ctrl+A:** reemplazo completo y parcial con cursor coherente.
12. **Pegado compacto:** `07102026` produce `07/10/2026`.
13. **Pegado formateado:** `07/10/2026` se conserva.
14. **Letras/símbolos:** no contaminan la entrada; guiones y exceso en pegado se rechazan.
15. **Fecha válida:** continúa aplicándose mediante parsing estricto `dd/MM/yyyy`.
16. **Fecha inválida:** `32102026` produce `32/10/2026` y después error semántico.
17. **Retención:** el texto inválido permanece visible.
18. **Feedback:** conserva borde rojo y mensaje humano R2.
19. **Calendario:** intacto; escribe al mismo buffer con formato estricto.
20. **Placeholder:** `dd/mm/aaaa`, completo.
21. **Rango válido:** aplica la consulta normalmente.
22. **Rango invertido:** se rechaza y conserva resultados/textos.
23. **Día Hasta:** inclusión completa preservada, sin tocar Application ni consulta.
24. **1280×820:** máscara, campos, calendario y contenido caben sin scroll horizontal.
25. **1920×1080:** mismos criterios aprobados por regresión.
26. **F11:** regresión conservada.
27. **Escape:** regreso de detalle y prioridad del shell conservados.
28. **Archivos:** DateEntryControl, AssemblyInfo de amistad de pruebas, SalesHistoryUsabilityTests, README y documentación R3.
29. **Focales:** SalesHistoryTests, SalesHistoryUsabilityTests y PopupEscapePresentationTests.
30. **Resultado focal:** 32/32, cero fallidas y omitidas.
31. **Suite:** `scripts/verify.ps1` sobre el código definitivo.
32. **Resultado:** 743/743 (561 generales + 182 WPF), cero fallidas y omitidas.
33. **Release:** general y Phase1K4C-R3, 0 errores y 0 advertencias.
34. **git diff --check:** correcto.
35. **Hash protegido inicial:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
36. **Hash protegido final:** el mismo valor.
37. **Igualdad:** base protegida byte-idéntica.
38. **Schema:** 16.
39. **Migración 017:** no existe.
40. **Domain:** sin cambios.
41. **Application:** sin cambios.
42. **Infrastructure:** sin cambios.
43. **Ventas:** ninguna venta o cobro ejecutado.
44. **Caja:** ninguna apertura, cierre, conteo o movimiento.
45. **Impresión:** ningún trabajo enviado.
46. **EXE:** `src/CasaMiga.Presentation/bin/Phase1K4C-R3/CasaMiga.Presentation.exe`.
47. **Launcher:** `work/Abrir-Casa-Miga-1K4C-R3.cmd`.
48. **DataDirectory:** `work/phase1k4c-r3-smoke-20261007`.
49. **Título:** `Casa Miga POS · Caja · DESARROLLO · 1K.4C-R3`.
50. **Launcher → EXE:** específico R3, limpia variable heredada y usa runtime local.
51. **Apertura:** se ejecuta automáticamente después del checkpoint y Git limpio.
52. **App:** se deja en login, sin autenticar ni operar.
53. **Commit:** único commit local `fix: add masked date entry for phase 1K.4C`.
54. **HEAD final:** se informa tras crear el commit para evitar referencia circular.
55. **Git final:** debe quedar limpio.
56. **Push/PR:** no se realizan.
57. **GitHub:** próximo checkpoint después de aprobar 1K.4D.
58. **Riesgo:** falta el smoke humano final de la máscara R3.
59. **Checklist:** login, Historial, `07102026`, rango, Ctrl+A, `32102026`, error, corrección numérica, Backspace, Delete, pegados, calendario, no reimprimir y salir.
60. **1K.4D:** no se inicia.

## Datos para revisión

- Usuario: `catalog-admin`
- Contraseña: `Phase1K2C-R2!2026`
