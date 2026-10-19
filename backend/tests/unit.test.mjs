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

import { checkedAdd, validateConfig, levelFor } from '../functions/lib/economyModel.js';
const syntheticConfig = { schemaVersion: 1, version: 'synthetic_unit', approval: 'synthetic_emulator_only', currency: 'TST', scale: 2,
  maxBalance: 10000, startingBalance: 100, maxXp: 1000, initialXp: 0, thresholds: [0, 10, 50], rulesVersion: 'synthetic_rules' };
test('economy exact integer arithmetic rejects negative, nonnumeric and unsafe values', () => {
  assert.equal(checkedAdd(100, -100, 10000), 0);
  for (const args of [[0,-1,100], [Number.MAX_SAFE_INTEGER,1,Number.MAX_SAFE_INTEGER], [1,0.1,100], [NaN,0,100], [1,Infinity,100], ['1',0,100], [101,0,100]]) {
    assert.throws(() => checkedAdd(...args));
  }
});
test('economy config is explicit synthetic-only; deterministic bounded thresholds', () => {
  const config = validateConfig(syntheticConfig, syntheticConfig.version);
  assert.deepEqual([0,9,10,49,50,1000].map(xp => levelFor(xp, config)), [1,1,2,2,3,3]);
  for (const change of [{ approval: 'approved' }, { currency: 'cash' }, { scale: 7 }, { startingBalance: -1 }, { initialXp: 1001 },
    { maxBalance: Infinity }, { thresholds: [] }, { thresholds: [1,2] }, { thresholds: [0,10,10] }, { thresholds: [0,20,10] },
    { thresholds: [0,1001] }, { thresholds: [0,0.5] }, { rulesVersion: '../unsafe' }, { extra: true }]) {
    assert.throws(() => validateConfig({ ...syntheticConfig, ...change }, syntheticConfig.version));
  }
  assert.throws(() => validateConfig(undefined, syntheticConfig.version));
  assert.throws(() => levelFor(1001, config));
});

import { logisticsFixture } from './logistics.fixtures.mjs';
import { validateCatalog, validateAsset, validateWarehouse, requireCapacity, warehouseId, pageRequest, digest } from '../functions/lib/logisticsModel.js';
import { validateSelection } from '../functions/lib/fleet.js';
import { Timestamp } from 'firebase-admin/firestore';

