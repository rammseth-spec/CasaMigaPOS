import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PGlite } from '@electric-sql/pglite';
import { migrate, migrations } from '../lib/migrate.js';
import { session } from './helpers.js';

test('fresh database gets every migration once, in order', async () => {
  const db = new PGlite(); const s = session(db);
  assert.deepEqual(await migrate(s), migrations().map(m => m.version));
  assert.deepEqual(await migrate(s), [], 'Already up to date.');
  assert.equal(await s.scalar('SELECT max(version) FROM schema_version'), migrations().at(-1)!.version);
});

test('upgrading from version 1 keeps received history and builds the latest state from it', async () => {
  const db = new PGlite(); const s = session(db);
  await migrate(s, migrations().slice(0, 1));
  await db.exec(`INSERT INTO branch(id, name) VALUES ('11111111-1111-4111-8111-111111111111', 'Centro');
    INSERT INTO terminal(id, branch_id, name, key_sha256) VALUES ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', '11111111-1111-4111-8111-111111111111', 'Caja', repeat('a', 64));
    INSERT INTO sync_event(terminal_id, seq, branch_id, entity, entity_id, change, enqueued_at, payload) VALUES
      ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 1, '11111111-1111-4111-8111-111111111111', 'cash_session', 'c1', 'insert', '2026-10-06T13:00:00Z', '{"state":"OPEN"}'),
      ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 2, '11111111-1111-4111-8111-111111111111', 'cash_session', 'c1', 'update', '2026-10-06T22:00:00Z', '{"state":"CLOSED"}'),
      ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 3, '11111111-1111-4111-8111-111111111111', 'sale', 's1', 'insert', '2026-10-06T14:00:00Z', '{"sale":{}}');`);
  assert.deepEqual(await migrate(s), [2]);
  assert.equal(await s.scalar(`SELECT payload->>'state' FROM entity_state WHERE entity = 'cash_session'`), 'CLOSED');
  assert.equal(await s.scalar('SELECT count(*)::int FROM entity_state'), 2);
  assert.equal(await s.scalar('SELECT count(*)::int FROM sync_event'), 3, 'History is never rewritten.');
  await assert.rejects(db.exec(`INSERT INTO sync_event(terminal_id, seq, branch_id, entity, entity_id, change, enqueued_at, payload)
    VALUES ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 4, '11111111-1111-4111-8111-111111111111', 'secret', 'x', 'insert', now(), '{}')`));
});

test('a failing migration rolls back completely', async () => {
  const db = new PGlite(); const s = session(db);
  const broken = [...migrations().slice(0, 1), { version: 2, name: '002_broken.sql', sql: 'CREATE TABLE half(x int); SELECT 1/0;' }];
  await assert.rejects(migrate(s, broken));
  assert.equal(await s.scalar('SELECT max(version) FROM schema_version'), 1);
  assert.equal(await s.scalar(`SELECT to_regclass('public.half') IS NULL`), true);
});
