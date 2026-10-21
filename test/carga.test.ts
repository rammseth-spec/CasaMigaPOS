// Carga de una caja por la web (scripts/cargar-caja.ts): catálogo autorizado con fotos, insumos, recetario en borrador y lo que lleva
// cada pan. Cada cambio que arma el plan debe aceptarlo la validación de la web, y repetir el plan no debe duplicar nada.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { createCommand, listCommands, pendingFor, recordResults } from '../lib/commands.js';
import { issueSession, login } from '../lib/auth.js';
import { handle } from '../lib/router.js';
import { catalog as webCatalog, recipes } from '../lib/web.js';
import { plan, type CatalogRow, type State, type Step } from '../scripts/carga-plan.js';
import { loadRecetario } from '../scripts/recetario.js';
import { caja1, centro, database, permissions, send, user } from './helpers.js';

// Usa el catálogo, el recetario y las fotos del repositorio completo; en la copia que sólo trae la web (casamiga-web) no existen.
const csv = new URL('../../docs/catalog/catalogo-autorizado-v2.csv', import.meta.url);
const skip = existsSync(csv) ? false : 'requiere el repositorio completo (docs/ y assets/)';
const catalogRows: CatalogRow[] = skip ? [] : readFileSync(csv, 'utf8').trim().split(/\r?\n/).slice(1)
  .map(line => line.split(',')).map(([id, name, pesos, category]) => ({ id: Number(id), name, pesos: Number(pesos), category }));
const recetario = skip ? (null as never) : loadRecetario();
const photo = (sku: string) => { const file = new URL(`../../assets/catalog-provisional-ia/${sku}.jpg`, import.meta.url); return existsSync(file) ? readFileSync(file).toString('base64') : null; };
const KIND: Record<string, string> = { RawMaterial: 'RAW_MATERIAL', FinishedGood: 'FINISHED_GOOD' };
const DIMENSION: Record<string, string> = { Mass: 'MASS', Volume: 'VOLUME', Count: 'COUNT' };

async function state(q: Awaited<ReturnType<typeof database>>['q']): Promise<State> {
  const c = await webCatalog(q, centro), r = await recipes(q, centro);
  return { categories: c.categories, products: c.products, items: r.items, formulas: r.formulas, profiles: r.profiles,
    queued: await listCommands(q, centro, true) };
}

/** La caja aplica lo pendiente (como lo subiría al sincronizar) y reporta cada cambio como aplicado. */
async function applyAsTill(q: Awaited<ReturnType<typeof database>>['q'], publish = false) {
  // La caja baja los cambios en tandas (hasta 50) y reporta cada tanda antes de pedir la siguiente.
  for (let pulled = await pendingFor(q, { id: caja1, branchId: centro }, 0); pulled.length; pulled = await pendingFor(q, { id: caja1, branchId: centro }, 0)) {
    await applyBatch(q, pulled);
    await recordResults(q, { id: caja1, branchId: centro }, { results: pulled.map(c => ({ id: c.id, status: 'applied' })) });
  }
  if (publish) {
    // Alguien revisa y publica las preparaciones en Recetas (la caja sube la versión publicada y activa).
    for (const row of await q(`SELECT entity_id, payload FROM entity_state WHERE entity = 'formula'`, [])) {
      const f = row.payload as Record<string, any>;
      for (const v of f.versions) v.state = 'PUBLISHED';
      f.formula.active_version_id = f.versions.at(-1)?.id ?? null;
      await send(q, caja1, centro, [{ entity: 'formula', id: String(row.entity_id), change: 'update', payload: f }]);
    }
  }
}

