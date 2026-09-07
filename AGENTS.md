# Continuidad

Lee README.md, docs/CURRENT_STATE.md y docs/NEXT_STEPS.md antes de trabajar. Luego sólo los ADRs y archivos pertinentes.
Alcance autorizado: FASE 1D (POS y carrito transitorio; sin venta persistida ni cobro). Detenerse al completar; no ventas ni hardware.
No copiar código copyleft ni elegir licencia del producto sin autorización expresa.
Domain no referencia infraestructura ni UI. Operaciones críticas atómicas; efectos físicos posteriores al commit.
No modificar migraciones aplicadas: agregar una nueva. Nunca editar manualmente producción.
Importes MXN en centavos Int64 checked; costos fraccionarios requieren política decimal explícita.
No borrar ventas completadas ni movimientos; usar reversos trazables.
Actualizar estado, próximos pasos y evidencia después de cada etapa. No declarar pruebas no ejecutadas.
Antes de cerrar una modificación: scripts/verify.ps1, revisión del diff y documentación actualizada.


