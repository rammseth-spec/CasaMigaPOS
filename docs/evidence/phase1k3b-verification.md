# Evidencia técnica — Inventario 1K.3B

04/10/2026. Baseline limpio `4f20e520d74c6cace8d13b2ad3b04f436fc6eb71`. Smoke manual pendiente; 1K.3A/R1 ya aprobadas por el usuario.

## Ejecuciones

| Verificación | Resultado |
| --- | --- |
| Focal InventoryViewModel, InventoryPresentation, Usability, popup/Esc, Domain/Application/Persistence de Inventario | 142/142: 107 generales + 35 WPF/Presentation |
| `scripts/verify.ps1 -Dotnet (Resolve-Path ./work/.dotnet/dotnet.exe) -OutputPath bin/Phase1K3B/` | 702/702: 558 generales + 144 fotos/WPF |
| Fallidas / omitidas | 0 / 0 |
| Restore locked | Correcto |
| Release, `CasaMigaReviewBuild=1K.3B` | 0 errores / 0 advertencias |
| Recompilación final de reconciliación | DLL/PDB de Presentation y ambos proyectos de pruebas idénticos a los de la suite |
| Revisión de diff y `git diff --check` | Correctos; se repiten sobre staged antes del commit |
| Capas de negocio y migraciones | Sin cambios; schema 16, sin 017 |

TRX definitivos locales (hashes y counters completos en [manifiesto](phase1k3b-verification.json)):

- `TestResults/phase1k3b-focal-verified_net10.0_20261004122206.trx`: 35/35.
- `TestResults/phase1k3b-focal-verified_net10.0_20261004122213.trx`: 107/107.
- `TestResults/foundation_net10.0_20261004122523.trx`: 144/144.
- `TestResults/foundation_net10.0_20261004122841.trx`: 558/558.

Logs locales: `work/phase1k3b-focal-verified.log`, `work/phase1k3b-verify.log`, `work/phase1k3b-reconcile-build.log`. No pruebas declaradas sin ejecución. Las primeras pasadas detectaron el enlace faltante de InventoryOperationPreview en el proyecto de pruebas y dos expectativas literales del texto renovado; se corrigieron antes de las pasadas definitivas. La revisión detectó paginación perdida al cancelar debounce y motivó una regresión WPF específica, ya incluida.

Cobertura nueva: 2 pruebas ViewModel y 21 casos WPF/preview, usando bases temporales. Búsqueda incremental/cancelada/obsoleta, selección válida, × Limpiar/foco, vacío, paginación, nombres/SKU sin GUID, equivalencia, fechas, ayudas, botones y precisión atómica exacta MASS/VOLUME/COUNT contra servicios reales. Comprobación de confirmación persistida cuando el saldo cambia después de la preview. La suite conserva crash, recuperación, permisos, balance/ledger, checkout, backups, rueda/scroll y popup Esc/fullscreen.

## Capturas reales de WPF

- [Detalle y asociaciones, 1280×820](phase1k3b-detail-1280.png): equivalencia y nombre/SKU; posición desplazada del detalle.
- [Búsqueda sin coincidencias, 1280×820](phase1k3b-search-empty-1280.png).
- [Entrada, 1280×820](phase1k3b-preview-receipt.png): 50 kg + 25 kg = 75 kg.
- [Conteo, 1280×820](phase1k3b-preview-count.png): total físico 48 kg y diferencia −2 kg.
- [Historial en ventana grande](phase1k3b-history-1920.png): fecha local y columnas humanas. Nombre de archivo conserva el tamaño solicitado; dimensiones efectivas **1554×882**, limitadas por Windows/monitor/DPI. No acredita captura física 1920×1080 ni múltiples DPI.

Los recortes superiores del detalle/historial corresponden al viewport desplazado. Los controles inferiores y columnas finales se alcanzan con las barras visibles. No se modificaron bindings del shell para simular tamaños. La revisión física y aceptación visual corresponden al usuario.

## Protección y entrega

La instalación original de revisión no se abrió mediante SQLite: únicamente se leyeron y copiaron archivos para hash/copia cerrada. Auditoría de datos y comprobación offline de credenciales realizadas sobre copia mediante `mode=ro` / `query_only=ON`, sin autenticar ni escribir contraseñas en UI.

SHA-256 original inicial y previo a apertura: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. Harina 50 kg; Bulto 25 kg activo; sin movimientos de esta fase en datos del usuario. Copia nueva coherente de 59 archivos, todos idénticos; ningún lock copiado. Consumo automático DISABLED, sin modificar.

`printing.json` original y copia idénticos: `88843EAF9F452FD94C7CDEA77A71657923DD3E123D082DB9EEEDEB9277464461`. POS-80/48/ibm850/tabla 2/Partial/sin cajón y logo Variante 04 intactos. Cero trabajos físicos.

Build: `src/CasaMiga.Presentation/bin/Phase1K3B/`. Launcher: `work/Abrir-Casa-Miga-1K3B.cmd`. DataDirectory: `work/phase1k3b-smoke-20261004`. El manifiesto incluye rutas absolutas, configuración efectiva, hashes de fuentes/binarios/settings/launcher/migraciones e identidad de pruebas.

Tras el commit único autorizado se comprueban proceso, ruta, título `Casa Miga POS · Caja · DESARROLLO · 1K.3B` y login vacío por Computer Use; se deja abierto. Observación y SHA final se registran en `work/phase1k3b-evidence/final-delivery.json` y conversación, junto con último hash original/Git limpio. No utilizar el acceso anclado, no login automático, no push.
