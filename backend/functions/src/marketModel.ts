import { DocumentData } from "firebase-admin/firestore";
import { configHash, EconomyConfig, exact, invalid, safe, versionId } from "./economyModel";
import { digest, LogisticsCatalog } from "./logisticsModel";

export const MARKET_POLICY = {version:1,policy:"synthetic_destination_pressure_v1",rounding:"floor",seedPolicy:"sha256_epoch_city_cargo_v1"} as const;
export const marketPolicyHash = () => digest(MARKET_POLICY);
const MAX_TIME=253402300799999;
function signed(n:unknown,min:number,max:number):number {
  if(typeof n!=="number"||!Number.isSafeInteger(n)||n<min||n>max)invalid();return n;
}
export function validateMarketCatalog(raw:DocumentData|undefined,version:string,l:LogisticsCatalog,e:EconomyConfig,cargoIds:string[]){
  exact(raw,"schemaVersion,version,approval,economyVersion,economyHash,logisticsVersion,logisticsHash,currency,scale,policy,policyHash,periodMs,indexMax,jitter,seed,sensitivityBps,minAdjustmentBps,maxAdjustmentBps,maxGrossReward,regions,cargo");
  if(raw.schemaVersion!==1||raw.version!==versionId(version)||raw.approval!=="synthetic_emulator_only"||raw.economyVersion!==e.version||raw.economyHash!==configHash(e)||
    raw.logisticsVersion!==l.version||raw.logisticsHash!==digest(l)||raw.currency!==e.currency||raw.scale!==e.scale||digest(raw.policy)!==digest(MARKET_POLICY)||raw.policyHash!==marketPolicyHash())invalid();
  if(safe(raw.periodMs,604800000)<1||safe(raw.indexMax,10000)<1||safe(raw.maxGrossReward,e.maxBalance)<1)invalid();
  safe(raw.jitter,raw.indexMax);safe(raw.sensitivityBps,1000);versionId(raw.seed);
  signed(raw.minAdjustmentBps,-1000,0);signed(raw.maxAdjustmentBps,0,1500);
  if(!Array.isArray(raw.regions)||raw.regions.length<1||raw.regions.length>16||!Array.isArray(raw.cargo)||raw.cargo.length<1||raw.cargo.length>5)invalid();
  const regions=raw.regions.map((r:DocumentData)=>{
    exact(r,"cityId,demand,supply,weightBps");if(!l.cities.includes(versionId(r.cityId)))invalid();
    return {cityId:r.cityId as string,demand:safe(r.demand,raw.indexMax),supply:safe(r.supply,raw.indexMax),weightBps:safe(r.weightBps,10000)};
  });
  const cargo=raw.cargo.map((c:DocumentData)=>{
    exact(c,"cargoId,weightBps");if(!cargoIds.includes(versionId(c.cargoId)))invalid();return {cargoId:c.cargoId as string,weightBps:safe(c.weightBps,10000)};
  });
  if(new Set(regions.map(r=>r.cityId)).size!==regions.length||new Set(cargo.map(c=>c.cargoId)).size!==cargo.length)invalid();
  return {schemaVersion:1,version,approval:"synthetic_emulator_only",economyVersion:e.version,economyHash:configHash(e),logisticsVersion:l.version,logisticsHash:digest(l),
    currency:e.currency,scale:e.scale,policy:MARKET_POLICY,policyHash:marketPolicyHash(),periodMs:raw.periodMs as number,indexMax:raw.indexMax as number,jitter:raw.jitter as number,
    seed:raw.seed as string,sensitivityBps:raw.sensitivityBps as number,minAdjustmentBps:raw.minAdjustmentBps as number,maxAdjustmentBps:raw.maxAdjustmentBps as number,
    maxGrossReward:raw.maxGrossReward as number,regions,cargo};
}
export type MarketCatalog=ReturnType<typeof validateMarketCatalog>;
export function marketEpoch(now:number,periodMs:number){safe(now,MAX_TIME);if(safe(periodMs,604800000)<1)invalid();return Math.floor(now/periodMs);}
export function marketSnapshot(c:MarketCatalog,epoch:number,cityId:string,cargoId:string){
  safe(epoch);const start=BigInt(epoch)*BigInt(c.periodMs),end=start+BigInt(c.periodMs);if(end>BigInt(MAX_TIME))invalid();
  const r=c.regions.find(r=>r.cityId===cityId),cargo=c.cargo.find(r=>r.cargoId===cargoId);if(!r||!cargo)invalid();
  const indicator=(base:number,side:string)=>{
    const hash=digest({seed:c.seed,epoch,cityId,cargoId,side,policyHash:c.policyHash});
    const offset=Number(BigInt(`0x${hash.slice(0,16)}`)%BigInt(2*c.jitter+1))-c.jitter;
    return Math.max(0,Math.min(c.indexMax,base+offset));
  };
  const demandIndex=indicator(r.demand,"demand"),supplyIndex=indicator(r.supply,"supply");
  // Signed integer division truncates toward zero; monetary rounding below is floor.
  const raw=BigInt(demandIndex-supplyIndex)*BigInt(c.sensitivityBps)*BigInt(r.weightBps)*BigInt(cargo.weightBps)/100000000n;
  const adjustmentBps=Math.max(c.minAdjustmentBps,Math.min(c.maxAdjustmentBps,Number(raw)));
  const body={schemaVersion:1,marketVersion:c.version,marketHash:digest(c),policyHash:c.policyHash,epoch,startsAt:Number(start),endsAt:Number(end),cityId,cargoId,demandIndex,supplyIndex,adjustmentBps};
  return {...body,snapshotHash:digest(body)};
}
export type MarketSnapshot=ReturnType<typeof marketSnapshot>;
export function adjustedReward(base:number,bps:number,maximum:number){
  safe(base,maximum);signed(bps,-1000,1500);safe(maximum);
  const value=BigInt(base)*BigInt(10000+bps)/10000n;if(value>BigInt(maximum)||value<1n)invalid();return Number(value);
}
export function marketQuote(c:MarketCatalog,epoch:number,cityId:string,cargoId:string,baseGrossReward:number){
  const snapshot=marketSnapshot(c,epoch,cityId,cargoId);
  return {pricingVersion:4,baseGrossReward,adjustedGrossReward:adjustedReward(baseGrossReward,snapshot.adjustmentBps,c.maxGrossReward),snapshot};
}
export type MarketQuote=ReturnType<typeof marketQuote>;
/** Structural/hash/arithmetic checks; the DB adapter also recomputes against the pinned catalog. */
export function validateMarketQuote(raw:DocumentData|undefined,reward:number,cityId:string,cargoId:string):asserts raw is MarketQuote {
  exact(raw,"pricingVersion,baseGrossReward,adjustedGrossReward,snapshot");const s=raw.snapshot;
  exact(s,"schemaVersion,marketVersion,marketHash,policyHash,epoch,startsAt,endsAt,cityId,cargoId,demandIndex,supplyIndex,adjustmentBps,snapshotHash");
  versionId(s.marketVersion);safe(s.epoch);safe(s.startsAt,MAX_TIME);safe(s.endsAt,MAX_TIME);safe(s.demandIndex,10000);safe(s.supplyIndex,10000);
  const {snapshotHash,...body}=s;
  if(raw.pricingVersion!==4||s.schemaVersion!==1||s.cityId!==cityId||s.cargoId!==cargoId||s.policyHash!==marketPolicyHash()||s.startsAt>=s.endsAt||
    typeof s.marketHash!=="string"||!/^[a-f0-9]{64}$/.test(s.marketHash)||digest(body)!==snapshotHash||
    adjustedReward(raw.baseGrossReward,s.adjustmentBps,Number.MAX_SAFE_INTEGER)!==raw.adjustedGrossReward||raw.adjustedGrossReward!==reward)invalid();
}
export function marketDto(q:MarketQuote){return {pricingVersion:4,baseGrossReward:String(q.baseGrossReward),adjustedGrossReward:String(q.adjustedGrossReward),
  adjustmentBps:q.snapshot.adjustmentBps,marketVersion:q.snapshot.marketVersion,epoch:q.snapshot.epoch,cityId:q.snapshot.cityId,cargoId:q.snapshot.cargoId};}
