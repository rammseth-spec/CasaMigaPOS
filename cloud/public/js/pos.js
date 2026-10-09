// Punto de venta en la web (docs/POS_WEB.md): elegir la caja web de este dispositivo, abrir turno, vender con fotos,
// cobrar, imprimir el ticket y hacer el corte. Las reglas viven en la nube (lib/pos.ts); aquí sólo se arma el carrito.
import { api, can, card, dialog, field, fmt, h, icon, METHOD, pesos, post, render, state, time, toast } from './app.js';

const TRASH = ['M4 7h16', 'M10 11v6', 'M14 11v6', 'M6 7l1 13h10l1-13', 'M9 7V4h6v3'];

const saved = {
  get(key, fallback) { try { return JSON.parse(localStorage.getItem(key) ?? 'null') ?? fallback; } catch { return fallback; } },
  set(key, value) { try { if (value === null) localStorage.removeItem(key); else localStorage.setItem(key, JSON.stringify(value)); } catch { /* sin almacenamiento */ } },
};
const tillKey = branch => `cm-pos-till:${branch}`;
const cartKey = till => `cm-pos-cart:${till}`;
const ui = { q: '', category: '' };
let cart = new Map();
let cartTill = null;
// Cobro sin confirmar (por ejemplo, se cayó la red): reintentar el mismo carrito usa el mismo identificador y nunca cobra dos veces.
let pending = null;

function useCart(till) {
  if (cartTill === till) return;
  cartTill = till; cart = new Map(saved.get(cartKey(till), []));
}
const keepCart = () => saved.set(cartKey(cartTill), cart.size ? [...cart.entries()] : null);
const money = text => { const t = String(text ?? '').trim().replace(/[$\s]/g, '').replace(',', '.'); return /^\d{1,7}(\.\d{1,2})?$/.test(t) ? Math.round(Number(t) * 100) : null; };
const normal = text => text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
const picture = (product, branchId, cls = '') => h('span', { class: `pic ${cls}`.trim() }, product.photo
  ? h('img', { src: `/api/web/photo?product=${product.id}&branch=${branchId}&v=${product.photo}`, alt: '', loading: 'lazy' })
  : h('span', { text: product.name.slice(0, 1) }));

export async function vender(ctx) {
  const branchId = ctx.branchId();
  const chosen = saved.get(tillKey(branchId), null);
  const data = await api('pos', { branch: branchId, till: chosen ?? '' });
  if (!data.till) { if (chosen) saved.set(tillKey(branchId), null); return chooseTill(data); }
  useCart(data.till.id);
  if (!data.session) return openShift(data);
  if (data.session.count) return countedShift(data);
  return sell(data);
}

// ---------- Caja y turno ----------

function tillStatus(t) {
  return t.counted ? ['Contada · falta cerrar', 'warn'] : t.open ? ['Turno abierto', 'ok'] : ['Sin turno', ''];
}
function chooseTill(data) {
  const pick = id => { saved.set(tillKey(data.branchId), id); render(); };
  const create = async e => {
    e.target.disabled = true;
    try { const t = await post('pos-till', { branchId: data.branchId }); toast(`Caja ${t.name} creada (folios ${t.code}).`); pick(t.id); }
    catch (error) { toast(error.message); e.target.disabled = false; }
  };
  return card('¿Qué caja es este dispositivo?',
    h('p', { class: 'muted', text: 'Cada caja web tiene su propio turno, fondo y folios. Usa una por dispositivo (por ejemplo, la tableta del mostrador).', style: { marginBottom: '12px' } }),
    data.tills.length ? h('div', { class: 'till-list' }, data.tills.map(t => {
      const [label, cls] = tillStatus(t);
      return h('button', { type: 'button', class: 'till', onclick: () => pick(t.id) }, h('strong', { text: t.name }), h('span', { class: 'muted small', text: `Folios ${t.code}-000001…` }), h('span', { class: `pill ${cls}`, text: label }));
    })) : h('p', { class: 'empty', text: can('users.manage') ? 'Todavía no hay cajas web en esta sucursal.' : 'Todavía no hay cajas web en esta sucursal. Pide al administrador que cree una.' }),
    can('users.manage') ? h('div', { class: 'toolbar', style: { marginTop: '12px' } }, h('button', { type: 'button', class: data.tills.length ? '' : 'primary', onclick: create }, '+ Nueva caja web')) : null);
}
const changeTill = data => h('button', { type: 'button', class: 'ghost', onclick: () => { saved.set(tillKey(data.branchId), null); render(); } }, 'Cambiar de caja');

