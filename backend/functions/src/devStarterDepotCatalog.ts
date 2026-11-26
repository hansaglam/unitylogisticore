import { DocumentData } from "firebase-admin/firestore";
import { DEV_FLEET_V2_CITIES, devFleetV2Document } from "./devFleetCatalog";
import { invalid } from "./economyModel";
import { digest, LogisticsCatalog, validateCatalog } from "./logisticsModel";

/**
 * Isolated synthetic emulator proposal, NOT an implicit catalog migration.
 * Storage footprint is a deliberate gameplay unit (24 pallet slots), not a
 * photographic rendering or a claim about real warehouse dimensions.
 * Only the eight legacy KGM distance-verified Turkey cities have templates.
 * They still cannot dispatch freight without separate route authorization.
 */
export const LOCAL_STARTER_DEPOT_CATALOG = "lc_dev_starter_depot_v1";
export const STARTER_DEPOT_TYPE = "starter_depot";
export const STARTER_DEPOT_CAPACITY = "pallet_slots_v1";
export const STARTER_DEPOT_SLOTS = 24;

export function devStarterDepotCatalogDocument(): Record<string, unknown> {
  const existing = devFleetV2Document();
  return {
    ...existing,
    version: LOCAL_STARTER_DEPOT_CATALOG,
    warehouses: DEV_FLEET_V2_CITIES.map(cityId => ({
      id: "starter_depot_" + cityId,
      cityId,
      type: STARTER_DEPOT_TYPE,
      level: 1,
      capacityModelId: STARTER_DEPOT_CAPACITY,
      capacityUnits: STARTER_DEPOT_SLOTS,
    })),
  };
}

/** Require exact canonical semantics, never an arbitrary client catalog. */
export function validateDevStarterDepotCatalog(data: DocumentData | undefined): LogisticsCatalog {
  const expected = validateCatalog(
    devStarterDepotCatalogDocument() as DocumentData, LOCAL_STARTER_DEPOT_CATALOG);
  const actual = validateCatalog(data, LOCAL_STARTER_DEPOT_CATALOG);
  if (digest(actual) !== digest(expected)) invalid();
  return actual;
}

export function supportedStarterDepotCity(countryCode: string, cityKey: string): boolean {
  return countryCode === "TUR" &&
    (DEV_FLEET_V2_CITIES as readonly string[]).includes(cityKey);
}
