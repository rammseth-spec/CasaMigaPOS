// Pantallas de Casa Miga en línea. Cada una pide sus datos a /api/web/<op> y dibuja con h() (sin HTML en texto).
import { api, at, bars, can, card, command, dateLong, dayLabel, dialog, field, fmt, h, METHOD, pesos, quantity, range, render, ROLE, state, table, time, toast } from './app.js';

const I = {
  home: ['M3 11.5 12 4l9 7.5', 'M5.5 10v10h13V10'], sales: ['M6 3h12v18l-3-2-3 2-3-2-3 2z', 'M9 8h6', 'M9 12h6'],
  reports: ['M4 20V11', 'M10 20V5', 'M16 20v-7', 'M21 20H3'], cash: ['M3 7h18v12H3z', 'M16 13h2', 'M3 7l2-3h12l2 3'],
  trays: ['M3 14h18', 'M5 14l2 5h10l2-5', 'M8 10c0-3.5 8-3.5 8 0'], catalog: ['M4 4h7v7H4z', 'M13 4h7v7h-7z', 'M4 13h7v7H4z', 'M13 13h7v7h-7z'],
  inventory: ['M3 7l9-4 9 4v10l-9 4-9-4z', 'M3 7l9 4 9-4', 'M12 11v10'], recipes: ['M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3z', 'M5 17a3 3 0 0 1 3-3h11'],
  production: ['M7 14a4 4 0 1 1 2.5-7.2A4 4 0 0 1 17 7a4 4 0 1 1 0 7v5H7z', 'M7 17h10'], users: ['M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z', 'M2 21a7 7 0 0 1 14 0', 'M17 11a3 3 0 1 0 0-6', 'M22 21a6 6 0 0 0-5-6'],
  terminals: ['M3 5h18v11H3z', 'M8 21h8', 'M12 16v5'],
};
const branchName = id => state.branches.find(b => b.id === id)?.name ?? '';
const multi = () => !state.branch && state.branches.length > 1;
const uuid = () => crypto.randomUUID();
const tabsBar = (tabs, active, onpick) => h('div', { class: 'tabs', role: 'tablist' }, tabs.map(([id, label]) =>
  h('button', { type: 'button', class: 'chip', role: 'tab', 'aria-pressed': String(id === active), 'aria-selected': String(id === active), onclick: () => onpick(id) }, label)));
const kpi = (label, value, detail, cls) => h('article', { class: 'card kpi' }, h('span', { text: label }), h('strong', { class: cls, text: value }), h('small', { text: detail ?? '' }));
const diffCell = d => d === null ? h('span', { class: 'pill info', text: 'Abierta' }) : h('span', { class: d < 0 ? 'short' : d > 0 ? 'over' : 'even', text: d === 0 ? 'Cuadra' : fmt(d) });

