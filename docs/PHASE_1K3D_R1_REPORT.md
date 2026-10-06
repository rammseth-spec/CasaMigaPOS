# FASE 1K.3D-R1 — reporte técnico

Fecha: 06/10/2026. Estado: técnicamente completada; smoke final corto pendiente.

1. Modelo utilizado: GPT-6.1 Sol.
2. HEAD inicial real: `bd6771ec05b35fde505020f872c6e878f09ddfe9`.
3. Git inicial: árbol limpio, rama local `master`; `origin` ya configurado y QA ya publicado por petición posterior del usuario. El baseline autorizado `cfcd80c5...` quedó superado por el merge de historial GitHub `bd6771e...`; R1 no hizo push, rebase ni cambios de remoto.
4. Archivos de producto/prueba modificados: `InventoryDisplay.cs`, `ProductionViewModel.cs`, `ProductionView.xaml`, `RecipeProductionViewModelTests.cs` y `UsabilityPresentationTests.cs`; además se actualizaron `CURRENT_STATE.md`, `NEXT_STEPS.md` y este reporte.
5. Pluralización: `FormatProduction` conserva la unidad interna y elige `pieza` sólo para 1; el resto usa `piezas`.
6. Ejemplos: `1 pieza`, `2 piezas`, `6 piezas`, `94 piezas`.
7. MASS humano: cantidades mayores a un kilogramo con fracción se expresan exactamente en kg + g; por ejemplo `99 kg 687.5 g` y `9 kg 995 g`.
8. VOLUME humano: cantidades mayores a un litro con fracción se expresan exactamente en L + ml; por ejemplo `99 L 875 ml`.
9. Precisión: no hay redondeo silencioso ni conversión de almacenamiento; `312.5 g` y `125 ml` se conservan.
10. Historial: la columna ahora se llama `Planeado / real`, mide 215 DIP y admite Wrap; se validó a 1280×820 y 1920×1080.
11. «Sin producto»: se eliminó el checkbox operativo y se sustituyó por `Resultado obtenido`, con `N piezas obtenidas` o `sin producto terminado`, según el `ActualCount` ya persistido.
12. «Plan bloqueado»: el detalle muestra `Plan de producción`; el mensaje explica que el plan ya quedó fijado y ahora debe registrarse el resultado real.
13. Borrador: `Ingredientes que se descontarán`.
14. Confirmada: `Ingredientes consumidos`.
15. Comparación: texto derivado cuantifica la diferencia sin cambiar cantidades ni reglas.
16. Ejemplos: `Igual a lo planeado`, `1 pieza menos`, `2 piezas menos`, `1 pieza más`, `2 piezas más`; cero reales muestra la diferencia total.
17. ContextHelp de preview: explica Necesario, Disponible, Faltante y Estado; incluye 500 g / 350 g / 150 g y reitera que no reserva, descuenta, registra movimientos ni modifica el ledger.
18. Resultado real: contenido semántico conservado; 5 planeadas / 4 reales consume insumos de 5 y agrega 4 terminadas; cero no agrega producto terminado.
19. 1280×820: prueba WPF materializada correcta.
20. 1920×1080: prueba WPF materializada correcta.
21. ContextHelp R3: regresión incluida; overlay y ciclo de Escape conservados.
22. Pruebas focales: formato/pluralización, estados borrador/confirmado, resultado cero, comparaciones, historial textual, layout y ayudas.
23. Resultado focal: 7/7 generales y 4/4 WPF, sin fallos ni omisiones.
24. Suite completa: `scripts/verify.ps1`.
25. Resultado: 560/560 generales + 159/159 WPF = **719/719**, 0 fallidas, 0 omitidas.
26. Release: 0 errores y 0 advertencias.
27. `git diff --check`: correcto.
28. Hash inicial de base protegida: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
29. Hash final: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
30. Igualdad: confirmada byte a byte por SHA-256.
31. Schema: 16.
32. Migración 017: no existe y no se creó.
33. Domain: sin cambios.
34. Application: sin cambios.
35. Infrastructure: sin cambios.
36. Impresión: no se envió ningún trabajo ni se cambió configuración física.
37. Ventas: ninguna operación ejecutada.
38. Caja: ninguna apertura, cierre o cobro ejecutado.
39. Producción comercial: ninguna; la copia usa la fixture sintética conocida y no se registró otra producción para R1.
40. EXE R1: `src/CasaMiga.Presentation/bin/Phase1K3D-R1/CasaMiga.Presentation.exe`.
41. Launcher R1: `work/Abrir-Casa-Miga-1K3D-R1.cmd`.
42. DataDirectory R1: `work/phase1k3d-r1-smoke-20261006`.
43. Título: `Casa Miga POS · Caja · DESARROLLO · 1K.3D-R1`.
44. Launcher→EXE: ruta resuelta y hash verificados después del build.
45. Apertura automática: se realiza después de pruebas, documentación, commit y comprobaciones finales.
46. Estado de apertura: la aplicación se deja en login, sin autenticación ni operaciones.
47. Acceso anclado: no utilizado.
48. Commit: `fix: polish production usability for phase 1K.3D`.
49. HEAD final: se entrega en conversación después del commit para evitar referencia circular.
50. Git final: debe quedar limpio; sin push de R1.
51. Riesgo pendiente: sólo la inspección humana breve de legibilidad y texto. La fixture aislada hereda datos de prueba previos sin modificarlos; el recorrido R1 debe evitar guardar el nuevo borrador preparado.
52. Checklist final: login → Producción → Historial 5/4 → detalle y textos → nueva producción 5 piezas → preview y ayuda → preparar borrador → real 4 → comprobar diferencia → no registrar → salir.
53. Fuera de alcance confirmado: no Pedidos, no Tiendas, no migración 017, no POS/Carrito, no ticket y ningún cambio fuera de Presentation/pruebas/documentación.

## Datos de la fixture

La copia se creó desde la evidencia sintética aislada, no desde el DataDirectory manipulado durante el smoke manual. Contiene una producción confirmada 5/4, un borrador de faltante, perfil publicado, inventario sintético MASS/VOLUME/COUNT y los saldos esperados. La base R1 tiene SHA-256 `0F4FA0D1A991CBFF92B725CDBE6469F6CEF5C84763D6DDFE278A88508513B24C`. Sus registros previos de ventas/caja proceden de la fixture fuente y permanecieron intactos; R1 no ejecutó operaciones.

Las credenciales de revisión se verificaron contra la copia preparada y se entregan sólo en conversación, no se versionan en este documento.
