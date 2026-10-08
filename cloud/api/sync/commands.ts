import { ensureSchema, query } from '../../lib/db.ts';
import { failure } from '../../lib/protocol.ts';
import { handleTerminal } from '../../lib/terminal.ts';

/** GET: la caja pide los cambios de su sucursal posteriores a su cursor (?after=seq). POST: reporta qué aplicó y qué rechazó. */
async function route(request: Request): Promise<Response> {
  try { await ensureSchema(); return await handleTerminal(query(), request, 'commands', process.env); }
  catch (error) { return failure(error); }
}
export const GET = route;
export const POST = route;
