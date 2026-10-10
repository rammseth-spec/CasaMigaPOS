// Reportes con las mismas cifras que la caja de escritorio (ReportService, SqliteReportStore y Analytics de QAv1):
// ventas por fecha de venta y devoluciones por fecha de devolución, en días locales de la sucursal; las ventas
// canceladas no cuentan como afluencia ni como demanda; el pronóstico promedia los días que sí hubo venta.
import { REFUNDS, SALES, type Query, type Range } from './panel.js';
import { names } from './web.js';

const n = (v: unknown) => (v === null || v === undefined ? 0 : Number(v));
const s = (v: unknown) => (v === null || v === undefined ? '' : String(v));
const collator = new Intl.Collator('es-MX');
/** Una venta con cancelación registrada (la venta no cambia; la cancelación es un registro aparte). */
const CANCELLED = `EXISTS (SELECT 1 FROM entity_state f WHERE f.entity = 'sale_refund' AND NOT f.deleted
  AND f.payload->'refund'->>'sale_id' = s.sale_id AND f.payload->'refund'->>'kind' = 'CANCELLATION')`;
const METHOD_ORDER = ['CASH', 'CARD', 'TRANSFER'];

export interface ProductRow { productId: string; name: string; quantitySold: number; sold: number; quantityRefunded: number; refunded: number; net: number }
export interface SessionRow { id: string; openedBy: string; openedAt: string; closedAt: string | null; closed: boolean; opening: number; cashSales: number;
  refunds: number; movements: number; expected: number; counted: number | null; difference: number | null }
export interface SalesData {
  saleCount: number; gross: number; refunded: number; net: number; averageTicket: number;
  products: ProductRow[]; cashiers: { userId: string; name: string; sales: number; sold: number }[];
  payments: { method: string; sales: number; sold: number; refunded: number; net: number }[]; sessions: SessionRow[];
}