function openShift(data) {
  if (!can('cash.open')) return card(`${data.till.name} · sin turno`, h('p', { text: 'Esta caja no tiene turno abierto y tu rol no puede abrirlo.' }), h('div', { class: 'toolbar', style: { marginTop: '12px' } }, changeTill(data)));
  const input = h('input', { name: 'fund', inputmode: 'decimal', placeholder: '500.00', autocomplete: 'off', 'aria-label': 'Fondo inicial' });
  const error = h('p', { class: 'error', role: 'alert' });
  const open = async e => {
    e.preventDefault();
    const cents = money(input.value);
    if (cents === null) { error.textContent = 'Escribe el fondo, por ejemplo 500.00 (o 0).'; return; }
    const button = e.target.querySelector('button[type=submit]'); button.disabled = true;
    try { await post('pos-open', { tillId: data.till.id, openingCents: cents }); toast('Turno abierto.'); render(); }
    catch (err) { error.textContent = err.message; button.disabled = false; }
  };
  return card(`${data.till.name} · abrir turno`,
    h('form', { class: 'open-shift', onsubmit: open }, h('p', { class: 'muted', text: 'Cuenta el efectivo con el que empieza la caja.' }),
      field('Fondo inicial (MXN)', input), error,
      h('div', { class: 'toolbar' }, h('button', { type: 'submit', class: 'primary' }, 'Abrir turno'), changeTill(data))));
}

function countRows(c) {
  const rows = [['Fondo', c.opening], ['Ventas en efectivo', c.cashSales], ['Devoluciones en efectivo', -c.cashRefunds], ['Gastos, retiros y entradas', c.movements]];
  return h('ul', { class: 'rows' }, rows.map(([label, cents]) => h('li', {}, h('span', { text: label }), h('span'), h('b', { class: 'num', text: fmt(cents) }))),
    h('li', {}, h('strong', { text: 'Esperado' }), h('span'), h('b', { class: 'num', text: fmt(c.expected) })),
    h('li', {}, h('strong', { text: 'Contado' }), h('span'), h('b', { class: 'num', text: fmt(c.counted) })),
    h('li', {}, h('strong', { text: 'Diferencia' }), h('span'), h('b', { class: 'num ' + (c.difference < 0 ? 'short' : c.difference > 0 ? 'over' : 'even'), text: c.difference === 0 ? 'Cuadra' : fmt(c.difference) })));
}
async function closeShift(data) {
  await post('pos-close', { tillId: data.till.id, sessionId: data.session.id });
  toast('Turno cerrado.'); render();
}
function countedShift(data) {
  if (!can('cash.close')) return card(`${data.till.name} · turno contado`, h('p', { text: 'Ya se hizo el conteo de esta caja; falta que el encargado cierre el turno. Mientras tanto no se puede vender.' }));
  return card(`${data.till.name} · conteo confirmado`, h('p', { class: 'muted', text: `Turno de ${data.session.openedBy || '—'} desde ${time(data.session.openedAt)} · ${data.session.tickets} tickets. Ya no entran ventas a este turno.`, style: { marginBottom: '10px' } }),
    countRows(data.session.count),
    h('div', { class: 'toolbar', style: { marginTop: '14px' } }, h('button', { type: 'button', class: 'primary', onclick: async e => {
      e.target.disabled = true;
      try { await closeShift(data); } catch (error) { toast(error.message); e.target.disabled = false; }
    } }, 'Cerrar turno')));
}
function countDialog(data) {
  dialog('Corte de caja · conteo ciego', body => {
    body.append(h('p', { class: 'muted wide', text: 'Cuenta el efectivo del cajón y escríbelo sin ver el esperado. Después de confirmarlo ya no entran ventas a este turno.' }),
      field('Efectivo contado (MXN)', h('input', { name: 'counted', inputmode: 'decimal', autocomplete: 'off', required: true }), true));
  }, { submit: 'Confirmar conteo', onSubmit: async form => {
    const cents = money(form.elements.counted.value);
    if (cents === null) throw new Error('Escribe lo contado, por ejemplo 1250.50.');
    const result = await post('pos-count', { tillId: data.till.id, sessionId: data.session.id, countedCents: cents });
    setTimeout(() => dialog('Resultado del conteo', body => {
      body.append(h('div', { class: 'wide' }, countRows(result)));
    }, { submit: 'Cerrar turno', onSubmit: async () => { await closeShift(data); } }));
    render({ silent: true });
  } });
}

