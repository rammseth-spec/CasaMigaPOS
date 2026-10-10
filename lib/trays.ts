// Charolas como la caja (ConsignmentService, docs/PLAN_COMPLETAR.md W7): estado de cuenta de una tienda y reporte del periodo,
// con los mismos cálculos (charolas entregadas en el periodo sin anuladas, pagos recibidos en el periodo, saldo a hoy).
import { ProtocolError } from './protocol.js';
import { isUuid } from './web.js';
import type { Query, Range, Row } from './panel.js';

const s = (v: unknown) => (v === null || v === undefined ? '' : String(v));
const n = (v: unknown) => Number(v ?? 0);
type Delivery = { delivery: Row; items: Row[]; settlement: Row | null; settlementItems: Row[]; void: Row | null };
const METHOD: Record<string, string> = { CASH: 'efectivo', CARD: 'tarjeta', TRANSFER: 'transferencia' };

/** Math.Round de .NET con un decimal (al par más cercano en el medio), para «% de venta». */
function sellThrough(sold: number, returned: number): number | null {
  if (sold + returned === 0) return null;
  const x = (sold * 1000) / (sold + returned); // décimas de punto porcentual
  const floor = Math.floor(x), diff = x - floor;
  const rounded = diff > 0.5 + 1e-9 ? floor + 1 : diff < 0.5 - 1e-9 ? floor : (floor % 2 === 0 ? floor : floor + 1);
  return rounded / 10;
}

async function load(q: Query, branch: string | null) {
  const [deliveries, payments, stores] = await Promise.all([
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_delivery' AND NOT deleted AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_payment' AND NOT deleted AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_store' AND NOT deleted AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
  ]);
  return {
    deliveries: deliveries.map(d => d.payload as Delivery),
    payments: payments.map(p => p.payload as Row),
    stores: stores.map(x => ({ branchId: s(x.branch_id), ...(x.payload as { store: Row; template: Row[] }) })),
  };
}
const balanceOf = (data: Awaited<ReturnType<typeof load>>, store: string) =>
  data.deliveries.filter(d => s(d.delivery.store_id) === store && d.settlement && !d.void).reduce((a, d) => a + n(d.settlement!.sold_cents), 0)
  - data.payments.filter(p => s(p.store_id) === store).reduce((a, p) => a + n(p.amount_cents), 0);

/** «Estado de cuenta»: cargos al recoger y pagos, del más reciente al más antiguo, con el saldo a hoy. */
export async function trayAccount(q: Query, branch: string | null, storeId: string, timeZone: string) {
  if (!isUuid(storeId)) throw new ProtocolError('Tienda inválida');
  const data = await load(q, branch);
  const store = data.stores.find(x => s(x.store.id) === storeId.toLowerCase());
  if (!store) throw new ProtocolError('No se encontró la tienda.', 404);
  // «ddd d MMM» de la caja (lun 5 oct), sin el «de» ni los puntos que agrega el navegador.
  const day = (iso: string) => { const part = (type: string) => new Intl.DateTimeFormat('es-MX', { timeZone, weekday: 'short', day: 'numeric', month: 'short' })
    .formatToParts(new Date(iso)).find(x => x.type === type)?.value.replace(/\./g, '') ?? ''; return `${part('weekday')} ${part('day')} ${part('month')}`; };
  const charges = data.deliveries.filter(d => s(d.delivery.store_id) === s(store.store.id) && d.settlement).map(d => ({
    at: s(d.settlement!.collected_utc),
    concept: `Charola del ${day(s(d.delivery.delivered_utc))}: ${d.settlementItems.reduce((a, i) => a + n(i.sold), 0)} vendidas, ${d.settlementItems.reduce((a, i) => a + n(i.returned), 0)} regresaron`,
    charge: n(d.settlement!.sold_cents), payment: 0 }));
  const payments = data.payments.filter(p => s(p.store_id) === s(store.store.id)).map(p => ({
    at: s(p.received_utc), concept: `Pago con ${METHOD[s(p.method)] ?? s(p.method).toLowerCase()}` + (s(p.note) ? ` · ${s(p.note)}` : ''), charge: 0, payment: n(p.amount_cents) }));
  return { storeId: s(store.store.id), movements: [...charges, ...payments].sort((a, b) => b.at.localeCompare(a.at)), balance: balanceOf(data, s(store.store.id)) };
}

