import { FieldValue, Firestore, Transaction } from "firebase-admin/firestore";
import { invalid, versionId } from "./economyModel";
import { catalogReader, CompanyState, logisticsCommand } from "./logisticsContext";
import { assertLocalEnvironment } from "./localEnvironment";
import { digest, warehouseId } from "./logisticsModel";

/** Only a synthetic foundation operation. No price, purchase, upgrade or starter warehouse grant.
 * Future purchase handler must perform wallet/eligibility reads before invoking this write phase.
 */
export async function createWarehouseInTransaction(tx: Transaction, db: Firestore, state: CompanyState,
  catalogVersion: string, catalogId: string) {
  assertLocalEnvironment(process.env, true);
  if (!state.economy) invalid();
  const catalog = await catalogReader(tx, db)(catalogVersion);
  const definition = catalog.warehouses.find(entry => entry.id === versionId(catalogId));
  if (!definition) invalid();
  const id = warehouseId(definition.cityId, definition.type);
  const ref = state.companyRef.collection("warehouses").doc(id);
  if ((await tx.get(ref)).exists) invalid();
  tx.create(ref, { schemaVersion: 1, warehouseId: id, companyId: state.companyId, catalogId: definition.id,
    catalogVersion: catalog.version, catalogHash: digest(catalog), cityId: definition.cityId, type: definition.type,
    level: definition.level, status: "operational", capacityModelId: definition.capacityModelId, usedCapacity: 0,
    acquiredAt: FieldValue.serverTimestamp() });
  return { warehouseId: id };
}
export function createSyntheticWarehouse(db: Firestore, auth: { uid: string } | undefined, data: unknown,
  catalogVersion: string, catalogId: string) {
  const version = versionId(catalogVersion); const id = versionId(catalogId);
  return logisticsCommand(db, auth, data, "createSyntheticWarehouse", { catalogVersion: version, catalogId: id },
    async () => {}, (tx, state) => createWarehouseInTransaction(tx, db, state, version, id));
}
