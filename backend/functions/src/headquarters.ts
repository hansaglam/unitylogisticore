import { DocumentData, FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { CommandError, executeCommand } from "./commands";
import { COMPANY_STATUS, normalizeCompanyName, readFoundation } from "./company";
import {
  HEADQUARTERS_CATALOG_VERSION, HEADQUARTERS_SELECTION_KIND,
  InitialHeadquartersLocation, validateInitialHeadquarters,
} from "./headquartersCatalog";

export interface CreateCompanyWithHeadquartersPayload {
  name: string;
  headquarters: InitialHeadquartersLocation;
}

/** Distinct endpoint: leaves existing createCompany(name) and its receipts untouched. */
export function validateCreateCompanyWithHeadquartersPayload(value: unknown): CreateCompanyWithHeadquartersPayload {
  if (typeof value !== "object" || value === null || Array.isArray(value) ||
      Object.keys(value).sort().join(",") !== "headquarters,name")
    throw new CommandError("INVALID_ARGUMENT");

  const payload = value as Record<string, unknown>;
  return {
    name: normalizeCompanyName(payload.name),
    headquarters: validateInitialHeadquarters(payload.headquarters),
  };
}

export function validateHeadquartersRecord(data: DocumentData | undefined, uid: string, companyId: string) {
  if (!data || Object.keys(data).sort().join(",") !==
      "catalogVersion,cityKey,companyId,countryCode,ownerUid,schemaVersion,selectedAt,selectionKind" ||
      data.schemaVersion !== 1 || data.ownerUid !== uid || data.companyId !== companyId ||
      data.catalogVersion !== HEADQUARTERS_CATALOG_VERSION ||
      data.selectionKind !== HEADQUARTERS_SELECTION_KIND ||
      !(data.selectedAt instanceof Timestamp))
    throw new CommandError("COMPANY_STATE_INVALID");

  let location: InitialHeadquartersLocation;
  try {
    location = validateInitialHeadquarters({
      countryCode: data.countryCode, cityKey: data.cityKey,
    });
  } catch {
    throw new CommandError("COMPANY_STATE_INVALID");
  }

  return {
    countryCode: location.countryCode,
    cityKey: location.cityKey,
    catalogVersion: HEADQUARTERS_CATALOG_VERSION,
  };
}

/**
 * Atomic company creation + explicit first headquarters selection.
 * The company document remains EXACT schemaVersion 1 to preserve all existing
 * economy/contract validators. Location is stored in a separately validated,
 * immutable child document. No retroactive mutation of existing companies.
 * Any repeat attempt has the same idempotency semantics as createCompany.
 */
export function createCompanyWithHeadquartersCommand(
  db: Firestore, auth: { uid: string } | undefined, data: unknown,
) {
  const snapshots = new WeakMap<Transaction, Awaited<ReturnType<typeof readFoundation>>>();
  return executeCommand(db, auth, data, {
    name: "createCompanyWithHeadquarters",
    validate: validateCreateCompanyWithHeadquartersPayload,
    resolveCompany: async (tx, uid) => {
      const state = await readFoundation(tx, db, uid);
      snapshots.set(tx, state);
      return state.companyId;
    },
    apply: async (tx, uid, payload) => {
      const state = snapshots.get(tx);
      if (!state) throw new CommandError("INTERNAL");
      if (state.companyData) throw new CommandError("COMPANY_EXISTS");

      // The client must not choose initial funds, owned depots, or roads.
      // Do not create a starting warehouse: that requires a separate
      // location-aware, authorized fleet/warehouse provisioning policy.
      tx.create(state.companyRef, {
        schemaVersion: 1,
        ownerUid: uid,
        name: payload.name,
        bootstrapStatus: COMPANY_STATUS,
        createdAt: FieldValue.serverTimestamp(),
      });
      tx.create(state.companyRef.collection("state").doc("headquarters"), {
        schemaVersion: 1,
        companyId: state.companyId,
        ownerUid: uid,
        countryCode: payload.headquarters.countryCode,
        cityKey: payload.headquarters.cityKey,
        catalogVersion: HEADQUARTERS_CATALOG_VERSION,
        selectionKind: HEADQUARTERS_SELECTION_KIND,
        selectedAt: FieldValue.serverTimestamp(),
      });
      tx.update(state.playerRef, { companyId: state.companyId });

      return {
        companyId: state.companyId,
        name: payload.name,
        schemaVersion: 1,
        bootstrapStatus: COMPANY_STATUS,
        economyReady: false,
        headquarters: {
          countryCode: payload.headquarters.countryCode,
          cityKey: payload.headquarters.cityKey,
          catalogVersion: HEADQUARTERS_CATALOG_VERSION,
        },
      };
    },
  });
}
