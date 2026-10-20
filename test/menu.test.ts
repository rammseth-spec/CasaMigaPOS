// Íconos ilustrados del menú lateral (public/nav, generados con IA; los mismos de la caja): cada sección de la web tiene el suyo,
// 128 × 128 con transparencia.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = (path: string) => readFileSync(new URL(path, import.meta.url));

test('every sidebar section of the web has its illustrated icon', () => {
  const app = read('../public/js/app.js').toString('utf8');
  const art = Object.fromEntries([...app.match(/const NAV_ART = \{([^}]*)\}/)![1].matchAll(/(\w+): '([\w-]+)'/g)].map(m => [m[1], m[2]]));
  const sections = [...read('../public/js/views.js').toString('utf8').matchAll(/\{ id: '(\w+)'/g)].map(m => m[1]);
  assert.ok(sections.length >= 11);
  for (const id of [...sections, 'salir']) {
    assert.ok(art[id], `${id} no tiene ícono ilustrado`);
    const png = read(`../public/nav/${art[id]}.png`);
    assert.equal(png.subarray(1, 4).toString('latin1'), 'PNG', art[id]);
    assert.deepEqual([png.readUInt32BE(16), png.readUInt32BE(20), png[24], png[25]], [128, 128, 8, 6], `${art[id]}.png debe ser 128 × 128 RGBA`);
  }
});
