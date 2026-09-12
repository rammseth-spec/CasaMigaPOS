CASA MIGA POS — FASE 1G
BLOQUE F — REIMPRESIÓN DESDE HISTORIAL Y CIERRE OPERATIVO DE TICKETS

MODELO RECOMENDADO:
SOL.

No usar Astra salvo que aparezca un bug serio de:
- persistencia;
- concurrencia;
- Winspool;
- idempotencia;
- corrupción del snapshot;
- comportamiento impredecible del driver.

==================================================
DECISIÓN FORMAL SOBRE BLOQUE E.1
==================================================

La investigación del margen superior queda APROBADA.

Decisión:

ACEPTAR EL MARGEN SUPERIOR ACTUAL.

NO crear una variante recortada del logo.

NO eliminar las ocho filas blancas superiores.

NO utilizar backfeed, reverse feed ni comandos equivalentes.

Motivo:

- Casa Miga POS genera cero alimentación antes del raster.
- GS v 0 comienza en offset 6.
- Antes sólo existen:
  ESC t 2
  ESC a 0
- El PNG aporta aproximadamente 1.00 mm de blanco.
- La mayor parte del espacio observado corresponde por inferencia
  a la posición física cabezal/cortador de la Qian.
- No existe documentación específica Qian que autorice backfeed.
- El beneficio de recortar 8 filas sería prácticamente irrelevante.

El logo maestro Opción 07 queda INTACTO.

==================================================
BASELINE FÍSICO APROBADO
==================================================

Impresora:

Qian QOP-T80UL-RI-02

Cola:
POS-80

Puerto:
USB001

Driver:
POS-80 11.3.0.1

Baseline aprobado:

- logo maestro Opción 07;
- bitmap oficial 560 × 262;
- GS v 0;
- centrado en lienzo de 576 puntos;
- CP850 / ibm850;
- ESC t 2;
- español correcto;
- 48 columnas;
- cuerpo legible;
- TOTAL doble altura;
- importes alineados;
- corte parcial automático;
- GS V 1;
- margen superior aceptado;
- openCashDrawer = false.

Suite vigente:

191 aprobadas
0 fallidas
0 omitidas

==================================================
OBJETIVO DEL BLOQUE F
==================================================

Completar la operación de tickets desde el Historial de ventas.

Implementar:

1. Reimpresión desde una venta confirmada.
2. Permiso sales.receipt.reprint.
3. Diferenciación clara entre ORIGINAL y REIMPRESIÓN.
4. Reutilización del snapshot persistido.
5. Trazabilidad completa del request/evento.
6. Nunca abrir cajón durante una reimpresión.
7. No modificar Sale, SaleItem ni Payment.

==================================================
REIMPRESIÓN DESDE HISTORIAL
==================================================

En la pantalla de detalle de una venta agregar:

REIMPRIMIR TICKET

Sólo visible/habilitado cuando:

- usuario activo;
- permiso sales.receipt.reprint.

Roles actuales:

Administrator: sí
Manager: sí
Cashier: no

Application debe volver a validar el permiso.

No confiar únicamente en visibilidad de UI.

==================================================
SNAPSHOT
==================================================

Usar el receipt_snapshot persistido.

Una reimpresión NO debe reconstruirse desde:

- catálogo actual;
- precios actuales;
- configuración comercial nueva.

Debe conservar el contenido comercial persistido.

Si una venta anterior a 1G todavía no tiene snapshot:

usar la política ya aprobada para crear el primer snapshot
una sola vez y persistirlo.

No modificar snapshots existentes.

==================================================
IDENTIFICACIÓN VISUAL
==================================================

Una reimpresión debe identificarse de forma evidente.

Mostrar:

REIMPRESIÓN

en una posición clara del ticket.

NO alterar los datos financieros originales.

Preservar:

- logo Opción 07;
- folio;
- productos;
- precios;
- total;
- efectivo;
- cambio;
- diseño general aprobado.

Puede añadirse únicamente la marca de reimpresión.

==================================================
CAJÓN
==================================================

REGLA ABSOLUTA:

UNA REIMPRESIÓN NUNCA ABRE EL CAJÓN.

Mantener:

openCashDrawer = false

para cualquier REPRINT.

Cero ESC p.

El usuario todavía no dispone de cajón físico.

No bloquear FASE 1G por eso.

==================================================
SOLICITUDES DE IMPRESIÓN
==================================================

Cada clic válido genera exactamente un:

receipt_print_request

con:

kind = REPRINT

Debe registrar:

- SaleId;
- usuario;
- UTC;
- snapshot SHA-256;
- queueName;
- relación de request cuando corresponda;
- eventos posteriores.

No crear otra venta.

No crear Payment.

No cambiar ExpectedCash.

==================================================
DOBLE CLIC / REPETICIONES
==================================================

Evitar reimpresiones accidentales múltiples.

Mientras exista una solicitud activa:

- deshabilitar botón;
- mostrar estado.

Un clic debe generar un único request.

No implementar reintentos automáticos invisibles.

==================================================
ESTADOS
==================================================

Conservar:

