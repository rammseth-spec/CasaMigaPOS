// Recorridos de la versión web en un navegador real, en orden (comparten la base): entrar, vender, historial, reportes, catálogo,
// usuarios, charolas, corte de caja y celular. Las capturas quedan en test-browser/output/.
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import type { Browser, Page } from 'playwright-core';
import { horizontalOverflow, newPage, PASSWORDS, shot, signIn, startApp } from './harness.js';
import { caja1, centro, send } from '../test/helpers.js';

let app: Awaited<ReturnType<typeof startApp>>;
let page: Page;
before(async () => { app = await startApp(); ({ page } = await newPage(app.browser)); });
after(async () => { await app?.close(); });

/** Si falla, deja la captura de lo que se veía. */
async function step(name: string, body: () => Promise<void>) {
  try { await body(); } catch (error) { await shot(page, `FALLA-${name}`).catch(() => {}); throw error; }
}
const nav = (hash: string) => page.goto(`${app.url}/#/${hash}`);

test('sign in: a wrong password is refused with a message, the right one opens the till', () => step('entrar', async () => {
  await signIn(page, app.url, 'admin', 'equivocada');
  await assert.doesNotReject(page.locator('#login-error').filter({ hasText: /./ }).waitFor());
  assert.match(await page.locator('#login-error').innerText(), /incorrect/i);
  await signIn(page, app.url, 'admin', PASSWORDS.admin);
  await page.locator('#app').waitFor({ state: 'visible' });
  const links = await page.locator('#nav-links a').allInnerTexts();
  for (const name of ['Venta', 'Caja', 'Charolas', 'Ventas', 'Reportes', 'Catálogo', 'Usuarios']) assert.ok(links.some(l => l.includes(name)), `${name} en el menú`);
}));

async function openTill() {
  await nav('caja');
  await page.getByRole('button', { name: /CAJA-WEB-02/ }).click();
  await page.getByRole('button', { name: 'Abrir caja' }).click();
  await page.getByLabel('Fondo inicial en MXN').fill('500');
  await page.getByRole('button', { name: 'Confirmar apertura' }).click();
  await page.getByText('Caja abierta.').first().waitFor();
}

async function sell(items: [string, number][], pay: (dialog: ReturnType<Page['getByRole']>) => Promise<void>) {
  await nav('vender');
  for (const [name, times] of items) for (let i = 0; i < times; i++) await page.locator('.tile', { hasText: name }).click();
  await page.locator('button.pay').click();
  const dialog = page.getByRole('dialog', { name: 'Cobro' });
  await pay(dialog);
  const result = await dialog.locator('.result-text').innerText();
  await dialog.getByRole('button', { name: 'Volver al POS' }).click();
  return result;
}

test('sell: cash with change and card with reference, folios of the web till', () => step('vender', async () => {
  await openTill();
  const cash = await sell([['Concha de vainilla', 2], ['Café de olla', 1]], async dialog => {
    await dialog.getByLabel('Efectivo recibido').fill('100');
    assert.equal(await dialog.locator('.checkout-change strong').innerText(), '$54.00');
    await dialog.getByRole('button', { name: 'Cobrar sin imprimir ticket' }).click();
  });
  assert.match(cash, /Folio: C02-000001/); assert.match(cash, /Cambio: \$54\.00/);
  await shot(page, 'venta-efectivo');
  const card = await sell([['Cuerno', 1]], async dialog => {
    await dialog.getByRole('radio', { name: 'Pago con tarjeta' }).click();
    await dialog.getByLabel('Referencia del pago').fill('4242');
    await dialog.getByRole('button', { name: 'Cobrar sin imprimir ticket' }).click();
  });
  assert.match(card, /Folio: C02-000002/); assert.match(card, /Tarjeta · Ref\. 4242/);
}));

