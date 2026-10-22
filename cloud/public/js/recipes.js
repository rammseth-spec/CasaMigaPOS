// Recetas como la caja de escritorio (RecipeView de QAv1): pestañas Recetas / Perfiles de producto, versiones con borrador, publicar
// y activar, y editores con ingredientes, grupos de alternativas, preparaciones e ingredientes directos.
// Todo se manda a la caja (lib/commands.ts, recipe.*); la caja pone el número de versión y lo aplica con sus reglas.
import { api, at, can, command, h, render, state } from './app.js';

const rec = { tab: 'formulas', screen: 'list', selected: null, formula: null, profile: null, version: null, form: null, search: '', message: '', ok: false, pending: [] };
const say = (message, ok = false) => { rec.message = message; rec.ok = ok; };
const redraw = () => render({ silent: true });
const plain = text => String(text ?? '').normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
const branchName = id => state.branches.find(b => b.id === id)?.name ?? '';
const multi = () => !state.branch && state.branches.length > 1;
const configure = () => can('recipe.configure');
/** Opciones por grupo de alternativas (sin Object.groupBy: tabletas viejas no lo tienen). */
const groups = rows => [...rows.reduce((m, r) => m.set(r.group, [...(m.get(r.group) ?? []), r]), new Map()).values()];

const DIMENSIONS = { MASS: 'Masa', VOLUME: 'Volumen', COUNT: 'Piezas' };
const DIMENSION_NAME = { MASS: 'Mass', VOLUME: 'Volume', COUNT: 'Count' };
const UNITS = { MASS: ['g', 'kg'], VOLUME: ['ml', 'L'], COUNT: ['pieza'] };
const FACTOR = { g: 1_000, kg: 1_000_000, ml: 1_000, L: 1_000_000, pieza: 1 };
const POLICIES = { COUNT: [['ExactWholeCount', 'Sólo piezas completas'], ['CeilingWholeCount', 'Redondear hacia arriba']], MASS: [['NotApplicable', 'Cantidad exacta']], VOLUME: [['NotApplicable', 'Cantidad exacta']] };
const defaultPolicy = dimension => POLICIES[dimension][0][0];
// Como InventoryDisplay: kg/L desde 1 000 000 unidades mínimas; si no, g/ml.
const unitOf = (atomic, dimension) => dimension === 'COUNT' ? 'pieza' : atomic >= 1_000_000 ? (dimension === 'MASS' ? 'kg' : 'L') : (dimension === 'MASS' ? 'g' : 'ml');
const numberOf = (atomic, dimension) => String(Number((atomic / FACTOR[unitOf(atomic, dimension)]).toFixed(6)));
const format = (atomic, dimension) => `${numberOf(atomic, dimension)} ${unitOf(atomic, dimension)}`;
/** ParseOperational de la caja: mayor que cero, punto decimal y exacto en la unidad mínima; null si no es válido. */
function atomic(text, unit) {
  const value = String(text ?? '').trim();
  if (!/^\d{1,9}(\.\d{1,6})?$/.test(value) || !FACTOR[unit]) return null;
  const [whole, fraction = ''] = value.split('.');
  const scaled = BigInt(whole + fraction) * BigInt(FACTOR[unit]);
  const divisor = 10n ** BigInt(fraction.length);
  if (scaled % divisor !== 0n || scaled === 0n) return null;
  return Number(scaled / divisor);
}

const HELP = {
  versions: 'BORRADOR: se puede editar y guardar. PUBLICADA: queda cerrada e inmutable para conservar el historial. ACTIVA: es la versión vigente para los usos correspondientes. Para cambiar versiones publicadas, usa «Crear nueva versión» y publica el nuevo borrador cuando esté revisado; no se modifica silenciosamente el historial.',
  yield: 'La cantidad total que produce esta versión. Escribe la cantidad, elige su dimensión y una unidad compatible: si produce 1 kg, Rendimiento = 1, Masa y kg; si produce 20 piezas, 20, Piezas y pieza.',
  policy: 'Para Masa y Volumen se conserva la cantidad exacta. Para Piezas, «Sólo piezas completas» exige que el cálculo ya dé un número entero; «Redondear hacia arriba» convierte 2.4 piezas en 3.',
  direct: 'Un ingrediente que la fórmula o el perfil utiliza directamente: elige el ingrediente, la cantidad y una unidad compatible. El buscador limita los candidatos; no cambia los ingredientes ya asignados.',
  alternatives: 'Un grupo reúne ingredientes que pueden sustituirse entre sí; Opción 1 es la primera preferencia. «Nuevo grupo de alternativas» crea un conjunto; «Agregar opción al grupo» añade otra opción al grupo de la fila elegida. Cada grupo necesita al menos dos opciones.',
  preparations: 'Una preparación es otra receta publicada que forma parte del producto. Elige la preparación, la cantidad por pieza y una unidad compatible con su rendimiento.',
  formulaPreparations: 'Otra receta que ésta usa ya preparada, como la masa dulce dentro de la masa enriquecida. Elige la receta y cuánto de ella usa esta receta completa. Puede estar en borrador, pero esta receta sólo se publica cuando las que usa ya estén publicadas.',
};

