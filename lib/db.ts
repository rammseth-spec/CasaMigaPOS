import { neon, Pool } from '@neondatabase/serverless';
import { ProtocolError, sha256Hex } from './protocol.ts';
import { migrate } from './migrate.ts';

function url(): string {
  const value = process.env.DATABASE_URL;
  if (!value) throw new Error('DATABASE_URL no configurada');
  return value;
}

export function sql() {
  return neon(url());
}

let schema: Promise<void> | null = null;
/** Pone la base al día una vez por instancia antes de atender; si falla, el siguiente intento lo repite. */
export function ensureSchema(): Promise<void> {
  schema ??= (async () => {
    const pool = new Pool({ connectionString: url() });
    const client = await pool.connect();
    try {
      await migrate({
        exec: async text => { await client.query(text); },
        scalar: async text => { const row = (await client.query(text)).rows[0]; return row ? Object.values(row)[0] : null; },
      });
    } finally {
      client.release();
      await pool.end();
    }
  })().catch(error => { schema = null; throw error; });
  return schema;
}

/** Resuelve la terminal por su llave; nunca se guarda la llave, sólo su SHA-256. */
export async function authenticate(key: string): Promise<{ id: string; branchId: string }> {
  const rows = await sql()`SELECT id, branch_id FROM terminal WHERE key_sha256 = ${sha256Hex(key)} AND revoked_at IS NULL`;
  if (rows.length !== 1) throw new ProtocolError('Terminal no autorizada', 401);
  return { id: rows[0].id as string, branchId: rows[0].branch_id as string };
}

/** Consultas con parámetros posicionales (mismo contrato que PGlite en las pruebas). */
export function query(): (text: string, params: unknown[]) => Promise<Record<string, unknown>[]> {
  const db = sql();
  return (text, params) => db.query(text, params) as Promise<Record<string, unknown>[]>;
}
