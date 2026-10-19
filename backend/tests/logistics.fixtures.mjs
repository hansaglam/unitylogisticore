// Synthetic emulator fixtures only. These values are NOT product defaults.
export const logisticsFixture = version => ({ schemaVersion: 1, version, approval: 'synthetic_emulator_only',
  assets: [{ id: 'test_truck', kind: 'truck' }, { id: 'test_driver', kind: 'driver' }, { id: 'test_trailer', kind: 'trailer' }],
  starter: { packageVersion: 'synthetic_package_v1', truck: 'test_truck', driver: 'test_driver' },
  cities: ['synthetic_city'], warehouses: [{ id: 'test_warehouse', cityId: 'synthetic_city', type: 'synthetic_depot', level: 1,
    capacityModelId: 'synthetic_slots', capacityUnits: 10 }] });
export const economyFixture = version => ({ schemaVersion: 1, version, approval: 'synthetic_emulator_only', currency: 'TST', scale: 2,
  maxBalance: 10000, startingBalance: 100, maxXp: 1000, initialXp: 0, thresholds: [0,10,50], rulesVersion: 'synthetic_v1' });
