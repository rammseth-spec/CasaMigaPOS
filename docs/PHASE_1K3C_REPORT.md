# FASE 1K.3C — Usabilidad específica de Recetas

Fecha: 05/10/2026

Estado: **técnicamente completada; smoke manual pendiente**

Modelo utilizado: Codex, GPT-6 (familia Sol)
Baseline: e9110714003fc8e1620b0f26c9b950926c6bcd53

## Resultado

1. El alcance se mantuvo en Presentation, pruebas y documentación.
2. Domain, Application e Infrastructure no fueron modificados.
3. No se modificaron migraciones; schema continúa en 16 y no existe 017.
4. No se cargaron recetas, versiones, perfiles ni ingredientes comerciales.
5. La búsqueda de ingredientes funciona por código o nombre sin Enter ni botón Buscar.
6. El filtro usa debounce de 250 ms y cancela consultas anteriores.
7. × Limpiar restaura la lista.
8. Una selección válida permanece disponible aunque el filtro cambie.
9. El estado vacío dice «No encontramos ingredientes con esa búsqueda.».
10. No se usan MessageBox para búsqueda o estados vacíos normales.
11. GUID e identificadores técnicos permanecen internos.
12. MASS, VOLUME y COUNT se muestran como Masa, Volumen y Piezas.
13. Masa permite solamente g/kg; Volumen ml/L; Piezas, pieza.
14. El usuario ya no escribe manualmente unidades en Recetas.
15. Las cantidades siguen pasando por InventoryDisplay.ParseOperational, decimal exacto y atomic units Int64; no se introdujo float/double.
16. Para piezas se muestran «Sólo piezas completas» y «Redondear hacia arriba».
17. Para Masa/Volumen se conserva la política exacta no aplicable a conteo.
18. La versión siguiente continúa derivándose de la arquitectura existente y ahora es de sólo lectura.
19. Los estados se presentan como Borrador, Publicada y «Activa · Publicada».
20. Publicar, activar y editar conservan las reglas existentes.
21. Versiones publicadas siguen siendo históricas e inmutables; la UI ofrece «Crear nueva versión».
22. Rendimiento tiene ayuda contextual basada en el factor proporcional actual.
23. Política tiene ayuda contextual basada en IngredientCountPolicy.
24. Alternativas se presentan como grupos con opciones ordenadas.
25. «Nuevo grupo de alternativas» crea el grupo inicial.
26. «Agregar opción al grupo» opera sobre el grupo seleccionado y permanece deshabilitado sin selección.
27. Eliminar una opción renumera visualmente el orden restante; la persistencia conserva grupos/opciones/preferencia existentes.
28. Un grupo con menos de dos opciones no habilita Guardar.
29. La arquitectura actual puede representar el futuro caso levadura fresca/Tradi Pan mediante grupo, opciones, preferencia y cantidad propia; no se hardcodeó ninguna regla, cantidad o redondeo.
30. Preparaciones por pieza muestran preparación publicada, cantidad por pieza y unidad compatible.
31. Ingredientes directos se separan visualmente de preparaciones y alternativas.
32. Estados vacíos cubren recetas, versiones, búsqueda, ingredientes directos, alternativas, preparaciones y perfiles.
33. Quitar ingrediente/preparación/opción y agregar opción dependen de selección válida.
34. Crear receta/perfil y guardar borrador dependen de campos válidos.
35. Publicar/activar/editar dependen del estado y selección existentes.
36. ContextHelp se usa para Rendimiento, Política, Alternativas, Preparaciones y publicación.
37. Recetas no expone timestamps; por ello no hay zona horaria técnica visible que convertir.
38. NestedScrollBehavior sigue activo en RecipeView; no se cambió shell, F11 ni prioridad Esc/ComboBox.
39. Se validó el layout WPF en 1280×820 y 1920×1080 lógicos mediante pruebas cargadas.
40. Se conservaron permisos: Administrator configura, Manager consulta y Cashier no navega a Recetas.
41. Se conservaron recuperación, revisión optimista y versionado del servicio.
42. No se alteraron Inventario, Producción, caja, ventas ni impresión.
43. La base protegida quedó byte-idéntica.
44. La revisión usa build, launcher y DataDirectory propios.
45. FASE 1K continúa abierta; 1K.3D no se inició.

