// Historial de ventas como la caja de escritorio (SalesHistoryView de QAv1): folio, días locales, Hoy / Ayer / Sesión actual,
// tabla de 50 por página y detalle con reimpresión, cancelaciones y devoluciones. Las reglas de las devoluciones viven en la nube (lib/pos.ts).
import { api, can, dialog, fmt, h, METHOD, post, render, state } from './app.js';
import { opFor, pendingOps, printTicket, stamp, tillOf } from './pos.js';

// Estado de la pantalla (se conserva al redibujar, como el ViewModel de la caja).
const hist = { mode: 'all', folio: '', from: '', to: '', session: null, page: 0, selected: null, detail: null, message: '',
  reprint: '', refund: { message: '', ok: false, reason: '', back: new Map() } };
const today = () => new Date().toLocaleDateString('en-CA', { timeZone: state.timeZone });
const shiftDay = (day, days) => { const d = new Date(day + 'T12:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
const LABEL = { all: 'Todos', today: 'Hoy', yesterday: 'Ayer', range: 'Rango personalizado', session: 'Sesión actual', folio: 'Venta encontrada' };
const branchOf = () => state.branch || (state.me.admin ? (state.branches[0]?.id ?? '') : state.me.branchId);

export async function ventas() {
  if (hist.detail) return detailView(hist.detail);
  return listView();
}

async function load() {
  const params = { branch: state.branch, page: hist.page };
  if (hist.mode === 'folio') params.q = hist.folio.trim();
  if (['today', 'yesterday', 'range'].includes(hist.mode)) { params.from = hist.from; params.to = hist.to; }
  if (hist.mode === 'session') params.session = hist.session;
  return api('sales', params);
}
const go = (mode, extra = {}) => { Object.assign(hist, { mode, page: 0, selected: null, message: '' }, extra); render(); };

async function listView() {
  let data = { items: [], total: 0, pageSize: 50 };
  if (hist.mode !== 'session' || hist.session) data = await load();
  const folio = h('input', { maxlength: '64', value: hist.folio, 'aria-label': 'Folio', placeholder: 'C01-001061', oninput: e => { hist.folio = e.target.value; },
    onkeydown: e => { if (e.key === 'Enter') searchFolio(); } });
  const from = h('input', { type: 'date', value: hist.from, 'aria-label': 'Fecha desde', onchange: e => { hist.from = e.target.value; } });
  const to = h('input', { type: 'date', value: hist.to, 'aria-label': 'Fecha hasta', onchange: e => { hist.to = e.target.value; } });
  const searchFolio = () => { if (!hist.folio.trim()) { hist.message = 'Escribe un folio, por ejemplo C01-001061 o sólo 1061.'; return render(); } go('folio'); };
  const searchDates = () => {
    if (!hist.from || !hist.to) { hist.message = 'Captura las dos fechas.'; return render(); }
    if (hist.to < hist.from) { hist.message = 'La fecha final es anterior a la inicial.'; return render(); }
    go('range');
  };
  const currentSession = async () => {
    const till = await tillOf(branchOf());
    if (!till.session) { Object.assign(hist, { mode: 'session', session: null, page: 0, selected: null, message: 'No hay una sesión de caja abierta.' }); return render(); }
    go('session', { session: till.session.id });
  };
  const pages = data.total ? Math.ceil(data.total / data.pageSize) : 0;
  const open = sale => { hist.detail = sale.id; hist.reprint = ''; hist.refund = { message: '', ok: false, reason: '', back: new Map() }; render(); };
  const rows = data.items.map(s => {
    const tr = h('tr', { class: 'click' + (hist.selected === s.id ? ' selected' : ''), tabindex: '0', 'aria-selected': String(hist.selected === s.id),
      onclick: () => { if (hist.selected === s.id) return open(s); hist.selected = s.id; for (const r of tr.parentNode.children) r.classList.toggle('selected', r === tr); },
      ondblclick: () => open(s), onkeydown: e => { if (e.key === 'Enter') open(s); } },
      h('td', { 'data-label': 'Folio' }, h('strong', { text: s.folio })),
      h('td', { 'data-label': 'Fecha local', text: stamp(s.at) }),
      h('td', { class: 'num', 'data-label': 'Total', text: fmt(s.total) }),
      h('td', { class: 'num' + (s.refunded ? '' : ' phone-hide'), 'data-label': 'Devuelto', text: s.refunded ? fmt(s.refunded) : fmt(0) }),
      h('td', { 'data-label': 'Pago', text: METHOD[s.method] ?? s.method }),
      h('td', { 'data-label': 'Cajero actual', text: s.cashier || '—' }),
      h('td', { class: 'phone-hide', 'data-label': 'Terminal actual', text: s.terminal }),
      h('td', { class: 'mono phone-hide', 'data-label': 'CashSessionId', text: s.cashSessionId }));
    return tr;
  });
  return h('div', { class: 'cash-card history' },
    h('label', { class: 'history-label', text: 'Buscar por folio · por ejemplo C01-001061 o sólo 1061' }),
    h('div', { class: 'history-row' }, folio, h('button', { type: 'button', class: 'secondary', onclick: searchFolio }, 'Buscar folio')),
    h('label', { class: 'history-label', text: 'O buscar por días locales' }),
    h('div', { class: 'history-row dates' }, from, to, h('button', { type: 'button', class: 'secondary', onclick: searchDates }, 'Buscar fechas'),
      h('button', { type: 'button', class: 'secondary', onclick: () => go('all', { folio: '', from: '', to: '', session: null }) }, 'Limpiar')),
    h('div', { class: 'history-quick' },
      h('button', { type: 'button', class: 'secondary' + (hist.mode === 'today' ? ' active' : ''), onclick: () => go('today', { from: today(), to: today() }) }, 'Hoy'),
      h('button', { type: 'button', class: 'secondary' + (hist.mode === 'yesterday' ? ' active' : ''), onclick: () => { const y = shiftDay(today(), -1); go('yesterday', { from: y, to: y }); } }, 'Ayer'),
      can('pos.use') ? h('button', { type: 'button', class: 'secondary' + (hist.mode === 'session' ? ' active' : ''), onclick: currentSession }, 'Sesión actual') : null,
      h('span', { class: 'caption', text: `${LABEL[hist.mode]} · ${data.total} ${data.total === 1 ? 'venta' : 'ventas'}` })),
    h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Historial de ventas' },
      h('thead', {}, h('tr', {}, ['Folio', 'Fecha local', 'Total', 'Devuelto', 'Pago', 'Cajero actual', 'Terminal actual', 'CashSessionId'].map((c, i) => h('th', { class: i === 2 || i === 3 ? 'num' : null, text: c })))),
      h('tbody', {}, rows.length ? rows : h('tr', {}, h('td', { colspan: '8', class: 'empty', text: hist.mode === 'session' && !hist.session ? 'Abre la caja de este dispositivo para ver su sesión.' : 'Sin ventas con ese filtro.' }))))),
    h('div', { class: 'history-foot' },
      h('span', { text: pages ? `Página ${hist.page + 1} de ${pages} · ${data.total} ${data.total === 1 ? 'venta' : 'ventas'} · ${data.pageSize} por página` : '' }),
      h('span', { class: 'history-actions' },
        h('button', { type: 'button', class: 'secondary', disabled: hist.page === 0, onclick: () => { hist.page--; hist.selected = null; render(); } }, 'Anterior'),
        h('button', { type: 'button', class: 'secondary', disabled: hist.page + 1 >= pages, onclick: () => { hist.page++; hist.selected = null; render(); } }, 'Siguiente'),
        h('button', { type: 'button', class: 'primary', onclick: () => {
          const s = data.items.find(x => x.id === hist.selected);
          if (!s) { hist.message = 'Selecciona una venta.'; return render(); }
          open(s);
        } }, 'Ver detalle'))),
    hist.message ? h('p', { class: 'error', role: 'alert', text: hist.message }) : null);
}

