// Punto de venta en la web (docs/POS_WEB.md): cajas web con su propio turno, folio y ventas, con las mismas reglas
// que la caja de escritorio (CheckoutService, CashService, CashClosingService).
// Cada operación es UNA sentencia: actualiza la fila de la caja (consecutivos y turno; sirve de candado y de condición)
// y escribe el registro inmutable (sync_event) y el estado (entity_state) con los mismos formatos que sube la caja,
// así Resumen, Ventas, Caja y Reportes las cuentan sin cambios. Si la condición no se cumple, no se escribe nada.
import { randomBytes, randomUUID } from 'node:crypto';
import { ProtocolError } from './protocol.js';
import { requirePermission, type WebUser } from './auth.js';
import { displayFolio, isUuid, names, terminalCode } from './web.js';
import type { Query, Row } from './panel.js';

const n = (v: unknown) => (v === null || v === undefined ? 0 : Number(v));
const s = (v: unknown) => (v === null || v === undefined ? '' : String(v));
export const MAX_LINES = 100;
export const MAX_QUANTITY = 999;
export const MAX_CENTS = 100_000_000; // $1,000,000
const METHODS = ['CASH', 'CARD', 'TRANSFER'] as const;
type Method = typeof METHODS[number];

const conflict = (message: string) => new ProtocolError(message, 409);
const CLOSED = 'La caja debe estar abierta y sin conteo confirmado.';

function id(value: unknown, label: string): string {
  if (!isUuid(value)) throw new ProtocolError(`${label} inválido`);
  return value.toLowerCase();
}
function cents(value: unknown, label: string, min = 0): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < min || value > MAX_CENTS) throw new ProtocolError(`${label} inválido`);
  return value;
}

// ---------- Cajas web ----------

interface Till { id: string; branchId: string; name: string; openSessionId: string | null; counted: boolean }

async function till(q: Query, user: WebUser, value: unknown): Promise<Till> {
  const tillId = id(value, 'Caja');
  const [row] = await q(`SELECT t.id, t.branch_id, t.name, t.revoked_at, w.open_session_id, w.session_counted
    FROM terminal t JOIN web_till w ON w.terminal_id = t.id WHERE t.id = $1`, [tillId]);
  if (!row || row.revoked_at) throw new ProtocolError('Caja web inexistente', 404);
  if (!user.admin && s(row.branch_id) !== user.branchId) throw new ProtocolError('Sin acceso a esa sucursal', 403);
  return { id: s(row.id), branchId: s(row.branch_id), name: s(row.name), openSessionId: row.open_session_id ? s(row.open_session_id) : null, counted: row.session_counted === true };
}

/**
 * Cierres anteriores de una caja web (CashHistoryView de la caja): sólo turnos cerrados, el más reciente primero,
 * 25 por página, con quién abrió, cerró y contó, y la nota de discrepancia. Sólo lectura (permiso cash.view).
 */
export async function closures(q: Query, user: WebUser, tillValue: unknown, pageValue: unknown) {
  requirePermission(user, 'cash.view');
  const t = await till(q, user, tillValue);
  const page = Number.isSafeInteger(Number(pageValue)) && Number(pageValue) >= 0 && Number(pageValue) < 10_000 ? Number(pageValue) : 0;
  const size = 25;
  const rows = await q(`SELECT c.payload AS p, n.payload AS n, count(*) OVER () AS total
    FROM entity_state c LEFT JOIN entity_state n ON n.entity = 'cash_count' AND NOT n.deleted AND n.payload->>'session_id' = c.entity_id
    WHERE c.entity = 'cash_session' AND NOT c.deleted AND c.terminal_id = $1 AND c.payload->>'state' = 'CLOSED'
    ORDER BY (c.payload->>'closed_utc')::timestamptz DESC, c.entity_id DESC LIMIT ${size} OFFSET $2`, [t.id, page * size]);
  const k = await names(q);
  const who = (v: unknown) => k.user.get(s(v)) ?? 'No disponible';
  const amount = (v: unknown) => (v === null || v === undefined ? null : n(v));
  return {
    page, pageSize: size, total: n(rows[0]?.total),
    items: rows.map(r => {
      const p = r.p as Row, c = (r.n ?? {}) as Row;
      return { id: s(p.id), terminal: t.name, openedAt: s(p.opened_utc), openedBy: who(p.opened_by), opening: n(p.opening_cents), closedAt: s(p.closed_utc),
        closedBy: who(p.closed_by), countedBy: who(c.counted_by), expected: amount(p.expected_cents), counted: amount(p.counted_cents ?? c.counted_cents),
        difference: amount(p.difference_cents), note: p.discrepancy_note ? s(p.discrepancy_note) : null };
    }),
  };
}

export async function tills(q: Query, branch: string | null) {
  const rows = await q(`SELECT t.id, t.branch_id, t.name, w.open_session_id, w.session_counted FROM terminal t JOIN web_till w ON w.terminal_id = t.id
    WHERE t.revoked_at IS NULL AND ($1::uuid IS NULL OR t.branch_id = $1::uuid) ORDER BY t.name`, [branch]);
  return rows.map(r => ({ id: s(r.id), branchId: s(r.branch_id), name: s(r.name), code: terminalCode(s(r.name)), open: r.open_session_id !== null, counted: r.session_counted === true }));
}

