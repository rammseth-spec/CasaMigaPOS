// Charolas como la caja de escritorio (ConsignmentView de QAv1): Ruta del día (entregar, recoger con saldo anterior, cobrar, anular),
// Tiendas (datos, charola habitual y estado de cuenta), Precios de charola y Reporte del periodo. Extras de la web: Entregas y Cobros del periodo.
// Lo que se registra se manda a la caja (consignment.*) y aparece como hecho cuando la caja lo aplica.
import { api, at, can, command, dialog, fmt, h, icon, ICON, METHOD, pesos, range, render, state, table, time } from './app.js';

const BREAD = 'M4,5 L16,5 18,8 18,17 2,17 2,8 Z M7,5 L8.5,2 11.5,2 13,5 M10,9 A3,3 0 1 1 9.99,9';
const t = { tab: 'route', action: null, storeId: null, deliver: null, collect: null, selectedStore: undefined, form: null, template: null, prices: null,
  report: null, from: null, to: null, message: '', ok: false };
const say = (message, ok = false) => { t.message = message; t.ok = ok; };
const redraw = () => render({ silent: true });
const plain = text => String(text ?? '').normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
const branchName = id => state.branches.find(b => b.id === id)?.name ?? '';
const multi = () => !state.branch && state.branches.length > 1;
const manage = () => can('consignment.manage');
const photoUrl = (p, branchId) => `/api/web/photo?product=${p.id ?? p.productId}&branch=${branchId}&v=${p.photo}`;
const pieces = n => n === 1 ? '1 pieza' : `${n} piezas`;
/** «Lun 5 oct», como ConsignmentViewModel.Day de la caja. */
const dayName = iso => { const parts = new Intl.DateTimeFormat('es-MX', { timeZone: state.timeZone, weekday: 'short', day: 'numeric', month: 'short' }).formatToParts(new Date(iso));
  const part = type => parts.find(x => x.type === type)?.value.replace(/\./g, '') ?? ''; const s = `${part('weekday')} ${part('day')} ${part('month')}`; return s.charAt(0).toUpperCase() + s.slice(1); };
const localDay = date => date.toLocaleDateString('en-CA', { timeZone: state.timeZone });
/** Como la caja: «500», «500.50», «$1,500.00»; vacío = sin importe (null). Inválido = undefined. */
function money(text) {
  const clean = String(text ?? '').replace(/\$/g, '').replace(/,/g, '').trim();
  if (!clean) return null;
  if (!/^\d{1,7}(\.\d{1,2})?$/.test(clean)) return undefined;
  return Math.round(Number(clean) * 100);
}
const METHODS = [['CASH', 'Efectivo'], ['CARD', 'Tarjeta'], ['TRANSFER', 'Transferencia']];
function methodPicker(f, onchange) {
  return h('div', { class: 'segmented', role: 'radiogroup', 'aria-label': 'Forma de pago' }, METHODS.map(([v, l]) =>
    h('button', { type: 'button', role: 'radio', 'aria-checked': String(f.method === v), onclick: () => { f.method = v; onchange(); } }, l)));
}

export async function charolas() {
  const tabs = [['route', 'Ruta del día'], ['stores', 'Tiendas'], ['prices', 'Precios de charola'], ['report', 'Reporte'], ['deliveries', 'Entregas'], ['payments', 'Cobros']];
  const workspace = t.tab === 'route' && (t.action === 'deliver' || t.action === 'collect');
  const head = workspace ? null : h('div', { class: 'report-tabs' }, tabs.map(([id, label]) =>
    h('button', { type: 'button', class: 'tab', 'aria-pressed': String(t.tab === id), onclick: () => { t.tab = id; t.action = null; say(''); render(); } }, label)));
  let body;
  if (t.tab === 'route') body = await routeTab();
  else if (t.tab === 'stores') body = await storesTab();
  else if (t.tab === 'prices') body = await pricesTab();
  else if (t.tab === 'report') body = await reportTab();
  else body = await periodTab(t.tab);
  return h('div', { class: 'cash-card trays' + (workspace ? ' trays-workspace' : '') }, head, body,
    t.message ? h('p', { class: t.ok ? 'success' : 'error', role: 'status', text: t.message, style: { marginTop: '14px' } }) : null);
}