export async function recetas() {
  const [data, sent] = await Promise.all([api('recipes', { branch: state.branch }), can('sales.view') ? api('commands', { branch: state.branch }) : []]);
  rec.data = data;
  const tabs = h('div', { class: 'recipe-head' },
    h('div', { class: 'report-tabs' },
      h('button', { type: 'button', class: 'tab', 'aria-pressed': String(rec.tab === 'formulas'), onclick: () => { Object.assign(rec, { tab: 'formulas', screen: 'list', selected: null }); say(''); redraw(); } }, 'Recetas'),
      h('button', { type: 'button', class: 'tab', 'aria-pressed': String(rec.tab === 'profiles'), onclick: () => { Object.assign(rec, { tab: 'profiles', screen: 'list', selected: null }); say(''); redraw(); } }, 'Perfiles de producto')),
    h('span', { class: 'muted', text: configure() ? 'Puedes crear y editar recetas y perfiles' : 'Consulta · sólo lectura' }));
  const body = rec.screen === 'detail' ? (rec.tab === 'formulas' ? formulaDetail(data) : profileDetail(data))
    : rec.screen === 'owner' ? (rec.tab === 'formulas' ? formulaOwner() : profileOwner(data))
    : rec.screen === 'editor' ? (rec.tab === 'formulas' ? formulaEditor(data) : profileEditor(data))
    : rec.tab === 'formulas' ? formulaList(data) : profileList(data);
  return h('div', { class: 'cash-card recipes' }, tabs, waiting(sent), body,
    rec.message ? h('p', { class: rec.ok ? 'success' : 'error', role: 'status', text: rec.message, style: { marginTop: '14px' } }) : null);
}

/** Lo que se mandó a la caja y todavía no aplica (o rechazó hace poco). */
function waiting(sent) {
  const recent = iso => Date.now() - new Date(iso).getTime() < 2 * 86_400_000;
  const list = sent.filter(c => c.kind.startsWith('recipe.') && (c.status === 'pending' || (c.status === 'rejected' && recent(c.at))));
  if (!list.length) return null;
  const label = { 'recipe.formula.create': 'Nueva receta', 'recipe.formula.draft': 'Borrador de receta', 'recipe.formula.publish': 'Publicar receta', 'recipe.formula.activate': 'Activar receta',
    'recipe.profile.create': 'Nuevo perfil', 'recipe.profile.draft': 'Borrador de perfil', 'recipe.profile.publish': 'Publicar perfil', 'recipe.profile.activate': 'Activar perfil' };
  return h('section', { class: 'inv-sent' }, h('strong', { text: 'Enviado a la caja' }),
    h('p', { class: 'caption', text: 'La caja lo aplica en cuanto se conecta; entonces aparece aquí.' }),
    h('ul', { class: 'rows' }, list.slice(0, 10).map(c => h('li', {},
      h('div', {}, h('strong', { text: `${label[c.kind] ?? c.kind}${c.payload.name ? ` · ${c.payload.name}` : c.payload.code ? ` · ${c.payload.code}` : ''}` }), h('small', { class: 'muted', text: ` ${c.by} · ${at(c.at)}` })),
      h('span', { class: 'pill ' + (c.status === 'pending' ? 'warn' : 'bad'), text: c.status === 'pending' ? 'Pendiente' : 'Rechazado' }),
      h('small', { class: 'muted', text: c.message ?? '' })))));
}

// ---------- Listas ----------
function selectable(rows, columns, label, empty, id, onOpen) {
  if (!rows.length) return h('p', { class: 'catalog-empty', text: empty });
  return h('div', { class: 'table-wrap history-table recipe-table' }, h('table', { class: 'cards', 'aria-label': label },
    h('thead', {}, h('tr', {}, columns.map(c => h('th', { text: c[0] })))),
    h('tbody', {}, rows.map(r => { const tr = h('tr', { class: 'click' + (rec.selected === id(r) ? ' selected' : ''), tabindex: '0',
      onclick: () => { if (rec.selected === id(r)) return onOpen(r); rec.selected = id(r); for (const x of tr.parentNode.children) x.classList.toggle('selected', x === tr); },
      ondblclick: () => onOpen(r), onkeydown: e => { if (e.key === 'Enter') onOpen(r); } },
      columns.map(c => h('td', { 'data-label': c[0], text: c[1](r) })));
      return tr; }))));
}
const versionLabel = (owner, list) => owner.activeVersionId ? `Versión ${list.find(v => v.id === owner.activeVersionId)?.version ?? '?'}` : 'Sin activar';
function formulaList(data) {
  const open = f => { rec.formula = f.id; rec.screen = 'detail'; rec.version = null; say(''); redraw(); };
  return h('div', {},
    selectable(data.formulas, [['Código', f => f.code], ['Nombre', f => f.name + (multi() ? ` (${branchName(f.branchId)})` : '')], ['Versión activa', f => versionLabel(f, f.list)],
      ['Borrador disponible', f => f.list.some(v => v.state === 'DRAFT') ? 'Sí' : 'No']], 'Recetas', 'No hay recetas registradas.', f => f.id, open),
    h('div', { class: 'catalog-actions' },
      configure() ? h('button', { type: 'button', class: 'primary', onclick: () => { rec.form = { id: crypto.randomUUID(), code: '', name: '', branchId: state.branch || state.me.branchId || state.branches[0]?.id }; rec.screen = 'owner'; say(''); redraw(); } }, 'Nueva receta') : null,
      h('button', { type: 'button', class: 'secondary', onclick: () => { const f = data.formulas.find(x => x.id === rec.selected); if (!f) { say('Selecciona una receta.'); return redraw(); } open(f); } }, 'Ver detalle')));
}
function profileList(data) {
  const open = p => { rec.profile = p.id; rec.screen = 'detail'; rec.version = null; say(''); redraw(); };
  return h('div', {},
    selectable(data.profiles, [['Producto', p => p.product + (multi() ? ` (${branchName(p.branchId)})` : '')], ['Artículo terminado', p => p.finishedItem || 'Artículo terminado'],
      ['Versión activa', p => versionLabel(p, p.list)], ['Borrador', p => p.list.some(v => v.state === 'DRAFT') ? 'Sí' : 'No']], 'Perfiles de producto', 'No hay perfiles de producto registrados.', p => p.id, open),
    h('div', { class: 'catalog-actions' },
      configure() ? h('button', { type: 'button', class: 'primary', onclick: () => { rec.form = { id: crypto.randomUUID(), productId: '', itemId: '', branchId: state.branch || state.me.branchId || state.branches[0]?.id }; rec.screen = 'owner'; say(''); redraw(); } }, 'Nuevo perfil') : null,
      h('button', { type: 'button', class: 'secondary', onclick: () => { const p = data.profiles.find(x => x.id === rec.selected); if (!p) { say('Selecciona un perfil.'); return redraw(); } open(p); } }, 'Ver detalle')));
}