/** Resumen del periodo (QuerySalesReport). */
export async function salesData(q: Query, r: Range): Promise<SalesData> {
  const p = [r.timeZone, r.from, r.to, r.branchId];
  const inRange = (alias: string) => `${alias}.day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR ${alias}.branch_id = $4::uuid)`;
  const [totals, sold, back, refundedTotal, byUser, byMethod, backByMethod, shifts, k] = await Promise.all([
    q(`WITH s AS (${SALES}) SELECT count(*) AS sales, COALESCE(sum(total), 0) AS gross FROM s WHERE ${inRange('s')}`, p),
    q(`WITH s AS (${SALES})
      SELECT i->>'product_id' AS id, (array_agg(i->>'product_name' ORDER BY (s.payload->'sale'->>'confirmed_utc')::timestamptz DESC))[1] AS name,
        sum((i->>'quantity')::bigint) AS quantity, sum((i->>'line_total_cents')::bigint) AS total
      FROM s, jsonb_array_elements(s.payload->'items') i WHERE ${inRange('s')} GROUP BY 1`, p),
    q(`WITH s AS (${SALES}), r AS (${REFUNDS})
      SELECT i->>'product_id' AS id, max(i->>'product_name') AS name, sum((ri->>'quantity')::bigint) AS quantity, sum((ri->>'line_total_cents')::bigint) AS total
      FROM r JOIN s ON s.sale_id = r.sale_id, jsonb_array_elements(r.payload->'items') ri, jsonb_array_elements(s.payload->'items') i
      WHERE (i->>'position')::int = (ri->>'position')::int AND ${inRange('r')} GROUP BY 1`, p),
    q(`WITH r AS (${REFUNDS}) SELECT COALESCE(sum(total), 0) AS total FROM r WHERE ${inRange('r')}`, p),
    q(`WITH s AS (${SALES}) SELECT s.payload->'sale'->>'user_id' AS user_id, count(*) AS sales, sum(total) AS sold FROM s WHERE ${inRange('s')} GROUP BY 1`, p),
    q(`WITH s AS (${SALES}) SELECT method, count(*) AS sales, sum(applied) AS sold FROM s WHERE ${inRange('s')} GROUP BY 1`, p),
    q(`WITH s AS (${SALES}), r AS (${REFUNDS}) SELECT s.method, sum(r.total) AS total FROM r JOIN s ON s.sale_id = r.sale_id WHERE ${inRange('r')} GROUP BY 1`, p),
    q(`WITH c AS (SELECT branch_id, payload AS c FROM entity_state WHERE entity = 'cash_session' AND NOT deleted),
        s AS (${SALES}), r AS (${REFUNDS}),
        cs AS (SELECT session_id, sum(applied) AS total FROM s WHERE method = 'CASH' GROUP BY 1),
        cr AS (SELECT r.session_id, sum(r.total) AS total FROM r JOIN s ON s.sale_id = r.sale_id WHERE s.method = 'CASH' GROUP BY 1),
        mv AS (SELECT payload->>'cash_session_id' AS session_id, sum(CASE WHEN payload->>'kind' = 'DEPOSIT' THEN 1 ELSE -1 END * (payload->>'amount_cents')::bigint) AS net
          FROM entity_state WHERE entity = 'cash_movement' AND NOT deleted GROUP BY 1),
        cn AS (SELECT payload->>'session_id' AS session_id, max((payload->>'counted_cents')::bigint) AS counted FROM entity_state WHERE entity = 'cash_count' AND NOT deleted GROUP BY 1)
      SELECT c.c->>'id' AS id, c.c->>'opened_by' AS opened_by, c.c->>'opened_utc' AS opened_at, c.c->>'closed_utc' AS closed_at, c.c->>'state' AS state,
        (c.c->>'opening_cents')::bigint AS opening, COALESCE((c.c->>'counted_cents')::bigint, cn.counted) AS counted, cs.total AS cash_sales, cr.total AS refunds, mv.net AS movements
      FROM c LEFT JOIN cs ON cs.session_id = c.c->>'id' LEFT JOIN cr ON cr.session_id = c.c->>'id' LEFT JOIN mv ON mv.session_id = c.c->>'id' LEFT JOIN cn ON cn.session_id = c.c->>'id'
      WHERE (((c.c->>'opened_utc')::timestamptz AT TIME ZONE $1)::date) BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR c.branch_id = $4::uuid)
      ORDER BY (c.c->>'opened_utc')::timestamptz, c.c->>'id'`, p),
    names(q),
  ]);
  const saleCount = n(totals[0].sales), gross = n(totals[0].gross), refunded = n(refundedTotal[0].total);
  const products = new Map<string, ProductRow>();
  for (const x of sold) products.set(s(x.id), { productId: s(x.id), name: s(x.name), quantitySold: n(x.quantity), sold: n(x.total), quantityRefunded: 0, refunded: 0, net: 0 });
  for (const x of back) {
    const row = products.get(s(x.id)) ?? { productId: s(x.id), name: s(x.name), quantitySold: 0, sold: 0, quantityRefunded: 0, refunded: 0, net: 0 };
    row.quantityRefunded += n(x.quantity); row.refunded += n(x.total); products.set(s(x.id), row);
  }
  const productRows = [...products.values()].map(x => ({ ...x, net: x.sold - x.refunded }))
    .sort((a, b) => b.net - a.net || collator.compare(a.name, b.name));
  const cashiers = byUser.map(x => ({ userId: s(x.user_id), name: k.user.get(s(x.user_id)) ?? 'Usuario', sales: n(x.sales), sold: n(x.sold) }))
    .sort((a, b) => b.sold - a.sold || collator.compare(a.name, b.name));
  const refundsByMethod = new Map(backByMethod.map(x => [s(x.method), n(x.total)]));
  const methods = new Set([...byMethod.map(x => s(x.method)), ...refundsByMethod.keys()]);
  const payments = [...methods].map(method => {
    const row = byMethod.find(x => s(x.method) === method);
    const soldCents = n(row?.sold), refundedCents = refundsByMethod.get(method) ?? 0;
    return { method, sales: n(row?.sales), sold: soldCents, refunded: refundedCents, net: soldCents - refundedCents };
  }).sort((a, b) => METHOD_ORDER.indexOf(a.method) - METHOD_ORDER.indexOf(b.method));
  const sessions = shifts.map(x => {
    const opening = n(x.opening), cashSales = n(x.cash_sales), refunds = n(x.refunds), movements = n(x.movements);
    const expected = opening + cashSales - refunds + movements;
    const counted = x.counted === null || x.counted === undefined ? null : n(x.counted);
    return { id: s(x.id), openedBy: k.user.get(s(x.opened_by)) ?? 'Usuario', openedAt: s(x.opened_at), closedAt: x.closed_at ? s(x.closed_at) : null, closed: x.state === 'CLOSED',
      opening, cashSales, refunds, movements, expected, counted, difference: counted === null ? null : counted - expected };
  });
  return { saleCount, gross, refunded, net: gross - refunded, averageTicket: saleCount ? Math.trunc(gross / saleCount) : 0, products: productRows, cashiers, payments, sessions };
}