/** Alta de una caja web (administrador). Su número sigue al mayor de la sucursal para que su folio no se confunda (C02, C03…). */
export async function createTill(q: Query, user: WebUser, branchValue: unknown) {
  requirePermission(user, 'users.manage');
  const branchId = id(branchValue, 'Sucursal');
  if (!user.admin && branchId !== user.branchId) throw new ProtocolError('Sin acceso a esa sucursal', 403);
  const [branch] = await q('SELECT id FROM branch WHERE id = $1', [branchId]);
  if (!branch) throw new ProtocolError('Sucursal inexistente', 404);
  const names = await q('SELECT name FROM terminal WHERE branch_id = $1', [branchId]);
  const next = Math.max(0, ...names.map(t => Number(terminalCode(s(t.name)).slice(1)))) + 1;
  if (next > 9999) throw conflict('Ya no hay números de caja disponibles.');
  const tillId = randomUUID();
  const name = `CAJA-WEB-${String(next).padStart(2, '0')}`;
  // La caja web no se registra con llave: la suya es aleatoria y nadie la conoce.
  await q(`WITH t AS (INSERT INTO terminal(id, branch_id, name, key_sha256, kind, last_seen_at) VALUES ($1, $2, $3, $4, 'web', now()) RETURNING id, branch_id)
    INSERT INTO web_till(terminal_id, branch_id, created_by) SELECT id, branch_id, $5 FROM t`, [tillId, branchId, name, randomBytes(32).toString('hex'), user.id]);
  return { id: tillId, branchId, name, code: terminalCode(name), open: false, counted: false };
}

// ---------- Escritura atómica ----------

type Event = { entity: 'cash_session' | 'cash_count' | 'sale' | 'sale_refund' | 'cash_movement' | 'audit_entry'; entity_id: string; change: 'insert' | 'update'; payload: unknown };

/** Condición sobre la caja y lo que cambia en ella. $4 = turno. */
const GUARD = {
  open: ['open_session_id IS NULL', 'open_session_id = $4::uuid, session_counted = false'],
  sale: ['open_session_id = $4::uuid AND NOT session_counted', 'sale_seq = sale_seq + 1'],
  count: ['open_session_id = $4::uuid AND NOT session_counted', 'session_counted = true'],
  close: ['open_session_id = $4::uuid AND session_counted', 'open_session_id = NULL, session_counted = false'],
  // Devoluciones y movimientos: sólo con el turno abierto y sin contar; no cambian nada más de la caja.
  drawer: ['open_session_id = $4::uuid AND NOT session_counted', ''],
} as const;

/**
 * Una sentencia: si la caja cumple la condición, reserva consecutivos, guarda el identificador de operación (cobros),
 * y escribe los eventos (la venta recibe aquí su consecutivo). Devuelve los registros escritos; vacío si no se cumplió.
 */
async function write(q: Query, tillId: string, guard: keyof typeof GUARD, session: string, events: Event[],
  operation?: { id: string; entityId: string; kind?: 'sale' | 'refund' | 'movement' }) {
  const [where, set] = GUARD[guard];
  const rows = await q(`WITH t AS (
      UPDATE web_till SET event_seq = event_seq + $2${set ? `, ${set}` : ''}
      WHERE terminal_id = $1 AND ${where}
      RETURNING branch_id, event_seq - $2 AS base, sale_seq),
    op AS (INSERT INTO web_operation(operation_id, terminal_id, kind, entity_id) SELECT $5::uuid, $1, $7::text, $6::text FROM t WHERE $5::uuid IS NOT NULL),
    ev AS (SELECT t.branch_id, t.base + e.ord AS seq, e.value->>'entity' AS entity, e.value->>'entity_id' AS entity_id, e.value->>'change' AS change,
        CASE WHEN e.value->>'entity' = 'sale' THEN jsonb_set(e.value->'payload', '{sale,sequence}', to_jsonb(t.sale_seq)) ELSE e.value->'payload' END AS payload
      FROM t, jsonb_array_elements($3::jsonb) WITH ORDINALITY AS e(value, ord)),
    incoming AS (INSERT INTO sync_event(terminal_id, seq, branch_id, entity, entity_id, change, enqueued_at, payload)
      SELECT $1, seq, branch_id, entity, entity_id, change, now(), payload FROM ev
      RETURNING terminal_id, seq, branch_id, entity, entity_id, enqueued_at, payload)
    INSERT INTO entity_state(branch_id, entity, entity_id, terminal_id, seq, enqueued_at, deleted, payload)
    SELECT branch_id, entity, entity_id, terminal_id, seq, enqueued_at, false, payload FROM incoming
    ON CONFLICT (branch_id, entity, entity_id) DO UPDATE SET terminal_id = EXCLUDED.terminal_id, seq = EXCLUDED.seq,
      enqueued_at = EXCLUDED.enqueued_at, deleted = false, payload = EXCLUDED.payload, received_at = now()
    RETURNING entity, entity_id, payload`,
    [tillId, events.length, JSON.stringify(events), session, operation?.id ?? null, operation?.entityId ?? null, operation?.kind ?? 'sale']);
  await q('UPDATE terminal SET last_seen_at = now() WHERE id = $1', [tillId]);
  return rows;
}

const audit = (user: WebUser, tillId: string, action: string, entityId: string, at: string, details: unknown): Event => {
  const auditId = randomUUID();
  return { entity: 'audit_entry', entity_id: auditId, change: 'insert', payload: { id: auditId, actor_id: user.id, terminal_id: tillId, action, entity_id: entityId, occurred_utc: at, details_json: JSON.stringify(details) } };
};

// ---------- Turno ----------

async function sessionPayload(q: Query, branchId: string, sessionId: string): Promise<Row | null> {
  const [row] = await q(`SELECT payload FROM entity_state WHERE branch_id = $1 AND entity = 'cash_session' AND entity_id = $2 AND NOT deleted`, [branchId, sessionId]);
  return (row?.payload as Row | undefined) ?? null;
}

/** Abre el turno de la caja web con su fondo (permiso cash.open). */
export async function openSession(q: Query, user: WebUser, body: Row | null) {
  requirePermission(user, 'cash.open');
  const t = await till(q, user, body?.tillId);
  if (t.openSessionId) throw conflict('Esta caja ya tiene un turno abierto.');
  const opening = cents(body?.openingCents, 'Fondo');
  const sessionId = randomUUID();
  const at = new Date().toISOString();
  const payload = { id: sessionId, terminal_id: t.id, opened_by: user.id, opened_utc: at, opening_cents: opening, state: 'OPEN',
    closed_utc: null, closed_by: null, expected_cents: null, counted_cents: null, difference_cents: null };
  const written = await write(q, t.id, 'open', sessionId, [
    { entity: 'cash_session', entity_id: sessionId, change: 'insert', payload },
    audit(user, t.id, 'cash.open', sessionId, at, { openingCents: opening, state: 'OPEN' }),
  ]);
  if (!written.length) throw conflict('Esta caja ya tiene un turno abierto.');
  return { sessionId };
}

