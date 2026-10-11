// Vista previa del ticket de la web tal como sale en la impresión del navegador (72 mm), sin imprimir. Sólo desarrollo.
// Uso: node --import tsx scripts/ticket-preview.ts <carpeta de salida>
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { newPage, PASSWORDS, signIn, startApp } from '../test-browser/harness.js';

const output = process.argv[2];
if (!output) throw new Error('Falta la carpeta de salida.');
mkdirSync(output, { recursive: true });
const app = await startApp();
try {
  const { page } = await newPage(app.browser);
  await signIn(page, app.url, 'admin', PASSWORDS.admin);
  await page.locator('#app').waitFor({ state: 'visible' });
  const sale = (method: string, items: [string, number, number][], received: number, reference: string | null) => {
    const total = items.reduce((a, [, q, p]) => a + q * p, 0);
    return { folio: 'C02-000123', at: '2026-10-10T15:42:15Z', cashier: 'Ana López', till: 'CAJA-WEB-02', branch: 'Centro', method, reference, total,
      received: method === 'CASH' ? received : total, change: method === 'CASH' ? received - total : 0,
      items: items.map(([name, quantity, unitPrice]) => ({ name, quantity, unitPrice, total: quantity * unitPrice })) };
  };
  const tickets: [string, unknown, boolean][] = [
    ['web-efectivo', sale('CASH', [['Concha de vainilla', 2, 1300], ['Cuerno', 1, 1500], ['Café de olla', 1, 2000]], 10000, null), false],
    ['web-tarjeta', sale('CARD', [['Rosca de reyes mediana', 1, 25000], ['Oreja', 3, 1200]], 0, '4242'), false],
    ['web-reimpresion', sale('CASH', [['Concha de vainilla', 2, 1300], ['Cuerno', 1, 1500], ['Café de olla', 1, 2000]], 10000, null), true],
  ];
  await page.emulateMedia({ media: 'print' });
  for (const [name, ticket, copy] of tickets) {
    await page.evaluate(async ([t, c]) => {
      window.print = () => {};
      const pos = await import('/js/pos.js');
      pos.printTicket(t, c);
    }, [ticket, copy] as const);
    await page.locator('#print-area .receipt img').evaluateAll(images => Promise.all(images.map(i => (i as HTMLImageElement).decode().catch(() => {}))));
    await page.locator('#print-area .receipt').screenshot({ path: join(output, `${name}.png`) });
    console.log(name);
  }
} finally { await app.close(); }
process.exit(0);
