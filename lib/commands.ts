// Cambios hechos en la versión web (docs/WEB.md). La nube sólo los valida y los guarda en orden;
// cada caja de la sucursal los aplica con sus propias reglas y reporta si quedaron o por qué no.
import { randomUUID } from 'node:crypto';
import { ProtocolError } from './protocol.js';
import { identityHash, requirePermission, temporaryPassword, type WebUser } from './auth.js';
import { isUuid } from './web.js';
import type { Query, Row } from './panel.js';

export const KINDS = ['category.save', 'product.save', 'product.active', 'user.create', 'user.update', 'user.active', 'user.reset_password',
  'consignment.store.save', 'consignment.template.save',
  // Ruta: lo que el repartidor registra en la tienda desde el celular.
  'consignment.deliver', 'consignment.collect', 'consignment.payment', 'consignment.void'] as const;
const METHODS = ['CASH', 'CARD', 'TRANSFER'];
export type Kind = (typeof KINDS)[number];

const s = (v: unknown) => (typeof v === 'string' ? v : '');
function text(v: unknown, field: string, min: number, max: number): string {
  const value = s(v).trim();
  if (value.length < min || value.length > max || /[\u0000-\u001f\u007f]/.test(value)) throw new ProtocolError(`${field}: de ${min} a ${max} caracteres, sin saltos de línea`);
  return value;
}
function id(v: unknown, field: string): string {
  if (!isUuid(v)) throw new ProtocolError(`${field} inválido`);
  return v.toLowerCase();
}
function int(v: unknown, field: string, min: number, max: number): number {
  if (typeof v !== 'number' || !Number.isSafeInteger(v) || v < min || v > max) throw new ProtocolError(`${field} fuera de rango`);
  return v;
}
function bool(v: unknown, field: string): boolean {
  if (typeof v !== 'boolean') throw new ProtocolError(`${field} inválido`);
  return v;
}

async function state(q: Query, branch: string, entity: string, entityId: string): Promise<Row | null> {
  const [row] = await q('SELECT payload FROM entity_state WHERE branch_id = $1 AND entity = $2 AND entity_id = $3 AND NOT deleted', [branch, entity, entityId]);
  return (row?.payload as Row | undefined) ?? null;
}

export interface CreatedCommand { id: string; kind: Kind; temporaryPassword?: string }

