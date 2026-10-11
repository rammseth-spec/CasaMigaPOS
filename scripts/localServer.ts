// Servidor local de la versión web (sin Vercel): sirve public/ y las rutas /api como en producción sobre una base PGlite.
// Lo usan scripts/dev-web.ts, las pruebas en navegador (test-browser/) y la prueba de punta a punta con la caja (tests/CasaMiga.E2E).
// Sólo desarrollo y pruebas: scripts/ no se publica (.vercelignore).
import { createServer, type Server } from 'node:http';
import { randomBytes } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { AddressInfo } from 'node:net';
import { PGlite } from '@electric-sql/pglite';
import { migrate } from '../lib/migrate.js';
import { handle } from '../lib/router.js';
import { handleTerminal } from '../lib/terminal.js';
import { handleBackup, handleDaily } from '../lib/backupRoutes.js';
import { memoryStorage } from '../lib/storage.js';
import { canUseWeb, issueSession, sessionCookie, toUser } from '../lib/auth.js';
import type { Query } from '../lib/panel.js';

export interface LocalEnv { SESSION_SECRET: string; ENROLLMENT_SECRET?: string; BACKUP_SECRET: string; CRON_SECRET: string }

/** Base PGlite en memoria con todas las migraciones de la nube. */
export async function memoryDatabase(): Promise<{ db: PGlite; q: Query }> {
  const db = new PGlite();
  await migrate({
    exec: async sql => { await db.exec(sql); },
    scalar: async sql => { const row = (await db.query(sql)).rows[0] as Record<string, unknown> | undefined; return row ? Object.values(row)[0] : null; },
  });
  return { db, q: async (text, params) => (await db.query(text, params)).rows as Record<string, unknown>[] };
}

export function localEnv(enrollmentSecret = process.env.ENROLLMENT_SECRET): LocalEnv {
  return { SESSION_SECRET: process.env.SESSION_SECRET ?? randomBytes(32).toString('hex'), ENROLLMENT_SECRET: enrollmentSecret,
    BACKUP_SECRET: process.env.BACKUP_SECRET ?? randomBytes(32).toString('hex'), CRON_SECRET: process.env.CRON_SECRET ?? randomBytes(16).toString('hex') };
}

/**
 * Arranca el servidor (puerto 0 = uno libre) y devuelve su dirección. noLogin agrega /api/web/dev-users y /api/web/dev-login para entrar
 * eligiendo el usuario, sin contraseña.
 */
export async function startLocalServer(q: Query, options: { port?: number; noLogin?: boolean; env?: LocalEnv } = {}): Promise<{ server: Server; url: string; env: LocalEnv }> {
  const env = options.env ?? localEnv();
  const noLogin = options.noLogin ?? false;
  const root = fileURLToPath(new URL('../public/', import.meta.url));
  const types: Record<string, string> = { '.html': 'text/html; charset=utf-8', '.css': 'text/css', '.js': 'text/javascript', '.png': 'image/png', '.svg': 'image/svg+xml', '.webmanifest': 'application/manifest+json' };
  let base = '';
  // Respaldos en memoria, servidos en /dev-blob/ como lo haría Vercel Blob.
  let storage: ReturnType<typeof memoryStorage> | null = null;
  const blobs = () => storage ??= memoryStorage(`${base}/dev-blob/`);

  const server = createServer(async (req, res) => {
    // Con el host pedido, para abrirla también desde un celular en la misma red (http://<IP de la PC>:<puerto>).
    const url = new URL(req.url ?? '/', `http://${req.headers.host ?? 'localhost'}`);
    const chunks: Buffer[] = [];
    for await (const chunk of req) chunks.push(chunk as Buffer);
    const request = () => new Request(url, { method: req.method, headers: req.headers as Record<string, string>, body: chunks.length ? Buffer.concat(chunks) : undefined });
    let response: Response;
    const terminalRoute = ({ '/api/terminals/register': 'register', '/api/sync/batch': 'batch', '/api/sync/commands': 'commands', '/api/sync/web-sales': 'web-sales' } as const)[url.pathname as '/api/sync/batch'];
    try {
      if (url.pathname.startsWith('/dev-blob/')) {
        const data = blobs().files.get(`${base}${url.pathname}`);
        response = data ? new Response(new Uint8Array(data), { headers: { 'content-type': 'application/octet-stream' } }) : new Response('No encontrado', { status: 404 });
      } else if (url.pathname.startsWith('/api/backup/') || url.pathname === '/api/cron/daily') {
        response = url.pathname === '/api/cron/daily'
          ? await handleDaily(q, blobs, request(), env)
          : await handleBackup(q, blobs, request(), url.pathname.split('/').pop() ?? '', env);
      } else if (terminalRoute) {
        // Las cajas de prueba se registran contra este servidor con ENROLLMENT_SECRET.
        response = await handleTerminal(q, request(), terminalRoute, env);
      } else if (noLogin && url.pathname === '/api/web/dev-users') {
        const rows = await q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'app_user' AND NOT deleted AND (payload->>'active')::int = 1`, []);
        const order = ['Administrator', 'Manager', 'Cashier'];
        const users = [];
        for (const row of rows) { const u = await toUser(q, row); if (canUseWeb(u.permissions)) users.push({ username: u.username, name: u.name, role: u.role }); }
        users.sort((a, b) => order.indexOf(a.role) - order.indexOf(b.role) || a.username.localeCompare(b.username));
        response = Response.json(users);
      } else if (noLogin && url.pathname === '/api/web/dev-login' && req.method === 'POST') {
        let username = '';
        try { username = String(JSON.parse(Buffer.concat(chunks).toString('utf8')).username ?? '').trim().toUpperCase(); } catch { /* vacío */ }
        const [row] = await q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'app_user' AND NOT deleted AND (payload->>'active')::int = 1 AND upper(payload->>'username') = $1`, [username]);
        const user = row ? await toUser(q, row) : null;
        response = !user || !canUseWeb(user.permissions) ? Response.json({ error: 'Usuario sin acceso a la web' }, { status: 403 })
          : Response.json({ ok: true }, { headers: { 'set-cookie': sessionCookie(issueSession(user, env.SESSION_SECRET)) } });
      } else if (url.pathname.startsWith('/api/web/')) {
        response = await handle(q, request(), url.pathname.split('/').pop() ?? '', env);
      } else {
        const path = normalize(url.pathname === '/' ? 'index.html' : decodeURIComponent(url.pathname.slice(1)));
        try {
          if (path.startsWith('..')) throw new Error('ruta');
          response = new Response(readFileSync(join(root, path)), { headers: { 'content-type': types[extname(path)] ?? 'application/octet-stream', 'cache-control': 'no-store' } });
        } catch { response = new Response('No encontrado', { status: 404 }); }
      }
    } catch (error) {
      response = Response.json({ error: error instanceof Error ? error.message : 'Error' }, { status: 500 });
    }
    const headers: [string, string][] = [];
    // Sólo local (http): sin «Secure» para que el navegador y las herramientas reenvíen la cookie. En producción siempre es https.
    response.headers.forEach((value, key) => headers.push([key, key === 'set-cookie' ? value.replace('; Secure', '') : value]));
    res.writeHead(response.status, headers);
    res.end(Buffer.from(await response.arrayBuffer()));
  });
  await new Promise<void>(resolve => server.listen(options.port ?? 0, resolve));
  base = `http://localhost:${(server.address() as AddressInfo).port}`;
  return { server, url: base, env };
}
