// I1 (docs/PLAN_COMPLETAR.md): lo que se cambia en la web en catálogo, usuarios y tiendas se ve al momento en la web, aunque la caja esté
// apagada. Al encolar el cambio se escribe en entity_state lo que la caja dejará (los mismos campos que ella sube) y se marca con
// pending_command. Cuando la caja lo aplica, sube su versión (que gana y limpia la marca); si lo rechaza, el registro vuelve a lo último
// que subió la caja y se vuelven a escribir los demás cambios que siguen pendientes, en orden.
import type { Query, Row } from './panel.js';

type Payload = Record<string, any>;
interface Effect { entity: string; entityId: string; project(current: Payload | null, at: string): Payload | null }

const flag = (value: unknown) => (value ? 1 : 0);

/** Lo que cada cambio deja en la caja (CloudCommandService), en el formato que ella sube. Null: no se adelanta. */
export function effect(kind: string, p: Payload): Effect | null {
  switch (kind) {
    case 'category.save':
      return { entity: 'category', entityId: p.id, project: (c, at) => ({
        ...(c ?? { id: p.id, active: 1, created_utc: at }), name: p.name, description: p.description, sort_order: p.sortOrder,
        active: p.active === undefined ? (c?.active ?? 1) : flag(p.active), modified_utc: at }) };
    case 'product.save':
      return { entity: 'product', entityId: p.id, project: (c, at) => ({
        inventoryLinks: [], ...(c ?? {}),
        product: { ...(c?.product ?? { id: p.id, active: 1, created_utc: at }), sku: p.sku, name: p.name, category_id: p.categoryId, description: p.description,
          price_cents: p.priceCents, track_inventory: flag(p.trackInventory), modified_utc: at } }) };
    case 'product.active':
      return { entity: 'product', entityId: p.id, project: (c, at) => c && ({ ...c, product: { ...c.product, active: flag(p.active), modified_utc: at } }) };
    case 'user.create':
      return { entity: 'app_user', entityId: p.id, project: (c, at) => c ?? ({ id: p.id, username: String(p.username).trim().toUpperCase(), display_name: p.displayName,
        password_hash: p.passwordHash, role_name: p.role, active: 1, must_change_password: 1, created_utc: at }) };
    case 'user.update':
      return { entity: 'app_user', entityId: p.id, project: c => c && ({ ...c, display_name: p.displayName, role_name: p.role }) };
    case 'user.active':
      return { entity: 'app_user', entityId: p.id, project: c => c && ({ ...c, active: flag(p.active) }) };
    case 'user.reset_password':
      return { entity: 'app_user', entityId: p.id, project: c => c && ({ ...c, password_hash: p.passwordHash, must_change_password: 1 }) };
    case 'consignment.store.save':
      return { entity: 'consignment_store', entityId: p.id, project: (c, at) => ({
        template: [], ...(c ?? {}),
        store: { ...(c?.store ?? { id: p.id, active: 1, created_utc: at }), name: p.name, contact: p.contact, phone: p.phone, address: p.address,
          ...(p.active === undefined ? {} : { active: flag(p.active) }), modified_utc: at } }) };
    case 'consignment.template.save':
      return { entity: 'consignment_store', entityId: p.storeId, project: c => c && ({ ...c,
        template: (p.lines as { productId: string; quantity: number }[]).map(l => ({ store_id: p.storeId, product_id: l.productId, quantity: l.quantity })) }) };
    default:
      return null;
  }
}

/** Escribe en la web lo que dejará el cambio. Un registro nuevo se cuelga de una caja de escritorio de la sucursal con la marca más vieja posible. */
export async function project(q: Query, branch: string, commandId: string, kind: string, payload: Payload): Promise<void> {
  const e = effect(kind, payload);
  if (!e) return;
  const [row] = await q('SELECT payload, deleted FROM entity_state WHERE branch_id = $1 AND entity = $2 AND entity_id = $3', [branch, e.entity, e.entityId]);
  const current = row && !row.deleted ? row.payload as Payload : null;
  const next = e.project(current, new Date().toISOString());
  if (!next) return;
  if (row) {
    await q('UPDATE entity_state SET payload = $4::jsonb, deleted = false, pending_command = $5 WHERE branch_id = $1 AND entity = $2 AND entity_id = $3',
      [branch, e.entity, e.entityId, JSON.stringify(next), commandId]);
    return;
  }
  // Sin caja de escritorio, nadie aplicaría el cambio: no se adelanta.
  await q(`INSERT INTO entity_state(branch_id, entity, entity_id, terminal_id, seq, enqueued_at, deleted, payload, pending_command)
    SELECT $1, $2, $3, t.id, 0, '-infinity', false, $4::jsonb, $5 FROM terminal t WHERE t.branch_id = $1 AND t.kind = 'desktop' AND t.revoked_at IS NULL
    ORDER BY t.registered_at, t.id LIMIT 1 ON CONFLICT DO NOTHING`, [branch, e.entity, e.entityId, JSON.stringify(next), commandId]);
}