/** Valida y encola un cambio. El rol del usuario web se revisa aquí; la caja vuelve a revisar sus propias reglas al aplicarlo. */
export async function createCommand(q: Query, user: WebUser, branchId: unknown, kind: unknown, body: unknown): Promise<CreatedCommand> {
  if (!(KINDS as readonly string[]).includes(s(kind))) throw new ProtocolError('Cambio desconocido');
  const branch = id(branchId, 'Sucursal');
  if (!user.admin && branch !== user.branchId) throw new ProtocolError('Sin acceso a esa sucursal', 403);
  if ((await q('SELECT 1 FROM branch WHERE id = $1', [branch])).length === 0) throw new ProtocolError('Sucursal inexistente', 404);
  const b = (body && typeof body === 'object' ? body : {}) as Record<string, unknown>;
  let payload: Record<string, unknown>;
  let temporary: string | undefined;

  switch (kind as Kind) {
    case 'category.save': {
      const target = id(b.id, 'Categoría');
      const existing = await state(q, branch, 'category', target);
      requirePermission(user, existing ? 'categories.edit' : 'categories.create');
      payload = { id: target, name: text(b.name, 'Nombre', 1, 80), description: text(b.description ?? '', 'Descripción', 0, 500), sortOrder: int(b.sortOrder ?? 0, 'Orden', 0, 9999) };
      if (b.active !== undefined) { requirePermission(user, 'categories.deactivate'); payload.active = bool(b.active, 'Activa'); }
      break;
    }
    case 'product.save': {
      const target = id(b.id, 'Producto');
      requirePermission(user, (await state(q, branch, 'product', target)) ? 'products.edit' : 'products.create');
      const category = id(b.categoryId, 'Categoría');
      if (!(await state(q, branch, 'category', category))) throw new ProtocolError('La categoría no existe en esa sucursal');
      const sku = b.sku === null || s(b.sku).trim() === '' ? null : text(b.sku, 'SKU', 1, 40);
      payload = { id: target, categoryId: category, sku, name: text(b.name, 'Nombre', 1, 160), description: text(b.description ?? '', 'Descripción', 0, 500),
        priceCents: int(b.priceCents, 'Precio', 0, 10_000_000), trackInventory: b.trackInventory === undefined ? true : bool(b.trackInventory, 'Inventario') };
      break;
    }
    case 'product.active': {
      requirePermission(user, 'products.deactivate');
      const target = id(b.id, 'Producto');
      if (!(await state(q, branch, 'product', target))) throw new ProtocolError('Producto inexistente', 404);
      payload = { id: target, active: bool(b.active, 'Activo') };
      break;
    }
    case 'user.create': {
      requirePermission(user, 'users.manage');
      const username = text(b.username, 'Usuario', 1, 80);
      if (!/^[A-Za-z0-9._-]+$/.test(username)) throw new ProtocolError('Usuario: sólo letras, números, punto, guion y guion bajo');
      const taken = await q(`SELECT 1 FROM entity_state WHERE branch_id = $1 AND entity = 'app_user' AND upper(payload->>'username') = upper($2)`, [branch, username]);
      if (taken.length > 0) throw new ProtocolError('Ese usuario ya existe en la sucursal');
      temporary = temporaryPassword();
      payload = { id: randomUUID(), username, displayName: text(b.displayName, 'Nombre', 1, 100), role: role(b.role), passwordHash: await identityHash(temporary) };
      break;
    }
    case 'user.update': case 'user.active': case 'user.reset_password': {
      requirePermission(user, 'users.manage');
      const target = id(b.id, 'Usuario');
      const existing = await state(q, branch, 'app_user', target);
      if (!existing) throw new ProtocolError('Usuario inexistente', 404);
      if (existing.role_name === 'Administrator') throw new ProtocolError('El administrador no se edita desde aquí');
      if (kind === 'user.update') payload = { id: target, displayName: text(b.displayName, 'Nombre', 1, 100), role: role(b.role) };
      else if (kind === 'user.active') payload = { id: target, active: bool(b.active, 'Activo') };
      else { temporary = temporaryPassword(); payload = { id: target, passwordHash: await identityHash(temporary) }; }
      break;
    }
    case 'consignment.store.save': {
      requirePermission(user, 'consignment.manage');
      payload = { id: id(b.id, 'Tienda'), name: text(b.name, 'Nombre', 1, 100), contact: text(b.contact ?? '', 'Contacto', 0, 100),
        phone: text(b.phone ?? '', 'Teléfono', 0, 40), address: text(b.address ?? '', 'Dirección', 0, 200) };
      if (b.active !== undefined) payload.active = bool(b.active, 'Activa');
      break;
    }
    case 'consignment.deliver': case 'consignment.collect': case 'consignment.payment': case 'consignment.void':
      requirePermission(user, 'consignment.manage');
      payload = {};
      break;
    case 'consignment.template.save': {
      requirePermission(user, 'consignment.manage');
      const store = id(b.storeId, 'Tienda');
      if (!(await state(q, branch, 'consignment_store', store))) throw new ProtocolError('Tienda inexistente', 404);
      if (!Array.isArray(b.lines) || b.lines.length > 80) throw new ProtocolError('Charola habitual inválida');
      const seen = new Set<string>();
      const lines = b.lines.map((l: unknown) => {
        const line = (l ?? {}) as Record<string, unknown>;
        const product = id(line.productId, 'Producto');
        if (seen.has(product)) throw new ProtocolError('Producto repetido en la charola');
        seen.add(product);
        return { productId: product, quantity: int(line.quantity, 'Cantidad', 1, 999) };
      });
      payload = { storeId: store, lines };
      break;
    }
  }
  if ((kind as string).startsWith('consignment.') && ['consignment.deliver', 'consignment.collect', 'consignment.payment', 'consignment.void'].includes(kind as string))
    payload = await route(q, branch, kind as Kind, b);
  const commandId = randomUUID();
  await q(`INSERT INTO branch_command(id, branch_id, kind, payload, requested_by_id, requested_by_name) VALUES ($1, $2, $3, $4::jsonb, $5, $6)`,
    [commandId, branch, kind, JSON.stringify(payload!), user.id, user.name]);
  return { id: commandId, kind: kind as Kind, ...(temporary ? { temporaryPassword: temporary } : {}) };
}

