// «Calcular necesidades» de Producción, igual que la caja (ProductionPlan.Calculate y ProductionScaling, docs/PLAN_COMPLETAR.md W6):
// cantidades exactas con fracciones (sin redondeos de punto flotante), alternativas por preferencia y existencias del almacén principal.
// Sólo consulta: no reserva ni descuenta nada. La caja vuelve a calcular y a revisar existencias al preparar y al registrar.
import { ProtocolError } from './protocol.js';
import { MAIN_LOCATION, isUuid, recipes, names } from './web.js';
import type { Query, Row } from './panel.js';

type Line = { itemId: string; dimension: string; atomic: number; policy: string };
type Ratio = { num: bigint; den: bigint };
export type Selection = { formulaVersionId: string; groupId: string; optionId: string };
export type PlanRequest = { outputs: { profileVersionId: string; planned: number }[]; selections: Selection[] };

const gcd = (a: bigint, b: bigint): bigint => { while (b !== 0n) [a, b] = [b, a % b]; return a < 0n ? -a : a; };
const ratio = (num: bigint, den: bigint): Ratio => { const g = gcd(num, den); return { num: num / g, den: den / g }; };
const LIMIT = 9_223_372_036_854_775_807n;

/** ProductionScaling.Scale: COUNT con política; masa y volumen sólo exactos. */
function scale(line: Line, factor: Ratio): { quantity: number | null; error: string | null; exact: Ratio } {
  const exact = ratio(factor.num * BigInt(line.atomic), factor.den);
  if (exact.num > LIMIT) return { quantity: null, error: 'Overflow', exact };
  if (line.dimension === 'COUNT' && line.policy === 'Unspecified') return { quantity: null, error: 'CountPolicyRequired', exact };
  if (exact.den === 1n) return { quantity: Number(exact.num), error: null, exact };
  if (line.dimension !== 'COUNT') return { quantity: null, error: 'NonRepresentableScale', exact };
  if (line.policy === 'ExactWholeCount') return { quantity: null, error: 'AlternativeNotEligible', exact };
  return { quantity: Number(exact.num / exact.den + 1n), error: null, exact };
}

export function parsePlan(text: string | null): PlanRequest {
  let raw: unknown;
  try { raw = JSON.parse(text ?? ''); } catch { throw new ProtocolError('Plan inválido'); }
  const b = (raw ?? {}) as { outputs?: unknown; selections?: unknown };
  if (!Array.isArray(b.outputs) || b.outputs.length === 0 || b.outputs.length > 20) throw new ProtocolError('Agrega de 1 a 20 productos');
  const outputs = b.outputs.map((o: unknown) => {
    const x = (o ?? {}) as Record<string, unknown>;
    if (!isUuid(x.profileVersionId)) throw new ProtocolError('Perfil inválido');
    if (typeof x.planned !== 'number' || !Number.isSafeInteger(x.planned) || x.planned < 1 || x.planned > 100_000) throw new ProtocolError('La cantidad planeada debe ser un entero mayor que cero.');
    return { profileVersionId: x.profileVersionId.toLowerCase(), planned: x.planned };
  });
  if (new Set(outputs.map(o => o.profileVersionId)).size !== outputs.length) throw new ProtocolError('No repitas el mismo producto en el plan.');
  const selections = (Array.isArray(b.selections) ? b.selections : []).slice(0, 50).map((s: unknown) => {
    const x = (s ?? {}) as Record<string, unknown>;
    if (!isUuid(x.formulaVersionId) || !isUuid(x.groupId) || !isUuid(x.optionId)) throw new ProtocolError('Alternativa inválida');
    return { formulaVersionId: x.formulaVersionId.toLowerCase(), groupId: x.groupId.toLowerCase(), optionId: x.optionId.toLowerCase() };
  });
  return { outputs, selections };
}

