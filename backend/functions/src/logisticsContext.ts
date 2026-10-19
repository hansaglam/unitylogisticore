import { DocumentData, Firestore, Transaction } from "firebase-admin/firestore";
import { executeCommand, Json } from "./commands";
import { readFoundation } from "./company";
import { exact, invalid, versionId } from "./economyModel";
import { LogisticsCatalog, validateCatalog } from "./logisticsModel";

export type CompanyState = Awaited<ReturnType<typeof readFoundation>>;
/** Per transaction attempt only; never share catalog snapshots across requests/retries. */
export function catalogReader(tx: Transaction, db: Firestore) {
  const cache = new Map<string, LogisticsCatalog>();
  return async (version: unknown) => {
    const id = versionId(version);
    if (!cache.has(id)) cache.set(id, validateCatalog((await tx.get(db.collection("v2LogisticsCatalog").doc(id))).data(), id));
    return cache.get(id)!;
  };
}
/** Internal adapter: selector is server-owned, client payload is empty. */
export function logisticsCommand<P extends Json>(db: Firestore, auth: { uid: string } | undefined, data: unknown,
  name: string, selector: P,
  resolve: (tx: Transaction, state: CompanyState) => Promise<void>,
  apply: (tx: Transaction, state: CompanyState) => Promise<Json>) {
  const snapshots = new WeakMap<Transaction, CompanyState>();
  return executeCommand(db, auth, data, {
    name,
    validate(payload) { exact(payload as DocumentData, ""); return selector; },
    async resolveCompany(tx, uid) {
      const state = await readFoundation(tx, db, uid);
      if (!state.economy) invalid();
      await resolve(tx, state); snapshots.set(tx, state); return state.companyId;
    },
    async apply(tx) { return apply(tx, snapshots.get(tx)!); },
  });
}
