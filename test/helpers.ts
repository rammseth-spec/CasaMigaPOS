// Base central en memoria (PGlite) con las mismas migraciones que Neon, y lotes como los manda la caja.
import { PGlite } from '@electric-sql/pglite';
import { migrate, type Session } from '../lib/migrate.js';
import { storeBatch } from '../lib/store.js';
import { identityHash } from '../lib/auth.js';
import type { Query } from '../lib/panel.js';

export const centro = '11111111-1111-4111-8111-111111111111';
export const norte = '22222222-2222-4222-8222-222222222222';
export const caja1 = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
export const caja2 = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

export function session(db: PGlite): Session {
  return { exec: async sql => { await db.exec(sql); }, scalar: async sql => { const row = (await db.query(sql)).rows[0] as Record<string, unknown> | undefined; return row ? Object.values(row)[0] : null; } };
}

export async function database(): Promise<{ db: PGlite; q: Query }> {
  const db = new PGlite();
  await migrate(session(db));
  await db.query(`INSERT INTO branch(id, name) VALUES ($1, 'Centro'), ($2, 'Norte')`, [centro, norte]);
  await db.query(`INSERT INTO terminal(id, branch_id, name, key_sha256, last_seen_at) VALUES
    ($1, $2, 'CASA-MIGA-CAJA-01', repeat('a', 64), '2026-10-07T18:00:00Z'), ($3, $4, 'CASA-MIGA-CAJA-02', repeat('b', 64), NULL)`, [caja1, centro, caja2, norte]);
  return { db, q: async (text, params) => (await db.query(text, params)).rows as Record<string, unknown>[] };
}

const seqs = new Map<string, number>();
/** Envía un lote como la caja (seq consecutivo por terminal). */
export async function send(q: Query, terminal: string, branch: string, entries: { entity: string; id: string; payload: unknown; change?: 'insert' | 'update' | 'delete'; at?: string }[]) {
  return storeBatch(q, { id: terminal, branchId: branch }, {
    branchId: branch,
    entries: entries.map(e => {
      const seq = (seqs.get(terminal) ?? 0) + 1; seqs.set(terminal, seq);
      return { seq, entity: e.entity as never, entityId: e.id, change: e.change ?? 'insert', enqueuedAt: e.at ?? new Date(Date.UTC(2026, 9, 7, 12) + seq * 1000).toISOString(), payload: e.payload };
    }),
  });
}

export function sale(id: string, session: string, at: string, method: string, items: [string, number, number][], sequence = 1, user = 'u-admin') {
  const total = items.reduce((a, [, q, p]) => a + q * p, 0);
  return {
    sale: { id, cash_session_id: session, confirmed_utc: at, subtotal_cents: total, total_cents: total, sequence, user_id: user },
    items: items.map(([name, quantity, price], i) => ({ sale_id: id, position: i + 1, product_id: `p-${name}`, product_name: name, unit_price_cents: price, quantity, line_total_cents: quantity * price })),
    payment: { sale_id: id, method, applied_cents: total, received_cents: total, change_cents: 0, reference: null },
  };
}

export const PERMISSIONS: Record<string, string[]> = {
  Administrator: ['sales.view', 'cash.view', 'catalog.view', 'consignment.view', 'consignment.manage', 'inventory.view', 'recipe.view', 'production.view', 'users.manage',
    'products.create', 'products.edit', 'products.deactivate', 'categories.create', 'categories.edit', 'categories.deactivate'],
  Manager: ['sales.view', 'cash.view', 'catalog.view', 'consignment.view', 'consignment.manage', 'inventory.view', 'recipe.view', 'production.view',
    'products.create', 'products.edit', 'products.deactivate', 'categories.create', 'categories.edit', 'categories.deactivate'],
  Cashier: ['cash.view', 'catalog.view', 'pos.use'],
};

export async function permissions(q: Query, terminal: string, branch: string) {
  const rows = Object.entries(PERMISSIONS).flatMap(([role, list]) => list.map(p => ({ role_name: role, permission_name: p })));
  await send(q, terminal, branch, [{ entity: 'role_permission', id: 'all', payload: { rows } }]);
}