// ---------- Resumen ----------
async function resumen() {
  const data = await api('dashboard', range());
  const t = data.totals;
  const byDay = new Map();
  for (const d of data.days) { const r = byDay.get(d.day) ?? { day: d.day, net: 0, tickets: 0 }; r.net += d.gross - d.refunds; r.tickets += d.tickets; byDay.set(d.day, r); }
  const days = [...byDay.values()].sort((a, b) => b.day.localeCompare(a.day));
  const methods = [['Efectivo', t.byMethod.cash], ['Tarjeta', t.byMethod.card], ['Transferencia', t.byMethod.transfer]];
  const top = Math.max(1, ...methods.map(m => m[1]));
  const diff = t.shortages + t.overages;
  return h('div', { style: { display: 'grid', gap: '16px' } },
    h('section', { class: 'kpis' },
      kpi('Ventas netas', fmt(t.net), `Vendido ${fmt(t.gross)}`), kpi('Tickets', String(t.tickets), t.tickets ? `Promedio ${fmt(Math.round(t.gross / t.tickets))}` : 'Sin ventas'),
      kpi('Devoluciones', fmt(t.refunds), 'Cancelaciones y devoluciones'), kpi('Diferencias de caja', fmt(diff), `Faltantes ${fmt(-t.shortages)} · Sobrantes ${fmt(t.overages)}`, diff < 0 ? 'short' : diff > 0 ? 'over' : '')),
    h('section', { class: 'grid-2' },
      card('Ventas por día', days.length ? bars(days, { value: d => d.net, label: d => dayLabel(d.day), title: d => `${d.tickets} tickets` }) : h('p', { class: 'empty', text: 'Sin ventas en el periodo.' })),
      card('Formas de pago',
        h('div', { style: { display: 'grid', gap: '10px' } }, methods.map(([name, cents]) => {
          const fill = h('div', { class: 'fill' }); fill.style.width = `${Math.max(0, cents) / top * 100}%`;
          return h('div', { class: 'meter' }, h('span', { text: name }), h('b', { class: 'num', text: fmt(cents) }), h('div', { class: 'track' }, fill));
        })),
        h('h2', { text: 'Más vendidos', style: { marginTop: '20px' } }),
        data.products.length ? h('ul', { class: 'rows' }, data.products.map(p => h('li', {}, h('strong', { text: p.name }), h('span', { class: 'muted num', text: `${p.quantity} pz` }), h('b', { class: 'num', text: fmt(p.total) }))))
          : h('p', { class: 'empty', text: 'Sin ventas en el periodo.' }))),
    card(h('div', { class: 'toolbar', style: { justifyContent: 'space-between', width: '100%' } }, h('h2', { text: 'Cortes recientes' }), h('a', { href: '#/caja', class: 'button ghost' }, 'Ver caja')),
      sessionsTable(data.sessions.slice(0, 6))),
    card('Cajas', terminalsList(data.terminals)));
}
function sessionsTable(sessions) {
  return table([
    { label: 'Caja', value: s => [s.terminal, multi() ? h('small', { text: branchName(s.branchId) }) : null] },
    { label: 'Turno', value: s => [at(s.openedAt), h('small', { text: s.closedAt ? `Cerró ${at(s.closedAt)}` : 'En curso' })] },
    { label: 'Fondo', num: true, value: s => fmt(s.opening) },
    { label: 'Esperado', num: true, value: s => [fmt(s.expected), (s.movements || s.cashRefunds) ? h('small', { text: [s.movements ? `Mov. ${fmt(s.movements)}` : '', s.cashRefunds ? `Dev. −${fmt(s.cashRefunds)}` : ''].filter(Boolean).join(' · ') }) : null] },
    { label: 'Contado', num: true, value: s => s.counted === null ? '—' : fmt(s.counted) },
    { label: 'Diferencia', num: true, value: s => diffCell(s.difference) },
  ], sessions, { empty: 'Sin cortes en el periodo.' });
}
function terminalsList(terminals) {
  if (!terminals.length) return h('p', { class: 'empty', text: 'Ninguna caja registrada. Regístrala desde la pantalla Nube de la app.' });
  return h('ul', { class: 'rows' }, terminals.map(t => {
    const recent = t.lastSeenAt && Date.now() - new Date(t.lastSeenAt).getTime() < 30 * 60000;
    return h('li', {}, h('div', {}, h('strong', { text: t.name }), h('small', { class: 'muted', text: ' ' + branchName(t.branchId) })),
      h('span', { class: 'pill ' + (t.revoked ? 'bad' : recent ? 'ok' : ''), text: t.revoked ? 'Dada de baja' : recent ? 'Conectada' : 'Sin conexión reciente' }),
      h('span', { class: 'muted small', text: t.lastSeenAt ? at(t.lastSeenAt) : 'Nunca' }));
  }));
}

