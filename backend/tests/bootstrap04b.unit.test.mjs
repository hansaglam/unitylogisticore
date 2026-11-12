import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { parseCommandRequest } from '../functions/lib/commands.js';
import { LOCAL_DEV_CONTRACT_VERSION, DEV_FIRST_CONTRACT_CANDIDATES as C } from '../functions/lib/devContractIds.js';
import { LOCAL_DEV_FLEET_V2, devFleetV2Document, validateDevFleetV2, devFleetV2Digest } from '../functions/lib/devFleetCatalog.js';
import { LOGISTICS_REPIN_MARKER } from '../functions/lib/repinCatalog.js';
import { LOCAL_DEV_FLEET_VERSION } from '../functions/lib/fleet.js';
import { digest, validateCatalog } from '../functions/lib/logisticsModel.js';
import { configHash, exact, validateConfig } from '../functions/lib/economyModel.js';
import { rewardAmount, validateContractConfig, epochFor, expired, acceptPayload, makeTerms } from '../functions/lib/contractModel.js';
import { arrivalTime, startPayload, claimPayload, validateDeliveryRules } from '../functions/lib/deliveryModel.js';

function economy() {
  return validateConfig({
    schemaVersion: 1, version: 'lc_dev_economy_v1', approval: 'synthetic_emulator_only', currency: 'LGC', scale: 2,
    maxBalance: 9999999999, startingBalance: 1500000, maxXp: 1000000, initialXp: 0,
    thresholds: [0, 100, 300, 600, 1000, 1500, 2100, 2800, 3600, 4500], rulesVersion: 'synthetic_v1',
  }, 'lc_dev_economy_v1');
}

test('lc_dev_fleet_v2 is schema 2 and preserves starter eligibility plus eight cities', () => {
  const catalog = validateDevFleetV2();
  assert.equal(catalog.version, LOCAL_DEV_FLEET_V2);
  assert.equal(catalog.schemaVersion, 2);
  assert.equal(catalog.starter.packageVersion, 'lc_starter_pkg_v1');
  assert.deepEqual(catalog.cities, [
    'izmir', 'istanbul', 'ankara', 'bursa', 'antalya', 'adana', 'diyarbakir', 'trabzon',
  ]);
  assert.equal(devFleetV2Digest(), '2dd718440d2f439de8bbf08c74eed150323fb046f76681d844ff667bf205bd57');
  assert.equal(catalog.warehouses.length, 0);
  const truck = catalog.assets.find(a => a.id === 'lc_starter_truck');
  const driver = catalog.assets.find(a => a.id === 'lc_starter_driver');
  assert.equal(truck.kind, 'truck');
  assert.deepEqual(truck.capabilities, ['road_freight']);
  assert.deepEqual(truck.capacity, { unit: 'kg', amount: 5000 });
  assert.equal(driver.kind, 'driver');
  assert.deepEqual(driver.capabilities, ['road_driver']);
  assert.equal(driver.capacity, null);
  assert.equal(devFleetV2Digest(), digest(catalog));
  assert.notEqual(LOCAL_DEV_FLEET_V2, LOCAL_DEV_FLEET_VERSION);
});

test('schemaVersion 1 cannot preserve capability or capacity fields', () => {
  const raw = devFleetV2Document();
  raw.schemaVersion = 1;
  assert.throws(() => validateCatalog(raw, LOCAL_DEV_FLEET_V2));
});

test('v2 catalog rejects missing cities, package drift and unapproved flags', () => {
  const raw = devFleetV2Document();
  assert.throws(() => validateDevFleetV2({ ...raw, cities: [] }));
  assert.throws(() => validateDevFleetV2({ ...raw, cities: ['izmir'] }));
  assert.throws(() => validateDevFleetV2({ ...raw, cities: ['izmir', 'istanbul'] }));
  assert.throws(() => validateDevFleetV2({ ...raw, approval: 'production' }));
  assert.throws(() => validateDevFleetV2({ ...raw, starter: { ...raw.starter, packageVersion: 'other_pkg' } }));
  assert.throws(() => validateDevFleetV2({
    ...raw, assets: raw.assets.map(a => a.id === 'lc_starter_truck' ? { ...a, capacity: { unit: 'kg', amount: 4999 } } : a),
  }));
});

test('first-contract candidates match formulas; distance remains required and unguessed', () => {
  assert.equal(rewardAmount(C.quantity, C.rewardPerUnit, 9999999999), C.expectedReward);
  assert.equal(C.expectedReward, 1000);
  assert.equal(arrivalTime(0, C.durationMs), C.durationMs);
  assert.equal(epochFor(C.epochDurationMs, C.epochDurationMs), 1);
  assert.equal(expired('offered', C.offerTtlMs, C.offerTtlMs), true);
  assert.ok(C.offerTtlMs <= C.epochDurationMs);
  const logistics = validateDevFleetV2();
  const e = economy();
  const incomplete = {
    schemaVersion: 1, version: LOCAL_DEV_CONTRACT_VERSION, approval: 'synthetic_emulator_only',
    logisticsVersion: logistics.version, logisticsHash: digest(logistics),
    economyVersion: e.version, economyHash: configHash(e),
    epochDurationMs: C.epochDurationMs, offerTtlMs: C.offerTtlMs, maxOffers: 1, maxAcceptedPerEpoch: 1,
    routes: [{
      id: 'lc_dev_route_izmir_istanbul_v1', originCityId: C.originCityId, destinationCityId: C.destinationCityId,
      verification: 'synthetic_fixture',
    }],
    cargo: [{ id: C.cargoId, unit: C.unit, capability: C.cargoCapability }],
    templates: [{
      id: 'lc_dev_job_izmir_istanbul_v1', routeId: 'lc_dev_route_izmir_istanbul_v1', cargoId: C.cargoId,
      quantity: C.quantity, rewardPerUnit: C.rewardPerUnit, minLevel: C.minLevel,
      truckCapability: C.truckCapability, driverCapability: C.driverCapability, trailerCapability: null,
    }],
  };
  assert.throws(() => validateContractConfig(incomplete, LOCAL_DEV_CONTRACT_VERSION, logistics, e));
});

