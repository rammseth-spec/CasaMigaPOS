// Respaldos en la nube (docs/BACKUPS.md). Formato cifrado: "CMB1" + nonce (12) + etiqueta GCM (16) + datos (AES-256-GCM).
// Llave por sucursal derivada de BACKUP_SECRET (HKDF-SHA256): sin ese secreto ningún respaldo se puede leer.
import { createCipheriv, createDecipheriv, createHash, hkdfSync, randomBytes, randomUUID } from 'node:crypto';
import { gzipSync } from 'node:zlib';
import { ProtocolError } from './protocol.js';
import type { Query, Row } from './panel.js';
import type { Storage } from './storage.js';

export const PART_BYTES = 3 * 1024 * 1024;
export const KEEP_ALL_HOURS = 48;
export const KEEP_DAYS = 30;
const MAGIC = Buffer.from('CMB1');
const HEX = /^[0-9A-F]{64}$/;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function backupSecret(env: string | undefined): string {
  if (!env || env.length < 32) throw new ProtocolError('Respaldos sin configurar (BACKUP_SECRET)', 503);
  return env;
}

/** Llave de 32 bytes para una sucursal ("branch:<id>") o para la copia de la nube ("cloud"). */
export function deriveKey(secret: string, scope: string): Buffer {
  return Buffer.from(hkdfSync('sha256', Buffer.from(secret, 'utf8'), Buffer.from('casamiga-backup-v1', 'utf8'), Buffer.from(scope, 'utf8'), 32));
}

export function encrypt(key: Buffer, plain: Uint8Array): Buffer {
  const nonce = randomBytes(12);
  const cipher = createCipheriv('aes-256-gcm', key, nonce);
  const data = Buffer.concat([cipher.update(plain), cipher.final()]);
  return Buffer.concat([MAGIC, nonce, cipher.getAuthTag(), data]);
}

export function decrypt(key: Buffer, sealed: Uint8Array): Buffer {
  const b = Buffer.from(sealed);
  if (b.length < 32 || !b.subarray(0, 4).equals(MAGIC)) throw new Error('Respaldo con formato desconocido');
  const decipher = createDecipheriv('aes-256-gcm', key, b.subarray(4, 16));
  decipher.setAuthTag(b.subarray(16, 32));
  return Buffer.concat([decipher.update(b.subarray(32)), decipher.final()]);
}

type Terminal = { id: string; branchId: string };
const iso = (v: unknown) => (v instanceof Date ? v.toISOString() : v === null || v === undefined ? null : String(v));

// ---------- Lado de la caja ----------

export async function state(q: Query, terminal: Terminal) {
  const [last] = await q(`SELECT database_sha256, created_at FROM backup_set WHERE terminal_id = $1 ORDER BY created_at DESC LIMIT 1`, [terminal.id]);
  return { databaseSha256: last ? String(last.database_sha256) : null, at: iso(last?.created_at) };
}

/** Fotos (por contenido) que esta sucursal todavía no tiene respaldadas. */
export async function missing(q: Query, terminal: Terminal, body: unknown) {
  const shas = (body as { shas?: unknown } | null)?.shas;
  if (!Array.isArray(shas) || shas.length > 50_000 || shas.some(s => typeof s !== 'string' || !HEX.test(s))) throw new ProtocolError('Lista de fotos inválida');
  if (shas.length === 0) return { missing: [] };
  const have = await q(`SELECT DISTINCT sha256 FROM backup_object WHERE branch_id = $1 AND kind = 'photo' AND completed_at IS NOT NULL AND sha256 = ANY($2::text[])`, [terminal.branchId, shas]);
  const known = new Set(have.map(r => String(r.sha256)));
  return { missing: [...new Set(shas as string[])].filter(s => !known.has(s)) };
}

export async function beginObject(q: Query, terminal: Terminal, body: unknown) {
  const b = (body ?? {}) as Record<string, unknown>;
  if (b.kind !== 'database' && b.kind !== 'photo') throw new ProtocolError('Tipo de respaldo inválido');
  if (typeof b.sha256 !== 'string' || !HEX.test(b.sha256)) throw new ProtocolError('Huella inválida');
  if (!Number.isSafeInteger(b.bytes) || (b.bytes as number) < 1 || (b.bytes as number) > 17_179_869_184) throw new ProtocolError('Tamaño inválido');
  if (!Number.isSafeInteger(b.parts) || (b.parts as number) < 1 || (b.parts as number) > 10_000) throw new ProtocolError('Partes inválidas');
  const id = randomUUID();
  await q(`INSERT INTO backup_object(id, branch_id, terminal_id, kind, sha256, bytes, parts) VALUES ($1, $2, $3, $4, $5, $6, $7)`,
    [id, terminal.branchId, terminal.id, b.kind, b.sha256, b.bytes, b.parts]);
  return { id };
}

