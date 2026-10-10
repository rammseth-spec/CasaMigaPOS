// Inventario como la caja de escritorio (InventoryView de QAv1): consumo automático por ventas, búsqueda con filtros, tabla de 50,
// detalle con presentaciones, asociaciones e historial, editores de artículo, operación (con vista previa), presentación y asociación.
// Lo que se pide desde aquí lo aplica la caja con sus reglas (lib/commands.ts); la existencia cambia cuando la caja lo sube.
import { api, at, can, command, dialog, fmt, h, pesos, render, state, toast } from './app.js';

const PAGE = 50;
const KINDS = { RAW_MATERIAL: 'Materia prima', FINISHED_GOOD: 'Producto terminado', PACKAGING: 'Empaque', SUPPLY: 'Insumo' };
// Nombres de la caja (InventoryItemKind / InventoryDimension) para los cambios.
const KIND_NAME = { RAW_MATERIAL: 'RawMaterial', FINISHED_GOOD: 'FinishedGood', PACKAGING: 'Packaging', SUPPLY: 'Supply' };
const DIMENSIONS = { MASS: 'Masa', VOLUME: 'Volumen', COUNT: 'Conteo' };
const DIMENSION_NAME = { MASS: 'Mass', VOLUME: 'Volume', COUNT: 'Count' };
const MOVEMENTS = { OPENING_BALANCE: 'Saldo inicial', RECEIPT: 'Entrada', ADJUSTMENT_GAIN: 'Ajuste positivo', ADJUSTMENT_LOSS: 'Ajuste negativo', WASTE: 'Merma',
  SALE_CONSUMPTION: 'Venta', PRODUCTION_CONSUMPTION: 'Consumo de producción', PRODUCTION_OUTPUT: 'Salida de producción' };
const REASONS = { DAMAGED: 'Dañado', EXPIRED: 'Caducado', PRODUCTION_ERROR: 'Error de producción', SPILL: 'Derrame', OTHER: 'Otro',
  PHYSICAL_COUNT_VARIANCE: 'Diferencia de conteo físico', DATA_CORRECTION: 'Corrección de datos' };
const WASTE = [['Damaged', 'Dañado'], ['Expired', 'Caducado'], ['ProductionError', 'Error de producción'], ['Spill', 'Derrame'], ['Other', 'Otro']];
const ADJUST = [['PhysicalCountVariance', 'Diferencia de conteo físico'], ['DataCorrection', 'Corrección de datos'], ['Other', 'Otro']];
// Mismo orden que la caja (InventoryUnits.All sin mg ni µL): la primera es la que aparece elegida.
const STANDARD = { g: ['MASS', 1_000, 'g'], kg: ['MASS', 1_000_000, 'kg'], ml: ['VOLUME', 1_000, 'ml'], L: ['VOLUME', 1_000_000, 'L'], piece: ['COUNT', 1, 'pieza'] };
const SENT_KINDS = new Set(['inventory.receive', 'inventory.waste', 'inventory.count', 'inventory.opening', 'inventory.item.save', 'inventory.item.active',
  'inventory.unit.add', 'inventory.link', 'inventory.unlink', 'inventory.consumption', 'inventory.cost']);

const inv = { screen: 'list', search: '', kind: '', active: 'true', page: 0, selected: null, detail: null, historyPage: 0, history: null, op: '', form: null,
  link: null, productSearch: '', productPage: 0, product: null, check: null, message: '', ok: false };
const say = (message, ok = false) => { inv.message = message; inv.ok = ok; };
const plain = text => String(text ?? '').normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
const branchName = id => state.branches.find(b => b.id === id)?.name ?? '';
const multi = () => !state.branch && state.branches.length > 1;

// ---------- Cantidades (InventoryDisplay de la caja) ----------
const factor = (atomic, dimension) => dimension === 'COUNT' ? 1 : Math.abs(atomic) >= 1_000_000 ? 1_000_000 : 1_000;
const unitOf = (atomic, dimension) => dimension === 'COUNT' ? 'pieza' : factor(atomic, dimension) === 1_000_000 ? (dimension === 'MASS' ? 'kg' : 'L') : (dimension === 'MASS' ? 'g' : 'ml');
const number = (atomic, dimension) => String(Number((atomic / factor(atomic, dimension)).toFixed(6)));
export const format = (atomic, dimension) => `${number(atomic, dimension)} ${unitOf(atomic, dimension)}`;
const signed = (value, dimension) => value === 0 ? `Sin cambio · ${format(0, dimension)}` : `${value > 0 ? '+' : '−'}${format(Math.abs(value), dimension)}`;
const captureUnit = dimension => ({ MASS: 'g', VOLUME: 'ml' })[dimension] ?? 'pieza';
/** Texto con punto decimal → unidades mínimas; null si no es un número válido. Sin redondeos: lo que no cabe exacto es error. */
function atomicOf(text, perUnit) {
  const value = String(text).trim();
  if (!/^\d{1,9}(\.\d{1,6})?$/.test(value)) return null;
  const [whole, fraction = ''] = value.split('.');
  const scaled = BigInt(whole + fraction) * BigInt(perUnit);
  const divisor = 10n ** BigInt(fraction.length);
  if (scaled % divisor !== 0n) return NaN;
  return Number(scaled / divisor);
}

export async function inventario() {
  const [data, sent] = await Promise.all([api('inventory', { branch: state.branch }), can('sales.view') ? api('commands', { branch: state.branch }) : []]);
  inv.data = data;
  const body = inv.screen === 'detail' ? await detailScreen(data) : inv.screen === 'item' ? itemEditor(data) : inv.screen === 'operation' ? operationEditor(data)
    : inv.screen === 'unit' ? unitEditor(data) : inv.screen === 'link' ? linkEditor(data) : listScreen(data, sent);
  return h('div', { class: 'cash-card inventory' }, body,
    inv.message ? h('p', { class: inv.ok ? 'success' : 'error', role: 'status', text: inv.message, style: { marginTop: '14px' } }) : null);
}
const redraw = () => render({ silent: true });
const current = data => data.items.find(i => i.id === inv.detail) ?? null;
const back = () => { inv.screen = inv.screen === 'detail' || !inv.detail ? 'list' : 'detail'; if (inv.screen === 'list') inv.detail = null; say(''); redraw(); };

