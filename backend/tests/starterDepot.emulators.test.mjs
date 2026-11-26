import test from "node:test";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { assertLocalEnvironment } from "../functions/lib/localEnvironment.js";

// Emulator only. Do not run against a real Firebase project.
assertLocalEnvironment(process.env);

const { createLocalFirestore, initializeIdentity } = await import("../functions/lib/identity.js");
const { companyIdForUid } = await import("../functions/lib/company.js");
const { createCompanyWithHeadquartersCommand } = await import("../functions/lib/headquarters.js");
const { runEconomyCommand, operationId } = await import("../functions/lib/economy.js");
const { provisionStarterFleet } = await import("../functions/lib/fleet.js");
const { devFleetV2Document } = await import("../functions/lib/devFleetCatalog.js");
const {
  devStarterDepotCatalogDocument, LOCAL_STARTER_DEPOT_CATALOG,
  STARTER_DEPOT_TYPE,
} = await import("../functions/lib/devStarterDepotCatalog.js");
const {
  activateHeadquartersStarterDepotCommand, readMyStarterDepotProgress,
} = await import("../functions/lib/starterDepotActivation.js");
const { readMyWarehouseState } = await import("../functions/lib/logisticsReads.js");
const { warehouseId } = await import("../functions/lib/logisticsModel.js");

const readRequest = { protocolVersion: 1 };
const command = (id, payload = {}) => ({ protocolVersion: 1, idempotencyKey: id, payload });
const stage = async (db, auth) =>
  (await readMyStarterDepotProgress(db, auth, readRequest)).result.stage;

