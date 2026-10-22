// Cambios hechos en la versión web (docs/WEB.md). La nube sólo los valida y los guarda en orden;
// cada caja de la sucursal los aplica con sus propias reglas y reporta si quedaron o por qué no.
import { randomUUID } from 'node:crypto';
import { ProtocolError } from './protocol.js';
import { identityHash, requirePermission, temporaryPassword, type WebUser } from './auth.js';
import { isUuid, MAIN_LOCATION } from './web.js';
import { parsePlan, productionPlan } from './planning.js';
import { project, settle } from './optimistic.js';
import type { Query, Row } from './panel.js';

export const KINDS = ['category.save', 'product.save', 'product.active', 'user.create', 'user.update', 'user.active', 'user.reset_password',
  'consignment.store.save', 'consignment.template.save',
  // Ruta: lo que el repartidor registra en la tienda desde el celular.
  'consignment.deliver', 'consignment.collect', 'consignment.payment', 'consignment.void',
  // Costo de referencia de un insumo, para calcular la ganancia desde las recetas.
  'inventory.cost',
  // Fase 3 del POS web (docs/POS_WEB.md): entradas, mermas y conteos de inventario, producción hecha y foto del producto.
  'inventory.receive', 'inventory.waste', 'inventory.count', 'production.record', 'product.photo',
  // Catálogo como la caja: quitar la foto de un producto.
  'product.photo.remove',
  // Inventario como la caja (W4): saldo inicial, artículos, presentaciones, asociaciones con productos y consumo automático por ventas.
  'inventory.opening', 'inventory.item.save', 'inventory.item.active', 'inventory.unit.add', 'inventory.link', 'inventory.unlink', 'inventory.consumption',
  // Recetas como la caja (W5): recetas (fórmulas) y perfiles de producto con borradores, publicar y activar.
  'recipe.formula.create', 'recipe.formula.draft', 'recipe.formula.publish', 'recipe.formula.activate',
  'recipe.profile.create', 'recipe.profile.draft', 'recipe.profile.publish', 'recipe.profile.activate',
  // Producción en dos pasos como la caja (W6): preparar el borrador con el plan y registrarlo con lo que salió.
  'production.prepare', 'production.confirm',
  // Charolas como la caja (W7): «Precios de charola».
  'consignment.price.save'] as const;
const POLICIES: Record<string, string[]> = { COUNT: ['ExactWholeCount', 'CeilingWholeCount'], MASS: ['NotApplicable'], VOLUME: ['NotApplicable'] };
const ITEM_KINDS = ['RawMaterial', 'FinishedGood', 'Packaging', 'Supply'];
const DIMENSIONS: Record<string, string> = { Mass: 'MASS', Volume: 'VOLUME', Count: 'COUNT' };
const CONSUMPTION: Record<string, [string, string]> = { Enable: ['Disabled', 'ENABLED'], Pause: ['Enabled', 'PAUSED'], Resume: ['Paused', 'ENABLED'] };
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

/** Alta pedida desde la web que la caja todavía no aplica (y no ha rechazado): cuenta como existente para lo que sigue en la fila. */
async function queued(q: Query, branch: string, kind: string, entityId: string): Promise<boolean> {
  return (await q(`SELECT 1 FROM branch_command c WHERE c.branch_id = $1 AND c.kind = $2 AND c.payload->>'id' = $3
    AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id AND r.status = 'rejected')`, [branch, kind, entityId])).length > 0;
}

/** Un cambio de ese tipo para ese registro ya está en la fila (sin rechazo): no se manda dos veces. */
async function sent(q: Query, branch: string, kind: string, key: string, value: string): Promise<boolean> {
  return (await q(`SELECT 1 FROM branch_command c WHERE c.branch_id = $1 AND c.kind = $2 AND c.payload->>$3 = $4
    AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id)`, [branch, kind, key, value])).length > 0;
}

/** Estado del consumo automático por ventas que subió la caja (dentro de la sucursal), o null si su versión todavía no lo sube. */
export async function consumptionState(q: Query, branch: string): Promise<{ state: string; version: number; updatedAt: string; updatedBy: string | null } | null> {
  const [row] = await q(`SELECT payload->'sale_consumption' AS c FROM entity_state WHERE branch_id = $1 AND entity = 'branch' AND NOT deleted`, [branch]);
  const c = row?.c as Row | null | undefined;
  if (!c || !s(c.state)) return null;
  return { state: s(c.state), version: Number(c.version), updatedAt: s(c.updated_utc), updatedBy: c.updated_by ? s(c.updated_by) : null };
}

