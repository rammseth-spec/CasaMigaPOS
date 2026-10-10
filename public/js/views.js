// Pantallas de Casa Miga en línea. Cada una pide sus datos a /api/web/<op> y dibuja con h() (sin HTML en texto).
import { api, at, can, card, command, dialog, field, fmt, h, METHOD, pesos, quantity, range, render, ROLE, state, table, time, toast } from './app.js';
import { ICON } from './icons.js';
import { tuCaja, vender } from './pos.js';
import { ventas } from './sales.js';
import { reportsView, setReportTab } from './reports.js';
import { catalogo } from './catalog.js';
import { inventario } from './inventory.js';
import { recetas } from './recipes.js';
import { produccion } from './production.js';
import { charolas, usesRange as traysUseRange } from './trays.js';
import { usuarios } from './users.js';

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
// ---------- Recetas y producción ----------
// Inventario vive en inventory.js (copia de la pantalla de la caja).
// ---------- Usuarios ----------
// ---------- Cajas y cambios ----------
const KIND = { 'consignment.deliver': 'Entrega de charola', 'consignment.collect': 'Recolección de charola', 'consignment.payment': 'Cobro de charola', 'consignment.void': 'Anulación de charola',
  'category.save': 'Categoría', 'product.save': 'Producto', 'product.active': 'Activar/desactivar producto', 'user.create': 'Alta de usuario', 'user.update': 'Usuario',
  'user.active': 'Activar/desactivar usuario', 'user.reset_password': 'Restablecer contraseña', 'user.password': 'Cambio de contraseña', 'consignment.store.save': 'Tienda de charolas', 'consignment.template.save': 'Charola habitual',
  'inventory.cost': 'Costo de insumo', 'inventory.receive': 'Entrada de inventario', 'inventory.waste': 'Merma', 'inventory.count': 'Conteo de inventario',
  'inventory.opening': 'Saldo inicial', 'inventory.item.save': 'Artículo de inventario', 'inventory.item.active': 'Activar/desactivar artículo', 'inventory.unit.add': 'Presentación',
  'inventory.link': 'Asociar producto', 'inventory.unlink': 'Desvincular producto', 'inventory.consumption': 'Consumo automático por ventas',
  'production.record': 'Producción', 'product.photo': 'Foto de producto', 'product.photo.remove': 'Quitar foto de producto',
  'recipe.formula.create': 'Nueva receta', 'recipe.formula.draft': 'Borrador de receta', 'recipe.formula.publish': 'Publicar receta', 'recipe.formula.activate': 'Activar receta',
  'recipe.profile.create': 'Nuevo perfil productivo', 'recipe.profile.draft': 'Borrador de perfil', 'recipe.profile.publish': 'Publicar perfil', 'recipe.profile.activate': 'Activar perfil',
  'production.prepare': 'Preparar producción', 'production.confirm': 'Registrar producción' };
const STOCK_KINDS = new Set(['inventory.receive', 'inventory.waste', 'inventory.count', 'inventory.opening']);
const REASON = { Damaged: 'Dañado', Expired: 'Caducado', ProductionError: 'Error de producción', Spill: 'Derrame', Other: 'Otro', PhysicalCountVariance: 'Diferencia de conteo físico', DataCorrection: 'Corrección de datos' };
const UNIT = { kg: 'kg', g: 'g', L: 'L', ml: 'ml', piece: 'pieza' };
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
  if (STOCK_KINDS.has(c.kind)) return `${Number(p.quantity).toLocaleString('es-MX', { maximumFractionDigits: 3 })} ${UNIT[p.unitCode] ?? p.unitCode}${p.reason ? ` · ${REASON[p.reason] ?? p.reason}` : ''}`;
  if (c.kind === 'inventory.item.save') return `${p.code ? `${p.code} · ` : ''}${p.name}`;
  if (c.kind === 'inventory.item.active') return p.active ? 'Activar' : 'Desactivar';
  if (c.kind === 'inventory.unit.add') return `${p.code} · ${p.name}`;
  if (c.kind === 'inventory.consumption') return { Enable: 'Activar', Pause: 'Pausar', Resume: 'Reanudar' }[p.action] ?? p.action;
  if (c.kind === 'production.prepare') return `${p.outputs.reduce((a, o) => a + o.planned, 0)} piezas planeadas`;
  if (c.kind === 'production.confirm') return `${p.actuals.reduce((a, o) => a + o.actual, 0)} piezas reales`;
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
  { id: 'charolas', title: 'Charolas', subtitle: 'Charolas en tiendas: entregar, recoger y cobrar · Aparte de la caja del mostrador.', hint: 'Ir a Charolas', icon: ICON.tray, group: 1, permission: 'consignment.view', usesRange: () => traysUseRange(), manual: true, render: charolas },
  // Fechas propias (como la caja): no usa el periodo de arriba; tampoco se redibuja sola mientras se arma una devolución.
  { id: 'ventas', label: 'Ventas', title: 'Historial de ventas', subtitle: 'Ventas confirmadas · Reimpresión, cancelaciones y devoluciones.', hint: 'Ir a Historial de ventas', icon: ICON.receipt, group: 2, permission: 'sales.view', usesRange: false, manual: true, render: ventas },
  { id: 'reportes', title: 'Reportes', subtitle: 'Ventas, devoluciones y cortes de caja por periodo · Sólo lectura.', hint: 'Ir a Reportes', icon: ICON.chart, group: 2, permission: 'sales.view', usesRange: false, manual: true, render: reportes, tab: setReportTab },
  { id: 'catalogo', title: 'Catálogo', subtitle: 'Categorías y productos · Sin proceso de venta.', hint: 'Ir a Catálogo', icon: ICON.grid, group: 3, permission: 'catalog.view', usesRange: false, manual: true, render: catalogo },
  { id: 'inventario', title: 'Inventario', subtitle: 'Existencias y movimientos del almacén principal · consulta abajo el estado del consumo automático por ventas.', hint: 'Ir a Inventario', icon: ICON.box, group: 3, permission: 'inventory.view', usesRange: false, manual: true, render: inventario },
  { id: 'recetas', title: 'Recetas', subtitle: 'Fórmulas y perfiles productivos versionados.', hint: 'Ir a Recetas', icon: ICON.book, group: 3, permission: 'recipe.view', usesRange: false, manual: true, render: recetas },
  { id: 'produccion', title: 'Producción', subtitle: 'Planeación, confirmación e historial de producción.', hint: 'Ir a Producción', icon: ICON.chef, group: 3, permission: 'production.view', usesRange: false, manual: true, render: produccion },
  { id: 'usuarios', title: 'Usuarios', subtitle: 'Crea usuarios y restablece contraseñas olvidadas · Cada acción queda auditada.', hint: 'Ir a Usuarios', icon: ICON.users, group: 3, permission: 'users.manage', usesRange: false, manual: true, render: usuarios },
  { id: 'nube', title: 'Nube', subtitle: 'Cajas conectadas, cambios hechos en la web y respaldos.', hint: 'Ir a Nube', icon: ICON.cloud, group: 3, permission: 'sales.view', usesRange: false, render: nube, tab: id => { cloudTab = id; } },
];
