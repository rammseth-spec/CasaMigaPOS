import { authenticate, sql } from '../../lib/db.ts';
import { bearer, failure, json, parseBatch, ProtocolError, readJson, reconcile } from '../../lib/protocol.ts';

/**
 * Recibe un lote de la cola local. Idempotente por (terminal, seq): un reenvío idéntico se confirma sin duplicar.
 * Un seq ya usado con otro contenido (p. ej. tras restaurar un respaldo viejo) se reporta como conflicto y no se acepta.
 */
export async function POST(request: Request): Promise<Response> {
  try {
    const terminal = await authenticate(bearer(request.headers.get('authorization')));
    const batch = parseBatch(await readJson(request));
    if (batch.branchId !== terminal.branchId) throw new ProtocolError('La sucursal no coincide con la terminal', 403);

    const db = sql();
    const rows = JSON.stringify(batch.entries.map(e => ({
      seq: e.seq, entity: e.entity, entity_id: e.entityId, change: e.change, enqueued_at: e.enqueuedAt, payload: e.payload,
    })));
    await db.transaction([
      db`INSERT INTO sync_event(terminal_id, seq, branch_id, entity, entity_id, change, enqueued_at, payload)
         SELECT ${terminal.id}, r.seq, ${terminal.branchId}, r.entity, r.entity_id, r.change, r.enqueued_at, r.payload
         FROM jsonb_to_recordset(${rows}::jsonb) AS r(seq bigint, entity text, entity_id text, change text, enqueued_at timestamptz, payload jsonb)
         ON CONFLICT (terminal_id, seq) DO NOTHING`,
      db`UPDATE terminal SET last_seen_at = now() WHERE id = ${terminal.id}`,
    ]);
    const seqs = batch.entries.map(e => e.seq);
    const stored = await db`SELECT seq, entity, entity_id, change FROM sync_event WHERE terminal_id = ${terminal.id} AND seq = ANY(${seqs}::bigint[])`;
    const existing = new Map(stored.map(r => [Number(r.seq), { entity: r.entity as string, entityId: r.entity_id as string, change: r.change as string }]));
    const result = reconcile(batch.entries, existing);
    return json(result.conflicts.length > 0 ? 409 : 200, result);
  } catch (error) {
    return failure(error);
  }
}