/** Las tarjetas sin foto usan todo el ancho, como las de la caja. */
test('sale tiles: the photo area is as wide as every card', () => step('tarjetas', async () => {
  await nav('vender');
  const widths = await page.locator('.tile').evaluateAll(tiles => tiles.map(t => [t.getBoundingClientRect().width, t.querySelector('.tile-photo')!.getBoundingClientRect().width]));
  assert.ok(widths.length >= 4);
  for (const [tile, photo] of widths) assert.ok(photo >= tile - 18, `foto ${photo}px en tarjeta de ${tile}px`);
}));

test('sales history lists both sales of today and opens the detail', () => step('historial', async () => {
  await nav('ventas');
  await page.getByRole('button', { name: 'Hoy', exact: true }).click();
  const table = page.getByRole('table', { name: 'Historial de ventas' });
  await table.getByText('C02-000002').waitFor();
  assert.equal(await table.locator('tbody tr').count(), 2);
  await table.getByText('C02-000001').click();
  await page.getByRole('button', { name: 'Ver detalle' }).click();
  const lines = page.getByRole('table', { name: 'Partidas de venta' });
  await lines.getByText('Concha de vainilla').waitFor();
  assert.equal(await lines.locator('tbody tr').count(), 2);
  await shot(page, 'historial-detalle');
}));

test('reports: the summary counts the two web sales', () => step('reportes', async () => {
  await nav('reportes');
  await page.getByRole('button', { name: 'Resumen' }).click();
  const kpi = (label: string) => page.locator('.report-kpi', { has: page.locator('.kpi-label', { hasText: new RegExp(`^${label}$`) }) }).locator('.kpi-value');
  await assert.doesNotReject(kpi('Ventas').filter({ hasText: '2' }).waitFor());
  assert.equal(await kpi('Vendido').innerText(), '$61.00');
  await shot(page, 'reportes-resumen');
}));

test('catalog: a new product shows up at once, marked until the register confirms it', () => step('catalogo', async () => {
  await nav('catalogo');
  await page.getByRole('button', { name: 'Nuevo producto' }).click();
  await page.getByLabel('Nombre de producto').fill('Bolillo');
  await page.getByLabel('Categoría del producto').selectOption({ label: 'Pan dulce' });
  await page.getByLabel('Precio MXN').fill('5.00');
  await page.getByRole('button', { name: 'Guardar producto' }).click();
  const row = page.locator('tr', { hasText: 'Bolillo' });
  await row.waitFor();
  assert.match(await row.innerText(), /\$5\.00/);
  assert.equal(await row.locator('.pending-pill').innerText(), 'Por confirmar');
  await shot(page, 'catalogo-por-confirmar');
  await nav('vender');
  await page.locator('.tile', { hasText: 'Bolillo' }).waitFor();
}));

test('users: a new cashier gets a temporary password, must change it and then only sells', () => step('usuarios', async () => {
  await nav('usuarios');
  await page.getByLabel('Usuario nuevo').fill('pepe');
  await page.getByLabel('Nombre para mostrar').fill('Pepe Cajero');
  await page.getByRole('button', { name: 'Crear usuario' }).click();
  const notice = await page.locator('.users-notice p').innerText();
  const temporary = /PEPE: (\S+)/.exec(notice)![1];
  await page.locator('tr', { hasText: 'Pepe Cajero' }).locator('.pending-pill').waitFor();

  await page.locator('#logout').click();
  await signIn(page, app.url, 'pepe', temporary);
  await page.locator('#password-form').waitFor({ state: 'visible' });
  assert.equal(await page.locator('#pw-username').inputValue(), 'pepe');
  await page.locator('#pw-current').fill(temporary);
  await page.locator('#pw-next').fill('Pepe#2026nueva');
  await page.locator('#pw-confirm').fill('Pepe#2026nueva');
  await page.locator('#password-form button[type=submit]').click();
  await page.locator('#app').waitFor({ state: 'visible' });
  const links = await page.locator('#nav-links a').allInnerTexts();
  assert.ok(links.some(l => l.includes('Venta')));
  assert.ok(!links.some(l => l.includes('Reportes') || l.includes('Usuarios')), `A cashier only sells: ${links.join(', ')}`);
  await page.locator('#logout').click();
  await signIn(page, app.url, 'admin', PASSWORDS.admin);
  await page.locator('#app').waitFor({ state: 'visible' });
}));

