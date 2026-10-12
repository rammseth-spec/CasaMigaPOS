// Nube local para probar la caja y la web en una sola computadora (scripts/run-local.ps1), o sólo la web en Mac o Linux:
// la base y los respaldos se guardan en una carpeta y sobreviven a reinicios. Sólo prueba, nunca producción.
// Uso: node --import tsx scripts/local-cloud.ts <carpeta de datos> [puerto] [--sin-contrasena] [--demo]   (o: npm run local -- …)
// --sin-contrasena: la web entra sola (como el servidor de desarrollo) y sólo acepta conexiones de esta misma computadora.
// --demo: si la base está vacía, crea una sucursal con un usuario fijo por rol, panes y una caja web, para probar la web sin la caja
//         de escritorio (por ejemplo en Mac). Los cambios que necesitan la caja (catálogo, inventario…) quedan «Por confirmar».
// Variables opcionales: ENROLLMENT_SECRET (alta de cajas), SESSION_SECRET, BACKUP_SECRET y CRON_SECRET (run-local.ps1 las genera).
import { spawn } from 'node:child_process';
import { randomBytes, randomUUID } from 'node:crypto';
import { createServer } from 'node:net';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { gunzipSync } from 'node:zlib';
import { login } from '../lib/auth.js';
import { createTill } from '../lib/pos.js';
import { sha256Hex } from '../lib/protocol.js';
import { storeBatch } from '../lib/store.js';
import { permissions, send, user } from '../test/helpers.js';
import { localEnv, memoryDatabase, startLocalServer } from './localServer.js';

/** Los mismos usuarios de prueba que crea la caja con run-local.ps1 (temporal, sólo prueba local). */
export const TEST_USERS = [
  { username: 'admin', password: 'Admin#2026', role: 'Administrator', name: 'Administrador' },
  { username: 'encargado', password: 'Encargado#2026', role: 'Manager', name: 'Encargado de prueba' },
  { username: 'cajero', password: 'Cajero#2026', role: 'Cashier', name: 'Cajero de prueba' },
];

const args = process.argv.slice(2);
const [directory, portText] = args.filter(a => !a.startsWith('--'));
const noLogin = args.includes('--sin-contrasena');
if (!directory) throw new Error('Falta la carpeta de datos de la nube local.');
const port = Number(portText ?? 4173);
function busy(): never {
  // Lo más común: ya hay una web corriendo en otra ventana de Terminal.
  console.error(`\nEl puerto ${port} ya está ocupado: probablemente la web ya está corriendo en otra ventana.`);
  console.error(`Ábrela en http://127.0.0.1:${port}, o cierra esa ventana (Ctrl+C), o usa otro puerto, por ejemplo: npm run local -- ${directory} ${port + 2} …\n`);
  process.exit(1);
}
// Antes de abrir la base: dos copias nunca deben abrir la misma carpeta de datos a la vez.
await new Promise<void>(resolve => {
  const probe = createServer().once('error', () => busy());
  probe.listen(port, noLogin ? '127.0.0.1' : undefined, () => probe.close(() => resolve()));
});
const { q } = await memoryDatabase(join(directory, 'base'));

