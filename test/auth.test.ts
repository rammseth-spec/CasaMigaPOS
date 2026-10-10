import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ProtocolError } from '../lib/protocol.js';
import { changePassword, identityHash, issueSession, login, passwordProblems, readSession, requireUser, scopeBranch, temporaryPassword, verifyIdentityHash, MAX_FAILURES } from '../lib/auth.js';
import { listCommands, pendingFor } from '../lib/commands.js';
import { caja1, caja2, centro, database, norte, permissions, user } from './helpers.js';

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

test('admin, managers and cashiers sign in with their terminal password; temporary passwords and roles without sales or POS cannot', async () => {
  const q = await seeded();
  const signed = await login(q, ' Admin ', 'Admin#2026centro');
  assert.equal(signed.admin, true); assert.equal(signed.branchId, centro);
  const ana = await login(q, 'ana', 'Ana#2026norte');
  assert.equal(ana.id, managerNorte, 'Same username in two branches: the password tells which one.');
  assert.equal(ana.admin, false);
  const luis = await login(q, 'luis', 'Luis#2026centro');
  assert.equal(luis.permissions.has('sales.view'), false, 'The cashier only gets the point of sale.');
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000006', 'bodega', 'Stock', 'Bodega#2026');
  await assert.rejects(login(q, 'bodega', 'Bodega#2026'), status(403));
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

test('simultaneous guesses are counted before checking the password: at most five get through', async () => {
  const q = await seeded();
  const now = new Date('2026-10-07T12:00:00Z');
  const results = await Promise.allSettled(Array.from({ length: 20 }, () => login(q, 'admin', 'mal', now)));
  const status = (r: PromiseSettledResult<unknown>) => (r.status === 'rejected' ? (r.reason as ProtocolError).status : 200);
  assert.ok(results.filter(r => status(r) === 401).length <= 5, 'At most five password checks.');
  assert.ok(results.filter(r => status(r) === 429).length >= 15);
  await assert.rejects(login(q, 'admin', 'Admin#2026centro', now), (e: unknown) => e instanceof ProtocolError && e.status === 429);
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
  assert.equal(scopeBranch(boss, norte.toUpperCase()), norte);
  assert.throws(() => scopeBranch(boss, "x' OR 1=1"), status(400));
});

test('change password like the till: the temporary one leads to it, the policy applies, it works on the web at once and reaches the till', async () => {
  const q = await seeded();
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000005', 'nuevo', 'Manager', 'Temporal#2026', { must_change_password: 1 });
  await assert.rejects(login(q, 'nuevo', 'Temporal#2026'), (e: unknown) => e instanceof ProtocolError && e.status === 428, 'A temporary password opens «Cambiar contraseña».');
  assert.deepEqual(passwordProblems('corta'), ['de 8 a 16 caracteres', 'una mayúscula', 'un número', 'un carácter especial (# $ % & * ! …)']);
  await assert.rejects(changePassword(q, 'nuevo', 'Temporal#2026', 'sinsimbolos2026'), /mayúscula, un carácter especial/);
  await assert.rejects(changePassword(q, 'nuevo', 'Temporal#2026', 'Temporal#2026'), /distinta/);
  await assert.rejects(changePassword(q, 'nuevo', 'Otra#2026x', 'Nueva#2026ok'), (e: unknown) => e instanceof ProtocolError && e.status === 401);
  await changePassword(q, 'nuevo', 'Temporal#2026', 'Nueva#2026ok');
  const signed = await login(q, 'nuevo', 'Nueva#2026ok');
  assert.equal(signed.username, 'NUEVO', 'The new password works on the web right away.');
  await assert.rejects(login(q, 'nuevo', 'Temporal#2026'), (e: unknown) => e instanceof ProtocolError && e.status === 401);
  const [pulled] = await pendingFor(q, { id: caja1, branchId: centro }, 0);
  assert.equal(pulled.kind, 'user.password');
  assert.equal(pulled.requestedBy.id, 'a0000000-0000-4000-8000-000000000005', 'The till applies it as the user themself.');
  assert.ok(await verifyIdentityHash(String((pulled.payload as Record<string, unknown>).passwordHash), 'Nueva#2026ok'));
  assert.equal((await listCommands(q, centro))[0].payload.passwordHash, undefined, 'The hash never reaches the list of changes.');
});