test('logistics catalog rejects unapproved, unknown, duplicate and incompatible definitions', () => {
  const fixture = logisticsFixture('synthetic_unit');
  assert.deepEqual(validateCatalog(fixture,fixture.version),fixture);
  for(const change of [{approval:'production'}, {assets:[{id:'test_truck',kind:'boat'}]}, {assets:[...fixture.assets,fixture.assets[0]]},
    {starter:{...fixture.starter,driver:'test_truck'}}, {cities:['synthetic_city','synthetic_city']},
    {warehouses:[{...fixture.warehouses[0],cityId:'unknown'}]}, {warehouses:[{...fixture.warehouses[0],capacityUnits:0.5}]},
    {warehouses:[{...fixture.warehouses[0],level:0}]}, {extra:1}, {version:'different'}]) {
    assert.throws(()=>validateCatalog({...fixture,...change},fixture.version));
  }
  assert.throws(()=>validateCatalog(undefined,fixture.version));
});
test('asset availability and assignments validate kind, owner and state', () => {
  const catalog=validateCatalog(logisticsFixture('synthetic_unit'),'synthetic_unit');
  const asset={schemaVersion:1,assetId:'test_asset',kind:'truck',companyId:'synthetic_company',catalogId:'test_truck',catalogVersion:catalog.version,
    catalogHash:digest(catalog),status:'operational',assignmentId:null,acquiredAt:Timestamp.now()};
  assert.equal(validateAsset(asset,asset.assetId,asset.companyId,catalog).available,true);
  assert.equal(validateAsset({...asset,status:'out_of_service'},asset.assetId,asset.companyId,catalog).available,false);
  assert.equal(validateAsset({...asset,assignmentId:'a'.repeat(64)},asset.assetId,asset.companyId,catalog).available,false);
  for(const change of [{companyId:'other'},{kind:'driver'},{status:'available'},{assignmentId:'client_id'},{acquiredAt:0},{catalogHash:'wrong'}]) {
    assert.throws(()=>validateAsset({...asset,...change},asset.assetId,asset.companyId,catalog));
  }
  const pair=[{assetId:'truck',kind:'truck'},{assetId:'driver',kind:'driver'}];
  assert.equal(validateSelection(pair).length,2);
  assert.equal(validateSelection([...pair,{assetId:'trailer',kind:'trailer'}]).length,3);
  for(const selection of [[],[pair[0]], [pair[0],pair[0]], [...pair,{assetId:'driver2',kind:'driver'}], [...pair,{assetId:'bad',kind:'boat'}]]) assert.throws(()=>validateSelection(selection));
});
test('warehouse capacity, ownership and deterministic city/type uniqueness are bounded', () => {
  const catalog=validateCatalog(logisticsFixture('synthetic_unit'),'synthetic_unit'); const def=catalog.warehouses[0];
  const id=warehouseId(def.cityId,def.type);
  assert.equal(id,warehouseId(def.cityId,def.type)); assert.notEqual(id,warehouseId(def.cityId,'other_type'));
  const record={schemaVersion:1,warehouseId:id,companyId:'synthetic_company',catalogId:def.id,catalogVersion:catalog.version,catalogHash:digest(catalog),
    cityId:def.cityId,type:def.type,level:def.level,status:'operational',capacityModelId:def.capacityModelId,usedCapacity:0,acquiredAt:Timestamp.now()};
  assert.equal(validateWarehouse(record,id,record.companyId,catalog).capacityUnits,'10');
  for(const change of [{companyId:'foreign'},{cityId:'unknown'},{usedCapacity:11},{level:2},{catalogHash:'wrong'}]) assert.throws(()=>validateWarehouse({...record,...change},id,record.companyId,catalog));
  requireCapacity(9,1,10);
  for(const args of [[9,2,10],[-1,0,10],[0,0.5,10],[0,NaN,10],[0,1,Infinity],[Number.MAX_SAFE_INTEGER,1,Number.MAX_SAFE_INTEGER]]) assert.throws(()=>requireCapacity(...args));
});
test('logistics pagination rejects spoofing and oversized limits/cursors', () => {
  assert.deepEqual(pageRequest({protocolVersion:1}),{limit:20,after:null});
  assert.deepEqual(pageRequest({protocolVersion:1,limit:1,after:'starter_driver'}),{limit:1,after:'starter_driver'});
  for(const data of [null,{protocolVersion:2},{protocolVersion:1,companyId:'other'},{protocolVersion:1,uid:'other'},
    {protocolVersion:1,limit:0},{protocolVersion:1,limit:21},{protocolVersion:1,limit:1.5},{protocolVersion:1,after:'../foreign'},
    {protocolVersion:1,after:'a'.repeat(65)}]) assert.throws(()=>pageRequest(data));
});

test('internal logistics transaction primitives reject nonlocal execution before database access', async () => {
  const { writeAssignment, readStarter } = await import('../functions/lib/fleet.js');
  const { createWarehouseInTransaction } = await import('../functions/lib/warehouses.js');
  const previous=process.env.GCLOUD_PROJECT;process.env.GCLOUD_PROJECT='synthetic-nonlocal';
  const unreachable=new Proxy({}, {get(){throw Error('database touched before isolation');}});
  try {
    await assert.rejects(readStarter(unreachable,unreachable,unreachable),/Local isolation/);
    await assert.rejects(writeAssignment(unreachable,unreachable,unreachable,[],'synthetic','reserve'),/Local isolation/);
    await assert.rejects(createWarehouseInTransaction(unreachable,unreachable,unreachable,'synthetic','synthetic'),/Local isolation/);
  } finally {if(previous===undefined)delete process.env.GCLOUD_PROJECT;else process.env.GCLOUD_PROJECT=previous;}
});
