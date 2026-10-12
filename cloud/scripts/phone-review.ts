// Revisión visual de la web en tamaño celular (375 × 812): entra con un usuario, recorre cada pantalla, guarda una captura de
// página completa y avisa si algo se desborda a lo ancho. Sólo desarrollo; usa Edge/Chrome instalado (playwright-core).
// Uso: node --import tsx scripts/phone-review.ts <dirección> <usuario> <contraseña> <carpeta de salida>
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { chromium } from 'playwright-core';

const [url, username, password, output] = process.argv.slice(2);
if (!output) throw new Error('Uso: phone-review.ts <dirección> <usuario> <contraseña> <carpeta>');
mkdirSync(output, { recursive: true });
const SCREENS = ['vender', 'caja', 'charolas', 'ventas', 'reportes', 'catalogo', 'inventario', 'recetas', 'produccion', 'usuarios', 'nube'];
const browser = await chromium.launch({ channel: process.env.BROWSER_CHANNEL ?? (process.platform === 'win32' ? 'msedge' : 'chrome'), headless: true });
try {
  const context = await browser.newContext({ viewport: { width: 375, height: 812 }, isMobile: true, hasTouch: true, locale: 'es-MX', timezoneId: 'America/Mexico_City' });
  const page = await context.newPage();
  await page.goto(url);
  await page.locator('#username').fill(username);
  await page.locator('#password').fill(password);
  await page.locator('#login-form button[type=submit]').click();
  await page.locator('#app').waitFor({ state: 'visible' });
  // Para ver la Venta: elige la caja web de este dispositivo y, si está cerrada, la abre con fondo $0.
  await page.goto(`${url}/#/caja`);
  const till = page.locator('button.till').first();
  if (await till.waitFor({ timeout: 5000 }).then(() => true, () => false)) await till.click();
  const open = page.getByRole('button', { name: 'Abrir caja' });
  if (await open.waitFor({ timeout: 5000 }).then(() => true, () => false)) {
    await open.click();
    await page.getByLabel('Fondo inicial en MXN').fill('0');
    await page.getByRole('button', { name: 'Confirmar apertura' }).click();
    await page.waitForTimeout(1000);
  }
  const problems: string[] = [];
  for (const screen of SCREENS) {
    await page.goto(`${url}/#/${screen}`);
    await page.locator('#view > *').first().waitFor({ timeout: 10000 }).catch(() => {});
    await page.waitForTimeout(1200);
    const overflow = await page.evaluate(() => document.scrollingElement!.scrollWidth - window.innerWidth);
    // Elementos que se salen por la derecha (sin contar los que viven dentro de un contenedor con desplazamiento propio).
    const wide = await page.evaluate(() => [...document.querySelectorAll('#view *')].filter(e => {
      const r = e.getBoundingClientRect();
      if (r.width === 0 || r.right <= window.innerWidth + 1) return false;
      for (let p = e.parentElement; p; p = p.parentElement) { const s = getComputedStyle(p); if (s.overflowX === 'auto' || s.overflowX === 'scroll' || s.overflowX === 'hidden') return false; }
      return true;
    }).slice(0, 3).map(e => `${e.tagName.toLowerCase()}${e.className ? '.' + String(e.className).split(' ')[0] : ''}`));
    await page.screenshot({ path: join(output, `${screen}.png`), fullPage: true });
    const line = `${screen}: ${overflow > 1 ? `SE DESBORDA ${overflow}px ${wide.join(', ')}` : 'ok'}`;
    if (overflow > 1) problems.push(line);
    console.log(line);
  }
  console.log(problems.length ? `\n${problems.length} pantallas con problema.` : '\nTodas las pantallas caben en el ancho del celular.');
} finally { await browser.close(); }
