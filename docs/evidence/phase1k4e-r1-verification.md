# Evidencia de verificación — FASE 1K.4E-R1

- HEAD inicial: `180134c96f8aeb57ea2e0bc20466a38f31499abc`.
- Focales de ticket: 30/30, 0 fallidas, 0 omitidas.
- `scripts/verify.ps1`: 576/576 generales + 187/187 WPF/Presentation = 763/763; 0 fallidas, 0 omitidas.
- Release de solución y build manual identificado: 0 errores, 0 advertencias.
- Metadata: `CasaMiga.ReviewBuild=1K.4E-R1`.
- EXE: `src/CasaMiga.Presentation/bin/Phase1K4E-R1/CasaMiga.Presentation.exe`, SHA-256 `8644236BAA06907D3C5CA894337F5664FC8BF09FC2E24D6A6C87942780D2AEC7`.
- DLL: `src/CasaMiga.Presentation/bin/Phase1K4E-R1/CasaMiga.Presentation.dll`, SHA-256 `221057B3D7C19FCB94D64FBD464CB012EC7E428A0999CC73FD7FCDDD16ED106D`.
- Launcher local: `work/Abrir-Casa-Miga-1K4E-R1.cmd`, SHA-256 `B1B77DAC244AF6BA3917A438AD3A9E83BF73FAE92C57B4F821DD55134306FCE2`.
- Settings locales: SHA-256 `22B017C60F393114B50E7B8591BEC542C655020CFC13D03399131C0486C8823B`; DataDirectory `work/phase1k4e-r1-smoke-20261008`; desarrollo activo.
- Título esperado: `Casa Miga POS · Caja · DESARROLLO · 1K.4E-R1`.
- Base protegida antes/después: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`, schema 16.
- Fixture: schema 17; credencial verificada; `openCashDrawer=false`; cero trabajos enviados a la impresora.
- Logo térmico 576×275, marca 446 puntos, SHA-256 `EA3258058C9060D85FBAD830C845EE5BD52813F7F9D29BD6EA43FFCD2395AFFD`.
- Payload original/reimpresión generado fuera de línea. Cada original contiene una Variante 04, un raster social 576×40 con los logotipos de Facebook e Instagram alineados sobre sus cuentas, un corazón, un corte parcial y cero ESC p.
- Migración 017 intacta; no existe 018.

La apertura automática sólo comprueba launcher, ejecutable, DataDirectory y título; no equivale a aprobación del ticket físico. La auditoría de bytes está en `phase1k4e-r1-byte-audit.json`.