// ---------- Lista ----------
function listScreen(data, sent) {
  const filtered = () => {
    const text = plain(inv.search.trim());
    return data.items.filter(i => (!inv.kind || i.kind === inv.kind) && (inv.active === '' || i.active === (inv.active === 'true'))
      && (!text || plain(`${i.code} ${i.name}`).includes(text)));
  };
  const tableBox = h('div');
  const pageLabel = h('p', { class: 'catalog-page' });
  const prev = h('button', { type: 'button', class: 'secondary', onclick: () => { inv.page--; draw(); } }, 'Anterior');
  const next = h('button', { type: 'button', class: 'secondary', onclick: () => { inv.page++; draw(); } }, 'Siguiente');
  const detailButton = h('button', { type: 'button', class: 'secondary', onclick: () => openDetail(inv.selected) }, 'Ver detalle');
  const choices = h('select', { 'aria-label': 'Seleccionar artículo de inventario', onchange: e => { const it = data.items.find(i => i.id === e.target.value);
    if (it) { inv.search = it.code; search.value = it.code; inv.page = 0; inv.selected = it.id; draw(); } } });
  const clear = h('button', { type: 'button', class: 'secondary', 'aria-label': 'Limpiar búsqueda de inventario', onclick: () => { inv.search = ''; search.value = ''; inv.page = 0; draw(); search.focus(); } }, '× Limpiar');
  let timer = 0;
  const search = h('input', { maxlength: '160', value: inv.search, 'aria-label': 'Buscar inventario',
    oninput: e => { inv.search = e.target.value; clearTimeout(timer); timer = setTimeout(() => { inv.page = 0; draw(); }, 250); },
    onkeydown: e => { if (e.key === 'Enter') { clearTimeout(timer); inv.page = 0; draw(); } } });
  const draw = () => {
    const list = filtered();
    const pages = Math.max(1, Math.ceil(list.length / PAGE));
    inv.page = Math.max(0, Math.min(inv.page, pages - 1));
    const page = list.slice(inv.page * PAGE, (inv.page + 1) * PAGE);
    if (!page.some(i => i.id === inv.selected)) inv.selected = null;
    clear.hidden = !inv.search;
    const pool = data.items.filter(i => (!inv.kind || i.kind === inv.kind) && (inv.active === '' || i.active === (inv.active === 'true')))
      .sort((a, b) => a.name.localeCompare(b.name, 'es') || a.code.localeCompare(b.code));
    choices.replaceChildren(h('option', { value: '', text: '' }), ...pool.map(i => h('option', { value: i.id, text: `${i.name} · ${i.code}` + (multi() ? ` (${branchName(i.branchId)})` : '') })));
    const rows = page.map(i => {
      const tr = h('tr', { class: 'click' + (inv.selected === i.id ? ' selected' : '') + (i.active ? '' : ' off'), tabindex: '0',
        onclick: () => { if (inv.selected === i.id) return openDetail(i.id); inv.selected = i.id; for (const r of tr.parentNode.children) r.classList.toggle('selected', r === tr); detailButton.disabled = false; },
        ondblclick: () => openDetail(i.id), onkeydown: e => { if (e.key === 'Enter') openDetail(i.id); } },
        h('td', { 'data-label': 'Código', text: i.code }), h('td', { 'data-label': 'Nombre', text: i.name + (multi() ? ` (${branchName(i.branchId)})` : '') }),
        h('td', { 'data-label': 'Tipo', text: KINDS[i.kind] ?? i.kind }),
        h('td', { class: 'num', 'data-label': 'Existencia actual', text: i.main === null ? 'Sin inicializar' : number(i.main, i.dimension) }),
        h('td', { 'data-label': 'Unidad', text: unitOf(i.main ?? 0, i.dimension) }), h('td', { 'data-label': 'Estado', text: i.active ? 'Activo' : 'Inactivo' }),
        h('td', { 'data-label': 'Producto', text: i.links.length ? 'Sí' : 'No' }));
      return tr;
    });
    tableBox.replaceChildren(page.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Artículos de inventario' },
      h('thead', {}, h('tr', {}, ['Código', 'Nombre', 'Tipo', 'Existencia actual', 'Unidad', 'Estado', 'Producto'].map((c, n) => h('th', { class: n === 3 ? 'num' : null, text: c })))),
      h('tbody', {}, rows))) : h('div', { class: 'catalog-empty', role: 'status', text: inv.search ? 'No encontramos artículos con esa búsqueda.' : 'No hay artículos con los filtros seleccionados.' }));
    pageLabel.textContent = `Página ${inv.page + 1} · ${list.length} artículos · hasta ${PAGE} por página`;
    prev.disabled = inv.page === 0; next.disabled = (inv.page + 1) * PAGE >= list.length; detailButton.disabled = !inv.selected;
  };
  draw();
  const waiting = waitingList(data, sent);
  return h('div', {},
    consumptionPanels(data),
    waiting,
    h('label', { class: 'history-label' }, 'Buscar por código o nombre', h('span', { class: 'inv-search' }, search, clear)),
    h('div', { class: 'inv-filters' },
      h('label', { class: 'history-label' }, 'Tipo', h('select', { 'aria-label': 'Tipo de artículo', onchange: e => { inv.kind = e.target.value; inv.page = 0; draw(); } },
        h('option', { value: '', text: 'Todos los tipos' }), Object.entries(KINDS).map(([v, l]) => h('option', { value: v, text: l, selected: inv.kind === v })))),
      h('label', { class: 'history-label' }, 'Estado', h('select', { 'aria-label': 'Estado del artículo', onchange: e => { inv.active = e.target.value; inv.page = 0; draw(); } },
        [['true', 'Activos'], ['false', 'Inactivos'], ['', 'Todos']].map(([v, l]) => h('option', { value: v, text: l, selected: inv.active === v })))),
      h('button', { type: 'button', class: 'secondary', onclick: () => { inv.page = 0; draw(); } }, 'Buscar')),
    h('label', { class: 'history-label' }, 'O selecciona un artículo de la lista', choices),
    tableBox,
    h('div', { class: 'inv-pager' }, pageLabel,
      h('div', { class: 'toolbar' }, prev, next,
        can('inventory.configure') ? h('button', { type: 'button', class: 'primary', onclick: () => openItem(null, data) }, 'Nuevo artículo') : null, detailButton)));
}

