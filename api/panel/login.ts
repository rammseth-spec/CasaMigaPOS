import { failure, json, ProtocolError, readJson } from '../../lib/protocol.ts';
import { issueSession, panelPassword, passwordMatches, sessionCookie } from '../../lib/panel.ts';

/** Inicia sesión en el panel con la contraseña de PANEL_PASSWORD. Un error tarda a propósito para frenar intentos repetidos. */
export async function POST(request: Request): Promise<Response> {
  try {
    const password = panelPassword(process.env.PANEL_PASSWORD);
    const body = (await readJson(request, 2_000)) as { password?: unknown } | null;
    if (!passwordMatches(body?.password, password)) {
      await new Promise(resolve => setTimeout(resolve, 800));
      throw new ProtocolError('Contraseña incorrecta', 401);
    }
    const response = json(200, { ok: true });
    response.headers.append('set-cookie', sessionCookie(issueSession(password)));
    return response;
  } catch (error) {
    return failure(error);
  }
}
