# CASA MIGA POS — REPORTE FINAL FASE 1J.4

Fecha: 2026-09-24. Reporte autocontenido de implementación, verificación y smoke.

1. **Estado FASE 1J:** abierta; 1J.1–1J.4 completadas dentro de sus alcances autorizados.
2. **Estado 1J.4:** completada y aprobada por suite automatizada y evidencia visual del smoke sintético.
3. **Modelo utilizado:** Sol, conforme a la recomendación de la autorización; no apareció un bloqueo estructural que justificara Astra.
4. **HEAD inicial:** `a14007d9921e378e57aabd5e68de7e34f7464389`.
5. **HEAD final:** corresponde al commit que incorpora este reporte; consultar `git rev-parse HEAD` en el checkpoint.
6. **Commits:** un commit cohesivo de producto, pruebas, evidencia y documentación para 1J.4.
7. **Git final:** limpio después del commit; `work/`, `artifacts/` y `TestResults/` permanecen ignorados.
8. **Schema final:** 15.
9. **Migraciones 001–015:** comparación Git contra el baseline aprobada; no se modificó ninguna.
10. **Migración 016:** no existe y no se creó.
11. **Presentation creada:** `RecipeView.xaml/.cs`, `RecipeViewModel.cs`, `ProductionView.xaml/.cs` y `ProductionViewModel.cs`.
12. **Presentation modificada:** `MainViewModel`, `MainWindow.xaml/.cs`, `PosView.xaml/.cs` e `InventoryDisplay`.
13. **Cambios mínimos Application/Infrastructure:** `StoredProductionBatch` expone nombres opcionales de creador/terminal y la lectura SQLite los obtiene mediante joins read-only; no cambió la escritura crítica.
14. **Navegación Recetas:** integrada con el estilo funcional existente y visible sólo cuando el permiso corresponde.
15. **Permisos Recetas:** Administrator consulta/configura; Manager consulta; Cashier no accede; Application sigue siendo autoridad.
16. **Lista de recetas:** pestañas Fórmulas/Perfiles con código, nombre, versión activa y disponibilidad de borrador.
17. **Detalle de receta:** muestra versiones, estado y versión activa sin GUID/hash como contenido operativo.
18. **Crear receta:** Administrator crea fórmulas y perfiles mediante `RecipeService`.
19. **Editar borrador:** editor funcional de versión DRAFT con revisión; una publicada no se edita.
20. **Publicar:** acción disponible para Administrator y refresco inmediato de versiones publicadas.
21. **Activar:** acción separada que actualiza la versión activa y conserva el historial.
22. **Manager read-only:** puede abrir Recetas y Perfiles, sin controles activos de creación/edición/publicación/activación.
23. **Ingredientes de fórmula:** grilla para artículo, cantidad, unidad y política.
24. **Unidades UI:** captura/visualización operativa exacta; sin float/double como fuente de verdad.
25. **Políticas COUNT:** etiquetas humanas para cantidad exacta y redondeo hacia pieza completa, conservando el enum interno.
26. **Alternativas:** grupos/opciones, orden de preferencia, cantidad, unidad y política; sin ingredientes hardcodeados.
27. **ProductProductionProfile:** UI administrativa para producto, artículo terminado y versiones del perfil.
28. **FormulaRequirement:** selección de una versión publicada y cantidad exacta por pieza.
29. **DirectIngredientRequirement:** artículo, cantidad operativa y política por pieza.
30. **Navegación Producción:** integrada para Administrator/Manager y ausente para Cashier.
31. **Builder de nueva producción:** agrega/elimina productos y valida cantidades planeadas positivas antes del DRAFT.
32. **Multi-output:** varias líneas de productos en un mismo plan; smoke con A=2 y B=3.
33. **Preview:** `Calcular necesidades` llama `PrepareProduction`; no reserva ni escribe stock.
34. **Agregación de fórmula:** muestra preparación compartida agregada; smoke 650 g.
35. **Insumos:** grilla de artículo, necesario, disponible y estado; agrupa líneas repetidas para lectura humana.
36. **Stock:** cantidades operativas exactas y estado Disponible.
37. **Faltantes:** presenta la colección completa devuelta por el servicio, no sólo el primero.
38. **Selección alternativa:** muestra la opción resuelta y conserva selecciones explícitas del contrato.
39. **NonRepresentableScale:** mensaje humano que pide revisar cantidad/receta; no redondea ni expone racionales crudos.
40. **Crear DRAFT:** disponible para Administrator/Manager después de recalcular y revalidar el preview.
41. **Bloqueo DRAFT:** el detalle explica que el plan ya creado no puede modificarse.
42. **Creador/terminal:** se muestran nombres humanos; la regla existente de confirmación ligada a creador/terminal no cambió.
43. **Resultado real:** captura entera no negativa por output con comparación inmediata.
44. **Actual menor:** permitido y marcado `Menor que lo planeado`; smoke A 2→1.
45. **Actual igual:** permitido y marcado `Igual a lo planeado`; cubierto por pruebas/UI.
46. **Actual mayor:** permitido y marcado `Mayor que lo planeado`; smoke B 3→4.
47. **Cero parcial:** permitido sin bloquear otros outputs positivos; cubierto por pruebas.
48. **Zero-output total:** todos los reales pueden ser cero; el servicio conserva consumo sin salida.
49. **Advertencia zero-output:** diálogo previo explica que se descontarán ingredientes y ofrece cancelar/confirmar.
50. **ConfirmProduction UI:** botón llama al servicio, presenta resumen y deja el detalle histórico de sólo lectura tras éxito.
51. **Doble envío:** comandos ocupados deshabilitan acciones durante la operación; la idempotencia real permanece en Application/Infrastructure.
52. **Recuperación idempotente:** un resultado recuperado se presenta como confirmación normal, no como error técnico.
53. **Stock insuficiente:** mensaje específico y lista completa de faltantes; el DRAFT permanece pendiente.
54. **Artículo inactivo:** mensajes distinguen insumo o producto terminado inactivo sin mostrar códigos internos.
55. **Concurrencia:** mensaje indica que la información cambió/fue confirmada y pide actualizar.
56. **Historial:** listado read-only con fecha, estado, creador, terminal, productos, planeado/real y sin salida.
57. **Detalle histórico:** productos, preparaciones, consumos y alternativas desde snapshots persistidos.
58. **Datos técnicos ocultos:** flujo habitual no muestra GUID, OperationId, hash, revisiones, tablas ni racionales.
59. **Presentación física:** `g/kg`, `ml/L` y `pieza`; las unidades atómicas quedan internas.
60. **Administrator:** experiencia completa para configurar, consultar, planear y confirmar.
61. **Manager:** recetas read-only y producción crear/confirmar/historial; smoke realizado con Manager.
62. **Cashier:** captura final mostró únicamente Caja, Catálogo y POS; Application también carece de permisos 1J.
63. **Smoke sintético:** fórmula/perfiles, preview, multi-output, DRAFT, reinicio, actuals, confirmación, saldos e historial aprobados.
64. **Sin recetas reales hardcodeadas:** sólo datos sintéticos locales de smoke/tests, ignorados por Git.
65. **Pruebas focalizadas:** 10/10 nuevas de UI/ViewModel; durante implementación, filtro ampliado 87/87.
66. **Suite completa:** 530/530 aprobadas; 0 fallidas y 0 omitidas; duración 5 m 13 s.
67. **Release:** 0 errores y 0 advertencias de compilación.
68. **`git diff --check`:** aprobado; sólo avisos informativos de normalización de finales de línea.
69. **Regresión POS:** incluida en la suite completa; smoke no registró ventas.
70. **Regresión Inventario:** suite aprobada y smoke confirmó consumo/salida/saldos exactos.
71. **Regresión Ventas:** incluida en 530/530; no se modificó Checkout ni persistencia comercial.
72. **Regresión Caja:** incluida en 530/530; no se abrió caja durante el smoke.
73. **Regresión Impresión:** incluida en suite; no se imprimió ni cambió transporte físico.
74. **RecipeService:** regresión de crear, guardar, publicar, activar, permisos y concurrencia aprobada.
75. **ProductionService:** regresión de preview, DRAFT, confirmación, stock, idempotencia, recuperación y concurrencia aprobada.
76. **Restricciones actuales:** MAIN, DRAFT ineditable, sin reserva, confirmación por creador/terminal, sin transferencia ni recetas reales.
77. **Riesgos:** el operador debe conservar identidad/revisión en reintentos; stock puede cambiar entre preview y confirmación; limitación creador/terminal puede afectar turnos.
78. **Pendientes 1J.5:** hardening operativo de recuperación, faltantes cambiantes, zero-output manual, backup/restore UI y evaluación de turnos.
79. **1J.5:** NO iniciada; no hay cambios de schema, rediseño, fullscreen, compras/proveedores o impresión de producción.
80. **Próximo paso:** revisar este cierre y autorizar por separado una propuesta acotada de 1J.5.
81. **Modelo recomendado para 1J.5:** Astra si aborda recuperación/concurrencia crítica; Sol para ajustes de Presentation aislados.

## Evidencia adicional

Base de smoke: `integrity_check=ok`, 0 violaciones FK, un lote confirmado, auditorías `production.created=1` y `production.confirmed=1`. Saldos finales: masa A 24.675 kg, alternativa 25 kg, líquido 19.935 L, COUNT 192, terminado A=1 y B=4. La documentación `PHASE_1J4_SMOKE.md` conserva el recorrido humano. El producto continúa como versión de desarrollo y no está listo para producción.
