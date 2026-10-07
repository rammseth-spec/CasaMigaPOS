import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { PGlite } from '@electric-sql/pglite';
import { ProtocolError } from '../lib/protocol.ts';
import {
  dashboard, issueSession, panelPassword, parseRange, passwordMatches, readCookie, requireSession, sessionValid, type Query,
} from '../lib/panel.ts';

const centro = '11111111-1111-4111-8111-111111111111';
const norte = '22222222-2222-4222-8222-222222222222';
const caja1 = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const caja2 = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const sessionA = 'c0000000-0000-4000-8000-00000000000a';
const sessionB = 'c0000000-0000-4000-8000-00000000000b';

async function database(): Promise<{ q: Query; db: PGlite }> {
  const db = new PGlite();
  await db.exec(readFileSync(new URL('../db/001_init.sql', import.meta.url), 'utf8'));
  await db.query(`INSERT INTO branch(id, name) VALUES ($1, 'Centro'), ($2, 'Norte')`, [centro, norte]);
  await db.query(`INSERT INTO terminal(id, branch_id, name, key_sha256, last_seen_at) VALUES
    ($1, $2, 'CASA-MIGA-CAJA-01', repeat('a', 64), '2026-10-07T18:00:00Z'), ($3, $4, 'CASA-MIGA-CAJA-02', repeat('b', 64), NULL)`, [caja1, centro, caja2, norte]);
  return { db, q: async (text, params) => (await db.query(text, params)).rows as Record<string, unknown>[] };
}

let seq = 0;
async function event(db: PGlite, terminal: string, branch: string, entity: string, entityId: string, payload: unknown, change = 'insert') {
  await db.query(`INSERT INTO sync_event(terminal_id, seq, branch_id, entity, entity_id, change, enqueued_at, payload)
    VALUES ($1, $2, $3, $4, $5, $6, now(), $7::jsonb)`, [terminal, ++seq, branch, entity, entityId, change, JSON.stringify(payload)]);
}

function sale(id: string, session: string, at: string, method: string, items: [string, number, number][]) {
  const total = items.reduce((a, [, q, p]) => a + q * p, 0);
  return {
    sale: { id, cash_session_id: session, confirmed_utc: at, subtotal_cents: total, total_cents: total, sequence: 1 },
    items: items.map(([name, quantity, price], i) => ({ sale_id: id, position: i + 1, product_name: name, unit_price_cents: price, quantity, line_total_cents: quantity * price })),
    payment: { sale_id: id, method, applied_cents: total, received_cents: total, change_cents: 0 },
  };
}

async function seed(db: PGlite) {
  // Centro, caja 1: turno A cerrado el 06/10 con $20 de faltante; turno B abierto el 07/10 con gasto y entrada.
  await event(db, caja1, centro, 'cash_session', sessionA, { id: sessionA, opened_utc: '2026-10-06T13:00:00+00:00', opening_cents: 50000, state: 'OPEN' });
  await event(db, caja1, centro, 'sale', 's1', sale('s1', sessionA, '2026-10-06T15:00:00+00:00', 'CASH', [['Concha', 2, 1900]]));
  await event(db, caja1, centro, 'sale', 's2', sale('s2', sessionA, '2026-10-06T16:00:00+00:00', 'CARD', [['Dona', 2, 1300]]));
  // 21:30 en México sigue siendo el 06/10 aunque en UTC ya es el 07/10.
  await event(db, caja1, centro, 'sale', 's3', sale('s3', sessionA, '2026-10-07T03:30:00+00:00', 'CASH', [['Concha', 1, 1900]]));
  await event(db, caja1, centro, 'sale_refund', 'r1', { refund: { id: 'r1', sale_id: 's1', cash_session_id: sessionA, refunded_utc: '2026-10-06T17:00:00+00:00', total_cents: 1900 }, items: [] });
  await event(db, caja1, centro, 'cash_session', sessionA, { id: sessionA, opened_utc: '2026-10-06T13:00:00+00:00', opening_cents: 50000, state: 'CLOSED',
    closed_utc: '2026-10-07T04:00:00+00:00', expected_cents: 53700, counted_cents: 51700, difference_cents: -2000 }, 'update');
  await event(db, caja1, centro, 'cash_session', sessionB, { id: sessionB, opened_utc: '2026-10-07T14:00:00+00:00', opening_cents: 20000, state: 'OPEN' });
  await event(db, caja1, centro, 'sale', 's4', sale('s4', sessionB, '2026-10-07T15:00:00+00:00', 'TRANSFER', [['Bolillo', 10, 300]]));
  await event(db, caja1, centro, 'sale', 's5', sale('s5', sessionB, '2026-10-07T16:00:00+00:00', 'CASH', [['Concha', 3, 1900]]));
  for (const [id, action, amount] of [['a1', 'cash.expense', 4550], ['a2', 'cash.deposit', 10000], ['a3', 'sale.completed', 999]] as const)
    await event(db, caja1, centro, 'audit_entry', id, { id, action, details_json: JSON.stringify({ cashSessionId: sessionB, amountCents: amount }) });
  // Un respaldo restaurado reenvía s5 con otro seq: se cuenta una sola vez.
  await event(db, caja1, centro, 'sale', 's5', sale('s5', sessionB, '2026-10-07T16:00:00+00:00', 'CASH', [['Concha', 3, 1900]]));
  // Norte, caja 2.
  await event(db, caja2, norte, 'sale', 'n1', sale('n1', 'c0000000-0000-4000-8000-0000000000ff', '2026-10-07T15:00:00+00:00', 'CASH', [['Rosca', 1, 25000]]));
}

