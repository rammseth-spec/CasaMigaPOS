# Evidencia técnica — FASE 1K.4D

Fecha: 08/10/2026. Baseline `86a7d22a69e1e4b884acce02a2f17e4d315dc501`, master, limpio. Modelo: GPT-6 (Codex). Smoke humano pendiente. Sin publicación remota.

## Ejecuciones

- Focales generales Cash, migración e histórico: **76/76**.
- Focales WPF CashUsabilityTests: **3/3**, dos tamaños y permisos.
- Regresiones de upgrades históricos tras incorporar la columna nullable en el snapshot: **7/7**.
- `scripts/verify.ps1 -Dotnet work/.dotnet/dotnet.exe`: **757/757** (572 generales + 185 WPF/Presentation), 0 fallidas y 0 omitidas.
- Build de revisión Release, `CasaMigaReviewBuild=1K.4D`, OutputPath `bin/Phase1K4D/`: **0 errores, 0 advertencias**.
- Diff revisado: alcance Caja, nota, consulta, migración 017, pruebas, herramienta y docs. Sin cambios a migraciones 001–016. Sin 018.

La primera suite completa detectó siete comparaciones de snapshots históricos: `SELECT *` ahora incluye la columna añadida por 017. Se corrigió el helper de pruebas para incorporar un NULL esperado al snapshot anterior; sigue comprobando todos los valores originales y el nuevo NULL, sin excluir campos ni tablas. WPF pasó 185/185 en esa ejecución. La suite final se repitió completa después de la corrección del helper.

Logs locales ignorados: `work/phase1k4d-evidence/verify.log` (primera ejecución), `verify-final.log`, `legacy-upgrades.log`, `release.log`, `fixture.log`. TRX en `TestResults/`. El manifiesto final local registra hashes y commit.

## Fuente, build y datos

Raíz: `C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1`.

- EXE: `src/CasaMiga.Presentation/bin/Phase1K4D/CasaMiga.Presentation.exe`.
- SHA-256 EXE: `6C59900B7F0E28FB0F086EF700EFE917D263DEC7DC36A7ED6555E246A102BCEE`.
- SHA-256 Presentation.dll: `18553BAF1E1E76EB3DEB71E148941C29CD96E2C89500C8EE8FA21DF06FCC7F5D`.
- SHA-256 Infrastructure.dll: `A03F1448F4C4DBFE3C16FF38C9809C3B4A7CE9FF21D18E28515E4EE1F9FC19A6`.
- Launcher: `work/Abrir-Casa-Miga-1K4D.cmd`; ejecuta exclusivamente ese EXE, limpia CASAMIGA_DATA_DIR localmente y fija DOTNET_ROOT al SDK local.
- `development.settings.json`: DataDirectory `C:\Users\ramms\Documents\ChatGPT\Casa Miga POS V1\work\phase1k4d-smoke-20261008`, IsDevelopment=true, tamaño 1280×820.
- Título requerido y metadata: `Casa Miga POS · Caja · DESARROLLO · 1K.4D`.
- Base protegida original: schema 16; SHA-256 inicial/final `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
- Fixture: copia migrada 16→17, 30 cierres, sin sesión abierta, 28 ventas sintéticas nuevas por tooling, ningún checkout o venta comercial.
- `cash-fixture-manifest.json` registra procedencia, schema, conteos, escenarios y validación de credencial. No registra la contraseña.
- Inventario operación/movimiento 1/1, producción 0, impresión solicitudes/eventos 17/34: sin variación antes/después del tooling.
- `printing.json` conservado byte a byte: SHA-256 `88843EAF9F452FD94C7CDEA77A71657923DD3E123D082DB9EEEDEB9277464461`, openCashDrawer=false. Sin trabajos de impresión ni pulsos.

## UI y límites

Capturas WPF revisadas: `cash-list-1280.png`, `cash-list-1920.png`, `cash-result-1280.png`, `cash-result-1920.png`, `cash-history-1280.png`, `cash-history-1920.png` en `work/phase1k4d-evidence/`. Lista compacta sin desplazamiento horizontal, detalle legible, resumen y nota con controles accesibles por scroll vertical. Las capturas preceden únicamente al ajuste de plural «1 cierre»; la suite y build finales incluyen ese ajuste.

La prueba WPF recorre apertura inválida/válida, doble comando, conteo ciego, resultado, obligatoriedad de nota, cierre único, histórico, detalle, F11/Escape y permisos. No equivale al smoke humano de la copia entregada. La comprobación de apertura final (PID, ruta, título, login) se registra después del commit en el manifiesto local y se entrega en conversación; no se inicia sesión desde UI.

Ver `../PHASE_1K4D_REPORT.md` para decisiones, fórmulas, compatibilidad, fixture y checklist humano. El checkpoint GitHub se difiere hasta aprobación manual. No iniciar fase siguiente.

TRX finales: `TestResults/foundation_net10.0_20261008113504.trx` (572 generales) y `TestResults/foundation_net10.0_20261008113202.trx` (185 WPF). `scripts/verify.ps1` terminó con exit code 0.
