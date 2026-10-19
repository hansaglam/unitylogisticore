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
    const paths = ['arbitrary/synthetic', 'v2Players/synthetic-user', 'v2Players/synthetic-user/commands/synthetic-key', 'v2Companies/synthetic-company', 'v2Companies/synthetic-company/state/finance', 'v2Companies/synthetic-company/state/progression', 'v2Companies/synthetic-company/ledger/synthetic', 'v2Companies/synthetic-company/claims/synthetic', 'v2Companies/synthetic-company/entitlements/synthetic', 'v2WorldConfig/synthetic', 'v2LogisticsCatalog/synthetic', 'v2Companies/synthetic-company/assets/synthetic', 'v2Companies/synthetic-company/provisioning/starterFleet', 'v2Companies/synthetic-company/assignments/synthetic', 'v2Companies/synthetic-company/warehouses/synthetic'];
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
      await assertFails(getDocs(collection(db, 'v2Players')));
      await assertFails(getDocs(collection(db, 'v2Players/synthetic-user/commands')));
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

// The guard above runs before these dynamic SDK imports. No ADC discovery.
const { Timestamp } = await import('firebase-admin/firestore');
const { initializeApp: initializeClient, deleteApp: deleteClient } = await import('firebase/app');
const { getAuth, connectAuthEmulator, signInAnonymously, linkWithCredential,
  EmailAuthProvider, signInWithEmailAndPassword, deleteUser } = await import('firebase/auth');
const { createLocalFirestore, requireCompanyOwner } = await import('../functions/lib/identity.js');

