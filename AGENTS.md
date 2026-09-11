# Continuidad

Lee README.md, docs/CURRENT_STATE.md y docs/NEXT_STEPS.md antes de trabajar. Luego sólo los ADRs y archivos pertinentes.
FASE 1E cerrada: venta confirmada y cobro efectivo transaccional, 1E.1–1E.5 y smoke WPF completos. No hay fase posterior autorizada. Sin stock, descuentos, devoluciones, tarjetas, fiscalidad, hardware, rediseño ni fullscreen. Baseline 1D: 92c5ef795b6da8e1cdf7f34ec9fb1b01cce4a5d2.
No copiar código copyleft ni elegir licencia del producto sin autorización expresa.
Domain no referencia infraestructura ni UI. Operaciones críticas atómicas; efectos físicos posteriores al commit.
No modificar migraciones aplicadas: agregar una nueva. Nunca editar manualmente producción.
Importes MXN en centavos Int64 checked; costos fraccionarios requieren política decimal explícita.
No borrar ventas completadas ni movimientos; usar reversos trazables.
Actualizar estado, próximos pasos y evidencia después de cada etapa. No declarar pruebas no ejecutadas.
Antes de cerrar una modificación de producto: scripts/verify.ps1, revisión del diff y documentación actualizada. Para cierres exclusivamente documentales, verificar diff y que src/tests/migrations permanezcan intactos; no repetir suites costosas sin causa.