// ---------- Venta ----------

function sell(data) {
  const branchId = data.branchId;
  const products = new Map(data.products.map(p => [p.id, p]));
  for (const id of cart.keys()) if (!products.has(id)) cart.delete(id); // ya no se vende (inactivo o sin categoría)
  keepCart();
  const grid = h('div', { class: 'products pos-grid' });
  const ticket = h('aside', { class: 'card pos-ticket', 'aria-label': 'Ticket' });
  const bar = h('div', { class: 'pos-bar-mobile' });
  const lines = () => [...cart.entries()].map(([id, quantity]) => ({ product: products.get(id), quantity }));
  const total = () => lines().reduce((a, l) => a + l.product.price * l.quantity, 0);
  const pieces = () => lines().reduce((a, l) => a + l.quantity, 0);

  const set = (id, quantity) => { if (quantity <= 0) cart.delete(id); else cart.set(id, Math.min(999, quantity)); keepCart(); refresh(); };
  const visible = () => {
    const text = normal(ui.q.trim());
    return data.products.filter(p => (!ui.category || p.categoryId === ui.category) && (!text || normal(`${p.name} ${p.sku ?? ''}`).includes(text)));
  };
  const drawGrid = () => {
    const list = visible();
    grid.replaceChildren(...(list.length ? list.map(p => h('button', { type: 'button', class: 'product', 'data-id': p.id, onclick: () => set(p.id, (cart.get(p.id) ?? 0) + 1) },
      h('div', { class: 'photo' }, p.photo ? h('img', { src: `/api/web/photo?product=${p.id}&branch=${branchId}&v=${p.photo}`, alt: '', loading: 'lazy' }) : 'Sin foto'),
      h('span', { class: 'badge', hidden: !cart.get(p.id), text: String(cart.get(p.id) ?? '') }),
      h('div', { class: 'info' }, h('strong', { text: p.name }), h('span', { class: 'price', text: fmt(p.price) }))))
      : [h('p', { class: 'empty', text: data.products.length ? 'Sin productos con ese filtro.' : 'No hay productos activos en el catálogo de esta sucursal.' })]));
  };
  const cartList = () => cart.size ? h('div', { class: 'cart' }, lines().map(({ product: p, quantity }) => h('div', { class: 'cart-line' },
    picture(p, branchId),
    h('div', { class: 'cart-name' }, h('strong', { text: p.name }), h('small', { text: `${fmt(p.price)} / pieza` })),
    h('b', { class: 'num', text: fmt(p.price * quantity) }),
    h('div', { class: 'cart-actions' },
      h('span', { class: 'stepper' },
        h('button', { type: 'button', 'aria-label': `Menos ${p.name}`, disabled: quantity <= 1, onclick: () => set(p.id, quantity - 1) }, '−'),
        h('output', { text: String(quantity) }),
        h('button', { type: 'button', 'aria-label': `Más ${p.name}`, onclick: () => set(p.id, quantity + 1) }, '+')),
      h('button', { type: 'button', class: 'ghost icon', 'aria-label': `Quitar ${p.name}`, onclick: () => set(p.id, 0) }, icon(TRASH))))))
    : h('div', { class: 'cart-empty' }, h('strong', { text: 'Carrito vacío' }), h('span', { class: 'muted', text: 'Toca un pan para agregarlo.' }));
  // En celular el ticket se abre como hoja; se redibuja con cada cambio mientras está abierta.
  let sheet = null, closeSheet = null;
  const charge = () => { if (sheet?.isConnected) closeSheet(); payDialog(data, lines(), total(), () => { cart.clear(); keepCart(); refresh(); }); };
  const totals = () => [
    h('div', { class: 'line muted' }, h('span', { text: 'Subtotal' }), h('span', { class: 'num', text: fmt(total()) })),
    h('div', { class: 'line total' }, h('span', { text: 'Total' }), h('strong', { class: 'num', text: fmt(total()) })),
    h('button', { type: 'button', class: 'primary pay', disabled: !cart.size, onclick: charge }, cart.size ? `Cobrar ${fmt(total())}` : 'Cobrar')];
  const refresh = () => {
    for (const tile of grid.querySelectorAll('.product')) {
      const q = cart.get(tile.dataset.id); const badge = tile.querySelector('.badge');
      badge.hidden = !q; badge.textContent = q ? String(q) : '';
    }
    ticket.replaceChildren(
      h('header', {}, h('div', {}, h('h2', { text: 'Ticket' }), h('span', { class: 'muted small', text: 'Venta temporal hasta confirmar el cobro' })),
        h('button', { type: 'button', class: 'ghost', disabled: !cart.size, onclick: () => { cart.clear(); keepCart(); refresh(); } }, 'Limpiar')),
      cartList(), h('div', { class: 'cart-totals' }, totals()));
    if (sheet?.isConnected) sheet.replaceChildren(cartList(), h('div', { class: 'cart-totals' }, totals()));
    bar.replaceChildren(
      h('button', { type: 'button', class: 'cart-open', disabled: !cart.size, onclick: () => {
        closeSheet = dialog('Ticket', body => { sheet = h('div', { class: 'wide sheet-cart' }); body.append(sheet); }).close; refresh();
      } },
        h('span', { text: cart.size ? `${pieces()} pz` : 'Carrito vacío' }), h('strong', { class: 'num', text: fmt(total()) })),
      h('button', { type: 'button', class: 'primary', disabled: !cart.size, onclick: charge }, 'Cobrar'));
  };

  let timer = 0;
  const search = h('input', { type: 'search', placeholder: 'Buscar pan o SKU', value: ui.q, 'aria-label': 'Buscar pan',
    oninput: e => { clearTimeout(timer); timer = setTimeout(() => { ui.q = e.target.value; drawGrid(); }, 150); } });
  const chips = h('div', { class: 'chips pos-chips', role: 'group', 'aria-label': 'Categorías' });
  const drawChips = () => chips.replaceChildren(...[{ id: '', name: 'Todos' }, ...data.categories].map(c =>
    h('button', { type: 'button', class: 'chip', 'aria-pressed': String(ui.category === c.id), onclick: () => { ui.category = c.id; drawChips(); drawGrid(); } }, c.name)));
  drawChips(); drawGrid(); refresh();
  const s = data.session;
  return h('div', { class: 'pos' },
    h('div', { class: 'pos-head' },
      h('div', {}, h('strong', { text: data.till.name }), state.branches.length > 1 ? h('span', { class: 'muted small', text: state.branches.find(b => b.id === data.branchId)?.name ?? '' }) : null,
        h('span', { class: 'pill ok', text: 'Turno abierto' }),
        h('span', { class: 'muted small', text: ` ${s.openedBy || '—'} desde ${time(s.openedAt)} · ${s.tickets} tickets` })),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', onclick: () => ticketsDialog(data) }, 'Tickets'),
        can('cash.movement') ? h('button', { type: 'button', onclick: () => movementDialog(data) }, 'Gasto, entrada o retiro') : null,
        can('cash.close') ? h('button', { type: 'button', onclick: () => countDialog(data) }, 'Corte de caja') : null,
        changeTill(data))),
    h('section', { class: 'pos-catalog' }, search, chips, grid),
    ticket, bar);
}

