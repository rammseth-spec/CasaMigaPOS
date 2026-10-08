import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ProtocolError } from '../lib/protocol.js';
import { login } from '../lib/auth.js';
import { dashboard, parseRange, sessions } from '../lib/panel.js';
import * as web from '../lib/web.js';
import * as pos from '../lib/pos.js';
import { handle } from '../lib/router.js';
import { issueSession } from '../lib/auth.js';
import { caja1, caja2, centro, database, norte, permissions, send, user } from './helpers.js';

const status = (code: number) => (e: unknown) => e instanceof ProtocolError && e.status === code;
const range = (from: string, to: string) => ({ ...parseRange(new URL(`https://x/?from=${from}&to=${to}`)), branchId: centro });
const today = () => new Date().toLocaleDateString('en-CA', { timeZone: 'America/Mexico_City' });
const op = () => crypto.randomUUID();

const pan = 'd0000000-0000-4000-8000-000000000001';
const bebida = 'd0000000-0000-4000-8000-000000000002';
const concha = 'e0000000-0000-4000-8000-000000000001';
const cafe = 'e0000000-0000-4000-8000-000000000002';
const viejo = 'e0000000-0000-4000-8000-000000000003';
const norteño = 'e0000000-0000-4000-8000-000000000004';

async function seeded() {
  const { q } = await database();
  await permissions(q, caja1, centro); await permissions(q, caja2, norte);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000002', 'ana', 'Manager', 'Ana#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000003', 'luis', 'Cashier', 'Luis#2026centro');
  await user(q, caja2, norte, 'a0000000-0000-4000-8000-000000000004', 'rosa', 'Cashier', 'Rosa#2026norte');
  const product = (id: string, name: string, price: number, category: string, active = 1) =>
    ({ entity: 'product', id, payload: { product: { id, name, price_cents: price, category_id: category, active, sku: null }, inventoryLinks: [] } });
  await send(q, caja1, centro, [
    { entity: 'category', id: pan, payload: { id: pan, name: 'Pan dulce', active: 1, sort_order: 1 } },
    { entity: 'category', id: bebida, payload: { id: bebida, name: 'Bebida', active: 1, sort_order: 2 } },
    product(concha, 'Concha de vainilla', 1300, pan), product(cafe, 'Vaso de café', 2000, bebida), product(viejo, 'Pan retirado', 900, pan, 0),
  ]);
  await send(q, caja2, norte, [{ entity: 'category', id: pan, payload: { id: pan, name: 'Pan dulce', active: 1, sort_order: 1 } }, product(norteño, 'Rosca', 25000, pan)]);
  const as = (name: string, password: string) => login(q, name, password);
  return { q, admin: await as('admin', 'Admin#2026centro'), ana: await as('ana', 'Ana#2026centro'), luis: await as('luis', 'Luis#2026centro'), rosa: await as('rosa', 'Rosa#2026norte') };
}

test('a web till gets the next number of its branch, so its folios never look like the desktop ones', async () => {
  const { q, admin, ana } = await seeded();
  await assert.rejects(pos.createTill(q, ana, centro), status(403), 'Only the administrator creates tills.');
  const till = await pos.createTill(q, admin, centro);
  assert.deepEqual([till.name, till.code], ['CAJA-WEB-02', 'C02'], 'CASA-MIGA-CAJA-01 already uses C01.');
  assert.equal((await pos.createTill(q, admin, centro)).name, 'CAJA-WEB-03');
  assert.equal((await pos.createTill(q, admin, norte)).name, 'CAJA-WEB-03', 'Norte already has CASA-MIGA-CAJA-02.');
  assert.deepEqual((await pos.tills(q, centro)).map(t => t.code), ['C02', 'C03']);
});

