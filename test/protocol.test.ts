import { test } from 'node:test';
import assert from 'node:assert/strict';
import { bearer, MAX_BATCH, newTerminalKey, parseBatch, parseRegistration, ProtocolError, reconcile, secretMatches, sha256Hex } from '../lib/protocol.ts';

const branchId = '7f9c2b1e-3d4a-4c5b-8e6f-112233445566';
const entry = (seq: number, extra: Record<string, unknown> = {}) =>
  ({ seq, entity: 'sale', entityId: 'a1b2', change: 'insert', enqueuedAt: '2026-10-06T12:00:00.000Z', payload: { total: 100 }, ...extra });

test('valid batch is parsed and normalized', () => {
  const batch = parseBatch({ branchId: branchId.toUpperCase(), entries: [entry(1), entry(2, { entity: 'audit_entry' })] });
  assert.equal(batch.branchId, branchId);
  assert.deepEqual(batch.entries.map(e => e.seq), [1, 2]);
});

test('batch rejects empty, oversized, duplicated or malformed entries', () => {
  assert.throws(() => parseBatch({ branchId, entries: [] }), ProtocolError);
  assert.throws(() => parseBatch({ branchId, entries: Array.from({ length: MAX_BATCH + 1 }, (_, i) => entry(i + 1)) }), ProtocolError);
  assert.throws(() => parseBatch({ branchId, entries: [entry(1), entry(1)] }), /repetido/);
  assert.throws(() => parseBatch({ branchId, entries: [entry(0)] }), ProtocolError);
  assert.throws(() => parseBatch({ branchId, entries: [entry(1, { entity: 'secret_table' })] }), /entity/);
  assert.throws(() => parseBatch({ branchId, entries: [entry(1, { change: 'truncate' })] }), /change/);
  assert.throws(() => parseBatch({ branchId, entries: [entry(1, { payload: null })] }), /payload/);
  assert.throws(() => parseBatch({ branchId, entries: [entry(1, { enqueuedAt: 'ayer' })] }), /enqueuedAt/);
  assert.throws(() => parseBatch({ branchId: 'no-uuid', entries: [entry(1)] }), /UUID/);
});

test('registration requires all identities', () => {
  const ok = parseRegistration({ enrollmentSecret: 's', branch: { id: branchId, name: ' Centro ' }, terminal: { id: branchId, name: 'Caja 1' } });
  assert.equal(ok.branch.name, 'Centro');
  assert.throws(() => parseRegistration({ enrollmentSecret: 's', branch: { id: branchId, name: '' }, terminal: { id: branchId, name: 'Caja' } }), ProtocolError);
  assert.throws(() => parseRegistration(null), ProtocolError);
});

test('enrollment secret comparison rejects weak or missing configuration', () => {
  const secret = 'x'.repeat(32);
  assert.equal(secretMatches(secret, secret), true);
  assert.equal(secretMatches('otro', secret), false);
  assert.equal(secretMatches('corto', 'corto'), false, 'A short configured secret is never accepted.');
  assert.equal(secretMatches('x', undefined), false);
});

test('terminal keys are 256-bit base64url and parsed only from Bearer headers', () => {
  const key = newTerminalKey();
  assert.equal(key.length, 43);
  assert.equal(bearer(`Bearer ${key}`), key);
  assert.throws(() => bearer(null), (e: ProtocolError) => e.status === 401);
  assert.throws(() => bearer(`Basic ${key}`), ProtocolError);
  assert.equal(sha256Hex('abc'), 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
});

test('reconcile accepts new and identical resends, flags reused sequences', () => {
  const entries = parseBatch({ branchId, entries: [entry(1), entry(2), entry(3)] }).entries;
  const existing = new Map([
    [1, { entity: 'sale', entityId: 'a1b2', change: 'insert' }],   // identical resend
    [2, { entity: 'audit_entry', entityId: 'zz', change: 'insert' }], // restored old backup reused seq 2
  ]);
  assert.deepEqual(reconcile(entries, existing), { accepted: [1, 3], conflicts: [2] });
});
