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

import { contractFixtures, candidates } from './contracts.fixtures.mjs';
import { validateContractConfig, rewardAmount, expired, acceptPayload, epochFor, makeTerms, contractIdFor } from '../functions/lib/contractModel.js';
test('contract catalog links city/cargo/equipment and rejects malformed or unapproved terms',()=>{
 const f=contractFixtures('synthetic_contract_unit');const economy=validateConfig(f.economy,f.economy.version);const logistics=validateCatalog(f.logistics,f.logistics.version);
 const config=validateContractConfig(f.config,f.config.version,logistics,economy);
 assert.equal(makeTerms(config,0,candidates,economy).reward,6);
 for(const patch of [{approval:'real'},{maxOffers:6},{offerTtlMs:600001},{maxAcceptedPerEpoch:4},{logisticsHash:'bad'},
  {routes:[{...f.config.routes[0],destinationCityId:'izmir'}]}, {templates:[{...f.config.templates[0],cargoId:'unknown'}]},
  {templates:[{...f.config.templates[0],quantity:0.5}]},{templates:[{...f.config.templates[0],rewardPerUnit:Number.MAX_SAFE_INTEGER}]}])
  assert.throws(()=>validateContractConfig({...f.config,...patch},f.config.version,logistics,economy));
 assert.throws(()=>validateContractConfig(f.config,f.config.version,validateCatalog(logisticsFixture(f.config.version),f.config.version),economy));
});
test('contract exact reward, server expiration boundaries and strict acceptance intent',()=>{
 assert.equal(rewardAmount(2,3,10),6);assert.throws(()=>rewardAmount(Number.MAX_SAFE_INTEGER,2,Number.MAX_SAFE_INTEGER));
 assert.equal(expired('offered',100,99),false);assert.equal(expired('offered',100,100),true);assert.equal(expired('accepted',100,101),false);
 assert.equal(epochFor(600001,600000),1);assert.equal(contractIdFor('company',1,0),contractIdFor('company',1,0));
 for(const payload of [null,{}, {contractId:'x',reward:1},{contractId:'../x'},{contractId:'x',uid:'spoof'}])assert.throws(()=>acceptPayload(payload));
 assert.deepEqual(acceptPayload({contractId:'safe_id'}),{contractId:'safe_id'});
});

import { arrivalTime, MAX_TIMESTAMP, startPayload, claimPayload, validateDeliveryRules } from '../functions/lib/deliveryModel.js';
test('delivery payloads allow intent only and timestamp arithmetic rejects overflow',()=>{
 assert.equal(arrivalTime(100,1),101);for(const args of [[100,0],[100,-1],[100,0.5],[MAX_TIMESTAMP,1],[0,604800001]])assert.throws(()=>arrivalTime(...args));
 assert.deepEqual(startPayload({contractId:'c',truckId:'t',driverId:'d'}),{contractId:'c',truckId:'t',driverId:'d'});
 for(const key of ['uid','companyId','reward','xp','startedAt','expectedArrivalAt','position'])assert.throws(()=>startPayload({contractId:'c',truckId:'t',driverId:'d',[key]:1}));
 assert.throws(()=>claimPayload({deliveryId:'d',now:123}));assert.throws(()=>claimPayload({deliveryId:'../other'}));
});
test('delivery rules require pinned explicit synthetic route/cargo and bounded duration/XP',()=>{
 const f=contractFixtures('synthetic_delivery_unit');const e=validateConfig(f.economy,f.economy.version);const l=validateCatalog(f.logistics,f.logistics.version);
 const config=validateContractConfig(f.config,f.config.version,l,e),terms=makeTerms(config,0,candidates,e);
 const rules={schemaVersion:1,version:config.version,approval:'synthetic_emulator_only',contractConfigHash:digest(config),logisticsHash:digest(l),economyHash:terms.economyHash,
 routes:[{routeId:terms.route.id,cargoId:terms.cargo.id,durationMs:100,xp:10}]};
 assert.equal(validateDeliveryRules(rules,terms).rule.durationMs,100);
 for(const change of [{approval:'real'},{economyHash:'bad'},{routes:[{...rules.routes[0],durationMs:0}]},{routes:[{...rules.routes[0],xp:-1}]},
 {routes:[{...rules.routes[0],cargoId:'unknown'}]},{routes:[rules.routes[0],rules.routes[0]]}])assert.throws(()=>validateDeliveryRules({...rules,...change},terms));
});

