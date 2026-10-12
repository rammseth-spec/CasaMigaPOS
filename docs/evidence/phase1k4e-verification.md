# Evidencia de verificación — FASE 1K.4E

- HEAD inicial: `30887e85de0c3c1505c67c7c4169092e5e9b2b54`.
- Focales de ticket: 28/28, 0 fallidas, 0 omitidas.
- `scripts/verify.ps1`: 574/574 generales + 187/187 WPF/Presentation = 761/761; 0 fallidas, 0 omitidas.
- Release de solución: 0 errores, 0 advertencias.
- Release manual identificado: `CasaMiga.ReviewBuild=1K.4E`; 0 errores, 0 advertencias.
- EXE: `src/CasaMiga.Presentation/bin/Phase1K4E/CasaMiga.Presentation.exe`, SHA-256 `BC667A902E652CEF3D44A614D999FA79366F66C46F87354E114F0BC58EF72837`.
- DLL: `src/CasaMiga.Presentation/bin/Phase1K4E/CasaMiga.Presentation.dll`, SHA-256 `0FC687F577241FB9D2E8C9C06DB3FCD50D719736A5C555C2A7C9CE0F1D08333C`.
- Launcher local: `work/Abrir-Casa-Miga-1K4E.cmd`, SHA-256 `3D9ABFBD9CFF2E9403DF0240CF2CD224D88EA88E5E2281139295F9DD04C41E7F`.
- Settings locales: SHA-256 `B15649B4CEE2150FA23C406FBE8F3E6E0DE7F248A2514EC7D5476E4ACDA46F81`; DataDirectory `work/phase1k4e-smoke-20261008`; desarrollo activo.
- Título esperado: `Casa Miga POS · Caja · DESARROLLO · 1K.4E`.
- Base protegida antes/después: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, schema 16.
- Fixture: schema 17; credencial verificada; `openCashDrawer=false`; cero trabajos de impresión enviados.
- `git diff --check`: correcto. Migración 017 intacta; no existe 018.

La verificación de bytes está en `phase1k4e-byte-audit.json`. La apertura automática sólo comprueba launcher, ejecutable, DataDirectory y título; no equivale a aprobación del ticket físico.
