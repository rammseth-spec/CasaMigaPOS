import { sql } from '../../lib/db.ts';
import { failure, json, newTerminalKey, parseRegistration, ProtocolError, readJson, secretMatches, sha256Hex } from '../../lib/protocol.ts';

/**
 * Alta o reinstalación de una terminal. Requiere el secreto de alta que sólo conoce el administrador.
 * Devuelve una llave nueva; la anterior deja de funcionar. Una terminal no puede cambiar de sucursal.
 */
export async function POST(request: Request): Promise<Response> {
  try {
    const registration = parseRegistration(await readJson(request, 10_000));
    if (!secretMatches(registration.enrollmentSecret, process.env.ENROLLMENT_SECRET)) throw new ProtocolError('Secreto de alta incorrecto', 403);
    const db = sql();
    const existing = await db`SELECT branch_id FROM terminal WHERE id = ${registration.terminal.id}`;
    if (existing.length === 1 && existing[0].branch_id !== registration.branch.id) throw new ProtocolError('La terminal ya pertenece a otra sucursal', 409);

    const key = newTerminalKey();
    const hash = sha256Hex(key);
    await db.transaction([
      db`INSERT INTO branch(id, name) VALUES (${registration.branch.id}, ${registration.branch.name})
         ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name`,
      db`INSERT INTO terminal(id, branch_id, name, key_sha256) VALUES (${registration.terminal.id}, ${registration.branch.id}, ${registration.terminal.name}, ${hash})
         ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, key_sha256 = EXCLUDED.key_sha256, key_rotated_at = now(), revoked_at = NULL`,
    ]);
    return json(200, { terminalKey: key, rotated: existing.length === 1 });
  } catch (error) {
    return failure(error);
  }
}
