import { FieldPath, Firestore } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { readFoundation } from "./company";
import { invalid } from "./economyModel";
import { readStarter } from "./fleet";
import { requireUid } from "./identity";
import { catalogReader } from "./logisticsContext";
import { pageRequest, validateAsset, validateWarehouse } from "./logisticsModel";
import { assertLocalEnvironment } from "./localEnvironment";

async function readLogistics(db: Firestore, auth: { uid: string } | undefined, data: unknown, kind: "assets" | "warehouses") {
  assertLocalEnvironment(process.env, true);
  const uid = requireUid(auth); const page = pageRequest(data);
  try {
    const result = await db.runTransaction(async tx => {
      const state = await readFoundation(tx, db, uid);
      const getCatalog = catalogReader(tx, db);
      const starter = kind === "assets" ? await readStarter(tx, db, state, getCatalog) : undefined;
      let query = state.companyRef.collection(kind).orderBy(FieldPath.documentId()).limit(page.limit + 1);
      if (page.after !== null) query = query.startAfter(page.after);
      const records = await tx.get(query);
      if (!state.economy && !records.empty) invalid();
      const items = [];
      for (const doc of records.docs.slice(0, page.limit)) {
        const raw = doc.data(); const catalog = await getCatalog(raw.catalogVersion);
        items.push(kind === "assets" ? validateAsset(raw, doc.id, state.companyId, catalog) : validateWarehouse(raw, doc.id, state.companyId, catalog));
      }
      return { hasCompany: !!state.companyData, economyReady: !!state.economy,
        ...(starter ? { starterFleetReady: starter.ready } : {}), items,
        nextCursor: records.size > page.limit ? records.docs[page.limit - 1]!.id : null };
    }, { readOnly: true });
    return { protocolVersion: 1, serverTime: new Date().toISOString(), result };
  } catch (error) {
    if (error instanceof CommandError) throw error;
    throw new CommandError("INTERNAL");
  }
}
export const readMyFleetState = (db: Firestore, auth: { uid: string } | undefined, data: unknown) => readLogistics(db, auth, data, "assets");
export const readMyWarehouseState = (db: Firestore, auth: { uid: string } | undefined, data: unknown) => readLogistics(db, auth, data, "warehouses");
