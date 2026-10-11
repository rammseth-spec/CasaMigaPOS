// Resumen de la versión web (docs/WEB.md): sólo lectura sobre entity_state, el último contenido de cada registro.
import { ProtocolError } from './protocol.js';

export type Row = Record<string, unknown>;
/** Ejecuta SQL con parámetros posicionales ($1, $2…). En producción es Neon; en pruebas, PGlite. */
export type Query = (text: string, params: unknown[]) => Promise<Row[]>;

export const MAX_DAYS = 92;
export const DEFAULT_TIME_ZONE = 'America/Mexico_City';

// ---------- Filtros ----------

export interface Range { from: string; to: string; branchId: string | null; timeZone: string }

const DAY = /^\d{4}-\d{2}-\d{2}$/;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function validDay(value: string): boolean {
  if (!DAY.test(value)) return false;
  const date = new Date(value + 'T00:00:00Z');
  return !Number.isNaN(date.getTime()) && date.toISOString().startsWith(value);
}

export function parseRange(url: URL, timeZone = DEFAULT_TIME_ZONE): Range {
  const from = url.searchParams.get('from') ?? '';
  const to = url.searchParams.get('to') ?? '';
  if (!validDay(from) || !validDay(to)) throw new ProtocolError('Fechas inválidas (AAAA-MM-DD)');
  const days = (Date.parse(to) - Date.parse(from)) / 86_400_000 + 1;
  if (days < 1) throw new ProtocolError('La fecha final es anterior a la inicial');
  if (days > MAX_DAYS) throw new ProtocolError(`El periodo admite hasta ${MAX_DAYS} días`);
  const branch = url.searchParams.get('branch');
  if (branch && !UUID.test(branch)) throw new ProtocolError('Sucursal inválida');
  return { from, to, branchId: branch ? branch.toLowerCase() : null, timeZone };
}

// ---------- Consultas ----------

export const SALES = `SELECT branch_id, terminal_id, entity_id AS sale_id, payload,
    (cm_utc(payload->'sale'->>'confirmed_utc') AT TIME ZONE $1)::date AS day,
    (payload->'sale'->>'total_cents')::bigint AS total,
    COALESCE(payload->'payment'->>'method', 'CASH') AS method,
    (payload->'sale'->>'cash_session_id') AS session_id,
    COALESCE((payload->'payment'->>'applied_cents')::bigint, (payload->'sale'->>'total_cents')::bigint) AS applied
  FROM entity_state WHERE entity = 'sale' AND NOT deleted`;

export const REFUNDS = `SELECT branch_id, entity_id AS refund_id, payload,
    (cm_utc(payload->'refund'->>'refunded_utc') AT TIME ZONE $1)::date AS day,
    (payload->'refund'->>'total_cents')::bigint AS total,
    payload->'refund'->>'sale_id' AS sale_id,
    payload->'refund'->>'cash_session_id' AS session_id
  FROM entity_state WHERE entity = 'sale_refund' AND NOT deleted`;

/**
 * El periodo en días locales como instantes UTC ($1 zona, $2 desde, $3 hasta): con los índices de la migración 009 sólo se leen las filas
 * del periodo en lugar de convertir la fecha de todo el historial. Igual que «día local BETWEEN desde AND hasta».
 */
export const inPeriod = (at: string, from = '$2', to = '$3', zone = '$1') =>
  `cm_utc(${at}) >= (${from}::date::timestamp AT TIME ZONE ${zone}) AND cm_utc(${at}) < ((${to}::date + 1)::timestamp AT TIME ZONE ${zone})`;
/** Ventas y devoluciones del periodo y la sucursal ($1–$4). */
export const SALES_IN = `${SALES} AND ${inPeriod("payload->'sale'->>'confirmed_utc'")} AND ($4::uuid IS NULL OR branch_id = $4::uuid)`;
export const REFUNDS_IN = `${REFUNDS} AND ${inPeriod("payload->'refund'->>'refunded_utc'")} AND ($4::uuid IS NULL OR branch_id = $4::uuid)`;
/** Turnos que abrieron en el periodo y la sucursal ($1–$4). */
export const SESSIONS_IN = `SELECT branch_id, terminal_id, payload AS p FROM entity_state WHERE entity = 'cash_session' AND NOT deleted
  AND ${inPeriod("payload->>'opened_utc'")} AND ($4::uuid IS NULL OR branch_id = $4::uuid)`;

const MOVEMENTS = `SELECT payload->>'cash_session_id' AS session_id, payload->>'kind' AS kind, (payload->>'amount_cents')::bigint AS amount
  FROM entity_state WHERE entity = 'cash_movement' AND NOT deleted`;

const n = (v: unknown) => (v === null || v === undefined ? 0 : Number(v));
const day = (v: unknown) => (v instanceof Date ? v.toISOString().slice(0, 10) : String(v).slice(0, 10));

