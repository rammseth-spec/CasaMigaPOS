// Punto de venta en la web, copia de la caja de escritorio (rediseño A de QAv1, docs/POS_WEB.md):
// «Punto de venta» (PosView: catálogo con fotos y ticket), «Cobro» (CheckoutDialog) y «Tu caja» (abrir, gastos/entradas/retiros,
// conteo ciego y cierre). Las reglas viven en la nube (lib/pos.ts); aquí sólo se arma el carrito.
import { api, can, dialog, field, fmt, h, icon, ICON, METHOD, post, render, setCashStatus, setHeading, state, time, toast } from './app.js';

const saved = {
  get(key, fallback) { try { return JSON.parse(localStorage.getItem(key) ?? 'null') ?? fallback; } catch { return fallback; } },
  set(key, value) { try { if (value === null) localStorage.removeItem(key); else localStorage.setItem(key, JSON.stringify(value)); } catch { /* sin almacenamiento */ } },
};
const tillKey = branch => `cm-pos-till:${branch}`;
const cartKey = till => `cm-pos-cart:${till}`;
/** Como la caja: 50 panes por página (SqliteCatalogStore). */
const PAGE = 50;
const ui = { q: '', category: '', page: 0 };
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
const photoUrl = (product, branchId) => `/api/web/photo?product=${product.id}&branch=${branchId}&v=${product.photo}`;
// Pan sin foto: el mismo dibujo de la caja (bolsa de pan en color trigo) y «Sin imagen».
const BREAD = 'M4,5 L16,5 18,8 18,17 2,17 2,8 Z M7,5 L8.5,2 11.5,2 13,5 M10,9 A3,3 0 1 1 9.99,9';

/** Lee la caja web de este dispositivo y avisa a la barra superior si está abierta o cerrada. */
async function loadTill(ctx) {
  return tillOf(ctx.branchId());
}
/** Caja web elegida en este dispositivo para la sucursal (con su turno), o sin caja. La usan Venta, Caja y el historial de ventas. */
export async function tillOf(branchId) {
  const chosen = saved.get(tillKey(branchId), null);
  const data = await api('pos', { branch: branchId, till: chosen ?? '' });
  if (!data.till && chosen) saved.set(tillKey(branchId), null);
  setCashStatus(data.till ? { open: !!data.session, till: data.till.name } : null);
  if (data.till) useCart(data.till.id);
  return data;
}

// ---------- Punto de venta ----------

export async function vender(ctx) {
  const data = await loadTill(ctx);
  // Como en la caja de escritorio: con la caja cerrada (o ya contada) no se entra a vender; se va a «Tu caja».
  if (!data.till || !data.session || data.session.count) { location.replace('#/caja'); return h('p', { class: 'muted', text: 'Abriendo Caja…' }); }
  return sell(data);
}

