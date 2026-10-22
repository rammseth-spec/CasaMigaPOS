// Recetas en la web como la caja (docs/PLAN_COMPLETAR.md, W5): datos con todas las versiones y validación de los cambios recipe.*.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { ProtocolError } from '../lib/protocol.js';
import { login } from '../lib/auth.js';
import { createCommand, pendingFor, recordResults } from '../lib/commands.js';
import { recipes } from '../lib/web.js';
import { caja1, centro, database, permissions, send, user } from './helpers.js';

const status = (code: number) => (e: unknown) => e instanceof ProtocolError && e.status === code;
const category = 'c1000000-0000-4000-8000-000000000001';
const concha = 'f1000000-0000-4000-8000-000000000001';
const dona = 'f1000000-0000-4000-8000-000000000002';
const flour = 'aaaaaaaa-0000-4000-8000-000000000001';
const eggs = 'aaaaaaaa-0000-4000-8000-000000000002';
const butter = 'aaaaaaaa-0000-4000-8000-000000000003';
const finished = 'aaaaaaaa-0000-4000-8000-000000000004';
const formula = 'b0000000-0000-4000-8000-000000000001';
const published = 'b1000000-0000-4000-8000-000000000001';
const draft = 'b1000000-0000-4000-8000-000000000002';
const profile = 'b2000000-0000-4000-8000-000000000001';
const item = (id: string, code: string, name: string, kind: string, dimension: string) =>
  ({ entity: 'inventory_item', id, payload: { item: { id, code, name, kind, dimension, active: 1 }, units: [], balances: [] } });

// draftPendingMeasure: «Por medir» del borrador como lo manda una caja con la migración 032; sin él, como una caja anterior.
async function seeded(draftPendingMeasure?: number) {
  const { q } = await database();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000002', 'ana', 'Manager', 'Ana#2026centro');
  await send(q, caja1, centro, [
    { entity: 'category', id: category, payload: { id: category, name: 'Pan dulce', active: 1, sort_order: 0 } },
    { entity: 'product', id: concha, payload: { product: { id: concha, name: 'Concha', category_id: category, price_cents: 1900, active: 1 }, inventoryLinks: [] } },
    { entity: 'product', id: dona, payload: { product: { id: dona, name: 'Dona', category_id: category, price_cents: 1500, active: 1 }, inventoryLinks: [] } },
    item(flour, 'HAR', 'Harina', 'RAW_MATERIAL', 'MASS'), item(eggs, 'HUE', 'Huevo', 'RAW_MATERIAL', 'COUNT'), item(butter, 'MAN', 'Mantequilla', 'RAW_MATERIAL', 'MASS'),
    item(finished, 'PT-CON', 'Concha terminada', 'FINISHED_GOOD', 'COUNT'),
    { entity: 'formula', id: formula, payload: {
      formula: { id: formula, code: 'MASA', name: 'Masa dulce', active_version_id: published, revision: 3 },
      versions: [
        { id: published, formula_id: formula, version: 1, name: 'Base', state: 'PUBLISHED', yield_dimension: 'MASS', yield_atomic: 1_000_000, notes: null, revision: 2 },
        { id: draft, formula_id: formula, version: 2, name: 'Más mantequilla', state: 'DRAFT', yield_dimension: 'MASS', yield_atomic: 1_200_000, notes: 'Prueba', revision: 4,
          ...(draftPendingMeasure === undefined ? {} : { pending_measure: draftPendingMeasure }) }],
      ingredients: [
        { version_id: published, sequence: 1, item_id: flour, dimension: 'MASS', quantity_atomic: 600_000, count_policy: 'NOT_APPLICABLE' },
        { version_id: published, sequence: 2, item_id: eggs, dimension: 'COUNT', quantity_atomic: 3, count_policy: 'EXACT_WHOLE_COUNT' },
        { version_id: draft, sequence: 1, item_id: flour, dimension: 'MASS', quantity_atomic: 650_000, count_policy: 'NOT_APPLICABLE' }],
      groups: [], options: [] } },
    { entity: 'production_profile', id: profile, payload: {
      profile: { id: profile, product_id: concha, finished_item_id: finished, active_version_id: null, revision: 1 }, versions: [], directIngredients: [], formulaRequirements: [] } },
  ]);
  return q;
}

