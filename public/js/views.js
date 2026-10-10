// Pantallas de Casa Miga en línea. Cada una pide sus datos a /api/web/<op> y dibuja con h() (sin HTML en texto).
import { api, at, can, card, command, dialog, field, fmt, h, METHOD, pesos, quantity, range, render, ROLE, state, table, time, toast } from './app.js';
import { ICON } from './icons.js';
import { tuCaja, vender } from './pos.js';
import { ventas } from './sales.js';
import { reportsView, setReportTab } from './reports.js';
import { catalogo } from './catalog.js';

const branchName = id => state.branches.find(b => b.id === id)?.name ?? '';
const multi = () => !state.branch && state.branches.length > 1;
const uuid = () => crypto.randomUUID();
const tabsBar = (tabs, active, onpick) => h('div', { class: 'tabs', role: 'tablist' }, tabs.map(([id, label]) =>
  h('button', { type: 'button', class: 'chip', role: 'tab', 'aria-pressed': String(id === active), 'aria-selected': String(id === active), onclick: () => onpick(id) }, label)));
const kpi = (label, value, detail, cls) => h('article', { class: 'card kpi' }, h('span', { text: label }), h('strong', { class: cls, text: value }), h('small', { text: detail ?? '' }));
// Fotos de los panes en todo el panel (como en la app): un índice del catálogo por identificador y por nombre.
let photoIndex = null;
window.addEventListener('cm-branch', () => { photoIndex = null; });
const plain = text => String(text ?? '').normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase().trim();
function indexPhotos(products) {
  photoIndex = new Map();
  for (const p of products) if (p.photo) { photoIndex.set(p.id, p); photoIndex.set(`${p.branchId}/${plain(p.name)}`, p); if (!photoIndex.has(plain(p.name))) photoIndex.set(plain(p.name), p); }
}
async function loadPhotos() {
  if (photoIndex) return;
  try { indexPhotos(can('catalog.view') ? (await api('catalog', { branch: state.branch })).products : []); } catch { photoIndex = new Map(); }
}
/** Miniatura del pan por identificador o, si no hay, por nombre (ventas viejas guardan el nombre). */
function pic({ id, name, branchId } = {}, size = 'sm') {
  const p = photoIndex?.get(id) ?? photoIndex?.get(`${branchId}/${plain(name)}`) ?? photoIndex?.get(plain(name));
  return h('span', { class: `pic ${size}` }, p ? h('img', { src: `/api/web/photo?product=${p.id}&branch=${p.branchId}&v=${p.photo.sha}`, alt: '', loading: 'lazy' })
    : h('span', { text: String(name ?? '?').slice(0, 1) }));
}
const withPic = (item, ...content) => h('span', { class: 'with-pic' }, pic(item), h('span', { class: 'with-pic-text' }, ...content));
const diffCell = d => d === null ? h('span', { class: 'pill info', text: 'Abierta' }) : h('span', { class: d < 0 ? 'short' : d > 0 ? 'over' : 'even', text: d === 0 ? 'Cuadra' : fmt(d) });

// ---------- Cortes ----------
function sessionsTable(sessions) {
  return table([
    { label: 'Caja', value: s => [s.terminal, multi() ? h('small', { text: branchName(s.branchId) }) : null] },
    { label: 'Turno', value: s => [at(s.openedAt), h('small', { text: s.closedAt ? `Cerró ${at(s.closedAt)}` : 'En curso' })] },
    { label: 'Fondo', num: true, value: s => fmt(s.opening) },
    { label: 'Esperado', num: true, value: s => [fmt(s.expected), (s.movements || s.cashRefunds) ? h('small', { text: [s.movements ? `Mov. ${fmt(s.movements)}` : '', s.cashRefunds ? `Dev. −${fmt(s.cashRefunds)}` : ''].filter(Boolean).join(' · ') }) : null] },
    { label: 'Contado', num: true, value: s => s.counted === null ? '—' : fmt(s.counted) },
    { label: 'Diferencia', num: true, value: s => [diffCell(s.difference), s.note ? h('small', { text: `«${s.note}»` }) : null] },
  ], sessions, { empty: 'Sin cortes en el periodo.' });
}
function terminalsList(terminals) {
  if (!terminals.length) return h('p', { class: 'empty', text: 'Ninguna caja registrada. Regístrala desde la pantalla Nube de la app.' });
  return h('ul', { class: 'rows' }, terminals.map(t => {
    const recent = t.lastSeenAt && Date.now() - new Date(t.lastSeenAt).getTime() < 30 * 60000;
    return h('li', {}, h('div', {}, h('strong', { text: t.name }), t.web ? h('span', { class: 'pill info', text: 'Caja web', style: { marginLeft: '6px' } }) : null, h('small', { class: 'muted', text: ' ' + branchName(t.branchId) })),
      h('span', { class: 'pill ' + (t.revoked ? 'bad' : recent ? 'ok' : ''), text: t.revoked ? 'Dada de baja' : recent ? 'Conectada' : 'Sin conexión reciente' }),
      h('span', { class: 'muted small', text: t.lastSeenAt ? at(t.lastSeenAt) : 'Nunca' }));
  }));
}

// ---------- Reportes ----------
// Reportes como la caja (reports.js); Ganancia es propia de la web y usa las fechas de esa pantalla.
async function reportes() {
  await loadPhotos();
  return reportsView({ profit: ganancia, canProfit: can('recipe.view') });
}

// ---------- Caja ----------
async function caja(ctx) {
  const parts = [];
  if (can('pos.use')) parts.push(await tuCaja(ctx));
  if (can('sales.view')) parts.push(await cajaPanel());
  return h('div', { style: { display: 'grid', gap: '16px' } }, parts);
}
async function cajaPanel() {
  const data = await api('cash', range());
  const kinds = { EXPENSE: ['Gasto', 'warn'], WITHDRAWAL: ['Retiro', 'warn'], DEPOSIT: ['Entrada', 'info'] };
  const categories = { SUPPLIES: 'Insumos', GAS: 'Gas', CLEANING: 'Limpieza', TRANSPORT: 'Transporte', MAINTENANCE: 'Mantenimiento', OTHER: 'Otro' };
  const c = data.cash; // totales calculados en la nube: las listas de abajo se recortan
  return h('div', { style: { display: 'grid', gap: '16px' } },
    h('section', { class: 'kpis' },
      kpi('Cortes', String(c.sessions), `${c.open} abiertos`),
      kpi('Gastos', fmt(c.expenses.amount), `${c.expenses.count} movimientos`),
      kpi('Retiros', fmt(c.withdrawals.amount), `${c.withdrawals.count} · efectivo a resguardo`), kpi('Entradas', fmt(c.deposits.amount), `${c.deposits.count} · cambio o fondo extra`)),
    card(h('div', {}, h('h2', { text: 'Cortes de caja de la sucursal', style: { margin: 0 } }), h('span', { class: 'caption', text: 'Sólo lectura · Todas las cajas (escritorio y web) en el periodo elegido' })), sessionsTable(data.sessions)),
    card('Gastos, retiros y entradas', table([
      { label: 'Fecha', value: m => [at(m.at), multi() ? h('small', { text: m.branch }) : null] },
      { label: 'Tipo', value: m => h('span', { class: 'pill ' + kinds[m.kind][1], text: kinds[m.kind][0] }) },
      { label: 'Detalle', value: m => [m.category ? categories[m.category] : '', m.note ? (m.category ? ' · ' : '') + m.note : ''].join('') || '—' },
      { label: 'Quién', value: m => m.user },
      { label: 'Importe', num: true, value: m => h('b', { text: (m.kind === 'DEPOSIT' ? '+' : '−') + fmt(m.amount) }) },
    ], data.movements, { empty: 'Sin gastos, retiros ni entradas en el periodo.' })));
}

