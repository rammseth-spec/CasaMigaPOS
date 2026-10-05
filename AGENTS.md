# Continuidad

Lee README.md, docs/CURRENT_STATE.md y docs/NEXT_STEPS.md antes de trabajar. Luego sólo los ADRs y archivos pertinentes.
FASE 1F cerrada; recuperación `db6422a1d09c36f1ae038c673d90ef9ec6f13ab4`. FASE 1G Bloque A 1G.1–1G.4 implementado y detenido antes del checkpoint físico Qian. No continuar 1G.5, codepage ESC/POS, corte, cajón, LAN o reimpresión UI sin nueva autorización. Sin cancelaciones, devoluciones, inventario, descuentos, tarjetas, fiscalidad, rediseño ni fullscreen.
No copiar código copyleft ni elegir licencia del producto sin autorización expresa.
Domain no referencia infraestructura ni UI. Operaciones críticas atómicas; efectos físicos posteriores al commit.
No modificar migraciones aplicadas: agregar una nueva. Nunca editar manualmente producción.
Importes MXN en centavos Int64 checked; costos fraccionarios requieren política decimal explícita.
No borrar ventas completadas ni movimientos; usar reversos trazables.
Actualizar estado, próximos pasos y evidencia después de cada etapa. No declarar pruebas no ejecutadas.
Antes de cerrar una modificación de producto: scripts/verify.ps1, revisión del diff y documentación actualizada. Para cierres exclusivamente documentales, verificar diff y que src/tests/migrations permanezcan intactos; no repetir suites costosas sin causa.

## Builds de revisión manual — regla permanente

Todo build destinado a revisión manual debe tener identificador inequívoco de desarrollo, carpeta de salida y launcher específicos. Verificar launcher → ejecutable/DLL, DataDirectory, hashes y título visible antes de entregarlo. No depender del acceso anclado de Windows.
Después de terminar pruebas, documentación y checkpoint autorizado, ejecutar automáticamente ese launcher exacto y dejar la aplicación abierta para el usuario, sin iniciar sesión ni realizar operaciones o imprimir. Esta apertura no equivale a aprobación humana del smoke.
La seguridad de datos tiene prioridad: usar copia aislada cuando la base original deba permanecer inmutable; comprobar procesos/DirectoryLease y no matar instancias ni abrir concurrencia insegura. Si no puede abrirse con seguridad, detener la apertura y reportarlo. No cambiar configuración física de impresión. Consultar docs/review-builds.md.

Preferencia permanente del usuario: después de cada modificación entregar en la conversación el usuario y la contraseña de revisión correspondientes a la copia preparada. Verificar que correspondan a esa copia; no inventar ni restablecer credenciales y no almacenarlas en documentación versionada.