const demoFile = new URL('./demo/casamiga-demo.json.gz', import.meta.url);
const empty = Number((await q('SELECT count(*) AS n FROM branch', []))[0].n) === 0;
if (args.includes('--demo') && empty && existsSync(demoFile)) {
  // La demo completa de la caja (scripts/build-demo-data.ts): 29 panes con foto, ventas, cortes, charolas, inventario, recetas y
  // producción, subida como la subiría la caja. Usuarios con las contraseñas fijas de prueba de su rol.
  const data = JSON.parse(gunzipSync(readFileSync(demoFile)).toString('utf8')) as
    { branch: { id: string; name: string }; terminal: { id: string; name: string }; entries: Parameters<typeof storeBatch>[2]['entries'] };
  await q('INSERT INTO branch(id, name) VALUES ($1, $2)', [data.branch.id, data.branch.name]);
  await q('INSERT INTO terminal(id, branch_id, name, key_sha256) VALUES ($1, $2, $3, $4)', [data.terminal.id, data.branch.id, data.terminal.name, sha256Hex(randomBytes(32).toString('hex'))]);
  for (let i = 0; i < data.entries.length; i += 200)
    await storeBatch(q, { id: data.terminal.id, branchId: data.branch.id }, { branchId: data.branch.id, entries: data.entries.slice(i, i + 200) });
  await createTill(q, await login(q, 'admin', 'Admin#2026'), data.branch.id);
  console.log(`Demo completa cargada: ${data.branch.name}, ${data.entries.length} registros (panes con foto, ventas, charolas, inventario y recetas) y la caja web CAJA-WEB-02.`);
} else if (args.includes('--demo') && empty) {
  // Sin el archivo de la demo: una sucursal mínima, como si una caja de escritorio hubiera subido permisos, usuarios y catálogo.
  const branch = randomUUID(), terminal = randomUUID();
  await q('INSERT INTO branch(id, name) VALUES ($1, $2)', [branch, 'Sucursal de prueba']);
  await q(`INSERT INTO terminal(id, branch_id, name, key_sha256) VALUES ($1, $2, 'CASA-MIGA-CAJA-01', $3)`, [terminal, branch, sha256Hex(randomBytes(32).toString('hex'))]);
  await permissions(q, terminal, branch);
  for (const u of TEST_USERS) await user(q, terminal, branch, randomUUID(), u.username, u.role, u.password, { display_name: u.name });
  const categories = [['Pan dulce', 1], ['Pan salado', 2], ['Bebidas', 3]].map(([name, order]) => ({ id: randomUUID(), name: String(name), order: Number(order) }));
  const breads: [string, number, number][] = [['Concha de vainilla', 1300, 0], ['Concha de chocolate', 1300, 0], ['Cuerno', 1500, 0], ['Oreja', 1200, 0],
    ['Dona de azúcar', 1300, 0], ['Rol de canela', 2200, 0], ['Bolillo', 500, 1], ['Telera', 600, 1], ['Café de olla', 2000, 2], ['Chocolate caliente', 2500, 2]];
  await send(q, terminal, branch, [
    ...categories.map(c => ({ entity: 'category', id: c.id, payload: { id: c.id, name: c.name, description: '', active: 1, sort_order: c.order } })),
    ...breads.map(([name, price, category]) => { const id = randomUUID(); return { entity: 'product', id, payload: {
      product: { id, sku: null, name, category_id: categories[category].id, description: '', price_cents: price, track_inventory: 0, active: 1 }, inventoryLinks: [] } }; }),
  ]);
  await createTill(q, await login(q, 'admin', 'Admin#2026'), branch);
  console.log('Datos de demostración creados: Sucursal de prueba, 3 usuarios (uno por rol), 10 panes y la caja web CAJA-WEB-02.');
}

try {
  await startLocalServer(q, { port, env: localEnv(), blobDirectory: join(directory, 'respaldos'), noLogin, host: noLogin ? '127.0.0.1' : undefined });
} catch (error) {
  if ((error as NodeJS.ErrnoException).code !== 'EADDRINUSE') throw error;
  busy();
}
// Sin contraseña sólo escucha en 127.0.0.1: esa es la dirección que hay que abrir (en Mac, «localhost» puede ir primero a ::1).
const url = noLogin ? `http://127.0.0.1:${port}` : `http://localhost:${port}`;
console.log(`\nCasa Miga web lista: ${url}`);
console.log(`Datos: ${directory}${noLogin ? ' · entra sola (sólo desde esta computadora)' : ''}`);
console.log('Usuarios de prueba: ' + TEST_USERS.map(u => `${u.username} / ${u.password}`).join(' · ') + ' (en la demo completa, ana y luis también usan Cajero#2026)');
console.log('Deja esta ventana abierta mientras la usas; Ctrl+C la apaga.\n');
// Con --demo se abre sola en el navegador (Mac, Windows o Linux).
if (args.includes('--demo')) {
  const [command, ...rest] = process.platform === 'darwin' ? ['open', url] : process.platform === 'win32' ? ['cmd', '/c', 'start', '', url] : ['xdg-open', url];
  spawn(command, rest, { stdio: 'ignore', detached: true }).on('error', () => console.log(`Abre ${url} en el navegador.`)).unref();
}
