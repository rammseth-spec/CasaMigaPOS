import { ensureSchema, query } from '../../lib/db.js';
import { failure } from '../../lib/protocol.js';
import { handleBackup } from '../../lib/backupRoutes.js';
import { blobStorage } from '../../lib/storage.js';

/** Respaldos que sube cada caja (llave, estado, partes y cierre). */
async function route(request: Request): Promise<Response> {
  try {
    await ensureSchema();
    const op = new URL(request.url).pathname.split('/').filter(Boolean).pop() ?? '';
    return await handleBackup(query(), () => blobStorage(process.env.BLOB_READ_WRITE_TOKEN), request, op, process.env);
  } catch (error) {
    return failure(error);
  }
}
export const GET = route;
export const POST = route;
export const PUT = route;