test('sell from the web: catalog price and name, folio, change, card reference, idempotent retry', async () => {
  const { q, admin, luis, rosa } = await seeded();
  const till = await pos.createTill(q, admin, centro);
  await assert.rejects(pos.checkout(q, luis, { tillId: till.id, sessionId: op(), operationId: op(), lines: [{ productId: concha, quantity: 1 }], method: 'CASH', receivedCents: 2000 }),
    /abierta/, 'No sales before opening.');
  const { sessionId } = await pos.openSession(q, luis, { tillId: till.id, openingCents: 50000 });
  await assert.rejects(pos.openSession(q, luis, { tillId: till.id, openingCents: 0 }), status(409), 'One open shift per till.');
  await assert.rejects(pos.openSession(q, rosa, { tillId: till.id, openingCents: 0 }), status(403), 'Other branch.');

  const sale = (lines: { productId: string; quantity: number }[], method: string, receivedCents?: number, reference?: string, operationId = op()) =>
    pos.checkout(q, luis, { tillId: till.id, sessionId, operationId, lines, method, receivedCents, reference, price: 1 });
  const first = await sale([{ productId: concha, quantity: 3 }, { productId: cafe, quantity: 1 }], 'CASH', 10000);
  assert.equal(first.folio, 'C02-000001');
  assert.deepEqual(first.items.map(i => [i.name, i.quantity, i.unitPrice, i.total]), [['Concha de vainilla', 3, 1300, 3900], ['Vaso de café', 1, 2000, 2000]], 'Price and name come from the catalog, not from the browser.');
  assert.deepEqual([first.total, first.received, first.change, first.cashier, first.till], [5900, 10000, 4100, 'luis (Cashier)', 'CAJA-WEB-02']);

  const id = op();
  const card = await sale([{ productId: cafe, quantity: 2 }], 'CARD', undefined, ' 4601 ', id);
  assert.deepEqual([card.folio, card.method, card.received, card.change, card.reference], ['C02-000002', 'CARD', 4000, 0, '4601'], 'Card is charged for the exact total.');
  assert.equal((await sale([{ productId: cafe, quantity: 2 }], 'CARD', undefined, '4601', id)).folio, 'C02-000002', 'Same operation, same sale.');
  await assert.rejects(sale([{ productId: cafe, quantity: 3 }], 'CARD', undefined, '4601', id), status(409), 'Same operation id with other content.');

  await assert.rejects(sale([{ productId: concha, quantity: 1 }], 'CASH', 500), /no alcanza/);
  await assert.rejects(sale([{ productId: concha, quantity: 1 }], 'CASH', 2000, '123'), /no lleva referencia/);
  await assert.rejects(sale([{ productId: concha, quantity: 1 }], 'CARD', undefined, 'x'.repeat(41)), /40/);
  await assert.rejects(sale([{ productId: viejo, quantity: 1 }], 'CASH', 2000), /ya no están activos/);
  await assert.rejects(sale([{ productId: norteño, quantity: 1 }], 'CASH', 30000), /ya no están activos/, 'A product from another branch is not sold here.');
  await assert.rejects(sale([{ productId: concha, quantity: 1 }, { productId: concha, quantity: 1 }], 'CASH', 5000), /únicas/);
  await assert.rejects(sale([{ productId: concha, quantity: 1000 }], 'CASH', 5000), /999/);
  await assert.rejects(sale([], 'CASH', 0), /vacío/);
  await assert.rejects(pos.checkout(q, luis, { tillId: till.id, sessionId, operationId: op(), lines: [{ productId: concha, quantity: 1 }], method: 'CASH', receivedCents: 2000, expectedTotal: 1200 }),
    /Cambió el precio/, 'The total the cashier saw must be the one charged.');
  await send(q, caja1, centro, [{ entity: 'category', id: bebida, change: 'update', payload: { id: bebida, name: 'Bebida', active: 0, sort_order: 2 } }]);
  await assert.rejects(sale([{ productId: cafe, quantity: 1 }], 'TRANSFER'), /ya no están activos/, 'Inactive category.');

  // Dos cobros a la vez en la misma caja: consecutivos distintos.
  const [a, b] = await Promise.all([sale([{ productId: concha, quantity: 1 }], 'CASH', 1300), sale([{ productId: concha, quantity: 2 }], 'TRANSFER')]);
  assert.deepEqual([a.folio, b.folio].sort(), ['C02-000003', 'C02-000004']);

  // Lo vendido en la web entra en los mismos reportes que la caja de escritorio.
  const day = today();
  const list = await web.salesList(q, { ...range(day, day), search: 'C02-000001', method: null, page: 0 });
  assert.deepEqual(list.items.map(s => s.folio), ['C02-000001']);
  const totals = (await dashboard(q, range(day, day))).totals;
  assert.equal(totals.tickets, 4); assert.equal(totals.gross, 5900 + 4000 + 1300 + 2600);
  const state = await pos.posState(q, luis, centro, till.id);
  assert.deepEqual([state.session?.tickets, state.session?.count, state.products.map(p => p.name)], [4, null, ['Concha de vainilla']], 'Blind: no expected cash before counting; inactive items are hidden.');
});