test('trays: a new store is listed at once', () => step('charolas', async () => {
  await nav('charolas');
  await page.getByRole('button', { name: 'Tiendas' }).click();
  await page.getByRole('button', { name: 'Nueva tienda' }).click();
  await page.getByLabel('Nombre de la tienda').fill('Abarrotes Lupita');
  await page.getByRole('button', { name: 'Guardar tienda' }).click();
  const store = page.locator('.trays-store-item', { hasText: 'Abarrotes Lupita' });
  await store.locator('.pending-pill').waitFor();
  await shot(page, 'charolas-tienda');
}));

test('cash: blind count, shortage needs a note, then the till closes', () => step('corte', async () => {
  await nav('caja');
  await page.getByRole('button', { name: 'Cerrar caja' }).click();
  await page.getByLabel('Efectivo contado en MXN').fill('520');
  await page.getByRole('button', { name: 'Continuar' }).click();
  // Esperado: fondo $500 + ventas en efectivo $46 = $546; contó $520.
  await page.getByText('Efectivo esperado: $546.00').waitFor();
  const confirm = page.getByRole('button', { name: 'Confirmar cierre' });
  assert.ok(await confirm.isDisabled(), 'Without a note the shortage cannot be closed.');
  await page.getByLabel('Nota de discrepancia').fill('Faltante en revisión');
  await confirm.click();
  await page.getByText('Caja cerrada.').first().waitFor();
  await shot(page, 'caja-cerrada');
}));

test('alert: the desktop register has its till open and stopped connecting', () => step('aviso', async () => {
  await send(app.q, caja1, centro, [{ entity: 'cash_session', id: 'cs-desk', payload: { id: 'cs-desk', terminal_id: caja1, opened_by: 'x', opened_utc: new Date().toISOString(), opening_cents: 0, state: 'OPEN' } }]);
  await app.q(`UPDATE terminal SET last_seen_at = now() - interval '25 minutes' WHERE id = $1`, [caja1]);
  await page.reload();
  const alert = page.locator('#alert');
  await alert.waitFor({ state: 'visible', timeout: 15000 });
  assert.match(await alert.innerText(), /CASA-MIGA-CAJA-01 tiene la caja abierta y no se conecta desde las/);
  await shot(page, 'aviso-caja-sin-conexion');
  await alert.click();
  await page.waitForURL(/#\/nube$/);
}));

test('phone: nothing scrolls sideways and the menu opens', async () => {
  const { context, page: phone } = await newPage(app.browser as Browser, 'phone');
  try {
    await signIn(phone, app.url, 'admin', PASSWORDS.admin);
    await phone.locator('#app').waitFor({ state: 'visible' });
    for (const screen of ['vender', 'caja', 'ventas', 'reportes', 'catalogo', 'usuarios', 'charolas']) {
      await phone.goto(`${app.url}/#/${screen}`);
      await phone.locator('#view').locator(':scope > *').first().waitFor();
      await phone.waitForTimeout(300);
      assert.ok(await horizontalOverflow(phone) <= 1, `${screen} se desplaza a lo ancho ${await horizontalOverflow(phone)} px`);
    }
    await phone.locator('#menu').click();
    // El menú entra deslizándose (0.2 s): debe quedar completo dentro de la pantalla.
    await phone.waitForFunction(() => document.getElementById('nav')!.getBoundingClientRect().left >= 0);
    await phone.waitForTimeout(250);
    await shot(phone, 'celular-menu');
  } catch (error) { await shot(phone, 'FALLA-celular').catch(() => {}); throw error; }
  finally { await context.close(); }
});
