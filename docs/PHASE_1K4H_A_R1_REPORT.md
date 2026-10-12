# FASE 1K.4H-A-R1 — Refinamiento del Reporte Diario y F11

Fecha técnica: 10/10/2026
Baseline: `ce663694074a792f59db063c75962f8b17f38ae8`
Commit autorizado: `fix: refine daily report usability for phase 1K.4H-A`

## Resultado

El Reporte Diario conserva su carácter de sólo lectura y ahora añade información operativa nueva: importe vendido por hora local, primera venta, última venta y hora pico. La jornada de caja muestra apertura, cierre, duración y responsable con lenguaje humano. El control global de pantalla completa explica su acción tanto en ventana como en F11.

La pantalla mantiene exactamente cuatro KPI superiores: Ventas, Tickets, Piezas y Ticket promedio. La antigua tarjeta duplicada «Lectura del día» se sustituyó por un único gráfico nativo de barras verticales. No se agregaron gráficas, KPI, librerías ni colores nuevos.

## Datos y semántica

- La consulta de timeline está limitada al intervalo UTC correspondiente a `[inicio local, día siguiente)`.
- Se consideran exclusivamente ventas formales con pago `CASH`; el importe usa `payment.applied_cents`.
- Una consulta obtiene `confirmed_utc` e importe agregado por venta; Application convierte cada timestamp con el `TimeZoneInfo` inyectado y agrupa por hora local.
- El reporte ejecuta cuatro consultas acotadas: timeline financiero, piezas, top de productos y sesiones de caja.
- No hay N+1, historia fuera del día ni 24 consultas horarias.
- La hora pico maximiza importe; desempata por número de tickets y después por hora más temprana.
- Primera y última venta conservan timestamps reales y se muestran en `HH:mm` local.
- Reimpresión no participa en los buckets y no altera ventas, pagos, caja, inventario, producción ni auditoría.
- El top de productos continúa usando snapshots históricos; únicamente el encabezado visible cambió de «Producto histórico» a «Producto».

## Presentación y acabado visual

- Recursos reutilizados: `PrimaryBrush` (Pan Artesanal), `BackgroundBrush`/`SurfaceBrush`/`SurfaceSecondaryBrush` (Fondo Natural y superficies), `WheatBrush`/`BorderBrush` (Trigo Suave), `AvocadoLeafBrush` (verde operativo) y `ErrorBrush` para faltantes.
- El orden visual es fecha/estado, cuatro KPI iguales, caja, productos + ventas por hora y, al final, bloques futuros.
- Grid: cuatro columnas uniformes para KPI, cinco métricas compactas de caja, relación 2:3 para tabla/gráfico y tres columnas para futuros. El contenido limita su ancho a 1480 DIP.
- Se redujo texto repetido y se conservó una sola frase resumen del día.
- Caja usa estado textual más acento discreto; una sesión se presenta compacta y varias sesiones conservan todos sus datos.
- Apertura y cierre son visibles; una sesión abierta dice «En curso» y conserva Contado/Diferencia como Pendiente. Una sesión cerrada muestra duración humana.
- El gráfico usa barras verdes de la paleta existente, ejes mínimos, etiquetas selectivas y descripción accesible por bucket con hora, importe y tickets.
- En día vacío el gráfico se oculta y aparece «No hubo ventas registradas este día» sin rectángulos vacíos innecesarios.
- La tabla conserva columnas `#`, `Producto`, `Piezas`, `Importe`, separadores discretos y snapshots históricos.
- Producción: «Completa las recetas para habilitar el resumen de producción.»
- Materias primas: «Se habilitará al configurar recetas e inventario.»
- Preparación para mañana: «Se habilitará al completar recetas, inventario y reservas.»

## Pantalla completa

- Ventana: `Pantalla completa · F11` con icono vectorial de expandir.
- F11: `Salir de pantalla completa · F11` con icono vectorial de restaurar.
- Clic y tecla F11 actualizan texto, icono, `ToolTip` y `AutomationProperties.Name` inmediatamente.
- Escape conserva la prioridad aprobada y sale de fullscreen cuando corresponde.
- Revisión a 1280×820, 1920×1080, 3440×1440 y F11: sin scroll horizontal global, sin clipping ni solapes de reloj, caja o Centro operativo. En ultrawide el ancho máximo evita tarjetas excesivamente largas.

## Caja

- Apertura: `Apertura: HH:mm`.
- Cierre cerrado: `Cierre: HH:mm`; abierto: `Cierre: En curso`.
- Duración cerrada: horas y minutos reales; abierta: `Abierta desde HH:mm · En curso`.
- Responsable humano y, si difiere, responsable de cierre; ningún GUID visible.
- Una sesión usa composición compacta. Varias sesiones muestran conteo y lista completa.
- Fondo inicial, efectivo vendido, esperado, contado y diferencia mantienen la semántica anterior.
- Exacta, sobrante, faltante y nota siguen visibles; caja abierta nunca muestra contado o diferencia falsamente en cero.

## Verificación

- Focal general Reporte Diario + shell: 7/7 tras actualizar el contrato del tercer icono vectorial.
- Focal WPF Reporte Diario: 6/6.
- Regresión WPF ampliada de Reporte Diario, Centro operativo, Escape y Caja: 23/23.
- Suite oficial final: 582/582 generales + 212/212 WPF = 794/794.
- Fallidas: 0. Omitidas: 0.
- Release: 0 errores, 0 advertencias.
- `git diff --check`: correcto.
- Schema de fixture: 17. Migración 018: no existe.
- Base protegida antes/después: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.

## Build de revisión

- EXE: `src/CasaMiga.Presentation/bin/Phase1K4H-A-R1/CasaMiga.Presentation.exe`
- Launcher: `work/Abrir-Casa-Miga-1K4H-A-R1.cmd`
- DataDirectory: `work/phase1k4h-a-r1-smoke-20261010`
- Título: `Casa Miga POS · Caja · DESARROLLO · 1K.4H-A-R1`
- Usuario verificado: `catalog-admin`
- La contraseña se entrega sólo en la conversación y no se versiona.

La fixture aislada contiene seis ventas, seis pagos, cinco sesiones y cero solicitudes/eventos de impresión. No existe `printing.json` en esa copia. El día 10/10/2026 contiene dos tickets, cinco piezas, $70.00, primera venta 09:00, última 17:00 y pico 09:00–09:59.

## Límites confirmados

No se modificaron Ticket, ESC/POS, Qian, `printing.json`, Operations Hub, recetas comerciales, escrituras de Production o Inventory, schema ni migraciones. No se imprimió ni abrió cajón. No se inició 1K.4G, 1K.4H-B, Premium UI ni 1K.5.

## Smoke humano pendiente

Revisar el launcher identificado y seguir `docs/NEXT_STEPS.md`. La apertura automática queda en login y no constituye aprobación humana.