// ---------- Ruta del día ----------
const SENT = { 'consignment.deliver': 'Entrega', 'consignment.collect': 'Recolección', 'consignment.payment': 'Cobro', 'consignment.void': 'Anulación' };
async function routeTab() {
  const data = await api('route', { branch: state.branch });
  t.route = data;
  if (t.action === 'deliver') return deliverWorkspace(data);
  if (t.action === 'collect') return collectWorkspace(data);
  const stores = data.stores;
  const withTray = stores.filter(s => s.atStore.length).length;
  const owed = stores.reduce((a, s) => a + Math.max(0, s.balance), 0);
  return h('div', {},
    stores.length ? h('p', { class: 'trays-summary', text: `${withTray} de ${stores.length} tiendas con charola · por cobrar ${fmt(owed)}` }) : null,
    h('details', { class: 'trays-help' }, h('summary', { text: '¿Cómo funciona?' }),
      h('ol', {},
        h('li', { text: 'Entregar: deja la charola con su charola habitual o ajusta las piezas.' }),
        h('li', { text: 'Recoger: al día siguiente captura cuántas piezas regresaron. Lo demás se vendió y se carga a la tienda.' }),
        h('li', { text: 'Lo que regresa se aparta para hacer Piedra.' }),
        h('li', { text: 'Cobrar: registra pagos de la tienda. No entra a la caja del mostrador.' })),
      h('p', { class: 'caption', text: 'Lo que registres aquí se manda a la caja; aparece como hecho cuando la caja lo aplica (se sincroniza cada 5 segundos).' })),
    !stores.length ? h('p', { class: 'catalog-empty', text: 'Agrega tus tiendas en la pestaña «Tiendas» y el precio de cada pan en «Precios de charola».' }) :
    h('section', { class: 'trays-route' }, stores.map(store => {
      const tray = store.atStore[0];
      const sentFor = store.sent;
      const collecting = tray && sentFor.some(c => c.payload.deliveryId === tray.id);
      return h('article', { class: 'trays-store' },
        h('div', { class: 'trays-store-head' },
          h('div', {}, h('h3', { text: store.name + (multi() ? ` · ${branchName(store.branchId)}` : '') }),
            [store.contact, store.phone].filter(Boolean).length ? h('span', { class: 'caption', text: [store.contact, store.phone].filter(Boolean).join(' · ') }) : null),
          h('strong', { class: store.balance > 0 ? 'over' : 'even', text: store.balance > 0 ? `Debe ${fmt(store.balance)}` : 'Al corriente' })),
        h('p', { class: 'trays-tray', text: !tray ? 'Sin charola en la tienda'
          : `Charola del ${dayName(tray.at)} · ${tray.items.reduce((a, i) => a + i.quantity, 0)} pzs · ${fmt(tray.items.reduce((a, i) => a + i.price * i.quantity, 0))}` + (store.atStore.length > 1 ? ` (+${store.atStore.length - 1} más)` : '') }),
        sentFor.map(c => h('p', { class: 'sent', text: `${SENT[c.kind] ?? 'Cambio'} enviada ${time(c.at)} · esperando a la caja` })),
        manage() ? h('div', { class: 'toolbar' },
          h('button', { type: 'button', class: tray ? 'secondary' : 'primary', onclick: () => startDeliver(store, data) }, 'Entregar'),
          tray ? h('button', { type: 'button', class: 'primary', disabled: collecting, onclick: () => startCollect(store, tray) }, 'Recoger') : null,
          h('button', { type: 'button', class: 'secondary', disabled: store.balance <= 0, onclick: () => payDialog(store) }, 'Cobrar')) : null);
    })));
}

