import { bankPolicy, validateBankPolicy } from "./bankModel";
import { DocumentData, Timestamp } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { EconomyConfig, configHash, exact, invalid, safe, versionId } from "./economyModel";
import { Selection, validateSelection } from "./fleet";
import { digest, LogisticsCatalog } from "./logisticsModel";
import { CostCatalog, CostQuote, makeCostQuote, settlementDto, validateCostQuote } from "./operatingCosts";

export const CONTRACT_STATUSES = ["offered", "accepted", "in_progress", "completed", "failed"] as const;
export type ContractStatus = typeof CONTRACT_STATUSES[number];
export type ServerClock = () => number;
export const serverClock: ServerClock = () => Date.now();
export function clockTime(clock: ServerClock): number { return safe(clock(), 253402300799999); }
export function rewardAmount(quantity: number, perUnit: number, maximum: number): number {
  safe(quantity); safe(perUnit); return safe(quantity * perUnit, maximum);
}
export function validateContractConfig(data: DocumentData | undefined, version: string, logistics: LogisticsCatalog, economy: EconomyConfig) {
  exact(data, "schemaVersion,version,approval,logisticsVersion,logisticsHash,economyVersion,economyHash,epochDurationMs,offerTtlMs,maxOffers,maxAcceptedPerEpoch,routes,cargo,templates"+(data?.schemaVersion>=2?",costCatalogVersion,costCatalogHash":"")+(data?.schemaVersion===3?",bankPolicy":""));
  if (![1,2,3].includes(data.schemaVersion) || data.version !== versionId(version) || data.approval !== "synthetic_emulator_only" ||
      logistics.schemaVersion !== 2 || data.logisticsVersion !== logistics.version || data.logisticsHash !== digest(logistics) ||
      data.economyVersion !== economy.version || data.economyHash !== configHash(economy)) invalid();
  if (safe(data.epochDurationMs, 604800000) < 1 || safe(data.offerTtlMs, data.epochDurationMs) < 1 ||
      safe(data.maxOffers, 5) < 1 || safe(data.maxAcceptedPerEpoch, data.maxOffers) < 1) invalid();
  for (const list of [data.routes, data.cargo, data.templates]) if (!Array.isArray(list) || list.length < 1 || list.length > 5) invalid();
  const routes = (data.routes as DocumentData[]).map(r => {
    exact(r, "id,originCityId,destinationCityId,distance,unit,verification");
    if (!logistics.cities.includes(r.originCityId) || !logistics.cities.includes(r.destinationCityId) || r.originCityId === r.destinationCityId ||
        r.verification !== "synthetic_fixture" || safe(r.distance) < 1) invalid();
    return { id: versionId(r.id), originCityId: versionId(r.originCityId), destinationCityId: versionId(r.destinationCityId),
      distance: r.distance as number, unit: versionId(r.unit), verification: "synthetic_fixture" };
  });
  const cargo = (data.cargo as DocumentData[]).map(c => {
    exact(c, "id,unit,capability");return { id: versionId(c.id), unit: versionId(c.unit), capability: versionId(c.capability) };
  });
  const templates = (data.templates as DocumentData[]).map(t => {
    exact(t, "id,routeId,cargoId,quantity,rewardPerUnit,minLevel,truckCapability,driverCapability,trailerCapability");
    if (!routes.some(r => r.id === t.routeId) || !cargo.some(c => c.id === t.cargoId) || safe(t.quantity) < 1 ||
        safe(t.minLevel, economy.thresholds.length) < 1) invalid();
    rewardAmount(t.quantity, t.rewardPerUnit, economy.maxBalance);
    return { id: versionId(t.id), routeId: t.routeId as string, cargoId: t.cargoId as string,
      quantity: t.quantity as number, rewardPerUnit: t.rewardPerUnit as number, minLevel: t.minLevel as number,
      truckCapability: versionId(t.truckCapability), driverCapability: versionId(t.driverCapability),
      trailerCapability: t.trailerCapability === null ? null : versionId(t.trailerCapability) };
  });
  if (templates.length > data.maxOffers) invalid();
  for (const list of [routes, cargo, templates]) if (new Set(list.map(e => e.id)).size !== list.length) invalid();
  const costPin: {costCatalogVersion?:string;costCatalogHash?:string}={};
  if(data.schemaVersion>=2){costPin.costCatalogVersion=versionId(data.costCatalogVersion);
    if(typeof data.costCatalogHash!=="string"||!/^[a-f0-9]{64}$/.test(data.costCatalogHash))invalid();costPin.costCatalogHash=data.costCatalogHash;}
  return { schemaVersion: data.schemaVersion as 1|2|3, ...costPin, ...(data.schemaVersion===3?{bankPolicy:validateBankPolicy(data.bankPolicy)}:{}), version, approval: "synthetic_emulator_only", logisticsVersion: logistics.version, logisticsHash: digest(logistics),
    economyVersion: economy.version, economyHash: configHash(economy), epochDurationMs: data.epochDurationMs as number,
    offerTtlMs: data.offerTtlMs as number, maxOffers: data.maxOffers as number, maxAcceptedPerEpoch: data.maxAcceptedPerEpoch as number,
    routes, cargo, templates };
}
export type ContractConfig = ReturnType<typeof validateContractConfig>;
export function epochFor(now: number, duration: number) { safe(now); if (safe(duration,604800000) < 1) invalid();return Math.floor(now / duration); }
export function contractIdFor(companyId: string, epoch: number, slot: number) { safe(epoch);safe(slot,4);return digest({companyId,epoch,slot}); }
export function makeTerms(config: ContractConfig, slot: number, assets: Selection, economy: EconomyConfig, costs?:CostCatalog) {
  const template = config.templates[slot];if (!template) invalid();
  const extension:{settlement?:CostQuote;bankPolicy?:ReturnType<typeof bankPolicy>}={};
  if(config.schemaVersion===3)extension.bankPolicy=bankPolicy();
  if(config.schemaVersion>=2){
    if(!costs || costs.version!==config.costCatalogVersion || digest(costs)!==config.costCatalogHash)invalid();
    extension.settlement=makeCostQuote(costs,template.id,template.routeId,template.cargoId,rewardAmount(template.quantity,template.rewardPerUnit,economy.maxBalance));
  }
  return { ...extension, templateId: template.id, route: config.routes.find(r => r.id === template.routeId)!, cargo: config.cargo.find(c => c.id === template.cargoId)!,
    quantity: template.quantity, reward: rewardAmount(template.quantity,template.rewardPerUnit,economy.maxBalance),
    currency: economy.currency, scale: economy.scale, minLevel: template.minLevel,
    requirements: { truck: template.truckCapability, driver: template.driverCapability, trailer: template.trailerCapability },
    candidateAssets: validateSelection(assets), configVersion: config.version, configHash: digest(config),
    logisticsVersion: config.logisticsVersion, logisticsHash: config.logisticsHash, economyVersion: config.economyVersion, economyHash: config.economyHash };
}
export type ContractTerms = ReturnType<typeof makeTerms>;
/** Snapshot validation does not consult mutable current config: accepted terms never reprice. */
export function validateContract(data: DocumentData | undefined, id: string, companyId: string) {
  if (!data) invalid();
  const accepted = data.status !== "offered";
  exact(data, "schemaVersion,companyId,contractId,status,epoch,slot,terms,termsHash,createdAt,expiresAt" + (accepted ? ",acceptedAt" : ""));
  if (![1,2,3].includes(data.schemaVersion) || data.companyId !== companyId || data.contractId !== id ||
      !CONTRACT_STATUSES.includes(data.status) || id !== contractIdFor(companyId, data.epoch, data.slot) ||
      !(data.createdAt instanceof Timestamp) || !(data.expiresAt instanceof Timestamp) || data.createdAt.toMillis() >= data.expiresAt.toMillis() ||
      (accepted && (!(data.acceptedAt instanceof Timestamp) || data.acceptedAt.toMillis() < data.createdAt.toMillis() || data.acceptedAt.toMillis() >= data.expiresAt.toMillis()))) invalid();
  const t = data.terms;
  exact(t, "templateId,route,cargo,quantity,reward,currency,scale,minLevel,requirements,candidateAssets,configVersion,configHash,logisticsVersion,logisticsHash,economyVersion,economyHash"+(data.schemaVersion>=2?",settlement":"")+(data.schemaVersion===3?",bankPolicy":""));
  if(data.schemaVersion===3)validateBankPolicy(t.bankPolicy);
  if(data.schemaVersion>=2)validateCostQuote(t.settlement,t.reward);
  exact(t.route,"id,originCityId,destinationCityId,distance,unit,verification");exact(t.cargo,"id,unit,capability");exact(t.requirements,"truck,driver,trailer");
  for (const value of [t.templateId,t.route.id,t.route.originCityId,t.route.destinationCityId,t.route.unit,t.cargo.id,t.cargo.unit,t.cargo.capability,
    t.requirements.truck,t.requirements.driver,t.configVersion,t.logisticsVersion,t.economyVersion]) versionId(value);
  if (t.requirements.trailer !== null) versionId(t.requirements.trailer);
  if (t.route.originCityId === t.route.destinationCityId || t.route.verification !== "synthetic_fixture" || safe(t.route.distance) < 1 ||
      safe(t.quantity) < 1 || safe(t.minLevel,1000) < 1 || !/^[A-Z]{3}$/.test(t.currency)) invalid();
  safe(t.reward);safe(t.scale,6);validateSelection(t.candidateAssets);
  for (const hash of [t.configHash,t.logisticsHash,t.economyHash,data.termsHash]) if (typeof hash !== "string" || !/^[a-f0-9]{64}$/.test(hash)) invalid();
  if (data.termsHash !== digest(t)) invalid();
  return { schemaVersion:data.schemaVersion as 1|2|3, contractId: id, terms: t as ContractTerms, status: data.status as ContractStatus,
    epoch: data.epoch as number, slot: data.slot as number, createdAt: data.createdAt as Timestamp,
    expiresAt: data.expiresAt as Timestamp, acceptedAt: accepted ? data.acceptedAt as Timestamp : null };
}
export function expired(status: ContractStatus, expiresAt: number, now: number): boolean { return status === "offered" && now >= expiresAt; }
export function acceptPayload(data: unknown): { contractId: string } {
  if (!data || typeof data !== "object" || Array.isArray(data) || Object.keys(data).join(",") !== "contractId") throw new CommandError("INVALID_ARGUMENT");
  try { return { contractId: versionId((data as DocumentData).contractId) }; }
  catch { throw new CommandError("INVALID_ARGUMENT"); }
}
export function contractDto(data: ReturnType<typeof validateContract>, now: number) {
  const t = data.terms;
  return { ...settlementDto(t), contractId: data.contractId as string, status: data.status, expired: expired(data.status,data.expiresAt.toMillis(),now),
    originCityId:t.route.originCityId,destinationCityId:t.route.destinationCityId,routeId:t.route.id,cargoId:t.cargo.id,
    quantity:String(t.quantity),unit:t.cargo.unit,reward:String(t.reward),currency:t.currency,scale:t.scale,requirements:t.requirements,
    minLevel:t.minLevel,configVersion:t.configVersion,logisticsVersion:t.logisticsVersion,economyVersion:t.economyVersion,
    expiresAt:data.expiresAt.toDate().toISOString(),acceptedAt:data.acceptedAt?.toDate().toISOString() ?? null };
}
