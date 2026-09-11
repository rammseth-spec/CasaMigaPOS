# Continuidad

Lee README.md, docs/CURRENT_STATE.md y docs/NEXT_STEPS.md antes de trabajar. Luego sólo los ADRs y archivos pertinentes.
FASE 1F cerrada: historial y detalle de ventas de solo lectura, permiso sales.view, migración 007, smoke WPF, concurrencia y backup/restore completos. Baseline 1E: d4118144dd6f19bff885ced15a3f6098c24f7c29. No hay fase posterior autorizada. Sin cancelaciones, devoluciones, inventario, descuentos, tarjetas, fiscalidad, hardware, rediseño ni fullscreen.
No copiar código copyleft ni elegir licencia del producto sin autorización expresa.
Domain no referencia infraestructura ni UI. Operaciones críticas atómicas; efectos físicos posteriores al commit.
No modificar migraciones aplicadas: agregar una nueva. Nunca editar manualmente producción.
Importes MXN en centavos Int64 checked; costos fraccionarios requieren política decimal explícita.
No borrar ventas completadas ni movimientos; usar reversos trazables.
Actualizar estado, próximos pasos y evidencia después de cada etapa. No declarar pruebas no ejecutadas.
Antes de cerrar una modificación de producto: scripts/verify.ps1, revisión del diff y documentación actualizada. Para cierres exclusivamente documentales, verificar diff y que src/tests/migrations permanezcan intactos; no repetir suites costosas sin causa.


