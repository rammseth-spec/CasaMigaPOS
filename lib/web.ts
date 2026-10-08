// Consultas de cada pantalla de la versión web (docs/WEB.md). Sólo lectura sobre entity_state.
// `branch` ya viene acotado por el rol (auth.scopeBranch): null = todas las sucursales (sólo administrador).
import { ProtocolError } from './protocol.js';
import { REFUNDS, SALES, type Query, type Range, type Row } from './panel.js';

const n = (v: unknown) => (v === null || v === undefined ? 0 : Number(v));
const s = (v: unknown) => (v === null || v === undefined ? '' : String(v));
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const isUuid = (v: unknown): v is string => typeof v === 'string' && UUID.test(v);

/** Mismo folio visible que la caja (DisplayFolio): clave por el número al final del nombre de la caja + consecutivo de 6 cifras. */
export function terminalCode(name: string): string {
  const match = /(\d+)\s*$/.exec(name.trim());
  const number = match ? Number(match[1]) : 1;
  return 'C' + String(number > 0 && number <= 9999 ? number : 1).padStart(2, '0');
}
export const displayFolio = (terminal: string, sequence: number) => `${terminalCode(terminal)}-${String(sequence).padStart(6, '0')}`;

/** Nombres para mostrar: usuarios, cajas y sucursales. */
async function names(q: Query) {
  const [users, terminals, branches] = await Promise.all([
    q(`SELECT entity_id AS id, payload->>'display_name' AS name FROM entity_state WHERE entity = 'app_user'`, []),
    q('SELECT id, name FROM terminal', []),
    q('SELECT id, name FROM branch', []),
  ]);
  return {
    user: new Map(users.map(u => [s(u.id), s(u.name)])),
    terminal: new Map(terminals.map(t => [s(t.id), s(t.name)])),
    branch: new Map(branches.map(b => [s(b.id), s(b.name)])),
  };
}

// ---------- Ventas ----------

export interface SaleFilter extends Range { search: string; method: string | null; page: number }
export const PAGE = 50;

export async function salesList(q: Query, f: SaleFilter) {
  const search = f.search.trim();
  const folio = /^(?:[Cc](\d{1,4})\s*-\s*)?(\d{1,12})$/.exec(search);
  const k = await names(q);
  // Con clave de caja (C02-…) sólo cuentan las cajas con esa clave; se filtra en la consulta para que el total y las páginas cuadren.
  const code = folio?.[1] ? 'C' + folio[1].padStart(2, '0') : null;
  const terminals = code ? [...k.terminal.entries()].filter(([, name]) => terminalCode(name) === code).map(([id]) => id) : null;
  const rows = await q(`WITH s AS (${SALES}), r AS (${REFUNDS}),
      refunded AS (SELECT sale_id, sum(total) AS total, bool_or(payload->'refund'->>'kind' = 'CANCELLATION') AS cancelled FROM r GROUP BY sale_id)
    SELECT s.sale_id, s.branch_id, s.terminal_id, s.payload->'sale'->>'confirmed_utc' AS at, (s.payload->'sale'->>'sequence')::bigint AS sequence,
      s.total, s.method, s.payload->'sale'->>'user_id' AS user_id, jsonb_array_length(s.payload->'items') AS lines,
      (SELECT sum((i->>'quantity')::bigint) FROM jsonb_array_elements(s.payload->'items') i) AS pieces,
      refunded.total AS refunded, refunded.cancelled, count(*) OVER () AS total_count
    FROM s LEFT JOIN refunded ON refunded.sale_id = s.sale_id
    WHERE s.day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR s.branch_id = $4::uuid)
      AND ($5::text IS NULL OR s.method = $5)
      AND ($6::bigint IS NULL OR (s.payload->'sale'->>'sequence')::bigint = $6)
      AND ($7::text IS NULL OR EXISTS (SELECT 1 FROM jsonb_array_elements(s.payload->'items') i WHERE i->>'product_name' ILIKE '%' || $7 || '%'))
      AND ($9::uuid[] IS NULL OR s.terminal_id = ANY($9::uuid[]))
    ORDER BY (s.payload->'sale'->>'confirmed_utc')::timestamptz DESC, s.sale_id
    LIMIT ${PAGE} OFFSET $8`,
    [f.timeZone, f.from, f.to, f.branchId, f.method, folio ? Number(folio[2]) : null, !folio && search ? search.slice(0, 80) : null, f.page * PAGE, terminals]);
  const items = rows.map(r => {
    const terminal = k.terminal.get(s(r.terminal_id)) ?? 'Caja';
    return {
      id: s(r.sale_id), branchId: s(r.branch_id), branch: k.branch.get(s(r.branch_id)) ?? '', terminal, folio: displayFolio(terminal, n(r.sequence)),
      at: s(r.at), total: n(r.total), method: s(r.method), cashier: k.user.get(s(r.user_id)) ?? '', lines: n(r.lines), pieces: n(r.pieces),
      refunded: n(r.refunded), cancelled: r.cancelled === true,
    };
  });
  return { items, total: n(rows[0]?.total_count), page: f.page, pageSize: PAGE };
}