/** Entregas, recolecciones, cobros y anulaciones de la ruta: se validan contra lo último que subió la caja y contra lo que ya se mandó. */
async function route(q: Query, branch: string, kind: Kind, b: Record<string, unknown>): Promise<Record<string, unknown>> {
  const note = text(b.note ?? '', 'Nota', 0, 200);
  const method = () => { if (!METHODS.includes(s(b.method))) throw new ProtocolError('Forma de pago inválida'); return s(b.method); };
  const reference = () => (s(b.method) === 'CASH' || !s(b.reference).trim() ? null : text(b.reference, 'Referencia', 1, 60));
  const pending = async (deliveryId: string) => {
    const delivery = await state(q, branch, 'consignment_delivery', deliveryId) as (Row & { items: Row[]; settlement: Row | null; void: Row | null }) | null;
    if (!delivery) throw new ProtocolError('Esa charola todavía no llega a la nube', 404);
    if (delivery.settlement || delivery.void) throw new ProtocolError(delivery.void ? 'Esa charola se anuló.' : 'Esa charola ya se recogió.');
    const sent = await q(`SELECT 1 FROM branch_command c WHERE c.branch_id = $1 AND c.kind IN ('consignment.collect','consignment.void') AND c.payload->>'deliveryId' = $2
      AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id AND r.status = 'rejected')`, [branch, deliveryId]);
    if (sent.length > 0) throw new ProtocolError('Esa charola ya se recogió o anuló desde la web; espera a que la caja lo aplique.');
    return delivery;
  };
  switch (kind) {
    case 'consignment.deliver': {
      const store = id(b.storeId, 'Tienda');
      const target = await state(q, branch, 'consignment_store', store) as (Row & { store: Row }) | null;
      if (!target || Number(target.store.active) !== 1) throw new ProtocolError('La tienda no existe o está desactivada');
      if (!Array.isArray(b.lines) || b.lines.length === 0 || b.lines.length > 80) throw new ProtocolError('Agrega al menos una pieza a la charola');
      const seen = new Set<string>();
      const lines = [];
      for (const l of b.lines as unknown[]) {
        const line = (l ?? {}) as Record<string, unknown>;
        const product = id(line.productId, 'Producto');
        if (seen.has(product)) throw new ProtocolError('Producto repetido en la charola');
        seen.add(product);
        const quantity = int(line.quantity, 'Cantidad', 1, 999);
        const [ok] = await q(`SELECT 1 FROM entity_state p JOIN entity_state c ON c.branch_id = p.branch_id AND c.entity = 'consignment_price' AND c.entity_id = p.entity_id AND NOT c.deleted
          WHERE p.branch_id = $1 AND p.entity = 'product' AND p.entity_id = $2 AND (p.payload->'product'->>'active')::int = 1`, [branch, product]);
        if (!ok) throw new ProtocolError('Un pan no está activo o no tiene precio de charola');
        lines.push({ productId: product, quantity });
      }
      return { storeId: store, lines, note };
    }
    case 'consignment.collect': {
      const deliveryId = id(b.deliveryId, 'Charola');
      const delivery = await pending(deliveryId);
      const items = new Map(delivery.items.map(i => [Number(i.position), Number(i.quantity)]));
      const returned = (Array.isArray(b.returned) ? b.returned : []).map((r: unknown) => {
        const x = (r ?? {}) as Record<string, unknown>;
        const position = int(x.position, 'Partida', 1, 999);
        if (!items.has(position)) throw new ProtocolError('La partida no pertenece a la charola');
        return { position, quantity: int(x.quantity, 'Piezas que regresan', 0, items.get(position)!) };
      });
      if (new Set(returned.map(r => r.position)).size !== returned.length) throw new ProtocolError('Partida repetida');
      const paidCents = int(b.paidCents ?? 0, 'Cobro', 0, 10_000_000);
      return { deliveryId, returned, paidCents, method: paidCents > 0 ? method() : 'CASH', reference: paidCents > 0 ? reference() : null, note };
    }
    case 'consignment.payment': {
      const store = id(b.storeId, 'Tienda');
      if (!(await state(q, branch, 'consignment_store', store))) throw new ProtocolError('Tienda inexistente', 404);
      return { storeId: store, amountCents: int(b.amountCents, 'Importe', 1, 10_000_000), method: method(), reference: reference(), note };
    }
    case 'consignment.void': {
      const deliveryId = id(b.deliveryId, 'Charola');
      await pending(deliveryId);
      return { deliveryId, reason: text(b.reason, 'Motivo', 3, 200) };
    }
  }
  throw new ProtocolError('Cambio desconocido');
}