// ---------- Entregar (pantalla completa, como el Punto de venta) ----------
function startDeliver(store, data) {
  const products = data.products.filter(p => p.branchId === store.branchId);
  if (!products.length) { say('Ningún pan tiene precio de charola. Defínelos en la pestaña Precios.'); return redraw(); }
  const template = new Map(store.template.map(l => [l.productId, l.quantity]));
  t.deliver = { quantities: new Map(products.map(p => [p.id, template.get(p.id) ?? 0])), search: '', category: '', note: '', template };
  t.storeId = store.id; t.action = 'deliver'; say(''); redraw();
}
function deliverWorkspace(data) {
  const store = data.stores.find(s => s.id === t.storeId);
  if (!store) { t.action = null; return routeTab(); }
  const d = t.deliver;
  // Primero los panes de la charola habitual; después el resto.
  const products = data.products.filter(p => p.branchId === store.branchId).sort((a, b) => (d.template.has(a.id) ? 0 : 1) - (d.template.has(b.id) ? 0 : 1));
  const set = (id, q) => { d.quantities.set(id, Math.max(0, Math.min(999, q))); refresh(); };
  const categories = [...new Set(products.map(p => p.category).filter(Boolean))];
  const chips = h('div', { class: 'pos-tabs', role: 'group', 'aria-label': 'Categorías' });
  const grid = h('div', { class: 'tiles' });
  const drawChips = () => chips.replaceChildren(...[['', 'Todos'], ...categories.map(c => [c, c])].map(([v, l]) =>
    h('button', { type: 'button', class: 'tab', 'aria-pressed': String(d.category === v), onclick: () => { d.category = v; drawChips(); drawGrid(); } }, l)));
  const drawGrid = () => {
    const text = plain(d.search.trim());
    const list = products.filter(p => (!d.category || p.category === d.category) && (!text || plain(`${p.name} ${p.sku}`).includes(text)));
    grid.replaceChildren(...(list.length ? list.map(p => h('button', { type: 'button', class: 'tile', 'data-id': p.id, 'aria-label': p.name, onclick: () => set(p.id, d.quantities.get(p.id) + 1) },
      h('span', { class: 'tile-photo' }, p.photo ? h('img', { src: photoUrl(p, store.branchId), alt: '', loading: 'lazy' }) : h('span', { class: 'tile-fallback' }, icon(BREAD), h('small', { text: 'Sin imagen' })),
        h('span', { class: 'count-badge', hidden: !d.quantities.get(p.id), text: String(d.quantities.get(p.id) || '') })),
      h('span', { class: 'tile-name', text: p.name }), h('span', { class: 'tile-price', text: fmt(p.price) })))
      : [h('p', { class: 'tiles-empty', text: 'Ningún pan con precio de charola coincide con la búsqueda.' })]));
  };
  let timer = 0;
  const search = h('input', { type: 'search', class: 'pos-search-input', placeholder: 'Buscar pan o SKU', maxlength: '160', value: d.search, 'aria-label': 'Buscar pan o SKU',
    oninput: e => { d.search = e.target.value; clearTimeout(timer); timer = setTimeout(drawGrid, 200); } });
  const panel = h('aside', { class: 'pos-ticket', 'aria-label': 'Charola' });
  const refresh = () => {
    for (const tile of grid.querySelectorAll('.tile')) { const q = d.quantities.get(tile.dataset.id); const b = tile.querySelector('.count-badge'); b.hidden = !q; b.textContent = q ? String(q) : ''; }
    const lines = products.filter(p => d.quantities.get(p.id) > 0);
    const count = lines.reduce((a, p) => a + d.quantities.get(p.id), 0);
    const value = lines.reduce((a, p) => a + p.price * d.quantities.get(p.id), 0);
    const retail = lines.reduce((a, p) => a + p.retail * d.quantities.get(p.id), 0);
    panel.replaceChildren(
      h('div', { class: 'ticket-head' }, h('div', {}, h('h2', { text: 'Charola' }), h('p', { class: 'caption', text: count ? `${pieces(count)} · ${fmt(value)} a precio de charola` : 'Agrega piezas a la charola.' })),
        h('div', { class: 'toolbar' },
          h('button', { type: 'button', class: 'ghost', disabled: !d.template.size, onclick: () => { for (const p of products) d.quantities.set(p.id, d.template.get(p.id) ?? 0); refresh(); } }, 'Charola habitual'),
          h('button', { type: 'button', class: 'ghost', disabled: !count, onclick: () => { for (const p of products) d.quantities.set(p.id, 0); refresh(); } }, 'Vaciar'))),
      h('div', { class: 'ticket-body' }, lines.length ? h('div', { class: 'cart' }, lines.map(p => h('div', { class: 'cart-line', 'aria-label': p.name },
        h('span', { class: 'line-photo' }, p.photo ? h('img', { src: photoUrl(p, store.branchId), alt: '' }) : null),
        h('span', { class: 'line-body' }, h('strong', { class: 'line-name', text: p.name }), h('span', { class: 'caption', text: `${fmt(p.price)} / pieza` }),
          h('span', { class: 'line-controls' }, h('span', { class: 'stepper-track' },
            h('button', { type: 'button', class: 'qty', 'aria-label': `Menos ${p.name}`, onclick: () => set(p.id, d.quantities.get(p.id) - 1) }, '−'),
            h('b', { class: 'qty-value', text: String(d.quantities.get(p.id)) }),
            h('button', { type: 'button', class: 'qty', 'aria-label': `Más ${p.name}`, onclick: () => set(p.id, d.quantities.get(p.id) + 1) }, '+')))),
        h('b', { class: 'line-total num', text: fmt(p.price * d.quantities.get(p.id)) }))))
        : h('div', { class: 'cart-empty' }, icon(ICON.tray), h('strong', { text: 'Charola vacía' }), h('span', { class: 'muted', text: 'Toca los panes para agregarlos a la charola.' }))),
      h('div', { class: 'ticket-foot' },
        h('div', { class: 'ticket-sum' }, h('span', { text: 'Piezas' }), h('span', { class: 'num', text: String(count) })),
        h('div', { class: 'ticket-total' }, h('span', { text: 'Precio de charola' }), h('strong', { class: 'num', text: fmt(value) })),
        h('p', { class: 'caption', text: `En mostrador valdrían ${fmt(retail)}` }),
        h('label', { class: 'label' }, 'Nota (opcional)', h('input', { maxlength: '200', value: d.note, 'aria-label': 'Nota de la entrega', oninput: e => { d.note = e.target.value; } })),
        h('button', { type: 'button', class: 'primary pay', disabled: !count, onclick: async e => {
          e.target.disabled = true;
          try {
            await command(store.branchId, 'consignment.deliver', { storeId: store.id, lines: lines.map(p => ({ productId: p.id, quantity: d.quantities.get(p.id) })), note: d.note.trim() });
            t.action = null; say(`Charola enviada a la caja para ${store.name}: ${pieces(count)} por ${fmt(value)}.`, true);
          } catch (error) { say(error.message); }
          redraw();
        } }, 'Confirmar entrega')));
  };
  drawChips(); drawGrid(); refresh();
  return h('div', {},
    h('div', { class: 'trays-workspace-head' },
      h('button', { type: 'button', class: 'secondary', onclick: () => { t.action = null; say(''); redraw(); } }, 'Volver a la ruta'),
      h('h2', { class: 'detail-title', text: `Entregar charola a ${store.name}` })),
    h('div', { class: 'pos' }, h('section', { class: 'pos-main' },
      h('div', { class: 'pos-search-row' }, h('label', { class: 'pos-search' }, icon(ICON.search), search)), chips, h('div', { class: 'tiles-scroll' }, grid)), panel));
}

