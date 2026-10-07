# Evidencia FASE 1K.4C-R2 — 07/10/2026

Baseline `50f7349e51688d34615b997228a6243ba0b1032e`, rama `master`, árbol inicial limpio. En R1, el `DatePicker` externo medía 190 px, el `DatePickerTextBox` 62 px y el viewport de texto sólo 38 px: el botón interno heredaba el `MinWidth=116` del estilo global. Al perder foco, el parsing interno levantaba `DateValidationError` y restauraba `Text` desde un `SelectedDate` nulo, eliminando el texto inválido antes del manejador de Aplicar.

R2 usa un control compuesto de Presentation con `TextBox`, placeholder de una línea, botón de calendario fijo de 42 px, `Popup` y `Calendar`. Los editores miden al menos 165 px dentro de campos de 210 px. El ViewModel conserva buffers crudos, valida con `DateOnly.TryParseExact(..., "dd/MM/yyyy", InvariantCulture, None)` al perder foco o aplicar y conserva los resultados anteriores ante cualquier error.

Focales 29/29: 18 generales y 11 WPF/Presentation. `scripts/verify.ps1`: 740/740 (561 generales + 179 WPF/Presentation), cero fallidas y omitidas; compilación Release general y build identificado Phase1K4C-R2 con cero errores y advertencias. La cobertura prueba placeholder, texto completo, calendario, cinco entradas inválidas, retención, error humano, escritura parcial, rango invertido, día final inclusivo, 1280×820, 1920×1080, Todos/Hoy/Ayer, sesión actual 50+7, paginación, F11 y Escape.

La copia aislada `work/phase1k4c-r2-smoke-20261007/casamiga.db` conserva SHA-256 `7DBBDF5FD9926B77EA60205C3FD28284E87C7C850A3433FA17DACEF81B9FCD36`, schema 16, credencial verificada y 57 ventas de sesión actual. La base protegida conserva SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. No se envió impresión ni se operaron ventas, caja, inventario o producción.

Artefactos locales ignorados: `src/CasaMiga.Presentation/bin/Phase1K4C-R2/`, `work/Abrir-Casa-Miga-1K4C-R2.cmd` y la fixture R2. Domain, Application, Infrastructure, SQLite y migraciones permanecen intactos; schema 16 y migración 017 ausente. El checkpoint es local; no hubo push ni PR y 1K.4D no se inició.
