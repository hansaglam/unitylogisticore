import { DocumentData, FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { CommandError, executeCommand } from "./commands";
import { readFoundation } from "./company";
import {
  LOCAL_STARTER_DEPOT_CATALOG, STARTER_DEPOT_CAPACITY, STARTER_DEPOT_SLOTS,
  STARTER_DEPOT_TYPE, supportedStarterDepotCity, validateDevStarterDepotCatalog,
} from "./devStarterDepotCatalog";
import { exact } from "./economyModel";
import { readStarter } from "./fleet";
import { validateHeadquartersRecord } from "./headquartersRecord";
import { requireUid, validateIdentityRequest } from "./identity";
import { assertLocalEnvironment } from "./localEnvironment";
import { digest, LogisticsCatalog, validateWarehouse, warehouseId } from "./logisticsModel";
import {
  STARTER_SITE_DOCUMENT, validateStarterSiteRecord,
} from "./starterDepotSite";

export const DEPOT_ACTIVATION_DOCUMENT = "starterDepotActivation";
export const DEPOT_ACTIVATED_STATUS = "warehouse_operational_dispatch_pending_routes";
export type DepotReadinessStage =
  "no_company" | "legacy_hq_missing" | "starter_site_missing" |
  "awaiting_economy" | "unsupported_starter_region" |
  "awaiting_starter_fleet" | "awaiting_warehouse_catalog" |
  "ready_to_activate" | "warehouse_operational_dispatch_pending_routes";

type Foundation = Awaited<ReturnType<typeof readFoundation>>;
type SiteLocation = { countryCode: string; cityKey: string };
type DepotProgress = {
  hasCompany: boolean; hasHeadquarters: boolean; hasStarterSite: boolean;
  economyReady: boolean; starterFleetReady: boolean; warehouseReady: boolean;
  dispatchReady: false; stage: DepotReadinessStage;
  headquarters?: SiteLocation;
  warehouse?: { warehouseId: string; capacityModelId: string; capacityUnits: number };
};

type Inspect = {
  progress: DepotProgress;
  activationRef?: FirebaseFirestore.DocumentReference;
  warehouseRef?: FirebaseFirestore.DocumentReference;
  catalog?: LogisticsCatalog;
  warehouseCatalogId?: string;
};

function progress(stage: DepotReadinessStage, company: boolean, headquarters: boolean,
  site: boolean, economy: boolean, fleet: boolean,
  location?: SiteLocation): DepotProgress {
  return {
    hasCompany: company, hasHeadquarters: headquarters, hasStarterSite: site,
    economyReady: economy, starterFleetReady: fleet, warehouseReady: false,
    dispatchReady: false, stage,
    ...(location ? { headquarters: location } : {}),
  };
}

/**
 * All eligibility and current activation state are read from Firestore in the
 * same transaction. No remote route API is invoked from a Firestore transaction.
 * Warehouse activation grants STORAGE ONLY. Truck dispatch must still verify
 * an actual authorized road geometry, border rights and a payable contract.
 */
async function inspectDepot(tx: Transaction, db: Firestore, uid: string,
  foundation: Foundation): Promise<Inspect> {
  if (!foundation.companyData)
    return { progress: progress("no_company", false, false, false, false, false) };

  const hqRef = foundation.companyRef.collection("state").doc("headquarters");
  const siteRef = foundation.companyRef.collection("state").doc(STARTER_SITE_DOCUMENT);
  const activationRef = foundation.companyRef.collection("state").doc(DEPOT_ACTIVATION_DOCUMENT);
  const [hq, site, activation] = await tx.getAll(hqRef, siteRef, activationRef);
  if (!hq!.exists) {
    if (site!.exists || activation!.exists)
      throw new CommandError("COMPANY_STATE_INVALID");
    return { progress: progress("legacy_hq_missing", true, false, false, !!foundation.economy, false) };
  }
  const location = validateHeadquartersRecord(hq!.data(), uid, foundation.companyId);

  if (!site!.exists) {
    if (activation!.exists)
      throw new CommandError("COMPANY_STATE_INVALID");
    return { progress: progress("starter_site_missing", true, true, false,
      !!foundation.economy, false, location) };
  }
  validateStarterSiteRecord(site!.data(), uid, foundation.companyId, location);

  const economyReady = !!foundation.economy;
  if (!economyReady) {
    if (activation!.exists)
      throw new CommandError("COMPANY_STATE_INVALID");
    return { progress: progress("awaiting_economy", true, true, true, false, false, location) };
  }

  // Current backend route and fleet fixture covers only the eight initial
  // Turkish KGM cities. All other launch locations remain reserved, not
  // activated. Never invent a contract or facility for Europe.
  if (!supportedStarterDepotCity(location.countryCode, location.cityKey)) {
    if (activation!.exists)
      throw new CommandError("COMPANY_STATE_INVALID");
    return { progress: progress("unsupported_starter_region", true, true, true,
      true, false, location) };
  }

  const fleet = await readStarter(tx, db, foundation);
  if (!fleet.ready) {
    if (activation!.exists)
      throw new CommandError("COMPANY_STATE_INVALID");
    return { progress: progress("awaiting_starter_fleet", true, true, true,
      true, false, location) };
  }

  const catalogSnap = await tx.get(
    db.collection("v2LogisticsCatalog").doc(LOCAL_STARTER_DEPOT_CATALOG));
  if (!catalogSnap.exists) {
    if (activation!.exists)
      throw new CommandError("COMPANY_STATE_INVALID");
    return { progress: progress("awaiting_warehouse_catalog", true, true, true,
      true, true, location) };
  }
  const catalog = validateDevStarterDepotCatalog(catalogSnap.data());
  const definition = catalog.warehouses.find(w =>
    w.cityId === location.cityKey && w.type === STARTER_DEPOT_TYPE);
  if (!definition || definition.level !== 1 ||
      definition.capacityModelId !== STARTER_DEPOT_CAPACITY ||
      definition.capacityUnits !== STARTER_DEPOT_SLOTS)
    throw new CommandError("COMPANY_STATE_INVALID");

  const id = warehouseId(definition.cityId, definition.type);
  const warehouseRef = foundation.companyRef.collection("warehouses").doc(id);
  const warehouse = await tx.get(warehouseRef);

  if (!activation!.exists) {
    if (warehouse.exists)
      throw new CommandError("COMPANY_STATE_INVALID");
    return {
      progress: progress("ready_to_activate", true, true, true, true, true, location),
      activationRef, warehouseRef, catalog, warehouseCatalogId: definition.id,
    };
  }

  if (!warehouse.exists)
    throw new CommandError("COMPANY_STATE_INVALID");
  const marker = activation!.data();
  exact(marker,
    "schemaVersion,companyId,ownerUid,countryCode,cityKey,warehouseId,catalogVersion,catalogHash,status,activatedAt");
  if (marker.schemaVersion !== 1 || marker.companyId !== foundation.companyId ||
      marker.ownerUid !== uid || marker.countryCode !== location.countryCode ||
      marker.cityKey !== location.cityKey || marker.warehouseId !== id ||
      marker.catalogVersion !== LOCAL_STARTER_DEPOT_CATALOG ||
      marker.catalogHash !== digest(catalog) ||
      marker.status !== DEPOT_ACTIVATED_STATUS ||
      !(marker.activatedAt instanceof Timestamp))
    throw new CommandError("COMPANY_STATE_INVALID");

  const saved = validateWarehouse(warehouse.data(), id, foundation.companyId, catalog);
  if (saved.cityId !== location.cityKey || saved.catalogId !== definition.id ||
      saved.status !== "operational" || saved.level !== 1 ||
      saved.capacityModelId !== STARTER_DEPOT_CAPACITY)
    throw new CommandError("COMPANY_STATE_INVALID");

  return { progress: {
    ...progress(DEPOT_ACTIVATED_STATUS, true, true, true, true, true, location),
    warehouseReady: true,
    warehouse: {
      warehouseId: id, capacityModelId: definition.capacityModelId,
      capacityUnits: definition.capacityUnits,
    },
  }, activationRef, warehouseRef, catalog, warehouseCatalogId: definition.id };
}

/**
 * One-time transaction after verified HQ, economy, starter truck+driver and
 * a separately provisioned exact-match local catalog. Server chooses the
 * location and capacity; payload MUST be {}. Does not charge or credit cash.
 */
export function activateHeadquartersStarterDepotCommand(
  db: Firestore, auth: { uid: string } | undefined, data: unknown,
) {
  const snapshots = new WeakMap<Transaction, Foundation>();
  return executeCommand(db, auth, data, {
    name: "activateHeadquartersStarterDepot",
    validate(payload: unknown) {
      exact(payload as DocumentData, "");
      return {};
    },
    async resolveCompany(tx, uid) {
      const foundation = await readFoundation(tx, db, uid);
      snapshots.set(tx, foundation);
      return foundation.companyId;
    },
    async apply(tx, uid) {
      const foundation = snapshots.get(tx);
      if (!foundation)
        throw new CommandError("INTERNAL");

      const inspected = await inspectDepot(tx, db, uid, foundation);
      if (inspected.progress.stage !== "ready_to_activate" ||
          !inspected.catalog || !inspected.activationRef ||
          !inspected.warehouseRef || !inspected.warehouseCatalogId ||
          !inspected.progress.headquarters)
        throw new CommandError("FAILED_PRECONDITION");

      const location = inspected.progress.headquarters;
      const catalog = inspected.catalog;
      const definition = catalog.warehouses.find(w =>
        w.id === inspected.warehouseCatalogId)!;
      const id = inspected.warehouseRef.id;
      const timestamp = FieldValue.serverTimestamp();
      tx.create(inspected.warehouseRef, {
        schemaVersion: 1, warehouseId: id, companyId: foundation.companyId,
        catalogId: definition.id, catalogVersion: catalog.version,
        catalogHash: digest(catalog), cityId: location.cityKey,
        type: definition.type, level: definition.level,
        status: "operational", capacityModelId: definition.capacityModelId,
        usedCapacity: 0, acquiredAt: timestamp,
      });
      tx.create(inspected.activationRef, {
        schemaVersion: 1, companyId: foundation.companyId,
        ownerUid: uid, countryCode: location.countryCode,
        cityKey: location.cityKey, warehouseId: id,
        catalogVersion: catalog.version, catalogHash: digest(catalog),
        status: DEPOT_ACTIVATED_STATUS, activatedAt: timestamp,
      });
      return {
        warehouseId: id, countryCode: location.countryCode,
        cityKey: location.cityKey, capacityModelId: definition.capacityModelId,
        capacityUnits: definition.capacityUnits,
        stage: DEPOT_ACTIVATED_STATUS, dispatchReady: false,
      };
    },
  });
}

/** Optional read-only status; never hides missing HQ or unsupported regions. */
export async function readMyStarterDepotProgress(
  db: Firestore, auth: { uid: string } | undefined, data: unknown,
) {
  assertLocalEnvironment(process.env, true);
  const uid = requireUid(auth);
  validateIdentityRequest(data);
  try {
    const result = await db.runTransaction(async tx => {
      const foundation = await readFoundation(tx, db, uid);
      return (await inspectDepot(tx, db, uid, foundation)).progress;
    }, { readOnly: true });
    return { protocolVersion: 1, serverTime: new Date().toISOString(), result };
  } catch (error) {
    if (error instanceof CommandError) throw error;
    throw new CommandError("INTERNAL");
  }
}
