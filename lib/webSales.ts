// D5: la caja de escritorio baja lo que registraron las cajas web de su sucursal (ventas, devoluciones, turnos, conteos y movimientos)
// para contarlo en sus reportes. Sólo lectura: sale del registro inmutable (sync_event) con un cursor por caja web; dentro de una caja web
// cada operación toma el candado de su fila (web_till), así que su seq crece en el orden en que se confirma y el cursor no se salta nada.
import { ProtocolError } from './protocol.js';
import type { Query } from './panel.js';

export const WEB_SALES_LIMIT = 500;
const ENTITIES = ['cash_session', 'cash_count', 'sale', 'sale_refund', 'cash_movement'];
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** «after» = «terminal:seq,terminal:seq»; una caja web que no aparece empieza desde el principio. */
export function parseCursors(text: string | null): { terminal: string; seq: number }[] {
  if (!text) return [];
  const parts = text.split(',').filter(Boolean);
  if (parts.length > 200) throw new ProtocolError('Demasiados cursores');
  return parts.map(part => {
    const [terminal, seq] = part.split(':');
    const n = Number(seq);
    if (!UUID.test(terminal ?? '') || !Number.isSafeInteger(n) || n < 0) throw new ProtocolError('Cursor inválido');
    return { terminal: terminal.toLowerCase(), seq: n };
  });
}

export async function webSalesFor(q: Query, terminal: { id: string; branchId: string }, cursors: { terminal: string; seq: number }[]) {
  const rows = await q(`SELECT e.terminal_id, t.name, e.seq, e.entity, e.entity_id, e.change, e.payload
    FROM sync_event e JOIN terminal t ON t.id = e.terminal_id
    LEFT JOIN jsonb_to_recordset($2::jsonb) AS c(terminal uuid, seq bigint) ON c.terminal = e.terminal_id
    WHERE e.branch_id = $1 AND t.kind = 'web' AND e.entity = ANY($3::text[]) AND e.seq > COALESCE(c.seq, 0)
    ORDER BY e.terminal_id, e.seq LIMIT ${WEB_SALES_LIMIT}`, [terminal.branchId, JSON.stringify(cursors), ENTITIES]);
  return rows.map(r => ({ terminal: String(r.terminal_id), terminalName: String(r.name), seq: Number(r.seq), entity: String(r.entity),
    entityId: String(r.entity_id), change: String(r.change), payload: r.payload }));
}
