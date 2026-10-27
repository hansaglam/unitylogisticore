import { DocumentData } from "firebase-admin/firestore";
import { configHash, EconomyConfig, exact, invalid, safe, versionId } from "./economyModel";
import { digest, LogisticsCatalog } from "./logisticsModel";
import { adjustedReward, MarketCatalog, MarketQuote } from "./marketModel";

export const LIVEOPS_POLICY = {version:1,selection:"highest_priority_then_ascii_id",composition:"add_bps_clamp_single_floor",maxEffectiveEvents:1} as const;
export const liveOpsPolicyHash=()=>digest(LIVEOPS_POLICY);
const MAX_TIME=253402300799999,MAX_DURATION=2678400000;
export function eventBps(value:unknown,min=-1000,max=1500):number {
  if(typeof value!=="number"||!Number.isSafeInteger(value)||value<min||value>max)invalid();return value;
}
function localizationKey(value:unknown):string {
  if(typeof value!=="string"||!/^events\.[a-z][a-z0-9_.]{0,95}$/.test(value))invalid();return value;
}
function scope(value:unknown,allowed:string[],max:number):string[]{
  if(!Array.isArray(value)||value.length<1||value.length>max)invalid();
  const ids=value.map(versionId);if(new Set(ids).size!==ids.length||ids.some(id=>!allowed.includes(id)))invalid();return ids;
}
function eventDefinition(raw:DocumentData,cities:string[],cargo:string[],min:number,max:number){
  exact(raw,"eventId,type,titleKey,descriptionKey,startsAt,endsAt,cityIds,cargoIds,adjustmentBps,priority,enabled");
  const startsAt=safe(raw.startsAt,MAX_TIME),endsAt=safe(raw.endsAt,MAX_TIME),adjustmentBps=eventBps(raw.adjustmentBps,min,max);
  if(startsAt>=endsAt||endsAt-startsAt>MAX_DURATION||typeof raw.enabled!=="boolean"||
    !["regional_demand_surge","logistics_congestion","seasonal_trade_activity"].includes(raw.type)||
    (raw.type==="regional_demand_surge"&&adjustmentBps<=0)||(raw.type==="logistics_congestion"&&adjustmentBps>=0))invalid();
  return {eventId:versionId(raw.eventId),type:raw.type as string,titleKey:localizationKey(raw.titleKey),descriptionKey:localizationKey(raw.descriptionKey),
    startsAt,endsAt,cityIds:scope(raw.cityIds,cities,16),cargoIds:scope(raw.cargoIds,cargo,5),adjustmentBps,priority:safe(raw.priority,1000),enabled:raw.enabled as boolean};
}
export function validateLiveOpsCatalog(raw:DocumentData|undefined,version:string,e:EconomyConfig,l:LogisticsCatalog,m:MarketCatalog){
  exact(raw,"schemaVersion,version,approval,economyVersion,economyHash,logisticsVersion,logisticsHash,marketVersion,marketHash,policy,policyHash,currency,scale,minEventBps,maxEventBps,minCombinedBps,maxCombinedBps,maxEvents,events");
  if(raw.schemaVersion!==1||raw.version!==versionId(version)||raw.approval!=="synthetic_emulator_only"||raw.economyVersion!==e.version||raw.economyHash!==configHash(e)||
    raw.logisticsVersion!==l.version||raw.logisticsHash!==digest(l)||raw.marketVersion!==m.version||raw.marketHash!==digest(m)||
    raw.currency!==e.currency||raw.scale!==e.scale||digest(raw.policy)!==digest(LIVEOPS_POLICY)||raw.policyHash!==liveOpsPolicyHash())invalid();
  const minEventBps=eventBps(raw.minEventBps,-1000,0),maxEventBps=eventBps(raw.maxEventBps,0,1500);
  // The combined envelope cannot widen the pinned market's economic bounds.
  const minCombinedBps=eventBps(raw.minCombinedBps,m.minAdjustmentBps,0),maxCombinedBps=eventBps(raw.maxCombinedBps,0,m.maxAdjustmentBps);
  if(minCombinedBps!==m.minAdjustmentBps||maxCombinedBps!==m.maxAdjustmentBps)invalid();
  const maxEvents=safe(raw.maxEvents,16);if(maxEvents<1||!Array.isArray(raw.events)||raw.events.length>maxEvents)invalid();
  const events=(raw.events as DocumentData[]).map(v=>eventDefinition(v,l.cities,m.cargo.map(c=>c.cargoId),minEventBps,maxEventBps));
  if(new Set(events.map(v=>v.eventId)).size!==events.length)invalid();
  const catalog={schemaVersion:1,version,approval:"synthetic_emulator_only",economyVersion:e.version,economyHash:configHash(e),logisticsVersion:l.version,logisticsHash:digest(l),
    marketVersion:m.version,marketHash:digest(m),policy:LIVEOPS_POLICY,policyHash:liveOpsPolicyHash(),currency:e.currency,scale:e.scale,minEventBps,maxEventBps,minCombinedBps,maxCombinedBps,maxEvents,events};
  digest(catalog);return catalog;
}
export type LiveOpsCatalog=ReturnType<typeof validateLiveOpsCatalog>;
export type WorldEvent=LiveOpsCatalog["events"][number];
export function eventStatus(event:WorldEvent,now:number){safe(now,MAX_TIME);return now<event.startsAt?"upcoming":now<event.endsAt?"active":"ended";}
export function activeEventIds(c:LiveOpsCatalog,now:number){return c.events.filter(e=>e.enabled&&eventStatus(e,now)==="active").map(e=>e.eventId).sort();}
export function selectEvent(c:LiveOpsCatalog,now:number,city:string,cargo:string){
  safe(now,MAX_TIME);
  return c.events.filter(e=>e.enabled&&eventStatus(e,now)==="active"&&e.cityIds.includes(city)&&e.cargoIds.includes(cargo))
    .sort((a,b)=>b.priority-a.priority||(a.eventId<b.eventId?-1:a.eventId>b.eventId?1:0))[0]??null;
}
export function liveOpsQuote(c:LiveOpsCatalog,m:MarketQuote,selectedAt:number,maximum:number){
  safe(selectedAt,MAX_TIME);const s=m.snapshot;
  if(s.marketVersion!==c.marketVersion||s.marketHash!==c.marketHash||selectedAt<s.startsAt||selectedAt>=s.endsAt)invalid();
  const event=selectEvent(c,selectedAt,s.cityId,s.cargoId),eventAdjustmentBps=event?.adjustmentBps??0;
  const combinedAdjustmentBps=Math.max(c.minCombinedBps,Math.min(c.maxCombinedBps,s.adjustmentBps+eventAdjustmentBps));
  const body={pricingVersion:5,catalogVersion:c.version,catalogHash:digest(c),policyVersion:c.policy.version,policyHash:c.policyHash,selectedAt,selectedEvent:event,
    marketQuoteHash:digest(m),baseGrossReward:m.baseGrossReward,marketGrossReward:m.adjustedGrossReward,marketAdjustmentBps:s.adjustmentBps,eventAdjustmentBps,
    minCombinedBps:c.minCombinedBps,maxCombinedBps:c.maxCombinedBps,combinedAdjustmentBps,finalGrossReward:adjustedReward(m.baseGrossReward,combinedAdjustmentBps,maximum)};
  return {...body,snapshotHash:digest(body)};
}
export type LiveOpsQuote=ReturnType<typeof liveOpsQuote>;
/** Full catalog recomputation is additionally mandatory at each non-replay DB lifecycle transition. */
export function validateLiveOpsQuote(raw:DocumentData|undefined,m:MarketQuote,reward:number,createdAt:number):asserts raw is LiveOpsQuote {
  exact(raw,"pricingVersion,catalogVersion,catalogHash,policyVersion,policyHash,selectedAt,selectedEvent,marketQuoteHash,baseGrossReward,marketGrossReward,marketAdjustmentBps,eventAdjustmentBps,minCombinedBps,maxCombinedBps,combinedAdjustmentBps,finalGrossReward,snapshotHash");
  versionId(raw.catalogVersion);safe(raw.selectedAt,MAX_TIME);eventBps(raw.minCombinedBps,-1000,0);eventBps(raw.maxCombinedBps,0,1500);eventBps(raw.eventAdjustmentBps);
  const {snapshotHash,...body}=raw,s=m.snapshot;
  if(raw.pricingVersion!==5||raw.policyVersion!==1||raw.policyHash!==liveOpsPolicyHash()||typeof raw.catalogHash!=="string"||!/^[a-f0-9]{64}$/.test(raw.catalogHash)||
    raw.selectedAt!==createdAt||raw.selectedAt<s.startsAt||raw.selectedAt>=s.endsAt||raw.marketQuoteHash!==digest(m)||raw.baseGrossReward!==m.baseGrossReward||
    raw.marketGrossReward!==m.adjustedGrossReward||raw.marketAdjustmentBps!==s.adjustmentBps||digest(body)!==snapshotHash)invalid();
  if(raw.selectedEvent===null){if(raw.eventAdjustmentBps!==0)invalid();}
  else {const e=eventDefinition(raw.selectedEvent,raw.selectedEvent?.cityIds??[],raw.selectedEvent?.cargoIds??[],-1000,1500);
    if(!e.enabled||eventStatus(e,raw.selectedAt)!=="active"||!e.cityIds.includes(s.cityId)||!e.cargoIds.includes(s.cargoId)||e.adjustmentBps!==raw.eventAdjustmentBps)invalid();}
  const combined=Math.max(raw.minCombinedBps,Math.min(raw.maxCombinedBps,s.adjustmentBps+raw.eventAdjustmentBps));
  if(raw.combinedAdjustmentBps!==combined||raw.finalGrossReward!==adjustedReward(m.baseGrossReward,combined,Number.MAX_SAFE_INTEGER)||raw.finalGrossReward!==reward)invalid();
}
export function liveOpsDto(q:LiveOpsQuote){return {pricingVersion:5,catalogVersion:q.catalogVersion,eventId:q.selectedEvent?.eventId??null,selectedAt:new Date(q.selectedAt).toISOString(),
  baseGrossReward:String(q.baseGrossReward),marketGrossReward:String(q.marketGrossReward),eventAdjustmentBps:q.eventAdjustmentBps,combinedAdjustmentBps:q.combinedAdjustmentBps,finalGrossReward:String(q.finalGrossReward)};}