// ---------- Ventas ----------
const salesState = { q: '', method: '', page: 0 };
async function ventas() {
  const data = await api('sales', range({ q: salesState.q, method: salesState.method, page: salesState.page }));
  let timer = 0;
  const search = h('input', { type: 'search', placeholder: 'Folio (C01-001061) o producto', value: salesState.q, 'aria-label': 'Buscar venta',
    oninput: e => { clearTimeout(timer); timer = setTimeout(() => { salesState.q = e.target.value; salesState.page = 0; render(); }, 400); } });
  const method = h('select', { 'aria-label': 'Forma de pago', onchange: e => { salesState.method = e.target.value; salesState.page = 0; render(); } },
    h('option', { value: '', text: 'Todas las formas de pago' }), Object.entries(METHOD).map(([v, l]) => h('option', { value: v, text: l, selected: salesState.method === v })));
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));
  return card(h('div', { class: 'toolbar', style: { width: '100%' } }, search, method, h('span', { class: 'muted small', text: `${data.total} ventas` })),
    table([
      { label: 'Folio', value: s => [h('strong', { text: s.folio }), multi() ? h('small', { text: s.branch }) : null] },
      { label: 'Fecha', value: s => at(s.at) },
      { label: 'Cajero', value: s => s.cashier || '—' },
      { label: 'Pago', value: s => METHOD[s.method] ?? s.method },
      { label: 'Piezas', num: true, value: s => String(s.pieces) },
      { label: 'Total', num: true, value: s => [fmt(s.total), s.cancelled ? h('small', { class: 'short', text: 'Cancelada' }) : s.refunded ? h('small', { class: 'over', text: `Dev. −${fmt(s.refunded)}` }) : null] },
    ], data.items, { onclick: s => saleDialog(s.id), empty: 'Sin ventas con ese filtro.' }),
    pages > 1 ? h('div', { class: 'toolbar', style: { justifyContent: 'center', marginTop: '12px' } },
      h('button', { type: 'button', disabled: salesState.page === 0, onclick: () => { salesState.page--; render(); } }, 'Anteriores'),
      h('span', { class: 'muted small', text: `Página ${salesState.page + 1} de ${pages}` }),
      h('button', { type: 'button', disabled: salesState.page + 1 >= pages, onclick: () => { salesState.page++; render(); } }, 'Siguientes')) : null);
}
async function saleDialog(id) {
  const s = await api('sale', { id, branch: state.branch });
  dialog(`Venta ${s.folio}`, body => {
    body.append(h('div', { class: 'wide ticket' },
      h('p', { class: 'muted', text: `${dateLong(s.at)} · ${time(s.at)} · ${s.terminal}${s.branch ? ' · ' + s.branch : ''}` }),
      h('p', { class: 'muted', text: `Cobró: ${s.cashier || '—'}` }),
      s.items.map(i => h('div', { class: 'line' }, h('span', { text: `${i.quantity} × ${i.name} (${fmt(i.unitPrice)})` }), h('b', { text: fmt(i.total) }))),
      h('div', { class: 'line total' }, h('span', { text: 'Total' }), h('span', { text: fmt(s.total) })),
      s.payment ? h('p', { class: 'muted', text: `${METHOD[s.payment.method] ?? s.payment.method}${s.payment.method === 'CASH' ? ` · recibido ${fmt(s.payment.received)} · cambio ${fmt(s.payment.change)}` : s.payment.reference ? ` · ref. ${s.payment.reference}` : ''}` }) : null,
      s.refunds.length ? h('div', { style: { marginTop: '14px' } }, h('h3', { text: 'Devoluciones' }), s.refunds.map(r =>
        h('div', { class: 'line' }, h('span', { text: `${r.kind === 'CANCELLATION' ? 'Cancelación' : 'Devolución'} · ${at(r.at)} · ${r.by} · «${r.reason}»` }), h('b', { class: 'short', text: `−${fmt(r.total)}` })))) : null));
  });
}

