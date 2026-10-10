// Inventario en la web como la caja (docs/PLAN_COMPLETAR.md, W4): artículos, presentaciones, asociaciones, saldo inicial,
// historial por artículo y consumo automático por ventas.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { ProtocolError } from '../lib/protocol.js';
import { login } from '../lib/auth.js';
import { createCommand, pendingFor, recordResults } from '../lib/commands.js';
import { consumptionCheck, inventory, inventoryItem } from '../lib/web.js';
import { caja1, centro, database, norte, permissions, send, user } from './helpers.js';

const status = (code: number) => (e: unknown) => e instanceof ProtocolError && e.status === code;
const MAIN = '10000000-0000-0000-0000-000000000001';
const category = 'c1000000-0000-4000-8000-000000000001';
const concha = 'f1000000-0000-4000-8000-000000000001';
const dona = 'f1000000-0000-4000-8000-000000000002';
const flour = 'aaaaaaaa-0000-4000-8000-000000000001';
const finished = 'aaaaaaaa-0000-4000-8000-000000000002';
const admin_ = 'a0000000-0000-4000-8000-000000000001';

const operation = (id: string, kind: string, at: string, movements: Record<string, unknown>[], extra: Record<string, unknown> = {}) => ({
  entity: 'inventory_operation', id, payload: { operation: { id, kind, state: 'CONFIRMED', actor_id: admin_, reason_code: null, note: null, external_reference: null,
    created_utc: at, confirmed_utc: at, ...extra }, movements } });
const move = (item: string, sequence: number, delta: number, before: number) =>
  ({ item_id: item, location_id: MAIN, sequence, dimension: 'MASS', delta_atomic: delta, balance_before_atomic: before, balance_after_atomic: before + delta });

async function seeded() {
  const { q } = await database();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, admin_, 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000002', 'ana', 'Manager', 'Ana#2026centro');
  await send(q, caja1, centro, [
    { entity: 'branch', id: centro, payload: { id: centro, name: 'Centro', sale_consumption: { state: 'DISABLED', version: 1, updated_utc: '2026-10-01T12:00:00Z', updated_by: null } } },
    { entity: 'category', id: category, payload: { id: category, name: 'Pan dulce', active: 1, sort_order: 0 } },
    { entity: 'product', id: concha, payload: { product: { id: concha, name: 'Concha', sku: 'CM-1', category_id: category, price_cents: 1900, active: 1, track_inventory: 1 },
      inventoryLinks: [{ id: randomUUID(), product_id: concha, inventory_item_id: finished, dimension: 'COUNT', quantity_per_product_unit_atomic: 1, active: 1 }] } },
    { entity: 'product', id: dona, payload: { product: { id: dona, name: 'Dona', sku: 'CM-2', category_id: category, price_cents: 1500, active: 1, track_inventory: 1 }, inventoryLinks: [] } },
    { entity: 'inventory_item', id: flour, payload: { item: { id: flour, code: 'HAR', name: 'Harina', kind: 'RAW_MATERIAL', dimension: 'MASS', active: 1 },
      units: [{ code: 'BULTO', name: 'Bulto 44 kg', atomic_units_per_whole_unit: 44_000_000, active: 1 }, { code: 'VIEJO', name: 'Costal viejo', atomic_units_per_whole_unit: 20_000_000, active: 0 }],
      balances: [{ location_id: MAIN, quantity_atomic: 7_000_000 }] } },
    { entity: 'inventory_item', id: finished, payload: { item: { id: finished, code: 'PT-CON', name: 'Concha terminada', kind: 'FINISHED_GOOD', dimension: 'COUNT', active: 1 }, units: [], balances: [] } },
    operation('0b000000-0000-4000-8000-000000000001', 'OPENING_BALANCE', '2026-10-01T13:00:00Z', [{ ...move(flour, 1, 5_000_000, 0) }], { external_reference: 'Apertura' }),
    operation('0b000000-0000-4000-8000-000000000002', 'RECEIPT', '2026-10-02T13:00:00Z', [{ ...move(flour, 1, 2_500_000, 5_000_000) }], { external_reference: 'Factura 18' }),
    operation('0b000000-0000-4000-8000-000000000003', 'WASTE', '2026-10-03T13:00:00Z', [{ ...move(flour, 1, -500_000, 7_500_000) }], { reason_code: 'DAMAGED' }),
  ]);
  return q;
}

