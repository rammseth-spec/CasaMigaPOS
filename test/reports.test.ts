import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseRange } from '../lib/panel.js';
import * as reports from '../lib/reports.js';
import { caja1, centro, database, seedSales, send, user } from './helpers.js';

const range = (from: string, to: string, branch: string | null = centro) =>
  ({ ...parseRange(new URL(`https://x/?from=${from}&to=${to}`)), branchId: branch });

test('the summary has the same figures as the till: products, cashiers, payments and shifts', async () => {
  const { q } = await database(); await seedSales(q);
  await user(q, caja1, centro, 'u-admin', 'admin', 'Administrator', 'Admin#2026centro');
  const r = await reports.report(q, range('2026-10-06', '2026-10-07'));
  const s = r.summary;
  assert.deepEqual([s.saleCount, s.gross, s.refunded, s.net, s.averageTicket], [5, 17000, 1900, 15100, 3400]);
  assert.deepEqual(s.products.map(p => [p.name, p.quantitySold, p.sold, p.quantityRefunded, p.refunded, p.net]),
    [['Concha', 6, 11400, 1, 1900, 9500], ['Bolillo', 10, 3000, 0, 0, 3000], ['Dona', 2, 2600, 0, 0, 2600]], 'By net, refunds by refund date.');
  assert.equal(s.cashiers.length, 1);
  assert.deepEqual([s.cashiers[0].sales, s.cashiers[0].sold], [5, 17000]);
  assert.deepEqual(s.payments.map(p => [p.method, p.sales, p.sold, p.refunded, p.net]),
    [['CASH', 3, 11400, 1900, 9500], ['CARD', 1, 2600, 0, 2600], ['TRANSFER', 1, 3000, 0, 3000]], 'Cash, card, transfer; refunds by the sale method.');
  const [a, b] = s.sessions;
  assert.deepEqual([a.opening, a.cashSales, a.refunds, a.movements, a.expected, a.counted, a.difference, a.closed], [50000, 5700, 1900, 0, 53800, 51700, -2100, true],
    'Expected is recomputed like the till (fund + cash sales − cash refunds + movements).');
  assert.deepEqual([b.expected, b.counted, b.difference, b.closed], [20000 + 5700 + 5450, null, null, false]);

  assert.deepEqual(r.days.map(d => [d.day, d.sales, d.amount]), [['2026-10-06', 3, 8300], ['2026-10-07', 2, 8700]]);
  assert.deepEqual([r.previousFrom, r.previousTo, r.previous.saleCount, r.comparison.salesChange], ['2026-10-04', '2026-10-05', 0, null], 'Same length right before; no base → null.');
  assert.deepEqual([r.heatmap.firstHour, r.heatmap.lastHour], [7, 21], 'At least 7–20; the 21:30 sale widens it.');
  assert.equal(r.heatmap.rows.flatMap(x => x.cells).reduce((t, c) => t + c.sales, 0), 5);

  const [cashier] = r.cashControl.cashiers;
  assert.deepEqual([cashier.shifts, cashier.closedShifts, cashier.withDifference, cashier.shortages, cashier.overages, cashier.net, cashier.refunds, cashier.refunded],
    [2, 1, 1, 2100, 0, -2100, 1, 1900]);
  assert.deepEqual(r.cashControl.reasons, [{ reason: 'Pan aplastado', count: 1, amount: 1900 }]);
});

test('cancelled sales leave the day and hour charts but their refund still counts', async () => {
  const { q } = await database(); await seedSales(q);
  await send(q, caja1, centro, [{ entity: 'sale_refund', id: 'r2', payload: { refund: { id: 'r2', kind: 'CANCELLATION', reason: 'pan aplastado ', user_id: 'u-admin', sale_id: 's4',
    cash_session_id: 'x', refunded_utc: '2026-10-07T15:30:00+00:00', total_cents: 3000 }, items: [{ position: 1, quantity: 10, line_total_cents: 3000 }] } }]);
  const r = await reports.report(q, range('2026-10-06', '2026-10-07'));
  assert.deepEqual(r.days.find(d => d.day === '2026-10-07'), { day: '2026-10-07', sales: 1, amount: 5700 });
  assert.equal(r.heatmap.rows.flatMap(x => x.cells).reduce((t, c) => t + c.sales, 0), 4);
  assert.equal(r.summary.refunded, 4900);
  assert.deepEqual(r.cashControl.reasons.map(x => [x.count, x.amount]), [[2, 4900]], 'Reasons group without case or spaces.');
});

test('the production forecast averages only the days with sales of active products, plus 10 %', async () => {
  const { q } = await database(); await seedSales(q);
  await send(q, caja1, centro, [
    { entity: 'product', id: 'p-Concha', payload: { product: { id: 'p-Concha', name: 'Concha', active: 1 } } },
    { entity: 'product', id: 'p-Dona', payload: { product: { id: 'p-Dona', name: 'Dona', active: 0 } } },
  ]);
  const f = await reports.forecast(q, centro, 'America/Mexico_City', '2026-10-13');
  assert.equal(f.weeksWithData, 1, 'Only one of the four Tuesdays had sales.');
  assert.deepEqual(f.products.map(p => [p.name, p.last, p.average, p.suggested, p.daysWithSales]), [['Concha', 3, 3, 4, 1]], 'Inactive Dona is left out.');
  assert.equal(f.total, 4);
});

test('percent change rounds like the till', () => {
  assert.equal(reports.change(150, 100), 50);
  assert.equal(reports.change(1, 3), -66.7);
  assert.equal(reports.change(5, 0), null);
  assert.equal(reports.change(1001, 800), 25.1);
});