export async function user(q: Query, terminal: string, branch: string, id: string, username: string, role: string, password: string, extra: Record<string, unknown> = {}) {
  await send(q, terminal, branch, [{ entity: 'app_user', id, payload: {
    id, username: username.toUpperCase(), display_name: `${username} (${role})`, password_hash: await identityHash(password, 10_000), role_name: role,
    active: 1, must_change_password: 0, created_utc: '2026-10-01T12:00:00Z', ...extra } }]);
}

export const sessionA = 'c0000000-0000-4000-8000-00000000000a';
export const sessionB = 'c0000000-0000-4000-8000-00000000000b';

/** Dos sucursales: turno cerrado con faltante, turno abierto con gasto y entrada, devoluciones y una venta en Norte. */
export async function seedSales(q: Query) {
  // Centro, caja 1: turno A cerrado el 06/10 con $20 de faltante; turno B abierto el 07/10 con gasto y entrada.
  await send(q, caja1, centro, [
    { entity: 'cash_session', id: sessionA, payload: { id: sessionA, opened_by: 'u-admin', opened_utc: '2026-10-06T13:00:00+00:00', opening_cents: 50000, state: 'OPEN' } },
    { entity: 'sale', id: 's1', payload: sale('s1', sessionA, '2026-10-06T15:00:00+00:00', 'CASH', [['Concha', 2, 1900]], 1) },
    { entity: 'sale', id: 's2', payload: sale('s2', sessionA, '2026-10-06T16:00:00+00:00', 'CARD', [['Dona', 2, 1300]], 2) },
    // 21:30 en México sigue siendo el 06/10 aunque en UTC ya es el 07/10.
    { entity: 'sale', id: 's3', payload: sale('s3', sessionA, '2026-10-07T03:30:00+00:00', 'CASH', [['Concha', 1, 1900]], 3) },
    { entity: 'sale_refund', id: 'r1', payload: { refund: { id: 'r1', kind: 'RETURN', reason: 'Pan aplastado', user_id: 'u-admin', sale_id: 's1', cash_session_id: sessionA, refunded_utc: '2026-10-06T17:00:00+00:00', total_cents: 1900 }, items: [{ position: 1, quantity: 1, line_total_cents: 1900 }] } },
  ]);
  // El cierre llega en otro lote como actualización del mismo turno.
  await send(q, caja1, centro, [{ entity: 'cash_session', id: sessionA, change: 'update', payload: { id: sessionA, opened_by: 'u-admin', opened_utc: '2026-10-06T13:00:00+00:00', opening_cents: 50000, state: 'CLOSED',
    closed_utc: '2026-10-07T04:00:00+00:00', expected_cents: 53700, counted_cents: 51700, difference_cents: -2000 } }]);
  await send(q, caja1, centro, [
    { entity: 'cash_session', id: sessionB, payload: { id: sessionB, opened_by: 'u-admin', opened_utc: '2026-10-07T14:00:00+00:00', opening_cents: 20000, state: 'OPEN' } },
    { entity: 'sale', id: 's4', payload: sale('s4', sessionB, '2026-10-07T15:00:00+00:00', 'TRANSFER', [['Bolillo', 10, 300]], 4) },
    { entity: 'sale', id: 's5', payload: sale('s5', sessionB, '2026-10-07T16:00:00+00:00', 'CASH', [['Concha', 3, 1900]], 5) },
    { entity: 'cash_movement', id: 'm1', payload: { id: 'm1', cash_session_id: sessionB, kind: 'EXPENSE', category: 'GAS', amount_cents: 4550, note: 'Tanque', user_id: 'u-admin', occurred_utc: '2026-10-07T17:00:00+00:00' } },
    { entity: 'cash_movement', id: 'm2', payload: { id: 'm2', cash_session_id: sessionB, kind: 'DEPOSIT', category: null, amount_cents: 10000, note: '', user_id: 'u-admin', occurred_utc: '2026-10-07T17:05:00+00:00' } },
  ]);
  // Norte, caja 2.
  await send(q, caja2, norte, [{ entity: 'sale', id: 'n1', payload: sale('n1', 'c0000000-0000-4000-8000-0000000000ff', '2026-10-07T15:00:00+00:00', 'CASH', [['Rosca', 1, 25000]], 1) }]);
}