// ---------- Detalle con versiones ----------
function versionsDetail(owner, kind) {
  const rows = owner.list;
  const selected = () => rows.find(v => v.id === rec.version) ?? null;
  const edit = h('button', { type: 'button', class: 'secondary' }, 'Editar borrador');
  const publish = h('button', { type: 'button', class: 'primary' }, 'Publicar versión');
  const activate = h('button', { type: 'button', class: 'secondary' }, 'Activar versión');
  const view = h('div');
  const sync = () => {
    const v = selected(); edit.disabled = publish.disabled = !(v?.state === 'DRAFT'); activate.disabled = !(v?.state === 'PUBLISHED' && owner.activeVersionId !== v.id);
    view.replaceChildren(versionView(v, kind) ?? '');
  };
  edit.addEventListener('click', () => { const v = selected(); if (!v) return; startEditor(owner, v, kind); });
  publish.addEventListener('click', async () => {
    const v = selected(); if (!v) return;
    try { await command(owner.branchId, `recipe.${kind}.publish`, { versionId: v.id, revision: v.revision }); say('Publicación enviada a la caja. Al aplicarse, la versión queda histórica e inmutable.', true); }
    catch (error) { say(error.message); }
    redraw();
  });
  activate.addEventListener('click', async () => {
    const v = selected(); if (!v) return;
    try { await command(owner.branchId, `recipe.${kind}.activate`, { [kind === 'formula' ? 'formulaId' : 'profileId']: owner.id, versionId: v.id, revision: owner.revision }); say('Activación enviada a la caja.', true); }
    catch (error) { say(error.message); }
    redraw();
  });
  const table = rows.length ? h('div', { class: 'table-wrap history-table recipe-table' }, h('table', { class: 'cards', 'aria-label': kind === 'formula' ? 'Versiones de la receta' : 'Versiones del perfil' },
    h('thead', {}, h('tr', {}, ['Versión', 'Nombre', 'Estado'].map(c => h('th', { text: c })))),
    h('tbody', {}, rows.map(v => { const tr = h('tr', { class: 'click' + (rec.version === v.id ? ' selected' : ''), tabindex: '0',
      onclick: () => { rec.version = v.id; for (const x of tr.parentNode.children) x.classList.toggle('selected', x === tr); sync(); } },
      h('td', { 'data-label': 'Versión', text: `Versión ${v.version}` }), h('td', { 'data-label': 'Nombre', text: v.name }),
      h('td', { 'data-label': 'Estado', text: owner.activeVersionId === v.id ? 'Activa · Publicada' : v.state === 'PUBLISHED' ? 'Publicada' : v.pendingMeasure ? 'Borrador · Por medir' : 'Borrador' }));
      return tr; }))))
    : h('p', { class: 'catalog-empty', text: kind === 'formula' ? 'No hay versiones registradas. Crea la primera versión para definir su fórmula.' : 'No hay versiones registradas. Crea la primera versión del perfil.' });
  sync();
  return h('div', {}, h('p', { class: 'caption recipe-help', text: HELP.versions }), table, view,
    h('div', { class: 'catalog-actions' },
      h('button', { type: 'button', class: 'secondary', onclick: () => { rec.screen = 'list'; rec.version = null; say(''); redraw(); } }, 'Volver'),
      configure() ? h('button', { type: 'button', class: 'secondary', onclick: () => startEditor(owner, null, kind) }, 'Crear nueva versión') : null,
      configure() ? [edit, publish, activate] : null));
}
/** Contenido de la versión elegida (sólo lectura), para revisar antes de publicar o activar. */
function versionView(v, kind) {
  if (!v) return null;
  const lines = list => list.map(l => h('li', {}, h('span', { text: l.item ?? l.formula }), h('b', { class: 'num', text: format(l.atomic, l.dimension) })));
  if (kind === 'formula') return h('div', { class: 'recipe-view' },
    h('p', { text: `Rinde ${format(v.yieldAtomic, v.yieldDimension)}${v.notes ? ` · ${v.notes}` : ''}` }),
    v.ingredients.length ? h('ul', { class: 'rows' }, lines(v.ingredients)) : null,
    v.preparations?.length ? h('div', {}, h('p', { class: 'small', text: 'Usa estas preparaciones:' }), h('ul', { class: 'rows' }, lines(v.preparations))) : null,
    v.groups.map((g, i) => h('p', { class: 'small', text: `Grupo ${i + 1}: ${g.options.map((o, n) => `Opción ${n + 1} ${o.item} ${format(o.atomic, o.dimension)}`).join(' · ')}` })));
  return h('div', { class: 'recipe-view' },
    v.formulas.length ? h('ul', { class: 'rows' }, lines(v.formulas)) : null,
    v.direct.length ? h('ul', { class: 'rows' }, lines(v.direct)) : null);
}
function formulaDetail(data) {
  const f = data.formulas.find(x => x.id === rec.formula);
  if (!f) return pendingOwner('formula');
  return h('div', {}, h('h2', { class: 'detail-title', text: f.name, style: { marginBottom: '2px' } }), h('p', { class: 'muted', text: f.code + (multi() ? ` · ${branchName(f.branchId)}` : '') }),
    versionsDetail(f, 'formula'));
}
function profileDetail(data) {
  const p = data.profiles.find(x => x.id === rec.profile);
  if (!p) return pendingOwner('profile');
  return h('div', {}, h('h2', { class: 'detail-title', text: 'Perfil productivo', style: { marginBottom: '2px' } }),
    h('p', { class: 'muted', text: `${p.product} → ${p.finishedItem}` + (multi() ? ` · ${branchName(p.branchId)}` : '') }), versionsDetail(p, 'profile'));
}
/** Recién creado desde la web: la caja todavía no lo aplica. Se puede preparar su primer borrador; va después en la fila. */
function pendingOwner(kind) {
  const pending = rec.pending.find(x => x.id === (kind === 'formula' ? rec.formula : rec.profile));
  if (!pending) { rec.screen = 'list'; return h('p', { class: 'catalog-empty', text: 'Ya no está disponible.' }); }
  return h('div', {}, h('h2', { class: 'detail-title', text: pending.title }),
    h('p', { class: 'notice', text: 'Enviado a la caja. Aparecerá con sus versiones en cuanto la caja lo aplique; mientras, puedes preparar su primera versión borrador.' }),
    h('div', { class: 'catalog-actions' },
      h('button', { type: 'button', class: 'secondary', onclick: () => { rec.screen = 'list'; say(''); redraw(); } }, 'Volver'),
      configure() ? h('button', { type: 'button', class: 'secondary', onclick: () => startEditor({ id: pending.id, branchId: pending.branchId, list: [], productId: pending.productId, finishedItemId: pending.itemId }, null, kind) }, 'Crear nueva versión') : null));
}

