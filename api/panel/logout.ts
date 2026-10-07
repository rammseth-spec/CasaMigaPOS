import { json } from '../../lib/protocol.ts';
import { clearedCookie } from '../../lib/panel.ts';

export async function POST(): Promise<Response> {
  const response = json(200, { ok: true });
  response.headers.append('set-cookie', clearedCookie);
  return response;
}