/** Efectivo esperado: fondo + ventas en efectivo (aplicado, sin cambio) − devoluciones de ventas en efectivo + entradas − gastos y retiros. */
export async function expectedCash(q: Query, branchId: string, sessionId: string) {
  const [row] = await q(`WITH
    sales AS (SELECT entity_id, COALESCE(payload->'payment'->>'method', 'CASH') AS method,
        COALESCE((payload->'payment'->>'applied_cents')::bigint, (payload->'sale'->>'total_cents')::bigint) AS applied
      FROM entity_state WHERE branch_id = $1 AND entity = 'sale' AND NOT deleted AND payload->'sale'->>'cash_session_id' = $2)
    SELECT (SELECT COALESCE(sum(applied), 0) FROM sales WHERE method = 'CASH') AS cash_sales,
      (SELECT count(*) FROM sales) AS tickets,
      (SELECT COALESCE(sum((r.payload->'refund'->>'total_cents')::bigint), 0) FROM entity_state r
        JOIN entity_state x ON x.branch_id = r.branch_id AND x.entity = 'sale' AND NOT x.deleted AND x.entity_id = r.payload->'refund'->>'sale_id'
        WHERE r.branch_id = $1 AND r.entity = 'sale_refund' AND NOT r.deleted AND r.payload->'refund'->>'cash_session_id' = $2
          AND COALESCE(x.payload->'payment'->>'method', 'CASH') = 'CASH') AS cash_refunds,
      (SELECT COALESCE(sum(CASE WHEN payload->>'kind' = 'DEPOSIT' THEN 1 ELSE -1 END * (payload->>'amount_cents')::bigint), 0)
        FROM entity_state WHERE branch_id = $1 AND entity = 'cash_movement' AND NOT deleted AND payload->>'cash_session_id' = $2) AS movements`, [branchId, sessionId]);
  const session = await sessionPayload(q, branchId, sessionId);
  const opening = n(session?.opening_cents);
  const cashSales = n(row?.cash_sales), cashRefunds = n(row?.cash_refunds), movements = n(row?.movements);
  return { opening, cashSales, cashRefunds, movements, tickets: n(row?.tickets), expected: opening + cashSales - cashRefunds + movements };
}

async function closer(q: Query, user: WebUser, body: Row | null) {
  requirePermission(user, 'cash.close');
  const t = await till(q, user, body?.tillId);
  const sessionId = id(body?.sessionId, 'Turno');
  if (t.openSessionId !== sessionId) throw conflict('Este turno ya está cerrado.');
  const session = await sessionPayload(q, t.branchId, sessionId);
  if (!session) throw conflict('No se encontró el turno.');
  if (s(session.opened_by) !== user.id && !user.permissions.has('cash.close.other'))
    throw new ProtocolError('Esta caja pertenece a otro usuario. Necesitas permiso para cerrar cajas ajenas.', 403);
  return { t, sessionId, session };
}

/** Conteo ciego: se captura sin ver el esperado; después ya no se vende ni se cambia el conteo. */
export async function countSession(q: Query, user: WebUser, body: Row | null) {
  const { t, sessionId } = await closer(q, user, body);
  if (t.counted) throw conflict('El conteo ya fue confirmado; puedes retomar el resultado, pero no modificarlo.');
  const counted = cents(body?.countedCents, 'Conteo');
  const at = new Date().toISOString();
  const written = await write(q, t.id, 'count', sessionId, [
    { entity: 'cash_count', entity_id: sessionId, change: 'insert', payload: { session_id: sessionId, counted_by: user.id, counted_utc: at, counted_cents: counted } },
    audit(user, t.id, 'cash.count', sessionId, at, { countedCents: counted }),
  ]);
  if (!written.length) throw conflict('El conteo ya fue confirmado; puedes retomar el resultado, pero no modificarlo.');
  const result = await countResult(q, t.branchId, sessionId);
  if (!result) throw new Error('El conteo no quedó guardado.');
  return result;
}

export async function countResult(q: Query, branchId: string, sessionId: string) {
  const [count] = await q(`SELECT payload FROM entity_state WHERE branch_id = $1 AND entity = 'cash_count' AND entity_id = $2`, [branchId, sessionId]);
  if (!count) return null;
  const counted = n((count.payload as Row).counted_cents);
  const e = await expectedCash(q, branchId, sessionId);
  return { ...e, counted, difference: counted - e.expected };
}

/**
 * Cierra el turno ya contado con el esperado de ese momento (después del conteo no entran ventas).
 * Con faltante o sobrante pide una nota de 1 a 500 caracteres, como la caja de escritorio (migración 026).
 */
