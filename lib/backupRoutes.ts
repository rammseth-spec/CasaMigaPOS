// Rutas de respaldo de las cajas (/api/backup/<op>) y tarea nocturna (/api/cron/daily), con base y almacenamiento inyectables.
import { failure, json, ProtocolError, readJson } from './protocol.js';
import { authenticate } from './terminal.js';
import { backupSecret, beginObject, completeObject, createSet, deriveKey, exportCloud, missing, prune, putPart, state } from './backup.js';
import { DEFAULT_TIME_ZONE, type Query } from './panel.js';
import type { Storage } from './storage.js';

export interface BackupEnv { BACKUP_SECRET?: string; CRON_SECRET?: string; PANEL_TIME_ZONE?: string }

export async function handleBackup(q: Query, storage: () => Storage, request: Request, op: string, env: BackupEnv): Promise<Response> {
  try {
    const secret = backupSecret(env.BACKUP_SECRET);
    const terminal = await authenticate(q, request.headers.get('authorization'));
    const url = new URL(request.url);
    switch (`${request.method} ${op}`) {
      case 'GET key': return json(200, { key: deriveKey(secret, `branch:${terminal.branchId}`).toString('base64') });
      case 'GET state': return json(200, await state(q, terminal));
      case 'POST missing': return json(200, await missing(q, terminal, await readJson(request, 4_000_000)));
      case 'POST object': return json(200, await beginObject(q, terminal, await readJson(request, 10_000)));
      case 'PUT part': {
        const length = Number(request.headers.get('content-length') ?? '0');
        if (length > 4_194_304) throw new ProtocolError('Parte demasiado grande', 413);
        const data = new Uint8Array(await request.arrayBuffer());
        return json(200, await putPart(q, storage(), terminal, url.searchParams.get('object'), url.searchParams.get('index'), data));
      }
      case 'POST complete': return json(200, await completeObject(q, terminal, url.searchParams.get('object')));
      case 'POST set': return json(200, await createSet(q, storage(), secret, terminal, await readJson(request, 8_000_000)));
    }
    throw new ProtocolError('Ruta desconocida', 404);
  } catch (error) {
    return failure(error);
  }
}

/** Vercel Cron manda «Authorization: Bearer CRON_SECRET». Sin ese secreto configurado no corre (nadie más puede dispararla). */
export async function handleDaily(q: Query, storage: () => Storage, request: Request, env: BackupEnv): Promise<Response> {
  try {
    if (!env.CRON_SECRET || env.CRON_SECRET.length < 16) throw new ProtocolError('Tarea sin configurar (CRON_SECRET)', 503);
    if (request.headers.get('authorization') !== `Bearer ${env.CRON_SECRET}`) throw new ProtocolError('No autorizado', 401);
    const secret = backupSecret(env.BACKUP_SECRET);
    const cloud = await exportCloud(q, storage(), secret);
    const cleaned = await prune(q, storage(), env.PANEL_TIME_ZONE || DEFAULT_TIME_ZONE);
    return json(200, { cloud, cleaned });
  } catch (error) {
    return failure(error);
  }
}
