import test from 'node:test';
import assert from 'node:assert/strict';
import { candidateAssetsDto, contractDto } from '../functions/lib/contractModel.js';
import { startPayload, startSelection } from '../functions/lib/deliveryModel.js';
import { digest } from '../functions/lib/logisticsModel.js';
import { validateSelection } from '../functions/lib/fleet.js';

const truckDriver = [
  { assetId: 'starter_truck', kind: 'truck' },
  { assetId: 'starter_driver', kind: 'driver' },
];
const withTrailer = [
  { assetId: 'starter_truck', kind: 'truck' },
  { assetId: 'fleet_trailer_1', kind: 'trailer' },
  { assetId: 'starter_driver', kind: 'driver' },
];

test('candidateAssetsDto exposes only assetId and kind in canonical order', () => {
  assert.deepEqual(candidateAssetsDto(truckDriver), [
    { assetId: 'starter_driver', kind: 'driver' },
    { assetId: 'starter_truck', kind: 'truck' },
  ]);
  assert.deepEqual(candidateAssetsDto(withTrailer), [
    { assetId: 'starter_driver', kind: 'driver' },
    { assetId: 'fleet_trailer_1', kind: 'trailer' },
    { assetId: 'starter_truck', kind: 'truck' },
  ]);
  assert.deepEqual(candidateAssetsDto(truckDriver), validateSelection(truckDriver));
});

test('corrupt candidateAssets fail closed', () => {
  const bad = [
    [],
    [{ assetId: 'starter_truck', kind: 'truck' }],
    [{ assetId: 'starter_truck', kind: 'truck' }, { assetId: 'starter_truck', kind: 'driver' }],
    [{ assetId: 'starter_truck', kind: 'truck' }, { assetId: 'starter_driver', kind: 'driver' }, { catalogId: 'x' }],
    [{ assetId: 'starter_truck', kind: 'truck' }, { assetId: 'starter_driver', kind: 'driver', assignmentId: 'a' }],
    [{ assetId: '../other', kind: 'truck' }, { assetId: 'starter_driver', kind: 'driver' }],
  ];
  for (const value of bad) assert.throws(() => candidateAssetsDto(value));
});

test('contractDto keeps existing fields and adds candidateAssets', () => {
  const terms = {
    templateId: 'synthetic_template',
    route: { id: 'synthetic_route', originCityId: 'izmir', destinationCityId: 'istanbul', distance: 1, unit: 'km', verification: 'synthetic_fixture' },
    cargo: { id: 'synthetic_cargo', unit: 'kg', capability: 'road_freight' },
    quantity: 2, reward: 6, currency: 'TST', scale: 2, minLevel: 1,
    requirements: { truck: 'synthetic_vehicle', driver: 'synthetic_license', trailer: null },
    candidateAssets: validateSelection(truckDriver),
    configVersion: 'synthetic_fixture', configHash: 'a'.repeat(64),
    logisticsVersion: 'synthetic_fixture', logisticsHash: 'b'.repeat(64),
    economyVersion: 'synthetic_fixture', economyHash: 'c'.repeat(64),
  };
  const dto = contractDto({
    schemaVersion: 1, contractId: 'd'.repeat(64), terms, status: 'offered', epoch: 1, slot: 0,
    createdAt: { toMillis: () => 1 },
    expiresAt: { toMillis: () => 2, toDate: () => new Date('2026-10-20T12:10:00.000Z') },
    acceptedAt: null,
  }, 1);
  const completed = contractDto({
    schemaVersion: 1, contractId: 'd'.repeat(64), terms, status: 'completed', epoch: 1, slot: 0,
    createdAt: { toMillis: () => 1 },
    expiresAt: { toMillis: () => 2, toDate: () => new Date('2026-10-20T12:10:00.000Z') },
    acceptedAt: { toDate: () => new Date('2026-10-20T12:00:00.000Z') },
  }, 1);
  for (const key of [
    'contractId', 'status', 'expired', 'originCityId', 'destinationCityId', 'routeId', 'cargoId',
    'quantity', 'unit', 'reward', 'currency', 'scale', 'requirements', 'candidateAssets',
    'minLevel', 'configVersion', 'logisticsVersion', 'economyVersion', 'expiresAt', 'acceptedAt',
  ]) assert.ok(key in dto, key);
  assert.equal(dto.contractId, 'd'.repeat(64));
  assert.equal(dto.status, 'offered');
  assert.equal(dto.originCityId, 'izmir');
  assert.equal(dto.reward, '6');
  assert.deepEqual(dto.requirements, terms.requirements);
  assert.deepEqual(dto.candidateAssets, candidateAssetsDto(truckDriver));
  assert.equal('configHash' in dto, false);
  assert.equal('termsHash' in dto, false);
  assert.equal('assignmentId' in dto, false);
  assert.deepEqual(completed.candidateAssets, dto.candidateAssets);
  assert.equal(completed.status, 'completed');
  assert.equal(completed.expired, false);
});

test('startDelivery payload still rejects client candidateAssets and requires digest match', () => {
  const ok = startPayload({ contractId: 'offer_ankara_bursa', truckId: 'starter_truck', driverId: 'starter_driver' });
  assert.deepEqual(ok, { contractId: 'offer_ankara_bursa', truckId: 'starter_truck', driverId: 'starter_driver' });
  assert.equal(digest(startSelection(ok)), digest(validateSelection(truckDriver)));
  assert.throws(() => startPayload({
    contractId: 'offer_ankara_bursa', truckId: 'starter_truck', driverId: 'starter_driver',
    candidateAssets: truckDriver,
  }));
  const other = startPayload({ contractId: 'offer_ankara_bursa', truckId: 'other_truck', driverId: 'starter_driver' });
  assert.notEqual(digest(startSelection(other)), digest(validateSelection(truckDriver)));
});