export async function closeSession(q: Query, user: WebUser, body: Row | null) {
  const { t, sessionId, session } = await closer(q, user, body);
  if (!t.counted) throw conflict('Confirma primero el conteo ciego.');
  const result = await countResult(q, t.branchId, sessionId);
  if (!result) throw conflict('Confirma primero el conteo ciego.');
  const raw = typeof body?.discrepancyNote === 'string' ? body.discrepancyNote.trim() : '';
  if (raw.length > 500) throw new ProtocolError('La nota admite hasta 500 caracteres.');
  if (result.difference !== 0 && !raw) throw new ProtocolError('Escribe una nota para explicar la diferencia antes de cerrar la caja.');
  const note = raw || null;
  const at = new Date().toISOString();
  const [count] = await q(`SELECT payload FROM entity_state WHERE branch_id = $1 AND entity = 'cash_count' AND entity_id = $2`, [t.branchId, sessionId]);
  const c = count.payload as Row;
  const closed = { ...session, state: 'CLOSED', closed_utc: at, closed_by: user.id, expected_cents: result.expected, counted_cents: result.counted, difference_cents: result.difference, discrepancy_note: note };
  const written = await write(q, t.id, 'close', sessionId, [
    { entity: 'cash_session', entity_id: sessionId, change: 'update', payload: closed },
    audit(user, t.id, 'cash.close', sessionId, at, { openedBy: session.opened_by, closedBy: user.id, closedByOtherUser: s(session.opened_by) !== user.id,
      countedBy: c.counted_by, countedAt: c.counted_utc, openingCents: result.opening, expectedCents: result.expected, countedCents: result.counted, differenceCents: result.difference, discrepancyNote: note }),
  ]);
  if (!written.length) throw conflict('Este turno ya está cerrado.');
  return result;
}

// ---------- Cobro ----------

interface Line { productId: string; quantity: number }

function parseLines(value: unknown): Line[] {
  if (!Array.isArray(value) || value.length === 0) throw new ProtocolError('El carrito está vacío.');
  if (value.length > MAX_LINES) throw new ProtocolError(`Máximo ${MAX_LINES} panes distintos por venta.`);
  const lines = value.map(l => {
    const line = l as Row;
    const quantity = line?.quantity;
    if (typeof quantity !== 'number' || !Number.isInteger(quantity) || quantity < 1 || quantity > MAX_QUANTITY) throw new ProtocolError(`Cantidad de 1 a ${MAX_QUANTITY} piezas.`);
    return { productId: id(line?.productId, 'Producto'), quantity };
  });
  if (new Set(lines.map(l => l.productId)).size !== lines.length) throw new ProtocolError('La venta debe tener partidas únicas.');
  return lines;
}

function parsePayment(body: Row) {
  const method = body.method as Method;
  if (!METHODS.includes(method)) throw new ProtocolError('Forma de pago inválida.');
  const raw = typeof body.reference === 'string' ? body.reference.trim() : body.reference === null || body.reference === undefined ? '' : null;
  if (raw === null) throw new ProtocolError('Referencia inválida.');
  const reference = raw === '' ? null : raw;
  if (method === 'CASH' && reference !== null) throw new ProtocolError('El pago en efectivo no lleva referencia.');
  if (reference !== null && reference.length > 40) throw new ProtocolError('La referencia admite máximo 40 caracteres.');
  return { method, reference, received: method === 'CASH' ? cents(body.receivedCents, 'Recibido') : null };
}

/** Lo que muestra e imprime el ticket. */
async function receipt(q: Query, branchId: string, saleId: string) {
  const [row] = await q(`SELECT e.payload, t.name AS till, b.name AS branch, u.payload->>'display_name' AS cashier
    FROM entity_state e JOIN terminal t ON t.id = e.terminal_id JOIN branch b ON b.id = e.branch_id
    LEFT JOIN entity_state u ON u.branch_id = e.branch_id AND u.entity = 'app_user' AND u.entity_id = e.payload->'sale'->>'user_id'
    WHERE e.branch_id = $1 AND e.entity = 'sale' AND e.entity_id = $2`, [branchId, saleId]);
  const p = row.payload as { sale: Row; items: Row[]; payment: Row };
  return {
    id: saleId, folio: displayFolio(s(row.till), n(p.sale.sequence)), at: s(p.sale.confirmed_utc), till: s(row.till), branch: s(row.branch), cashier: s(row.cashier),
    items: p.items.map(i => ({ productId: s(i.product_id), name: s(i.product_name), quantity: n(i.quantity), unitPrice: n(i.unit_price_cents), total: n(i.line_total_cents) })),
    total: n(p.sale.total_cents), method: s(p.payment.method), received: n(p.payment.received_cents), change: n(p.payment.change_cents),
    reference: p.payment.reference === null || p.payment.reference === undefined ? null : s(p.payment.reference),
  };
}

/** Reintento del mismo cobro: devuelve la venta ya hecha si es el mismo contenido; si no, lo rechaza. */
async function previous(q: Query, user: WebUser, t: Till, operationId: string, sessionId: string, lines: Line[], pay: ReturnType<typeof parsePayment>) {
  const [op] = await q('SELECT terminal_id, entity_id FROM web_operation WHERE operation_id = $1', [operationId]);
  if (!op) return null;
  const [row] = await q(`SELECT payload FROM entity_state WHERE entity = 'sale' AND entity_id = $1 AND terminal_id = $2`, [s(op.entity_id), t.id]);
  const p = row?.payload as { sale: Row; items: Row[]; payment: Row } | undefined;
  const same = p && s(op.terminal_id) === t.id && s(p.sale.user_id) === user.id && s(p.sale.cash_session_id) === sessionId
    && p.items.length === lines.length && lines.every((l, i) => s(p.items[i].product_id) === l.productId && n(p.items[i].quantity) === l.quantity)
    && s(p.payment.method) === pay.method && (p.payment.reference ?? null) === pay.reference && (pay.received === null || n(p.payment.received_cents) === pay.received);
  if (!same) throw conflict('El identificador de operación ya corresponde a otro contenido.');
  return receipt(q, t.branchId, s(op.entity_id));
}