import { acquisitionPayload, acquisitionId, acquisitionSource, validateCommerceCatalog, validateAcquisitionOffer } from '../functions/lib/commerceModel.js';
import { commerceFixtures } from './commerce.fixtures.mjs';
test('commerce strict intent, positive bounded price, pinned config, progression and one-step warehouse definitions',()=>{
 const f=commerceFixtures('synthetic_commerce_unit');const e=validateConfig(f.economy,f.economy.version),l=validateCatalog(f.logistics,f.logistics.version);
 assert.equal(validateCommerceCatalog(f.commerce,f.commerce.version,l,e).items.length,7);
 for(const extra of ['price','ownerUid','companyId','level','catalogVersion'])assert.throws(()=>acquisitionPayload({offerId:'x',[extra]:1}));
 assert.throws(()=>acquisitionPayload({offerId:'../x'}));assert.deepEqual(acquisitionPayload({offerId:'x'}),{offerId:'x'});
 for(const price of [-1,0,0.1,Number.MAX_SAFE_INTEGER+1,e.maxBalance+1])assert.throws(()=>validateCommerceCatalog({...f.commerce,items:[{...f.commerce.items[0],price}]},f.commerce.version,l,e));
 for(const change of [{approval:'production'},{economyHash:'bad'},{logisticsHash:'bad'},{rulesVersion:'bad'},
  {items:[{...f.commerce.items[4],expectedLevel:0}]},{items:[{...f.commerce.items[0],requiredLevel:4}]}])assert.throws(()=>validateCommerceCatalog({...f.commerce,...change},f.commerce.version,l,e));
 assert.notEqual(acquisitionId('company','a'),acquisitionId('company','b'));
 assert.equal(acquisitionSource('company','a'),`commerce_${acquisitionId('company','a')}`);
});

