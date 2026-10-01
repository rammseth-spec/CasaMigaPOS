# Verificación de corrección Inventario — 01/10/2026

Baseline: `353c7e271dfbdfe66b2e2dc8bc6c28e46914a971`.

## Reproducción previa

Tres casos WPF fallaron antes de cambiar el binding. TRX locales: `tests/CasaMiga.PhotoTests/TestResults/inventory-wpf-baseline.trx` y `inventory-wpf-save-baseline.trx`.

El probe sobre `work/inventory-crash-20261001/casamiga.db` produjo la misma excepción:

```text
System.InvalidOperationException: Un enlace TwoWay u OneWayToSource no puede funcionar en la propiedad de sólo lectura "IsActive" de tipo "CasaMiga.Presentation.InventoryUnitView".
MS.Internal.Data.PropertyPathWorker.CheckReadOnly
MS.Internal.Data.PropertyPathWorker.ReplaceItem
MS.Internal.Data.PropertyPathWorker.UpdateSourceValueState
MS.Internal.Data.ClrBindingWorker.AttachDataItem
System.Windows.Data.BindingExpression.Activate
System.Windows.Data.BindingExpression.AttachToContext
MS.Internal.Data.DataBindEngine.Task.Run
MS.Internal.Data.DataBindEngine.Run
MS.Internal.Data.DataBindEngine.OnLayoutUpdated
System.Windows.ContextLayoutManager.fireLayoutUpdateEvent
System.Windows.ContextLayoutManager.UpdateLayout
InventoryDetailProbe: layout de InventoryView
```

## Focal después de corregir

- `InventoryPresentationTests`: **3/3**, cero errores/omitidas; `TestResults/inventory-wpf-fixed.trx`.
- `InventoryViewModelTests`: **35/35**, cero errores/omitidas; `TestResults/inventory-vm-fixed.trx`.
- El primer intento de las nuevas pruebas de recuperación tenía dos consultas de auditoría con columna incorrecta (`event_type`); se corrigieron a `action` y se ejecutó de nuevo toda la focal. No fue un fallo de producto.

Probe con copia actual, salida obtenida después del fix:

```text
Integrity and foreign keys: OK (isolated copy).
Restart=0 Open=0..2: HAR-BULTO-25KG, 25 kg, Checked=true, SIN INICIALIZAR, Links=0, Movements=0
Restart=1 Open=0..2: HAR-BULTO-25KG, 25 kg, Checked=true, SIN INICIALIZAR, Links=0, Movements=0
```

`Restart` en esta salida representa reconstrucción de servicios/ViewModel. El comando también se ejecutó en un segundo proceso independiente: otras seis aperturas correctas y validación de integridad/relaciones OK. No se ejecuta FoundationRuntime ni impresión; el login auditado pertenece exclusivamente a la copia aislada.

La base de revisión original mantuvo SHA-256 `BC58A1D3CA30B266657B42C236D8802BA05FA6B2DE74B77356BD771B9A44125F` al comprobarla después de la reproducción. No se abrió desde herramientas ni se modificaron sus filas. La aplicación puede ser abierta posteriormente por el usuario durante la revisión.

R2 fue cerrada por el usuario y recompilada con `dotnet build src/CasaMiga.Presentation -c Release --no-restore -p:OutputPath=bin/R2/`: 0 errores/0 advertencias. DLL idéntica a la verificada: SHA-256 `EE073CE0AD850FFF312E20D7B5BCD4A8B917AD723BB2F954E6A50BA58EC66CD6`. Configuración de desarrollo y printing.json conservados.

## Suite final

Comando terminado con exit 0: `scripts/verify.ps1 -Dotnet work/.dotnet/dotnet.exe -OutputPath bin/InventoryFix/`.

