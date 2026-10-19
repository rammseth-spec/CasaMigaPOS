// Qué falta mandarle a la caja por la web: catálogo autorizado con sus fotos provisionales, insumos, recetas en borrador y lo que lleva
// cada pan. Compara contra lo que la caja ya subió y contra lo que ya va en la fila, así que volver a correrlo nunca duplica nada.
// Etapas: 1 catálogo y fotos · 2 insumos y productos terminados · 3 preparaciones (borrador) · 4 panes (borrador, sólo con
// preparaciones publicadas: es lo que pide la caja).
import { finishedCode, namesOf, normalize, quantity, stableId, type Line, type Recetario } from './recetario.js';

export type CatalogRow = { id: number; name: string; pesos: number; category: string };
export type Queued = { kind: string; payload: Record<string, unknown> };
export type State = {
  categories: { id: string; name: string }[];
  products: { id: string; name: string; photo: unknown }[];
  items: { id: string; code: string; name: string; kind: string; dimension: string }[];
  formulas: { id: string; code: string; activeVersionId: string | null; list: { id: string; name: string; state: string }[] }[];
  profiles: { id: string; productId: string; finishedItemId: string; list: { id: string; name: string; state: string }[] }[];
  /** Cambios mandados que la caja todavía no aplica (ni rechazó). */
  queued: Queued[];
};
export type Step = { stage: number; kind: string; label: string; payload: Record<string, unknown> };
export type Wait = { stage: number; label: string; reason: string };

const WEB_DIMENSION = { MASS: 'Mass', VOLUME: 'Volume', COUNT: 'Count' } as const;
const MIDE = { g: 'MASS', ml: 'VOLUME', pz: 'COUNT' } as const;

