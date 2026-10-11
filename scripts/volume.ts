// Prueba de volumen de la nube (docs/VOLUME.md): un año de operación de una sucursal (por omisión 300 ventas al día) subido como lo
// sube la caja, y lo que tarda cada pantalla de la web por HTTP. Base PGlite en memoria: sirve para comparar, no es Neon.
// Uso: node --import tsx scripts/volume.ts [ventas por día] [días]
import { randomUUID } from 'node:crypto';
import { performance } from 'node:perf_hooks';
import { startLocalServer } from './localServer.js';
import { caja1, centro, database, permissions, send, user } from '../test/helpers.js';

const perDay = Number(process.argv[2] ?? 300);
const days = Number(process.argv[3] ?? 365);
const { q } = await database();
await permissions(q, caja1, centro);
const admin = 'a0000000-0000-4000-8000-000000000001';
await user(q, caja1, centro, admin, 'admin', 'Administrator', 'Admin#2026centro');

const categories = Array.from({ length: 6 }, (_, i) => ({ id: randomUUID(), name: `Categoría ${i + 1}` }));
const products = Array.from({ length: 60 }, (_, i) => ({ id: randomUUID(), name: `Pan ${String(i + 1).padStart(2, '0')}`, price: 800 + (i % 12) * 150, category: categories[i % 6].id }));
await send(q, caja1, centro, [
  ...categories.map((c, i) => ({ entity: 'category', id: c.id, payload: { id: c.id, name: c.name, description: '', active: 1, sort_order: i } })),
  ...products.map(p => ({ entity: 'product', id: p.id, payload: { product: { id: p.id, sku: null, name: p.name, category_id: p.category, description: '', price_cents: p.price, track_inventory: 0, active: 1 }, inventoryLinks: [] } })),
]);

// Datos repetibles: el mismo «azar» en cada corrida.
let seed = 7;
const random = () => (seed = (seed * 1_103_515_245 + 12_345) % 2_147_483_648) / 2_147_483_648;
const today = new Date(); today.setUTCHours(0, 0, 0, 0);
const started = performance.now();
let sequence = 0, sales = 0, entries: { entity: string; id: string; payload: unknown; change?: 'insert' | 'update' }[] = [];
const flush = async (force = false) => { if (entries.length >= 1500 || (force && entries.length)) { await send(q, caja1, centro, entries); entries = []; } };
for (let d = days - 1; d >= 0; d--) {
  const day = new Date(today.getTime() - d * 86_400_000);
  const at = (minutes: number) => new Date(day.getTime() + (13 * 60 + minutes) * 60_000).toISOString(); // 7:00 de la mañana en México
  const session = randomUUID();
  let cash = 0;
  entries.push({ entity: 'cash_session', id: session, payload: { id: session, terminal_id: caja1, opened_by: admin, opened_utc: at(0), opening_cents: 50_000, state: 'OPEN' } });
  for (let s = 0; s < perDay; s++) {
    const id = randomUUID();
    const lines = Array.from({ length: 1 + Math.floor(random() * 4) }, () => products[Math.floor(random() * products.length)])
      .filter((p, i, all) => all.findIndex(x => x.id === p.id) === i)
      .map((p, i) => { const quantity = 1 + Math.floor(random() * 3); return { sale_id: id, position: i + 1, product_id: p.id, product_name: p.name, unit_price_cents: p.price, quantity, line_total_cents: p.price * quantity }; });
    const total = lines.reduce((a, l) => a + l.line_total_cents, 0);
    const r = random(); const method = r < 0.85 ? 'CASH' : r < 0.95 ? 'CARD' : 'TRANSFER';
    if (method === 'CASH') cash += total;
    const when = at(Math.floor((s / perDay) * 720));
    entries.push({ entity: 'sale', id, payload: {
      sale: { id, operation_id: randomUUID(), user_id: admin, terminal_id: caja1, cash_session_id: session, sequence: ++sequence, confirmed_utc: when, subtotal_cents: total, total_cents: total },
      items: lines, payment: { id: randomUUID(), sale_id: id, method, applied_cents: total, received_cents: total, change_cents: 0, reference: method === 'CASH' ? null : '4242' } } });
    if (random() < 0.005) {
      const refund = randomUUID();
      entries.push({ entity: 'sale_refund', id: refund, payload: {
        refund: { id: refund, operation_id: randomUUID(), sale_id: id, kind: 'RETURN', reason: 'Volumen', user_id: admin, terminal_id: caja1, cash_session_id: session, refunded_utc: when, total_cents: lines[0].unit_price_cents },
        items: [{ refund_id: refund, sale_id: id, position: 1, quantity: 1, line_total_cents: lines[0].unit_price_cents }] } });
    }
    sales++;
    await flush();
  }
  const closed = at(730), expected = 50_000 + cash;
  entries.push({ entity: 'cash_count', id: session, payload: { session_id: session, counted_by: admin, counted_utc: closed, counted_cents: expected } });
  entries.push({ entity: 'cash_session', id: session, change: 'update', payload: { id: session, terminal_id: caja1, opened_by: admin, opened_utc: at(0), opening_cents: 50_000, state: 'CLOSED',
    closed_utc: closed, closed_by: admin, expected_cents: expected, counted_cents: expected, difference_cents: 0 } });
}
await flush(true);
console.log(`${sales.toLocaleString('es-MX')} ventas de ${days} días cargadas en ${((performance.now() - started) / 1000).toFixed(0)} s`);