// ---------- Cobro y ticket ----------

function payDialog(data, lines, total, done) {
  const signature = JSON.stringify(lines.map(l => [l.product.id, l.quantity]));
  let method = 'CASH';
  const received = h('input', { name: 'received', inputmode: 'decimal', autocomplete: 'off', value: pesos(total), 'aria-label': 'Efectivo recibido' });
  const reference = h('input', { name: 'reference', maxlength: '40', autocomplete: 'off', placeholder: 'Últimos 4 dígitos o folio' });
  const change = h('p', { class: 'change wide' });
  const cashBox = h('div', { class: 'wide pay-cash' });
  const refBox = h('div', { class: 'wide', hidden: true }, field('Referencia (opcional)', reference));
  const update = () => {
    const cents = money(received.value);
    change.replaceChildren(...(method !== 'CASH' ? [h('span', { text: `Se cobra exacto: ${fmt(total)}` })]
      : cents === null ? [h('span', { class: 'short', text: 'Escribe lo recibido.' })]
      : cents < total ? [h('span', { class: 'short', text: `Faltan ${fmt(total - cents)}` })]
      : [h('span', { text: 'Cambio' }), h('strong', { class: 'num', text: fmt(cents - total) })]));
  };
  const bills = [...new Set([total, ...[5000, 10000, 20000, 50000].filter(b => b > total)])].slice(0, 4);
  cashBox.append(field('Efectivo recibido (MXN)', received),
    h('div', { class: 'chips' }, bills.map(b => h('button', { type: 'button', class: 'chip', onclick: () => { received.value = pesos(b); update(); } }, b === total ? 'Exacto' : fmt(b)))));
  received.addEventListener('input', update);
  const methods = h('div', { class: 'chips wide', role: 'group', 'aria-label': 'Forma de pago' });
  const drawMethods = () => methods.replaceChildren(...Object.entries(METHOD).map(([id, label]) => h('button', { type: 'button', class: 'chip', 'aria-pressed': String(method === id),
    onclick: () => { method = id; cashBox.hidden = id !== 'CASH'; refBox.hidden = id === 'CASH'; drawMethods(); update(); } }, label)));
  drawMethods(); update();
  dialog(`Cobrar ${fmt(total)}`, body => {
    body.append(h('div', { class: 'wide ticket' }, lines.map(l => h('div', { class: 'line' }, h('span', { text: `${l.quantity} × ${l.product.name}` }), h('b', { text: fmt(l.product.price * l.quantity) })))),
      methods, cashBox, refBox, change);
    setTimeout(() => received.select());
  }, { submit: 'Cobrar', onSubmit: async () => {
    const cents = method === 'CASH' ? money(received.value) : null;
    if (method === 'CASH' && (cents === null || cents < total)) throw new Error('El efectivo recibido no alcanza para el total.');
    const key = `${signature}|${method}|${cents}|${reference.value.trim()}`;
    if (!pending || pending.key !== key) pending = { key, id: crypto.randomUUID() };
    let sale;
    try {
      sale = await post('pos-sale', { tillId: data.till.id, sessionId: data.session.id, operationId: pending.id, method,
        lines: lines.map(l => ({ productId: l.product.id, quantity: l.quantity })), receivedCents: cents, reference: method === 'CASH' ? null : reference.value, expectedTotal: total });
    } catch (error) {
      if (error.status === 0) throw new Error('No se pudo confirmar el cobro. Revisa la conexión y toca Cobrar otra vez: si ya se había cobrado, no se duplica.');
      if (error.status === 409) setTimeout(() => render({ silent: true }));
      throw error;
    }
    pending = null;
    done();
    setTimeout(() => receiptDialog(sale));
  } });
}

