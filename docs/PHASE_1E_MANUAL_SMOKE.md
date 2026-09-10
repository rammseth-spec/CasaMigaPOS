# FASE 1E — comprobación manual pendiente

Entorno exclusivamente sintético preparado el 2026-09-10. No usar dinero ni datos reales. No modificar código, migraciones ni SQLite para completar el recorrido.

## Instalación preparada

Ejecutable: `artifacts/development/CasaMiga.Presentation.exe`, desde la raíz del repositorio. Su development.settings.json apunta a `work/phase1e-visual-20260910`, carpeta nueva, con fondo cero, dos productos y ninguna venta inicial. La instalación previa permanece intacta.

Usuario de prueba: `smoke`. Contraseña temporal aleatoria en `work/phase1e-visual-20260910/synthetic-credential.txt`, sólo para esta instalación sintética; no copiarla al reporte ni a Git. Si la aplicación sigue abierta en el diálogo, no hace falta volver a iniciar sesión.

## Recorrido

1. Si es necesario, iniciar ejecutable, hacer login y verificar CAJA ABIERTA. Entrar al POS.
2. Con carrito vacío, comprobar total $0.00 y Cobrar deshabilitado. Agregar dos Conchas de $19.00 y tres Bolillos de $5.00: dos líneas, total $53.00 y Cobrar habilitado. Si el carrito preparado ya contiene esas líneas, no volver a agregarlas.
3. Abrir Cobrar. Verificar total $53.00. Hacer clic directamente en Efectivo recibido y escribir `50.00`. Confirmar que no se permita completar: botón deshabilitado. No confundir campo vacío con rechazo de efectivo insuficiente.
4. Seleccionar el importe y reemplazar por `100.00`. Debe aparecer cambio $47.00 y Confirmar cobro habilitado. Registrar ambos resultados antes de continuar.
5. Confirmar una sola vez. Debe mostrarse Venta confirmada, folio, total $53.00, recibido $100.00 y cambio $47.00. Anotar folio; no repetir cobro si aparece resultado pendiente o error.
6. Pulsar Volver al POS para aceptar el resultado. Comprobar carrito vacío, total $0.00 y Cobrar deshabilitado.
7. Cerrar normalmente y reabrir. Iniciar sesión con las mismas credenciales. Confirmar carrito vacío y ausencia de aviso de cobro pendiente. El archivo checkout.pending debe estar ausente después de aceptar el resultado.
8. En Caja, pulsar Cerrar caja. Capturar contado `53.00` y Continuar. Debe mostrar esperado $53.00 y diferencia $0.00, porque el fondo inicial es cero y sólo hubo una venta de $53.00.
9. Confirmar cierre mediante su botón separado. Comprobar CAJA CERRADA. No abrir otra sesión ni repetir venta en este recorrido.
10. Informar resultados, folio, cualquier error exacto y si se necesitó intervención manual en campos/botones. Si hay un resultado distinto, detenerse y conservar la instalación para revisión.

## Comprobaciones que hará Codex en sólo lectura

Una venta, dos partidas, un pago y una auditoría sale.completed; total/aplicado 5300, recibido 10000, cambio 4700 centavos; mismo folio/UUID después del reinicio; ausencia de checkout.pending aceptado; caja CLOSED con esperado y contado 5300 y diferencia cero. No sustituir esta comprobación por insertar datos desde scripts.

La preparación de usuario/catálogo/caja empleó servicios existentes antes del smoke; no creó ventas. Las pruebas automatizadas previas no sustituyen la aceptación WPF de los pasos anteriores.