/** Vista previa de necesidades de un plan (ProductionService.PrepareProduction). */
export async function productionPlan(q: Query, branch: string, request: PlanRequest) {
  const data = await recipes(q, branch);
  const items = new Map((await q(`SELECT entity_id AS id, payload FROM entity_state WHERE entity = 'inventory_item' AND branch_id = $1 AND NOT deleted`, [branch]))
    .map(r => { const p = r.payload as { item: Row; balances: Row[] };
      const main = p.balances.find(b => String(b.location_id) === MAIN_LOCATION);
      return [String(r.id), { name: String(p.item.name), dimension: String(p.item.dimension), active: Number(p.item.active) === 1, available: main ? Number(main.quantity_atomic) : null }]; }));
  const profiles = new Map(data.profiles.flatMap(p => p.list.map(v => [v.id, { profile: p, version: v }])));
  const formulas = new Map(data.formulas.flatMap(f => f.list.map(v => [v.id, { formula: f, version: v }])));
  const issues: { code: string; subject: string }[] = [];
  const ineligible = new Set<string>();
  const outputs = request.outputs.map(o => {
    const found = profiles.get(o.profileVersionId);
    if (!found || found.version.state !== 'PUBLISHED') throw new ProtocolError('La receta de un producto cambió; actualiza la página', 409);
    return { ...o, ...found };
  }).sort((a, b) => a.profile.productId.localeCompare(b.profile.productId));
  const fail = (code: string) => ({ ok: false as const, code });
  if (new Set(outputs.map(o => o.profile.productId)).size !== outputs.length || new Set(outputs.map(o => o.profile.finishedItemId)).size !== outputs.length) return answer(fail('DuplicateOutput'));
  if (new Set(outputs.map(o => o.profile.id)).size !== outputs.length) return answer(fail('ConflictingDefinition'));

  const required = new Map<string, { dimension: string; atomic: bigint }>();
  const totals = new Map<string, { dimension: string; atomic: bigint }>();
  const add = (target: typeof totals, id: string, dimension: string, atomic: bigint) => {
    const before = target.get(id);
    if (before && before.dimension !== dimension) throw Object.assign(new Error('DimensionMismatch'), { code: 'DimensionMismatch' });
    target.set(id, { dimension, atomic: (before?.atomic ?? 0n) + atomic });
  };
  const planned: { id: string; name: string; version: number; dimension: string; required: number; groups: { groupId: string; label: string; options: { id: string; label: string; eligible: boolean }[]; selected: string | null }[] }[] = [];
  try {
    for (const o of outputs) {
      for (const need of o.version.formulas) add(required, need.formulaVersionId, need.dimension, BigInt(need.atomic) * BigInt(o.planned));
      for (const d of o.version.direct) {
        const r = scale(d, { num: BigInt(o.planned), den: 1n });
        if (r.error) issues.push({ code: r.error, subject: d.itemId }); else add(totals, d.itemId, d.dimension, BigInt(r.quantity!));
      }
    }
    // Preparaciones dentro de preparaciones (masa dulce → masa enriquecida), como la caja: cada una suma lo que necesita a la
    // preparación que usa, en orden de dependencias, para que una masa compartida se calcule una sola vez con todos sus usos.
    const order: string[] = [];
    const visiting = new Map<string, boolean>();
    const visit = (versionId: string) => {
      const seen = visiting.get(versionId);
      if (seen !== undefined) { if (!seen) throw Object.assign(new Error('PreparationCycle'), { code: 'PreparationCycle' }); return; }
      const f = formulas.get(versionId);
      if (!f) throw Object.assign(new Error('InvalidVersion'), { code: 'InvalidVersion' });
      visiting.set(versionId, false);
      for (const p of f.version.preparations) visit(p.formulaVersionId);
      visiting.set(versionId, true);
      order.push(versionId);
    };
    for (const versionId of [...required.keys()].sort()) visit(versionId);
    for (const versionId of order.reverse()) {
      const f = formulas.get(versionId)!;
      const need = required.get(versionId);
      if (!need || f.version.preparations.length === 0) continue;
      if (f.version.state !== 'PUBLISHED') return answer(fail('InvalidVersion'));
      if (f.version.yieldDimension !== need.dimension) return answer(fail('DimensionMismatch'));
      const factor = ratio(need.atomic, BigInt(f.version.yieldAtomic));
      for (const p of f.version.preparations) {
        const exact = ratio(factor.num * BigInt(p.atomic), factor.den);
        if (exact.num > LIMIT) issues.push({ code: 'Overflow', subject: p.formulaVersionId });
        else if (exact.den !== 1n) issues.push({ code: 'NonRepresentableScale', subject: p.formulaVersionId });
        else add(required, p.formulaVersionId, p.dimension, exact.num);
      }
    }
    for (const s of request.selections) {
      const f = formulas.get(s.formulaVersionId);
      if (!required.has(s.formulaVersionId) || !f || !f.version.groups.some(g => g.id === s.groupId && g.options.some(x => x.id === s.optionId))) return answer(fail('InvalidAlternative'));
    }
    let groupNumber = 0;
    for (const [versionId, need] of [...required.entries()].sort(([a], [b]) => a.localeCompare(b))) {
      const f = formulas.get(versionId);
      if (!f || f.version.state !== 'PUBLISHED') return answer(fail('InvalidVersion'));
      if (f.version.yieldDimension !== need.dimension) return answer(fail('DimensionMismatch'));
      const factor = ratio(need.atomic, BigInt(f.version.yieldAtomic));
      for (const i of f.version.ingredients) {
        const r = scale(i, factor);
        if (r.error) issues.push({ code: r.error, subject: i.itemId }); else add(totals, i.itemId, i.dimension, BigInt(r.quantity!));
      }
      const groups = [];
      for (const g of f.version.groups) {
        groupNumber++;
        const choice = request.selections.find(s => s.formulaVersionId === versionId && s.groupId === g.id);
        const order = choice ? [choice.optionId] : g.options.filter(o => o.rank !== null).map(o => o.id);
        let selected: string | null = null;
        if (order.length === 0) issues.push({ code: 'InvalidAlternative', subject: g.id });
        for (const optionId of order) {
          const option = g.options.find(o => o.id === optionId)!;
          const r = scale(option, factor);
          if (r.error) {
            ineligible.add(optionId);
            if (r.error === 'CountPolicyRequired') { issues.push({ code: r.error, subject: option.itemId }); break; }
            continue;
          }
          add(totals, option.itemId, option.dimension, BigInt(r.quantity!)); selected = optionId; break;
        }
        if (order.length && !selected) issues.push({ code: 'AlternativeNotEligible', subject: g.id });
        // Opciones con la preferida primero, como la caja; las que no alcanzan se marcan.
        const listed = [...g.options.filter(o => o.rank !== null), ...g.options.filter(o => o.rank === null)];
        groups.push({ groupId: g.id, label: `Grupo ${groupNumber} · ${f.version.name || f.formula.name}`, selected,
          options: listed.map(o => ({ id: o.id, label: o.item, eligible: !ineligible.has(o.id) })) });
      }
      planned.push({ id: versionId, name: f.version.name || f.formula.name, version: f.version.version, dimension: need.dimension, required: Number(need.atomic), groups });
    }
    if (outputs.some(o => totals.has(o.profile.finishedItemId))) return answer(fail('ConflictingDefinition'));
  } catch (error) {
    const code = (error as { code?: string }).code;
    if (code) return answer(fail(code));
    throw error;
  }
  return answer({ ok: true as const });

  function answer(result: { ok: true } | { ok: false; code: string }) {
    if (!result.ok) issues.push({ code: result.code, subject: '' });
    const resolved = result.ok && issues.length === 0;
    const inputs = resolved ? [...totals.entries()].sort(([a], [b]) => a.localeCompare(b)) : [];
    const stockIds = [...new Set([...inputs.map(([id]) => id), ...outputs.map(o => o.profile.finishedItemId)])];
    const stock = stockIds.map(id => ({ id, ...(items.get(id) ?? { name: 'Artículo', dimension: '', active: false, available: null }) }));
    const requirements = inputs.map(([id, total]) => {
      const s = stock.find(x => x.id === id)!;
      const need = Number(total.atomic);
      const short = s.available === null || s.available < need;
      return { itemId: id, name: s.name, dimension: total.dimension, required: need, available: s.available, shortage: short ? need - (s.available ?? 0) : 0,
        state: !s.active ? 'Inactivo' : s.available === null ? 'Sin saldo inicial' : short ? 'Faltante' : 'Suficiente' };
    });
    const inactive = stock.some(s => !s.active);
    const shortages = requirements.some(r => r.state === 'Faltante' || r.state === 'Sin saldo inicial');
    const uninitialized = stock.some(s => s.available === null);
    const canConfirm = resolved && !inactive && !shortages && !uninitialized;
    const problem = canConfirm ? null
      : issues.some(i => i.code === 'NonRepresentableScale') ? 'No es posible calcular esta receta exactamente con las unidades configuradas. Revisa la cantidad planeada o la receta.'
      : inactive ? 'Hay insumos o productos terminados inactivos.'
      : requirements.some(r => r.state === 'Faltante') ? 'No hay existencia suficiente. Revisa todos los faltantes mostrados.'
      : uninitialized ? 'Hay insumos o producto terminado sin saldo inicial.'
      : 'El plan tiene datos pendientes de corregir.';
    return { canConfirm, problem, formulas: planned.map(({ groups, ...f }) => f), alternatives: planned.flatMap(f => f.groups.map(g => ({ formulaVersionId: f.id, ...g }))), requirements };
  }
}