export interface DayRow { branchId: string; day: string; tickets: number; gross: number; cash: number; card: number; transfer: number; refunds: number }

export async function salesByDay(q: Query, r: Range): Promise<DayRow[]> {
  const sales = await q(`WITH s AS (${SALES_IN})
    SELECT branch_id, day::text AS day, count(*) AS tickets, sum(total) AS gross,
      sum(total) FILTER (WHERE method = 'CASH') AS cash, sum(total) FILTER (WHERE method = 'CARD') AS card,
      sum(total) FILTER (WHERE method = 'TRANSFER') AS transfer
    FROM s WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)
    GROUP BY branch_id, day`, [r.timeZone, r.from, r.to, r.branchId]);
  const refunds = await q(`WITH r AS (${REFUNDS_IN})
    SELECT branch_id, day::text AS day, sum(total) AS refunds FROM r
    WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)
    GROUP BY branch_id, day`, [r.timeZone, r.from, r.to, r.branchId]);
  const rows = new Map<string, DayRow>();
  const at = (branchId: string, d: string) => {
    const k = branchId + d;
    if (!rows.has(k)) rows.set(k, { branchId, day: d, tickets: 0, gross: 0, cash: 0, card: 0, transfer: 0, refunds: 0 });
    return rows.get(k)!;
  };
  for (const s of sales) Object.assign(at(String(s.branch_id), day(s.day)), { tickets: n(s.tickets), gross: n(s.gross), cash: n(s.cash), card: n(s.card), transfer: n(s.transfer) });
  for (const f of refunds) at(String(f.branch_id), day(f.day)).refunds = n(f.refunds);
  return [...rows.values()].sort((a, b) => a.day.localeCompare(b.day) || a.branchId.localeCompare(b.branchId));
}

export interface RefundsByMethod { cash: number; card: number; transfer: number }

export async function refundsByMethod(q: Query, r: Range): Promise<RefundsByMethod> {
  const rows = await q(`WITH s AS (${SALES}), r AS (${REFUNDS_IN})
    SELECT s.method, sum(r.total) AS total FROM r JOIN s ON s.sale_id = r.sale_id AND s.branch_id = r.branch_id
    WHERE r.day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR r.branch_id = $4::uuid)
    GROUP BY s.method`, [r.timeZone, r.from, r.to, r.branchId]);
  const by = (m: string) => n(rows.find(x => x.method === m)?.total);
  return { cash: by('CASH'), card: by('CARD'), transfer: by('TRANSFER') };
}

export interface ProductRow { name: string; quantity: number; total: number }

export async function topProducts(q: Query, r: Range, limit = 10): Promise<ProductRow[]> {
  const rows = await q(`WITH s AS (${SALES_IN})
    SELECT item->>'product_name' AS name, sum((item->>'quantity')::bigint) AS quantity, sum((item->>'line_total_cents')::bigint) AS total
    FROM s, jsonb_array_elements(s.payload->'items') AS item
    WHERE s.day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR s.branch_id = $4::uuid)
    GROUP BY 1 ORDER BY total DESC, name LIMIT $5`, [r.timeZone, r.from, r.to, r.branchId, limit]);
  return rows.map(x => ({ name: String(x.name), quantity: n(x.quantity), total: n(x.total) }));
}

export interface SessionRow {
  id: string; branchId: string; terminal: string; openedAt: string; closedAt: string | null; open: boolean;
  opening: number; cashSales: number; cashRefunds: number; movements: number; expected: number; counted: number | null; difference: number | null;
  note: string | null;
}