/** Cobra con el precio y el nombre vigentes del catálogo (no los del navegador); sólo productos y categorías activos. */
export async function checkout(q: Query, user: WebUser, body: Row | null) {
  for (const p of ['catalog.view', 'pos.use', 'sales.complete']) requirePermission(user, p);
  if (!body) throw new ProtocolError('Cobro inválido');
  const t = await till(q, user, body.tillId);
  const operationId = id(body.operationId, 'Operación');
  const sessionId = id(body.sessionId, 'Turno');
  const lines = parseLines(body.lines);
  const pay = parsePayment(body);

  const done = await previous(q, user, t, operationId, sessionId, lines, pay);
  if (done) return done;
  if (t.openSessionId !== sessionId || t.counted) throw conflict(CLOSED);

  const rows = await q(`SELECT p.entity_id AS id, p.payload->'product' AS product, (c.payload->>'active')::int AS category_active
    FROM entity_state p LEFT JOIN entity_state c ON c.branch_id = p.branch_id AND c.entity = 'category' AND NOT c.deleted AND c.entity_id = p.payload->'product'->>'category_id'
    WHERE p.branch_id = $1 AND p.entity = 'product' AND NOT p.deleted AND p.entity_id = ANY($2::text[])`, [t.branchId, lines.map(l => l.productId)]);
  const catalog = new Map(rows.map(r => [s(r.id).toLowerCase(), r]));
  const saleId = randomUUID();
  let total = 0;
  const items = lines.map((l, i) => {
    const r = catalog.get(l.productId);
    const product = r?.product as Row | undefined;
    if (!product || n(product.active) !== 1 || n(r!.category_active) !== 1) throw conflict('Un producto o su categoría ya no están activos. El carrito no cambió.');
    const price = n(product.price_cents);
    if (!Number.isSafeInteger(price) || price < 0) throw conflict('Un producto tiene un precio inválido.');
    const line = price * l.quantity;
    total += line;
    return { sale_id: saleId, position: i + 1, product_id: s(product.id), product_name: s(product.name), unit_price_cents: price, quantity: l.quantity, line_total_cents: line };
  });
  if (total > MAX_CENTS) throw new ProtocolError('El total excede el máximo permitido.');
  // El ticket que vio el cajero debe ser el que se cobra: si un precio cambió mientras tanto, se avisa en vez de cobrar otro total.
  if (body.expectedTotal !== undefined && body.expectedTotal !== total) throw conflict('Cambió el precio de un producto. Revisa el ticket antes de cobrar.');
  const received = pay.method === 'CASH' ? pay.received! : total;
  if (received < total) throw new ProtocolError('El efectivo recibido no alcanza para el total.');
  const at = new Date().toISOString();
  const paymentId = randomUUID();
  const payload = {
    sale: { id: saleId, operation_id: operationId, user_id: user.id, terminal_id: t.id, cash_session_id: sessionId, sequence: 0, confirmed_utc: at,
      subtotal_cents: total, total_cents: total, inventory_evaluation_contract_version: null, inventory_evaluation_id: null },
    items,
    payment: { id: paymentId, sale_id: saleId, method: pay.method, applied_cents: total, received_cents: received, change_cents: received - total, reference: pay.reference },
  };
  let written: Row[];
  try {
    written = await write(q, t.id, 'sale', sessionId, [
      { entity: 'sale', entity_id: saleId, change: 'insert', payload },
      audit(user, t.id, 'sale.completed', saleId, at, { operationId, cashSessionId: sessionId, paymentId, totalCents: total, receivedCents: received, changeCents: received - total }),
    ], { id: operationId, entityId: saleId });
  } catch (error) {
    // Dos envíos simultáneos del mismo cobro: el segundo choca con el identificador y devuelve la venta del primero.
    const again = await previous(q, user, t, operationId, sessionId, lines, pay);
    if (again) return again;
    throw error;
  }
  if (!written.length) throw conflict(CLOSED);
  return receipt(q, t.branchId, saleId);
}

// ---------- Tickets, reimpresión y devoluciones ----------

const peso = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });
const money = (cents: number) => peso.format(cents / 100);
type SalePayload = { sale: Row; items: Row[]; payment: Row };

async function findSale(q: Query, branchId: string, saleId: string) {
  const [row] = await q(`SELECT e.payload, e.terminal_id, t.name, t.kind FROM entity_state e JOIN terminal t ON t.id = e.terminal_id
    WHERE e.branch_id = $1 AND e.entity = 'sale' AND e.entity_id = $2 AND NOT e.deleted`, [branchId, saleId]);
  return row ? { payload: row.payload as SalePayload, terminalId: s(row.terminal_id), web: row.kind === 'web' } : null;
}
async function refundsOf(q: Query, branchId: string, saleId: string) {
  const rows = await q(`SELECT r.payload, u.payload->>'display_name' AS by FROM entity_state r
    LEFT JOIN entity_state u ON u.branch_id = r.branch_id AND u.entity = 'app_user' AND u.entity_id = r.payload->'refund'->>'user_id'
    WHERE r.branch_id = $1 AND r.entity = 'sale_refund' AND NOT r.deleted AND r.payload->'refund'->>'sale_id' = $2
    ORDER BY r.payload->'refund'->>'refunded_utc'`, [branchId, saleId]);
  return rows.map(r => ({ ...(r.payload as { refund: Row; items: Row[] }), by: s(r.by) }));
}