// ---------- Alta de receta y de perfil ----------
function formulaOwner() {
  const f = rec.form;
  const bind = key => e => { f[key] = e.target.value; create.disabled = !f.code.trim() || !f.name.trim(); };
  const create = h('button', { type: 'button', class: 'primary', disabled: !f.code.trim() || !f.name.trim(), onclick: async () => {
    try {
      await command(f.branchId, 'recipe.formula.create', { id: f.id, code: f.code.trim(), name: f.name.trim() });
      rec.pending.push({ id: f.id, branchId: f.branchId, title: `${f.name.trim()} (${f.code.trim().toUpperCase()})` });
      rec.formula = f.id; rec.screen = 'detail'; say('Receta enviada a la caja. Agrega su primera versión borrador.', true);
    } catch (error) { say(error.message); }
    redraw();
  } }, 'Crear receta');
  return h('div', { class: 'catalog-editor' }, h('h2', { class: 'detail-title', text: 'Receta' }),
    branchPicker(f),
    h('div', { class: 'catalog-two' },
      h('label', { class: 'label' }, 'Código', h('input', { maxlength: '60', value: f.code, 'aria-label': 'Código de receta', oninput: bind('code') })),
      h('label', { class: 'label' }, 'Nombre de la receta', h('input', { maxlength: '160', value: f.name, 'aria-label': 'Nombre de receta', oninput: bind('name') }))),
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: () => { rec.screen = 'list'; say(''); redraw(); } }, 'Cancelar'), create));
}
const branchPicker = f => state.me.admin && state.branches.length > 1 && !state.branch ? h('label', { class: 'label' }, 'Sucursal',
  h('select', { onchange: e => { f.branchId = e.target.value; f.productId = ''; f.itemId = ''; redraw(); } }, state.branches.map(b => h('option', { value: b.id, text: b.name, selected: b.id === f.branchId })))) : null;
function profileOwner(data) {
  const f = rec.form;
  const used = new Set(data.profiles.map(p => p.productId));
  const products = data.products.filter(p => p.branchId === f.branchId && !used.has(p.id));
  const finished = data.items.filter(i => i.branchId === f.branchId && i.kind === 'FINISHED_GOOD' && i.dimension === 'COUNT');
  const create = h('button', { type: 'button', class: 'primary', disabled: !f.productId || !f.itemId, onclick: async () => {
    try {
      await command(f.branchId, 'recipe.profile.create', { id: f.id, productId: f.productId, finishedItemId: f.itemId });
      const product = products.find(p => p.id === f.productId)?.name ?? 'Producto', item = finished.find(i => i.id === f.itemId)?.name ?? '';
      rec.pending.push({ id: f.id, branchId: f.branchId, productId: f.productId, itemId: f.itemId, title: `Perfil productivo · ${product} → ${item}` });
      rec.profile = f.id; rec.screen = 'detail'; say('Perfil enviado a la caja. Agrega su primera versión borrador.', true);
    } catch (error) { say(error.message); }
    redraw();
  } }, 'Crear perfil');
  const refresh = () => { create.disabled = !f.productId || !f.itemId; };
  return h('div', { class: 'catalog-editor' }, h('h2', { class: 'detail-title', text: 'Perfil productivo' }),
    branchPicker(f),
    h('div', { class: 'catalog-two' },
      h('label', { class: 'label' }, 'Producto', h('select', { 'aria-label': 'Producto', onchange: e => { f.productId = e.target.value; refresh(); } },
        h('option', { value: '', text: 'Elige un producto' }), products.map(p => h('option', { value: p.id, text: p.name, selected: p.id === f.productId })))),
      h('label', { class: 'label' }, 'Artículo terminado', h('select', { 'aria-label': 'Artículo terminado', onchange: e => { f.itemId = e.target.value; refresh(); } },
        h('option', { value: '', text: 'Elige el artículo terminado' }), finished.map(i => h('option', { value: i.id, text: `${i.code} · ${i.name}`, selected: i.id === f.itemId }))))),
    finished.length ? null : h('p', { class: 'catalog-empty', text: 'No hay artículos terminados disponibles. Crea primero un artículo de tipo Producto terminado en Inventario.' }),
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: () => { rec.screen = 'list'; say(''); redraw(); } }, 'Cancelar'), create));
}