/** Cortes que abrieron en el periodo. Abiertos: el esperado es lo que va del turno; cerrados: lo que se registró al cerrar. */
export async function sessions(q: Query, r: Range): Promise<SessionRow[]> {
  // Sólo las ventas, devoluciones y movimientos de esos turnos (índices por turno de la migración 009).
  const rows = await q(`WITH c AS (${SESSIONS_IN}), ids AS (SELECT p->>'id' AS id FROM c),
    s AS NOT MATERIALIZED (${SALES}), r AS NOT MATERIALIZED (${REFUNDS}), m AS NOT MATERIALIZED (${MOVEMENTS}),
    cs AS (SELECT ids.id AS session_id, x.total FROM ids CROSS JOIN LATERAL (SELECT sum(applied) AS total FROM s WHERE method = 'CASH' AND session_id = ids.id) x),
    cr AS (SELECT r.session_id, sum(r.total) AS total FROM r JOIN s ON s.sale_id = r.sale_id AND s.branch_id = r.branch_id
      WHERE s.method = 'CASH' AND r.session_id IN (SELECT id FROM ids) GROUP BY r.session_id),
    mv AS (SELECT session_id, sum(CASE WHEN kind = 'DEPOSIT' THEN amount ELSE -amount END) AS net FROM m WHERE session_id IN (SELECT id FROM ids) GROUP BY session_id)
    SELECT c.p->>'id' AS id, c.branch_id, COALESCE(t.name, 'Caja') AS terminal,
      c.p->>'opened_utc' AS opened_at, c.p->>'closed_utc' AS closed_at, c.p->>'state' AS state,
      (c.p->>'opening_cents')::bigint AS opening, cs.total AS cash_sales, cr.total AS cash_refunds, mv.net AS movements,
      (c.p->>'expected_cents')::bigint AS expected, (c.p->>'counted_cents')::bigint AS counted, (c.p->>'difference_cents')::bigint AS difference,
      c.p->>'discrepancy_note' AS note
    FROM c LEFT JOIN terminal t ON t.id = c.terminal_id
      LEFT JOIN cs ON cs.session_id = c.p->>'id' LEFT JOIN cr ON cr.session_id = c.p->>'id' LEFT JOIN mv ON mv.session_id = c.p->>'id'
    ORDER BY cm_utc(c.p->>'opened_utc') DESC LIMIT 300`, [r.timeZone, r.from, r.to, r.branchId]);
  return rows.map(x => {
    const open = x.state !== 'CLOSED';
    const running = n(x.opening) + n(x.cash_sales) - n(x.cash_refunds) + n(x.movements);
    return {
      id: String(x.id), branchId: String(x.branch_id), terminal: String(x.terminal), openedAt: String(x.opened_at),
      closedAt: x.closed_at ? String(x.closed_at) : null, open, opening: n(x.opening), cashSales: n(x.cash_sales),
      cashRefunds: n(x.cash_refunds), movements: n(x.movements), expected: open ? running : n(x.expected),
      counted: open || x.counted === null ? null : n(x.counted), difference: open || x.difference === null ? null : n(x.difference),
      note: open || x.note === null || x.note === undefined ? null : String(x.note),
    };
  });
}

export interface TerminalRow { id: string; branchId: string; name: string; registeredAt: string; lastSeenAt: string | null; revoked: boolean; web: boolean }

export async function terminals(q: Query, branchId: string | null): Promise<TerminalRow[]> {
  const rows = await q(`SELECT id, branch_id, name, registered_at, last_seen_at, revoked_at, kind FROM terminal
    WHERE ($1::uuid IS NULL OR branch_id = $1::uuid) ORDER BY name`, [branchId]);
  const iso = (v: unknown) => (v instanceof Date ? v.toISOString() : String(v));
  return rows.map(x => ({ id: String(x.id), branchId: String(x.branch_id), name: String(x.name), registeredAt: iso(x.registered_at),
    lastSeenAt: x.last_seen_at ? iso(x.last_seen_at) : null, revoked: x.revoked_at !== null && x.revoked_at !== undefined, web: x.kind === 'web' }));
}

export async function branches(q: Query): Promise<{ id: string; name: string }[]> {
  return (await q('SELECT id, name FROM branch ORDER BY name', [])).map(x => ({ id: String(x.id), name: String(x.name) }));
}

/** Faltantes y sobrantes de todos los cortes cerrados que abrieron en el periodo. */
export async function cashDifferences(q: Query, r: Range) {
  const [row] = await q(`SELECT COALESCE(sum(LEAST((payload->>'difference_cents')::bigint, 0)), 0) AS shortages,
      COALESCE(sum(GREATEST((payload->>'difference_cents')::bigint, 0)), 0) AS overages
    FROM entity_state WHERE entity = 'cash_session' AND NOT deleted AND payload->>'state' = 'CLOSED'
      AND ${inPeriod("payload->>'opened_utc'")} AND ($4::uuid IS NULL OR branch_id = $4::uuid)`,
    [r.timeZone, r.from, r.to, r.branchId]);
  return { shortages: n(row.shortages), overages: n(row.overages) };
}

/** Todo lo que muestra el panel para un periodo, en una sola respuesta. */
export async function dashboard(q: Query, r: Range) {
  const [days, refundMethods, products, cortes, terminalRows, branchRows, differences] = await Promise.all([
    salesByDay(q, r), refundsByMethod(q, r), topProducts(q, r), sessions(q, r), terminals(q, r.branchId), branches(q), cashDifferences(q, r),
  ]);
  const sum = (f: (d: DayRow) => number) => days.reduce((a, d) => a + f(d), 0);
  const totals = {
    tickets: sum(d => d.tickets), gross: sum(d => d.gross), refunds: sum(d => d.refunds),
    net: sum(d => d.gross) - sum(d => d.refunds),
    byMethod: {
      cash: sum(d => d.cash) - refundMethods.cash,
      card: sum(d => d.card) - refundMethods.card,
      transfer: sum(d => d.transfer) - refundMethods.transfer,
    },
    // De todos los cortes del periodo (la lista de cortes se recorta a 300).
    shortages: differences.shortages, overages: differences.overages,
  };
  return { range: r, branches: branchRows, days, totals, products, sessions: cortes, terminals: terminalRows };
}
