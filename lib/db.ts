import { neon } from '@neondatabase/serverless';
import { ProtocolError, sha256Hex } from './protocol.ts';

export function sql() {
  const url = process.env.DATABASE_URL;
  if (!url) throw new Error('DATABASE_URL no configurada');
  return neon(url);
}

/** Resuelve la terminal por su llave; nunca se guarda la llave, sólo su SHA-256. */
export async function authenticate(key: string): Promise<{ id: string; branchId: string }> {
  const rows = await sql()`SELECT id, branch_id FROM terminal WHERE key_sha256 = ${sha256Hex(key)} AND revoked_at IS NULL`;
  if (rows.length !== 1) throw new ProtocolError('Terminal no autorizada', 401);
  return { id: rows[0].id as string, branchId: rows[0].branch_id as string };
}

/** Consultas con parámetros posicionales para el panel (mismo contrato que PGlite en las pruebas). */
export function query(): (text: string, params: unknown[]) => Promise<Record<string, unknown>[]> {
  const db = sql();
  return (text, params) => db.query(text, params) as Promise<Record<string, unknown>[]>;
}