test('inventory view: main balance, all presentations, links inside the products and the consumption state of each till', async () => {
  const q = await seeded();
  const view = await inventory(q, centro, 'America/Mexico_City');
  const item = view.items.find(i => i.id === flour)!;
  assert.equal(item.main, 7_000_000);
  assert.deepEqual(item.units.map(u => [u.code, u.active]), [['BULTO', true], ['VIEJO', false]], 'Inactive presentations are listed too, like the till.');
  assert.deepEqual(view.items.find(i => i.id === finished)!.links, [{ productId: concha, product: 'Concha', sku: 'CM-1', atomic: 1 }]);
  assert.equal(view.items.find(i => i.id === finished)!.main, null, 'Without opening balance.');
  assert.deepEqual(view.products.map(p => [p.name, p.linkedItemId, p.trackInventory]), [['Concha', finished, true], ['Dona', null, true]]);
  assert.deepEqual(view.consumption, [{ branchId: centro, state: 'DISABLED', version: 1, updatedAt: '2026-10-01T12:00:00Z', updatedBy: null }]);
});

test('item detail: confirmed history newest first with balances before and after, and whether it matches the balance', async () => {
  const q = await seeded();
  const detail = await inventoryItem(q, centro, flour, 0);
  assert.equal(detail.total, 3);
  assert.deepEqual(detail.history.map(h => [h.kind, h.delta, h.before, h.after, h.reason, h.reference]), [
    ['WASTE', -500_000, 7_500_000, 7_000_000, 'DAMAGED', ''],
    ['RECEIPT', 2_500_000, 5_000_000, 7_500_000, '', 'Factura 18'],
    ['OPENING_BALANCE', 5_000_000, 0, 5_000_000, '', 'Apertura'],
  ]);
  assert.equal(detail.history[0].user, 'admin (Administrator)');
  assert.equal(detail.consistent, true);
  assert.equal((await inventoryItem(q, centro, flour, 1)).history.length, 0, 'Pages of 25.');
  assert.equal((await inventoryItem(q, centro, finished, 0)).consistent, true, 'No balance and no movements is consistent.');
  await send(q, caja1, centro, [operation('0b000000-0000-4000-8000-000000000004', 'RECEIPT', '2026-10-04T13:00:00Z', [{ ...move(flour, 1, 1_000_000, 7_000_000) }])]);
  assert.equal((await inventoryItem(q, centro, flour, 0)).consistent, false, 'A movement whose balance has not arrived does not match.');
  await assert.rejects(inventoryItem(q, norte, flour, 0), status(404), 'Another branch does not see it.');
});

test('verify configuration like the till: tracked products without an item, items without opening balance', async () => {
  const q = await seeded();
  const { problems } = await consumptionCheck(q, centro);
  assert.deepEqual(problems, [
    { code: 'NotInitialized', product: 'Concha', item: 'Concha terminada' },
    { code: 'TrackedProductWithoutInventoryLink', product: 'Dona', item: null },
  ]);
});

