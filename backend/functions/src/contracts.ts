import { DocumentData, FieldPath, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { CommandError, executeCommand, parseCommandRequest } from "./commands";
import { readFoundation } from "./company";
import { exact, invalid, safe, versionId } from "./economyModel";
import { Selection, validateSelection } from "./fleet";
import { requireUid } from "./identity";
import { CompanyState } from "./logisticsContext";
import { digest, pageRequest } from "./logisticsModel";
import { assertLocalEnvironment } from "./localEnvironment";
import { acceptPayload, clockTime, contractDto, contractIdFor, epochFor, expired, makeTerms, ServerClock, serverClock, validateContract } from "./contractModel";
import { readContractConfig, requireContractEligibility } from "./contractEligibility";

const poolRef = (state: CompanyState, epoch: number) => state.companyRef.collection("contractPools").doc(`epoch_${safe(epoch)}`);
/** Validate the entire bounded pool, including aggregate quota, without repricing snapshots. */
export async function readPool(tx: Transaction, state: CompanyState, epoch: number) {
  const ref = poolRef(state,epoch); const snapshot = await tx.get(ref);
  if (!snapshot.exists) return null;
  const marker = snapshot.data();
  exact(marker,"schemaVersion,companyId,epoch,epochDurationMs,offerTtlMs,configVersion,configHash,termsHashes,acceptedCount,maxAcceptedPerEpoch,createdAt");
  versionId(marker.configVersion);
  if (marker.schemaVersion !== 1 || marker.companyId !== state.companyId || marker.epoch !== epoch ||
      safe(marker.epochDurationMs,604800000)<1 || safe(marker.offerTtlMs,marker.epochDurationMs)<1 ||
      !Array.isArray(marker.termsHashes) || marker.termsHashes.length<1 || marker.termsHashes.length>5 ||
      safe(marker.maxAcceptedPerEpoch,marker.termsHashes.length)<1 || !(marker.createdAt instanceof Timestamp) ||
      epochFor(marker.createdAt.toMillis(),marker.epochDurationMs)!==epoch) invalid();
  safe(marker.acceptedCount,marker.maxAcceptedPerEpoch);
  const refs=marker.termsHashes.map((_:unknown,i:number)=>state.companyRef.collection("contracts").doc(contractIdFor(state.companyId,epoch,i)));
  const docs=await tx.getAll(...refs);
  const contracts=docs.map((doc,i)=>{
    const value=validateContract(doc.data(),doc.id,state.companyId);
    if(value.epoch!==epoch || value.slot!==i || value.terms.configVersion!==marker.configVersion || value.terms.configHash!==marker.configHash ||
      digest(value.terms)!==marker.termsHashes[i] || !value.createdAt.isEqual(marker.createdAt) ||
      value.expiresAt.toMillis()!==epoch*marker.epochDurationMs+marker.offerTtlMs) invalid();
    return value;
  });
  if(contracts.filter(c=>c.status!=="offered").length!==marker.acceptedCount) invalid();
  return {ref,marker,contracts,refs};
}
/** Trusted backend plan only: epoch and candidate assets are never public request fields. */
export function generateContractOffers(db: Firestore, auth: {uid:string}|undefined, data:unknown,
  plan:{configVersion:string;epoch:number;assets:Selection}, clock:ServerClock=serverClock) {
  const version=versionId(plan.configVersion);const epoch=safe(plan.epoch);const assets=validateSelection(plan.assets);
  const states=new WeakMap<Transaction,CompanyState>();
  const key=parseCommandRequest(data).idempotencyKey;
  return executeCommand(db,auth,data,{
    name:"generateContractOffers",
    validate(payload){exact(payload as DocumentData,"");return {configVersion:version,epoch,assets};},
    async resolveCompany(tx,uid){
      const state=await readFoundation(tx,db,uid);if(!state.economy) invalid();
      if(!await readPool(tx,state,epoch)) {
        const remnants=await tx.getAll(state.playerRef.collection("commands").doc(key),
          ...Array.from({length:5},(_,slot)=>state.companyRef.collection("contracts").doc(contractIdFor(state.companyId,epoch,slot))));
        if(remnants.some(doc=>doc.exists)) invalid();
      }
      states.set(tx,state);return state.companyId;
    },
    async apply(tx){
      const state=states.get(tx)!;
      const {config}=await readContractConfig(tx,db,state,version);
      if((await tx.get(poolRef(state,epoch))).exists) invalid();
      const now=clockTime(clock); const expiry=epoch*config.epochDurationMs+config.offerTtlMs;
      if(epochFor(now,config.epochDurationMs)!==epoch || now>=expiry) throw new CommandError("OFFER_EXPIRED");
      const terms=config.templates.map((_,slot)=>makeTerms(config,slot,assets,state.economy!.config));
      const refs=terms.map((_,slot)=>state.companyRef.collection("contracts").doc(contractIdFor(state.companyId,epoch,slot)));
      if((await tx.getAll(...refs)).some(doc=>doc.exists)) invalid();
      for(const term of terms) await requireContractEligibility(tx,db,state,term);
      const createdAt=Timestamp.fromMillis(now),expiresAt=Timestamp.fromMillis(expiry);
      for(let slot=0;slot<terms.length;slot++) tx.create(refs[slot]!,{schemaVersion:1,companyId:state.companyId,contractId:refs[slot]!.id,
        status:"offered",epoch,slot,terms:terms[slot],termsHash:digest(terms[slot]),createdAt,expiresAt});
      tx.create(poolRef(state,epoch),{schemaVersion:1,companyId:state.companyId,epoch,epochDurationMs:config.epochDurationMs,offerTtlMs:config.offerTtlMs,
        configVersion:version,configHash:digest(config),termsHashes:terms.map(digest),acceptedCount:0,maxAcceptedPerEpoch:config.maxAcceptedPerEpoch,createdAt});
      return {epoch,contractIds:refs.map(ref=>ref.id)};
    }
  });
}
export function acceptContractCommand(db:Firestore,auth:{uid:string}|undefined,data:unknown,clock:ServerClock=serverClock) {
  const states=new WeakMap<Transaction,CompanyState>();
  return executeCommand(db,auth,data,{
    name:"acceptContract",validate:acceptPayload,
    // Only current authorization/consistency here. Historical replay bypasses current offer/availability predicates.
    async resolveCompany(tx,uid){const state=await readFoundation(tx,db,uid);if(!state.economy) invalid();states.set(tx,state);return state.companyId;},
    async apply(tx,_uid,payload){
      const state=states.get(tx)!;const ref=state.companyRef.collection("contracts").doc(payload.contractId);
      const contract=validateContract((await tx.get(ref)).data(),payload.contractId,state.companyId);
      const pool=await readPool(tx,state,contract.epoch);if(!pool) invalid();
      if(contract.status!=="offered") throw new CommandError("CONTRACT_NOT_OFFERED");
      const {config}=await readContractConfig(tx,db,state,contract.terms.configVersion);
      if(digest(makeTerms(config,contract.slot,contract.terms.candidateAssets,state.economy!.config))!==digest(contract.terms)) invalid();
      await requireContractEligibility(tx,db,state,contract.terms);
      const now=clockTime(clock);
      if(now<contract.createdAt.toMillis() || expired(contract.status,contract.expiresAt.toMillis(),now)) throw new CommandError("OFFER_EXPIRED");
      if(pool.marker.acceptedCount>=pool.marker.maxAcceptedPerEpoch) throw new CommandError("CONTRACT_QUOTA_REACHED");
      tx.update(ref,{status:"accepted",acceptedAt:Timestamp.fromMillis(now)});
      tx.update(pool.ref,{acceptedCount:pool.marker.acceptedCount+1});
      return {contractId:payload.contractId,status:"accepted",acceptedAt:new Date(now).toISOString(),reward:String(contract.terms.reward),currency:contract.terms.currency,
        scale:contract.terms.scale,equipmentReserved:false};
    }
  });
}
export async function readMyContracts(db:Firestore,auth:{uid:string}|undefined,data:unknown,clock:ServerClock=serverClock) {
  assertLocalEnvironment(process.env,true);const uid=requireUid(auth);const page=pageRequest(data);
  try {
    const result=await db.runTransaction(async tx=>{
      const state=await readFoundation(tx,db,uid);if(!state.companyData) invalid();
      let query=state.companyRef.collection("contracts").orderBy(FieldPath.documentId()).limit(page.limit+1);
      if(page.after!==null) query=query.startAfter(page.after);
      const records=await tx.get(query);if(!state.economy && !records.empty) invalid();
      const pools=new Map<number,Awaited<ReturnType<typeof readPool>>>();const contracts=[];
      for(const doc of records.docs.slice(0,page.limit)){
        const contract=validateContract(doc.data(),doc.id,state.companyId);
        if(!pools.has(contract.epoch)) pools.set(contract.epoch,await readPool(tx,state,contract.epoch));
        if(!pools.get(contract.epoch)?.contracts.some(c=>c.contractId===doc.id)) invalid();contracts.push(contract);
      }
      const now=clockTime(clock);
      return {items:contracts.map(c=>contractDto(c,now)),nextCursor:records.size>page.limit?records.docs[page.limit-1]!.id:null,serverTime:new Date(now).toISOString()};
    },{readOnly:true});
    return {protocolVersion:1,serverTime:result.serverTime,result:{items:result.items,nextCursor:result.nextCursor}};
  }catch(error){if(error instanceof CommandError)throw error;throw new CommandError("INTERNAL");}
}
