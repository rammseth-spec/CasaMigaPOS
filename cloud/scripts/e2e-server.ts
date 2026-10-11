// Nube vacía para la prueba de punta a punta con la caja de escritorio (tests/CasaMiga.Tests/CloudEndToEndTests.cs):
// PGlite en memoria con todas las migraciones y el servidor local con login real. Escribe {"url": ...} en la primera línea
// y se apaga cuando la prueba cierra la entrada estándar. Uso: node --import tsx scripts/e2e-server.ts <secreto de alta>
import { localEnv, memoryDatabase, startLocalServer } from './localServer.js';

const secret = process.argv[2];
if (!secret) throw new Error('Falta el secreto de alta.');
const { q } = await memoryDatabase();
const { server, url } = await startLocalServer(q, { env: localEnv(secret) });
process.stdout.write(JSON.stringify({ url }) + '\n');
process.stdin.resume();
process.stdin.on('end', () => server.close(() => process.exit(0)));