function role(v: unknown): string {
  if (v !== 'Manager' && v !== 'Cashier') throw new ProtocolError('Rol inválido: encargado o cajero');
  return v;
}

/** Últimos cambios con su estado en cada caja de la sucursal. */
export async function listCommands(q: Query, branch: string | null) {
  const rows = await q(`SELECT c.id, c.seq, c.branch_id, c.kind, c.payload, c.requested_by_name, c.requested_at,
      (SELECT count(*) FROM terminal t WHERE t.branch_id = c.branch_id AND t.revoked_at IS NULL) AS terminals,
      (SELECT count(*) FROM command_result r WHERE r.command_id = c.id AND r.status = 'applied') AS applied,
      (SELECT string_agg(r.message, ' · ') FROM command_result r WHERE r.command_id = c.id AND r.status = 'rejected') AS rejected
    FROM branch_command c WHERE ($1::uuid IS NULL OR c.branch_id = $1::uuid) ORDER BY c.seq DESC LIMIT 100`, [branch]);
  return rows.map(r => {
    const payload = { ...(r.payload as Row) }; delete payload.passwordHash;
    const terminals = Number(r.terminals), applied = Number(r.applied);
    return { id: String(r.id), branchId: String(r.branch_id), kind: String(r.kind), payload, by: String(r.requested_by_name),
      at: r.requested_at instanceof Date ? r.requested_at.toISOString() : String(r.requested_at),
      status: r.rejected ? 'rejected' : terminals > 0 && applied >= terminals ? 'applied' : 'pending', message: r.rejected ? String(r.rejected) : null, applied, terminals };
  });
}

// ---------- Lado de la caja ----------

export async function pendingFor(q: Query, terminal: { id: string; branchId: string }, after: number) {
  const rows = await q(`SELECT id, seq, kind, payload, requested_by_id, requested_by_name, requested_at FROM branch_command
    WHERE branch_id = $1 AND seq > $2 ORDER BY seq LIMIT 50`, [terminal.branchId, after]);
  return rows.map(r => ({ id: String(r.id), seq: Number(r.seq), kind: String(r.kind), payload: r.payload, requestedBy: { id: String(r.requested_by_id), name: String(r.requested_by_name) },
    requestedAt: r.requested_at instanceof Date ? r.requested_at.toISOString() : String(r.requested_at) }));
}

export async function recordResults(q: Query, terminal: { id: string; branchId: string }, body: unknown) {
  const results = (body as { results?: unknown } | null)?.results;
  if (!Array.isArray(results) || results.length === 0 || results.length > 50) throw new ProtocolError('Resultados inválidos');
  const clean = results.map((r: unknown) => {
    const x = (r ?? {}) as Record<string, unknown>;
    if (x.status !== 'applied' && x.status !== 'rejected') throw new ProtocolError('Estado inválido');
    return { id: id(x.id, 'Cambio'), status: x.status, message: x.message === undefined || x.message === null ? null : s(x.message).slice(0, 300) };
  });
  await q(`INSERT INTO command_result(command_id, terminal_id, status, message)
    SELECT r.id, $1, r.status, r.message FROM jsonb_to_recordset($2::jsonb) AS r(id uuid, status text, message text)
    WHERE EXISTS (SELECT 1 FROM branch_command c WHERE c.id = r.id AND c.branch_id = $3)
    ON CONFLICT (command_id, terminal_id) DO NOTHING`, [terminal.id, JSON.stringify(clean), terminal.branchId]);
  return { recorded: clean.length };
}