// Consultas lentas (más de 150 ms), para saber qué optimizar.
const slow = new Map<string, { count: number; ms: number }>();
// PGlite atiende una consulta a la vez: se forman en fila para medir sólo lo que tarda cada una, no la espera.
let queue: Promise<unknown> = Promise.resolve();
const timed: typeof q = (text, params) => {
  const run = queue.then(async () => {
    const t = performance.now(); const result = await q(text, params); const ms = performance.now() - t;
    if (ms > 150) { const flat = text.replace(/\s+/g, ' '); const key = flat.length > 300 ? `${flat.slice(0, 90)} … ${flat.slice(-200)}` : flat; const x = slow.get(key) ?? { count: 0, ms: 0 }; x.count++; x.ms += ms; slow.set(key, x); }
    return result;
  });
  queue = run.catch(() => undefined);
  return run;
};
const { server, url } = await startLocalServer(timed, { noLogin: true });
const cookie = (await fetch(`${url}/api/web/dev-login`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ username: 'admin' }) }))
  .headers.get('set-cookie')!.split(';')[0];
const iso = (offset: number) => new Date(today.getTime() - offset * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'America/Mexico_City' });
const ranges: [string, string][] = [['hoy', `from=${iso(0)}&to=${iso(0)}`], ['30 días', `from=${iso(29)}&to=${iso(0)}`], ['92 días (máximo)', `from=${iso(91)}&to=${iso(0)}`]];
const screens: [string, string, boolean][] = [
  ['Reportes · Resumen', 'report', true], ['Reportes · Comparativo', 'compare', true], ['Reportes · Ventas por hora', 'heatmap', true],
  ['Reportes · Control de caja', 'cash-control', true], ['Reportes · Ganancia', 'profit', true], ['Inicio (tablero)', 'dashboard', true],
  ['Ventas (historial, 1.ª página)', 'sales', true], ['Catálogo', 'catalog', false], ['Usuarios', 'users', false], ['Producción · pronóstico', 'forecast', false],
];
const rows: string[] = [];
for (const [label, op, ranged] of screens) {
  for (const [name, query] of ranged ? ranges : [['—', ''] as [string, string]]) {
    const t = performance.now();
    const response = await fetch(`${url}/api/web/${op}?branch=${centro}${query ? '&' + query : ''}`, { headers: { cookie } });
    const body = await response.text();
    const ms = performance.now() - t;
    rows.push(`| ${label} | ${name} | ${response.status} | ${ms.toFixed(0)} ms | ${(body.length / 1024).toFixed(0)} KB |`);
  }
}
console.log('\n| Pantalla | Periodo | HTTP | Tiempo | Respuesta |\n|---|---|---|---|---|\n' + rows.join('\n'));
if (slow.size) console.log('\nConsultas de más de 150 ms:\n' + [...slow.entries()].sort((a, b) => b[1].ms - a[1].ms).slice(0, 12)
  .map(([sql, x]) => `- ${x.count}× ${Math.round(x.ms / x.count)} ms · ${sql}`).join('\n'));
server.close();
process.exit(0);