// ---------- Recoger (con saldo anterior, cobro y anulación) ----------
function startCollect(store, tray) {
  const amount = tray.items.reduce((a, i) => a + i.price * i.quantity, 0);
  t.collect = { trayId: tray.id, returned: new Map(tray.items.map(i => [i.position, 0])), paid: pesos(amount), paidEdited: false, method: 'CASH', reference: '', note: '', voidOpen: false, reason: '' };
  t.storeId = store.id; t.action = 'collect'; say(''); redraw();
}
function collectWorkspace(data) {
  const store = data.stores.find(s => s.id === t.storeId);
  const tray = store?.atStore.find(x => x.id === t.collect?.trayId);
  if (!store || !tray) { t.action = null; say('Esa charola ya no está en la tienda.'); return routeTab(); }
  const c = t.collect;
  const products = new Map(data.products.map(p => [p.id, p]));
  const sold = i => i.quantity - c.returned.get(i.position);
  const amount = () => tray.items.reduce((a, i) => a + i.price * sold(i), 0);
  const panel = h('aside', { class: 'pos-ticket trays-charge' });
  const tiles = h('div', { class: 'tiles trays-collect' });
  const setBack = (i, v) => { c.returned.set(i.position, Math.max(0, Math.min(i.quantity, v))); if (!c.paidEdited) c.paid = pesos(amount()); draw(); };
  const draw = () => {
    tiles.replaceChildren(...tray.items.map(i => { const p = products.get(i.productId); const back = c.returned.get(i.position);
      return h('div', { class: 'tile trays-collect-tile' + (back ? ' has-returns' : '') },
        h('button', { type: 'button', class: 'tile-photo', 'aria-label': `Regresó una pieza de ${i.name}`, onclick: () => setBack(i, back + 1) },
          p?.photo ? h('img', { src: photoUrl(p, store.branchId), alt: '' }) : h('span', { class: 'tile-fallback' }, icon(BREAD)),
          h('span', { class: 'count-badge', hidden: !back, text: back === 1 ? 'Regresó 1' : `Regresaron ${back}` })),
        h('span', { class: 'tile-name', text: i.name }),
        h('span', { class: 'caption', text: `Dejadas ${i.quantity} · ${fmt(i.price)}` }),
        h('span', { class: 'stepper-track' },
          h('button', { type: 'button', class: 'qty', 'aria-label': `Menos regresos de ${i.name}`, onclick: () => setBack(i, back - 1) }, '−'),
          h('b', { class: 'qty-value', text: String(back) }),
          h('button', { type: 'button', class: 'qty', 'aria-label': `Más regresos de ${i.name}`, onclick: () => setBack(i, back + 1) }, '+')),
        h('span', { class: 'tile-price', text: `Vendidas ${sold(i)} · ${fmt(i.price * sold(i))}` }));
    }));
    const soldCount = tray.items.reduce((a, i) => a + sold(i), 0), backCount = tray.items.reduce((a, i) => a + c.returned.get(i.position), 0);
    if (c.voidOpen) {
      panel.replaceChildren(h('h2', { text: 'Anular charola' }),
        h('p', { class: 'muted', text: '¿Se capturó mal la entrega? Al anularla no se cobra ni cuenta en los reportes.' }),
        h('label', { class: 'label' }, 'Motivo', h('input', { maxlength: '200', value: c.reason, placeholder: 'Ej.: era otra tienda', 'aria-label': 'Motivo de anulación', oninput: e => { c.reason = e.target.value; } })),
        h('button', { type: 'button', class: 'danger-solid pay', onclick: async e => {
          if (c.reason.trim().length < 3) { say('Escribe el motivo.'); return redraw(); }
          e.target.disabled = true;
          try { await command(store.branchId, 'consignment.void', { deliveryId: tray.id, reason: c.reason.trim() }); t.action = null; say('Anulación enviada a la caja. Al aplicarse no se cobra ni cuenta en los reportes.', true); }
          catch (error) { say(error.message); }
          redraw();
        } }, 'Anular charola'),
        h('button', { type: 'button', class: 'secondary', onclick: () => { c.voidOpen = false; draw(); } }, 'Cancelar y volver al cobro'));
      return;
    }
    const paidCents = money(c.paid);
    panel.replaceChildren(...[h('h2', { text: 'Cobro' }),
      h('div', { class: 'trays-figures' },
        h('div', {}, h('span', { text: 'Vendidas' }), h('b', { text: pieces(soldCount) })),
        h('div', {}, h('span', { text: 'Regresan para Piedra' }), h('b', { text: pieces(backCount) })),
        h('div', {}, h('span', { text: 'Importe de la charola' }), h('b', { text: fmt(amount()) })),
        store.balance > 0 ? h('div', {}, h('span', { text: 'Saldo anterior' }), h('b', { text: fmt(store.balance) })) : null,
        h('div', { class: 'total' }, h('span', { text: 'Total a pagar' }), h('strong', { text: fmt(store.balance + amount()) }))),
      h('label', { class: 'label' }, 'Cobrado ahora', h('input', { inputmode: 'decimal', value: c.paid, 'aria-label': 'Cobrado ahora', oninput: e => { c.paid = e.target.value; c.paidEdited = true; } })),
      h('p', { class: 'caption', text: 'Si la tienda paga después, deja 0 y queda a cuenta.' }),
      h('span', { class: 'label' }, 'Forma de pago'), methodPicker(c, draw),
      c.method !== 'CASH' ? h('label', { class: 'label' }, 'Referencia', h('input', { maxlength: '60', value: c.reference, 'aria-label': 'Referencia del pago', oninput: e => { c.reference = e.target.value; } })) : null,
      h('label', { class: 'label' }, 'Nota (opcional)', h('input', { maxlength: '200', value: c.note, 'aria-label': 'Nota de la recolección', oninput: e => { c.note = e.target.value; } })),
      h('button', { type: 'button', class: 'primary pay', onclick: async e => {
        const paid = money(c.paid);
        if (paid === undefined) { say('Revisa el importe de «Cobrado ahora» (ejemplo: 150.00).'); return redraw(); }
        e.target.disabled = true;
        try {
          await command(store.branchId, 'consignment.collect', { deliveryId: tray.id, returned: tray.items.filter(i => c.returned.get(i.position) > 0).map(i => ({ position: i.position, quantity: c.returned.get(i.position) })),
            paidCents: paid ?? 0, method: c.method, reference: c.method === 'CASH' ? '' : c.reference.trim(), note: c.note.trim() });
          const owes = store.balance + amount() - (paid ?? 0);
          t.action = null;
          say(`Recolección de ${store.name} enviada a la caja: ${soldCount} vendidas por ${fmt(amount())}, ${backCount} regresan para Piedra.` + (paid ? ` Cobrado ${fmt(paid)}.` : '') + (owes > 0 ? ` Quedará debiendo ${fmt(owes)}.` : ' Quedará al corriente.'), true);
        } catch (error) { say(error.message); }
        redraw();
      } }, 'Confirmar recolección'),
      paidCents === undefined ? h('p', { class: 'error', text: 'Revisa el importe (ejemplo: 150.00).' }) : null,
      h('button', { type: 'button', class: 'ghost trays-void-link', onclick: () => { c.voidOpen = true; c.reason = ''; draw(); } }, 'Anular esta charola…')].filter(Boolean));
  };
  draw();
  return h('div', {},
    h('div', { class: 'trays-workspace-head' },
      h('button', { type: 'button', class: 'secondary', onclick: () => { t.action = null; say(''); redraw(); } }, 'Volver a la ruta'),
      h('div', {}, h('h2', { class: 'detail-title', text: `Recoger charola · ${store.name}` }), h('p', { class: 'caption', text: `Dejada el ${dayName(tray.at).toLowerCase()}. Toca la foto por cada pieza que regresó.` }))),
    h('div', { class: 'pos' }, h('section', { class: 'pos-main' }, h('div', { class: 'tiles-scroll' }, tiles)), panel));
}