test('items, presentations, links, opening balance and consumption are validated before reaching the till', async () => {
  const q = await seeded();
  const admin = await login(q, 'admin', 'Admin#2026centro');
  const ana = await login(q, 'ana', 'Ana#2026centro');
  const sugar = randomUUID();

  await createCommand(q, admin, centro, 'inventory.item.save', { id: sugar, code: 'AZU', name: 'Azúcar', kind: 'RawMaterial', dimension: 'Mass' });
  await assert.rejects(createCommand(q, admin, centro, 'inventory.item.save', { id: randomUUID(), code: 'har', name: 'Otra harina', kind: 'RawMaterial', dimension: 'Mass' }), /código/);
  await assert.rejects(createCommand(q, admin, centro, 'inventory.item.save', { id: randomUUID(), code: 'X', name: 'X', kind: 'Flour', dimension: 'Mass' }), /Tipo/);
  await assert.rejects(createCommand(q, admin, centro, 'inventory.item.save', { id: randomUUID(), code: 'X', name: 'X', kind: 'Supply', dimension: 'Weight' }), /Dimensión/);
  await assert.rejects(createCommand(q, ana, centro, 'inventory.item.save', { id: randomUUID(), code: 'X', name: 'X', kind: 'Supply', dimension: 'Count' }), status(403), 'A manager does not configure inventory.');
  await createCommand(q, admin, centro, 'inventory.item.save', { id: flour, code: 'IGNORED', name: 'Harina de trigo', kind: 'RawMaterial', dimension: 'Count' });

  await assert.rejects(createCommand(q, admin, centro, 'inventory.item.active', { id: flour, active: false }), /existencia/);
  await createCommand(q, admin, centro, 'inventory.item.active', { id: finished, active: false });

  await createCommand(q, admin, centro, 'inventory.unit.add', { itemId: flour, code: 'COSTAL', name: 'Costal 25 kg', quantity: '25000' });
  await assert.rejects(createCommand(q, admin, centro, 'inventory.unit.add', { itemId: flour, code: 'bulto', name: 'Otro', quantity: '1' }), /código/);
  await assert.rejects(createCommand(q, admin, centro, 'inventory.unit.add', { itemId: flour, code: 'kg', name: 'Kilo', quantity: '1000' }), /código/, 'Standard units are taken.');
  await assert.rejects(createCommand(q, admin, centro, 'inventory.unit.add', { itemId: flour, code: 'MEDIO', name: 'Medio', quantity: '0.0005' }), /3 decimales/);

  await createCommand(q, admin, centro, 'inventory.link', { itemId: flour, productId: dona, quantity: '60.5' });
  await assert.rejects(createCommand(q, admin, centro, 'inventory.link', { itemId: flour, productId: concha, quantity: '10' }), /desvincúlalo/);
  await createCommand(q, admin, centro, 'inventory.unlink', { productId: concha });
  await assert.rejects(createCommand(q, admin, centro, 'inventory.unlink', { productId: dona }), /no tiene una asociación/);

  await createCommand(q, admin, centro, 'inventory.opening', { itemId: finished, quantity: '0', unitCode: 'piece', reference: 'Arranque' });
  await assert.rejects(createCommand(q, admin, centro, 'inventory.opening', { itemId: finished, quantity: '3', unitCode: 'piece' }), /Ya se mandó/);
  await assert.rejects(createCommand(q, admin, centro, 'inventory.opening', { itemId: flour, quantity: '3', unitCode: 'kg' }), /ya tiene saldo inicial/);
  await assert.rejects(createCommand(q, ana, centro, 'inventory.opening', { itemId: finished, quantity: '3', unitCode: 'piece' }), status(403));

  await createCommand(q, admin, centro, 'inventory.consumption', { action: 'Enable', version: 1 });
  await assert.rejects(createCommand(q, admin, centro, 'inventory.consumption', { action: 'Enable', version: 1 }), /Ya se mandó/, 'One change at a time until the till answers.');
  await assert.rejects(createCommand(q, admin, centro, 'inventory.consumption', { action: 'Pause', version: 1 }), status(409), 'Pause only from ENABLED.');
  await assert.rejects(createCommand(q, admin, centro, 'inventory.consumption', { action: 'Enable', version: 2 }), status(409), 'The version must be the one the till uploaded.');
  await assert.rejects(createCommand(q, ana, centro, 'inventory.consumption', { action: 'Enable', version: 1 }), status(403));

  const pulled = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  assert.deepEqual(pulled.map(c => [c.kind, c.payload]), [
    ['inventory.item.save', { id: sugar, code: 'AZU', name: 'Azúcar', kind: 'RawMaterial', dimension: 'Mass' }],
    ['inventory.item.save', { id: flour, name: 'Harina de trigo', kind: 'RawMaterial' }],
    ['inventory.item.active', { id: finished, active: false }],
    ['inventory.unit.add', { itemId: flour, code: 'COSTAL', name: 'Costal 25 kg', atomic: 25_000_000 }],
    ['inventory.link', { itemId: flour, productId: dona, atomic: 60_500 }],
    ['inventory.unlink', { productId: concha }],
    ['inventory.opening', { itemId: finished, locationId: MAIN, quantity: '0', unitCode: 'piece', reference: 'Arranque' }],
    ['inventory.consumption', { action: 'Enable', state: 'Disabled', version: 1 }],
  ]);
  // Si la caja rechaza el saldo inicial, se puede volver a mandar.
  await recordResults(q, { id: caja1, branchId: centro }, { results: [{ id: pulled[6].id, status: 'rejected', message: 'El artículo está inactivo.' }] });
  await createCommand(q, admin, centro, 'inventory.opening', { itemId: finished, quantity: '3', unitCode: 'piece' });
});
