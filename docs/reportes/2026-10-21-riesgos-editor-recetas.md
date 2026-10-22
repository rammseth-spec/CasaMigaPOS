# Reporte de bloque — dos riesgos del editor de recetas

Fecha: 21/10/2026. Sin commit al principio, por instrucción del usuario; el 22/10 el usuario pidió subirlo a `main`.

## 1. Resultado

**Terminado en el código y subido a `main` (22/10, a pedido del usuario); sin instalar en la caja real.** Las dos protecciones funcionan y tienen sus pruebas automáticas. La verificación completa pasó (repetida el 22/10 con el máximo): 777 pruebas correctas (1 omitida, como siempre) y 245/245 de pantallas.

## 2. Qué cambió y qué puedes hacer ahora

- **Aviso de rendimiento sospechoso.** Al guardar un borrador, si el rendimiento cambia más de 10 veces respecto al anterior, el programa pregunta antes de guardar.
  - Muestra el antes y el ahora en la misma medida (g, ml o piezas) y advierte del posible error de unidad.
  - **Cancelar** no guarda nada; **Aceptar** guarda exactamente lo escrito. Nunca corrige solo.
- **Máximo de Casa Miga** (tu decisión del 22/10): si una receta rinde más de **20 kg**, **50 L** o **500 piezas**, el mismo aviso pregunta antes de guardar.
  - Avisa también en la primera versión de una receta, que no tiene con qué compararse.
  - Como pediste, avisa y deja decidir: no bloquea.
- **Marca «Por medir».** Es una casilla nueva en el editor de cada versión.
  - Mientras esté marcada, la versión no se puede publicar, y el mensaje explica por qué. La regla vive también en las reglas de la receta y en la base de datos, no sólo en la pantalla.
  - Quitar la marca no publica nada.
  - Se ve en la lista de versiones («Borrador · Por medir») y en la lista de recetas (columna «Por medir»).

## 3. Dónde está

| Qué | Dónde |
|---|---|
| Código y pruebas | `C:\Users\ramms\Documents\ChatGPT\CasaMigaPOS-main`, rama `main`, subido a GitHub el 22/10 |
| Caja real | **Sin cambios.** Sigue con el programa `4e8ec88` y la base en versión 30 |
| Copia de revisión (`recetas-copia-20261020`) | Sin cambios; no se abrió ni se actualizó |
| **Copia de PRUEBA para ver las dos protecciones** | Programa `C:\Users\ramms\CasaMiga-Revision\programa-riesgos-recetas`, compilado del código sin commit, con el título «DESARROLLO · RIESGOS-f00baa2-local». Datos `C:\Users\ramms\CasaMiga-Revision\riesgos-recetas-prueba-20261021`, una base nueva en versión 32 sólo con datos inventados: PRUEBA-MASA en borrador de 1,800 g y PRUEBA-RELLENO en borrador «Por medir». Lanzador `C:\Users\ramms\CasaMiga-Revision\Abrir-Casa-Miga-riesgos-recetas.vbs`. La credencial no está en el repositorio |
| **Programa de prueba con el máximo (22/10)** | `C:\Users\ramms\CasaMiga-Revision\programa-riesgos-recetas-limite`, con el título «DESARROLLO · RIESGOS-LIMITE-f00baa2-local». Usa los mismos datos de prueba. Lanzador `C:\Users\ramms\CasaMiga-Revision\Abrir-Casa-Miga-riesgos-recetas-limite.vbs`. Sólo se abre después de cerrar la ventana anterior, porque comparten los datos |

## 4. Qué verifiqué y qué no

- **Con datos de prueba en bases temporales:** 16 pruebas nuevas del programa, 1 de la web y las anteriores de recetas (punto 6).
- **No usé datos reales.** No abrí la caja real, la copia de revisión ni ninguna base comercial.
- **No probé con la pantalla real**, con clics: la ventana de confirmación y la casilla sólo se probaron con las pruebas automáticas. Los pasos para probarlo a mano están en el punto 10 del informe técnico.

## 5. Recetas, producción e inventario

Sin cambios en ninguna base real: 10 preparaciones en borrador en la caja real, 0 publicadas, 0 recetas de pan, 0 producciones, 0 existencias y el descuento por venta apagado.

