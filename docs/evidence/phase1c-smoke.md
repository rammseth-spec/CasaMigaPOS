# Smoke WPF — FASE 1C

Ejecutado 2026-09-06 con aplicación Release win-x64 autocontenida, entorno work/ui-smoke-visible marcado DESARROLLO. Usuario sintético SMOKE-ADMIN; contraseña aleatoria sólo en memoria, no incluida en evidencia.

| Caso | Resultado observado |
|---|---|
| A | PAN DULCE creada; Concha vainilla apareció activa a $18.00 DEMO. |
| B | Editor cambió $18.00 a $19.00; lista y auditoría SQL confirman 1800→1900. |
| C | PAN SALADO y Bolillo $5.00 DEMO creados; filtro devolvió sólo Bolillo. |
| D | Bolillo desapareció de activos; con Mostrar inactivos apareció Inactivo a $5.00, datos conservados. |
| E | PAN DULCE rechazó desactivación: contiene productos activos; desactívalos o muévelos primero. |
| F | Cierre real y reapertura, login requerido; Concha activa $19.00, Bolillo inactivo $5.00, filtro estable. |

Incidencia inicial: al renderizar primera categoría, WPF lanzó InvalidOperationException por enlace TwoWay a IsActive de sólo lectura. Commit y auditoría ya existían. Corregido OneWay, recompilado y probado creación/lista de segunda categoría sin cierre. Se mantuvo instancia de filtros cuando no cambia su contenido para evitar reinicio visual de SelectedValue. Repetición final comprobó selector PAN SALADO conservado al aplicar y mostrar inactivos.

Uso real de mouse, Tab, Ctrl+A, escritura, desplazamiento y Alt+F4. No se ensayó hardware táctil. No se cargó demo automático en este catálogo: las tres categorías/diez productos de SeedDemo se verificaron en tests aislados.

La aplicación terminó cerrada. Backup CLI completo restaurado en work/phase1c-cli-restored; comparadas todas las filas de catálogo, auditorías de catálogo, terminal y cuatro cash_session históricas: iguales. Ver phase1c-smoke-and-restore.json y phase1c-cli-restore.txt.
