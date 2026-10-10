// Charolas en la web como la caja (docs/PLAN_COMPLETAR.md, W7): estado de cuenta, reporte del periodo y precios de charola.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { ProtocolError } from '../lib/protocol.js';
import { login } from '../lib/auth.js';
import { createCommand, pendingFor } from '../lib/commands.js';
import { trayAccount, trayReport } from '../lib/trays.js';
import { route } from '../lib/web.js';
import { caja1, centro, database, permissions, send, user } from './helpers.js';

const status = (code: number) => (e: unknown) => e instanceof ProtocolError && e.status === code;
const category = 'c1000000-0000-4000-8000-000000000001';
const concha = 'f1000000-0000-4000-8000-000000000001', dona = 'f1000000-0000-4000-8000-000000000002';
const lupita = 'e1000000-0000-4000-8000-000000000001', pepe = 'e1000000-0000-4000-8000-000000000002';
const tz = 'America/Mexico_City';

function delivery(id: string, store: string, at: string, items: [string, string, number, number, number][], settled?: { at: string; returned: number[] }, voided = false) {
  const lines = items.map(([product, name, price, retail, quantity], i) => ({ delivery_id: id, position: i + 1, product_id: product, product_name: name, unit_price_cents: price, retail_price_cents: retail, quantity }));
  const settlementItems = settled ? lines.map((l, i) => ({ delivery_id: id, position: l.position, sold: l.quantity - settled.returned[i], returned: settled.returned[i], line_total_cents: l.unit_price_cents * (l.quantity - settled.returned[i]) })) : [];
  return { entity: 'consignment_delivery', id, payload: {
    delivery: { id, store_id: store, delivered_utc: at, note: '' }, items: lines,
    settlement: settled ? { delivery_id: id, collected_utc: settled.at, sold_cents: settlementItems.reduce((a, l) => a + l.line_total_cents, 0), note: '' } : null,
    settlementItems, void: voided ? { delivery_id: id, reason: 'Otra tienda' } : null } };
}

async function seeded() {
  const { q } = await database();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000003', 'luis', 'Cashier', 'Luis#2026centro');
  await send(q, caja1, centro, [
    { entity: 'category', id: category, payload: { id: category, name: 'Pan dulce', active: 1, sort_order: 0 } },
    { entity: 'product', id: concha, payload: { product: { id: concha, name: 'Concha', sku: 'CM-1', category_id: category, price_cents: 1500, active: 1 }, inventoryLinks: [] } },
    { entity: 'product', id: dona, payload: { product: { id: dona, name: 'Dona', sku: 'CM-2', category_id: category, price_cents: 1400, active: 1 }, inventoryLinks: [] } },
    { entity: 'consignment_price', id: concha, payload: { product_id: concha, price_cents: 1000 } },
    { entity: 'consignment_store', id: lupita, payload: { store: { id: lupita, name: 'Abarrotes Lupita', contact: 'Lupita', phone: '55', address: '', active: 1 }, template: [{ store_id: lupita, product_id: concha, quantity: 6 }] } },
    { entity: 'consignment_store', id: pepe, payload: { store: { id: pepe, name: 'Tienda Don Pepe', contact: '', phone: '', address: '', active: 0 }, template: [] } },
    // Lupita: una recogida (10 conchas, regresan 2; 4 donas, regresa 1), una anulada y una todavía en la tienda.
    delivery('d1000000-0000-4000-8000-000000000001', lupita, '2026-10-05T15:00:00Z', [[concha, 'Concha', 1000, 1500, 10], [dona, 'Dona', 900, 1400, 4]], { at: '2026-10-06T15:00:00Z', returned: [2, 1] }),
    delivery('d1000000-0000-4000-8000-000000000002', lupita, '2026-10-06T15:00:00Z', [[concha, 'Concha', 1000, 1500, 5]], undefined, true),
    delivery('d1000000-0000-4000-8000-000000000003', lupita, '2026-10-07T15:00:00Z', [[concha, 'Concha', 1000, 1500, 6]]),
    { entity: 'consignment_payment', id: 'a1000000-0000-4000-8000-000000000001', payload: { id: 'a1000000-0000-4000-8000-000000000001', store_id: lupita, received_utc: '2026-10-06T16:00:00Z', amount_cents: 5000, method: 'CASH', reference: null, note: 'Abono' } },
  ]);
  return q;
}