// ---------- Editores de versión ----------
const ingredientRow = (line = null) => ({ key: crypto.randomUUID(), id: line?.id ?? crypto.randomUUID(), itemId: line?.itemId ?? '',
  quantity: line ? numberOf(line.atomic, line.dimension) : '1', unit: line ? unitOf(line.atomic, line.dimension) : 'g', policy: line?.policy ?? 'NotApplicable', group: 0, preference: 0 });
function startEditor(owner, version, kind) {
  const next = Math.max(0, ...owner.list.map(v => v.version)) + 1;
  if (kind === 'formula') rec.form = { owner, versionId: version?.id ?? crypto.randomUUID(), revision: version?.revision ?? null, number: version?.version ?? next,
    name: version?.name ?? '', notes: version?.notes ?? '', yieldDimension: version?.yieldDimension ?? 'MASS',
    yield: version ? numberOf(version.yieldAtomic, version.yieldDimension) : '1', yieldUnit: version ? unitOf(version.yieldAtomic, version.yieldDimension) : 'g',
    ingredients: (version?.ingredients ?? []).map(ingredientRow),
    alternatives: (version?.groups ?? []).flatMap((g, i) => g.options.map((o, n) => ({ ...ingredientRow(o), group: i + 1, preference: n + 1 }))),
    preparations: (version?.preparations ?? []).map(p => ({ key: crypto.randomUUID(), formulaVersionId: p.formulaVersionId, quantity: numberOf(p.atomic, p.dimension), unit: unitOf(p.atomic, p.dimension) })),
    selected: null, selectedAlt: null, selectedPrep: null };
  else rec.form = { owner, versionId: version?.id ?? crypto.randomUUID(), revision: version?.revision ?? null, number: version?.version ?? next, name: version?.name ?? '',
    formulas: (version?.formulas ?? []).map(f => ({ key: crypto.randomUUID(), formulaVersionId: f.formulaVersionId, quantity: numberOf(f.atomic, f.dimension), unit: unitOf(f.atomic, f.dimension) })),
    direct: (version?.direct ?? []).map(ingredientRow), selectedPrep: null, selectedDirect: null };
  rec.search = ''; rec.screen = 'editor'; say(''); redraw();
}
const items = (data, branchId) => data.items.filter(i => i.branchId === branchId);
const matches = (data, branchId) => { const text = plain(rec.search.trim()); return items(data, branchId).filter(i => !text || plain(`${i.code} ${i.name}`).includes(text)); };
const validIngredient = (row, data, branchId) => { const it = items(data, branchId).find(i => i.id === row.itemId); return !!it && UNITS[it.dimension].includes(row.unit) && POLICIES[it.dimension].some(p => p[0] === row.policy) && atomic(row.quantity, row.unit) !== null; };
const payloadLine = (row, data, branchId) => { const it = items(data, branchId).find(i => i.id === row.itemId); return { itemId: row.itemId, atomic: atomic(row.quantity, row.unit), policy: row.policy, dimension: it.dimension }; };

/** Tabla editable de ingredientes (renglones con artículo, cantidad, unidad y política), como las DataGrid de la caja. */
function ingredientTable(rows, data, branchId, { label, selectedKey, onSelect, groups = false, onChange }) {
  const candidates = matches(data, branchId);
  if (!rows.length) return null;
  return h('div', { class: 'table-wrap history-table recipe-table' }, h('table', { class: 'cards', 'aria-label': label },
    h('thead', {}, h('tr', {}, [...(groups ? ['Grupo', 'Orden'] : []), groups ? 'Ingrediente alternativo' : 'Ingrediente', 'Cantidad', 'Unidad', 'Política de cantidad'].map(c => h('th', { text: c })))),
    h('tbody', {}, rows.map(row => {
      const item = items(data, branchId).find(i => i.id === row.itemId);
      const options = item && !candidates.some(c => c.id === item.id) ? [item, ...candidates] : candidates;
      const tr = h('tr', { class: 'click' + (selectedKey === row.key ? ' selected' : ''), onclick: () => { onSelect(row.key); for (const x of tr.parentNode.children) x.classList.toggle('selected', x === tr); } },
        groups ? [h('td', { 'data-label': 'Grupo', text: `Grupo ${row.group}` }), h('td', { 'data-label': 'Orden', text: `Opción ${row.preference}` })] : null,
        h('td', { 'data-label': groups ? 'Ingrediente alternativo' : 'Ingrediente' }, h('select', { 'aria-label': groups ? 'Seleccionar opción alternativa' : 'Seleccionar ingrediente', onchange: e => {
          row.itemId = e.target.value; const it = items(data, branchId).find(i => i.id === row.itemId);
          if (it) { if (!UNITS[it.dimension].includes(row.unit)) row.unit = UNITS[it.dimension][0]; row.policy = defaultPolicy(it.dimension); }
          onChange(true);
        } }, h('option', { value: '', text: 'Elige un artículo' }), options.map(i => h('option', { value: i.id, text: `${i.code} · ${i.name}`, selected: i.id === row.itemId })))),
        h('td', { 'data-label': 'Cantidad' }, h('input', { value: row.quantity, inputmode: 'decimal', maxlength: '20', 'aria-label': 'Cantidad', oninput: e => { row.quantity = e.target.value; onChange(false); } })),
        h('td', { 'data-label': 'Unidad' }, h('select', { 'aria-label': 'Unidad', disabled: !item, onchange: e => { row.unit = e.target.value; onChange(false); } },
          (item ? UNITS[item.dimension] : []).map(u => h('option', { value: u, text: u, selected: u === row.unit })))),
        h('td', { 'data-label': 'Política de cantidad' }, h('select', { 'aria-label': 'Política de cantidad', disabled: !item, onchange: e => { row.policy = e.target.value; onChange(false); } },
          (item ? POLICIES[item.dimension] : []).map(([v, l]) => h('option', { value: v, text: l, selected: v === row.policy })))));
      return tr;
    }))));
}
function searchBox(onChange) {
  let timer = 0;
  const input = h('input', { value: rec.search, maxlength: '160', 'aria-label': 'Buscar ingrediente por nombre o código', oninput: e => { rec.search = e.target.value; clearTimeout(timer); timer = setTimeout(onChange, 250); } });
  return h('div', {}, h('label', { class: 'label' }, 'Buscar ingrediente', h('span', { class: 'inv-search' }, input,
    h('button', { type: 'button', class: 'secondary', disabled: !rec.search, onclick: () => { rec.search = ''; onChange(); } }, '× Limpiar'))));
}

