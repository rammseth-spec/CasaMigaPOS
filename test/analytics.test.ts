import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseRange } from '../lib/panel.js';
import { productCosts, profit, trayAnalytics } from '../lib/analytics.js';
import { caja1, centro, database, sale, send } from './helpers.js';

const range = (from: string, to: string) => ({ ...parseRange(new URL(`https://x/?from=${from}&to=${to}`)), branchId: centro });
const KG = 1_000_000; // mg

async function seedRecipes(q: Awaited<ReturnType<typeof database>>['q']) {
  const item = (id: string, name: string, dimension: string, cost: [number, number] | null) => ({ entity: 'inventory_item', id, payload: {
    item: { id, code: id, name, kind: 'RAW_MATERIAL', dimension, active: 1 }, units: [], balances: [],
    cost: cost ? { cost_cents: cost[0], per_atomic: cost[1], unit_label: 'kg', updated_utc: '2026-10-01T00:00:00Z' } : null } });
  await send(q, caja1, centro, [
    item('harina', 'Harina', 'MASS', [2000, KG]),      // $20/kg
    item('azucar', 'Azúcar', 'MASS', [3000, KG]),      // $30/kg
    item('huevo', 'Huevo', 'COUNT', [300, 1]),         // $3/pieza
    item('mantequilla', 'Mantequilla', 'MASS', null),  // sin costo
    { entity: 'formula', id: 'f1', payload: { formula: { id: 'f1', code: 'MASA', name: 'Masa dulce', active_version_id: 'fv1' },
      versions: [{ id: 'fv1', formula_id: 'f1', version: 1, state: 'PUBLISHED', yield_dimension: 'MASS', yield_atomic: 1000 * 1000 }],
      ingredients: [{ version_id: 'fv1', sequence: 1, item_id: 'harina', quantity_atomic: 600_000 }, { version_id: 'fv1', sequence: 2, item_id: 'azucar', quantity_atomic: 200_000 }],
      groups: [{ version_id: 'fv1', id: 'g1', sequence: 1, name: 'Grasa o huevo' }],
      options: [{ version_id: 'fv1', group_id: 'g1', id: 'o2', sequence: 2, item_id: 'mantequilla', quantity_atomic: 50_000, preference_rank: 2 },
        { version_id: 'fv1', group_id: 'g1', id: 'o1', sequence: 1, item_id: 'huevo', quantity_atomic: 4, preference_rank: 1 }] } },
    // Concha: 10 g de azúcar directo + 60 g de masa por pieza.
    { entity: 'production_profile', id: 'pp1', payload: { profile: { id: 'pp1', product_id: 'p-Concha', active_version_id: 'pv1' },
      versions: [{ id: 'pv1', profile_id: 'pp1', version: 1, state: 'PUBLISHED' }],
      directIngredients: [{ profile_version_id: 'pv1', sequence: 1, item_id: 'azucar', quantity_atomic: 10_000 }],
      formulaRequirements: [{ profile_version_id: 'pv1', formula_version_id: 'fv1', sequence: 1, quantity_atomic: 60_000 }] } },
    // Rol: lleva mantequilla directa, que no tiene costo.
    { entity: 'production_profile', id: 'pp2', payload: { profile: { id: 'pp2', product_id: 'p-Rol', active_version_id: 'pv2' },
      versions: [{ id: 'pv2', profile_id: 'pp2', version: 1, state: 'PUBLISHED' }],
      directIngredients: [{ profile_version_id: 'pv2', sequence: 1, item_id: 'mantequilla', quantity_atomic: 20_000 }], formulaRequirements: [] } },
    ...['Concha', 'Dona', 'Rol'].map(name => ({ entity: 'product', id: `p-${name}`, payload: { product: { id: `p-${name}`, name, price_cents: 1900, active: 1 }, inventoryLinks: [] } })),
  ]);
}

test('cost per piece comes from the recipe: direct ingredients + share of each preparation (preferred alternative)', async () => {
  const { q } = await database(); await seedRecipes(q);
  const costs = await productCosts(q, centro);
  // Masa: harina 600 g × $20/kg = $12 + azúcar 200 g × $30/kg = $6 + 4 huevos × $3 = $12 → $30 por kg de masa.
  // Concha: azúcar 10 g = $0.30 + masa 60 g = $1.80 → $2.10.
  assert.equal(costs.get(`${centro}/p-Concha`)!.cost, 210);
  assert.deepEqual(costs.get(`${centro}/p-Rol`), { productId: 'p-Rol', branchId: centro, name: 'Rol', cost: null, missing: ['Mantequilla'], hasRecipe: true });
  assert.equal(costs.get(`${centro}/p-Dona`)!.hasRecipe, false);
});