export async function putPart(q: Query, storage: Storage, terminal: Terminal, objectId: string | null, indexText: string | null, data: Uint8Array) {
  if (!objectId || !UUID.test(objectId)) throw new ProtocolError('Respaldo inválido');
  const index = Number(indexText);
  const [object] = await q(`SELECT branch_id, parts, completed_at FROM backup_object WHERE id = $1 AND terminal_id = $2`, [objectId.toLowerCase(), terminal.id]);
  if (!object) throw new ProtocolError('Respaldo inexistente', 404);
  if (object.completed_at) throw new ProtocolError('Ese respaldo ya está completo', 409);
  if (!Number.isSafeInteger(index) || index < 0 || index >= Number(object.parts)) throw new ProtocolError('Parte inválida');
  if (data.length < 1 || data.length > 4_194_304) throw new ProtocolError('Parte de tamaño inválido', 413);
  const url = await storage.put(`backups/${terminal.branchId}/${objectId.toLowerCase()}/${index}`, data);
  const [old] = await q(`SELECT url FROM backup_part WHERE object_id = $1 AND index = $2`, [objectId.toLowerCase(), index]);
  await q(`INSERT INTO backup_part(object_id, index, url, bytes) VALUES ($1, $2, $3, $4)
    ON CONFLICT (object_id, index) DO UPDATE SET url = EXCLUDED.url, bytes = EXCLUDED.bytes`, [objectId.toLowerCase(), index, url, data.length]);
  if (old) await storage.del([String(old.url)]);
  return { ok: true };
}

export async function completeObject(q: Query, terminal: Terminal, objectId: string | null) {
  if (!objectId || !UUID.test(objectId)) throw new ProtocolError('Respaldo inválido');
  const [object] = await q(`SELECT o.parts, (SELECT count(*) FROM backup_part p WHERE p.object_id = o.id) AS stored
    FROM backup_object o WHERE o.id = $1 AND o.terminal_id = $2`, [objectId.toLowerCase(), terminal.id]);
  if (!object) throw new ProtocolError('Respaldo inexistente', 404);
  if (Number(object.stored) !== Number(object.parts)) throw new ProtocolError('Faltan partes del respaldo', 409);
  await q(`UPDATE backup_object SET completed_at = COALESCE(completed_at, now()) WHERE id = $1`, [objectId.toLowerCase()]);
  return { ok: true };
}

const PHOTO_PATH = /^product-images\/v1\/[0-9a-f]{32}\/(original\.(jpg|png)|pos-v1-[0-9A-F]{64}\.jpg)$/;

/** Cierra un respaldo de la caja: base + identidad + fotos que ya están arriba. Deja también un manifiesto cifrado junto a los archivos. */
export async function createSet(q: Query, storage: Storage, secret: string, terminal: Terminal, body: unknown) {
  const b = (body ?? {}) as Record<string, unknown>;
  const objectId = typeof b.databaseObject === 'string' && UUID.test(b.databaseObject) ? b.databaseObject.toLowerCase() : null;
  const [db] = objectId ? await q(`SELECT sha256, bytes FROM backup_object WHERE id = $1 AND terminal_id = $2 AND kind = 'database' AND completed_at IS NOT NULL`, [objectId, terminal.id]) : [];
  if (!db) throw new ProtocolError('La base del respaldo no está completa');
  if (typeof b.terminalJson !== 'string' || b.terminalJson.length > 65_536) throw new ProtocolError('Identidad de caja inválida');
  if (!Number.isSafeInteger(b.schemaVersion) || (b.schemaVersion as number) < 1) throw new ProtocolError('Versión de base inválida');
  const media = Array.isArray(b.media) ? b.media : null;
  if (!media || media.length > 40_000) throw new ProtocolError('Lista de fotos inválida');
  const entries = media.map((m: unknown) => {
    const e = (m ?? {}) as Record<string, unknown>;
    if (typeof e.path !== 'string' || !PHOTO_PATH.test(e.path) || (e.role !== 'original' && e.role !== 'thumbnail')
      || typeof e.sha256 !== 'string' || !HEX.test(e.sha256) || !Number.isSafeInteger(e.bytes) || (e.bytes as number) < 1) throw new ProtocolError('Foto del respaldo inválida');
    return { path: e.path, role: e.role, sha256: e.sha256, bytes: e.bytes as number };
  });
  const photos = await q(`SELECT DISTINCT ON (sha256) id, sha256 FROM backup_object WHERE branch_id = $1 AND kind = 'photo' AND completed_at IS NOT NULL AND sha256 = ANY($2::text[])
    ORDER BY sha256, completed_at DESC`, [terminal.branchId, entries.map(e => e.sha256)]);
  const objectOf = new Map(photos.map(p => [String(p.sha256), String(p.id)]));
  if (entries.some(e => !objectOf.has(e.sha256))) throw new ProtocolError('Faltan fotos del respaldo', 409);

  const id = randomUUID();
  const total = Number(db.bytes) + entries.reduce((a, e) => a + e.bytes, 0);
  await q(`INSERT INTO backup_set(id, branch_id, terminal_id, kind, database_object, database_sha256, terminal_json, schema_version, media, bytes)
    VALUES ($1, $2, $3, 'terminal', $4, $5, $6, $7, $8::jsonb, $9)`,
    [id, terminal.branchId, terminal.id, objectId, String(db.sha256), b.terminalJson, b.schemaVersion, JSON.stringify(entries), total]);
  const manifest = await detail(q, id);
  const url = await storage.put(`backups/${terminal.branchId}/sets/${id}/manifest`, encrypt(deriveKey(secret, `branch:${terminal.branchId}`), Buffer.from(JSON.stringify(manifest))));
  await q(`UPDATE backup_set SET manifest_url = $2 WHERE id = $1`, [id, url]);
  return { id };
}

