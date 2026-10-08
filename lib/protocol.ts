// Contrato entre la app de escritorio y la nube. Sólo sintaxis TypeScript borrable (Node 24 la ejecuta sin compilar).
import { createHash, randomBytes, timingSafeEqual } from 'node:crypto';

export const MAX_BATCH = 200;
export const ENTITIES = [
  'cash_session', 'cash_count', 'sale', 'sale_refund', 'audit_entry',
  'branch', 'app_user', 'role_permission', 'category', 'product', 'product_photo', 'cash_movement',
  'consignment_store', 'consignment_price', 'consignment_delivery', 'consignment_payment',
  'measurement_unit', 'inventory_location', 'inventory_item', 'inventory_operation',
  'formula', 'production_profile', 'production_batch',
] as const;
export const CHANGES = ['insert', 'update', 'delete'] as const;
export type Entity = (typeof ENTITIES)[number];

export interface SyncEntry {
  seq: number;
  entity: Entity;
  entityId: string;
  change: (typeof CHANGES)[number];
  enqueuedAt: string;
  payload: unknown;
}
export interface SyncBatch { branchId: string; entries: SyncEntry[] }
export interface Registration { enrollmentSecret: string; branch: { id: string; name: string }; terminal: { id: string; name: string } }

export class ProtocolError extends Error {
  readonly status: number;
  constructor(message: string, status = 400) { super(message); this.status = status; }
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function text(value: unknown, field: string, max: number): string {
  if (typeof value !== 'string' || value.trim().length === 0 || value.trim().length > max) throw new ProtocolError(`${field} inválido`);
  return value.trim();
}
function uuid(value: unknown, field: string): string {
  if (typeof value !== 'string' || !UUID.test(value)) throw new ProtocolError(`${field} debe ser UUID`);
  return value.toLowerCase();
}

export function parseRegistration(body: unknown): Registration {
  const b = body as Record<string, any> | null;
  if (!b || typeof b !== 'object') throw new ProtocolError('Cuerpo inválido');
  return {
    enrollmentSecret: text(b.enrollmentSecret, 'enrollmentSecret', 200),
    branch: { id: uuid(b.branch?.id, 'branch.id'), name: text(b.branch?.name, 'branch.name', 100) },
    terminal: { id: uuid(b.terminal?.id, 'terminal.id'), name: text(b.terminal?.name, 'terminal.name', 100) },
  };
}

export function parseBatch(body: unknown): SyncBatch {
  const b = body as Record<string, any> | null;
  if (!b || typeof b !== 'object' || !Array.isArray(b.entries)) throw new ProtocolError('Cuerpo inválido');
  if (b.entries.length === 0 || b.entries.length > MAX_BATCH) throw new ProtocolError(`El lote debe tener de 1 a ${MAX_BATCH} entradas`);
  const seen = new Set<number>();
  const entries = b.entries.map((e: any, i: number): SyncEntry => {
    if (!e || typeof e !== 'object') throw new ProtocolError(`Entrada ${i} inválida`);
    if (!Number.isSafeInteger(e.seq) || e.seq < 1 || seen.has(e.seq)) throw new ProtocolError(`Entrada ${i}: seq inválido o repetido`);
    seen.add(e.seq);
    if (!ENTITIES.includes(e.entity)) throw new ProtocolError(`Entrada ${i}: entity desconocida`);
    if (!CHANGES.includes(e.change)) throw new ProtocolError(`Entrada ${i}: change inválido`);
    if (typeof e.enqueuedAt !== 'string' || Number.isNaN(Date.parse(e.enqueuedAt))) throw new ProtocolError(`Entrada ${i}: enqueuedAt inválido`);
    if (e.payload === undefined || e.payload === null || typeof e.payload !== 'object') throw new ProtocolError(`Entrada ${i}: payload requerido`);
    return { seq: e.seq, entity: e.entity, entityId: text(e.entityId, `Entrada ${i}: entityId`, 64), change: e.change, enqueuedAt: e.enqueuedAt, payload: e.payload };
  });
  return { branchId: uuid(b.branchId, 'branchId'), entries };
}

export function sha256Hex(value: string): string {
  return createHash('sha256').update(value, 'utf8').digest('hex');
}

/** Comparación en tiempo constante del secreto de alta (sin filtrar longitud por tiempo). */
export function secretMatches(provided: string, expected: string | undefined): boolean {
  if (!expected || expected.length < 24) return false;
  const a = Buffer.from(sha256Hex(provided), 'hex');
  const b = Buffer.from(sha256Hex(expected), 'hex');
  return timingSafeEqual(a, b);
}

export function newTerminalKey(): string {
  return randomBytes(32).toString('base64url');
}

export function bearer(header: string | null): string {
  const match = /^Bearer ([A-Za-z0-9_-]{43})$/.exec(header ?? '');
  if (!match) throw new ProtocolError('Credencial de terminal requerida', 401);
  return match[1];
}

/** Clasifica cada entrada: nueva, ya recibida idéntica (reenvío) o en conflicto (mismo seq con otro contenido). */
export function reconcile(entries: SyncEntry[], existing: Map<number, { entity: string; entityId: string; change: string }>) {
  const accepted: number[] = [];
  const conflicts: number[] = [];
  for (const e of entries) {
    const prior = existing.get(e.seq);
    if (!prior || (prior.entity === e.entity && prior.entityId === e.entityId && prior.change === e.change)) accepted.push(e.seq);
    else conflicts.push(e.seq);
  }
  return { accepted, conflicts };
}

export function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' } });
}

export async function readJson(request: Request, maxBytes = 4_000_000): Promise<unknown> {
  const length = Number(request.headers.get('content-length') ?? '0');
  if (length > maxBytes) throw new ProtocolError('Lote demasiado grande', 413);
  const raw = await request.text();
  if (raw.length > maxBytes) throw new ProtocolError('Lote demasiado grande', 413);
  try { return JSON.parse(raw); } catch { throw new ProtocolError('JSON inválido'); }
}

export function failure(error: unknown): Response {
  if (error instanceof ProtocolError) return json(error.status, { error: error.message });
  console.error('casamiga.api.failure', error instanceof Error ? error.name : 'unknown');
  return json(500, { error: 'Error interno' });
}
