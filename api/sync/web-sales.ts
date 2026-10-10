import { ensureSchema, query } from '../../lib/db.js';
import { failure } from '../../lib/protocol.js';
import { handleTerminal } from '../../lib/terminal.js';

/** GET: la caja de escritorio baja lo registrado por las cajas web de su sucursal después de sus cursores (?after=terminal:seq,...). */
export async function GET(request: Request): Promise<Response> {
  try { await ensureSchema(); return await handleTerminal(query(), request, 'web-sales', process.env); }
  catch (error) { return failure(error); }
}