// ---------- Reportes ----------
let reportTab = 'compare';
async function reportes() {
  const pick = id => { reportTab = id; render(); };
  const tabs = tabsBar([['compare', 'Comparativo'], ['heatmap', 'Ventas por hora'], ['cash-control', 'Control de caja'], ['production', 'Producción sugerida']], reportTab, pick);
  let body;
  if (reportTab === 'compare') {
    const c = await api('compare', range());
    const delta = (a, b) => b ? `${a >= b ? '+' : ''}${Math.round((a - b) / b * 100)} %` : '—';
    body = [h('section', { class: 'kpis' },
      kpi('Ventas netas', fmt(c.current.net), `Antes ${fmt(c.previous.net)} (${delta(c.current.net, c.previous.net)})`),
      kpi('Tickets', String(c.current.tickets), `Antes ${c.previous.tickets} (${delta(c.current.tickets, c.previous.tickets)})`),
      kpi('Ticket promedio', fmt(c.current.tickets ? Math.round(c.current.gross / c.current.tickets) : 0), `Antes ${fmt(c.previous.tickets ? Math.round(c.previous.gross / c.previous.tickets) : 0)}`),
      kpi('Devoluciones', fmt(c.current.refunds), `Antes ${fmt(c.previous.refunds)}`)),
      card(`Día por día contra ${dayLabel(c.previousRange.from)} – ${dayLabel(c.previousRange.to)}`,
        h('div', { class: 'bars' }, c.daily.map(d => {
          const top = Math.max(1, ...c.daily.flatMap(x => [x.current, x.previous]));
          const now = h('div', { class: 'fill' }); now.style.width = `${d.current / top * 100}%`;
          const before = h('div', { class: 'fill prev' }); before.style.width = `${d.previous / top * 100}%`;
          return h('div', { style: { display: 'grid', gap: '3px', marginBottom: '6px' } },
            h('div', { class: 'bar' }, h('span', { class: 'label', text: dayLabel(d.day) }), h('div', { class: 'track' }, now), h('b', { text: fmt(d.current) })),
            h('div', { class: 'bar' }, h('span', { class: 'label muted small', text: dayLabel(d.previousDay) }), h('div', { class: 'track' }, before), h('b', { class: 'muted', text: fmt(d.previous) })));
        })))];
  } else if (reportTab === 'heatmap') {
    const cells = await api('heatmap', range());
    const hours = cells.length ? [Math.min(...cells.map(c => c.hour)), Math.max(...cells.map(c => c.hour))] : [7, 21];
    const span = Array.from({ length: hours[1] - hours[0] + 1 }, (_, i) => hours[0] + i);
    const max = Math.max(1, ...cells.map(c => c.tickets));
    const peak = cells.reduce((a, c) => (c.tickets > (a?.tickets ?? 0) ? c : a), null);
    const names = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'];
    const grid = h('div', { class: 'heat', role: 'table', 'aria-label': 'Tickets por día y hora' });
    grid.style.setProperty('--hours', String(span.length));
    grid.append(h('span'), ...span.map(x => h('span', { class: 'h', text: String(x) })));
    for (const wd of [1, 2, 3, 4, 5, 6, 0]) {
      grid.append(h('span', { class: 'd', text: names[wd] }));
      for (const hour of span) {
        const c = cells.find(x => x.weekday === wd && x.hour === hour);
        const cell = h('span', { class: 'c', title: c ? `${names[wd]} ${hour}:00 · ${c.tickets} tickets · ${fmt(c.gross)}` : `${names[wd]} ${hour}:00 · sin ventas` });
        if (c) cell.style.background = `color-mix(in srgb, var(--primary) ${Math.round(12 + c.tickets / max * 88)}%, var(--surface-2))`;
        grid.append(cell);
      }
    }
    body = [card(peak ? `Hora pico: ${names[peak.weekday]} ${peak.hour}:00 (${peak.tickets} tickets)` : 'Ventas por hora', cells.length ? grid : h('p', { class: 'empty', text: 'Sin ventas en el periodo.' }))];
  } else if (reportTab === 'cash-control') {
    const c = await api('cash-control', range());
    body = [h('section', { class: 'grid-2' },
      card('Faltantes y sobrantes por quien abrió', table([
        { label: 'Abrió', value: x => x.user }, { label: 'Cortes', num: true, value: x => String(x.sessions) }, { label: 'Con diferencia', num: true, value: x => String(x.uneven) },
        { label: 'Faltantes', num: true, value: x => h('span', { class: x.shortages ? 'short' : '', text: fmt(x.shortages) }) }, { label: 'Sobrantes', num: true, value: x => fmt(x.overages) },
      ], c.cuts, { empty: 'Sin cortes cerrados.' })),
      card('Motivos de devolución', c.reasons.length ? h('ul', { class: 'rows' }, c.reasons.map(r => h('li', {}, h('span', { text: r.reason }), h('span'), h('b', { text: String(r.count) })))) : h('p', { class: 'empty', text: 'Sin devoluciones.' }))),
      card('Devoluciones por quien las hizo', table([
        { label: 'Usuario', value: x => x.name }, { label: 'Devoluciones', num: true, value: x => String(x.count) }, { label: 'Cancelaciones', num: true, value: x => String(x.cancellations) }, { label: 'Importe', num: true, value: x => fmt(x.total) },
      ], c.refunds, { empty: 'Sin devoluciones.' }))];
  } else {
    const input = h('input', { type: 'date', 'aria-label': 'Día a producir', value: productionDay ?? '', onchange: e => { productionDay = e.target.value; render(); } });
    const p = await api('production-suggestion', { branch: state.branch, day: productionDay ?? '' });
    input.value = p.day;
    body = [card(h('div', { class: 'toolbar', style: { width: '100%' } }, h('h2', { text: 'Piezas sugeridas para' }), input),
      h('p', { class: 'muted small', text: `Promedio del mismo día de la semana en las 4 semanas anteriores (${p.weeks.map(dayLabel).join(', ')}) + 10 %.` }),
      table([{ label: 'Producto', value: x => h('strong', { text: x.name }) }, { label: 'Promedio', num: true, value: x => String(x.average) },
        { label: 'Semanas con venta', num: true, value: x => `${x.weeksWithSales} de 4` }, { label: 'Sugeridas', num: true, value: x => h('b', { text: String(x.suggested) }) }],
      p.products, { empty: 'Sin ventas esos días.' }))];
  }
  return h('div', { style: { display: 'grid', gap: '16px' } }, tabs, ...body);
}
let productionDay = null;