function ticketLines(r, copy = false) {
  const row = (left, right, cls) => h('div', { class: `line ${cls ?? ''}`.trim() }, h('span', { text: left }), h('span', { class: 'num', text: right }));
  return [
    copy ? h('p', { class: 'center', text: '*** REIMPRESIÓN ***' }) : null,
    h('p', { class: 'center', text: 'Casa Miga' }), h('p', { class: 'center muted', text: r.branch }),
    h('hr'),
    h('p', { text: `Folio: ${r.folio}` }), h('p', { text: `Fecha: ${new Date(r.at).toLocaleString('es-MX', { timeZone: state.timeZone, dateStyle: 'short', timeStyle: 'medium' })}` }),
    h('p', { text: `Cajero: ${r.cashier || '—'}` }), h('p', { text: `Caja: ${r.till}` }),
    h('hr'),
    r.items.map(i => [h('p', { text: i.name }), row(`${i.quantity} x ${fmt(i.unitPrice)}`, fmt(i.total))]),
    h('hr'),
    row('Subtotal', fmt(r.total)), row('TOTAL', fmt(r.total), 'total'),
    r.method === 'CASH' ? [row('Pago', 'EFECTIVO'), row('Efectivo', fmt(r.received)), row('Cambio', fmt(r.change))]
      : [row('Pago', r.method === 'CARD' ? 'TARJETA' : 'TRANSFERENCIA'), row('Pagado', fmt(r.total)), r.reference ? row('Referencia', r.reference) : null],
    h('hr'), h('p', { class: 'center', text: 'Gracias por su compra.' }), copy ? h('p', { class: 'center', text: 'COPIA' }) : null];
}
export function printTicket(r, copy = false) {
  let area = document.getElementById('print-area');
  if (!area) { area = h('div', { id: 'print-area' }); document.body.append(area); }
  area.replaceChildren(h('div', { class: 'receipt' }, ticketLines(r, copy)));
  window.print();
}
function receiptDialog(r) {
  dialog(`Venta ${r.folio}`, body => {
    body.append(h('div', { class: 'wide sale-done' },
      r.method === 'CASH' ? h('div', { class: 'big-change' }, h('span', { text: 'Cambio' }), h('strong', { class: 'num', text: fmt(r.change) })) : null,
      h('div', { class: 'receipt on-screen' }, ticketLines(r))),
      h('div', { class: 'wide toolbar' }, h('button', { type: 'button', onclick: () => printTicket(r) }, 'Imprimir ticket')));
  }, { submit: 'Nueva venta', cancel: null, onSubmit: async () => { render({ silent: true }); } });
}

