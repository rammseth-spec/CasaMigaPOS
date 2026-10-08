import { ensureSchema, sql } from '../lib/db.js';
import { failure, json } from '../lib/protocol.js';

export async function GET(): Promise<Response> {
  try {
    await ensureSchema();
    const rows = await sql()`SELECT max(version) AS version FROM schema_version`;
    return json(200, { ok: true, schema: rows[0].version });
  } catch (error) {
    return failure(error);
  }
}
