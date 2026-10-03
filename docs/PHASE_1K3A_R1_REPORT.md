# CASA MIGA POS — FASE 1K.3A-R1

03/10/2026. **Corrección técnica completada; smoke manual R1 pendiente.** No equivale a aprobación del usuario ni inicia 1K.3B.

## Baseline y diagnóstico

Modelo: Codex basado en GPT-6. HEAD inicial `6bf3b5256290bc053003b8b5f67e2a4a6d1148bc` (`feat: improve global usability for phase 1K.3A`), estado limpio comprobado antes de las primeras ediciones. Identidad local conservada: Rammseth / rammstudioworkshop@gmail.com. Commit único previsto tras validación: `fix: reconcile phase 1K.3A manual smoke`. El SHA del checkpoint y la comprobación de apertura posterior se entregan en la conversación y en el reporte final local para evitar autorreferencia en este commit.

Se inspeccionaron launcher, FoundationOptions, App, MainWindow, vistas, ViewModels, bindings, handlers, binarios y pruebas anteriores antes de decidir la corrección.

- El launcher anterior ejecuta `work/.dotnet/dotnet.exe` con `src/CasaMiga.Presentation/bin/Phase1K3A/CasaMiga.Presentation.dll`. No apunta a R2 ni Debug. Los hashes EXE/DLL coinciden con los documentados al entregar 1K.3A. No hay evidencia de copia parcial o DLL sustituida en esa carpeta.
- La metadata informativa de aquel ensamblado contiene el padre `6942c061bf6a68a65d42f57db5b00fbe567d1548`, porque se compiló antes del commit 6bf3b52. No se presenta esa metadata como prueba de que el archivo lleve el SHA posterior. La correspondencia se verifica por hashes y comportamiento del artefacto conservado.
- Sí existe un problema de configuración de desarrollo: el launcher anterior define CASAMIGA_DATA_DIR. FoundationOptions.Load omite development.settings.json si esa variable existe; por tanto IsDevelopment queda false y no se aplica la identificación de desarrollo prevista. Se corrige exclusivamente en el launcher nuevo, sin cambiar Infrastructure.
- **Esc:** PreviewKeyDown de MainWindow y de Inventario consumía Esc antes del manejo nativo del ComboBox. Según el estado, salía de fullscreen o abandonaba el editor. Es un defecto comprobado de prioridad del evento en Presentation.
- **Hallazgos 2–7:** las vistas actuales ya están conectadas a sus ViewModels correctos. Usan Click + bindings IsEnabled + notificación de propiedades; no usan ICommand en esas acciones. No hay un RaiseCanExecuteChanged faltante, binding roto ni plantilla que explique los resultados descritos en el artefacto 1K.3A conservado.
- Las pruebas anteriores sí materializaban vistas, pero no cubrían toda la ventana final, el texto ModeLabel, los tres textos del popup, Agregar producto ni la prioridad de Esc. Se agregaron comprobaciones en MainWindow real, sin atribuirles retroactivamente esa cobertura.

Comparación experimental, usando copias de binarios sin modificar las entregas previas:

| Artefacto | Comprobaciones de vistas finales | Comprobaciones de Esc |
|---|---|---|
| Phase1K3A conservado | 3/3 pasan (hallazgos 2–7) | 0/3 pasan; reproducen el defecto |
| R2 conservado | 0/3 pasan; faltan textos/selector/estado vacío de 1K.3A | No repetidas |
| R1 corregido | 3/3 pasan | 3/3 pasan |

Los fallos contra artefactos anteriores son pruebas diagnósticas esperadas, no fallos de R1. No se conoce con certeza qué proceso/atajo se utilizó en el smoke histórico: no se afirma que el usuario abriera R2. La entrega nueva elimina esa ambigüedad con carpeta, launcher, título y hashes explícitos.

## Estado individual

| Hallazgo | Resultado técnico R1 |
|---|---|
| 1. Esc con ComboBox abierto | Corregido mediante PopupEscape antes de fullscreen/navegación. Primer Esc cierra el desplegable; mantiene editor, selección y fullscreen. Después se conservan Esc normal y F11. Probado también desde ComboBoxItem. |
| 2. Texto de Recetas | Verificado en MainWindow: «Puedes crear y editar recetas y perfiles». No se duplicó un cambio ya presente. |
| 3. Recetas vacías | Texto «No hay recetas registradas.» visible y medido en la vista final. |
| 4. Ver detalle | Button.IsEnabled=false sin selección; Nueva receta sigue habilitado y la validación defensiva interna permanece. |
| 5. Dimensiones | Popup WPF abierto muestra Masa / Volumen / Piezas. SelectedValue y modelo conservan InventoryDimension. |
| 6. Nueva producción | Sin perfiles: Agregar, Quitar, Calcular y Crear deshabilitados; Cancelar habilitado. Seleccionar la única fila no habilita Quitar; cantidades inválidas o cantidad válida sin perfil no habilitan el plan. |
| 7. Producción vacía | «No hay perfiles productivos activos.» visible en la vista final, sin MessageBox. |

## Archivos y validación

Presentation: App.xaml.cs, CasaMiga.Presentation.csproj, DevelopmentBuildIdentity.cs, InventoryView.xaml.cs, MainWindow.xaml.cs y PopupEscape.cs. Pruebas: DevelopmentBuildIdentityTests.cs, ManualSmokeReconciliationTests.cs y PopupEscapePresentationTests.cs. Documentación: AGENTS.md, README.md, CURRENT_STATE.md, NEXT_STEPS.md, este reporte, review-builds.md y evidencia phase1k3a-r1.

