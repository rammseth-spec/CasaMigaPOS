// Recepción de lotes: registro inmutable (sync_event) y estado más reciente (entity_state) en una sola sentencia.
import { reconcile, type SyncBatch } from './protocol.js';
import type { Query } from './panel.js';

export async function storeBatch(q: Query, terminal: { id: string; branchId: string }, batch: SyncBatch) {
  const rows = JSON.stringify(batch.entries.map(e => ({
    seq: e.seq, entity: e.entity, entity_id: e.entityId, change: e.change, enqueued_at: e.enqueuedAt, payload: e.payload,
  })));
  // Sólo lo recién insertado actualiza el estado: un reenvío idéntico o un seq en conflicto no lo tocan.
  // Dentro del lote (una sola caja), el mismo registro puede venir varias veces: gana el de seq más alto.
  await q(`WITH incoming AS (
      INSERT INTO sync_event(terminal_id, seq, branch_id, entity, entity_id, change, enqueued_at, payload)
      SELECT $1, r.seq, $2, r.entity, r.entity_id, r.change, r.enqueued_at, r.payload
      FROM jsonb_to_recordset($3::jsonb) AS r(seq bigint, entity text, entity_id text, change text, enqueued_at timestamptz, payload jsonb)
      ON CONFLICT (terminal_id, seq) DO NOTHING
      RETURNING terminal_id, seq, branch_id, entity, entity_id, change, enqueued_at, payload),
    latest AS (SELECT DISTINCT ON (entity, entity_id) * FROM incoming ORDER BY entity, entity_id, seq DESC)
    INSERT INTO entity_state(branch_id, entity, entity_id, terminal_id, seq, enqueued_at, deleted, payload)
    SELECT branch_id, entity, entity_id, terminal_id, seq, enqueued_at, change = 'delete', payload FROM latest
    ON CONFLICT (branch_id, entity, entity_id) DO UPDATE SET terminal_id = EXCLUDED.terminal_id, seq = EXCLUDED.seq,
      enqueued_at = EXCLUDED.enqueued_at, deleted = EXCLUDED.deleted, payload = EXCLUDED.payload, received_at = now()
    -- Misma caja: manda el orden de su cola (seq), aunque su reloj se haya atrasado. Entre cajas distintas: la hora de encolado.
    WHERE (EXCLUDED.terminal_id = entity_state.terminal_id AND EXCLUDED.seq > entity_state.seq)
       OR (EXCLUDED.terminal_id <> entity_state.terminal_id AND (EXCLUDED.enqueued_at, EXCLUDED.seq) >= (entity_state.enqueued_at, entity_state.seq))`,
    [terminal.id, terminal.branchId, rows]);
  await q('UPDATE terminal SET last_seen_at = now() WHERE id = $1', [terminal.id]);
  const seqs = batch.entries.map(e => e.seq);
  const stored = await q('SELECT seq, entity, entity_id, change FROM sync_event WHERE terminal_id = $1 AND seq = ANY($2::bigint[])', [terminal.id, seqs]);
  const existing = new Map(stored.map(r => [Number(r.seq), { entity: String(r.entity), entityId: String(r.entity_id), change: String(r.change) }]));
  return reconcile(batch.entries, existing);
}