test('identity transaction, real emulator linking/collision and canonical ownership', { timeout: 120000 }, async () => {
  const previousRuntime = process.env.FUNCTIONS_EMULATOR;
  process.env.FUNCTIONS_EMULATOR = 'true';
  const db = createLocalFirestore();
  assert.equal(process.env.FIRESTORE_ENABLE_TRACING, 'off');
  const apps = [];
  const users = new Map();
  const refs = new Map();
  const track = ref => { refs.set(ref.path, ref); return ref; };
  const makeAuth = () => {
    const app = initializeClient({ projectId: LOCAL_PROJECT, apiKey: 'synthetic-key' }, `synthetic-${Date.now()}-${apps.length}`);
    apps.push(app);
    const auth = getAuth(app);
    connectAuthEmulator(auth, 'http://127.0.0.1:9099', { disableWarnings: true });
    return auth;
  };
  const url = `http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/ensurePlayerIdentity`;
  const call = (token, data = { protocolVersion: 1 }) => post(url, { data }, token);
  // Ownership is an internal primitive, not a public callable. Match guarded local runtime.
  try {
    assert.equal((await call()).status, 401);
    assert.equal((await call('synthetic-invalid-token')).status, 401);
    const auth = makeAuth();
    const guest = (await signInAnonymously(auth)).user;
    users.set(guest.uid, guest);
    const uid = guest.uid;
    const ref = track(db.doc(`v2Players/${uid}`));
    const token = await guest.getIdToken();
    for (const field of ['uid', 'ownerUid', 'userId', 'authUid', 'companyOwnerUid', 'role', 'isAdmin', 'cash', 'unexpected']) {
      const rejected = await call(token, { protocolVersion: 1, [field]: 'synthetic-other' });
      assert.equal(rejected.status, 400);
      assert.equal(rejected.body.error.details.code, 'INVALID_ARGUMENT');
    }
    for (const payload of [null, [], {}, { protocolVersion: 2 }]) assert.equal((await call(token, payload)).status, 400);
    assert.equal((await ref.get()).exists, false);
    const started = Date.now();
    const responses = await Promise.all(Array.from({ length: 6 }, () => call(token)));
    for (const response of responses) {
      assert.equal(response.status, 200);
      assert.deepEqual(response.body.result.result, { playerId: uid, schemaVersion: 1, identityStatus: 'active' });
      assert.ok(Number.isFinite(Date.parse(response.body.result.serverTime)));
    }
    const initial = (await ref.get()).data();
    assert.deepEqual(Object.keys(initial).sort(), ['authUid', 'createdAt', 'identityStatus', 'schemaVersion']);
    assert.ok(initial.createdAt instanceof Timestamp);
    assert.ok(initial.createdAt.toMillis() >= started - 1000 && initial.createdAt.toMillis() <= Date.now() + 1000);
    assert.equal((await db.collection('v2Players').where('authUid', '==', uid).get()).size, 1);
    await ref.update({ preservedSyntheticMarker: 'keep' });
    const preserved = (await ref.get()).data();
    assert.equal((await call(token)).status, 200);
    assert.deepEqual((await ref.get()).data(), preserved);
    assert.equal((await db.collection('v2Companies').get()).size, 0);
    assert.deepEqual((await db.listCollections()).map(c => c.id).sort(), ['v2Players']);

    // Supported SDK linking, not a copied Firestore UID.
    const email = `synthetic-${uid.toLowerCase()}@example.invalid`;
    const credential = EmailAuthProvider.credential(email, 'Synthetic-only-password-42!');
    const linked = (await linkWithCredential(guest, credential)).user;
    assert.equal(linked.uid, uid);
    assert.equal(linked.isAnonymous, false);
    assert.equal((await call(await linked.getIdToken(true))).status, 200);
    assert.deepEqual((await ref.get()).data(), preserved);
    const secondDevice = makeAuth();
    const returning = (await signInWithEmailAndPassword(secondDevice, email, 'Synthetic-only-password-42!')).user;
    assert.equal(returning.uid, uid);
    assert.equal((await call(await returning.getIdToken())).body.result.result.playerId, uid);

    const otherAuth = makeAuth();
    const other = (await signInAnonymously(otherAuth)).user;
    users.set(other.uid, other);
    const otherRef = track(db.doc(`v2Players/${other.uid}`));
    assert.equal((await call(await other.getIdToken())).status, 200);
    const otherInitial = (await otherRef.get()).data();
    await assert.rejects(linkWithCredential(other, credential), error =>
      ['auth/email-already-in-use', 'auth/credential-already-in-use'].includes(error.code));
    assert.equal(otherAuth.currentUser.uid, other.uid);
    assert.notEqual(other.uid, uid);
    assert.deepEqual((await ref.get()).data(), preserved);
    assert.deepEqual((await otherRef.get()).data(), otherInitial);

    for (const mutation of [{ authUid: uid }, { schemaVersion: 2 }, { createdAt: 'device-time' }, { identityStatus: 'suspicious' }]) {
      const malformed = { ...otherInitial, ...mutation };
      await otherRef.set(malformed);
      const result = await call(await other.getIdToken());
      assert.equal(result.status, 400);
      assert.equal(result.body.error.status, 'FAILED_PRECONDITION');
      assert.equal(result.body.error.details.messageKey, 'errors.identity_conflict');
      assert.deepEqual((await otherRef.get()).data(), malformed);
    }
    const company = track(db.doc(`v2Companies/synthetic-${uid}`));
    await company.set({ ownerUid: uid });
    await requireCompanyOwner(db, { uid }, company.id);
    await db.runTransaction(tx => requireCompanyOwner(db, { uid }, company.id, tx));
    await assert.rejects(requireCompanyOwner(db, undefined, company.id), { code: 'unauthenticated' });
    await assert.rejects(requireCompanyOwner(db, { uid: other.uid, ownerUid: uid }, company.id), { code: 'permission-denied' });
    await assert.rejects(requireCompanyOwner(db, { uid }, `synthetic-missing-${uid}`), { code: 'not-found' });
    for (const id of ['', ' ', 'a/b', '.', '..', '__reserved__', null, 123, 'x'.repeat(1501)]) {
      await assert.rejects(requireCompanyOwner(db, { uid }, id), { code: 'invalid-argument' });
    }
    for (const owner of [null, '', 123, 'a/b']) {
      await company.set({ ownerUid: owner });
      await assert.rejects(requireCompanyOwner(db, { uid }, company.id), { code: 'failed-precondition' });
    }
    // Legacy fixtures are independent: bootstrap must neither read nor transform them.
    const legacy = track(db.doc(`users/${uid}`));
    const legacyState = track(db.doc(`users/${uid}/serverState/current`));
    await legacy.set({ money: 17, level: 2, ownerUid: uid });
    await legacyState.set({ cash: 29, companyLevel: 3 });
    assert.equal((await call(await linked.getIdToken())).status, 200);
    assert.deepEqual((await legacy.get()).data(), { money: 17, level: 2, ownerUid: uid });
    assert.deepEqual((await legacyState.get()).data(), { cash: 29, companyLevel: 3 });
  } finally {
    if (previousRuntime === undefined) delete process.env.FUNCTIONS_EMULATOR;
    else process.env.FUNCTIONS_EMULATOR = previousRuntime;
    const cleanup = await Promise.allSettled([
      ...[...refs.values()].map(ref => ref.delete()),
      ...[...users.values()].map(user => deleteUser(user)),
    ]);
    await Promise.all(apps.map(app => deleteClient(app)));
    await db.terminate();
    for (const result of cleanup) assert.equal(result.status, 'fulfilled', 'synthetic cleanup must succeed');
  }
});

