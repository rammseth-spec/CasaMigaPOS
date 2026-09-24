# Smoke funcional FASE 1J.4

Fecha de cierre: 2026-09-24. Datos exclusivamente sintéticos en `work/phase1j4-smoke-20260922`, ignorados por Git. No se abrió caja, no se registraron ventas y no se utilizó impresora.

## Resultado humano

- Administrator creó `FORMULA-J14`, guardó el borrador, publicó la versión 1 y la activó.
- Configuró ingredientes sintéticos MASS/VOLUME y publicó/activó perfiles sintéticos A y B.
- Manager abrió la consulta de recetas sin controles de configuración y creó un plan multi-output A=2, B=3.
- El preview mostró una preparación agregada de 650 g y consumos de 325 g, 65 ml y 8 piezas, todos disponibles.
- El DRAFT mostró creador `Manager sintético`, terminal `CASA-MIGA-CAJA-01` y quedó bloqueado.
- Tras cerrar y volver a abrir la aplicación, el DRAFT reapareció.
- Se capturó B: planeado 3/real 4 y A: planeado 2/real 1; ambos casos se aceptaron.
- La confirmación mostró el mensaje `Producción registrada correctamente.` y el detalle quedó de sólo lectura.
- El historial mostró estado Confirmada, creador, terminal, productos y planeado/real.
- Cashier sólo vio Caja, Catálogo y POS; Recetas y Producción quedaron ocultas.

## Resultado técnico read-only

- Schema aplicado: 15 migraciones, máximo 15; no 016.
- `PRAGMA integrity_check`: `ok`.
- `PRAGMA foreign_key_check`: 0 filas.
- Un lote `CONFIRMED`, con salida A=1 y B=4.
- Consumo persistido: 325000 mg, 65000 µL y líneas COUNT 6+2; la UI las agregó como 325 g, 65 ml y 8 piezas.
- Saldos finales: 24.675 kg, 19.935 L, 192 piezas; terminado A=1 y B=4. El alternativo no elegido conservó 25 kg.
- Auditoría: `production.created=1`, `production.confirmed=1`, además de creación/guardado/publicación/activación de fórmula y perfiles.

Smoke aprobado por evidencia visual del usuario y cierre explícito de los bloques. La instalación sigue siendo de desarrollo.
