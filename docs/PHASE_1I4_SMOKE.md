# Smoke humano WPF 1I.4 — PENDIENTE

Aceptación manual: ninguna observación declarada todavía. No cerrar 1I.4 ni FASE 1I. No iniciar 1I.5.

Aplicación: artifacts/phase1i4-smoke/CasaMiga.Presentation.exe.
Datos aislados: work/phase1i4-smoke-20260913/valid y /invalid. Credencial compartida exclusivamente sintética: work/phase1i4-smoke-20260913/synthetic-credential.txt. Usuarios SMOKEADMIN, SMOKEMANAGER y SMOKECASHIER.

Los lanzadores Abrir_valido.cmd y Abrir_bloqueado.cmd de esa carpeta fijan CASAMIGA_DATA_DIR y DOTNET_ROOT. Cerrar la aplicación anterior antes de cambiar de entorno. No lanzar el ejecutable directamente sin el lanzador, para evitar usar el directorio habitual.

Preparación técnica verificada: ambos schema 12, DISABLED/v1, sin venta, pago, caja ni configuración de impresora. Valid contiene Concha sintética asociada a artículo activo con Opening Balance de cero; preflight verde. Invalid contiene Concha sin asociación con tracking activo; preflight bloqueado. No se activó durante preparación.

## Entorno válido — Administrator

1. Iniciar sesión con SMOKEADMIN y abrir Inventario.
2. Confirmar estado DESACTIVADO y explicación.
3. Pulsar Verificar configuración.
4. Observar resultado válido sin bloqueadores.
5. Pulsar Activar y aceptar la confirmación sobre descuento de existencias.
6. Confirmar ACTIVO; sólo aparece Pausar como transición.
7. Pulsar Pausar y aceptar la advertencia de ventas sin descuento.
8. Confirmar PAUSADO; sólo aparece Reanudar como transición.
9. Pulsar Reanudar y aceptar confirmación.
10. Confirmar ACTIVO nuevamente.

## Roles

11. Salir de sesión e ingresar SMOKEMANAGER: puede entrar a Inventario y ver ACTIVO, pero no aparecen Verificar configuración/Activar/Pausar/Reanudar.
12. Salir e ingresar SMOKECASHIER: no aparece acceso a Inventario ni nuevas acciones administrativas.

## Entorno bloqueado

13. Cerrar la aplicación y usar Abrir_bloqueado.cmd. Ingresar SMOKEADMIN, Inventario, Verificar configuración: debe explicar que Concha sin asociación controla inventario pero no tiene artículo asociado.
14. Pulsar Activar y aceptar confirmación: debe seguir DESACTIVADO y mostrar el bloqueo comprensible. No presentar IDs, SQL ni trazas.

No abrir caja, cobrar, imprimir ni modificar datos reales. No realizar smoke integral de venta/backup/cierre: pertenece a 1I.5. Informar resultado por roles y caso bloqueado, o texto exacto del fallo; sólo después registrar aceptación humana.
