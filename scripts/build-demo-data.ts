// Arma scripts/demo/casamiga-demo.json.gz (la demo completa de la web local) desde una exportación de la caja
// (CasaMiga.Maintenance export-sync <copia> <json>). Cambia la contraseña de cada usuario por la contraseña fija de prueba de su rol,
// así el repositorio nunca guarda contraseñas reales ni de revisión. Uso: node --import tsx scripts/build-demo-data.ts <export.json>
import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { createHash, randomUUID } from 'node:crypto';
import { gzipSync } from 'node:zlib';
import { identityHash } from '../lib/auth.js';
import { loadCatalog, photoDates } from './catalogo.js';
import { finishedCode, loadRecetario, namesOf, normalize, POR_MEDIR, quantity, stableId, validate, type Line } from './recetario.js';

const PASSWORDS: Record<string, string> = { Administrator: 'Admin#2026', Manager: 'Encargado#2026', Cashier: 'Cajero#2026' };
const file = process.argv[2];
if (!file) throw new Error('Falta la exportación.');
const data = JSON.parse(readFileSync(file, 'utf8')) as { branch: unknown; terminal: unknown; entries: { entity: string; payload: Record<string, unknown> }[] };
const hashes = new Map<string, string>();
for (const [role, password] of Object.entries(PASSWORDS)) hashes.set(role, await identityHash(password));
let users = 0;
for (const entry of data.entries) {
  if (entry.entity !== 'app_user') continue;
  const p = entry.payload;
  const hash = hashes.get(String(p.role_name));
  if (!hash) throw new Error(`Rol sin contraseña de prueba: ${String(p.role_name)}`);
  // El cajero de la demo se llama «cajero», como en run-local.ps1.
  if (p.username === 'CAJERO1') p.username = 'CAJERO';
  p.password_hash = hash; p.must_change_password = 0; users++;
}
// Panes del catálogo autorizado (docs/catalog) que la exportación todavía no tiene: se agregan como si la caja los hubiera dado de alta,
// con su categoría (si es nueva) y su foto provisional (assets/catalog-provisional-ia/CM-NNN.jpg, usada tal cual como miniatura).
type Entry = { seq: number; entity: string; entityId: string; change: string; enqueuedAt: string; payload: Record<string, unknown> };
const entries = data.entries as unknown as Entry[];
const catalog = loadCatalog().map(r => [String(r.id), r.name, String(r.pesos), r.category] as const);
const catalogRows = loadCatalog(), dates = photoDates();
const products = entries.filter(e => e.entity === 'product').map(e => (e.payload.product as Record<string, unknown>));
const categories = entries.filter(e => e.entity === 'category').map(e => e.payload);
const categoryId = new Map(categories.map(c => [String(c.name), String(c.id)]));
const admin = entries.find(e => e.entity === 'app_user' && e.payload.role_name === 'Administrator')?.payload.id;
const terminal = (data.terminal as { id: string }).id;
let seq = Math.max(...entries.map(e => e.seq)), added = 0, newCategories = 0, photos = 0;
const now = new Date().toISOString();
// Panes renombrados (docs/catalog/renombres.json): se renombran en la demo en lugar de darlos de alta otra vez.
type R = Record<string, unknown>;
const latestOf = (entity: string) => { const map = new Map<string, Entry>(); for (const e of entries) if (e.entity === entity) map.set(e.entityId, e); return map; };
let renamed = 0, refreshed = 0;
for (const row of catalogRows) {
  const current = [...latestOf('product').values()];
  if (current.some(e => (e.payload.product as R).name === row.name)) continue;
  const old = current.find(e => (row.before ?? []).includes(String((e.payload.product as R).name)));
  if (!old) continue;
  entries.push({ seq: ++seq, entity: 'product', entityId: old.entityId, change: 'update', enqueuedAt: now, payload: { ...old.payload, product: { ...(old.payload.product as R), name: row.name, modified_utc: now } } });
  renamed++;
}
const hasProduct = (name: string) => [...latestOf('product').values()].some(e => (e.payload.product as R).name === name);
for (const [id, name, price, category] of catalog) {
  if (hasProduct(name)) continue;
  if (!categoryId.has(category)) {
    const categoryKey = randomUUID();
    entries.push({ seq: ++seq, entity: 'category', entityId: categoryKey, change: 'insert', enqueuedAt: now, payload: { id: categoryKey, name: category, active: 1,
      description: 'Catálogo autorizado v2 · docs/catalog', sort_order: categories.length + newCategories, created_utc: now, modified_utc: now } });
    categoryId.set(category, categoryKey); newCategories++;
  }
  const productId = randomUUID(), sku = `CM-${id.padStart(3, '0')}`;
  entries.push({ seq: ++seq, entity: 'product', entityId: productId, change: 'insert', enqueuedAt: now, payload: {
    product: { id: productId, sku, name, category_id: categoryId.get(category), description: '', price_cents: Number(price) * 100,
      track_inventory: 0, active: 1, created_utc: now, modified_utc: now }, inventoryLinks: [] } });
  added++;
  const photoFile = new URL(`../../assets/catalog-provisional-ia/${sku}.jpg`, import.meta.url);
  if (!existsSync(photoFile)) continue;
  const jpeg = readFileSync(photoFile), photoId = randomUUID();
  entries.push({ seq: ++seq, entity: 'product_photo', entityId: productId, change: 'insert', enqueuedAt: now, payload: {
    photo: { product_id: productId, current_photo_id: photoId, revision: 1, last_operation_id: photoId, modified_utc: now, modified_by: admin, terminal_id: terminal },
    asset: { id: photoId, provenance: 'PROVISIONAL', thumbnail_sha256: createHash('sha256').update(jpeg).digest('hex').toUpperCase(), thumbnail_edge: 768, created_utc: now },
    thumbnail: jpeg.toString('base64') } });
  photos++;
}
// Fotos provisionales más nuevas que las de la demo (por ejemplo, la de un pan renombrado); una foto real nunca se cambia.
const productByName = new Map([...latestOf('product').values()].map(e => [String((e.payload.product as R).name), e.entityId]));
const currentPhotos = latestOf('product_photo');
for (const row of catalogRows) {
  const sku = `CM-${String(row.id).padStart(3, '0')}`, productId = productByName.get(row.name), generated = dates.get(sku);
  const current = productId ? currentPhotos.get(productId) : undefined;
  if (!productId || !current || !generated) continue;
  const asset = current.payload.asset as R, photo = current.payload.photo as R;
  const at = String(photo.modified_utc ?? asset.created_utc ?? '');
  if (asset.provenance !== 'PROVISIONAL' || !at || Date.parse(at) >= Date.parse(generated)) continue;
  const jpeg = readFileSync(new URL(`../../assets/catalog-provisional-ia/${sku}.jpg`, import.meta.url)), photoId = randomUUID();
  entries.push({ seq: ++seq, entity: 'product_photo', entityId: productId, change: 'update', enqueuedAt: now, payload: {
    photo: { ...photo, current_photo_id: photoId, revision: Number(photo.revision ?? 1) + 1, last_operation_id: photoId, modified_utc: now },
    asset: { id: photoId, provenance: 'PROVISIONAL', thumbnail_sha256: createHash('sha256').update(jpeg).digest('hex').toUpperCase(), thumbnail_edge: 768, created_utc: now },
    thumbnail: jpeg.toString('base64') } });
  refreshed++;
}
console.log(`${added} panes nuevos del catálogo autorizado agregados a la demo (${photos} con foto, ${newCategories} categorías nuevas); ${renamed} renombrados y ${refreshed} con foto nueva.`);
// Recetario en borrador (docs/catalog/recetario-borrador.json): insumos, preparaciones y lo que lleva cada pan, sin publicar, como si
// la caja lo hubiera capturado. Reutiliza los insumos que ya existen con el mismo código o nombre y no repite lo ya cargado.
const recetario = loadRecetario();
const problems = validate(recetario, catalog.map(row => row[1]));
if (problems.length) throw new Error(`El recetario tiene problemas:\n${problems.join('\n')}`);
const latest = (entity: string) => { const map = new Map<string, Entry>(); for (const e of entries) if (e.entity === entity) map.set(e.entityId, e); return [...map.values()]; };
const push = (entity: string, entityId: string, payload: Record<string, unknown>, change = 'insert') => entries.push({ seq: ++seq, entity, entityId, change, enqueuedAt: now, payload });
const items = latest('inventory_item').map(e => e.payload.item as Record<string, unknown>);
let newItems = 0, newFormulas = 0, newProfiles = 0;
const addItem = (code: string, name: string, kind: string, dimension: string, names: string[]) => {
  const found = items.find(i => normalize(String(i.code)) === normalize(code) || names.includes(normalize(String(i.name))));
  if (found) return String(found.id);
  const id = stableId(`insumo:${code}`);
  push('inventory_item', id, { item: { id, code, name, kind, dimension, active: 1, created_utc: now, modified_utc: now }, units: [], balances: [] });
  items.push({ id, code, name }); newItems++;
  return id;
};
const DIMENSION = { g: 'MASS', ml: 'VOLUME', pz: 'COUNT' } as const;
const itemId = new Map(recetario.insumos.map(i => [i.codigo, addItem(i.codigo, i.nombre, 'RAW_MATERIAL', DIMENSION[i.mide], namesOf(i))]));
const line = (l: Line) => {
  const q = quantity(l.cantidad);
  return { item_id: itemId.get(l.insumo)!, quantity_atomic: q.atomic, dimension: q.dimension, ...(q.dimension === 'COUNT' ? { count_policy: 'CEILING_WHOLE_COUNT' } : {}) };
};
const formulaVersion = new Map<string, string>();
const formulas = latest('formula');
for (const p of recetario.preparaciones) {
  const existing = formulas.find(f => normalize(String((f.payload.formula as Record<string, unknown>).code)) === normalize(p.codigo));
  const versionId = stableId(`preparacion:${p.codigo}:${recetario.fecha}`);
  const id = stableId(`preparacion:${p.codigo}`), y = quantity(p.rinde), group = (g: number) => stableId(`preparacion:${p.codigo}:alternativa:${g}`);
  // Una preparación con una cantidad «por medir» de otra queda sin versión, como en la caja: no se supone el dato.
  const inner = p.preparaciones ?? [];
  if (inner.some(r => r.cantidad === POR_MEDIR || !formulaVersion.has(r.codigo))) {
    if (!existing) { push('formula', id, { formula: { id, code: p.codigo, name: p.nombre, active_version_id: null, revision: 1 }, versions: [], ingredients: [], groups: [], options: [], preparations: [] }); newFormulas++; }
    continue;
  }
  formulaVersion.set(p.codigo, versionId);
  if (existing) continue;
  push('formula', id, {
    formula: { id, code: p.codigo, name: p.nombre, active_version_id: null, revision: 1 },
    versions: [{ id: versionId, formula_id: id, version: 1, state: 'DRAFT', yield_dimension: y.dimension, yield_atomic: y.atomic, name: recetario.nombreBorrador, notes: p.notas ?? null, revision: 1 }],
    preparations: inner.map((r, i) => { const q = quantity(r.cantidad); return { version_id: versionId, sequence: i + 1, preparation_version_id: formulaVersion.get(r.codigo), dimension: q.dimension, quantity_atomic: q.atomic }; }),
    ingredients: p.ingredientes.map((l, i) => ({ version_id: versionId, sequence: i + 1, ...line(l) })),
    groups: (p.alternativas ?? []).map((_, g) => ({ version_id: versionId, id: group(g), sequence: g + 1, name: `Alternativa ${g + 1}` })),
    options: (p.alternativas ?? []).flatMap((options, g) => options.map((l, o) => ({ version_id: versionId, group_id: group(g), id: stableId(`preparacion:${p.codigo}:alternativa:${g}:${l.insumo}`),
      sequence: o + 1, preference_rank: o + 1, ...line(l) }))),
  });
  newFormulas++;
}
const productId = new Map(latest('product').map(e => e.payload.product as Record<string, unknown>).map(p => [normalize(String(p.name)), String(p.id)]));
const profiles = latest('production_profile');
for (const pan of recetario.panes) {
  const product = productId.get(normalize(pan.pan))!;
  const current = profiles.find(e => String((e.payload.profile as Record<string, unknown>).product_id) === product);
  const versions = (current?.payload.versions as Record<string, unknown>[] | undefined) ?? [];
  if (versions.some(v => v.name === recetario.nombreBorrador)) continue;
  if (pan.preparaciones.some(r => !formulaVersion.has(r.codigo))) continue; // su preparación espera un dato por medir
  const id = current ? String((current.payload.profile as Record<string, unknown>).id) : stableId(`pan:${pan.catalogo}`);
  const versionId = stableId(`pan:${pan.catalogo}:${recetario.fecha}`);
  const finished = current ? String((current.payload.profile as Record<string, unknown>).finished_item_id) : addItem(finishedCode(pan.catalogo), pan.pan, 'FINISHED_GOOD', 'COUNT', [normalize(pan.pan)]);
  const payload = current?.payload ?? { profile: { id, product_id: product, active_version_id: null, finished_item_id: finished, revision: 0 }, versions: [], directIngredients: [], formulaRequirements: [] };
  const profile = payload.profile as Record<string, unknown>;
  push('production_profile', id, {
    profile: { ...profile, revision: Number(profile.revision ?? 0) + 1 },
    versions: [...versions, { id: versionId, profile_id: id, version: versions.length + 1, state: 'DRAFT', name: recetario.nombreBorrador, revision: 1 }],
    directIngredients: [...(payload.directIngredients as unknown[]), ...(pan.directos ?? []).map((l, i) => ({ profile_version_id: versionId, sequence: i + 1, ...line(l) }))],
    formulaRequirements: [...(payload.formulaRequirements as unknown[]), ...pan.preparaciones.map((r, i) => {
      const q = quantity(r.cantidad);
      return { profile_version_id: versionId, formula_version_id: formulaVersion.get(r.codigo), sequence: i + 1, quantity_atomic: q.atomic, dimension: q.dimension };
    })],
  }, current ? 'update' : 'insert');
  newProfiles++;
}
console.log(`Recetario en borrador: ${newItems} artículos de inventario, ${newFormulas} preparaciones y ${newProfiles} panes.`);
mkdirSync(new URL('./demo/', import.meta.url), { recursive: true });
const output = new URL('./demo/casamiga-demo.json.gz', import.meta.url);
writeFileSync(output, gzipSync(JSON.stringify(data), { level: 9 }));
console.log(`${data.entries.length} registros, ${users} usuarios con contraseña de prueba → ${output.pathname}`);