// ---------- Charolas ----------
let trayTab = 'route';
async function charolas(ctx) {
  await loadPhotos();
  const tabs = tabsBar([['route', 'Ruta'], ['analysis', 'Análisis'], ['stores', 'Tiendas'], ['deliveries', 'Entregas'], ['payments', 'Cobros']], trayTab, id => { trayTab = id; render(); });
  if (trayTab === 'route') return h('div', { style: { display: 'grid', gap: '16px' } }, tabs, await ruta());
  if (trayTab === 'analysis') return h('div', { style: { display: 'grid', gap: '16px' } }, tabs, await analisisCharolas());
  const data = await api('consignment', range());
  const storeName = new Map(data.stores.map(s => [s.id, s.name]));
  const manage = can('consignment.manage');
  let body;
  if (trayTab === 'stores') {
    body = [manage ? h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'primary', onclick: () => storeDialog(null, ctx.branchId()) }, '+ Nueva tienda')) : null,
      data.stores.length ? h('section', { class: 'grid-3' }, data.stores.map(s => card(
        h('div', { style: { display: 'grid' } }, h('h2', { text: s.name, style: { margin: 0 } }), h('span', { class: 'muted small', text: [s.contact, s.phone].filter(Boolean).join(' · ') || 'Sin contacto' })),
        h('div', { class: 'toolbar', style: { justifyContent: 'space-between', margin: '4px 0 10px' } },
          h('span', { class: 'pill ' + (s.active ? 'ok' : ''), text: s.active ? 'Activa' : 'Inactiva' }),
          h('strong', { class: s.balance > 0 ? 'over' : 'even', text: s.balance > 0 ? `Debe ${fmt(s.balance)}` : 'Al corriente' })),
        h('ul', { class: 'rows' },
          h('li', {}, h('span', { text: 'Entregadas en el periodo' }), h('span'), h('b', { class: 'num', text: `${s.delivered} pz` })),
          h('li', {}, h('span', { text: 'Vendidas / regresaron' }), h('span'), h('b', { class: 'num', text: `${s.sold} / ${s.returned}` })),
          h('li', {}, h('span', { text: 'Cobrado / pagado en el periodo' }), h('span'), h('b', { class: 'num', text: `${fmt(s.periodCharged)} / ${fmt(s.periodPaid)}` })),
          h('li', {}, h('span', { text: 'Charolas por recoger' }), h('span'), h('b', { class: 'num', text: String(s.pending) }))),
        h('h3', { text: 'Charola habitual', style: { marginTop: '12px' } }),
        s.template.length ? h('p', { class: 'small', text: s.template.map(t => `${t.quantity} ${t.name}`).join(' · ') }) : h('p', { class: 'empty', text: 'Sin charola habitual.' }),
        manage ? h('div', { class: 'toolbar', style: { marginTop: '12px' } },
          h('button', { type: 'button', onclick: () => storeDialog(s, s.branchId) }, 'Editar'),
          h('button', { type: 'button', onclick: () => templateDialog(s) }, 'Charola habitual')) : null))) : h('p', { class: 'empty', text: 'Sin tiendas.' })];
  } else if (trayTab === 'deliveries') {
    const label = { pending: ['Por recoger', 'warn'], collected: ['Recogida', 'ok'], voided: ['Anulada', 'bad'] };
    body = [card('Charolas entregadas en el periodo', table([
      { label: 'Entrega', value: d => at(d.at) }, { label: 'Tienda', value: d => storeName.get(d.store) ?? '—' },
      { label: 'Estado', value: d => h('span', { class: 'pill ' + label[d.status][1], text: label[d.status][0] }) },
      { label: 'Piezas', num: true, value: d => String(d.pieces) }, { label: 'Vendidas / regreso', num: true, value: d => d.status === 'collected' ? `${d.sold} / ${d.returned}` : '—' },
      { label: 'Cobrado', num: true, value: d => d.status === 'collected' ? fmt(d.charged) : '—' },
      { label: 'Contenido', span: true, value: d => h('div', { class: 'pieces' }, d.items.map(i => h('span', { class: 'piece' }, pic({ id: i.productId, name: i.name }, 'xs'), `${i.quantity} ${i.name}`))) },
    ], data.deliveries, { empty: 'Sin entregas en el periodo.' }))];
  } else {
    body = [card('Cobros en el periodo', table([
      { label: 'Fecha', value: p => at(p.at) }, { label: 'Tienda', value: p => storeName.get(p.store) ?? '—' },
      { label: 'Forma', value: p => METHOD[p.method] ?? p.method }, { label: 'Nota', value: p => p.note || '—' }, { label: 'Importe', num: true, value: p => h('b', { text: fmt(p.amount) }) },
    ], data.payments, { empty: 'Sin cobros en el periodo.' }))];
  }
  return h('div', { style: { display: 'grid', gap: '16px' } }, tabs, ...body);
}
// ---------- Ruta del repartidor ----------
const SENT = { 'consignment.deliver': 'Entrega', 'consignment.collect': 'Recolección', 'consignment.payment': 'Cobro', 'consignment.void': 'Anulación' };
async function ruta() {
  const data = await api('route', { branch: state.branch });
  const manage = can('consignment.manage');
  if (!data.stores.length) return h('p', { class: 'empty', text: 'Sin tiendas activas.' });
  return h('div', { style: { display: 'grid', gap: '10px' } },
    h('p', { class: 'muted small', text: 'Lo que registres aquí se manda a la caja; aparece como hecho cuando la caja lo aplica (se sincroniza cada 5 segundos).' }),
    h('section', { class: 'route' }, data.stores.map(store => h('article', { class: 'card' },
      h('div', { class: 'head' },
        h('div', {}, h('h2', { text: store.name }), h('span', { class: 'muted small', text: [store.contact, store.address].filter(Boolean).join(' · ') || 'Sin datos de contacto' }),
          store.phone ? h('a', { href: `tel:${store.phone.replace(/[^\d+]/g, '')}`, class: 'small', style: { display: 'block' } }, store.phone) : null),
        h('strong', { class: store.balance > 0 ? 'over' : 'even', text: store.balance > 0 ? `Debe ${fmt(store.balance)}` : 'Al corriente' })),
      store.lastDelivery ? h('span', { class: 'muted small', text: `Última entrega: ${at(store.lastDelivery)}` }) : null,
      store.atStore.map(tray => h('div', { class: 'tray' },
        h('header', {}, h('strong', { text: `Charola del ${at(tray.at)}` }), h('span', { class: 'pill warn', text: `${tray.items.reduce((a, i) => a + i.quantity, 0)} pz` })),
        h('div', { class: 'pieces' }, tray.items.map(i => h('span', { class: 'piece' }, pic({ id: i.productId, name: i.name, branchId: store.branchId }, 'xs'), `${i.quantity} ${i.name}`))),
        manage && !store.sent.some(c => c.payload.deliveryId === tray.id) ? h('button', { type: 'button', class: 'primary', onclick: () => collectDialog(store, tray) }, 'Recoger') : null)),
      store.sent.map(c => h('p', { class: 'sent', text: `${SENT[c.kind] ?? 'Cambio'} enviada ${time(c.at)} · esperando a la caja` })),
      manage ? h('div', { class: 'actions' },
        h('button', { type: 'button', class: store.atStore.length ? '' : 'primary', onclick: () => deliverDialog(store, data.products.filter(p => p.branchId === store.branchId)) }, 'Entregar charola'),
        h('button', { type: 'button', disabled: store.balance <= 0, onclick: () => payDialog(store) }, 'Cobrar')) : null))));
}
function stepper(value, max, onchange, label) {
  const output = h('output', { 'aria-live': 'polite', text: String(value) });
  const set = next => { value = Math.max(0, Math.min(max, next)); output.textContent = String(value); onchange(value); };
  return h('span', { class: 'stepper' },
    h('button', { type: 'button', 'aria-label': `Menos ${label}`, onclick: () => set(value - 1) }, '−'), output,
    h('button', { type: 'button', 'aria-label': `Más ${label}`, onclick: () => set(value + 1) }, '+'));
}
function methodField() {
  return [field('Forma de pago', h('select', { name: 'method' }, Object.entries(METHOD).map(([v, l]) => h('option', { value: v, text: l })))),
    field('Referencia (tarjeta o transferencia)', h('input', { name: 'reference', maxlength: '60' }))];
}
function deliverDialog(store, products) {
  const template = new Map(store.template.map(t => [t.productId, t.quantity]));
  const quantities = new Map(products.map(p => [p.id, template.get(p.id) ?? 0]));
  const summary = h('div', { class: 'totals wide' });
  const refresh = () => {
    const pieces = [...quantities.values()].reduce((a, b) => a + b, 0);
    const value = products.reduce((a, p) => a + p.price * quantities.get(p.id), 0);
    summary.replaceChildren(h('span', { text: `${pieces} piezas` }), h('span', { text: fmt(value) }));
  };
  dialog(`Entregar a ${store.name}`, body => {
    body.append(h('p', { class: 'muted small wide', text: store.template.length ? 'Viene con la charola habitual; ajusta lo que cambie.' : 'Sin charola habitual: agrega las piezas.' }),
      h('div', { class: 'stepper-list wide' }, products.length ? products.map(p => h('div', { class: 'stepper-row' },
        withPic({ id: p.id, name: p.name, branchId: p.branchId }, p.name, h('small', { text: `${fmt(p.price)} c/u` })), stepper(quantities.get(p.id), 999, v => { quantities.set(p.id, v); refresh(); }, p.name)))
        : h('p', { class: 'empty', text: 'Ningún pan tiene precio de charola.' })),
      summary, field('Nota', h('input', { name: 'note', maxlength: '200' }), true));
    refresh();
  }, { submit: 'Registrar entrega', onSubmit: async form => {
    const lines = [...quantities.entries()].filter(([, q]) => q > 0).map(([productId, quantity]) => ({ productId, quantity }));
    if (!lines.length) throw new Error('Agrega al menos una pieza.');
    await command(store.branchId, 'consignment.deliver', { storeId: store.id, lines, note: form.elements.note.value });
    render({ silent: true });
  } });
}
function collectDialog(store, tray) {
  const back = new Map(tray.items.map(i => [i.position, 0]));
  const summary = h('div', { class: 'totals wide' });
  let paidInput;
  const soldTotal = () => tray.items.reduce((a, i) => a + i.price * (i.quantity - back.get(i.position)), 0);
  const refresh = () => {
    const sold = tray.items.reduce((a, i) => a + i.quantity - back.get(i.position), 0);
    summary.replaceChildren(h('span', { text: `${sold} vendidas` }), h('span', { text: `Cargo ${fmt(soldTotal())}` }));
    if (paidInput && !paidInput.dataset.touched) paidInput.value = pesos(soldTotal());
  };
  dialog(`Recoger en ${store.name}`, body => {
    paidInput = h('input', { name: 'paid', inputmode: 'decimal', oninput: e => { e.target.dataset.touched = '1'; } });
    body.append(h('p', { class: 'muted small wide', text: `Charola del ${at(tray.at)}. Marca cuántas piezas regresan; las demás se vendieron.` }),
      h('div', { class: 'stepper-list wide' }, tray.items.map(i => h('div', { class: 'stepper-row' },
        withPic({ id: i.productId, name: i.name, branchId: store.branchId }, i.name, h('small', { text: `Entregadas ${i.quantity} · ${fmt(i.price)} c/u · regresan:` })),
        stepper(0, i.quantity, v => { back.set(i.position, v); refresh(); }, `regresan de ${i.name}`)))),
      summary,
      field('Cobro ahora (MXN)', paidInput), ...methodField(),
      field('Nota', h('input', { name: 'note', maxlength: '200' }), true),
      h('details', { class: 'wide' }, h('summary', { text: 'Anular esta charola' }),
        h('div', { style: { display: 'grid', gap: '8px', marginTop: '8px' } },
          h('input', { name: 'reason', maxlength: '200', placeholder: 'Motivo (por ejemplo, la tienda estaba cerrada)', 'aria-label': 'Motivo de anulación' }),
          h('button', { type: 'button', class: 'danger', onclick: async e => {
            const reason = e.target.closest('form').elements.reason.value.trim();
            if (reason.length < 3) return toast('Escribe el motivo.');
            try { await command(store.branchId, 'consignment.void', { deliveryId: tray.id, reason }); e.target.closest('dialog').close(); e.target.closest('dialog').remove(); render({ silent: true }); }
            catch (error) { toast(error.message); }
          } }, 'Anular charola'))));
    refresh();
  }, { submit: 'Registrar recolección', onSubmit: async form => {
    const f = form.elements;
    const text = String(f.paid.value || '0').trim().replace(',', '.');
    if (!/^\d{1,7}(\.\d{1,2})?$/.test(text)) throw new Error('Escribe el cobro, por ejemplo 150.00 (o 0).');
    await command(store.branchId, 'consignment.collect', { deliveryId: tray.id, returned: [...back.entries()].filter(([, q]) => q > 0).map(([position, quantity]) => ({ position, quantity })),
      paidCents: Math.round(Number(text) * 100), method: f.method.value, reference: f.reference.value, note: f.note.value });
    render({ silent: true });
  } });
}
function payDialog(store) {
  dialog(`Cobrar a ${store.name}`, body => {
    body.append(h('p', { class: 'muted wide', text: `Saldo: ${fmt(store.balance)}` }),
      field('Importe (MXN)', h('input', { name: 'amount', inputmode: 'decimal', value: pesos(store.balance) })), ...methodField(),
      field('Nota', h('input', { name: 'note', maxlength: '200' }), true));
  }, { submit: 'Registrar cobro', onSubmit: async form => {
    const f = form.elements;
    const text = String(f.amount.value).trim().replace(',', '.');
    if (!/^\d{1,7}(\.\d{1,2})?$/.test(text) || Number(text) <= 0) throw new Error('Escribe el importe, por ejemplo 150.00.');
    if (Math.round(Number(text) * 100) > store.balance) throw new Error(`No puede ser mayor que el saldo (${fmt(store.balance)}).`);
    await command(store.branchId, 'consignment.payment', { storeId: store.id, amountCents: Math.round(Number(text) * 100), method: f.method.value, reference: f.reference.value, note: f.note.value });
    render({ silent: true });
  } });
}

