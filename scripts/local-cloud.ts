// Nube local para probar la caja y la web en una sola computadora (scripts/run-local.ps1), o sólo la web en Mac o Linux:
// la base y los respaldos se guardan en una carpeta y sobreviven a reinicios. Sólo prueba, nunca producción.
// Uso: node --import tsx scripts/local-cloud.ts <carpeta de datos> [puerto] [--sin-contrasena] [--demo]   (o: npm run local -- …)
// --sin-contrasena: la web entra sola (como el servidor de desarrollo) y sólo acepta conexiones de esta misma computadora.
// --demo: si la base está vacía, crea una sucursal con un usuario fijo por rol, panes y una caja web, para probar la web sin la caja
//         de escritorio (por ejemplo en Mac). Los cambios que necesitan la caja (catálogo, inventario…) quedan «Por confirmar».
// Variables opcionales: ENROLLMENT_SECRET (alta de cajas), SESSION_SECRET, BACKUP_SECRET y CRON_SECRET (run-local.ps1 las genera).
import { randomBytes, randomUUID } from 'node:crypto';
import { join } from 'node:path';
import { login } from '../lib/auth.js';
import { createTill } from '../lib/pos.js';
import { sha256Hex } from '../lib/protocol.js';
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
const { q } = await memoryDatabase(join(directory, 'base'));

if (args.includes('--demo') && Number((await q('SELECT count(*) AS n FROM branch', []))[0].n) === 0) {
  // Como si una caja de escritorio hubiera subido su sucursal: permisos, usuarios y catálogo.
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

const { url } = await startLocalServer(q, { port: Number(portText ?? 4173), env: localEnv(), blobDirectory: join(directory, 'respaldos'), noLogin, host: noLogin ? '127.0.0.1' : undefined });
console.log(`Nube local en ${url} · datos en ${directory}${noLogin ? ' · la web entra sola (sólo desde esta computadora)' : ''}`);
if (noLogin) console.log(`Abre http://127.0.0.1:${portText ?? 4173} en el navegador.`);
console.log('Usuarios de prueba: ' + TEST_USERS.map(u => `${u.username} / ${u.password}`).join(' · '));