test('recipes data: every version with its lines, published preparations, active items and products for the editors', async () => {
  const q = await seeded();
  const data = await recipes(q, centro);
  const masa = data.formulas[0];
  assert.deepEqual(masa.list.map(v => [v.version, v.state, v.revision]), [[1, 'PUBLISHED', 2], [2, 'DRAFT', 4]]);
  assert.equal(masa.revision, 3);
  assert.equal(masa.activeVersionId, published);
  assert.deepEqual(masa.list[0].ingredients.map(i => [i.item, i.atomic, i.policy]), [['Harina', 600_000, 'NotApplicable'], ['Huevo', 3, 'ExactWholeCount']]);
  assert.equal(masa.active!.version, 1, 'The summary is the active version.');
  assert.deepEqual(data.publishedFormulas, [{ id: published, label: 'Masa dulce · versión 1', dimension: 'MASS' }], 'Only published versions can be a preparation.');
  assert.deepEqual(data.items.map(i => i.code), ['PT-CON', 'HAR', 'HUE', 'MAN'], 'Active items by name.');
  assert.deepEqual(data.profiles.map(p => [p.product, p.finishedItem, p.revision, p.list.length]), [['Concha', 'Concha terminada', 1, 0]]);
});

test('recipe changes are validated before reaching the till', async () => {
  const q = await seeded();
  const admin = await login(q, 'admin', 'Admin#2026centro');
  const ana = await login(q, 'ana', 'Ana#2026centro');
  const fresh = randomUUID();
  const line = (itemId: string, atomic: number, policy = 'NotApplicable') => ({ itemId, atomic, policy });

  await assert.rejects(createCommand(q, ana, centro, 'recipe.formula.create', { id: fresh, code: 'X', name: 'X' }), status(403), 'Only who configures recipes.');
  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.create', { id: fresh, code: 'masa', name: 'Otra' }), /código/);
  await createCommand(q, admin, centro, 'recipe.formula.create', { id: fresh, code: 'RELL', name: 'Relleno' });
  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.create', { id: randomUUID(), code: 'rell', name: 'Otro' }), /Ya se mandó/);

  // Un borrador para la receta recién mandada: va después en la fila.
  const v1 = randomUUID();
  await createCommand(q, admin, centro, 'recipe.formula.draft', { formulaId: fresh, versionId: v1, revision: null, name: 'Base', notes: '',
    yield: { dimension: 'Mass', atomic: 500_000 }, ingredients: [line(butter, 200_000)],
    groups: [[{ id: randomUUID(), ...line(flour, 100_000) }, { id: randomUUID(), ...line(eggs, 2, 'CeilingWholeCount') }]] });
  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.draft', { formulaId: fresh, versionId: randomUUID(), name: 'X', yield: { dimension: 'Mass', atomic: 1 },
    ingredients: [line(eggs, 2)], groups: [] }), /política/, 'Pieces need a piece policy.');
  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.draft', { formulaId: fresh, versionId: randomUUID(), name: 'X', yield: { dimension: 'Mass', atomic: 1 },
    ingredients: [line(flour, 1), line(flour, 2)], groups: [] }), /repetirse/);
  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.draft', { formulaId: fresh, versionId: randomUUID(), name: 'X', yield: { dimension: 'Mass', atomic: 1 },
    ingredients: [], groups: [[{ id: randomUUID(), ...line(flour, 1) }]] }), /dos opciones/);
  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.draft', { formulaId: formula, versionId: draft, revision: 3, name: 'X', yield: { dimension: 'Mass', atomic: 1 },
    ingredients: [line(flour, 1)], groups: [] }), status(409), 'An old revision of the draft.');
  await createCommand(q, admin, centro, 'recipe.formula.draft', { formulaId: formula, versionId: draft, revision: 4, name: 'Más mantequilla', notes: 'Ok', yield: { dimension: 'Mass', atomic: 1_200_000 },
    ingredients: [line(flour, 650_000), line(butter, 150_000)], groups: [] });

  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.publish', { versionId: published, revision: 2 }), /ya está publicada/);
  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.publish', { versionId: draft, revision: 1 }), status(409));
  await createCommand(q, admin, centro, 'recipe.formula.publish', { versionId: draft, revision: 4 });
  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.activate', { formulaId: formula, versionId: draft, revision: 3 }), /Publica/);
  await assert.rejects(createCommand(q, admin, centro, 'recipe.formula.activate', { formulaId: formula, versionId: published, revision: 3 }), /ya está activa/);

  await assert.rejects(createCommand(q, admin, centro, 'recipe.profile.create', { id: randomUUID(), productId: concha, finishedItemId: finished }), /ya tiene un perfil/);
  await assert.rejects(createCommand(q, admin, centro, 'recipe.profile.create', { id: randomUUID(), productId: dona, finishedItemId: flour }), /Producto terminado/);
  const doughnut = randomUUID();
  await createCommand(q, admin, centro, 'recipe.profile.create', { id: doughnut, productId: dona, finishedItemId: finished });
  await assert.rejects(createCommand(q, admin, centro, 'recipe.profile.draft', { profileId: profile, versionId: randomUUID(), name: 'V1',
    formulas: [{ formulaVersionId: draft, atomic: 60_000 }], direct: [] }), /publicada/, 'Drafts are not preparations yet.');
  await createCommand(q, admin, centro, 'recipe.profile.draft', { profileId: doughnut, versionId: randomUUID(), revision: null, name: 'Dona v1',
    formulas: [{ formulaVersionId: published, atomic: 60_000 }], direct: [line(eggs, 1, 'ExactWholeCount')] });

  const pulled = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  assert.deepEqual(pulled.map(c => c.kind), ['recipe.formula.create', 'recipe.formula.draft', 'recipe.formula.draft', 'recipe.formula.publish', 'recipe.profile.create', 'recipe.profile.draft']);
  assert.deepEqual((pulled[1].payload as any).groups[0].map((o: any) => o.itemId), [flour, eggs], 'Options keep the preference order.');
  assert.deepEqual((pulled[3].payload as any), { versionId: draft, revision: 4 });
  // Si la caja rechaza el alta, ya se puede volver a usar el código.
  await recordResults(q, { id: caja1, branchId: centro }, { results: [{ id: pulled[0].id, status: 'rejected', message: 'Ya existe una receta con ese código.' }] });
  await createCommand(q, admin, centro, 'recipe.formula.create', { id: randomUUID(), code: 'RELL', name: 'Relleno' });
});

test('a version marked «Por medir» shows the mark and cannot be published from the web', async () => {
  const marked = await seeded(1);
  const data = await recipes(marked, centro);
  assert.deepEqual(data.formulas[0].list.map(v => [v.version, v.pendingMeasure]), [[1, false], [2, true]]);
  const admin = await login(marked, 'admin', 'Admin#2026centro');
  await assert.rejects(createCommand(marked, admin, centro, 'recipe.formula.publish', { versionId: draft, revision: 4 }), /Por medir/);

  // Una caja anterior a la marca no la manda: la versión no aparece marcada y se publica como antes.
  const older = await seeded();
  assert.equal((await recipes(older, centro)).formulas[0].list[1].pendingMeasure, false);
  await createCommand(older, await login(older, 'admin', 'Admin#2026centro'), centro, 'recipe.formula.publish', { versionId: draft, revision: 4 });
});
