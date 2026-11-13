// Catálogo autorizado (docs/catalog): filas del CSV con sus nombres anteriores (renombres.json) y la fecha de cada foto provisional
// (assets/catalog-provisional-ia/manifest.json). Lo usan la carga a una caja (cargar-caja.ts) y la demo de la web (build-demo-data.ts).
import { readFileSync } from 'node:fs';
import type { CatalogRow } from './carga-plan.js';

const docs = (path: string) => new URL(`../../docs/catalog/${path}`, import.meta.url);

export function loadCatalog(): CatalogRow[] {
  const renames = JSON.parse(readFileSync(docs('renombres.json'), 'utf8')) as { renombres: { id: number; antes: string[] }[] };
  const before = new Map(renames.renombres.map(r => [r.id, r.antes]));
  return readFileSync(docs('catalogo-autorizado-v2.csv'), 'utf8').trim().split(/\r?\n/).slice(1).map(line => line.split(','))
    .map(([id, name, pesos, category]) => ({ id: Number(id), name, pesos: Number(pesos), category, before: before.get(Number(id)) ?? [] }));
}

/** Cuándo se generó la foto provisional de cada SKU (CM-001 …): una caja con una foto provisional anterior recibe la nueva. */
export function photoDates(): Map<string, string> {
  const manifest = JSON.parse(readFileSync(new URL('../../assets/catalog-provisional-ia/manifest.json', import.meta.url), 'utf8')) as { sku: string; generated_utc: string }[];
  return new Map(manifest.map(e => [e.sku, e.generated_utc]));
}
