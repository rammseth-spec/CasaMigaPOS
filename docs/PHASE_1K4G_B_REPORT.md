# CASA MIGA POS — REPORTE FINAL FASE 1K.4G-B

1. **Fase:** 1K.4G-B — Plan de producción multiproducto y ejecución segura.
2. **Estado:** técnicamente completada; smoke manual humano pendiente.
3. **Fecha de cierre técnico:** 10/10/2026.
4. **Modelo utilizado:** GPT-6 Sol.
5. **HEAD inicial:** `462b4233cb2ef6592fe4a049addbcf24e741d7a4`.
6. **Working tree inicial:** limpio.
7. **Rama local:** `master`.
8. **Identidad Git:** `Rammseth <rammstudioworkshop@gmail.com>`.
9. **Repositorio:** `https://github.com/rammseth-spec/CasaMigaPOS.git`.
10. **Checkpoint previo:** publicado por avance normal en `origin/QA` antes de modificar producto.
11. **Resultado del push previo:** `origin/QA` quedó en `462b4233cb2ef6592fe4a049addbcf24e741d7a4`.
12. **Seguridad Git del push previo:** sin force, rebase, reset, merge ni rama nueva.
13. **Push final:** no realizado; el cierre 1K.4G-B permanece local.
14. **Migraciones:** no se creó migración 018.
15. **Schema protegido:** 16, intacto.
16. **SHA-256 protegido esperado:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
17. **Schema de fixture:** 17.
18. **Persistencia nueva:** ninguna; se reutiliza el contrato de producción existente.
19. **Productos visibles:** la selección enumera todos los productos activos.
20. **Búsqueda:** filtra por producto, receta o estado derivado.
21. **Estado listo:** `LISTO PARA PRODUCIR`.
22. **Estado incompleto:** `RECETA INCOMPLETA`.
23. **Estado ausente:** `SIN RECETA`.
24. **Naturaleza de estados:** derivados en lectura; no agregan columnas ni flags persistidos.
25. **Criterio listo:** perfil activo publicado, fórmulas publicadas, insumos activos y salida terminada activa en piezas.
26. **Sin receta:** puede agregarse al plan para que el bloqueo sea visible y explicable.
27. **Receta incompleta:** puede agregarse, pero impide calcular o confirmar.
28. **Reventa:** no se infiere a partir de la ausencia de perfil.
29. **Duplicados:** el mismo producto no puede repetirse en el plan.
30. **Cantidad planeada:** entero estrictamente mayor que cero.
31. **Plan vacío:** permanece no calculable.
32. **Mensajes:** explican producto y configuración faltante con texto humano.
33. **Fórmulas compartidas:** se conserva la agrupación por `FormulaVersionId` del dominio existente.
34. **Consolidación:** varias salidas que usan la misma preparación suman una sola necesidad exacta.
35. **Ingredientes directos:** permanecen incluidos y consolidados por el plan existente.
36. **Alternativas:** conservan el orden explícito de preferencia y la selección visible.
37. **Vista previa:** sólo consulta; no reserva, descuenta ni escribe movimientos.
38. **Revalidación:** el stock se vuelve a comprobar al registrar la producción.
39. **Estados de stock:** `Suficiente` y `Faltante`, además de bloqueos de configuración existentes.
40. **Masa interna:** miligramos, sin `double`.
41. **Volumen interno:** microlitros, sin `double`.
42. **Conteo interno:** piezas enteras.
43. **Presentación humana:** g/kg, ml/L y pieza/piezas.
44. **Escala:** usa razones exactas del dominio y conserva errores no representables.
45. **Borrador:** inmoviliza snapshots de salidas, fórmulas, insumos y alternativas.
46. **Resultado real:** se captura por producto.
47. **Resultado real válido:** entero igual o mayor que cero.
48. **Resultado menor:** los insumos se consumen según el plan bloqueado.
49. **Salida terminada:** se incrementa según las piezas reales obtenidas.
50. **Resultado cero:** consume el plan y no crea movimiento de salida terminado.
51. **Confirmación:** requiere acción explícita del usuario.
52. **Atomicidad:** consumo, salida y cambio de estado se ejecutan en una sola unidad de trabajo.
53. **Fallo:** cualquier error revierte todos los efectos de confirmación.
54. **Trazabilidad:** conserva lote, actor, terminal, revisión, snapshots y operaciones del ledger.
55. **Consumo:** utiliza `PRODUCTION_CONSUMPTION`.
56. **Salida:** utiliza `PRODUCTION_OUTPUT`.
57. **Venta posterior:** conserva `product_inventory_link` y el consumo existente sin doble descuento.
58. **Merma terminada:** se habilitó desde Producción reutilizando el ledger `WASTE` existente.
59. **Merma mínima:** producto, piezas, motivo y observación.
60. **Merma segura:** exige entero positivo y rechaza existencia negativa.
61. **Motivos:** dañado, vencido, error de producción u otro con observación obligatoria.
62. **Fixture:** `work/phase1k4g-b-smoke-20261010`.
63. **Datos de fixture:** exclusivamente sintéticos y marcados como no comerciales.
64. **Escenario compartido:** Producto sintético A + B sobre una preparación común.
65. **Escenario separado:** Producto sintético C con faltante controlado.
66. **Escenario sin receta:** producto activo visible y bloqueante.
67. **Escenario incompleto:** perfil sin versión activa visible y bloqueante.
68. **Dimensiones fixture:** MASS, VOLUME y COUNT.
69. **Salidas fixture:** artículos terminados inicializados y vinculados uno a uno.
70. **Borrador fixture:** A+B preparado para capturar resultado real menor al planeado.
71. **Ventas fixture:** cero ventas comerciales.
72. **Impresión fixture:** sin `printing.json`; no se envió ningún trabajo.
73. **Cajón:** no se abrió ni se modificó su configuración.
74. **Focales de producción:** 78/78 aprobadas.
75. **Focales WPF:** 3/3 aprobadas, incluyendo 1280×820, 1920×1080 y 3440×1440.
76. **Suite oficial:** 796/796 aprobadas: 584 generales + 212 WPF.
77. **Fallidas/omitidas:** 0/0.
78. **Release identificado:** `src/CasaMiga.Presentation/bin/Phase1K4G-B/`, 0 errores y 0 advertencias.
79. **Launcher:** `work/Abrir-Casa-Miga-1K4G-B.cmd`, DataDirectory aislado y título `Casa Miga POS · Caja · DESARROLLO · 1K.4G-B`.
80. **Fuera de alcance:** Reporte Diario conserva Producción como no disponible; no se inició 1K.4G-C, 1K.4H-B, Premium UI ni 1K.5.

## Revisión manual solicitada

Iniciar sesión, abrir Producción y revisar búsqueda/estados, plan A+B, consolidación de la preparación compartida, faltante de C, bloqueos sin receta/incompleta, borrador existente, captura real menor y merma terminada. No imprimir ni usar datos comerciales.