// ---------- Cobrar ----------
function payDialog(store) {
  const f = { method: 'CASH' };
  dialog(`Cobrar a ${store.name}`, body => {
    const refBox = h('div', { class: 'wide' });
    const drawRef = () => { refBox.replaceChildren(f.method === 'CASH' ? '' : h('label', { class: 'field' }, 'Referencia (opcional)', h('input', { name: 'reference', maxlength: '60' }))); picker.replaceWith(picker = methodPicker(f, () => drawRef())); };
    let picker = methodPicker(f, drawRef);
    body.append(h('p', { class: 'muted wide', text: `Saldo: ${fmt(store.balance)}` }),
      h('label', { class: 'field wide' }, 'Importe', h('input', { name: 'amount', inputmode: 'decimal', value: pesos(store.balance) })),
      h('div', { class: 'wide' }, h('span', { class: 'label' }, 'Forma de pago'), picker), refBox,
      h('label', { class: 'field wide' }, 'Nota (opcional)', h('input', { name: 'note', maxlength: '200' })));
  }, { submit: 'Registrar cobro', onSubmit: async form => {
    const amount = money(form.elements.amount.value);
    if (!amount) throw new Error('Revisa el importe de «Importe» (ejemplo: 150.00).');
    if (amount > store.balance) throw new Error(`No puede ser mayor que el saldo (${fmt(store.balance)}).`);
    await command(store.branchId, 'consignment.payment', { storeId: store.id, amountCents: amount, method: f.method, reference: f.method === 'CASH' ? '' : (form.elements.reference?.value ?? '').trim(), note: form.elements.note.value.trim() });
    say(`Cobro de ${fmt(amount)} a ${store.name} enviado a la caja.`, true); redraw();
  } });
}