// ---------- Caja ----------
async function caja() {
  const data = await api('cash', range());
  const kinds = { EXPENSE: ['Gasto', 'warn'], WITHDRAWAL: ['Retiro', 'warn'], DEPOSIT: ['Entrada', 'info'] };
  const categories = { SUPPLIES: 'Insumos', GAS: 'Gas', CLEANING: 'Limpieza', TRANSPORT: 'Transporte', MAINTENANCE: 'Mantenimiento', OTHER: 'Otro' };
  const sum = k => data.movements.filter(m => m.kind === k).reduce((a, m) => a + m.amount, 0);
  return h('div', { style: { display: 'grid', gap: '16px' } },
    h('section', { class: 'kpis' },
      kpi('Cortes', String(data.sessions.length), `${data.sessions.filter(s => s.open).length} abiertos`),
      kpi('Gastos', fmt(sum('EXPENSE')), `${data.movements.filter(m => m.kind === 'EXPENSE').length} movimientos`),
      kpi('Retiros', fmt(sum('WITHDRAWAL')), 'Efectivo a resguardo'), kpi('Entradas', fmt(sum('DEPOSIT')), 'Cambio o fondo extra')),
    card('Cortes de caja', sessionsTable(data.sessions)),
    card('Gastos, retiros y entradas', table([
      { label: 'Fecha', value: m => [at(m.at), multi() ? h('small', { text: m.branch }) : null] },
      { label: 'Tipo', value: m => h('span', { class: 'pill ' + kinds[m.kind][1], text: kinds[m.kind][0] }) },
      { label: 'Detalle', value: m => [m.category ? categories[m.category] : '', m.note ? (m.category ? ' · ' : '') + m.note : ''].join('') || '—' },
      { label: 'Quién', value: m => m.user },
      { label: 'Importe', num: true, value: m => h('b', { text: (m.kind === 'DEPOSIT' ? '+' : '−') + fmt(m.amount) }) },
    ], data.movements, { empty: 'Sin gastos, retiros ni entradas en el periodo.' })));
}

// ---------- Charolas ----------
let trayTab = 'stores';
async function charolas(ctx) {
  const data = await api('consignment', range());
  const tabs = tabsBar([['stores', 'Tiendas'], ['deliveries', 'Entregas'], ['payments', 'Cobros']], trayTab, id => { trayTab = id; render(); });
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
      { label: 'Contenido', span: true, value: d => h('small', { text: d.items.map(i => `${i.quantity} ${i.name}`).join(' · ') }) },
    ], data.deliveries, { empty: 'Sin entregas en el periodo.' }))];
  } else {
    body = [card('Cobros en el periodo', table([
      { label: 'Fecha', value: p => at(p.at) }, { label: 'Tienda', value: p => storeName.get(p.store) ?? '—' },
      { label: 'Forma', value: p => METHOD[p.method] ?? p.method }, { label: 'Nota', value: p => p.note || '—' }, { label: 'Importe', num: true, value: p => h('b', { text: fmt(p.amount) }) },
    ], data.payments, { empty: 'Sin cobros en el periodo.' }))];
  }
  return h('div', { style: { display: 'grid', gap: '16px' } }, tabs, ...body);
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

