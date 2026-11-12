import test from 'node:test';
import assert from 'node:assert/strict';
import { LOCAL_DEV_CONTRACT_VERSION, DEV_FIRST_CONTRACT_CANDIDATES as C } from '../functions/lib/devContractIds.js';
import { validateDevFleetV2 } from '../functions/lib/devFleetCatalog.js';
import {
  LOCAL_DEV_ROUTE_NETWORK_VERSION, contractRouteFromNetwork, devRouteNetworkDigest, devRouteNetworkDocument,
  validateDevRouteNetwork,
} from '../functions/lib/devRouteNetwork.js';
import {
  LOCAL_DEV_ROUTE_NETWORK_V2, contractRouteFromNetworkV2, devRouteNetworkV2Digest, devRouteNetworkV2Document,
  validateDevRouteNetworkV2,
} from '../functions/lib/devRouteNetworkV2.js';
import { digest } from '../functions/lib/logisticsModel.js';
import { configHash, exact, validateConfig } from '../functions/lib/economyModel.js';
import { rewardAmount, validateContractConfig, makeTerms } from '../functions/lib/contractModel.js';
import {
  DISTANCE_PROVENANCE_KGM_IL_MERKEZ_2026_03_03, DISTANCE_PROVENANCE_LEGACY_APPROXIMATE,
  ROUTE_VERIFICATION_KGM_ILMESAFE, assertContractRoutesOnNetwork, requireRouteNetwork, validateRouteNetwork,
} from '../functions/lib/routeNetworkModel.js';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const EIGHT = ['izmir', 'istanbul', 'ankara', 'bursa', 'antalya', 'adana', 'diyarbakir', 'trabzon'];
const KGM = {
  'izmir-istanbul': 566, 'izmir-ankara': 586, 'izmir-bursa': 325, 'izmir-antalya': 447,
  'istanbul-ankara': 453, 'istanbul-bursa': 244, 'istanbul-antalya': 715,
  'ankara-bursa': 385, 'ankara-antalya': 542, 'bursa-antalya': 543,
  'adana-ankara': 491, 'adana-antalya': 550, 'adana-istanbul': 951, 'adana-izmir': 909, 'adana-bursa': 856,
  'adana-diyarbakir': 537, 'adana-trabzon': 832,
  'diyarbakir-ankara': 898, 'diyarbakir-istanbul': 1349, 'diyarbakir-izmir': 1429, 'diyarbakir-bursa': 1270,
  'diyarbakir-antalya': 1087, 'diyarbakir-trabzon': 610,
  'trabzon-ankara': 733, 'trabzon-istanbul': 1057, 'trabzon-izmir': 1328, 'trabzon-bursa': 1065, 'trabzon-antalya': 1224,
};

function economy() {
  return validateConfig({
    schemaVersion: 1, version: 'lc_dev_economy_v1', approval: 'synthetic_emulator_only', currency: 'LGC', scale: 2,
    maxBalance: 9999999999, startingBalance: 1500000, maxXp: 1000000, initialXp: 0,
    thresholds: [0, 100, 300, 600, 1000, 1500, 2100, 2800, 3600, 4500], rulesVersion: 'synthetic_v1',
  }, 'lc_dev_economy_v1');
}

function schema1(route) {
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
      quantity: C.quantity, rewardPerUnit: C.rewardPerUnit, minLevel: C.minLevel,
      truckCapability: C.truckCapability, driverCapability: C.driverCapability, trailerCapability: null,
    }],
  };
}

