import { DocumentData, Firestore, Transaction } from "firebase-admin/firestore";
import { configHash, EconomyConfig, exact, invalid, safe, versionId } from "./economyModel";
import { digest, LogisticsCatalog } from "./logisticsModel";
import { catalogReader, CompanyState } from "./logisticsContext";
import type { ContractTerms } from "./contractModel";
import { assertLocalEnvironment } from "./localEnvironment";

export const NET_POLICY = "synthetic_net_claim_v1";
export function costTotal(costs: {fuelCost:number;driverCost:number;maintenanceCost:number;tollCost:number}, maximum=Number.MAX_SAFE_INTEGER) {
  exact(costs,"fuelCost,driverCost,maintenanceCost,tollCost");safe(maximum);
  const total=Object.values(costs).reduce((sum,n)=>sum+BigInt(safe(n,maximum)),0n);
  if(total>BigInt(maximum))invalid();return Number(total);
}
export function validateCostCatalog(raw:DocumentData|undefined,version:string,logistics:LogisticsCatalog,economy:EconomyConfig) {
  exact(raw,"schemaVersion,version,approval,economyVersion,economyHash,logisticsVersion,logisticsHash,policy,currency,scale,maxCost,entries");
  if(raw.schemaVersion!==1 || raw.version!==versionId(version) || raw.approval!=="synthetic_emulator_only" ||
    raw.economyVersion!==economy.version || raw.economyHash!==configHash(economy) ||
    raw.logisticsVersion!==logistics.version || raw.logisticsHash!==digest(logistics) || logistics.schemaVersion!==2 ||
    raw.policy!==NET_POLICY || raw.currency!==economy.currency || raw.scale!==economy.scale ||
    !Array.isArray(raw.entries) || raw.entries.length<1 || raw.entries.length>5)invalid();
  const maxCost=safe(raw.maxCost,economy.maxBalance);
  const entries=(raw.entries as DocumentData[]).map(e=>{
    exact(e,"templateId,routeId,cargoId,costs");costTotal(e.costs,maxCost);
    return {templateId:versionId(e.templateId),routeId:versionId(e.routeId),cargoId:versionId(e.cargoId),
      costs:{fuelCost:e.costs.fuelCost as number,driverCost:e.costs.driverCost as number,maintenanceCost:e.costs.maintenanceCost as number,tollCost:e.costs.tollCost as number}};
  });
  if(new Set(entries.map(e=>e.templateId)).size!==entries.length)invalid();
  return {schemaVersion:1,version,approval:"synthetic_emulator_only",economyVersion:economy.version,economyHash:configHash(economy),
    logisticsVersion:logistics.version,logisticsHash:digest(logistics),policy:NET_POLICY,currency:economy.currency,scale:economy.scale,maxCost,entries};
}
export type CostCatalog=ReturnType<typeof validateCostCatalog>;
export function makeCostQuote(c:CostCatalog,templateId:string,routeId:string,cargoId:string,grossReward:number) {
  const entry=c.entries.find(e=>e.templateId===templateId&&e.routeId===routeId&&e.cargoId===cargoId);if(!entry)invalid();
  const totalOperatingCosts=costTotal(entry.costs,c.maxCost);safe(grossReward);
  if(grossReward<=totalOperatingCosts)invalid();
  return {settlementVersion:2,costCatalogVersion:c.version,costCatalogHash:digest(c),policy:c.policy,
    grossReward,...entry.costs,totalOperatingCosts,netReward:grossReward-totalOperatingCosts};
}
export type CostQuote=ReturnType<typeof makeCostQuote>;
export function validateCostQuote(q:DocumentData|undefined,gross:number):asserts q is CostQuote {
  exact(q,"settlementVersion,costCatalogVersion,costCatalogHash,policy,grossReward,fuelCost,driverCost,maintenanceCost,tollCost,totalOperatingCosts,netReward");
  versionId(q.costCatalogVersion);
  if(q.settlementVersion!==2 || q.policy!==NET_POLICY || typeof q.costCatalogHash!=="string" || !/^[a-f0-9]{64}$/.test(q.costCatalogHash) || q.grossReward!==gross)invalid();
  const total=costTotal({fuelCost:q.fuelCost,driverCost:q.driverCost,maintenanceCost:q.maintenanceCost,tollCost:q.tollCost});
  if(safe(gross)<=total || q.totalOperatingCosts!==total || q.netReward!==gross-total)invalid();
}
/** Re-read the pinned immutable version, never a latest catalog pointer. Drift rejects; it never reprices. */
export async function readPinnedCosts(tx:Transaction,db:Firestore,state:CompanyState,terms:ContractTerms) {
  assertLocalEnvironment(process.env,true);
  if(!terms.settlement)return;
  if(!state.economy)invalid();
  const q=terms.settlement, logistics=await catalogReader(tx,db)(terms.logisticsVersion);
  const catalog=validateCostCatalog((await tx.get(db.collection("v2OperatingCostCatalog").doc(q.costCatalogVersion))).data(),q.costCatalogVersion,logistics,state.economy.config);
  if(terms.economyHash!==catalog.economyHash || terms.logisticsHash!==catalog.logisticsHash || terms.currency!==catalog.currency || terms.scale!==catalog.scale ||
    digest(makeCostQuote(catalog,terms.templateId,terms.route.id,terms.cargo.id,terms.reward))!==digest(q))invalid();
}
export function settlementDto(terms:ContractTerms) {
  const q=terms.settlement;
  return q?{settlement:{settlementVersion:terms.bankPolicy?3:2,policy:terms.bankPolicy?.policy??q.policy,grossReward:String(q.grossReward),fuelCost:String(q.fuelCost),driverCost:String(q.driverCost),
    maintenanceCost:String(q.maintenanceCost),tollCost:String(q.tollCost),totalOperatingCosts:String(q.totalOperatingCosts),netReward:String(q.netReward),currency:terms.currency,scale:terms.scale}}:{};
}
