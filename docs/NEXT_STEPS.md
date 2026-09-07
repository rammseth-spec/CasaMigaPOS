# Próximos pasos

Detenerse después del reporte 1D. Esperar revisión y autorización explícita antes de 1E. No implementar venta persistida, pagos, cobro, ticket ni hardware con la autorización actual.

Para acordar 1E: revalidación de comerciabilidad/precio de snapshots temporales, alcance de venta y pago, idempotencia y atomicidad, productos controlados, congelamiento desde conteo confirmado y manejo de fallos. Cart is transient and not a sale; no tratarlo como una venta pendiente persistida.

Antes de producción: precios/reglas comerciales, administración y recuperación de usuarios, política de cierre para cajeros, correcciones auditadas de conteo, zona horaria comercial, activación/rollback de restauración, retención/segunda copia/cifrado y pruebas táctiles/físicas. No editar producción para eludir reglas.

Mantener inmutables migraciones 001–005 y catalog_key_v1. ADR-009 documenta captura fatal implementada; no usarla para ocultar errores ni continuar tras estado desconocido.

Git carece de identidad y commits. El propietario debe ejecutar con sus datos reales:

```powershell
git config user.name "Tu nombre real"
git config user.email "Tu correo real"
```

Después, recuperar el baseline aprobado 1C desde work/phase1d-before en un procedimiento revisable y registrar 1D por separado. No sustituir el árbol actual sin conservar sus cambios. No se requiere remoto ni elegir licencia para continuar localmente.