/** Lo que se mandó a la caja y todavía no aplica (o rechazó hace poco), como en las otras pantallas de la web. */
function waitingList(data, sent) {
  const recent = iso => Date.now() - new Date(iso).getTime() < 2 * 86_400_000;
  const waiting = sent.filter(c => SENT_KINDS.has(c.kind) && (c.status === 'pending' || (c.status === 'rejected' && recent(c.at))));
  if (!waiting.length) return null;
  const items = new Map(data.items.map(i => [i.id, i]));
  return h('section', { class: 'inv-sent' }, h('strong', { text: 'Enviado a la caja' }),
    h('p', { class: 'caption', text: 'La caja lo aplica en cuanto se conecta; entonces cambian las existencias.' }),
    h('ul', { class: 'rows' }, waiting.slice(0, 12).map(c => h('li', {},
      h('div', {}, h('strong', { text: describeSent(c, items) }), h('small', { class: 'muted', text: ` ${c.by} · ${at(c.at)}` })),
      h('span', { class: 'pill ' + (c.status === 'pending' ? 'warn' : 'bad'), text: c.status === 'pending' ? 'Pendiente' : 'Rechazado' }),
      h('small', { class: 'muted', text: c.message ?? '' })))));
}
function describeSent(c, items) {
  const p = c.payload, item = items.get(p.itemId ?? p.id);
  const name = item?.name ?? p.name ?? 'Artículo';
  const amount = () => `${p.quantity} ${STANDARD[p.unitCode]?.[2] ?? item?.units.find(u => u.code === p.unitCode)?.name ?? p.unitCode}`;
  switch (c.kind) {
    case 'inventory.receive': return `Entrada · ${name} · ${amount()}`;
    case 'inventory.waste': return `Merma · ${name} · ${amount()}`;
    case 'inventory.count': return `Conteo físico · ${name} · ${amount()}`;
    case 'inventory.opening': return `Saldo inicial · ${name} · ${amount()}`;
    case 'inventory.item.save': return `Artículo · ${p.code ? `${p.code} · ` : ''}${p.name}`;
    case 'inventory.item.active': return `${p.active ? 'Activar' : 'Desactivar'} · ${name}`;
    case 'inventory.unit.add': return `Presentación · ${name} · ${p.name}`;
    case 'inventory.link': return `Asociar producto · ${name}`;
    case 'inventory.unlink': return 'Desvincular producto';
    case 'inventory.consumption': return `Consumo automático · ${{ Enable: 'Activar', Pause: 'Pausar', Resume: 'Reanudar' }[p.action]}`;
    case 'inventory.cost': return `Costo de referencia · ${name} · ${fmt(p.costCents)} por ${p.unitLabel}`;
    default: return c.kind;
  }
}

// ---------- Consumo automático por ventas ----------
function consumptionPanels(data) {
  const ids = state.branch ? [state.branch] : state.branches.map(b => b.id);
  return ids.map(id => consumptionPanel(id, data.consumption.find(c => c.branchId === id)));
}
function consumptionPanel(branchId, status) {
  const configure = can('inventory.sale_consumption.configure');
  const s = status?.state;
  const label = { DISABLED: 'DESACTIVADO', ENABLED: 'ACTIVO', PAUSED: 'PAUSADO' }[s] ?? 'No disponible';
  const change = (action, message) => dialog('Consumo automático por ventas', b => b.append(h('p', { class: 'wide', text: message })), {
    submit: 'Sí', cancel: 'No', onSubmit: async () => {
      await command(branchId, 'inventory.consumption', { action, version: status.version });
      say('Cambio enviado a la caja. El estado se actualiza cuando la caja lo aplica.', true); redraw();
    } });
  const check = inv.check?.branchId === branchId ? inv.check : null;
  return h('section', { class: 'inv-consumption' },
    h('h2', { text: 'Consumo automático por ventas' + (multi() ? ` · ${branchName(branchId)}` : '') }),
    h('p', { class: 'inv-state', text: label }),
    h('p', { text: s === 'ENABLED' ? 'Las ventas configuradas descuentan existencias automáticamente.' : 'Las ventas se registran sin descontar existencias automáticamente.' }),
    status?.updatedAt ? h('p', { class: 'caption', text: `Última actualización: ${localDate(status.updatedAt).replace(' · ', ' ')}${status.updatedBy ? ` · ${status.updatedBy}` : ''}` })
      : !status?.state ? h('p', { class: 'caption', text: 'La caja de esta sucursal todavía no sube este estado; actualiza Casa Miga en la caja.' }) : null,
    h('div', { class: 'toolbar' },
      h('button', { type: 'button', class: 'secondary', onclick: () => { inv.check = null; say(''); render(); } }, 'Actualizar estado'),
      configure && s ? h('button', { type: 'button', class: 'secondary', onclick: async () => {
        try { inv.check = { branchId, ...(await api('inventory-check', { branch: branchId })) }; } catch (error) { toast(error.message); }
        redraw();
      } }, 'Verificar configuración') : null,
      configure && s === 'DISABLED' ? h('button', { type: 'button', class: 'primary', onclick: () => change('Enable', 'Al activar esta función, las ventas de productos configurados descontarán existencias automáticamente. ¿Deseas continuar?') }, 'Activar') : null,
      configure && s === 'ENABLED' ? h('button', { type: 'button', class: 'secondary', onclick: () => change('Pause', 'Durante la pausa, las ventas continuarán registrándose pero no descontarán inventario. ¿Confirmas la pausa?') }, 'Pausar') : null,
      configure && s === 'PAUSED' ? h('button', { type: 'button', class: 'secondary', onclick: () => change('Resume', 'Al activar esta función, las ventas de productos configurados descontarán existencias automáticamente. ¿Deseas continuar?') }, 'Reanudar') : null),
    check ? h('div', { class: 'inv-check' },
      h('p', { text: check.problems.length ? 'Corrige los siguientes problemas antes de activar o reanudar:' : 'Configuración válida. Se verificará nuevamente al activar o reanudar.' }),
      check.problems.length ? h('ul', {}, check.problems.map(p => h('li', { text: problem(p) }))) : null) : null);
}
/** Mismos textos que la caja (ConsumptionProblems). */
function problem(p) {
  const who = p.product ?? p.item ?? 'El artículo';
  return {
    TrackedProductWithoutInventoryLink: `${who} controla inventario pero no tiene un artículo asociado.`,
    UnexpectedInventoryLink: `${who} tiene un artículo asociado, pero no está marcado para controlar inventario.`,
    InventoryItemInactive: `${who} está asociado a un artículo de inventario inactivo.`,
    NotInitialized: `${who} no tiene saldo inicial registrado.`,
    ReconciliationMismatch: `Hay diferencias entre los movimientos y las existencias (${p.item}). Solicita revisión administrativa; no se repararán automáticamente.`,
  }[p.code] ?? `${who} tiene una asociación o cantidad de inventario inválida. Revisa su configuración.`;
}