export async function saleDetail(q: Query, branch: string | null, id: string) {
  if (!isUuid(id)) throw new ProtocolError('Venta inválida');
  const [row] = await q(`SELECT branch_id, terminal_id, payload FROM entity_state WHERE entity = 'sale' AND entity_id = $1 AND ($2::uuid IS NULL OR branch_id = $2::uuid)`, [id.toLowerCase(), branch]);
  if (!row) throw new ProtocolError('Venta no encontrada', 404);
  const refunds = await q(`SELECT payload FROM entity_state WHERE entity = 'sale_refund' AND payload->'refund'->>'sale_id' = $1 AND branch_id = $2
    ORDER BY payload->'refund'->>'refunded_utc'`, [id.toLowerCase(), row.branch_id]);
  const k = await names(q);
  const p = row.payload as Row & { sale: Row; items: Row[]; payment: Row | null };
  const terminal = k.terminal.get(s(row.terminal_id)) ?? 'Caja';
  return {
    id, branch: k.branch.get(s(row.branch_id)) ?? '', terminal, folio: displayFolio(terminal, n(p.sale.sequence)), at: s(p.sale.confirmed_utc),
    cashier: k.user.get(s(p.sale.user_id)) ?? '', subtotal: n(p.sale.subtotal_cents), total: n(p.sale.total_cents),
    items: p.items.map(i => ({ position: n(i.position), name: s(i.product_name), quantity: n(i.quantity), unitPrice: n(i.unit_price_cents), total: n(i.line_total_cents) })),
    payment: p.payment ? { method: s(p.payment.method), applied: n(p.payment.applied_cents), received: n(p.payment.received_cents), change: n(p.payment.change_cents), reference: s(p.payment.reference) } : null,
    refunds: refunds.map(r => {
      const f = (r.payload as Row & { refund: Row; items: Row[] });
      return { kind: s(f.refund.kind), reason: s(f.refund.reason), at: s(f.refund.refunded_utc), total: n(f.refund.total_cents), by: k.user.get(s(f.refund.user_id)) ?? '',
        items: f.items.map(i => ({ position: n(i.position), quantity: n(i.quantity), total: n(i.line_total_cents) })) };
    }),
  };
}

// ---------- Reportes ----------

function shift(day: string, days: number) {
  const d = new Date(day + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10);
}

/** Periodo contra el anterior de igual duración, día por día. */
export async function comparison(q: Query, r: Range) {
  const length = (Date.parse(r.to) - Date.parse(r.from)) / 86_400_000 + 1;
  const previous = { ...r, from: shift(r.from, -length), to: shift(r.from, -1) };
  const totals = async (x: Range) => {
    const rows = await q(`WITH s AS (${SALES}), r AS (${REFUNDS})
      SELECT (SELECT count(*) FROM s WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)) AS tickets,
        (SELECT COALESCE(sum(total), 0) FROM s WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)) AS gross,
        (SELECT COALESCE(sum(total), 0) FROM r WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)) AS refunds`,
      [x.timeZone, x.from, x.to, x.branchId]);
    const days = await q(`WITH s AS (${SALES}) SELECT day::text AS day, sum(total) AS gross FROM s
      WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid) GROUP BY day`, [x.timeZone, x.from, x.to, x.branchId]);
    const t = rows[0];
    return { tickets: n(t.tickets), gross: n(t.gross), refunds: n(t.refunds), net: n(t.gross) - n(t.refunds), days: new Map(days.map(d => [s(d.day), n(d.gross)])) };
  };
  const [now, before] = await Promise.all([totals(r), totals(previous)]);
  const daily = Array.from({ length }, (_, i) => ({ day: shift(r.from, i), previousDay: shift(previous.from, i), current: now.days.get(shift(r.from, i)) ?? 0, previous: before.days.get(shift(previous.from, i)) ?? 0 }));
  const strip = ({ days, ...rest }: Awaited<ReturnType<typeof totals>>) => rest;
  return { current: strip(now), previous: strip(before), previousRange: { from: previous.from, to: previous.to }, daily };
}