async function activeItem(q: Query, branch: string, itemId: unknown) {
  const found = await state(q, branch, 'inventory_item', id(itemId, 'Artículo')) as (Row & { item: Row; units?: Row[] }) | null;
  if (!found) throw new ProtocolError('Artículo inexistente', 404);
  if (Number(found.item.active) !== 1) throw new ProtocolError('El artículo está inactivo');
  return found;
}

/**
 * Cantidad en la unidad de captura de la caja (g, ml o pieza), como InventoryDisplay.ParseCapture: hasta 3 decimales en g/ml,
 * enteros en piezas. Devuelve unidades mínimas (mg, µL, pieza).
 */
function capture(v: unknown, dimension: string, field: string): number {
  const value = s(v).trim();
  const count = dimension === 'COUNT';
  if (!(count ? /^\d{1,9}$/ : /^\d{1,9}(\.\d{1,3})?$/).test(value)) throw new ProtocolError(`${field}: ${count ? 'piezas enteras' : 'número con punto decimal, hasta 3 decimales'}`);
  const [whole, fraction = ''] = value.split('.');
  const atomic = count ? Number(whole) : Number(whole) * 1000 + Number(fraction.padEnd(3, '0'));
  if (atomic <= 0) throw new ProtocolError(`${field}: mayor que cero`);
  return atomic;
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
      // Como en la caja, la categoría puede crearse sin salir del producto: su alta va antes en la fila.
      if (!(await state(q, branch, 'category', category)) && !(await queued(q, branch, 'category.save', category))) throw new ProtocolError('La categoría no existe en esa sucursal');
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
      if (!(await state(q, branch, 'product', product)) && !(await queued(q, branch, 'product.save', product))) throw new ProtocolError('Producto inexistente', 404);
      const image = s(b.image);
      if (image.length === 0 || image.length > MAX_PHOTO_BASE64 || !/^[A-Za-z0-9+/]+={0,2}$/.test(image)) throw new ProtocolError('Foto inválida o muy grande (máximo 1 MB)');
      const size = jpegSize(Buffer.from(image, 'base64'));
      if (!size) throw new ProtocolError('La foto debe ser JPEG');
      if (size.width !== size.height || size.width < 256 || size.width > 2048) throw new ProtocolError('La foto debe ser cuadrada, de 256 a 2048 píxeles');
      // Origen como en la caja: «Foto real» o «Foto provisional» (referencia de trabajo).
      const provenance = b.provenance === undefined ? 'REAL' : b.provenance === 'REAL' || b.provenance === 'PROVISIONAL' ? b.provenance : null;
      if (!provenance) throw new ProtocolError('Origen de la foto inválido');
      payload = { productId: product, image, edge: size.width, name: 'foto-web.jpg', provenance };
      break;
    }
    case 'product.photo.remove': {
      requirePermission(user, 'products.edit');
      const product = id(b.productId, 'Producto');
      if (!(await state(q, branch, 'product', product))) throw new ProtocolError('Producto inexistente', 404);
      payload = { productId: product };
      break;
    }
    case 'inventory.opening': {
      // Como «Saldo inicial» en la caja: sólo el administrador, una vez por artículo, y puede ser cero.
      requirePermission(user, 'inventory.adjust');
      if (!user.admin) throw new ProtocolError('El saldo inicial lo registra el administrador', 403);
      const base = await stock(q, branch, b, true);
      const found = await state(q, branch, 'inventory_item', base.itemId) as (Row & { balances: Row[] });
      if (found.balances.some(x => s(x.location_id) === base.locationId)) throw new ProtocolError('Este artículo ya tiene saldo inicial; usa Conteo físico');
      if (await sent(q, branch, 'inventory.opening', 'itemId', base.itemId)) throw new ProtocolError('Ya se mandó el saldo inicial de este artículo; espera a que la caja lo aplique');
      payload = { ...base, ...optional(b.reference, 'Referencia', 160, 'reference') };
      break;
    }
    case 'inventory.item.save': {
      requirePermission(user, 'inventory.configure');
      const target = id(b.id, 'Artículo');
      const existing = await state(q, branch, 'inventory_item', target) as (Row & { item: Row }) | null;
      const kindValue = s(b.kind);
      if (!ITEM_KINDS.includes(kindValue)) throw new ProtocolError('Tipo de artículo inválido');
      const name = text(b.name, 'Nombre', 1, 160);
      if (existing) {
        // El código y la dimensión quedan fijos después de crear, como en la caja.
        payload = { id: target, name, kind: kindValue };
      } else {
        const code = text(b.code, 'Código', 1, 80);
        if (!DIMENSIONS[s(b.dimension)]) throw new ProtocolError('Dimensión inválida');
        const [taken] = await q(`SELECT 1 FROM entity_state WHERE branch_id = $1 AND entity = 'inventory_item' AND upper(payload->'item'->>'code') = upper($2)`, [branch, code]);
        if (taken) throw new ProtocolError('Ya existe un artículo con ese código');
        payload = { id: target, code, name, kind: kindValue, dimension: s(b.dimension) };
      }
      break;
    }
    case 'inventory.item.active': {
      requirePermission(user, 'inventory.configure');
      const target = id(b.id, 'Artículo');
      const found = await state(q, branch, 'inventory_item', target) as (Row & { item: Row; balances: Row[] }) | null;
      if (!found) throw new ProtocolError('Artículo inexistente', 404);
      const active = bool(b.active, 'Activo');
      if (!active && found.balances.some(x => s(x.location_id) === MAIN_LOCATION && Number(x.quantity_atomic) !== 0)) throw new ProtocolError('No se puede desactivar un artículo con existencia');
      payload = { id: target, active };
      break;
    }
    case 'inventory.unit.add': {
      requirePermission(user, 'inventory.configure');
      const item = await activeItem(q, branch, b.itemId);
      const code = text(b.code, 'Código', 1, 80);
      if (UNITS[code] || (item.units ?? []).some(u => s(u.code).toUpperCase() === code.toUpperCase())) throw new ProtocolError('Ya existe una presentación con ese código');
      payload = { itemId: s(item.item.id), code, name: text(b.name, 'Nombre', 1, 100), atomic: capture(b.quantity, s(item.item.dimension), 'Cantidad por presentación') };
      break;
    }
    case 'inventory.link': {
      requirePermission(user, 'inventory.configure');
      const item = await activeItem(q, branch, b.itemId);
      const product = id(b.productId, 'Producto');
      const found = await state(q, branch, 'product', product) as (Row & { product: Row; inventoryLinks?: Row[] }) | null;
      if (!found) throw new ProtocolError('Producto inexistente', 404);
      if ((found.inventoryLinks ?? []).some(l => Number(l.active) === 1)) throw new ProtocolError('El producto ya tiene una asociación activa; desvincúlalo primero');
      payload = { itemId: s(item.item.id), productId: product, atomic: capture(b.quantity, s(item.item.dimension), 'Cantidad por unidad vendida') };
      break;
    }
    case 'inventory.unlink': {
      requirePermission(user, 'inventory.configure');
      const product = id(b.productId, 'Producto');
      const found = await state(q, branch, 'product', product) as (Row & { inventoryLinks?: Row[] }) | null;
      if (!found || !(found.inventoryLinks ?? []).some(l => Number(l.active) === 1)) throw new ProtocolError('El producto no tiene una asociación activa');
      payload = { productId: product };
      break;
    }
    case 'production.prepare': {
      requirePermission(user, 'production.create');
      const plan = parsePlan(JSON.stringify({ outputs: b.outputs, selections: b.selections ?? [] }));
      const preview = await productionPlan(q, branch, plan);
      if (!preview.canConfirm) throw new ProtocolError(preview.problem ?? 'El plan tiene datos pendientes de corregir.');
      payload = { batchId: id(b.batchId, 'Producción'), operationId: id(b.operationId, 'Operación'), outputs: plan.outputs, selections: plan.selections,
        ...optional(b.notes, 'Notas', 500, 'notes') };
      break;
    }
    case 'production.confirm': {
      requirePermission(user, 'production.confirm');
      const batchId = id(b.batchId, 'Producción');
      const batch = await state(q, branch, 'production_batch', batchId) as (Row & { batch: Row; outputs: Row[] }) | null;
      if (!batch) throw new ProtocolError('La producción todavía no llega de la caja', 404);
      if (s(batch.batch.status) !== 'DRAFT') throw new ProtocolError('Esa producción ya está registrada');
      // Como en la caja: sólo quien creó el borrador lo registra.
      if (s(batch.batch.created_by) !== user.id) throw new ProtocolError('Sólo el usuario que creó este borrador puede registrarlo', 403);
      if (await sent(q, branch, 'production.confirm', 'batchId', batchId)) throw new ProtocolError('Ya se mandó el registro de esta producción; espera a que la caja lo aplique');
      const given = new Map((Array.isArray(b.actuals) ? b.actuals : []).map((a: unknown) => { const x = (a ?? {}) as Record<string, unknown>; return [s(x.productId), x.actual]; }));
      const actuals = batch.outputs.map(o => ({ productId: s(o.product_id), actual: int(given.get(s(o.product_id)), `Piezas reales de ${s(o.product_name)}`, 0, 100_000) }));
      if (given.size !== actuals.length) throw new ProtocolError('Captura las piezas reales de cada producto del plan');
      payload = { batchId, actuals };
      break;
    }
    case 'consignment.price.save': {
      // Lo que paga la tienda por cada pieza vendida; sin precio, el pan ya no se puede poner en charolas.
      requirePermission(user, 'consignment.manage');
      if (!Array.isArray(b.prices) || b.prices.length === 0 || b.prices.length > 300) throw new ProtocolError('Precios inválidos');
      const seen = new Set<string>();
      const prices = [];
      for (const raw of b.prices as unknown[]) {
        const x = (raw ?? {}) as Record<string, unknown>;
        const product = id(x.productId, 'Producto');
        if (seen.has(product)) throw new ProtocolError('Producto repetido');
        seen.add(product);
        if (!(await state(q, branch, 'product', product))) throw new ProtocolError('No se encontró el producto.', 404);
        prices.push({ productId: product, priceCents: x.priceCents === null ? null : int(x.priceCents, 'Precio de charola', 0, 10_000_000) });
      }
      payload = { prices };
      break;
    }
    case 'inventory.consumption': {
      requirePermission(user, 'inventory.sale_consumption.configure');
      const action = s(b.action);
      if (!CONSUMPTION[action]) throw new ProtocolError('Acción inválida');
      const current = await consumptionState(q, branch);
      if (!current) throw new ProtocolError('La caja todavía no sube el estado del consumo automático; actualízala', 409);
      const [from] = CONSUMPTION[action];
      if (current.state !== from.toUpperCase() || int(b.version, 'Versión', 1, Number.MAX_SAFE_INTEGER) !== current.version)
        throw new ProtocolError('El estado cambió; actualiza la página y revisa antes de intentar nuevamente', 409);
      if (await sent(q, branch, 'inventory.consumption', 'version', String(current.version)))
        throw new ProtocolError('Ya se mandó un cambio del consumo automático; espera a que la caja lo aplique', 409);
      payload = { action, state: from, version: current.version };
      break;
    }
  }
  if ((kind as string).startsWith('consignment.') && ['consignment.deliver', 'consignment.collect', 'consignment.payment', 'consignment.void'].includes(kind as string))
    payload = await route(q, branch, kind as Kind, b);
  if ((kind as string).startsWith('recipe.')) { requirePermission(user, 'recipe.configure'); payload = await recipe(q, branch, kind as Kind, b); }
  const commandId = randomUUID();
  await q(`INSERT INTO branch_command(id, branch_id, kind, payload, requested_by_id, requested_by_name) VALUES ($1, $2, $3, $4::jsonb, $5, $6)`,
    [commandId, branch, kind, JSON.stringify(payload!), user.id, user.name]);
  // I1: catálogo, usuarios y tiendas se ven cambiados en la web desde ya; la caja lo confirma o, si lo rechaza, se regresa.
  await project(q, branch, commandId, kind as string, payload!);
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

