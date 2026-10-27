import { Firestore } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { clockTime, ServerClock, serverClock } from "./contractModel";
import { readFoundation } from "./company";
import { readContractConfig } from "./contractEligibility";
import { exact, invalid, versionId } from "./economyModel";
import { readStarter } from "./fleet";
import { requireUid, validateIdentityRequest } from "./identity";
import { assertLocalEnvironment } from "./localEnvironment";
import { digest } from "./logisticsModel";
import { eventStatus } from "./liveOpsModel";

/** Bounded immutable calendar; no activation, initialization or financial writes. */
export async function readMyWorldEvents(db:Firestore,auth:{uid:string}|undefined,data:unknown,clock:ServerClock=serverClock){
  assertLocalEnvironment(process.env,true);const uid=requireUid(auth);validateIdentityRequest(data);
  try{return await db.runTransaction(async tx=>{
    const s=await readFoundation(tx,db,uid);if(!s.economy||!(await readStarter(tx,db,s)).ready)invalid();
    const selector=(await tx.get(db.collection("v2MarketSelection").doc(s.economy.config.version))).data();
    exact(selector,"schemaVersion,approval,contractVersion,contractHash");
    if(selector.schemaVersion!==1||selector.approval!=="synthetic_emulator_only")invalid();
    const {config,liveOps}=await readContractConfig(tx,db,s,versionId(selector.contractVersion));
    if(!liveOps||digest(config)!==selector.contractHash)invalid();
    const now=clockTime(clock);
    // At most16 events, within next7 days / previous24 hours. Disabled entries never appear.
    const events=liveOps.events.filter(e=>e.enabled&&e.startsAt<=now+604800000&&e.endsAt>now-86400000)
      .sort((a,b)=>a.startsAt-b.startsAt||(a.eventId<b.eventId?-1:a.eventId>b.eventId?1:0))
      .map(e=>({eventId:e.eventId,type:e.type,titleKey:e.titleKey,descriptionKey:e.descriptionKey,startsAt:new Date(e.startsAt).toISOString(),endsAt:new Date(e.endsAt).toISOString(),
        cityIds:e.cityIds,cargoIds:e.cargoIds,adjustmentBps:e.adjustmentBps,status:eventStatus(e,now)}));
    return {protocolVersion:1,serverTime:new Date(now).toISOString(),result:{synthetic:true,catalogVersion:liveOps.version,policyVersion:liveOps.policy.version,
      currentEventIds:events.filter(e=>e.status==="active").map(e=>e.eventId),events}};
  },{readOnly:true});}catch(e){if(e instanceof CommandError)throw e;throw new CommandError("INTERNAL");}
}
