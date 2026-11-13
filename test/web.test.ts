import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseRange } from '../lib/panel.js';
import * as web from '../lib/web.js';
import { handle } from '../lib/router.js';
import { issueSession, login } from '../lib/auth.js';
import { caja1, caja2, centro, database, norte, permissions, sale, seedSales, send, user } from './helpers.js';

const range = (from: string, to: string, branch: string | null = null) =>
  ({ ...parseRange(new URL(`https://x/?from=${from}&to=${to}`)), branchId: branch });

test('display folio matches the terminal (C01-000123)', () => {
  assert.equal(web.displayFolio('CASA-MIGA-CAJA-01', 1061), 'C01-001061');
  assert.equal(web.displayFolio('Caja 12', 7), 'C12-000007');
  assert.equal(web.displayFolio('Mostrador', 5), 'C01-000005');
});

test('sales list searches by folio or product and the detail shows items, payment and refunds', async () => {
  const { q } = await database(); await seedSales(q);
  await user(q, caja1, centro, 'u-admin', 'admin', 'Administrator', 'Admin#2026centro');
  const all = await web.salesList(q, { ...range('2026-10-06', '2026-10-07'), search: '', method: null, page: 0 });
  assert.equal(all.total, 6);
  assert.deepEqual([all.items[0].id, all.items[0].folio], ['s5', 'C01-000005'], 'Newest first.');
  const byFolio = await web.salesList(q, { ...range('2026-10-06', '2026-10-07', centro), search: 'C01-000003', method: null, page: 0 });
  assert.deepEqual(byFolio.items.map(s => s.id), ['s3']);
  const otherTill = await web.salesList(q, { ...range('2026-10-06', '2026-10-07'), search: 'C02-000001', method: null, page: 0 });
  assert.deepEqual([otherTill.total, otherTill.items.map(s => s.id)], [1, ['n1']], 'The till code filters in the query, so the count matches.');
  const noTill = await web.salesList(q, { ...range('2026-10-06', '2026-10-07'), search: 'C07-000001', method: null, page: 0 });
  assert.equal(noTill.total, 0);
  const byProduct = await web.salesList(q, { ...range('2026-10-06', '2026-10-07'), search: 'boli', method: null, page: 0 });
  assert.deepEqual(byProduct.items.map(s => s.id), ['s4']);
  const cards = await web.salesList(q, { ...range('2026-10-06', '2026-10-07'), search: '', method: 'CARD', page: 0 });
  assert.deepEqual(cards.items.map(s => s.id), ['s2']);
  const s1 = all.items.find(s => s.id === 's1')!;
  assert.equal(s1.refunded, 1900); assert.equal(s1.cashier, 'admin (Administrator)'); assert.equal(s1.pieces, 2);

  await assert.rejects(web.saleDetail(q, null, 's1'), /inválida/, 'Only UUIDs.');
});

test('the history filters like the till: all days, one shift, and the ids shown in the detail', async () => {
  const { q } = await database(); await seedSales(q);
  const tz = parseRange(new URL('https://x/?from=2026-10-06&to=2026-10-06')).timeZone;
  const everything = await web.salesList(q, { from: null, to: null, branchId: null, timeZone: tz, search: '', method: null, page: 0 });
  const twoDays = await web.salesList(q, { ...range('2026-10-06', '2026-10-07'), search: '', method: null, page: 0 });
  assert.ok(everything.total >= twoDays.total, '«Todos» does not filter by day.');
  const first = twoDays.items.find(s => s.id === 's1')!;
  assert.equal(first.terminalId, caja1);
  const shift = await web.salesList(q, { from: null, to: null, branchId: centro, timeZone: tz, search: '', method: null, page: 0, session: first.cashSessionId });
  assert.deepEqual(shift.items.map(s => s.id).sort(), ['s1', 's2', 's3'], '«Sesión actual» keeps only that shift.');

  const id = '5a1e0000-0000-4000-8000-000000000001';
  await send(q, caja1, centro, [{ entity: 'sale', id, payload: sale(id, first.cashSessionId, '2026-10-07T18:00:00+00:00', 'CASH', [['Concha', 1, 1900]], 9) }]);
  const detail = await web.saleDetail(q, null, id);
  assert.deepEqual([detail.internalFolio, detail.userId, detail.terminalId, detail.cashSessionId, detail.web],
    [`${caja1.replace(/-/g, '')}-00000009`, 'u-admin', caja1, first.cashSessionId, false], 'Same internal folio as Sale.Folio on the till.');
});

