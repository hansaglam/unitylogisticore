import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { assertLocalEnvironment, assertSafeOverrides, LOCAL_PROJECT, LOCAL_ENDPOINTS } from '../functions/lib/localEnvironment.js';

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
