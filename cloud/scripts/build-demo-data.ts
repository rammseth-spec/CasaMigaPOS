// Arma scripts/demo/casamiga-demo.json.gz (la demo completa de la web local) desde una exportación de la caja
// (CasaMiga.Maintenance export-sync <copia> <json>). Cambia la contraseña de cada usuario por la contraseña fija de prueba de su rol,
// así el repositorio nunca guarda contraseñas reales ni de revisión. Uso: node --import tsx scripts/build-demo-data.ts <export.json>
import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { createHash, randomUUID } from 'node:crypto';
import { gzipSync } from 'node:zlib';
import { identityHash } from '../lib/auth.js';

const PASSWORDS: Record<string, string> = { Administrator: 'Admin#2026', Manager: 'Encargado#2026', Cashier: 'Cajero#2026' };
const file = process.argv[2];
if (!file) throw new Error('Falta la exportación.');
const data = JSON.parse(readFileSync(file, 'utf8')) as { branch: unknown; terminal: unknown; entries: { entity: string; payload: Record<string, unknown> }[] };
const hashes = new Map<string, string>();
for (const [role, password] of Object.entries(PASSWORDS)) hashes.set(role, await identityHash(password));
let users = 0;
for (const entry of data.entries) {
  if (entry.entity !== 'app_user') continue;
  const p = entry.payload;
  const hash = hashes.get(String(p.role_name));
  if (!hash) throw new Error(`Rol sin contraseña de prueba: ${String(p.role_name)}`);
  // El cajero de la demo se llama «cajero», como en run-local.ps1.
  if (p.username === 'CAJERO1') p.username = 'CAJERO';
  p.password_hash = hash; p.must_change_password = 0; users++;
}
// Panes del catálogo autorizado (docs/catalog) que la exportación todavía no tiene: se agregan como si la caja los hubiera dado de alta,
// con su categoría (si es nueva) y su foto provisional (assets/catalog-provisional-ia/CM-NNN.jpg, usada tal cual como miniatura).
type Entry = { seq: number; entity: string; entityId: string; change: string; enqueuedAt: string; payload: Record<string, unknown> };
const entries = data.entries as unknown as Entry[];
const catalog = readFileSync(new URL('../../docs/catalog/catalogo-autorizado-v2.csv', import.meta.url), 'utf8').trim().split(/\r?\n/).slice(1).map(l => l.split(','));
const products = entries.filter(e => e.entity === 'product').map(e => (e.payload.product as Record<string, unknown>));
const categories = entries.filter(e => e.entity === 'category').map(e => e.payload);
const categoryId = new Map(categories.map(c => [String(c.name), String(c.id)]));
const admin = entries.find(e => e.entity === 'app_user' && e.payload.role_name === 'Administrator')?.payload.id;
const terminal = (data.terminal as { id: string }).id;
let seq = Math.max(...entries.map(e => e.seq)), added = 0, newCategories = 0, photos = 0;
const now = new Date().toISOString();
for (const [id, name, price, category] of catalog) {
  if (products.some(p => p.name === name)) continue;
  if (!categoryId.has(category)) {
    const categoryKey = randomUUID();
    entries.push({ seq: ++seq, entity: 'category', entityId: categoryKey, change: 'insert', enqueuedAt: now, payload: { id: categoryKey, name: category, active: 1,
      description: 'Catálogo autorizado v2 · docs/catalog', sort_order: categories.length + newCategories, created_utc: now, modified_utc: now } });
    categoryId.set(category, categoryKey); newCategories++;
  }
  const productId = randomUUID(), sku = `CM-${id.padStart(3, '0')}`;
  entries.push({ seq: ++seq, entity: 'product', entityId: productId, change: 'insert', enqueuedAt: now, payload: {
    product: { id: productId, sku, name, category_id: categoryId.get(category), description: '', price_cents: Number(price) * 100,
      track_inventory: 0, active: 1, created_utc: now, modified_utc: now }, inventoryLinks: [] } });
  added++;
  const photoFile = new URL(`../../assets/catalog-provisional-ia/${sku}.jpg`, import.meta.url);
  if (!existsSync(photoFile)) continue;
  const jpeg = readFileSync(photoFile), photoId = randomUUID();
  entries.push({ seq: ++seq, entity: 'product_photo', entityId: productId, change: 'insert', enqueuedAt: now, payload: {
    photo: { product_id: productId, current_photo_id: photoId, revision: 1, last_operation_id: photoId, modified_utc: now, modified_by: admin, terminal_id: terminal },
    asset: { id: photoId, provenance: 'PROVISIONAL', thumbnail_sha256: createHash('sha256').update(jpeg).digest('hex').toUpperCase(), thumbnail_edge: 768, created_utc: now },
    thumbnail: jpeg.toString('base64') } });
  photos++;
}
console.log(`${added} panes nuevos del catálogo autorizado agregados a la demo (${photos} con foto, ${newCategories} categorías nuevas).`);
mkdirSync(new URL('./demo/', import.meta.url), { recursive: true });
const output = new URL('./demo/casamiga-demo.json.gz', import.meta.url);
writeFileSync(output, gzipSync(JSON.stringify(data), { level: 9 }));
console.log(`${data.entries.length} registros, ${users} usuarios con contraseña de prueba → ${output.pathname}`);