async function applyBatch(q: Awaited<ReturnType<typeof database>>['q'], pulled: Awaited<ReturnType<typeof pendingFor>>) {
  const formulas = new Map<string, Record<string, unknown>>();
  const formula = async (id: string) => {
    if (!formulas.has(id)) formulas.set(id, (await q(`SELECT payload FROM entity_state WHERE entity = 'formula' AND entity_id = $1`, [id]))[0].payload as Record<string, unknown>);
    return formulas.get(id)!;
  };
  for (const c of pulled) {
    const p = c.payload as Record<string, any>;
    if (c.kind === 'product.photo')
      await send(q, caja1, centro, [{ entity: 'product_photo', id: p.productId, payload: { photo: { product_id: p.productId }, asset: { id: c.id, provenance: p.provenance, thumbnail_sha256: c.id }, thumbnail: p.image } }]);
    if (c.kind === 'inventory.item.save')
      await send(q, caja1, centro, [{ entity: 'inventory_item', id: p.id, payload: { item: { id: p.id, code: p.code, name: p.name, kind: KIND[p.kind], dimension: DIMENSION[p.dimension], active: 1 }, units: [], balances: [] } }]);
    if (c.kind === 'recipe.formula.create') formulas.set(p.id, { formula: { id: p.id, code: p.code, name: p.name, active_version_id: null, revision: 1 }, versions: [], ingredients: [], groups: [], options: [] });
    if (c.kind === 'recipe.formula.draft') {
      const f = await formula(p.formulaId);
      (f.versions as unknown[]).push({ id: p.versionId, formula_id: p.formulaId, version: 1, state: 'DRAFT', yield_dimension: DIMENSION[p.yield.dimension], yield_atomic: p.yield.atomic, name: p.name, notes: p.notes, revision: 1 });
    }
    if (c.kind === 'recipe.profile.create')
      await send(q, caja1, centro, [{ entity: 'production_profile', id: p.id, payload: { profile: { id: p.id, product_id: p.productId, finished_item_id: p.finishedItemId, active_version_id: null, revision: 1 }, versions: [], directIngredients: [], formulaRequirements: [] } }]);
    if (c.kind === 'recipe.profile.draft') {
      const [row] = await q(`SELECT payload FROM entity_state WHERE entity = 'production_profile' AND entity_id = $1`, [p.profileId]);
      const profile = row.payload as Record<string, any>;
      profile.versions.push({ id: p.versionId, profile_id: p.profileId, version: 1, state: 'DRAFT', name: p.name, revision: 1 });
      await send(q, caja1, centro, [{ entity: 'production_profile', id: p.profileId, change: 'update', payload: profile }]);
    }
  }
  for (const [id, f] of formulas) await send(q, caja1, centro, [{ entity: 'formula', id, change: 'update', payload: f }]);
}

