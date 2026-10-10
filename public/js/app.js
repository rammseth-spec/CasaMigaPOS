// Casa Miga en línea: sesión, rutas, filtros y piezas comunes. Todo texto entra con textContent (nunca HTML).
import { ICON } from './icons.js';
import { VIEWS } from './views.js';

export { ICON };

const $ = id => document.getElementById(id);
export const state = { me: null, branches: [], period: 'today', from: '', to: '', branch: '', timeZone: 'America/Mexico_City', view: null };

// ---------- DOM ----------
export function h(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(props ?? {})) {
    if (v === null || v === undefined || v === false) continue;
    if (k === 'class') node.className = v;
    else if (k === 'text') node.textContent = v;
    else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
    else if (k === 'style') Object.assign(node.style, v);
    else if (v === true) node.setAttribute(k, '');
    else node.setAttribute(k, v);
  }
  for (const child of children.flat(Infinity)) if (child !== null && child !== undefined && child !== false) node.append(child instanceof Node ? child : String(child));
  return node;
}
const SVG = 'http://www.w3.org/2000/svg';
export function icon(path) {
  const svg = document.createElementNS(SVG, 'svg'); svg.setAttribute('viewBox', '0 0 24 24'); svg.setAttribute('fill', 'none');
  svg.setAttribute('stroke', 'currentColor'); svg.setAttribute('stroke-width', '1.8'); svg.setAttribute('stroke-linecap', 'round'); svg.setAttribute('stroke-linejoin', 'round');
  svg.setAttribute('aria-hidden', 'true');
  for (const d of [].concat(path)) { const p = document.createElementNS(SVG, 'path'); p.setAttribute('d', d); svg.append(p); }
  return svg;
}

