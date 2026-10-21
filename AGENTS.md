# Continuidad

Lee README.md, docs/CURRENT_STATE.md, docs/NEXT_STEPS.md y docs/PLAN_SEGUIMIENTO.md antes de trabajar. Luego sólo los ADRs y archivos pertinentes.
Estado vigente (20/10/2026): `main` es la versión 1.0 candidata, en piloto.
- Ya tiene, autorizado por el usuario: cancelaciones, devoluciones, tarjeta y transferencia, inventario, recetas y producción, rediseño, pantalla completa, reimpresión, nube y web.
- No implementar sin nueva autorización: descuentos, fiscalidad, cajón físico, impresión por LAN, pedidos, compras ni pagos mixtos.
- El seguimiento del plan y sus pendientes están en docs/PLAN_SEGUIMIENTO.md. Las fases anteriores son historia en docs/roadmap.md.
Caja con ventas reales: datos en `C:\Users\ramms\CasaMiga-Revision\local-main-c2fb393\caja`, programa en `C:\Users\ramms\CasaMiga-Ventas\programa`. Nunca usarla para pruebas, cargas ni scripts de desarrollo. Actualizarla sólo con autorización del usuario y siguiendo docs/ACTUALIZAR_CAJA.md. No cerrar su caja ni registrar conteos o movimientos sin las cantidades reales del usuario.
Recetas: no publicar recetas incompletas ni convertir rendimientos históricos, aproximados o inconsistentes en datos confirmados. Estado en docs/RECETAS_ESTADO.md.
Al terminar cada bloque, entregar el reporte completo en el chat y guardarlo en docs/reportes/.
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