test('KGM v2 has 8 cities, 28 official pairs, 56 directed ids matching v1', () => {
  const v1 = validateDevRouteNetwork();
  const v2 = validateDevRouteNetworkV2();
  assert.equal(v1.version, LOCAL_DEV_ROUTE_NETWORK_VERSION);
  assert.equal(v2.version, LOCAL_DEV_ROUTE_NETWORK_V2);
  assert.deepEqual(v2.cityIds, EIGHT);
  assert.equal(v2.routes.length, 56);
  assert.equal(new Set(v2.routes.map(r => r.id)).size, 56);
  assert.deepEqual(new Set(v2.routes.map(r => r.id)), new Set(v1.routes.map(r => r.id)));
  assert.equal(v2.distanceProvenance, DISTANCE_PROVENANCE_KGM_IL_MERKEZ_2026_03_03);
  assert.equal(v1.distanceProvenance, DISTANCE_PROVENANCE_LEGACY_APPROXIMATE);
  const pairs = new Set(v2.routes.map(r => [r.originCityId, r.destinationCityId].sort().join('|')));
  assert.equal(pairs.size, 28);
  for (const [id, km] of Object.entries(KGM)) {
    const [a, b] = id.split('-');
    const forward = v2.routes.find(r => r.id === `${a}-${b}`);
    const reverse = v2.routes.find(r => r.id === `${b}-${a}`);
    assert.equal(forward.distance, km);
    assert.equal(reverse.distance, km);
    assert.equal(forward.verification, ROUTE_VERIFICATION_KGM_ILMESAFE);
  }
  assert.equal(v2.routes.find(r => r.id === 'izmir-istanbul').distance, 566);
  assert.equal(v1.routes.find(r => r.id === 'izmir-istanbul').distance, 480);
  const workbook = readFileSync('/tmp/logisticore-contract-01b/kgm-source/ilmesafe.xlsx');
  assert.equal(createHash('sha256').update(workbook).digest('hex'),
    '9202ff47d216e8d2c59cff9356ed141733b60a432e41dc7fa4be113b20be1062');
  const directed = JSON.parse(readFileSync('/tmp/logisticore-contract-01b/kgm-source/directed.json', 'utf8'));
  assert.equal(directed.length, 56);
  for (const [from, to, km] of directed) {
    const row = v2.routes.find(r => r.id === `${from}-${to}`);
    assert.equal(row.distance, km);
    assert.equal(row.unit, 'km');
    const v1row = v1.routes.find(r => r.id === `${from}-${to}`);
    assert.equal(row.difficultyMilli, v1row.difficultyMilli);
    assert.equal(row.tollCost, v1row.tollCost);
  }
  assert.equal(devRouteNetworkV2Digest(), digest(v2));
  assert.notEqual(devRouteNetworkV2Digest(), devRouteNetworkDigest());
});

test('official provenance is closed; missing or mismatched v2 pin fails closed without v1 fallback', () => {
  const raw = devRouteNetworkV2Document();
  assert.throws(() => validateDevRouteNetworkV2({ ...raw, distanceProvenance: DISTANCE_PROVENANCE_LEGACY_APPROXIMATE }));
  assert.throws(() => validateDevRouteNetworkV2({ ...raw, distanceProvenance: 'official_road' }));
  assert.throws(() => validateDevRouteNetworkV2({ ...raw, distanceProvenance: 'kgm_ilmesafe' }));
  assert.throws(() => validateRouteNetwork(undefined, LOCAL_DEV_ROUTE_NETWORK_V2));
  assert.throws(() => validateDevRouteNetworkV2({ ...raw, distanceProvenance: undefined }));
  assert.throws(() => validateDevRouteNetworkV2({
    ...raw, routes: raw.routes.map((r, i) => i === 0 ? { ...r, verification: 'synthetic_fixture' } : r),
  }));
  assert.throws(() => validateDevRouteNetworkV2({
    ...raw, routes: raw.routes.map((r, i) => i === 0 ? { ...r, verification: 'official_road' } : r),
  }));
  const missingVerification = JSON.parse(JSON.stringify(raw));
  delete missingVerification.routes[0].verification;
  assert.throws(() => validateDevRouteNetworkV2(missingVerification));
  const missingProvenance = JSON.parse(JSON.stringify(raw));
  delete missingProvenance.distanceProvenance;
  assert.throws(() => validateDevRouteNetworkV2(missingProvenance));
  assert.throws(() => validateDevRouteNetworkV2({
    ...raw, routes: raw.routes.map((r, i) => r.id === 'istanbul-izmir' ? { ...r, distance: 480 } : r),
  }));
  assert.throws(() => requireRouteNetwork(undefined, LOCAL_DEV_ROUTE_NETWORK_V2, devRouteNetworkV2Digest()));
  assert.throws(() => requireRouteNetwork(devRouteNetworkDocument(), LOCAL_DEV_ROUTE_NETWORK_V2, devRouteNetworkV2Digest()));
  assert.throws(() => requireRouteNetwork(devRouteNetworkV2Document(), LOCAL_DEV_ROUTE_NETWORK_VERSION, devRouteNetworkDigest()));
  requireRouteNetwork(devRouteNetworkV2Document(), LOCAL_DEV_ROUTE_NETWORK_V2, devRouteNetworkV2Digest());
});

