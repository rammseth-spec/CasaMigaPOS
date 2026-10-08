import { ensureSchema, query } from '../../lib/db.js';
import { failure } from '../../lib/protocol.js';
import { handle } from '../../lib/router.js';

async function route(request: Request): Promise<Response> {
  try {
    await ensureSchema();
    const op = new URL(request.url).pathname.split('/').filter(Boolean).pop() ?? '';
    return await handle(query(), request, op, process.env);
  } catch (error) {
    return failure(error);
  }
}

export const GET = route;
export const POST = route;
