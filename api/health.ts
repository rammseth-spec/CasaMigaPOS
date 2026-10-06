import { sql } from '../lib/db.ts';
import { failure, json } from '../lib/protocol.ts';

export async function GET(): Promise<Response> {
  try {
    const rows = await sql()`SELECT max(version) AS version FROM schema_version`;
    return json(200, { ok: true, schema: rows[0].version });
  } catch (error) {
    return failure(error);
  }
}
