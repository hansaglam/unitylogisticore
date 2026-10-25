import { FieldPath, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { CommandError, executeCommand } from "./commands";
import { readFoundation } from "./company";
import { clockTime, ServerClock, serverClock, validateContract } from "./contractModel";
import { requireContractEligibility } from "./contractEligibility";
import { readPool } from "./contracts";
import { prepareDeliverySettlement, validateSettlementJournal } from "./settlement";
import { readPinnedCosts, settlementDto } from "./operatingCosts";
import { configHash, invalid, safe, versionId } from "./economyModel";
import { prepareAssignment, readStarter } from "./fleet";
import { requireUid } from "./identity";
import { CompanyState } from "./logisticsContext";
import { digest, pageRequest } from "./logisticsModel";
import { assertLocalEnvironment } from "./localEnvironment";
import { arrivalTime, claimPayload, deliveryIdFor, startPayload, startSelection, validateDelivery, validateDeliveryRules } from "./deliveryModel";

async function contractState(tx:Transaction,state:CompanyState,id:string) {
  const ref=state.companyRef.collection("contracts").doc(versionId(id));
  const contract=validateContract((await tx.get(ref)).data(),id,state.companyId);
  const pool=await readPool(tx,state,contract.epoch);
  if(!pool?.contracts.some(c=>c.contractId===id))invalid();
  return {ref,contract};
}
async function deliveryState(tx:Transaction,db:Firestore,state:CompanyState,id:string) {
  const ref=state.companyRef.collection("deliveries").doc(versionId(id));const raw=(await tx.get(ref)).data();
  const {ref:contractRef,contract}=await contractState(tx,state,versionId(raw?.contractId));
  const delivery=validateDelivery(raw,id,state.companyId,contract.terms);
  await readPinnedCosts(tx,db,state,contract.terms);
  if(contract.status!==(delivery.status==="active"?"in_progress":"completed") || !contract.acceptedAt || delivery.startedAt.toMillis()<contract.acceptedAt.toMillis())invalid();
  const rules=validateDeliveryRules((await tx.get(db.collection("v2DeliveryRules").doc(contract.terms.configVersion))).data(),contract.terms);
  if(digest(rules.rules)!==delivery.deliveryRulesHash || arrivalTime(delivery.startedAt.toMillis(),rules.rule.durationMs)!==delivery.expectedArrivalAt.toMillis())invalid();
  if(delivery.status==="completed")await validateSettlementJournal(tx,state,id,contract.terms,rules.rule.xp);
  return {ref,contractRef,contract,delivery,rules};
}
export function startDeliveryCommand(db:Firestore,auth:{uid:string}|undefined,data:unknown,clock:ServerClock=serverClock) {
  const states=new WeakMap<Transaction,CompanyState>();
  return executeCommand(db,auth,data,{
    name:"startDelivery",validate:startPayload,
    async resolveCompany(tx,uid){const state=await readFoundation(tx,db,uid);if(!state.economy)invalid();states.set(tx,state);return state.companyId;},
    async apply(tx,_uid,payload){
      const state=states.get(tx)!;const {ref:contractRef,contract}=await contractState(tx,state,payload.contractId);
      if(contract.status!=="accepted")throw new CommandError("DELIVERY_STATE_INVALID");
      const assets=startSelection(payload);if(digest(assets)!==digest(contract.terms.candidateAssets))throw new CommandError("CONTRACT_INELIGIBLE");
      const id=deliveryIdFor(state.companyId,payload.contractId),ref=state.companyRef.collection("deliveries").doc(id);
      if((await tx.get(ref)).exists)invalid();
      await requireContractEligibility(tx,db,state,contract.terms);
      await readPinnedCosts(tx,db,state,contract.terms);
      const rules=validateDeliveryRules((await tx.get(db.collection("v2DeliveryRules").doc(contract.terms.configVersion))).data(),contract.terms);
      if(configHash(state.economy!.config)!==contract.terms.economyHash)invalid();
      safe(rules.rule.xp,state.economy!.config.maxXp);
      const assignment=await prepareAssignment(tx,db,state,assets,id,"reserve");
      const now=clockTime(clock);if(!contract.acceptedAt || now<contract.acceptedAt.toMillis())invalid();
      const arrival=arrivalTime(now,rules.rule.durationMs);
      assignment.apply();
      tx.create(ref,{schemaVersion:contract.schemaVersion,...(contract.terms.settlement?{settlement:contract.terms.settlement}:{}),deliveryId:id,companyId:state.companyId,contractId:payload.contractId,status:"active",assignmentId:assignment.assignmentId,
        assets,termsHash:digest(contract.terms),routeId:contract.terms.route.id,cargoId:contract.terms.cargo.id,startedAt:Timestamp.fromMillis(now),expectedArrivalAt:Timestamp.fromMillis(arrival),
        deliveryRulesVersion:rules.rules.version,deliveryRulesHash:digest(rules.rules),rewardState:"unclaimed"});
      tx.update(contractRef,{status:"in_progress"});
      return {deliveryId:id,contractId:payload.contractId,status:"active",startedAt:new Date(now).toISOString(),expectedArrivalAt:new Date(arrival).toISOString()};
    }
  });
}
export function claimDeliveryRewardCommand(db:Firestore,auth:{uid:string}|undefined,data:unknown,clock:ServerClock=serverClock) {
  const states=new WeakMap<Transaction,CompanyState>();
  return executeCommand(db,auth,data,{
    name:"claimDeliveryReward",validate:claimPayload,
    async resolveCompany(tx,uid){const state=await readFoundation(tx,db,uid);if(!state.economy)invalid();states.set(tx,state);return state.companyId;},
    async apply(tx,_uid,payload){
      const state=states.get(tx)!;const loaded=await deliveryState(tx,db,state,payload.deliveryId);const {delivery,contract,rules}=loaded;
      if(delivery.status!=="active")throw new CommandError("DELIVERY_STATE_INVALID");
      if(!(await readStarter(tx,db,state)).ready)invalid();
      const release=await prepareAssignment(tx,db,state,delivery.assets,delivery.deliveryId,"release");
      const reward=await prepareDeliverySettlement(tx,state,delivery.deliveryId,contract.terms,rules.rule.xp);
      const now=clockTime(clock);if(now<delivery.expectedArrivalAt.toMillis())throw new CommandError("DELIVERY_NOT_ARRIVED");
      release.apply();const paid=reward.apply();
      tx.update(loaded.ref,{status:"completed",rewardState:"claimed",completedAt:Timestamp.fromMillis(now)});
      tx.update(loaded.contractRef,{status:"completed"});
      return {...settlementDto(contract.terms),...(reward.bankRepayment?{bankRepayment:reward.bankRepayment}:{}),deliveryId:delivery.deliveryId,status:"completed",completedAt:new Date(now).toISOString(),reward:String(contract.terms.reward),currency:contract.terms.currency,
        scale:contract.terms.scale,operationId:paid.operationId,balance:paid.balance,xp:paid.xp,level:paid.level};
    }
  });
}
export async function readMyDeliveries(db:Firestore,auth:{uid:string}|undefined,data:unknown,clock:ServerClock=serverClock) {
  assertLocalEnvironment(process.env,true);const uid=requireUid(auth),page=pageRequest(data);
  try{
    const result=await db.runTransaction(async tx=>{
      const state=await readFoundation(tx,db,uid);if(!state.companyData)invalid();
      let query=state.companyRef.collection("deliveries").orderBy(FieldPath.documentId()).limit(page.limit+1);if(page.after)query=query.startAfter(page.after);
      const docs=await tx.get(query);if(!state.economy && !docs.empty)invalid();
      const rows=[];
      for(const doc of docs.docs.slice(0,page.limit)){
        const loaded=await deliveryState(tx,db,state,doc.id);const {delivery,contract}=loaded;
        // Read-only preparation verifies active reservations but never applies release.
        if(delivery.status==="active")await prepareAssignment(tx,db,state,delivery.assets,delivery.deliveryId,"release");
        rows.push({delivery,contract});
      }
      const now=clockTime(clock);
      return {serverTime:new Date(now).toISOString(),items:rows.map(({delivery:d,contract:c})=>({...settlementDto(c.terms),deliveryId:d.deliveryId,contractId:d.contractId,status:d.status,
        originCityId:c.terms.route.originCityId,destinationCityId:c.terms.route.destinationCityId,routeId:c.terms.route.id,cargoId:c.terms.cargo.id,
        quantity:String(c.terms.quantity),unit:c.terms.cargo.unit,assets:d.assets,startedAt:d.startedAt.toDate().toISOString(),expectedArrivalAt:d.expectedArrivalAt.toDate().toISOString(),
        arrivalReady:d.status==="active"&&now>=d.expectedArrivalAt.toMillis(),completedAt:d.completedAt?.toDate().toISOString()??null,
        reward:String(c.terms.reward),currency:c.terms.currency,scale:c.terms.scale})),nextCursor:docs.size>page.limit?docs.docs[page.limit-1]!.id:null};
    },{readOnly:true});
    return {protocolVersion:1,serverTime:result.serverTime,result:{items:result.items,nextCursor:result.nextCursor}};
  }catch(error){if(error instanceof CommandError)throw error;throw new CommandError("INTERNAL");}
}