// ---------- Tickets: reimprimir, cancelar y devolver ----------

// Operación pendiente por contenido: reintentar lo mismo (por ejemplo, tras una falla de red) nunca lo registra dos veces.
const pendingOps = new Map();
const opFor = key => { if (!pendingOps.has(key)) pendingOps.set(key, crypto.randomUUID()); return pendingOps.get(key); };
const KIND_LABEL = { CANCELLATION: 'Cancelación', RETURN: 'Devolución' };

function ticketsDialog(data) {
  const list = h('div', { class: 'ticket-list wide' }, h('p', { class: 'muted', text: 'Cargando…' }));
  let timer = 0;
  const load = async text => {
    try {
      const rows = await api('pos-tickets', { till: data.till.id, q: text });
      list.replaceChildren(...(rows.length ? rows.map(r => h('button', { type: 'button', class: 'ticket-row', onclick: () => ticketDialog(data, r.id) },
        h('span', {}, h('strong', { text: r.folio }), h('small', { text: `${time(r.at)} · ${r.pieces} pz · ${METHOD[r.method] ?? r.method}` })),
        h('span', { class: 'num' }, h('b', { text: fmt(r.total) }),
          r.cancelled ? h('small', { class: 'short', text: 'Cancelada' }) : r.refunded ? h('small', { class: 'over', text: `Dev. −${fmt(r.refunded)}` }) : null)))
        : [h('p', { class: 'empty', text: text ? 'No se encontró ese folio.' : 'Todavía no hay ventas en este turno.' })]));
    } catch (error) { list.replaceChildren(h('p', { class: 'error', text: error.message })); }
  };
  dialog('Tickets', body => {
    body.append(h('input', { class: 'wide', type: 'search', placeholder: 'Folio, por ejemplo C02-000015 o 15', 'aria-label': 'Buscar folio',
      oninput: e => { clearTimeout(timer); timer = setTimeout(() => load(e.target.value.trim()), 300); } }),
      h('p', { class: 'muted small wide', text: 'Del turno abierto; con un folio buscas cualquier venta de la sucursal.' }), list);
    load('');
  });
}

async function ticketDialog(data, saleId) {
  let t;
  try { t = await api('pos-ticket', { till: data.till.id, id: saleId }); } catch (error) { toast(error.message); return; }
  const r = t.receipt;
  const refunds = can('sales.refund');
  dialog(`Venta ${r.folio}`, (body, close) => {
    body.append(h('div', { class: 'wide receipt on-screen' }, ticketLines(r)),
      t.refunds.length ? h('div', { class: 'wide ticket' }, h('h3', { text: 'Devoluciones' }), t.refunds.map(x =>
        h('div', { class: 'line' }, h('span', { text: `${KIND_LABEL[x.kind] ?? x.kind} · ${time(x.at)} · ${x.by || '—'} · «${x.reason}»` }), h('b', { class: 'short', text: `−${fmt(x.total)}` })))) : null,
      refunds && t.blocker ? h('p', { class: 'notice wide', text: t.blocker }) : null,
      h('div', { class: 'wide toolbar' },
        can('sales.receipt.reprint') ? h('button', { type: 'button', onclick: () => printTicket(r, true) }, 'Reimprimir') : null,
        refunds && t.canCancel ? h('button', { type: 'button', class: 'danger', onclick: () => { close(); refundDialog(data, t, 'CANCELLATION'); } }, 'Cancelar venta') : null,
        refunds && t.canReturn ? h('button', { type: 'button', onclick: () => { close(); refundDialog(data, t, 'RETURN'); } }, 'Devolver productos') : null));
  });
}

