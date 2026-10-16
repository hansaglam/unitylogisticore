import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { assertLocalEnvironment, LOCAL_PROJECT } from '../functions/lib/localEnvironment.js';
assertLocalEnvironment(process.env);

const { createLocalFirestore, initializeIdentity } = await import('../functions/lib/identity.js');
const { createCompanyCommand, readMyCompanyState, companyIdForUid } = await import('../functions/lib/company.js');
const { runEconomyCommand, readMyEconomyState } = await import('../functions/lib/economy.js');
const { configHash, validateConfig } = await import('../functions/lib/economyModel.js');
const request = (key, payload = {}) => ({ protocolVersion: 1, idempotencyKey: key, payload });
const readRequest = { protocolVersion: 1 };
async function post(name, data, token) {
  const response = await fetch(`http://127.0.0.1:5001/${LOCAL_PROJECT}/us-central1/${name}`, {
    method: 'POST', signal: AbortSignal.timeout(15000), headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: JSON.stringify({ data }),
  });
  return { status: response.status, body: await response.json() };
}

test('economy: real emulator atomic initialization, finance, rewards, isolation and historical replay', { timeout: 180000 }, async t => {
  const previous = process.env.FUNCTIONS_EMULATOR; process.env.FUNCTIONS_EMULATOR = 'true';
  const db = createLocalFirestore();
  const refs = new Map();
  const own = path => { const ref = db.doc(path); refs.set(path, ref); return ref; };
  const uid = `synthetic-economy-${randomUUID()}`;
  const auth = { uid }; const companyId = companyIdForUid(uid); const root = `v2Companies/${companyId}`;
  const player = own(`v2Players/${uid}`); const company = own(root);
  const finance = own(`${root}/state/finance`); const progression = own(`${root}/state/progression`);
  const version = `synthetic_${randomUUID()}`;
  const configRef = own(`v2WorldConfig/${version}`);
  const config = { schemaVersion: 1, version, approval: 'synthetic_emulator_only', currency: 'TST', scale: 2,
    maxBalance: 10000, startingBalance: 100, maxXp: 1000, initialXp: 0, thresholds: [0,10,50], rulesVersion: 'synthetic_v1' };
  const receipt = key => own(`v2Players/${uid}/commands/${key}`);
  const run = async (key, kind, id, database = db) => {
    receipt(key);
    const response = await runEconomyCommand(database, auth, request(key), { kind, id });
    own(`${root}/ledger/${response.result.operationId}`);
    own(`${root}/claims/${kind === 'initialize' ? 'initialization' : `entitlement_${id}`}`);
    return response;
  };
  const entitlement = async (id, delta, xpDelta = 0, overrides = {}) => {
    const ref = own(`${root}/entitlements/${id}`);
    await ref.set({ schemaVersion: 1, approval: 'synthetic_emulator_only', ownerUid: uid, companyId,
      configVersion: version, configHash: configHash(validateConfig(config, version)), currency: 'TST', scale: 2, delta, xpDelta,
      reason: delta < 0 ? 'synthetic_debit' : 'synthetic_reward', ...overrides });
    return ref;
  };
  const snapshot = async () => {
    const data = await db.getAll(company, finance, progression);
    return data.map(doc => ({ data: doc.data(), update: doc.updateTime?.toMillis() }));
  };
  let authToken;
  try {
    await initializeIdentity(db, auth, readRequest); receipt('create');
    const created = await createCompanyCommand(db, auth, request('create', { name: 'Synthetic Economy' }));
    await t.test('pending has no fake state; missing/unapproved config and partial state reject without writes', async () => {
      assert.deepEqual((await readMyEconomyState(db, auth, readRequest)).result, { hasCompany: true, economyReady: false });
      assert.equal((await finance.get()).exists, false); assert.equal((await progression.get()).exists, false);
      await assert.rejects(run('missing', 'initialize', version));
      assert.equal((await receipt('missing').get()).exists, false);
      await configRef.set({ ...config, approval: 'unapproved' });
      await assert.rejects(run('unapproved', 'initialize', version));
      await finance.set({ balance: 100 });
      await configRef.set(config);
      await assert.rejects(run('partial', 'initialize', version));
      await finance.delete();
      assert.equal((await company.get()).data().bootstrapStatus, 'foundation_pending_economy');
    });
    let initialized;
    await t.test('initialization same-key concurrent replay and different-key duplicate protection', async () => {
      const [a,b] = await Promise.all([run('init', 'initialize', version), run('init', 'initialize', version)]);
      assert.deepEqual(a,b); initialized = a;
      await assert.rejects(run('init_other', 'initialize', version));
      assert.equal((await finance.get()).data().balance, 100);
      assert.equal((await progression.get()).data().xp, 0);
      assert.equal((await company.get()).data().bootstrapStatus, 'economy_ready');
      assert.deepEqual(await createCompanyCommand(db, auth, request('create', { name: 'Synthetic Economy' })), created);
      receipt('create_other');
      await assert.rejects(createCompanyCommand(db, auth, request('create_other', { name: 'Other Company' })), e => e.details.code === 'COMPANY_EXISTS');
      assert.equal((await readMyCompanyState(db, auth, readRequest)).result.economyReady, true);
    });
    await t.test('real concurrent spending cannot overdraw; losing operation has no partial writes', async () => {
      await entitlement('spend_a', -80); await entitlement('spend_b', -80);
      const outcomes = await Promise.allSettled([run('spend_a','entitlement','spend_a'), run('spend_b','entitlement','spend_b')]);
      assert.equal(outcomes.filter(x => x.status === 'fulfilled').length, 1);
      const balance = (await finance.get()).data().balance; assert.equal(balance,20);
      for (let i=0;i<2;i++) {
        const key = i === 0 ? 'spend_a' : 'spend_b';
        assert.equal((await receipt(key).get()).exists, outcomes[i].status === 'fulfilled');
        assert.equal((await own(`${root}/claims/entitlement_${key}`).get()).exists, outcomes[i].status === 'fulfilled');
      }
      const winner = outcomes.find(x => x.status === 'fulfilled').value;
      const ledger = (await own(`${root}/ledger/${winner.result.operationId}`).get()).data();
      assert.equal(ledger.delta,-80); assert.equal(ledger.balanceAfter,balance);
    });
    await t.test('reward same-key replay and different-key claim race update money/XP exactly once', async () => {
      await entitlement('reward', 30, 50);
      const outcomes = await Promise.allSettled([run('reward_a','entitlement','reward'),run('reward_b','entitlement','reward')]);
      assert.equal(outcomes.filter(x => x.status === 'fulfilled').length,1);
      const i = outcomes.findIndex(x => x.status === 'fulfilled'); const key = i === 0 ? 'reward_a' : 'reward_b';
      const response = outcomes[i].value;
      assert.deepEqual(await run(key,'entitlement','reward'),response);
      assert.equal(response.result.balance,'50'); assert.equal(response.result.xp,50); assert.equal(response.result.level,3);
      assert.equal((await own(`${root}/claims/entitlement_reward`).get()).data().operationId,response.result.operationId);
    });
    await t.test('malformed/foreign entitlements, currency, negative funds and overflow fail atomically', async () => {
      const cases = [{ delta: -51 }, { delta: NaN }, { delta: Infinity }, { delta: 0.5 }, { delta: '1' },
        { delta: Number.MAX_SAFE_INTEGER }, { delta: 9999 }, { xpDelta: 1000 }, { currency: 'USD' }, { scale: 3 },
        { ownerUid: 'foreign' }, { companyId: 'foreign' }, { approval: 'unapproved' }, { configHash: 'wrong' }, { reason: 'arbitrary' }];
      for (let i=0;i<cases.length;i++) {
        const key=`bad_${i}`; await entitlement(key,1,0,cases[i]); const before=await snapshot();
        await assert.rejects(run(key,'entitlement',key));
        assert.deepEqual(await snapshot(),before); assert.equal((await receipt(key).get()).exists,false);
        assert.equal((await own(`${root}/claims/entitlement_${key}`).get()).exists,false);
      }
      await assert.rejects(run('absent','entitlement','absent'));
      await entitlement('blocked_ledger', 1);
      const blockedId = createHash('sha256').update(`${companyId}:entitlement_blocked_ledger`).digest('hex');
      const blockedLedger = own(`${root}/ledger/${blockedId}`);
      await blockedLedger.set({ synthetic: true });
      const before = await snapshot();
      await assert.rejects(run('blocked_ledger','entitlement','blocked_ledger'));
      assert.deepEqual(await snapshot(),before);
      assert.deepEqual((await blockedLedger.get()).data(),{synthetic:true});
      assert.equal((await receipt('blocked_ledger').get()).exists,false);
    });
    await t.test('ownership, migration, config drift and corrupt ready data fail closed; reads never repair', async () => {
      const companyData=(await company.get()).data(), financeData=(await finance.get()).data(), progressionData=(await progression.get()).data();
      for (const [ref, original, change] of [[company,companyData,{ownerUid:'foreign'}],[company,companyData,{bootstrapStatus:'unknown'}],
        [company,companyData,{migrationPending:false}],[finance,financeData,{balance:-1}],[finance,financeData,{currency:'USD'}],
        [progression,progressionData,{level:99}],[progression,progressionData,{xp:1.1}]]) {
        await ref.set({...original,...change}); const before=await snapshot();
        await assert.rejects(readMyEconomyState(db,auth,readRequest)); await assert.rejects(readMyCompanyState(db,auth,readRequest));
        assert.deepEqual(await snapshot(),before); await ref.set(original);
      }
      await progression.delete(); await assert.rejects(readMyEconomyState(db,auth,readRequest)); await progression.set(progressionData);
      await configRef.set({...config,startingBalance:101}); await assert.rejects(readMyEconomyState(db,auth,readRequest)); await configRef.set(config);
      const legacy=own(`users/${uid}`); await legacy.set({synthetic:true}); await assert.rejects(run('legacy','entitlement','reward')); await legacy.delete();
      const identity=(await player.get()).data(); await player.update({companyId:'foreign'}); await assert.rejects(readMyEconomyState(db,auth,readRequest)); await player.set(identity);
      await assert.rejects(readMyEconomyState(db,undefined,readRequest));
      await assert.rejects(readMyEconomyState(db,auth,{...readRequest,companyId}));
      const before=await snapshot(); await readMyEconomyState(db,auth,readRequest); await readMyCompanyState(db,auth,readRequest); assert.deepEqual(await snapshot(),before);
      assert.deepEqual(await run('init','initialize',version),initialized);
    });
    await t.test('post-commit lost response and injected ABORTED SDK retry retain one effect', async () => {
      await entitlement('lost',1,1);
      const originalCollection=db.collection.bind(db); let lose=true;
      const proxy=new Proxy(db,{get(target,prop) {
        if(prop==='collection') return path => {
          const collection=originalCollection(path);
          if(path!=='v2Players') return collection;
          return new Proxy(collection,{get(c,p) { if(p==='doc') return id => {
            const ref=c.doc(id);
            return new Proxy(ref,{get(r,q) {if(q==='collection') return sub => {
              const subcollection=r.collection(sub);
              return new Proxy(subcollection,{get(s,k) {if(k==='doc') return key => {
                const doc=s.doc(key); const originalGet=doc.get.bind(doc);
                doc.get=async (...args) => {if(key==='lost' && lose){lose=false;throw Object.assign(Error('synthetic lost response'),{code:14});} return originalGet(...args);};
                return doc;
              }; const v=s[k];return typeof v==='function'?v.bind(s):v;}});
            };const v=r[q];return typeof v==='function'?v.bind(r):v;}});
          };const v=c[p];return typeof v==='function'?v.bind(c):v;}});
        }; const value=target[prop];return typeof value==='function'?value.bind(target):value;
      }});
      await assert.rejects(run('lost','entitlement','lost',proxy), e=>e.details.code==='RETRY_LATER');
      const persisted=(await receipt('lost').get()).data();
      own(`${root}/ledger/${persisted.result.operationId}`); own(`${root}/claims/entitlement_lost`);
      const recovered=await run('lost','entitlement','lost'); assert.deepEqual(recovered.result,persisted.result);
      assert.equal(recovered.result.balance,'51'); assert.equal(recovered.result.xp,51);
      await entitlement('retry',1,0);
      // Force ABORTED after queued writes; the real SDK retries without committing that attempt.
      let callbackAttempts=0;
      const retrySafeDb=new Proxy(db,{get(_target,prop) {
        if(prop==='runTransaction') return callback => db.runTransaction(async tx => {
          callbackAttempts++; const result=await callback(tx);
          if(callbackAttempts===1) throw Object.assign(Error('synthetic aborted attempt'),{code:10});
          return result;
        });
        const value=db[prop];return typeof value==='function'?value.bind(db):value;
      }});
      await run('retry','entitlement','retry',retrySafeDb); assert.ok(callbackAttempts>=2);
      assert.equal((await finance.get()).data().balance,52);
    });
    await t.test('read callable authenticates, rejects spoofing, returns only pending/ready state without writes', async () => {
      assert.equal((await post('getMyEconomyState',readRequest)).status,401);
      assert.equal((await post('getMyEconomyState',readRequest,'invalid')).status,401);
      const signup=await fetch('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signUp?key=synthetic-key',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({returnSecureToken:true})});
      const identity=await signup.json(); authToken=identity.idToken;
      const apiUid=identity.localId; const apiCompany=companyIdForUid(apiUid);
      own(`v2Players/${apiUid}`); own(`v2Companies/${apiCompany}`); own(`v2Players/${apiUid}/commands/api_create`);
      assert.equal((await post('ensurePlayerIdentity',readRequest,authToken)).status,200);
      assert.equal((await post('getMyEconomyState',{...readRequest,uid},authToken)).status,400);
      assert.equal((await post('createCompany',request('api_create',{name:'Synthetic API'}),authToken)).status,200);
      assert.deepEqual((await post('getMyEconomyState',readRequest,authToken)).body.result.result,{hasCompany:true,economyReady:false});
      own(`v2Companies/${apiCompany}/state/finance`); own(`v2Companies/${apiCompany}/state/progression`); own(`v2Players/${apiUid}/commands/api_init`);
      const ready=await runEconomyCommand(db,{uid:apiUid},request('api_init'),{kind:'initialize',id:version});
      own(`v2Companies/${apiCompany}/ledger/${ready.result.operationId}`);own(`v2Companies/${apiCompany}/claims/initialization`);
      const response=await post('getMyEconomyState',readRequest,authToken);assert.equal(response.status,200);assert.equal(response.body.result.result.balance,'100');
    });
  } finally {
    if(authToken) await fetch('http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:delete?key=synthetic-key',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({idToken:authToken})});
    for(const ref of refs.values()) await ref.delete();
    await db.terminate(); if(previous===undefined) delete process.env.FUNCTIONS_EMULATOR; else process.env.FUNCTIONS_EMULATOR=previous;
  }
});