// ---------- Catálogo ----------
const catalogState = { q: '', category: '', inactive: false };
async function catalogo(ctx) {
  const data = await api('catalog', { branch: state.branch });
  const categories = new Map(data.categories.map(c => [c.id, c]));
  const text = catalogState.q.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
  const list = data.products.filter(p => (catalogState.inactive || p.active) && (!catalogState.category || p.categoryId === catalogState.category)
    && (!text || `${p.name} ${p.sku ?? ''}`.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase().includes(text)));
  let timer = 0;
  const grid = h('div', { class: 'products' }, list.map(p => h('button', { type: 'button', class: 'product' + (p.active ? '' : ' off'), onclick: () => productDialog(p, data.categories) },
    h('div', { class: 'photo' }, p.photo ? h('img', { src: `/api/web/photo?product=${p.id}&branch=${p.branchId}&v=${p.photo.sha}`, alt: '', loading: 'lazy' }) : 'Sin foto'),
    h('div', { class: 'info' }, h('strong', { text: p.name }), h('span', { class: 'muted small', text: [categories.get(p.categoryId)?.name, p.sku].filter(Boolean).join(' · ') }),
      h('span', { class: 'price', text: fmt(p.price) }), multi() ? h('span', { class: 'muted small', text: branchName(p.branchId) }) : null, p.active ? null : h('span', { class: 'pill', text: 'Inactivo' })))));
  return h('div', { style: { display: 'grid', gap: '16px' } },
    h('div', { class: 'toolbar' },
      h('input', { type: 'search', placeholder: 'Buscar por nombre o SKU', value: catalogState.q, 'aria-label': 'Buscar producto',
        oninput: e => { clearTimeout(timer); timer = setTimeout(() => { catalogState.q = e.target.value; render(); }, 300); } }),
      h('select', { 'aria-label': 'Categoría', onchange: e => { catalogState.category = e.target.value; render(); } }, h('option', { value: '', text: 'Todas las categorías' }),
        data.categories.map(c => h('option', { value: c.id, text: c.name + (multi() ? ` (${branchName(c.branchId)})` : ''), selected: catalogState.category === c.id }))),
      h('label', { class: 'check' }, h('input', { type: 'checkbox', checked: catalogState.inactive, onchange: e => { catalogState.inactive = e.target.checked; render(); } }), 'Mostrar inactivos'),
      can('products.create') ? h('button', { type: 'button', class: 'primary', onclick: () => productDialog(null, data.categories, ctx.branchId()) }, '+ Nuevo producto') : null,
      can('categories.edit') || can('categories.create') ? h('button', { type: 'button', onclick: () => categoriesDialog(data.categories, ctx.branchId()) }, 'Categorías') : null),
    h('p', { class: 'muted small', text: `${list.length} productos` }),
    list.length ? grid : h('p', { class: 'empty', text: 'Sin productos con ese filtro.' }));
}
function productDialog(product, categories, branchId) {
  const editable = product ? can('products.edit') : can('products.create');
  const branch = product?.branchId ?? branchId;
  const options = branchId => categories.filter(c => c.branchId === branchId && (c.active || c.id === product?.categoryId));
  dialog(product ? product.name : 'Nuevo producto', body => {
    const categorySelect = h('select', { name: 'category', required: true, disabled: !editable }, options(branch).map(c => h('option', { value: c.id, text: c.name, selected: c.id === product?.categoryId })));
    const picker = product ? null : branchPicker(branch);
    picker?.querySelector('select').addEventListener('change', e => categorySelect.replaceChildren(...options(e.target.value).map(c => h('option', { value: c.id, text: c.name }))));
    body.append(product?.photo ? h('img', { class: 'wide', src: `/api/web/photo?product=${product.id}&branch=${product.branchId}&v=${product.photo.sha}`, alt: '', style: { width: '100%', maxHeight: '220px', objectFit: 'cover', borderRadius: '12px' } }) : '',
      picker ?? '',
      field('Nombre', h('input', { name: 'name', required: true, maxlength: '160', value: product?.name ?? '', disabled: !editable }), true),
      field('Categoría', categorySelect), field('SKU', h('input', { name: 'sku', maxlength: '40', value: product?.sku ?? '', disabled: !editable })),
      field('Precio (MXN)', h('input', { name: 'price', required: true, inputmode: 'decimal', value: product ? pesos(product.price) : '', disabled: !editable })),
      h('label', { class: 'check' }, h('input', { type: 'checkbox', name: 'track', checked: product?.trackInventory ?? true, disabled: !editable }), 'Descuenta inventario al vender'),
      field('Descripción', h('textarea', { name: 'description', maxlength: '500', disabled: !editable }, product?.description ?? ''), true),
      product?.consignmentPrice !== null && product ? h('p', { class: 'muted small wide', text: `Precio de charola: ${fmt(product.consignmentPrice)}` }) : '',
      product && can('products.deactivate') ? h('div', { class: 'wide toolbar' }, h('button', { type: 'button', class: product.active ? 'danger' : '', onclick: async e => {
        e.target.disabled = true;
        try { await command(product.branchId, 'product.active', { id: product.id, active: !product.active }); e.target.closest('dialog').close(); e.target.closest('dialog').remove(); }
        catch (error) { toast(error.message); e.target.disabled = false; }
      } }, product.active ? 'Desactivar producto' : 'Reactivar producto')) : '');
  }, editable ? { onSubmit: async form => {
    const f = form.elements;
    const price = Math.round(Number(String(f.price.value).replace(',', '.')) * 100);
    if (!Number.isFinite(price) || price < 0) throw new Error('Escribe el precio, por ejemplo 19.50.');
    await command(product?.branchId ?? f.branch?.value ?? branch, 'product.save', { id: product?.id ?? uuid(), categoryId: f.category.value, sku: f.sku.value.trim() || null, name: f.name.value,
      description: f.description.value, priceCents: price, trackInventory: f.track.checked });
  } } : {});
}
function categoriesDialog(categories, branchId) {
  const list = categories.filter(c => !branchId || c.branchId === branchId || state.me.admin);
  const edit = category => dialog(category ? `Categoría ${category.name}` : 'Nueva categoría', body => {
    body.append(category ? '' : branchPicker(branchId) ?? '',
      field('Nombre', h('input', { name: 'name', required: true, maxlength: '80', value: category?.name ?? '' }), true),
      field('Orden', h('input', { name: 'order', type: 'number', min: '0', max: '9999', value: String(category?.sortOrder ?? 0) })),
      category && can('categories.deactivate') ? h('label', { class: 'check' }, h('input', { type: 'checkbox', name: 'active', checked: category.active }), 'Activa') : '',
      field('Descripción', h('textarea', { name: 'description', maxlength: '500' }, category?.description ?? ''), true));
  }, { onSubmit: async form => {
    const f = form.elements;
    await command(category?.branchId ?? f.branch?.value ?? branchId, 'category.save', { id: category?.id ?? uuid(), name: f.name.value, description: f.description.value,
      sortOrder: Number(f.order.value) || 0, ...(category && f.active ? { active: f.active.checked } : {}) });
  } });
  dialog('Categorías', body => {
    body.append(h('div', { class: 'wide' }, table([
      { label: 'Categoría', value: c => [h('strong', { text: c.name }), multi() ? h('small', { text: branchName(c.branchId) }) : null] },
      { label: 'Estado', value: c => h('span', { class: 'pill ' + (c.active ? 'ok' : ''), text: c.active ? 'Activa' : 'Inactiva' }) },
    ], list, { onclick: can('categories.edit') ? c => edit(c) : null, empty: 'Sin categorías.' })),
      can('categories.create') ? h('button', { type: 'button', class: 'primary wide', onclick: () => edit(null) }, '+ Nueva categoría') : '');
  });
}

