// «Actualizar app» en la web (js/version.js): al publicar se escribe version.json, el servidor local lo calcula, no se guarda en caché,
// y el HTML trae la pantalla de carga, el manifest y sus íconos.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { startLocalServer } from '../scripts/localServer.js';
import { database } from './helpers.js';

const read = (path: string) => readFileSync(new URL(path, import.meta.url));

test('publishing writes version.json with the commit and the time', () => {
  const file = join(mkdtempSync(join(tmpdir(), 'cm-version-')), 'version.json');
  execFileSync(process.execPath, [new URL('../build/version.mjs', import.meta.url).pathname, file], { env: { ...process.env, VERCEL_GIT_COMMIT_SHA: '0123456789abcdef' }, stdio: 'ignore' });
  const v = JSON.parse(readFileSync(file, 'utf8'));
  assert.equal(v.version, '0123456');
  assert.ok(Math.abs(Date.parse(v.builtAt) - Date.now()) < 60_000);
});

test('vercel builds version.json and never caches what tells which version is published', () => {
  const vercel = JSON.parse(read('../vercel.json').toString('utf8'));
  assert.equal(vercel.buildCommand, 'node build/version.mjs');
  // build/ sí se sube; scripts/ no (.vercelignore).
  assert.ok(!read('../.vercelignore').toString('utf8').split(/\r?\n/).includes('build'));
  for (const source of ['/', '/index.html', '/version.json', '/manifest.webmanifest', '/js/(.*)'])
    assert.ok(vercel.headers.some((r: { source: string; headers: { key: string; value: string }[] }) => r.source === source && r.headers.some(x => x.key === 'Cache-Control' && x.value === 'no-cache')), source);
});

test('the local server answers version.json from its files, without cache', async () => {
  const { q } = await database();
  const { server, url } = await startLocalServer(q, { port: 0 });
  try {
    const response = await fetch(`${url}/version.json`);
    assert.equal(response.status, 200);
    assert.equal(response.headers.get('cache-control'), 'no-store');
    const v = await response.json() as { version: string; builtAt: string };
    assert.match(v.version, /^local-[0-9a-f]{7}$/);
    assert.ok(!Number.isNaN(Date.parse(v.builtAt)));
    assert.deepEqual(await (await fetch(`${url}/version.json`)).json(), v, 'Without edits the version does not change.');
  } finally { server.close(); }
});

test('the page never starts blank and can be added to the home screen', () => {
  const html = read('../public/index.html').toString('utf8');
  assert.ok(html.indexOf('js/arranque.js') > 0 && html.indexOf('js/arranque.js') < html.indexOf('js/app.js'), 'The boot watchdog loads before the app.');
  assert.match(html, /<div id="arranque"[^>]*role="status">/);
  assert.match(html, /id="arranque-reintentar"[^>]*hidden/);
  assert.ok(!/<script>/.test(html), 'No inline scripts: the Content-Security-Policy forbids them.');
  assert.match(html, /<link rel="manifest" href="manifest.webmanifest">/);
  assert.match(html, /<link rel="apple-touch-icon" href="apple-touch-icon.png">/);
  const manifest = JSON.parse(read('../public/manifest.webmanifest').toString('utf8'));
  assert.equal(manifest.display, 'standalone');
  const size = (file: string) => { const png = read(`../public/${file}`); return [png.readUInt32BE(16), png.readUInt32BE(20)]; };
  for (const icon of manifest.icons) assert.deepEqual(size(icon.src), icon.sizes.split('x').map(Number), icon.src);
  assert.ok(manifest.icons.some((i: { sizes: string; purpose?: string }) => i.sizes === '192x192') && manifest.icons.some((i: { purpose?: string }) => i.purpose === 'maskable'));
  assert.deepEqual(size('apple-touch-icon.png'), [180, 180]);
});