// ---------- Tiendas ----------
async function storesTab() {
  const data = await api('route', { branch: state.branch });
  const stores = data.allStores;
  const selected = t.selectedStore === null ? null : stores.find(s => s.id === t.selectedStore) ?? null;
  if (!t.form || t.form.id !== (selected?.id ?? t.form.newId)) {
    t.form = selected ? { id: selected.id, name: selected.name, contact: selected.contact, phone: selected.phone, address: selected.address, branchId: selected.branchId }
      : { id: undefined, newId: crypto.randomUUID(), name: '', contact: '', phone: '', address: '', branchId: state.branch || state.me.branchId || state.branches[0]?.id };
    t.template = selected ? new Map(selected.template.map(l => [l.productId, l.quantity])) : null;
  }
  const f = t.form;
  const account = selected ? await api('tray-account', { branch: selected.branchId, store: selected.id }) : null;
  const bind = key => e => { f[key] = e.target.value; };
  const pick = id => { t.selectedStore = id; t.form = null; say(''); redraw(); };
  const priced = selected ? data.products.filter(p => p.branchId === selected.branchId) : [];
  const templateBox = h('div');
  const drawTemplate = () => templateBox.replaceChildren(
    h('p', { class: 'caption', text: `${[...t.template.values()].reduce((a, b) => a + b, 0)} piezas en la charola habitual` }),
    priced.length ? h('div', { class: 'stepper-list' }, priced.map(p => h('div', { class: 'stepper-row' },
      h('span', {}, h('strong', { text: p.name }), h('small', { text: ` · ${fmt(p.price)}` })),
      h('span', { class: 'stepper-track' },
        h('button', { type: 'button', class: 'qty', 'aria-label': `Menos ${p.name}`, disabled: !manage(), onclick: () => { t.template.set(p.id, Math.max(0, (t.template.get(p.id) ?? 0) - 1)); drawTemplate(); } }, '−'),
        h('b', { class: 'qty-value', text: String(t.template.get(p.id) ?? 0) }),
        h('button', { type: 'button', class: 'qty', 'aria-label': `Más ${p.name}`, disabled: !manage(), onclick: () => { t.template.set(p.id, Math.min(999, (t.template.get(p.id) ?? 0) + 1)); drawTemplate(); } }, '+')))))
      : h('p', { class: 'catalog-empty', text: 'Ningún pan tiene precio de charola.' }));
  if (selected) drawTemplate();
  return h('div', { class: 'trays-stores' },
    h('div', { class: 'trays-store-list' },
      manage() ? h('button', { type: 'button', class: 'primary', onclick: () => pick(null) }, 'Nueva tienda') : null,
      stores.map(s => h('button', { type: 'button', class: 'trays-store-item' + (s.id === selected?.id ? ' selected' : ''), onclick: () => pick(s.id) },
        h('strong', { text: s.name }), h('small', { text: (s.active ? (s.balance > 0 ? `Debe ${fmt(s.balance)}` : 'Al corriente') : 'Desactivada') + (multi() ? ` · ${branchName(s.branchId)}` : '') })))),
    h('div', { class: 'trays-store-detail' },
      h('h2', { class: 'detail-title', text: selected ? `Editar ${selected.name}` : 'Nueva tienda' }),
      !selected && state.me.admin && state.branches.length > 1 ? h('label', { class: 'label' }, 'Sucursal', h('select', { onchange: bind('branchId') }, state.branches.map(b => h('option', { value: b.id, text: b.name, selected: b.id === f.branchId })))) : null,
      h('label', { class: 'label' }, 'Nombre de la tienda', h('input', { maxlength: '100', value: f.name, 'aria-label': 'Nombre de la tienda', oninput: bind('name') })),
      h('div', { class: 'catalog-two' },
        h('label', { class: 'label' }, 'Contacto', h('input', { maxlength: '100', value: f.contact, oninput: bind('contact') })),
        h('label', { class: 'label' }, 'Teléfono', h('input', { maxlength: '40', inputmode: 'tel', value: f.phone, oninput: bind('phone') }))),
      h('label', { class: 'label' }, 'Dirección', h('input', { maxlength: '200', value: f.address, oninput: bind('address') })),
      manage() ? h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'primary', onclick: async () => {
          if (!f.name.trim()) { say('Escribe el nombre de la tienda.'); return redraw(); }
          try {
            const id = f.id ?? f.newId;
            await command(f.branchId, 'consignment.store.save', { id, name: f.name.trim(), contact: f.contact.trim(), phone: f.phone.trim(), address: f.address.trim() });
            say('Tienda enviada a la caja.', true);
          } catch (error) { say(error.message); }
          redraw();
        } }, 'Guardar tienda'),
        selected ? h('button', { type: 'button', class: 'secondary', onclick: async () => {
          try { await command(selected.branchId, 'consignment.store.save', { id: selected.id, name: selected.name, contact: selected.contact, phone: selected.phone, address: selected.address, active: !selected.active });
            say(selected.active ? 'Desactivación enviada: la tienda dejará de aparecer en la ruta.' : 'Reactivación enviada a la caja.', true); }
          catch (error) { say(error.message); }
          redraw();
        } }, selected.active ? 'Desactivar tienda' : 'Reactivar tienda') : null) : null,
      selected ? [
        h('h3', { class: 'inv-section', text: 'Charola habitual' }), h('p', { class: 'caption', text: 'Se propone al entregar; siempre puedes ajustar las piezas ese día.' }), templateBox,
        manage() ? h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: async () => {
          try { await command(selected.branchId, 'consignment.template.save', { storeId: selected.id, lines: [...t.template.entries()].filter(([, q]) => q > 0).map(([productId, quantity]) => ({ productId, quantity })) });
            say('Charola habitual enviada a la caja. Se propondrá al entregar.', true); }
          catch (error) { say(error.message); }
          redraw();
        } }, 'Guardar charola habitual')) : null,
        h('h3', { class: 'inv-section', text: 'Estado de cuenta' }),
        h('p', { class: 'trays-balance', text: account.balance === 0 ? 'Al corriente' : `Debe ${fmt(account.balance)}` }),
        account.movements.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Estado de cuenta' },
          h('thead', {}, h('tr', {}, ['Fecha', 'Concepto', 'Cargo', 'Pago'].map((c, i) => h('th', { class: i > 1 ? 'num' : null, text: c })))),
          h('tbody', {}, account.movements.map(m => h('tr', {}, h('td', { 'data-label': 'Fecha', text: dayName(m.at) }), h('td', { 'data-label': 'Concepto', text: m.concept }),
            h('td', { class: 'num', 'data-label': 'Cargo', text: m.charge ? fmt(m.charge) : '' }), h('td', { class: 'num', 'data-label': 'Pago', text: m.payment ? fmt(m.payment) : '' }))))))
          : h('p', { class: 'catalog-empty', text: 'Sin movimientos todavía.' })] : null));
}