test('a till starts empty and gets the catalog with photos, the supplies and the recipe book as drafts through web commands', { skip }, async () => {
  const { q } = await database();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  const admin = await login(q, 'admin', 'Admin#2026centro');
  const run = async (steps: Step[]) => { for (const s of steps) await createCommand(q, admin, centro, s.kind, s.payload); };
  const count = (steps: Step[], kind: string) => steps.filter(s => s.kind === kind).length;

  // 1ª vuelta: todo el catálogo con sus fotos, los insumos y las preparaciones; sus borradores esperan a que la caja tenga los insumos.
  let first = plan(await state(q), catalogRows, recetario, photo);
  assert.equal(count(first.steps, 'category.save'), 6);
  assert.equal(count(first.steps, 'product.save'), catalogRows.length);
  assert.equal(count(first.steps, 'product.photo'), catalogRows.length);
  assert.equal(count(first.steps, 'inventory.item.save'), recetario.insumos.length + recetario.panes.length);
  assert.equal(count(first.steps, 'recipe.formula.create'), recetario.preparaciones.length);
  assert.equal(count(first.steps, 'recipe.formula.draft'), 0);
  assert.ok(first.waits.every(w => w.stage >= 3), 'Only drafts and per-bread recipes wait.');
  await run(first.steps);
  assert.equal(plan(await state(q), catalogRows, recetario, photo).steps.length, 0, 'What is already queued is never sent again.');

  // 2ª vuelta: con los insumos en la caja, los borradores de las preparaciones simples; las que usan otras esperan a que la caja
  // tenga esas, y la masa enriquecida espera el dato «por medir» de cuánta masa dulce lleva. Las recetas de cada pan esperan su publicación.
  const nested = recetario.preparaciones.filter(p => p.preparaciones?.length);
  const unmeasured = new Set(nested.filter(p => p.preparaciones!.some(r => r.cantidad === 'por medir')).map(p => p.codigo));
  await applyAsTill(q);
  const second = plan(await state(q), catalogRows, recetario, photo);
  assert.equal(count(second.steps, 'recipe.formula.draft'), recetario.preparaciones.length - nested.length);
  assert.equal(second.steps.length, recetario.preparaciones.length - nested.length);
  assert.ok(second.waits.some(w => w.stage === 3 && w.reason.startsWith('esperando que la caja aplique MASA-DULCE')), 'The roll batch waits for its doughs.');
  assert.ok(second.waits.some(w => w.stage === 3 && w.reason === 'falta medir cuánto lleva de MASA-DULCE'), 'The enriched dough is never sent with a guessed amount.');
  assert.ok(second.waits.filter(w => w.stage === 4).every(w => w.reason.startsWith('falta publicar')));
  await run(second.steps);

  // 3ª vuelta: con la masa dulce y el relleno en la caja, la tanda de roles (usa las dos); sin publicar nada, no se manda ningún pan.
  await applyAsTill(q);
  const batches = plan(await state(q), catalogRows, recetario, photo);
  assert.deepEqual(batches.steps.map(s => [s.kind, (s.payload as { preparations?: unknown[] }).preparations?.length]), [['recipe.formula.draft', 2]]);
  await run(batches.steps);
  await applyAsTill(q);
  const waiting = plan(await state(q), catalogRows, recetario, photo);
  assert.equal(waiting.steps.length, 0);
  assert.equal(waiting.waits.length, recetario.panes.length + unmeasured.size);

  // 4ª vuelta: con las preparaciones publicadas, el perfil y el borrador de cada pan, menos los que usan la masa enriquecida.
  const blocked = recetario.panes.filter(p => p.preparaciones.some(r => unmeasured.has(r.codigo))).length;
  assert.equal(blocked, 3, 'Cuernito, danés and bigote wait for the enriched dough.');
  await applyAsTill(q, true);
  const third = plan(await state(q), catalogRows, recetario, photo);
  assert.equal(count(third.steps, 'recipe.profile.create'), recetario.panes.length - blocked);
  assert.equal(count(third.steps, 'recipe.profile.draft'), recetario.panes.length - blocked);
  await run(third.steps);
  await applyAsTill(q);
  const done = plan(await state(q), catalogRows, recetario, photo);
  assert.deepEqual([done.steps.length, done.waits.length], [0, unmeasured.size + blocked], 'Only what waits for a measured amount is left; a new run sends nothing.');
});

test('a product photo of the catalog fits through the web command endpoint', { skip }, async () => {
  const { q } = await database();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  const admin = await login(q, 'admin', 'Admin#2026centro');
  const category = 'c1000000-0000-4000-8000-000000000001', product = 'f1000000-0000-4000-8000-000000000001';
  await createCommand(q, admin, centro, 'category.save', { id: category, name: 'Galleta' });
  await createCommand(q, admin, centro, 'product.save', { id: product, categoryId: category, name: 'Galleta multigrano', priceCents: 1500, trackInventory: false });
  // La foto más pesada del catálogo (≈180 KB, ≈245 KB en base64): antes el límite de 50 KB la rechazaba.
  const image = photo('CM-042')!;
  assert.ok(image.length > 200_000);
  const env = { SESSION_SECRET: 'z'.repeat(40) };
  const body = JSON.stringify({ branchId: centro, kind: 'product.photo', payload: { productId: product, image, provenance: 'PROVISIONAL' } });
  const response = await handle(q, new Request('https://x/api/web/command', { method: 'POST', body,
    headers: { 'content-type': 'application/json', origin: 'https://x', cookie: `cm_web=${issueSession(admin, env.SESSION_SECRET)}` } }), 'command', env);
  assert.equal(response.status, 200, await response.clone().text());
});
