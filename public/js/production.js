// Producción como la caja de escritorio: primero «Capturar lote» (la cuadrícula de panes con foto de Venta, BatchEntryView; pedido del
// dueño 22/10/2026) y luego «Producción con receta»: nueva producción con necesidades y faltantes, alternativas, preparar el borrador,
// producciones en borrador e historial con detalle y «Registrar producción» con lo que salió.
// Calcular necesidades sólo consulta (lib/planning.ts); preparar y registrar se mandan a la caja (production.prepare / production.confirm);
// un lote se manda con /api/web/production-batch (lib/commands.ts → captureBatch).
import { api, at, can, command, h, post, render, state } from './app.js';

const prod = { screen: 'home', plan: [], selected: null, preview: null, choices: {}, batches: [], row: null, batch: null, actuals: {}, message: '', ok: false, ids: null };
const say = (message, ok = false) => { prod.message = message; prod.ok = ok; };
const lot = { productId: null, pieces: '', busy: false, message: '', ok: false };
const redraw = () => render({ silent: true });
const branchName = id => state.branches.find(b => b.id === id)?.name ?? '';
const multi = () => !state.branch && state.branches.length > 1;

const HELP = {
  planned: 'La cantidad planeada es el número entero de piezas terminadas que se pretende elaborar. Determina las preparaciones y los insumos que se consumirán al registrar.',
  preview: 'Calcular necesidades sólo consulta el plan y el inventario: no reserva ni descuenta existencias. Necesario es lo que requiere el plan; Disponible, la existencia actual; Faltante, la diferencia cuando no alcanza. El stock se valida de nuevo al registrar.',
  actual: 'La cantidad real es el número de piezas terminadas obtenidas. El inventario recibe esa cantidad real, pero los insumos se descuentan según el plan fijado. Registrar cero consume los insumos planeados y no genera producto terminado.',
};
/** InventoryDisplay.FormatProduction: piezas en entero; masa y volumen en kg/g o L/ml. */
export function formatProduction(atomic, dimension) {
  if (dimension === 'COUNT') return `${atomic} ${atomic === 1 ? 'pieza' : 'piezas'}`;
  const big = dimension === 'MASS' ? 'kg' : 'L', small = dimension === 'MASS' ? 'g' : 'ml';
  const abs = Math.abs(atomic);
  if (abs < 1_000_000) return `${Number((atomic / 1_000).toFixed(3))} ${small}`;
  if (abs % 1_000_000 === 0) return `${atomic / 1_000_000} ${big}`;
  const major = Math.trunc(abs / 1_000_000), minor = (abs - major * 1_000_000) / 1_000;
  return `${atomic < 0 ? '-' : ''}${major} ${big} ${Number(minor.toFixed(3))} ${small}`;
}
const localDate = iso => { const d = new Date(iso); const p = n => String(n).padStart(2, '0'); return `${p(d.getDate())}/${p(d.getMonth() + 1)}/${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}`; };
const pieces = (n, one, many) => `${n} ${n === 1 ? one : many}`;

export async function produccion() {
  const [recipes, sent] = await Promise.all([can('production.create') ? api('recipes', { branch: state.branch }) : null, can('sales.view') ? api('commands', { branch: state.branch }) : []]);
  prod.profiles = (recipes?.profiles ?? []).filter(p => p.activeVersionId).map(p => { const v = p.list.find(x => x.id === p.activeVersionId);
    return { versionId: p.activeVersionId, branchId: p.branchId, label: `${p.product} · ${v?.name || 'Perfil'} · Versión ${v?.version ?? ''}` + (multi() ? ` (${branchName(p.branchId)})` : '') }; });
  if (prod.screen === 'drafts' || prod.screen === 'history') prod.batches = await api('production-batches', { branch: state.branch, status: prod.screen === 'drafts' ? 'DRAFT' : 'CONFIRMED' });
  const batches = prod.screen === 'home' ? await batchData() : null;
  const body = prod.screen === 'builder' ? builder() : prod.screen === 'drafts' ? list(true) : prod.screen === 'history' ? list(false) : prod.screen === 'detail' ? detail() : [lotes(batches), home(sent, !!batches)];
  return h('div', { class: 'cash-card production' }, body,
    prod.message ? h('p', { class: prod.ok ? 'success' : 'error', role: 'status', text: prod.message, style: { marginTop: '14px' } }) : null);
}