/** Tickets y ventas por día de la semana (0 = domingo) y hora local. */
export async function heatmap(q: Query, r: Range) {
  const rows = await q(`WITH s AS (${SALES})
    SELECT extract(dow FROM (payload->'sale'->>'confirmed_utc')::timestamptz AT TIME ZONE $1)::int AS weekday,
      extract(hour FROM (payload->'sale'->>'confirmed_utc')::timestamptz AT TIME ZONE $1)::int AS hour, count(*) AS tickets, sum(total) AS gross
    FROM s WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid) GROUP BY 1, 2`, [r.timeZone, r.from, r.to, r.branchId]);
  return rows.map(x => ({ weekday: n(x.weekday), hour: n(x.hour), tickets: n(x.tickets), gross: n(x.gross) }));
}

/** Faltantes y sobrantes por quien abrió la caja; devoluciones por quien las hizo y sus motivos. */
export async function cashControl(q: Query, r: Range) {
  const k = await names(q);
  const cuts = await q(`SELECT payload->>'opened_by' AS user_id, count(*) AS sessions,
      sum(LEAST((payload->>'difference_cents')::bigint, 0)) AS shortages, sum(GREATEST((payload->>'difference_cents')::bigint, 0)) AS overages,
      count(*) FILTER (WHERE (payload->>'difference_cents')::bigint <> 0) AS uneven
    FROM entity_state WHERE entity = 'cash_session' AND payload->>'state' = 'CLOSED'
      AND (((payload->>'opened_utc')::timestamptz AT TIME ZONE $1)::date) BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)
    GROUP BY 1`, [r.timeZone, r.from, r.to, r.branchId]);
  const refunds = await q(`WITH r AS (${REFUNDS})
    SELECT payload->'refund'->>'user_id' AS user_id, payload->'refund'->>'kind' AS kind, payload->'refund'->>'reason' AS reason, total
    FROM r WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)`, [r.timeZone, r.from, r.to, r.branchId]);
  const byUser = new Map<string, { name: string; count: number; total: number; cancellations: number }>();
  const reasons = new Map<string, number>();
  for (const f of refunds) {
    const id = s(f.user_id);
    const entry = byUser.get(id) ?? { name: k.user.get(id) ?? 'Usuario', count: 0, total: 0, cancellations: 0 };
    entry.count++; entry.total += n(f.total); if (f.kind === 'CANCELLATION') entry.cancellations++;
    byUser.set(id, entry);
    const reason = s(f.reason).trim() || 'Sin motivo';
    reasons.set(reason, (reasons.get(reason) ?? 0) + 1);
  }
  return {
    cuts: cuts.map(c => ({ user: k.user.get(s(c.user_id)) ?? 'Usuario', sessions: n(c.sessions), uneven: n(c.uneven), shortages: n(c.shortages), overages: n(c.overages) }))
      .sort((a, b) => a.shortages - b.shortages),
    refunds: [...byUser.values()].sort((a, b) => b.total - a.total),
    reasons: [...reasons.entries()].map(([reason, count]) => ({ reason, count })).sort((a, b) => b.count - a.count).slice(0, 15),
  };
}

