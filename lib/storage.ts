// Dónde viven los archivos de respaldo: Vercel Blob en producción; memoria en pruebas y en el servidor local.
// Todo lo que se guarda ya va cifrado; las direcciones de Blob llevan un sufijo aleatorio imposible de adivinar.
import { del, put } from '@vercel/blob';
import { ProtocolError } from './protocol.js';

export interface Storage {
  put(path: string, data: Uint8Array): Promise<string>;
  get(url: string): Promise<Uint8Array>;
  del(urls: string[]): Promise<void>;
}

export function blobStorage(token: string | undefined): Storage {
  if (!token) throw new ProtocolError('Respaldos sin configurar (BLOB_READ_WRITE_TOKEN)', 503);
  return {
    async put(path, data) {
      const result = await put(path, Buffer.from(data), { access: 'public', token, addRandomSuffix: true, contentType: 'application/octet-stream' });
      return result.url;
    },
    async get(url) {
      const response = await fetch(url);
      if (!response.ok) throw new Error(`Archivo de respaldo no disponible (${response.status})`);
      return new Uint8Array(await response.arrayBuffer());
    },
    async del(urls) {
      for (let i = 0; i < urls.length; i += 500) if (urls.length) await del(urls.slice(i, i + 500), { token });
    },
  };
}

/** En memoria; `base` permite servir los archivos desde el servidor local (p. ej. http://localhost:4173/dev-blob/). */
export function memoryStorage(base = 'memory://') {
  const files = new Map<string, Uint8Array>();
  let counter = 0;
  const storage: Storage & { files: Map<string, Uint8Array> } = {
    files,
    async put(path, data) { const url = `${base}${path}-${++counter}`; files.set(url, new Uint8Array(data)); return url; },
    async get(url) { const data = files.get(url); if (!data) throw new Error('Archivo de respaldo no disponible (404)'); return data; },
    async del(urls) { for (const url of urls) files.delete(url); },
  };
  return storage;
}