Cuando se instale la versión 32, esas 10 preparaciones quedarán marcadas «Por medir». Ninguna se dará por verificada.

## 6. Avance frente al plan

Cierra los dos riesgos que encontró la auditoría del 21/10 (`C:\Users\ramms\CasaMiga-Revision\reportes\2026-10-21-auditoria-recetario-produccion.md`): la unidad del rendimiento al editar y la falta de un estado «por medir». El resto del plan sigue igual (`PLAN_SEGUIMIENTO.md`).

## 7. Qué necesito de ti

Tu resultado de las dos pruebas en pantalla (A, B, C o D). Sin él no se dan por aprobadas.

## 8. Siguiente bloque

- Preparar una copia de revisión **con datos de prueba** para que veas las dos protecciones en pantalla. Abrir la copia con este programa la subiría a la versión 32.
- Antes de instalar en la caja real, repetir la prueba de actualización sobre una copia nueva con la versión 32 (`ACTUALIZAR_CAJA.md`).

## 9. Respaldo técnico

- **Commit:** en `main` sobre `f00baa2`, subido a GitHub el 22/10 a pedido del usuario; 23 archivos cambiados o nuevos. La prueba manual en pantalla sigue pendiente.
- **Pruebas:** `verify.ps1` completo del 22/10 con 777/778 (1 omitida) y 245/245; la nube da 81/82, con 1 falla ajena (ver abajo).
- **Este reporte:** `docs/reportes/2026-10-21-riesgos-editor-recetas.md`.

---

# Informe técnico (10 puntos pedidos)

## 1. Repositorio, rama y commit inicial

- Repositorio: `C:\Users\ramms\Documents\ChatGPT\CasaMigaPOS-main`, rama `main`, árbol limpio al empezar.
- Commit local inicial: `bf66f30`. `origin/main` tenía 2 commits más, sólo de la web: `d6ac7cd` y `f00baa2`, ya en GitHub.
  - Se adelantó la rama local con `git merge --ff-only`, sin cambiar de rama ni tocar GitHub.
  - Base de trabajo: **`f00baa2`**, con migraciones hasta la `031_formula_preparations.sql`.

## 2. Archivos y por qué

| Archivo | Por qué |
|---|---|
| `migrations/032_formula_pending_measure.sql` (nuevo) | Columna `pending_measure` (0/1); estado inicial; trigger que impide publicar una versión marcada |
| `src/CasaMiga.Domain/ProductionFormula.cs` | `FormulaVersion.PendingMeasure` (parámetro opcional); publicar exige que no esté marcada |
| `src/CasaMiga.Domain/ProductionScaling.cs` | `ProductionError.PendingMeasure`, agregado al final del enum |
| `src/CasaMiga.Application/RecipeProblems.cs` | Mensaje claro de «Por medir» |
| `src/CasaMiga.Application/RecipeYieldCheck.cs` (nuevo) | Regla de rendimiento sospechoso, sin pantalla y reutilizable |
| `src/CasaMiga.Application/CloudCommandService.cs` | Un borrador desde la web conserva la marca si no la trae; acepta `pendingMeasure` booleano |
| `src/CasaMiga.Infrastructure/SqliteRecipeStore.cs` | Lee y guarda la marca (con protección para bases anteriores a 032); error claro al cambiar el tipo de rendimiento de una preparación usada |
| `src/CasaMiga.Presentation/RecipeViewModel.cs` | Comparación antes de guardar; casilla «Por medir»; estado en listas |
| `src/CasaMiga.Presentation/RecipeView.xaml` y `.xaml.cs` | Casilla con ayuda, columna «Por medir» y ventana de confirmación (Aceptar/Cancelar, Cancelar por omisión) |
| `tools/recorrido-receta/Program.cs` | Al regrabar el borrador del polvorón conserva la marca, para no quitarla en silencio |
| `cloud/lib/web.ts`, `cloud/public/js/recipes.js` | La web muestra «Borrador · Por medir» |
| `cloud/lib/commands.ts` | La web avisa y no manda publicar una versión marcada |
| `tests/CasaMiga.Tests/RecipeEditorSafetyTests.cs` (nuevo), `cloud/test/recipes.test.ts` | Pruebas nuevas |
| `tests/CasaMiga.Tests/ZeroOutputMigrationTests.cs` | La comparación histórica compara las columnas anteriores de `formula_version`, revisión incluida, sin la columna nueva |
| `docs/ACTUALIZAR_CAJA.md`, `GUIA_RECORRIDO_POLVORON.md`, `PLAN_SEGUIMIENTO.md`, `RECETAS_ESTADO.md`, `CURRENT_STATE.md` | Documentación |