// ---------- Capturar lote ----------
/** Como la caja: quien puede recibir y configurar inventario. */
const canCapture = () => can('inventory.receive') && can('inventory.configure');
/** El artículo terminado de un pan en la caja: «PT-» + su SKU (PT-CM-001…), o los primeros 12 del id sin SKU (igual que lib/commands.ts). */
const batchCode = p => 'PT-' + (p.sku?.trim() ? p.sku.trim().toUpperCase() : p.id.replace(/-/g, '').slice(0, 12).toUpperCase());
/** Un lote necesita una sucursal: la elegida arriba, o la única que hay. */
const batchBranch = () => state.branch || (state.branches.length === 1 ? state.branches[0].id : null);

async function batchData() {
  if (!canCapture()) return null;
  const branchId = batchBranch();
  if (!branchId) return { branchId: null };
  const [catalog, inventory, pending] = await Promise.all([api('catalog', { branch: branchId }), api('inventory', { branch: branchId }), api('commands', { branch: branchId, pendientes: '1' })]);
  const waiting = pending.filter(c => c.status === 'pending');
  // Artículo de cada pan: el que subió la caja (ligado al pan) o el que ya va en la fila; y las piezas mandadas que la caja aún no suma.
  const queuedItem = new Map(waiting.filter(c => c.kind === 'inventory.item.save' && c.payload.code).map(c => [String(c.payload.code).toUpperCase(), c.payload.id]));
  const toAdd = new Map();
  for (const c of waiting) if (c.kind === 'inventory.receive' && c.payload.reference === 'LOTE') toAdd.set(c.payload.itemId, (toAdd.get(c.payload.itemId) ?? 0) + Number(c.payload.quantity));
  const products = catalog.products.filter(p => p.active && p.branchId === branchId).sort((a, b) => a.name.localeCompare(b.name, 'es')).map(p => {
    const code = batchCode(p);
    const item = inventory.items.find(i => i.code.toUpperCase() === code && i.kind === 'FINISHED_GOOD' && i.active);
    const linked = item?.links.some(l => l.productId === p.id);
    const itemId = item?.id ?? queuedItem.get(code);
    return { ...p, stock: linked ? item.main : null, adding: itemId ? toAdd.get(itemId) ?? 0 : 0 };
  });
  return { branchId, products };
}

