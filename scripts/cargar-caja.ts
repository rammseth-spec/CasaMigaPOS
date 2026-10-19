// Carga en una caja, por la web, el catálogo autorizado con sus fotos provisionales y el recetario en borrador (scripts/carga-plan.ts).
// La caja aplica cada cambio al sincronizar, con sus propias reglas. Sin --enviar sólo muestra qué falta.
// Uso (desde cloud/): npm run cargar -- <dirección de la web> [--enviar] [--esperar] [--sucursal <id>] [--usuario <usuario>]
// La contraseña se toma de CASAMIGA_CONTRASENA o se pregunta. --esperar repite hasta que la caja aplique todo lo que se puede mandar.
import { existsSync, readFileSync } from 'node:fs';
import { createInterface } from 'node:readline/promises';
import { plan, type CatalogRow, type State, type Step } from './carga-plan.js';
import { loadRecetario, validate } from './recetario.js';

const args = process.argv.slice(2);
const flag = (name: string) => args.includes(name);
const option = (name: string) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : undefined; };
const base = args.find(a => /^https?:\/\//.test(a))?.replace(/\/+$/, '');
if (!base) throw new Error('Uso: npm run cargar -- <dirección de la web> [--enviar] [--esperar] [--sucursal <id>] [--usuario <usuario>]');

const catalog: CatalogRow[] = readFileSync(new URL('../../docs/catalog/catalogo-autorizado-v2.csv', import.meta.url), 'utf8').trim().split(/\r?\n/).slice(1)
  .map(line => line.split(',')).map(([id, name, pesos, category]) => ({ id: Number(id), name, pesos: Number(pesos), category }));
const recetario = loadRecetario();
const problems = validate(recetario, catalog.map(r => r.name));
if (problems.length) throw new Error(`El recetario tiene problemas:\n${problems.join('\n')}`);
const photo = (sku: string) => {
  const file = new URL(`../../assets/catalog-provisional-ia/${sku}.jpg`, import.meta.url);
  return existsSync(file) ? readFileSync(file).toString('base64') : null;
};

let cookie = '';
async function call(path: string, body?: unknown) {
  const response = await fetch(`${base}${path}`, {
    method: body === undefined ? 'GET' : 'POST', headers: { 'content-type': 'application/json', origin: base!, ...(cookie ? { cookie } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const set = response.headers.get('set-cookie');
  if (set) cookie = set.split(';')[0];
  const data = await response.json().catch(() => ({})) as Record<string, unknown>;
  if (!response.ok) throw new Error(String(data.error ?? `HTTP ${response.status}`));
  return data;
}

const ask = async (question: string) => { const rl = createInterface({ input: process.stdin, output: process.stdout }); try { return await rl.question(question); } finally { rl.close(); } };
const username = option('--usuario') ?? process.env.CASAMIGA_USUARIO ?? await ask('Usuario administrador: ');
const password = process.env.CASAMIGA_CONTRASENA ?? await ask('Contraseña: ');
await call('/api/web/login', { username, password });
const me = await call('/api/web/me') as { branches: { id: string; name: string }[] };
const branch = option('--sucursal') ?? (me.branches.length === 1 ? me.branches[0].id : undefined);
if (!branch) throw new Error(`Hay varias sucursales; elige una con --sucursal: ${me.branches.map(b => `${b.id} (${b.name})`).join(', ')}`);
const branchName = me.branches.find(b => b.id === branch)?.name ?? branch;

async function load(): Promise<State> {
  const catalogData = await call(`/api/web/catalog?branch=${branch}`) as Pick<State, 'categories' | 'products'>;
  const recipes = await call(`/api/web/recipes?branch=${branch}`) as Pick<State, 'items' | 'formulas' | 'profiles'>;
  const commands = await call(`/api/web/commands?branch=${branch}&pendientes=1`) as unknown as { branchId: string; kind: string; payload: Record<string, unknown>; status: string }[];
  return { categories: catalogData.categories, products: catalogData.products, items: recipes.items, formulas: recipes.formulas, profiles: recipes.profiles,
    queued: commands.filter(c => c.branchId === branch && c.status === 'pending') };
}

const STAGES = ['', 'Catálogo y fotos', 'Insumos y productos terminados', 'Preparaciones (borrador)', 'Panes (borrador)'];
const pending = (state: State) => state.queued.length;
console.log(`Sucursal: ${branchName}${flag('--enviar') ? '' : ' · sólo muestra el plan (agrega --enviar para mandarlo)'}`);
const deadline = Date.now() + 15 * 60_000;
for (let round = 1; ; round++) {
  const state = await load();
  const { steps, waits } = plan(state, catalog, recetario, photo);
  for (let stage = 1; stage <= 4; stage++) {
    const s = steps.filter(x => x.stage === stage), w = waits.filter(x => x.stage === stage);
    if (s.length || w.length) console.log(`\n${stage}. ${STAGES[stage]}: ${s.length} por mandar${w.length ? `, ${w.length} en espera` : ''}`);
    for (const x of s.slice(0, 6)) console.log(`   + ${x.label}`);
    if (s.length > 6) console.log(`   + … y ${s.length - 6} más`);
    for (const reason of [...new Set(w.map(x => x.reason))]) console.log(`   · ${w.filter(x => x.reason === reason).length} ${reason}`);
  }
  if (!flag('--enviar')) break;
  let sent = 0;
  const failed: [Step, string][] = [];
  for (const step of steps) {
    try { await call('/api/web/command', { branchId: branch, kind: step.kind, payload: step.payload }); sent++; }
    catch (e) { failed.push([step, (e as Error).message]); }
  }
  console.log(`\nMandados: ${sent}${failed.length ? ` · con error: ${failed.length}` : ''}`);
  for (const [step, message] of failed) console.log(`   ✗ ${step.label}: ${message}`);
  const waitingForTill = waits.some(w => w.reason.startsWith('esperando'));
  if (!flag('--esperar') || (!sent && !waitingForTill && !pending(state)) || Date.now() > deadline) {
    if (waits.some(w => w.reason.startsWith('falta publicar'))) console.log('\nLas recetas de cada pan se mandan cuando sus preparaciones estén publicadas en Recetas; vuelve a correr esto después.');
    break;
  }
  console.log(`\nEsperando a que la caja aplique los cambios (vuelta ${round})…`);
  for (let i = 0; i < 60 && Date.now() < deadline; i++) {
    await new Promise(r => setTimeout(r, 5_000));
    if (pending(await load()) === 0) break;
  }
}
