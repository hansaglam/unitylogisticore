import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { parseCommandRequest } from '../functions/lib/commands.js';
import { LOCAL_DEV_CONTRACT_VERSION, DEV_FIRST_CONTRACT_CANDIDATES as C } from '../functions/lib/devContractIds.js';
import { LOCAL_DEV_FLEET_V2, devFleetV2Document, validateDevFleetV2, devFleetV2Digest } from '../functions/lib/devFleetCatalog.js';
import {
  LOCAL_DEV_ROUTE_NETWORK_VERSION, contractRouteFromNetwork, devRouteNetworkDigest, devRouteNetworkDocument,
  validateDevRouteNetwork,
} from '../functions/lib/devRouteNetwork.js';
import { LOCAL_DEV_FLEET_VERSION } from '../functions/lib/fleet.js';
import { digest, validateCatalog } from '../functions/lib/logisticsModel.js';
import { configHash, exact, validateConfig } from '../functions/lib/economyModel.js';
import { rewardAmount, validateContractConfig, makeTerms, acceptPayload } from '../functions/lib/contractModel.js';
import { startPayload, claimPayload } from '../functions/lib/deliveryModel.js';
import {
  DISTANCE_PROVENANCE_LEGACY_APPROXIMATE, assertContractRoutesOnNetwork, requireRouteNetwork, validateRouteNetwork,
} from '../functions/lib/routeNetworkModel.js';

const EIGHT = ['izmir', 'istanbul', 'ankara', 'bursa', 'antalya', 'adana', 'diyarbakir', 'trabzon'];
const EXPECTED_FLEET_DIGEST = '2dd718440d2f439de8bbf08c74eed150323fb046f76681d844ff667bf205bd57';
const RN_ROUTES = '/Users/ethemsincar/Desktop/Apps/-Cursor/logisticoreyeni/src/data/routes.ts';

function economy() {
  return validateConfig({
    schemaVersion: 1, version: 'lc_dev_economy_v1', approval: 'synthetic_emulator_only', currency: 'LGC', scale: 2,
    maxBalance: 9999999999, startingBalance: 1500000, maxXp: 1000000, initialXp: 0,
    thresholds: [0, 100, 300, 600, 1000, 1500, 2100, 2800, 3600, 4500], rulesVersion: 'synthetic_v1',
  }, 'lc_dev_economy_v1');
}

function schema1Config(route) {
  const logistics = validateDevFleetV2();
  const e = economy();
  return {
    schemaVersion: 1, version: LOCAL_DEV_CONTRACT_VERSION, approval: 'synthetic_emulator_only',
    logisticsVersion: logistics.version, logisticsHash: digest(logistics),
    economyVersion: e.version, economyHash: configHash(e),
    epochDurationMs: C.epochDurationMs, offerTtlMs: C.offerTtlMs, maxOffers: 1, maxAcceptedPerEpoch: 1,
    routes: [route],
    cargo: [{ id: C.cargoId, unit: 'kg', capability: 'road_freight' }],
    templates: [{
      id: 'lc_dev_job_izmir_istanbul_v1', routeId: route.id, cargoId: C.cargoId,
      quantity: 1000, rewardPerUnit: 1, minLevel: 1,
      truckCapability: 'road_freight', driverCapability: 'road_driver', trailerCapability: null,
    }],
  };
}