test('profit compares the counter with trays; returned tray pieces count as lost cost', async () => {
  const { q } = await database(); await seedRecipes(q);
  await send(q, caja1, centro, [
    { entity: 'sale', id: 's1', payload: sale('s1', 'c1', '2026-10-06T15:00:00Z', 'CASH', [['Concha', 10, 1900], ['Dona', 2, 1300]]) },
    { entity: 'sale_refund', id: 'r1', payload: { refund: { id: 'r1', sale_id: 's1', refunded_utc: '2026-10-06T16:00:00Z', total_cents: 1900, kind: 'RETURN' }, items: [{ position: 1, quantity: 1, line_total_cents: 1900 }] } },
    { entity: 'consignment_delivery', id: 'd1', payload: { delivery: { id: 'd1', store_id: 'st1', delivered_utc: '2026-10-05T14:00:00Z' },
      items: [{ position: 1, product_id: 'p-Concha', product_name: 'Concha', quantity: 6, unit_price_cents: 1500 }],
      settlement: { delivery_id: 'd1', collected_utc: '2026-10-06T14:00:00Z', sold_cents: 6000 }, settlementItems: [{ position: 1, sold: 4, returned: 2, line_total_cents: 6000 }], void: null } },
  ]);
  const p = await profit(q, range('2026-10-06', '2026-10-06'));
  const concha = p.products.find(x => x.name === 'Concha')!;
  assert.deepEqual([concha.counterPieces, concha.counterRevenue, concha.trayPieces, concha.trayRevenue, concha.returned], [9, 17100, 4, 6000, 2]);
  assert.equal(concha.counterProfit, 17100 - 210 * 9);
  assert.equal(concha.trayProfit, 6000 - 210 * 6, 'The two pieces that came back cost too.');
  assert.equal(concha.perPieceCounter, 1900 - 210);
  assert.equal(concha.perPieceTray, 1500 - 210);
  assert.equal(p.counter.revenue, 17100 + 2600);
  assert.equal(p.counter.coveredRevenue, 17100, 'Bread without cost is shown but left out of profit.');
  assert.equal(p.trays.returnedCost, 420);
  assert.equal(p.products.find(x => x.name === 'Dona')!.counterProfit, null);
});

test('tray analytics: order frequency, usual days, best sellers and sell-through per store', async () => {
  const { q } = await database();
  const delivery = (id: string, at: string, sold: number, returned: number) => ({ entity: 'consignment_delivery', id, payload: {
    delivery: { id, store_id: 'st1', delivered_utc: at },
    items: [{ position: 1, product_id: 'p1', product_name: 'Concha', quantity: sold + returned, unit_price_cents: 1500 }, { position: 2, product_id: 'p2', product_name: 'Dona', quantity: 3, unit_price_cents: 1200 }],
    settlement: { sold_cents: sold * 1500 + 3 * 1200, collected_utc: at }, settlementItems: [{ position: 1, sold, returned, line_total_cents: sold * 1500 }, { position: 2, sold: 3, returned: 0, line_total_cents: 3600 }], void: null } });
  await send(q, caja1, centro, [
    { entity: 'consignment_store', id: 'st1', payload: { store: { id: 'st1', name: 'Abarrotes Lupita', active: 1 }, template: [] } },
    { entity: 'consignment_store', id: 'st2', payload: { store: { id: 'st2', name: 'Sin pedidos', active: 1 }, template: [] } },
    delivery('d1', '2026-10-05T14:00:00Z', 4, 2), delivery('d2', '2026-10-07T14:00:00Z', 6, 0), delivery('d3', '2026-10-09T14:00:00Z', 5, 1),
  ]);
  const [lupita, none] = await trayAnalytics(q, range('2026-10-01', '2026-10-10'));
  assert.equal(lupita.name, 'Abarrotes Lupita');
  assert.equal(lupita.deliveries, 3); assert.equal(lupita.everyDays, 2);
  assert.deepEqual(lupita.usualDays.sort(), ['lunes', 'miércoles', 'viernes']);
  assert.deepEqual(lupita.products.map(p => [p.name, p.sold]), [['Concha', 15], ['Dona', 9]]);
  assert.equal(lupita.sellThrough, 24 / 27);
  assert.equal(none.deliveries, 0); assert.equal(none.everyDays, null);
});
