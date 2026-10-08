// Acceso a la versión web con el usuario y la contraseña de la caja (docs/WEB.md).
// La contraseña sólo se compara contra el hash PBKDF2 que subió la caja; nunca se guarda ni viaja de regreso.
import { createHash, createHmac, pbkdf2, randomBytes, timingSafeEqual } from 'node:crypto';
import { promisify } from 'node:util';
import { ProtocolError } from './protocol.js';
import type { Query, Row } from './panel.js';

const derive = promisify(pbkdf2);
export const COOKIE = 'cm_web';
export const SESSION_HOURS = 12;
export const MAX_FAILURES = 5;
export const LOCK_MINUTES = 15;
/** Para entrar a la web: consultar ventas (administrador, encargado) o vender (cajero, sólo el punto de venta). */
export const WEB_PERMISSIONS = ['sales.view', 'pos.use'] as const;
export const canUseWeb = (permissions: Set<string>) => WEB_PERMISSIONS.some(p => permissions.has(p));

// ---------- Hash de ASP.NET Identity V3 (el mismo de la caja) ----------

const DIGESTS: Record<number, string> = { 0: 'sha1', 1: 'sha256', 2: 'sha512' };

export async function verifyIdentityHash(hash: string, password: string): Promise<boolean> {
  let bytes: Buffer;
  try { bytes = Buffer.from(hash, 'base64'); } catch { return false; }
  if (bytes.length < 13 || bytes[0] !== 0x01) return false;
  const digest = DIGESTS[bytes.readUInt32BE(1)];
  const iterations = bytes.readUInt32BE(5);
  const saltLength = bytes.readUInt32BE(9);
  if (!digest || iterations < 10_000 || iterations > 2_000_000 || saltLength < 16 || saltLength > 64 || bytes.length < 13 + saltLength + 16) return false;
  const salt = bytes.subarray(13, 13 + saltLength);
  const expected = bytes.subarray(13 + saltLength);
  const actual = await derive(password, salt, iterations, expected.length, digest);
  return timingSafeEqual(actual, expected);
}

/** Hash que la caja acepta (PBKDF2-SHA512, 210 000 iteraciones), para contraseñas temporales creadas en la web. */
export async function identityHash(password: string, iterations = 210_000): Promise<string> {
  const salt = randomBytes(16);
  const subkey = await derive(password, salt, iterations, 32, 'sha512');
  const header = Buffer.alloc(13);
  header[0] = 0x01; header.writeUInt32BE(2, 1); header.writeUInt32BE(iterations, 5); header.writeUInt32BE(salt.length, 9);
  return Buffer.concat([header, salt, subkey]).toString('base64');
}

/** Contraseña temporal con la política de la caja: 12 caracteres con mayúscula, minúscula, número y símbolo. */
export function temporaryPassword(): string {
  const sets = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnopqrstuvwxyz', '23456789', '#$%*+=?!'];
  const all = sets.join('');
  const pick = (s: string) => s[randomBytes(1)[0] % s.length];
  const chars = [...sets.map(pick), ...Array.from({ length: 8 }, () => pick(all))];
  for (let i = chars.length - 1; i > 0; i--) { const j = randomBytes(1)[0] % (i + 1); [chars[i], chars[j]] = [chars[j], chars[i]]; }
  return chars.join('');
}

// ---------- Usuarios y permisos sincronizados ----------

export interface WebUser {
  id: string; branchId: string; username: string; name: string; role: string;
  permissions: Set<string>; admin: boolean; fingerprint: string;
}

const fingerprint = (hash: string) => createHash('sha256').update(hash).digest('hex').slice(0, 16);
export const normalize = (username: string) => username.trim().toUpperCase();

async function permissionsFor(q: Query, branchId: string, role: string): Promise<Set<string>> {
  const rows = await q(`SELECT r->>'permission_name' AS permission FROM entity_state, jsonb_array_elements(payload->'rows') AS r
    WHERE entity = 'role_permission' AND entity_id = 'all' AND branch_id = $1 AND r->>'role_name' = $2`, [branchId, role]);
  return new Set(rows.map(r => String(r.permission)));
}

export async function toUser(q: Query, row: Row): Promise<WebUser> {
  const p = row.payload as Record<string, unknown>;
  const role = String(p.role_name);
  return {
    id: String(p.id), branchId: String(row.branch_id), username: String(p.username), name: String(p.display_name), role,
    permissions: await permissionsFor(q, String(row.branch_id), role), admin: role === 'Administrator', fingerprint: fingerprint(String(p.password_hash)),
  };
}

let dummy: Promise<string> | null = null;
const dummyHash = () => (dummy ??= identityHash(randomBytes(12).toString('hex')));