const WATCHED = new Set(['category', 'product', 'app_user', 'consignment_store']);

/** Cambios de la sucursal que ninguna caja ha respondido, en su orden. */
async function waiting(q: Query, branch: string) {
  return q(`SELECT c.id, c.kind, c.payload FROM branch_command c WHERE c.branch_id = $1
    AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id) ORDER BY c.seq`, [branch]);
}

/** Después de que la caja sube su versión de un registro, los cambios de la web que siguen sin respuesta se vuelven a adelantar encima. */
export async function reproject(q: Query, branch: string, touched: { entity: string; entityId: string }[]): Promise<void> {
  const relevant = touched.filter(t => WATCHED.has(t.entity));
  if (relevant.length === 0) return;
  for (const c of await waiting(q, branch)) {
    const e = effect(String(c.kind), c.payload as Payload);
    if (e && relevant.some(t => t.entity === e.entity && t.entityId === e.entityId)) await project(q, branch, String(c.id), String(c.kind), c.payload as Payload);
  }
}

/**
 * Tras el resultado de la caja. Aplicado (por cualquier caja): el contenido ya es el de la caja; sólo sigue marcado si otro cambio del mismo
 * registro espera respuesta. Rechazado por todas las que respondieron: si el registro sigue adelantado, vuelve a lo último que subió la caja
 * (o desaparece si era nuevo) y se adelantan otra vez los cambios que siguen sin respuesta.
 */
export async function settle(q: Query, commandIds: string[]): Promise<void> {
  if (commandIds.length === 0) return;
  const done = await q(`SELECT c.id, c.branch_id, c.kind, c.payload, EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id AND r.status = 'applied') AS applied
    FROM branch_command c WHERE c.id = ANY($1::uuid[]) AND EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id) ORDER BY c.seq`, [commandIds]);
  for (const c of done) {
    const branch = String(c.branch_id);
    const e = effect(String(c.kind), c.payload as Payload);
    if (!e) continue;
    const [row] = await q('SELECT pending_command FROM entity_state WHERE branch_id = $1 AND entity = $2 AND entity_id = $3', [branch, e.entity, e.entityId]);
    if (!row || row.pending_command === null) continue;
    if (c.applied === true) {
      const later = (await waiting(q, branch)).filter(w => { const x = effect(String(w.kind), w.payload as Payload); return x && x.entity === e.entity && x.entityId === e.entityId; });
      await q('UPDATE entity_state SET pending_command = $4 WHERE branch_id = $1 AND entity = $2 AND entity_id = $3',
        [branch, e.entity, e.entityId, later.length ? String(later[later.length - 1].id) : null]);
    } else await restore(q, branch, e.entity, e.entityId);
  }
}

async function restore(q: Query, branch: string, entity: string, entityId: string) {
  const [real] = await q(`SELECT terminal_id, seq, enqueued_at, change, payload FROM sync_event WHERE branch_id = $1 AND entity = $2 AND entity_id = $3
    ORDER BY enqueued_at DESC, seq DESC LIMIT 1`, [branch, entity, entityId]);
  if (real) await q(`UPDATE entity_state SET terminal_id = $4, seq = $5, enqueued_at = $6, deleted = $7, payload = $8::jsonb, pending_command = NULL, received_at = now()
    WHERE branch_id = $1 AND entity = $2 AND entity_id = $3`, [branch, entity, entityId, real.terminal_id, real.seq, real.enqueued_at, real.change === 'delete', JSON.stringify(real.payload)]);
  else await q('DELETE FROM entity_state WHERE branch_id = $1 AND entity = $2 AND entity_id = $3', [branch, entity, entityId]);
  await reproject(q, branch, [{ entity, entityId }]);
}

/** Para las pantallas: true si el registro tiene un cambio de la web que la caja todavía no confirma. */
export const pendingOf = (row: Row) => row.pending_command !== null && row.pending_command !== undefined;
