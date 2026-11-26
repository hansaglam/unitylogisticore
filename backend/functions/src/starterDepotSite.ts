import { DocumentData, FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { readFoundation } from "./company";
import { validateHeadquartersRecord } from "./headquartersRecord";
import { InitialHeadquartersLocation, HEADQUARTERS_CATALOG_VERSION } from "./headquartersCatalog";
import { requireUid, validateIdentityRequest } from "./identity";
import { assertLocalEnvironment } from "./localEnvironment";

/**
 * A location-bound initial DEPOT SITE entitlement, not a usable warehouse,
 * route, vehicle or commercial purchase. No inventory, cost, capacity, money,
 * revenue or ownership of road routes is implied. Activation remains blocked
 * until starter fleet + verified road network + warehouse catalog are ready.
 */
export const STARTER_SITE_DOCUMENT = "starterDepotSite";
export const STARTER_SITE_STATUS = "site_reserved";
export const STARTER_SITE_DISPATCH = "unavailable_pending_verified_routes";

export type VerifiedStarterSite = {
  countryCode: string;
  cityKey: string;
  catalogVersion: string;
  status: typeof STARTER_SITE_STATUS;
  dispatchStatus: typeof STARTER_SITE_DISPATCH;
};

/**
 * Called ONLY inside the existing atomic creation transaction after server
 * HQ validation. No new public command is exposed and old company creation
 * stays unchanged. Client cannot choose/grant the depot position independently.
 */
export function createInitialStarterSite(tx: Transaction,
  companyRef: FirebaseFirestore.DocumentReference, companyId: string,
  uid: string, verifiedHq: InitialHeadquartersLocation): void {
  assertLocalEnvironment(process.env, true);
  tx.create(companyRef.collection("state").doc(STARTER_SITE_DOCUMENT), {
    schemaVersion: 1,
    companyId,
    ownerUid: uid,
    countryCode: verifiedHq.countryCode,
    cityKey: verifiedHq.cityKey,
    catalogVersion: HEADQUARTERS_CATALOG_VERSION,
    status: STARTER_SITE_STATUS,
    dispatchStatus: STARTER_SITE_DISPATCH,
    source: "initial_company_setup",
    reservedAt: FieldValue.serverTimestamp(),
  });
}

/** Strictly bound to the same company's immutable HQ record. */
export function validateStarterSiteRecord(data: DocumentData | undefined,
  uid: string, companyId: string, verifiedHq: InitialHeadquartersLocation): VerifiedStarterSite {
  if (!data || Object.keys(data).sort().join(",") !==
      "catalogVersion,cityKey,companyId,countryCode,dispatchStatus,ownerUid,reservedAt,schemaVersion,source,status" ||
      data.schemaVersion !== 1 || data.companyId !== companyId || data.ownerUid !== uid ||
      data.countryCode !== verifiedHq.countryCode || data.cityKey !== verifiedHq.cityKey ||
      data.catalogVersion !== HEADQUARTERS_CATALOG_VERSION ||
      data.status !== STARTER_SITE_STATUS || data.dispatchStatus !== STARTER_SITE_DISPATCH ||
      data.source !== "initial_company_setup" || !(data.reservedAt instanceof Timestamp))
    throw new CommandError("COMPANY_STATE_INVALID");

  return {
    countryCode: data.countryCode,
    cityKey: data.cityKey,
    catalogVersion: data.catalogVersion,
    status: STARTER_SITE_STATUS,
    dispatchStatus: STARTER_SITE_DISPATCH,
  };
}

/**
 * Compatibility-friendly separate read. Legacy companies with no HQ/site are
 * valid; never assign them a default city or mutate their records. An orphan
 * site or a mismatched country/city is an invalid company state.
 */
export async function readMyStarterDepotSite(
  db: Firestore, auth: { uid: string } | undefined, data: unknown,
) {
  assertLocalEnvironment(process.env, true);
  const uid = requireUid(auth);
  validateIdentityRequest(data);

  try {
    const result = await db.runTransaction(async tx => {
      const foundation = await readFoundation(tx, db, uid);
      if (!foundation.companyData)
        return { hasCompany: false, hasHeadquarters: false, hasStarterSite: false };

      const hqRef = foundation.companyRef.collection("state").doc("headquarters");
      const siteRef = foundation.companyRef.collection("state").doc(STARTER_SITE_DOCUMENT);
      const [hq, site] = await tx.getAll(hqRef, siteRef);

      if (!hq!.exists) {
        if (site!.exists)
          throw new CommandError("COMPANY_STATE_INVALID");
        return { hasCompany: true, hasHeadquarters: false, hasStarterSite: false };
      }

      const verified = validateHeadquartersRecord(
        hq!.data(), uid, foundation.companyId);

      if (!site!.exists)
        return { hasCompany: true, hasHeadquarters: true, hasStarterSite: false };

      return {
        hasCompany: true, hasHeadquarters: true, hasStarterSite: true,
        companyId: foundation.companyId,
        starterSite: validateStarterSiteRecord(
          site!.data(), uid, foundation.companyId, verified),
      };
    }, { readOnly: true });

    return { protocolVersion: 1, serverTime: new Date().toISOString(), result };
  } catch (error) {
    if (error instanceof CommandError) throw error;
    throw new CommandError("INTERNAL");
  }
}