// ---------- Formato ----------
const money = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });
export const fmt = cents => money.format((cents || 0) / 100);
// I1: lo cambiado en la web se ve al momento; mientras la caja no lo confirma, se marca.
export const pendingPill = x => x?.pending ? h('span', { class: 'pill warn pending-pill', title: 'Ya se ve en la web; la caja lo confirmará al conectarse', text: 'Por confirmar' }) : null;
export const pesos = cents => ((cents || 0) / 100).toFixed(2);
export const at = (iso, options = { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) =>
  iso ? new Date(iso).toLocaleString('es-MX', { timeZone: state.timeZone, ...options }) : '';
export const time = iso => at(iso, { hour: '2-digit', minute: '2-digit' });
export const dateLong = iso => at(iso, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
export function dayLabel(day) {
  const [y, m, d] = day.split('-').map(Number); const date = new Date(y, m - 1, d);
  return `${date.toLocaleDateString('es-MX', { weekday: 'short' })} ${d} ${date.toLocaleDateString('es-MX', { month: 'short' })}`.replace(/\./g, '');
}
export const METHOD = { CASH: 'Efectivo', CARD: 'Tarjeta', TRANSFER: 'Transferencia' };
export const ROLE = { Administrator: 'Administrador', Manager: 'Encargado', Cashier: 'Cajero' };
/** Cantidad en unidades atómicas (mg, ml, piezas…) con la unidad más grande que quepa. */
export function quantity(atomic, dimension, measures) {
  const units = (measures || []).filter(u => u.dimension === dimension).sort((a, b) => b.atomic - a.atomic);
  // Cero se muestra en la unidad mayor (0 kg, no 0 mg).
  const unit = atomic === 0 ? units[0] : units.find(u => Math.abs(atomic) >= u.atomic) ?? units[units.length - 1];
  if (!unit) return String(atomic);
  const value = atomic / unit.atomic;
  return `${value.toLocaleString('es-MX', { maximumFractionDigits: 3 })} ${unit.symbol}`;
}

// ---------- Red ----------
export class ApiError extends Error { constructor(message, status) { super(message); this.status = status; } }
async function request(op, init = {}, params = {}) {
  const query = new URLSearchParams(Object.entries(params).filter(([, v]) => v !== '' && v !== null && v !== undefined));
  let response;
  // toString() y no .size: Safari anterior a iOS 17 no tiene URLSearchParams.size.
  const qs = query.toString();
  try { response = await fetch(`/api/web/${op}${qs ? '?' + qs : ''}`, { ...init, headers: { accept: 'application/json', ...(init.body ? { 'content-type': 'application/json' } : {}) } }); }
  catch { throw new ApiError('Sin conexión con la nube. Las cajas siguen vendiendo; reintenta en un momento.', 0); }
  const body = await response.json().catch(() => ({}));
  if (response.status === 401 && op !== 'login' && op !== 'password') { showLogin(''); throw new ApiError('Inicia sesión', 401); }
  if (!response.ok) throw new ApiError(body.error || 'No se pudo completar.', response.status);
  return body;
}
export const api = (op, params = {}) => request(op, {}, params);
export const post = (op, body) => request(op, { method: 'POST', body: JSON.stringify(body) });
/** Periodo y sucursal actuales para las consultas. */
export const range = (extra = {}) => ({ from: state.from, to: state.to, branch: state.branch, ...extra });
export const can = permission => state.me?.permissions.includes(permission);

// ---------- Avisos y diálogos ----------
export function toast(message) {
  const node = h('div', { class: 'toast', role: 'status', text: message });
  document.body.append(node); setTimeout(() => node.remove(), 4200);
}
export function dialog(title, build, { submit = 'Guardar', onSubmit, wide = false, cancel = 'Cancelar' } = {}) {
  const error = h('p', { class: 'error', role: 'alert' });
  const body = h('div', { class: 'form-grid' });
  const form = h('form', { method: 'dialog', novalidate: true },
    h('header', {}, h('h2', { text: title }), h('button', { type: 'button', class: 'icon ghost', 'aria-label': 'Cerrar', onclick: () => close() }, '✕')),
    body, error,
    onSubmit ? h('footer', {}, cancel ? h('button', { type: 'button', onclick: () => close() }, cancel) : null, h('button', { type: 'submit', class: 'primary' }, submit)) : null);
  const node = h('dialog', { 'aria-label': title }, form);
  if (wide) node.style.width = 'min(860px, calc(100vw - 32px))';
  const close = () => { node.close(); node.remove(); };
  build(body, close);
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (!onSubmit) return close();
    const button = form.querySelector('button[type=submit]'); button.disabled = true; error.textContent = '';
    try { if ((await onSubmit(form, close)) !== false) close(); }
    catch (e) { error.textContent = e.message; }
    finally { button.disabled = false; }
  });
  node.addEventListener('cancel', event => { event.preventDefault(); close(); });
  document.body.append(node); node.showModal();
  return { node, close };
}
export function field(label, input, wide = false) { return h('label', { class: 'field' + (wide ? ' wide' : '') }, label, input); }

// I1: estos cambios se ven en la web desde ya; los demás (inventario, producción, recetas, ruta) esperan a que la caja los aplique.
const AT_ONCE = /^(category\.save|product\.(save|active)|user\.(create|update|active|reset_password)|consignment\.(store|template)\.save)$/;
/** Envía un cambio a las cajas de la sucursal y avisa cuándo se verá. */
export async function command(branchId, kind, payload) {
  const result = await post('command', { branchId, kind, payload });
  toast(AT_ONCE.test(kind) ? 'Guardado: ya se ve en la web. La caja lo confirma al conectarse (si lo rechaza, se regresa).'
    : 'Enviado. La caja lo aplica en cuanto se conecta (ver Nube → Cajas y cambios).');
  return result;
}

// ---------- Tablas ----------
/** columns: [{ label, value(row) → Node|string, num?, span? }]. En celular cada renglón es una tarjeta. */
export function table(columns, rows, { onclick, empty = 'Sin datos en el periodo.' } = {}) {
  if (!rows.length) return h('p', { class: 'empty', text: empty });
  return h('div', { class: 'table-wrap' }, h('table', { class: 'cards' },
    h('thead', {}, h('tr', {}, columns.map(c => h('th', { class: c.num ? 'num' : null, text: c.label })))),
    h('tbody', {}, rows.map(row => h('tr', { class: onclick ? 'click' : null, tabindex: onclick ? '0' : null, onclick: onclick ? () => onclick(row) : null,
      onkeydown: onclick ? e => { if (e.key === 'Enter') onclick(row); } : null },
      columns.map(c => h('td', { class: [c.num ? 'num' : '', c.span ? 'span' : ''].join(' ').trim() || null, 'data-label': c.label }, c.value(row))))))));
}
export function bars(items, { value, label, title } = {}) {
  const top = Math.max(1, ...items.map(value));
  return h('div', { class: 'bars', role: 'list' }, items.map(item => {
    const fill = h('div', { class: 'fill' }); fill.style.width = `${Math.max(0, value(item)) / top * 100}%`;
    return h('div', { class: 'bar', role: 'listitem', title: title ? title(item) : null }, h('span', { class: 'label', text: label(item) }), h('div', { class: 'track' }, fill), h('b', { text: fmt(value(item)) }));
  }));
}
export const card = (title, ...children) => h('article', { class: 'card' }, title ? h('header', {}, typeof title === 'string' ? h('h2', { text: title }) : title) : null, ...children);