function formulaEditor(data) {
  const f = rec.form, branchId = f.owner.branchId;
  const save = h('button', { type: 'button', class: 'primary' }, 'Guardar borrador');
  // Otras recetas de la sucursal que ésta puede usar ya preparadas (publicadas o en borrador); nunca ella misma.
  const usable = data.formulas.filter(fm => fm.branchId === branchId && fm.id !== f.owner.id)
    .flatMap(fm => fm.list.map(v => ({ id: v.id, label: `${fm.name} · versión ${v.version}${v.state === 'DRAFT' ? ' (borrador)' : ''}`, dimension: v.yieldDimension })));
  const validPrep = r => { const fm = usable.find(x => x.id === r.formulaVersionId); return !!fm && UNITS[fm.dimension].includes(r.unit) && atomic(r.quantity, r.unit) !== null; };
  const complete = () => !!f.name.trim() && atomic(f.yield, f.yieldUnit) !== null && f.ingredients.length + f.alternatives.length + f.preparations.length > 0
    && f.ingredients.every(r => validIngredient(r, data, branchId)) && f.alternatives.every(r => validIngredient(r, data, branchId))
    && f.preparations.every(validPrep) && groups(f.alternatives).every(g => g.length >= 2);
  const refresh = () => { save.disabled = !configure() || !complete(); };
  const rows = h('div');
  const draw = () => {
    rows.replaceChildren(
      searchBox(draw),
      matches(data, branchId).length ? '' : h('p', { class: 'catalog-empty', text: 'No encontramos ingredientes con esa búsqueda.' }),
      h('h3', { class: 'inv-section', text: 'Ingredientes directos de la receta' }), h('p', { class: 'caption', text: HELP.direct }),
      ingredientTable(f.ingredients, data, branchId, { label: 'Ingredientes directos de receta', selectedKey: f.selected, onSelect: k => { f.selected = k; }, onChange: full => full ? draw() : refresh() })
        ?? h('p', { class: 'catalog-empty', text: 'Aún no hay ingredientes directos en esta versión.' }),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'secondary', disabled: !items(data, branchId).length, onclick: () => { const r = ingredientRow(); f.ingredients.push(r); f.selected = r.key; draw(); } }, 'Agregar ingrediente'),
        h('button', { type: 'button', class: 'secondary', onclick: () => { f.ingredients = f.ingredients.filter(r => r.key !== f.selected); f.selected = null; draw(); } }, 'Quitar ingrediente')),
      h('h3', { class: 'inv-section', text: 'Preparaciones que usa esta receta' }), h('p', { class: 'caption', text: HELP.formulaPreparations }),
      f.preparations.length ? h('div', { class: 'table-wrap history-table recipe-table' }, h('table', { class: 'cards', 'aria-label': 'Preparaciones que usa la receta' },
        h('thead', {}, h('tr', {}, ['Receta que usa', 'Cantidad', 'Unidad compatible'].map(c => h('th', { text: c })))),
        h('tbody', {}, f.preparations.map(r => { const fm = usable.find(x => x.id === r.formulaVersionId);
          const tr = h('tr', { class: 'click' + (f.selectedPrep === r.key ? ' selected' : ''), onclick: () => { f.selectedPrep = r.key; for (const x of tr.parentNode.children) x.classList.toggle('selected', x === tr); } },
            h('td', { 'data-label': 'Receta que usa' }, h('select', { 'aria-label': 'Receta que usa', onchange: e => { r.formulaVersionId = e.target.value; const x = usable.find(p => p.id === r.formulaVersionId);
              if (x && !UNITS[x.dimension].includes(r.unit)) r.unit = UNITS[x.dimension][0]; draw(); } },
              h('option', { value: '', text: 'Elige una receta' }), usable.map(p => h('option', { value: p.id, text: p.label, selected: p.id === r.formulaVersionId })))),
            h('td', { 'data-label': 'Cantidad' }, h('input', { value: r.quantity, inputmode: 'decimal', maxlength: '20', 'aria-label': 'Cantidad de la preparación', oninput: e => { r.quantity = e.target.value; refresh(); } })),
            h('td', { 'data-label': 'Unidad compatible' }, h('select', { 'aria-label': 'Unidad compatible', disabled: !fm, onchange: e => { r.unit = e.target.value; refresh(); } },
              (fm ? UNITS[fm.dimension] : []).map(u => h('option', { value: u, text: u, selected: u === r.unit })))));
          return tr; })))) : h('p', { class: 'catalog-empty', text: 'Esta receta no usa otras recetas.' }),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'secondary', disabled: !usable.length, onclick: () => { const r = { key: crypto.randomUUID(), formulaVersionId: '', quantity: '1', unit: 'g' }; f.preparations.push(r); f.selectedPrep = r.key; draw(); } }, 'Agregar preparación'),
        h('button', { type: 'button', class: 'secondary', onclick: () => { f.preparations = f.preparations.filter(r => r.key !== f.selectedPrep); f.selectedPrep = null; draw(); } }, 'Quitar preparación')),
      h('h3', { class: 'inv-section', text: 'Grupos de alternativas' }), h('p', { class: 'caption', text: HELP.alternatives }),
      ingredientTable([...f.alternatives].sort((a, b) => a.group - b.group || a.preference - b.preference), data, branchId,
        { label: 'Grupos y opciones de alternativas', groups: true, selectedKey: f.selectedAlt, onSelect: k => { f.selectedAlt = k; }, onChange: full => full ? draw() : refresh() })
        ?? h('p', { class: 'catalog-empty', text: 'Aún no hay grupos de alternativas. Crea uno sólo cuando varios ingredientes puedan sustituirse.' }),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'secondary', disabled: !items(data, branchId).length, onclick: () => {
          const r = { ...ingredientRow(), group: Math.max(0, ...f.alternatives.map(x => x.group)) + 1, preference: 1 }; f.alternatives.push(r); f.selectedAlt = r.key; draw(); } }, 'Nuevo grupo de alternativas'),
        h('button', { type: 'button', class: 'secondary', onclick: () => {
          const sel = f.alternatives.find(x => x.key === f.selectedAlt); if (!sel) { say('Selecciona una fila del grupo.'); return redraw(); }
          const r = { ...ingredientRow(), group: sel.group, preference: Math.max(0, ...f.alternatives.filter(x => x.group === sel.group).map(x => x.preference)) + 1 };
          f.alternatives.push(r); f.selectedAlt = r.key; draw(); } }, 'Agregar opción al grupo'),
        h('button', { type: 'button', class: 'secondary', onclick: () => {
          const sel = f.alternatives.find(x => x.key === f.selectedAlt); if (!sel) return;
          f.alternatives = f.alternatives.filter(x => x.key !== sel.key); let n = 0;
          for (const r of f.alternatives.filter(x => x.group === sel.group).sort((a, b) => a.preference - b.preference)) r.preference = ++n;
          f.selectedAlt = null; draw(); } }, 'Quitar opción')));
    refresh();
  };
  save.addEventListener('click', async () => {
    if (!complete()) { say('Completa nombre, rendimiento e ingredientes; cada grupo necesita al menos dos opciones.'); return redraw(); }
    const options = groups(f.alternatives).map(g => g.sort((a, b) => a.preference - b.preference)
      .map(r => ({ id: r.id, ...payloadLine(r, data, branchId) })).map(({ dimension, ...o }) => o));
    try {
      await command(branchId, 'recipe.formula.draft', { formulaId: f.owner.id, versionId: f.versionId, revision: f.revision, name: f.name.trim(), notes: f.notes.trim(),
        yield: { dimension: DIMENSION_NAME[f.yieldDimension], atomic: atomic(f.yield, f.yieldUnit) },
        ingredients: f.ingredients.map(r => payloadLine(r, data, branchId)).map(({ dimension, ...o }) => o), groups: options,
        preparations: f.preparations.map(r => ({ formulaVersionId: r.formulaVersionId, atomic: atomic(r.quantity, r.unit) })) });
      rec.screen = 'detail'; rec.formula = f.owner.id; say('Versión borrador enviada a la caja.', true);
    } catch (error) { say(error.message); }
    redraw();
  });
  const yieldUnit = h('select', { 'aria-label': 'Unidad del rendimiento', onchange: e => { f.yieldUnit = e.target.value; refresh(); } });
  const units = () => yieldUnit.replaceChildren(...UNITS[f.yieldDimension].map(u => h('option', { value: u, text: u, selected: u === f.yieldUnit })));
  units();
  draw();
  return h('div', { class: 'catalog-editor recipe-editor' }, h('h2', { class: 'detail-title', text: 'Receta' }),
    h('div', { class: 'recipe-box' },
      h('div', {},
        h('label', { class: 'label' }, 'Versión', h('input', { value: String(f.number), readonly: true, 'aria-label': 'Número de versión automático' })),
        h('label', { class: 'label' }, 'Nombre de esta versión', h('input', { maxlength: '160', value: f.name, 'aria-label': 'Nombre de esta versión', oninput: e => { f.name = e.target.value; refresh(); } })),
        h('label', { class: 'label' }, 'Notas (opcional)', h('textarea', { maxlength: '500', rows: '3', 'aria-label': 'Notas', oninput: e => { f.notes = e.target.value; } }, f.notes))),
      h('div', {},
        h('label', { class: 'label' }, 'Rendimiento', h('input', { inputmode: 'decimal', maxlength: '20', value: f.yield, title: HELP.yield, 'aria-label': 'Rendimiento', oninput: e => { f.yield = e.target.value; refresh(); } })),
        h('label', { class: 'label' }, 'Dimensión', h('select', { 'aria-label': 'Dimensión del rendimiento', onchange: e => { f.yieldDimension = e.target.value; f.yieldUnit = UNITS[f.yieldDimension][0]; units(); refresh(); } },
          Object.entries(DIMENSIONS).map(([v, l]) => h('option', { value: v, text: l, selected: v === f.yieldDimension })))),
        h('label', { class: 'label' }, 'Unidad', yieldUnit),
        h('p', { class: 'caption', text: f.revision === null ? 'La caja asigna el número definitivo al aplicarlo.' : `Borrador · revisión ${f.revision}` }))),
    rows, h('p', { class: 'caption', text: HELP.policy }),
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: () => { rec.screen = 'detail'; say(''); redraw(); } }, 'Cancelar'), save));
}