/** Piezas sugeridas para un día: promedio del mismo día de la semana en las 4 semanas anteriores + 10 % (como la caja). */
export async function productionSuggestion(q: Query, branch: string | null, timeZone: string, day: string) {
  const weeks = [1, 2, 3, 4].map(w => shift(day, -7 * w));
  const rows = await q(`WITH s AS (${SALES})
    SELECT i->>'product_name' AS name, s.day::text AS day, sum((i->>'quantity')::bigint) AS quantity
    FROM s, jsonb_array_elements(s.payload->'items') i
    WHERE s.day = ANY($2::date[]) AND ($3::uuid IS NULL OR s.branch_id = $3::uuid) GROUP BY 1, 2`, [timeZone, weeks, branch]);
  const byProduct = new Map<string, number[]>();
  for (const x of rows) { const list = byProduct.get(s(x.name)) ?? []; list.push(n(x.quantity)); byProduct.set(s(x.name), list); }
  return {
    day, weeks,
    products: [...byProduct.entries()].map(([name, list]) => {
      const average = list.reduce((a, b) => a + b, 0) / 4;
      return { name, average: Math.round(average * 10) / 10, suggested: Math.ceil(average * 1.1), weeksWithSales: list.length };
    }).sort((a, b) => b.suggested - a.suggested || a.name.localeCompare(b.name)),
  };
}

// ---------- Caja ----------

export async function cashMovements(q: Query, r: Range) {
  const k = await names(q);
  const rows = await q(`SELECT branch_id, terminal_id, payload FROM entity_state WHERE entity = 'cash_movement' AND NOT deleted
    AND (((payload->>'occurred_utc')::timestamptz AT TIME ZONE $1)::date) BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)
    ORDER BY (payload->>'occurred_utc')::timestamptz DESC LIMIT 500`, [r.timeZone, r.from, r.to, r.branchId]);
  return rows.map(x => {
    const p = x.payload as Row;
    return { id: s(p.id), branch: k.branch.get(s(x.branch_id)) ?? '', terminal: k.terminal.get(s(x.terminal_id)) ?? '', at: s(p.occurred_utc),
      kind: s(p.kind), category: p.category === null ? null : s(p.category), amount: n(p.amount_cents), note: s(p.note), user: k.user.get(s(p.user_id)) ?? '' };
  });
}

/** Totales de caja del periodo calculados en la base (las listas se recortan). */
export async function cashTotals(q: Query, r: Range) {
  const [cuts] = await q(`SELECT count(*) AS sessions, count(*) FILTER (WHERE payload->>'state' <> 'CLOSED') AS open
    FROM entity_state WHERE entity = 'cash_session' AND NOT deleted
      AND (((payload->>'opened_utc')::timestamptz AT TIME ZONE $1)::date) BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)`,
    [r.timeZone, r.from, r.to, r.branchId]);
  const kinds = await q(`SELECT payload->>'kind' AS kind, count(*) AS count, sum((payload->>'amount_cents')::bigint) AS amount
    FROM entity_state WHERE entity = 'cash_movement' AND NOT deleted
      AND (((payload->>'occurred_utc')::timestamptz AT TIME ZONE $1)::date) BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)
    GROUP BY 1`, [r.timeZone, r.from, r.to, r.branchId]);
  const by = (kind: string) => { const k = kinds.find(x => x.kind === kind); return { count: n(k?.count), amount: n(k?.amount) }; };
  return { sessions: n(cuts.sessions), open: n(cuts.open), expenses: by('EXPENSE'), withdrawals: by('WITHDRAWAL'), deposits: by('DEPOSIT') };
}

// ---------- Charolas ----------