// ---------- Consulta y restauración (administrador) ----------

async function parts(q: Query, objectId: string) {
  const rows = await q(`SELECT url, bytes FROM backup_part WHERE object_id = $1 ORDER BY index`, [objectId]);
  return rows.map(r => ({ url: String(r.url), bytes: Number(r.bytes) }));
}

/** Todo lo necesario para reconstruir un respaldo: direcciones de cada parte y huellas para verificar. */
export async function detail(q: Query, id: string) {
  if (!UUID.test(id)) throw new ProtocolError('Respaldo inválido');
  const [set] = await q(`SELECT s.*, o.bytes AS database_bytes, t.name AS terminal_name, b.name AS branch_name FROM backup_set s
    JOIN backup_object o ON o.id = s.database_object LEFT JOIN terminal t ON t.id = s.terminal_id LEFT JOIN branch b ON b.id = s.branch_id WHERE s.id = $1`, [id.toLowerCase()]);
  if (!set) throw new ProtocolError('Respaldo inexistente', 404);
  const media = (set.media as { path: string; role: string; sha256: string; bytes: number }[]) ?? [];
  const photoObjects = media.length ? await q(`SELECT DISTINCT ON (sha256) id, sha256 FROM backup_object WHERE branch_id = $1 AND kind = 'photo' AND completed_at IS NOT NULL
    AND sha256 = ANY($2::text[]) ORDER BY sha256, completed_at DESC`, [set.branch_id, media.map(m => m.sha256)]) : [];
  const objectOf = new Map(photoObjects.map(p => [String(p.sha256), String(p.id)]));
  return {
    id: String(set.id), kind: String(set.kind), branchId: set.branch_id ? String(set.branch_id) : null, branch: set.branch_name ? String(set.branch_name) : null,
    terminalId: set.terminal_id ? String(set.terminal_id) : null, terminal: set.terminal_name ? String(set.terminal_name) : null,
    createdAt: iso(set.created_at), schemaVersion: set.schema_version === null ? null : Number(set.schema_version), terminalJson: set.terminal_json === null ? null : String(set.terminal_json),
    database: { sha256: String(set.database_sha256), bytes: Number(set.database_bytes), parts: await parts(q, String(set.database_object)) },
    media: await Promise.all(media.map(async m => ({ ...m, parts: objectOf.has(m.sha256) ? await parts(q, objectOf.get(m.sha256)!) : [] }))),
  };
}

export async function list(q: Query, branch: string | null) {
  const rows = await q(`SELECT s.id, s.kind, s.branch_id, s.terminal_id, t.name AS terminal, s.created_at, s.bytes, jsonb_array_length(s.media) AS files
    FROM backup_set s LEFT JOIN terminal t ON t.id = s.terminal_id
    WHERE ($1::uuid IS NULL OR s.branch_id = $1::uuid OR s.kind = 'cloud') ORDER BY s.created_at DESC LIMIT 300`, [branch]);
  return rows.map(r => ({ id: String(r.id), kind: String(r.kind), branchId: r.branch_id ? String(r.branch_id) : null, terminal: r.terminal ? String(r.terminal) : null,
    createdAt: iso(r.created_at), bytes: Number(r.bytes), files: Number(r.files) }));
}

// ---------- Nube: copia nocturna y limpieza ----------

const TABLES = ['schema_version', 'branch', 'terminal', 'sync_event', 'branch_command', 'command_result'] as const;