test('cash totals come from the database, not from the truncated lists', async () => {
  const { q } = await database(); await seedSales(q);
  const totals = await web.cashTotals(q, range('2026-10-06', '2026-10-07', centro));
  assert.deepEqual(totals, { sessions: 2, open: 1, expenses: { count: 1, amount: 4550 }, withdrawals: { count: 0, amount: 0 }, deposits: { count: 1, amount: 10000 } });
});

test('reports: comparison, hours, cash control and production suggestion', async () => {
  const { q } = await database(); await seedSales(q);
  const compare = await web.comparison(q, range('2026-10-07', '2026-10-07', centro));
  assert.equal(compare.current.tickets, 2); assert.equal(compare.previous.tickets, 3);
  assert.deepEqual(compare.previousRange, { from: '2026-10-06', to: '2026-10-06' });
  assert.equal(compare.daily[0].previous, 8300);

  const hours = await web.heatmap(q, range('2026-10-06', '2026-10-07', centro));
  assert.deepEqual(hours.find(h => h.weekday === 2 && h.hour === 21), { weekday: 2, hour: 21, tickets: 1, gross: 1900 }, 'Tuesday 21:30 local.');

  const control = await web.cashControl(q, range('2026-10-06', '2026-10-07', centro));
  assert.equal(control.cuts[0].shortages, -2000);
  assert.equal(control.refunds[0].total, 1900);
  assert.deepEqual(control.reasons, [{ reason: 'Pan aplastado', count: 1 }]);

  // Cuatro martes de conchas: 3, 2, 0 (sin venta) y 3 → promedio 2 → sugerido 3 (+10 % hacia arriba).
  for (const [day, quantity] of [['2026-09-15', 3], ['2026-09-22', 2], ['2026-10-06', 3]] as const)
    await send(q, caja1, centro, [{ entity: 'sale', id: `t-${day}`, payload: sale(`t-${day}`, 'x', `${day}T18:00:00Z`, 'CASH', [['Concha martes', quantity, 1900]]) }]);
  const suggestion = await web.productionSuggestion(q, centro, 'America/Mexico_City', '2026-10-13');
  assert.deepEqual(suggestion.weeks, ['2026-10-06', '2026-09-29', '2026-09-22', '2026-09-15']);
  assert.deepEqual(suggestion.products.find(p => p.name === 'Concha martes'), { name: 'Concha martes', average: 2, suggested: 3, weeksWithSales: 3 });
});

test('consignment balances, catalog with photos and users without hashes', async () => {
  const { q } = await database();
  const store = 'e1000000-0000-4000-8000-000000000001';
  await send(q, caja1, centro, [
    { entity: 'product', id: 'p1', payload: { product: { id: 'p1', name: 'Concha', category_id: 'c1', price_cents: 1900, active: 1, sku: 'CM-1', track_inventory: 1 }, inventoryLinks: [] } },
    { entity: 'category', id: 'c1', payload: { id: 'c1', name: 'Pan dulce', active: 1, sort_order: 0, description: '' } },
    { entity: 'product_photo', id: 'p1', payload: { photo: { product_id: 'p1' }, asset: { id: 'x', provenance: 'PROVISIONAL', thumbnail_sha256: 'ABC' }, thumbnail: Buffer.from([0xff, 0xd8, 0xff]).toString('base64') } },
    { entity: 'consignment_price', id: 'p1', payload: { product_id: 'p1', price_cents: 1500 } },
    { entity: 'consignment_store', id: store, payload: { store: { id: store, name: 'Abarrotes Lupita', contact: 'Lupita', phone: '555', address: '', active: 1 }, template: [{ store_id: store, product_id: 'p1', quantity: 6 }] } },
    { entity: 'consignment_delivery', id: 'd1', payload: { delivery: { id: 'd1', store_id: store, delivered_utc: '2026-10-06T14:00:00Z' }, items: [{ product_name: 'Concha', quantity: 6, unit_price_cents: 1500 }],
      settlement: { delivery_id: 'd1', collected_utc: '2026-10-07T14:00:00Z', sold_cents: 6000 }, settlementItems: [{ sold: 4, returned: 2 }], void: null } },
    { entity: 'consignment_delivery', id: 'd2', payload: { delivery: { id: 'd2', store_id: store, delivered_utc: '2026-10-07T14:00:00Z' }, items: [{ product_name: 'Concha', quantity: 6, unit_price_cents: 1500 }], settlement: null, settlementItems: [], void: null } },
    { entity: 'consignment_payment', id: 'pay1', payload: { id: 'pay1', store_id: store, received_utc: '2026-10-07T15:00:00Z', amount_cents: 4000, method: 'CASH', note: '' } },
  ]);
  await user(q, caja1, centro, 'u1', 'ana', 'Manager', 'Ana#2026centro');

  const trays = await web.consignment(q, range('2026-10-01', '2026-10-07', centro));
  const lupita = trays.stores[0];
  assert.equal(lupita.balance, 2000); assert.equal(lupita.pending, 1); assert.equal(lupita.sold, 4); assert.equal(lupita.returned, 2); assert.equal(lupita.delivered, 12);
  assert.deepEqual(lupita.template, [{ productId: 'p1', name: 'Concha', quantity: 6 }]);
  assert.deepEqual(trays.deliveries.map(d => d.status), ['pending', 'collected']);

  const items = await web.catalog(q, centro);
  assert.deepEqual(items.products[0].photo, { sha: 'ABC', provisional: true, at: null });
  assert.equal(items.products[0].consignmentPrice, 1500);
  await assert.rejects(web.photo(q, centro, 'p1'), /inválido/);

  const people = await web.users(q, centro);
  assert.equal(people[0].username, 'ANA');
  assert.equal(JSON.stringify(people).includes('AQAAAA'), false, 'No password hashes in the web.');
});