/** «Reporte» (ConsignmentService.Summarize): por tienda y por pan, en el periodo; saldo a hoy por tienda. */
export async function trayReport(q: Query, r: Range) {
  const data = await load(q, r.branchId);
  const local = (iso: string) => new Date(iso).toLocaleDateString('en-CA', { timeZone: r.timeZone });
  const inRange = (iso: string) => { const d = local(iso); return d >= r.from && d <= r.to; };
  const valid = data.deliveries.filter(d => !d.void && inRange(s(d.delivery.delivered_utc)));
  const paid = new Map<string, number>();
  for (const p of data.payments.filter(p => inRange(s(p.received_utc)))) paid.set(s(p.store_id), (paid.get(s(p.store_id)) ?? 0) + n(p.amount_cents));
  const names = new Map(data.stores.map(x => [s(x.store.id), s(x.store.name)]));
  const settled = (d: Delivery) => d.items.map(i => ({ item: i, settled: d.settlementItems.find(x => n(x.position) === n(i.position)) ?? null }));
  const storeIds = [...new Set([...valid.map(d => s(d.delivery.store_id)), ...paid.keys()])];
  const stores = storeIds.map(id => {
    const own = valid.filter(d => s(d.delivery.store_id) === id);
    const collected = own.filter(d => d.settlement);
    const lines = collected.flatMap(settled).filter(l => l.settled);
    const sold = lines.reduce((a, l) => a + n(l.settled!.sold), 0), returned = lines.reduce((a, l) => a + n(l.settled!.returned), 0);
    const charged = lines.reduce((a, l) => a + n(l.settled!.line_total_cents), 0);
    const retail = lines.reduce((a, l) => a + n(l.item.retail_price_cents) * n(l.settled!.sold), 0);
    const storeName = names.get(id) ?? '';
    return { storeId: id, storeName, trays: own.length, delivered: own.reduce((a, d) => a + d.items.reduce((b, i) => b + n(i.quantity), 0), 0), collected: collected.length,
      sold, returned, atStore: own.filter(d => !d.settlement).reduce((a, d) => a + d.items.reduce((b, i) => b + n(i.quantity), 0), 0),
      charged, retail, paid: paid.get(id) ?? 0, balance: balanceOf(data, id), sellThrough: sellThrough(sold, returned) };
  }).filter(x => x.storeName.length > 0 || x.paid > 0)
    .sort((a, b) => b.charged - a.charged || a.storeName.localeCompare(b.storeName, 'es'));
  const byProduct = new Map<string, { productId: string; productName: string; delivered: number; sold: number; returned: number; charged: number; retail: number }>();
  for (const d of valid) for (const { item, settled: x } of settled(d)) {
    const id = s(item.product_id);
    const row = byProduct.get(id) ?? { productId: id, productName: '', delivered: 0, sold: 0, returned: 0, charged: 0, retail: 0 };
    row.productName = s(item.product_name); row.delivered += n(item.quantity); row.sold += n(x?.sold); row.returned += n(x?.returned);
    row.charged += n(x?.line_total_cents); row.retail += n(item.retail_price_cents) * n(x?.sold);
    byProduct.set(id, row);
  }
  const products = [...byProduct.values()].map(p => ({ ...p, sellThrough: sellThrough(p.sold, p.returned) }))
    .sort((a, b) => b.sold - a.sold || a.productName.localeCompare(b.productName, 'es'));
  const sum = (key: 'trays' | 'delivered' | 'sold' | 'returned' | 'atStore' | 'charged' | 'retail' | 'paid' | 'balance') => stores.reduce((a, x) => a + x[key], 0);
  return { from: r.from, to: r.to, stores, products,
    totals: { trays: sum('trays'), delivered: sum('delivered'), sold: sum('sold'), returned: sum('returned'), atStore: sum('atStore'), charged: sum('charged'),
      retail: sum('retail'), paid: sum('paid'), balance: sum('balance'), leftToStores: sum('retail') - sum('charged'), sellThrough: sellThrough(sum('sold'), sum('returned')) } };
}