Las siete pruebas nuevas incluyen seis WPF sobre ventanas cargadas, controles materializados y bindings reales, más aislamiento de identidad comercial/desarrollo. Los datos son sintéticos y temporales. Se reutiliza además la regresión ejecutada de scroll anidado, rueda en ComboBox cerrado y detalle de Inventario.

Focales: **18/18**, 0 fallidas, 0 omitidas. Filtro: PopupEscapePresentationTests, ManualSmokeReconciliationTests, DevelopmentBuildIdentityTests, UsabilityPresentationTests e InventoryPresentationTests. Evidencia local: `TestResults/phase1k3a-r1-focal.trx` y `work/phase1k3a-r1-focal.log`.

Suite completa mediante scripts/verify.ps1: **679/679** (556 generales + 123 fotos/Presentation), **0 fallidas, 0 omitidas**. Release: **0 errores, 0 advertencias**. Evidencia en `evidence/phase1k3a-r1-verification.md`. Comando:

```powershell
$env:CasaMigaReviewBuild='1K.3A-R1'
.\scripts\verify.ps1 -Dotnet (Resolve-Path .\work\.dotnet\dotnet.exe) -OutputPath bin/Phase1K3A-R1/
```

`git diff --check` correcto, sin errores de whitespace. Revisión independiente sin hallazgos bloqueantes. Después de las pruebas sólo se actualizaron documentación/evidencia; los hashes de código y pruebas se volvieron a contrastar con el manifiesto antes del commit.

## Entrega inequívoca

Raíz local: `C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1`.

- EXE: `C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\src\CasaMiga.Presentation\bin\Phase1K3A-R1\CasaMiga.Presentation.exe`.
- Launcher: `C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\work\Abrir-Casa-Miga-1K3A-R1.cmd`.
- DataDirectory: `C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\work\phase1k3a-r1-smoke-20261003`.
- Título de desarrollo: `Casa Miga POS · Caja · DESARROLLO · 1K.3A-R1`.

Contenido del launcher, verificado:

```bat
@echo off
setlocal
set "CASAMIGA_DATA_DIR="
set "DOTNET_ROOT=%~dp0.dotnet"
set "DOTNET_ROOT_X64=%~dp0.dotnet"
start "" "%~dp0..\src\CasaMiga.Presentation\bin\Phase1K3A-R1\CasaMiga.Presentation.exe"
endlocal
```

development.settings.json local junto al EXE configura IsDevelopment=true, PreviewWidth=1280, PreviewHeight=820 y el DataDirectory absoluto indicado. No hereda la variable de datos, no cambia el entorno global y no apunta a R2, Phase1K3A anterior ni Debug. Todos estos archivos locales/builds siguen ignorados por Git.

La identidad de revisión es metadata opcional de MSBuild, mostrada sólo si IsDevelopment=true; no cambia la identidad comercial. Se documentó la regla permanente de verificar y abrir automáticamente el launcher exacto al finalizar y dejar la aplicación abierta en acceso, sin depender del anclado de Windows ni automatizar login. La comprobación efectiva de apertura se registra después del commit en el reporte final local/conversación.

## Protección de datos

Base protegida: `C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\work\phase1k2c-r2-smoke-20260928\casamiga.db`.

SHA-256 inicial: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.

SHA-256 al terminar correcciones y pruebas, antes del checkpoint: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. Iguales exactamente; cero mutaciones en la base original.

Para la apertura final se preparó una copia aislada coherente de esa base, terminal, printing.json y product-images, sin copiar locks. La base copiada coincide byte por byte antes del primer arranque. El arranque normal puede inicializar runtime/terminal/respaldo en la copia: por eso no se abre contra la base original protegida. La igualdad final del original se comprueba antes del checkpoint y otra vez tras abrir R1.

Harina conserva 50 kg; HAR-BULTO-25KG, 25 kg, activa. Schema 16, migración 016 intacta, sin 017. No se modifican Domain/Application/Infrastructure, SQLite ni migraciones. Se conserva POS-80, 48 columnas, ibm850/tabla 2, corte Partial y cajón false; Variante 04 integrada permanece intacta. No se envía ningún trabajo de impresión.

No ventas, pagos, caja, entradas, mermas, conteos, asociaciones, recetas reales, perfiles reales ni producciones reales. No se vuelve a registrar Saldo inicial. Los fixtures de regresión son temporales y separados. No push, remoto nuevo ni cambio de identidad Git.

## Checklist manual mínimo pendiente

1. Usar la instancia abierta cuyo título indica DESARROLLO · 1K.3A-R1; iniciar sesión manualmente con las credenciales entregadas. No abrir el acceso anclado.
2. Inventario → Harina → Editar → abrir Tipo → Esc: sólo cierra el desplegable; editor y selección permanecen. Con fullscreen, primer Esc tampoco debe salir de él. Después comprobar salida normal de fullscreen y F11. Volver sin guardar.
3. Recetas: comprobar nuevo texto, estado vacío y Ver detalle deshabilitado sin selección.
4. Nueva receta → Dimensión: comprobar Masa, Volumen y Piezas en el desplegable. Cancelar sin guardar.
5. Producción → Nueva producción: comprobar explicación sin perfiles y Agregar/Quitar/Calcular/Crear deshabilitados. Seleccionar la única fila no debe habilitar Quitar. Cancelar.

Pendientes: confirmación humana sobre esta instancia exacta y dispositivos físicos. La comparación histórica no permite reconstruir qué aplicación estaba abierta durante el smoke anterior. FASE 1K sigue abierta. No iniciar 1K.3B hasta nueva autorización.
