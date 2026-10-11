// Pruebas en navegador de la versión web: servidor local (scripts/localServer.ts) con login real sobre PGlite y un Chromium ya instalado
// (Edge en Windows, Chrome en Linux; BROWSER_CHANNEL lo cambia). playwright-core no descarga navegadores.
import { mkdirSync } from 'node:fs';
import { chromium, type Browser, type BrowserContext, type Page } from 'playwright-core';
import { createTill } from '../lib/pos.js';
import { login as apiLogin } from '../lib/auth.js';
import { startLocalServer } from '../scripts/localServer.js';
import { caja1, centro, database, permissions, send, user } from '../test/helpers.js';

export const PASSWORDS = { admin: 'Admin#2026centro', ana: 'Ana#2026centro', luis: 'Luis#2026centro' } as const;
export const SHOTS = new URL('./output/', import.meta.url);
const pan = 'd0000000-0000-4000-8000-000000000001';
const bebida = 'd0000000-0000-4000-8000-000000000002';

/** Sucursal Centro con su caja de escritorio, tres usuarios, cuatro panes y una caja web lista (CAJA-WEB-02). */
export async function startApp() {
  const { q } = await database();
  await permissions(q, caja1, centro);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000001', 'admin', 'Administrator', PASSWORDS.admin);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000002', 'ana', 'Manager', PASSWORDS.ana);
  await user(q, caja1, centro, 'a0000000-0000-4000-8000-000000000003', 'luis', 'Cashier', PASSWORDS.luis);
  const product = (id: string, name: string, price: number, category: string) =>
    ({ entity: 'product', id, payload: { product: { id, sku: null, name, category_id: category, description: '', price_cents: price, track_inventory: 0, active: 1 }, inventoryLinks: [] } });
  await send(q, caja1, centro, [
    { entity: 'category', id: pan, payload: { id: pan, name: 'Pan dulce', description: '', active: 1, sort_order: 1 } },
    { entity: 'category', id: bebida, payload: { id: bebida, name: 'Bebidas', description: '', active: 1, sort_order: 2 } },
    product('e0000000-0000-4000-8000-000000000001', 'Concha de vainilla', 1300, pan),
    product('e0000000-0000-4000-8000-000000000002', 'Cuerno', 1500, pan),
    product('e0000000-0000-4000-8000-000000000003', 'Oreja', 1200, pan),
    product('e0000000-0000-4000-8000-000000000004', 'Café de olla', 2000, bebida),
  ]);
  const till = await createTill(q, await apiLogin(q, 'admin', PASSWORDS.admin), centro);
  const { server, url } = await startLocalServer(q);
  const browser = await chromium.launch({ channel: process.env.BROWSER_CHANNEL ?? (process.platform === 'win32' ? 'msedge' : 'chrome'), headless: true });
  mkdirSync(SHOTS, { recursive: true });
  return { q, url, till, browser, close: async () => { await browser.close(); await new Promise(r => server.close(r)); } };
}

/** Pantalla de escritorio como la caja (1280 × 820) o celular (375 × 812). */
export async function newPage(browser: Browser, size: 'desktop' | 'phone' = 'desktop'): Promise<{ context: BrowserContext; page: Page }> {
  const context = await browser.newContext(size === 'phone'
    ? { viewport: { width: 375, height: 812 }, isMobile: true, hasTouch: true, locale: 'es-MX', timezoneId: 'America/Mexico_City' }
    : { viewport: { width: 1280, height: 820 }, locale: 'es-MX', timezoneId: 'America/Mexico_City' });
  const page = await context.newPage();
  page.setDefaultTimeout(8000);
  return { context, page };
}

/** Entra con el formulario real (usuario y contraseña). */
export async function signIn(page: Page, url: string, username: string, password: string) {
  await page.goto(url);
  await page.locator('#username').fill(username);
  await page.locator('#password').fill(password);
  await page.locator('#login-form button[type=submit]').click();
}

export async function shot(page: Page, name: string) {
  await page.screenshot({ path: new URL(`${name}.png`, SHOTS).pathname.replace(/^\/([A-Za-z]:)/, '$1'), fullPage: false });
}

/** Ninguna pantalla debe desplazarse a lo ancho (celular). */
export async function horizontalOverflow(page: Page) {
  return page.evaluate(() => document.scrollingElement!.scrollWidth - window.innerWidth);
}
