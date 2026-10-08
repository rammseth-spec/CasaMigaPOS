// Versión web local sin Neon ni Vercel: PGlite en memoria con la cola exportada de una copia de la app
// (CasaMiga.Maintenance export-sync <copia> <json>). Sólo desarrollo.
// Uso: node scripts/dev-web.ts <export.json> [puerto]
import { createServer } from 'node:http';
import { randomBytes } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PGlite } from '@electric-sql/pglite';
import { migrate } from '../lib/migrate.ts';
import { storeBatch } from '../lib/store.ts';
import { handle } from '../lib/router.ts';
import type { Query } from '../lib/panel.ts';

const [file, portText] = process.argv.slice(2);
if (!file) throw new Error('Falta el archivo exportado.');
const data = JSON.parse(readFileSync(file, 'utf8')) as { branch: { id: string; name: string }; terminal: { id: string; name: string }; entries: any[] };

const db = new PGlite();
await migrate({
  exec: async sql => { await db.exec(sql); },
  scalar: async sql => { const row = (await db.query(sql)).rows[0] as Record<string, unknown> | undefined; return row ? Object.values(row)[0] : null; },
});
const q: Query = async (text, params) => (await db.query(text, params)).rows as Record<string, unknown>[];
await q('INSERT INTO branch(id, name) VALUES ($1, $2)', [data.branch.id, data.branch.name]);
await q(`INSERT INTO terminal(id, branch_id, name, key_sha256) VALUES ($1, $2, $3, repeat('0', 64))`, [data.terminal.id, data.branch.id, data.terminal.name]);
for (let i = 0; i < data.entries.length; i += 200)
  await storeBatch(q, { id: data.terminal.id, branchId: data.branch.id }, { branchId: data.branch.id, entries: data.entries.slice(i, i + 200) });

const env = { SESSION_SECRET: process.env.SESSION_SECRET ?? randomBytes(32).toString('hex') };
const root = fileURLToPath(new URL('../public/', import.meta.url));
const types: Record<string, string> = { '.html': 'text/html; charset=utf-8', '.css': 'text/css', '.js': 'text/javascript', '.png': 'image/png', '.svg': 'image/svg+xml', '.webmanifest': 'application/manifest+json' };
const port = Number(portText ?? 4173);

createServer(async (req, res) => {
  const url = new URL(req.url ?? '/', `http://localhost:${port}`);
  const chunks: Buffer[] = [];
  for await (const chunk of req) chunks.push(chunk as Buffer);
  let response: Response;
  if (url.pathname.startsWith('/api/web/')) {
    const request = new Request(url, { method: req.method, headers: req.headers as Record<string, string>, body: chunks.length ? Buffer.concat(chunks) : undefined });
    response = await handle(q, request, url.pathname.split('/').pop() ?? '', env);
  } else {
    const path = normalize(url.pathname === '/' ? 'index.html' : decodeURIComponent(url.pathname.slice(1)));
    try {
      if (path.startsWith('..')) throw new Error('ruta');
      response = new Response(readFileSync(join(root, path)), { headers: { 'content-type': types[extname(path)] ?? 'application/octet-stream' } });
    } catch { response = new Response('No encontrado', { status: 404 }); }
  }
  const headers: [string, string][] = [];
  response.headers.forEach((value, key) => headers.push([key, value]));
  res.writeHead(response.status, headers);
  res.end(Buffer.from(await response.arrayBuffer()));
}).listen(port, () => console.log(`Versión web local en http://localhost:${port} (${data.entries.length} registros)`));