// ---------- Precios de charola ----------
async function pricesTab() {
  const data = await api('catalog', { branch: state.branch });
  const categories = new Map(data.categories.map(c => [c.id, c.name]));
  const products = data.products.filter(p => p.active);
  t.prices ??= new Map();
  const text = p => t.prices.get(p.id) ?? (p.consignmentPrice === null ? '' : pesos(p.consignmentPrice));
  const original = p => p.consignmentPrice === null ? '' : pesos(p.consignmentPrice);
  const difference = p => { const v = money(text(p)); if (v === undefined || v === null) return 'Sin precio'; if (!p.price) return '—';
    const off = Math.round(((p.price - v) * 100) / p.price); return off === 0 ? 'Igual que mostrador' : off > 0 ? `${off} % menos` : `${-off} % más`; };
  return h('div', {},
    h('p', { class: 'muted', text: 'Lo que paga la tienda por cada pieza vendida, igual para todas las tiendas. Un pan sin precio no se puede poner en charolas.' }),
    h('div', { class: 'table-wrap history-table trays-prices' }, h('table', { class: 'cards', 'aria-label': 'Precios de charola' },
      h('thead', {}, h('tr', {}, ['Pan', 'Categoría', 'Mostrador', 'Charola', 'Diferencia'].map((c, i) => h('th', { class: i === 2 ? 'num' : null, text: c })))),
      h('tbody', {}, products.map(p => { const diff = h('td', { 'data-label': 'Diferencia', text: difference(p) });
        return h('tr', {}, h('td', { 'data-label': 'Pan', text: p.name + (multi() ? ` (${branchName(p.branchId)})` : '') }), h('td', { 'data-label': 'Categoría', text: categories.get(p.categoryId) ?? '' }),
          h('td', { class: 'num', 'data-label': 'Mostrador', text: fmt(p.price) }),
          h('td', { 'data-label': 'Charola' }, h('input', { inputmode: 'decimal', value: text(p), disabled: !manage(), 'aria-label': `Precio de charola de ${p.name}`, oninput: e => { t.prices.set(p.id, e.target.value); diff.textContent = difference(p); } })),
          diff); })))),
    manage() ? h('div', { class: 'toolbar', style: { marginTop: '12px' } }, h('button', { type: 'button', class: 'primary', onclick: async () => {
      const changed = products.filter(p => t.prices.has(p.id) && t.prices.get(p.id).trim() !== original(p));
      if (!changed.length) { say('No había cambios.', true); return redraw(); }
      const bad = changed.find(p => money(t.prices.get(p.id)) === undefined);
      if (bad) { say(`Revisa el precio de ${bad.name} (ejemplo: 12.50).`); return redraw(); }
      const byBranch = new Map();
      for (const p of changed) byBranch.set(p.branchId, [...(byBranch.get(p.branchId) ?? []), { productId: p.id, priceCents: money(t.prices.get(p.id)) }]);
      try {
        for (const [branchId, prices] of byBranch) await command(branchId, 'consignment.price.save', { prices });
        t.prices = new Map(); say(`${changed.length} precio(s) de charola enviados a la caja.`, true);
      } catch (error) { say(error.message); }
      redraw();
    } }, 'Guardar precios')) : null);
}