function lotes(data) {
  if (!data) return null;
  const intro = h('p', { class: 'caption', text: 'Toca un pan y escribe cuántas piezas salieron del lote. Se suman a su existencia y la venta las va descontando.' });
  if (!data.branchId) return h('section', { class: 'batch-entry' }, h('h2', { class: 'detail-title', text: 'Capturar lote' }), intro,
    h('p', { class: 'muted', text: 'Elige arriba la sucursal donde salió el lote.' }));
  const stockText = p => (p.stock === null || p.stock === undefined ? 'Sin lotes capturados' : `Hay ${p.stock} ${p.stock === 1 ? 'pieza' : 'piezas'}`) + (p.adding ? ` · +${p.adding} por confirmar` : '');
  // Tocar un pan sólo cambia el recuadro de captura; la cuadrícula (con sus fotos) no se vuelve a dibujar.
  const form = h('div'), message = h('div');
  const tiles = new Map();
  const say = (text, ok) => { lot.message = text; lot.ok = ok; message.replaceChildren(text ? h('p', { class: ok ? 'success' : 'error', role: 'status', text }) : ''); };
  const tileText = p => { const t = tiles.get(p.id); t.querySelector('.tile-price').textContent = stockText(p);
    const badge = t.querySelector('.count-badge'); badge.hidden = !p.adding; badge.textContent = `+${p.adding}`; };
  const choose = id => {
    lot.productId = id; lot.pieces = '';
    for (const [key, t] of tiles) t.classList.toggle('selected', key === id);
    const p = data.products.find(x => x.id === id);
    if (!p) { form.replaceChildren(); return; }
    const pieces = h('input', { class: 'big-input', inputmode: 'numeric', maxlength: '5', 'aria-label': 'Piezas que salieron', autocomplete: 'off',
      oninput: e => { lot.pieces = e.target.value; }, onkeydown: e => { if (e.key === 'Enter') save(p, buttons); } });
    const buttons = [h('button', { type: 'button', class: 'primary', onclick: () => save(p, buttons) }, 'Sumar a la existencia'),
      h('button', { type: 'button', class: 'secondary', onclick: () => choose(null) }, 'Cancelar')];
    form.replaceChildren(h('div', { class: 'card batch-form' }, h('h3', { text: p.name }), h('p', { class: 'muted', text: stockText(p) }),
      h('label', { class: 'label', text: '¿Cuántas piezas salieron?' }), h('div', { class: 'toolbar' }, pieces, ...buttons)));
    pieces.focus();
  };
  const save = async (p, buttons) => {
    if (lot.busy) return;
    const count = Number(lot.pieces.trim());
    if (!/^\d{1,5}$/.test(lot.pieces.trim()) || count < 1 || count > 10_000) { say('Escribe cuántas piezas salieron: un número entero de 1 a 10,000.', false); return; }
    lot.busy = true; for (const b of buttons) b.disabled = true; say('', false);
    try {
      await post('production-batch', { branchId: data.branchId, productId: p.id, pieces: count });
      p.adding += count; tileText(p); choose(null);
      say(`Listo: se mandaron ${count} ${count === 1 ? 'pieza' : 'piezas'} de ${p.name}. La caja las suma a su existencia en cuanto se conecta.`, true);
    } catch (e) { say(`No se capturó el lote: ${e.message}`, false); for (const b of buttons) b.disabled = false; }
    finally { lot.busy = false; }
  };
  const grid = h('div', { class: 'tiles', 'aria-label': 'Panes para capturar lote' }, data.products.length ? data.products.map(p => {
    const tile = h('button', { type: 'button', class: 'tile', title: p.name, 'aria-label': p.name, onclick: () => { say('', false); choose(p.id); } },
      h('span', { class: 'tile-photo' },
        p.photo ? h('img', { src: `/api/web/photo?product=${p.id}&branch=${p.branchId}&v=${p.photo.sha}`, alt: '', loading: 'lazy' }) : h('span', { class: 'tile-fallback' }, h('small', { text: 'Sin imagen' })),
        h('span', { class: 'count-badge', hidden: !p.adding, text: `+${p.adding}` })),
      h('span', { class: 'tile-name', text: p.name }),
      h('span', { class: 'tile-price', text: stockText(p) }));
    tiles.set(p.id, tile);
    return tile;
  }) : [h('p', { class: 'tiles-empty', text: 'No hay panes activos en el catálogo de esta sucursal.' })]);
  if (lot.message) say(lot.message, lot.ok);
  if (data.products.some(p => p.id === lot.productId)) setTimeout(() => choose(lot.productId));
  return h('section', { class: 'batch-entry' }, h('h2', { class: 'detail-title', text: 'Capturar lote' }), intro, form, message, grid);
}

function home(sent, withBatches = false) {
  const recent = iso => Date.now() - new Date(iso).getTime() < 2 * 86_400_000;
  const waiting = sent.filter(c => c.kind.startsWith('production.') && (c.status === 'pending' || (c.status === 'rejected' && recent(c.at))));
  const label = { 'production.prepare': 'Preparar producción', 'production.confirm': 'Registrar producción', 'production.record': 'Producción' };
  return h('div', { class: withBatches ? 'with-batches' : '' },
    h('h2', { class: 'detail-title', text: withBatches ? 'Producción con receta' : '¿Qué deseas hacer?' }),
    h('div', { class: 'toolbar' },
      can('production.create') ? h('button', { type: 'button', class: 'primary', onclick: start }, 'Nueva producción') : null,
      h('button', { type: 'button', class: 'secondary', onclick: () => go('drafts') }, 'Producciones en borrador'),
      h('button', { type: 'button', class: 'secondary', onclick: () => go('history') }, 'Historial')),
    waiting.length ? h('section', { class: 'inv-sent', style: { marginTop: '16px' } }, h('strong', { text: 'Enviado a la caja' }),
      h('p', { class: 'caption', text: 'La caja lo aplica en cuanto se conecta; entonces aparece en borradores o en el historial.' }),
      h('ul', { class: 'rows' }, waiting.slice(0, 10).map(c => h('li', {},
        h('div', {}, h('strong', { text: `${label[c.kind] ?? c.kind}${c.payload.outputs ? ` · ${c.payload.outputs.reduce((a, o) => a + o.planned, 0)} piezas planeadas` : c.payload.actuals ? ` · ${c.payload.actuals.reduce((a, o) => a + o.actual, 0)} piezas reales` : ''}` }),
          h('small', { class: 'muted', text: ` ${c.by} · ${at(c.at)}` })),
        h('span', { class: 'pill ' + (c.status === 'pending' ? 'warn' : 'bad'), text: c.status === 'pending' ? 'Pendiente' : 'Rechazado' }),
        h('small', { class: 'muted', text: c.message ?? '' }))))) : null,
    h('p', { class: 'caption', text: 'Para saber cuánto producir mañana: Reportes → Producción sugerida.', style: { marginTop: '16px' } }));
}
const go = screen => { prod.screen = screen; prod.row = null; say(''); render(); };