// ---------- Detalle ----------

const KIND = { CANCELLATION: 'CANCELACIÓN', RETURN: 'DEVOLUCIÓN' };
async function detailView(id) {
  const s = await api('sale', { id, branch: state.branch });
  // Devoluciones: con la caja web de este dispositivo y su turno abierto (como en la caja: el efectivo sale de la caja abierta).
  let till = null, options = null, blocker = '';
  if (can('sales.refund')) {
    try {
      till = await tillOf(s.branchId);
      if (!till.till) blocker = 'Elige la caja de este dispositivo en Caja para registrar cancelaciones o devoluciones.';
      else options = await api('pos-ticket', { till: till.till.id, id });
    } catch (error) { blocker = error.message; }
    blocker ||= options?.blocker ?? '';
  }
  const receipt = { folio: s.folio, branch: s.branch, at: s.at, cashier: s.cashier, till: s.terminal, total: s.total, method: s.payment?.method ?? 'CASH',
    received: s.payment?.received ?? s.total, change: s.payment?.change ?? 0, reference: s.payment?.reference || null,
    items: s.items.map(i => ({ name: i.name, quantity: i.quantity, unitPrice: i.unitPrice, total: i.total })) };
  const names = new Map(s.items.map(i => [i.position, i.name]));
  const refunded = s.refunds.reduce((a, r) => a + r.total, 0);
  const history = s.refunds.length
    ? s.refunds.map(r => `${KIND[r.kind] ?? r.kind} · ${stamp(r.at).slice(0, 16)} · ${fmt(r.total)} · ${r.by} · ${r.reason} · ${r.items.map(i => `${i.quantity} × ${names.get(i.position) ?? '—'}`).join(', ')}`).join('\n') + `\nTotal devuelto: ${fmt(refunded)}`
    : 'Sin cancelaciones ni devoluciones.';
  const method = METHOD[s.payment?.method] ?? 'Efectivo';
  const cash = !s.payment || s.payment.method === 'CASH';
  const r = hist.refund;
  const lines = options?.lines ?? [];
  const back = position => r.back.get(position) ?? 0;
  const confirmAndSend = async kind => {
    const reason = r.reason.trim();
    r.message = ''; r.ok = false;
    if (reason.length < 3) { r.message = 'Escribe el motivo (de 3 a 200 caracteres).'; return render(); }
    const pieces = kind === 'RETURN' ? lines.filter(l => back(l.position) > 0).map(l => ({ position: l.position, quantity: back(l.position) })) : [];
    if (kind === 'RETURN' && !pieces.length) { r.message = 'Indica en «A devolver» al menos una pieza.'; return render(); }
    const text = kind === 'CANCELLATION'
      ? `Se cancelará la venta ${s.folio}. ${instruction(s.payment?.method ?? 'CASH', s.total)} No se puede deshacer. ¿Continuar?`
      : `${cash ? 'Se registrará la devolución de las piezas indicadas y se entregará el efectivo correspondiente.' : `Se registrará la devolución de las piezas indicadas y se reembolsará por ${method.toLowerCase()}; no sale efectivo de la caja.`} No se puede deshacer. ¿Continuar?`;
    dialog(kind === 'CANCELLATION' ? 'Cancelar venta completa' : 'Devolver piezas indicadas', body => body.append(h('p', { class: 'wide', text })), { submit: 'Continuar', onSubmit: async () => {
      const key = `refund|${s.id}|${kind}|${JSON.stringify(pieces)}|${reason}`;
      try {
        const done = await post('pos-refund', { tillId: till.till.id, sessionId: till.session?.id, operationId: opFor(key), saleId: s.id, kind, lines: kind === 'RETURN' ? pieces : undefined, reason });
        pendingOps.delete(key);
        Object.assign(r, { ok: true, reason: '', back: new Map(), message: `${kind === 'CANCELLATION' ? 'Venta cancelada' : 'Devolución registrada'}. ${done.instruction}` });
      } catch (error) {
        r.ok = false;
        r.message = error.status === 0 ? 'No se pudo registrar la devolución. Revisa la conexión y vuelve a intentarlo: si ya se registró, no se duplica.' : error.message;
      }
      render({ silent: true });
    } });
  };
  return h('div', { class: 'cash-card history' },
    h('h2', { class: 'detail-title', text: `Venta ${s.folio}` }),
    h('p', { class: 'detail-text', text: `Fecha: ${stamp(s.at)}\nFolio interno: ${s.internalFolio || '—'}\nCajero actual: ${s.cashier || '—'}\nUserId: ${s.userId}\nTerminal actual: ${s.terminal}\nTerminalId: ${s.terminalId}\nCashSessionId: ${s.cashSessionId}\nSaleId: ${s.id}` }),
    h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Partidas de venta' },
      h('thead', {}, h('tr', {}, ['#', 'ProductId', 'Nombre vendido', 'Precio', 'Cantidad', 'Total línea'].map((c, i) => h('th', { class: i >= 3 ? 'num' : null, text: c })))),
      h('tbody', {}, s.items.map(i => h('tr', {},
        h('td', { 'data-label': '#', text: String(i.position) }), h('td', { class: 'mono', 'data-label': 'ProductId', text: i.productId }),
        h('td', { 'data-label': 'Nombre vendido', text: i.name }), h('td', { class: 'num', 'data-label': 'Precio', text: fmt(i.unitPrice) }),
        h('td', { class: 'num', 'data-label': 'Cantidad', text: String(i.quantity) }), h('td', { class: 'num', 'data-label': 'Total línea', text: fmt(i.total) })))))),
    h('p', { class: 'detail-payment', text: `Subtotal: ${fmt(s.subtotal)}\nTotal: ${fmt(s.total)}\nMétodo: ${method}${s.payment?.reference ? ` · Ref. ${s.payment.reference}` : ''}\nAplicado: ${fmt(s.payment?.applied ?? s.total)}\nRecibido: ${fmt(s.payment?.received ?? s.total)}\nCambio: ${fmt(s.payment?.change ?? 0)}` }),
    h('div', { class: 'toolbar' },
      h('button', { type: 'button', class: 'secondary', onclick: () => { hist.detail = null; render(); } }, 'Volver'),
      can('sales.receipt.reprint') ? h('button', { type: 'button', class: 'secondary', 'aria-label': 'Reimprimir ticket', onclick: () => { printTicket(receipt, true); hist.reprint = 'Reimpresión enviada a la impresión del navegador (copia).'; render({ silent: true }); } }, 'REIMPRIMIR TICKET') : null),
    hist.reprint ? h('p', { class: 'caption', role: 'status', text: hist.reprint, style: { marginTop: '12px' } }) : null,
    can('sales.refund') ? h('section', { class: 'refunds' },
      h('h2', { class: 'section-title', text: 'Cancelaciones y devoluciones' }),
      h('p', { class: 'detail-text small-text', text: history }),
      blocker ? h('p', { class: 'warning', text: blocker }) : null,
      lines.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Piezas a devolver' },
        h('thead', {}, h('tr', {}, ['#', 'Producto', 'Precio', 'Vendidas', 'Ya devueltas', 'Disponibles', 'A devolver'].map((c, i) => h('th', { class: i >= 2 ? 'num' : null, text: c })))),
        h('tbody', {}, lines.map(l => h('tr', {},
          h('td', { 'data-label': '#', text: String(l.position) }), h('td', { 'data-label': 'Producto', text: l.name }), h('td', { class: 'num', 'data-label': 'Precio', text: fmt(l.unitPrice) }),
          h('td', { class: 'num', 'data-label': 'Vendidas', text: String(l.sold) }), h('td', { class: 'num', 'data-label': 'Ya devueltas', text: String(l.refunded) }),
          h('td', { class: 'num', 'data-label': 'Disponibles', text: String(l.remaining) }),
          h('td', { class: 'num', 'data-label': 'A devolver' }, h('input', { type: 'number', min: '0', max: String(l.remaining), step: '1', inputmode: 'numeric', class: 'qty-input', value: String(back(l.position)),
            disabled: !options?.canReturn || l.remaining === 0, 'aria-label': `A devolver de ${l.name}`,
            oninput: e => { const v = Math.max(0, Math.min(l.remaining, Math.trunc(Number(e.target.value) || 0))); r.back.set(l.position, v); } })))))) ) : null,
      h('label', { class: 'label', style: { marginTop: '12px' } }, 'Motivo (obligatorio)',
        h('input', { maxlength: '200', value: r.reason, disabled: !!blocker, 'aria-label': 'Motivo de cancelación o devolución', oninput: e => { r.reason = e.target.value; } })),
      h('p', { class: 'caption', text: cash ? 'El efectivo sale de la caja abierta de esta terminal. El inventario no se modifica.'
        : `Venta pagada con ${method.toLowerCase()}: se reembolsa por el mismo medio y no sale efectivo de la caja. El inventario no se modifica.`, style: { margin: '0 0 12px' } }),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'secondary', disabled: !options?.canReturn, onclick: () => confirmAndSend('RETURN') }, 'Devolver piezas indicadas'),
        h('button', { type: 'button', class: 'danger-solid', disabled: !options?.canCancel, onclick: () => confirmAndSend('CANCELLATION') }, 'Cancelar venta completa')),
      r.message ? h('p', { class: r.ok ? 'success' : 'error', role: 'status', text: r.message, style: { fontSize: '16px', marginTop: '8px' } }) : null) : null);
}
const money = cents => fmt(cents);
function instruction(method, total) {
  return method === 'CASH' ? `Entrega ${money(total)} en efectivo al cliente.`
    : method === 'CARD' ? `Reembolsa ${money(total)} a la tarjeta del cliente desde la terminal bancaria. No entregues efectivo de la caja.`
    : `Reembolsa ${money(total)} al cliente por transferencia. No entregues efectivo de la caja.`;
}
