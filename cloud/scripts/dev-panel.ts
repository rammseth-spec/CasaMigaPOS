// Panel local para revisar la página sin Neon: PGlite en memoria con eventos exportados de una copia de la app.
// Uso: PANEL_PASSWORD=<12+ caracteres> node scripts/dev-panel.ts <events.json> [puerto]
import { createServer } from 'node:http';
import { readFileSync } from 'node:fs';
import { extname, join, normalize } from 'node:path';
import { PGlite } from '@electric-sql/pglite';
import { failure, json } from '../lib/protocol.ts';
import { dashboard, DEFAULT_TIME_ZONE, parseRange, requireSession, type Query } from '../lib/panel.ts';
import { POST as login } from '../api/panel/login.ts';
import { POST as logout } from '../api/panel/logout.ts';

const [file, portText] = process.argv.slice(2);
if (!file) throw new Error('Falta el archivo de eventos.');
const data = JSON.parse(readFileSync(file, 'utf8')) as {
  branch: { id: string; name: string }; terminal: { id: string; name: string };
  events: { seq: number; entity: string; entityId: string; change: string; enqueuedAt: string; payload: unknown }[];
};

const db = new PGlite();
await db.exec(readFileSync(new URL('../db/001_init.sql', import.meta.url), 'utf8'));
await db.query('INSERT INTO branch(id, name) VALUES ($1, $2)', [data.branch.id, data.branch.name]);
await db.query(`INSERT INTO terminal(id, branch_id, name, key_sha256, last_seen_at) VALUES ($1, $2, $3, repeat('0', 64), now())`,
  [data.terminal.id, data.branch.id, data.terminal.name]);
await db.query(`INSERT INTO sync_event(terminal_id, seq, branch_id, entity, entity_id, change, enqueued_at, payload)
  SELECT $1, r.seq, $2, r.entity, r."entityId", r.change, r."enqueuedAt", r.payload
  FROM jsonb_to_recordset($3::jsonb) AS r(seq bigint, entity text, "entityId" text, change text, "enqueuedAt" timestamptz, payload jsonb)`,
  [data.terminal.id, data.branch.id, JSON.stringify(data.events)]);
const q: Query = async (text, params) => (await db.query(text, params)).rows as Record<string, unknown>[];

const root = new URL('../public/', import.meta.url);
const types: Record<string, string> = { '.html': 'text/html; charset=utf-8', '.css': 'text/css', '.js': 'text/javascript', '.png': 'image/png' };
const port = Number(portText ?? 4173);

createServer(async (req, res) => {
  const url = new URL(req.url ?? '/', `http://localhost:${port}`);
  const chunks: Buffer[] = [];
  for await (const chunk of req) chunks.push(chunk as Buffer);
  const request = new Request(url, { method: req.method, headers: req.headers as Record<string, string>, body: chunks.length ? Buffer.concat(chunks) : undefined });
  let response: Response;
  try {
    if (url.pathname === '/api/panel/login' && req.method === 'POST') response = await login(request);
    else if (url.pathname === '/api/panel/logout' && req.method === 'POST') response = await logout();
    else if (url.pathname === '/api/panel/data') {
      requireSession(request, process.env.PANEL_PASSWORD);
      response = json(200, await dashboard(q, parseRange(url, DEFAULT_TIME_ZONE)));
    } else {
      const path = normalize(url.pathname === '/' ? 'index.html' : url.pathname.slice(1));
      if (path.startsWith('..')) throw new Error('Ruta inválida');
      response = new Response(readFileSync(join(root.pathname.replace(/^\/([A-Za-z]:)/, '$1'), path)), { headers: { 'content-type': types[extname(path)] ?? 'application/octet-stream' } });
    }
  } catch (error) {
    response = (error as NodeJS.ErrnoException).code === 'ENOENT' ? new Response('No encontrado', { status: 404 }) : failure(error);
  }
  res.writeHead(response.status, Object.fromEntries(response.headers));
  res.end(Buffer.from(await response.arrayBuffer()));
}).listen(port, () => console.log(`Panel local en http://localhost:${port} (${data.events.length} eventos)`));