test('lc_dev_contract_v1 must copy v2 izmir-istanbul 566; 480 and template overflow fail', () => {
  const logistics = validateDevFleetV2();
  const e = economy();
  const v2 = validateDevRouteNetworkV2();
  const official = contractRouteFromNetworkV2('izmir-istanbul');
  assert.equal(official.distance, 566);
  const validated = validateContractConfig(schema1(official), LOCAL_DEV_CONTRACT_VERSION, logistics, e);
  assertContractRoutesOnNetwork(validated, v2);
  const terms = makeTerms(validated, 0, [{ assetId: 'starter_truck', kind: 'truck' }, { assetId: 'starter_driver', kind: 'driver' }], e);
  assert.equal(terms.reward, rewardAmount(1000, 1, e.maxBalance));
  assert.equal(terms.reward, 1000);
  const legacy = contractRouteFromNetwork('izmir-istanbul');
  assert.equal(legacy.distance, 480);
  assert.throws(() => assertContractRoutesOnNetwork(
    validateContractConfig(schema1(legacy), LOCAL_DEV_CONTRACT_VERSION, logistics, e), v2,
  ));
  const extra = schema1(official);
  extra.maxOffers = 5;
  extra.routes = Array.from({ length: 6 }, (_, i) => ({ ...official, id: i === 0 ? official.id : `extra-${i}` }));
  extra.templates = extra.routes.map((r, i) => ({ ...extra.templates[0], id: `t${i}`, routeId: r.id }));
  assert.throws(() => validateContractConfig(extra, LOCAL_DEV_CONTRACT_VERSION, logistics, e));
  const truck = logistics.assets.find(a => a.id === 'lc_starter_truck');
  assert.ok(C.quantity <= truck.capacity.amount);
  assert.equal(C.quantity, 1000);
  assert.equal(C.rewardPerUnit, 1);
  assert.equal(C.durationMs, 5000);
  assert.equal(C.xp, 1);
  const wrongVerification = schema1({ ...official, verification: 'synthetic_fixture' });
  assert.throws(() => assertContractRoutesOnNetwork(
    validateContractConfig(wrongVerification, LOCAL_DEV_CONTRACT_VERSION, logistics, e), v2,
  ));
});

test('generate still pins server-owned v2 in source; re-pin remains unpublished', async () => {
  const contracts = readFileSync(fileURLToPath(new URL('../functions/src/contracts.ts', import.meta.url)), 'utf8');
  assert.match(contracts, /LOCAL_DEV_ROUTE_NETWORK_V2/);
  assert.match(contracts, /devRouteNetworkV2Digest/);
  assert.doesNotMatch(contracts, /devRouteNetworkDigest\(\)/);
  assert.doesNotMatch(contracts, /networkVersion/);
  const index = readFileSync(fileURLToPath(new URL('../functions/src/index.ts', import.meta.url)), 'utf8');
  assert.match(index, /generateCompanyContractOffers/);
  assert.doesNotMatch(index, /repinCompanyLogisticsCatalog/);
  const { parseCommandRequest } = await import('../functions/lib/commands.js');
  const base = { protocolVersion: 1, idempotencyKey: 'lc-contract-01c', payload: {} };
  assert.deepEqual(parseCommandRequest(base), { idempotencyKey: 'lc-contract-01c', payload: {} });
  assert.throws(() => exact({ networkVersion: LOCAL_DEV_ROUTE_NETWORK_V2 }, ''));
  assert.throws(() => exact({ networkVersion: LOCAL_DEV_ROUTE_NETWORK_VERSION }, ''));
});
