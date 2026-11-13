// Producción por lotes desde la web (Producción → Capturar lote), como en la caja: la primera vez da de alta el artículo terminado PT-<SKU>,
// su saldo inicial en cero, el vínculo con el pan y lo marca para descontar; después sólo suma las piezas. No repite lo que ya va en la fila.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { batchCode, captureBatch, listCommands } from '../lib/commands.js';
import { login } from '../lib/auth.js';
import { MAIN_LOCATION } from '../lib/web.js';
import { caja1, centro, database, permissions, send, user } from './helpers.js';

const category = 'c1000000-0000-4000-8000-000000000001';
const concha = 'f1000000-0000-4000-8000-000000000001', bolillo = 'f1000000-0000-4000-8000-000000000002';
const product = (id: string, sku: string, name: string) => ({ entity: 'product', id, payload: {
  product: { id, category_id: category, sku, name, description: '', price_cents: 1300, active: 1, track_inventory: 0 }, inventoryLinks: [] } });

async function setup() {
  const { q } = await database();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000002', 'encargado', 'Manager', 'Encargado#2026');
  await send(q, caja1, centro, [{ entity: 'category', id: category, payload: { id: category, name: 'Pan dulce', description: '', sort_order: 0, active: 1 } },
    product(concha, 'CM-001', 'Concha de vainilla'), product(bolillo, 'CM-003', 'Bolillo mantequilla')]);
  return { q, admin: await login(q, 'admin', 'Admin#2026centro'), manager: await login(q, 'encargado', 'Encargado#2026') };
}
// En el orden en que la caja los aplica (la lista viene del más nuevo al más viejo).
const queue = async (q: Awaited<ReturnType<typeof setup>>['q']) => (await listCommands(q, centro, true)).reverse();
const kinds = async (q: Awaited<ReturnType<typeof setup>>['q']) => (await queue(q)).map(c => c.kind);

test('the first batch of a bread sets it up like the till; later ones only add pieces', async () => {
  const { q, admin } = await setup();
  assert.equal(batchCode('cm-001 ', concha), 'PT-CM-001');
  assert.equal(batchCode(null, concha), 'PT-F10000000000');

  const first = await captureBatch(q, admin, centro, { productId: concha, pieces: 24 });
  assert.equal(first.code, 'PT-CM-001');
  assert.deepEqual(await kinds(q), ['inventory.item.save', 'inventory.opening', 'inventory.link', 'product.save', 'inventory.receive']);
  const queued = await queue(q);
  assert.deepEqual(queued[0].payload, { id: first.itemId, code: 'PT-CM-001', name: 'Concha de vainilla', kind: 'FinishedGood', dimension: 'Count' });
  assert.deepEqual(queued[1].payload, { itemId: first.itemId, locationId: MAIN_LOCATION, quantity: '0', unitCode: 'piece' });
  assert.deepEqual(queued[2].payload, { itemId: first.itemId, productId: concha, atomic: 1 });
  assert.equal(queued[3].payload.trackInventory, true);
  assert.deepEqual(queued[4].payload, { itemId: first.itemId, locationId: MAIN_LOCATION, quantity: '24', unitCode: 'piece', reference: 'LOTE', note: 'Producción: piezas que salieron del lote' });

  // Otro lote antes de que la caja aplique: reutiliza el artículo de la fila y sólo suma.
  const second = await captureBatch(q, admin, centro, { productId: concha, pieces: 6 });
  assert.equal(second.itemId, first.itemId);
  assert.equal(second.sent, 1);

  // La caja ya aplicó todo: artículo con saldo, pan ligado y marcado; un lote más sólo suma.
  await send(q, caja1, centro, [
    { entity: 'inventory_item', id: first.itemId, payload: { item: { id: first.itemId, code: 'PT-CM-001', name: 'Concha de vainilla', kind: 'FINISHED_GOOD', dimension: 'COUNT', active: 1 }, units: [],
      balances: [{ location_id: MAIN_LOCATION, quantity_atomic: 30 }] } },
    { entity: 'product', id: concha, change: 'update', payload: { ...product(concha, 'CM-001', 'Concha de vainilla').payload,
      product: { ...product(concha, 'CM-001', 'Concha de vainilla').payload.product, track_inventory: 1 }, inventoryLinks: [{ inventory_item_id: first.itemId, active: 1, quantity_per_product_unit_atomic: 1 }] } }]);
  await q(`INSERT INTO command_result(command_id, terminal_id, status) SELECT id, $1, 'applied' FROM branch_command WHERE branch_id = $2`, [caja1, centro]);
  const third = await captureBatch(q, admin, centro, { productId: concha, pieces: 12 });
  assert.deepEqual([third.itemId, third.sent], [first.itemId, 1]);
});

test('a batch is rejected with the reason when it cannot be captured', async () => {
  const { q, admin, manager } = await setup();
  for (const pieces of [0, 10_001, 2.5, 'doce'])
    await assert.rejects(captureBatch(q, admin, centro, { productId: concha, pieces }), /número entero de 1 a 10,000/);
  await assert.rejects(captureBatch(q, admin, centro, { productId: 'f1000000-0000-4000-8000-000000000099', pieces: 5 }), /no existe/);
  // Como en la caja: capturar lotes pide recibir y configurar inventario (el encargado no configura).
  await assert.rejects(captureBatch(q, manager, centro, { productId: bolillo, pieces: 5 }), /no permite/);
  // Un artículo con ese código que no es producto terminado no se mezcla.
  await send(q, caja1, centro, [{ entity: 'inventory_item', id: 'b1000000-0000-4000-8000-000000000001', payload: { item: { id: 'b1000000-0000-4000-8000-000000000001', code: 'PT-CM-003',
    name: 'Bolillo', kind: 'RAW_MATERIAL', dimension: 'COUNT', active: 1 }, units: [], balances: [] } }]);
  await assert.rejects(captureBatch(q, admin, centro, { productId: bolillo, pieces: 5 }), /no es un producto terminado/);
  assert.equal((await listCommands(q, centro, true)).length, 0, 'Nothing is queued when the batch is rejected.');
});