## Archivos

Producto:

- src/CasaMiga.Presentation/RecipeViewModel.cs
- src/CasaMiga.Presentation/RecipeView.xaml
- src/CasaMiga.Presentation/RecipeView.xaml.cs

Pruebas:

- tests/CasaMiga.PhotoTests/RecipeUsabilityPresentationTests.cs
- tests/CasaMiga.PhotoTests/ManualSmokeReconciliationTests.cs
- tests/CasaMiga.PhotoTests/UsabilityPresentationTests.cs

Documentación: README.md, docs/CURRENT_STATE.md, docs/NEXT_STEPS.md, este reporte y docs/evidence/phase1k3c-verification.md.

## Verificación

- Focal Recipe/persistencia: **109/109**, 0 fallidas, 0 omitidas.
- Focal WPF compartida: **38/38**, 0 fallidas, 0 omitidas.
- Nueva WPF específica de Recetas: **6/6**, 0 fallidas, 0 omitidas.
- Suite completa scripts/verify.ps1: **708/708**: 558 generales + 150 WPF/Presentación; 0 fallidas, 0 omitidas.
- Release: **0 errores, 0 advertencias**.
- Build manual Phase1K3C con metadata CasaMigaReviewBuild=1K.3C: 0 errores, 0 advertencias.
- Las pruebas usan bases temporales aisladas.
- El primer intento de testhost dentro del sandbox abortó antes de ejecutar pruebas por bloqueo de su canal de conexión. Todas las cifras anteriores proceden de repeticiones completas fuera del sandbox, terminadas correctamente.

## Datos protegidos e impresión

Base protegida: work/phase1k2c-r2-smoke-20260928/casamiga.db.

SHA-256 inicial y posterior: CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D.

Copia: work/phase1k3c-smoke-20261005. Se copiaron coherentemente 59 archivos; no se copiaron locks ni sidecars SQLite. Harina conserva 50 kg y Bulto 25 kg activo. La copia contiene cero formula, cero formula_version, cero production_profile y cero production_batch.

Impresión conservada byte por byte: SHA-256 88843EAF9F452FD94C7CDEA77A71657923DD3E123D082DB9EEEDEB9277464461; POS-80, 48 columnas, ibm850, tabla 2, corte Partial, openCashDrawer=false. Variante 04 permanece en el flujo existente. No se envió ningún trabajo.

## Entrega de revisión

- EXE: src/CasaMiga.Presentation/bin/Phase1K3C/CasaMiga.Presentation.exe
- EXE SHA-256: F15FE53EA51ED27EE6F1995CE7579228E975B26988AB13AAC352B49661602070
- DLL SHA-256: 4BD8B58190037E421E243EF4A1B2854643DB1E060F4FAFF5B42E4FEBA045E7A0
- Launcher: work/Abrir-Casa-Miga-1K3C.cmd
- DataDirectory: work/phase1k3c-smoke-20261005
- Título: Casa Miga POS · Caja · DESARROLLO · 1K.3C
- Tamaño de revisión: 1280×820; la ventana conserva fullscreen/F11 del shell aprobado.
- Credencial catalog-admin verificada offline contra la copia sin iniciar sesión ni renovar hash.

## Smoke manual seguro

No guardar durante esta revisión.

1. Entrar a Recetas y comprobar lista vacía y Ver detalle deshabilitado.
2. Abrir Nueva receta; revisar Código, Nombre y Crear receta deshabilitado hasta completar campos. Cancelar sin crear.
3. Revisar a futuro, con un fixture no comercial, búsqueda por Harina/MP-HAR-001, limpieza y sin resultados. La copia entregada no tiene recetas y no debe poblarse sólo para probar.
4. Comprobar dimensiones/unidades, ayudas, grupos/opciones, preparaciones y acciones inválidas.
5. Desplazarse hasta el final; probar ComboBox abierto + Esc, luego F11/Esc normal.

Commit autorizado: feat: improve recipe usability for phase 1K.3C. El SHA final, Git limpio y comprobación del proceso abierto se entregan en conversación para evitar referencia circular.