// ---------- Periodo ----------
const PERIODS = [['today', 'Hoy'], ['yesterday', 'Ayer'], ['7', '7 días'], ['month', 'Este mes'], ['30', '30 días']];
// «Hoy» es el de la sucursal (hora de México), no el del aparato: alguien de viaje ve los mismos días que la caja.
const todayThere = () => new Date().toLocaleDateString('en-CA', { timeZone: state.timeZone });
const shiftDay = (day, days) => { const d = new Date(day + 'T12:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
function periodRange(period) {
  const today = todayThere();
  if (period === 'yesterday') { const y = shiftDay(today, -1); return [y, y]; }
  if (period === '7' || period === '30') return [shiftDay(today, -Number(period) + 1), today];
  if (period === 'month') return [today.slice(0, 8) + '01', today];
  return [today, today];
}
function remember() { try { localStorage.setItem('cm-web', JSON.stringify({ period: state.period, branch: state.branch })); } catch { /* sin almacenamiento */ } }
function recall() { try { const saved = JSON.parse(localStorage.getItem('cm-web') || '{}'); state.period = saved.period || 'today'; state.branch = saved.branch || ''; } catch { /* sin almacenamiento */ } }

function choose(period, reload = true) {
  state.period = period;
  if (period !== 'custom') [state.from, state.to] = periodRange(period);
  $('from').value = state.from; $('to').value = state.to;
  for (const chip of $('periods').children) chip.setAttribute('aria-pressed', String(chip.dataset.period === period));
  remember(); if (reload) render();
}

// ---------- Rutas y marco ----------
const allows = v => [].concat(v.permission ?? []).some(p => can(p)) || !v.permission;
function allowed() { return VIEWS.filter(allows); }
// Direcciones de antes del marco de escritorio: Resumen vive en Reportes; Cajas y Respaldos, en Nube.
const MOVED = { resumen: ['reportes', 'summary'], cajas: ['nube', 'terminals'], respaldos: ['nube', 'backups'] };
function current() {
  let id = location.hash.replace(/^#\/?/, '').split('?')[0];
  if (MOVED[id]) { const [view, tab] = MOVED[id]; VIEWS.find(v => v.id === view)?.tab?.(tab); history.replaceState(null, '', `#/${view}`); id = view; }
  id ||= can('pos.use') ? 'vender' : 'reportes';
  return allowed().find(v => v.id === id) ?? allowed()[0];
}
const usesRange = view => typeof view.usesRange === 'function' ? view.usesRange() : view.usesRange;
export const initials = name => String(name ?? '').split(/\s+/).filter(Boolean).slice(0, 2).map(w => w[0].toLocaleUpperCase('es-MX')).join('');

/**
 * Estado de la caja web de este dispositivo en la barra superior («Caja abierta» / «Caja cerrada», como en la caja de escritorio)
 * y su nombre en la ficha del usuario. Lo actualizan Venta y Caja cada vez que leen la caja; null lo oculta.
 */
export function setCashStatus(cash) {
  state.cash = cash;
  $('cash-pill').hidden = !cash;
  if (cash) { $('cash-label').textContent = cash.open ? 'Caja abierta' : 'Caja cerrada'; $('cash-pill').querySelector('.dot').className = 'dot' + (cash.open ? ' ok' : ''); }
  $('user-detail').textContent = `${ROLE[state.me.role] ?? state.me.role} · ${cash?.till ?? (state.me.admin && !state.branch ? 'todas las sucursales' : state.branches.find(b => b.id === (state.branch || state.me.branchId))?.name ?? '')}`;
}
/** Título y subtítulo de la barra superior cuando una pantalla cambia de paso (como «Abrir caja» o «Conteo de caja» en la caja). */
export function setHeading(title, subtitle = '') { $('view-title').textContent = title; $('view-subtitle').textContent = subtitle; }
function setCloud(ok) {
  const pill = $('refresh');
  pill.querySelector('.dot').className = 'dot ' + (ok ? 'ok' : 'warn');
  $('updated').textContent = ok ? 'Nube: al día' : 'Nube: sin conexión';
  pill.title = ok ? `Actualizado ${new Date().toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit', second: '2-digit' })} · se actualiza sola; toca para hacerlo ahora.`
    : 'No se pudo leer la nube; se reintenta sola. Toca para intentarlo ahora.';
}

function layout() {
  // Mismo orden y grupos que el riel de la caja: Venta, Caja, Charolas | Ventas, Reportes | Catálogo … Nube.
  const links = [];
  let group = null;
  for (const v of allowed().filter(v => !v.hidden)) {
    if (group !== null && v.group !== group) links.push(h('hr', { class: 'nav-sep' }));
    group = v.group;
    links.push(h('a', { href: `#/${v.id}`, class: 'nav-item', 'data-view': v.id, title: v.hint ?? v.title, onclick: () => document.body.classList.remove('menu-open') }, icon(v.icon), h('span', { text: v.label ?? v.title })));
  }
  $('nav-links').replaceChildren(...links);
  $('logout').replaceChildren(icon(ICON.logout), h('span', { text: 'Salir' }));
  $('who-name').textContent = state.me.name;
  $('who-role').textContent = `${ROLE[state.me.role] ?? state.me.role}${state.me.admin ? ' · todas las sucursales' : ' · ' + (state.branches[0]?.name ?? '')}`;
  $('user-initials').textContent = initials(state.me.name);
  $('user-name').textContent = state.me.name;
  const full = $('fullscreen');
  full.hidden = !document.fullscreenEnabled;
  const drawFull = () => full.replaceChildren(icon(document.fullscreenElement ? ICON.collapse : ICON.expand));
  drawFull(); document.onfullscreenchange = drawFull;
  $('periods').replaceChildren(...PERIODS.map(([id, label]) => h('button', { type: 'button', class: 'chip', 'data-period': id, onclick: () => choose(id) }, label)));
  const select = $('branch');
  select.replaceChildren(...(state.me.admin ? [h('option', { value: '', text: 'Todas las sucursales' })] : []), ...state.branches.map(b => h('option', { value: b.id, text: b.name })));
  if (!state.me.admin || !state.branches.some(b => b.id === state.branch)) state.branch = state.me.admin ? '' : state.me.branchId;
  select.value = state.branch;
  select.hidden = state.branches.length < 2;
  setCashStatus(state.cash ?? null);
}

let renderToken = 0;
/**
 * Dibuja la pantalla actual. En modo silencioso (actualización automática o botón) conserva lo que se ve
 * hasta tener datos nuevos y la posición de la página, para que no parpadee ni salte.
 */
export async function render({ silent = false } = {}) {
  const view = current(); state.view = view;
  if (!view) return;
  for (const a of $('nav-links').querySelectorAll('a')) a.setAttribute('aria-current', a.dataset.view === view.id ? 'page' : 'false');
  $('view-title').textContent = view.title;
  $('view-subtitle').textContent = (typeof view.subtitle === 'function' ? view.subtitle() : view.subtitle) ?? '';
  $('periods').hidden = $('dates').hidden = !usesRange(view);
  $('filters').hidden = !usesRange(view) && $('branch').hidden;
  $('error').hidden = true;
  const token = ++renderToken;
  const container = $('view');
  const scroll = window.scrollY;
  if (!silent) container.replaceChildren(h('p', { class: 'muted', text: 'Cargando…' }));
  $('refresh').classList.add('busy');
  try {
    const node = await view.render({ branchId: () => state.branch || (state.me.admin ? (state.branches[0]?.id ?? '') : state.me.branchId) });
    if (token !== renderToken) return;
    container.replaceChildren(node);
    if (silent) window.scrollTo(0, scroll);
    setCloud(true);
  } catch (error) {
    if (token !== renderToken || error.status === 401) return;
    if (!silent) container.replaceChildren();
    if (error.status === 0) setCloud(false);
    $('error').textContent = error.message; $('error').hidden = false;
  } finally {
    if (token === renderToken) $('refresh').classList.remove('busy');
  }
}

/**
 * Cada 15 s con la página visible y sin un formulario abierto; también al volver a la pestaña. Las cajas suben cada 5 s,
 * así que lo vendido en la caja aparece aquí en menos de 20 s.
 */
const REFRESH_MS = 15_000;
let lastRefresh = Date.now();
function autoRefresh() {
  const typing = document.activeElement?.matches?.('#view input, #view select, #view textarea');
  if (!state.me || document.hidden || typing || document.querySelector('dialog[open]') || state.view?.manual) return;
  lastRefresh = Date.now();
  render({ silent: true });
}

// ---------- Acceso ----------
export function showLogin(message) {
  $('app').hidden = true; $('login').hidden = false; $('password-form').hidden = true; $('login-form').hidden = false;
  $('login-error').textContent = message || '';
  $('username').focus();
}
async function login(event) {
  event.preventDefault();
  const button = $('login-form').querySelector('button'); button.disabled = true; $('login-error').textContent = '';
  try {
    await post('login', { username: $('username').value, password: $('password').value });
    $('password').value = '';
    await start();
  } catch (e) {
    // Contraseña temporal: como la caja, primero se crea la definitiva.
    if (e.status === 428) showPasswordForm($('username').value, $('password').value, e.message);
    else $('login-error').textContent = e.message;
  }
  finally { button.disabled = false; }
}

// ---------- Cambiar contraseña (PasswordPolicy de la caja) ----------
const PASSWORD_RULES = [
  ['De 8 a 16 caracteres', p => [...p].length >= 8 && [...p].length <= 16], ['Una mayúscula', p => /\p{Lu}/u.test(p)], ['Una minúscula', p => /\p{Ll}/u.test(p)],
  ['Un número', p => /\p{Nd}/u.test(p)], ['Un carácter especial (# $ % & * ! …)', p => [...p].some(c => !/[\p{L}\p{Nd}\s\p{Cc}]/u.test(c))]];
function drawChecklist() {
  const value = $('pw-next').value;
  $('pw-checklist').replaceChildren(...PASSWORD_RULES.map(([label, ok]) => h('li', { class: ok(value) ? 'ok' : '', text: `${ok(value) ? '✓' : '•'} ${label}` })));
}
function showPasswordForm(username = '', current = '', message = '') {
  $('login-form').hidden = true; $('password-form').hidden = false;
  $('pw-username').value = username; $('pw-current').value = current; $('pw-next').value = ''; $('pw-confirm').value = '';
  $('pw-error').textContent = message; $('pw-error').className = message ? 'muted' : 'error';
  drawChecklist();
  (username ? (current ? $('pw-next') : $('pw-current')) : $('pw-username')).focus();
}
async function changePassword(event) {
  event.preventDefault();
  const username = $('pw-username').value, current = $('pw-current').value, next = $('pw-next').value;
  const fail = message => { $('pw-error').className = 'error'; $('pw-error').textContent = message; };
  const missing = PASSWORD_RULES.filter(([, ok]) => !ok(next)).map(([label]) => label.toLowerCase());
  if (missing.length) return fail(`La contraseña debe tener ${missing.join(', ')}.`);
  if (next !== $('pw-confirm').value) return fail('La confirmación no coincide con la contraseña nueva.');
  if (next === current) return fail('La contraseña nueva debe ser distinta de la actual.');
  const button = $('password-form').querySelector('button[type=submit]'); button.disabled = true;
  try {
    await post('password', { username, current, next });
    await post('login', { username, password: next });
    $('password-form').reset();
    toast('Contraseña guardada. La caja la recibe en cuanto se conecta.');
    await start();
  } catch (e) { fail(e.message); }
  finally { button.disabled = false; }
}
// Servidor de prueba sin contraseña (npm run dev:web): elegir usuario. En la nube publicada /dev-users no existe.
let devUsers = null;
// session: sólo esta pestaña. Todo dentro de try: el almacenamiento puede estar bloqueado.
const setKey = (key, value, session = false) => { try { const s = session ? sessionStorage : localStorage; if (value === null) s.removeItem(key); else s.setItem(key, value); } catch { /* sin almacenamiento */ } };
const getKey = (key, session = false) => { try { return (session ? sessionStorage : localStorage).getItem(key); } catch { return null; } };
async function devEnter(username) {
  await post('dev-login', { username });
  setKey('cm-dev-user', username); setKey('cm-dev-out', null, true);
}
function showDevUsers() {
  const box = $('dev-users');
  box.hidden = !devUsers?.length;
  if (!devUsers?.length) return;
  box.replaceChildren(h('p', { class: 'muted small', text: 'Servidor de prueba: entra sin contraseña.' }),
    ...devUsers.map(u => h('button', { type: 'button', onclick: async () => {
      try { await devEnter(u.username); await start(); } catch (e) { $('login-error').textContent = e.message; }
    } }, h('strong', { text: u.name }), h('span', { class: 'muted small', text: ROLE[u.role] ?? u.role }))));
}
async function start() {
  let me;
  try { me = await api('me'); }
  catch (error) {
    if (error.status !== 401) throw error;
    devUsers ??= await fetch('/api/web/dev-users').then(r => (r.ok ? r.json() : [])).catch(() => []);
    showDevUsers();
    if (!devUsers.length || getKey('cm-dev-out', true)) throw error;
    const user = devUsers.find(u => u.username === getKey('cm-dev-user')) ?? devUsers[0];
    await devEnter(user.username);
    me = await api('me');
  }
  state.me = me; state.branches = me.branches; state.timeZone = me.timeZone;
  $('login').hidden = true; $('app').hidden = false;
  layout(); choose(state.period === 'custom' ? 'today' : state.period, false);
  if (!location.hash) location.hash = `#/${allowed()[0]?.id ?? 'resumen'}`; else render();
}

document.addEventListener('DOMContentLoaded', () => {
  recall();
  $('login-form').addEventListener('submit', login);
  $('password-form').addEventListener('submit', changePassword);
  $('pw-next').addEventListener('input', drawChecklist);
  $('pw-cancel').addEventListener('click', () => showLogin(''));
  $('change-password').addEventListener('click', () => showPasswordForm($('username').value));
  $('forgot').addEventListener('click', () => { $('login-help').hidden = !$('login-help').hidden; });
  $('logout').addEventListener('click', async () => {
    await post('logout', {}).catch(() => {}); state.me = null;
    setKey('cm-dev-out', '1', true); // en el servidor de prueba, no volver a entrar solo
    showLogin(''); showDevUsers();
  });
  $('menu').addEventListener('click', () => document.body.classList.toggle('menu-open'));
  // Pantalla completa, como el botón de la caja (F11); en iPhone no existe y el botón no aparece.
  $('fullscreen').addEventListener('click', () => (document.fullscreenElement ? document.exitFullscreen() : document.documentElement.requestFullscreen()).catch(() => {}));
  document.addEventListener('click', e => { if (document.body.classList.contains('menu-open') && !e.target.closest('#nav') && !e.target.closest('#menu')) document.body.classList.remove('menu-open'); });
  const custom = () => { if ($('from').value && $('to').value) { state.from = $('from').value; state.to = $('to').value; choose('custom'); } };
  $('from').addEventListener('change', custom); $('to').addEventListener('change', custom);
  $('branch').addEventListener('change', () => { state.branch = $('branch').value; remember(); window.dispatchEvent(new Event('cm-branch')); render(); });
  window.addEventListener('hashchange', () => { if (state.me) render(); });
  $('refresh').addEventListener('click', () => { lastRefresh = Date.now(); render({ silent: true }); });
  setInterval(() => { if (Date.now() - lastRefresh >= REFRESH_MS) autoRefresh(); }, 5_000);
  document.addEventListener('visibilitychange', () => { if (!document.hidden && Date.now() - lastRefresh >= 15_000) autoRefresh(); });
  start().catch(e => showLogin(e.status === 401 ? '' : e.message));
});
