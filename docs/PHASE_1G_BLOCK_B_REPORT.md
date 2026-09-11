# FASE 1G Bloque B — Primer envío físico controlado

Fecha: 2026-09-11. Estado: **FASE 1G ABIERTA; envío técnico terminado y validación física humana pendiente**.

## Punto de partida y Git

- HEAD inicial: `93d6c105c2c6dbab65a7f411363f21bae3076e54`.
- El árbol inicial estaba limpio.
- Commit de implementación: `cd19686` (`feat: add controlled physical receipt print probe`).
- El HEAD final se registra al cerrar este reporte documental.
- No se hizo push ni se configuró remoto.

## Configuración local utilizada

`work/phase1g-block-b/printing.json` permanece ignorado por Git y fuera de `.cmbackup`. Contiene `enabled: true`, `queueName: POS-80`, `columns: 48`, `encodingName: us-ascii`, `cutMode: Disabled`, `openCashDrawer: false` y perfil `PRUEBA CASA MIGA`. Se eligió `us-ascii` para que esta primera prueba no dependa de una página de caracteres todavía no verificada.

Windows confirmó antes del envío la cola `POS-80`, puerto `USB001`, driver `POS-80 11.3.0.1` y estado `Normal`. Casa Miga usó exclusivamente Winspool RAW y la cola lógica; no abrió USB001 directamente ni utilizó LAN o puerto 9100.

## Trabajo único enviado

- PrintRequestId técnico: `7ba5b12f-2482-457d-abf4-725181b25d60`.
- Timestamp REQUESTED: `2026-09-11T18:58:21.8519943+00:00`.
- Timestamp SUBMITTED: `2026-09-11T18:58:21.9696303+00:00`.
- JobId de Winspool: `3`.
- Estado final técnico: `SUBMITTED`.
- Cola: `POS-80`.
- Bytes enviados: `762`.
- Error: ninguno informado por Winspool.

`SUBMITTED` sólo significa que Windows aceptó el trabajo. No se registró `COMPLETED` ni se afirma que el papel haya salido. La traza local contiene exactamente dos eventos, `REQUESTED` y `SUBMITTED`, y la evidencia técnica persistente está en `docs/evidence/phase1g-block-b-first-job.json`.

## Contenido exacto

```text
                PRUEBA CASA MIGA
              PRUEBA DE IMPRESION
------------------------------------------------
Folio: TEST-001
Fecha: 11/09/2026 18:58:21 +00:00
Cajero: PRUEBA
Terminal: DESARROLLO
------------------------------------------------
Producto prueba
1 x $19.00                                $19.00
------------------------------------------------
Subtotal                                  $19.00
TOTAL                                     $19.00
Pago                                        CASH
Efectivo                                  $20.00
Aplicado                                  $19.00
Cambio                                     $1.00
------------------------------------------------
             GRACIAS POR SU COMPRA
```

El payload fue validado antes de Winspool: sólo ASCII, máximo 48 columnas, sin byte ESC `0x1B`, sin byte GS `0x1D`, sin comando de corte y sin pulso de cajón. El mecanismo de mantenimiento no abre la base comercial, no crea Sale/Payment, no abre caja y no cambia ExpectedCash.

## Verificación automática

- Focalizadas `ReceiptTests`: 15 aprobadas, 0 fallidas, 0 omitidas.
- Suite completa: 185 aprobadas, 0 fallidas, 0 omitidas.
- Build Release: 0 errores, 0 advertencias.
- Las tres pruebas nuevas cubren un solo Submit y traza, rechazo previo con configuración deshabilitada y fallo conocido sin reintento.
- Las pruebas existentes conservan configuración estricta, impresión deshabilitada sin afectar POS, renderer determinista, ancho, ausencia de ESC/GS, trazabilidad comercial, fallo sin modificar venta y exclusión de `printing.json` del backup.

## Archivos del bloque

- `src/CasaMiga.Infrastructure/ControlledReceiptPrintTest.cs`: documento sintético, barreras ASCII y traza técnica.
- `src/CasaMiga.Maintenance/Program.cs`: comando explícito `print-test`.
- `tests/CasaMiga.Tests/ReceiptTests.cs`: pruebas focalizadas.
- `docs/evidence/phase1g-block-b-first-job.json`: resultado técnico del único envío.
- `docs/PHASE_1G_BLOCK_B_REPORT.md`, `docs/CURRENT_STATE.md`, `docs/NEXT_STEPS.md` y `docs/PHASE_1G_PHYSICAL_CHECKPOINT.md`: continuidad.
- `work/phase1g-block-b/printing.json` y `physical-print-test.jsonl`: configuración/traza locales ignoradas, no incluidas en backup.

No cambiaron Sale, SaleItem, Payment, CheckoutService, ExpectedCash, cierre de caja, historial, migraciones ni Presentation.

## Validación humana requerida

Se requiere una foto completa del ticket y respuesta a estas diez preguntas:

1. ¿Salió papel?
2. ¿El texto es legible?
3. ¿Está centrado correctamente?
4. ¿Se aprovecha el ancho del rollo?
5. ¿Los precios están alineados?
6. ¿Hay texto cortado por la derecha?
7. ¿Hay márgenes excesivos?
8. ¿Hay líneas superpuestas?
9. ¿La densidad se ve adecuada?
10. ¿El ticket termina correctamente?

También debe anotarse si hubo corte automático. El payload de Casa Miga no contiene orden de corte; si ocurrió, provino de la configuración global del driver o de la impresora.

## Riesgos y pendientes

La aceptación del spooler no demuestra salida física, legibilidad ni geometría. La página de caracteres real continúa sin confirmar. Permanecen pendientes codepage, prueba de español, logo, corte, cajón, reimpresión desde UI y LAN. No se debe ajustar el renderer sin la foto ni ejecutar otra impresión en este checkpoint.

Para el siguiente bloque se recomienda **SOL** porque lo inmediato es interpretar evidencia visual y hacer ajustes acotados. Cambiar a **ASTRA** sólo si aparece un defecto técnico real en Winspool RAW, driver POS-80, bytes, codepage o bloqueo del spooler.
