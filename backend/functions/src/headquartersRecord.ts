import { DocumentData, Timestamp } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import {
  HEADQUARTERS_CATALOG_VERSION, HEADQUARTERS_SELECTION_KIND,
  InitialHeadquartersLocation, validateInitialHeadquarters,
} from "./headquartersCatalog";

/**
 * Shared immutable HQ record validator: both company reads and depot site
 * reads use one verification contract with no dependency cycle.
 */
export function validateHeadquartersRecord(data: DocumentData | undefined,
  uid: string, companyId: string): InitialHeadquartersLocation & { catalogVersion: string } {
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
