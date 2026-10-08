import { ensureSchema, query } from '../../lib/db.js';
import { failure } from '../../lib/protocol.js';
import { handleDaily } from '../../lib/backupRoutes.js';
import { blobStorage } from '../../lib/storage.js';

/** Cada noche (vercel.json → crons): copia cifrada de la base de la nube y limpieza de respaldos viejos. */
export async function GET(request: Request): Promise<Response> {
  try {
    await ensureSchema();
    return await handleDaily(query(), () => blobStorage(process.env.BLOB_READ_WRITE_TOKEN), request, process.env);
  } catch (error) {
    return failure(error);
  }
}
