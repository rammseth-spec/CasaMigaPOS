// «Actualizar app» (skill pwa-actualizar-app adaptado: la web no usa Vite ni service worker y cada vez que se abre baja sus archivos).
// Lo que hay que cuidar es la pestaña o el acceso directo que se queda abierto días, como la tableta del mostrador: al publicar,
// build/version.mjs escribe version.json (commit y hora); el servidor local lo calcula de los archivos. La web guarda la versión con la
// que abrió y la compara al volver a la app y cada 10 minutos; si cambió, avisa «Hay una versión nueva · Actualizar» y no recarga sola
// para no cortar un cobro o una captura. «Buscar» (Nube → Cajas y cambios) revisa y, si hay una nueva, recarga.
const EVERY = 10 * 60_000;
let installed = null;
let lastCheck = 0;
let notice = null;

async function published() {
  const response = await fetch('/version.json', { cache: 'no-store' });
  const v = response.ok ? await response.json().catch(() => null) : null;
  if (typeof v?.version !== 'string') throw new Error('No se pudo revisar la versión. Revisa el internet e inténtalo otra vez.');
  return v;
}

/** «del 21 oct 2026, 1:07 a.m. · d6ac7cd», con la hora de la sucursal. */
export function describeVersion(v, timeZone = 'America/Mexico_City') {
  if (!v) return 'sin dato todavía';
  const at = new Date(v.builtAt);
  const when = Number.isNaN(at.getTime()) ? '' : new Intl.DateTimeFormat('es-MX', { timeZone, dateStyle: 'medium', timeStyle: 'short' }).format(at);
  return [when && `del ${when}`, v.version].filter(Boolean).join(' · ');
}
export const installedVersion = () => installed;

/** Revisa la versión publicada; si ya no es la que se abrió, muestra el aviso y devuelve true. */
export async function checkVersion() {
  lastCheck = Date.now();
  const v = await published();
  if (!installed) { installed = v; return false; }
  if (v.version === installed.version) return false;
  showNotice();
  return true;
}

export const reloadApp = () => location.reload();

function showNotice() {
  if (notice?.isConnected) return;
  const text = document.createElement('span'); text.textContent = 'Hay una versión nueva';
  const button = document.createElement('button'); button.type = 'button'; button.className = 'primary'; button.textContent = 'Actualizar';
  button.addEventListener('click', reloadApp);
  notice = document.createElement('div'); notice.className = 'update-notice'; notice.setAttribute('role', 'status');
  notice.append(text, button);
  document.body.append(notice);
}

/** Desde que abre la web: guarda su versión y revisa al volver a la app (en el iPhone es lo único confiable) y cada 10 minutos. */
export function watchVersion() {
  checkVersion().catch(() => {});
  document.addEventListener('visibilitychange', () => { if (!document.hidden && Date.now() - lastCheck >= 30_000) checkVersion().catch(() => {}); });
  setInterval(() => { if (!document.hidden && Date.now() - lastCheck >= EVERY) checkVersion().catch(() => {}); }, 60_000);
}