function branchPicker(current) {
  if (!state.me.admin || state.branches.length < 2) return null;
  return field('Sucursal', h('select', { name: 'branch' }, state.branches.map(b => h('option', { value: b.id, text: b.name, selected: b.id === current }))), true);
}
function storeDialog(store, branchId) {
  dialog(store ? `Editar ${store.name}` : 'Nueva tienda', body => {
    body.append(store ? '' : branchPicker(branchId) ?? '',
      field('Nombre', h('input', { name: 'name', required: true, maxlength: '100', value: store?.name ?? '' }), true),
      field('Contacto', h('input', { name: 'contact', maxlength: '100', value: store?.contact ?? '' })),
      field('Teléfono', h('input', { name: 'phone', maxlength: '40', inputmode: 'tel', value: store?.phone ?? '' })),
      field('Dirección', h('input', { name: 'address', maxlength: '200', value: store?.address ?? '' }), true),
      store ? h('label', { class: 'check wide' }, h('input', { type: 'checkbox', name: 'active', checked: store.active }), 'Tienda activa (recibe charolas)') : '');
  }, { onSubmit: async form => {
    const f = form.elements;
    await command(store?.branchId ?? f.branch?.value ?? branchId, 'consignment.store.save', { id: store?.id ?? uuid(), name: f.name.value, contact: f.contact.value, phone: f.phone.value, address: f.address.value,
      ...(store ? { active: f.active.checked } : {}) });
  } });
}
async function templateDialog(store) {
  const products = (await api('catalog', { branch: store.branchId })).products.filter(p => p.active && p.consignmentPrice !== null);
  const quantities = new Map(store.template.map(t => [t.productId, t.quantity]));
  dialog(`Charola habitual · ${store.name}`, body => {
    body.append(h('p', { class: 'muted small wide', text: 'Sólo aparecen los panes con precio de charola. Deja en 0 lo que no va.' }),
      h('div', { class: 'wide', style: { display: 'grid', gap: '8px' } }, products.length ? products.map(p => h('label', { class: 'check', style: { justifyContent: 'space-between' } },
        h('span', { text: `${p.name} · ${fmt(p.consignmentPrice)}` }),
        h('input', { type: 'number', name: p.id, min: '0', max: '999', step: '1', value: String(quantities.get(p.id) ?? 0), style: { width: '90px' }, 'aria-label': `Piezas de ${p.name}` })))
        : h('p', { class: 'empty', text: 'Ningún pan tiene precio de charola en esta sucursal.' })));
  }, { onSubmit: async form => {
    const lines = products.map(p => ({ productId: p.id, quantity: Number(form.elements[p.id].value) })).filter(l => l.quantity > 0);
    if (lines.some(l => !Number.isInteger(l.quantity) || l.quantity > 999)) throw new Error('Cantidades de 0 a 999 piezas.');
    await command(store.branchId, 'consignment.template.save', { storeId: store.id, lines });
  } });
}