/** Tickets del turno abierto de la caja (o los últimos de la caja); con folio busca cualquier venta de la sucursal. */
export async function tickets(q: Query, user: WebUser, tillValue: unknown, search: string) {
  requirePermission(user, 'pos.use');
  const t = await till(q, user, tillValue);
  const folio = /^(?:[Cc](\d{1,4})\s*-\s*)?(\d{1,12})$/.exec(search.trim());
  let rows: Row[];
  if (folio) {
    const code = folio[1] ? 'C' + folio[1].padStart(2, '0') : terminalCode(t.name);
    const terminals = (await q('SELECT id, name FROM terminal WHERE branch_id = $1', [t.branchId])).filter(x => terminalCode(s(x.name)) === code).map(x => s(x.id));
    rows = await q(`SELECT e.entity_id, e.payload, t.name FROM entity_state e JOIN terminal t ON t.id = e.terminal_id
      WHERE e.branch_id = $1 AND e.entity = 'sale' AND NOT e.deleted AND e.terminal_id = ANY($2::uuid[]) AND (e.payload->'sale'->>'sequence')::bigint = $3`,
      [t.branchId, terminals, Number(folio[2])]);
  } else if (t.openSessionId) {
    rows = await q(`SELECT e.entity_id, e.payload, t.name FROM entity_state e JOIN terminal t ON t.id = e.terminal_id
      WHERE e.branch_id = $1 AND e.entity = 'sale' AND NOT e.deleted AND e.payload->'sale'->>'cash_session_id' = $2
      ORDER BY (e.payload->'sale'->>'sequence')::bigint DESC LIMIT 100`, [t.branchId, t.openSessionId]);
  } else {
    rows = await q(`SELECT e.entity_id, e.payload, t.name FROM entity_state e JOIN terminal t ON t.id = e.terminal_id
      WHERE e.branch_id = $1 AND e.entity = 'sale' AND NOT e.deleted AND e.terminal_id = $2
      ORDER BY (e.payload->'sale'->>'sequence')::bigint DESC LIMIT 30`, [t.branchId, t.id]);
  }
  const refunded = rows.length ? await q(`SELECT payload->'refund'->>'sale_id' AS sale_id, sum((payload->'refund'->>'total_cents')::bigint) AS total,
      bool_or(payload->'refund'->>'kind' = 'CANCELLATION') AS cancelled
    FROM entity_state WHERE branch_id = $1 AND entity = 'sale_refund' AND NOT deleted AND payload->'refund'->>'sale_id' = ANY($2::text[]) GROUP BY 1`,
    [t.branchId, rows.map(r => s(r.entity_id))]) : [];
  const back = new Map(refunded.map(r => [s(r.sale_id), r]));
  return rows.map(r => {
    const p = r.payload as SalePayload;
    const b = back.get(s(r.entity_id));
    return { id: s(r.entity_id), folio: displayFolio(s(r.name), n(p.sale.sequence)), at: s(p.sale.confirmed_utc), total: n(p.sale.total_cents),
      method: s(p.payment?.method ?? 'CASH'), pieces: p.items.reduce((a, i) => a + n(i.quantity), 0), refunded: n(b?.total), cancelled: b?.cancelled === true };
  });
}

/** Ticket con lo que se puede devolver desde esta caja (mismas reglas que RefundService de la caja de escritorio). */
async function refundable(q: Query, t: Till, saleId: string) {
  const sale = await findSale(q, t.branchId, saleId);
  if (!sale) throw new ProtocolError('No se encontró la venta.', 404);
  const refunds = await refundsOf(q, t.branchId, saleId);
  const done = new Map<number, number>();
  for (const r of refunds) for (const i of r.items) done.set(n(i.position), (done.get(n(i.position)) ?? 0) + n(i.quantity));
  const lines = sale.payload.items.map(i => ({ position: n(i.position), name: s(i.product_name), unitPrice: n(i.unit_price_cents), sold: n(i.quantity), refunded: done.get(n(i.position)) ?? 0 }))
    .map(l => ({ ...l, remaining: l.sold - l.refunded }));
  let blocker = '';
  if (!sale.web) blocker = 'Esta venta es de la caja de escritorio: devuélvela en esa caja.';
  else if (!t.openSessionId) blocker = 'Abre el turno de esta caja para registrar devoluciones.';
  else if (t.counted) blocker = 'La caja ya tiene conteo confirmado; ciérrala y abre un turno nuevo para registrar devoluciones.';
  else if (lines.every(l => l.remaining === 0)) blocker = 'Esta venta ya fue cancelada o devuelta por completo.';
  const canCancel = !blocker && refunds.length === 0 && t.openSessionId === s(sale.payload.sale.cash_session_id);
  return { sale, refunds, lines, blocker, canCancel, canReturn: !blocker };
}

export async function ticket(q: Query, user: WebUser, tillValue: unknown, saleValue: unknown) {
  requirePermission(user, 'pos.use');
  const t = await till(q, user, tillValue);
  const r = await refundable(q, t, id(saleValue, 'Venta'));
  return {
    receipt: await receipt(q, t.branchId, s(r.sale.payload.sale.id)),
    refunds: r.refunds.map(x => ({ kind: s(x.refund.kind), at: s(x.refund.refunded_utc), by: x.by, reason: s(x.refund.reason), total: n(x.refund.total_cents) })),
    lines: r.lines, blocker: r.blocker, canCancel: r.canCancel, canReturn: r.canReturn,
  };
}

const instruction = (method: string, total: number) => method === 'CASH' ? `Entrega ${money(total)} en efectivo al cliente.`
  : method === 'CARD' ? `Reembolsa ${money(total)} a la tarjeta del cliente desde la terminal bancaria. No entregues efectivo de la caja.`
  : `Reembolsa ${money(total)} al cliente por transferencia. No entregues efectivo de la caja.`;

