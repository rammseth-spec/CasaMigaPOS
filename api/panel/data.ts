import { query } from '../../lib/db.ts';
import { failure, json } from '../../lib/protocol.ts';
import { dashboard, DEFAULT_TIME_ZONE, parseRange, requireSession } from '../../lib/panel.ts';

/** Ventas, cortes, productos y terminales del periodo (?from=AAAA-MM-DD&to=AAAA-MM-DD&branch=uuid opcional). */
export async function GET(request: Request): Promise<Response> {
  try {
    requireSession(request, process.env.PANEL_PASSWORD);
    const range = parseRange(new URL(request.url), process.env.PANEL_TIME_ZONE || DEFAULT_TIME_ZONE);
    return json(200, await dashboard(query(), range));
  } catch (error) {
    return failure(error);
  }
}
