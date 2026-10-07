'use strict';
// Panel de sucursales: sólo lectura. Todo el texto se inserta con textContent (nunca HTML).
(() => {
  const $ = id => document.getElementById(id);
  const money = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });
  const fmt = cents => money.format((cents || 0) / 100);
  const state = { period: 'today', from: '', to: '', branch: '', timeZone: 'America/Mexico_City', timer: 0 };

  function el(tag, props = {}, ...children) {
    const node = document.createElement(tag);
    for (const [k, v] of Object.entries(props)) {
      if (k === 'class') node.className = v; else if (k === 'text') node.textContent = v; else node.setAttribute(k, v);
    }
    for (const child of children) if (child !== null && child !== undefined) node.append(child);
    return node;
  }

  // ---- Fechas en la hora local del navegador ----
  const iso = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  function periodRange(period) {
    const today = new Date(); const start = new Date(today);
    if (period === 'yesterday') { start.setDate(today.getDate() - 1); return [iso(start), iso(start)]; }
    if (period === '7' || period === '30') { start.setDate(today.getDate() - Number(period) + 1); return [iso(start), iso(today)]; }
    if (period === 'month') { start.setDate(1); return [iso(start), iso(today)]; }
    return [iso(today), iso(today)];
  }
  const dayLabel = day => {
    const [y, m, d] = day.split('-').map(Number);
    const date = new Date(y, m - 1, d);
    return `${date.toLocaleDateString('es-MX', { weekday: 'short' })} ${d} ${date.toLocaleDateString('es-MX', { month: 'short' })}`.replace(/\./g, '');
  };
  const timeLabel = value => value ? new Date(value).toLocaleString('es-MX', { timeZone: state.timeZone, day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) : '';
  function ago(value) {
    if (!value) return 'Nunca se ha conectado';
    const minutes = Math.round((Date.now() - new Date(value).getTime()) / 60000);
    if (minutes < 1) return 'Conectada hace un momento';
    if (minutes < 60) return `Conectada hace ${minutes} min`;
    if (minutes < 48 * 60) return `Conectada hace ${Math.round(minutes / 60)} h`;
    return `Última conexión: ${timeLabel(value)}`;
  }

  function remember() { try { localStorage.setItem('cm-panel', JSON.stringify({ period: state.period, branch: state.branch })); } catch { /* sin almacenamiento */ } }
  function recall() {
    try { const saved = JSON.parse(localStorage.getItem('cm-panel') || '{}'); state.period = saved.period || 'today'; state.branch = saved.branch || ''; } catch { /* sin almacenamiento */ }
  }

  // ---- Acceso ----
  function showLogin(message) {
    $('app').hidden = true; $('login').hidden = false; $('login-error').textContent = message || '';
    clearInterval(state.timer); $('password').focus();
  }
  async function login(event) {
    event.preventDefault();
    const button = event.submitter || $('login-form').querySelector('button');
    button.disabled = true; $('login-error').textContent = '';
    try {
      const response = await fetch('/api/panel/login', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ password: $('password').value }) });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) { $('login-error').textContent = body.error || 'No se pudo entrar.'; return; }
      $('password').value = ''; start();
    } catch { $('login-error').textContent = 'Sin conexión. Intenta de nuevo.'; }
    finally { button.disabled = false; }
  }
  async function logout() {
    await fetch('/api/panel/logout', { method: 'POST' }).catch(() => {});
    showLogin('');
  }

  // ---- Datos ----
  async function load() {
    const params = new URLSearchParams({ from: state.from, to: state.to });
    if (state.branch) params.set('branch', state.branch);
    $('refresh').disabled = true; $('updated').textContent = 'Actualizando…';
    try {
      const response = await fetch('/api/panel/data?' + params, { headers: { accept: 'application/json' } });
      const body = await response.json().catch(() => ({}));
      if (response.status === 401 || response.status === 503) { showLogin(response.status === 503 ? 'El panel aún no tiene contraseña configurada.' : ''); return; }
      if (!response.ok) { $('error').textContent = body.error || 'No se pudieron leer los datos.'; $('error').hidden = false; return; }
      $('error').hidden = true; $('login').hidden = true; $('app').hidden = false;
      render(body);
      $('updated').textContent = 'Actualizado ' + new Date().toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' });
    } catch {
      $('error').textContent = 'Sin conexión con la nube. Las cajas siguen vendiendo; reintenta en un momento.'; $('error').hidden = false;
    } finally { $('refresh').disabled = false; }
  }

  function render(data) {
    state.timeZone = data.range.timeZone || state.timeZone;
    const branchNames = new Map(data.branches.map(b => [b.id, b.name]));
    const select = $('branch');
    if (select.options.length - 1 !== data.branches.length) {
      select.replaceChildren(el('option', { value: '', text: 'Todas las sucursales' }), ...data.branches.map(b => el('option', { value: b.id, text: b.name })));
    }
    select.value = state.branch;
    const multi = !state.branch && data.branches.length > 1;

    const t = data.totals;
    $('k-net').textContent = fmt(t.net);
    $('k-gross').textContent = `Vendido ${fmt(t.gross)}`;
    $('k-tickets').textContent = String(t.tickets);
    $('k-average').textContent = t.tickets ? `Promedio ${fmt(Math.round(t.gross / t.tickets))}` : 'Sin ventas';
    $('k-refunds').textContent = fmt(t.refunds);
    const diff = t.shortages + t.overages;
    $('k-diff').textContent = fmt(diff);
    $('k-diff').className = diff < 0 ? 'short' : diff > 0 ? 'over' : '';
    $('k-diff-detail').textContent = `Faltantes ${fmt(-t.shortages)} · Sobrantes ${fmt(t.overages)}`;

    // Ventas por día (todas las sucursales juntas o la elegida).
    const byDay = new Map();
    for (const d of data.days) {
      const row = byDay.get(d.day) || { net: 0, tickets: 0, parts: [] };
      row.net += d.gross - d.refunds; row.tickets += d.tickets; row.parts.push(`${branchNames.get(d.branchId) || 'Sucursal'} ${fmt(d.gross - d.refunds)}`);
      byDay.set(d.day, row);
    }
    const days = [...byDay.entries()].sort((a, b) => b[0].localeCompare(a[0]));
    const top = Math.max(1, ...days.map(([, r]) => r.net));
    $('days').replaceChildren(...(days.length ? days.map(([day, r]) => {
      const fill = el('div', { class: 'fill' }); fill.style.width = `${Math.max(0, r.net) / top * 100}%`;
      const item = el('div', { class: 'bar', role: 'listitem', title: multi ? r.parts.join(' · ') : `${r.tickets} tickets` },
        el('span', { class: 'label', text: dayLabel(day) }), el('div', { class: 'track' }, fill), el('b', { text: fmt(r.net) }));
      return item;
    }) : [el('p', { class: 'empty', text: 'Sin ventas en el periodo.' })]));

    const methods = [['Efectivo', t.byMethod.cash], ['Tarjeta', t.byMethod.card], ['Transferencia', t.byMethod.transfer]];
    const methodTop = Math.max(1, ...methods.map(m => m[1]));
    $('methods').replaceChildren(...methods.map(([name, cents]) => {
      const fill = el('div', { class: 'fill' }); fill.style.width = `${Math.max(0, cents) / methodTop * 100}%`;
      return el('div', { class: 'method' }, el('span', { text: name }), el('b', { text: fmt(cents) }), el('div', { class: 'track' }, fill));
    }));

    $('products').replaceChildren(...(data.products.length ? data.products.map(p =>
      el('li', {}, el('strong', { text: p.name }), el('span', { text: `${p.quantity} pz` }), el('b', { text: fmt(p.total) })))
      : [el('li', { class: 'empty', text: 'Sin ventas en el periodo.' })]));

    const body = $('sessions').tBodies[0];
    body.replaceChildren(...data.sessions.map(s => {
      const diffCell = s.difference === null ? el('td', { class: 'num', 'data-label': 'Diferencia' }, el('span', { class: 'pill open', text: 'Abierta' }))
        : el('td', { class: 'num ' + (s.difference < 0 ? 'short' : s.difference > 0 ? 'over' : 'even'), 'data-label': 'Diferencia', text: s.difference === 0 ? 'Cuadra' : fmt(s.difference) });
      const extra = [s.movements ? `Movimientos ${fmt(s.movements)}` : '', s.cashRefunds ? `Devoluciones −${fmt(s.cashRefunds)}` : ''].filter(Boolean).join(' · ');
      return el('tr', {},
        el('td', {}, document.createTextNode(s.terminal), multi ? el('small', { text: branchNames.get(s.branchId) || '' }) : null),
        el('td', {}, document.createTextNode(timeLabel(s.openedAt)), el('small', { text: s.closedAt ? `Cerró ${timeLabel(s.closedAt)}` : 'En curso' })),
        el('td', { class: 'num', 'data-label': 'Fondo', text: fmt(s.opening) }),
        el('td', { class: 'num', 'data-label': 'Esperado' }, document.createTextNode(fmt(s.expected)), extra ? el('small', { text: extra }) : null),
        el('td', { class: 'num', 'data-label': 'Contado', text: s.counted === null ? '—' : fmt(s.counted) }),
        diffCell);
    }));
    $('sessions-empty').hidden = data.sessions.length > 0;

    $('terminals').replaceChildren(...(data.terminals.length ? data.terminals.map(term => {
      const recent = term.lastSeenAt && Date.now() - new Date(term.lastSeenAt).getTime() < 30 * 60000;
      return el('li', {}, el('span', { class: 'dot ' + (term.revoked ? 'off' : recent ? 'on' : '') }),
        el('div', {}, el('strong', { text: term.name }), el('small', { text: branchNames.get(term.branchId) || '' })),
        el('span', { class: 'muted', text: term.revoked ? 'Dada de baja' : ago(term.lastSeenAt) }));
    }) : [el('li', { class: 'empty', text: 'Ninguna caja registrada todavía. Regístrala desde la pantalla Nube de la app.' })]));
  }

  // ---- Filtros ----
  function choose(period) {
    state.period = period;
    if (period !== 'custom') [state.from, state.to] = periodRange(period);
    $('from').value = state.from; $('to').value = state.to;
    for (const chip of document.querySelectorAll('[data-period]')) chip.setAttribute('aria-pressed', String(chip.dataset.period === period));
    remember(); load();
  }
  function custom() {
    if (!$('from').value || !$('to').value) return;
    state.from = $('from').value; state.to = $('to').value; choose('custom');
  }

  function start() {
    $('login').hidden = true;
    choose(state.period);
    clearInterval(state.timer);
    state.timer = setInterval(() => { if (!document.hidden && !$('app').hidden) load(); }, 120000);
  }

  document.addEventListener('DOMContentLoaded', () => {
    recall();
    $('login-form').addEventListener('submit', login);
    $('logout').addEventListener('click', logout);
    $('refresh').addEventListener('click', load);
    for (const chip of document.querySelectorAll('[data-period]')) chip.addEventListener('click', () => choose(chip.dataset.period));
    $('from').addEventListener('change', custom); $('to').addEventListener('change', custom);
    $('branch').addEventListener('change', () => { state.branch = $('branch').value; remember(); load(); });
    if (state.period === 'custom') state.period = 'today';
    start();
  });
})();