function profileEditor(data) {
  const f = rec.form, branchId = f.owner.branchId;
  const published = data.publishedFormulas.filter(x => data.formulas.some(fm => fm.branchId === branchId && fm.list.some(v => v.id === x.id)));
  const save = h('button', { type: 'button', class: 'primary' }, 'Guardar borrador');
  const validPrep = r => { const fm = published.find(x => x.id === r.formulaVersionId); return !!fm && UNITS[fm.dimension].includes(r.unit) && atomic(r.quantity, r.unit) !== null; };
  const complete = () => !!f.name.trim() && f.formulas.length + f.direct.length > 0 && f.formulas.every(validPrep) && f.direct.every(r => validIngredient(r, data, branchId));
  const refresh = () => { save.disabled = !configure() || !complete(); };
  const rows = h('div');
  const draw = () => {
    rows.replaceChildren(
      h('h3', { class: 'inv-section', text: 'Preparaciones por pieza' }), h('p', { class: 'caption', text: HELP.preparations }),
      f.formulas.length ? h('div', { class: 'table-wrap history-table recipe-table' }, h('table', { class: 'cards', 'aria-label': 'Preparaciones del perfil' },
        h('thead', {}, h('tr', {}, ['Preparación publicada', 'Cantidad por pieza', 'Unidad compatible'].map(c => h('th', { text: c })))),
        h('tbody', {}, f.formulas.map(r => { const fm = published.find(x => x.id === r.formulaVersionId);
          const tr = h('tr', { class: 'click' + (f.selectedPrep === r.key ? ' selected' : ''), onclick: () => { f.selectedPrep = r.key; for (const x of tr.parentNode.children) x.classList.toggle('selected', x === tr); } },
            h('td', { 'data-label': 'Preparación publicada' }, h('select', { 'aria-label': 'Preparación publicada', onchange: e => { r.formulaVersionId = e.target.value; const x = published.find(p => p.id === r.formulaVersionId);
              if (x && !UNITS[x.dimension].includes(r.unit)) r.unit = UNITS[x.dimension][0]; draw(); } },
              h('option', { value: '', text: 'Elige una preparación' }), published.map(p => h('option', { value: p.id, text: p.label, selected: p.id === r.formulaVersionId })))),
            h('td', { 'data-label': 'Cantidad por pieza' }, h('input', { value: r.quantity, inputmode: 'decimal', maxlength: '20', 'aria-label': 'Cantidad por pieza', oninput: e => { r.quantity = e.target.value; refresh(); } })),
            h('td', { 'data-label': 'Unidad compatible' }, h('select', { 'aria-label': 'Unidad compatible', disabled: !fm, onchange: e => { r.unit = e.target.value; refresh(); } },
              (fm ? UNITS[fm.dimension] : []).map(u => h('option', { value: u, text: u, selected: u === r.unit })))));
          return tr; })))) : h('p', { class: 'catalog-empty', text: 'Aún no hay preparaciones en este perfil.' }),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'secondary', disabled: !published.length, onclick: () => { const r = { key: crypto.randomUUID(), formulaVersionId: '', quantity: '1', unit: 'g' }; f.formulas.push(r); f.selectedPrep = r.key; draw(); } }, 'Agregar preparación'),
        h('button', { type: 'button', class: 'secondary', onclick: () => { f.formulas = f.formulas.filter(r => r.key !== f.selectedPrep); f.selectedPrep = null; draw(); } }, 'Quitar preparación')),
      h('h3', { class: 'inv-section', text: 'Ingredientes directos por pieza' }), h('p', { class: 'caption', text: HELP.direct }),
      searchBox(draw),
      matches(data, branchId).length ? '' : h('p', { class: 'catalog-empty', text: 'No encontramos ingredientes con esa búsqueda.' }),
      ingredientTable(f.direct, data, branchId, { label: 'Ingredientes directos del perfil', selectedKey: f.selectedDirect, onSelect: k => { f.selectedDirect = k; }, onChange: full => full ? draw() : refresh() })
        ?? h('p', { class: 'catalog-empty', text: 'Aún no hay ingredientes directos en este perfil.' }),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'secondary', disabled: !items(data, branchId).length, onclick: () => { const r = ingredientRow(); f.direct.push(r); f.selectedDirect = r.key; draw(); } }, 'Agregar ingrediente directo'),
        h('button', { type: 'button', class: 'secondary', onclick: () => { f.direct = f.direct.filter(r => r.key !== f.selectedDirect); f.selectedDirect = null; draw(); } }, 'Quitar ingrediente directo')));
    refresh();
  };
  save.addEventListener('click', async () => {
    if (!complete()) { say('Completa el nombre y al menos una preparación o ingrediente directo con cantidades válidas.'); return redraw(); }
    try {
      await command(branchId, 'recipe.profile.draft', { profileId: f.owner.id, versionId: f.versionId, revision: f.revision, name: f.name.trim(),
        formulas: f.formulas.map(r => ({ formulaVersionId: r.formulaVersionId, atomic: atomic(r.quantity, r.unit) })),
        direct: f.direct.map(r => payloadLine(r, data, branchId)).map(({ dimension, ...o }) => o) });
      rec.screen = 'detail'; rec.profile = f.owner.id; say('Versión de perfil enviada a la caja.', true);
    } catch (error) { say(error.message); }
    redraw();
  });
  draw();
  const product = data.products.find(p => p.id === f.owner.productId)?.name ?? f.owner.product ?? 'Producto';
  const item = data.items.find(i => i.id === f.owner.finishedItemId)?.name ?? f.owner.finishedItem ?? '';
  return h('div', { class: 'catalog-editor recipe-editor' }, h('h2', { class: 'detail-title', text: 'Perfil productivo' }),
    h('p', { class: 'muted', text: `${product} → ${item}` }),
    h('div', { class: 'catalog-two' },
      h('label', { class: 'label' }, 'Versión', h('input', { value: String(f.number), readonly: true, 'aria-label': 'Número de versión automático' })),
      h('label', { class: 'label' }, 'Nombre de esta versión', h('input', { maxlength: '160', value: f.name, 'aria-label': 'Nombre de esta versión', oninput: e => { f.name = e.target.value; refresh(); } }))),
    rows, h('p', { class: 'caption', text: HELP.policy }),
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: () => { rec.screen = 'detail'; say(''); redraw(); } }, 'Cancelar'), save));
}
