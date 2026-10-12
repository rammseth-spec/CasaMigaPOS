// Nube local para probar la caja y la web en una sola computadora (scripts/run-local.ps1): la base y los respaldos se guardan
// en una carpeta y sobreviven a reinicios; se entra con usuario y contraseña reales. Sólo prueba, nunca producción.
// Uso: node --import tsx scripts/local-cloud.ts <carpeta de datos> [puerto] [--sin-contrasena]
// --sin-contrasena: la web entra sola (como el servidor de desarrollo) y sólo acepta conexiones de esta misma computadora.
// Variables: ENROLLMENT_SECRET (alta de cajas), SESSION_SECRET, BACKUP_SECRET y CRON_SECRET (run-local.ps1 las genera y las guarda).
import { join } from 'node:path';
import { localEnv, memoryDatabase, startLocalServer } from './localServer.js';

const args = process.argv.slice(2);
const [directory, portText] = args.filter(a => !a.startsWith('--'));
const noLogin = args.includes('--sin-contrasena');
if (!directory) throw new Error('Falta la carpeta de datos de la nube local.');
const { q } = await memoryDatabase(join(directory, 'base'));
const { url } = await startLocalServer(q, { port: Number(portText ?? 4173), env: localEnv(), blobDirectory: join(directory, 'respaldos'), noLogin, host: noLogin ? '127.0.0.1' : undefined });
console.log(`Nube local en ${url} · datos en ${directory}${noLogin ? ' · la web entra sola (sólo desde esta computadora)' : ''}`);
console.log('Para registrar la caja: Nube → dirección ' + url + ' y el secreto de alta que muestra run-local.ps1.');