// ---------- Detalle ----------
function openDetail(id) {
  if (!id) { say('Selecciona un artículo.'); return redraw(); }
  inv.detail = id; inv.historyPage = 0; inv.screen = 'detail'; inv.link = null; say(''); redraw();
}
async function detailScreen(data) {
  const item = current(data);
  if (!item) { inv.screen = 'list'; inv.detail = null; say('El artículo ya no está disponible.'); return listScreen(data, []); }
  const history = await api('inventory-item', { id: item.id, page: inv.historyPage, branch: item.branchId });
  const balance = item.main;
  const warning = !history.consistent;
  const configure = can('inventory.configure');
  const usable = item.active && balance !== null;
  const button = (text, enabled, onclick, primary = false) => h('button', { type: 'button', class: primary ? 'primary' : 'secondary', disabled: !enabled, onclick }, text);
  const links = item.links;
  unlink.disabled = !configure || !links.some(l => l.productId === inv.link);
  return h('div', { class: 'inv-detail' },
    h('h2', { class: 'inv-title', text: `${item.code} · ${item.name}` }),
    h('p', { class: 'inv-meta', text: `Tipo: ${KINDS[item.kind] ?? item.kind}\nDimensión: ${DIMENSIONS[item.dimension]}\nEstado: ${item.active ? 'Activo' : 'Inactivo'}\nUbicación: Almacén principal` + (multi() ? `\nSucursal: ${branchName(item.branchId)}` : '') }),
    h('p', { class: 'inv-balance', text: balance === null ? 'Existencia actual: sin saldo inicial' : `Existencia actual: ${format(balance, item.dimension)}` }),
    h('div', { class: 'inv-reconcile' + (warning ? ' bad' : ''), role: 'status', text: warning
      ? 'ADVERTENCIA: La existencia registrada no coincide con el historial de movimientos. No se realizó ninguna reparación.' : 'Inventario consistente.' }),
    h('div', { class: 'toolbar inv-actions' },
      button('Volver', true, back),
      configure ? button('Editar', true, () => openItem(item, data)) : null,
      configure ? button(item.active ? 'Desactivar' : 'Activar', !item.active || (balance ?? 0) === 0, () => toggle(item)) : null,
      can('inventory.adjust') && state.me.admin && item.active && balance === null ? button('Saldo inicial', true, () => begin('opening', item)) : null,
      can('inventory.receive') ? button('Entrada', usable, () => begin('receipt', item)) : null,
      can('inventory.waste') ? button('Merma', usable && balance > 0, () => begin('waste', item)) : null,
      can('inventory.adjust') ? button('Conteo físico', usable && !warning && state.me.admin, () => begin('count', item)) : null,
      configure ? button('Costo de referencia', true, () => costDialog(item, data.measures)) : null),
    h('h3', { class: 'inv-section', text: 'Presentaciones' }),
    item.units.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Presentaciones del artículo' },
      h('thead', {}, h('tr', {}, ['Código', 'Nombre', 'Equivalencia', 'Activa'].map(c => h('th', { text: c })))),
      h('tbody', {}, item.units.map(u => h('tr', {}, h('td', { 'data-label': 'Código', text: u.code }), h('td', { 'data-label': 'Nombre', text: u.name }),
        h('td', { 'data-label': 'Equivalencia', text: `1 ${u.name} = ${format(u.atomic, item.dimension)}` }),
        h('td', { 'data-label': 'Activa' }, h('input', { type: 'checkbox', checked: u.active, disabled: true, 'aria-label': u.active ? 'Activa' : 'Inactiva' }))))))) :
      h('p', { class: 'catalog-empty', text: 'No hay presentaciones registradas.' }),
    configure ? h('div', { class: 'toolbar' }, button('Agregar presentación', item.active, () => { inv.form = { code: '', name: '', factor: '' }; inv.screen = 'unit'; say(''); redraw(); })) : null,
    h('h3', { class: 'inv-section', text: 'Asociaciones con productos' }),
    h('p', { text: association(item) }),
    h('p', { class: 'caption', text: 'Asociar un producto define qué cantidad de este artículo corresponde a cada unidad vendida. Crear la asociación no activa el consumo automático ni registra un movimiento.' }),
    links.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Asociaciones de productos' },
      h('thead', {}, h('tr', {}, ['Producto', 'SKU', 'Cantidad por venta'].map(c => h('th', { text: c })))),
      h('tbody', {}, links.map(l => { const tr = h('tr', { class: 'click' + (inv.link === l.productId ? ' selected' : ''), tabindex: '0',
        onclick: () => { inv.link = l.productId; for (const r of tr.parentNode.children) r.classList.toggle('selected', r === tr); unlink.disabled = !configure; } },
        h('td', { 'data-label': 'Producto', text: l.product }), h('td', { 'data-label': 'SKU', text: l.sku || '—' }), h('td', { 'data-label': 'Cantidad por venta', text: format(l.atomic, item.dimension) }));
        return tr; })))) : h('p', { class: 'catalog-empty', text: 'No hay productos asociados.' }),
    configure ? h('div', { class: 'toolbar' }, button('Asociar producto', item.active, () => { inv.form = { quantity: '' }; inv.productSearch = ''; inv.productPage = 0; inv.product = null; inv.screen = 'link'; say(''); redraw(); }), unlink) : null,
    h('h3', { class: 'inv-section', text: 'Historial de movimientos' }),
    history.history.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Historial de inventario' },
      h('thead', {}, h('tr', {}, ['Fecha local', 'Operación', 'Movimiento', 'Existencia anterior', 'Existencia resultante', 'Usuario', 'Motivo', 'Referencia'].map(c => h('th', { text: c })))),
      h('tbody', {}, history.history.map(m => h('tr', {},
        h('td', { 'data-label': 'Fecha local', text: localDate(m.at) }), h('td', { 'data-label': 'Operación', text: MOVEMENTS[m.kind] ?? 'Movimiento no disponible' }),
        h('td', { 'data-label': 'Movimiento', text: signed(m.delta, item.dimension) }), h('td', { 'data-label': 'Existencia anterior', text: format(m.before, item.dimension) }),
        h('td', { 'data-label': 'Existencia resultante', text: format(m.after, item.dimension) }), h('td', { 'data-label': 'Usuario', text: m.user || '—' }),
        h('td', { 'data-label': 'Motivo', text: m.reason ? REASONS[m.reason] ?? 'Motivo no disponible' : '—' }), h('td', { 'data-label': 'Referencia', text: m.reference || '—' }))))))
      : h('p', { class: 'catalog-empty', text: 'Aún no hay movimientos registrados.' }),
    h('div', { class: 'inv-pager' }, h('p', { class: 'catalog-page', text: `Página ${history.page + 1} · ${history.total} movimientos · hasta ${history.pageSize} por página` }),
      h('div', { class: 'toolbar' },
        button('Anterior', history.page > 0, () => { inv.historyPage--; redraw(); }),
        button('Siguiente', (history.page + 1) * history.pageSize < history.total, () => { inv.historyPage++; redraw(); }))));
}
// Un solo botón para todas las vistas del detalle: se habilita al elegir una asociación.
const unlink = h('button', { type: 'button', class: 'secondary', disabled: true }, 'Desvincular seleccionado');
unlink.addEventListener('click', async () => {
  const item = current(inv.data);
  if (!inv.link || !item) { say('Selecciona una asociación.'); return redraw(); }
  try { await command(item.branchId, 'inventory.unlink', { productId: inv.link }); say('Desvinculación enviada a la caja.', true); inv.link = null; }
  catch (error) { say(error.message); }
  redraw();
});
const association = item => (item.kind === 'RAW_MATERIAL' ? 'Esta asociación descuenta directamente la materia prima; no calcula una receta. ' : 'La asociación indica cuánto descontar de este artículo por unidad vendida. ')
  + 'Sólo se utiliza si el producto controla inventario y el consumo automático está activo. Asociar no activa el consumo automático.';