const range = (from: string, to: string, branch?: string) =>
  parseRange(new URL(`https://x/api/panel/data?from=${from}&to=${to}${branch ? `&branch=${branch}` : ''}`));

test('dashboard groups sales by local day, method and branch, net of refunds', async () => {
  const { db, q } = await database(); await seed(db);
  const all = await dashboard(q, range('2026-10-06', '2026-10-07'));
  assert.equal(all.totals.tickets, 6, 's5 duplicated by a restored backup counts once');
  assert.equal(all.totals.gross, 3800 + 2600 + 1900 + 3000 + 5700 + 25000);
  assert.equal(all.totals.refunds, 1900);
  assert.equal(all.totals.net, all.totals.gross - 1900);
  assert.deepEqual(all.totals.byMethod, { cash: 3800 + 1900 + 5700 + 25000 - 1900, card: 2600, transfer: 3000 });

  const centroDays = all.days.filter(d => d.branchId === centro);
  assert.deepEqual(centroDays.map(d => [d.day, d.tickets, d.gross, d.refunds]), [['2026-10-06', 3, 8300, 1900], ['2026-10-07', 2, 8700, 0]]);
  assert.deepEqual(all.branches.map(b => b.name), ['Centro', 'Norte']);
  assert.deepEqual(all.products[0], { name: 'Rosca', quantity: 1, total: 25000 });
  assert.deepEqual(all.products.find(p => p.name === 'Concha'), { name: 'Concha', quantity: 6, total: 11400 });

  const onlyNorte = await dashboard(q, range('2026-10-06', '2026-10-07', norte));
  assert.equal(onlyNorte.totals.tickets, 1);
  assert.equal(onlyNorte.sessions.length, 0);
  assert.deepEqual(onlyNorte.terminals.map(t => t.name), ['CASA-MIGA-CAJA-02']);
  assert.equal(onlyNorte.terminals[0].lastSeenAt, null);
});

test('cortes show recorded result when closed and running expected cash when open', async () => {
  const { db, q } = await database(); await seed(db);
  const data = await dashboard(q, range('2026-10-06', '2026-10-07', centro));
  const [open, closed] = data.sessions;
  assert.equal(closed.id, sessionA); assert.equal(closed.open, false);
  assert.equal(closed.expected, 53700); assert.equal(closed.counted, 51700); assert.equal(closed.difference, -2000);
  assert.equal(closed.cashSales, 3800 + 1900); assert.equal(closed.cashRefunds, 1900);
  assert.equal(closed.terminal, 'CASA-MIGA-CAJA-01');
  assert.equal(open.id, sessionB); assert.equal(open.open, true);
  assert.equal(open.movements, 10000 - 4550);
  assert.equal(open.expected, 20000 + 5700 + 10000 - 4550, 'Opening + cash sales + deposit − expense');
  assert.equal(open.counted, null); assert.equal(open.difference, null);
  assert.equal(data.totals.shortages, -2000); assert.equal(data.totals.overages, 0);
});

test('periods outside the data return empty sections', async () => {
  const { db, q } = await database(); await seed(db);
  const data = await dashboard(q, range('2026-09-01', '2026-09-30'));
  assert.equal(data.totals.tickets, 0); assert.equal(data.days.length, 0); assert.equal(data.sessions.length, 0); assert.equal(data.products.length, 0);
  assert.equal(data.terminals.length, 2);
});

test('range validation', () => {
  assert.deepEqual(range('2026-10-01', '2026-10-07', centro.toUpperCase()), { from: '2026-10-01', to: '2026-10-07', branchId: centro, timeZone: 'America/Mexico_City' });
  for (const [from, to] of [['2026-10-07', '2026-10-01'], ['2026-02-30', '2026-03-01'], ['ayer', '2026-10-01'], ['2026-01-01', '2026-12-31']])
    assert.throws(() => range(from, to), ProtocolError, `${from}..${to}`);
  assert.throws(() => range('2026-10-01', '2026-10-02', 'centro'), /Sucursal/);
});

test('panel session cookie is signed, expires and dies when the password changes', () => {
  const password = 'Panel-de-prueba-2026';
  const now = Date.UTC(2026, 9, 7, 12);
  const cookie = issueSession(password, now);
  assert.equal(sessionValid(cookie, password, now + 1000), true);
  assert.equal(sessionValid(cookie, password, now + 13 * 3_600_000), false, 'Expires after 12 h');
  assert.equal(sessionValid(cookie, 'Otra-contraseña-2026', now), false);
  const [expires, signature] = cookie.split('.');
  assert.equal(sessionValid(`${Number(expires) + 1000}.${signature}`, password, now), false, 'Tampered expiry');
  assert.equal(sessionValid(undefined, password, now), false);
  assert.equal(sessionValid('basura', password, now), false);

  assert.equal(passwordMatches(password, password), true);
  assert.equal(passwordMatches('panel-de-prueba-2026', password), false);
  assert.equal(passwordMatches(undefined, password), false);
  assert.throws(() => panelPassword(undefined), (e: ProtocolError) => e.status === 503);
  assert.throws(() => panelPassword('corta'), (e: ProtocolError) => e.status === 503);
  assert.equal(readCookie(`otra=1; cm_panel=${cookie}; x=2`), cookie);

  const signed = new Request('https://x/api/panel/data', { headers: { cookie: `cm_panel=${issueSession(password)}` } });
  assert.doesNotThrow(() => requireSession(signed, password));
  assert.throws(() => requireSession(new Request('https://x/api/panel/data'), password), (e: ProtocolError) => e.status === 401);
});
