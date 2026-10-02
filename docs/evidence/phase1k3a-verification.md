# Evidencia de verificación — 1K.3A

01/10/2026. Baseline: 6942c061bf6a68a65d42f57db5b00fbe567d1548.

Comando final: scripts/verify.ps1 -Dotnet (Resolve-Path work/.dotnet/dotnet.exe) -OutputPath bin/Phase1K3A/
Restore locked-mode correcto. Build Release: 0 warnings y 0 errores. Suite 672/672, cero fallidas y omitidas.

| TRX local | Total | Aprobadas | Fallidas | No ejecutadas | SHA-256 |
|---|---:|---:|---:|---:|---|
| phase1k3a-vm.trx | 52 | 52 | 0 | 0 | 7EE9D38BC3AEFFAFF673B6DF812E8383EC318181346539D289B1F8ADF8D54AC9 |
| phase1k3a-wpf.trx | 11 | 11 | 0 | 0 | F54135980869BEB906E92BD9BD3582347328FC4104BDBF3167F9A34962D4C467 |
| foundation_net10.0_20261001163525.trx | 116 | 116 | 0 | 0 | 55B9431102CF830C296EBF6B10D60F451CCDB1CAAC9239F941FF87FEFBA660D5 |
| foundation_net10.0_20261001163929.trx | 556 | 556 | 0 | 0 | AD3CC2B161555DB0BE9C306DFD67BBBF9B31CB48AB3FFFE392C543CD7FE46969 |

Focales: 52 + 11 = 63. Suite completa: 556 + 116 = 672. Baseline anterior 653; 19 casos nuevos. Los TRX se mantienen locales/ignorados; este resumen conserva sus hashes.

## Hashes de código y pruebas verificados

| Archivo | SHA-256 |
|---|---|
| src/CasaMiga.Presentation/ContextHelp.xaml | 9EC2C2213F73BE9DB063CB40FFB0F478557046F5B33D6A545AEBEF7DAD84748F |
| src/CasaMiga.Presentation/ContextHelp.xaml.cs | DF4C34D348CEA387CEDF875AF3CCE591263E0CCBED57DB442B6F67B656EE8ACE |
| src/CasaMiga.Presentation/InventoryView.xaml | BB9FE2B26148BEC30900FA1CE86C3428D3F07D9ED2EC7AE02E8B7A3E5457B4FF |
| src/CasaMiga.Presentation/InventoryViewModel.cs | 8C7E563215A934C7F3ABB65A9766F0A46BFC7445642276E602602735B2CA8A2B |
| src/CasaMiga.Presentation/NestedScrollBehavior.cs | CDA7221E98B60F558852CA6CAA5F6000AD358FACC494571B8B3F9CA3BE534B19 |
| src/CasaMiga.Presentation/ProductionView.xaml | E535B7B0FE7BF6B1529FF7E8B7C6C35E2AB3AC918B761DEDE85E49876DC7D5D7 |
| src/CasaMiga.Presentation/ProductionViewModel.cs | 1DB5546F401CB549ACA7096558644746C535A9444EEA88ED9E15EA20D50427B6 |
| src/CasaMiga.Presentation/RecipeView.xaml | 0D284F4DF64F2C4E9745D6C0A814652915251273437DB7A2AFAF077525C5FC73 |
| src/CasaMiga.Presentation/RecipeViewModel.cs | 6BA1EB9692948AF07D5F47E89143D8E1A387EE1D96E585F33C7947D4DA7C16EB |
| src/CasaMiga.Presentation/Resources/Theme.xaml | 8B5F28EE602B53CD96A8AE24F97D2BAC3D87CB6470F3D4A1F745766D97C3A9DE |
| src/CasaMiga.Presentation/UiLabels.cs | 81FD7B2F8070B45FD5C48452955FEDC3566F08A76EFFF61F964B83B36CA88456 |
| tests/CasaMiga.PhotoTests/InventoryPresentationTests.cs | B963457921529B51346646C5594E1C7EAFBBECA743E95F24E79E8DA0AE29464E |
| tests/CasaMiga.PhotoTests/UsabilityPresentationTests.cs | 59C5212A2692F1610B6C3190015B168967614A6128E8313F8352EE577B528FE3 |
| tests/CasaMiga.PhotoTests/WpfTestHost.cs | 2113541AAF04E93627B8B826E70FF6212B3D2800B32F1CFC940F71B2AED85D3E |
| tests/CasaMiga.Tests/CasaMiga.Tests.csproj | 53547430CBD7519D7B9DBD52B7737D40BDAF2BEBD3508FCD92810312A8C449C6 |
| tests/CasaMiga.Tests/InventoryViewModelTests.cs | E0D9940F60EA1F4CA664EBAB04B4604AB2658D348D0D786F4E140A5169DD7F33 |
| tests/CasaMiga.Tests/Phase1K3AViewModelTests.cs | CEF3E0B8C9C3E06E8DE33469DD1AC34FD81D64E49CC403366926EBF20F50228F |

## Build y datos

- src/CasaMiga.Presentation/bin/Phase1K3A/CasaMiga.Presentation.dll : 4245A51C360213D3EA48833D67C445BF0488343DBF09DB1932520FE7AF735876
- src/CasaMiga.Presentation/bin/Phase1K3A/CasaMiga.Presentation.exe : 3B0FEBAB473E16C6141979E20CF0F18E800D7AE5C9B2DB2957466BA24318D084
- migrations/016_product_photos.sql : 9223CAA06CD02FA32E874CC1516EE0C6DB798CB76E2FBE1BF267BAB9B695ED76
- work/phase1k2c-r2-smoke-20260928/casamiga.db : CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D

Base original SHA-256 de inicio y final idéntico: CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D. Auditoría únicamente sobre copia byte-idéntica en mode=ro/query_only; Harina 50 kg, presentación activa 25 kg y schema 16. Sin operaciones ni impresión en la instalación original.

Acceso catalog-admin verificado con la herramienta existente sobre una segunda copia aislada, sin cambiar la contraseña ni tocar la original. development.settings.json copiado exactamente de R2 a bin/Phase1K3A; sólo binarios/configuración local, ignorados por Git.

git diff --check correcto. Diff protegido Domain/Application/Infrastructure/migrations vacío. Revisión adicional sin hallazgos bloqueantes; sus observaciones de foco real en ComboBox y acciones sin catálogo se incorporaron antes de la suite final. No se ejecutó el smoke físico del usuario, touchpad físico ni impresión.
