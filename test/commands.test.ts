import { test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { ProtocolError } from '../lib/protocol.js';
import { login, verifyIdentityHash } from '../lib/auth.js';
import { createCommand, listCommands, pendingFor, recordResults } from '../lib/commands.js';
import { caja1, caja2, centro, database, norte, permissions, send, user } from './helpers.js';
import { route as routeView } from '../lib/web.js';

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