import {settlementFixtures} from './settlement.fixtures.mjs';
import {bankFixtures} from './bank.fixtures.mjs';
import {bankCatalog,bankPolicy,validateBankPolicy,creditLimit,loanTerms,amountIntent,repayment,offerIdFor,loanIdFor} from '../functions/lib/bankModel.js';
test('bank catalog strict tiers cover canonical progression without gaps, overlap, drift or unsafe limits',()=>{
 const f=bankFixtures('bank_unit'),c=bankCatalog(f.bank,f.bank.version,f.economy);assert.equal(digest(c),digest(f.bank));
 for(const patch of [{tiers:[]},{tiers:[{from:1,to:2,maximum:40},{from:2,to:3,maximum:60}]},{tiers:[{from:1,to:1,maximum:40},{from:3,to:3,maximum:60}]},
  {maxLevel:50},{minLevel:0},{minPrincipal:0},{increment:0},{minPrincipal:11},{maxLiability:10},{chargeBps:10001},{repaymentBps:-1},{quoteTtlMs:0},{maxQuoteRequests:101},{maxQuoteWrites:21},{reserve:10001},{extra:1}])assert.throws(()=>bankCatalog({...f.bank,...patch},f.bank.version,f.economy));
 assert.equal(creditLimit(c,1),40);assert.equal(creditLimit(c,2),60);assert.throws(()=>creditLimit(c,4));
});
test('bank money intents and fixed fees use exact bounded integers; fee-first reserve/debt caps',()=>{
 const f=bankFixtures('bank_math'),c=bankCatalog(f.bank,f.bank.version,f.economy);assert.equal(amountIntent('9007199254740991'),Number.MAX_SAFE_INTEGER);
 for(const n of ['01','+1','1.0','1e3','0','-1','9007199254740992',10,null])assert.throws(()=>amountIntent(n));
 assert.equal(loanTerms(c,10,1).fee,1);assert.equal(loanTerms(c,40,1).fee,3);assert.throws(()=>loanTerms(c,50,1));
 const l={terms:loanTerms(c,40,1),remainingPrincipal:40,remainingFee:3};
 assert.deepEqual(repayment(l,999,7n),{total:2,fee:2,principal:0,debtBefore:43,debtAfter:41,remainingPrincipal:40,remainingFee:1});
 assert.equal(repayment(l,999,5n).total,0);assert.equal(repayment(l,999,100n).total,43);
 assert.equal(repayment(l,999,BigInt(Number.MAX_SAFE_INTEGER)+100n).total,43);
});
test('V3 policy pin extends schema while V2 operating cost snapshot remains authoritative',()=>{
 const f=bankFixtures('bank_terms'),l=validateCatalog(f.logistics,f.logistics.version),c=validateContractConfig(f.config,f.config.version,l,f.economy),t=makeTerms(c,0,candidates,f.economy,f.costCatalog);
 assert.equal(digest(c),digest(f.config));assert.deepEqual(t.bankPolicy,bankPolicy());assert.equal(t.settlement.settlementVersion,2);assert.equal(t.settlement.netReward,20);
 assert.throws(()=>validateBankPolicy({...bankPolicy(),policy:'other'}));assert.notEqual(offerIdFor('a',1),offerIdFor('a',2));assert.notEqual(loanIdFor('a',1),loanIdFor('a',2));
});
test('bank entry points and settlement reject nonlocal environment before touching database',async()=>{
 const {initializeBank,bankCommand,readMyBankState,readBank}=await import('../functions/lib/bank.js');
 const {prepareBankSettlement,validateBankSettlement}=await import('../functions/lib/bankSettlement.js');
 const previous=process.env.GCLOUD_PROJECT;process.env.GCLOUD_PROJECT='synthetic-nonlocal';const db=new Proxy({}, {get(){throw Error('database accessed');}});
 try{for(const action of [()=>initializeBank(db,{uid:'synthetic'},{},'test'),()=>bankCommand(db,{uid:'synthetic'},{},'quoteCompanyLoan'),()=>readMyBankState(db,{uid:'synthetic'},{}),()=>readBank(db,db,db),()=>prepareBankSettlement(db,db,'test',db,0),()=>validateBankSettlement(db,db,'test',db,0)])await assert.rejects(action,/Local isolation/);}
 finally{if(previous===undefined)delete process.env.GCLOUD_PROJECT;else process.env.GCLOUD_PROJECT=previous;}
});
import {costTotal,validateCostCatalog,validateCostQuote,makeCostQuote} from '../functions/lib/operatingCosts.js';
import {settlementAccounting} from '../functions/lib/settlement.js';
test('net settlement helpers reject nonlocal execution before any database access',async()=>{
 const {prepareDeliverySettlement,validateSettlementJournal}=await import('../functions/lib/settlement.js');
 const {readPinnedCosts}=await import('../functions/lib/operatingCosts.js');
 const previous=process.env.GCLOUD_PROJECT;process.env.GCLOUD_PROJECT='synthetic-nonlocal';
 const unreachable=new Proxy({}, {get(){throw Error('database accessed');}});
 try{
  await assert.rejects(prepareDeliverySettlement(unreachable,unreachable,'synthetic',unreachable,0),/Local isolation/);
  await assert.rejects(validateSettlementJournal(unreachable,unreachable,'synthetic',unreachable,0),/Local isolation/);
  await assert.rejects(readPinnedCosts(unreachable,unreachable,unreachable,unreachable),/Local isolation/);
 }finally{if(previous===undefined)delete process.env.GCLOUD_PROJECT;else process.env.GCLOUD_PROJECT=previous;}
});
test('operating costs use bounded exact sums and reject negative/fractional/unsafe categories',()=>{
 assert.equal(costTotal({fuelCost:70000,driverCost:40000,maintenanceCost:20000,tollCost:10000}),140000);
 for(const n of [-1,0.1,NaN,Infinity,Number.MAX_SAFE_INTEGER+1])assert.throws(()=>costTotal({fuelCost:n,driverCost:0,maintenanceCost:0,tollCost:0}));
 assert.throws(()=>costTotal({fuelCost:Number.MAX_SAFE_INTEGER,driverCost:1,maintenanceCost:0,tollCost:0}));
 assert.throws(()=>costTotal({fuelCost:2,driverCost:0,maintenanceCost:0,tollCost:0},1));
});
test('cost catalog has strict acyclic pins, bounded applicability and synthetic approval',()=>{
 const f=settlementFixtures('synthetic_cost_unit'),e=validateConfig(f.economy,f.economy.version),l=validateCatalog(f.logistics,f.logistics.version);
 assert.deepEqual(validateCostCatalog(f.costCatalog,f.costCatalog.version,l,e),f.costCatalog);
 for(const patch of [{approval:'production'},{currency:'LGC'},{scale:3},{economyHash:'bad'},{logisticsVersion:'other'},
  {entries:[]},{entries:Array(6).fill(f.costCatalog.entries[0])},{entries:[f.costCatalog.entries[0],f.costCatalog.entries[0]]},{contractConfigHash:'circular'}])
  assert.throws(()=>validateCostCatalog({...f.costCatalog,...patch},f.costCatalog.version,l,e));
});
test('new quotes require strictly positive net; minimal positive and zero costs valid',()=>{
 const f=settlementFixtures('synthetic_quote_unit');
 for(const gross of [0,3,4])assert.throws(()=>makeCostQuote(f.costCatalog,'synthetic_job_0','synthetic_route','synthetic_cargo',gross));
 assert.equal(makeCostQuote(f.costCatalog,'synthetic_job_0','synthetic_route','synthetic_cargo',5).netReward,1);
 const zero=settlementFixtures('synthetic_zero',{costs:{fuelCost:0,driverCost:0,maintenanceCost:0,tollCost:0}});
 assert.equal(makeCostQuote(zero.costCatalog,'synthetic_job_0','synthetic_route','synthetic_cargo',6).netReward,6);
});
test('v2 terms pin exact quote; schema1 hashes and missing-cost rejection remain explicit',()=>{
 const f=settlementFixtures('synthetic_terms_unit'),e=validateConfig(f.economy,f.economy.version),l=validateCatalog(f.logistics,f.logistics.version);
 const cfg=validateContractConfig(f.config,f.config.version,l,e),terms=makeTerms(cfg,0,candidates,e,f.costCatalog);
 assert.equal(terms.settlement.netReward,2);assert.equal(digest(cfg),digest(f.config));assert.throws(()=>makeTerms(cfg,0,candidates,e));
 for(const patch of [{netReward:6},{fuelCost:0},{grossReward:7},{costCatalogHash:'bad'},{unexpected:1}])assert.throws(()=>validateCostQuote({...terms.settlement,...patch},6));
 const old=contractFixtures('synthetic_old');const oldCfg=validateContractConfig(old.config,old.config.version,validateCatalog(old.logistics,old.logistics.version),validateConfig(old.economy,old.economy.version));
 assert.equal(digest(oldCfg),digest(old.config));assert.equal(Object.hasOwn(makeTerms(oldCfg,0,candidates,old.economy),'settlement'),false);
 assert.deepEqual(settlementAccounting(terms,10,'delivery'),{grossRevenue:6,totalOperatingExpenses:4,netOperatingProfit:2,finalWalletDelta:2,eligibleXp:10,source:'delivery_delivery'});
});