// ---------- Inventario, recetas y producción ----------
async function inventario() {
  const data = await api('inventory', { branch: state.branch });
  const kinds = { RAW: 'Insumo', INGREDIENT: 'Insumo', PACKAGING: 'Empaque', FINISHED: 'Terminado', INTERMEDIATE: 'Preparación' };
  return h('div', { style: { display: 'grid', gap: '16px' } },
    card('Existencias', table([
      { label: 'Insumo', value: i => [h('strong', { text: i.name }), h('small', { text: i.code })] },
      { label: 'Tipo', value: i => kinds[i.kind] ?? i.kind },
      { label: 'Existencia', num: true, value: i => h('b', { text: quantity(i.balances.reduce((a, b) => a + b.atomic, 0), i.dimension, data.measures) }) },
      { label: 'Ubicación', value: i => i.balances.map(b => b.location).join(', ') || '—' },
      { label: 'Estado', value: i => h('span', { class: 'pill ' + (i.active ? 'ok' : ''), text: i.active ? 'Activo' : 'Inactivo' }) },
    ], data.items, { empty: 'Sin insumos registrados.' })),
    card('Movimientos recientes', table([
      { label: 'Fecha', value: o => at(o.at) }, { label: 'Operación', value: o => [o.kind, o.reason ? h('small', { text: o.reason }) : null] },
      { label: 'Estado', value: o => o.state }, { label: 'Quién', value: o => o.user || '—' },
      { label: 'Renglones', span: true, value: o => h('small', { text: o.lines.map(l => `${l.item} ${l.delta > 0 ? '+' : ''}${quantity(l.delta, l.dimension, data.measures)}`).join(' · ') }) },
    ], data.operations, { empty: 'Sin movimientos de inventario.' })));
}
async function recetas() {
  const data = await api('recipes', { branch: state.branch });
  const lines = list => list.length ? h('ul', { class: 'rows' }, list.map(l => h('li', {}, h('span', { text: l.item ?? l.formula }), h('span'), h('b', { class: 'num', text: quantity(l.atomic, l.dimension, data.measures) })))) : h('p', { class: 'empty', text: 'Sin ingredientes.' });
  return h('div', { style: { display: 'grid', gap: '16px' } },
    card('Preparaciones (fórmulas)', data.formulas.length ? h('section', { class: 'grid-3' }, data.formulas.map(f => h('div', { class: 'card' },
      h('h3', { text: f.name }), h('p', { class: 'muted small', text: f.active ? `${f.code} · versión ${f.active.version} · rinde ${quantity(f.active.yieldAtomic, f.active.yieldDimension, data.measures)}` : f.code }),
      f.active ? lines(f.active.ingredients) : null,
      f.active?.alternatives.map(g => h('p', { class: 'small', text: `${g.name}: ${g.options.map(o => `${o.item} ${quantity(o.atomic, o.dimension, data.measures)}`).join(' o ')}` }))))) : h('p', { class: 'empty', text: 'Sin preparaciones.' })),
    card('Recetas por producto', data.profiles.length ? h('section', { class: 'grid-3' }, data.profiles.map(p => h('div', { class: 'card' },
      h('h3', { text: p.product }), h('p', { class: 'muted small', text: p.active ? `Versión ${p.active.version} · por pieza` : '' }),
      p.active ? [lines(p.active.ingredients), p.active.formulas.length ? lines(p.active.formulas) : null] : null))) : h('p', { class: 'empty', text: 'Sin recetas de producto.' })));
}
async function produccion() {
  const batches = await api('production', range());
  const status = { CONFIRMED: ['Registrada', 'ok'], DRAFT: ['Borrador', ''], PLANNED: ['Planeada', 'info'], CANCELLED: ['Cancelada', 'bad'] };
  return h('div', { style: { display: 'grid', gap: '16px' } },
    h('p', { class: 'notice', text: 'Para saber cuánto producir mañana: Reportes → Producción sugerida.' }),
    card('Producciones del periodo', table([
      { label: 'Fecha', value: b => at(b.at) }, { label: 'Estado', value: b => h('span', { class: 'pill ' + (status[b.status]?.[1] ?? ''), text: status[b.status]?.[0] ?? b.status }) },
      { label: 'Productos', value: b => b.outputs.map(o => `${o.product}: ${o.actual ?? o.planned}${o.actual !== null && o.actual !== o.planned ? ` de ${o.planned}` : ''} pz`).join(' · ') },
      { label: 'Quién', value: b => b.by || '—' },
    ], batches, { empty: 'Sin producciones en el periodo.' })));
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
const KIND = { 'category.save': 'Categoría', 'product.save': 'Producto', 'product.active': 'Activar/desactivar producto', 'user.create': 'Alta de usuario', 'user.update': 'Usuario',
  'user.active': 'Activar/desactivar usuario', 'user.reset_password': 'Restablecer contraseña', 'consignment.store.save': 'Tienda de charolas', 'consignment.template.save': 'Charola habitual' };
function describe(c) {
  const p = c.payload;
  if (c.kind === 'product.save') return `${p.name} · ${fmt(p.priceCents)}`;
  if (c.kind === 'product.active' || c.kind === 'user.active') return p.active ? 'Reactivar' : 'Desactivar';
  if (c.kind === 'user.create') return `${p.username} (${ROLE[p.role]})`;
  if (c.kind === 'user.update') return `${p.displayName} (${ROLE[p.role]})`;
  if (c.kind === 'consignment.template.save') return `${p.lines.length} panes`;
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

export const VIEWS = [
  { id: 'resumen', title: 'Resumen', icon: I.home, permission: 'sales.view', usesRange: true, render: resumen },
  { id: 'ventas', title: 'Ventas', subtitle: 'Toca una venta para ver su ticket', icon: I.sales, permission: 'sales.view', usesRange: true, render: ventas },
  { id: 'reportes', title: 'Reportes', icon: I.reports, permission: 'sales.view', usesRange: true, render: reportes },
  { id: 'caja', title: 'Caja', subtitle: 'Cortes, gastos, retiros y entradas', icon: I.cash, permission: 'cash.view', usesRange: true, render: caja },
  { id: 'charolas', title: 'Charolas', icon: I.trays, permission: 'consignment.view', usesRange: true, render: charolas },
  { id: 'catalogo', title: 'Catálogo', icon: I.catalog, permission: 'catalog.view', usesRange: false, render: catalogo },
  { id: 'inventario', title: 'Inventario', icon: I.inventory, permission: 'inventory.view', usesRange: false, render: inventario },
  { id: 'recetas', title: 'Recetas', icon: I.recipes, permission: 'recipe.view', usesRange: false, render: recetas },
  { id: 'produccion', title: 'Producción', icon: I.production, permission: 'production.view', usesRange: true, render: produccion },
  { id: 'usuarios', title: 'Usuarios', icon: I.users, permission: 'users.manage', usesRange: false, render: usuarios },
  { id: 'cajas', title: 'Cajas', subtitle: 'Conexión y cambios enviados', icon: I.terminals, permission: 'sales.view', usesRange: false, render: cajas },
];