function sell(data) {
  const branchId = data.branchId;
  const products = new Map(data.products.map(p => [p.id, p]));
  for (const id of cart.keys()) if (!products.has(id)) cart.delete(id); // ya no se vende (inactivo o sin categoría)
  keepCart();
  const lines = () => [...cart.entries()].map(([id, quantity]) => ({ product: products.get(id), quantity }));
  const total = () => lines().reduce((a, l) => a + l.product.price * l.quantity, 0);
  const pieces = () => lines().reduce((a, l) => a + l.quantity, 0);
  const set = (id, quantity) => { if (quantity <= 0) cart.delete(id); else cart.set(id, Math.min(999, quantity)); keepCart(); refresh(); };

  // Buscador con lupa, «Buscar» y «× Limpiar» (sólo con texto).
  const input = h('input', { type: 'search', class: 'pos-search-input', placeholder: 'Buscar pan o SKU', maxlength: '160', value: ui.q, 'aria-label': 'Buscar producto por nombre o SKU', title: 'Buscar nombre o SKU' });
  const clearSearch = h('button', { type: 'button', class: 'ghost', hidden: !ui.q, onclick: () => { input.value = ''; apply(); input.focus(); } }, '× Limpiar');
  let timer = 0;
  const apply = () => { clearTimeout(timer); ui.q = input.value; ui.page = 0; clearSearch.hidden = !ui.q; drawGrid(); };
  input.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(apply, 200); });
  input.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); apply(); } });
  const searchRow = h('div', { class: 'pos-search-row' }, h('label', { class: 'pos-search' }, icon(ICON.search), input),
    h('button', { type: 'button', class: 'secondary', onclick: apply }, 'Buscar'), clearSearch);

  const chips = h('div', { class: 'pos-tabs', role: 'group', 'aria-label': 'Categorías' });
  const drawChips = () => chips.replaceChildren(...[{ id: '', name: 'Todos' }, ...data.categories].map(c =>
    h('button', { type: 'button', class: 'tab', 'aria-pressed': String(ui.category === c.id), onclick: () => { ui.category = c.id; ui.page = 0; drawChips(); drawGrid(); } }, c.name)));

  const grid = h('div', { class: 'tiles' });
  const pager = h('div', { class: 'pos-pager' });
  const drawGrid = () => {
    const text = normal(ui.q.trim());
    const list = data.products.filter(p => (!ui.category || p.categoryId === ui.category) && (!text || normal(`${p.name} ${p.sku ?? ''}`).includes(text)));
    const pages = Math.max(1, Math.ceil(list.length / PAGE));
    ui.page = Math.min(ui.page, pages - 1);
    const page = list.slice(ui.page * PAGE, (ui.page + 1) * PAGE);
    grid.replaceChildren(...(page.length ? page.map(p => h('button', { type: 'button', class: 'tile', 'data-id': p.id, title: p.name, 'aria-label': p.name, onclick: () => set(p.id, (cart.get(p.id) ?? 0) + 1) },
      h('span', { class: 'tile-photo' },
        p.photo ? h('img', { src: photoUrl(p, branchId), alt: '', loading: 'lazy' }) : h('span', { class: 'tile-fallback' }, icon(BREAD), h('small', { text: 'Sin imagen' })),
        h('span', { class: 'count-badge', hidden: !cart.get(p.id), text: String(cart.get(p.id) ?? '') })),
      h('span', { class: 'tile-name', text: p.name }),
      h('span', { class: 'tile-price', text: fmt(p.price) })))
      : [h('p', { class: 'tiles-empty', text: ui.q ? `No hay productos para «${ui.q}».` : data.products.length ? 'No hay productos en esta categoría.' : 'No hay productos activos en el catálogo de esta sucursal.' })]));
    pager.replaceChildren(h('span', { class: 'caption', text: `${list.length} productos · página ${ui.page + 1}` }),
      h('span', { class: 'pager-buttons' },
        h('button', { type: 'button', class: 'secondary', disabled: ui.page === 0, onclick: () => { ui.page--; drawGrid(); } }, 'Anterior'),
        h('button', { type: 'button', class: 'secondary', disabled: (ui.page + 1) * PAGE >= list.length, onclick: () => { ui.page++; drawGrid(); } }, 'Siguiente')));
  };

  // Ticket a la derecha (en celular, hoja desde la barra de abajo).
  const ticket = h('aside', { class: 'pos-ticket', 'aria-label': 'Ticket' });
  const bar = h('div', { class: 'pos-bar-mobile' });
  let sheet = null, closeSheet = null;
  const charge = print => { if (sheet?.isConnected) closeSheet(); checkout(data, lines(), total(), print, () => { cart.clear(); keepCart(); refresh(); }); };
  const cartLines = () => cart.size
    ? h('div', { class: 'cart', 'aria-label': 'Partidas del carrito' }, lines().map(({ product: p, quantity }) => h('div', { class: 'cart-line', 'aria-label': p.name },
      h('span', { class: 'line-photo' }, p.photo ? h('img', { src: photoUrl(p, branchId), alt: '' }) : null),
      h('span', { class: 'line-body' },
        h('strong', { class: 'line-name', text: p.name, title: p.name }),
        h('span', { class: 'caption', text: `${fmt(p.price)} / pieza` }),
        h('span', { class: 'line-controls' },
          h('span', { class: 'stepper-track' },
            h('button', { type: 'button', class: 'qty', 'aria-label': 'Disminuir cantidad', disabled: quantity <= 1, onclick: () => set(p.id, quantity - 1) }, '−'),
            h('b', { class: 'qty-value', text: String(quantity) }),
            h('button', { type: 'button', class: 'qty', 'aria-label': 'Incrementar cantidad', onclick: () => set(p.id, quantity + 1) }, '+')),
          h('button', { type: 'button', class: 'qty', 'aria-label': 'Eliminar', title: 'Quitar del ticket', onclick: () => set(p.id, 0) }, icon(ICON.trash)))),
      h('b', { class: 'line-total num', text: fmt(p.price * quantity) }))))
    : h('div', { class: 'cart-empty' }, icon(ICON.sale), h('strong', { text: 'Carrito vacío' }), h('span', { class: 'muted', text: 'Toca un pan para agregarlo.' }));
  const totals = () => h('div', { class: 'ticket-foot' },
    h('div', { class: 'ticket-sum' }, h('span', { text: 'Subtotal' }), h('span', { class: 'num', text: fmt(total()) })),
    h('div', { class: 'ticket-total' }, h('span', { text: 'Total' }), h('strong', { class: 'num', text: fmt(total()) })),
    h('button', { type: 'button', class: 'primary pay', disabled: !cart.size, title: 'Confirmar venta sin imprimir ticket', 'aria-label': 'Cobrar', onclick: () => charge(false) }, `Cobrar ${fmt(total())}`),
    h('button', { type: 'button', class: 'secondary pay-print', disabled: !cart.size, 'aria-label': 'Cobrar e imprimir ticket', onclick: () => charge(true) }, 'Cobrar + imprimir ticket'));
  const refresh = () => {
    for (const tile of grid.querySelectorAll('.tile')) {
      const q = cart.get(tile.dataset.id); const badge = tile.querySelector('.count-badge');
      badge.hidden = !q; badge.textContent = q ? String(q) : '';
    }
    ticket.replaceChildren(
      h('div', { class: 'ticket-head' },
        h('div', {}, h('h2', { text: 'Ticket' }), h('p', { class: 'caption', text: 'Venta temporal hasta confirmar el cobro' })),
        h('button', { type: 'button', class: 'ghost ticket-clear', disabled: !cart.size, 'aria-label': 'Limpiar', onclick: () => discard('limpiar el carrito', () => { cart.clear(); keepCart(); refresh(); }) }, icon(ICON.trash), h('span', { text: 'Limpiar' }))),
      h('div', { class: 'ticket-body' }, cartLines()), totals());
    if (sheet?.isConnected) sheet.replaceChildren(cartLines(), totals());
    bar.replaceChildren(
      h('button', { type: 'button', class: 'cart-open', disabled: !cart.size, onclick: () => {
        closeSheet = dialog('Ticket', body => { sheet = h('div', { class: 'wide sheet-cart' }); body.append(sheet); }).close; refresh();
      } }, h('span', { text: cart.size ? `${pieces()} pz` : 'Carrito vacío' }), h('strong', { class: 'num', text: fmt(total()) })),
      h('button', { type: 'button', class: 'primary', disabled: !cart.size, onclick: () => charge(false) }, 'Cobrar'));
  };
  drawChips(); drawGrid(); refresh();
  return h('div', { class: 'pos' },
    h('section', { class: 'pos-main' }, searchRow, chips, h('div', { class: 'tiles-scroll' }, grid), pager,
      h('p', { class: 'caption pos-operator', text: `Usuario: ${state.me.name} · Terminal: ${data.till.name}` })),
    ticket, bar);
}

