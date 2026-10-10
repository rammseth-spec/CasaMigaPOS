// Producción en la web en dos pasos como la caja (docs/PLAN_COMPLETAR.md, W6): necesidades, alternativas y faltantes; preparar y registrar.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { ProtocolError } from '../lib/protocol.js';
import { login } from '../lib/auth.js';
import { createCommand, pendingFor } from '../lib/commands.js';
import { productionBatches, productionPlan } from '../lib/planning.js';
import { caja1, centro, database, permissions, send, user } from './helpers.js';

const status = (code: number) => (e: unknown) => e instanceof ProtocolError && e.status === code;
const MAIN = '10000000-0000-0000-0000-000000000001';
const category = 'c1000000-0000-4000-8000-000000000001';
const concha = 'f1000000-0000-4000-8000-000000000001';
const [flour, butter, margarine, eggs, finished] = ['01', '02', '03', '04', '05'].map(n => `aaaaaaaa-0000-4000-8000-0000000000${n}`);
const formula = 'b0000000-0000-4000-8000-000000000001', formulaV1 = 'b1000000-0000-4000-8000-000000000001';
const group = 'b3000000-0000-4000-8000-000000000001', optButter = 'b4000000-0000-4000-8000-000000000001', optMargarine = 'b4000000-0000-4000-8000-000000000002';
const profile = 'b2000000-0000-4000-8000-000000000001', profileV1 = 'b5000000-0000-4000-8000-000000000001';
const admin_ = 'a0000000-0000-4000-8000-000000000001', ana_ = 'a0000000-0000-4000-8000-000000000002';
const item = (id: string, code: string, name: string, dimension: string, balance: number | null, kind = 'RAW_MATERIAL') => ({ entity: 'inventory_item', id,
  payload: { item: { id, code, name, kind, dimension, active: 1 }, units: [], balances: balance === null ? [] : [{ location_id: MAIN, quantity_atomic: balance }] } });

async function seeded(butterStock = 0) {
  const { q } = await database();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, admin_, 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, ana_, 'ana', 'Manager', 'Ana#2026centro');
  await send(q, caja1, centro, [
    { entity: 'category', id: category, payload: { id: category, name: 'Pan dulce', active: 1, sort_order: 0 } },
    { entity: 'product', id: concha, payload: { product: { id: concha, name: 'Concha', category_id: category, price_cents: 1900, active: 1 }, inventoryLinks: [] } },
    item(flour, 'HAR', 'Harina', 'MASS', 5_000_000), item(butter, 'MAN', 'Mantequilla', 'MASS', butterStock), item(margarine, 'MAR', 'Margarina', 'MASS', 1_000_000),
    item(eggs, 'HUE', 'Huevo', 'COUNT', 10), item(finished, 'PT-CON', 'Concha terminada', 'COUNT', 0, 'FINISHED_GOOD'),
    { entity: 'formula', id: formula, payload: {
      formula: { id: formula, code: 'MASA', name: 'Masa dulce', active_version_id: formulaV1, revision: 2 },
      versions: [{ id: formulaV1, formula_id: formula, version: 1, name: 'Base', state: 'PUBLISHED', yield_dimension: 'MASS', yield_atomic: 1_000_000, notes: null, revision: 2 }],
      ingredients: [
        { version_id: formulaV1, sequence: 1, item_id: flour, dimension: 'MASS', quantity_atomic: 600_000, count_policy: 'NOT_APPLICABLE' },
        { version_id: formulaV1, sequence: 2, item_id: eggs, dimension: 'COUNT', quantity_atomic: 3, count_policy: 'CEILING_WHOLE_COUNT' }],
      groups: [{ version_id: formulaV1, id: group, sequence: 1 }],
      options: [
        { version_id: formulaV1, group_id: group, id: optButter, sequence: 1, item_id: butter, dimension: 'MASS', quantity_atomic: 100_000, count_policy: 'NOT_APPLICABLE', preference_rank: 1 },
        { version_id: formulaV1, group_id: group, id: optMargarine, sequence: 2, item_id: margarine, dimension: 'MASS', quantity_atomic: 120_000, count_policy: 'NOT_APPLICABLE', preference_rank: 2 }] } },
    { entity: 'production_profile', id: profile, payload: {
      profile: { id: profile, product_id: concha, finished_item_id: finished, active_version_id: profileV1, revision: 2 },
      versions: [{ id: profileV1, profile_id: profile, version: 1, name: 'Concha v1', state: 'PUBLISHED', revision: 2 }],
      directIngredients: [], formulaRequirements: [{ profile_version_id: profileV1, formula_version_id: formulaV1, sequence: 1, dimension: 'MASS', quantity_atomic: 60_000 }] } },
  ]);
  return q;
}