// ---------- Inventario, recetas y producción ----------
// Lo que se pide desde aquí lo aplica la caja con sus reglas (docs/POS_WEB.md, fase 3); las existencias cambian cuando la caja lo sube.
const WASTE_REASONS = { Damaged: 'Dañado', Expired: 'Caducado', ProductionError: 'Error de producción', Spill: 'Derrame', Other: 'Otro' };
const COUNT_REASONS = { PhysicalCountVariance: 'Diferencia en el conteo físico', DataCorrection: 'Corrección de captura', Other: 'Otro' };
const STOCK_OPS = {
  receive: { title: 'Entrada de inventario', label: 'Entrada', permission: 'inventory.receive', kind: 'inventory.receive', submit: 'Registrar entrada' },
  waste: { title: 'Merma', label: 'Merma', permission: 'inventory.waste', kind: 'inventory.waste', submit: 'Registrar merma' },
  count: { title: 'Conteo físico', label: 'Conteo', permission: 'inventory.adjust', kind: 'inventory.count', submit: 'Guardar conteo' },
};
// Unidades para capturar (sin mg ni µL); si la caja no subió su catálogo de unidades se usan las del sistema.
const BASE_UNITS = [{ code: 'kg', symbol: 'kg', dimension: 'MASS', atomic: 1_000_000 }, { code: 'g', symbol: 'g', dimension: 'MASS', atomic: 1_000 },
  { code: 'L', symbol: 'L', dimension: 'VOLUME', atomic: 1_000_000 }, { code: 'ml', symbol: 'ml', dimension: 'VOLUME', atomic: 1_000 }, { code: 'piece', symbol: 'pieza', dimension: 'COUNT', atomic: 1 }];
const captureUnits = (item, measures) => [
  ...(measures?.length ? measures : BASE_UNITS).filter(u => u.dimension === item.dimension && (u.dimension === 'COUNT' || u.atomic >= 1_000)).sort((a, b) => b.atomic - a.atomic).map(u => ({ code: u.code, label: u.symbol })),
  ...item.units.map(u => ({ code: u.code, label: u.name }))];
const stockOps = () => Object.entries(STOCK_OPS).filter(([, o]) => can(o.permission));

async function inventario() {
  const [data, sent] = await Promise.all([api('inventory', { branch: state.branch }), can('sales.view') ? api('commands', { branch: state.branch }) : []]);
  const kinds = { RAW_MATERIAL: 'Materia prima', FINISHED_GOOD: 'Producto terminado', PACKAGING: 'Empaque', SUPPLY: 'Insumo' };
  const names = new Map(data.items.map(i => [i.id, i]));
  const recent = iso => Date.now() - new Date(iso).getTime() < 2 * 86_400_000;
  const waiting = sent.filter(c => STOCK_KINDS.has(c.kind) && (c.status === 'pending' || (c.status === 'rejected' && recent(c.at))));
  const actionable = stockOps().length > 0 || can('inventory.configure');
  return h('div', { style: { display: 'grid', gap: '16px' } },
    stockOps().length ? h('div', { class: 'toolbar' }, stockOps().map(([op, o]) => h('button', { type: 'button', class: op === 'receive' ? 'primary' : '', onclick: () => stockDialog(op, data) }, o.label))) : null,
    waiting.length ? card('Enviado a la caja', h('p', { class: 'muted small', text: 'La caja lo aplica en cuanto se conecta; entonces cambia la existencia de abajo.', style: { marginBottom: '8px' } }),
      h('ul', { class: 'rows' }, waiting.map(c => { const item = names.get(c.payload.itemId);
        return h('li', {}, h('div', {}, h('strong', { text: `${KIND[c.kind]} · ${item?.name ?? 'Insumo'}` }), h('small', { class: 'muted', text: ` ${stockAmount(c.payload, item, data.measures)} · ${c.by} · ${at(c.at)}` })),
          h('span', { class: 'pill ' + (c.status === 'pending' ? 'warn' : 'bad'), text: c.status === 'pending' ? 'Pendiente' : 'Rechazado' }), h('small', { class: 'muted', text: c.message ?? '' })); }))) : null,
    card('Existencias', table([
      { label: 'Insumo', value: i => [h('strong', { text: i.name }), h('small', { text: i.code })] },
      { label: 'Tipo', value: i => kinds[i.kind] ?? i.kind },
      { label: 'Existencia', num: true, value: i => i.balances.length ? h('b', { text: quantity(i.balances.reduce((a, b) => a + b.atomic, 0), i.dimension, data.measures) }) : h('span', { class: 'muted', text: 'Sin conteo inicial' }) },
      { label: 'Ubicación', value: i => i.balances.map(b => b.location).join(', ') || '—' },
      { label: 'Costo', num: true, value: i => i.cost ? [fmt(i.cost.cents), h('small', { text: `por ${i.cost.unit}` })] : h('span', { class: 'muted', text: 'Sin costo' }) },
      { label: 'Estado', value: i => h('span', { class: 'pill ' + (i.active ? 'ok' : ''), text: i.active ? 'Activo' : 'Inactivo' }) },
    ], data.items, { empty: 'Sin insumos registrados.', onclick: actionable ? i => itemActions(i, data) : null })),
    actionable ? h('p', { class: 'muted small', text: 'Toca un insumo para registrar una entrada, una merma o un conteo' + (can('inventory.configure') ? ', o para capturar su costo (con las recetas se calcula el costo de cada pan en Reportes → Ganancia).' : '.') }) : null,
    card('Movimientos recientes', table([
      { label: 'Fecha', value: o => at(o.at) }, { label: 'Operación', value: o => [o.kind, o.reason ? h('small', { text: o.reason }) : null] },
      { label: 'Estado', value: o => o.state }, { label: 'Quién', value: o => o.user || '—' },
      { label: 'Renglones', span: true, value: o => h('small', { text: o.lines.map(l => `${l.item} ${l.delta > 0 ? '+' : ''}${quantity(l.delta, l.dimension, data.measures)}`).join(' · ') }) },
    ], data.operations, { empty: 'Sin movimientos de inventario.' })));
}
const STOCK_KINDS = new Set(['inventory.receive', 'inventory.waste', 'inventory.count']);
function stockAmount(payload, item, measures) {
  const unit = item ? captureUnits(item, measures).find(u => u.code === payload.unitCode)?.label : null;
  return `${Number(payload.quantity).toLocaleString('es-MX', { maximumFractionDigits: 3 })} ${unit ?? payload.unitCode}`;
}
/** Lo que se puede hacer con un insumo: entrada, merma, conteo o costo, según el rol. */
function itemActions(item, data) {
  const ops = item.active ? stockOps() : [];
  dialog(item.name, (body, close) => {
    body.append(
      h('ul', { class: 'rows wide' }, item.balances.length ? item.balances.map(b => h('li', {}, h('span', { text: b.location }), h('span'), h('b', { class: 'num', text: quantity(b.atomic, item.dimension, data.measures) })))
        : h('li', {}, h('span', { class: 'muted', text: 'Todavía sin conteo inicial.' }), h('span'), h('span'))),
      item.active ? '' : h('p', { class: 'notice wide', text: 'Insumo inactivo: se reactiva en la caja.' }),
      h('div', { class: 'wide toolbar' },
        ops.map(([op, o]) => h('button', { type: 'button', class: op === 'receive' ? 'primary' : '', onclick: () => { close(); stockDialog(op, data, item); } }, o.label)),
        can('inventory.configure') ? h('button', { type: 'button', onclick: () => { close(); costDialog(item, data.measures); } }, 'Costo') : null));
  });
}
/** Entrada, merma o conteo de un insumo. La cantidad va como texto decimal; la caja la convierte y revisa existencia y permisos. */
function stockDialog(op, data, item = null) {
  const o = STOCK_OPS[op];
  const items = data.items.filter(i => i.active);
  if (!items.length) return toast('No hay insumos activos.');
  dialog(o.title, body => {
    const itemSelect = h('select', { name: 'supply', required: true }, items.map(i => h('option', { value: i.id, text: i.name + (multi() ? ` (${branchName(i.branchId)})` : ''), selected: i.id === item?.id })));
    const locationSelect = h('select', { name: 'location' });
    const unitSelect = h('select', { name: 'unit' });
    const hint = h('p', { class: 'muted small wide' });
    const current = () => items.find(i => i.id === itemSelect.value);
    const showStock = () => {
      const it = current(); const balance = it.balances.find(b => b.locationId === locationSelect.value);
      hint.textContent = balance ? `Existencia: ${quantity(balance.atomic, it.dimension, data.measures)}`
        : op === 'count' ? 'Sin saldo todavía: este conteo será el saldo inicial.' : 'Sin saldo todavía: primero el administrador registra el conteo inicial.';
      // El saldo inicial no lleva motivo: sólo se pide cuando ya hay existencia que corregir.
      if (op === 'count') body.querySelector('[name=reason]').closest('label').hidden = !balance;
    };
    const sync = () => {
      const it = current();
      const places = data.locations.filter(l => l.branchId === it.branchId);
      locationSelect.replaceChildren(...places.map(l => h('option', { value: l.id, text: l.name })));
      locationSelect.closest('label').hidden = places.length < 2;
      const preferred = { MASS: 'kg', VOLUME: 'L', COUNT: 'piece' }[it.dimension];
      unitSelect.replaceChildren(...captureUnits(it, data.measures).map(u => h('option', { value: u.code, text: u.label, selected: u.code === preferred })));
      showStock();
    };
    itemSelect.addEventListener('change', sync); locationSelect.addEventListener('change', showStock);
    body.append(field('Insumo', itemSelect, true), field('Ubicación', locationSelect, true),
      field(op === 'count' ? 'Cantidad contada' : 'Cantidad', h('input', { name: 'quantity', required: true, inputmode: 'decimal', autocomplete: 'off', placeholder: op === 'count' ? 'Lo que hay físicamente' : 'Ej. 2.5' })),
      field('Unidad', unitSelect), hint,
      op === 'receive' ? field('Referencia (factura, nota)', h('input', { name: 'reference', maxlength: '200' })) : '',
      op === 'waste' ? field('Motivo', h('select', { name: 'reason' }, Object.entries(WASTE_REASONS).map(([v, l]) => h('option', { value: v, text: l })))) : '',
      op === 'count' ? field('Motivo de la diferencia', h('select', { name: 'reason' }, Object.entries(COUNT_REASONS).map(([v, l]) => h('option', { value: v, text: l })))) : '',
      field(op === 'receive' ? 'Observación (opcional)' : 'Observación (obligatoria con «Otro»)', h('input', { name: 'note', maxlength: '500' }), op !== 'receive'));
    sync();
  }, { submit: o.submit, onSubmit: async form => {
    const f = form.elements;
    const amount = String(f.quantity.value).trim().replace(',', '.');
    if (!/^\d{1,6}(\.\d{1,3})?$/.test(amount) || (op !== 'count' && Number(amount) === 0)) throw new Error(op === 'count' ? 'Escribe lo que contaste, por ejemplo 12.5 (0 si no hay).' : 'Escribe una cantidad mayor que cero, por ejemplo 2.5.');
    if (f.reason?.value === 'Other' && !f.note.value.trim()) throw new Error('Con motivo «Otro» escribe una observación.');
    const it = items.find(i => i.id === f.supply.value);
    await command(it.branchId, o.kind, { itemId: it.id, locationId: f.location.value, quantity: amount, unitCode: f.unit.value,
      ...(f.reference?.value.trim() ? { reference: f.reference.value.trim() } : {}), ...(f.reason ? { reason: f.reason.value } : {}), ...(f.note.value.trim() ? { note: f.note.value.trim() } : {}) });
    render({ silent: true });
  } });
}
function costDialog(item, measures) {
  const units = [...measures.filter(u => u.dimension === item.dimension).map(u => ({ label: u.symbol, atomic: u.atomic })),
    ...item.units.map(u => ({ label: u.name, atomic: u.atomic }))].filter(u => u.atomic > 0);
  dialog(`Costo de ${item.name}`, body => {
    body.append(h('p', { class: 'muted small wide', text: 'Precio de compra de referencia. Sólo se usa para estimar el costo de cada pan; no cambia el inventario.' }),
      field('Precio (MXN)', h('input', { name: 'price', inputmode: 'decimal', value: item.cost ? pesos(item.cost.cents) : '' })),
      field('Por', h('select', { name: 'unit' }, units.map((u, i) => h('option', { value: String(i), text: u.label, selected: item.cost ? u.label === item.cost.unit : u.label === 'kg' || u.label === 'L' || u.label === 'pieza' })))));
  }, { onSubmit: async form => {
    const text = String(form.elements.price.value).trim().replace(',', '.');
    if (!/^\d{1,7}(\.\d{1,2})?$/.test(text)) throw new Error('Escribe el precio, por ejemplo 28.50.');
    const unit = units[Number(form.elements.unit.value)];
    await command(item.branchId, 'inventory.cost', { itemId: item.id, costCents: Math.round(Number(text) * 100), perAtomic: unit.atomic, unitLabel: unit.label });
  } });
}