test('eight unique cities and 56 directed edges match the legacy RN table', () => {
  const network = validateDevRouteNetwork();
  assert.equal(network.version, LOCAL_DEV_ROUTE_NETWORK_VERSION);
  assert.deepEqual(network.cityIds, EIGHT);
  assert.equal(new Set(network.cityIds).size, 8);
  assert.equal(network.routes.length, 56);
  assert.equal(new Set(network.routes.map(r => r.id)).size, 56);
  const pairs = new Set(network.routes.map(r => [r.originCityId, r.destinationCityId].sort().join('|')));
  assert.equal(pairs.size, 28);
  for (const pair of pairs) {
    const [a, b] = pair.split('|');
    assert.ok(network.routes.some(r => r.originCityId === a && r.destinationCityId === b));
    assert.ok(network.routes.some(r => r.originCityId === b && r.destinationCityId === a));
  }
  assert.equal(network.distanceProvenance, DISTANCE_PROVENANCE_LEGACY_APPROXIMATE);
  assert.equal(network.distanceUnit, 'km');
  const izmirIstanbul = network.routes.find(r => r.id === 'izmir-istanbul');
  assert.equal(izmirIstanbul.distance, 480);
  const rn = readFileSync(RN_ROUTES, 'utf8');
  const parsed = [...rn.matchAll(/route\('([a-z]+)', '([a-z]+)', (\d+), ([0-9.]+), (\d+)\)/g)];
  assert.equal(parsed.length, 56);
  for (const [, from, to, distance, difficulty, toll] of parsed) {
    const row = network.routes.find(r => r.id === `${from}-${to}`);
    assert.ok(row, `missing ${from}-${to}`);
    assert.equal(row.distance, Number(distance));
    assert.equal(row.difficultyMilli, Math.round(Number(difficulty) * 100));
    assert.equal(row.tollCost, Number(toll));
    assert.equal(row.unit, 'km');
    assert.equal(row.verification, 'synthetic_fixture');
  }
});

test('invalid city ids, non-positive distances and duplicate edges are rejected', () => {
  const raw = devRouteNetworkDocument();
  assert.throws(() => validateRouteNetwork(undefined, LOCAL_DEV_ROUTE_NETWORK_VERSION));
  assert.throws(() => validateDevRouteNetwork({ ...raw, cityIds: [...EIGHT, 'unknown'] }));
  assert.throws(() => validateDevRouteNetwork({
    ...raw, routes: raw.routes.map((r, i) => i === 0 ? { ...r, originCityId: 'unknown', id: 'unknown-istanbul' } : r),
  }));
  assert.throws(() => validateDevRouteNetwork({
    ...raw, routes: raw.routes.map((r, i) => i === 0 ? { ...r, distance: 0 } : r),
  }));
  assert.throws(() => validateDevRouteNetwork({
    ...raw, routes: raw.routes.map((r, i) => i === 0 ? { ...r, distance: 1.5 } : r),
  }));
  assert.throws(() => validateDevRouteNetwork({
    ...raw, routes: raw.routes.map((r, i) => i === 1 ? { ...r, id: raw.routes[0].id, originCityId: raw.routes[0].originCityId, destinationCityId: raw.routes[0].destinationCityId } : r),
  }));
  assert.throws(() => validateDevRouteNetwork({ ...raw, distanceProvenance: 'official_road' }));
  assert.throws(() => requireRouteNetwork(undefined, LOCAL_DEV_ROUTE_NETWORK_VERSION, devRouteNetworkDigest()));
});

test('eight-city lc_dev_fleet_v2 digest matches the locked hash; v1 id is unchanged', () => {
  const catalog = validateDevFleetV2();
  assert.equal(catalog.cities.length, 8);
  assert.deepEqual(catalog.cities, EIGHT);
  const digestNow = digest(catalog);
  assert.equal(digestNow, EXPECTED_FLEET_DIGEST);
  assert.equal(devFleetV2Digest(), EXPECTED_FLEET_DIGEST);
  if (digestNow !== EXPECTED_FLEET_DIGEST) assert.fail('fleet digest mismatch');
  assert.equal(LOCAL_DEV_FLEET_VERSION, 'lc_dev_fleet_v1');
  assert.notEqual(LOCAL_DEV_FLEET_V2, LOCAL_DEV_FLEET_VERSION);
  const truck = catalog.assets.find(a => a.id === 'lc_starter_truck');
  const driver = catalog.assets.find(a => a.id === 'lc_starter_driver');
  assert.deepEqual(truck.capabilities, ['road_freight']);
  assert.deepEqual(truck.capacity, { unit: 'kg', amount: 5000 });
  assert.deepEqual(driver.capabilities, ['road_driver']);
  assert.ok(C.quantity <= truck.capacity.amount);
  assert.throws(() => validateDevFleetV2({ ...devFleetV2Document(), version: LOCAL_DEV_FLEET_VERSION }));
});

