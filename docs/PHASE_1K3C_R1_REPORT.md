# FASE 1K.3C-R1 — filtro de ingredientes y ayudas operativas

Fecha: 05/10/2026
Estado: **técnicamente completada; smoke manual R1 pendiente**
Modelo utilizado: Codex, GPT-6 (familia Sol)
HEAD inicial: `f1803659b3f93a100039aa166ee7e4903d74e399`
Git inicial: limpio

## Diagnóstico y solución

`IngredientEdit.AvailableItems` se sustituía por una nueva lista cada vez que terminaba el debounce del filtro. Cada `ComboBox` enlaza esa lista como `ItemsSource` y su objeto como `SelectedItem` en modo TwoWay. Aunque el código intentaba incluir el valor seleccionado en la lista nueva, WPF podía invalidarlo durante el reemplazo de `ItemsSource`, dejar la celda visualmente vacía y propagar temporalmente `null` a la fila editable.

El catálogo filtrado `IngredientMatches` continúa siendo la fuente de candidatos. Cada fila usa ahora una `ObservableCollection<RecipeItemChoice>` estable. `UpdateAvailableItems` agrega/mueve candidatos en sitio, conserva siempre primero el ingrediente ya asignado cuando queda fuera del filtro y sólo después retira los sobrantes. La identidad del `ItemsSource` no cambia y el seleccionado nunca desaparece durante la actualización. × Limpiar restaura candidatos; Cancelar conserva el flujo existente y reabrir vuelve a leer el borrador persistido.

La solución se limita a Presentation. No cambió IDs, igualdad, persistencia, contratos, versiones, alternativas ni políticas.

## Comportamiento comprobado

- Una prueba con `RecipeView` real materializa un borrador persistido con `SMOKE-MASA-001` 500 g, `SMOKE-VOL-001` 250 ml y `SMOKE-COUNT-001` 2 pieza / Sólo piezas completas.
- Con `SIN-RESULTADOS-999` aparece «No encontramos ingredientes con esa búsqueda.» y permanecen visibles los tres `SelectedItem` con sus IDs, cantidades, unidades y políticas.
- Con `SMOKE-MASA-001`, MASS queda como candidato y COUNT/VOLUME continúan visibles en sus filas.
- × Limpiar restaura los tres candidatos sin cambiar las filas.
- Cancelar y reabrir mantiene el borrador original.
- Buscar no crea filas. Agregar ingrediente sigue siendo explícito y permite MASS g/kg, VOLUME ml/L y COUNT pieza; COUNT conserva Sólo piezas completas y Redondear hacia arriba.

## Pulido UX y ayudas

El encabezado visible «Política para piezas» cambió a «Política de cantidad» en ingredientes de receta, alternativas e ingredientes directos de perfil. Valores, enum y reglas no cambiaron.

ContextHelp ahora admite hasta 520 DIP, se estira sólo hasta el ancho disponible, usa padding compacto, altura automática y `TextWrapping=Wrap`. Sigue plegable, no modal y cercano al signo `?`; Esc lo cierra. Las pruebas cubren ancho amplio 420–520, contenedor estrecho de 340, altura menor de 400 para ayuda media, texto visible, wrap, Esc y scroll compartido. La regresión de shell conserva F11.

Las ayudas usan español operativo y ejemplos genéricos:

- **Rendimiento:** qué produce la versión; cantidad, dimensión y unidad; uso proporcional; ejemplos sintéticos de 1 kg y 20 piezas.
- **Política de cantidad:** Masa/Volumen exactos; Piezas enteras o redondeo; 2.4 redondea a 3, mientras Sólo piezas completas rechaza un resultado fraccionario.
- **Ingredientes directos:** buscar, agregar fila, seleccionar, capturar cantidad/unidad y aplicar política sólo cuando corresponde; ejemplo sintético de 500 g.
- **Alternativas:** grupo, opciones, preferencia, cantidades distintas, mínimo dos; «Nuevo grupo» crea otro conjunto y «Agregar opción» añade al grupo seleccionado.
- **Preparaciones:** diferencia entre ingrediente directo y componente publicado reutilizable; cantidad/unidad por pieza; ejemplo sintético.
- **Versiones:** Borrador editable, Publicada inmutable y Activa vigente; los cambios se realizan mediante una versión nueva.

No se usaron productos, recetas, ingredientes ni cantidades comerciales reales.