async function recetas() {
  const [data] = await Promise.all([api('recipes', { branch: state.branch }), loadPhotos()]);
  const lines = list => list.length ? h('ul', { class: 'rows' }, list.map(l => h('li', {}, h('span', { text: l.item ?? l.formula }), h('span'), h('b', { class: 'num', text: quantity(l.atomic, l.dimension, data.measures) })))) : h('p', { class: 'empty', text: 'Sin ingredientes.' });
  return h('div', { style: { display: 'grid', gap: '16px' } },
    card('Preparaciones (fórmulas)', data.formulas.length ? h('section', { class: 'grid-3' }, data.formulas.map(f => h('div', { class: 'card' },
      h('h3', { text: f.name }), h('p', { class: 'muted small', text: f.active ? `${f.code} · versión ${f.active.version} · rinde ${quantity(f.active.yieldAtomic, f.active.yieldDimension, data.measures)}` : f.code }),
      f.active ? lines(f.active.ingredients) : null,
      f.active?.alternatives.map(g => h('p', { class: 'small', text: `${g.name}: ${g.options.map(o => `${o.item} ${quantity(o.atomic, o.dimension, data.measures)}`).join(' o ')}` }))))) : h('p', { class: 'empty', text: 'Sin preparaciones.' })),
    card('Recetas por producto', data.profiles.length ? h('section', { class: 'grid-3' }, data.profiles.map(p => h('div', { class: 'card' },
      withPic({ name: p.product }, h('h3', { text: p.product, style: { margin: 0 } })), h('p', { class: 'muted small', text: p.active ? `Versión ${p.active.version} · por pieza` : '' }),
      p.active ? [lines(p.active.ingredients), p.active.formulas.length ? lines(p.active.formulas) : null] : null))) : h('p', { class: 'empty', text: 'Sin recetas de producto.' })));
}
async function produccion() {
  const recording = can('production.create') && can('production.confirm');
  const [batches, recipes, sent] = await Promise.all([api('production', range()), recording ? api('recipes', { branch: state.branch }) : null,
    recording && can('sales.view') ? api('commands', { branch: state.branch }) : [], loadPhotos()]);
  const status = { CONFIRMED: ['Registrada', 'ok'], DRAFT: ['Borrador', ''], PLANNED: ['Planeada', 'info'], CANCELLED: ['Cancelada', 'bad'] };
  const ready = recipes?.profiles.filter(p => p.activeVersionId) ?? [];
  const byVersion = new Map(ready.map(p => [p.activeVersionId, p]));
  const recent = iso => Date.now() - new Date(iso).getTime() < 2 * 86_400_000;
  const waiting = sent.filter(c => c.kind === 'production.record' && (c.status === 'pending' || (c.status === 'rejected' && recent(c.at))));
  return h('div', { style: { display: 'grid', gap: '16px' } },
    recording ? h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'primary', disabled: !ready.length, onclick: () => productionDialog(ready) }, '+ Registrar producción'),
      ready.length ? null : h('span', { class: 'muted small', text: 'Ningún pan tiene receta activa; se configuran en la caja (Recetas).' })) : null,
    waiting.length ? card('Enviado a la caja', h('ul', { class: 'rows' }, waiting.map(c => h('li', {},
      h('div', {}, h('strong', { text: c.payload.outputs.map(o => `${byVersion.get(o.profileVersionId)?.product ?? 'Pan'}: ${o.actual} pz`).join(' · ') }), h('small', { class: 'muted', text: ` ${c.by} · ${at(c.at)}` })),
      h('span', { class: 'pill ' + (c.status === 'pending' ? 'warn' : 'bad'), text: c.status === 'pending' ? 'Pendiente' : 'Rechazado' }), h('small', { class: 'muted', text: c.message ?? '' }))))) : null,
    h('p', { class: 'notice', text: 'Para saber cuánto producir mañana: Reportes → Producción sugerida.' }),
    card('Producciones del periodo', table([
      { label: 'Fecha', value: b => at(b.at) }, { label: 'Estado', value: b => h('span', { class: 'pill ' + (status[b.status]?.[1] ?? ''), text: status[b.status]?.[0] ?? b.status }) },
      { label: 'Productos', value: b => h('div', { class: 'pieces' }, b.outputs.map(o => h('span', { class: 'piece' }, pic({ name: o.product }, 'xs'), `${o.product}: ${o.actual ?? o.planned}${o.actual !== null && o.actual !== o.planned ? ` de ${o.planned}` : ''} pz`))) },
      { label: 'Quién', value: b => b.by || '—' },
    ], batches, { empty: 'Sin producciones en el periodo.' })));
}
/**
 * Producción ya hecha: panes con receta activa, piezas planeadas y las que salieron. La caja la prepara con esa receta, descuenta
 * los insumos y suma lo que salió; si falta existencia la rechaza y aquí se ve el motivo.
 */