## 3. Validación del rendimiento (mejora A)

- **Dónde compara:** en `RecipeViewModel.SaveFormulaVersionAsync`, con la regla `RecipeYieldCheck.Warning(anterior, nuevo)`.
  - **Rendimiento anterior:** el guardado del borrador que se edita. En una versión nueva, el de la última versión de la receta.
  - **Medida común:** se compara en unidades atómicas (mg, µL o piezas), así que kg y g se comparan bien.
  - **Umbral:** pide confirmación si `nuevo > anterior × 10` o `anterior > nuevo × 10`. Exactamente 10 veces no avisa.
- **Mensaje:** muestra «Antes: 1,800 g / Ahora: 2,000,000 g (unas 1,111 veces más)» y el aviso de posible error de unidad.
  - **Cancelar** (la opción por omisión) no guarda y deja el editor abierto.
  - **Aceptar** guarda el valor tal cual; nunca lo corrige.
  - Sin quién responda (`ConfirmYieldChange` vacío), no guarda.
- **Peso, volumen y piezas nunca se comparan por cantidad.** Si en un borrador cambia el tipo, se pide confirmación con un texto propio.
  - Si esa preparación ya la usa otra receta o un perfil, el guardado se rechaza con un mensaje claro en vez del error técnico de la base.
- **Valores no válidos:** texto, «1,800» con coma, vacío, 0 o negativos.
  - Siguen la validación que ya existía: el botón Guardar queda apagado y, si se intenta, sale «Escribe una cantidad mayor que cero con punto decimal.» sin guardar.
  - Se rechazan antes de cualquier comparación.
- **Sin rendimiento anterior válido** (primera versión de una receta, o un valor anterior no positivo, que la base no permite): no hay con qué comparar. Sólo se revisa el máximo, y el valor se valida igual.
- **Máximo de Casa Miga** (decisión del dueño, 22/10/2026): `RecipeYieldCheck.Maximum` = 20 kg, 50 L o 500 piezas.
  - Por encima de eso se pide confirmación, con o sin versión anterior; exactamente en el máximo no avisa.
  - Si coincide con el cambio de más de 10 veces, sale una sola pregunta con las dos razones.
  - El dueño eligió «avisar y dejar decidir», no bloquear.
  - Sólo aplica al rendimiento de la receta, no a cada ingrediente.
- **Alcance:** es una confirmación del editor de la caja.
  - La aplicación no puede exigirla por su cuenta, porque no sabe si una persona confirmó.
  - La regla sí es de la capa de aplicación (`RecipeYieldCheck`) y puede reutilizarse en la web.

## 4. «Por medir» (mejora B)

- **Persistente** por versión: `formula_version.pending_measure`.
- **Se marca y desmarca** con la casilla del editor al guardar el borrador. Se ve en el editor, en la lista de versiones y en la lista de recetas; en la web, en el estado de la versión.
- **Impide publicar en 4 capas**, la última con mensaje explicado:
  - dominio: `FormulaVersion.Publish` lanza `ProductionError.PendingMeasure`;
  - almacén: `PublishFormula` valida con el dominio;
  - base de datos: trigger `formula_version_publish_pending_measure`;
  - aplicación: `RecipeService` y los comandos de la web reciben el error y lo traducen con `RecipeProblems`.