export async function consignment(q: Query, r: Range) {
  const [stores, products, deliveries, payments] = await Promise.all([
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_store' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [r.branchId]),
    q(`SELECT entity_id AS id, payload->'product'->>'name' AS name FROM entity_state WHERE entity = 'product'`, []),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_delivery' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [r.branchId]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_payment' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [r.branchId]),
  ]);
  const product = new Map(products.map(p => [s(p.id), s(p.name)]));
  const local = (iso: string) => new Date(iso).toLocaleDateString('en-CA', { timeZone: r.timeZone });
  const inRange = (iso: string) => { const d = local(iso); return d >= r.from && d <= r.to; };
  const summary = new Map<string, { charged: number; paid: number; delivered: number; sold: number; returned: number; periodCharged: number; periodPaid: number; pending: number }>();
  const at = (id: string) => { if (!summary.has(id)) summary.set(id, { charged: 0, paid: 0, delivered: 0, sold: 0, returned: 0, periodCharged: 0, periodPaid: 0, pending: 0 }); return summary.get(id)!; };
  const list = [];
  for (const d of deliveries) {
    const p = d.payload as Row & { delivery: Row; items: Row[]; settlement: Row | null; settlementItems: Row[]; void: Row | null };
    const store = s(p.delivery.store_id); const t = at(store);
    const pieces = p.items.reduce((a, i) => a + n(i.quantity), 0);
    const sold = p.settlementItems.reduce((a, i) => a + n(i.sold), 0);
    const returned = p.settlementItems.reduce((a, i) => a + n(i.returned), 0);
    if (p.void) { /* anulada: no cuenta */ }
    else if (p.settlement) { t.charged += n(p.settlement.sold_cents); if (inRange(s(p.settlement.collected_utc))) t.periodCharged += n(p.settlement.sold_cents); }
    else t.pending++;
    if (!p.void && inRange(s(p.delivery.delivered_utc))) { t.delivered += pieces; t.sold += sold; t.returned += returned; }
    if (inRange(s(p.delivery.delivered_utc)))
      list.push({ id: s(p.delivery.id), store, at: s(p.delivery.delivered_utc), pieces, sold, returned,
        status: p.void ? 'voided' : p.settlement ? 'collected' : 'pending', charged: p.settlement ? n(p.settlement.sold_cents) : 0,
        collectedAt: p.settlement ? s(p.settlement.collected_utc) : null,
        items: p.items.map(i => ({ name: s(i.product_name), quantity: n(i.quantity), price: n(i.unit_price_cents) })) });
  }
  const paymentList = [];
  for (const x of payments) {
    const p = x.payload as Row; const t = at(s(p.store_id));
    t.paid += n(p.amount_cents);
    if (inRange(s(p.received_utc))) { t.periodPaid += n(p.amount_cents); paymentList.push({ id: s(p.id), store: s(p.store_id), at: s(p.received_utc), amount: n(p.amount_cents), method: s(p.method), note: s(p.note) }); }
  }
  return {
    stores: stores.map(x => {
      const p = x.payload as Row & { store: Row; template: Row[] }; const t = at(s(p.store.id));
      return { id: s(p.store.id), branchId: s(x.branch_id), name: s(p.store.name), contact: s(p.store.contact), phone: s(p.store.phone), address: s(p.store.address),
        active: n(p.store.active) === 1, template: p.template.map(i => ({ productId: s(i.product_id), name: product.get(s(i.product_id)) ?? 'Producto', quantity: n(i.quantity) })),
        balance: t.charged - t.paid, ...t };
    }).sort((a, b) => a.name.localeCompare(b.name)),
    deliveries: list.sort((a, b) => b.at.localeCompare(a.at)).slice(0, 300),
    payments: paymentList.sort((a, b) => b.at.localeCompare(a.at)).slice(0, 300),
  };
}

