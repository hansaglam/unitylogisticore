import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { assertLocalEnvironment, LOCAL_PROJECT } from '../functions/lib/localEnvironment.js';

// Validate before importing any Firebase SDK or making any request.
assertLocalEnvironment(process.env);
const { initializeTestEnvironment, assertFails } = await import('@firebase/rules-unit-testing');
const { doc, collection, getDoc, getDocs, setDoc, updateDoc, deleteDoc } = await import('firebase/firestore');

test('default-deny rules reject unauthenticated and authenticated document/list access', async () => {
  const env = await initializeTestEnvironment({
    projectId: LOCAL_PROJECT,
    firestore: { host: '127.0.0.1', port: 8080, rules: await readFile(new URL('../firestore.rules', import.meta.url), 'utf8') },
  });
  try {
    const paths = ['arbitrary/synthetic', 'v2Players/synthetic-user', 'v2Companies/synthetic-company/state/finance'];
    for (const context of [env.unauthenticatedContext(), env.authenticatedContext('synthetic-user')]) {
      const db = context.firestore();
      for (const path of paths) {
        const ref = doc(db, path);
        await assertFails(getDoc(ref));
        await assertFails(setDoc(ref, { synthetic: true }));
        await assertFails(updateDoc(ref, { synthetic: false }));
        await assertFails(deleteDoc(ref));
      }
      await assertFails(getDocs(collection(db, 'v2Companies')));
    }
  } finally { await env.cleanup(); }
});

async function post(url, data, token) {
  const response = await fetch(url, {
    method: 'POST', redirect: 'error', signal: AbortSignal.timeout(15000),
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: JSON.stringify(data),
  });
  return { status: response.status, body: await response.json() };
}

test('Auth + Functions: synthetic identity accepted, missing/invalid identity and payload UID rejected', async () => {
  const url = `http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/localHealth`;
  const anonymous = await post(url, { data: { protocolVersion: 1 } });
  assert.equal(anonymous.status, 401);
  assert.equal(anonymous.body.error.status, 'UNAUTHENTICATED');
  const invalid = await post(url, { data: { protocolVersion: 1 } }, 'synthetic-invalid-token');
  assert.equal(invalid.status, 401);

  const signup = await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=synthetic-key', { returnSecureToken: true });
  assert.equal(signup.status, 200);
  const token = signup.body.idToken;
  assert.equal(typeof token, 'string');
  try {
    const response = await post(url, { data: { protocolVersion: 1 } }, token);
    assert.equal(response.status, 200);
    assert.deepEqual(response.body.result.result, { status: 'ok' });
    assert.equal(response.body.result.protocolVersion, 1);
    assert.ok(Number.isFinite(Date.parse(response.body.result.serverTime)));
    assert.deepEqual(Object.keys(response.body.result).sort(), ['protocolVersion', 'result', 'serverTime']);
    for (const payload of [{ protocolVersion: 2 }, { protocolVersion: 1, uid: 'synthetic-other' }, null]) {
      const rejected = await post(url, { data: payload }, token);
      assert.equal(rejected.status, 400);
      assert.equal(rejected.body.error.details.code, 'INVALID_ARGUMENT');
    }
  } finally {
    const removed = await post('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:delete?key=synthetic-key', { idToken: token });
    assert.equal(removed.status, 200);
  }
});
