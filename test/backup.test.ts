import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { gunzipSync, gzipSync } from 'node:zlib';
import { ProtocolError } from '../lib/protocol.js';
import { decrypt, deriveKey, encrypt, exportCloud, prune } from '../lib/backup.js';
import { handleBackup, handleDaily } from '../lib/backupRoutes.js';
import { memoryStorage } from '../lib/storage.js';
import { handle } from '../lib/router.js';
import { issueSession, login } from '../lib/auth.js';
import { caja1, caja2, centro, database, norte, permissions, user } from './helpers.js';

const SECRET = 'b'.repeat(40);
const env = { BACKUP_SECRET: SECRET, CRON_SECRET: 'c'.repeat(20), SESSION_SECRET: 's'.repeat(40) };
const sha = (data: Uint8Array) => createHash('sha256').update(data).digest('hex').toUpperCase();

async function keyed(q: Awaited<ReturnType<typeof database>>['q'], terminal: string, branch: string) {
  const key = 'k'.repeat(42) + terminal[0];
  await q(`UPDATE terminal SET key_sha256 = $2 WHERE id = $1`, [terminal, createHash('sha256').update(key).digest('hex')]);
  return key;
}

function client(q: Parameters<typeof handleBackup>[0], storage: ReturnType<typeof memoryStorage>, key: string) {
  return async (method: string, op: string, body?: unknown, query = '') => {
    const raw = body instanceof Uint8Array;
    const response = await handleBackup(q, () => storage, new Request(`https://x/api/backup/${op}${query}`, {
      method, headers: { authorization: `Bearer ${key}`, ...(raw ? {} : { 'content-type': 'application/json' }) },
      body: body === undefined ? undefined : (raw ? new Blob([body as Uint8Array<ArrayBuffer>]) : JSON.stringify(body)) }), op, env);
    const json = await response.json();
    if (!response.ok) throw new ProtocolError(json.error, response.status);
    return json;
  };
}

test('encryption round-trips, rejects tampering and keys differ per branch', () => {
  const a = deriveKey(SECRET, `branch:${centro}`);
  assert.deepEqual(a, deriveKey(SECRET, `branch:${centro}`));
  assert.notDeepEqual(a, deriveKey(SECRET, `branch:${norte}`));
  const sealed = encrypt(a, Buffer.from('ventas'));
  assert.equal(decrypt(a, sealed).toString(), 'ventas');
  sealed[sealed.length - 1] ^= 1;
  assert.throws(() => decrypt(a, sealed));
  assert.throws(() => decrypt(deriveKey(SECRET, `branch:${norte}`), encrypt(a, Buffer.from('x'))));
});

