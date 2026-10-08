// Casa Miga en línea: sesión, rutas, filtros y piezas comunes. Todo texto entra con textContent (nunca HTML).
import { VIEWS } from './views.js';

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
  if (response.status === 401 && op !== 'login') { showLogin(''); throw new ApiError('Inicia sesión', 401); }
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

/** Envía un cambio a las cajas de la sucursal; avisa que se aplicará cuando la caja se conecte. */
export async function command(branchId, kind, payload) {
  const result = await post('command', { branchId, kind, payload });
  toast('Enviado. La caja lo aplica en cuanto se conecta (ver Cajas → Cambios).');
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
function allowed() { return VIEWS.filter(v => !v.permission || can(v.permission)); }
function current() {
  const id = location.hash.replace(/^#\/?/, '').split('?')[0] || (can('sales.view') ? 'resumen' : 'vender');
  return allowed().find(v => v.id === id) ?? allowed()[0];
}

function layout() {
  $('nav-links').replaceChildren(...allowed().map(v => h('a', { href: `#/${v.id}`, 'data-view': v.id, onclick: () => document.body.classList.remove('menu-open') }, icon(v.icon), h('span', { text: v.title }))));
  $('who-name').textContent = state.me.name;
  $('who-role').textContent = `${ROLE[state.me.role] ?? state.me.role}${state.me.admin ? ' · todas las sucursales' : ' · ' + (state.branches[0]?.name ?? '')}`;
  $('periods').replaceChildren(...PERIODS.map(([id, label]) => h('button', { type: 'button', class: 'chip', 'data-period': id, onclick: () => choose(id) }, label)));
  const select = $('branch');
  select.replaceChildren(...(state.me.admin ? [h('option', { value: '', text: 'Todas las sucursales' })] : []), ...state.branches.map(b => h('option', { value: b.id, text: b.name })));
  if (!state.me.admin || !state.branches.some(b => b.id === state.branch)) state.branch = state.me.admin ? '' : state.me.branchId;
  select.value = state.branch;
  select.hidden = state.branches.length < 2 && !state.me.admin;
}

let renderToken = 0;
/**
 * Dibuja la pantalla actual. En modo silencioso (actualización automática o botón) conserva lo que se ve
 * hasta tener datos nuevos y la posición de la página, para que no parpadee ni salte.
 */
export async function render({ silent = false } = {}) {
  const view = current(); state.view = view;
  if (!view) return;
  for (const a of $('nav-links').children) a.setAttribute('aria-current', a.dataset.view === view.id ? 'page' : 'false');
  $('view-title').textContent = view.title;
  $('view-subtitle').textContent = view.subtitle ?? '';
  $('periods').hidden = $('dates').hidden = !view.usesRange;
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
    $('updated').textContent = `Actualizado ${new Date().toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' })} ↻`;
  } catch (error) {
    if (token !== renderToken || error.status === 401) return;
    if (!silent) container.replaceChildren();
    $('error').textContent = error.message; $('error').hidden = false;
  } finally {
    if (token === renderToken) $('refresh').classList.remove('busy');
  }
}

/** Cada 60 s con la página visible y sin un formulario abierto; también al volver a la pestaña. */
const REFRESH_MS = 60_000;
let lastRefresh = Date.now();
function autoRefresh() {
  const typing = document.activeElement?.matches?.('#view input, #view select, #view textarea');
  if (!state.me || document.hidden || typing || document.querySelector('dialog[open]') || state.view?.manual) return;
  lastRefresh = Date.now();
  render({ silent: true });
}

// ---------- Acceso ----------
export function showLogin(message) {
  $('app').hidden = true; $('login').hidden = false; $('login-error').textContent = message || '';
  $('username').focus();
}
async function login(event) {
  event.preventDefault();
  const button = $('login-form').querySelector('button'); button.disabled = true; $('login-error').textContent = '';
  try {
    await post('login', { username: $('username').value, password: $('password').value });
    $('password').value = '';
    await start();
  } catch (e) { $('login-error').textContent = e.message; }
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
  $('logout').addEventListener('click', async () => {
    await post('logout', {}).catch(() => {}); state.me = null;
    setKey('cm-dev-out', '1', true); // en el servidor de prueba, no volver a entrar solo
    showLogin(''); showDevUsers();
  });
  $('menu').addEventListener('click', () => document.body.classList.toggle('menu-open'));
  document.addEventListener('click', e => { if (document.body.classList.contains('menu-open') && !e.target.closest('#nav') && !e.target.closest('#menu')) document.body.classList.remove('menu-open'); });
  const custom = () => { if ($('from').value && $('to').value) { state.from = $('from').value; state.to = $('to').value; choose('custom'); } };
  $('from').addEventListener('change', custom); $('to').addEventListener('change', custom);
  $('branch').addEventListener('change', () => { state.branch = $('branch').value; remember(); render(); });
  window.addEventListener('hashchange', () => { if (state.me) render(); });
  $('refresh').addEventListener('click', () => { lastRefresh = Date.now(); render({ silent: true }); });
  setInterval(() => { if (Date.now() - lastRefresh >= REFRESH_MS) autoRefresh(); }, 5_000);
  document.addEventListener('visibilitychange', () => { if (!document.hidden && Date.now() - lastRefresh >= 15_000) autoRefresh(); });
  start().catch(e => showLogin(e.status === 401 ? '' : e.message));
});