function productionDialog(profiles) {
  dialog('Registrar producción', body => {
    const rows = h('div', { class: 'wide production-rows' });
    const row = () => {
      const planned = h('input', { name: 'planned', type: 'number', min: '1', max: '100000', inputmode: 'numeric', required: true });
      const actual = h('input', { name: 'actual', type: 'number', min: '0', max: '100000', inputmode: 'numeric', required: true });
      planned.addEventListener('input', () => { if (!actual.dataset.touched) actual.value = planned.value; });
      actual.addEventListener('input', () => { actual.dataset.touched = '1'; });
      const line = h('div', { class: 'production-row' },
        field('Pan', h('select', { name: 'profile', required: true }, h('option', { value: '', text: 'Elige un pan' }),
          profiles.map(p => h('option', { value: p.activeVersionId, text: p.product + (multi() ? ` (${branchName(p.branchId)})` : '') })))),
        field('Planeadas', planned), field('Salieron', actual),
        h('button', { type: 'button', class: 'icon ghost', 'aria-label': 'Quitar pan', onclick: () => { if (rows.children.length > 1) line.remove(); } }, '✕'));
      return line;
    };
    body.append(h('p', { class: 'muted small wide', text: 'La caja descuenta los insumos de la receta y suma al inventario las piezas que salieron.' }),
      rows, h('div', { class: 'wide' }, h('button', { type: 'button', onclick: () => { if (rows.children.length < 20) rows.append(row()); } }, '+ Otro pan')),
      field('Notas (opcional)', h('input', { name: 'notes', maxlength: '500' }), true));
    rows.append(row());
  }, { submit: 'Registrar', onSubmit: async form => {
    const whole = (v, min) => /^\d{1,6}$/.test(String(v).trim()) && Number(v) >= min && Number(v) <= 100_000;
    const outputs = [...form.querySelectorAll('.production-row')].map(r => ({ profileVersionId: r.querySelector('[name=profile]').value,
      planned: r.querySelector('[name=planned]').value, actual: r.querySelector('[name=actual]').value }));
    if (outputs.some(o => !o.profileVersionId)) throw new Error('Elige el pan de cada renglón.');
    if (new Set(outputs.map(o => o.profileVersionId)).size !== outputs.length) throw new Error('No repitas el mismo pan.');
    if (outputs.some(o => !whole(o.planned, 1) || !whole(o.actual, 0))) throw new Error('Piezas en enteros: planeadas desde 1, salieron desde 0.');
    const branches = new Set(outputs.map(o => profiles.find(p => p.activeVersionId === o.profileVersionId).branchId));
    if (branches.size > 1) throw new Error('Registra cada sucursal por separado.');
    const notes = form.elements.notes.value.trim();
    await command([...branches][0], 'production.record', { outputs: outputs.map(o => ({ profileVersionId: o.profileVersionId, planned: Number(o.planned), actual: Number(o.actual) })),
      ...(notes ? { notes } : {}) });
    render({ silent: true });
  } });
}

// ---------- Usuarios ----------
async function usuarios(ctx) {
  const people = await api('users', { branch: state.branch });
  return card(h('div', { class: 'toolbar', style: { justifyContent: 'space-between', width: '100%' } }, h('h2', { text: 'Usuarios de las cajas' }),
    h('button', { type: 'button', class: 'primary', onclick: () => userDialog(null, ctx.branchId()) }, '+ Nuevo usuario')),
    h('p', { class: 'muted small', text: 'Los cambios bajan a todas las cajas de la sucursal. Las contraseñas nunca se ven aquí; al restablecer se muestra una temporal una sola vez.', style: { marginBottom: '10px' } }),
    table([
      { label: 'Usuario', value: u => [h('strong', { text: u.username }), multi() ? h('small', { text: u.branch }) : null] },
      { label: 'Nombre', value: u => u.name }, { label: 'Rol', value: u => ROLE[u.role] ?? u.role },
      { label: 'Estado', value: u => h('span', { class: 'pill ' + (!u.active ? 'bad' : u.mustChangePassword ? 'warn' : 'ok'), text: !u.active ? 'Desactivado' : u.mustChangePassword ? 'Contraseña temporal' : 'Activo' }) },
    ], people, { onclick: u => u.role === 'Administrator' ? toast('El administrador se administra en la caja.') : userDialog(u, u.branchId), empty: 'Sin usuarios.' }));
}
function showTemporary(name, password) {
  dialog('Contraseña temporal', body => body.append(h('p', { class: 'wide', text: `Entrégala a ${name}. Al entrar a la caja deberá crear su contraseña; no se vuelve a mostrar.` }),
    h('p', { class: 'secret wide', text: password }), h('p', { class: 'muted small wide', text: 'Funciona en cuanto la caja reciba el cambio (cuando tenga Internet).' })));
}
function userDialog(user, branchId) {
  const roles = h('select', { name: 'role' }, ['Cashier', 'Manager'].map(r => h('option', { value: r, text: ROLE[r], selected: user?.role === r })));
  dialog(user ? user.name : 'Nuevo usuario', body => {
    body.append(user ? '' : branchPicker(branchId) ?? '',
      user ? h('p', { class: 'muted wide', text: `Usuario ${user.username}` }) : field('Usuario', h('input', { name: 'username', required: true, maxlength: '80', autocapitalize: 'none', pattern: '[A-Za-z0-9._-]+' })),
      field('Nombre para mostrar', h('input', { name: 'name', required: true, maxlength: '100', value: user?.name ?? '' }), !user ? false : true), field('Rol', roles),
      user ? h('div', { class: 'wide toolbar' },
        h('button', { type: 'button', class: user.active ? 'danger' : '', onclick: async e => {
          try { await command(user.branchId, 'user.active', { id: user.id, active: !user.active }); e.target.closest('dialog').close(); e.target.closest('dialog').remove(); } catch (error) { toast(error.message); }
        } }, user.active ? 'Desactivar' : 'Reactivar'),
        h('button', { type: 'button', onclick: async e => {
          try { const r = await command(user.branchId, 'user.reset_password', { id: user.id }); e.target.closest('dialog').close(); e.target.closest('dialog').remove(); showTemporary(user.name, r.temporaryPassword); } catch (error) { toast(error.message); }
        } }, 'Restablecer contraseña')) : '');
  }, { onSubmit: async form => {
    const f = form.elements;
    if (user) await command(user.branchId, 'user.update', { id: user.id, displayName: f.name.value, role: f.role.value });
    else { const r = await command(f.branch?.value ?? branchId, 'user.create', { username: f.username.value, displayName: f.name.value, role: f.role.value }); setTimeout(() => showTemporary(f.name.value, r.temporaryPassword)); }
  } });
}