/** Confirmación para descartar el carrito (DiscardDialog de la caja). */
function discard(reason, done) {
  dialog('Descartar carrito', body => body.append(h('p', { class: 'wide muted', text: `Hay partidas en el carrito temporal. Para ${reason} debes descartarlas. No se registrará una venta.` })),
    { submit: 'Descartar', cancel: 'Conservar', onSubmit: async () => { done(); } });
}

// ---------- Cobro (CheckoutDialog) ----------

function checkout(data, lines, total, printAfter, done) {
  const signature = JSON.stringify(lines.map(l => [l.product.id, l.quantity]));
  let method = 'CASH', sale = null, busy = false, message = '', printMessage = '';
  const received = h('input', { name: 'received', inputmode: 'decimal', autocomplete: 'off', maxlength: '24', class: 'big-input', 'aria-label': 'Efectivo recibido' });
  const reference = h('input', { name: 'reference', maxlength: '40', autocomplete: 'off', 'aria-label': 'Referencia del pago' });
  const node = h('dialog', { class: 'checkout', 'aria-label': 'Cobro' });
  const close = () => { node.close(); node.remove(); if (sale) done(); };
  const cents = () => money(received.value);
  const canConfirm = () => !busy && !sale && (method !== 'CASH' || (cents() !== null && cents() >= total));
  const draw = () => {
    const capture = !sale;
    const changeText = cents() !== null && cents() >= total ? fmt(cents() - total) : '—';
    node.replaceChildren(h('div', { class: 'checkout-card' },
      h('h2', { class: 'checkout-title', text: 'Cobro' }),
      capture ? h('div', { class: 'checkout-capture' },
        h('div', { class: 'checkout-total' }, h('span', { text: 'Total a cobrar' }), h('strong', { class: 'num', text: fmt(total) })),
        h('p', { class: 'label', text: 'Forma de pago' }),
        h('div', { class: 'segmented wide-seg', role: 'radiogroup', 'aria-label': 'Forma de pago' }, Object.entries(METHOD).map(([id, label]) =>
          h('button', { type: 'button', role: 'radio', 'aria-checked': String(method === id), 'aria-label': `Pago ${id === 'CASH' ? 'en efectivo' : id === 'CARD' ? 'con tarjeta' : 'por transferencia'}`,
            onclick: () => { method = id; message = ''; draw(); (id === 'CASH' ? received : reference).focus(); } }, label))),
        method === 'CASH'
          ? h('div', {}, h('label', { class: 'label', text: 'Efectivo recibido (MXN)' }), received,
            h('div', { class: 'checkout-change' }, h('span', { text: 'Cambio' }), h('strong', { class: 'num', text: changeText })))
          : h('div', {}, h('label', { class: 'label', text: 'Referencia (opcional) · últimos 4 dígitos, autorización o folio' }), reference),
        h('p', { class: 'caption instructions', text: method === 'CASH' ? 'Confirma sólo después de recibir el efectivo. Volver conserva el carrito.'
          : 'Confirma sólo cuando la terminal bancaria o la transferencia muestre el pago aprobado por el total exacto.' }))
        : h('div', { class: 'checkout-result' },
          h('p', { class: 'result-text', text: resultText(sale) }),
          h('p', { class: 'muted', text: printMessage }),
          h('button', { type: 'button', class: 'secondary', onclick: () => { printTicket(sale); printMessage = 'Ticket enviado a la impresión del navegador.'; draw(); } }, printAfter ? 'Reintentar ticket' : 'Imprimir ticket')),
      h('p', { class: 'error', role: 'alert', text: message }),
      h('div', { class: 'checkout-actions' + (capture ? '' : ' done') },
        h('button', { type: 'button', class: 'secondary', disabled: busy, onclick: close }, 'Volver al POS'),
        capture ? h('button', { type: 'button', class: 'primary', disabled: !canConfirm(), 'aria-label': 'Cobrar sin imprimir ticket', onclick: () => confirm(false) }, 'Cobrar') : null,
        capture ? h('button', { type: 'button', class: 'primary', disabled: !canConfirm(), 'aria-label': 'Cobrar e imprimir ticket', onclick: () => confirm(true) }, 'Cobrar + imprimir ticket') : null)));
  };
  // Al escribir sólo se actualizan el cambio y los botones (sin redibujar el campo que se está usando).
  received.addEventListener('input', () => {
    const value = node.querySelector('.checkout-change strong'); if (value) value.textContent = cents() !== null && cents() >= total ? fmt(cents() - total) : '—';
    for (const b of node.querySelectorAll('.checkout-actions .primary')) b.disabled = !canConfirm();
  });
  received.addEventListener('keydown', e => { if (e.key === 'Enter' && canConfirm()) confirm(printAfter); });
  const confirm = async print => {
    if (!canConfirm()) return;
    busy = true; message = ''; draw();
    const cashCents = method === 'CASH' ? cents() : null;
    const key = `${signature}|${method}|${cashCents}|${reference.value.trim()}`;
    if (!pending || pending.key !== key) pending = { key, id: crypto.randomUUID() };
    try {
      sale = await post('pos-sale', { tillId: data.till.id, sessionId: data.session.id, operationId: pending.id, method,
        lines: lines.map(l => ({ productId: l.product.id, quantity: l.quantity })), receivedCents: cashCents, reference: method === 'CASH' ? null : reference.value, expectedTotal: total });
      pending = null;
      printAfter = print;
      if (print) { printMessage = 'Ticket enviado a la impresión del navegador.'; setTimeout(() => printTicket(sale), 50); }
      else printMessage = 'Venta cobrada correctamente. Quedó registrada en Historial sin imprimir ticket.';
    } catch (error) {
      message = error.status === 0 ? 'No se pudo completar o verificar el cobro. Revisa la conexión y vuelve a tocar Cobrar: si ya se había cobrado, no se duplica.' : error.message;
      if (error.status === 409) setTimeout(() => render({ silent: true }));
    } finally { busy = false; draw(); }
  };
  node.addEventListener('cancel', e => { e.preventDefault(); if (!busy) close(); });
  draw();
  document.body.append(node); node.showModal();
  setTimeout(() => received.focus());
}
function resultText(r) {
  return r.method === 'CASH'
    ? `Venta confirmada\nFolio: ${r.folio}\nTotal: ${fmt(r.total)}\nRecibido: ${fmt(r.received)}\nCambio: ${fmt(r.change)}`
    : `Venta confirmada\nFolio: ${r.folio}\nTotal: ${fmt(r.total)}\nPago: ${METHOD[r.method] ?? r.method}${r.reference ? ` · Ref. ${r.reference}` : ''}`;
}