const localDate = iso => { const d = new Date(iso); const p = n => String(n).padStart(2, '0'); return `${p(d.getDate())}/${p(d.getMonth() + 1)}/${d.getFullYear()} · ${p(d.getHours())}:${p(d.getMinutes())}`; };

async function toggle(item) {
  try { await command(item.branchId, 'inventory.item.active', { id: item.id, active: !item.active }); say(item.active ? 'Desactivación enviada a la caja.' : 'Activación enviada a la caja.', true); }
  catch (error) { say(error.message); }
  redraw();
}

// ---------- Artículo ----------
function openItem(item, data) {
  inv.form = item ? { id: item.id, branchId: item.branchId, code: item.code, name: item.name, kind: item.kind, dimension: item.dimension }
    : { id: crypto.randomUUID(), branchId: state.branch || state.me.branchId || state.branches[0]?.id, code: '', name: '', kind: 'RAW_MATERIAL', dimension: 'MASS', creating: true };
  inv.screen = 'item'; say(''); redraw();
}
function itemEditor() {
  const f = inv.form;
  const bind = key => e => { f[key] = e.target.value; };
  return h('div', { class: 'catalog-editor' },
    h('h2', { class: 'detail-title', text: f.creating ? 'Crear artículo' : 'Editar artículo' }),
    f.creating && state.me.admin && state.branches.length > 1 ? h('label', { class: 'label' }, 'Sucursal', h('select', { onchange: bind('branchId') },
      state.branches.map(b => h('option', { value: b.id, text: b.name, selected: b.id === f.branchId })))) : null,
    h('label', { class: 'label' }, 'Código', h('input', { maxlength: '80', value: f.code, readonly: !f.creating, 'aria-label': 'Código', oninput: bind('code') })),
    h('label', { class: 'label' }, 'Nombre', h('input', { maxlength: '160', value: f.name, 'aria-label': 'Nombre', oninput: bind('name') })),
    h('label', { class: 'label' }, 'Tipo', h('select', { 'aria-label': 'Tipo', onchange: bind('kind') }, Object.entries(KINDS).map(([v, l]) => h('option', { value: v, text: l, selected: f.kind === v })))),
    h('label', { class: 'label' }, 'Dimensión · queda fija después de crear', h('select', { 'aria-label': 'Dimensión', disabled: !f.creating, onchange: bind('dimension') },
      Object.entries(DIMENSIONS).map(([v, l]) => h('option', { value: v, text: l, selected: f.dimension === v })))),
    h('div', { class: 'toolbar' },
      h('button', { type: 'button', class: 'secondary', onclick: back }, 'Cancelar'),
      h('button', { type: 'button', class: 'primary', onclick: async () => {
        if (!f.code.trim() || !f.name.trim()) { say('Escribe el código y el nombre del artículo.'); return redraw(); }
        try {
          await command(f.branchId, 'inventory.item.save', { id: f.id, ...(f.creating ? { code: f.code.trim(), dimension: DIMENSION_NAME[f.dimension] } : {}), name: f.name.trim(), kind: KIND_NAME[f.kind] });
          say('Artículo enviado a la caja. Aparecerá aquí en cuanto la caja lo aplique.', true);
          inv.screen = f.creating ? 'list' : 'detail';
        } catch (error) { say(error.message); }
        redraw();
      } }, 'Guardar')));
}