// ---------- Nueva producción ----------
function start() {
  prod.plan = [{ key: crypto.randomUUID(), versionId: '', planned: '1' }]; prod.selected = null; prod.preview = null; prod.choices = {};
  prod.ids = { batchId: crypto.randomUUID(), operationId: crypto.randomUUID() };
  prod.screen = 'builder'; say(''); redraw();
}
const validPlan = () => prod.plan.length > 0 && prod.plan.every(r => r.versionId && /^\d{1,6}$/.test(String(r.planned).trim()) && Number(r.planned) > 0)
  && new Set(prod.plan.map(r => r.versionId)).size === prod.plan.length;
const planBranch = () => prod.profiles.find(p => p.versionId === prod.plan[0]?.versionId)?.branchId;
const request = () => ({ outputs: prod.plan.map(r => ({ profileVersionId: r.versionId, planned: Number(r.planned) })),
  selections: Object.entries(prod.choices).filter(([, v]) => v).map(([k, optionId]) => { const [formulaVersionId, groupId] = k.split('/'); return { formulaVersionId, groupId, optionId }; }) });
function builder() {
  const calc = h('button', { type: 'button', class: 'primary' }, 'Calcular necesidades');
  const create = h('button', { type: 'button', class: 'primary' }, 'Preparar registro de producción');
  const remove = h('button', { type: 'button', class: 'secondary' }, 'Quitar seleccionado');
  const previewBox = h('div');
  const invalidate = () => { prod.preview = null; previewBox.replaceChildren(); refresh(); };
  const refresh = () => {
    calc.disabled = !validPlan(); create.disabled = !(validPlan() && prod.preview?.canConfirm);
    remove.disabled = !(prod.plan.length > 1 && prod.plan.some(r => r.key === prod.selected));
  };
  const rows = prod.plan.map(r => { const tr = h('tr', { class: 'click' + (prod.selected === r.key ? ' selected' : ''), onclick: () => { prod.selected = r.key; for (const x of tr.parentNode.children) x.classList.toggle('selected', x === tr); refresh(); } },
    h('td', { 'data-label': 'Producto · perfil · versión activa' }, h('select', { 'aria-label': 'Seleccionar perfil productivo activo', onchange: e => { r.versionId = e.target.value; prod.choices = {}; invalidate(); } },
      h('option', { value: '', text: 'Elige un producto' }), prod.profiles.map(p => h('option', { value: p.versionId, text: p.label, selected: p.versionId === r.versionId })))),
    h('td', { 'data-label': 'Piezas planeadas' }, h('input', { value: r.planned, inputmode: 'numeric', maxlength: '6', 'aria-label': 'Piezas planeadas', oninput: e => { r.planned = e.target.value; invalidate(); } })));
    return tr; });
  calc.addEventListener('click', async () => {
    if (!validPlan()) return;
    const branches = new Set(prod.plan.map(r => prod.profiles.find(p => p.versionId === r.versionId)?.branchId));
    if (branches.size > 1) { say('Planea cada sucursal por separado.'); return redraw(); }
    calc.disabled = true;
    try { prod.preview = await api('production-plan', { branch: planBranch(), plan: JSON.stringify(request()) }); say(''); }
    catch (error) { prod.preview = null; say(error.message); }
    redraw();
  });
  create.addEventListener('click', async () => {
    create.disabled = true;
    try {
      await command(planBranch(), 'production.prepare', { ...prod.ids, ...request() });
      prod.screen = 'home'; say('Producción enviada a la caja. El plan quedará fijado como borrador; ábrelo en «Producciones en borrador» para registrar lo que salió.', true);
    } catch (error) { say(error.message); }
    redraw();
  });
  const p = prod.preview;
  if (p) previewBox.replaceChildren(
    p.alternatives.length ? p.alternatives.map(a => h('div', { class: 'production-alt' }, h('strong', { text: a.label }),
      h('p', { class: 'caption', text: 'Elige el ingrediente alternativo que se usará en este plan.' }),
      h('select', { 'aria-label': a.label, onchange: e => { prod.choices[`${a.formulaVersionId}/${a.groupId}`] = e.target.value; calc.click(); } },
        a.options.map(o => h('option', { value: o.id, text: o.eligible ? o.label : `${o.label} · no disponible`, selected: (prod.choices[`${a.formulaVersionId}/${a.groupId}`] ?? a.selected) === o.id }))))) : '',
    h('section', { class: 'production-card' },
      h('h3', { class: 'inv-section', text: '2. Vista previa de necesidades' }), h('p', { class: 'caption', text: HELP.preview }),
      h('strong', { text: 'Preparaciones requeridas' }),
      p.formulas.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Preparaciones requeridas' },
        h('thead', {}, h('tr', {}, ['Preparación', 'Versión', 'Cantidad requerida'].map(c => h('th', { text: c })))),
        h('tbody', {}, p.formulas.map(f => h('tr', {}, h('td', { 'data-label': 'Preparación', text: f.name }), h('td', { 'data-label': 'Versión', text: `Versión ${f.version}` }),
          h('td', { 'data-label': 'Cantidad requerida', text: formatProduction(f.required, f.dimension) }))))))
        : h('p', { class: 'muted', text: 'Este plan no usa preparaciones.' }),
      h('strong', { text: 'Insumos y stock', style: { display: 'block', marginTop: '12px' } }),
      p.requirements.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Necesidades y disponibilidad de insumos' },
        h('thead', {}, h('tr', {}, ['Insumo', 'Necesario', 'Disponible', 'Faltante', 'Estado'].map(c => h('th', { text: c })))),
        h('tbody', {}, p.requirements.map(r => h('tr', {}, h('td', { 'data-label': 'Insumo', text: r.name }), h('td', { 'data-label': 'Necesario', text: formatProduction(r.required, r.dimension) }),
          h('td', { 'data-label': 'Disponible', text: r.available === null ? 'Sin saldo inicial' : formatProduction(r.available, r.dimension) }),
          h('td', { 'data-label': 'Faltante', text: r.shortage ? formatProduction(r.shortage, r.dimension) : '—' }),
          h('td', { 'data-label': 'Estado' }, h('span', { class: 'pill ' + (r.state === 'Suficiente' ? 'ok' : 'bad'), text: r.state })))))))
        : h('p', { class: 'muted', text: 'Sin insumos calculados.' })),
    h('div', { class: 'production-state', role: 'status', text: p.canConfirm ? 'Plan calculado. El stock mostrado es informativo y se validará de nuevo al registrar.' : p.problem }));
  refresh();
  return h('div', {},
    h('h2', { class: 'detail-title', text: 'Nueva producción', style: { marginBottom: '4px' } }),
    h('p', { class: 'muted', text: 'Elige qué vas a elaborar y revisa las necesidades antes de preparar el registro.' }),
    h('section', { class: 'production-card' },
      h('h3', { class: 'inv-section', text: '1. Productos y piezas planeadas' }), h('p', { class: 'caption', text: HELP.planned }),
      h('p', { class: 'muted', text: 'Selecciona el producto, el perfil publicado y su versión activa. Escribe piezas enteras mayores que cero.' }),
      prod.profiles.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Productos planeados para producción' },
        h('thead', {}, h('tr', {}, ['Producto · perfil · versión activa', 'Piezas planeadas'].map(c => h('th', { text: c })))), h('tbody', {}, rows)))
        : h('p', { class: 'catalog-empty', text: 'No hay perfiles productivos activos.' }),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'secondary', disabled: !prod.profiles.length, onclick: () => { prod.plan.push({ key: crypto.randomUUID(), versionId: '', planned: '1' }); prod.preview = null; redraw(); } }, 'Agregar producto'),
        remove, calc)),
    previewBox,
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: () => go('home') }, 'Cancelar'), create));
}