const { executeCommand, CommandError } = await import('../functions/lib/commands.js');

test('durable commands: atomic effect, replay, conflicts, concurrency and rollback', { timeout: 120000 }, async () => {
  const previousRuntime = process.env.FUNCTIONS_EMULATOR;
  process.env.FUNCTIONS_EMULATOR = 'true';
  const db = createLocalFirestore();
  const suffix = `${Date.now()}-${process.pid}`;
  const uid = `synthetic-command-${suffix}`;
  const secondUid = `${uid}-second`;
  const refs = new Map();
  const tracked = path => { const ref = db.doc(path); refs.set(path, ref); return ref; };
  const state = player => tracked(`backend03Synthetic/${player}`);
  const receipt = (player, key) => tracked(`v2Players/${player}/commands/${key}`);
  const request = (key, payload = { label: 'proof' }) => ({ protocolVersion: 1, idempotencyKey: key, payload });
  const definition = {
    name: 'syntheticProof',
    validate(payload) {
      if (Object.keys(payload).join(',') !== 'label' || typeof payload.label !== 'string' || !['proof', 'other'].includes(payload.label.trim())) throw new CommandError('INVALID_ARGUMENT');
      return { label: payload.label.trim() };
    },
    async apply(tx, player, payload) {
      const ref = state(player);
      const snapshot = await tx.get(ref);
      const count = (snapshot.data()?.count ?? 0) + 1;
      tx.set(ref, { count, label: payload.label });
      return { count, status: 'recorded' };
    },
  };
  const run = (player, key, payload, command = definition) => {
    receipt(player, key);
    return executeCommand(db, { uid: player }, request(key, payload), command);
  };
  const rejected = code => error => error.details?.code === code;
  try {
    const legacy = tracked(`users/${uid}/serverState/current`);
    await legacy.set({ money: 13, cash: 27, level: 2, companyLevel: 4 });
    await assert.rejects(executeCommand(db, undefined, request('no-auth'), definition), rejected('UNAUTHENTICATED'));
    await assert.rejects(executeCommand(db, { uid: '' }, request('bad-auth'), definition), rejected('UNAUTHENTICATED'));
    await assert.rejects(executeCommand(db, { uid }, { ...request('spoof'), uid: secondUid }, definition), rejected('INVALID_ARGUMENT'));
    await assert.rejects(run(uid, 'payload-owner', { label: 'proof', ownerUid: secondUid }), rejected('INVALID_ARGUMENT'));
    assert.equal((await state(uid).get()).exists, false);
    const outputs = await Promise.all(Array.from({ length: 8 }, () => run(uid, 'concurrent')));
    for (const output of outputs) assert.deepEqual(output, outputs[0]);
    assert.equal((await state(uid).get()).data().count, 1);
    const stored = (await receipt(uid, 'concurrent').get()).data();
    assert.ok(stored.committedAt instanceof Timestamp);
    assert.equal(outputs[0].serverTime, stored.committedAt.toDate().toISOString());
    const businessBefore = (await state(uid).get()).data();
    const stateUpdate = (await state(uid).get()).updateTime;
    const receiptUpdate = (await receipt(uid, 'concurrent').get()).updateTime;
    assert.deepEqual(await run(uid, 'concurrent', { label: ' proof ' }), outputs[0]);
    assert.deepEqual((await receipt(uid, 'concurrent').get()).data(), stored);
    assert.deepEqual((await receipt(uid, 'concurrent').get()).updateTime, receiptUpdate);
    assert.deepEqual((await state(uid).get()).data(), businessBefore);
    assert.deepEqual((await state(uid).get()).updateTime, stateUpdate);
    await assert.rejects(run(uid, 'concurrent', { label: 'other' }), rejected('IDEMPOTENCY_CONFLICT'));
    await assert.rejects(run(uid, 'concurrent', undefined, { ...definition, name: 'differentCommand' }), rejected('IDEMPOTENCY_CONFLICT'));
    const independent = await run(secondUid, 'concurrent');
    assert.equal(independent.result.count, 1);
    assert.equal((await state(uid).get()).data().count, 1);

    // Commit succeeds; the caller deliberately discards the response and observes a transport failure.
    await assert.rejects((async () => { await run(uid, 'lost-response'); throw new Error('synthetic response lost'); })(), /synthetic response lost/);
    const lostReceipt = (await receipt(uid, 'lost-response').get()).data();
    const recovered = await run(uid, 'lost-response');
    assert.deepEqual(recovered.result, lostReceipt.result);
    assert.equal(recovered.serverTime, lostReceipt.committedAt.toDate().toISOString());
    assert.equal((await state(uid).get()).data().count, 2);

    const beforeFailure = (await state(uid).get()).data();
    for (const [key, code, apply] of [
      ['rollback', 'INTERNAL', async (tx, player, payload) => { await definition.apply(tx, player, payload); throw Error('private database path must not leak'); }],
      ['precondition', 'FAILED_PRECONDITION', async () => { throw new CommandError('FAILED_PRECONDITION'); }],
      ['denied', 'PERMISSION_DENIED', async () => { throw new CommandError('PERMISSION_DENIED'); }],
      ['oversized-result', 'INTERNAL', async (tx, player, payload) => { await definition.apply(tx, player, payload); return { value: 'x'.repeat(8193) }; }],
      ['read-after-write', 'INTERNAL', async (tx, player, payload) => { await definition.apply(tx, player, payload); await tx.get(state(player)); return {}; }],
      ['transient', 'RETRY_LATER', async () => { throw Object.assign(new Error('private unavailable'), { code: 14 }); }],
    ]) {
      await assert.rejects(run(uid, key, undefined, { ...definition, apply }), error => {
        assert.equal(error.message, code);
        return rejected(code)(error);
      });
      assert.equal((await receipt(uid, key).get()).exists, false);
      assert.deepEqual((await state(uid).get()).data(), beforeFailure);
    }
    // Failed keys have no successful receipt and can succeed after preconditions change.
    assert.equal((await run(uid, 'precondition')).result.count, 3);
    // Canonical scope resolved in the same transaction is part of the fingerprint.
    const scopeRef = tracked(`backend03Synthetic/scope-${suffix}`);
    await scopeRef.set({ companyId: 'synthetic-company-a' });
    const scoped = { ...definition, resolveCompany: async tx => (await tx.get(scopeRef)).data().companyId };
    await run(uid, 'scoped', undefined, scoped);
    assert.equal((await receipt(uid, 'scoped').get()).data().companyId, 'synthetic-company-a');
    await scopeRef.set({ companyId: 'synthetic-company-b' });
    await assert.rejects(run(uid, 'scoped', undefined, scoped), rejected('IDEMPOTENCY_CONFLICT'));
    const corrupt = receipt(uid, 'corrupt');
    await corrupt.set({ receiptSchemaVersion: 999 });
    await assert.rejects(run(uid, 'corrupt'), rejected('FAILED_PRECONDITION'));
    assert.deepEqual((await corrupt.get()).data(), { receiptSchemaVersion: 999 });
    const company = tracked(`v2Companies/synthetic-command-${suffix}`);
    await company.set({ ownerUid: uid });
    const owned = {
      ...definition,
      resolveCompany: async () => company.id,
      apply: async (tx, player, payload, companyId) => {
        await requireCompanyOwner(db, { uid: player }, companyId, tx);
        return definition.apply(tx, player, payload);
      },
    };
    await run(uid, 'owned', undefined, owned);
    const secondBefore = (await state(secondUid).get()).data();
    await assert.rejects(run(secondUid, 'owned', undefined, owned), rejected('PERMISSION_DENIED'));
    assert.equal((await receipt(secondUid, 'owned').get()).exists, false);
    assert.deepEqual((await state(secondUid).get()).data(), secondBefore);
    assert.deepEqual((await legacy.get()).data(), { money: 13, cash: 27, level: 2, companyLevel: 4 });
  } finally {
    const cleanup = await Promise.allSettled([...refs.values()].map(ref => ref.delete()));
    await db.terminate();
    if (previousRuntime === undefined) delete process.env.FUNCTIONS_EMULATOR;
    else process.env.FUNCTIONS_EMULATOR = previousRuntime;
    for (const result of cleanup) assert.equal(result.status, 'fulfilled');
  }
});