/** Ventas por día local (sin canceladas), una entrada por día aunque no haya ventas (Analytics.Daily). */
async function daily(q: Query, r: Range) {
  const rows = await q(`WITH s AS (${SALES}) SELECT day::text AS day, count(*) AS sales, sum(total) AS total FROM s
    WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid) AND NOT ${CANCELLED} GROUP BY 1`, [r.timeZone, r.from, r.to, r.branchId]);
  const byDay = new Map(rows.map(x => [s(x.day).slice(0, 10), { sales: n(x.sales), amount: n(x.total) }]));
  const length = Math.round((Date.parse(r.to) - Date.parse(r.from)) / 86_400_000) + 1;
  return Array.from({ length }, (_, i) => { const day = shift(r.from, i); return { day, ...(byDay.get(day) ?? { sales: 0, amount: 0 }) }; });
}
const shift = (day: string, days: number) => { const d = new Date(day + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };

/** Cambio porcentual con un decimal; null sin base (Analytics.Change). */
export function change(current: number, previous: number): number | null {
  return previous === 0 ? null : roundHalfEven((current - previous) * 1000 / Math.abs(previous)) / 10;
}
/** Redondeo al entero más cercano y, en empate, al par (como Math.Round de .NET). */
function roundHalfEven(x: number) {
  const floor = Math.floor(x), diff = x - floor;
  if (Math.abs(diff - 0.5) < 1e-9) return floor % 2 === 0 ? floor : floor + 1;
  return Math.round(x);
}

/** Todo lo de Generar en la caja: Resumen, Comparativo, Ventas por hora y Control de caja del periodo. */
export async function report(q: Query, r: Range) {
  const length = Math.round((Date.parse(r.to) - Date.parse(r.from)) / 86_400_000) + 1;
  const previous: Range = { ...r, from: shift(r.from, -length), to: shift(r.from, -1) };
  const [current, before, days, previousDays, heat, refundFacts] = await Promise.all([
    salesData(q, r), salesData(q, previous), daily(q, r), daily(q, previous),
    q(`WITH s AS (${SALES})
      SELECT extract(isodow FROM (payload->'sale'->>'confirmed_utc')::timestamptz AT TIME ZONE $1)::int AS weekday,
        extract(hour FROM (payload->'sale'->>'confirmed_utc')::timestamptz AT TIME ZONE $1)::int AS hour, count(*) AS sales, sum(total) AS total
      FROM s WHERE day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid) AND NOT ${CANCELLED} GROUP BY 1, 2`, [r.timeZone, r.from, r.to, r.branchId]),
    q(`WITH s AS (${SALES}), r AS (${REFUNDS})
      SELECT r.payload->'refund'->>'reason' AS reason, r.total, s.payload->'sale'->>'user_id' AS cashier
      FROM r JOIN s ON s.sale_id = r.sale_id WHERE r.day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR r.branch_id = $4::uuid)
      ORDER BY r.payload->'refund'->>'refunded_utc'`, [r.timeZone, r.from, r.to, r.branchId]),
  ]);
  const k = await names(q);

  // Comparativo por producto (Analytics.CompareProducts): piezas netas y neto.
  const map = new Map<string, { name: string; quantity: number; net: number; previousQuantity: number; previousNet: number }>();
  for (const x of current.products) map.set(x.productId, { name: x.name, quantity: x.quantitySold - x.quantityRefunded, net: x.net, previousQuantity: 0, previousNet: 0 });
  for (const x of before.products) {
    const row = map.get(x.productId) ?? { name: x.name, quantity: 0, net: 0, previousQuantity: 0, previousNet: 0 };
    row.previousQuantity = x.quantitySold - x.quantityRefunded; row.previousNet = x.net; map.set(x.productId, row);
  }
  const products = [...map.values()].map(x => ({ ...x, quantityChange: change(x.quantity, x.previousQuantity), netChange: change(x.net, x.previousNet) }))
    .sort((a, b) => b.net - a.net || b.previousNet - a.previousNet || collator.compare(a.name, b.name));

  // Ventas por hora (Analytics.Heatmap): lunes a domingo, al menos de 7:00 a 20:00.
  const hours = heat.map(x => n(x.hour));
  const firstHour = Math.min(7, ...(hours.length ? hours : [7])), lastHour = Math.max(20, ...(hours.length ? hours : [20]));
  const cell = new Map(heat.map(x => [`${n(x.weekday)}/${n(x.hour)}`, { sales: n(x.sales), amount: n(x.total) }]));
  const rows = [1, 2, 3, 4, 5, 6, 7].map(weekday => ({ weekday, cells: Array.from({ length: lastHour - firstHour + 1 }, (_, i) => ({ hour: firstHour + i, ...(cell.get(`${weekday}/${firstHour + i}`) ?? { sales: 0, amount: 0 }) })) }));

  // Control de caja (Analytics.CashControl): diferencias por quien abrió; devoluciones por quien cobró la venta original.
  const shifts = current.sessions.map(x => ({ openedBy: x.openedBy, closed: x.closed, difference: x.difference }));
  const refunds = refundFacts.map(x => ({ reason: s(x.reason).trim(), total: n(x.total), cashier: k.user.get(s(x.cashier)) ?? 'Usuario' }));
  const people = [...new Set([...shifts.map(x => x.openedBy), ...refunds.map(x => x.cashier)])];
  const cashiers = people.map(name => {
    const own = shifts.filter(x => x.openedBy === name);
    const counted = own.filter(x => x.closed && x.difference !== null).map(x => x.difference as number);
    const theirs = refunds.filter(x => x.cashier === name);
    const shortages = -counted.filter(d => d < 0).reduce((a, b) => a + b, 0), overages = counted.filter(d => d > 0).reduce((a, b) => a + b, 0);
    return { name, shifts: own.length, closedShifts: counted.length, withDifference: counted.filter(d => d !== 0).length, shortages, overages, net: overages - shortages,
      refunds: theirs.length, refunded: theirs.reduce((a, x) => a + x.total, 0) };
  }).sort((a, b) => b.shortages - a.shortages || b.refunded - a.refunded || collator.compare(a.name, b.name));
  const reasons = new Map<string, { reason: string; count: number; amount: number }>();
  for (const x of refunds) {
    const key = x.reason.toLocaleLowerCase('es-MX');
    const row = reasons.get(key) ?? { reason: x.reason, count: 0, amount: 0 };
    row.count++; row.amount += x.total; reasons.set(key, row);
  }

  return {
    from: r.from, to: r.to, previousFrom: previous.from, previousTo: previous.to,
    summary: current, previous: { saleCount: before.saleCount, net: before.net, averageTicket: before.averageTicket, sessions: before.sessions.length },
    comparison: { salesChange: change(current.saleCount, before.saleCount), netChange: change(current.net, before.net), ticketChange: change(current.averageTicket, before.averageTicket), products },
    days, previousDays,
    heatmap: { firstHour, lastHour, rows },
    cashControl: { cashiers, reasons: [...reasons.values()].sort((a, b) => b.count - a.count || b.amount - a.amount || collator.compare(a.reason, b.reason)) },
  };
}

/** Piezas sugeridas para un día (Analytics.Forecast): mismo día de las 4 semanas anteriores, sólo días con venta y productos activos, +10 %. */
export async function forecast(q: Query, branch: string | null, timeZone: string, target: string, weeks = 4) {
  const days = Array.from({ length: weeks }, (_, i) => shift(target, -7 * (i + 1)));
  const rows = await q(`WITH s AS (${SALES})
    SELECT i->>'product_id' AS id, i->>'product_name' AS name, s.day::text AS day, (s.payload->'sale'->>'confirmed_utc')::timestamptz AS at, (i->>'quantity')::bigint AS quantity
    FROM s CROSS JOIN LATERAL jsonb_array_elements(s.payload->'items') i
    JOIN entity_state p ON p.entity = 'product' AND p.entity_id = i->>'product_id' AND p.branch_id = s.branch_id AND (p.payload->'product'->>'active')::int = 1
    WHERE s.day = ANY($2::date[]) AND ($3::uuid IS NULL OR s.branch_id = $3::uuid) AND NOT ${CANCELLED}`, [timeZone, days, branch]);
  const openDays = new Set(rows.map(x => s(x.day).slice(0, 10))).size;
  const last = days[0];
  const byProduct = new Map<string, { name: string; at: number; total: number; last: number; days: Set<string> }>();
  for (const x of rows) {
    const id = s(x.id), at = new Date(s(x.at)).getTime(), day = s(x.day).slice(0, 10);
    const row = byProduct.get(id) ?? { name: s(x.name), at: -Infinity, total: 0, last: 0, days: new Set<string>() };
    if (at > row.at) { row.at = at; row.name = s(x.name); }
    row.total += n(x.quantity); if (day === last) row.last += n(x.quantity); row.days.add(day);
    byProduct.set(id, row);
  }
  const products = [...byProduct.entries()].map(([productId, x]) => {
    const tenths = roundHalfEven(x.total * 10 / openDays);
    return { productId, name: x.name, last: x.last, average: tenths / 10, suggested: Math.ceil(tenths * 11 / 100 - 1e-9), daysWithSales: x.days.size };
  }).sort((a, b) => b.suggested - a.suggested || collator.compare(a.name, b.name));
  return { target, weeks, weeksWithData: openDays, products, total: products.reduce((a, x) => a + x.suggested, 0) };
}
