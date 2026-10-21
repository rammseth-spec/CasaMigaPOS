# ADR-024 — Nube y versión web, sin que la venta dependa de la red

Fecha de la decisión: 06–09/10/2026 (decisiones del usuario). Registrada el 20/10/2026: `docs/roadmap.md:56` pide un ADR para pasar a servidor, PostgreSQL, multiterminal o sucursales, y no se había escrito.

## Contexto

El pedido original (`docs/USER_REQUIREMENTS.md`) dejaba fuera de la primera etapa la sincronización distribuida, la nube compleja, varias sucursales y el uso en celular (líneas 1001 y 1007–1024). Esos temas iban a la fase 6 «solamente cuando sea necesario» (líneas 1107–1115).

## Decisión

El usuario decidió adelantarlos, en estas fechas:

- **06/10/2026** (`docs/SYNC_PLAN.md`, «Decisiones del usuario»):
  - vender sin Internet es obligatorio y la sincronización ocurre después, en automático;
  - habrá varias sucursales, un solo administrador para todas y cada cajero en una sucursal;
  - los precios se definen por sucursal y los roles se conservan;
  - la nube será Neon (PostgreSQL) y Vercel (API en TypeScript).
- **07/10/2026** (`docs/WEB.md`): una web que muestra lo mismo que la caja y permite editar precios, productos, usuarios y charolas.
- **08–09/10/2026** (`docs/POS_WEB.md`, `docs/PLAN_COMPLETAR.md`): venta completa desde la web, con caja web por dispositivo; sincronización cada 5 s.

## Consecuencias

- La base SQLite de cada caja sigue siendo la fuente de verdad. Cada cambio se anota en `sync_outbox` dentro de la misma transacción, y la nube lo aplica sin duplicar (identificadores UUID).
- Los cambios hechos en la web viajan como órdenes que la caja valida y aplica; mientras tanto se ven «por confirmar».
- La nube **no** es necesaria para vender. Hasta el 20/10/2026 **no está publicada**: no hay copia de los respaldos fuera del equipo.
- Siguen fuera, en espera de nueva autorización: vender desde la web sin Internet, impresora y cajón desde el navegador, y que las ventas web descuenten inventario.
- Limitaciones conocidas en `docs/SYNC_PLAN.md` y `docs/WEB.md`: un producto dado de alta en una caja no se reparte a otras cajas de la misma sucursal, y las tablas de charolas sólo viajan como auditoría.