test('router: cashier cannot enter, manager sees own branch only, admin sees all', async () => {
  const { q } = await database(); await seedSales(q);
  await permissions(q, caja1, centro); await permissions(q, caja2, norte);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000002', 'ana', 'Manager', 'Ana#2026centro');
  const env = { SESSION_SECRET: 'z'.repeat(40) };
  const call = async (op: string, cookie: string, query = '') =>
    handle(q, new Request(`https://x/api/web/${op}?${query}`, { headers: { cookie: `cm_web=${cookie}` } }), op, env);

  const ana = issueSession(await login(q, 'ana', 'Ana#2026centro'), env.SESSION_SECRET);
  const me = await (await call('me', ana)).json();
  assert.deepEqual(me.branches.map((b: { name: string }) => b.name), ['Centro']);
  assert.equal((await call('dashboard', ana, `from=2026-10-06&to=2026-10-07&branch=${norte}`)).status, 403);
  const own = await (await call('dashboard', ana, 'from=2026-10-06&to=2026-10-07')).json();
  assert.equal(own.totals.tickets, 5, 'Without a branch, a manager gets their own.');
  assert.equal((await call('users', ana)).status, 403, 'Users screen is for the administrator.');

  const boss = issueSession(await login(q, 'admin', 'Admin#2026centro'), env.SESSION_SECRET);
  assert.equal((await (await call('dashboard', boss, 'from=2026-10-06&to=2026-10-07')).json()).totals.tickets, 6);
  assert.equal((await call('users', boss)).status, 200);
  assert.equal((await call('dashboard', 'basura')).status, 401);
  assert.equal((await handle(q, new Request('https://x/api/web/me'), 'me', {})).status, 503, 'Without SESSION_SECRET the web stays closed.');

  const post = (headers: Record<string, string>) => handle(q, new Request('https://x/api/web/login', { method: 'POST', headers, body: JSON.stringify({ username: 'ana', password: 'Ana#2026centro' }) }), 'login', env);
  assert.equal((await post({ 'content-type': 'application/json', origin: 'https://evil.example' })).status, 403, 'Another site cannot post.');
  assert.equal((await post({ 'content-type': 'application/json', 'sec-fetch-site': 'cross-site' })).status, 403);
  assert.equal((await post({ 'content-type': 'text/plain' })).status, 415, 'Plain form posts are refused.');
  const loginResponse = await post({ 'content-type': 'application/json', origin: 'https://x', 'sec-fetch-site': 'same-origin' });
  assert.equal(loginResponse.status, 200);
  assert.match(loginResponse.headers.get('set-cookie') ?? '', /cm_web=.+; Path=\/; HttpOnly; Secure; SameSite=Strict/);
});
