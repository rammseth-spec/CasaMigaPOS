// Recetario en borrador (docs/catalog/recetario-borrador.json): lo leen la demo (build-demo-data.ts) y el cargador a la caja
// real (cargar-caja.ts). Aquí se convierten las cantidades a unidades mínimas (mg, µL, piezas) y se fijan ids estables,
// para que volver a cargar nunca duplique nada.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';

export type Line = { insumo: string; cantidad: string; estado?: string };
export type Recetario = {
  fecha: string; nombreBorrador: string;
  insumos: { codigo: string; nombre: string; mide: 'g' | 'ml' | 'pz'; tambienSeLlama?: string[]; nota?: string }[];
  preparaciones: { codigo: string; nombre: string; rinde: string; notas?: string; ingredientes: Line[]; alternativas?: Line[][] }[];
  panes: { catalogo: number; pan: string; preparaciones: { codigo: string; cantidad: string; estado?: string }[]; directos?: Line[]; notas?: string }[];
};
export type Dimension = 'MASS' | 'VOLUME' | 'COUNT';

export function loadRecetario(): Recetario {
  return JSON.parse(readFileSync(new URL('../../docs/catalog/recetario-borrador.json', import.meta.url), 'utf8')) as Recetario;
}

const UNIT: Record<string, { dimension: Dimension; atomic: number }> = { g: { dimension: 'MASS', atomic: 1000 }, ml: { dimension: 'VOLUME', atomic: 1000 }, pz: { dimension: 'COUNT', atomic: 1 } };

/** «84.615 g» → 84,615 mg. Una cantidad que no da unidades mínimas enteras es un error del recetario, no se redondea. */
export function quantity(text: string): { dimension: Dimension; atomic: number } {
  const match = /^(\d+(?:\.\d+)?) (g|ml|pz)$/.exec(text.trim());
  if (!match) throw new Error(`Cantidad inválida: «${text}»`);
  const unit = UNIT[match[2]];
  const atomic = Number(match[1]) * unit.atomic;
  if (!Number.isInteger(Math.round(atomic * 1e6) / 1e6) || atomic < 1) throw new Error(`«${text}» no da unidades mínimas enteras`);
  return { dimension: unit.dimension, atomic: Math.round(atomic) };
}

/** Id estable por clave: la misma receta, versión o artículo siempre tiene el mismo id. */
export function stableId(key: string): string {
  const h = createHash('sha256').update(`casamiga-recetario:${key}`).digest('hex');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-4${h.slice(13, 16)}-8${h.slice(17, 20)}-${h.slice(20, 32)}`;
}

export const finishedCode = (catalogo: number) => `PT-CM-${String(catalogo).padStart(3, '0')}`;
export const normalize = (text: string) => text.trim().toLocaleLowerCase('es-MX');

/** Nombres con los que un insumo puede existir ya (en la caja o en la demo). */
export const namesOf = (insumo: Recetario['insumos'][number]) => [insumo.nombre, ...(insumo.tambienSeLlama ?? [])].map(normalize);

/** Revisa que el recetario sea coherente: insumos y preparaciones conocidos, sin repetidos y con cantidades válidas. */
export function validate(r: Recetario, catalog?: string[]): string[] {
  const problems: string[] = [];
  const insumos = new Map(r.insumos.map(i => [i.codigo, i]));
  if (insumos.size !== r.insumos.length) problems.push('Insumo con código repetido');
  const preparaciones = new Map(r.preparaciones.map(p => [p.codigo, p]));
  const check = (line: Line, where: string) => {
    const insumo = insumos.get(line.insumo);
    if (!insumo) { problems.push(`${where}: insumo desconocido ${line.insumo}`); return; }
    try { if (quantity(line.cantidad).dimension !== quantity(`1 ${insumo.mide}`).dimension) problems.push(`${where}: ${line.insumo} se mide en ${insumo.mide}`); }
    catch (e) { problems.push(`${where}: ${(e as Error).message}`); }
  };
  for (const p of r.preparaciones) {
    try { quantity(p.rinde); } catch (e) { problems.push(`${p.codigo}: rinde ${(e as Error).message}`); }
    const used = [...p.ingredientes, ...(p.alternativas ?? []).flat()].map(l => l.insumo);
    if (new Set(used).size !== used.length) problems.push(`${p.codigo}: un insumo se repite`);
    p.ingredientes.forEach(l => check(l, p.codigo));
    for (const group of p.alternativas ?? []) { if (group.length < 2) problems.push(`${p.codigo}: alternativa con una sola opción`); group.forEach(l => check(l, p.codigo)); }
  }
  for (const pan of r.panes) {
    for (const req of pan.preparaciones) {
      const prep = preparaciones.get(req.codigo);
      if (!prep) problems.push(`${pan.pan}: preparación desconocida ${req.codigo}`);
      else if (quantity(req.cantidad).dimension !== quantity(prep.rinde).dimension) problems.push(`${pan.pan}: ${req.codigo} rinde en otra unidad`);
    }
    const direct = (pan.directos ?? []).map(l => l.insumo);
    if (new Set(direct).size !== direct.length) problems.push(`${pan.pan}: un insumo directo se repite`);
    (pan.directos ?? []).forEach(l => check(l, pan.pan));
    if (catalog && !catalog.some(name => normalize(name) === normalize(pan.pan))) problems.push(`${pan.pan}: no está en el catálogo`);
  }
  if (new Set(r.panes.map(p => p.catalogo)).size !== r.panes.length) problems.push('Pan repetido');
  return problems;
}
