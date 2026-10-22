// Rendimientos sospechosos, igual que la caja (RecipeYieldCheck): escribir gramos con la unidad en kg multiplica el rendimiento
// por 1,000. Sólo avisa; nunca corrige el valor. Compara en g, ml o piezas y nunca peso contra volumen o piezas.
// Sin dependencias, para que la prueben también las pruebas de la nube.

/** Más de este número de veces mayor o menor que el rendimiento anterior pide confirmación. */
export const FACTOR = 10;
/** Lo más que rinde una sola receta en Casa Miga (decisión del dueño, 22/10/2026): 20 kg, 50 L o 500 piezas (unidades mínimas). */
export const MAXIMUM = { MASS: 20_000_000, VOLUME: 50_000_000, COUNT: 500 };

const number = (value, digits) => new Intl.NumberFormat('en-US', { maximumFractionDigits: digits }).format(value);
const kind = dimension => ({ MASS: 'peso', VOLUME: 'volumen' })[dimension] ?? 'piezas';

/** En la unidad base para comparar sin confusión: g, ml o piezas. */
export function normalized({ dimension, atomic }) {
  if (dimension === 'MASS') return `${number(atomic / 1000, 3)} g`;
  if (dimension === 'VOLUME') return `${number(atomic / 1000, 3)} ml`;
  return `${number(atomic, 0)} ${atomic === 1 ? 'pieza' : 'piezas'}`;
}

/**
 * Texto de confirmación, o null si no hace falta. previous y next son { dimension: 'MASS'|'VOLUME'|'COUNT', atomic }.
 * Sin rendimiento anterior válido sólo se revisa el máximo.
 */
export function yieldWarning(previous, next) {
  if (!next || !(next.atomic > 0)) return null;
  const reasons = [];
  const maximum = { dimension: next.dimension, atomic: MAXIMUM[next.dimension] ?? MAXIMUM.COUNT };
  if (next.atomic > maximum.atomic) reasons.push(`El rendimiento (${normalized(next)}) pasa del máximo que rinde una receta en Casa Miga: ${normalized(maximum)}.`);
  if (previous && previous.atomic > 0) {
    if (previous.dimension !== next.dimension)
      reasons.push(`Cambiaste el tipo de rendimiento: antes ${kind(previous.dimension)} (${normalized(previous)}), ahora ${kind(next.dimension)} (${normalized(next)}).\n\n` +
        'Peso, volumen y piezas no se comparan entre sí. Revisa que esta receta realmente se mida así.');
    else if (next.atomic > previous.atomic * FACTOR || previous.atomic > next.atomic * FACTOR) {
      const times = Math.round(Math.max(previous.atomic, next.atomic) / Math.min(previous.atomic, next.atomic));
      reasons.push(`El rendimiento cambia más de ${FACTOR} veces respecto al anterior.\n\n` +
        `Antes: ${normalized(previous)}\nAhora: ${normalized(next)} (unas ${number(times, 0)} veces ${next.atomic > previous.atomic ? 'más' : 'menos'})`);
    }
  }
  if (!reasons.length) return null;
  return `${reasons.join('\n\n')}\n\nPuede ser un error de unidad, por ejemplo escribir gramos con la unidad en kg. El valor no se corrige solo: si es un error, cancela y revisa la cantidad y la unidad.\n\n¿Guardar de todos modos?`;
}
