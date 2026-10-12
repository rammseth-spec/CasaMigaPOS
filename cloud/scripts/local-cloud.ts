// Nube local para probar la caja y la web en una sola computadora (scripts/run-local.ps1): la base y los respaldos se guardan
// en una carpeta y sobreviven a reinicios; se entra con usuario y contraseña reales. Sólo prueba, nunca producción.
// Uso: node --import tsx scripts/local-cloud.ts <carpeta de datos> [puerto]
// Variables: ENROLLMENT_SECRET (alta de cajas), SESSION_SECRET, BACKUP_SECRET y CRON_SECRET (run-local.ps1 las genera y las guarda).
import { join } from 'node:path';
import { localEnv, memoryDatabase, startLocalServer } from './localServer.js';

const [directory, portText] = process.argv.slice(2);
if (!directory) throw new Error('Falta la carpeta de datos de la nube local.');
const { q } = await memoryDatabase(join(directory, 'base'));
const { url } = await startLocalServer(q, { port: Number(portText ?? 4173), env: localEnv(), blobDirectory: join(directory, 'respaldos') });
console.log(`Nube local en ${url} · datos en ${directory}`);
console.log('Para registrar la caja: Nube → dirección ' + url + ' y el secreto de alta que muestra run-local.ps1.');