async function storeObject(q: Query, storage: Storage, kind: 'cloud', sha256: string, plainBytes: number, sealed: Buffer) {
  const id = randomUUID();
  const count = Math.max(1, Math.ceil(sealed.length / PART_BYTES));
  await q(`INSERT INTO backup_object(id, branch_id, terminal_id, kind, sha256, bytes, parts) VALUES ($1, NULL, NULL, $2, $3, $4, $5)`, [id, kind, sha256, plainBytes, count]);
  for (let i = 0; i < count; i++) {
    const part = sealed.subarray(i * PART_BYTES, (i + 1) * PART_BYTES);
    const url = await storage.put(`backups/cloud/${id}/${i}`, part);
    await q(`INSERT INTO backup_part(object_id, index, url, bytes) VALUES ($1, $2, $3, $4)`, [id, i, url, part.length]);
  }
  await q(`UPDATE backup_object SET completed_at = now() WHERE id = $1`, [id]);
  return id;
}

/** Copia de la base de la nube: cada tabla como renglones JSON, comprimida y cifrada con la llave "cloud". */
export async function exportCloud(q: Query, storage: Storage, secret: string) {
  const lines: string[] = [];
  for (const table of TABLES) {
    if (table === 'sync_event') {
      let after: [string, number] = ['00000000-0000-0000-0000-000000000000', 0];
      for (;;) {
        const rows = await q(`SELECT * FROM sync_event WHERE (terminal_id, seq) > ($1::uuid, $2::bigint) ORDER BY terminal_id, seq LIMIT 2000`, after);
        for (const row of rows) lines.push(JSON.stringify({ table, row }));
        if (rows.length < 2000) break;
        const last = rows[rows.length - 1]; after = [String(last.terminal_id), Number(last.seq)];
      }
    } else for (const row of await q(`SELECT * FROM ${table}`, [])) lines.push(JSON.stringify({ table, row }));
  }
  const plain = gzipSync(Buffer.from(lines.join('\n'), 'utf8'));
  const sha = createHash('sha256').update(plain).digest('hex').toUpperCase();
  const objectId = await storeObject(q, storage, 'cloud', sha, plain.length, encrypt(deriveKey(secret, 'cloud'), plain));
  const id = randomUUID();
  await q(`INSERT INTO backup_set(id, kind, database_object, database_sha256, bytes) VALUES ($1, 'cloud', $2, $3, $4)`, [id, objectId, sha, plain.length]);
  return { id, rows: lines.length, bytes: plain.length };
}

/**
 * Conserva todo lo de las últimas 48 h; de ahí a 30 días, el último de cada día por caja; siempre el más reciente.
 * Borra las partes, manifiestos y fotos que ya no usa ningún respaldo conservado.
 */
export async function prune(q: Query, storage: Storage, timeZone: string, now = new Date()) {
  const recent = new Date(now.getTime() - KEEP_ALL_HOURS * 3_600_000).toISOString();
  const oldest = new Date(now.getTime() - KEEP_DAYS * 86_400_000).toISOString();
  const doomed = await q(`WITH ranked AS (
      SELECT id, kind, terminal_id, created_at, manifest_url, database_object,
        row_number() OVER (PARTITION BY kind, terminal_id ORDER BY created_at DESC) AS newest,
        row_number() OVER (PARTITION BY kind, terminal_id, (created_at AT TIME ZONE $3)::date ORDER BY created_at DESC) AS daily
      FROM backup_set)
    SELECT id, manifest_url, database_object FROM ranked
    WHERE newest > 1 AND created_at < $1::timestamptz AND (daily > 1 OR created_at < $2::timestamptz)`, [recent, oldest, timeZone]);
  const urls: string[] = [];
  for (const set of doomed) {
    if (set.manifest_url) urls.push(String(set.manifest_url));
    await q('DELETE FROM backup_set WHERE id = $1', [set.id]);
  }
  // Objetos sin uso: bases de respaldos borrados, fotos que ningún respaldo menciona e intentos incompletos (con 2 días de gracia).
  const grace = new Date(now.getTime() - 48 * 3_600_000).toISOString();
  const orphans = await q(`SELECT o.id FROM backup_object o WHERE o.created_at < $1::timestamptz AND (
      (o.kind IN ('database','cloud') AND NOT EXISTS (SELECT 1 FROM backup_set s WHERE s.database_object = o.id))
      OR (o.kind = 'photo' AND NOT EXISTS (SELECT 1 FROM backup_set s, jsonb_array_elements(s.media) m WHERE s.branch_id = o.branch_id AND m->>'sha256' = o.sha256))
      OR o.completed_at IS NULL)`, [grace]);
  for (const o of orphans) {
    for (const p of await q('SELECT url FROM backup_part WHERE object_id = $1', [o.id])) urls.push(String(p.url));
    await q('DELETE FROM backup_object WHERE id = $1', [o.id]);
  }
  await storage.del(urls);
  return { sets: doomed.length, objects: orphans.length, files: urls.length };
}

export type BackupRow = Row;