// ---------- Reporte ----------
async function reportTab() {
  const today = localDay(new Date());
  t.from ??= localDay(new Date(Date.now() - 6 * 86_400_000)); t.to ??= today;
  const generate = (from, to) => { t.from = from; t.to = to; t.report = null; redraw(); };
  if (!t.report || t.report.from !== t.from || t.report.to !== t.to) {
    try { t.report = await api('tray-report', { branch: state.branch, from: t.from, to: t.to }); t.generated = new Date(); }
    catch (error) { say(error.message); t.report = null; }
  }
  const r = t.report;
  const pct = v => v === null ? '—' : `${v.toFixed(1)} %`;
  const from = h('input', { type: 'date', value: t.from, 'aria-label': 'Desde' }), to = h('input', { type: 'date', value: t.to, 'aria-label': 'Hasta' });
  const first = `${today.slice(0, 8)}01`;
  const kpi = (label, value) => h('article', { class: 'card kpi' }, h('span', { text: label }), h('strong', { text: value }));
  return h('div', {},
    h('div', { class: 'report-dates' },
      h('label', { class: 'label' }, 'Desde', from), h('label', { class: 'label' }, 'Hasta', to),
      h('button', { type: 'button', class: 'primary', onclick: () => generate(from.value, to.value) }, 'Generar'),
      h('button', { type: 'button', class: 'secondary', onclick: () => generate(today, today) }, 'Hoy'),
      h('button', { type: 'button', class: 'secondary', onclick: () => generate(localDay(new Date(Date.now() - 6 * 86_400_000)), today) }, 'Últimos 7 días'),
      h('button', { type: 'button', class: 'secondary', onclick: () => generate(first, today) }, 'Este mes')),
    !r ? null : !r.stores.length ? h('p', { class: 'catalog-empty', text: 'Sin charolas entregadas en este periodo.' }) : [
      h('section', { class: 'report-kpis four' },
        kpi('Charolas entregadas', r.totals.trays.toLocaleString('es-MX')), kpi('Piezas entregadas', r.totals.delivered.toLocaleString('es-MX')),
        kpi('Vendidas · % de venta', `${r.totals.sold.toLocaleString('es-MX')}${r.totals.sellThrough === null ? '' : ` · ${pct(r.totals.sellThrough)}`}`), kpi('Regresaron para Piedra', r.totals.returned.toLocaleString('es-MX')),
        kpi('Cargado a tiendas', fmt(r.totals.charged)), kpi('Pagos recibidos', fmt(r.totals.paid)), kpi('Por cobrar hoy', fmt(r.totals.balance)), kpi('Margen que se queda en tiendas', fmt(r.totals.leftToStores))),
      r.totals.atStore ? h('p', { class: 'caption', text: `${r.totals.atStore} piezas siguen en las tiendas (charolas sin recoger).` }) : null,
      h('h3', { class: 'inv-section', text: '% de venta por tienda' }), h('p', { class: 'caption', text: 'De las charolas ya recogidas: piezas vendidas entre piezas dejadas.' }),
      h('div', { class: 'bars' }, r.stores.filter(s => s.sellThrough !== null).sort((a, b) => b.sellThrough - a.sellThrough).map(s => h('div', { class: 'bar', title: `${s.sold} vendidas, ${s.returned} regresaron para Piedra` },
        h('span', { class: 'label', text: s.storeName }), h('span', { class: 'track' }, h('span', { class: 'fill', style: { width: `${s.sellThrough}%` } })),
        h('span', { class: 'value', text: `${pct(s.sellThrough)} · ${s.sold} de ${s.sold + s.returned}` })))),
      h('h3', { class: 'inv-section', text: 'Por tienda' }),
      table([{ label: 'Tienda', value: s => s.storeName }, { label: 'Charolas', num: true, value: s => String(s.trays) }, { label: 'Dejadas', num: true, value: s => String(s.delivered) },
        { label: 'Vendidas', num: true, value: s => String(s.sold) }, { label: 'Piedra', num: true, value: s => String(s.returned) }, { label: '% venta', num: true, value: s => pct(s.sellThrough) },
        { label: 'Cargo', num: true, value: s => fmt(s.charged) }, { label: 'Pagado', num: true, value: s => s.paid ? fmt(s.paid) : '—' }, { label: 'Debe', num: true, value: s => s.balance ? fmt(s.balance) : '—' }], r.stores),
      h('h3', { class: 'inv-section', text: 'Por pan' }),
      table([{ label: 'Pan', value: p => p.productName }, { label: 'Dejadas', num: true, value: p => String(p.delivered) }, { label: 'Vendidas', num: true, value: p => String(p.sold) },
        { label: 'Regresaron (Piedra)', num: true, value: p => String(p.returned) }, { label: '% venta', num: true, value: p => pct(p.sellThrough) }, { label: 'Cargo', num: true, value: p => fmt(p.charged) }], r.products),
      t.generated ? h('p', { class: 'caption', text: `Actualizado a las ${t.generated.toLocaleTimeString('es-MX', { hour12: false })}` }) : null]);
}

// ---------- Entregas y cobros del periodo (sólo en la web) ----------
async function periodTab(kind) {
  const data = await api('consignment', range());
  const storeName = new Map(data.stores.map(s => [s.id, s.name]));
  if (kind === 'deliveries') {
    const label = { pending: ['Por recoger', 'warn'], collected: ['Recogida', 'ok'], voided: ['Anulada', 'bad'] };
    return h('div', {}, h('p', { class: 'caption', text: 'Charolas entregadas en el periodo elegido arriba.' }), table([
      { label: 'Entrega', value: d => at(d.at) }, { label: 'Tienda', value: d => storeName.get(d.store) ?? '—' },
      { label: 'Estado', value: d => h('span', { class: 'pill ' + label[d.status][1], text: label[d.status][0] }) },
      { label: 'Piezas', num: true, value: d => String(d.pieces) }, { label: 'Vendidas / regreso', num: true, value: d => d.status === 'collected' ? `${d.sold} / ${d.returned}` : '—' },
      { label: 'Cobrado', num: true, value: d => d.status === 'collected' ? fmt(d.charged) : '—' },
      { label: 'Contenido', span: true, value: d => h('small', { text: d.items.map(i => `${i.quantity} ${i.name}`).join(' · ') }) },
    ], data.deliveries, { empty: 'Sin entregas en el periodo.' }));
  }
  return h('div', {}, h('p', { class: 'caption', text: 'Cobros recibidos en el periodo elegido arriba.' }), table([
    { label: 'Fecha', value: p => at(p.at) }, { label: 'Tienda', value: p => storeName.get(p.store) ?? '—' },
    { label: 'Forma', value: p => METHOD[p.method] ?? p.method }, { label: 'Nota', value: p => p.note || '—' }, { label: 'Importe', num: true, value: p => h('b', { text: fmt(p.amount) }) },
  ], data.payments, { empty: 'Sin cobros en el periodo.' }));
}
export const usesRange = () => t.tab === 'deliveries' || t.tab === 'payments';