// ---------- Operación (saldo inicial, entrada, merma, conteo) ----------
const OPS = {
  opening: { title: 'Registrar saldo inicial', label: 'Existencia al comenzar', movement: 'Saldo inicial', kind: 'inventory.opening',
    help: 'Registra una sola vez la existencia física al comenzar a controlar el artículo. No es una entrada de compra. Puede ser cero.' },
  receipt: { title: 'Registrar entrada', label: 'Cantidad recibida', movement: 'Entrada', kind: 'inventory.receive',
    help: 'Registra mercancía o material que ingresa al inventario y aumenta la existencia. Requiere saldo inicial.' },
  waste: { title: 'Registrar merma', label: 'Cantidad de merma', movement: 'Merma', kind: 'inventory.waste',
    help: 'Registra material perdido, dañado, caducado o no utilizable. Disminuye la existencia y conserva el motivo.' },
  count: { title: 'Conteo físico', label: 'Nueva existencia física', movement: 'Conteo físico', kind: 'inventory.count',
    help: 'Captura todo lo contado físicamente, no la diferencia. Se compara con la existencia registrada y se deja trazabilidad del ajuste. Si coincide, no se crea un movimiento.' },
};
const unitChoices = item => [
  ...Object.entries(STANDARD).filter(([, u]) => u[0] === item.dimension).map(([code, u]) => ({ code, label: `${u[2]} · unidad estándar`, atomic: u[1], presentation: false })),
  ...item.units.filter(u => u.active).map(u => ({ code: u.code, label: `${u.name} · ${format(u.atomic, item.dimension)}`, atomic: u.atomic, presentation: true, name: u.name }))];
