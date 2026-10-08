// Todas las rutas de la versión web en una sola función (/api/web/<op>), con la base inyectable para pruebas y revisión local.
import { failure, json, ProtocolError, readJson } from './protocol.js';
import { clearedCookie, issueSession, login, requirePermission, requireUser, scopeBranch, sessionCookie, sessionSecret, type WebUser } from './auth.js';
import { dashboard, DEFAULT_TIME_ZONE, MAX_DAYS, parseRange, terminals, branches, type Query, type Range } from './panel.js';
import * as web from './web.js';
import { createCommand, listCommands } from './commands.js';
import * as backups from './backup.js';

export interface Env { SESSION_SECRET?: string; PANEL_TIME_ZONE?: string; BACKUP_SECRET?: string }

/** Permiso que pide cada pantalla (el mismo de la caja). */
const VIEW: Record<string, string> = {
  dashboard: 'sales.view', sales: 'sales.view', sale: 'sales.view', compare: 'sales.view', heatmap: 'sales.view', 'cash-control': 'sales.view',
  'production-suggestion': 'sales.view', terminals: 'sales.view', cash: 'cash.view', consignment: 'consignment.view', catalog: 'catalog.view',
  photo: 'catalog.view', route: 'consignment.view', backups: 'users.manage', backup: 'users.manage', inventory: 'inventory.view', recipes: 'recipe.view', production: 'production.view', users: 'users.manage', commands: 'sales.view',
};

function me(user: WebUser) {
  return { id: user.id, name: user.name, username: user.username, role: user.role, admin: user.admin, branchId: user.branchId, permissions: [...user.permissions].sort() };
}

function day(value: string | null, timeZone: string): string {
  if (value && /^\d{4}-\d{2}-\d{2}$/.test(value)) return value;
  const tomorrow = new Date(Date.now() + 86_400_000);
  return tomorrow.toLocaleDateString('en-CA', { timeZone });
}

export async function handle(q: Query, request: Request, op: string, env: Env): Promise<Response> {
  try {
    const url = new URL(request.url);
    const timeZone = env.PANEL_TIME_ZONE || DEFAULT_TIME_ZONE;
    const secret = sessionSecret(env.SESSION_SECRET);
    if (request.method === 'POST') {
      // Sólo desde la propia página: otro sitio no puede iniciar sesión ni mandar cambios a nombre del usuario.
      const site = request.headers.get('sec-fetch-site');
      const origin = request.headers.get('origin');
      let sameOrigin = true;
      try { sameOrigin = !origin || new URL(origin).host === url.host; } catch { sameOrigin = false; }
      if (!sameOrigin || (site && site !== 'same-origin' && site !== 'none')) throw new ProtocolError('Origen no permitido', 403);
      if (!(request.headers.get('content-type') ?? '').toLowerCase().startsWith('application/json')) throw new ProtocolError('Formato no permitido', 415);
    }

    if (op === 'login' && request.method === 'POST') {
      const body = (await readJson(request, 2_000)) as { username?: unknown; password?: unknown } | null;
      const user = await login(q, body?.username, body?.password).catch(async error => {
        if (error instanceof ProtocolError && error.status === 401) await new Promise(r => setTimeout(r, 400));
        throw error;
      });
      const response = json(200, me(user));
      response.headers.append('set-cookie', sessionCookie(issueSession(user, secret)));
      return response;
    }
    if (op === 'logout' && request.method === 'POST') {
      const response = json(200, { ok: true });
      response.headers.append('set-cookie', clearedCookie);
      return response;
    }

    const user = await requireUser(q, request, secret);
    if (op === 'me') return json(200, { ...me(user), branches: (await branches(q)).filter(b => user.admin || b.id === user.branchId), timeZone, maxDays: MAX_DAYS });

    if (op === 'command' && request.method === 'POST') {
      const body = (await readJson(request, 50_000)) as { branchId?: unknown; kind?: unknown; payload?: unknown } | null;
      return json(200, await createCommand(q, user, body?.branchId, body?.kind, body?.payload));
    }
    if (request.method !== 'GET') throw new ProtocolError('Método no permitido', 405);

    const permission = VIEW[op];
    if (!permission) throw new ProtocolError('Ruta desconocida', 404);
    requirePermission(user, permission);
    const branch = scopeBranch(user, url.searchParams.get('branch') || null);
    const range = (): Range => ({ ...parseRange(url, timeZone), branchId: branch });

    switch (op) {
      case 'dashboard': return json(200, await dashboard(q, range()));
      case 'sales': {
        const page = Number(url.searchParams.get('page') ?? '0');
        const method = url.searchParams.get('method');
        return json(200, await web.salesList(q, { ...range(), search: (url.searchParams.get('q') ?? '').slice(0, 80),
          method: method === 'CASH' || method === 'CARD' || method === 'TRANSFER' ? method : null, page: Number.isSafeInteger(page) && page >= 0 && page < 1000 ? page : 0 }));
      }
      case 'sale': return json(200, await web.saleDetail(q, branch, url.searchParams.get('id') ?? ''));
      case 'compare': return json(200, await web.comparison(q, range()));
      case 'heatmap': return json(200, await web.heatmap(q, range()));
      case 'cash-control': return json(200, await web.cashControl(q, range()));
      case 'production-suggestion': return json(200, await web.productionSuggestion(q, branch, timeZone, day(url.searchParams.get('day'), timeZone)));
      case 'terminals': return json(200, await terminals(q, branch));
      case 'cash': {
        const r = range();
        const [data, movements, totals] = await Promise.all([dashboard(q, r), web.cashMovements(q, r), web.cashTotals(q, r)]);
        return json(200, { sessions: data.sessions, totals: data.totals, movements, cash: totals });
      }
      case 'consignment': return json(200, await web.consignment(q, range()));
      case 'catalog': return json(200, await web.catalog(q, branch));
      case 'route': return json(200, await web.route(q, branch));
      case 'photo': {
        const image = await web.photo(q, branch, url.searchParams.get('product') ?? '');
        if (!image) return new Response('Sin foto', { status: 404 });
        if (request.headers.get('if-none-match') === `"${image.sha}"`) return new Response(null, { status: 304 });
        return new Response(new Uint8Array(image.bytes), { headers: { 'content-type': 'image/jpeg', etag: `"${image.sha}"`, 'cache-control': 'private, max-age=86400' } });
      }
      case 'inventory': return json(200, await web.inventory(q, branch, timeZone));
      case 'recipes': return json(200, await web.recipes(q, branch));
      case 'production': return json(200, await web.production(q, range()));
      case 'users': return json(200, await web.users(q, branch));
      case 'commands': return json(200, await listCommands(q, branch));
      case 'backups': return json(200, { sets: await backups.list(q, branch), terminals: await terminals(q, branch) });
      case 'backup': {
        // Para restaurar con CasaMiga.Maintenance restore-cloud: direcciones de cada parte y la llave para descifrarlas.
        const found = await backups.detail(q, url.searchParams.get('id') ?? '');
        if (found.branchId && !user.admin && found.branchId !== user.branchId) throw new ProtocolError('Sin acceso a esa sucursal', 403);
        const secret = backups.backupSecret(env.BACKUP_SECRET);
        return json(200, { ...found, key: backups.deriveKey(secret, found.kind === 'cloud' ? 'cloud' : `branch:${found.branchId}`).toString('base64') });
      }
    }
    throw new ProtocolError('Ruta desconocida', 404);
  } catch (error) {
    return failure(error);
  }
}