test('contract templates stay within 1-5 and must copy an authoritative network route', () => {
  const logistics = validateDevFleetV2();
  const e = economy();
  const network = validateDevRouteNetwork();
  const route = contractRouteFromNetwork('izmir-istanbul');
  const config = schema1Config(route);
  const validated = validateContractConfig(config, LOCAL_DEV_CONTRACT_VERSION, logistics, e);
  assertContractRoutesOnNetwork(validated, network);
  assert.equal(validated.routes.length, 1);
  assert.equal(validated.routes[0].distance, 480);
  assert.equal(validated.routes[0].unit, 'km');
  const terms = makeTerms(validated, 0, [{ assetId: 'starter_truck', kind: 'truck' }, { assetId: 'starter_driver', kind: 'driver' }], e);
  assert.equal(terms.reward, rewardAmount(1000, 1, e.maxBalance));
  const unknown = schema1Config({
    id: 'izmir-mars', originCityId: 'izmir', destinationCityId: 'istanbul',
    distance: 480, unit: 'km', verification: 'synthetic_fixture',
  });
  unknown.routes[0].destinationCityId = 'izmir';
  assert.throws(() => validateContractConfig(unknown, LOCAL_DEV_CONTRACT_VERSION, logistics, e));
  const drifted = validateContractConfig(schema1Config({
    id: 'izmir-istanbul', originCityId: 'izmir', destinationCityId: 'istanbul',
    distance: 1, unit: 'synthetic_hop', verification: 'synthetic_fixture',
  }), LOCAL_DEV_CONTRACT_VERSION, logistics, e);
  assert.throws(() => assertContractRoutesOnNetwork(drifted, network));
  const extra = { ...config, maxOffers: 5, templates: Array.from({ length: 6 }, (_, i) => ({ ...config.templates[0], id: `t${i}` })) };
  extra.routes = Array.from({ length: 6 }, (_, i) => ({ ...route, id: i === 0 ? route.id : `extra-${i}` }));
  extra.templates = extra.routes.map((r, i) => ({ ...config.templates[0], id: `t${i}`, routeId: r.id }));
  extra.maxOffers = 5;
  assert.throws(() => validateContractConfig(extra, LOCAL_DEV_CONTRACT_VERSION, logistics, e));
});

test('client cannot supply route distance, reward, xp or duration', () => {
  const base = { protocolVersion: 1, idempotencyKey: 'lc-route02', payload: {} };
  assert.deepEqual(parseCommandRequest(base), { idempotencyKey: 'lc-route02', payload: {} });
  for (const field of ['distance', 'reward', 'xp', 'durationMs', 'routeId', 'originCityId']) {
    assert.throws(() => parseCommandRequest({ ...base, [field]: 1 }));
    assert.throws(() => exact({ [field]: 480 }, ''));
  }
  assert.throws(() => acceptPayload({ contractId: 'safe_id', distance: 480 }));
  assert.throws(() => startPayload({ contractId: 'c', truckId: 't', driverId: 'd', durationMs: 5000 }));
  assert.throws(() => claimPayload({ deliveryId: 'd', reward: 1000 }));
});

test('generate still fails closed outside the emulator; re-pin remains unpublished', async () => {
  const index = readFileSync(fileURLToPath(new URL('../functions/src/index.ts', import.meta.url)), 'utf8');
  assert.doesNotMatch(index, /repinCompanyLogisticsCatalog/);
  assert.doesNotMatch(index, /v2RouteNetworkCatalog/);
  const { generateCompanyContractOffersCommand } = await import('../functions/lib/devContract.js');
  const previous = process.env.GCLOUD_PROJECT;
  process.env.GCLOUD_PROJECT = 'synthetic-nonlocal';
  const unreachable = new Proxy({}, { get() { throw Error('database touched before isolation'); } });
  try {
    assert.throws(
      () => generateCompanyContractOffersCommand(unreachable, { uid: 'synthetic' }, { protocolVersion: 1, idempotencyKey: 'k', payload: {} }),
      /Local isolation/,
    );
  } finally {
    if (previous === undefined) delete process.env.GCLOUD_PROJECT;
    else process.env.GCLOUD_PROJECT = previous;
  }
});