test('needs like the till: exact fractions, pieces rounded up, the preferred alternative and what is missing', async () => {
  const q = await seeded();
  const plan = await productionPlan(q, centro, { outputs: [{ profileVersionId: profileV1, planned: 5 }], selections: [] });
  assert.deepEqual(plan.formulas, [{ id: formulaV1, name: 'Base', version: 1, dimension: 'MASS', required: 300_000 }], '5 pieces × 60 g.');
  assert.deepEqual(plan.requirements.map(r => [r.name, r.required, r.available, r.shortage, r.state]), [
    ['Harina', 180_000, 5_000_000, 0, 'Suficiente'],
    ['Mantequilla', 30_000, 0, 30_000, 'Faltante'],
    ['Huevo', 1, 10, 0, 'Suficiente'],
  ],
    '0.3 × 600 g, 0.3 × 100 g of the first option, 0.9 eggs rounded up to 1.');
  assert.equal(plan.canConfirm, false);
  assert.equal(plan.problem, 'No hay existencia suficiente. Revisa todos los faltantes mostrados.');
  assert.deepEqual(plan.alternatives.map(a => [a.label, a.selected, a.options.map(o => o.label)]), [['Grupo 1 · Base', optButter, ['Mantequilla', 'Margarina']]]);

  const margarineChosen = await productionPlan(q, centro, { outputs: [{ profileVersionId: profileV1, planned: 5 }],
    selections: [{ formulaVersionId: formulaV1, groupId: group, optionId: optMargarine }] });
  assert.equal(margarineChosen.canConfirm, true, 'Choosing margarine resolves the shortage.');
  assert.equal(margarineChosen.requirements.find(r => r.itemId === margarine)!.required, 36_000);
});

test('prepare and register from the web are validated before reaching the till', async () => {
  const q = await seeded(1_000_000);
  const admin = await login(q, 'admin', 'Admin#2026centro');
  const ana = await login(q, 'ana', 'Ana#2026centro');
  const outputs = [{ profileVersionId: profileV1, planned: 5 }];
  await assert.rejects(createCommand(q, admin, centro, 'production.prepare', { batchId: randomUUID(), operationId: randomUUID(), outputs: [{ profileVersionId: profileV1, planned: 200_000 }] }), /excede|Cantidad|entero/);
  const batchId = randomUUID();
  await createCommand(q, ana, centro, 'production.prepare', { batchId, operationId: randomUUID(), outputs, selections: [], notes: 'Mañana' });
  await assert.rejects(createCommand(q, admin, centro, 'production.confirm', { batchId, actuals: [{ productId: concha, actual: 5 }] }), status(404), 'The draft has not arrived yet.');

  // La caja lo aplica y sube el borrador.
  await send(q, caja1, centro, [{ entity: 'production_batch', id: batchId, payload: {
    batch: { id: batchId, operation_id: randomUUID(), status: 'DRAFT', created_by: ana_, terminal_id: caja1, created_utc: '2026-10-09T15:00:00Z', confirmed_utc: null, notes: 'Mañana' },
    outputs: [{ product_id: concha, product_name: 'Concha', profile_name: 'Concha v1', planned_count: 5, actual_count: null }],
    inputs: [{ item_id: flour, item_name: 'Harina', dimension: 'MASS', required_atomic: 180_000, group_id: null }, { item_id: butter, item_name: 'Mantequilla', dimension: 'MASS', required_atomic: 30_000, group_id: group }],
    formulas: [{ formula_version_id: formulaV1, formula_name: 'Base', dimension: 'MASS', required_atomic: 300_000 }] } }]);
  const drafts = await productionBatches(q, centro, 'DRAFT');
  assert.deepEqual(drafts.map(d => [d.createdBy, d.terminal, d.outputs[0].planned, d.formulas[0].required, d.inputs.length]), [['ana (Manager)', 'CASA-MIGA-CAJA-01', 5, 300_000, 2]]);
  assert.equal((await productionBatches(q, centro, 'CONFIRMED')).length, 0);

  await assert.rejects(createCommand(q, admin, centro, 'production.confirm', { batchId, actuals: [{ productId: concha, actual: 5 }] }), status(403), 'Only who created the draft registers it.');
  await assert.rejects(createCommand(q, ana, centro, 'production.confirm', { batchId, actuals: [] }), /Piezas reales|cada producto/);
  await createCommand(q, ana, centro, 'production.confirm', { batchId, actuals: [{ productId: concha, actual: 4 }] });
  await assert.rejects(createCommand(q, ana, centro, 'production.confirm', { batchId, actuals: [{ productId: concha, actual: 4 }] }), /Ya se mandó/);
  const pulled = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  assert.deepEqual(pulled.map(c => c.kind), ['production.prepare', 'production.confirm']);
  assert.deepEqual(pulled[1].payload, { batchId, actuals: [{ productId: concha, actual: 4 }] });
});
