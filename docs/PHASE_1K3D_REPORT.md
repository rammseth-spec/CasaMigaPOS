# FASE 1K.3D — reporte técnico

Fecha: 06/10/2026. Baseline: `3419cc32214036b28468c09c78845bda9138fee6` (`fix: harden contextual help window lifecycle`). Estado inicial: rama `master`, árbol limpio, sin remoto configurado y sin instancia de Casa Miga abierta.

## Auditoría y semántica preservada

Producción ya operaba mediante un perfil productivo activo y publicado. Cada línea selecciona una versión exacta de perfil y una cantidad planeada entera positiva de piezas terminadas. `ProductionService.PrepareProduction` resuelve perfiles, fórmulas, alternativas, cantidades racionales exactas y stock; Presentation no reproduce ese cálculo. La vista previa es de sólo lectura y no reserva inventario.

Al crear el registro se persiste un borrador inmutable con productos, versiones, fórmulas, alternativas e insumos congelados. Al registrarlo, Application vuelve a validar permisos, revisión, artículos activos, saldos iniciales y faltantes dentro de una transacción. El consumo se calcula con la cantidad planeada; la entrada de producto terminado usa la cantidad real. Cero piezas reales conserva el consumo planeado y no crea una salida terminada. MASS/VOLUME/COUNT permanecen en Int64 atómico, con ledger inmutable e integración producción↔inventario atómica.

## Cambios de usabilidad

- Selección humana: producto, nombre del perfil y `Versión X`.
- Captura explícita de `Piezas planeadas`, con ContextHelp sobre significado e impacto.
- Vista previa estructurada en preparaciones e insumos; muestra cantidad requerida, disponible, faltante y estado textual `Suficiente`, `Faltante`, `Sin saldo inicial` o `Inactivo`.
- Preparaciones muestran su versión activa.
- Cantidades visibles reutilizan `InventoryDisplay`: g/kg, ml/L y pieza/piezas.
- Acción de borrador renombrada `Preparar registro de producción`; acción transaccional final `Registrar producción`.
- El detalle explica qué insumos se descontarán y qué piezas reales ingresarán.
- Cantidad real con ayuda contextual y comparación contra el plan. El botón queda deshabilitado si falta una cantidad o no es entero no negativo.
- `Busy` conserva la protección contra doble ejecución; Application conserva idempotencia y revisión.
- Historial y detalle usan frases humanas, fecha/hora local, resultado planeado/real y fecha de registro; no muestran GUID ni UTC.
- Se conservaron estados vacíos, selección requerida, scroll del shell, F11 y ContextHelp R3.

## Pruebas

Focales generales: 10/10. Cubren preview sin mutación, perfil/versión visible, unidades humanas, disponibilidad/faltante, captura real válida, doble llamada, cero salida, historial humano, stock insuficiente y atomicidad.

Focales WPF: 15/15. Cubren controles materializados a 1280×820 y 1920×1080, botones, estados vacíos, ContextHelp y reconciliación manual existente.

Suite oficial `scripts/verify.ps1`: 559/559 generales + 159/159 WPF = **718/718**, 0 fallidas, 0 omitidas. Release: 0 errores y 0 advertencias. `git diff --check`: correcto.

## Fixture y ejecución sintética

DataDirectory: `work/phase1k3d-smoke-20261006`. Todos los registros agregados por el tooling están marcados SMOKE/NO COMERCIAL. Perfil activo: `SMOKE-PROFILE-001 · Perfil sintético publicado · Versión 1`. Incluye MASS, VOLUME, COUNT, preparación, alternativa y producto terminado.

Saldos iniciales sintéticos:

| Artículo | Antes |
|---|---:|
| SMOKE-MASA-001 | 100 kg |
| SMOKE-VOL-001 | 100 L |
| SMOKE-COUNT-001 | 100 piezas |
| SMOKE-ALT-A | 10 kg |
| SMOKE-ALT-B | 10 kg |
| SMOKE-FIN-001 | 0 piezas |

La vista previa de 5 piezas produjo estos requerimientos sin mutar ninguna tabla ni saldo: 312.5 g de masa, 125 ml de volumen, 6 piezas y 5 g de alternativa A. Se creó el borrador y se registró una sola producción con 4 piezas reales.

Saldos posteriores:

| Artículo | Después |
|---|---:|
| SMOKE-MASA-001 | 99.6875 kg |
| SMOKE-VOL-001 | 99.875 L |
| SMOKE-COUNT-001 | 94 piezas |
| SMOKE-ALT-A | 9.995 kg |
| SMOKE-ALT-B | 10 kg |
| SMOKE-FIN-001 | 4 piezas |

El ledger agregó exactamente una operación `PRODUCTION_CONSUMPTION` y una `PRODUCTION_OUTPUT`. El historial contiene una producción confirmada con 5 planeadas y 4 reales.

Para el caso de faltante se creó un borrador sintético de 1,000 piezas. Requería 1,200 piezas de COUNT y sólo quedaban 94: faltaban 1,106. La confirmación devolvió `InsufficientStock`; antes/después permanecieron idénticos: 9 operaciones, 12 movimientos, una producción confirmada, saldos sin cambio y salida terminada en 4. El borrador visible se conserva para revisión.

La copia de origen ya contenía 16 ventas y 2 sesiones de caja. Esos conteos no cambiaron durante preview, ejecución ni faltante. No se ejecutó venta, cobro, apertura/cierre de caja o impresión.

## Integridad y alcance

- Base protegida inicial/final: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
- Schema 16; no existe migración 017.
- Domain, Application, Infrastructure y migraciones sin cambios.
- Sin recetas comerciales reales.
- Sin cambios a Qian, driver, puerto o configuración física.
- Código de producto modificado sólo en `ProductionView.xaml` y `ProductionViewModel.cs`.
- Regresiones modificadas en `RecipeProductionViewModelTests.cs`, `UsabilityPresentationTests.cs` y `ManualSmokeReconciliationTests.cs`.
- Este reporte, `CURRENT_STATE.md` y `NEXT_STEPS.md` documentan el cierre técnico.

## Entrega de revisión

- Build: `src/CasaMiga.Presentation/bin/Phase1K3D/`.
- Launcher: `work/Abrir-Casa-Miga-1K3D.cmd`.
- DataDirectory: `work/phase1k3d-smoke-20261006`.
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.3D`.
- Estado: técnicamente completada; smoke manual pendiente.
- Commit autorizado: `feat: improve production usability for phase 1K.3D`; el SHA final se entrega en conversación para evitar una referencia circular.

No se inicia Pedidos, Tiendas, migración 017, carga comercial, POS/Carrito/Historial/Caja/ticket ni otra fase.
