import { test } from 'node:test';
import assert from 'node:assert/strict';
import { alerts } from '../lib/panel.js';
import { caja1, caja2, centro, database, norte, send } from './helpers.js';

const session = (id: string, terminal: string, state: 'OPEN' | 'CLOSED') =>
  ({ entity: 'cash_session', id, payload: { id, terminal_id: terminal, opened_by: 'u', opened_utc: '2026-10-10T13:00:00Z', opening_cents: 50000, state } });

test('a register with its till open that stopped connecting is flagged; closed tills are not', async () => {
  const { q } = await database();
  await send(q, caja1, centro, [session('s1', caja1, 'OPEN')]);
  await send(q, caja2, norte, [session('s2', caja2, 'CLOSED')]);
  await q(`UPDATE terminal SET last_seen_at = now() - interval '40 minutes'`, []);

  const list = await alerts(q, null);
  assert.deepEqual(list.map(a => a.terminal), ['CASA-MIGA-CAJA-01'], 'Norte has its till closed: normal at night.');
  assert.deepEqual(await alerts(q, norte), []);

  await q(`UPDATE terminal SET last_seen_at = now() - interval '2 minutes' WHERE id = $1`, [caja1]);
  assert.deepEqual(await alerts(q, centro), [], 'Connected a moment ago: nothing to say.');
});
