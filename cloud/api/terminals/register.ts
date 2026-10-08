import { ensureSchema, query } from '../../lib/db.ts';
import { failure } from '../../lib/protocol.ts';
import { handleTerminal } from '../../lib/terminal.ts';

/** Alta o reinstalación de una caja con el secreto de alta (lib/terminal.ts). */
export async function POST(request: Request): Promise<Response> {
  try { await ensureSchema(); return await handleTerminal(query(), request, 'register', process.env); }
  catch (error) { return failure(error); }
}