/** Ruta del repartidor: tiendas activas con su charola habitual, charolas por recoger (con partidas), saldo y lo enviado desde la web. */
export async function route(q: Query, branch: string | null) {
  const [stores, deliveries, payments, products, prices, sent] = await Promise.all([
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_store' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_delivery' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_payment' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, entity_id AS id, payload->'product'->>'name' AS name, (payload->'product'->>'active')::int AS active FROM entity_state WHERE entity = 'product' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, entity_id AS id, (payload->>'price_cents')::bigint AS price FROM entity_state WHERE entity = 'consignment_price' AND NOT deleted AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT c.id, c.kind, c.payload, c.requested_at, c.requested_by_name FROM branch_command c
       WHERE c.kind LIKE 'consignment.%' AND ($1::uuid IS NULL OR c.branch_id = $1::uuid)
         AND NOT EXISTS (SELECT 1 FROM command_result r WHERE r.command_id = c.id)
       ORDER BY c.seq`, [branch]),
  ]);
  const key = (b: unknown, x: unknown) => `${s(b)}/${s(x)}`;
  const productName = new Map(products.map(p => [key(p.branch_id, p.id), s(p.name)]));
  const price = new Map(prices.map(p => [key(p.branch_id, p.id), n(p.price)]));
  const charged = new Map<string, number>(); const paid = new Map<string, number>(); const last = new Map<string, string>();
  const open = new Map<string, unknown[]>();
  for (const d of deliveries) {
    const p = d.payload as { delivery: Row; items: Row[]; settlement: Row | null; void: Row | null };
    const store = s(p.delivery.store_id);
    if (!p.void && (!last.has(store) || s(p.delivery.delivered_utc) > last.get(store)!)) last.set(store, s(p.delivery.delivered_utc));
    if (p.settlement) charged.set(store, (charged.get(store) ?? 0) + n(p.settlement.sold_cents));
    if (!p.settlement && !p.void) {
      const list = open.get(store) ?? []; open.set(store, list);
      list.push({ id: s(p.delivery.id), at: s(p.delivery.delivered_utc), note: s(p.delivery.note),
        items: p.items.map(i => ({ position: n(i.position), name: s(i.product_name), quantity: n(i.quantity), price: n(i.unit_price_cents) })) });
    }
  }
  for (const x of payments) { const p = x.payload as Row; paid.set(s(p.store_id), (paid.get(s(p.store_id)) ?? 0) + n(p.amount_cents)); }
  const pendingSent = sent.map(c => ({ id: s(c.id), kind: s(c.kind), payload: c.payload as Row, at: c.requested_at instanceof Date ? c.requested_at.toISOString() : s(c.requested_at), by: s(c.requested_by_name) }));
  return {
    stores: stores.map(x => {
      const p = x.payload as { store: Row; template: Row[] };
      const id = s(p.store.id);
      return { id, branchId: s(x.branch_id), name: s(p.store.name), contact: s(p.store.contact), phone: s(p.store.phone), address: s(p.store.address),
        active: n(p.store.active) === 1, balance: (charged.get(id) ?? 0) - (paid.get(id) ?? 0), lastDelivery: last.get(id) ?? null,
        template: p.template.map(t => ({ productId: s(t.product_id), name: productName.get(key(x.branch_id, t.product_id)) ?? 'Pan', quantity: n(t.quantity) })),
        atStore: (open.get(id) ?? []).sort((a: any, b: any) => a.at.localeCompare(b.at)),
        sent: pendingSent.filter(c => s(c.payload.storeId) === id || (open.get(id) ?? []).some((d: any) => d.id === s(c.payload.deliveryId))) };
    }).filter(st => st.active).sort((a, b) => a.name.localeCompare(b.name)),
    products: products.filter(p => n(p.active) === 1 && price.has(key(p.branch_id, p.id)))
      .map(p => ({ id: s(p.id), branchId: s(p.branch_id), name: s(p.name), price: price.get(key(p.branch_id, p.id))! })).sort((a, b) => a.name.localeCompare(b.name)),
  };
}

// ---------- Catálogo ----------

export async function catalog(q: Query, branch: string | null) {
  const [categories, products, photos, prices] = await Promise.all([
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'category' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload->'product' AS p FROM entity_state WHERE entity = 'product' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, entity_id, payload->'asset'->>'thumbnail_sha256' AS sha, payload->'asset'->>'provenance' AS provenance, payload->>'thumbnail' IS NOT NULL AS has
       FROM entity_state WHERE entity = 'product_photo' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, entity_id, (payload->>'price_cents')::bigint AS price FROM entity_state WHERE entity = 'consignment_price' AND NOT deleted AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
  ]);
  const key = (b: unknown, id: unknown) => `${s(b)}/${s(id)}`;
  const photo = new Map(photos.filter(p => p.has === true).map(p => [key(p.branch_id, p.entity_id), { sha: s(p.sha), provisional: p.provenance === 'PROVISIONAL' }]));
  const price = new Map(prices.map(p => [key(p.branch_id, p.entity_id), n(p.price)]));
  return {
    categories: categories.map(c => { const p = c.payload as Row; return { id: s(p.id), branchId: s(c.branch_id), name: s(p.name), description: s(p.description), sortOrder: n(p.sort_order), active: n(p.active) === 1 }; })
      .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name)),
    products: products.map(x => {
      const p = x.p as Row;
      return { id: s(p.id), branchId: s(x.branch_id), sku: p.sku === null ? null : s(p.sku), name: s(p.name), description: s(p.description), categoryId: s(p.category_id),
        price: n(p.price_cents), active: n(p.active) === 1, trackInventory: n(p.track_inventory) === 1, photo: photo.get(key(x.branch_id, p.id)) ?? null,
        consignmentPrice: price.get(key(x.branch_id, p.id)) ?? null, modifiedAt: s(p.modified_utc) };
    }).sort((a, b) => a.name.localeCompare(b.name)),
  };
}

export async function photo(q: Query, branch: string | null, product: string): Promise<{ bytes: Buffer; sha: string } | null> {
  if (!isUuid(product)) throw new ProtocolError('Producto inválido');
  const [row] = await q(`SELECT payload->>'thumbnail' AS thumbnail, payload->'asset'->>'thumbnail_sha256' AS sha FROM entity_state
    WHERE entity = 'product_photo' AND entity_id = $1 AND ($2::uuid IS NULL OR branch_id = $2::uuid) ORDER BY received_at DESC LIMIT 1`, [product.toLowerCase(), branch]);
  return row?.thumbnail ? { bytes: Buffer.from(s(row.thumbnail), 'base64'), sha: s(row.sha) } : null;
}

// ---------- Inventario, recetas y producción ----------

async function units(q: Query) {
  const [row] = await q(`SELECT payload FROM entity_state WHERE entity = 'measurement_unit' AND entity_id = 'all' ORDER BY received_at DESC LIMIT 1`, []);
  return ((row?.payload as { rows?: Row[] } | undefined)?.rows ?? []).map(u => ({ code: s(u.code), dimension: s(u.dimension), symbol: s(u.symbol), atomic: n(u.atomic_units_per_unit) }));
}

export async function inventory(q: Query, branch: string | null, timeZone: string) {
  const [items, locations, operations, measures] = await Promise.all([
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'inventory_item' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'inventory_location' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'inventory_operation' AND ($1::uuid IS NULL OR branch_id = $1::uuid)
       ORDER BY payload->'operation'->>'created_utc' DESC LIMIT 200`, [branch]),
    units(q),
  ]);
  const location = new Map(locations.map(l => [s((l.payload as Row).id), s((l.payload as Row).name)]));
  const itemName = new Map(items.map(i => [s((i.payload as { item: Row }).item.id), s((i.payload as { item: Row }).item.name)]));
  const k = await names(q);
  return {
    measures, timeZone,
    items: items.map(x => {
      const p = x.payload as { item: Row; units: Row[]; balances: Row[] };
      const cost = (p as { cost?: Row | null }).cost;
      return { id: s(p.item.id), branchId: s(x.branch_id), code: s(p.item.code), name: s(p.item.name), kind: s(p.item.kind), dimension: s(p.item.dimension), active: n(p.item.active) === 1,
        cost: cost ? { cents: n(cost.cost_cents), perAtomic: n(cost.per_atomic), unit: s(cost.unit_label), updatedAt: s(cost.updated_utc) } : null,
        units: p.units.filter(u => n(u.active) === 1).map(u => ({ code: s(u.code), name: s(u.name), atomic: n(u.atomic_units_per_whole_unit) })),
        balances: p.balances.map(b => ({ location: location.get(s(b.location_id)) ?? 'Almacén', atomic: n(b.quantity_atomic), updatedAt: s(b.updated_utc) })) };
    }).sort((a, b) => a.name.localeCompare(b.name)),
    operations: operations.map(x => {
      const p = x.payload as { operation: Row; movements: Row[] };
      return { id: s(p.operation.id), kind: s(p.operation.kind), state: s(p.operation.state), at: s(p.operation.confirmed_utc || p.operation.created_utc), note: s(p.operation.note),
        reason: s(p.operation.reason_code), user: k.user.get(s(p.operation.actor_id)) ?? '',
        lines: p.movements.map(m => ({ item: itemName.get(s(m.item_id)) ?? 'Insumo', dimension: s(m.dimension), delta: n(m.delta_atomic) })) };
    }),
  };
}

