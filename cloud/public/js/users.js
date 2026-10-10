// Usuarios como la caja de escritorio (panel Usuarios de QAv1): «Usuarios registrados», editar el seleccionado (nombre, rol, desactivar,
// restablecer contraseña con confirmación), aviso de la contraseña temporal y «Nuevo usuario». Los cambios bajan a las cajas de la sucursal.
import { api, command, dialog, h, render, ROLE, state } from './app.js';

const u = { selected: null, edit: null, notice: '', message: '', ok: false, form: { username: '', name: '', role: 'Cashier', branchId: '' } };
const say = (message, ok = false) => { u.message = message; u.ok = ok; };
const redraw = () => render({ silent: true });
const multi = () => !state.branch && state.branches.length > 1;
const ROLES = [['Cashier', 'Cajero'], ['Manager', 'Encargado']];
const status = x => !x.active ? 'Inactivo' : x.mustChangePassword ? 'Debe cambiar contraseña' : 'Activo';
const notice = (username, temporary) => `Contraseña temporal de ${username.toUpperCase()}: ${temporary}\nEntrégala en persona. Sólo se muestra esta vez y deberá cambiarla al iniciar sesión (en la caja o en la web).`;

export async function usuarios() {
  const people = await api('users', { branch: state.branch });
  const selected = people.find(p => p.id === u.selected) ?? null;
  if (selected && (!u.edit || u.edit.id !== selected.id)) u.edit = { id: selected.id, name: selected.name, role: selected.role };
  const admin = selected?.role === 'Administrator';
  const rows = people.map(p => { const tr = h('tr', { class: 'click' + (p.id === u.selected ? ' selected' : '') + (p.active ? '' : ' off'), tabindex: '0',
    onclick: () => { u.selected = p.id; u.edit = null; say(''); redraw(); }, onkeydown: e => { if (e.key === 'Enter') tr.click(); } },
    h('td', { 'data-label': 'Usuario', text: p.username }), h('td', { 'data-label': 'Nombre', text: p.name + (multi() ? ` · ${p.branch}` : '') }),
    h('td', { 'data-label': 'Rol', text: ROLE[p.role] ?? p.role }), h('td', { 'data-label': 'Estado', text: status(p) }));
    return tr; });
  const f = u.form;
  f.branchId ||= state.branch || state.me.branchId || state.branches[0]?.id;
  return h('div', { class: 'cash-card users' },
    h('h2', { class: 'detail-title', text: 'Usuarios registrados' }),
    people.length ? h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Usuarios registrados' },
      h('thead', {}, h('tr', {}, ['Usuario', 'Nombre', 'Rol', 'Estado'].map(c => h('th', { text: c })))), h('tbody', {}, rows)))
      : h('p', { class: 'catalog-empty', text: 'Sin usuarios.' }),
    h('section', { class: 'users-edit' },
      h('label', { class: 'label' }, 'Nombre del usuario seleccionado',
        h('input', { maxlength: '100', value: u.edit?.name ?? '', disabled: !selected || admin, 'aria-label': 'Nombre del usuario seleccionado', oninput: e => { u.edit.name = e.target.value; } })),
      h('label', { class: 'label' }, 'Rol', h('select', { disabled: !selected || admin, onchange: e => { u.edit.role = e.target.value; } },
        (admin ? [['Administrator', 'Administrador']] : ROLES).map(([v, l]) => h('option', { value: v, text: l, selected: u.edit?.role === v })))),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'primary', disabled: !selected || admin, onclick: async () => {
          if (!u.edit.name.trim()) { say('Escribe el nombre para mostrar.'); return redraw(); }
          try { await command(selected.branchId, 'user.update', { id: selected.id, displayName: u.edit.name.trim(), role: u.edit.role }); say('Cambios enviados a la caja.', true); }
          catch (error) { say(error.message); }
          redraw();
        } }, 'Guardar cambios'),
        h('button', { type: 'button', class: 'secondary', disabled: !selected || admin, onclick: async () => {
          try { await command(selected.branchId, 'user.active', { id: selected.id, active: !selected.active });
            say(selected.active ? 'Desactivación enviada: al aplicarse ya no podrá iniciar sesión.' : 'Reactivación enviada a la caja.', true); }
          catch (error) { say(error.message); }
          redraw();
        } }, selected && !selected.active ? 'Reactivar usuario' : 'Desactivar usuario'),
        h('button', { type: 'button', class: 'secondary', disabled: !selected || admin || !selected.active, onclick: () => dialog('Restablecer contraseña', body => body.append(
          h('p', { class: 'wide', text: `Se asignará una contraseña temporal a ${selected.username} y deberá cambiarla al entrar. ¿Continuar?` })), {
          submit: 'Sí', cancel: 'No', onSubmit: async () => {
            const result = await command(selected.branchId, 'user.reset_password', { id: selected.id });
            u.notice = notice(selected.username, result.temporaryPassword); say('Contraseña restablecida (funciona en cuanto la caja reciba el cambio).', true); redraw();
          } }) }, 'Restablecer contraseña')),
      h('p', { class: 'caption', text: 'El administrador no se edita aquí. Si olvida su contraseña, se recupera en el equipo de la caja con: CasaMiga.Maintenance reset-admin <carpeta de datos>' })),
    u.notice ? h('div', { class: 'users-notice', role: 'status' }, h('p', { text: u.notice }),
      h('button', { type: 'button', class: 'ghost', onclick: () => { u.notice = ''; redraw(); } }, 'Ya la entregué')) : null,
    h('section', { class: 'users-new' },
      h('h3', { class: 'inv-section', text: 'Nuevo usuario' }),
      state.me.admin && state.branches.length > 1 ? h('label', { class: 'label' }, 'Sucursal', h('select', { onchange: e => { f.branchId = e.target.value; } },
        state.branches.map(b => h('option', { value: b.id, text: b.name, selected: b.id === f.branchId })))) : null,
      h('div', { class: 'catalog-two' },
        h('label', { class: 'label' }, 'Usuario', h('input', { maxlength: '80', value: f.username, autocapitalize: 'none', spellcheck: 'false', 'aria-label': 'Usuario nuevo', oninput: e => { f.username = e.target.value; } })),
        h('label', { class: 'label' }, 'Nombre para mostrar', h('input', { maxlength: '100', value: f.name, 'aria-label': 'Nombre para mostrar', oninput: e => { f.name = e.target.value; } }))),
      h('label', { class: 'label' }, 'Rol', h('select', { onchange: e => { f.role = e.target.value; } }, ROLES.map(([v, l]) => h('option', { value: v, text: l, selected: f.role === v })))),
      h('p', { class: 'caption', text: 'Se genera una contraseña temporal; el usuario debe cambiarla en su primer acceso.' }),
      h('div', { class: 'toolbar' }, h('button', { type: 'button', class: 'primary', onclick: async () => {
        if (!/^[A-Za-z0-9._-]{1,80}$/.test(f.username.trim())) { say('Usuario: sólo letras, números, punto, guion y guion bajo.'); return redraw(); }
        if (!f.name.trim()) { say('Escribe el nombre para mostrar.'); return redraw(); }
        try {
          const result = await command(f.branchId, 'user.create', { username: f.username.trim(), displayName: f.name.trim(), role: f.role });
          u.notice = notice(f.username.trim(), result.temporaryPassword);
          u.form = { username: '', name: '', role: 'Cashier', branchId: f.branchId }; say('Usuario enviado a la caja.', true);
        } catch (error) { say(error.message); }
        redraw();
      } }, 'Crear usuario'))),
    u.message ? h('p', { class: u.ok ? 'success' : 'error', role: 'status', text: u.message, style: { marginTop: '12px' } }) : null);
}
