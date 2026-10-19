import { createHash } from "node:crypto";
import { DocumentData, Timestamp } from "firebase-admin/firestore";
import { canonicalJson, CommandError } from "./commands";
import { exact, invalid, safe, versionId } from "./economyModel";

export type AssetKind = "truck" | "driver" | "trailer";
export function assetKind(value: unknown): AssetKind {
  if (value !== "truck" && value !== "driver" && value !== "trailer") invalid();
  return value;
}
export function digest(value: unknown): string { return createHash("sha256").update(canonicalJson(value, 32768)).digest("hex"); }
export function validateCatalog(data: DocumentData | undefined, version: string) {
  exact(data, "schemaVersion,version,approval,assets,starter,cities,warehouses");
  if (![1, 2].includes(data.schemaVersion) || data.version !== versionId(version) || data.approval !== "synthetic_emulator_only") invalid();
  for (const list of [data.assets, data.cities, data.warehouses]) {
    if (!Array.isArray(list) || list.length > 32) invalid();
  }
  const assets = (data.assets as DocumentData[]).map(entry => {
    const kind = assetKind(entry.kind);
    if (data.schemaVersion === 1) {
      exact(entry, "id,kind"); return { id: versionId(entry.id), kind };
    }
    exact(entry, "id,kind,capabilities,capacity");
    if (!Array.isArray(entry.capabilities) || entry.capabilities.length > 16) invalid();
    const capabilities = (entry.capabilities as unknown[]).map(versionId);
    if (new Set(capabilities).size !== capabilities.length) invalid();
    let capacity: { unit: string; amount: number } | null = null;
    if (kind === "driver") { if (entry.capacity !== null) invalid(); }
    else {
      exact(entry.capacity, "unit,amount");
      if (safe(entry.capacity.amount) < 1) invalid();
      capacity = { unit: versionId(entry.capacity.unit), amount: entry.capacity.amount as number };
    }
    return { id: versionId(entry.id), kind, capabilities, capacity };
  });
  const cities = (data.cities as unknown[]).map(versionId);
  const warehouses = (data.warehouses as DocumentData[]).map(entry => {
    exact(entry, "id,cityId,type,level,capacityModelId,capacityUnits");
    const cityId = versionId(entry.cityId);
    if (!cities.includes(cityId) || safe(entry.level, 1000) < 1 || safe(entry.capacityUnits) < 1) invalid();
    return { id: versionId(entry.id), cityId, type: versionId(entry.type), level: entry.level as number,
      capacityModelId: versionId(entry.capacityModelId), capacityUnits: entry.capacityUnits as number };
  });
  for (const ids of [assets.map(a => a.id), cities, warehouses.map(w => w.id)]) {
    if (new Set(ids).size !== ids.length) invalid();
  }
  exact(data.starter, "packageVersion,truck,driver");
  const starter = { packageVersion: versionId(data.starter.packageVersion), truck: versionId(data.starter.truck), driver: versionId(data.starter.driver) };
  if (!assets.some(a => a.id === starter.truck && a.kind === "truck") || !assets.some(a => a.id === starter.driver && a.kind === "driver")) invalid();
  return { schemaVersion: data.schemaVersion as number, version, approval: "synthetic_emulator_only", assets, cities, warehouses, starter };
}
export type LogisticsCatalog = ReturnType<typeof validateCatalog>;
export function validateAsset(data: DocumentData | undefined, id: string, companyId: string, catalog: LogisticsCatalog) {
  exact(data, "schemaVersion,assetId,kind,companyId,catalogId,catalogVersion,catalogHash,status,assignmentId,acquiredAt");
  const kind = assetKind(data.kind);
  if (data.schemaVersion !== 1 || data.assetId !== versionId(id) || data.companyId !== companyId ||
      data.catalogVersion !== catalog.version || data.catalogHash !== digest(catalog) ||
      !catalog.assets.some(a => a.id === data.catalogId && a.kind === kind) ||
      !["operational", "out_of_service"].includes(data.status) || !(data.acquiredAt instanceof Timestamp)) invalid();
  if (data.assignmentId !== null && (typeof data.assignmentId !== "string" || !/^[a-f0-9]{64}$/.test(data.assignmentId))) invalid();
  return { assetId: id, kind, catalogId: data.catalogId as string, catalogVersion: catalog.version,
    status: data.status as string, assignmentId: data.assignmentId as string | null,
    available: data.status === "operational" && data.assignmentId === null };
}
export function warehouseId(cityId: string, type: string): string { return digest({ cityId: versionId(cityId), type: versionId(type) }); }
export function requireCapacity(used: unknown, requested: unknown, maximum: number): void {
  safe(maximum); const current = safe(used, maximum); const addition = safe(requested, maximum);
  if (addition > maximum - current) invalid();
}
export function validateWarehouse(data: DocumentData | undefined, id: string, companyId: string, catalog: LogisticsCatalog) {
  exact(data, "schemaVersion,warehouseId,companyId,catalogId,catalogVersion,catalogHash,cityId,type,level,status,capacityModelId,usedCapacity,acquiredAt");
  const definition = catalog.warehouses.find(w => w.id === data.catalogId);
  if (!definition || data.schemaVersion !== 1 || data.companyId !== companyId || data.warehouseId !== id ||
      id !== warehouseId(definition.cityId, definition.type) || data.catalogVersion !== catalog.version || data.catalogHash !== digest(catalog) ||
      data.cityId !== definition.cityId || data.type !== definition.type || data.level !== definition.level ||
      data.capacityModelId !== definition.capacityModelId || !["operational", "out_of_service"].includes(data.status) || !(data.acquiredAt instanceof Timestamp)) invalid();
  requireCapacity(data.usedCapacity, 0, definition.capacityUnits);
  return { warehouseId: id, catalogId: definition.id, catalogVersion: catalog.version, cityId: definition.cityId,
    type: definition.type, level: definition.level, status: data.status as string,
    capacityModelId: definition.capacityModelId, capacityUnits: String(definition.capacityUnits), usedCapacity: String(data.usedCapacity) };
}
export function pageRequest(data: unknown): { limit: number; after: string | null } {
  canonicalJson(data);
  if (!data || typeof data !== "object" || Array.isArray(data)) throw new CommandError("INVALID_ARGUMENT");
  const value = data as Record<string, unknown>;
  if (value.protocolVersion !== 1 || Object.keys(value).some(k => !["protocolVersion", "limit", "after"].includes(k)) ||
      (value.limit !== undefined && (!Number.isSafeInteger(value.limit) || (value.limit as number) < 1 || (value.limit as number) > 20)) ||
      (value.after !== undefined && (typeof value.after !== "string" || !/^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$/.test(value.after)))) throw new CommandError("INVALID_ARGUMENT");
  return { limit: (value.limit as number | undefined) ?? 20, after: (value.after as string | undefined) ?? null };
}
