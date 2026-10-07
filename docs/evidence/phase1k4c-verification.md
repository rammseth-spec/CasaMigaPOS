# Evidencia FASE 1K.4C — 07/10/2026

Baseline `01383e900f09311f9322782bb0ce6d5891dc7cb0`, rama `master`, árbol inicial limpio. El remoto permanece fuera de este checkpoint.

| Verificación | Resultado |
|---|---|
| Focales Application | 18/18 |
| Focales WPF + F11/Escape | 8/8 |
| `scripts/verify.ps1` | 737/737: 561 generales + 176 WPF |
| Fallidas / omitidas | 0 / 0 |
| Release Phase1K4C | 0 errores / 0 advertencias |
| `git diff --check` | correcto |
| Schema / 017 | 16 / ausente |
| Domain / migraciones | sin cambios / sin cambios |

La regresión de paginación crea 60 ventas más recientes en otra sesión y 57 en la sesión solicitada. El query de sesión devuelve 50 + 7 y nunca mezcla la otra sesión. `CashSessionId=null` conserva las 117 filas del conjunto de la prueba. La UI comprueba Hoy/Ayer/rango final inclusivo, sesión real, ausencia de sesión, sesión vacía, correlativo persistido, texto humano, detalles financieros, permisos sin imprimir y ausencia de mutaciones.

Fixture de revisión: `work/phase1k4c-smoke-20261007/fixture-manifest.json`. Parte de una copia aislada, verifica `catalog-admin`, conserva schema 16 y contiene 57 ventas de la sesión actual, 60 de una sesión auxiliar, 115 de Hoy, una de Ayer y una anterior. Conteos aislados: sale/payment 16→133; cash_session 2→3; receipt_print_request 17→17; inventory_operation 1→1; inventory_movement 1→1; production_batch 0→0. SHA-256 final aislado: `7DBBDF5FD9926B77EA60205C3FD28284E87C7C850A3433FA17DACEF81B9FCD36`.

Base protegida inicial/final: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. No se realizaron ventas, caja, inventario, producción o impresión en ella.

Build: `src/CasaMiga.Presentation/bin/Phase1K4C/`; metadata `CasaMiga.ReviewBuild=1K.4C`. Los hashes previos al commit demostraron la entrega preparada; la recompilación posterior incorpora el HEAD definitivo en metadata, por lo que sus hashes finales se registran en la evidencia local ignorada. Launcher SHA-256 `E8E9C6F5CE1E7215D1EE62153176C3FEB7392CB70271C727C2F6670656CDF443`.

La apertura del launcher exacto ocurre después del commit local. El PID, título, hash posterior al commit y DirectoryLease se guardan en evidencia local ignorada y se informan en conversación.