test('offline fixture with explicit test-only distance validates schema 1 contract and delivery rules', () => {
  const logistics = validateDevFleetV2();
  const e = economy();
  const config = {
    schemaVersion: 1, version: LOCAL_DEV_CONTRACT_VERSION, approval: 'synthetic_emulator_only',
    logisticsVersion: logistics.version, logisticsHash: digest(logistics),
    economyVersion: e.version, economyHash: configHash(e),
    epochDurationMs: C.epochDurationMs, offerTtlMs: C.offerTtlMs, maxOffers: 1, maxAcceptedPerEpoch: 1,
    routes: [{
      id: 'lc_dev_route_izmir_istanbul_v1', originCityId: 'izmir', destinationCityId: 'istanbul',
      distance: 1, unit: 'synthetic_hop', verification: 'synthetic_fixture',
    }],
    cargo: [{ id: C.cargoId, unit: 'kg', capability: 'road_freight' }],
    templates: [{
      id: 'lc_dev_job_izmir_istanbul_v1', routeId: 'lc_dev_route_izmir_istanbul_v1', cargoId: C.cargoId,
      quantity: 1000, rewardPerUnit: 1, minLevel: 1,
      truckCapability: 'road_freight', driverCapability: 'road_driver', trailerCapability: null,
    }],
  };
  const validated = validateContractConfig(config, LOCAL_DEV_CONTRACT_VERSION, logistics, e);
  assert.equal(validated.schemaVersion, 1);
  const terms = makeTerms(validated, 0, [{ assetId: 'starter_truck', kind: 'truck' }, { assetId: 'starter_driver', kind: 'driver' }], e);
  assert.equal(terms.reward, 1000);
  const rules = {
    schemaVersion: 1, version: LOCAL_DEV_CONTRACT_VERSION, approval: 'synthetic_emulator_only',
    contractConfigHash: digest(validated), logisticsHash: digest(logistics), economyHash: configHash(e),
    routes: [{ routeId: 'lc_dev_route_izmir_istanbul_v1', cargoId: C.cargoId, durationMs: C.durationMs, xp: C.xp }],
  };
  assert.equal(validateDeliveryRules(rules, terms).rule.xp, 1);
  assert.equal(validateDeliveryRules(rules, terms).rule.durationMs, 5000);
});

test('client cannot supply economic fields on generate/accept/start/claim envelopes', () => {
  const base = { protocolVersion: 1, idempotencyKey: 'lc04b-offline', payload: {} };
  assert.deepEqual(parseCommandRequest(base), { idempotencyKey: 'lc04b-offline', payload: {} });
  for (const field of ['reward', 'xp', 'quantity', 'catalogVersion', 'epoch']) {
    assert.throws(() => parseCommandRequest({ ...base, [field]: 1 }));
    assert.throws(() => exact({ [field]: 1 }, ''));
  }
  exact({}, '');
  assert.throws(() => acceptPayload({ contractId: 'safe_id', reward: 1 }));
  assert.throws(() => startPayload({ contractId: 'c', truckId: 't', driverId: 'd', xp: 1 }));
  assert.throws(() => claimPayload({ deliveryId: 'd', reward: 1000 }));
});

test('re-pin is not a public index export; generate callable is declared', () => {
  const index = readFileSync(fileURLToPath(new URL('../functions/src/index.ts', import.meta.url)), 'utf8');
  assert.match(index, /generateCompanyContractOffers/);
  assert.doesNotMatch(index, /repinCompanyLogisticsCatalog/);
  assert.equal(LOGISTICS_REPIN_MARKER, 'logisticsCatalogRepin');
});

test('quantity 1000 kg is within starter capacity; cargo capability must be road_freight', () => {
  const truck = validateDevFleetV2().assets.find(a => a.id === 'lc_starter_truck');
  assert.ok(C.quantity <= truck.capacity.amount);
  assert.equal(C.unit, truck.capacity.unit);
  assert.ok(truck.capabilities.includes(C.cargoCapability));
});

test('generate and re-pin commands fail closed before database access outside the local emulator', async () => {
  const { generateCompanyContractOffersCommand } = await import('../functions/lib/devContract.js');
  const { repinCompanyLogisticsCatalogCommand } = await import('../functions/lib/repinCatalog.js');
  const previous = process.env.GCLOUD_PROJECT;
  process.env.GCLOUD_PROJECT = 'synthetic-nonlocal';
  const unreachable = new Proxy({}, { get() { throw Error('database touched before isolation'); } });
  try {
    assert.throws(
      () => generateCompanyContractOffersCommand(unreachable, { uid: 'synthetic' }, { protocolVersion: 1, idempotencyKey: 'k', payload: {} }),
      /Local isolation/,
    );
    assert.throws(
      () => repinCompanyLogisticsCatalogCommand(unreachable, { uid: 'synthetic' }, { protocolVersion: 1, idempotencyKey: 'k', payload: {} }),
      /Local isolation/,
    );
  } finally {
    if (previous === undefined) delete process.env.GCLOUD_PROJECT;
    else process.env.GCLOUD_PROJECT = previous;
  }
});