test('account like the till: charges when collected and payments, newest first, with the balance today', async () => {
  const q = await seeded();
  const account = await trayAccount(q, centro, lupita, tz);
  assert.equal(account.balance, 8000 + 2700 - 5000, '8 conchas × $10 + 3 donas × $9 − $50.');
  assert.deepEqual(account.movements.map(m => [m.concept, m.charge, m.payment]), [
    ['Pago con efectivo · Abono', 0, 5000],
    ['Charola del lun 5 oct: 11 vendidas, 3 regresaron', 10_700, 0],
  ]);
  await assert.rejects(trayAccount(q, centro, randomUUID(), tz), status(404));
});

test('report like the till: voided trays do not count, what is still at the store, sell-through rounded to one decimal', async () => {
  const q = await seeded();
  const r = await trayReport(q, { from: '2026-10-01', to: '2026-10-09', branchId: centro, timeZone: tz } as never);
  assert.deepEqual(r.stores.map(s => [s.storeName, s.trays, s.delivered, s.collected, s.sold, s.returned, s.atStore, s.charged, s.paid, s.balance, s.sellThrough]), [
    ['Abarrotes Lupita', 2, 20, 1, 11, 3, 6, 10_700, 5000, 5700, 78.6]]);
  assert.equal(r.totals.leftToStores, 8 * 1500 + 3 * 1400 - 10_700, 'What those pieces would have left at the counter above the tray price.');
  assert.deepEqual(r.products.map(p => [p.productName, p.delivered, p.sold, p.returned, p.charged, p.sellThrough]), [
    ['Concha', 16, 8, 2, 8000, 80], ['Dona', 4, 3, 1, 2700, 75]]);
  const empty = await trayReport(q, { from: '2026-09-01', to: '2026-09-30', branchId: centro, timeZone: tz } as never);
  assert.equal(empty.stores.length, 0);
});

test('route lists every store for the Stores tab and products with counter price, category and SKU', async () => {
  const q = await seeded();
  const data = await route(q, centro);
  assert.deepEqual(data.stores.map(s => s.name), ['Abarrotes Lupita'], 'The route only shows active stores.');
  assert.deepEqual(data.allStores.map(s => [s.name, s.active]), [['Abarrotes Lupita', true], ['Tienda Don Pepe', false]]);
  assert.deepEqual(data.products.map(p => [p.name, p.price, p.retail, p.category, p.sku]), [['Concha', 1000, 1500, 'Pan dulce', 'CM-1']]);
});

test('tray prices are validated before reaching the till', async () => {
  const q = await seeded();
  const admin = await login(q, 'admin', 'Admin#2026centro');
  const luis = await login(q, 'luis', 'Luis#2026centro');
  await assert.rejects(createCommand(q, luis, centro, 'consignment.price.save', { prices: [{ productId: dona, priceCents: 900 }] }), status(403));
  await assert.rejects(createCommand(q, admin, centro, 'consignment.price.save', { prices: [{ productId: randomUUID(), priceCents: 900 }] }), status(404));
  await assert.rejects(createCommand(q, admin, centro, 'consignment.price.save', { prices: [{ productId: dona, priceCents: -1 }] }), /Precio/);
  await assert.rejects(createCommand(q, admin, centro, 'consignment.price.save', { prices: [] }), /inválidos/);
  await createCommand(q, admin, centro, 'consignment.price.save', { prices: [{ productId: dona, priceCents: 900 }, { productId: concha, priceCents: null }] });
  const [pulled] = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  assert.deepEqual(pulled.payload, { prices: [{ productId: dona, priceCents: 900 }, { productId: concha, priceCents: null }] });
});
