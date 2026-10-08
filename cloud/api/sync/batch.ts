import { authenticate, ensureSchema, query } from '../../lib/db.ts';
import { bearer, failure, json, parseBatch, ProtocolError, readJson } from '../../lib/protocol.ts';
import { storeBatch } from '../../lib/store.ts';

/**
 * Recibe un lote de la cola local. Idempotente por (terminal, seq): un reenvío idéntico se confirma sin duplicar.
 * Un seq ya usado con otro contenido (p. ej. tras restaurar un respaldo viejo) se reporta como conflicto y no se acepta.
 */
export async function POST(request: Request): Promise<Response> {
  try {
    await ensureSchema();
    const terminal = await authenticate(bearer(request.headers.get('authorization')));
    const batch = parseBatch(await readJson(request));
    if (batch.branchId !== terminal.branchId) throw new ProtocolError('La sucursal no coincide con la terminal', 403);
    const result = await storeBatch(query(), terminal, batch);
    return json(result.conflicts.length > 0 ? 409 : 200, result);
  } catch (error) {
    return failure(error);
  }
}