function refundDialog(data, t, kind) {
  const r = t.receipt;
  const back = new Map(t.lines.map(l => [l.position, kind === 'CANCELLATION' ? l.sold : 0]));
  const total = () => t.lines.reduce((a, l) => a + l.unitPrice * back.get(l.position), 0);
  const summary = h('div', { class: 'totals wide' });
  const refresh = () => summary.replaceChildren(h('span', { text: kind === 'CANCELLATION' ? 'Se cancela la venta completa' : `${[...back.values()].reduce((a, b) => a + b, 0)} pz` }),
    h('span', { text: `${r.method === 'CASH' ? 'Sale de la caja' : 'A reembolsar'} ${fmt(total())}` }));
  dialog(kind === 'CANCELLATION' ? `Cancelar ${r.folio}` : `Devolver de ${r.folio}`, body => {
    body.append(kind === 'RETURN'
      ? h('div', { class: 'stepper-list wide' }, t.lines.filter(l => l.remaining > 0).map(l => h('div', { class: 'stepper-row' },
        h('span', {}, l.name, h('small', { text: `Vendidas ${l.sold}${l.refunded ? ` · ya devueltas ${l.refunded}` : ''} · ${fmt(l.unitPrice)} c/u` })),
        stepper(0, l.remaining, v => { back.set(l.position, v); refresh(); }, `devolver de ${l.name}`))))
      : h('p', { class: 'wide', text: 'La venta original no cambia: se registra una cancelación por el total y queda en la bitácora.' }),
      summary, field('Motivo', h('input', { name: 'reason', maxlength: '200', required: true, placeholder: 'Por ejemplo, pan aplastado' }), true));
    refresh();
  }, { submit: kind === 'CANCELLATION' ? 'Cancelar venta' : 'Registrar devolución', onSubmit: async form => {
    const reason = form.elements.reason.value.trim();
    if (reason.length < 3) throw new Error('Escribe el motivo (de 3 a 200 caracteres).');
    const lines = [...back.entries()].filter(([, q]) => q > 0).map(([position, quantity]) => ({ position, quantity }));
    if (!lines.length) throw new Error('Indica al menos una pieza a devolver.');
    const key = `refund|${r.id}|${kind}|${JSON.stringify(lines)}|${reason}`;
    let done;
    try {
      done = await post('pos-refund', { tillId: data.till.id, sessionId: data.session.id, operationId: opFor(key), saleId: r.id, kind, lines: kind === 'RETURN' ? lines : undefined, reason });
    } catch (error) {
      if (error.status === 0) throw new Error('No se pudo confirmar. Revisa la conexión y vuelve a tocar el botón: si ya se registró, no se duplica.');
      throw error;
    }
    pendingOps.delete(key);
    setTimeout(() => dialog(kind === 'CANCELLATION' ? 'Venta cancelada' : 'Devolución registrada', b => b.append(
      h('div', { class: 'wide big-change' }, h('span', { text: done.method === 'CASH' ? 'Entrega' : 'Reembolsa' }), h('strong', { class: 'num', text: fmt(done.total) })),
      h('p', { class: 'wide', text: done.instruction })), { submit: 'Listo', cancel: null, onSubmit: async () => { render({ silent: true }); } }));
  } });
}
function stepper(value, max, onchange, label) {
  const output = h('output', { 'aria-live': 'polite', text: String(value) });
  const set = next => { value = Math.max(0, Math.min(max, next)); output.textContent = String(value); onchange(value); };
  return h('span', { class: 'stepper' },
    h('button', { type: 'button', 'aria-label': `Menos ${label}`, onclick: () => set(value - 1) }, '−'), output,
    h('button', { type: 'button', 'aria-label': `Más ${label}`, onclick: () => set(value + 1) }, '+'));
}

