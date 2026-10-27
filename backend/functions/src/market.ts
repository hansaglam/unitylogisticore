import { Firestore, Transaction } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { readFoundation } from "./company";
import { readContractConfig } from "./contractEligibility";
import { clockTime, ContractTerms, makeTerms, ServerClock, serverClock } from "./contractModel";
import { exact, invalid, versionId } from "./economyModel";
import { readStarter } from "./fleet";
import { requireUid, validateIdentityRequest } from "./identity";
import { assertLocalEnvironment } from "./localEnvironment";
import { CompanyState } from "./logisticsContext";
import { digest } from "./logisticsModel";
import { marketEpoch, marketSnapshot } from "./marketModel";

/** Recompute only at the ORIGINAL pinned epoch. No current-market selector is read here. */
export async function readPinnedMarket(tx:Transaction,db:Firestore,state:CompanyState,t:ContractTerms){
  assertLocalEnvironment(process.env,true);if(!t.market)return;
  const {config,costs,market,liveOps}=await readContractConfig(tx,db,state,t.configVersion);
  if(![4,5].includes(config.schemaVersion)||!market||digest(config)!==t.configHash)invalid();
  const slot=config.templates.findIndex(x=>x.id===t.templateId);if(slot<0)invalid();
  if(digest(makeTerms(config,slot,t.candidateAssets,state.economy!.config,costs,market,t.market.snapshot.epoch,liveOps,t.liveOps?.selectedAt))!==digest(t))invalid();
}

/** Server-owned selector is scoped to the canonical economy version, never client input.
 * Publishing/activation is intentionally not a callable or automatic initializer.
 */
export async function readMyMarketState(db:Firestore,auth:{uid:string}|undefined,data:unknown,clock:ServerClock=serverClock){
  assertLocalEnvironment(process.env,true);const uid=requireUid(auth);validateIdentityRequest(data);
  try{return await db.runTransaction(async tx=>{
    const s=await readFoundation(tx,db,uid);if(!s.economy||!(await readStarter(tx,db,s)).ready)invalid();
    const selector=(await tx.get(db.collection("v2MarketSelection").doc(s.economy.config.version))).data();
    exact(selector,"schemaVersion,approval,contractVersion,contractHash");
    if(selector.schemaVersion!==1||selector.approval!=="synthetic_emulator_only")invalid();
    const {config,market}=await readContractConfig(tx,db,s,versionId(selector.contractVersion));
    if(!market||digest(config)!==selector.contractHash)invalid();
    const now=clockTime(clock),epoch=marketEpoch(now,market.periodMs);
    const conditions=market.regions.flatMap(r=>market.cargo.map(c=>{
      const v=marketSnapshot(market,epoch,r.cityId,c.cargoId);
      return {cityId:v.cityId,cargoId:v.cargoId,demandIndex:v.demandIndex,supplyIndex:v.supplyIndex,adjustmentBps:v.adjustmentBps,trendBps:0};
    }));
    return {protocolVersion:1,serverTime:new Date(now).toISOString(),result:{synthetic:true,marketVersion:market.version,policy:market.policy.policy,policyVersion:market.policy.version,
      epoch,periodEndsAt:new Date((epoch+1)*market.periodMs).toISOString(),conditions}};
  },{readOnly:true});}catch(e){if(e instanceof CommandError)throw e;throw new CommandError("INTERNAL");}
}
