/** Server-owned contract catalog id for a future 04C seed. Distance/unit are not frozen in source. */
export const LOCAL_DEV_CONTRACT_VERSION = "lc_dev_contract_v1";

/** Emulator-only candidates. Not production defaults. Route distance/unit remain OWNER HOLD. */
export const DEV_FIRST_CONTRACT_CANDIDATES = {
  originCityId: "izmir",
  destinationCityId: "istanbul",
  cargoId: "lc_dev_general_freight_v1",
  quantity: 1000,
  unit: "kg",
  cargoCapability: "road_freight",
  truckCapability: "road_freight",
  driverCapability: "road_driver",
  rewardPerUnit: 1,
  expectedReward: 1000,
  xp: 1,
  durationMs: 5000,
  epochDurationMs: 3600000,
  offerTtlMs: 3600000,
  minLevel: 1,
} as const;