/**
 * Recetas y perfiles: se revisan contra lo último que subió la caja (y lo que ya va en la fila) para que la caja sólo reciba cambios
 * que puede aplicar. Las cantidades viajan en unidades mínimas (mg, µL o piezas); la caja pone el número de versión.
 */
async function recipe(q: Query, branch: string, kind: Kind, b: Record<string, unknown>): Promise<Record<string, unknown>> {
  const formulas = (await q(`SELECT payload FROM entity_state WHERE branch_id = $1 AND entity = 'formula' AND NOT deleted`, [branch])).map(r => r.payload as Row & { formula: Row; versions: Row[] });
  const profiles = (await q(`SELECT payload FROM entity_state WHERE branch_id = $1 AND entity = 'production_profile' AND NOT deleted`, [branch])).map(r => r.payload as Row & { profile: Row; versions: Row[] });
  const revision = (v: unknown) => int(v, 'Revisión', 1, Number.MAX_SAFE_INTEGER);
  const formulaVersion = (versionId: string) => {
    for (const f of formulas) { const v = f.versions.find(x => s(x.id) === versionId); if (v) return { formula: f.formula, version: v }; }
    return null;
  };
  const profileVersion = (versionId: string) => {
    for (const p of profiles) { const v = p.versions.find(x => s(x.id) === versionId); if (v) return { profile: p.profile, version: v }; }
    return null;
  };
  const items = new Map<string, Row>();
  const line = async (raw: unknown, field: string) => {
    const x = (raw ?? {}) as Record<string, unknown>;
    const itemId = id(x.itemId, 'Ingrediente');
    if (!items.has(itemId)) items.set(itemId, ((await state(q, branch, 'inventory_item', itemId)) as (Row & { item: Row }) | null)?.item ?? {});
    const item = items.get(itemId)!;
    if (Number(item.active) !== 1) throw new ProtocolError(`${field}: el artículo no existe o está inactivo`);
    const policy = s(x.policy);
    if (!POLICIES[s(item.dimension)]?.includes(policy)) throw new ProtocolError(`${field}: política de cantidad inválida para ese artículo`);
    return { itemId, atomic: int(x.atomic, `${field}: cantidad`, 1, 1_000_000_000_000), policy };
  };
  const list = (v: unknown, field: string, max: number) => { if (!Array.isArray(v) || v.length > max) throw new ProtocolError(`${field} inválidos`); return v as unknown[]; };
  const unique = (ids: string[]) => { if (new Set(ids).size !== ids.length) throw new ProtocolError('Un mismo artículo no puede repetirse en la receta'); };
  const draftTarget = (found: { version: Row } | null, rev: number | null, owner: string, ownerKey: string) => {
    if (rev === null) { if (found) throw new ProtocolError('Esa versión ya existe; actualiza la página'); return; }
    if (!found || s(found.version[ownerKey]) !== owner || s(found.version.state) !== 'DRAFT' || Number(found.version.revision) !== rev)
      throw new ProtocolError('La receta cambió. Actualiza la información antes de continuar', 409);
  };
  switch (kind) {
    case 'recipe.formula.create': {
      const target = id(b.id, 'Receta');
      const code = text(b.code, 'Código', 1, 60);
      if (formulas.some(f => s(f.formula.code).toUpperCase() === code.toUpperCase())) throw new ProtocolError('Ya existe una receta con ese código');
      const pending = await q(`SELECT 1 FROM branch_command c WHERE c.branch_id = $1 AND c.kind = 'recipe.formula.create' AND upper(c.payload->>'code') = upper($2)
        AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id AND r.status = 'rejected')`, [branch, code]);
      if (pending.length) throw new ProtocolError('Ya se mandó una receta con ese código');
      return { id: target, code, name: text(b.name, 'Nombre', 1, 160) };
    }
    case 'recipe.formula.draft': {
      const formulaId = id(b.formulaId, 'Receta');
      if (!formulas.some(f => s(f.formula.id) === formulaId) && !(await queued(q, branch, 'recipe.formula.create', formulaId))) throw new ProtocolError('La receta no existe', 404);
      const versionId = id(b.versionId, 'Versión');
      const rev = b.revision === null || b.revision === undefined ? null : revision(b.revision);
      draftTarget(formulaVersion(versionId), rev, formulaId, 'formula_id');
      const y = (b.yield ?? {}) as Record<string, unknown>;
      if (!DIMENSIONS[s(y.dimension)]) throw new ProtocolError('Dimensión del rendimiento inválida');
      const ingredients = [];
      for (const [n, raw] of list(b.ingredients, 'Ingredientes', 60).entries()) ingredients.push(await line(raw, `Ingrediente ${n + 1}`));
      const groups = [];
      for (const [g, rawGroup] of list(b.groups, 'Grupos de alternativas', 20).entries()) {
        const options = list(rawGroup, `Grupo ${g + 1}`, 10);
        if (options.length < 2) throw new ProtocolError('Cada grupo de alternativas requiere al menos dos opciones');
        const group = [];
        for (const [o, raw] of options.entries()) group.push({ id: id((raw as Record<string, unknown>)?.id, 'Opción'), ...(await line(raw, `Grupo ${g + 1}, opción ${o + 1}`)) });
        groups.push(group);
      }
      // Otras preparaciones que usa (masa dulce dentro de la enriquecida). Si no vienen, la caja conserva las que ya tenía el borrador.
      const preparations = b.preparations === undefined ? undefined : list(b.preparations, 'Preparaciones', 20).map((raw, n) => {
        const x = (raw ?? {}) as Record<string, unknown>;
        const used = id(x.formulaVersionId, `Preparación ${n + 1}`);
        const found = formulaVersion(used);
        if (!found) throw new ProtocolError(`Preparación ${n + 1}: la receta no existe`);
        if (s(found.formula.id) === formulaId) throw new ProtocolError('Una receta no puede usar otra versión de sí misma');
        return { formulaVersionId: used, atomic: int(x.atomic, `Preparación ${n + 1}: cantidad`, 1, 1_000_000_000_000) };
      });
      if (preparations && new Set(preparations.map(p => p.formulaVersionId)).size !== preparations.length) throw new ProtocolError('Una misma preparación no puede repetirse en la receta');
      if (ingredients.length + groups.length + (preparations?.length ?? 0) === 0) throw new ProtocolError('Agrega al menos un ingrediente, una preparación o un grupo de alternativas');
      unique([...ingredients.map(i => i.itemId), ...groups.flat().map(o => o.itemId)]);
      return { formulaId, versionId, revision: rev, name: text(b.name, 'Nombre de la versión', 1, 160), notes: text(b.notes ?? '', 'Notas', 0, 500),
        yield: { dimension: s(y.dimension), atomic: int(y.atomic, 'Rendimiento', 1, 1_000_000_000_000) }, ingredients, groups, ...(preparations ? { preparations } : {}) };
    }
    case 'recipe.formula.publish': case 'recipe.profile.publish': {
      const versionId = id(b.versionId, 'Versión');
      const found = kind === 'recipe.formula.publish' ? formulaVersion(versionId) : profileVersion(versionId);
      if (!found) throw new ProtocolError('La versión no existe', 404);
      if (s(found.version.state) !== 'DRAFT') throw new ProtocolError('Esa versión ya está publicada');
      // La caja también lo rechaza; aquí se avisa antes de mandarlo.
      if (Number(found.version.pending_measure) === 1) throw new ProtocolError('No se puede publicar: esta versión está marcada «Por medir» porque tiene datos pendientes de confirmar. Quita la marca en la caja cuando las medidas estén confirmadas');
      if (Number(found.version.revision) !== revision(b.revision)) throw new ProtocolError('La receta cambió. Actualiza la información antes de continuar', 409);
      return { versionId, revision: Number(found.version.revision) };
    }
    case 'recipe.formula.activate': case 'recipe.profile.activate': {
      const formula = kind === 'recipe.formula.activate';
      const ownerId = id(formula ? b.formulaId : b.profileId, formula ? 'Receta' : 'Perfil');
      const versionId = id(b.versionId, 'Versión');
      const found = formula ? formulaVersion(versionId) : profileVersion(versionId);
      const owner = formula ? (found as { formula: Row } | null)?.formula : (found as { profile: Row } | null)?.profile;
      if (!found || !owner || s(owner.id) !== ownerId) throw new ProtocolError('La versión no existe', 404);
      if (s(found.version.state) !== 'PUBLISHED') throw new ProtocolError('Publica la versión antes de activarla');
      if (s(owner.active_version_id) === versionId) throw new ProtocolError('Esa versión ya está activa');
      if (Number(owner.revision) !== revision(b.revision)) throw new ProtocolError('La receta cambió. Actualiza la información antes de continuar', 409);
      return { [formula ? 'formulaId' : 'profileId']: ownerId, versionId, revision: Number(owner.revision) };
    }
    case 'recipe.profile.create': {
      const target = id(b.id, 'Perfil');
      const productId = id(b.productId, 'Producto');
      const product = (await state(q, branch, 'product', productId)) as (Row & { product: Row }) | null;
      if (!product || Number(product.product.active) !== 1) throw new ProtocolError('El producto no existe o está inactivo');
      const itemId = id(b.finishedItemId, 'Artículo terminado');
      const item = ((await state(q, branch, 'inventory_item', itemId)) as (Row & { item: Row }) | null)?.item;
      if (!item || Number(item.active) !== 1 || s(item.kind) !== 'FINISHED_GOOD' || s(item.dimension) !== 'COUNT')
        throw new ProtocolError('El artículo terminado debe ser un Producto terminado activo que se cuenta en piezas');
      const pending = await q(`SELECT 1 FROM branch_command c WHERE c.branch_id = $1 AND c.kind = 'recipe.profile.create' AND c.payload->>'productId' = $2
        AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id AND r.status = 'rejected')`, [branch, productId]);
      if (profiles.some(p => s(p.profile.product_id) === productId) || pending.length) throw new ProtocolError('Ese producto ya tiene un perfil productivo');
      return { id: target, productId, finishedItemId: itemId };
    }
    case 'recipe.profile.draft': {
      const profileId = id(b.profileId, 'Perfil');
      if (!profiles.some(p => s(p.profile.id) === profileId) && !(await queued(q, branch, 'recipe.profile.create', profileId))) throw new ProtocolError('El perfil no existe', 404);
      const versionId = id(b.versionId, 'Versión');
      const rev = b.revision === null || b.revision === undefined ? null : revision(b.revision);
      draftTarget(profileVersion(versionId), rev, profileId, 'profile_id');
      const requirements = list(b.formulas, 'Preparaciones', 30).map((raw, n) => {
        const x = (raw ?? {}) as Record<string, unknown>;
        const formulaVersionId = id(x.formulaVersionId, `Preparación ${n + 1}`);
        if (s(formulaVersion(formulaVersionId)?.version.state) !== 'PUBLISHED') throw new ProtocolError(`Preparación ${n + 1}: debe estar publicada`);
        return { formulaVersionId, atomic: int(x.atomic, `Preparación ${n + 1}: cantidad`, 1, 1_000_000_000_000) };
      });
      if (new Set(requirements.map(r => r.formulaVersionId)).size !== requirements.length) throw new ProtocolError('Una preparación no puede repetirse');
      const direct = [];
      for (const [n, raw] of list(b.direct, 'Ingredientes directos', 60).entries()) direct.push(await line(raw, `Ingrediente ${n + 1}`));
      unique(direct.map(d => d.itemId));
      if (requirements.length + direct.length === 0) throw new ProtocolError('Agrega al menos una preparación o ingrediente directo');
      return { profileId, versionId, revision: rev, name: text(b.name, 'Nombre de la versión', 1, 160), formulas: requirements, direct };
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
/** Últimos cambios de la sucursal; con «pending», todos los que la caja todavía no aplica ni rechazó (para el cargador, sin tope de 100). */
export async function listCommands(q: Query, branch: string | null, pending = false) {
  const columns = `c.id, c.seq, c.branch_id, c.kind, c.payload - 'image' - 'passwordHash' AS payload, c.requested_by_name, c.requested_at,
      (SELECT count(*) FROM terminal t WHERE t.branch_id = c.branch_id AND t.revoked_at IS NULL AND t.kind = 'desktop') AS terminals,
      (SELECT count(*) FROM command_result r WHERE r.command_id = c.id AND r.status = 'applied') AS applied,
      (SELECT string_agg(r.message, ' · ') FROM command_result r WHERE r.command_id = c.id AND r.status = 'rejected') AS rejected`;
  const rows = pending
    ? await q(`SELECT * FROM (SELECT ${columns} FROM branch_command c WHERE ($1::uuid IS NULL OR c.branch_id = $1::uuid)
        AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id AND r.status = 'rejected')) x
        WHERE x.terminals = 0 OR x.applied < x.terminals ORDER BY x.seq DESC LIMIT 5000`, [branch])
    : await q(`SELECT ${columns} FROM branch_command c WHERE ($1::uuid IS NULL OR c.branch_id = $1::uuid) ORDER BY c.seq DESC LIMIT 100`, [branch]);
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
  await settle(q, clean.map(r => r.id));
  return { recorded: clean.length };
}
