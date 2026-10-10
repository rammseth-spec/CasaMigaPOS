import { test } from 'node:test';
import assert from 'node:assert/strict';
import { login } from '../lib/auth.js';
import { createCommand, recordResults } from '../lib/commands.js';
import { ProtocolError } from '../lib/protocol.js';
import * as web from '../lib/web.js';
import { caja1, centro, database, permissions, send, user } from './helpers.js';

const pan = 'd0000000-0000-4000-8000-000000000001';
const concha = 'e0000000-0000-4000-8000-000000000001';
const terminal = { id: caja1, branchId: centro };
const conchaRow = (price: number, name = 'Concha de vainilla') =>
  ({ product: { id: concha, sku: null, name, category_id: pan, description: '', price_cents: price, track_inventory: 1, active: 1 }, inventoryLinks: [] });

async function setup() {
  const { q } = await database();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  await send(q, caja1, centro, [
    { entity: 'category', id: pan, payload: { id: pan, name: 'Pan dulce', description: '', sort_order: 1, active: 1 } },
    { entity: 'product', id: concha, payload: conchaRow(1300) },
  ]);
  return { q, admin: await login(q, 'admin', 'Admin#2026centro') };
}
const results = (q: Parameters<typeof recordResults>[0], ...r: { id: string; status: 'applied' | 'rejected'; message?: string }[]) => recordResults(q, terminal, { results: r });

test('catalog changes made on the web show up at once, marked until the register confirms them', async () => {
  const { q, admin } = await setup();
  const bebida = crypto.randomUUID(); const cafe = crypto.randomUUID();
  await createCommand(q, admin, centro, 'category.save', { id: bebida, name: 'Bebidas', description: '', sortOrder: 2 });
  const created = await createCommand(q, admin, centro, 'product.save', { id: cafe, categoryId: bebida, sku: null, name: 'Café', description: '', priceCents: 2000, trackInventory: false });
  const price = await createCommand(q, admin, centro, 'product.save', { id: concha, categoryId: pan, sku: null, name: 'Concha de vainilla', description: '', priceCents: 1500, trackInventory: true });

  let catalog = await web.catalog(q, centro);
  assert.deepEqual(catalog.categories.map(c => [c.name, c.pending]), [['Pan dulce', false], ['Bebidas', true]]);
  const byName = new Map(catalog.products.map(p => [p.name, p]));
  assert.deepEqual([byName.get('Café')!.price, byName.get('Café')!.active, byName.get('Café')!.pending, byName.get('Café')!.categoryId], [2000, true, true, bebida], 'New product, active, in its new category.');
  assert.deepEqual([byName.get('Concha de vainilla')!.price, byName.get('Concha de vainilla')!.pending], [1500, true], 'New price at once.');

  // La caja aplica el precio y sube su versión: gana y deja de estar pendiente. El café se aplica sin que la caja suba nada todavía.
  await send(q, caja1, centro, [{ entity: 'product', id: concha, payload: conchaRow(1500), change: 'update' }]);
  await results(q, { id: price.id, status: 'applied' }, { id: created.id, status: 'applied' });
  catalog = await web.catalog(q, centro);
  assert.deepEqual(catalog.products.map(p => [p.name, p.price, p.pending]), [['Café', 2000, false], ['Concha de vainilla', 1500, false]]);
});

test('a change the register rejects goes back to what the register has, keeping later pending changes', async () => {
  const { q, admin } = await setup();
  const first = await createCommand(q, admin, centro, 'product.save', { id: concha, categoryId: pan, sku: null, name: 'Concha', description: '', priceCents: 1500, trackInventory: true });
  const second = await createCommand(q, admin, centro, 'product.active', { id: concha, active: false });
  let concha_ = (await web.catalog(q, centro)).products[0];
  assert.deepEqual([concha_.name, concha_.price, concha_.active, concha_.pending], ['Concha', 1500, false, true]);

  await results(q, { id: first.id, status: 'rejected', message: 'Ya existe un producto con ese nombre.' });
  concha_ = (await web.catalog(q, centro)).products[0];
  assert.deepEqual([concha_.name, concha_.price, concha_.active, concha_.pending], ['Concha de vainilla', 1300, false, true], 'Back to the register\'s name and price; the deactivation is still pending.');

  await results(q, { id: second.id, status: 'rejected', message: 'x' });
  concha_ = (await web.catalog(q, centro)).products[0];
  assert.deepEqual([concha_.name, concha_.price, concha_.active, concha_.pending], ['Concha de vainilla', 1300, true, false]);
});

test('a user created or reset on the web can sign in on the web right away; if the register rejects it, it disappears', async () => {
  const { q, admin } = await setup();
  const created = await createCommand(q, admin, centro, 'user.create', { username: 'pepe', displayName: 'Pepe', role: 'Cashier' });
  const listed = (await web.users(q, centro)).find(u => u.username === 'PEPE')!;
  assert.deepEqual([listed.name, listed.role, listed.mustChangePassword, listed.pending], ['Pepe', 'Cashier', true, true]);
  await assert.rejects(login(q, 'pepe', created.temporaryPassword!), (e: unknown) => e instanceof ProtocolError && e.status === 428, 'The temporary password works and asks for a new one.');
  await assert.rejects(createCommand(q, admin, centro, 'user.create', { username: 'PEPE', displayName: 'Otro', role: 'Cashier' }), /ya existe/);

  await results(q, { id: created.id, status: 'rejected', message: 'Ese usuario ya existe.' });
  assert.equal((await web.users(q, centro)).some(u => u.username === 'PEPE'), false);
  await assert.rejects(login(q, 'pepe', created.temporaryPassword!), (e: unknown) => e instanceof ProtocolError && e.status === 401);
});

test('stores and their usual tray show up at once too', async () => {
  const { q, admin } = await setup();
  const store = crypto.randomUUID();
  await createCommand(q, admin, centro, 'consignment.store.save', { id: store, name: 'Abarrotes Lupita', contact: 'Lupita', phone: '', address: '' });
  await createCommand(q, admin, centro, 'consignment.template.save', { storeId: store, lines: [{ productId: concha, quantity: 6 }] });
  const route = await web.route(q, centro);
  const lupita = route.allStores.find(s => s.id === store)!;
  assert.deepEqual([lupita.name, lupita.active, lupita.pending, lupita.template.map(t => [t.name, t.quantity])], ['Abarrotes Lupita', true, true, [['Concha de vainilla', 6]]]);
});

test('an older edit the register uploads meanwhile does not hide the web change still waiting', async () => {
  const { q, admin } = await setup();
  const change = await createCommand(q, admin, centro, 'product.save', { id: concha, categoryId: pan, sku: null, name: 'Concha de vainilla', description: '', priceCents: 1600, trackInventory: true });
  // La caja (sin conexión) había cambiado la descripción; la sube antes de aplicar lo de la web.
  await send(q, caja1, centro, [{ entity: 'product', id: concha, payload: { ...conchaRow(1300), product: { ...conchaRow(1300).product, description: 'Receta de la casa' } }, change: 'update' }]);
  let row = (await web.catalog(q, centro)).products[0];
  assert.deepEqual([row.price, row.description, row.pending], [1600, '', true], 'The web change goes on top again.');
  await results(q, { id: change.id, status: 'applied' });
  row = (await web.catalog(q, centro)).products[0];
  assert.deepEqual([row.price, row.pending], [1600, false]);
});