test('a terminal uploads its database in parts and photos once; the set and its encrypted manifest can be read back', async () => {
  const { q } = await database(); const storage = memoryStorage();
  const call = client(q, storage, await keyed(q, caja1, centro));
  const key = Buffer.from((await call('GET', 'key')).key, 'base64');
  assert.deepEqual(key, deriveKey(SECRET, `branch:${centro}`));
  assert.deepEqual(await call('GET', 'state'), { databaseSha256: null, at: null });

  const db = Buffer.from('SQLite format 3\0' + 'x'.repeat(5000));
  const sealed = encrypt(key, gzipSync(db));
  const half = Math.ceil(sealed.length / 2);
  const object = (await call('POST', 'object', { kind: 'database', sha256: sha(db), bytes: db.length, parts: 2 })).id;
  await call('PUT', 'part', sealed.subarray(0, half), `?object=${object}&index=0`);
  await assert.rejects(call('POST', 'complete', undefined, `?object=${object}`), /Faltan partes/);
  await call('PUT', 'part', sealed.subarray(half), `?object=${object}&index=1`);
  await assert.rejects(call('PUT', 'part', sealed.subarray(half), `?object=${object}&index=2`), /Parte inválida/);
  await call('POST', 'complete', undefined, `?object=${object}`);

  const photo = Buffer.from('jpeg-bytes'); const photoSha = sha(photo);
  const path = `product-images/v1/${'a'.repeat(32)}/original.jpg`;
  assert.deepEqual((await call('POST', 'missing', { shas: [photoSha] })).missing, [photoSha]);
  const media = [{ path, role: 'original', sha256: photoSha, bytes: photo.length }];
  await assert.rejects(call('POST', 'set', { databaseObject: object, terminalJson: '{}', schemaVersion: 24, media }), /Faltan fotos/);
  const photoObject = (await call('POST', 'object', { kind: 'photo', sha256: photoSha, bytes: photo.length, parts: 1 })).id;
  await call('PUT', 'part', encrypt(key, photo), `?object=${photoObject}&index=0`);
  await call('POST', 'complete', undefined, `?object=${photoObject}`);
  assert.deepEqual((await call('POST', 'missing', { shas: [photoSha] })).missing, [], 'A photo already up is never sent again.');

  const set = (await call('POST', 'set', { databaseObject: object, terminalJson: '{"Id":"x"}', schemaVersion: 24, media })).id;
  assert.equal((await call('GET', 'state')).databaseSha256, sha(db));

  // Otra caja no puede escribir en el respaldo de ésta.
  const other = client(q, storage, await keyed(q, caja2, norte));
  await assert.rejects(other('PUT', 'part', Buffer.from('x'), `?object=${object}&index=0`), (e: ProtocolError) => e.status === 404);

  // El manifiesto cifrado basta para reconstruir aunque se pierda la base de la nube.
  const [row] = await q('SELECT manifest_url FROM backup_set WHERE id = $1', [set]);
  const manifest = JSON.parse(decrypt(key, await storage.get(String(row.manifest_url))).toString());
  const parts = await Promise.all(manifest.database.parts.map((p: { url: string }) => storage.get(p.url)));
  assert.deepEqual(gunzipSync(decrypt(key, Buffer.concat(parts))), db);
  assert.deepEqual(decrypt(key, await storage.get(manifest.media[0].parts[0].url)), photo);
});

test('the administrator lists backups and gets what restore needs; managers cannot', async () => {
  const { q } = await database(); const storage = memoryStorage();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000002', 'ana', 'Manager', 'Ana#2026centro');
  const call = client(q, storage, await keyed(q, caja1, centro));
  const db = Buffer.from('db'); const object = (await call('POST', 'object', { kind: 'database', sha256: sha(db), bytes: 2, parts: 1 })).id;
  await call('PUT', 'part', Buffer.from('sealed'), `?object=${object}&index=0`); await call('POST', 'complete', undefined, `?object=${object}`);
  const set = (await call('POST', 'set', { databaseObject: object, terminalJson: '{}', schemaVersion: 24, media: [] })).id;

  const web = async (op: string, who: string, query = '') => handle(q, new Request(`https://x/api/web/${op}?${query}`, { headers: { cookie: `cm_web=${who}` } }), op, env);
  const boss = issueSession(await login(q, 'admin', 'Admin#2026centro'), env.SESSION_SECRET);
  const ana = issueSession(await login(q, 'ana', 'Ana#2026centro'), env.SESSION_SECRET);
  const listed = await (await web('backups', boss)).json();
  assert.equal(listed.sets[0].id, set); assert.equal(listed.sets[0].terminal, 'CASA-MIGA-CAJA-01');
  const restore = await (await web('backup', boss, `id=${set}`)).json();
  assert.deepEqual(Buffer.from(restore.key, 'base64'), deriveKey(SECRET, `branch:${centro}`));
  assert.equal(restore.database.parts.length, 1);
  assert.equal((await web('backups', ana)).status, 403);
  assert.equal((await web('backup', ana, `id=${set}`)).status, 403);
});

