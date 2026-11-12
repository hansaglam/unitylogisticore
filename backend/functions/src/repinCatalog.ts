import { DocumentData, FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { executeCommand } from "./commands";
import { readFoundation } from "./company";
import { LOCAL_DEV_FLEET_V2, validateDevFleetV2 } from "./devFleetCatalog";
import { exact, invalid } from "./economyModel";
import { LOCAL_DEV_FLEET_VERSION, readStarter, STARTER_DRIVER, STARTER_TRUCK } from "./fleet";
import { CompanyState, catalogReader } from "./logisticsContext";
import { digest, validateAsset } from "./logisticsModel";
import { assertLocalEnvironment } from "./localEnvironment";

export const LOGISTICS_REPIN_MARKER = "logisticsCatalogRepin";

export async function assertLogisticsRepinComplete(tx: Transaction, db: Firestore, state: CompanyState) {
  assertLocalEnvironment(process.env, true);
  const starter = await readStarter(tx, db, state);
  if (!starter.ready) invalid();
  const expected = validateDevFleetV2();
  const toHash = digest(expected);
  const marker = (await tx.get(state.companyRef.collection("provisioning").doc(LOGISTICS_REPIN_MARKER))).data();
  exact(marker, "schemaVersion,companyId,fromVersion,fromHash,toVersion,toHash,packageVersion,committedAt");
  if (marker.schemaVersion !== 1 || marker.companyId !== state.companyId ||
      marker.fromVersion !== LOCAL_DEV_FLEET_VERSION || marker.toVersion !== LOCAL_DEV_FLEET_V2 ||
      marker.toHash !== toHash || marker.packageVersion !== "lc_starter_pkg_v1" ||
      typeof marker.fromHash !== "string" || !/^[a-f0-9]{64}$/.test(marker.fromHash) ||
      !(marker.committedAt instanceof Timestamp)) invalid();
  const live = await catalogReader(tx, db)(LOCAL_DEV_FLEET_V2);
  if (digest(live) !== toHash) invalid();
  for (const [ref, kind, catalogId] of [
    [starter.truckRef, "truck", "lc_starter_truck"],
    [starter.driverRef, "driver", "lc_starter_driver"],
  ] as const) {
    const asset = validateAsset((await tx.get(ref)).data(), ref.id, state.companyId, live);
    if (asset.kind !== kind || asset.catalogId !== catalogId || asset.catalogVersion !== LOCAL_DEV_FLEET_V2) invalid();
  }
}

/** Internal LOCAL command. Not exported from index.ts. Never auto-invoked. */
export function repinCompanyLogisticsCatalogCommand(
  db: Firestore, auth: { uid: string } | undefined, data: unknown,
) {
  assertLocalEnvironment(process.env, true);
  const expected = validateDevFleetV2();
  const toHash = digest(expected);
  const states = new WeakMap<Transaction, CompanyState>();
  return executeCommand(db, auth, data, {
    name: "repinCompanyLogisticsCatalog",
    validate(payload) {
      exact(payload as DocumentData, "");
      return { fromVersion: LOCAL_DEV_FLEET_VERSION, toVersion: LOCAL_DEV_FLEET_V2 };
    },
    async resolveCompany(tx, uid) {
      const state = await readFoundation(tx, db, uid);
      if (!state.economy) invalid();
      states.set(tx, state);
      return state.companyId;
    },
    async apply(tx) {
      const state = states.get(tx)!;
      const starter = await readStarter(tx, db, state);
      if (!starter.ready) invalid();
      const fromCatalog = await catalogReader(tx, db)(LOCAL_DEV_FLEET_VERSION);
      const fromHash = digest(fromCatalog);
      const toCatalog = await catalogReader(tx, db)(LOCAL_DEV_FLEET_V2);
      if (digest(toCatalog) !== toHash) invalid();
      if (toCatalog.starter.packageVersion !== "lc_starter_pkg_v1" ||
          fromCatalog.starter.packageVersion !== "lc_starter_pkg_v1") invalid();
      const truck = validateAsset((await tx.get(starter.truckRef)).data(), STARTER_TRUCK, state.companyId, fromCatalog);
      const driver = validateAsset((await tx.get(starter.driverRef)).data(), STARTER_DRIVER, state.companyId, fromCatalog);
      if (truck.kind !== "truck" || driver.kind !== "driver" || !truck.available || !driver.available ||
          truck.catalogId !== "lc_starter_truck" || driver.catalogId !== "lc_starter_driver") invalid();
      const markerRef = state.companyRef.collection("provisioning").doc(LOGISTICS_REPIN_MARKER);
      if ((await tx.get(markerRef)).exists) invalid();
      const marker = (await tx.get(starter.markerRef)).data();
      if (marker?.catalogVersion !== LOCAL_DEV_FLEET_VERSION || marker.catalogHash !== fromHash) invalid();
      tx.update(starter.truckRef, { catalogVersion: LOCAL_DEV_FLEET_V2, catalogHash: toHash });
      tx.update(starter.driverRef, { catalogVersion: LOCAL_DEV_FLEET_V2, catalogHash: toHash });
      tx.update(starter.markerRef, { catalogVersion: LOCAL_DEV_FLEET_V2, catalogHash: toHash });
      tx.create(markerRef, {
        schemaVersion: 1, companyId: state.companyId, fromVersion: LOCAL_DEV_FLEET_VERSION, fromHash,
        toVersion: LOCAL_DEV_FLEET_V2, toHash, packageVersion: "lc_starter_pkg_v1", committedAt: FieldValue.serverTimestamp(),
      });
      return {
        repinReady: true, fromVersion: LOCAL_DEV_FLEET_VERSION, toVersion: LOCAL_DEV_FLEET_V2,
        truckId: STARTER_TRUCK, driverId: STARTER_DRIVER, packageVersion: "lc_starter_pkg_v1",
      };
    },
  });
}