export function plan(state: State, catalog: CatalogRow[], recetario: Recetario, photo: (sku: string) => string | null): { steps: Step[]; waits: Wait[] } {
  const steps: Step[] = [], waits: Wait[] = [];
  const queued = (kind: string, test: (p: Record<string, unknown>) => boolean) => state.queued.find(c => c.kind === kind && test(c.payload))?.payload;
  const same = (a: unknown, b: string) => normalize(String(a ?? '')) === normalize(b);

  // 1. Catálogo autorizado: categorías, panes y fotos provisionales (la foto puede ir en la fila detrás del alta del pan).
  const categoryId = new Map<string, string>();
  [...new Set(catalog.map(r => r.category))].forEach((name, i) => {
    const id = state.categories.find(c => same(c.name, name))?.id ?? String(queued('category.save', p => same(p.name, name))?.id ?? '');
    if (id) { categoryId.set(name, id); return; }
    const fresh = stableId(`categoria:${name}`);
    categoryId.set(name, fresh);
    steps.push({ stage: 1, kind: 'category.save', label: `Categoría ${name}`, payload: { id: fresh, name, description: 'Catálogo autorizado · docs/catalog', sortOrder: i } });
  });
  const productId = new Map<string, string>();
  for (const row of catalog) {
    const sku = `CM-${String(row.id).padStart(3, '0')}`;
    const existing = state.products.find(p => same(p.name, row.name));
    let id = existing?.id ?? String(queued('product.save', p => same(p.name, row.name))?.id ?? '');
    if (!id) {
      id = stableId(`producto:${row.id}`);
      steps.push({ stage: 1, kind: 'product.save', label: `Pan ${row.id} ${row.name}`,
        payload: { id, categoryId: categoryId.get(row.category), sku, name: row.name, description: '', priceCents: row.pesos * 100, trackInventory: false } });
    }
    productId.set(normalize(row.name), id);
    if (existing?.photo || queued('product.photo', p => p.productId === id)) continue;
    const image = photo(sku);
    if (image) steps.push({ stage: 1, kind: 'product.photo', label: `Foto de ${row.name}`, payload: { productId: id, image, provenance: 'PROVISIONAL' } });
    else waits.push({ stage: 1, label: `Foto de ${row.name}`, reason: `no se encontró ${sku}.jpg` });
  }

  // 2. Insumos y un producto terminado (por pieza) por cada pan con receta.
  const itemOf = (code: string, names: string[]) => state.items.find(i => same(i.code, code) || names.includes(normalize(i.name)));
  const queuedItem = (code: string) => queued('inventory.item.save', p => same(p.code, code));
  const item = (code: string, name: string, kind: string, dimension: keyof typeof WEB_DIMENSION, names: string[]) => {
    const found = itemOf(code, names);
    if (found) return { id: found.id, ready: true };
    const pending = queuedItem(code);
    if (pending) return { id: String(pending.id), ready: false };
    const id = stableId(`insumo:${code}`);
    steps.push({ stage: 2, kind: 'inventory.item.save', label: `${kind === 'FinishedGood' ? 'Producto terminado' : 'Insumo'} ${name}`,
      payload: { id, code, name, kind, dimension: WEB_DIMENSION[dimension] } });
    return { id, ready: false };
  };
  const insumo = new Map(recetario.insumos.map(i => [i.codigo, item(i.codigo, i.nombre, 'RawMaterial', MIDE[i.mide], namesOf(i))]));
  // Un pan que ya tiene perfil conserva su producto terminado; los demás reciben uno propio.
  const finished = new Map(recetario.panes.map(p => {
    const current = state.profiles.find(x => x.productId === state.products.find(pr => same(pr.name, p.pan))?.id);
    return [p.catalogo, current ? { id: current.finishedItemId, ready: true } : item(finishedCode(p.catalogo), p.pan, 'FinishedGood', 'COUNT', [normalize(p.pan)])];
  }));
  const line = (l: Line) => { const q = quantity(l.cantidad); return { itemId: insumo.get(l.insumo)!.id, atomic: q.atomic, policy: q.dimension === 'COUNT' ? 'CeilingWholeCount' : 'NotApplicable' }; };
  const ready = (lines: Line[]) => lines.every(l => insumo.get(l.insumo)!.ready);

  // 3. Preparaciones en borrador: la caja sólo acepta ingredientes que ya existen en ella.
  const published = new Map<string, string>();
  for (const p of recetario.preparaciones) {
    const formula = state.formulas.find(f => same(f.code, p.codigo));
    const formulaId = formula?.id ?? String(queued('recipe.formula.create', x => same(x.code, p.codigo))?.id ?? '');
    const versions = formula?.list ?? [];
    const pub = versions.find(v => v.id === formula?.activeVersionId && v.state === 'PUBLISHED') ?? [...versions].reverse().find(v => v.state === 'PUBLISHED');
    if (pub) published.set(p.codigo, pub.id);
    const id = formulaId || stableId(`preparacion:${p.codigo}`);
    if (!formulaId) steps.push({ stage: 3, kind: 'recipe.formula.create', label: `Preparación ${p.nombre}`, payload: { id, code: p.codigo, name: p.nombre } });
    const versionId = stableId(`preparacion:${p.codigo}:${recetario.fecha}`);
    if (versions.some(v => v.name === recetario.nombreBorrador || v.id === versionId) || queued('recipe.formula.draft', x => x.versionId === versionId)) continue;
    const lines = [...p.ingredientes, ...(p.alternativas ?? []).flat()];
    if (!ready(lines)) { waits.push({ stage: 3, label: `Borrador de ${p.nombre}`, reason: 'esperando que la caja aplique sus insumos' }); continue; }
    const y = quantity(p.rinde);
    steps.push({ stage: 3, kind: 'recipe.formula.draft', label: `Borrador de ${p.nombre}`, payload: {
      formulaId: id, versionId, revision: null, name: recetario.nombreBorrador, notes: (p.notas ?? '').slice(0, 500),
      yield: { dimension: WEB_DIMENSION[y.dimension], atomic: y.atomic }, ingredients: p.ingredientes.map(line),
      groups: (p.alternativas ?? []).map((g, n) => g.map(l => ({ id: stableId(`preparacion:${p.codigo}:alternativa:${n}:${l.insumo}`), ...line(l) }))) } });
  }

  // 4. Lo que lleva cada pan, en borrador. La caja pide que sus preparaciones ya estén publicadas.
  for (const pan of recetario.panes) {
    const product = state.products.find(p => same(p.name, pan.pan));
    const profile = state.profiles.find(p => p.productId === product?.id);
    const versionId = stableId(`pan:${pan.catalogo}:${recetario.fecha}`);
    if (profile?.list.some(v => v.name === recetario.nombreBorrador || v.id === versionId) || queued('recipe.profile.draft', x => x.versionId === versionId)) continue;
    const label = `Receta de ${pan.pan}`;
    if (!product) { waits.push({ stage: 4, label, reason: 'esperando que la caja aplique el pan' }); continue; }
    if (!finished.get(pan.catalogo)!.ready) { waits.push({ stage: 4, label, reason: 'esperando que la caja aplique su producto terminado' }); continue; }
    const missing = pan.preparaciones.filter(r => !published.has(r.codigo)).map(r => r.codigo);
    if (missing.length) { waits.push({ stage: 4, label, reason: `falta publicar ${missing.join(', ')}` }); continue; }
    if (!ready(pan.directos ?? [])) { waits.push({ stage: 4, label, reason: 'esperando que la caja aplique sus insumos' }); continue; }
    const profileId = (profile?.id ?? String(queued('recipe.profile.create', x => x.productId === product.id)?.id ?? '')) || stableId(`pan:${pan.catalogo}`);
    if (!profile && !queued('recipe.profile.create', x => x.productId === product.id))
      steps.push({ stage: 4, kind: 'recipe.profile.create', label: `Perfil de ${pan.pan}`, payload: { id: profileId, productId: product.id, finishedItemId: finished.get(pan.catalogo)!.id } });
    steps.push({ stage: 4, kind: 'recipe.profile.draft', label, payload: {
      profileId, versionId, revision: null, name: recetario.nombreBorrador,
      formulas: pan.preparaciones.map(r => ({ formulaVersionId: published.get(r.codigo), atomic: quantity(r.cantidad).atomic })),
      direct: (pan.directos ?? []).map(line) } });
  }
  return { steps, waits };
}
