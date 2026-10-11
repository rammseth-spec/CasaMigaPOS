// Versión web local sin Neon ni Vercel: PGlite en memoria con la cola exportada de una copia de la app
// (CasaMiga.Maintenance export-sync <copia> <json>). Sólo desarrollo.
// Uso: node scripts/dev-web.ts <export.json> [puerto] [--con-login]
// Por omisión entra SIN contraseña (eligiendo el usuario): sólo para probar en local. scripts/ no se publica (.vercelignore).
import { readFileSync } from 'node:fs';
import { storeBatch } from '../lib/store.js';
import { toUser } from '../lib/auth.js';
import { createTill } from '../lib/pos.js';
import { localEnv, memoryDatabase, startLocalServer } from './localServer.js';

const args = process.argv.slice(2);
const [file, portText] = args.filter(a => !a.startsWith('--'));
const noLogin = !args.includes('--con-login');
if (!file) throw new Error('Falta el archivo exportado.');
const data = JSON.parse(readFileSync(file, 'utf8')) as { branch: { id: string; name: string }; terminal: { id: string; name: string }; entries: any[] };

const { q } = await memoryDatabase();
await q('INSERT INTO branch(id, name) VALUES ($1, $2)', [data.branch.id, data.branch.name]);
await q(`INSERT INTO terminal(id, branch_id, name, key_sha256) VALUES ($1, $2, $3, repeat('0', 64))`, [data.terminal.id, data.branch.id, data.terminal.name]);
for (let i = 0; i < data.entries.length; i += 200)
  await storeBatch(q, { id: data.terminal.id, branchId: data.branch.id }, { branchId: data.branch.id, entries: data.entries.slice(i, i + 200) });

// Una caja web lista para vender (la base de prueba vive en memoria y empieza vacía en cada arranque).
const [admin] = await q(`SELECT branch_id, payload FROM entity_state WHERE entity = 'app_user' AND NOT deleted AND payload->>'role_name' = 'Administrator' LIMIT 1`, []);
const webTill = admin ? await createTill(q, await toUser(q, admin), data.branch.id) : null;

const { url } = await startLocalServer(q, { port: Number(portText ?? 4173), noLogin, env: localEnv() });
console.log(`Versión web local en ${url} (${data.entries.length} registros)${noLogin ? ' · sin contraseña (sólo prueba; --con-login para pedirla)' : ''}${webTill ? ` · caja web ${webTill.name}` : ''}`);
