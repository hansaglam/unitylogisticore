import { createHash } from "node:crypto";
import { DocumentData, FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { CommandError, executeCommand } from "./commands";
import { assertIdentityRecord, requireUid, validateIdentityRequest } from "./identity";
import { assertLocalEnvironment } from "./localEnvironment";

import { ECONOMY_READY, invalid, validateConfig, validateEconomy, versionId } from "./economyModel";

export const COMPANY_STATUS = "foundation_pending_economy";

export function companyIdForUid(uid: string): string {
  requireUid({ uid });
  return `co_${createHash("sha256").update(uid, "utf8").digest("hex")}`;
}

/** Display text only: NFC, trim, collapse Unicode space separators; 2–64 code points. */
export function normalizeCompanyName(value: unknown): string {
  if (typeof value !== "string" || Buffer.byteLength(value, "utf8") > 512 ||
      /[\p{Cc}\p{Cf}\p{Cs}\p{Zl}\p{Zp}]/u.test(value) || /\p{Default_Ignorable_Code_Point}/u.test(value)) {
    throw new CommandError("INVALID_ARGUMENT");
  }
  const name = value.normalize("NFC").replace(/\p{Zs}+/gu, " ").trim();
  if ([...name].length < 2 || [...name].length > 64 || !/[\p{L}\p{N}]/u.test(name)) {
    throw new CommandError("INVALID_ARGUMENT");
  }
  return name;
}

export function validateCompanyPayload(payload: unknown): { name: string } {
  if (typeof payload !== "object" || payload === null || Array.isArray(payload) ||
      Object.keys(payload).length !== 1 || !("name" in payload)) {
    throw new CommandError("INVALID_ARGUMENT");
  }
  return { name: normalizeCompanyName(payload.name) };
}

function hasMigrationMarker(data: DocumentData): boolean {
  // No meaning is inferred from true/false/completed legacy marker values.
  return Object.keys(data).some(key => /^migration/i.test(key));
}

/** Fixed direct reads in one consistent snapshot; never follow a client or corrupt mapping path. */
export async function readFoundation(tx: Transaction, db: Firestore, uid: string) {
  const companyId = companyIdForUid(uid);
  const playerRef = db.collection("v2Players").doc(uid);
  const companyRef = db.collection("v2Companies").doc(companyId);
  const [player, company, legacy, legacyState, migration] = await tx.getAll(
    playerRef, companyRef, db.collection("users").doc(uid),
    db.collection("users").doc(uid).collection("serverState").doc("current"),
    db.collection("v2MigrationRecords").doc(uid),
  );
  const identity = player!.data();
  try { assertIdentityRecord(identity, uid); }
  catch { throw new CommandError("FAILED_PRECONDITION"); }
  const companyData = company!.data();
  if (legacy!.exists || legacyState!.exists || migration!.exists || hasMigrationMarker(identity!) ||
      (companyData && hasMigrationMarker(companyData))) throw new CommandError("MIGRATION_REQUIRED");
  const mapped = Object.prototype.hasOwnProperty.call(identity, "companyId");
  if ((mapped && identity!.companyId !== companyId) || mapped !== company!.exists) {
    throw new CommandError("COMPANY_STATE_INVALID");
  }
  if (companyData) {
    let validName = false;
    try { validName = normalizeCompanyName(companyData.name) === companyData.name; } catch { /* reject below */ }
    if (Object.keys(companyData).sort().join(",") !== "bootstrapStatus,createdAt,name,ownerUid,schemaVersion" ||
        companyData.schemaVersion !== 1 || companyData.ownerUid !== uid ||
        ![COMPANY_STATUS, ECONOMY_READY].includes(companyData.bootstrapStatus) || !(companyData.createdAt instanceof Timestamp) || !validName) {
      throw new CommandError("COMPANY_STATE_INVALID");
    }
  }
  const financeRef = companyRef.collection("state").doc("finance");
  const progressionRef = companyRef.collection("state").doc("progression");
  const [finance, progression] = await tx.getAll(financeRef, progressionRef);
  let economy: ReturnType<typeof validateEconomy> | undefined;
  if (companyData?.bootstrapStatus === ECONOMY_READY) {
    const version = versionId(finance!.data()?.configVersion);
    const config = validateConfig((await tx.get(db.collection("v2WorldConfig").doc(version))).data(), version);
    economy = validateEconomy(finance!.data(), progression!.data(), config);
  } else if (finance!.exists || progression!.exists) invalid();
  return { playerRef, companyRef, companyId, companyData, financeRef, progressionRef, economy };
}

export function createCompanyCommand(db: Firestore, auth: { uid: string } | undefined, data: unknown) {
  // Per invocation and transaction attempt: scope validation also protects replay.
  // Replaced by every resolver call, including SDK retries; never cached across requests.
  const snapshots = new WeakMap<Transaction, Awaited<ReturnType<typeof readFoundation>>>();
  return executeCommand(db, auth, data, {
    name: "createCompany", validate: validateCompanyPayload,
    resolveCompany: async (tx, uid) => {
      const state = await readFoundation(tx, db, uid);
      snapshots.set(tx, state);
      return state.companyId;
    },
    apply: async (tx, uid, payload) => {
      const state = snapshots.get(tx);
      if (!state) throw new CommandError("INTERNAL");
      if (state.companyData) throw new CommandError("COMPANY_EXISTS");
      tx.create(state.companyRef, {
        schemaVersion: 1, ownerUid: uid, name: payload.name,
        bootstrapStatus: COMPANY_STATUS, createdAt: FieldValue.serverTimestamp(),
      });
      tx.update(state.playerRef, { companyId: state.companyId });
      return { companyId: state.companyId, name: payload.name, schemaVersion: 1,
        bootstrapStatus: COMPANY_STATUS, economyReady: false };
    },
  });
}

export async function readMyCompanyState(db: Firestore, auth: { uid: string } | undefined, data: unknown) {
  assertLocalEnvironment(process.env, true);
  const uid = requireUid(auth);
  validateIdentityRequest(data);
  try {
    const state = await db.runTransaction(tx => readFoundation(tx, db, uid), { readOnly: true });
    const company = state.companyData;
    return { protocolVersion: 1, serverTime: new Date().toISOString(), result: company ? {
      hasCompany: true, companyId: state.companyId, name: company.name as string,
      schemaVersion: 1, bootstrapStatus: company.bootstrapStatus as string, economyReady: !!state.economy,
    } : { hasCompany: false } };
  } catch (error) {
    if (error instanceof CommandError) throw error;
    throw new CommandError("INTERNAL");
  }
}