## Archivos versionados modificados

- `src/CasaMiga.Presentation/RecipeViewModel.cs`
- `src/CasaMiga.Presentation/RecipeView.xaml`
- `src/CasaMiga.Presentation/ContextHelp.xaml`
- `tests/CasaMiga.PhotoTests/RecipeUsabilityPresentationTests.cs`
- `docs/CURRENT_STATE.md`
- `docs/NEXT_STEPS.md`
- `docs/PHASE_1K3C_R1_REPORT.md`

Domain, Application, Infrastructure, SQLite y migraciones permanecen intactos.

## Verificación

- Focal Recetas/reglas: **142/142**, 0 fallidas, 0 omitidas.
- Focal WPF de Recetas/Presentation: **41/41**, 0 fallidas, 0 omitidas.
- Suite completa `scripts/verify.ps1`: **711/711** = 558 generales + 153 WPF, 0 fallidas, 0 omitidas.
- Release: **0 errores, 0 advertencias**.
- Build identificado `Phase1K3C-R1`: **0 errores, 0 advertencias**.
- `git diff --check`: correcto.
- Schema: **16**; migración 017: **no existe**.

Los primeros intentos de testhost dentro del sandbox no conectaron al canal del runner y fueron anulados. Todas las cifras acreditadas corresponden a repeticiones finalizadas fuera del sandbox.

## Datos, impresión y build de revisión

- Base protegida: `work/phase1k2c-r2-smoke-20260928/casamiga.db`.
- SHA-256 inicial y previo a entrega: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
- Copia nueva: `work/phase1k3c-r1-smoke-20261005`.
- Validación de copia: integridad `ok`, 0 claves foráneas inválidas, schema 16, 2 recetas sintéticas, 1 borrador, 2 publicadas, 2 activas, alternativas, preparación y perfil sintético; 0 balances y 0 movimientos para artículos `SMOKE-*`.
- Impresión conservada: POS-80, 48 columnas, ibm850, tabla 2, Partial, cajón desactivado; `printing.json` SHA-256 `88843EAF9F452FD94C7CDEA77A71657923DD3E123D082DB9EEEDEB9277464461`.
- No se envió ningún trabajo de impresión.
- EXE: `src/CasaMiga.Presentation/bin/Phase1K3C-R1/CasaMiga.Presentation.exe`, SHA-256 `A4944C3F603E2A81FEA54511D14F8FF2C73BD8EF7531805DFF03D8D611D12728`.
- DLL: SHA-256 `44288D6C5E8C82AD671B2D8893369CDF5CD466ECD6ED8A5A4C803D45A3BB928C`.
- Launcher: `work/Abrir-Casa-Miga-1K3C-R1.cmd`.
- DataDirectory: `work/phase1k3c-r1-smoke-20261005`.
- Metadata: `CasaMiga.ReviewBuild=1K.3C-R1`.
- Título esperado y verificado tras apertura post-commit: `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R1`.
- El launcher limpia `CASAMIGA_DATA_DIR` heredado y apunta exactamente al EXE R1. No depende del acceso anclado.

## Checklist manual R1

1. Iniciar sesión y abrir Recetas → `SMOKE-REC-001` → Versión 2 → Editar borrador.
2. Confirmar COUNT, VOLUME y MASS visibles.
3. Buscar `SIN-RESULTADOS-999`; confirmar mensaje y las tres filas intactas sin limpiar.
4. Buscar `SMOKE-MASA-001`; confirmar candidato MASS y filas COUNT/VOLUME visibles.
5. Pulsar × Limpiar; confirmar todo intacto y encabezado «Política de cantidad».
6. Abrir ayudas de Rendimiento, Política de cantidad, Ingredientes directos y Alternativas; revisar ancho, instrucciones y ejemplo.
7. En Perfiles, abrir ayuda de Preparaciones.
8. Cancelar, reabrir Versión 2 y confirmar contenido original.
9. Continuar el smoke pendiente de Perfiles de producto.

## Límites y pendientes

El smoke humano R1 sigue pendiente; esta entrega no cierra 1K.3C ni FASE 1K. No se inició 1K.3D ni Pedidos. No se cargaron recetas/datos reales, no hubo producción, ventas, caja, movimientos de inventario, migración 017 ni impresión. El commit final y la observación de la instancia post-commit se registran en el reporte de conversación para evitar una referencia circular.
