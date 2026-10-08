import { ensureSchema, query } from '../../lib/db.js';
import { failure } from '../../lib/protocol.js';
import { handleTerminal } from '../../lib/terminal.js';

/**
 * Recibe un lote de la cola local. Idempotente por (terminal, seq): un reenvío idéntico se confirma sin duplicar.
 * Un seq ya usado con otro contenido (p. ej. tras restaurar un respaldo viejo) se reporta como conflicto y no se acepta.
 */
export async function POST(request: Request): Promise<Response> {
  try { await ensureSchema(); return await handleTerminal(query(), request, 'batch', process.env); }
  catch (error) { return failure(error); }
}