REQUESTED
SUBMITTED
COMPLETED
FAILED
UNKNOWN

No afirmar:

"Impreso físicamente"

solamente porque Winspool aceptó el trabajo.

Mensajes UI deben diferenciar:

- enviado a cola;
- fallo;
- resultado incierto.

==================================================
FALLO DE REIMPRESIÓN
==================================================

Un fallo:

NO modifica:

- Sale;
- SaleItem;
- Payment;
- folio;
- ExpectedCash;
- caja.

Debe permitir un nuevo intento controlado.

Si el resultado queda UNKNOWN:

advertir al operador que revise físicamente antes de reintentar.

==================================================
TICKET ORIGINAL
==================================================

No alterar el comportamiento ya implementado del ticket ORIGINAL.

Confirmar mediante pruebas que:

- sigue ocurriendo sólo post-commit;
- un fallo de impresión no revierte venta;
- no crea doble cobro;
- reimpresión no afecta al original.

==================================================
UI
==================================================

Mantener el estilo funcional actual.

NO iniciar rediseño profesional.

En detalle de Historial:

- botón Reimprimir ticket;
- estado de impresión;
- feedback claro;
- deshabilitación durante trabajo activo.

No implementar F11/fullscreen.

==================================================
PRUEBAS
==================================================

Agregar pruebas para:

- Administrator puede reimprimir.
- Manager puede reimprimir.
- Cashier no.
- Usuario inactivo no.
- Permiso revocado no.
- Snapshot existente se reutiliza.
- Snapshot no cambia.
- REIMPRESIÓN aparece.
- Datos financieros permanecen idénticos.
- Logo Opción 07 permanece.
- PC850 permanece.
- Corte parcial permanece.
- Cero cajón.
- Un clic = un request.
- Doble ejecución bloqueada.
- FAILED no modifica venta.
- UNKNOWN no modifica venta.
- Reintento no crea venta nueva.
- Sale/Payment/ExpectedCash intactos.
- Backup/restore conserva trazabilidad.

Preservar toda la suite anterior.

==================================================
SMOKE FÍSICO
==================================================

Después de implementar y verificar:

NO imprimir automáticamente varias copias.

Realizar UN SOLO smoke controlado de reimpresión desde Historial
cuando todo esté listo.

Usar una venta sintética o de prueba apropiada.

No generar una nueva venta sólo para probar reimpresión si ya existe
una adecuada.

La impresión debe:

- llevar logo Opción 07;
- llevar REIMPRESIÓN;
- mantener español;
- mantener layout;
- cortar parcialmente;
- NO abrir cajón.

Después:

DETENERSE.

Pedir foto al usuario.

==================================================
NO IMPLEMENTAR
==================================================

NO iniciar:

- cajón físico;
- LAN;
- inventario;
- recetas;
- producción;
- facturación;
- QR fiscal;
- rediseño global;
- F11;
- instalador;
- FASE 1H.

==================================================
CIERRE DE BLOQUE E.1
==================================================

Actualizar documentación indicando:

- margen superior aceptado;
- causa predominantemente mecánica;
- logo no recortado;
- backfeed prohibido sin documentación Qian;
- Opción 07 preservada intacta.

==================================================
REPORTE FINAL OBLIGATORIO PARA CHATGPT
==================================================

AL TERMINAR EL BLOQUE F ENTREGAR UN REPORTE COMPLETO,
AUTOCONTENIDO Y LISTO PARA QUE EL USUARIO LO PEGUE
DIRECTAMENTE EN CHATGPT.

Debe incluir:

1. Estado exacto de FASE 1G.
2. Confirmación de cierre de E.1.
3. HEAD inicial/final.
4. Commits.
5. Estado Git.
6. Archivos modificados.
7. UI implementada.
8. Permiso sales.receipt.reprint.
9. Roles finales.
10. Flujo exacto de reimpresión.
11. Política de snapshot.
12. Identificación REIMPRESIÓN.
13. Confirmación de logo Opción 07.
14. Confirmación de CP850.
15. Confirmación de corte parcial.
16. Confirmación de cero cajón.
17. Estados de impresión.
18. Protección contra doble solicitud.
19. Comportamiento FAILED/UNKNOWN.
20. Pruebas focalizadas.
21. Suite completa:
    - aprobadas;
    - fallidas;
    - omitidas.
22. Build:
    - errores;
    - warnings.
23. Backup/restore.
24. Resultado del smoke.
25. PrintRequestId.
26. JobId.
27. Estado Winspool.
28. Confirmación de ninguna mutación financiera.
29. Qué requiere validación humana.
30. Solicitud explícita de foto del ticket de REIMPRESIÓN.
31. Pendientes restantes para cerrar FASE 1G.
32. Recomendación del modelo Codex para el siguiente bloque:
    SOL o ASTRA, con explicación.

IMPORTANTE:

EL REPORTE DEBE ESTAR PREPARADO PARA QUE EL USUARIO
LO PEGUE DIRECTAMENTE EN CHATGPT.

DETENERSE DESPUÉS DEL REPORTE.

NO INICIAR OTRA FASE.
NO CONTINUAR AUTOMÁTICAMENTE.