import { DocumentData, Timestamp } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { configHash, EconomyConfig, exact, invalid, safe, versionId } from "./economyModel";
import { digest, LogisticsCatalog, warehouseId } from "./logisticsModel";
import { MAX_TIMESTAMP } from "./deliveryModel";

export const commerceKinds = ["buyVehicle", "hireDriver", "acquireTrailer", "openWarehouse", "upgradeWarehouse"] as const;
export type CommerceKind = typeof commerceKinds[number];
export function commerceKind(value: unknown): CommerceKind {
  if (!commerceKinds.includes(value as CommerceKind)) invalid();
  return value as CommerceKind;
}
export function acquisitionPayload(value: unknown) {
  try { exact(value as DocumentData, "offerId"); return { offerId: versionId((value as DocumentData).offerId) }; }
  catch { throw new CommandError("INVALID_ARGUMENT"); }
}
export function acquisitionId(companyId: string, offerId: string) {
  return digest({ domain: "commerce_v1", companyId, offerId: versionId(offerId) });
}
export function acquisitionSource(companyId: string, offerId: string) { return `commerce_${acquisitionId(companyId, offerId)}`; }
export function validateCommerceCatalog(raw: DocumentData | undefined, version: string, logistics: LogisticsCatalog, economy: EconomyConfig) {
  exact(raw, "schemaVersion,version,approval,economyVersion,economyHash,rulesVersion,logisticsVersion,logisticsHash,items");
  if (raw.schemaVersion !== 1 || raw.version !== versionId(version) || raw.approval !== "synthetic_emulator_only" ||
      raw.economyVersion !== economy.version || raw.economyHash !== configHash(economy) || raw.rulesVersion !== economy.rulesVersion ||
      raw.logisticsVersion !== logistics.version || raw.logisticsHash !== digest(logistics) || logistics.schemaVersion !== 2 ||
      !Array.isArray(raw.items) || raw.items.length < 1 || raw.items.length > 32) invalid();
  const items = (raw.items as DocumentData[]).map(item => {
    exact(item, "id,kind,catalogId,price,requiredLevel,expectedLevel");
    const kind = commerceKind(item.kind), catalogId = versionId(item.catalogId);
    if (safe(item.price, economy.maxBalance) < 1 || safe(item.requiredLevel, economy.thresholds.length) < 1) invalid();
    safe(item.expectedLevel, 999);
    if (kind === "openWarehouse" || kind === "upgradeWarehouse") {
      const definition = logistics.warehouses.find(w => w.id === catalogId);
      if (!definition || definition.level !== item.expectedLevel + 1 ||
          (kind === "openWarehouse" ? item.expectedLevel !== 0 : item.expectedLevel < 1)) invalid();
    } else {
      const assetKind = kind === "buyVehicle" ? "truck" : kind === "hireDriver" ? "driver" : "trailer";
      if (item.expectedLevel !== 0 || !logistics.assets.some(a => a.id === catalogId && a.kind === assetKind)) invalid();
    }
    return { id: versionId(item.id), kind, catalogId, price: item.price as number,
      requiredLevel: item.requiredLevel as number, expectedLevel: item.expectedLevel as number };
  });
  if (new Set(items.map(i => i.id)).size !== items.length) invalid();
  return { schemaVersion: 1, version, approval: "synthetic_emulator_only", economyVersion: economy.version,
    economyHash: configHash(economy), rulesVersion: economy.rulesVersion, logisticsVersion: logistics.version, logisticsHash: digest(logistics), items };
}
export type CommerceCatalog = ReturnType<typeof validateCommerceCatalog>;
export function validateAcquisitionOffer(raw: DocumentData | undefined, id: string, companyId: string, catalog: CommerceCatalog,
  logistics: LogisticsCatalog, economy: EconomyConfig) {
  if (!raw) invalid();
  exact(raw, "schemaVersion,offerId,companyId,catalogVersion,catalogHash,itemId,kind,price,currency,scale,requiredLevel,expectedLevel,targetId,sourceId,createdAt,expiresAt,status" +
    (raw.status === "consumed" ? ",consumedAt,operationId" : ""));
  const item = catalog.items.find(i => i.id === raw.itemId);
  if (!item) invalid();
  const definition = logistics.warehouses.find(w => w.id === item.catalogId);
  const isWarehouse = item.kind === "openWarehouse" || item.kind === "upgradeWarehouse";
  const targetId = isWarehouse ? warehouseId(definition!.cityId, definition!.type) : acquisitionId(companyId, id);
  if (raw.schemaVersion !== 1 || raw.offerId !== versionId(id) || raw.companyId !== companyId || raw.catalogVersion !== catalog.version ||
      raw.catalogHash !== digest(catalog) || raw.kind !== item.kind || raw.price !== item.price || raw.currency !== economy.currency || raw.scale !== economy.scale ||
      raw.requiredLevel !== item.requiredLevel || raw.expectedLevel !== item.expectedLevel || raw.targetId !== targetId || raw.sourceId !== acquisitionSource(companyId,id) ||
      !["available", "consumed"].includes(raw.status) || !(raw.createdAt instanceof Timestamp) || !(raw.expiresAt instanceof Timestamp)) invalid();
  const created = safe(raw.createdAt.toMillis(), MAX_TIMESTAMP), expires = safe(raw.expiresAt.toMillis(), MAX_TIMESTAMP);
  if (expires <= created || expires - created > 604800000) invalid();
  if (raw.status === "consumed" && (!(raw.consumedAt instanceof Timestamp) || raw.consumedAt.toMillis() < created || raw.consumedAt.toMillis() >= expires ||
      typeof raw.operationId !== "string" || !/^[a-f0-9]{64}$/.test(raw.operationId))) invalid();
  return { offerId: id, companyId, item, targetId, sourceId: raw.sourceId as string, createdAt: raw.createdAt as Timestamp,
    expiresAt: raw.expiresAt as Timestamp, status: raw.status as "available" | "consumed", consumedAt: raw.consumedAt as Timestamp | undefined,
    operationId: raw.operationId as string | undefined };
}
