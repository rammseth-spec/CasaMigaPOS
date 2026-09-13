# Smoke humano WPF 1I.4 — COMPLETADO

Aceptación manual: capturas del usuario de DESACTIVADO, ACTIVO, PAUSADO, ACTIVO reanudado; Manager sin controles; Cashier identificado sin Inventario; preflight bloqueado. El usuario confirmó explícitamente que pulsó Activar y Sí y permaneció DESACTIVADO. FASE 1I no se cierra; 1I.5 no inicia.

Aplicación del smoke observado: artifacts/phase1i4-smoke/CasaMiga.Presentation.exe. Compilación final con subtítulo corregido: artifacts/phase1i4-final/CasaMiga.Presentation.exe; los lanzadores locales apuntan a ésta.
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

## Evidencia y alcance de observación

Los 14 pasos del flujo están completados por evidencia combinada. Estados y visibilidad de controles se observan directamente en capturas; autorización/transacciones/preflight autoritativo se verifican con pruebas automáticas. No hay captura individual de cada diálogo ni del verde informativo inicial; no se declara observación visual de esos detalles. La última confirmación humana SI acredita el intento rechazado tras Activar/Sí.

Capturas de referencia: dec7f9ed (activación), 27b0010c (pausa), 8c780b4c (reanudación), c6485cab (Manager), 95e145d2 (Cashier), acdd4423 (preflight inválido), 021e699a (rechazo confirmado). Adjuntas en la conversación; no se incluyen credenciales en evidencia versionada.

Correcciones de cierre: lanzadores locales .cmd reparados por rutas partidas; subtítulo general cambiado a texto neutral que remite al estado mostrado. Las capturas corresponden al build previo a esa corrección exclusivamente textual; no se solicita repetir transiciones. No se imprimió ni se abrió caja.
