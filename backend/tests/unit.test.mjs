import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { assertLocalEnvironment, assertSafeOverrides, LOCAL_PROJECT, LOCAL_ENDPOINTS } from '../functions/lib/localEnvironment.js';
import { requireUid, validateIdentityRequest } from '../functions/lib/identity.js';

test('identity trust boundary rejects malformed auth and all non-allowlisted request fields', () => {
  for (const auth of [undefined, null, {}, { uid: '' }, { uid: ' ' }, { uid: 7 }, { uid: 'a/b' }, { uid: '..' }, { uid: 'x'.repeat(129) }]) {
    assert.throws(() => requireUid(auth), error => error.code === 'unauthenticated' && error.details.messageKey === 'errors.unauthenticated');
  }
  assert.equal(requireUid({ uid: 'synthetic-player' }), 'synthetic-player');
  validateIdentityRequest({ protocolVersion: 1 });
  for (const field of ['uid', 'ownerUid', 'userId', 'authUid', 'companyOwnerUid', 'role', 'isAdmin', 'cash', 'unexpected']) {
    assert.throws(() => validateIdentityRequest({ protocolVersion: 1, [field]: 'synthetic' }), { code: 'invalid-argument' });
  }
  for (const data of [null, [], {}, { protocolVersion: 2 }, { protocolVersion: '1' }]) {
    assert.throws(() => validateIdentityRequest(data), { code: 'invalid-argument' });
  }
});

const valid = { GCLOUD_PROJECT: LOCAL_PROJECT, ...LOCAL_ENDPOINTS, FUNCTIONS_EMULATOR: 'true' };

test('only the exact demo project and loopback endpoints are accepted', () => {
  assert.doesNotThrow(() => assertLocalEnvironment(valid, true));
  for (const key of ['GCLOUD_PROJECT', 'GOOGLE_CLOUD_PROJECT', 'GCP_PROJECT', 'FIREBASE_PROJECT_ID']) {
    for (const value of ['synthetic-real-looking-project', 'demo-other', '']) {
      assert.throws(() => assertLocalEnvironment({ ...valid, [key]: value }, true), /Local isolation/);
    }
  }
});

test('missing or remote endpoints fail closed before SDK use', () => {
  for (const key of Object.keys(LOCAL_ENDPOINTS)) {
    const missing = { ...valid }; delete missing[key];
    assert.throws(() => assertLocalEnvironment(missing), /required/);
    for (const value of ['example.invalid:8080', '0.0.0.0:8080', '127.0.0.1:1', '']) {
      assert.throws(() => assertLocalEnvironment({ ...valid, [key]: value }), /Local isolation/);
    }
  }
  assert.throws(() => assertLocalEnvironment({ ...valid, FUNCTIONS_EMULATOR: 'false' }, true));
  for (const key of ['FIRESTORE_EMULATOR_HOST', 'FIREBASE_AUTH_EMULATOR_HOST']) {
    const missing = { ...valid }; delete missing[key];
    assert.throws(() => assertLocalEnvironment(missing, true), /required/);
  }
  assert.throws(() => assertLocalEnvironment({}));
});

test('credentials and conflicting Firebase configuration are rejected without reading files', () => {
  for (const key of ['GOOGLE_APPLICATION_CREDENTIALS', 'FIREBASE_TOKEN', 'GOOGLE_OAUTH_ACCESS_TOKEN']) {
    assert.throws(() => assertSafeOverrides({ ...valid, [key]: 'synthetic-do-not-use' }), /must be unset/);
  }
  for (const config of ['a-file-path.json', '{}', '{"projectId":"synthetic-other"}', 'null']) {
    assert.throws(() => assertSafeOverrides({ ...valid, FIREBASE_CONFIG: config }), /Local isolation/);
  }
  assert.doesNotThrow(() => assertSafeOverrides({ ...valid, FIREBASE_CONFIG: JSON.stringify({ projectId: LOCAL_PROJECT }) }));
});

test('actual Functions entry point cannot load outside the emulator or with a conflicting project', () => {
  const entry = fileURLToPath(new URL('../functions/lib/index.js', import.meta.url));
  for (const env of [{}, { ...valid, GCLOUD_PROJECT: 'synthetic-other' }, { ...valid, FIRESTORE_EMULATOR_HOST: '' }]) {
    const result = spawnSync(process.execPath, [entry], { env, encoding: 'utf8', timeout: 15000 });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Local isolation/);
  }
});