function begin(op, item) {
  inv.op = op; inv.form = { quantity: op === 'opening' ? '0' : '', unit: unitChoices(item)[0]?.code ?? '', reference: '', note: '', waste: 'Damaged', adjust: 'PhysicalCountVariance' };
  inv.screen = 'operation'; say(''); redraw();
}
/** Vista previa como InventoryOperationPreview: no escribe nada; la caja vuelve a revisar al aplicar. */
function preview(item, op, f) {
  const allowZero = op === 'opening' || op === 'count';
  if (!String(f.quantity).trim()) throw new Error('Escribe una cantidad para ver el resultado.');
  const unit = unitChoices(item).find(u => u.code === f.unit);
  if (!unit) throw new Error('Selecciona una unidad o presentación.');
  if (((op === 'waste' && f.waste === 'Other') || (op === 'count' && f.adjust === 'Other')) && !f.note.trim()) throw new Error('Escribe una observación para el motivo Otro.');
  if (f.note.length > 500) throw new Error('La observación admite hasta 500 caracteres.');
  if (f.reference.length > 160) throw new Error('La referencia admite hasta 160 caracteres.');
  if (!item.active) throw new Error('El artículo está inactivo.');
  if (op === 'opening' && item.main !== null) throw new Error('Este artículo ya tiene saldo inicial.');
  if (op !== 'opening' && item.main === null) throw new Error('Este artículo todavía no tiene saldo inicial.');
  const text = String(f.quantity).trim().replace(',', '.');
  if (unit.presentation && !/^\d+$/.test(text)) throw new Error(/^\d+(\.\d+)?$/.test(text) ? 'La presentación se captura en unidades enteras.' : 'La cantidad ingresada no es válida.');
  const amount = atomicOf(text, unit.atomic);
  if (amount === null) throw new Error('La cantidad ingresada no es válida.');
  if (Number.isNaN(amount)) throw new Error('La cantidad excede la precisión admitida.');
  if (amount === 0 && !allowZero) throw new Error('Escribe una cantidad mayor que cero.');
  if (amount > Number.MAX_SAFE_INTEGER) throw new Error('La cantidad excede el rango permitido.');
  const before = item.main ?? 0;
  if (op === 'opening') return { before: 0, delta: amount, after: amount, captured: amount };
  if (op === 'count') return { before, delta: amount - before, after: amount, captured: amount };
  const delta = op === 'receipt' ? amount : -amount;
  if (before + delta < 0) throw new Error('No hay existencia suficiente para esta merma.');
  return { before, delta, after: before + delta, captured: amount };
}
function operationEditor(data) {
  const item = current(data);
  if (!item) { inv.screen = 'list'; return listScreen(data, []); }
  const op = inv.op, o = OPS[op], f = inv.form;
  const box = h('div', { class: 'inv-preview-slot' });
  const confirm = h('button', { type: 'button', class: 'primary' }, 'Confirmar');
  const equivalence = h('p', { class: 'caption' });
  const draw = () => {
    const unit = item.units.find(u => u.active && u.code === f.unit);
    equivalence.textContent = unit ? `1 ${unit.name} = ${format(unit.atomic, item.dimension)} · captura presentaciones enteras` : '';
    let result = null, error = '';
    try { result = preview(item, op, f); } catch (e) { error = e.message; }
    confirm.disabled = !result;
    box.replaceChildren(!result ? '' : h('div', { class: 'inv-preview', 'aria-label': 'Vista previa del movimiento' },
      h('strong', { text: 'Vista previa' }),
      h('div', { class: 'inv-preview-grid' },
        h('div', {}, h('small', { text: op === 'count' ? 'Existencia registrada' : 'Existencia actual' }), h('b', { text: op === 'opening' ? 'Sin saldo inicial' : format(result.before, item.dimension) })),
        h('div', {}, h('small', { text: o.movement }), h('b', { text: op === 'count' ? format(result.captured, item.dimension) : signed(result.delta, item.dimension) })),
        h('div', {}, h('small', { text: 'Existencia resultante' }), h('b', { class: 'strong', text: format(result.after, item.dimension) }))),
      op === 'count' ? h('div', { class: 'inv-difference' }, h('small', { text: 'Diferencia con la existencia registrada' }), h('b', { text: signed(result.delta, item.dimension) })) : null,
      h('p', { class: 'caption', text: 'Revisa los valores antes de confirmar.' })),
      error ? h('p', { class: 'error', role: 'status', text: error }) : '');
  };
  const bind = key => e => { f[key] = e.target.value; draw(); };
  confirm.addEventListener('click', async () => {
    let result;
    try { result = preview(item, op, f); } catch (e) { say(e.message); return redraw(); }
    const text = String(f.quantity).trim().replace(',', '.');
    if (!/^\d{1,6}(\.\d{1,3})?$/.test(text)) { say('Escribe la cantidad con hasta 6 enteros y 3 decimales.'); return redraw(); }
    confirm.disabled = true;
    try {
      await command(item.branchId, o.kind, { itemId: item.id, locationId: data.locations.find(l => l.branchId === item.branchId)?.id, quantity: text, unitCode: f.unit,
        ...(op === 'opening' || op === 'receipt') && f.reference.trim() ? { reference: f.reference.trim() } : {},
        ...(op === 'waste' ? { reason: f.waste } : op === 'count' ? { reason: f.adjust } : {}),
        ...(op !== 'opening' && f.note.trim() ? { note: f.note.trim() } : {}) });
      const label = { opening: 'Saldo inicial enviado', receipt: 'Entrada enviada', waste: 'Merma enviada', count: 'Conteo físico enviado' }[op];
      say(op === 'count' && result.delta === 0 ? 'El conteo coincide con la existencia registrada. La caja no realizará ningún movimiento.'
        : `${label} a la caja: ${op === 'count' ? format(result.captured, item.dimension) : signed(result.delta, item.dimension)}. Existencia resultante: ${format(result.after, item.dimension)} cuando la caja lo aplique.`
          + (f.reference.trim() && (op === 'opening' || op === 'receipt') ? ` Referencia: ${f.reference.trim()}.` : ''), true);
      inv.screen = 'detail'; inv.op = '';
    } catch (error) { say(error.message); }
    redraw();
  });
  const node = h('div', { class: 'catalog-editor inv-operation' },
    h('h2', { class: 'detail-title', text: o.title }),
    h('p', { class: 'muted', text: `${item.code} · ${item.name}` }),
    h('p', { class: 'inv-current', text: item.main === null ? 'Existencia actual: sin saldo inicial' : `Existencia actual: ${format(item.main, item.dimension)}` }),
    h('p', { class: 'caption inv-help', text: o.help }),
    h('div', { class: 'catalog-two' },
      h('label', { class: 'label' }, o.label, h('input', { maxlength: '40', inputmode: 'decimal', value: f.quantity, 'aria-label': o.label, oninput: bind('quantity') })),
      h('label', { class: 'label' }, 'Unidad o presentación', h('select', { 'aria-label': 'Unidad o presentación del movimiento', onchange: bind('unit') },
        unitChoices(item).map(u => h('option', { value: u.code, text: u.label, selected: u.code === f.unit }))))),
    equivalence, box,
    op === 'opening' || op === 'receipt' ? h('label', { class: 'label' }, 'Referencia / factura / nota (opcional)',
      h('input', { maxlength: '160', value: f.reference, title: 'Por ejemplo: número de factura, nota o documento. Puedes dejarlo vacío.', 'aria-label': 'Referencia, factura o nota opcional', oninput: bind('reference') })) : null,
    op === 'waste' ? h('label', { class: 'label' }, 'Motivo de merma', h('select', { onchange: bind('waste') }, WASTE.map(([v, l]) => h('option', { value: v, text: l, selected: f.waste === v })))) : null,
    op === 'count' ? h('label', { class: 'label' }, 'Motivo de ajuste', h('select', { onchange: bind('adjust') }, ADJUST.map(([v, l]) => h('option', { value: v, text: l, selected: f.adjust === v })))) : null,
    op !== 'opening' ? h('label', { class: 'label' }, 'Observación opcional · obligatoria cuando el motivo es Otro',
      h('textarea', { maxlength: '500', rows: '3', 'aria-label': 'Observación', oninput: bind('note') }, f.note)) : null,
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: back }, 'Cancelar'), confirm));
  draw();
  return node;
}

// ---------- Presentación ----------
function unitEditor(data) {
  const item = current(data), f = inv.form;
  const bind = key => e => { f[key] = e.target.value; };
  return h('div', { class: 'catalog-editor' },
    h('h2', { class: 'detail-title', text: 'Agregar presentación' }),
    h('p', { class: 'muted', text: `${item.code} · ${item.name}` }),
    h('label', { class: 'label' }, 'Código', h('input', { maxlength: '80', value: f.code, 'aria-label': 'Código de presentación', oninput: bind('code') })),
    h('label', { class: 'label' }, 'Nombre', h('input', { maxlength: '100', value: f.name, 'aria-label': 'Nombre de presentación', oninput: bind('name') })),
    h('label', { class: 'label' }, `Cantidad por presentación completa (${captureUnit(item.dimension)})`, h('input', { maxlength: '20', inputmode: 'decimal', value: f.factor, 'aria-label': 'Cantidad por presentación completa', oninput: bind('factor') })),
    h('div', { class: 'toolbar' },
      h('button', { type: 'button', class: 'secondary', onclick: back }, 'Cancelar'),
      h('button', { type: 'button', class: 'primary', onclick: async () => {
        try {
          await command(item.branchId, 'inventory.unit.add', { itemId: item.id, code: f.code.trim(), name: f.name.trim(), quantity: String(f.factor).trim() });
          inv.screen = 'detail'; say('Presentación enviada a la caja.', true);
        } catch (error) { say(error.message); }
        redraw();
      } }, 'Agregar')));
}

