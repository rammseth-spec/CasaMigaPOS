import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ProtocolError } from '../lib/protocol.ts';
import { dashboard, parseRange, type Query } from '../lib/panel.ts';
import { caja1, centro, database, norte, seedSales, send, sessionA, sessionB } from './helpers.ts';

const range = (from: string, to: string, branch?: string) =>
  parseRange(new URL(`https://x/api/web/dashboard?from=${from}&to=${to}${branch ? `&branch=${branch}` : ''}`));

test('dashboard groups sales by local day, method and branch, net of refunds', async () => {
  const { q } = await database(); await seedSales(q);
  const all = await dashboard(q, range('2026-10-06', '2026-10-07'));
  assert.equal(all.totals.tickets, 6);
  assert.equal(all.totals.gross, 3800 + 2600 + 1900 + 3000 + 5700 + 25000);
  assert.equal(all.totals.refunds, 1900);
  assert.equal(all.totals.net, all.totals.gross - 1900);
  assert.deepEqual(all.totals.byMethod, { cash: 3800 + 1900 + 5700 + 25000 - 1900, card: 2600, transfer: 3000 });

  const centroDays = all.days.filter(d => d.branchId === centro);
  assert.deepEqual(centroDays.map(d => [d.day, d.tickets, d.gross, d.refunds]), [['2026-10-06', 3, 8300, 1900], ['2026-10-07', 2, 8700, 0]]);
  assert.deepEqual(all.branches.map(b => b.name), ['Centro', 'Norte']);
  assert.deepEqual(all.products[0], { name: 'Rosca', quantity: 1, total: 25000 });
  assert.deepEqual(all.products.find(p => p.name === 'Concha'), { name: 'Concha', quantity: 6, total: 11400 });

  const onlyNorte = await dashboard(q, range('2026-10-06', '2026-10-07', norte));
  assert.equal(onlyNorte.totals.tickets, 1);
  assert.equal(onlyNorte.sessions.length, 0);
  assert.deepEqual(onlyNorte.terminals.map(t => t.name), ['CASA-MIGA-CAJA-02']);
  assert.equal(onlyNorte.terminals[0].lastSeenAt === null, false, 'Receiving a batch updates last contact.');
});

test('cortes show recorded result when closed and running expected cash with movements when open', async () => {
  const { q } = await database(); await seedSales(q);
  const data = await dashboard(q, range('2026-10-06', '2026-10-07', centro));
  const [open, closed] = data.sessions;
  assert.equal(closed.id, sessionA); assert.equal(closed.open, false);
  assert.equal(closed.expected, 53700); assert.equal(closed.counted, 51700); assert.equal(closed.difference, -2000);
  assert.equal(closed.cashSales, 3800 + 1900); assert.equal(closed.cashRefunds, 1900);
  assert.equal(open.id, sessionB); assert.equal(open.open, true);
  assert.equal(open.movements, 10000 - 4550);
  assert.equal(open.expected, 20000 + 5700 + 10000 - 4550);
  assert.equal(data.totals.shortages, -2000); assert.equal(data.totals.overages, 0);
});

test('resends do not duplicate, reused sequences conflict and the latest content wins', async () => {
  const { q } = await database();
  const first = await send(q, caja1, centro, [{ entity: 'product', id: 'p1', payload: { product: { id: 'p1', name: 'Concha', price_cents: 1900 } } }]);
  assert.equal(first.accepted.length, 1);
  await send(q, caja1, centro, [
    { entity: 'product', id: 'p1', change: 'update', payload: { product: { id: 'p1', name: 'Concha', price_cents: 2000 } } },
    { entity: 'product', id: 'p1', change: 'update', payload: { product: { id: 'p1', name: 'Concha de vainilla', price_cents: 2100 } } },
  ]);
  const [state] = await q(`SELECT payload FROM entity_state WHERE entity = 'product' AND entity_id = 'p1'`, []);
  assert.equal((state.payload as any).product.price_cents, 2100, 'Within a batch the last queued change wins.');

  // Una entrada vieja que llega tarde (encolada antes) no pisa el estado más reciente.
  await send(q, caja1, centro, [{ entity: 'product', id: 'p1', change: 'update', at: '2026-01-01T00:00:00Z', payload: { product: { id: 'p1', name: 'Viejo', price_cents: 1 } } }]);
  const [still] = await q(`SELECT payload FROM entity_state WHERE entity = 'product' AND entity_id = 'p1'`, []);
  assert.equal((still.payload as any).product.name, 'Concha de vainilla');

  await send(q, caja1, centro, [{ entity: 'consignment_price', id: 'p1', change: 'delete', payload: { deleted: true, id: 'p1' } }]);
  const [removed] = await q(`SELECT deleted FROM entity_state WHERE entity = 'consignment_price' AND entity_id = 'p1'`, []);
  assert.equal(removed.deleted, true);
});

test('periods outside the data return empty sections', async () => {
  const { q } = await database(); await seedSales(q);
  const data = await dashboard(q, range('2026-09-01', '2026-09-30'));
  assert.equal(data.totals.tickets, 0); assert.equal(data.days.length, 0); assert.equal(data.sessions.length, 0); assert.equal(data.products.length, 0);
  assert.equal(data.terminals.length, 2);
});

test('range validation', () => {
  assert.deepEqual(range('2026-10-01', '2026-10-07', centro.toUpperCase()), { from: '2026-10-01', to: '2026-10-07', branchId: centro, timeZone: 'America/Mexico_City' });
  for (const [from, to] of [['2026-10-07', '2026-10-01'], ['2026-02-30', '2026-03-01'], ['ayer', '2026-10-01'], ['2026-01-01', '2026-12-31']])
    assert.throws(() => range(from, to), ProtocolError, `${from}..${to}`);
  assert.throws(() => range('2026-10-01', '2026-10-02', 'centro'), /Sucursal/);
});
