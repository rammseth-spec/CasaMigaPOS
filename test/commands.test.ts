import { test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { ProtocolError } from '../lib/protocol.js';
import { login, verifyIdentityHash } from '../lib/auth.js';
import { createCommand, listCommands, pendingFor, recordResults } from '../lib/commands.js';
import { caja1, caja2, centro, database, norte, permissions, send, user } from './helpers.js';
import { inventory as inventoryView, route as routeView } from '../lib/web.js';

const status = (code: number) => (e: unknown) => e instanceof ProtocolError && e.status === code;
const category = 'c1000000-0000-4000-8000-000000000001';
const product = 'f1000000-0000-4000-8000-000000000001';
const store = 'e1000000-0000-4000-8000-000000000001';

async function seeded() {
  const { q } = await database();
  await permissions(q, caja1, centro); await permissions(q, caja2, norte);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000002', 'ana', 'Manager', 'Ana#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000003', 'luis', 'Cashier', 'Luis#2026centro');
  await send(q, caja1, centro, [
    { entity: 'category', id: category, payload: { id: category, name: 'Pan dulce', active: 1, sort_order: 0 } },
    { entity: 'product', id: product, payload: { product: { id: product, name: 'Concha', category_id: category, price_cents: 1900, active: 1 }, inventoryLinks: [] } },
    { entity: 'consignment_store', id: store, payload: { store: { id: store, name: 'Abarrotes Lupita', active: 1 }, template: [] } },
  ]);
  return q;
}

test('managers change prices and products of their branch only; the terminal reads them in order', async () => {
  const q = await seeded();
  const ana = await login(q, 'ana', 'Ana#2026centro');
  const price = await createCommand(q, ana, centro, 'product.save', { id: product, categoryId: category, sku: 'CM-1', name: 'Concha', priceCents: 2000 });
  const fresh = randomUUID();
  await createCommand(q, ana, centro, 'product.save', { id: fresh, categoryId: category, sku: '', name: 'Rol de canela', description: 'Nuevo', priceCents: 2500, trackInventory: false });
  await createCommand(q, ana, centro, 'product.active', { id: product, active: false });
  await assert.rejects(createCommand(q, ana, norte, 'product.save', { id: product, categoryId: category, name: 'X', priceCents: 1 }), status(403));
  await assert.rejects(createCommand(q, ana, centro, 'product.save', { id: product, categoryId: randomUUID(), name: 'X', priceCents: 1 }), /categoría/);
  await assert.rejects(createCommand(q, ana, centro, 'product.save', { id: product, categoryId: category, name: 'X', priceCents: -1 }), /Precio/);
  await assert.rejects(createCommand(q, ana, centro, 'product.save', { id: product, categoryId: category, name: 'Línea\nnueva', priceCents: 1 }), /saltos/);
  await assert.rejects(createCommand(q, ana, centro, 'user.create', { username: 'x', displayName: 'X', role: 'Cashier' }), status(403), 'Managers do not manage users.');
  await assert.rejects(createCommand(q, ana, centro, 'sale.delete', {}), /desconocido/);

  const first = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  assert.deepEqual(first.map(c => c.kind), ['product.save', 'product.save', 'product.active']);
  assert.equal((first[0].payload as any).priceCents, 2000);
  assert.equal(first[0].id, price.id);
  assert.equal((await pendingFor(q, { id: caja1, branchId: centro }, first[1].seq)).length, 1, 'The cursor skips what was already read.');
  assert.equal((await pendingFor(q, { id: caja2, branchId: norte }, 0)).length, 0, 'Other branches never see them.');

  assert.equal((await listCommands(q, centro))[2].status, 'pending');
  await recordResults(q, { id: caja1, branchId: centro }, { results: [{ id: price.id, status: 'applied' }, { id: first[1].id, status: 'rejected', message: 'El SKU ya existe.' }] });
  await recordResults(q, { id: caja2, branchId: norte }, { results: [{ id: first[2].id, status: 'applied' }] });
  const listed = await listCommands(q, centro);
  assert.equal(listed.find(c => c.id === price.id)!.status, 'applied');
  assert.equal(listed.find(c => c.id === first[1].id)!.message, 'El SKU ya existe.');
  assert.equal(listed.find(c => c.id === first[2].id)!.status, 'pending', 'A terminal of another branch cannot answer for this one.');
});

test('the administrator creates users with a temporary password the terminal accepts; the hash never reaches the web list', async () => {
  const q = await seeded();
  const boss = await login(q, 'admin', 'Admin#2026centro');
  const created = await createCommand(q, boss, centro, 'user.create', { username: 'maria', displayName: 'María', role: 'Cashier' });
  assert.ok(created.temporaryPassword);
  const [command] = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  const payload = command.payload as { passwordHash: string; id: string; role: string };
  assert.equal(await verifyIdentityHash(payload.passwordHash, created.temporaryPassword!), true);
  assert.equal(payload.role, 'Cashier');
  assert.equal(JSON.stringify(await listCommands(q, centro)).includes(payload.passwordHash), false);

  await assert.rejects(createCommand(q, boss, centro, 'user.create', { username: 'ANA', displayName: 'Otra', role: 'Cashier' }), /ya existe/);
  await assert.rejects(createCommand(q, boss, centro, 'user.create', { username: 'jefe', displayName: 'Otro', role: 'Administrator' }), /Rol/);
  await assert.rejects(createCommand(q, boss, centro, 'user.update', { id: 'a0000000-0000-4000-8000-000000000001', displayName: 'Yo', role: 'Manager' }), /administrador/);
  const reset = await createCommand(q, boss, centro, 'user.reset_password', { id: 'a0000000-0000-4000-8000-000000000003' });
  assert.ok(reset.temporaryPassword);
  await createCommand(q, boss, norte, 'consignment.store.save', { id: randomUUID(), name: 'Tienda Norte', contact: '', phone: '', address: '' });
});

test('tray templates are validated before reaching the terminal', async () => {
  const q = await seeded();
  const ana = await login(q, 'ana', 'Ana#2026centro');
  await createCommand(q, ana, centro, 'consignment.template.save', { storeId: store, lines: [{ productId: product, quantity: 6 }] });
  await assert.rejects(createCommand(q, ana, centro, 'consignment.template.save', { storeId: store, lines: [{ productId: product, quantity: 6 }, { productId: product, quantity: 1 }] }), /repetido/);
  await assert.rejects(createCommand(q, ana, centro, 'consignment.template.save', { storeId: store, lines: [{ productId: product, quantity: 0 }] }), /Cantidad/);
  await assert.rejects(createCommand(q, ana, centro, 'consignment.template.save', { storeId: randomUUID(), lines: [] }), status(404));
  await assert.rejects(recordResults(q, { id: caja1, branchId: centro }, { results: [{ id: 'no', status: 'applied' }] }), ProtocolError);
});

test('route from the phone: deliver, collect once, pay and void are validated against what the terminal uploaded', async () => {
  const q = await seeded();
  const ana = await login(q, 'ana', 'Ana#2026centro');
  const delivery = 'd1000000-0000-4000-8000-000000000001';
  await send(q, caja1, centro, [
    { entity: 'consignment_price', id: product, payload: { product_id: product, price_cents: 1500 } },
    { entity: 'consignment_delivery', id: delivery, payload: { delivery: { id: delivery, store_id: store, delivered_utc: '2026-10-07T14:00:00Z', note: '' },
      items: [{ position: 1, product_name: 'Concha', quantity: 6, unit_price_cents: 1500 }], settlement: null, settlementItems: [], void: null } },
  ]);
  await createCommand(q, ana, centro, 'consignment.deliver', { storeId: store, lines: [{ productId: product, quantity: 6 }], note: '' });
  await assert.rejects(createCommand(q, ana, centro, 'consignment.deliver', { storeId: store, lines: [] }), /al menos/);
  await assert.rejects(createCommand(q, ana, centro, 'consignment.deliver', { storeId: store, lines: [{ productId: randomUUID(), quantity: 1 }] }), /precio de charola/);

  await assert.rejects(createCommand(q, ana, centro, 'consignment.collect', { deliveryId: delivery, returned: [{ position: 1, quantity: 7 }], paidCents: 0 }), /regresan/);
  await assert.rejects(createCommand(q, ana, centro, 'consignment.collect', { deliveryId: delivery, returned: [{ position: 2, quantity: 1 }], paidCents: 0 }), /partida/);
  const collect = await createCommand(q, ana, centro, 'consignment.collect', { deliveryId: delivery, returned: [{ position: 1, quantity: 2 }], paidCents: 6000, method: 'CASH', note: '' });
  await assert.rejects(createCommand(q, ana, centro, 'consignment.collect', { deliveryId: delivery, returned: [], paidCents: 0 }), /espera a que la caja/, 'Two phones cannot collect the same tray.');
  await assert.rejects(createCommand(q, ana, centro, 'consignment.void', { deliveryId: delivery, reason: 'Cerrada' }), /espera a que la caja/);

  const view = await routeView(q, centro);
  assert.equal(view.stores[0].atStore.length, 1);
  assert.deepEqual(view.stores[0].sent.map((c: { kind: string }) => c.kind), ['consignment.deliver', 'consignment.collect']);
  assert.deepEqual(view.products.map(p => p.name), ['Concha']);

  // Si la caja la rechaza, se puede volver a mandar.
  await recordResults(q, { id: caja1, branchId: centro }, { results: [{ id: collect.id, status: 'rejected', message: 'El cobro no puede ser mayor.' }] });
  await createCommand(q, ana, centro, 'consignment.collect', { deliveryId: delivery, returned: [], paidCents: 0 });
  await createCommand(q, ana, centro, 'consignment.payment', { storeId: store, amountCents: 1000, method: 'TRANSFER', reference: 'SPEI 123', note: '' });
  await assert.rejects(createCommand(q, ana, centro, 'consignment.payment', { storeId: store, amountCents: 0, method: 'CASH' }), /Importe/);
  await assert.rejects(createCommand(q, ana, centro, 'consignment.payment', { storeId: store, amountCents: 100, method: 'CHEQUE' }), /Forma de pago/);
  const [pulled] = (await pendingFor(q, { id: caja1, branchId: centro }, 0)).filter(c => c.kind === 'consignment.payment');
  assert.deepEqual(pulled.payload, { storeId: store, amountCents: 1000, method: 'TRANSFER', reference: 'SPEI 123', note: '' });
});

const flour = 'aaaaaaaa-0000-4000-8000-000000000001';
const MAIN = '10000000-0000-0000-0000-000000000001';

test('inventory from the web: receive, waste and count are validated before reaching the terminal', async () => {
  const q = await seeded();
  const inactive = 'aaaaaaaa-0000-4000-8000-000000000002';
  await send(q, caja1, centro, [
    { entity: 'inventory_item', id: flour, payload: { item: { id: flour, code: 'HAR', name: 'Harina', kind: 'RAW_MATERIAL', dimension: 'MASS', active: 1 },
      units: [{ code: 'BULTO', name: 'Bulto 44 kg', atomic_units_per_whole_unit: 44_000_000, active: 1 }], balances: [{ location_id: MAIN, quantity_atomic: 5_000_000 }] } },
    { entity: 'inventory_item', id: inactive, payload: { item: { id: inactive, code: 'OLD', name: 'Viejo', kind: 'SUPPLY', dimension: 'COUNT', active: 0 }, units: [], balances: [] } },
  ]);
  const ana = await login(q, 'ana', 'Ana#2026centro');
  const admin = await login(q, 'admin', 'Admin#2026centro');
  const luis = await login(q, 'luis', 'Luis#2026centro');

  await createCommand(q, ana, centro, 'inventory.receive', { itemId: flour, quantity: '2.5', unitCode: 'kg', reference: 'Factura 18', note: '' });
  await createCommand(q, ana, centro, 'inventory.receive', { itemId: flour, locationId: MAIN, quantity: '2', unitCode: 'BULTO' });
  await assert.rejects(createCommand(q, ana, centro, 'inventory.receive', { itemId: flour, quantity: '1.5', unitCode: 'BULTO' }), /enteros/);
  await assert.rejects(createCommand(q, ana, centro, 'inventory.receive', { itemId: flour, quantity: '0', unitCode: 'kg' }), /mayor que cero/);
  await assert.rejects(createCommand(q, ana, centro, 'inventory.receive', { itemId: flour, quantity: '1', unitCode: 'piece' }), /no corresponde/);
  await assert.rejects(createCommand(q, ana, centro, 'inventory.receive', { itemId: flour, quantity: '0.5', unitCode: 'mg' }), /decimales/);
  await assert.rejects(createCommand(q, ana, centro, 'inventory.receive', { itemId: flour, quantity: 2, unitCode: 'kg' }), /Cantidad/, 'Quantities travel as text.');
  await assert.rejects(createCommand(q, ana, centro, 'inventory.receive', { itemId: inactive, quantity: '1', unitCode: 'piece' }), /inactivo/);
  await assert.rejects(createCommand(q, ana, centro, 'inventory.receive', { itemId: flour, locationId: randomUUID(), quantity: '1', unitCode: 'kg' }), status(404));
  await assert.rejects(createCommand(q, luis, centro, 'inventory.receive', { itemId: flour, quantity: '1', unitCode: 'kg' }), status(403));

  await createCommand(q, ana, centro, 'inventory.waste', { itemId: flour, quantity: '300', unitCode: 'g', reason: 'Damaged' });
  await assert.rejects(createCommand(q, ana, centro, 'inventory.waste', { itemId: flour, quantity: '1', unitCode: 'kg', reason: 'Other', note: ' ' }), /observación/);
  await assert.rejects(createCommand(q, ana, centro, 'inventory.waste', { itemId: flour, quantity: '1', unitCode: 'kg', reason: 'Robo' }), /Motivo/);

  await assert.rejects(createCommand(q, ana, centro, 'inventory.count', { itemId: flour, quantity: '4', unitCode: 'kg' }), status(403), 'Only the administrator adjusts.');
  await createCommand(q, admin, centro, 'inventory.count', { itemId: flour, quantity: '0', unitCode: 'kg' });

  const pulled = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  assert.deepEqual(pulled.map(c => c.payload), [
    { itemId: flour, locationId: MAIN, quantity: '2.5', unitCode: 'kg', reference: 'Factura 18' },
    { itemId: flour, locationId: MAIN, quantity: '2', unitCode: 'BULTO' },
    { itemId: flour, locationId: MAIN, quantity: '300', unitCode: 'g', reason: 'Damaged' },
    { itemId: flour, locationId: MAIN, quantity: '0', unitCode: 'kg', reason: 'PhysicalCountVariance' },
  ]);
  const view = await inventoryView(q, centro, 'America/Mexico_City');
  assert.deepEqual(view.locations, [{ id: MAIN, branchId: centro, name: 'Almacén principal' }], 'The main warehouse is always there.');
  assert.equal(view.items.find(i => i.id === flour)!.balances[0].locationId, MAIN);
});

/** JPEG mínimo: inicio, cuadro SOF0 con alto y ancho, fin. Basta para validar su forma en la nube. */
function jpeg(width: number, height: number): string {
  const sof = [0xff, 0xc0, 0x00, 0x11, 0x08, height >> 8, height & 255, width >> 8, width & 255, 0x03, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1];
  return Buffer.from([0xff, 0xd8, ...sof, 0xff, 0xd9]).toString('base64');
}

test('production and photos from the web reach the terminal; photos never reach the list of changes', async () => {
  const q = await seeded();
  const profile = 'cccccccc-0000-4000-8000-000000000001', active = 'cccccccc-0000-4000-8000-000000000002', draft = 'cccccccc-0000-4000-8000-000000000003';
  await send(q, caja1, centro, [{ entity: 'production_profile', id: profile, payload: { profile: { id: profile, product_id: product, active_version_id: active },
    versions: [{ id: active, version: 1, state: 'PUBLISHED' }, { id: draft, version: 2, state: 'DRAFT' }], directIngredients: [], formulaRequirements: [] } }]);
  const ana = await login(q, 'ana', 'Ana#2026centro');
  const luis = await login(q, 'luis', 'Luis#2026centro');

  await createCommand(q, ana, centro, 'production.record', { outputs: [{ profileVersionId: active, planned: 24, actual: 22 }], notes: 'Mañana' });
  await assert.rejects(createCommand(q, ana, centro, 'production.record', { outputs: [{ profileVersionId: draft, planned: 1, actual: 1 }] }), /cambió/, 'Only the active published recipe.');
  await assert.rejects(createCommand(q, ana, centro, 'production.record', { outputs: [{ profileVersionId: active, planned: 1, actual: 1 }, { profileVersionId: active, planned: 2, actual: 2 }] }), /repetido/);
  await assert.rejects(createCommand(q, ana, centro, 'production.record', { outputs: [{ profileVersionId: active, planned: 0, actual: 1 }] }), /planeadas/);
  await assert.rejects(createCommand(q, ana, centro, 'production.record', { outputs: [] }), /1 a 20/);
  await assert.rejects(createCommand(q, luis, centro, 'production.record', { outputs: [{ profileVersionId: active, planned: 1, actual: 1 }] }), status(403));

  const photo = await createCommand(q, ana, centro, 'product.photo', { productId: product, image: jpeg(512, 512) });
  await assert.rejects(createCommand(q, ana, centro, 'product.photo', { productId: product, image: jpeg(512, 300) }), /cuadrada/);
  await assert.rejects(createCommand(q, ana, centro, 'product.photo', { productId: product, image: Buffer.from([0x89, 0x50, 0x4e, 0x47, 13, 10, 26, 10]).toString('base64') }), /JPEG/);
  await assert.rejects(createCommand(q, ana, centro, 'product.photo', { productId: randomUUID(), image: jpeg(512, 512) }), status(404));
  await assert.rejects(createCommand(q, luis, centro, 'product.photo', { productId: product, image: jpeg(512, 512) }), status(403));
  // Un producto recién dado de alta en la web puede llevar su foto de una vez.
  const fresh = randomUUID();
  await createCommand(q, ana, centro, 'product.save', { id: fresh, categoryId: category, name: 'Rol de canela', priceCents: 2500 });
  await createCommand(q, ana, centro, 'product.photo', { productId: fresh, image: jpeg(1024, 1024) });

  const pulled = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  assert.deepEqual(pulled[0].payload, { outputs: [{ profileVersionId: active, planned: 24, actual: 22 }], notes: 'Mañana' });
  assert.deepEqual(pulled[1].payload, { productId: product, image: jpeg(512, 512), edge: 512, name: 'foto-web.jpg' });
  assert.equal((pulled[3].payload as { edge: number }).edge, 1024);
  const listed = await listCommands(q, centro);
  assert.ok(listed.every(c => !('image' in c.payload)), 'The photo bytes stay out of the web list.');

  // Lo que la caja ya reportó no vuelve a bajar; las cajas web no cuentan como cajas que aplican cambios.
  await q(`INSERT INTO terminal(id, branch_id, name, key_sha256, kind) VALUES ($1, $2, 'CAJA-WEB-02', repeat('c', 64), 'web')`, [randomUUID(), centro]);
  await recordResults(q, { id: caja1, branchId: centro }, { results: [{ id: photo.id, status: 'applied' }] });
  assert.ok(!(await pendingFor(q, { id: caja1, branchId: centro }, 0)).some(c => c.id === photo.id));
  assert.equal((await listCommands(q, centro)).find(c => c.id === photo.id)!.status, 'applied');
});

test('a terminal never downloads more than about 3 MB of changes at once', async () => {
  const q = await seeded();
  const ana = await login(q, 'ana', 'Ana#2026centro');
  const big = 'A'.repeat(1_000_000 - 2) + '==';
  // Cuatro filas de ~1 MB: sólo caben tres; la cuarta baja en la siguiente vuelta.
  for (let i = 0; i < 4; i++) await q(`INSERT INTO branch_command(id, branch_id, kind, payload, requested_by_id, requested_by_name) VALUES ($1, $2, 'product.photo', $3::jsonb, $4, 'Ana')`,
    [randomUUID(), centro, JSON.stringify({ productId: product, image: big, edge: 512, name: 'x.jpg' }), ana.id]);
  const first = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  assert.equal(first.length, 3);
  assert.equal((await pendingFor(q, { id: caja1, branchId: centro }, first[2].seq)).length, 1);
});