- **Desmarcar no publica:** sólo cambia la marca del borrador; publicar sigue siendo un paso aparte.
- **Versiones publicadas:** siguen inmutables (el trigger `formula_version_immutable` impide cambiar la marca) y nunca están «Por medir».
- **Desde la web:** un borrador que no trae la marca conserva la que tenía, y la web avisa antes de mandar una publicación.
- **Decisión de estado inicial:**
  - los **borradores existentes quedan «Por medir» (1)**, para no darlos por verificados;
  - las versiones publicadas quedan en 0 (son inmutables);
  - una versión nueva empieza sin marca y se marca explícitamente.

  No hizo falta una decisión de negocio nueva. Las instrucciones pedían un estado seguro y no dar por verificadas las recetas existentes, y eso es lo que aplica.

## 5. Migración

- `032_formula_pending_measure.sql`, nueva; no se modificó ninguna migración aplicada.
- **Ejecutada sólo en bases temporales de las pruebas.** No se ejecutó en la caja real ni en la copia de revisión, ni en ninguna base comercial.

## 6. Pruebas añadidas y resultados reales

`tests/CasaMiga.Tests/RecipeEditorSafetyTests.cs`, 17 pruebas, todas correctas (Debug). El 22/10 se agregó `AYieldAboveTheCasaMigaMaximumAsksEvenOnTheFirstVersion`:
- exactamente 20 kg, 50 L y 500 piezas no avisan; un poco más sí;
- las dos razones salen en una sola pregunta;
- en la primera versión, 25 kg avisa: cancelar no crea nada y confirmar guarda 25 kg.

**Rendimiento:**
1. `AYieldChangeOfTenTimesOrLessSavesWithoutAsking`: 1,800 g → 2,000 g sin aviso; exactamente 10× sin aviso.
2. `AYieldChangeOfMoreThanTenTimesAsksAndCancellingSavesNothing`: 1.8 kg → «2000» con kg avisa; cancelar no guarda ni cambia la revisión.
3. `ConfirmingASuspiciousYieldSavesExactlyWhatWasTyped`: confirmar guarda 2,000 kg tal cual.
4. `GramsAndKilogramsAreComparedInTheSameUnit`:
   - 1,800 g → 2 kg sin aviso;
   - 2,000 g → 2,000,000 g avisa;
   - una versión nueva se compara con la anterior.
5. `MassVolumeAndPiecesAreNeverComparedByAmount`: peso, volumen y piezas no se comparan por cantidad; cambiar el tipo pide confirmación; error claro si la preparación ya se usa.
6. `InvalidOrNonPositiveYieldsAreRefusedWithAMessageAndSaveNothing`: «abc», «1,800», vacío, 0 y −5.

**«Por medir»:**
7. `PorMedirIsSavedLoadedAndShownInTheEditorAndTheLists`
8. `PorMedirBlocksPublishingFromTheScreenWithTheReason`
9. `PorMedirIsEnforcedByTheDomainTheServiceAndTheDatabase`
10. `UnmarkingPorMedirAllowsPublishingButNeverPublishes`
11. `PublishedVersionsStayImmutableAndNeverPorMedir`
12. `NestedPreparationsKeepWorkingWithPorMedir`
13. `ExistingDraftsStartPorMedirAndPublishedVersionsStayAsTheyWere`: base 31 → 32. Cantidades, unidades y revisiones sin cambio; la marca viaja a la nube.
14. `TheWebKeepsTheMarkWhenItDoesNotSendItAndCannotPublishAMarkedVersion`

**Otras:**
- La prueba del recorrido del polvorón (`TheFirstRecipeRouteWorksFromTheNormalScreens`) sigue pasando sin cambios: 1,800 g → 2,000 g no pide confirmación.
- Web: `cloud/test/recipes.test.ts`, prueba nueva «a version marked «Por medir»…» (3/3 en ese archivo).
  - La nube completa da 81 de 82: falla `actualizar.test.ts › publishing writes version.json…`.
  - Viene del commit `f00baa2` («Actualizar app»), ajeno a este trabajo. Usa `new URL(...).pathname` como ruta de archivo, que en Windows no funciona. No se corrigió por estar fuera del alcance.
- Filtro de recetas, migraciones y comandos de la web (Debug): 140/140, después de ajustar la comparación histórica.
  - Antes del ajuste fallaban 7 casos de `CorrectiveUpgradePreservesHistoryAndRollback` y `PermissionFifteenUpgradeAndRollbackPreserveAllHistory`, porque la columna nueva y la marca inicial cambian `formula_version`.

