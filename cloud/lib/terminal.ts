// Lado de las cajas: alta de terminal, autenticación por llave y rutas de sincronización, con la base inyectable.
import { bearer, failure, json, newTerminalKey, parseBatch, parseRegistration, ProtocolError, readJson, secretMatches, sha256Hex } from './protocol.ts';
import { storeBatch } from './store.ts';
import { pendingFor, recordResults } from './commands.ts';
import type { Query } from './panel.ts';

/** Resuelve la terminal por su llave; nunca se guarda la llave, sólo su SHA-256. */
export async function authenticate(q: Query, header: string | null): Promise<{ id: string; branchId: string }> {
  const rows = await q('SELECT id, branch_id FROM terminal WHERE key_sha256 = $1 AND revoked_at IS NULL', [sha256Hex(bearer(header))]);
  if (rows.length !== 1) throw new ProtocolError('Terminal no autorizada', 401);
  return { id: String(rows[0].id), branchId: String(rows[0].branch_id) };
}

/**
 * Alta o reinstalación de una terminal. Requiere el secreto de alta que sólo conoce el administrador.
 * Devuelve una llave nueva; la anterior deja de funcionar. Una terminal no puede cambiar de sucursal.
 */
export async function register(q: Query, body: unknown, enrollmentSecret: string | undefined) {
  const registration = parseRegistration(body);
  if (!secretMatches(registration.enrollmentSecret, enrollmentSecret)) throw new ProtocolError('Secreto de alta incorrecto', 403);
  const existing = await q('SELECT branch_id FROM terminal WHERE id = $1', [registration.terminal.id]);
  if (existing.length === 1 && existing[0].branch_id !== registration.branch.id) throw new ProtocolError('La terminal ya pertenece a otra sucursal', 409);
  const key = newTerminalKey();
  await q(`WITH b AS (INSERT INTO branch(id, name) VALUES ($1, $2) ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name RETURNING id)
    INSERT INTO terminal(id, branch_id, name, key_sha256) SELECT $3, b.id, $4, $5 FROM b
    ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, key_sha256 = EXCLUDED.key_sha256, key_rotated_at = now(), revoked_at = NULL`,
    [registration.branch.id, registration.branch.name, registration.terminal.id, registration.terminal.name, sha256Hex(key)]);
  return { terminalKey: key, rotated: existing.length === 1 };
}

export interface SyncEnv { ENROLLMENT_SECRET?: string }

/** /api/terminals/register, /api/sync/batch y /api/sync/commands. */
export async function handleTerminal(q: Query, request: Request, route: 'register' | 'batch' | 'commands', env: SyncEnv): Promise<Response> {
  try {
    if (route === 'register' && request.method === 'POST') return json(200, await register(q, await readJson(request, 10_000), env.ENROLLMENT_SECRET));
    const terminal = await authenticate(q, request.headers.get('authorization'));
    if (route === 'batch' && request.method === 'POST') {
      const batch = parseBatch(await readJson(request));
      if (batch.branchId !== terminal.branchId) throw new ProtocolError('La sucursal no coincide con la terminal', 403);
      const result = await storeBatch(q, terminal, batch);
      return json(result.conflicts.length > 0 ? 409 : 200, result);
    }
    if (route === 'commands' && request.method === 'GET') {
      const after = Number(new URL(request.url).searchParams.get('after') ?? '0');
      return json(200, { commands: await pendingFor(q, terminal, Number.isSafeInteger(after) && after >= 0 ? after : 0) });
    }
    if (route === 'commands' && request.method === 'POST') return json(200, await recordResults(q, terminal, await readJson(request, 100_000)));
    throw new ProtocolError('Método no permitido', 405);
  } catch (error) {
    return failure(error);
  }
}