const { companyIdForUid } = await import('../functions/lib/company.js');

test('company callable bootstrap: atomic uniqueness, safe state read, migration denial and linking', { timeout: 180000 }, async () => {
  const previousRuntime = process.env.FUNCTIONS_EMULATOR;
  process.env.FUNCTIONS_EMULATOR = 'true';
  const db = createLocalFirestore();
  const apps = [];
  const users = [];
  const refs = new Map();
  const track = path => { const ref = db.doc(path); refs.set(path, ref); return ref; };
  const player = uid => track(`v2Players/${uid}`);
  const company = uid => track(`v2Companies/${companyIdForUid(uid)}`);
  const receipt = (uid,key) => track(`v2Players/${uid}/commands/${key}`);
  const invoke = async (user, name, data) => post(`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/${name}`, { data }, user ? await user.getIdToken() : undefined);
  const read = user => invoke(user, 'getMyCompanyState', { protocolVersion: 1 });
  const create = (user,key,name = 'Synthetic Logistics', extra = {}) => {
    receipt(user.uid,key);
    return invoke(user,'createCompany',{ protocolVersion:1,idempotencyKey:key,payload:{name,...extra} });
  };
  async function guest(identity = true) {
    const app = initializeClient({ projectId:LOCAL_PROJECT,apiKey:'synthetic-key' }, `company-${Date.now()}-${apps.length}`);
    apps.push(app);
    const auth = getAuth(app);
    connectAuthEmulator(auth,'http://127.0.0.1:9099',{disableWarnings:true});
    const user = (await signInAnonymously(auth)).user;
    users.push(user); player(user.uid); company(user.uid);
    if (identity) assert.equal((await invoke(user,'ensurePlayerIdentity',{protocolVersion:1})).status,200);
    return user;
  }
  const errorCode = (response,code) => { assert.notEqual(response.status,200); assert.equal(response.body.error.details.code,code); };
  try {
    assert.equal((await read()).status,401);
    assert.equal((await invoke(undefined,'createCompany',{protocolVersion:1,idempotencyKey:'x',payload:{name:'Synthetic'}})).status,401);
    const first = await guest();
    const identityBefore = (await player(first.uid).get()).data();
    const noCompanyBefore = (await player(first.uid).get()).updateTime;
    assert.deepEqual((await read(first)).body.result.result,{hasCompany:false});
    assert.deepEqual((await player(first.uid).get()).updateTime,noCompanyBefore);
    assert.equal((await company(first.uid).get()).exists,false);
    for (const field of ['uid','ownerUid','companyId','balance','cash','money','xp','level','reputation','isAdmin','role','startingFunds']) {
      errorCode(await create(first,`spoof-${field}`,'Synthetic',{[field]:'spoof'}),'INVALID_ARGUMENT');
      errorCode(await invoke(first,'createCompany',{protocolVersion:1,idempotencyKey:'spoof-top',payload:{name:'Synthetic'},[field]:'spoof'}),'INVALID_ARGUMENT');
      errorCode(await invoke(first,'getMyCompanyState',{protocolVersion:1,[field]:'spoof'}),'INVALID_ARGUMENT');
    }
    for (const [i,name] of ['', 'a'.repeat(65), 'a'.repeat(9000), 'A\u202eB', 'A\nB'].entries()) errorCode(await create(first,`invalid-${i}`,name),'INVALID_ARGUMENT');
    const results = await Promise.all(Array.from({length:8},()=>create(first,'concurrent','  İstanbul  Lojistik ')));
    for (const result of results) { assert.equal(result.status,200); assert.deepEqual(result.body,results[0].body); }
    const companyRef = company(first.uid);
    const companyData = (await companyRef.get()).data();
    assert.deepEqual(Object.keys(companyData).sort(),['bootstrapStatus','createdAt','name','ownerUid','schemaVersion']);
    assert.equal(companyData.ownerUid,first.uid);
    assert.equal(companyData.name,'İstanbul Lojistik');
    assert.equal(companyData.bootstrapStatus,'foundation_pending_economy');
    assert.ok(companyData.createdAt instanceof Timestamp);
    assert.deepEqual((await player(first.uid).get()).data(),{...identityBefore,companyId:companyRef.id});
    assert.deepEqual(await companyRef.listCollections(),[]);
    const stablePlayer = (await player(first.uid).get());
    const stableCompany = await companyRef.get();
    const stableReceipt = await receipt(first.uid,'concurrent').get();
    assert.deepEqual((await create(first,'concurrent','İstanbul Lojistik')).body,results[0].body);
    errorCode(await create(first,'concurrent','Different Name'),'IDEMPOTENCY_CONFLICT');
    errorCode(await create(first,'another-key'),'COMPANY_EXISTS');
    assert.equal((await receipt(first.uid,'another-key').get()).exists,false);
    const state = await read(first);
    assert.deepEqual(state.body.result.result,{hasCompany:true,...results[0].body.result.result});
    assert.deepEqual((await player(first.uid).get()).updateTime,stablePlayer.updateTime);
    assert.deepEqual((await companyRef.get()).updateTime,stableCompany.updateTime);
    assert.deepEqual((await receipt(first.uid,'concurrent').get()).updateTime,stableReceipt.updateTime);
    assert.equal((await invoke(first,'ensurePlayerIdentity',{protocolVersion:1})).status,200);
    assert.deepEqual((await player(first.uid).get()).data(),stablePlayer.data());
    const linked = (await linkWithCredential(first,EmailAuthProvider.credential(`company-${first.uid.toLowerCase()}@example.invalid`,'Synthetic-only-password-42!'))).user;
    assert.equal(linked.uid,first.uid);
    assert.equal((await read(linked)).body.result.result.companyId,companyRef.id);
    assert.deepEqual((await player(first.uid).get()).data(),stablePlayer.data());

    // A historical receipt cannot bypass current account/mapping/migration checks.
    await player(first.uid).update({ identityStatus:'suspended' });
    errorCode(await create(first,'concurrent','İstanbul Lojistik'),'FAILED_PRECONDITION');
    await player(first.uid).set({...stablePlayer.data(),migrationPending:true});
    errorCode(await create(first,'concurrent','İstanbul Lojistik'),'MIGRATION_REQUIRED');
    await player(first.uid).set({...stablePlayer.data(),companyId:'foreign-id'});
    errorCode(await create(first,'concurrent','İstanbul Lojistik'),'COMPANY_STATE_INVALID');
    await player(first.uid).set(stablePlayer.data());
    assert.deepEqual((await receipt(first.uid,'concurrent').get()).data(),stableReceipt.data());

    const second = await guest();
    const race = await Promise.all(Array.from({length:8},(_,i)=>create(second,`key-${i}`,'Müller Straße')));
    assert.equal(race.filter(r=>r.status===200).length,1);
    for (const result of race.filter(r=>r.status!==200)) errorCode(result,'COMPANY_EXISTS');
    assert.notEqual(company(second.uid).id,companyRef.id);
    assert.equal((await read(second)).body.result.result.companyId,company(second.uid).id);
    assert.equal((await db.collection('v2Companies').where('ownerUid','==',second.uid).get()).size,1);
    const receiptExists = await Promise.all(Array.from({length:8},(_,i)=>receipt(second.uid,`key-${i}`).get()));
    assert.equal(receiptExists.filter(r=>r.exists).length,1);

    const missing = await guest(false);
    errorCode(await create(missing,'no-identity'),'FAILED_PRECONDITION');
    errorCode(await read(missing),'FAILED_PRECONDITION');
    assert.equal((await player(missing.uid).get()).exists,false);
    assert.equal((await company(missing.uid).get()).exists,false);
    assert.equal((await receipt(missing.uid,'no-identity').get()).exists,false);

    const fixture = await guest();
    const cleanIdentity = (await player(fixture.uid).get()).data();
    const legacy = track(`users/${fixture.uid}`);
    const legacyState = track(`users/${fixture.uid}/serverState/current`);
    const migration = track(`v2MigrationRecords/${fixture.uid}`);
    const scenarios = [
      {identity:{...cleanIdentity,companyId:null},code:'COMPANY_STATE_INVALID'},
      {identity:{...cleanIdentity,companyId:companyRef.id},code:'COMPANY_STATE_INVALID'},
      {identity:{...cleanIdentity,companyId:company(fixture.uid).id},code:'COMPANY_STATE_INVALID'},
      {identity:{...cleanIdentity,schemaVersion:2},code:'FAILED_PRECONDITION'},
      {identity:{...cleanIdentity,identityStatus:'suspended'},code:'FAILED_PRECONDITION'},
      {identity:{...cleanIdentity,authUid:first.uid},code:'FAILED_PRECONDITION'},
      {identity:{...cleanIdentity,migrationPending:true},code:'MIGRATION_REQUIRED'},
      {identity:{...cleanIdentity,migrationCompleted:true},code:'MIGRATION_REQUIRED'},
      {legacy:true,code:'MIGRATION_REQUIRED'},
      {legacyState:true,code:'MIGRATION_REQUIRED'},
      {migration:true,code:'MIGRATION_REQUIRED'},
      {company:{...companyData,ownerUid:fixture.uid},code:'COMPANY_STATE_INVALID'},
      {identity:{...cleanIdentity,companyId:company(fixture.uid).id},company:{...companyData,ownerUid:first.uid},code:'COMPANY_STATE_INVALID'},
      {identity:{...cleanIdentity,companyId:company(fixture.uid).id},company:{schemaVersion:1,name:'Synthetic',bootstrapStatus:'foundation_pending_economy',createdAt:Timestamp.now()},code:'COMPANY_STATE_INVALID'},
      {identity:{...cleanIdentity,companyId:company(fixture.uid).id},company:{...companyData,ownerUid:fixture.uid,schemaVersion:99},code:'COMPANY_STATE_INVALID'},
      {identity:{...cleanIdentity,companyId:company(fixture.uid).id},company:{...companyData,ownerUid:fixture.uid,bootstrapStatus:'ready'},code:'COMPANY_STATE_INVALID'},
    ];
    for (const [i,scenario] of scenarios.entries()) {
      await player(fixture.uid).set(scenario.identity ?? cleanIdentity);
      if (scenario.company) await company(fixture.uid).set(scenario.company);
      if (scenario.legacy) await legacy.set({money:17,level:2,migrationCompleted:true});
      if (scenario.legacyState) await legacyState.set({cash:31,companyLevel:4});
      if (scenario.migration) await migration.set({status:'pending'});
      const testedRefs = [player(fixture.uid),company(fixture.uid),legacy,legacyState,migration];
      const before = await Promise.all(testedRefs.map(ref=>ref.get()));
      errorCode(await create(fixture,`fixture-${i}`),scenario.code);
      errorCode(await read(fixture),scenario.code);
      assert.equal((await receipt(fixture.uid,`fixture-${i}`).get()).exists,false);
      const after = await Promise.all(testedRefs.map(ref=>ref.get()));
      for (let j=0;j<before.length;j++) { assert.deepEqual(after[j].data(),before[j].data()); assert.deepEqual(after[j].updateTime,before[j].updateTime); }
      await Promise.all([company(fixture.uid).delete(),legacy.delete(),legacyState.delete(),migration.delete()]);
    }
  } finally {
    const cleaned = await Promise.allSettled([...refs.values()].map(ref=>ref.delete()));
    const removed = await Promise.allSettled(users.map(user=>deleteUser(user)));
    await Promise.all(apps.map(app=>deleteClient(app)));
    await db.terminate();
    if (previousRuntime === undefined) delete process.env.FUNCTIONS_EMULATOR; else process.env.FUNCTIONS_EMULATOR=previousRuntime;
    for (const result of [...cleaned,...removed]) assert.equal(result.status,'fulfilled');
  }
});

await import('./economy.emulators.test.mjs');

await import('./logistics.emulators.test.mjs');