test('blind count and close: only who may close; no sales after counting; expected matches the desktop rule', async () => {
  const { q, admin, ana, luis } = await seeded();
  const till = await pos.createTill(q, admin, centro);
  const { sessionId } = await pos.openSession(q, luis, { tillId: till.id, openingCents: 50000 });
  await pos.checkout(q, luis, { tillId: till.id, sessionId, operationId: op(), lines: [{ productId: concha, quantity: 2 }], method: 'CASH', receivedCents: 5000 });
  await pos.checkout(q, luis, { tillId: till.id, sessionId, operationId: op(), lines: [{ productId: cafe, quantity: 1 }], method: 'CARD' });
  await assert.rejects(pos.countSession(q, luis, { tillId: till.id, sessionId, countedCents: 52000 }), status(403), 'Cashiers do not count, like on the desktop.');
  await assert.rejects(pos.closeSession(q, ana, { tillId: till.id, sessionId }), /conteo ciego/);

  const counted = await pos.countSession(q, ana, { tillId: till.id, sessionId, countedCents: 52500 });
  assert.deepEqual([counted.expected, counted.counted, counted.difference], [50000 + 2600, 52500, -100], 'Only cash: fund + cash sales net of change.');
  await assert.rejects(pos.countSession(q, ana, { tillId: till.id, sessionId, countedCents: 52600 }), /ya fue confirmado/);
  await assert.rejects(pos.checkout(q, luis, { tillId: till.id, sessionId, operationId: op(), lines: [{ productId: concha, quantity: 1 }], method: 'CASH', receivedCents: 1300 }), /sin conteo/);

  await pos.closeSession(q, ana, { tillId: till.id, sessionId });
  await assert.rejects(pos.closeSession(q, ana, { tillId: till.id, sessionId }), /cerrado/);
  const [closed] = await sessions(q, range(today(), today()));
  assert.deepEqual([closed.terminal, closed.open, closed.expected, closed.counted, closed.difference], ['CAJA-WEB-02', false, 52600, 52500, -100]);
  assert.equal((await pos.tills(q, centro))[0].open, false, 'The till can open a new shift.');
  await pos.openSession(q, luis, { tillId: till.id, openingCents: 0 });
});

test('the cashier signs in to the web but only reaches the point of sale', async () => {
  const { q, admin, luis } = await seeded();
  const till = await pos.createTill(q, admin, centro);
  const SECRET = 'x'.repeat(40);
  const call = (who: typeof luis, op: string, init: RequestInit = {}) => handle(q, new Request(`https://casamiga.test/api/web/${op}`, {
    ...init, headers: { cookie: `cm_web=${issueSession(who, SECRET)}`, ...(init.body ? { 'content-type': 'application/json', origin: 'https://casamiga.test' } : {}) } }), op.split('?')[0], { SESSION_SECRET: SECRET });
  assert.equal((await call(luis, 'dashboard')).status, 403);
  assert.equal((await call(luis, 'sales')).status, 403);
  const state = await (await call(luis, `pos?till=${till.id}`)).json() as { tills: { id: string }[]; till: { id: string } };
  assert.deepEqual([state.tills.length, state.till.id], [1, till.id]);
  const opened = await call(luis, 'pos-open', { method: 'POST', body: JSON.stringify({ tillId: till.id, openingCents: 10000 }) });
  assert.equal(opened.status, 200);
  const { sessionId } = await opened.json() as { sessionId: string };
  const sold = await call(luis, 'pos-sale', { method: 'POST', body: JSON.stringify({ tillId: till.id, sessionId, operationId: op(), lines: [{ productId: concha, quantity: 1 }], method: 'CASH', receivedCents: 1300 }) });
  assert.equal(((await sold.json()) as { folio: string }).folio, 'C02-000001');
  assert.equal((await call(luis, 'pos-till', { method: 'POST', body: JSON.stringify({ branchId: centro }) })).status, 403);
  assert.equal((await call(luis, 'cash')).status, 403, 'The Caja screen shows the running expected cash: not for a blind count.');
});
