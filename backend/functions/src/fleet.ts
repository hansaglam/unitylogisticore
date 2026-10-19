import { FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { exact, invalid, versionId } from "./economyModel";
import { catalogReader, CompanyState, logisticsCommand } from "./logisticsContext";
import { assertLocalEnvironment } from "./localEnvironment";
import { AssetKind, assetKind, digest, validateAsset } from "./logisticsModel";

export const STARTER_TRUCK = "starter_truck";
export const STARTER_DRIVER = "starter_driver";
/** Absence is pending; any partial or inconsistent package is an error, including on replay. */
export async function readStarter(tx: Transaction, db: Firestore, state: CompanyState, getCatalog = catalogReader(tx, db)) {
  assertLocalEnvironment(process.env, true);
  const markerRef = state.companyRef.collection("provisioning").doc("starterFleet");
  const truckRef = state.companyRef.collection("assets").doc(STARTER_TRUCK);
  const driverRef = state.companyRef.collection("assets").doc(STARTER_DRIVER);
  const [marker, truck, driver] = await tx.getAll(markerRef, truckRef, driverRef);
  if (!marker!.exists) {
    if (truck!.exists || driver!.exists) invalid();
    return { ready: false, markerRef, truckRef, driverRef };
  }
  if (!state.economy) invalid();
  const data = marker!.data();
  exact(data, "schemaVersion,companyId,packageVersion,catalogVersion,catalogHash,truckId,driverId,committedAt");
  const catalog = await getCatalog(data.catalogVersion);
  if (data.schemaVersion !== 1 || data.companyId !== state.companyId || data.packageVersion !== catalog.starter.packageVersion ||
      data.catalogHash !== digest(catalog) || data.truckId !== STARTER_TRUCK || data.driverId !== STARTER_DRIVER || !(data.committedAt instanceof Timestamp)) invalid();
  const truckData = validateAsset(truck!.data(), STARTER_TRUCK, state.companyId, catalog);
  const driverData = validateAsset(driver!.data(), STARTER_DRIVER, state.companyId, catalog);
  if (truckData.kind !== "truck" || driverData.kind !== "driver" || truckData.catalogId !== catalog.starter.truck || driverData.catalogId !== catalog.starter.driver) invalid();
  return { ready: true, markerRef, truckRef, driverRef };
}
export function provisionStarterFleet(db: Firestore, auth: { uid: string } | undefined, data: unknown, catalogVersion: string) {
  const version = versionId(catalogVersion);
  const snapshots = new WeakMap<Transaction, Awaited<ReturnType<typeof readStarter>>>();
  return logisticsCommand(db, auth, data, "provisionStarterFleet", { catalogVersion: version }, async (tx, state) => {
    snapshots.set(tx, await readStarter(tx, db, state));
  }, async (tx, state) => {
    const starter = snapshots.get(tx)!;
    if (starter.ready) invalid();
    const catalog = await catalogReader(tx, db)(version);
    const timestamp = FieldValue.serverTimestamp();
    for (const [ref, kind, catalogId] of [[starter.truckRef, "truck", catalog.starter.truck], [starter.driverRef, "driver", catalog.starter.driver]] as const) {
      tx.create(ref, { schemaVersion: 1, assetId: ref.id, kind, companyId: state.companyId, catalogId,
        catalogVersion: version, catalogHash: digest(catalog), status: "operational", assignmentId: null, acquiredAt: timestamp });
    }
    tx.create(starter.markerRef, { schemaVersion: 1, companyId: state.companyId, packageVersion: catalog.starter.packageVersion,
      catalogVersion: version, catalogHash: digest(catalog), truckId: STARTER_TRUCK, driverId: STARTER_DRIVER, committedAt: timestamp });
    return { starterFleetReady: true, truckId: STARTER_TRUCK, driverId: STARTER_DRIVER, packageVersion: catalog.starter.packageVersion };
  });
}
export type Selection = { assetId: string; kind: AssetKind }[];
export function validateSelection(selection: Selection): Selection {
  if (!Array.isArray(selection) || selection.length < 2 || selection.length > 3) invalid();
  const entries = selection.map(entry => {
    exact(entry, "assetId,kind"); return { assetId: versionId(entry.assetId), kind: assetKind(entry.kind) };
  }).sort((a,b) => a.kind.localeCompare(b.kind));
  if (new Set(entries.map(e => e.assetId)).size !== entries.length ||
      entries.map(e => e.kind).join(",") !== (entries.length === 2 ? "driver,truck" : "driver,trailer,truck")) invalid();
  return entries;
}
/** Stable backend operation source, not a client delivery ID; never reuse after release. */
export function assignmentId(companyId: string, source: string): string { return digest({ companyId, source: versionId(source) }); }
/** All reads/preconditions finish before the first write. Reusable by a future domain transaction.
 * state must come from readFoundation in THIS transaction and trusted Auth context.
 */
export async function prepareAssignment(tx: Transaction, db: Firestore, state: CompanyState, selection: Selection,
  source: string, action: "reserve" | "release") {
  assertLocalEnvironment(process.env, true);
  if (!state.economy || !["reserve", "release"].includes(action)) invalid();
  const entries = validateSelection(selection);
  const id = assignmentId(state.companyId, source);
  const assignmentRef = state.companyRef.collection("assignments").doc(id);
  const refs = entries.map(e => state.companyRef.collection("assets").doc(e.assetId));
  const [assignment, ...assets] = await tx.getAll(assignmentRef, ...refs);
  if (action === "reserve") {
    if (assignment!.exists) invalid();
  } else {
    const existing = assignment!.data();
    exact(existing, "schemaVersion,assignmentId,companyId,assets,status,createdAt");
    if (existing.schemaVersion !== 1 || existing.assignmentId !== id || existing.companyId !== state.companyId ||
        existing.status !== "active" || !(existing.createdAt instanceof Timestamp) || digest(existing.assets) !== digest(entries)) invalid();
  }
  const getCatalog = catalogReader(tx, db);
  for (let i = 0; i < entries.length; i++) {
    const data = assets[i]!.data();
    const asset = validateAsset(data, entries[i]!.assetId, state.companyId, await getCatalog(data?.catalogVersion));
    if (asset.kind !== entries[i]!.kind || (action === "reserve" ? !asset.available : asset.assignmentId !== id)) invalid();
  }
  return { assignmentId: id, apply: () => {
  for (const ref of refs) tx.update(ref, { assignmentId: action === "reserve" ? id : null });
  if (action === "reserve") tx.create(assignmentRef, { schemaVersion: 1, assignmentId: id, companyId: state.companyId,
    assets: entries, status: "active", createdAt: FieldValue.serverTimestamp() });
  else tx.update(assignmentRef, { status: "released", releasedAt: FieldValue.serverTimestamp() });
  return { assignmentId: id, status: action === "reserve" ? "active" : "released" };
  } };
}
export function assignmentCommand(db: Firestore, auth: { uid: string } | undefined, data: unknown,
  action: "reserve" | "release", source: string, selection: Selection) {
  const entries = validateSelection(selection); const sourceId = versionId(source);
  if (!["reserve", "release"].includes(action)) invalid();
  return logisticsCommand(db, auth, data, action === "reserve" ? "reserveFleet" : "releaseFleet",
    { action, source: sourceId, assets: entries }, async () => {},
    (tx, state) => writeAssignment(tx, db, state, entries, sourceId, action));
}

/** Compatibility adapter. Composed domain commands call prepareAssignment and defer apply. */
export async function writeAssignment(tx: Transaction, db: Firestore, state: CompanyState, selection: Selection,
  source: string, action: "reserve" | "release") {
  return (await prepareAssignment(tx, db, state, selection, source, action)).apply();
}
