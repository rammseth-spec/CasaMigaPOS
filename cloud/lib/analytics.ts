// Ganancia estimada y análisis de charolas (docs/PROFIT.md). Sólo lectura sobre entity_state.
// Costo de cada pan = su receta (perfil de producción publicado): ingredientes directos + preparaciones, a costo de referencia
// de cada insumo. Política de redondeo: el costo por pieza se redondea al centavo (mitad hacia arriba) y los totales son
// costo por pieza × piezas; son estimaciones para decidir, no contabilidad.
import type { Query, Range, Row } from './panel.js';
import { REFUNDS, SALES } from './panel.js';

const n = (v: unknown) => (v === null || v === undefined ? 0 : Number(v));
const s = (v: unknown) => (v === null || v === undefined ? '' : String(v));
const cents = (v: number) => Math.round(v);

export interface ProductCost { productId: string; branchId: string; name: string; cost: number | null; missing: string[]; hasRecipe: boolean }

type Line = { item_id: string; quantity_atomic: number };

/** Costo estimado por pieza de cada producto con receta publicada; null si falta receta o el costo de algún insumo. */
export async function productCosts(q: Query, branch: string | null): Promise<Map<string, ProductCost>> {
  const [items, formulas, profiles, products] = await Promise.all([
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'inventory_item' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'formula' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'production_profile' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
    q(`SELECT branch_id, entity_id AS id, payload->'product'->>'name' AS name FROM entity_state WHERE entity = 'product' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [branch]),
  ]);
  const key = (b: unknown, id: unknown) => `${s(b)}/${s(id)}`;
  // Costo por unidad atómica (centavos por mg, µL o pieza).
  const perAtomic = new Map<string, number>(); const itemName = new Map<string, string>();
  for (const x of items) {
    const p = x.payload as { item: Row; cost: Row | null };
    itemName.set(key(x.branch_id, p.item.id), s(p.item.name));
    if (p.cost && n(p.cost.per_atomic) > 0) perAtomic.set(key(x.branch_id, p.item.id), n(p.cost.cost_cents) / n(p.cost.per_atomic));
  }
  const lineCost = (branchId: string, lines: Line[], missing: Set<string>) => lines.reduce((a, l) => {
    const c = perAtomic.get(key(branchId, l.item_id));
    if (c === undefined) { missing.add(itemName.get(key(branchId, l.item_id)) ?? 'Insumo'); return a; }
    return a + c * n(l.quantity_atomic);
  }, 0);
  // Preparación: costo por unidad atómica de lo que rinde (ingredientes + la opción preferida de cada grupo de alternativas).
  const formulaPerAtomic = new Map<string, { cost: number; missing: Set<string> }>();
  for (const x of formulas) {
    const p = x.payload as { versions: Row[]; ingredients: Row[]; groups: Row[]; options: Row[] };
    for (const v of p.versions) {
      const missing = new Set<string>();
      const ingredients = p.ingredients.filter(i => s(i.version_id) === s(v.id)) as unknown as Line[];
      const preferred = p.groups.filter(g => s(g.version_id) === s(v.id)).map(g => p.options
        .filter(o => s(o.version_id) === s(v.id) && s(o.group_id) === s(g.id))
        .sort((a, b) => (n(a.preference_rank) || 1e9) - (n(b.preference_rank) || 1e9) || n(a.sequence) - n(b.sequence))[0]).filter(Boolean) as unknown as Line[];
      const total = lineCost(s(x.branch_id), [...ingredients, ...preferred], missing);
      formulaPerAtomic.set(key(x.branch_id, v.id), { cost: total / Math.max(1, n(v.yield_atomic)), missing });
    }
  }
  const result = new Map<string, ProductCost>();
  for (const pr of products) result.set(key(pr.branch_id, pr.id), { productId: s(pr.id), branchId: s(pr.branch_id), name: s(pr.name), cost: null, missing: [], hasRecipe: false });
  for (const x of profiles) {
    const p = x.payload as { profile: Row; versions: Row[]; directIngredients: Row[]; formulaRequirements: Row[] };
    const version = p.versions.find(v => s(v.id) === s(p.profile.active_version_id) && s(v.state) === 'PUBLISHED')
      ?? [...p.versions].filter(v => s(v.state) === 'PUBLISHED').sort((a, b) => n(b.version) - n(a.version))[0];
    const target = result.get(key(x.branch_id, p.profile.product_id));
    if (!version || !target) continue;
    const missing = new Set<string>();
    let total = lineCost(s(x.branch_id), p.directIngredients.filter(d => s(d.profile_version_id) === s(version.id)) as unknown as Line[], missing);
    for (const r of p.formulaRequirements.filter(f => s(f.profile_version_id) === s(version.id))) {
      const f = formulaPerAtomic.get(key(x.branch_id, r.formula_version_id));
      if (!f) { missing.add('Preparación'); continue; }
      f.missing.forEach(m => missing.add(m));
      total += f.cost * n(r.quantity_atomic);
    }
    target.hasRecipe = true;
    target.missing = [...missing].sort();
    target.cost = missing.size ? null : cents(total);
  }
  return result;
}

/** Mostrador contra charolas en el periodo: ingreso, piezas, costo estimado y ganancia, en total y por producto. */
export async function profit(q: Query, r: Range) {
  const costs = await productCosts(q, r.branchId);
  const costOf = (branch: unknown, product: unknown) => costs.get(`${s(branch)}/${s(product)}`)?.cost ?? null;
  type P = { productId: string; branchId: string; name: string; counterPieces: number; counterRevenue: number; trayPieces: number; trayRevenue: number; returned: number; unitCost: number | null };
  const rows = new Map<string, P>();
  const at = (branch: string, product: string, name: string) => {
    const k = `${branch}/${product}`;
    if (!rows.has(k)) rows.set(k, { productId: product, branchId: branch, name, counterPieces: 0, counterRevenue: 0, trayPieces: 0, trayRevenue: 0, returned: 0, unitCost: costOf(branch, product) });
    return rows.get(k)!;
  };
  // Mostrador: lo vendido menos lo devuelto (por partida de la venta).
  const sold = await q(`WITH s AS (${SALES})
    SELECT s.branch_id, i->>'product_id' AS product_id, i->>'product_name' AS name, (i->>'position')::int AS position, s.sale_id,
      (i->>'quantity')::bigint AS quantity, (i->>'line_total_cents')::bigint AS total
    FROM s, jsonb_array_elements(s.payload->'items') i
    WHERE s.day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR s.branch_id = $4::uuid)`, [r.timeZone, r.from, r.to, r.branchId]);
  const position = new Map<string, Row>();
  for (const x of sold) {
    position.set(`${s(x.sale_id)}/${n(x.position)}`, x);
    const p = at(s(x.branch_id), s(x.product_id), s(x.name)); p.counterPieces += n(x.quantity); p.counterRevenue += n(x.total);
  }
  const refunded = await q(`WITH r AS (${REFUNDS})
    SELECT r.sale_id, (i->>'position')::int AS position, (i->>'quantity')::bigint AS quantity, (i->>'line_total_cents')::bigint AS total
    FROM r, jsonb_array_elements(r.payload->'items') i WHERE r.day BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR r.branch_id = $4::uuid)`,
    [r.timeZone, r.from, r.to, r.branchId]);
  for (const x of refunded) {
    const line = position.get(`${s(x.sale_id)}/${n(x.position)}`);
    if (!line) continue; // devolución de una venta de otro periodo: no se mezcla
    const p = at(s(line.branch_id), s(line.product_id), s(line.name)); p.counterPieces -= n(x.quantity); p.counterRevenue -= n(x.total);
  }
  // Charolas: lo cobrado de las recogidas en el periodo; lo que regresa se cuenta como costo perdido.
  const trays = await q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_delivery' AND payload->'settlement' IS NOT NULL AND payload->'settlement' <> 'null'::jsonb
    AND (((payload->'settlement'->>'collected_utc')::timestamptz AT TIME ZONE $1)::date) BETWEEN $2::date AND $3::date AND ($4::uuid IS NULL OR branch_id = $4::uuid)`,
    [r.timeZone, r.from, r.to, r.branchId]);
  for (const x of trays) {
    const p = x.payload as { items: Row[]; settlementItems: Row[] };
    for (const item of p.items) {
      const settled = p.settlementItems.find(si => n(si.position) === n(item.position));
      if (!settled) continue;
      const row = at(s(x.branch_id), s(item.product_id), s(item.product_name));
      row.trayPieces += n(settled.sold); row.trayRevenue += n(settled.line_total_cents); row.returned += n(settled.returned);
    }
  }
  const products = [...rows.values()].map(p => {
    const c = p.unitCost;
    return { ...p,
      counterProfit: c === null ? null : p.counterRevenue - c * p.counterPieces,
      trayProfit: c === null ? null : p.trayRevenue - c * (p.trayPieces + p.returned),
      perPieceCounter: c === null || p.counterPieces === 0 ? null : Math.round(p.counterRevenue / p.counterPieces) - c,
      perPieceTray: c === null || p.trayPieces === 0 ? null : Math.round(p.trayRevenue / p.trayPieces) - c,
    };
  }).sort((a, b) => (b.counterPieces + b.trayPieces) - (a.counterPieces + a.trayPieces) || a.name.localeCompare(b.name));
  const channel = (pieces: (p: typeof products[number]) => number, revenue: (p: typeof products[number]) => number, cost: (p: typeof products[number]) => number) => {
    const withCost = products.filter(p => p.unitCost !== null);
    const rev = products.reduce((a, p) => a + revenue(p), 0);
    const covered = withCost.reduce((a, p) => a + revenue(p), 0);
    const c = withCost.reduce((a, p) => a + cost(p), 0);
    return { pieces: products.reduce((a, p) => a + pieces(p), 0), revenue: rev, coveredRevenue: covered, cost: c, profit: covered - c, margin: covered ? (covered - c) / covered : null };
  };
  return {
    counter: channel(p => p.counterPieces, p => p.counterRevenue, p => (p.unitCost ?? 0) * p.counterPieces),
    trays: { ...channel(p => p.trayPieces, p => p.trayRevenue, p => (p.unitCost ?? 0) * (p.trayPieces + p.returned)), returned: products.reduce((a, p) => a + p.returned, 0),
      returnedCost: products.reduce((a, p) => a + (p.unitCost ?? 0) * p.returned, 0) },
    products,
    costs: [...costs.values()].filter(c => r.branchId === null || c.branchId === r.branchId).sort((a, b) => a.name.localeCompare(b.name)),
  };
}

