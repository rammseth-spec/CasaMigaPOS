// Reportes como la caja de escritorio (ReportsView de QAv1): fechas propias con «Generar», «Exportar CSV» y atajos;
// pestañas Resumen, Comparativo, Ventas por hora, Control de caja y Producción con las mismas cifras (lib/reports.ts).
// La pestaña Ganancia es propia de la web.
import { api, fmt, h, METHOD, render, state } from './app.js';

const rep = { tab: 'summary', from: '', to: '', day: '', data: null, forecast: null, generated: '', message: '', key: '' };
const today = () => new Date().toLocaleDateString('en-CA', { timeZone: state.timeZone });
const shiftDay = (day, days) => { const d = new Date(day + 'T12:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
const date = day => new Date(day + 'T12:00:00Z');
const fmtDay = (day, options) => date(day).toLocaleDateString('es-MX', { timeZone: 'UTC', ...options });
const cap = text => text ? text[0].toLocaleUpperCase('es-MX') + text.slice(1) : text;
const plural = (count, one, many) => count === 1 ? `1 ${one}` : `${count.toLocaleString('es-MX')} ${many}`;
const MONTHS = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
// «martes 6 de octubre» (y el año al final), como el formato «dddd d 'de' MMMM yyyy» de la caja.
const longDay = (day, year = false) => `${fmtDay(day, { weekday: 'long' })} ${Number(day.slice(8))} de ${MONTHS[Number(day.slice(5, 7)) - 1]}${year ? ' ' + day.slice(0, 4) : ''}`;
const period = (from, to) => from === to ? cap(longDay(from, true))
  : `Del ${fmtDay(from, { day: 'numeric', month: 'short', year: 'numeric' })} al ${fmtDay(to, { day: 'numeric', month: 'short', year: 'numeric' })}`;
const dayName = day => fmtDay(day, { weekday: 'long' });
const WEEK = ['lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado', 'domingo'];
const changeText = (change, current, previous) => change === null ? (current === 0 && previous === 0 ? '—' : 'Nuevo')
  : change === 0 ? '= 0 %' : change > 0 ? `▲ ${change.toLocaleString('es-MX', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} %` : `▼ ${Math.abs(change).toLocaleString('es-MX', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} %`;
const trend = (a, b) => Math.sign(a - b);
const compact = cents => cents >= 100000 ? `$${(cents / 100000).toFixed(1).replace(/\.0$/, '')}k` : `$${Math.round(cents / 100)}`;
const CHART = 150;
const barHeight = (cents, max) => cents <= 0 ? 0 : Math.max(2, CHART * cents / max);
const TABS = [['summary', 'Resumen'], ['comparison', 'Comparativo'], ['heatmap', 'Ventas por hora'], ['cash', 'Control de caja'], ['forecast', 'Producción'], ['profit', 'Ganancia']];

export async function reportsView({ profit, canProfit }) {
  if (!rep.from) { rep.from = today(); rep.to = today(); rep.day = shiftDay(today(), 1); }
  const key = `${state.branch}|${rep.from}|${rep.to}`;
  if (rep.tab !== 'forecast' && rep.tab !== 'profit' && (!rep.data || rep.key !== key)) await generate(false);
  if (rep.tab === 'forecast' && !rep.forecast) await calculate(false);
  const tabs = h('div', { class: 'report-tabs' }, TABS.filter(([id]) => id !== 'profit' || canProfit).map(([id, label]) =>
    h('button', { type: 'button', class: 'tab', 'aria-pressed': String(rep.tab === id), 'aria-label': id === 'forecast' ? 'Pronóstico de producción' : label, onclick: () => { rep.tab = id; rep.message = ''; render(); } }, label)));
  const body = rep.tab === 'summary' ? summary() : rep.tab === 'comparison' ? comparison() : rep.tab === 'heatmap' ? heatmap() : rep.tab === 'cash' ? cashControl()
    : rep.tab === 'forecast' ? forecastView() : await profit({ from: rep.from, to: rep.to });
  return h('div', { class: 'cash-card reports' }, tabs, rep.tab === 'forecast' ? forecastControls() : periodControls(),
    rep.message ? h('p', { class: 'error', role: 'alert', text: rep.message }) : null, body);
}

async function generate(redraw = true) {
  if (!rep.from || !rep.to) { rep.message = 'Elige la fecha inicial y la final.'; return redraw && render(); }
  if (rep.from > rep.to) { rep.message = 'La fecha Desde no puede ser posterior a Hasta.'; return redraw && render(); }
  try {
    rep.data = await api('report', { from: rep.from, to: rep.to, branch: state.branch });
    rep.key = `${state.branch}|${rep.from}|${rep.to}`; rep.message = '';
    rep.generated = `Actualizado a las ${new Date().toLocaleTimeString('en-GB')}`;
  } catch (error) { rep.message = error.message; }
  if (redraw) render({ silent: true });
}
async function calculate(redraw = true) {
  if (!rep.day) { rep.message = 'Elige el día que quieres planear.'; return redraw && render(); }
  try { rep.forecast = await api('forecast', { day: rep.day, branch: state.branch }); rep.message = ''; rep.generated = `Actualizado a las ${new Date().toLocaleTimeString('en-GB')}`; }
  catch (error) { rep.message = error.message; }
  if (redraw) render({ silent: true });
}
const range = (from, to) => { rep.from = from; rep.to = to; generate(); };

function periodControls() {
  return h('div', { class: 'report-period' },
    h('div', { class: 'report-dates' },
      h('label', { class: 'kpi-label' }, 'Desde', h('input', { type: 'date', value: rep.from, 'aria-label': 'Fecha desde', onchange: e => { rep.from = e.target.value; } })),
      h('label', { class: 'kpi-label' }, 'Hasta', h('input', { type: 'date', value: rep.to, 'aria-label': 'Fecha hasta', onchange: e => { rep.to = e.target.value; } })),
      h('button', { type: 'button', class: 'primary', onclick: () => generate() }, 'Generar'),
      h('span', { class: 'spacer' }),
      h('button', { type: 'button', class: 'secondary', disabled: rep.tab === 'profit' || !rep.data, 'aria-label': 'Exportar reporte a CSV', onclick: exportCsv }, 'Exportar CSV')),
    h('div', { class: 'report-quick' },
      h('span', { class: 'kpi-label', text: 'Rápido:' }),
      h('button', { type: 'button', class: 'secondary chip-button', onclick: () => range(today(), today()) }, 'Hoy'),
      h('button', { type: 'button', class: 'secondary chip-button', onclick: () => { const y = shiftDay(today(), -1); range(y, y); } }, 'Ayer'),
      h('button', { type: 'button', class: 'secondary chip-button', onclick: () => range(shiftDay(today(), -6), today()) }, 'Últimos 7 días'),
      h('button', { type: 'button', class: 'secondary chip-button', onclick: () => range(today().slice(0, 8) + '01', today()) }, 'Este mes'),
      h('span', { class: 'spacer' }), h('span', { class: 'kpi-label', role: 'status', text: rep.generated })));
}
function forecastControls() {
  return h('div', { class: 'report-dates' },
    h('label', { class: 'kpi-label' }, 'Día a planear', h('input', { type: 'date', value: rep.day, 'aria-label': 'Día a planear', onchange: e => { rep.day = e.target.value; } })),
    h('button', { type: 'button', class: 'primary', onclick: () => calculate() }, 'Calcular'),
    h('span', { class: 'kpi-label generated', text: rep.generated }), h('span', { class: 'spacer' }),
    h('button', { type: 'button', class: 'secondary', disabled: !rep.forecast, 'aria-label': 'Exportar pronóstico a CSV', onclick: exportCsv }, 'Exportar CSV'));
}

// ---------- Piezas comunes ----------
const kpi = (label, value, cls) => h('div', { class: 'report-kpi' }, h('span', { class: 'kpi-label', text: label }), h('strong', { class: `kpi-value ${cls ?? ''}`.trim(), text: value }));
const section = (title, caption) => [h('h3', { class: 'report-section', text: title }), caption ? h('p', { class: 'caption report-caption', text: caption }) : null];
const empty = text => h('p', { class: 'report-empty', text });
const chart = (...children) => h('div', { class: 'chart-card' }, ...children);
const grid = (columns, rows, label) => h('div', { class: 'table-wrap report-table' }, h('table', { class: 'cards', 'aria-label': label },
  h('thead', {}, h('tr', {}, columns.map(c => h('th', { class: c.num ? 'num' : null, text: c.label })))),
  h('tbody', {}, rows.map(row => h('tr', {}, columns.map(c => { const v = c.value(row); return h('td', { class: [c.num ? 'num' : '', c.cls?.(row) ?? ''].join(' ').trim() || null, 'data-label': c.label }, v); }))))));
function hbars(items, label) {
  return h('div', { class: 'hbars', role: 'list', 'aria-label': label }, items.map(b => {
    const fill = h('div', { class: 'hbar-fill' }); fill.style.width = `${Math.max(0, Math.min(1, b.ratio)) * 100}%`;
    return h('div', { class: 'hbar', role: 'listitem', title: b.tip }, h('span', { class: 'hbar-label', text: b.label }), h('div', { class: 'hbar-track' }, fill), h('b', { text: b.value }));
  }));
}
// Hasta 31 días una barra por día; más largo, una por cada 7 días desde el inicio (como la caja).
function buckets(days) {
  if (days.length <= 31) return days.map(d => ({ from: d.day, to: d.day, sales: d.sales, cents: d.amount }));
  const out = [];
  for (let i = 0; i < days.length; i += 7) { const c = days.slice(i, i + 7); out.push({ from: c[0].day, to: c.at(-1).day, sales: c.reduce((a, d) => a + d.sales, 0), cents: c.reduce((a, d) => a + d.amount, 0) }); }
  return out;
}
const span = b => b.from === b.to ? cap(longDay(b.from)) : `Del ${fmtDay(b.from, { day: 'numeric', month: 'short' })} al ${fmtDay(b.to, { day: 'numeric', month: 'short' })}`;
function barLabel(b, index, count) {
  const every = count <= 16 ? 1 : Math.ceil(count / 16);
  if (index % every !== 0) return '';
  return b.from !== b.to ? fmtDay(b.from, { day: 'numeric', month: 'short' }) : count <= 10 ? `${dayName(b.from).slice(0, 3)} ${Number(b.from.slice(8))}` : String(Number(b.from.slice(8)));
}

// ---------- Resumen ----------
function summary() {
  const d = rep.data; if (!d) return null;
  const s = d.summary;
  const current = buckets(d.days);
  const max = Math.max(1, ...current.map(b => b.cents));
  const totalNet = s.products.reduce((a, p) => a + p.net, 0), maxNet = Math.max(0, ...s.products.map(p => p.net));
  const top = s.products.slice(0, 10), topMax = Math.max(1, ...top.map(p => p.net));
  const netTotal = s.payments.reduce((a, m) => a + m.net, 0), methodMax = Math.max(1, ...s.payments.map(m => m.net));
  const pct = v => `${v.toFixed(1)} %`;
  return h('div', {},
    h('p', { class: 'report-period-label', text: period(d.from, d.to) }),
    h('div', { class: 'report-kpis five' }, kpi('Ventas', s.saleCount.toLocaleString('es-MX')), kpi('Vendido', fmt(s.gross)), kpi('Devuelto', fmt(s.refunded)), kpi('Neto', fmt(s.net)), kpi('Ticket promedio', fmt(s.averageTicket))),
    current.some(b => b.cents > 0) ? chart(h('h4', { class: 'chart-title', text: current.length && current[0].from !== current[0].to ? 'Ventas por semana' : 'Ventas por día' }),
      h('p', { class: 'kpi-label', text: 'Monto vendido sin ventas canceladas. Pasa el cursor sobre una barra para ver el detalle.', style: { margin: '2px 0 10px' } }),
      h('div', { class: 'vbars', role: 'list', 'aria-label': 'Gráfica de ventas por día' }, current.map((b, i) => {
        const bar = h('span', { class: 'vbar-fill' }); bar.style.height = `${barHeight(b.cents, max)}px`;
        return h('div', { class: 'vbar', role: 'listitem', title: `${span(b)} · ${plural(b.sales, 'venta', 'ventas')} · ${fmt(b.cents)}` },
          h('div', { class: 'vbar-area' }, h('span', { class: 'axis', text: current.length <= 14 && b.cents > 0 ? compact(b.cents) : '' }), bar), h('span', { class: 'axis', text: barLabel(b, i, current.length) }));
      }))) : null,
    top.length ? chart(h('h4', { class: 'chart-title', text: 'Productos más vendidos' }), h('p', { class: 'kpi-label', text: 'Los 10 con más venta neta del periodo.', style: { margin: '2px 0 8px' } }),
      hbars(top.map(p => ({ label: p.name, value: `${fmt(p.net)} · ${p.quantitySold - p.quantityRefunded} pzs`, ratio: p.net / topMax, tip: `${p.quantitySold} vendidas · ${p.quantityRefunded} devueltas · neto ${fmt(p.net)}` })), 'Gráfica de productos más vendidos')) : null,
    section('Productos'),
    s.products.length ? grid([
      { label: '#', num: true, value: p => String(s.products.indexOf(p) + 1) }, { label: 'Producto', value: p => p.name },
      { label: 'Vendidas', num: true, value: p => String(p.quantitySold) }, { label: 'Vendido', num: true, value: p => fmt(p.sold) },
      { label: 'Devueltas', num: true, value: p => p.quantityRefunded ? String(p.quantityRefunded) : '—' }, { label: 'Devuelto', num: true, value: p => p.refunded ? fmt(p.refunded) : '—' },
      { label: 'Neto', num: true, value: p => fmt(p.net) }, { label: '% neto', num: true, value: p => totalNet <= 0 ? '—' : `${(p.net * 100 / totalNet).toFixed(1)} %` },
    ], s.products, 'Ventas por producto') : empty('Sin ventas ni devoluciones en este periodo.'),
    h('div', { class: 'report-two' },
      h('div', {}, section('Formas de pago'), s.payments.length ? chart(h('p', { class: 'kpi-label', text: 'Neto y porcentaje por forma de pago; pasa el cursor para ver ventas y devoluciones.', style: { margin: '0 0 6px' } }),
        hbars(s.payments.map(m => ({ label: METHOD[m.method] ?? m.method, value: `${fmt(m.net)} · ${pct(netTotal <= 0 ? 0 : m.net * 100 / netTotal)}`, ratio: m.net / methodMax,
          tip: `${plural(m.sales, 'venta', 'ventas')} · vendido ${fmt(m.sold)} · devuelto ${fmt(m.refunded)}` })), 'Gráfica de ventas por forma de pago')) : empty('Sin cobros en este periodo.')),
      h('div', {}, section('Cajeros'), s.cashiers.length ? grid([
        { label: 'Cajero', value: c => c.name }, { label: 'Ventas', num: true, value: c => String(c.sales) },
        { label: 'Vendido', num: true, value: c => fmt(c.sold) }, { label: 'Promedio', num: true, value: c => c.sales ? fmt(Math.trunc(c.sold / c.sales)) : '—' },
      ], s.cashiers, 'Ventas por cajero') : empty('Sin ventas en este periodo.'))),
    section('Cortes de caja', 'Turnos que se abrieron dentro del periodo.'),
    s.sessions.length ? grid([
      { label: 'Turno', value: x => shiftLabel(x) }, { label: 'Abrió', value: x => x.openedBy }, { label: 'Fondo', num: true, value: x => fmt(x.opening) },
      { label: 'Efectivo', num: true, value: x => fmt(x.cashSales) }, { label: 'Devol.', num: true, value: x => x.refunds ? fmt(x.refunds) : '—' },
      { label: 'Mov.', num: true, value: x => x.movements ? `${x.movements > 0 ? '+' : '−'}${fmt(Math.abs(x.movements))}` : '—' },
      { label: 'Esperado', num: true, value: x => fmt(x.expected) }, { label: 'Contado', num: true, value: x => x.counted === null ? 'Sin conteo' : fmt(x.counted) },
      { label: 'Diferencia', value: x => x.difference === null ? '—' : x.difference === 0 ? 'Exacta' : x.difference > 0 ? `Sobrante ${fmt(x.difference)}` : `Faltante ${fmt(-x.difference)}`,
        cls: x => x.difference > 0 ? 'over' : x.difference < 0 ? 'short' : '' },
    ], s.sessions, 'Cortes de caja') : empty('No se abrió ninguna caja en este periodo.'));
}
const local = iso => new Date(iso).toLocaleString('en-GB', { timeZone: state.timeZone, day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).replace(',', '');
const localDay = iso => new Date(iso).toLocaleDateString('en-CA', { timeZone: state.timeZone });
function shiftLabel(x) {
  const opened = local(x.openedAt);
  if (!x.closedAt) return `${opened} – abierta`;
  return `${opened} – ${localDay(x.closedAt) === localDay(x.openedAt) ? local(x.closedAt).slice(-5) : local(x.closedAt)}`;
}

// ---------- Comparativo ----------
function comparison() {
  const d = rep.data; if (!d) return null;
  const c = d.comparison, s = d.summary, p = d.previous;
  const current = buckets(d.days), previous = buckets(d.previousDays);
  const pairMax = Math.max(1, ...current.map(b => b.cents), ...previous.map(b => b.cents));
  const coverage = p.saleCount > 0 && s.sessions.length !== p.sessions
    ? `Ojo: este periodo tiene ${plural(s.sessions.length, 'turno de caja', 'turnos de caja')} y el anterior ${plural(p.sessions, 'turno de caja', 'turnos de caja')}; si la sucursal abrió días distintos, la comparación no es pareja.` : '';
  const kpis = [['Ventas', s.saleCount.toLocaleString('es-MX'), p.saleCount.toLocaleString('es-MX'), c.salesChange, s.saleCount, p.saleCount],
    ['Neto', fmt(s.net), fmt(p.net), c.netChange, s.net, p.net], ['Ticket promedio', fmt(s.averageTicket), fmt(p.averageTicket), c.ticketChange, s.averageTicket, p.averageTicket]];
  return h('div', {},
    h('p', { class: 'report-period-label', text: `${period(d.from, d.to)} contra ${period(d.previousFrom, d.previousTo).toLocaleLowerCase('es-MX')}` }),
    p.saleCount === 0 ? empty('El periodo anterior no tiene ventas: los productos aparecen como «Nuevo».') : null,
    coverage ? h('p', { class: 'warning', text: coverage }) : null,
    h('div', { class: 'report-kpis three' }, kpis.map(([label, value, before, change, a, b]) => h('div', { class: 'report-kpi' },
      h('span', { class: 'kpi-label', text: label }), h('strong', { class: 'kpi-value', text: value }),
      h('span', { class: 'kpi-change' }, h('b', { class: trend(a, b) > 0 ? 'up' : trend(a, b) < 0 ? 'down' : 'muted', text: changeText(change, a, b) }), h('span', { class: 'kpi-label', text: ` antes ${before}` }))))),
    current.some(b => b.cents > 0) || previous.some(b => b.cents > 0) ? chart(
      h('div', { class: 'chart-head' }, h('div', {}, h('h4', { class: 'chart-title', text: 'Día por día' }), h('p', { class: 'kpi-label', text: 'Monto vendido de cada día junto al día equivalente del periodo anterior.' })),
        h('div', { class: 'legend' }, h('i', { class: 'swatch soft' }), h('span', { class: 'kpi-label', text: 'Periodo anterior' }), h('i', { class: 'swatch' }), h('span', { class: 'kpi-label', text: 'Este periodo' }))),
      h('div', { class: 'vbars pairs', role: 'list', 'aria-label': 'Gráfica comparativa por día' }, current.map((b, i) => {
        const before = previous[i];
        const now = h('span', { class: 'vbar-fill' }); now.style.height = `${barHeight(b.cents, pairMax)}px`;
        const old = h('span', { class: 'vbar-fill soft' }); old.style.height = `${barHeight(before?.cents ?? 0, pairMax)}px`;
        return h('div', { class: 'vbar', role: 'listitem', title: `${span(b)}: ${plural(b.sales, 'venta', 'ventas')} · ${fmt(b.cents)}${before ? `\nAntes, ${span(before)}: ${plural(before.sales, 'venta', 'ventas')} · ${fmt(before.cents)}` : ''}` },
          h('div', { class: 'vbar-area pair' }, old, now), h('span', { class: 'axis', text: barLabel(b, i, current.length) }));
      }))) : null,
    section('Productos'),
    c.products.length ? grid([
      { label: 'Producto', value: x => x.name }, { label: 'Piezas', num: true, value: x => String(x.quantity) }, { label: 'Antes', num: true, value: x => String(x.previousQuantity) },
      { label: 'Cambio', num: true, value: x => changeText(x.quantityChange, x.quantity, x.previousQuantity), cls: x => trend(x.quantity, x.previousQuantity) > 0 ? 'up' : trend(x.quantity, x.previousQuantity) < 0 ? 'down' : '' },
      { label: 'Neto', num: true, value: x => fmt(x.net) }, { label: 'Antes', num: true, value: x => fmt(x.previousNet) },
      { label: 'Cambio', num: true, value: x => changeText(x.netChange, x.net, x.previousNet), cls: x => trend(x.net, x.previousNet) > 0 ? 'up' : trend(x.net, x.previousNet) < 0 ? 'down' : '' },
    ], c.products, 'Comparativo por producto') : empty('Sin ventas en ninguno de los dos periodos.'));
}

// ---------- Ventas por hora ----------
function heatmap() {
  const d = rep.data; if (!d) return null;
  const hm = d.heatmap;
  const total = hm.rows.flatMap(r => r.cells).reduce((a, c) => a + c.sales, 0);
  const max = Math.max(1, ...hm.rows.flatMap(r => r.cells.map(c => c.sales)));
  const byHour = hm.rows[0].cells.map((_, i) => ({ hour: hm.firstHour + i, sales: hm.rows.reduce((a, r) => a + r.cells[i].sales, 0), amount: hm.rows.reduce((a, r) => a + r.cells[i].amount, 0) }));
  const hourMax = Math.max(1, ...byHour.map(c => c.sales));
  let peak = null;
  for (const r of hm.rows) for (const c of r.cells) if (c.sales > 0 && (!peak || c.sales > peak.sales)) peak = { day: WEEK[r.weekday - 1], hour: c.hour, sales: c.sales };
  const hh = n => String(n).padStart(2, '0');
  const cell = (where, c, scale) => {
    const fill = h('span', { class: 'heat-fill' }); fill.style.opacity = String(c.sales === 0 ? 0 : 0.12 + 0.88 * c.sales / scale);
    return h('span', { class: 'heat-cell' + (c.sales * 2 > scale ? ' hot' : ''), title: `${where} · ${hh(c.hour)}:00 a ${hh(c.hour)}:59 · ${plural(c.sales, 'venta', 'ventas')} · ${fmt(c.amount)}` },
      fill, h('span', { class: 'heat-text', text: c.sales ? c.sales.toLocaleString('es-MX') : '' }));
  };
  const columns = `58px repeat(${byHour.length}, minmax(26px, 1fr)) 132px`;
  const row = (label, cells, right) => { const r = h('div', { class: 'heat-row' }, h('span', { class: 'heat-day', text: label }), cells, right); r.style.gridTemplateColumns = columns; return r; };
  return h('div', {},
    h('p', { class: 'report-period-label', text: period(d.from, d.to), style: { marginBottom: '4px' } }),
    Date.parse(d.to) - Date.parse(d.from) < 6 * 86_400_000 ? h('p', { class: 'caption', text: 'Para ver el patrón de toda la semana elige «Últimos 7 días» o «Este mes».', style: { marginBottom: '6px' } }) : null,
    total === 0 ? empty('Sin ventas en este periodo.') : h('div', { class: 'heat-wrap' },
      peak ? h('div', { class: 'report-kpi peak' }, h('strong', { text: `Hora pico: ${cap(peak.day)} de ${hh(peak.hour)}:00 a ${hh(peak.hour)}:59 · ${plural(peak.sales, 'venta', 'ventas')}` })) : null,
      h('div', { class: 'heat-scroll' },
        row('', byHour.map(c => h('span', { class: 'kpi-label center', text: String(c.hour) })), h('span', { class: 'kpi-label right', text: 'Ventas del día' })),
        hm.rows.map(r => {
          const sales = r.cells.reduce((a, c) => a + c.sales, 0), amount = r.cells.reduce((a, c) => a + c.amount, 0);
          return row(cap(WEEK[r.weekday - 1]).slice(0, 3), r.cells.map(c => cell(cap(WEEK[r.weekday - 1]), c, max)),
            h('span', { class: 'heat-total' }, h('b', { text: sales ? sales.toLocaleString('es-MX') : '—' }), h('small', { class: 'kpi-label', text: sales ? fmt(amount) : '' })));
        }),
        row('Total', byHour.map(c => cell('Todos los días', c, hourMax)), h('span'))),
      h('div', { class: 'heat-legend' }, h('span', { class: 'kpi-label', text: 'Menos' }), [0, 0.12, 0.34, 0.56, 0.78, 1].map(o => { const b = h('i', { class: 'swatch' + (o === 0 ? ' track' : '') }); if (o) b.style.opacity = String(o); return b; }), h('span', { class: 'kpi-label', text: 'Más' })),
      h('p', { class: 'kpi-label', text: 'El número es la cantidad de ventas en esa hora; pasa el cursor para ver el monto. No incluye ventas canceladas.', style: { margin: '8px 0 0 58px' } })));
}

// ---------- Control de caja ----------
function cashControl() {
  const d = rep.data; if (!d) return null;
  const c = d.cashControl;
  const shortages = c.cashiers.reduce((a, x) => a + x.shortages, 0), overages = c.cashiers.reduce((a, x) => a + x.overages, 0);
  const refunds = c.reasons.reduce((a, x) => a + x.count, 0), refunded = c.reasons.reduce((a, x) => a + x.amount, 0);
  const reasonMax = Math.max(1, ...c.reasons.map(x => x.count));
  return h('div', {},
    h('p', { class: 'report-period-label', text: period(d.from, d.to) }),
    h('div', { class: 'report-kpis four' }, kpi('Faltantes', fmt(shortages), 'short'), kpi('Sobrantes', fmt(overages), 'over'), kpi('Devoluciones', refunds.toLocaleString('es-MX')), kpi('Monto devuelto', fmt(refunded))),
    section('Por cajero', 'Faltantes y sobrantes de los cortes que abrió cada quien. Las devoluciones se cuentan a quien cobró la venta original.'),
    c.cashiers.length ? grid([
      { label: 'Cajero', value: x => x.name },
      { label: 'Turnos', num: true, value: x => x.shifts === 0 ? '—' : x.closedShifts === x.shifts ? String(x.shifts) : `${x.closedShifts} de ${x.shifts}` },
      { label: 'Con diferencia', num: true, value: x => x.closedShifts === 0 ? '—' : String(x.withDifference) },
      { label: 'Faltantes', num: true, value: x => x.shortages ? fmt(x.shortages) : '—' }, { label: 'Sobrantes', num: true, value: x => x.overages ? fmt(x.overages) : '—' },
      { label: 'Neto', num: true, value: x => x.net === 0 ? (x.closedShifts === 0 ? '—' : 'Exacto') : `${x.net > 0 ? '+' : '−'}${fmt(Math.abs(x.net))}`, cls: x => x.net > 0 ? 'over' : x.net < 0 ? 'short' : '' },
      { label: 'Devol.', num: true, value: x => x.refunds ? String(x.refunds) : '—' }, { label: 'Devuelto', num: true, value: x => x.refunded ? fmt(x.refunded) : '—' },
    ], c.cashiers, 'Control de caja por cajero') : empty('Sin turnos ni devoluciones en este periodo.'),
    section('Motivos de devolución'),
    c.reasons.length ? chart(hbars(c.reasons.map(x => ({ label: x.reason, value: `${x.count} · ${fmt(x.amount)}`, ratio: x.count / reasonMax, tip: `${plural(x.count, 'devolución', 'devoluciones')} · ${fmt(x.amount)}` })), 'Motivos de devolución: veces y monto'))
      : empty('Sin devoluciones en este periodo.'));
}

// ---------- Producción (pronóstico) ----------
function forecastView() {
  const f = rep.forecast; if (!f) return null;
  if (!f.products.length) return empty('No hay ventas de ese día de la semana en las últimas semanas. Elige otro día o espera a tener más historial.');
  const name = dayName(f.target);
  const pluralDay = name.endsWith('s') ? name : name + 's';
  const max = Math.max(1, ...f.products.map(p => p.suggested));
  return h('div', {},
    h('h3', { class: 'forecast-title', text: `Para el ${longDay(f.target)}` }),
    h('p', { class: 'caption', text: `Promedio de los últimos ${f.weeks} ${pluralDay} con venta, más 10 % de margen, redondeado hacia arriba.${f.weeksWithData < f.weeks ? ` Hay datos de ${f.weeksWithData} de ${f.weeks} semanas: la sugerencia mejora con más historial.` : ''}`, style: { marginBottom: '10px' } }),
    h('div', { class: 'report-kpis one' }, kpi('Piezas sugeridas', f.total.toLocaleString('es-MX'))),
    grid([
      { label: 'Producto', value: p => p.name }, { label: 'Semana pasada', num: true, value: p => p.last ? String(p.last) : '—' },
      { label: 'Promedio', num: true, value: p => p.average.toFixed(1) },
      { label: 'Sugerido', value: p => { const fill = h('span', { class: 'hbar-fill' }); fill.style.width = `${p.suggested / max * 100}%`; return h('span', { class: 'suggested' }, h('span', { class: 'hbar-track small' }, fill), h('b', { text: String(p.suggested) })); } },
      { label: 'Días con venta', num: true, value: p => `${p.daysWithSales} de ${f.weeksWithData}` },
    ], f.products, 'Piezas sugeridas por producto'));
}

// ---------- CSV (mismos formatos que la caja: ReportService.ToCsv) ----------
function cell(value) {
  let text = value === null || value === undefined ? '' : String(value);
  if (text.length && '=+-@'.includes(text[0]) && !/^[+-]?\d+(\.\d+)?$/.test(text)) text = "'" + text;
  return /[,"\n\r]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}
const amount = cents => (cents / 100).toFixed(2);
const percent = v => v === null || v === undefined ? '' : v.toFixed(1);
function csvRows() {
  const d = rep.data, rows = [];
  const row = (...values) => rows.push(values.map(cell).join(','));
  if (rep.tab === 'forecast') {
    const f = rep.forecast;
    row('Pronóstico de producción Casa Miga', f.target, dayName(f.target), `${f.weeksWithData} de ${f.weeks} semanas`, 'margen 10 %'); row();
    row('Producto', 'Última semana', 'Promedio', 'Sugerido', 'Días con venta');
    for (const p of f.products) row(p.name, p.last, p.average.toFixed(1), p.suggested, p.daysWithSales);
    return [rows, `CasaMiga-produccion-${f.target}.csv`];
  }
  const s = d.summary, name = { summary: 'ventas', comparison: 'comparativo', heatmap: 'ventas-por-hora', cash: 'control-de-caja' }[rep.tab] ?? 'ventas';
  if (rep.tab === 'comparison') {
    const c = d.comparison, p = d.previous;
    row('Comparativo Casa Miga', d.from, d.to, 'contra', d.previousFrom, d.previousTo); row();
    row('Indicador', 'Periodo', 'Anterior', 'Cambio %');
    row('Ventas', s.saleCount, p.saleCount, percent(c.salesChange)); row('Neto', amount(s.net), amount(p.net), percent(c.netChange)); row('Ticket promedio', amount(s.averageTicket), amount(p.averageTicket), percent(c.ticketChange)); row();
    row('Producto', 'Piezas', 'Piezas antes', 'Cambio piezas %', 'Neto', 'Neto antes', 'Cambio neto %');
    for (const x of c.products) row(x.name, x.quantity, x.previousQuantity, percent(x.quantityChange), amount(x.net), amount(x.previousNet), percent(x.netChange));
  } else if (rep.tab === 'heatmap') {
    const hm = d.heatmap;
    row('Ventas por hora Casa Miga', d.from, d.to); row();
    row('Día', ...hm.rows[0].cells.map(c => `${String(c.hour).padStart(2, '0')}:00`), 'Ventas', 'Vendido');
    for (const r of hm.rows) row(WEEK[r.weekday - 1], ...r.cells.map(c => c.sales), r.cells.reduce((a, c) => a + c.sales, 0), amount(r.cells.reduce((a, c) => a + c.amount, 0)));
  } else if (rep.tab === 'cash') {
    const c = d.cashControl;
    row('Control de caja Casa Miga', d.from, d.to); row();
    row('Cajero', 'Turnos', 'Turnos contados', 'Con diferencia', 'Faltantes', 'Sobrantes', 'Neto', 'Devoluciones', 'Monto devuelto');
    for (const x of c.cashiers) row(x.name, x.shifts, x.closedShifts, x.withDifference, amount(x.shortages), amount(x.overages), amount(x.net), x.refunds, amount(x.refunded));
    row(); row('Motivo de devolución', 'Veces', 'Monto');
    for (const x of c.reasons) row(x.reason, x.count, amount(x.amount));
  } else {
    const at = iso => new Date(iso).toLocaleString('sv-SE', { timeZone: state.timeZone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
    row('Reporte de ventas Casa Miga', d.from, d.to); row();
    row('Ventas', 'Vendido', 'Devuelto', 'Neto', 'Ticket promedio'); row(s.saleCount, amount(s.gross), amount(s.refunded), amount(s.net), amount(s.averageTicket)); row();
    row('Producto', 'Piezas vendidas', 'Vendido', 'Piezas devueltas', 'Devuelto', 'Neto');
    for (const p of s.products) row(p.name, p.quantitySold, amount(p.sold), p.quantityRefunded, amount(p.refunded), amount(p.net));
    row(); row('Forma de pago', 'Ventas', 'Vendido', 'Devuelto', 'Neto');
    for (const m of s.payments) row(METHOD[m.method] ?? m.method, m.sales, amount(m.sold), amount(m.refunded), amount(m.net));
    row(); row('Cajero', 'Ventas', 'Vendido');
    for (const c of s.cashiers) row(c.name, c.sales, amount(c.sold));
    row(); row('Apertura', 'Cierre', 'Abrió', 'Estado', 'Fondo', 'Ventas efectivo', 'Devoluciones', 'Movimientos', 'Esperado', 'Contado', 'Diferencia');
    for (const x of s.sessions) row(at(x.openedAt), x.closedAt ? at(x.closedAt) : '', x.openedBy, x.closed ? 'Cerrada' : 'Abierta', amount(x.opening), amount(x.cashSales), amount(x.refunds), amount(x.movements), amount(x.expected),
      x.counted === null ? '' : amount(x.counted), x.difference === null ? '' : amount(x.difference));
  }
  return [rows, `CasaMiga-${name}-${d.from}_${d.to}.csv`];
}
/** Descarga con BOM UTF-8 para que Excel muestre los acentos (como la caja). */
function exportCsv() {
  const [rows, file] = csvRows();
  const blob = new Blob(['﻿' + rows.join('\r\n') + '\r\n'], { type: 'text/csv;charset=utf-8' });
  const link = h('a', { href: URL.createObjectURL(blob), download: file });
  document.body.append(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(link.href), 1000);
}
/** Pestaña inicial al llegar por una dirección vieja (#/resumen). */
export function setReportTab(id) { rep.tab = id; }
