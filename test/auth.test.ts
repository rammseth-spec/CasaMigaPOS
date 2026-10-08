import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ProtocolError } from '../lib/protocol.ts';
import { identityHash, issueSession, login, readSession, requireUser, scopeBranch, temporaryPassword, verifyIdentityHash, MAX_FAILURES } from '../lib/auth.ts';
import { caja1, caja2, centro, database, norte, permissions, user } from './helpers.ts';

const SECRET = 'x'.repeat(40);
const admin = 'a0000000-0000-4000-8000-000000000001';
const manager = 'a0000000-0000-4000-8000-000000000002';
const cashier = 'a0000000-0000-4000-8000-000000000003';
const managerNorte = 'a0000000-0000-4000-8000-000000000004';

async function seeded() {
  const { q } = await database();
  await permissions(q, caja1, centro); await permissions(q, caja2, norte);
  await user(q, caja1, centro, admin, 'admin', 'Administrator', 'Admin#2026centro');
  await user(q, caja1, centro, manager, 'ana', 'Manager', 'Ana#2026centro');
  await user(q, caja1, centro, cashier, 'luis', 'Cashier', 'Luis#2026centro');
  await user(q, caja2, norte, managerNorte, 'ana', 'Manager', 'Ana#2026norte');
  return q;
}
const status = (code: number) => (e: unknown) => e instanceof ProtocolError && e.status === code;

test('hash from the terminal (.NET Identity V3) and from the cloud verify the same way', async () => {
  // Vector generado por la caja (PasswordHasher, PBKDF2-SHA512, 210 000 iteraciones): ver CloudPasswordCompatibilityTests.
  const cloud = 'AQAAAAIAAzRQAAAAEBxRae3FEw6FtrMg/PLCO2PyRY0QCpPaCsZdt3OpOY7AbpYhUQCF95JqER4xcPLzdA==';
  assert.equal(await verifyIdentityHash(cloud, 'Prueba#2026nube'), true);
  assert.equal(await verifyIdentityHash(cloud, 'Prueba#2026Nube'), false);
  assert.equal(await verifyIdentityHash('basura', 'x'), false);
  assert.equal(await verifyIdentityHash(Buffer.from([0x00, 1, 2, 3]).toString('base64'), 'x'), false, 'Identity V2 or malformed hashes are rejected.');
  assert.match(await identityHash('x', 10_000), /^AQAAAAIAACcQAAAAE/);
});

test('temporary passwords follow the terminal policy', () => {
  for (let i = 0; i < 50; i++) {
    const p = temporaryPassword();
    assert.equal(p.length, 12);
    assert.match(p, /[A-Z]/); assert.match(p, /[a-z]/); assert.match(p, /[0-9]/); assert.match(p, /[#$%*+=?!]/);
  }
});

test('admin and managers sign in with their terminal password; cashiers and temporary passwords cannot', async () => {
  const q = await seeded();
  const signed = await login(q, ' Admin ', 'Admin#2026centro');
  assert.equal(signed.admin, true); assert.equal(signed.branchId, centro);
  const ana = await login(q, 'ana', 'Ana#2026norte');
  assert.equal(ana.id, managerNorte, 'Same username in two branches: the password tells which one.');
  assert.equal(ana.admin, false);
  await assert.rejects(login(q, 'luis', 'Luis#2026centro'), status(403));
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000005', 'nuevo', 'Manager', 'Temporal#2026', { must_change_password: 1 });
  await assert.rejects(login(q, 'nuevo', 'Temporal#2026'), /temporal/);
  await assert.rejects(login(q, 'nadie', 'x'), status(401));
  await assert.rejects(login(q, '', 'x'), ProtocolError);
});

test('five failures lock the user in the web for fifteen minutes; success resets', async () => {
  const q = await seeded();
  const now = new Date('2026-10-07T12:00:00Z');
  await assert.rejects(login(q, 'admin', 'mal', now), status(401));
  await login(q, 'admin', 'Admin#2026centro', now);
  for (let i = 0; i < MAX_FAILURES; i++) await assert.rejects(login(q, 'admin', 'mal', now), status(401));
  await assert.rejects(login(q, 'admin', 'Admin#2026centro', now), status(429));
  await assert.rejects(login(q, 'admin', 'Admin#2026centro', new Date('2026-10-07T12:14:00Z')), status(429));
  const later = await login(q, 'admin', 'Admin#2026centro', new Date('2026-10-07T12:16:00Z'));
  assert.equal(later.id, admin);
});

test('session cookie is signed and revalidated against the latest synced user', async () => {
  const q = await seeded();
  const ana = await login(q, 'ana', 'Ana#2026centro');
  const cookie = issueSession(ana, SECRET);
  const request = (value: string) => new Request('https://x/api/web/me', { headers: { cookie: `cm_web=${value}` } });
  assert.equal((await requireUser(q, request(cookie), SECRET)).id, manager);
  assert.equal(readSession(cookie, 'y'.repeat(40)), null, 'Another secret does not validate.');
  assert.equal(readSession(cookie.replace(/\.\d{13}\./, `.${Date.now() + 999_999_999}.`), SECRET), null, 'Tampered expiry.');
  assert.equal(readSession(cookie, SECRET, Date.now() + 13 * 3_600_000), null, 'Expires after 12 h.');

  // La caja cambia la contraseña: la sesión web anterior deja de valer.
  await user(q, caja1, centro, manager, 'ana', 'Manager', 'Ana#2026nueva');
  await assert.rejects(requireUser(q, request(cookie), SECRET), status(401));
  const again = issueSession(await login(q, 'ana', 'Ana#2026nueva'), SECRET);
  // Desactivada en la caja: fuera.
  await user(q, caja1, centro, manager, 'ana', 'Manager', 'Ana#2026nueva', { active: 0 });
  await assert.rejects(requireUser(q, request(again), SECRET), status(401));
});

test('managers only reach their branch; the administrator reaches all', async () => {
  const q = await seeded();
  const ana = await login(q, 'ana', 'Ana#2026centro');
  assert.equal(scopeBranch(ana, null), centro);
  assert.equal(scopeBranch(ana, centro), centro);
  assert.throws(() => scopeBranch(ana, norte), status(403));
  const boss = await login(q, 'admin', 'Admin#2026centro');
  assert.equal(scopeBranch(boss, null), null);
  assert.equal(scopeBranch(boss, norte), norte);
});
