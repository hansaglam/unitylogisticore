import { invalid } from "./economyModel";
import { digest, LogisticsCatalog, validateCatalog } from "./logisticsModel";

/** Next emulator logistics document id. Never overwrite lc_dev_fleet_v1. */
export const LOCAL_DEV_FLEET_V2 = "lc_dev_fleet_v2";
export const DEV_FLEET_V2_CITIES = [
  "izmir", "istanbul", "ankara", "bursa", "antalya", "adana", "diyarbakir", "trabzon",
] as const;

/** Immutable in-source proposal. Not a Firestore write. */
export function devFleetV2Document(): Record<string, unknown> {
  return {
    schemaVersion: 2,
    version: LOCAL_DEV_FLEET_V2,
    approval: "synthetic_emulator_only",
    assets: [
      { id: "lc_starter_truck", kind: "truck", capabilities: ["road_freight"], capacity: { unit: "kg", amount: 5000 } },
      { id: "lc_starter_driver", kind: "driver", capabilities: ["road_driver"], capacity: null },
    ],
    starter: { packageVersion: "lc_starter_pkg_v1", truck: "lc_starter_truck", driver: "lc_starter_driver" },
    cities: [...DEV_FLEET_V2_CITIES],
    warehouses: [],
  };
}

export function validateDevFleetV2(data: unknown = devFleetV2Document()): LogisticsCatalog {
  const catalog = validateCatalog(data as Parameters<typeof validateCatalog>[0], LOCAL_DEV_FLEET_V2);
  const truck = catalog.assets.find(a => a.id === "lc_starter_truck");
  const driver = catalog.assets.find(a => a.id === "lc_starter_driver");
  if (catalog.schemaVersion !== 2 || catalog.starter.packageVersion !== "lc_starter_pkg_v1" ||
      catalog.starter.truck !== "lc_starter_truck" || catalog.starter.driver !== "lc_starter_driver" ||
      !truck || truck.kind !== "truck" || !truck.capabilities?.includes("road_freight") ||
      truck.capacity?.unit !== "kg" || truck.capacity.amount !== 5000 ||
      !driver || driver.kind !== "driver" || !driver.capabilities?.includes("road_driver") || driver.capacity !== null ||
      catalog.cities.length !== DEV_FLEET_V2_CITIES.length ||
      DEV_FLEET_V2_CITIES.some((id, i) => catalog.cities[i] !== id) || catalog.warehouses.length !== 0) invalid();
  return catalog;
}

export function devFleetV2Digest(): string {
  return digest(validateDevFleetV2());
}