// ---------- Cajas y cambios ----------
const KIND = { 'consignment.deliver': 'Entrega de charola', 'consignment.collect': 'Recolección de charola', 'consignment.payment': 'Cobro de charola', 'consignment.void': 'Anulación de charola',
  'category.save': 'Categoría', 'product.save': 'Producto', 'product.active': 'Activar/desactivar producto', 'user.create': 'Alta de usuario', 'user.update': 'Usuario',
  'user.active': 'Activar/desactivar usuario', 'user.reset_password': 'Restablecer contraseña', 'consignment.store.save': 'Tienda de charolas', 'consignment.template.save': 'Charola habitual',
  'inventory.cost': 'Costo de insumo', 'inventory.receive': 'Entrada de inventario', 'inventory.waste': 'Merma', 'inventory.count': 'Conteo de inventario',
  'production.record': 'Producción', 'product.photo': 'Foto de producto', 'product.photo.remove': 'Quitar foto de producto' };
function describe(c) {
  const p = c.payload;
  if (c.kind === 'product.save') return `${p.name} · ${fmt(p.priceCents)}`;
  if (c.kind === 'product.active' || c.kind === 'user.active') return p.active ? 'Reactivar' : 'Desactivar';
  if (c.kind === 'user.create') return `${p.username} (${ROLE[p.role]})`;
  if (c.kind === 'user.update') return `${p.displayName} (${ROLE[p.role]})`;
  if (c.kind === 'consignment.template.save') return `${p.lines.length} panes`;
  if (c.kind === 'consignment.deliver') return `${p.lines.reduce((a, l) => a + l.quantity, 0)} piezas`;
  if (c.kind === 'consignment.collect') return p.paidCents ? `Cobro ${fmt(p.paidCents)}` : 'Sin cobro';
  if (c.kind === 'consignment.payment') return fmt(p.amountCents);
  if (c.kind === 'consignment.void') return p.reason;
  if (c.kind === 'inventory.cost') return `${fmt(p.costCents)} por ${p.unitLabel}`;
  if (STOCK_KINDS.has(c.kind)) return `${stockAmount(p, null)}${p.reason ? ` · ${WASTE_REASONS[p.reason] ?? COUNT_REASONS[p.reason] ?? p.reason}` : ''}`;
  if (c.kind === 'production.record') return `${p.outputs.reduce((a, o) => a + o.actual, 0)} piezas de ${p.outputs.length} ${p.outputs.length === 1 ? 'pan' : 'panes'}`;
  if (c.kind === 'product.photo') return `${p.edge} × ${p.edge} px`;
  return p.name ?? p.displayName ?? '';
}
async function cajas() {
  const [terminals, commands] = await Promise.all([api('terminals', { branch: state.branch }), api('commands', { branch: state.branch })]);
  const status = { applied: ['Aplicado', 'ok'], pending: ['Pendiente', 'warn'], rejected: ['Rechazado', 'bad'] };
  return h('div', { style: { display: 'grid', gap: '16px' } },
    card('Cajas registradas', terminalsList(terminals)),
    card('Cambios hechos en la web', h('p', { class: 'muted small', text: 'Cada caja los aplica con sus propias reglas cuando se conecta. Si una caja rechaza un cambio, aquí verás el motivo.', style: { marginBottom: '10px' } }),
      table([
        { label: 'Fecha', value: c => [at(c.at), multi() ? h('small', { text: branchName(c.branchId) }) : null] },
        { label: 'Cambio', value: c => [h('strong', { text: KIND[c.kind] ?? c.kind }), h('small', { text: describe(c) })] },
        { label: 'Quién', value: c => c.by },
        { label: 'Estado', value: c => [h('span', { class: 'pill ' + status[c.status][1], text: status[c.status][0] }), c.message ? h('small', { text: c.message }) : c.status === 'pending' && c.terminals > 1 ? h('small', { text: `${c.applied} de ${c.terminals} cajas` }) : null] },
      ], commands, { empty: 'Todavía no hay cambios hechos en la web.' })));
}

let cloudTab = 'terminals';
async function nube() {
  const tabs = tabsBar([['terminals', 'Cajas y cambios'], ['backups', 'Respaldos']].filter(([id]) => id !== 'backups' || can('users.manage')), cloudTab, id => { cloudTab = id; render(); });
  if (cloudTab === 'backups' && !can('users.manage')) cloudTab = 'terminals';
  return h('div', { style: { display: 'grid', gap: '16px' } }, tabs, cloudTab === 'backups' ? await respaldos() : await cajas());
}

// ---------- Ganancia y análisis de charolas ----------
const pct = v => v === null || v === undefined ? '—' : `${Math.round(v * 100)} %`;
async function ganancia(period) {
  const p = await api('profit', period ? { from: period.from, to: period.to, branch: state.branch } : range());
  const missing = p.costs.filter(c => c.cost === null);
  const channel = (title, c, extra) => h('article', { class: 'card kpi' }, h('span', { text: title }), h('strong', { text: c.coveredRevenue ? fmt(c.profit) : '—' }),
    h('small', { text: !c.coveredRevenue ? `Venta ${fmt(c.revenue)} · faltan costos para calcularla`
      : `${c.coveredRevenue < c.revenue ? `Venta con costo ${fmt(c.coveredRevenue)} de ${fmt(c.revenue)}` : `Venta ${fmt(c.revenue)}`} · costo ${fmt(c.cost)} · margen ${pct(c.margin)}` }),
    extra ? h('small', { text: extra }) : null);
  return [
    missing.length ? h('p', { class: 'notice', text: `${missing.length} productos sin costo (sin receta o con insumos sin costo); su venta no entra en la ganancia. Captura costos en Inventario.` }) : null,
    h('section', { class: 'grid-2' },
      channel('Ganancia en mostrador', p.counter, `${p.counter.pieces} piezas vendidas`),
      channel('Ganancia en charolas', p.trays, `${p.trays.pieces} vendidas · ${p.trays.returned} regresaron (costo ${fmt(p.trays.returnedCost)})`)),
    card('Por pan', table([
      { label: 'Pan', value: x => withPic({ id: x.productId, name: x.name, branchId: x.branchId }, h('strong', { text: x.name }), x.unitCost === null ? h('small', { text: 'sin costo' }) : h('small', { text: `costo ${fmt(x.unitCost)} por pieza` })) },
      { label: 'Mostrador', num: true, value: x => [`${x.counterPieces} pz`, h('small', { text: x.perPieceCounter === null ? fmt(x.counterRevenue) : `${fmt(x.counterRevenue)} · gana ${fmt(x.perPieceCounter)}/pz` })] },
      { label: 'Charolas', num: true, value: x => [`${x.trayPieces} pz`, h('small', { text: x.perPieceTray === null ? fmt(x.trayRevenue) : `${fmt(x.trayRevenue)} · gana ${fmt(x.perPieceTray)}/pz` })] },
      { label: 'Regresaron', num: true, value: x => String(x.returned) },
      { label: 'Ganancia', num: true, value: x => x.unitCost === null ? '—' : h('b', { class: (x.counterProfit + x.trayProfit) < 0 ? 'short' : '', text: fmt(x.counterProfit + x.trayProfit) }) },
    ], p.products, { empty: 'Sin ventas en el periodo.' })),
    card('Costo por pieza (desde la receta)', table([
      { label: 'Pan', value: c => c.name }, { label: 'Costo', num: true, value: c => c.cost === null ? '—' : fmt(c.cost) },
      { label: 'Falta', value: c => !c.hasRecipe ? 'Receta' : c.missing.length ? c.missing.join(', ') : 'Completo' },
    ], p.costs, { empty: 'Sin productos.' })),
  ];
}
const often = days => days === null || days === undefined ? 'una sola vez' : days <= 1 ? 'a diario' : `cada ${days} días`;
async function analisisCharolas() {
  const stores = await api('tray-analytics', range());
  return h('div', { style: { display: 'grid', gap: '12px' } },
    h('p', { class: 'muted small', text: 'Del periodo elegido arriba. «Vendido» es lo que no regresó al recoger.' }),
    h('section', { class: 'route' }, stores.map(st => card(
      h('div', { style: { display: 'grid' } }, h('h2', { text: st.name, style: { margin: 0 } }),
        h('span', { class: 'muted small', text: st.deliveries ? `${st.deliveries} ${st.deliveries === 1 ? 'charola' : 'charolas'} · ${often(st.everyDays)} · suele ser ${st.usualDays.join(', ')}` : 'Sin charolas en el periodo' })),
      h('ul', { class: 'rows' },
        h('li', {}, h('span', { text: 'Cobrado' }), h('span'), h('b', { class: 'num', text: fmt(st.revenue) })),
        h('li', {}, h('span', { text: 'Vendido / regresó' }), h('span'), h('b', { class: 'num', text: `${st.sold} / ${st.returned} pz (${pct(st.sellThrough)})` })),
        h('li', {}, h('span', { text: 'Última entrega' }), h('span'), h('b', { class: 'num', text: st.lastDelivery ? at(st.lastDelivery) : '—' }))),
      st.products.length ? [h('h3', { text: 'Lo que más se vende aquí', style: { marginTop: '10px' } }),
        h('ul', { class: 'rows' }, st.products.map(p => h('li', {}, withPic({ id: p.productId, name: p.name, branchId: st.branchId }, h('span', { text: p.name })), h('span', { class: 'muted small', text: `${p.returned} regresaron` }), h('b', { class: 'num', text: `${p.sold} pz` }))))] : null))));
}