// ---------- Asociar producto ----------
function linkEditor(data) {
  const item = current(data), f = inv.form;
  const PAGE_PRODUCTS = 50;
  const text = plain(inv.productSearch.trim());
  const products = data.products.filter(p => p.branchId === item.branchId && (!text || plain(`${p.name} ${p.sku}`).includes(text)));
  const page = products.slice(inv.productPage * PAGE_PRODUCTS, (inv.productPage + 1) * PAGE_PRODUCTS);
  const save = h('button', { type: 'button', class: 'primary' }, 'Asociar');
  const valid = () => { const a = atomicOf(String(f.quantity).trim(), item.dimension === 'COUNT' ? 1 : 1000); return !!inv.product && a !== null && !Number.isNaN(a) && a > 0; };
  const refresh = () => { save.disabled = !valid(); };
  save.addEventListener('click', async () => {
    try {
      await command(item.branchId, 'inventory.link', { itemId: item.id, productId: inv.product, quantity: String(f.quantity).trim() });
      inv.screen = 'detail';
      say('Producto asociado (se aplica cuando la caja se conecte). Descontará existencias al confirmar la venta cuando el consumo automático esté ACTIVO. También requiere que el producto controle inventario. Esta asociación no activa el consumo automático.', true);
    } catch (error) { say(error.message); }
    redraw();
  });
  const search = h('input', { maxlength: '160', value: inv.productSearch, 'aria-label': 'Buscar producto', oninput: e => { inv.productSearch = e.target.value; },
    onkeydown: e => { if (e.key === 'Enter') { inv.productPage = 0; inv.product = null; redraw(); } } });
  const node = h('div', { class: 'catalog-editor' },
    h('h2', { class: 'detail-title', text: 'Asociar producto' }),
    h('p', { text: association(item) }),
    h('p', { class: 'caption', text: 'La cantidad por unidad vendida se descuenta únicamente si el consumo automático está activo y se cumplen sus reglas. Guardar una asociación no cambia ese estado ni descuenta existencias.' }),
    h('div', { class: 'inv-search' }, search, h('button', { type: 'button', class: 'secondary', onclick: () => { inv.productPage = 0; inv.product = null; redraw(); } }, 'Buscar')),
    page.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Productos para asociar' },
      h('thead', {}, h('tr', {}, ['Nombre', 'SKU', 'Estado', 'Asociación'].map(c => h('th', { text: c })))),
      h('tbody', {}, page.map(p => { const tr = h('tr', { class: 'click' + (inv.product === p.id ? ' selected' : '') + (p.active ? '' : ' off'), tabindex: '0',
        onclick: () => { inv.product = p.id; for (const r of tr.parentNode.children) r.classList.toggle('selected', r === tr); refresh(); } },
        h('td', { 'data-label': 'Nombre', text: p.name }), h('td', { 'data-label': 'SKU', text: p.sku || '—' }), h('td', { 'data-label': 'Estado', text: p.active ? 'Activo' : 'Inactivo' }),
        h('td', { 'data-label': 'Asociación', text: p.linkedItemId ? (data.items.find(i => i.id === p.linkedItemId)?.name ?? 'Otro artículo') : '—' }));
        return tr; })))) : h('p', { class: 'catalog-empty', text: 'No hay productos que coincidan con la búsqueda.' }),
    h('div', { class: 'inv-pager' }, h('p', { class: 'catalog-page', text: `Página ${inv.productPage + 1} · ${products.length} productos` }),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'secondary', disabled: inv.productPage === 0, onclick: () => { inv.productPage--; redraw(); } }, 'Anterior'),
        h('button', { type: 'button', class: 'secondary', disabled: (inv.productPage + 1) * PAGE_PRODUCTS >= products.length, onclick: () => { inv.productPage++; redraw(); } }, 'Siguiente'))),
    h('label', { class: 'label' }, `Cantidad por unidad vendida (${captureUnit(item.dimension)})`,
      h('input', { maxlength: '20', inputmode: 'decimal', value: f.quantity, 'aria-label': 'Cantidad por unidad vendida', oninput: e => { f.quantity = e.target.value; refresh(); } })),
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: back }, 'Cancelar'), save));
  refresh();
  return node;
}

// ---------- Costo de referencia (sólo en la web; con las recetas da la ganancia) ----------
function costDialog(item, measures) {
  const units = [...measures.filter(u => u.dimension === item.dimension).map(u => ({ label: u.symbol, atomic: u.atomic })),
    ...item.units.filter(u => u.active).map(u => ({ label: u.name, atomic: u.atomic }))].filter(u => u.atomic > 0);
  dialog(`Costo de ${item.name}`, body => {
    body.append(h('p', { class: 'muted small wide', text: 'Precio de compra de referencia. Sólo se usa para estimar el costo de cada pan; no cambia el inventario.' }),
      h('label', { class: 'field' }, 'Precio (MXN)', h('input', { name: 'price', inputmode: 'decimal', value: item.cost ? pesos(item.cost.cents) : '' })),
      h('label', { class: 'field' }, 'Por', h('select', { name: 'unit' }, units.map((u, i) => h('option', { value: String(i), text: u.label,
        selected: item.cost ? u.label === item.cost.unit : u.label === 'kg' || u.label === 'L' || u.label === 'pieza' })))));
  }, { onSubmit: async form => {
    const text = String(form.elements.price.value).trim().replace(',', '.');
    if (!/^\d{1,7}(\.\d{1,2})?$/.test(text)) throw new Error('Escribe el precio, por ejemplo 28.50.');
    const unit = units[Number(form.elements.unit.value)];
    await command(item.branchId, 'inventory.cost', { itemId: item.id, costCents: Math.round(Number(text) * 100), perAtomic: unit.atomic, unitLabel: unit.label });
    say('Costo enviado a la caja.', true); redraw();
  } });
}