## 7. Resultado de `verify.ps1`

Ejecutado completo el 21/10/2026, con salida 0. Bitácora: `C:\Users\ramms\CasaMiga-Revision\verify-riesgos-recetas.log`.

- `restore --locked-mode`: correcto.
- Compilación Release, con advertencias tratadas como errores: 0 errores.
- `CasaMiga.Tests`: **777 correctas, 0 con error, 1 omitida** (778), en la ejecución del 22/10 con el máximo (bitácora `C:\Users\ramms\CasaMiga-Revision\verify-riesgos-limite.log`). La del 21/10 dio 776/777. La omitida es la de volumen de un año, igual que antes.
- `CasaMiga.PhotoTests` (WPF): **245/245**.

`verify.ps1` no incluye la nube. Ver el punto 6: 81/82, con la falla ajena a este trabajo.

## 8. Datos comerciales

No se abrieron ni modificaron:
- la caja real: `C:\Users\ramms\CasaMiga-Revision\local-main-c2fb393\caja` y el programa `C:\Users\ramms\CasaMiga-Ventas\programa`;
- la copia de revisión `recetas-copia-20261020`;
- ninguna receta, inventario, movimiento, permiso, credencial ni la configuración del descuento por venta.

Tampoco se publicó ninguna receta real. Todas las pruebas usan bases temporales y datos sintéticos («PRUEBA-…»).

## 9. Riesgos pendientes

1. **El editor de recetas de la web no tiene el aviso de rendimiento ni la casilla «Por medir»**: sólo muestra la marca y no deja publicar una versión marcada. La marca se cambia en la caja.
2. **Borradores nuevos que llegan desde la web o con el cargador del recetario** (`cloud/scripts`) entran **sin** marca. Hay que decidir si el cargador debe mandar `pendingMeasure: true` para los rendimientos reportados.
3. **Instalar la 32 en la caja real** marca las 10 preparaciones como «Por medir». Hay que quitar la marca una por una cuando se confirmen sus datos. Además, `ACTUALIZAR_CAJA.md` se probó sólo hasta la 31 y hay que repetir la prueba sobre una copia.
4. **Una versión anterior del programa no abre una base en 32**, igual que pasaba con la 31.
5. **La herramienta `recorrido-receta` conserva la marca.** Si el borrador del polvorón está marcado, publicar desde la herramienta fallará con el mensaje de «Por medir» hasta quitarla en la pantalla.
6. **El aviso compara con la versión anterior más reciente**, no con la publicada activa. Si la última versión ya tenía un error, comparará contra ese error.
7. **Prueba de la nube** `actualizar.test.ts`: falla en Windows desde `f00baa2` y no depende de este trabajo.

## 10. Validación manual en la pantalla

En una copia con **datos de prueba**, nunca en la caja real:

1. Abrir **Recetas** → **Nueva receta** → crear «PRUEBA-MASA».
2. **Crear nueva versión** con rendimiento **1800** g y un ingrediente → **Guardar borrador**. Debe guardar sin preguntar.
3. Seleccionar la versión → **Editar borrador**. El rendimiento aparece como **1.8 kg**.
4. Escribir **2000** sin cambiar la unidad (kg) → **Guardar borrador**. Debe aparecer «Revisa el rendimiento» con «Antes: 1,800 g / Ahora: 2,000,000 g».
   - **Cancelar**: sale «No se guardó…» y el borrador sigue en 1.8 kg.
5. Cambiar la unidad a **g** (2000 g) → **Guardar borrador**. Guarda sin preguntar.
6. Editar de nuevo, marcar **Por medir** → **Guardar borrador**.
   - La lista de versiones dice «Borrador · Por medir».
   - En la lista de recetas, la columna «Por medir» dice «Sí».
7. **Publicar versión**: sale el mensaje «No se puede publicar: esta versión está marcada «Por medir»…» y sigue en borrador.
8. Editar, quitar **Por medir** → **Guardar borrador**. Sigue «Borrador», sin publicarse.
9. **Publicar versión**: ahora publica.
10. Escribir «abc», «0» o «-5» en el rendimiento: el botón Guardar se apaga.