// ---------- Respaldos ----------
const size = bytes => bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
async function respaldos() {
  const data = await api('backups', { branch: state.branch });
  const latest = new Map();
  for (const s of data.sets) if (s.kind === 'terminal' && !latest.has(s.terminal)) latest.set(s.terminal, s);
  const cloud = data.sets.find(s => s.kind === 'cloud');
  const age = iso => (Date.now() - new Date(iso).getTime()) / 3_600_000;
  return h('div', { style: { display: 'grid', gap: '16px' } },
    h('p', { class: 'notice', text: 'Cada caja sube su base cada hora con la app abierta (sólo si cambió) y sus fotos una vez. Cada noche se copia la base de la nube. Todo va cifrado; se conserva todo lo de 48 h y uno por día hasta 30 días.' }),
    card('Última copia por caja', data.terminals.length ? h('ul', { class: 'rows' }, data.terminals.map(t => {
      const last = latest.get(t.name);
      const fresh = last && age(last.createdAt) < 2;
      return h('li', {}, h('div', {}, h('strong', { text: t.name }), h('small', { class: 'muted', text: last ? ` ${size(last.bytes)} · ${last.files} fotos` : '' })),
        h('span', { class: 'pill ' + (!last ? 'bad' : fresh ? 'ok' : 'warn'), text: !last ? 'Sin respaldo' : fresh ? 'Al día' : 'Atrasado' }),
        h('span', { class: 'muted small', text: last ? at(last.createdAt) : '—' }));
    })) : h('p', { class: 'empty', text: 'Ninguna caja registrada.' })),
    card('Copia de la base de la nube', h('p', { text: cloud ? `Última: ${at(cloud.createdAt)} · ${size(cloud.bytes)}` : 'Todavía no hay copia (se hace cada noche a las 3:00).' })),
    card('Historial', table([
      { label: 'Fecha', value: s => at(s.createdAt) }, { label: 'Origen', value: s => s.kind === 'cloud' ? 'Base de la nube' : s.terminal ?? 'Caja' },
      { label: 'Tamaño', num: true, value: s => size(s.bytes) }, { label: 'Fotos', num: true, value: s => s.kind === 'cloud' ? '—' : String(s.files) },
      { label: 'Identificador', value: s => h('small', { text: s.id }) },
    ], data.sets, { empty: 'Todavía no hay respaldos.' })),
    card('Restaurar una caja', h('p', { class: 'small', text: 'En la computadora nueva, con Casa Miga instalado y cerrado: CasaMiga.Maintenance restore-cloud <dirección de la nube> <usuario administrador> <carpeta nueva> [identificador]. Pide la contraseña, baja el respaldo más reciente de esa caja (o el indicado), lo descifra, lo verifica y deja una instalación completa en la carpeta nueva.' })));
}

// Mismo orden, grupos, nombres y subtítulos que el riel de la caja de escritorio (MainWindow, rediseño A de QAv1).
export const VIEWS = [
  // Sin actualización automática: no debe redibujarse mientras se arma una venta.
  { id: 'vender', label: 'Venta', title: 'Punto de venta', subtitle: 'Selecciona productos, revisa el carrito y cobra.', hint: 'Ir a Punto de venta', icon: ICON.sale, group: 1, permission: 'pos.use', usesRange: false, manual: true, render: vender },
  // Tu caja (cajero y encargado) y, para encargados, los cortes del periodo. El esperado del turno abierto nunca se muestra antes del conteo ciego.
  { id: 'caja', label: 'Caja', title: 'Tu caja', subtitle: 'Caja web · lo que registras se guarda en la nube al momento.', hint: 'Ir a Caja', icon: ICON.cash, group: 1, permission: ['pos.use', 'sales.view'], usesRange: () => can('sales.view'), manual: true, render: caja },
  { id: 'charolas', title: 'Charolas', subtitle: 'Charolas en tiendas: entregar, recoger y cobrar · Aparte de la caja del mostrador.', hint: 'Ir a Charolas', icon: ICON.tray, group: 1, permission: 'consignment.view', usesRange: true, render: charolas },
  // Fechas propias (como la caja): no usa el periodo de arriba; tampoco se redibuja sola mientras se arma una devolución.
  { id: 'ventas', label: 'Ventas', title: 'Historial de ventas', subtitle: 'Ventas confirmadas · Reimpresión, cancelaciones y devoluciones.', hint: 'Ir a Historial de ventas', icon: ICON.receipt, group: 2, permission: 'sales.view', usesRange: false, manual: true, render: ventas },
  { id: 'reportes', title: 'Reportes', subtitle: 'Ventas, devoluciones y cortes de caja por periodo · Sólo lectura.', hint: 'Ir a Reportes', icon: ICON.chart, group: 2, permission: 'sales.view', usesRange: false, manual: true, render: reportes, tab: setReportTab },
  { id: 'catalogo', title: 'Catálogo', subtitle: 'Categorías y productos · Sin proceso de venta.', hint: 'Ir a Catálogo', icon: ICON.grid, group: 3, permission: 'catalog.view', usesRange: false, manual: true, render: catalogo },
  { id: 'inventario', title: 'Inventario', subtitle: 'Existencias y movimientos del almacén principal · Entradas, mermas y conteos.', hint: 'Ir a Inventario', icon: ICON.box, group: 3, permission: 'inventory.view', usesRange: false, render: inventario },
  { id: 'recetas', title: 'Recetas', subtitle: 'Fórmulas y perfiles productivos versionados.', hint: 'Ir a Recetas', icon: ICON.book, group: 3, permission: 'recipe.view', usesRange: false, render: recetas },
  { id: 'produccion', title: 'Producción', subtitle: 'Registro e historial de producción.', hint: 'Ir a Producción', icon: ICON.chef, group: 3, permission: 'production.view', usesRange: true, render: produccion },
  { id: 'usuarios', title: 'Usuarios', subtitle: 'Crea usuarios y restablece contraseñas olvidadas · Cada acción queda auditada.', hint: 'Ir a Usuarios', icon: ICON.users, group: 3, permission: 'users.manage', usesRange: false, render: usuarios },
  { id: 'nube', title: 'Nube', subtitle: 'Cajas conectadas, cambios hechos en la web y respaldos.', hint: 'Ir a Nube', icon: ICON.cloud, group: 3, permission: 'sales.view', usesRange: false, render: nube, tab: id => { cloudTab = id; } },
];
