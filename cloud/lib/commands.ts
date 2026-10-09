// Cambios hechos en la versión web (docs/WEB.md). La nube sólo los valida y los guarda en orden;
// cada caja de la sucursal los aplica con sus propias reglas y reporta si quedaron o por qué no.
import { randomUUID } from 'node:crypto';
import { ProtocolError } from './protocol.js';
import { identityHash, requirePermission, temporaryPassword, type WebUser } from './auth.js';
import { isUuid, MAIN_LOCATION } from './web.js';
import type { Query, Row } from './panel.js';

export const KINDS = ['category.save', 'product.save', 'product.active', 'user.create', 'user.update', 'user.active', 'user.reset_password',
  'consignment.store.save', 'consignment.template.save',
  // Ruta: lo que el repartidor registra en la tienda desde el celular.
  'consignment.deliver', 'consignment.collect', 'consignment.payment', 'consignment.void',
  // Costo de referencia de un insumo, para calcular la ganancia desde las recetas.
  'inventory.cost',
  // Fase 3 del POS web (docs/POS_WEB.md): entradas, mermas y conteos de inventario, producción hecha y foto del producto.
  'inventory.receive', 'inventory.waste', 'inventory.count', 'production.record', 'product.photo'] as const;
const METHODS = ['CASH', 'CARD', 'TRANSFER'];
/** Unidades del sistema (InventoryUnits de la caja): dimensión y cuántas unidades mínimas tiene cada una. */
const UNITS: Record<string, [string, number]> = { mg: ['MASS', 1], g: ['MASS', 1_000], kg: ['MASS', 1_000_000], uL: ['VOLUME', 1], ml: ['VOLUME', 1_000], L: ['VOLUME', 1_000_000], piece: ['COUNT', 1] };
const WASTE_REASONS = ['Damaged', 'Expired', 'ProductionError', 'Spill', 'Other'];
const COUNT_REASONS = ['PhysicalCountVariance', 'DataCorrection', 'Other'];
/** Foto ya recortada en cuadro por el navegador; base64 de hasta ~1 MB para no cargar la fila de cambios. */
const MAX_PHOTO_BASE64 = 1_400_000;
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
    case 'inventory.cost': {
      requirePermission(user, 'inventory.configure');
      const item = id(b.itemId, 'Insumo');
      if (!(await state(q, branch, 'inventory_item', item))) throw new ProtocolError('Insumo inexistente', 404);
      payload = { itemId: item, costCents: int(b.costCents, 'Costo', 0, 100_000_000), perAtomic: int(b.perAtomic, 'Cantidad', 1, 1_000_000_000_000), unitLabel: text(b.unitLabel, 'Unidad', 1, 40) };
      break;
    }
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
    case 'inventory.receive': {
      requirePermission(user, 'inventory.receive');
      payload = { ...(await stock(q, branch, b, false)), ...optional(b.reference, 'Referencia', 200, 'reference'), ...optional(b.note, 'Observación', 500, 'note') };
      break;
    }
    case 'inventory.waste': {
      requirePermission(user, 'inventory.waste');
      payload = { ...(await stock(q, branch, b, false)), ...reason(b, WASTE_REASONS, 'Motivo de merma') };
      break;
    }
    case 'inventory.count': {
      requirePermission(user, 'inventory.adjust');
      payload = { ...(await stock(q, branch, b, true)), ...reason({ ...b, reason: b.reason ?? 'PhysicalCountVariance' }, COUNT_REASONS, 'Motivo del ajuste') };
      break;
    }
    case 'production.record': {
      requirePermission(user, 'production.create'); requirePermission(user, 'production.confirm');
      if (!Array.isArray(b.outputs) || b.outputs.length === 0 || b.outputs.length > 20) throw new ProtocolError('Agrega de 1 a 20 productos');
      const profiles = await q(`SELECT payload->'profile' AS p FROM entity_state WHERE branch_id = $1 AND entity = 'production_profile' AND NOT deleted`, [branch]);
      const published = new Set(profiles.map(r => s((r.p as Row).active_version_id)).filter(Boolean));
      const seen = new Set<string>();
      const outputs = (b.outputs as unknown[]).map(o => {
        const x = (o ?? {}) as Record<string, unknown>;
        const version = id(x.profileVersionId, 'Receta');
        if (!published.has(version)) throw new ProtocolError('La receta de un producto cambió; actualiza la página');
        if (seen.has(version)) throw new ProtocolError('Producto repetido');
        seen.add(version);
        return { profileVersionId: version, planned: int(x.planned, 'Piezas planeadas', 1, 100_000), actual: int(x.actual, 'Piezas que salieron', 0, 100_000) };
      });
      payload = { outputs, ...optional(b.notes, 'Notas', 500, 'notes') };
      break;
    }
    case 'product.photo': {
      requirePermission(user, 'products.edit');
      const product = id(b.productId, 'Producto');
      // Un producto recién creado en la web todavía no llega de la caja: basta con que su alta vaya en la fila (se aplica antes).
      const known = (await state(q, branch, 'product', product)) !== null || (await q(`SELECT 1 FROM branch_command c WHERE c.branch_id = $1 AND c.kind = 'product.save' AND c.payload->>'id' = $2
        AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id AND r.status = 'rejected')`, [branch, product])).length > 0;
      if (!known) throw new ProtocolError('Producto inexistente', 404);
      const image = s(b.image);
      if (image.length === 0 || image.length > MAX_PHOTO_BASE64 || !/^[A-Za-z0-9+/]+={0,2}$/.test(image)) throw new ProtocolError('Foto inválida o muy grande (máximo 1 MB)');
      const size = jpegSize(Buffer.from(image, 'base64'));
      if (!size) throw new ProtocolError('La foto debe ser JPEG');
      if (size.width !== size.height || size.width < 256 || size.width > 2048) throw new ProtocolError('La foto debe ser cuadrada, de 256 a 2048 píxeles');
      payload = { productId: product, image, edge: size.width, name: 'foto-web.jpg' };
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

/**
 * Insumo, ubicación y cantidad de una operación de inventario. La cantidad viaja como texto decimal («2.5») y debe dar un número
 * entero de unidades mínimas (2.5 kg sí; 0.0005 g no); las presentaciones del insumo (bulto, caja) sólo en enteros.
 */
async function stock(q: Query, branch: string, b: Record<string, unknown>, zero: boolean) {
  const itemId = id(b.itemId, 'Insumo');
  const found = await state(q, branch, 'inventory_item', itemId) as (Row & { item: Row; units?: Row[] }) | null;
  if (!found) throw new ProtocolError('Insumo inexistente', 404);
  if (Number(found.item.active) !== 1) throw new ProtocolError('El insumo está inactivo');
  const locationId = b.locationId === undefined ? MAIN_LOCATION : id(b.locationId, 'Ubicación');
  if (locationId !== MAIN_LOCATION && !(await state(q, branch, 'inventory_location', locationId))) throw new ProtocolError('Ubicación inexistente', 404);
  const quantity = s(b.quantity).trim();
  if (!/^\d{1,6}(\.\d{1,3})?$/.test(quantity) || (!zero && /^0+(\.0+)?$/.test(quantity)))
    throw new ProtocolError(zero ? 'Cantidad: de 0 a 999999, hasta 3 decimales' : 'Cantidad: mayor que cero, hasta 3 decimales');
  const unitCode = s(b.unitCode);
  const [whole, fraction = ''] = quantity.split('.');
  const thousandths = BigInt(whole + fraction.padEnd(3, '0'));
  const standard = UNITS[unitCode];
  if (standard) {
    if (standard[0] !== s(found.item.dimension)) throw new ProtocolError('Esa unidad no corresponde al insumo');
    if ((thousandths * BigInt(standard[1])) % 1000n !== 0n) throw new ProtocolError('Demasiados decimales para esa unidad');
  } else {
    if (!(found.units ?? []).some(u => s(u.code) === unitCode && Number(u.active) === 1)) throw new ProtocolError('Unidad inválida');
    if (thousandths % 1000n !== 0n) throw new ProtocolError('Las presentaciones se capturan en enteros');
  }
  return { itemId, locationId, quantity, unitCode };
}
function optional(v: unknown, field: string, max: number, key: string): Record<string, string> {
  const value = text(v ?? '', field, 0, max);
  return value ? { [key]: value } : {};
}
/** Motivo de merma o ajuste (los nombres de la caja); «Otro» exige observación. */
function reason(b: Record<string, unknown>, allowed: string[], field: string): Record<string, string> {
  if (!allowed.includes(s(b.reason))) throw new ProtocolError(`${field} inválido`);
  const note = optional(b.note, 'Observación', 500, 'note');
  if (b.reason === 'Other' && !note.note) throw new ProtocolError('Con motivo «Otro» escribe una observación');
  return { reason: s(b.reason), ...note };
}
/** Ancho y alto de un JPEG completo (marcador SOF), o null si no lo es. */
export function jpegSize(bytes: Buffer): { width: number; height: number } | null {
  if (bytes.length < 4 || bytes[0] !== 0xff || bytes[1] !== 0xd8 || bytes[bytes.length - 2] !== 0xff || bytes[bytes.length - 1] !== 0xd9) return null;
  let i = 2;
  while (i + 9 < bytes.length) {
    if (bytes[i] !== 0xff) return null;
    const marker = bytes[i + 1];
    if (marker === 0xff) { i++; continue; }
    const length = bytes.readUInt16BE(i + 2);
    if (marker >= 0xc0 && marker <= 0xcf && marker !== 0xc4 && marker !== 0xc8 && marker !== 0xcc)
      return { height: bytes.readUInt16BE(i + 5), width: bytes.readUInt16BE(i + 7) };
    if (marker === 0xda || length < 2) return null;
    i += 2 + length;
  }
  return null;
}

/** Últimos cambios con su estado en cada caja de la sucursal. */
export async function listCommands(q: Query, branch: string | null) {
  const rows = await q(`SELECT c.id, c.seq, c.branch_id, c.kind, c.payload, c.requested_by_name, c.requested_at,
      (SELECT count(*) FROM terminal t WHERE t.branch_id = c.branch_id AND t.revoked_at IS NULL AND t.kind = 'desktop') AS terminals,
      (SELECT count(*) FROM command_result r WHERE r.command_id = c.id AND r.status = 'applied') AS applied,
      (SELECT string_agg(r.message, ' · ') FROM command_result r WHERE r.command_id = c.id AND r.status = 'rejected') AS rejected
    FROM branch_command c WHERE ($1::uuid IS NULL OR c.branch_id = $1::uuid) ORDER BY c.seq DESC LIMIT 100`, [branch]);
  return rows.map(r => {
    const payload = { ...(r.payload as Row) }; delete payload.passwordHash; delete payload.image;
    const terminals = Number(r.terminals), applied = Number(r.applied);
    return { id: String(r.id), branchId: String(r.branch_id), kind: String(r.kind), payload, by: String(r.requested_by_name),
      at: r.requested_at instanceof Date ? r.requested_at.toISOString() : String(r.requested_at),
      status: r.rejected ? 'rejected' : terminals > 0 && applied >= terminals ? 'applied' : 'pending', message: r.rejected ? String(r.rejected) : null, applied, terminals };
  });
}

// ---------- Lado de la caja ----------

/**
 * Cambios que la caja todavía no reporta, en orden. Lo que ya reportó no vuelve a bajar (la caja relee un tramo por si llegaron
 * fuera de orden) y un lote no pasa de ~3 MB aunque traiga fotos: el resto baja en la siguiente vuelta.
 */
export async function pendingFor(q: Query, terminal: { id: string; branchId: string }, after: number) {
  const rows = await q(`SELECT id, seq, kind, payload, requested_by_id, requested_by_name, requested_at FROM (
      SELECT c.*, sum(octet_length(c.payload::text)) OVER (ORDER BY c.seq) - octet_length(c.payload::text) AS before
      FROM branch_command c WHERE c.branch_id = $1 AND c.seq > $2
        AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id AND r.terminal_id = $3)
      ORDER BY c.seq LIMIT 50) x
    WHERE before < 3000000 ORDER BY seq`, [terminal.branchId, after, terminal.id]);
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