/** Entra con el usuario de la caja. Cinco errores seguidos bloquean ese usuario 15 minutos en la web (la caja no se afecta). */
export async function login(q: Query, username: unknown, password: unknown, now = new Date()): Promise<WebUser> {
  if (typeof username !== 'string' || typeof password !== 'string' || !username.trim() || username.length > 80 || !password || password.length > 200)
    throw new ProtocolError('Escribe usuario y contraseña');
  const key = normalize(username);
  const at = now.toISOString();
  const locked = () => new ProtocolError('Usuario bloqueado por intentos fallidos. Intenta en unos minutos.', 429);
  // El intento se cuenta ANTES de revisar la contraseña y en una sola sentencia: cien intentos simultáneos no pasan de cinco.
  const [attempt] = await q(`INSERT INTO web_lockout(username, failures, locked_until, updated_at) VALUES ($1, 1, NULL, $2)
    ON CONFLICT (username) DO UPDATE SET
      failures = CASE WHEN web_lockout.locked_until IS NOT NULL AND web_lockout.locked_until <= $2 THEN 1 ELSE web_lockout.failures + 1 END,
      locked_until = CASE WHEN web_lockout.locked_until IS NOT NULL AND web_lockout.locked_until <= $2 THEN NULL ELSE web_lockout.locked_until END,
      updated_at = $2
    RETURNING failures, locked_until`, [key, at]);
  if (attempt.locked_until && new Date(String(attempt.locked_until)) > now) throw locked();
  if (Number(attempt.failures) > MAX_FAILURES) {
    await q(`UPDATE web_lockout SET locked_until = $2::timestamptz + make_interval(mins => $3) WHERE username = $1 AND locked_until IS NULL`, [key, at, LOCK_MINUTES]);
    throw locked();
  }

  const candidates = await q(`SELECT branch_id, payload FROM entity_state
    WHERE entity = 'app_user' AND NOT deleted AND upper(payload->>'username') = $1 AND (payload->>'active')::int = 1`, [key]);
  let match: Row | undefined;
  for (const c of candidates) if (await verifyIdentityHash(String((c.payload as Row).password_hash), password)) { match = c; break; }
  if (candidates.length === 0) await verifyIdentityHash(await dummyHash(), password); // mismo tiempo exista o no el usuario

  if (!match) {
    if (Number(attempt.failures) >= MAX_FAILURES)
      await q(`UPDATE web_lockout SET locked_until = $2::timestamptz + make_interval(mins => $3) WHERE username = $1 AND locked_until IS NULL`, [key, at, LOCK_MINUTES]);
    throw new ProtocolError('Usuario o contraseña incorrectos', 401);
  }
  await q('DELETE FROM web_lockout WHERE username = $1', [key]);
  if (Number((match.payload as Row).must_change_password) === 1)
    throw new ProtocolError('Tu contraseña es temporal: cámbiala primero en la caja.', 403);
  const user = await toUser(q, match);
  if (!canUseWeb(user.permissions)) throw new ProtocolError('Tu usuario no tiene acceso a la versión web.', 403);
  return user;
}

// ---------- Sesión ----------

export function sessionSecret(env: string | undefined): string {
  if (!env || env.length < 32) throw new ProtocolError('Versión web sin configurar (SESSION_SECRET)', 503);
  return env;
}

const UUID = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}';
const sign = (secret: string, body: string) => createHmac('sha256', secret).update(body).digest('base64url');

export function issueSession(user: WebUser, secret: string, now = Date.now()): string {
  const body = `${user.id}.${user.branchId}.${now + SESSION_HOURS * 3_600_000}.${user.fingerprint}`;
  return `${body}.${sign(secret, body)}`;
}

export function readSession(value: string | undefined, secret: string, now = Date.now()) {
  const match = new RegExp(`^(${UUID})\\.(${UUID})\\.(\\d{13})\\.([0-9a-f]{16})\\.([A-Za-z0-9_-]{43})$`, 'i').exec(value ?? '');
  if (!match) return null;
  const [, userId, branchId, expires, print, signature] = match;
  const expected = Buffer.from(sign(secret, `${userId}.${branchId}.${expires}.${print}`));
  if (!timingSafeEqual(Buffer.from(signature), expected)) return null;
  if (!(Number(expires) > now) || Number(expires) > now + SESSION_HOURS * 3_600_000) return null;
  return { userId: userId.toLowerCase(), branchId: branchId.toLowerCase(), fingerprint: print };
}

export function readCookie(header: string | null, name = COOKIE): string | undefined {
  for (const part of (header ?? '').split(';')) {
    const [k, ...v] = part.trim().split('=');
    if (k === name) return v.join('=');
  }
  return undefined;
}

export const sessionCookie = (value: string) => `${COOKIE}=${value}; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=${SESSION_HOURS * 3600}`;
export const clearedCookie = `${COOKIE}=; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=0`;

/** Usuario de la cookie, revalidado contra lo último que subió la caja: desactivado, sin permiso o con contraseña nueva ya no entra. */
export async function requireUser(q: Query, request: Request, secret: string): Promise<WebUser> {
  const session = readSession(readCookie(request.headers.get('cookie')), secret);
  if (!session) throw new ProtocolError('Inicia sesión', 401);
  const [row] = await q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'app_user' AND NOT deleted AND branch_id = $1 AND entity_id = $2`,
    [session.branchId, session.userId]);
  if (!row || Number((row.payload as Row).active) !== 1) throw new ProtocolError('Inicia sesión', 401);
  const user = await toUser(q, row);
  if (user.fingerprint !== session.fingerprint || !canUseWeb(user.permissions)) throw new ProtocolError('Inicia sesión', 401);
  return user;
}

/** Sucursales que puede ver: el administrador todas; los demás la suya. */
export function scopeBranch(user: WebUser, requested: string | null): string | null {
  if (requested && !new RegExp(`^${UUID}$`, 'i').test(requested)) throw new ProtocolError('Sucursal inválida');
  requested = requested?.toLowerCase() ?? null;
  if (user.admin) return requested;
  if (requested && requested !== user.branchId) throw new ProtocolError('Sin acceso a esa sucursal', 403);
  return user.branchId;
}

export function requirePermission(user: WebUser, permission: string): void {
  if (!user.permissions.has(permission)) throw new ProtocolError('Tu rol no permite esta acción', 403);
}