// ---------- Borradores e historial ----------
function list(drafts) {
  const rows = prod.batches;
  const open = h('button', { type: 'button', class: 'primary', disabled: true, onclick: () => { prod.batch = rows.find(b => b.id === prod.row); if (!prod.batch) return; prod.actuals = {}; prod.screen = 'detail'; say(''); redraw(); } }, 'Ver detalle');
  const products = b => b.outputs.map(o => o.product).join(', ');
  const result = b => b.outputs.map(o => `${pieces(o.planned, 'planeada', 'planeadas')} · ${o.actual === null ? 'resultado pendiente' : pieces(o.actual, 'real', 'reales')}`).join(' · ');
  const obtained = b => b.outputs.map(o => o.actual === null ? `${o.product}: resultado pendiente` : o.actual === 0 ? `${o.product}: sin producto terminado` : `${o.product}: ${pieces(o.actual, 'pieza obtenida', 'piezas obtenidas')}`).join(' · ');
  const columns = drafts
    ? [['Fecha local', b => localDate(b.createdAt)], ['Estado', () => 'Borrador'], ['Creado por', b => b.createdBy], ['Terminal', b => b.terminal], ['Productos', products], ['Resultado', result]]
    : [['Fecha local', b => localDate(b.createdAt)], ['Estado', () => 'Confirmada'], ['Registrado por', b => b.createdBy], ['Productos', products], ['Planeado / real', result], ['Resultado obtenido', obtained]];
  return h('div', {},
    h('h2', { class: 'detail-title', text: drafts ? 'Producciones en borrador' : 'Historial de producción', style: { marginBottom: '4px' } }),
    h('p', { class: 'muted', text: drafts ? 'Estos planes ya están bloqueados. Abre uno para capturar el resultado real y registrarlo.' : 'Consulta de producciones registradas. Las fechas se muestran en la hora local de este equipo.' }),
    rows.length ? h('div', { class: 'table-wrap history-table production-table' }, h('table', { class: 'cards', 'aria-label': drafts ? 'Producciones en borrador' : 'Historial de producción' },
      h('thead', {}, h('tr', {}, columns.map(c => h('th', { text: c[0] })))),
      h('tbody', {}, rows.map(b => { const tr = h('tr', { class: 'click', tabindex: '0',
        onclick: () => { if (prod.row === b.id) return open.click(); prod.row = b.id; open.disabled = false; for (const x of tr.parentNode.children) x.classList.toggle('selected', x === tr); },
        onkeydown: e => { if (e.key === 'Enter') { prod.row = b.id; open.click(); } } },
        columns.map(c => h('td', { 'data-label': c[0], text: c[1](b) + (c[0] === 'Productos' && multi() ? ` (${branchName(b.branchId)})` : '') })));
        return tr; }))))
      : h('p', { class: 'catalog-empty', text: drafts ? 'No hay producciones en borrador.' : 'Aún no hay producciones confirmadas.' }),
    h('div', { class: 'toolbar', style: { marginTop: '12px' } }, h('button', { type: 'button', class: 'secondary', onclick: () => go('home') }, 'Volver'), open));
}

