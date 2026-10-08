// Aplica db/NNN_*.sql pendientes sobre la base central. Corre al arrancar cada función (una vez por instancia):
// un candado de Postgres evita que dos instancias apliquen la misma migración, y cada archivo va en su propia transacción.
import { readdirSync, readFileSync } from 'node:fs';

export interface Migration { version: number; name: string; sql: string }
/** Una sola conexión: lo que corre entre BEGIN y COMMIT comparte transacción. */
export interface Session {
  exec(sql: string): Promise<void>;
  scalar(sql: string): Promise<unknown>;
}

const LOCK = 7_234_501;

export function migrations(directory = new URL('../db/', import.meta.url)): Migration[] {
  return readdirSync(directory).filter(f => /^\d{3}_[a-z0-9_]+\.sql$/.test(f)).sort().map(name => ({
    version: Number(name.slice(0, 3)), name, sql: readFileSync(new URL(name, directory), 'utf8'),
  }));
}

async function version(session: Session): Promise<number> {
  const exists = await session.scalar(`SELECT to_regclass('public.schema_version') IS NOT NULL`);
  return exists ? Number(await session.scalar('SELECT COALESCE(max(version), 0) FROM schema_version')) : 0;
}

/** Devuelve las versiones aplicadas en esta llamada (vacío si ya estaba al día). */
export async function migrate(session: Session, files: Migration[] = migrations()): Promise<number[]> {
  const applied: number[] = [];
  if (files.length === 0 || (await version(session)) >= files[files.length - 1].version) return applied;
  for (const file of files) {
    await session.exec('BEGIN');
    try {
      await session.exec(`SELECT pg_advisory_xact_lock(${LOCK})`);
      if ((await version(session)) >= file.version) { await session.exec('COMMIT'); continue; }
      await session.exec(file.sql);
      if ((await version(session)) !== file.version) throw new Error(`La migración ${file.name} no registró su versión.`);
      await session.exec('COMMIT');
      applied.push(file.version);
    } catch (error) {
      await session.exec('ROLLBACK').catch(() => {});
      throw error;
    }
  }
  return applied;
}