test("START-DEPOT-02: real emulator warehouse creation requires HQ, economy and fleet", {
  timeout: 180000,
}, async () => {
  const previousRuntime = process.env.FUNCTIONS_EMULATOR;
  process.env.FUNCTIONS_EMULATOR = "true";
  const db = createLocalFirestore();
  const uid = "synthetic-starter-depot-" + randomUUID();
  const auth = { uid };
  const companyId = companyIdForUid(uid);
  const root = "v2Companies/" + companyId;
  const economyVersion = "synthetic_economy_" + randomUUID().replaceAll("-", "");
  const fleetVersion = "synthetic_fleet_" + randomUUID().replaceAll("-", "");
  const receiptKeys = ["create", "economy", "fleet", "activate", "duplicate", "not-ready"];
  const tracked = [];
  const own = path => {
    const ref = db.doc(path);
    tracked.push(ref);
    return ref;
  };

  const player = own("v2Players/" + uid);
  const company = own(root);
  const hq = own(root + "/state/headquarters");
  const site = own(root + "/state/starterDepotSite");
  const activation = own(root + "/state/starterDepotActivation");
  const depotId = warehouseId("ankara", STARTER_DEPOT_TYPE);
  const warehouse = own(root + "/warehouses/" + depotId);
  const economy = own("v2WorldConfig/" + economyVersion);
  const finance = own(root + "/state/finance");
  const progression = own(root + "/state/progression");
  const ledger = own(root + "/ledger/" + operationId(companyId, "initialization"));
  const claim = own(root + "/claims/initialization");
  const fleetCatalog = own("v2LogisticsCatalog/" + fleetVersion);
  const depotCatalog = own("v2LogisticsCatalog/" + LOCAL_STARTER_DEPOT_CATALOG);
  own(root + "/provisioning/starterFleet");
  own(root + "/assets/starter_truck");
  own(root + "/assets/starter_driver");
  for (const key of receiptKeys)
    own("v2Players/" + uid + "/commands/" + key);

  try {
    await initializeIdentity(db, auth, readRequest);
    assert.equal(await stage(db, auth), "no_company");
    await createCompanyWithHeadquartersCommand(db, auth, command("create", {
      name: "Synthetic Regional Logistics",
      headquarters: { countryCode: "TUR", cityKey: "ankara" },
    }));
    assert.equal(await stage(db, auth), "awaiting_economy");
    await assert.rejects(
      activateHeadquartersStarterDepotCommand(db, auth, command("not-ready")),
      error => error.details?.code === "FAILED_PRECONDITION");
    assert.equal((await warehouse.get()).exists, false);

    await economy.set({
      schemaVersion: 1, version: economyVersion,
      approval: "synthetic_emulator_only",
      currency: "LGC", scale: 2, maxBalance: 9999999999,
      startingBalance: 1500000, maxXp: 1000000, initialXp: 0,
      thresholds: [0, 100, 300, 600], rulesVersion: "synthetic_v1",
    });
    await runEconomyCommand(db, auth, command("economy"), {
      kind: "initialize", id: economyVersion,
    });
    assert.equal(await stage(db, auth), "awaiting_starter_fleet");

    await fleetCatalog.set({ ...devFleetV2Document(), version: fleetVersion });
    await provisionStarterFleet(db, auth, command("fleet"), fleetVersion);
    assert.equal(await stage(db, auth), "awaiting_warehouse_catalog");

    const cashBefore = (await finance.get()).data();
    const financeVersionBefore = (await finance.get()).updateTime.toMillis();
    await depotCatalog.set(devStarterDepotCatalogDocument());
    assert.equal(await stage(db, auth), "ready_to_activate");
    await assert.rejects(activateHeadquartersStarterDepotCommand(
      db, auth, command("bad-payload", { cityId: "berlin", capacityUnits: 9000 })));
    assert.equal((await warehouse.get()).exists, false);

    const created = await activateHeadquartersStarterDepotCommand(
      db, auth, command("activate"));
    assert.equal(created.result.cityKey, "ankara");
    assert.equal(created.result.capacityUnits, 24);
    assert.equal(created.result.dispatchReady, false);
    assert.equal(created.result.stage, "warehouse_operational_dispatch_pending_routes");

    const trusted = (await readMyStarterDepotProgress(db, auth, readRequest)).result;
    assert.equal(trusted.hasCompany, true);
    assert.equal(trusted.hasHeadquarters, true);
    assert.equal(trusted.hasStarterSite, true);
    assert.equal(trusted.economyReady, true);
    assert.equal(trusted.starterFleetReady, true);
    assert.equal(trusted.warehouseReady, true);
    assert.equal(trusted.dispatchReady, false);
    assert.equal(trusted.warehouse.capacityUnits, 24);
    assert.equal(trusted.warehouse.warehouseId, depotId);
    assert.equal((await activation.get()).data().ownerUid, uid);
    assert.equal((await warehouse.get()).data().usedCapacity, 0);
    assert.equal((await warehouse.get()).data().cityId, "ankara");
    assert.equal((await site.get()).data().status, "site_reserved"); // Immutable site.
    assert.equal((await hq.get()).data().cityKey, "ankara");

    const warehouseList = (await readMyWarehouseState(
      db, auth, { protocolVersion: 1, limit: 20 })).result;
    assert.equal(warehouseList.items.length, 1);
    assert.equal(warehouseList.items[0].cityId, "ankara");
    assert.equal(warehouseList.items[0].capacityUnits, "24");

    assert.deepEqual(await activateHeadquartersStarterDepotCommand(
      db, auth, command("activate")), created);
    await assert.rejects(activateHeadquartersStarterDepotCommand(
      db, auth, command("duplicate")), error =>
      error.details?.code === "FAILED_PRECONDITION");
    assert.equal((await warehouse.get()).data().cityId, "ankara");
    assert.deepEqual((await finance.get()).data(), cashBefore);
    assert.equal((await finance.get()).updateTime.toMillis(), financeVersionBefore);

    const correct = (await warehouse.get()).data();
    await warehouse.update({ usedCapacity: 25 });
    await assert.rejects(readMyStarterDepotProgress(db, auth, readRequest));
    await warehouse.set(correct);
    assert.equal(await stage(db, auth), "warehouse_operational_dispatch_pending_routes");

    await assert.rejects(activateHeadquartersStarterDepotCommand(
      db, undefined, command("anonymous")));
  } finally {
    const all = await Promise.allSettled(tracked.map(ref => ref.delete()));
    await db.terminate();
    if (previousRuntime === undefined) delete process.env.FUNCTIONS_EMULATOR;
    else process.env.FUNCTIONS_EMULATOR = previousRuntime;
    for (const item of all)
      assert.equal(item.status, "fulfilled");
  }
});