/** Cancelación (venta completa, del mismo turno, sin devoluciones previas) o devolución de piezas. La venta original no cambia. */
export async function refund(q: Query, user: WebUser, body: Row | null) {
  requirePermission(user, 'sales.refund');
  if (!body) throw new ProtocolError('Devolución inválida');
  const t = await till(q, user, body.tillId);
  const operationId = id(body.operationId, 'Operación');
  const sessionId = id(body.sessionId, 'Turno');
  const saleId = id(body.saleId, 'Venta');
  const kind = body.kind === 'CANCELLATION' ? 'CANCELLATION' : body.kind === 'RETURN' ? 'RETURN' : null;
  if (!kind) throw new ProtocolError('Tipo de devolución inválido.');
  const reason = typeof body.reason === 'string' ? body.reason.trim() : '';
  if (reason.length < 3 || reason.length > 200) throw new ProtocolError('Escribe el motivo (de 3 a 200 caracteres).');

  const [op] = await q(`SELECT o.entity_id, e.payload FROM web_operation o LEFT JOIN entity_state e ON e.entity = 'sale_refund' AND e.entity_id = o.entity_id
    WHERE o.operation_id = $1 AND o.kind = 'refund'`, [operationId]);
  if (op) {
    const p = op.payload as { refund: Row } | null;
    if (!p || s(p.refund.sale_id) !== saleId || s(p.refund.kind) !== kind || s(p.refund.user_id) !== user.id) throw conflict('El identificador de operación ya corresponde a otro contenido.');
    const sale = await findSale(q, t.branchId, saleId);
    const method = s(sale?.payload.payment?.method ?? 'CASH');
    return { refundId: s(op.entity_id), kind, total: n(p.refund.total_cents), method, instruction: instruction(method, n(p.refund.total_cents)) };
  }
  if (t.openSessionId !== sessionId) throw conflict('Abre el turno de esta caja para registrar devoluciones.');
  const options = await refundable(q, t, saleId);
  if (options.blocker) throw conflict(options.blocker);
  if (kind === 'CANCELLATION' && !options.canCancel)
    throw conflict('Sólo se puede cancelar una venta completa, sin devoluciones previas y en el mismo turno de caja. Usa «Devolver productos».');

  let lines: { position: number; quantity: number }[];
  if (kind === 'CANCELLATION') lines = options.lines.map(l => ({ position: l.position, quantity: l.sold }));
  else {
    if (!Array.isArray(body.lines)) throw new ProtocolError('Indica al menos una pieza a devolver.');
    const wanted = new Map<number, number>();
    for (const raw of body.lines as Row[]) {
      const position = raw?.position, quantity = raw?.quantity;
      if (typeof position !== 'number' || !Number.isInteger(position) || typeof quantity !== 'number' || !Number.isInteger(quantity)) throw new ProtocolError('Partida inválida.');
      if (quantity < 0) throw new ProtocolError('La cantidad a devolver no puede ser negativa.');
      wanted.set(position, (wanted.get(position) ?? 0) + quantity);
    }
    lines = [];
    for (const [position, quantity] of [...wanted.entries()].sort((a, b) => a[0] - b[0])) {
      if (quantity === 0) continue;
      const line = options.lines.find(l => l.position === position);
      if (!line) throw new ProtocolError('La partida no pertenece a la venta.');
      if (quantity > line.remaining) throw new ProtocolError(`Sólo quedan ${line.remaining} pieza(s) de ${line.name} por devolver.`);
      lines.push({ position, quantity });
    }
    if (!lines.length) throw new ProtocolError('Indica al menos una pieza a devolver.');
  }
  const refundId = randomUUID();
  const items = lines.map(l => {
    const line = options.lines.find(x => x.position === l.position)!;
    return { refund_id: refundId, sale_id: saleId, position: l.position, quantity: l.quantity, line_total_cents: line.unitPrice * l.quantity };
  });
  const total = items.reduce((a, i) => a + i.line_total_cents, 0);
  if (total <= 0) throw conflict('La devolución debe tener un importe mayor que cero.');
  const method = s(options.sale.payload.payment?.method ?? 'CASH');
  if (method === 'CASH' && total > (await expectedCash(q, t.branchId, sessionId)).expected)
    throw conflict(`No hay suficiente efectivo esperado en caja para devolver ${money(total)}.`);
  const at = new Date().toISOString();
  const written = await write(q, t.id, 'drawer', sessionId, [
    { entity: 'sale_refund', entity_id: refundId, change: 'insert', payload: {
      refund: { id: refundId, operation_id: operationId, sale_id: saleId, kind, reason, user_id: user.id, terminal_id: t.id, cash_session_id: sessionId, refunded_utc: at, total_cents: total },
      items } },
    audit(user, t.id, kind === 'CANCELLATION' ? 'sale.cancelled' : 'sale.returned', refundId, at,
      { saleId, cashSessionId: sessionId, totalCents: total, items: items.map(i => ({ position: i.position, quantity: i.quantity, lineTotalCents: i.line_total_cents })) }),
  ], { id: operationId, entityId: refundId, kind: 'refund' });
  if (!written.length) throw conflict('La caja ya tiene conteo confirmado; ciérrala y abre un turno nuevo para registrar devoluciones.');
  return { refundId, kind, total, method, instruction: instruction(method, total) };
}

// ---------- Gastos, retiros y entradas ----------

const MOVEMENT_KINDS = ['EXPENSE', 'WITHDRAWAL', 'DEPOSIT'] as const;
const CATEGORIES = ['SUPPLIES', 'GAS', 'CLEANING', 'TRANSPORT', 'MAINTENANCE', 'OTHER'] as const;

async function movements(q: Query, branchId: string, sessionId: string) {
  const rows = await q(`SELECT m.payload, u.payload->>'display_name' AS by FROM entity_state m
    LEFT JOIN entity_state u ON u.branch_id = m.branch_id AND u.entity = 'app_user' AND u.entity_id = m.payload->>'user_id'
    WHERE m.branch_id = $1 AND m.entity = 'cash_movement' AND NOT m.deleted AND m.payload->>'cash_session_id' = $2
    ORDER BY m.payload->>'occurred_utc' DESC`, [branchId, sessionId]);
  return rows.map(r => { const p = r.payload as Row; return { id: s(p.id), kind: s(p.kind), category: p.category === null ? null : s(p.category), amount: n(p.amount_cents), note: s(p.note), at: s(p.occurred_utc), by: s(r.by) }; });
}

