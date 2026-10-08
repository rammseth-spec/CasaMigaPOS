import { authenticate, ensureSchema, query } from '../../lib/db.ts';
import { bearer, failure, json, readJson } from '../../lib/protocol.ts';
import { pendingFor, recordResults } from '../../lib/commands.ts';

/** La caja pide los cambios de su sucursal posteriores a su cursor (?after=seq). */
export async function GET(request: Request): Promise<Response> {
  try {
    await ensureSchema();
    const terminal = await authenticate(bearer(request.headers.get('authorization')));
    const after = Number(new URL(request.url).searchParams.get('after') ?? '0');
    return json(200, { commands: await pendingFor(query(), terminal, Number.isSafeInteger(after) && after >= 0 ? after : 0) });
  } catch (error) {
    return failure(error);
  }
}

/** La caja reporta qué aplicó y qué rechazó (con el motivo que mostraría en pantalla). */
export async function POST(request: Request): Promise<Response> {
  try {
    await ensureSchema();
    const terminal = await authenticate(bearer(request.headers.get('authorization')));
    return json(200, await recordResults(query(), terminal, await readJson(request, 100_000)));
  } catch (error) {
    return failure(error);
  }
}