/** Por tienda: frecuencia de pedido, pan que más se vende allí, porcentaje vendido y lo que regresa. */
export async function trayAnalytics(q: Query, r: Range) {
  const [stores, deliveries] = await Promise.all([
    q(`SELECT branch_id, payload->'store' AS store FROM entity_state WHERE entity = 'consignment_store' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [r.branchId]),
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'consignment_delivery' AND ($1::uuid IS NULL OR branch_id = $1::uuid)`, [r.branchId]),
  ]);
  const local = (iso: string) => new Date(iso).toLocaleDateString('en-CA', { timeZone: r.timeZone });
  const weekday = (iso: string) => Number(new Date(local(iso) + 'T12:00:00Z').getUTCDay());
  const names = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];
  const byStore = new Map<string, { delivered: string[]; products: Map<string, { name: string; delivered: number; sold: number; returned: number; revenue: number }>; sold: number; returned: number; deliveredPieces: number; revenue: number }>();
  for (const d of deliveries) {
    const p = d.payload as { delivery: Row; items: Row[]; settlement: Row | null; settlementItems: Row[]; void: Row | null };
    if (p.void) continue;
    const day = local(s(p.delivery.delivered_utc));
    if (day < r.from || day > r.to) continue;
    const id = s(p.delivery.store_id);
    const st = byStore.get(id) ?? { delivered: [] as string[], products: new Map(), sold: 0, returned: 0, deliveredPieces: 0, revenue: 0 };
    byStore.set(id, st);
    st.delivered.push(s(p.delivery.delivered_utc));
    for (const item of p.items) {
      const settled = p.settlementItems.find(si => n(si.position) === n(item.position));
      const prod = st.products.get(s(item.product_id)) ?? { name: s(item.product_name), delivered: 0, sold: 0, returned: 0, revenue: 0 };
      st.products.set(s(item.product_id), prod);
      prod.delivered += n(item.quantity); st.deliveredPieces += n(item.quantity);
      if (settled) { prod.sold += n(settled.sold); prod.returned += n(settled.returned); prod.revenue += n(settled.line_total_cents); st.sold += n(settled.sold); st.returned += n(settled.returned); st.revenue += n(settled.line_total_cents); }
    }
  }
  return stores.map(x => {
    const store = x.store as Row; const id = s(store.id);
    const st = byStore.get(id);
    const dates = (st?.delivered ?? []).sort();
    const gaps = dates.slice(1).map((d, i) => (Date.parse(d) - Date.parse(dates[i])) / 86_400_000);
    const days = new Map<number, number>(); for (const d of dates) days.set(weekday(d), (days.get(weekday(d)) ?? 0) + 1);
    const usual = [...days.entries()].sort((a, b) => b[1] - a[1]).slice(0, 3).map(([w]) => names[w]);
    return {
      id, branchId: s(x.branch_id), name: s(store.name), active: n(store.active) === 1, deliveries: dates.length,
      everyDays: gaps.length ? Math.round(gaps.reduce((a, b) => a + b, 0) / gaps.length * 10) / 10 : null, usualDays: usual,
      lastDelivery: dates.length ? dates[dates.length - 1] : null,
      deliveredPieces: st?.deliveredPieces ?? 0, sold: st?.sold ?? 0, returned: st?.returned ?? 0, revenue: st?.revenue ?? 0,
      sellThrough: st && st.sold + st.returned > 0 ? st.sold / (st.sold + st.returned) : null,
      products: [...(st?.products.values() ?? [])].sort((a, b) => b.sold - a.sold || b.delivered - a.delivered).slice(0, 8),
    };
  }).sort((a, b) => b.revenue - a.revenue || a.name.localeCompare(b.name));
}
