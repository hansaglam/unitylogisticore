import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { Timestamp } from "firebase-admin/firestore";
import {
  LOCAL_STARTER_DEPOT_CATALOG, STARTER_DEPOT_CAPACITY,
  STARTER_DEPOT_SLOTS, STARTER_DEPOT_TYPE,
  devStarterDepotCatalogDocument, supportedStarterDepotCity,
  validateDevStarterDepotCatalog,
} from "../functions/lib/devStarterDepotCatalog.js";
import {
  DEPOT_ACTIVATED_STATUS, DEPOT_ACTIVATION_DOCUMENT,
} from "../functions/lib/starterDepotActivation.js";
import { digest, validateWarehouse, warehouseId } from "../functions/lib/logisticsModel.js";

const codeIs = code => error => error.details?.code === code;

test("depot catalog is a new immutable eight-city source without changing prior fleet fixtures", () => {
  const raw = devStarterDepotCatalogDocument();
  const valid = validateDevStarterDepotCatalog(raw);
  assert.equal(raw.schemaVersion, 2);
  assert.equal(valid.version, LOCAL_STARTER_DEPOT_CATALOG);
  assert.equal(valid.warehouses.length, 8);
  assert.equal(valid.assets.length, 2);
  assert.deepEqual(valid.cities, [
    "izmir", "istanbul", "ankara", "bursa",
    "antalya", "adana", "diyarbakir", "trabzon",
  ]);
  for (const city of valid.cities) {
    assert.ok(supportedStarterDepotCity("TUR", city));
    const warehouse = valid.warehouses.find(w => w.cityId === city);
    assert.equal(warehouse.id, "starter_depot_" + city);
    assert.equal(warehouse.type, STARTER_DEPOT_TYPE);
    assert.equal(warehouse.level, 1);
    assert.equal(warehouse.capacityModelId, STARTER_DEPOT_CAPACITY);
    assert.equal(warehouse.capacityUnits, STARTER_DEPOT_SLOTS);
  }
  for (const [country, city] of [
    ["DEU", "berlin"], ["FRA", "paris"], ["TUR", "edirne"],
    ["USA", "ankara"], ["TUR", "not_on_map"],
  ])
    assert.equal(supportedStarterDepotCity(country, city), false);
  assert.equal(DEPOT_ACTIVATED_STATUS, "warehouse_operational_dispatch_pending_routes");
  assert.equal(DEPOT_ACTIVATION_DOCUMENT, "starterDepotActivation");

  const assetMarker = readFileSync(fileURLToPath(
    new URL("../functions/src/devFleetCatalog.ts", import.meta.url)), "utf8");
  assert.match(assetMarker, /warehouses: \[\]/);
});

test("forged capacity, extra warehouses, cross-country data or active dispatch fail exact catalog check", () => {
  const base = devStarterDepotCatalogDocument();
  const cloned = () => structuredClone(base);
  for (const patch of [
    { capacityUnits: 2400 }, { capacityModelId: "infinite_space" },
    { type: "international_super_hub" }, { level: 9 },
    { cityId: "berlin" }, { id: "faked_berlin_warehouse" },
  ]) {
    const altered = cloned();
    altered.warehouses[0] = { ...altered.warehouses[0], ...patch };
    assert.throws(() => validateDevStarterDepotCatalog(altered), codeIs("FAILED_PRECONDITION"));
  }
  for (const change of [
    { approval: "production" }, { schemaVersion: 1 },
    { version: "lc_dev_fleet_v2" },
  ]) {
    assert.throws(() => validateDevStarterDepotCatalog({ ...base, ...change }));
  }
  const copied = cloned();
  copied.warehouses.push({ ...copied.warehouses[0], id: "duplicated" });
  assert.throws(() => validateDevStarterDepotCatalog(copied));
});

test("new warehouse record matches the existing authoritative warehouse validator", () => {
  const catalog = validateDevStarterDepotCatalog(devStarterDepotCatalogDocument());
  const definition = catalog.warehouses.find(w => w.cityId === "ankara");
  const id = warehouseId("ankara", STARTER_DEPOT_TYPE);
  const data = {
    schemaVersion: 1, warehouseId: id, companyId: "co_synthetic",
    catalogId: definition.id, catalogVersion: catalog.version,
    catalogHash: digest(catalog),
    cityId: "ankara", type: definition.type, level: 1,
    status: "operational", capacityModelId: STARTER_DEPOT_CAPACITY,
    usedCapacity: 0, acquiredAt: Timestamp.now(),
  };
  const verified = validateWarehouse(data, id, "co_synthetic", catalog);
  assert.equal(verified.capacityUnits, "24");
  assert.equal(verified.cityId, "ankara");
  for (const changed of [
    { cityId: "istanbul" }, { usedCapacity: 25 },
    { capacityModelId: "unlimited" }, { level: 5 },
    { status: "fantasy_active" }, { companyId: "other" },
    { catalogVersion: "unknown" },
  ])
    assert.throws(() => validateWarehouse(
      { ...data, ...changed }, id, "co_synthetic", catalog));
});

test("starter depot remains a non-payable local-only command and never grants route dispatch", () => {
  const source = readFileSync(fileURLToPath(
    new URL("../functions/src/starterDepotActivation.ts", import.meta.url)), "utf8");
  assert.match(source, /dispatchReady: false/);
  assert.match(source, /exact\(payload as DocumentData, ""\)/);
  assert.match(source, /readStarter\(tx, db, foundation\)/);
  assert.match(source, /validateDevStarterDepotCatalog/);
  assert.doesNotMatch(source, /planEconomyMutation/);
  assert.doesNotMatch(source, /getMyContracts|startDeliveryCommand/);
});
