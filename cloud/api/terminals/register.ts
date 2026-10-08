import { ensureSchema, query } from '../../lib/db.js';
import { failure } from '../../lib/protocol.js';
import { handleTerminal } from '../../lib/terminal.js';

/** Alta o reinstalación de una caja con el secreto de alta (lib/terminal.ts). */
export async function POST(request: Request): Promise<Response> {
  try { await ensureSchema(); return await handleTerminal(query(), request, 'register', process.env); }
  catch (error) { return failure(error); }
}