import { canonicalJson, parseCommandRequest, requestFingerprint, CommandError } from '../functions/lib/commands.js';

test('command envelope enforces protocol, bounded JSON and key allowlist', () => {
  const validRequest = { protocolVersion: 1, idempotencyKey: 'synthetic-key_1', payload: {} };
  assert.deepEqual(parseCommandRequest(validRequest), { idempotencyKey: 'synthetic-key_1', payload: {} });
  for (const key of ['', ' ', 'a/b', '.', 'é', '__reserved__', 'x'.repeat(129)]) {
    assert.throws(() => parseCommandRequest({ ...validRequest, idempotencyKey: key }), e => e.details.code === 'INVALID_ARGUMENT');
  }
  for (const data of [null, [], { ...validRequest, uid: 'spoof' }, { ...validRequest, payload: [] }, { ...validRequest, payload: { huge: 'x'.repeat(8193) } }]) {
    assert.throws(() => parseCommandRequest(data), e => e.details.code === 'INVALID_ARGUMENT');
  }
  for (const protocolVersion of [2, '1', null]) assert.throws(() => parseCommandRequest({ ...validRequest, protocolVersion }), e => e.details.code === 'UNSUPPORTED_PROTOCOL');
});

test('fingerprints are deterministic, scoped and independent of object property order', () => {
  assert.equal(canonicalJson({ b: 2, a: { z: -0, c: [1, 2] } }), canonicalJson({ a: { c: [1, 2], z: 0 }, b: 2 }));
  const hash = requestFingerprint('synthetic', null, { b: 2, a: 1 });
  assert.equal(hash, requestFingerprint('synthetic', null, { a: 1, b: 2 }));
  for (const args of [['other', null, { a: 1, b: 2 }], ['synthetic', 'company', { a: 1, b: 2 }], ['synthetic', null, { a: 2, b: 2 }]]) {
    assert.notEqual(hash, requestFingerprint(...args));
  }
  const cycle = {}; cycle.self = cycle;
  for (const value of [undefined, NaN, Infinity, new Date(), cycle, { x: undefined }, JSON.parse('{"__proto__":1}'), { get value() { throw Error('getter executed'); } }]) {
    assert.throws(() => canonicalJson(value), e => e.details?.code === 'INVALID_ARGUMENT');
  }
});

test('domain errors expose only stable localization codes', () => {
  for (const code of ['UNAUTHENTICATED','INVALID_ARGUMENT','UNSUPPORTED_PROTOCOL','IDEMPOTENCY_CONFLICT','PERMISSION_DENIED','FAILED_PRECONDITION','RETRY_LATER','INTERNAL']) {
    const error = new CommandError(code);
    assert.deepEqual(error.details, { code, messageKey: `errors.${code.toLowerCase()}` });
    assert.equal(error.message, code);
  }
});

import { normalizeCompanyName, validateCompanyPayload, companyIdForUid } from '../functions/lib/company.js';

test('company names normalize TR/EN/DE display text and reject unsafe or unbounded input', () => {
  assert.equal(normalizeCompanyName('  İstanbul\u00a0 Lojistik  '), 'İstanbul Lojistik');
  assert.equal(normalizeCompanyName('Mu\u0308ller Straße'), 'Müller Straße');
  assert.equal(normalizeCompanyName('English Logistics'), 'English Logistics');
  assert.equal(normalizeCompanyName('a'.repeat(64)).length, 64);
  for (const value of ['', '  ', 'a', 'a'.repeat(65), 'a'.repeat(513), 'A\nB', 'A\u202eB', 'A\u200bB', 'A\u034fB', 'A\u2028B', '\ud800X', null, 123, '---']) {
    assert.throws(() => normalizeCompanyName(value), e => e.details.code === 'INVALID_ARGUMENT');
  }
  for (const key of ['uid','ownerUid','companyId','balance','cash','money','xp','level','reputation','isAdmin','role','startingFunds']) {
    assert.throws(() => validateCompanyPayload({ name: 'Synthetic', [key]: 'spoof' }), e => e.details.code === 'INVALID_ARGUMENT');
  }
  assert.equal(companyIdForUid('synthetic-uid'), companyIdForUid('synthetic-uid'));
  assert.match(companyIdForUid('synthetic-uid'), /^co_[a-f0-9]{64}$/);
  assert.notEqual(companyIdForUid('synthetic-uid'), companyIdForUid('synthetic-other'));
});