- Restore locked correcto.
- Release: **0 errores, 0 advertencias**.
- CasaMiga.Tests: **545/545**, cero fallidas/omitidas. TRX `TestResults/foundation_net10.0_20261001112128.trx`.
- CasaMiga.PhotoTests: **108/108**, cero fallidas/omitidas. TRX `TestResults/foundation_net10.0_20261001111723.trx`.
- Total: **653/653**; 12 casos nuevos frente al baseline 641 (9 de persistencia/recuperación y 3 de WPF).
- `git diff --check`: correcto. Revisión del diff completada; cambios de producto sólo en Presentation.
- Domain/Application/Infrastructure/migrations sin diff. Schema 16, SHA-256 de 016: `9223CAA06CD02FA32E874CC1516EE0C6DB798CB76E2FBE1BF267BAB9B695ED76`.
- HEAD conservado `353c7e271dfbdfe66b2e2dc8bc6c28e46914a971`; ocho archivos modificados y cinco nuevos sin stage ni commit. Sin push/remoto.
- Base de revisión original, tras cerrar el usuario la aplicación: hash idéntico al inicial. Caja/ventas/stock originales no se mutaron desde herramientas. No se imprimió.

La consulta de Harina quedó preparada con `src/CasaMiga.Presentation/bin/R2/CasaMiga.Presentation.exe`, que contiene la DLL verificada. El 01/10/2026 el usuario confirmó «todo correcto» y pidió el reporte. Se registra la validación humana de la corrección de Inventario, sin afirmar que las herramientas observaron sus pasos manuales. FASE 1K permanece abierta; esta confirmación no acredita el smoke global R2 ni impresión física Qian.

El cierre posterior a la aprobación es exclusivamente documental. Se conserva la evidencia de suite completa anterior y no se repiten pruebas costosas. Código de producto, pruebas y herramienta de reproducción permanecen idénticos; migraciones sin cambios. Sin stage, commit ni push.

## Checkpoint Git autorizado — 01/10/2026

El usuario confirmó manualmente: detalle de MP-HAR-001 sin cierre, HAR-BULTO-25KG persistido con 25 kg y Activa marcada, Harina sin saldo inicial y sin entradas/mermas/conteos/asociaciones. Autorizó un único commit del bugfix: `fix: prevent inventory detail crash after presentation load`. La revisión completa de diff y archivos nuevos no encontró cambios ajenos al alcance. El padre del checkpoint es `353c7e271dfbdfe66b2e2dc8bc6c28e46914a971`; SHA final en conversación, para evitar autorreferencia en el commit.

Hashes SHA-256 comprobados contra los registrados al finalizar la verificación anterior:

| Archivo | SHA-256 |
|---|---|
| src/CasaMiga.Presentation/InventoryView.xaml | 131D54A20162E8C99C110F48C5D446321C3B889A63042B8FE4F1A869DA6A5BAB |
| src/CasaMiga.Presentation/InventoryViewModel.cs | BD886380497C4382C9D6DD1A43577F81AE9577816A4ED2B201EFBAEF81FF8454 |
| tests/CasaMiga.Tests/InventoryViewModelTests.cs | 5FA5CFE6A4CC5DCDC5D3D4A5ADA0E17359F32D15303DC6DCEC0BD79310A7D6D4 |
| tests/CasaMiga.PhotoTests/InventoryPresentationTests.cs | 3145028373E00A4AC9B4C6D1FE069EE386FC15206D423BAC031B0F1F3A6433D6 |
| tools/CasaMiga.Phase1K2CSmoke/Program.cs | DB24CC193AB461520D90C8678701B361447D19E8A7E5495B5FBC5174CDAAE7C5 |
| tools/CasaMiga.Phase1K2CSmoke/InventoryDetailProbe.cs | 2E7114C8015D213B39C371151BD18BFE3E41C351D465A53467313993B9D79F3E |

Los seis coinciden. DLL R2 y migración 016 también conservan los hashes documentados arriba. Se releen los TRX: 545/545 y 108/108 aprobadas. No se repite la suite: sólo cambió documentación desde la verificación. Schema 16 intacto; sin push, cambios funcionales adicionales, modificación de datos, impresión ni operaciones comerciales. Reanudar manualmente Inventario → MP-HAR-001 → Ver detalle → «Saldo inicial» con R2. FASE 1K permanece abierta.
