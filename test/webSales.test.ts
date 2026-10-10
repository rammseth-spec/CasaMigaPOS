import { test } from 'node:test';
import assert from 'node:assert/strict';
import { login } from '../lib/auth.js';
import { newTerminalKey, sha256Hex } from '../lib/protocol.js';
import * as pos from '../lib/pos.js';
import { handleTerminal } from '../lib/terminal.js';
import { parseCursors } from '../lib/webSales.js';
import { caja1, caja2, centro, database, norte, permissions, send, user } from './helpers.js';

const op = () => crypto.randomUUID();
const pan = 'd0000000-0000-4000-8000-000000000001';
const concha = 'e0000000-0000-4000-8000-000000000001';

type Event = { terminal: string; terminalName: string; seq: number; entity: string; entityId: string; change: string; payload: Record<string, any> };

async function setup() {
  const { q } = await database();
  await permissions(q, caja1, centro); await permissions(q, caja2, norte);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000002', 'ana', 'Manager', 'Ana#2026centro');
  await send(q, caja1, centro, [
    { entity: 'category', id: pan, payload: { id: pan, name: 'Pan dulce', active: 1, sort_order: 1 } },
    { entity: 'product', id: concha, payload: { product: { id: concha, name: 'Concha de vainilla', price_cents: 1300, category_id: pan, active: 1, sku: null }, inventoryLinks: [] } },
  ]);
  // Llaves conocidas para las dos cajas de escritorio (en la base de prueba sólo hay su SHA-256 de relleno).
  const keys = { centro: newTerminalKey(), norte: newTerminalKey() };
  await q('UPDATE terminal SET key_sha256 = $2 WHERE id = $1', [caja1, sha256Hex(keys.centro)]);
  await q('UPDATE terminal SET key_sha256 = $2 WHERE id = $1', [caja2, sha256Hex(keys.norte)]);
  const pull = async (key: string, after = '') => {
    const response = await handleTerminal(q, new Request(`https://x/api/sync/web-sales${after ? `?after=${after}` : ''}`, { headers: { authorization: `Bearer ${key}` } }), 'web-sales', {});
    return { status: response.status, body: await response.json() as { events: Event[]; error?: string } };
  };
  return { q, keys, pull, admin: await login(q, 'admin', 'Admin#2026centro'), ana: await login(q, 'ana', 'Ana#2026centro') };
}

test('the desktop downloads what its branch web tills recorded, in order and from its cursor', async () => {
  const { q, keys, pull, admin, ana } = await setup();
  assert.deepEqual((await pull(keys.centro)).body.events, [], 'No web tills yet.');
  const till = await pos.createTill(q, admin, centro);
  const { sessionId } = await pos.openSession(q, ana, { tillId: till.id, openingCents: 50000 });
  const sale = await pos.checkout(q, ana, { tillId: till.id, sessionId, operationId: op(), lines: [{ productId: concha, quantity: 2 }], method: 'CARD', reference: '4601', price: 1 });
  await pos.refund(q, ana, { tillId: till.id, sessionId, operationId: op(), saleId: sale.id, kind: 'RETURN', lines: [{ position: 1, quantity: 1 }], reason: 'Cliente lo regresó' });
  await pos.movement(q, ana, { tillId: till.id, sessionId, operationId: op(), kind: 'EXPENSE', amountCents: 5000, category: 'GAS', note: '' });
  await pos.countSession(q, ana, { tillId: till.id, sessionId, countedCents: 45000 });
  await pos.closeSession(q, ana, { tillId: till.id, sessionId });

  const first = await pull(keys.centro);
  assert.equal(first.status, 200);
  const events = first.body.events;
  assert.deepEqual(events.map(e => [e.entity, e.change]), [['cash_session', 'insert'], ['sale', 'insert'], ['sale_refund', 'insert'], ['cash_movement', 'insert'], ['cash_count', 'insert'], ['cash_session', 'update']],
    'Audit entries stay in the cloud.');
  assert.ok(events.every(e => e.terminal === till.id && e.terminalName === 'CAJA-WEB-02'));
  assert.ok(events.every((e, i) => i === 0 || e.seq > events[i - 1].seq));
  const saleEvent = events.find(e => e.entity === 'sale')!;
  assert.equal(saleEvent.payload.sale.sequence, 1, 'Same format the desktop uploads, with the folio number.');
  assert.deepEqual([saleEvent.payload.payment.method, saleEvent.payload.payment.reference, saleEvent.payload.items[0].quantity], ['CARD', '4601', 2]);
  assert.equal(events.find(e => e.entity === 'sale_refund')!.payload.refund.total_cents, 1300);

  const last = events.at(-1)!.seq;
  assert.deepEqual((await pull(keys.centro, `${till.id}:${last}`)).body.events, [], 'Nothing new after the cursor.');
  assert.equal((await pull(keys.centro, `${till.id}:${events[2].seq}`)).body.events.length, 3);
  assert.deepEqual((await pull(keys.norte)).body.events, [], 'Other branch: nothing.');
  assert.equal((await pull('x'.repeat(43))).status, 401);
  assert.equal((await pull(keys.centro, 'nope:1')).status, 400);
});

test('cursors are validated', () => {
  assert.deepEqual(parseCursors(null), []);
  assert.deepEqual(parseCursors('E0000000-0000-4000-8000-000000000001:5'), [{ terminal: 'e0000000-0000-4000-8000-000000000001', seq: 5 }]);
  assert.throws(() => parseCursors('e0000000-0000-4000-8000-000000000001:-1'));
  assert.throws(() => parseCursors('e0000000-0000-4000-8000-000000000001:1.5'));
});