/** Producciones (borradores o confirmadas) con su plan, preparaciones e insumos, como Producciones en borrador e Historial. */
export async function productionBatches(q: Query, branch: string | null, status: 'DRAFT' | 'CONFIRMED') {
  const [rows, k, terminals] = await Promise.all([
    q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'production_batch' AND NOT deleted AND payload->'batch'->>'status' = $2 AND ($1::uuid IS NULL OR branch_id = $1::uuid)
       ORDER BY payload->'batch'->>'created_utc' DESC LIMIT 200`, [branch, status]),
    names(q),
    q(`SELECT id, name FROM terminal`, []),
  ]);
  const terminal = new Map(terminals.map(t => [String(t.id), String(t.name)]));
  return rows.map(x => {
    const p = x.payload as { batch: Row; outputs: Row[]; inputs: Row[]; formulas?: Row[] };
    const s = (v: unknown) => (v === null || v === undefined ? '' : String(v));
    return { id: s(p.batch.id), branchId: s(x.branch_id), status: s(p.batch.status), createdAt: s(p.batch.created_utc), confirmedAt: s(p.batch.confirmed_utc) || null,
      createdById: s(p.batch.created_by), createdBy: k.user.get(s(p.batch.created_by)) ?? '', terminal: terminal.get(s(p.batch.terminal_id)) ?? 'Caja', notes: s(p.batch.notes),
      outputs: p.outputs.map(o => ({ productId: s(o.product_id), product: s(o.product_name), profile: s(o.profile_name), planned: Number(o.planned_count), actual: o.actual_count === null || o.actual_count === undefined ? null : Number(o.actual_count) })),
      formulas: (p.formulas ?? []).map(f => ({ name: s(f.formula_name), dimension: s(f.dimension), required: Number(f.required_atomic) })),
      inputs: p.inputs.map(i => ({ itemId: s(i.item_id), name: s(i.item_name), dimension: s(i.dimension), required: Number(i.required_atomic), alternative: !!i.group_id })) };
  });
}