// ---------- Ticket impreso ----------

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

// ---------- Tu caja ----------

// Pantalla dentro de «Tu caja» (como la caja: tablero, abrir, conteo y resultado). Se conserva al redibujar.
const cash = { screen: 'dashboard', till: null, historyPage: 0, selected: null, detail: null };
const MOVE = { EXPENSE: 'Gasto', DEPOSIT: 'Entrada', WITHDRAWAL: 'Retiro' };
const MOVE_HINT = { EXPENSE: 'Efectivo que sale para pagar algo del negocio.', DEPOSIT: 'Efectivo que entra y no es venta: cambio que se trae o fondo extra.',
  WITHDRAWAL: 'Efectivo que sale a resguardo: depósito al banco o entrega al dueño. No es gasto.' };
const MOVE_ACTION = { EXPENSE: 'Registrar gasto', DEPOSIT: 'Registrar entrada', WITHDRAWAL: 'Registrar retiro' };
const CATEGORY = { SUPPLIES: 'Insumos', GAS: 'Gas', CLEANING: 'Limpieza', TRANSPORT: 'Transporte', MAINTENANCE: 'Mantenimiento', OTHER: 'Otro' };
const move = { kind: 'EXPENSE', category: null, amount: '', note: '', message: '', ok: false };
// Operación pendiente por contenido: reintentar lo mismo (por ejemplo, tras una falla de red) nunca lo registra dos veces.
export const pendingOps = new Map();
export const opFor = key => { if (!pendingOps.has(key)) pendingOps.set(key, crypto.randomUUID()); return pendingOps.get(key); };
export const stamp = iso => {
  // dd/MM/yyyy HH:mm:ss -06:00, como «Hora de apertura» en la caja.
  const d = new Date(iso); const parts = Object.fromEntries(new Intl.DateTimeFormat('en-GB', { timeZone: state.timeZone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23', timeZoneName: 'longOffset' }).formatToParts(d).map(p => [p.type, p.value]));
  return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}:${parts.second} ${(parts.timeZoneName || '').replace('GMT', '') || '+00:00'}`;
};
const differenceLabel = d => d === 0 ? 'Diferencia: $0.00 · Caja exacta' : d > 0 ? `SOBRANTE ${fmt(d)}` : `FALTANTE ${fmt(-d)}`;

export async function tuCaja(ctx) {
  const data = await loadTill(ctx);
  if (cash.till !== data.till?.id) { cash.till = data.till?.id ?? null; cash.screen = 'dashboard'; }
  if (!data.till) return chooseTill(data);
  const s = data.session;
  if (!s && cash.screen !== 'opening') cash.screen = 'dashboard';
  if (s?.count && cash.screen !== 'dashboard') cash.screen = 'result';
  if (s && !s.count && cash.screen === 'opening') cash.screen = 'dashboard';
  if (cash.screen === 'opening') return opening(data);
  if (cash.screen === 'counting' && s && !s.count) return counting(data);
  if (cash.screen === 'result' && s?.count) return closingResult(data);
  cash.screen = 'dashboard';
  const history = can('cash.view') ? await api('pos-closures', { till: data.till.id, page: cash.historyPage }).catch(() => null) : null;
  if (cash.detail) return closureDetail(cash.detail);
  return h('div', { class: 'cash-card' }, dashboard(data), history ? closures(history) : null);
}
const backTo = screen => { cash.screen = screen; render(); };
const owner = s => `Abierta por: ${s.openedBy || '—'}${s.mine ? ' · Tu sesión' : ' · Cierre por otro usuario'}`;

function dashboard(data) {
  setHeading('Tu caja', 'Caja web · lo que registras se guarda en la nube al momento.');
  const s = data.session;
  const canClose = s && (s.mine ? can('cash.close') : can('cash.close.other'));
  const status = h('div', { class: 'cash-status' },
    h('p', { class: 'eyebrow', text: s ? 'CAJA ABIERTA' : 'CAJA CERRADA' }),
    h('div', { class: 'cash-state' }, h('i', { class: 'state-dot' + (s ? ' ok' : '') }), h('span', { text: s ? 'Caja abierta' : 'Caja cerrada' })),
    h('div', { class: 'cash-details', text: s
      ? `Abierta por: ${s.openedBy || '—'}\nHora de apertura: ${stamp(s.openedAt)}\n\n${s.count ? 'Conteo confirmado: falta confirmar el cierre.' : 'Importes ocultos hasta confirmar el conteo ciego. Las ventas en efectivo, los gastos, los retiros y las entradas se incluyen en el cierre.'}`
      : 'Captura el fondo inicial para comenzar.' }),
    !s ? h('button', { type: 'button', class: 'primary big', disabled: !can('cash.open'), onclick: () => backTo('opening') }, 'Abrir caja') : null,
    !s && !can('cash.open') ? h('p', { class: 'caption', text: 'Tu rol no puede abrir la caja; pide al encargado que la abra.' }) : null,
    canClose ? h('button', { type: 'button', class: 'danger-solid big', onclick: () => backTo(s.count ? 'result' : 'counting') }, 'Cerrar caja') : null,
    s && !canClose ? h('p', { class: 'caption', text: s.count ? 'Ya se hizo el conteo de esta caja; falta que el encargado confirme el cierre.' : 'El cierre lo hace quien abrió la caja o el encargado.' }) : null,
    h('p', { class: 'caption', text: 'Salir del usuario o cerrar el navegador no cierra la caja.' }),
    // Sólo en la web: varias cajas por sucursal; cada dispositivo elige la suya.
    h('div', { class: 'cash-till' }, h('span', { class: 'caption', text: `Esta caja: ${data.till.name} · folios ${data.till.code}-000001…` }),
      s ? h('button', { type: 'button', class: 'ghost', onclick: () => ticketsDialog(data) }, 'Ventas de este turno') : null,
      h('button', { type: 'button', class: 'ghost', onclick: () => { saved.set(tillKey(data.branchId), null); render(); } }, 'Cambiar de caja')));
  return h('div', { class: 'cash-wrap' }, status, s && !s.count && can('cash.movement') ? movements(data) : null);
}

const closureDate = iso => new Date(iso).toLocaleString('en-GB', { timeZone: state.timeZone, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).replace(', ', ' · ');
const moneyOrNone = value => value === null || value === undefined ? 'No disponible' : fmt(value);
function closures(history) {
  const pages = Math.max(1, Math.ceil(history.total / history.pageSize));
  const open = row => { cash.detail = row; render({ silent: true }); };
  const rows = history.items.map(x => {
    const tr = h('tr', { class: 'click' + (cash.selected === x.id ? ' selected' : ''), tabindex: '0',
      onclick: () => { if (cash.selected === x.id) return open(x); cash.selected = x.id; for (const r of tr.parentNode.children) r.classList.toggle('selected', r === tr); },
      ondblclick: () => open(x), onkeydown: e => { if (e.key === 'Enter') open(x); } },
      h('td', { 'data-label': 'Cierre', text: closureDate(x.closedAt) }), h('td', { 'data-label': 'Caja', text: x.terminal }), h('td', { 'data-label': 'Responsable', text: x.closedBy }),
      h('td', { class: 'num', 'data-label': 'Esperado', text: moneyOrNone(x.expected) }), h('td', { class: 'num', 'data-label': 'Contado', text: moneyOrNone(x.counted) }),
      h('td', { 'data-label': 'Diferencia', text: x.difference === null ? 'No disponible' : differenceLabel(x.difference) }));
    return tr;
  });
  return h('section', { class: 'closures' },
    h('h2', { class: 'closures-title', text: 'Cierres anteriores' }), h('p', { class: 'caption', text: 'Sólo lectura · Cierres de esta caja' }),
    history.total ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Cierres anteriores' },
      h('thead', {}, h('tr', {}, ['Cierre', 'Caja', 'Responsable', 'Esperado', 'Contado', 'Diferencia'].map((c, i) => h('th', { class: i === 3 || i === 4 ? 'num' : null, text: c })))),
      h('tbody', {}, rows))) : h('p', { text: 'Todavía no hay cierres registrados en esta caja.', style: { marginTop: '6px' } }),
    history.total ? h('div', { class: 'history-foot' }, h('span', { text: `${history.total} ${history.total === 1 ? 'cierre' : 'cierres'} · página ${cash.historyPage + 1} de ${pages}` }),
      h('span', { class: 'history-actions' },
        h('button', { type: 'button', class: 'secondary', disabled: cash.historyPage === 0, onclick: () => { cash.historyPage--; cash.selected = null; render({ silent: true }); } }, 'Anterior'),
        h('button', { type: 'button', class: 'secondary', disabled: cash.historyPage + 1 >= pages, onclick: () => { cash.historyPage++; cash.selected = null; render({ silent: true }); } }, 'Siguiente'),
        h('button', { type: 'button', class: 'primary', onclick: () => { const x = history.items.find(r => r.id === cash.selected); if (x) open(x); else toast('Selecciona un cierre.'); } }, 'Ver cierre'))) : null);
}
function closureDetail(x) {
  setHeading('Tu caja', 'Detalle de un cierre anterior · Sólo lectura.');
  const box = (title, lines) => h('div', { class: 'closure-box' }, h('strong', { text: title }), lines.map(l => h('p', { text: l })));
  return h('div', { class: 'cash-card' }, h('div', { class: 'closure-detail' },
    h('h2', { class: 'closures-title', text: 'Detalle del cierre' }), h('p', { class: 'caption', text: 'Sólo lectura', style: { marginBottom: '14px' } }),
    h('div', { class: 'closure-boxes' },
      box('Identificación', [`Caja: ${x.terminal}`, `Apertura: ${closureDate(x.openedAt)}`, `Abierta por: ${x.openedBy}`, `Cierre: ${closureDate(x.closedAt)}`, `Cerrada por: ${x.closedBy}`, `Conteo realizado por: ${x.countedBy}`]),
      box('Resumen financiero', [`Fondo inicial: ${fmt(x.opening)}`, `Ventas en efectivo: ${x.expected === null ? 'No disponible' : fmt(x.expected - x.opening)}`, `Efectivo esperado: ${moneyOrNone(x.expected)}`,
        `Efectivo contado: ${moneyOrNone(x.counted)}`, x.difference === null ? 'No disponible' : differenceLabel(x.difference)]),
      box('Nota de discrepancia', [x.note ?? 'Sin nota registrada'])),
    h('div', { class: 'toolbar', style: { marginTop: '14px' } }, h('button', { type: 'button', class: 'secondary', onclick: () => { cash.detail = null; render({ silent: true }); } }, 'Volver'))));
}

function chooseTill(data) {
  setHeading('Tu caja', 'Elige la caja web de este dispositivo.');
  const pick = id => { saved.set(tillKey(data.branchId), id); render(); };
  const create = async e => {
    e.target.disabled = true;
    try { const t = await post('pos-till', { branchId: data.branchId }); toast(`Caja ${t.name} creada (folios ${t.code}).`); pick(t.id); }
    catch (error) { toast(error.message); e.target.disabled = false; }
  };
  return h('div', { class: 'cash-card' },
    h('p', { class: 'eyebrow', text: 'CAJA DE ESTE DISPOSITIVO' }),
    h('h2', { class: 'section-title', text: '¿Qué caja es este dispositivo?' }),
    h('p', { class: 'caption', text: 'Cada caja web tiene su propio turno, fondo y folios. Usa una por dispositivo (por ejemplo, la tableta del mostrador).', style: { margin: '4px 0 16px' } }),
    data.tills.length ? h('div', { class: 'till-list' }, data.tills.map(t => h('button', { type: 'button', class: 'till secondary', onclick: () => pick(t.id) },
      h('strong', { text: t.name }), h('span', { class: 'caption', text: `Folios ${t.code}-000001…` }),
      h('span', { class: 'pill ' + (t.counted ? 'warn' : t.open ? 'ok' : ''), text: t.counted ? 'Contada · falta cerrar' : t.open ? 'Caja abierta' : 'Caja cerrada' }))))
      : h('p', { class: 'empty', text: can('users.manage') ? 'Todavía no hay cajas web en esta sucursal.' : 'Todavía no hay cajas web en esta sucursal. Pide al administrador que cree una.' }),
    can('users.manage') ? h('div', { class: 'toolbar', style: { marginTop: '16px' } }, h('button', { type: 'button', class: data.tills.length ? 'secondary' : 'primary', onclick: create }, '+ Nueva caja web')) : null);
}

function opening(data) {
  setHeading('Abrir caja', 'Registra el efectivo con el que comienza la sesión.');
  const input = h('input', { inputmode: 'decimal', autocomplete: 'off', maxlength: '20', class: 'big-input', 'aria-label': 'Fondo inicial en MXN' });
  const error = h('p', { class: 'error', role: 'alert' });
  const open = async e => {
    e.preventDefault();
    const cents = money(input.value);
    if (cents === null) { error.textContent = 'Escribe el fondo inicial, por ejemplo 500.00 (o 0).'; return; }
    const button = e.target.querySelector('button[type=submit]'); button.disabled = true;
    try { await post('pos-open', { tillId: data.till.id, openingCents: cents }); cash.screen = 'dashboard'; toast('Caja abierta.'); render(); }
    catch (err) { error.textContent = err.message; button.disabled = false; }
  };
  setTimeout(() => input.focus());
  return h('div', { class: 'cash-card' }, h('form', { class: 'cash-form', onsubmit: open },
    h('label', { class: 'label', text: 'Fondo inicial en MXN' }), input,
    h('p', { class: 'caption', text: 'Ejemplo: 500.00 · Se permite iniciar con $0.00' }), error,
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: () => backTo('dashboard') }, 'Cancelar'), h('button', { type: 'submit', class: 'primary' }, 'Confirmar apertura'))));
}

function counting(data) {
  setHeading('Conteo de caja', 'Cuenta el efectivo sin consultar el esperado. Continuar guarda tu conteo y no permite cambiarlo después.');
  const input = h('input', { inputmode: 'decimal', autocomplete: 'off', maxlength: '20', class: 'big-input', 'aria-label': 'Efectivo contado en MXN' });
  const error = h('p', { class: 'error', role: 'alert' });
  const submit = async e => {
    e.preventDefault();
    const cents = money(input.value);
    if (cents === null) { error.textContent = 'Escribe lo contado, por ejemplo 1250.50.'; return; }
    const button = e.target.querySelector('button[type=submit]'); button.disabled = true;
    try { await post('pos-count', { tillId: data.till.id, sessionId: data.session.id, countedCents: cents }); cash.screen = 'result'; render(); }
    catch (err) { error.textContent = err.message; button.disabled = false; }
  };
  setTimeout(() => input.focus());
  return h('div', { class: 'cash-card' }, h('form', { class: 'cash-form', onsubmit: submit },
    h('p', { class: 'muted', text: owner(data.session), style: { marginBottom: '16px' } }),
    h('label', { class: 'label', text: 'Efectivo contado en MXN' }), input, error,
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: () => backTo('dashboard') }, 'Cancelar'), h('button', { type: 'submit', class: 'primary' }, 'Continuar'))));
}

function closingResult(data) {
  setHeading('Resultado de caja', 'Revisa el resultado y confirma el cierre definitivo.');
  const s = data.session, c = s.count;
  const sum = kind => (s.movements ?? []).filter(m => m.kind === kind).reduce((a, m) => a + m.amount, 0);
  const [expenses, withdrawals, deposits] = [sum('EXPENSE'), sum('WITHDRAWAL'), sum('DEPOSIT')];
  const details = [`Fondo inicial: ${fmt(c.opening)}`, c.cashRefunds > 0 ? `Devoluciones en efectivo: −${fmt(c.cashRefunds)}` : null,
    expenses > 0 ? `Gastos: −${fmt(expenses)}` : null, withdrawals > 0 ? `Retiros: −${fmt(withdrawals)}` : null, deposits > 0 ? `Entradas: +${fmt(deposits)}` : null,
    `Efectivo esperado: ${fmt(c.expected)}`, `Efectivo contado: ${fmt(c.counted)}`, differenceLabel(c.difference)].filter(Boolean).join('\n');
  const canClose = s.mine ? can('cash.close') : can('cash.close.other');
  const note = h('textarea', { maxlength: '500', rows: '3', 'aria-label': 'Nota de discrepancia' });
  const counter = h('p', { class: 'caption', text: '0/500 caracteres' });
  const error = h('p', { class: 'error', role: 'alert' });
  const confirm = h('button', { type: 'button', class: 'danger-solid', disabled: !canClose || c.difference !== 0, onclick: async e => {
    e.target.disabled = true;
    try { await post('pos-close', { tillId: data.till.id, sessionId: s.id, discrepancyNote: note.value.trim() }); cash.screen = 'dashboard'; toast('Caja cerrada.'); render(); }
    catch (err) { error.textContent = err.message; e.target.disabled = false; }
  } }, 'Confirmar cierre');
  note.addEventListener('input', () => { counter.textContent = `${note.value.length}/500 caracteres`; confirm.disabled = !canClose || !note.value.trim(); });
  return h('div', { class: 'cash-card' }, h('div', { class: 'cash-form wide-form' },
    h('p', { class: 'muted', text: owner(s), style: { marginBottom: '16px' } }),
    h('div', { class: 'cash-details big', text: details }),
    h('p', { text: 'La sesión quedará cerrada y auditada. No podrá volver a abrirse. Volver conserva el conteo confirmado.', style: { margin: '0 0 20px' } }),
    c.difference === 0 ? h('p', { class: 'caption', text: 'Sin diferencia. No se requiere nota.', style: { fontSize: '15px', marginBottom: '12px' } })
      : h('div', { class: 'note-editor' }, h('strong', { text: 'Nota de discrepancia · obligatoria' }), h('p', { class: 'caption', text: 'Escribe una nota para habilitar el cierre.' }), note, counter),
    !canClose ? h('p', { class: 'caption', text: 'El cierre lo confirma quien abrió la caja o el encargado.' }) : null,
    error,
    h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'secondary', onclick: () => backTo('dashboard') }, 'Volver'), confirm)));
}

/** Gastos, entradas y retiros de la caja abierta (CashMovementsView): tipo segmentado, categoría, importe y nota; abajo, lo del turno. */
function movements(data) {
  const s = data.session;
  if (move.kind === 'WITHDRAWAL' && !can('cash.withdrawal')) move.kind = 'EXPENSE';
  const amount = h('input', { inputmode: 'decimal', autocomplete: 'off', maxlength: '12', value: move.amount, class: 'amount-input', 'aria-label': 'Importe del movimiento', oninput: e => { move.amount = e.target.value; } });
  const note = h('input', { maxlength: '200', value: move.note, placeholder: move.category === 'OTHER' ? 'Nota (obligatoria con «Otro»)' : 'Nota (opcional)', 'aria-label': 'Nota del movimiento', oninput: e => { move.note = e.target.value; } });
  const record = async e => {
    move.message = ''; move.ok = false;
    const text = String(move.amount).trim().replace(/[$\s]/g, '').replace(',', '.');
    if (!/^\d{1,7}(\.\d{1,2})?$/.test(text) || Number(text) <= 0) { move.message = 'Escribe un importe mayor a cero, por ejemplo 150.00.'; return render({ silent: true }); }
    if (move.kind === 'EXPENSE' && !move.category) { move.message = 'Elige la categoría del gasto.'; return render({ silent: true }); }
    const amountCents = Math.round(Number(text) * 100);
    const key = `move|${s.id}|${move.kind}|${move.category}|${amountCents}|${move.note.trim()}`;
    e.target.disabled = true;
    try {
      await post('pos-movement', { tillId: data.till.id, sessionId: s.id, operationId: opFor(key), kind: move.kind, category: move.kind === 'EXPENSE' ? move.category : null, amountCents, note: move.note.trim() });
      pendingOps.delete(key);
      move.message = `${MOVE[move.kind]} de ${fmt(amountCents)} registrado.`; move.ok = true; move.amount = ''; move.note = ''; move.category = null;
    } catch (error) {
      move.message = error.status === 0 ? 'No se pudo confirmar. Revisa la conexión y vuelve a tocar el botón: si ya se registró, no se duplica.' : error.message;
    }
    render({ silent: true });
  };
  const list = s.movements ?? [];
  const sum = kind => list.filter(m => m.kind === kind).reduce((a, m) => a + m.amount, 0);
  return h('section', { class: 'moves' },
    h('h2', { class: 'section-title', text: 'Movimientos de efectivo' }),
    h('p', { class: 'caption', text: 'Cambian el efectivo esperado del corte. Un error se corrige con el movimiento contrario.', style: { margin: '2px 0 14px' } }),
    h('div', { class: 'segmented', role: 'radiogroup', 'aria-label': 'Tipo de movimiento' }, Object.entries(MOVE).filter(([k]) => k !== 'WITHDRAWAL' || can('cash.withdrawal')).map(([k, label]) =>
      h('button', { type: 'button', role: 'radio', 'aria-checked': String(move.kind === k), 'aria-label': `Movimiento: ${label}`, onclick: () => { move.kind = k; if (k !== 'EXPENSE') move.category = null; move.message = ''; render({ silent: true }); } }, label))),
    h('p', { class: 'caption', text: MOVE_HINT[move.kind], style: { margin: '10px 0 12px' } }),
    move.kind === 'EXPENSE' ? h('div', {}, h('p', { class: 'label', text: 'Categoría' }), h('div', { class: 'category-tabs' }, Object.entries(CATEGORY).map(([k, label]) =>
      h('button', { type: 'button', class: 'tab', 'aria-pressed': String(move.category === k), 'aria-label': `Categoría: ${label}`, onclick: () => { move.category = k; move.message = ''; render({ silent: true }); } }, label)))) : null,
    h('div', { class: 'move-fields' }, h('label', { class: 'label' }, 'Importe', amount), h('label', { class: 'label' }, 'Nota', note)),
    h('button', { type: 'button', class: 'primary', style: { minWidth: '200px' }, 'aria-label': 'Registrar movimiento', onclick: record }, MOVE_ACTION[move.kind]),
    move.message ? h('p', { class: move.ok ? 'success' : 'error', role: 'status', text: move.message, style: { marginTop: '10px' } }) : null,
    h('div', { class: 'moves-list' },
      h('div', { class: 'moves-head' }, h('strong', { text: 'En este turno' }), list.length ? h('span', { class: 'caption', text: `Gastos ${fmt(sum('EXPENSE'))} · Retiros ${fmt(sum('WITHDRAWAL'))} · Entradas ${fmt(sum('DEPOSIT'))}` }) : null),
      list.length ? list.map(m => h('div', { class: 'move-row' },
        h('span', { class: 'caption num', text: time(m.at) }),
        h('span', { class: 'move-kind' + (m.kind === 'DEPOSIT' ? ' income' : ''), text: MOVE[m.kind] ?? m.kind }),
        h('span', { class: 'move-detail', text: [m.category ? CATEGORY[m.category] : '', m.note, m.by].filter(Boolean).join(' · ') || '—', title: [m.note, m.by].filter(Boolean).join(' · ') }),
        h('b', { class: 'num', text: `${m.kind === 'DEPOSIT' ? '+' : '−'}${fmt(m.amount)}` })))
        : h('p', { class: 'caption', text: 'Sin gastos, retiros ni entradas todavía.' })));
}

// ---------- Ventas del turno: reimprimir, cancelar y devolver ----------

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
  dialog('Ventas de este turno', body => {
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
        h('div', { class: 'line' }, h('span', { text: `${KIND_LABEL[x.kind] ?? x.kind} · ${time(x.at)} · ${x.by || '—'} · «${x.reason}»` }), h('b', { class: 'short', text: `−${fmt(x.total)}` })))) : '',
      refunds && t.blocker ? h('p', { class: 'notice wide', text: t.blocker }) : '',
      h('div', { class: 'wide toolbar' },
        can('sales.receipt.reprint') ? h('button', { type: 'button', class: 'secondary', onclick: () => printTicket(r, true) }, 'REIMPRIMIR TICKET') : null,
        refunds && t.canCancel ? h('button', { type: 'button', class: 'danger', onclick: () => { close(); refundDialog(data, t, 'CANCELLATION'); } }, 'Cancelar venta') : null,
        refunds && t.canReturn ? h('button', { type: 'button', class: 'secondary', onclick: () => { close(); refundDialog(data, t, 'RETURN'); } }, 'Devolver productos') : null));
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