// ---------- Detalle ----------
function detail() {
  const b = prod.batch;
  if (!b) { prod.screen = 'home'; return home([]); }
  const draft = b.status === 'DRAFT';
  const own = draft && can('production.confirm') && b.createdById === state.me.id;
  const value = o => prod.actuals[o.productId] ?? String(o.actual ?? o.planned);
  const valid = () => b.outputs.every(o => /^\d{1,6}$/.test(String(value(o)).trim()));
  const restriction = h('div', { class: 'production-state', role: 'status' });
  const confirm = h('button', { type: 'button', class: 'primary production-confirm' }, 'Registrar producción');
  const comparisons = new Map();
  const refresh = () => {
    restriction.textContent = !draft ? 'Consulta histórica de solo lectura.' : !own ? 'Sólo el usuario y terminal que crearon este borrador pueden registrarlo.'
      : !valid() ? 'Captura una cantidad real entera, igual o mayor que cero, para cada producto.' : 'El plan ya quedó fijado. Revisa el resultado real antes de registrarlo.';
    confirm.disabled = !(own && valid());
    for (const o of b.outputs) {
      const text = String(value(o)).trim(), n = Number(text);
      comparisons.get(o.productId).textContent = !/^\d{1,6}$/.test(text) ? 'Escribe un entero igual o mayor que cero' : n === o.planned ? 'Igual a lo planeado'
        : `${pieces(Math.abs(n - o.planned), 'pieza', 'piezas')} ${n > o.planned ? 'más' : 'menos'} de lo planeado`;
    }
  };
  for (const o of b.outputs) comparisons.set(o.productId, h('span'));
  confirm.addEventListener('click', async () => {
    if (!valid()) return;
    confirm.disabled = true;
    try {
      await command(b.branchId, 'production.confirm', { batchId: b.id, actuals: b.outputs.map(o => ({ productId: o.productId, actual: Number(value(o)) })) });
      prod.screen = 'home'; say('Registro enviado a la caja. Al aplicarse se descuentan los insumos del plan y entran las piezas reales.', true);
    } catch (error) { say(error.message); }
    redraw();
  });
  const inputs = [...b.inputs.reduce((m, i) => { const k = `${i.itemId}/${i.dimension}`; const x = m.get(k) ?? { ...i, required: 0, alternative: false };
    x.required += i.required; x.alternative ||= i.alternative; return m.set(k, x); }, new Map()).values()];
  const node = h('div', {},
    h('h2', { class: 'detail-title', text: `Producción · ${draft ? 'Borrador' : 'Confirmada'}`, style: { marginBottom: '4px' } }),
    h('p', { class: 'muted inv-meta', text: `Creada: ${localDate(b.createdAt)}\nCreada por: ${b.createdBy}\nTerminal: ${b.terminal}` + (b.confirmedAt ? `\nRegistrada: ${localDate(b.confirmedAt)}` : '') + (b.notes ? `\nNotas: ${b.notes}` : '') }),
    restriction,
    h('section', { class: 'production-card' }, h('h3', { class: 'inv-section', text: 'Plan de producción' }),
      h('strong', { text: 'Productos' }), h('ul', { class: 'production-lines' }, b.outputs.map(o => h('li', { text: `${o.product}: ${pieces(o.planned, 'pieza planeada', 'piezas planeadas')} · ${o.actual === null ? 'resultado pendiente' : pieces(o.actual, 'pieza real', 'piezas reales')}` }))),
      h('strong', { text: 'Preparaciones del plan' }), h('ul', { class: 'production-lines' }, b.formulas.length ? b.formulas.map(f => h('li', { text: `${f.name}: ${formatProduction(f.required, f.dimension)} requeridos` })) : h('li', { class: 'muted', text: 'Sin preparaciones (o la caja todavía no las sube).' })),
      h('strong', { text: draft ? 'Ingredientes que se descontarán' : 'Ingredientes consumidos' }),
      h('ul', { class: 'production-lines' }, inputs.map(i => h('li', { text: `${i.name}: ${formatProduction(i.required, i.dimension)}${i.alternative ? ' · alternativa seleccionada' : ''}` })))),
    draft ? h('section', { class: 'production-card' }, h('h3', { class: 'inv-section', text: 'Resultado real obtenido' }), h('p', { class: 'caption', text: HELP.actual }),
      h('p', { class: 'muted', text: 'Escribe las piezas terminadas que realmente se obtuvieron. Se permiten cero piezas.' }),
      h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Resultado real de producción' },
        h('thead', {}, h('tr', {}, ['Producto', 'Piezas planeadas', 'Piezas reales', 'Comparación'].map(c => h('th', { text: c })))),
        h('tbody', {}, b.outputs.map(o => h('tr', {}, h('td', { 'data-label': 'Producto', text: o.product }), h('td', { 'data-label': 'Piezas planeadas', text: String(o.planned) }),
          h('td', { 'data-label': 'Piezas reales' }, h('input', { value: value(o), inputmode: 'numeric', maxlength: '6', disabled: !own, 'aria-label': `Piezas reales de ${o.product}`, oninput: e => { prod.actuals[o.productId] = e.target.value; refresh(); } })),
          h('td', { 'data-label': 'Comparación' }, comparisons.get(o.productId))))))),
      h('p', { text: 'Al registrar se descontarán los insumos del plan y se ingresarán las piezas reales obtenidas.', style: { fontWeight: '600', margin: '10px 0 8px' } }),
      confirm) : null,
    h('div', { class: 'toolbar', style: { marginTop: '12px' } }, h('button', { type: 'button', class: 'secondary', onclick: () => go('home') }, 'Volver')));
  refresh();
  return node;
}