export async function recipes(q: Query, branch: string | null) {
  const [formulas, profiles, items, products, measures] = await Promise.all([
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'formula' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'production_profile' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT entity_id AS id, payload->'item'->>'name' AS name FROM entity_state WHERE entity = 'inventory_item'`, []),
    q(`SELECT entity_id AS id, payload->'product'->>'name' AS name FROM entity_state WHERE entity = 'product'`, []),
    units(q),
  ]);
  const item = new Map(items.map(i => [s(i.id), s(i.name)]));
  const product = new Map(products.map(p => [s(p.id), s(p.name)]));
  const formulaName = new Map<string, string>();
  const line = (l: Row) => ({ item: item.get(s(l.item_id)) ?? 'Insumo', dimension: s(l.dimension), atomic: n(l.quantity_atomic) });
  const formulaList = formulas.map(x => {
    const p = x.payload as { formula: Row; versions: Row[]; ingredients: Row[]; groups: Row[]; options: Row[] };
    const active = p.versions.find(v => s(v.id) === s(p.formula.active_version_id)) ?? p.versions[p.versions.length - 1];
    for (const v of p.versions) formulaName.set(s(v.id), s(p.formula.name));
    return { id: s(p.formula.id), code: s(p.formula.code), name: s(p.formula.name), versions: p.versions.length,
      active: active ? { version: n(active.version), name: s(active.name), state: s(active.state), yieldDimension: s(active.yield_dimension), yieldAtomic: n(active.yield_atomic), notes: s(active.notes),
        ingredients: p.ingredients.filter(i => s(i.version_id) === s(active.id)).map(line),
        alternatives: p.groups.filter(g => s(g.version_id) === s(active.id)).map(g => ({ name: s(g.name),
          options: p.options.filter(o => s(o.version_id) === s(active.id) && s(o.group_id) === s(g.id)).map(line) })) } : null };
  });
  return {
    measures, formulas: formulaList.sort((a, b) => a.name.localeCompare(b.name)),
    profiles: profiles.map(x => {
      const p = x.payload as { profile: Row; versions: Row[]; directIngredients: Row[]; formulaRequirements: Row[] };
      const active = p.versions.find(v => s(v.id) === s(p.profile.active_version_id)) ?? p.versions[p.versions.length - 1];
      return { id: s(p.profile.id), product: product.get(s(p.profile.product_id)) ?? 'Producto', finishedItem: item.get(s(p.profile.finished_item_id)) ?? '',
        active: active ? { version: n(active.version), name: s(active.name), state: s(active.state),
          ingredients: p.directIngredients.filter(i => s(i.profile_version_id) === s(active.id)).map(line),
          formulas: p.formulaRequirements.filter(f => s(f.profile_version_id) === s(active.id)).map(f => ({ formula: formulaName.get(s(f.formula_version_id)) ?? 'Preparación', dimension: s(f.dimension), atomic: n(f.quantity_atomic) })) } : null };
    }).sort((a, b) => a.product.localeCompare(b.product)),
  };
}

export async function production(q: Query, r: Range) {
  const k = await names(q);
  const rows = await q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'production_batch' AND ($4::uuid IS NULL OR branch_id = $4::uuid)
    AND (((payload->'batch'->>'created_utc')::timestamptz AT TIME ZONE $1)::date) BETWEEN $2::date AND $3::date
    ORDER BY payload->'batch'->>'created_utc' DESC LIMIT 300`, [r.timeZone, r.from, r.to, r.branchId]);
  return rows.map(x => {
    const p = x.payload as { batch: Row; outputs: Row[]; inputs: Row[] };
    return { id: s(p.batch.id), status: s(p.batch.status), at: s(p.batch.confirmed_utc || p.batch.created_utc), notes: s(p.batch.notes), by: k.user.get(s(p.batch.created_by)) ?? '',
      outputs: p.outputs.map(o => ({ product: s(o.product_name), planned: n(o.planned_count), actual: o.actual_count === null ? null : n(o.actual_count) })),
      inputs: p.inputs.map(i => ({ item: s(i.item_name), dimension: s(i.dimension), atomic: n(i.required_atomic) })) };
  });
}

// ---------- Usuarios y cajas ----------

export async function users(q: Query, branch: string | null) {
  const k = await names(q);
  const rows = await q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'app_user' AND NOT deleted AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]);
  // Nunca el hash: sólo lo que se ve en la pantalla Usuarios de la caja.
  return rows.map(x => { const p = x.payload as Row; return { id: s(p.id), branchId: s(x.branch_id), branch: k.branch.get(s(x.branch_id)) ?? '', username: s(p.username),
    name: s(p.display_name), role: s(p.role_name), active: n(p.active) === 1, mustChangePassword: n(p.must_change_password) === 1, createdAt: s(p.created_utc) }; })
    .sort((a, b) => a.branch.localeCompare(b.branch) || a.name.localeCompare(b.name));
}
