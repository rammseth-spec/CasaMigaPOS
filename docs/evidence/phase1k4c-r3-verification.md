# Evidencia FASE 1K.4C-R3 — 07/10/2026

Baseline `51651283cb97efe36e1f8f891998ba888a8232b6`, rama `master`, árbol inicial limpio. La máscara opera sobre una secuencia máxima de ocho dígitos y deriva siempre la presentación `dd/mm/aaaa`; el cursor se traduce entre posiciones visuales y posiciones de dígito para insertar, sustituir, Backspace y Delete sin barras dobles ni estados rotos. El pegado admite `07102026` y `07/10/2026`; rechaza guiones, contenido arbitrario y más de ocho dígitos.

La validación semántica R2 permanece separada: `32102026` se presenta como `32/10/2026`, conserva el texto y activa “Fecha inválida. Usa dd/mm/aaaa.” sin consultar ni sustituir los resultados previos. El calendario sigue escribiendo `dd/MM/yyyy` en el mismo buffer. No cambiaron filtros, consulta, UTC/local, día Hasta, listado, detalle, reimpresión, F11 o Escape.

Focales 32/32: 18 generales y 14 WPF/Presentation. `scripts/verify.ps1`: 743/743 (561 generales + 182 WPF/Presentation), cero fallidas y omitidas; compilación Release general y build identificado Phase1K4C-R3 con cero errores y advertencias. La cobertura incluye teclado real del control, secuencia progresiva, límite, caracteres, Backspace, Delete, selección/reemplazo, ambos pegados, error semántico, calendario, rangos, placeholder, 1280×820, 1920×1080, F11 y Escape.

La copia aislada `work/phase1k4c-r3-smoke-20261007/casamiga.db` conserva SHA-256 `7DBBDF5FD9926B77EA60205C3FD28284E87C7C850A3433FA17DACEF81B9FCD36`, schema 16 y credencial verificada. La base protegida conserva SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. No se operaron ventas, caja, inventario, producción ni impresión. Domain, Application, Infrastructure, SQLite y migraciones permanecen intactos; no existe 017.

Artefactos locales ignorados: `src/CasaMiga.Presentation/bin/Phase1K4C-R3/`, `work/Abrir-Casa-Miga-1K4C-R3.cmd` y la fixture R3. El checkpoint es local; no hubo push ni PR y 1K.4D no se inició.
