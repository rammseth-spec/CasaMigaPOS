// Versión publicada de la web: public/version.json con el commit y la hora de publicación. js/version.js la compara con la que abrió
// para avisar «Hay una versión nueva · Actualizar». Vercel lo corre al publicar (vercel.json → buildCommand; fuera de scripts/, que
// .vercelignore no sube). El archivo no se versiona; el servidor local lo calcula de los archivos (scripts/localServer.ts).
// Uso: node build/version.mjs [archivo de salida]
import { execFileSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';

const commit = process.env.VERCEL_GIT_COMMIT_SHA || (() => {
  try { return execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim(); } catch { return ''; }
})();
const builtAt = new Date().toISOString();
// Sin commit (publicación sin Git), cada publicación es una versión distinta.
const version = commit ? commit.slice(0, 7) : `sin-commit-${builtAt}`;
const file = process.argv[2] ?? new URL('../public/version.json', import.meta.url);
writeFileSync(file, JSON.stringify({ version, builtAt }) + '\n');
console.log(`version.json: ${version} · ${builtAt}`);