/** Gasto, retiro (encargado y administrador) o entrada en el turno abierto. Inmutable: un error se corrige con el movimiento contrario. */
export async function movement(q: Query, user: WebUser, body: Row | null) {
  requirePermission(user, 'cash.movement');
  if (!body) throw new ProtocolError('Movimiento inválido');
  const t = await till(q, user, body.tillId);
  const operationId = id(body.operationId, 'Operación');
  const sessionId = id(body.sessionId, 'Turno');
  const kind = body.kind as typeof MOVEMENT_KINDS[number];
  if (!MOVEMENT_KINDS.includes(kind)) throw new ProtocolError('Movimiento inválido.');
  if (kind === 'WITHDRAWAL' && !user.permissions.has('cash.withdrawal')) throw new ProtocolError('Sólo el encargado o el administrador registran retiros.', 403);
  const amount = cents(body.amountCents, 'Importe', 1);
  const category = body.category === null || body.category === undefined ? null : body.category as typeof CATEGORIES[number];
  if (kind === 'EXPENSE' && (!category || !CATEGORIES.includes(category))) throw new ProtocolError('Elige la categoría del gasto.');
  if (kind !== 'EXPENSE' && category !== null) throw new ProtocolError('Sólo los gastos llevan categoría.');
  const note = typeof body.note === 'string' ? body.note.trim() : '';
  if (note.length > 200) throw new ProtocolError('La nota admite hasta 200 caracteres.');
  if (/[\u0000-\u001f\u007f]/.test(note)) throw new ProtocolError('La nota no admite saltos de línea ni caracteres de control.');
  if (category === 'OTHER' && note.length < 3) throw new ProtocolError('Explica el gasto en la nota cuando la categoría es «Otro».');

  const [op] = await q(`SELECT o.entity_id, e.payload FROM web_operation o LEFT JOIN entity_state e ON e.entity = 'cash_movement' AND e.entity_id = o.entity_id
    WHERE o.operation_id = $1 AND o.kind = 'movement'`, [operationId]);
  if (op) {
    const p = op.payload as Row | null;
    if (!p || s(p.kind) !== kind || n(p.amount_cents) !== amount || s(p.user_id) !== user.id) throw conflict('El identificador de operación ya corresponde a otro contenido.');
    return { id: s(p.id), kind, category: p.category === null ? null : s(p.category), amount, note: s(p.note), at: s(p.occurred_utc), by: user.name };
  }
  if (t.openSessionId !== sessionId) throw conflict('Abre la caja antes de registrar movimientos de efectivo.');
  if (t.counted) throw conflict('La caja ya tiene su conteo de cierre; los movimientos se registran antes de contar.');
  if (kind !== 'DEPOSIT' && (await expectedCash(q, t.branchId, sessionId)).expected < amount)
    throw conflict(`No hay suficiente efectivo esperado en caja para sacar ${money(amount)}.`);
  const movementId = randomUUID();
  const at = new Date().toISOString();
  const written = await write(q, t.id, 'drawer', sessionId, [
    { entity: 'cash_movement', entity_id: movementId, change: 'insert', payload: { id: movementId, operation_id: operationId, cash_session_id: sessionId, terminal_id: t.id,
      user_id: user.id, kind, category, amount_cents: amount, note, occurred_utc: at } },
    audit(user, t.id, `cash.${kind.toLowerCase()}`, movementId, at, { operationId, cashSessionId: sessionId, kind, category, amountCents: amount, note }),
  ], { id: operationId, entityId: movementId, kind: 'movement' });
  if (!written.length) throw conflict('La caja ya tiene su conteo de cierre; los movimientos se registran antes de contar.');
  return { id: movementId, kind, category, amount, note, at, by: user.name };
}

// ---------- Pantalla ----------

/** Todo lo que necesita la pantalla de venta: cajas web de la sucursal, la elegida con su turno, y el catálogo vendible con fotos. */
export async function posState(q: Query, user: WebUser, branch: string, tillValue: string | null) {
  const list = await tills(q, branch);
  const chosen = tillValue && isUuid(tillValue) ? list.find(t => t.id === tillValue.toLowerCase()) ?? null : null;
  let session = null;
  if (chosen) {
    const [row] = await q(`SELECT open_session_id FROM web_till WHERE terminal_id = $1`, [chosen.id]);
    const sessionId = row?.open_session_id ? s(row.open_session_id) : null;
    const p = sessionId ? await sessionPayload(q, branch, sessionId) : null;
    if (sessionId && p) {
      const [opener] = await q(`SELECT payload->>'display_name' AS name FROM entity_state WHERE branch_id = $1 AND entity = 'app_user' AND entity_id = $2`, [branch, s(p.opened_by)]);
      const e = await expectedCash(q, branch, sessionId);
      // Conteo ciego: el esperado sólo se muestra después de contar.
      session = { id: sessionId, openedAt: s(p.opened_utc), openedBy: s(opener?.name), mine: s(p.opened_by) === user.id, opening: n(p.opening_cents), tickets: e.tickets,
        count: chosen.counted ? await countResult(q, branch, sessionId) : null, movements: await movements(q, branch, sessionId) };
    }
  }
  const [categories, products, photos] = await Promise.all([
    q(`SELECT payload FROM entity_state WHERE branch_id = $1 AND entity = 'category' AND NOT deleted AND (payload->>'active')::int = 1`, [branch]),
    q(`SELECT payload->'product' AS p FROM entity_state WHERE branch_id = $1 AND entity = 'product' AND NOT deleted AND (payload->'product'->>'active')::int = 1`, [branch]),
    q(`SELECT entity_id, payload->'asset'->>'thumbnail_sha256' AS sha FROM entity_state WHERE branch_id = $1 AND entity = 'product_photo' AND NOT deleted AND payload->>'thumbnail' IS NOT NULL`, [branch]),
  ]);
  const activeCategories = categories.map(c => c.payload as Row).sort((a, b) => n(a.sort_order) - n(b.sort_order) || s(a.name).localeCompare(s(b.name)));
  const activeIds = new Set(activeCategories.map(c => s(c.id)));
  const photo = new Map(photos.map(p => [s(p.entity_id), s(p.sha)]));
  return {
    branchId: branch, tills: list, till: chosen, session,
    categories: activeCategories.map(c => ({ id: s(c.id), name: s(c.name) })),
    products: products.map(x => x.p as Row).filter(p => activeIds.has(s(p.category_id)))
      .map(p => ({ id: s(p.id), name: s(p.name), sku: p.sku === null ? null : s(p.sku), categoryId: s(p.category_id), price: n(p.price_cents), photo: photo.get(s(p.id)) ?? null }))
      .sort((a, b) => a.name.localeCompare(b.name)),
  };
}
