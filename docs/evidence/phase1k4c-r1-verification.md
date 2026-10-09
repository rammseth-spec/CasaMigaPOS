# Evidencia FASE 1K.4C-R1 — 07/10/2026

Baseline `cb3f55f4ea47372be98d50bf1eb467bfa2b135e9`, rama `master`, árbol inicial limpio. Focales 28/28. `scripts/verify.ps1`: 739/739 (561 generales + 178 WPF), cero fallidas y omitidas. Build Release `Phase1K4C-R1`: 0 errores y 0 advertencias. `git diff --check` correcto.

La regresión cubre formato manual estricto `dd/MM/yyyy`, fechas inexistentes, texto arbitrario, rango invertido, conservación del filtro previo, selección por calendario, placeholder de una línea, separación Venta/Caja, título de detalle, singular/plural, vacío y sesión de 57 ventas distribuida en 50 + 7. Se preservan Hoy, Ayer, día final inclusivo, detalle financiero, permiso de reimpresión, 1280×820, 1920×1080, F11 y Escape. No se imprime.

La copia aislada `work/phase1k4c-r1-smoke-20261007` es byte-idéntica a la fixture 1K.4C en `casamiga.db`: SHA-256 `7DBBDF5FD9926B77EA60205C3FD28284E87C7C850A3433FA17DACEF81B9FCD36`; su manifiesto acredita credenciales, schema 16, 57 ventas actuales y 60 auxiliares. La base protegida conserva SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.

Artefactos locales ignorados: build `src/CasaMiga.Presentation/bin/Phase1K4C-R1/`, metadata `CasaMiga.ReviewBuild=1K.4C-R1`, launcher `work/Abrir-Casa-Miga-1K4C-R1.cmd` y settings que apuntan exclusivamente a la copia R1. Domain, Application, Infrastructure, SQLite y migraciones permanecen intactos; schema 16 y migración 017 ausente.