test('nightly copy of the cloud database and retention: 48 h complete, then one per day for 30 days', async () => {
  const { q } = await database(); const storage = memoryStorage();
  const call = client(q, storage, await keyed(q, caja1, centro));
  const now = new Date('2026-10-08T12:00:00Z');
  const make = async (hoursAgo: number, photo: string | null = null) => {
    const object = (await call('POST', 'object', { kind: 'database', sha256: 'A'.repeat(64), bytes: 1, parts: 1 })).id;
    await call('PUT', 'part', Buffer.from('p'), `?object=${object}&index=0`); await call('POST', 'complete', undefined, `?object=${object}`);
    const media = photo ? [{ path: `product-images/v1/${'b'.repeat(32)}/original.jpg`, role: 'original', sha256: photo, bytes: 1 }] : [];
    const id = (await call('POST', 'set', { databaseObject: object, terminalJson: '{}', schemaVersion: 24, media })).id;
    const at = new Date(now.getTime() - hoursAgo * 3_600_000).toISOString();
    await q('UPDATE backup_set SET created_at = $2 WHERE id = $1', [id, at]);
    await q('UPDATE backup_object SET created_at = $2 WHERE id = $1', [object, at]);
    return id;
  };
  const oldPhoto = 'D'.repeat(64);
  const photoObject = (await call('POST', 'object', { kind: 'photo', sha256: oldPhoto, bytes: 1, parts: 1 })).id;
  await call('PUT', 'part', Buffer.from('f'), `?object=${photoObject}&index=0`); await call('POST', 'complete', undefined, `?object=${photoObject}`);
  await q(`UPDATE backup_object SET created_at = $2 WHERE id = $1`, [photoObject, '2026-08-01T00:00:00Z']);

  const keep = [await make(1), await make(30), await make(60), await make(80 + 24)];   // reciente, reciente, último del 6/10, último del 4/10
  const drop = [await make(61), await make(40 * 24, oldPhoto)];                          // mismo día que uno más nuevo; más de 30 días
  const files = storage.files.size;
  const result = await prune(q, storage, 'America/Mexico_City', now);
  const left = new Set((await q('SELECT id FROM backup_set', [])).map(r => String(r.id)));
  for (const id of keep) assert.ok(left.has(id), 'kept');
  for (const id of drop) assert.ok(!left.has(id), 'dropped');
  assert.equal(result.sets, 2);
  assert.equal((await q(`SELECT count(*)::int AS n FROM backup_object WHERE kind = 'photo'`, []))[0].n, 0, 'The photo only the dropped backup used is removed.');
  assert.ok(storage.files.size < files, 'Their files are deleted from storage too.');

  const exported = await exportCloud(q, storage, SECRET);
  const [row] = await q(`SELECT o.id FROM backup_set s JOIN backup_object o ON o.id = s.database_object WHERE s.id = $1`, [exported.id]);
  const parts = await q('SELECT url FROM backup_part WHERE object_id = $1 ORDER BY index', [row.id]);
  const plain = gunzipSync(decrypt(deriveKey(SECRET, 'cloud'), Buffer.concat(await Promise.all(parts.map(p => storage.get(String(p.url)))))));
  const tables = new Set(plain.toString().split('\n').map(l => JSON.parse(l).table));
  assert.ok(tables.has('branch') && tables.has('terminal') && tables.has('schema_version'));

  const cron = (auth?: string) => handleDaily(q, () => storage, new Request('https://x/api/cron/daily', { headers: auth ? { authorization: auth } : {} }), env);
  assert.equal((await cron()).status, 401);
  assert.equal((await cron('Bearer otro')).status, 401);
  assert.equal((await handleDaily(q, () => storage, new Request('https://x/api/cron/daily'), { BACKUP_SECRET: SECRET })).status, 503, 'Without CRON_SECRET nobody can trigger it.');
  assert.equal((await cron(`Bearer ${env.CRON_SECRET}`)).status, 200);
});