// ---------- Gastos, retiros y entradas ----------

const MOVE = { EXPENSE: 'Gasto', DEPOSIT: 'Entrada', WITHDRAWAL: 'Retiro' };
const MOVE_HELP = { EXPENSE: 'Efectivo que sale para pagar algo del negocio.', DEPOSIT: 'Efectivo que entra a la caja (cambio, fondo extra).', WITHDRAWAL: 'Efectivo que se saca para guardarlo (encargado o administrador).' };
const CATEGORY = { SUPPLIES: 'Insumos', GAS: 'Gas', CLEANING: 'Limpieza', TRANSPORT: 'Transporte', MAINTENANCE: 'Mantenimiento', OTHER: 'Otro' };

function movementDialog(data) {
  let kind = 'EXPENSE', category = null;
  const kinds = h('div', { class: 'chips wide', role: 'group', 'aria-label': 'Tipo de movimiento' });
  const help = h('p', { class: 'muted small wide' });
  const categories = h('div', { class: 'chips wide', role: 'group', 'aria-label': 'Categoría' });
  const draw = () => {
    kinds.replaceChildren(...Object.entries(MOVE).filter(([k]) => k !== 'WITHDRAWAL' || can('cash.withdrawal')).map(([k, label]) =>
      h('button', { type: 'button', class: 'chip', 'aria-pressed': String(kind === k), onclick: () => { kind = k; if (k !== 'EXPENSE') category = null; draw(); } }, label)));
    help.textContent = MOVE_HELP[kind];
    categories.hidden = kind !== 'EXPENSE';
    categories.replaceChildren(...Object.entries(CATEGORY).map(([k, label]) =>
      h('button', { type: 'button', class: 'chip', 'aria-pressed': String(category === k), onclick: () => { category = k; draw(); } }, label)));
  };
  draw();
  const list = data.session.movements ?? [];
  dialog('Movimiento de efectivo', body => {
    body.append(kinds, help, categories,
      field('Importe (MXN)', h('input', { name: 'amount', inputmode: 'decimal', autocomplete: 'off', required: true })),
      field('Nota', h('input', { name: 'note', maxlength: '200', placeholder: 'Opcional (obligatoria en «Otro»)' })),
      h('div', { class: 'wide' }, h('h3', { text: 'En este turno' }), list.length ? h('ul', { class: 'rows' }, list.map(m => h('li', {},
        h('span', {}, h('strong', { text: MOVE[m.kind] ?? m.kind }), h('small', { class: 'muted', text: ` ${[m.category ? CATEGORY[m.category] : '', m.note].filter(Boolean).join(' · ')}` })),
        h('span', { class: 'muted small', text: `${time(m.at)} · ${m.by || '—'}` }),
        h('b', { class: 'num', text: `${m.kind === 'DEPOSIT' ? '+' : '−'}${fmt(m.amount)}` }))))
        : h('p', { class: 'empty', text: 'Sin gastos, retiros ni entradas todavía.' })));
  }, { submit: 'Registrar', onSubmit: async form => {
    const text = String(form.elements.amount.value).trim().replace(/[$\s]/g, '').replace(',', '.');
    if (!/^\d{1,7}(\.\d{1,2})?$/.test(text) || Number(text) <= 0) throw new Error('Escribe un importe mayor a cero, por ejemplo 150.00.');
    if (kind === 'EXPENSE' && !category) throw new Error('Elige la categoría del gasto.');
    const note = form.elements.note.value.trim();
    const amountCents = Math.round(Number(text) * 100);
    const key = `move|${data.session.id}|${kind}|${category}|${amountCents}|${note}`;
    try { await post('pos-movement', { tillId: data.till.id, sessionId: data.session.id, operationId: opFor(key), kind, category, amountCents, note }); }
    catch (error) {
      if (error.status === 0) throw new Error('No se pudo confirmar. Revisa la conexión y vuelve a tocar Registrar: si ya se registró, no se duplica.');
      throw error;
    }
    pendingOps.delete(key);
    toast(`${MOVE[kind]} de ${fmt(amountCents)} registrado.`);
    render({ silent: true });
  } });
}
