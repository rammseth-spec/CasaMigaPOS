# Verificación 1K.3A-R1 — 03/10/2026

## Evidencia reproducible

- Focales: 18/18, 0 fallidas, 0 omitidas. `TestResults/phase1k3a-r1-focal.trx`.
- Comparación binaria anterior Phase1K3A: 3 vistas pasan, 3 Esc fallan. `TestResults/phase1k3a-r1-baseline-probe.trx`.
- Comparación R2: 3 vistas fallan al faltar texto/selector/estado vacío de 1K.3A. `TestResults/phase1k3a-r1-r2-probe.trx`.
- Suite completa: **679/679**, 0 fallidas, 0 omitidas. `scripts/verify.ps1` terminó con código 0; 123 fotos/Presentation en `TestResults/foundation_net10.0_20261003114812.trx` y 556 generales en `TestResults/foundation_net10.0_20261003115216.trx`.
- Release: 0 errores y 0 advertencias en `work/phase1k3a-r1-verify.log`.

No hubo cambios de código/pruebas después de esta ejecución; sólo documentación/evidencia. No se repite la suite por esos cambios. Revisión independiente del diff sin hallazgos bloqueantes. Los resultados y hashes de TRX están en `phase1k3a-r1/test-results.json`.

`git diff --check` correcto. Los avisos informativos CRLF→LF de Git no son fallos del diff ni advertencias de compilación.

Las comparaciones usan copias bajo work; las DLL originales no se sustituyeron. Se mantuvieron el tema real, las vistas cargadas en MainWindow, permisos válidos y controles habilitados de referencia, para no aprobar IsEnabled=false por una ventana completa bloqueada. SelectedValue se verifica como enum y se inspecciona el popup real con sus tres textos visibles.

## Capturas de WPF

RenderTargetBitmap de las pruebas focales sobre datos sintéticos, inspeccionadas visualmente. Son evidencia automatizada y no aceptación manual del usuario:

- [Recetas vacías y texto actual](phase1k3a-r1/r1-recipes-empty.png).
- [Popup con Masa / Volumen / Piezas](phase1k3a-r1/r1-dimension-popup.png).
- [Editor en MainWindow](phase1k3a-r1/r1-recipe-dimensions.png).
- [Producción sin perfiles](phase1k3a-r1/r1-production-no-profiles.png).

## Artefactos y trazabilidad

[build-manifest.json](phase1k3a-r1/build-manifest.json) registra SHA-256 de nueve archivos de código/pruebas, EXE/DLL R1, ensamblados de pruebas, launcher y settings locales. Compilación Release con `CasaMigaReviewBuild=1K.3A-R1` y OutputPath `bin/Phase1K3A-R1/`. El build es anterior al commit de cierre; su metadata informativa lleva el HEAD inicial, mientras el manifiesto enlaza los contenidos exactos probados.

Phase1K3A previo: DLL `4245A51C360213D3EA48833D67C445BF0488343DBF09DB1932520FE7AF735876`, EXE `3B0FEBAB473E16C6141979E20CF0F18E800D7AE5C9B2DB2957466BA24318D084`. Ambos corresponden a los hashes del reporte previo. R2 DLL: `EE073CE0AD850FFF312E20D7B5BCD4A8B917AD723BB2F954E6A50BA58EC66CD6`.

El archivo local work/phase1k3a-r1-evidence/baseline.json se escribió después de las primeras ediciones delegadas: su campo InitialStatus contiene esas ediciones, no el status previo al trabajo. La comprobación Git limpia inicial se realizó antes de editar. No se usa ese campo para afirmar que las ediciones preexistían.

## Datos protegidos y límites

Original work/phase1k2c-r2-smoke-20260928/casamiga.db, SHA-256 inicial y precheckpoint:

`CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`

La copia work/phase1k3a-r1-smoke-20261003/casamiga.db coincide antes de arrancar. Schema 16 y Harina 50 kg/presentación activa de 25 kg acreditados por la evidencia `phase1k3a-data.json` de la misma base byte-idéntica. No se abre la base original con SQLite durante R1.

printing.json original conserva SHA-256 `88843EAF9F452FD94C7CDEA77A71657923DD3E123D082DB9EEEDEB9277464461`; copia de revisión idéntica. POS-80, 48 columnas, ibm850, tabla 2, Partial, cajón false. Logo Variante 04 intacto. Sin impresión ni operaciones comerciales.

Migración 016: SHA-256 `9223CAA06CD02FA32E874CC1516EE0C6DB798CB76E2FBE1BF267BAB9B695ED76`. Ninguna migración modificada, sin 017. Domain/Application/Infrastructure intactos en diff.

La observación de proceso/título y el hash final tras la apertura automática ocurren después del commit; se registran en el reporte final local y en conversación. No se atribuye una instancia histórica al usuario sin evidencia. Estado: corrección técnica, aprobación manual R1 pendiente.
