import { ensureSchema, query } from '../../lib/db.ts';
import { failure } from '../../lib/protocol.ts';
import { handle } from '../../lib/router.ts';

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
