import { DocumentData, FieldValue, Firestore, Timestamp, Transaction } from "firebase-admin/firestore";
import { CommandError, executeCommand, parseCommandRequest } from "./commands";
import { readFoundation } from "./company";
import { clockTime, ServerClock, serverClock } from "./contractModel";
import { operationId, planEconomyMutation } from "./economy";
import { exact, invalid, safe, versionId } from "./economyModel";
import { readStarter } from "./fleet";
import { requireUid, validateIdentityRequest } from "./identity";
import { CompanyState } from "./logisticsContext";
import { digest } from "./logisticsModel";
import { assertLocalEnvironment } from "./localEnvironment";
import { checkedAdd, amountIntent, bankAnchor, bankCatalog, creditLimit, Loan, loanIdFor, loanTerms, offerIdFor, quoteDto, repayment, validateLoan, validateQuote } from "./bankModel";

async function ready(tx:Transaction,db:Firestore,state:CompanyState) {
  if(!state.economy||!(await readStarter(tx,db,state)).ready)invalid();
}
/** Explicit trusted final LOCAL onboarding step. Not exported as a callable; never a wallet writer. */
export function initializeBank(db:Firestore,auth:{uid:string}|undefined,data:unknown,catalogVersion:string) {
  const version=versionId(catalogVersion),states=new WeakMap<Transaction,CompanyState>();
  return executeCommand(db,auth,data,{
    name:"initializeBank",validate(p){exact(p as DocumentData,"");return {catalogVersion:version};},
    async resolveCompany(tx,uid){const s=await readFoundation(tx,db,uid);states.set(tx,s);return s.companyId;},
    async apply(tx){const state=states.get(tx)!;await ready(tx,db,state);
      const ref=state.companyRef.collection("bank").doc("current"),marker=state.companyRef.collection("provisioning").doc("bank");
      if((await tx.getAll(ref,marker)).some(s=>s.exists))invalid();
      const c=bankCatalog((await tx.get(db.collection("v2BankCatalog").doc(version))).data(),version,state.economy!.config);
      const timestamp=FieldValue.serverTimestamp();
      tx.create(ref,{schemaVersion:1,companyId:state.companyId,catalogVersion:version,catalogHash:digest(c),sequence:0,activeLoanId:null,lastClosedLoanId:null,
        currentQuoteId:null,generation:0,budgetWindow:0,requests:0,writes:0,createdAt:timestamp});
      tx.create(marker,{schemaVersion:1,companyId:state.companyId,catalogVersion:version,catalogHash:digest(c),committedAt:timestamp});
      return {bankReady:true,catalogVersion:version};
    }
  });
}
/** Fixed point reads; the last payment and origination bind mutable debt to immutable evidence. */
export async function readBank(tx:Transaction,db:Firestore,state:CompanyState) {
  assertLocalEnvironment(process.env,true);if(!state.economy)invalid();
  const ref=state.companyRef.collection("bank").doc("current"),a=bankAnchor((await tx.get(ref)).data(),state.companyId);
  const c=bankCatalog((await tx.get(db.collection("v2BankCatalog").doc(a.catalogVersion))).data(),a.catalogVersion,state.economy.config);
  if(digest(c)!==a.catalogHash)invalid();creditLimit(c,state.economy.level);
  const marker=(await tx.get(state.companyRef.collection("provisioning").doc("bank"))).data();
  exact(marker,"schemaVersion,companyId,catalogVersion,catalogHash,committedAt");
  if(marker.schemaVersion!==1||marker.companyId!==state.companyId||marker.catalogVersion!==c.version||marker.catalogHash!==a.catalogHash||
    !(marker.committedAt instanceof Timestamp)||!marker.committedAt.isEqual(a.createdAt))invalid();
  let loan:Loan|null=null;
  const loadLoan=async(id:string)=>{
    const l=validateLoan((await tx.get(state.companyRef.collection("loans").doc(id))).data(),id,state.companyId,c);
    const source=`bank_origination_${id}`,op=operationId(state.companyId,source);
    const [journal,ledger,claim,offer]=await tx.getAll(state.companyRef.collection("bankJournals").doc(op),state.companyRef.collection("ledger").doc(op),
      state.companyRef.collection("claims").doc(source),state.companyRef.collection("loanOffers").doc(l.offerId));
    const j=journal!.data(),o=validateQuote(offer!.data(),l.offerId,state.companyId,c);
    exact(j,"schemaVersion,kind,companyId,loanId,source,operationId,terms,previousBalance,balanceAfter,committedAt");
    if(j.schemaVersion!==1||j.kind!=="origination"||j.companyId!==state.companyId||j.loanId!==id||j.source!==source||j.operationId!==op||digest(j.terms)!==digest(l.terms)||
      o.status!=="consumed"||o.sequence+1!==l.sequence||digest(o.terms)!==digest(l.terms)||o.level!==l.level||
      checkedAdd(safe(j.previousBalance,state.economy!.config.maxBalance),l.terms.principal,state.economy!.config.maxBalance)!==j.balanceAfter||
      !(j.committedAt instanceof Timestamp)||!j.committedAt.isEqual(l.createdAt))invalid();
    validateEvidence(ledger!.data(),claim!.data(),state,source,l.terms.principal,j.balanceAfter,"bank_origination",j.committedAt);
    if(l.lastPaymentSource!==null){
      const p=(await tx.get(state.companyRef.collection("loans").doc(id).collection("repayments").doc(l.lastPaymentSource))).data();
      validatePayment(p,l,state);
      if(p.paymentSequence!==l.paymentSequence||p.remainingPrincipal!==l.remainingPrincipal||p.remainingFee!==l.remainingFee||
        (l.status==="paid"&&!l.closedAt!.isEqual(p.committedAt)))invalid();
      const operation=operationId(state.companyId,l.lastPaymentSource);
      const [pl,pc,pj]=await tx.getAll(state.companyRef.collection("ledger").doc(operation),state.companyRef.collection("claims").doc(l.lastPaymentSource),
        state.companyRef.collection(p.kind==="manual"?"bankJournals":"settlementJournals").doc(operation));
      const journalData=pj!.data();
      if(!journalData||journalData.loanId!==id||journalData.source!==l.lastPaymentSource||digest(journalData.repayment)!==digest(paymentNumbers(p))||
        !(journalData.committedAt instanceof Timestamp)||!journalData.committedAt.isEqual(p.committedAt))invalid();
      if(p.kind==="manual"){
        exact(journalData,"schemaVersion,kind,companyId,loanId,source,operationId,repayment,termsHash,previousBalance,balanceAfter,committedAt");
        if(journalData.schemaVersion!==1||journalData.kind!=="manual"||journalData.companyId!==state.companyId||journalData.operationId!==operation||
          journalData.termsHash!==digest(l.terms)||journalData.previousBalance!==p.previousBalance||journalData.balanceAfter!==p.balanceAfter||p.walletDelta!==-p.total)invalid();
      }
      validateEvidence(pl!.data(),pc!.data(),state,l.lastPaymentSource,p.walletDelta,p.balanceAfter,p.kind==="manual"?"bank_repayment":"delivery_net_reward",p.committedAt);
    }
    return l;
  };
  if(a.activeLoanId!==null){loan=await loadLoan(a.activeLoanId);if(loan.status!=="active"||loan.sequence!==a.sequence)invalid();}
  else if(a.sequence>0){if(!a.lastClosedLoanId)invalid();const closed=await loadLoan(a.lastClosedLoanId);if(closed.status!=="paid"||closed.sequence!==a.sequence)invalid();}
  let quote:ReturnType<typeof validateQuote>|null=null;
  if(a.currentQuoteId!==null){quote=validateQuote((await tx.get(state.companyRef.collection("loanOffers").doc(a.currentQuoteId))).data(),a.currentQuoteId,state.companyId,c);
    if(quote.status!=="available"||quote.generation!==a.generation||quote.sequence!==a.sequence||loan)invalid();}
  return {ref,a,c,loan,quote};
}
function validateEvidence(l:DocumentData|undefined,c:DocumentData|undefined,s:CompanyState,source:string,delta:number,balance:number,reason:string,time:Timestamp){
  exact(l,"schemaVersion,operationId,reason,delta,balanceAfter,currency,scale,source,configVersion,rulesVersion,committedAt");
  exact(c,"schemaVersion,source,operationId,configVersion,committedAt");const e=s.economy!.config,op=operationId(s.companyId,source);
  if(l.schemaVersion!==1||l.operationId!==op||l.reason!==reason||l.delta!==delta||l.balanceAfter!==balance||l.currency!==e.currency||l.scale!==e.scale||
    l.source!==source||l.configVersion!==e.version||l.rulesVersion!==e.rulesVersion||!(l.committedAt instanceof Timestamp)||!l.committedAt.isEqual(time)||
    c.schemaVersion!==1||c.source!==source||c.operationId!==op||c.configVersion!==e.version||!(c.committedAt instanceof Timestamp)||!c.committedAt.isEqual(time))invalid();
}
const paymentNumbers=(p:DocumentData)=>({total:p.total,fee:p.fee,principal:p.principal,debtBefore:p.debtBefore,debtAfter:p.debtAfter,remainingPrincipal:p.remainingPrincipal,remainingFee:p.remainingFee});
function validatePayment(p:DocumentData|undefined,l:Loan,s:CompanyState):asserts p is DocumentData {
  exact(p,"schemaVersion,loanId,companyId,source,kind,paymentSequence,total,fee,principal,debtBefore,debtAfter,remainingPrincipal,remainingFee,walletDelta,previousBalance,balanceAfter,committedAt");
  if(p.schemaVersion!==1||p.loanId!==l.loanId||p.companyId!==s.companyId||p.source!==l.lastPaymentSource||!['manual','delivery'].includes(p.kind)||!(p.committedAt instanceof Timestamp)||safe(p.paymentSequence)<1||
    safe(p.total)<1||checkedAdd(safe(p.fee,l.terms.fee),safe(p.principal,l.terms.principal))!==p.total||
    checkedAdd(safe(p.remainingPrincipal,l.terms.principal),safe(p.remainingFee,l.terms.fee))!==p.debtAfter||
    checkedAdd(p.debtAfter,p.total,l.terms.liability)!==p.debtBefore||
    checkedAdd(safe(p.previousBalance,s.economy!.config.maxBalance),p.walletDelta,s.economy!.config.maxBalance)!==p.balanceAfter)invalid();
}
export type BankState=Awaited<ReturnType<typeof readBank>>;
export async function uniqueBankOperation(tx:Transaction,s:CompanyState,source:string){
  assertLocalEnvironment(process.env,true);
  const op=operationId(s.companyId,source);
  if((await tx.getAll(s.companyRef.collection("claims").doc(source),s.companyRef.collection("ledger").doc(op),s.companyRef.collection("bankJournals").doc(op))).some(d=>d.exists))invalid();
  return op;
}
/** No wallet write here: caller commits the ONE combined delta using planEconomyMutation. */
export async function prepareLoanPayment(tx:Transaction,s:CompanyState,b:BankState,source:string,requested:number,available:bigint,kind:"manual"|"delivery",profit=0){
  assertLocalEnvironment(process.env,true);
  const l=b.loan;if(!l)invalid();const p=repayment(l,requested,available);
  const ref=s.companyRef.collection("loans").doc(l.loanId),history=ref.collection("repayments").doc(source);
  if((await tx.get(history)).exists)invalid();
  const walletDelta=profit-p.total,balanceAfter=checkedAdd(s.economy!.balance,walletDelta,s.economy!.config.maxBalance);
  const paymentSequence=checkedAdd(l.paymentSequence,1);
  return {repayment:p,loanId:l.loanId,apply(){if(p.total===0)return;
    const time=FieldValue.serverTimestamp();
    tx.update(ref,{remainingPrincipal:p.remainingPrincipal,remainingFee:p.remainingFee,status:p.debtAfter===0?"paid":"active",paymentSequence,lastPaymentSource:source,closedAt:p.debtAfter===0?time:null});
    if(p.debtAfter===0)tx.update(b.ref,{activeLoanId:null,lastClosedLoanId:l.loanId});
    tx.create(history,{schemaVersion:1,loanId:l.loanId,companyId:s.companyId,source,kind,paymentSequence,...p,walletDelta,previousBalance:s.economy!.balance,balanceAfter,committedAt:time});
  }};
}
export type BankCommand="quoteCompanyLoan"|"cancelCompanyLoanQuote"|"takeCompanyLoan"|"repayCompanyLoan";
export function bankCommand(db:Firestore,auth:{uid:string}|undefined,data:unknown,kind:BankCommand,clock:ServerClock=serverClock){
  const states=new WeakMap<Transaction,CompanyState>();
  // Key is used only to derive a permanent manual-payment source; executor owns its validation and receipt.
  return executeCommand(db,auth,data,{
    name:kind,validate(raw){
      const p=raw as DocumentData,keys=kind==="quoteCompanyLoan"?"requestedMinorUnits":kind==="repayCompanyLoan"?"loanId,requestedMinorUnits":"offerId";
      try{exact(p,keys);if('requestedMinorUnits' in p)amountIntent(p.requestedMinorUnits);if('loanId'in p)versionId(p.loanId);if('offerId'in p)versionId(p.offerId);}catch{throw new CommandError("INVALID_ARGUMENT");}
      return p as Record<string,string>;
    },
    async resolveCompany(tx,uid){const state=await readFoundation(tx,db,uid);states.set(tx,state);return state.companyId;},
    async apply(tx,uid,p){const s=states.get(tx)!;await ready(tx,db,s);const b=await readBank(tx,db,s),{a,c}=b,e=s.economy!,now=clockTime(clock);
      if(kind==="quoteCompanyLoan"||kind==="cancelCompanyLoanQuote"){
        const window=Math.floor(now/c.budgetWindowMs),requests=checkedAdd(a.budgetWindow===window?a.requests:0,1),oldWrites=a.budgetWindow===window?a.writes:0;
        if(requests>c.maxQuoteRequests)throw new CommandError("BANK_RATE_LIMITED");
        if(kind==="cancelCompanyLoanQuote"){
          if(!b.quote||b.quote.offerId!==p.offerId)throw new CommandError("BANK_QUOTE_STALE");
          if(oldWrites>=c.maxQuoteWrites)throw new CommandError("BANK_RATE_LIMITED");
          tx.update(s.companyRef.collection("loanOffers").doc(b.quote.offerId),{status:"cancelled"});
          tx.update(b.ref,{currentQuoteId:null,budgetWindow:window,requests,writes:oldWrites+1});return {offerId:p.offerId!,status:"cancelled"};
        }
        if(b.loan)throw new CommandError("BANK_ACTIVE_LOAN");
        const terms=loanTerms(c,amountIntent(p.requestedMinorUnits),e.level);checkedAdd(e.balance,terms.principal,e.config.maxBalance);
        const reusable=b.quote&&b.quote.level===e.level&&b.quote.xp===e.xp&&b.quote.createdAt.toMillis()<=now&&now<b.quote.expiresAt.toMillis()&&digest(b.quote.terms)===digest(terms);
        if(reusable){tx.update(b.ref,{budgetWindow:window,requests,writes:oldWrites});return quoteDto(b.quote!,now);}
        if(oldWrites>=c.maxQuoteWrites)throw new CommandError("BANK_RATE_LIMITED");
        const generation=checkedAdd(a.generation,1),offerId=offerIdFor(s.companyId,generation),ref=s.companyRef.collection("loanOffers").doc(offerId);
        if((await tx.get(ref)).exists)invalid();
        const quote={schemaVersion:1 as const,offerId,companyId:s.companyId,status:"available",generation,sequence:a.sequence,level:e.level,xp:e.xp,terms,
          createdAt:Timestamp.fromMillis(now),expiresAt:Timestamp.fromMillis(checkedAdd(now,c.quoteTtlMs,253402300799999))};
        if(b.quote)tx.update(s.companyRef.collection("loanOffers").doc(b.quote.offerId),{status:"replaced"});
        tx.create(ref,quote);tx.update(b.ref,{currentQuoteId:offerId,generation,budgetWindow:window,requests,writes:oldWrites+1});return quoteDto(quote,now);
      }
      if(kind==="takeCompanyLoan"){
        if(b.loan)throw new CommandError("BANK_ACTIVE_LOAN");const q=b.quote;
        if(!q||q.offerId!==p.offerId||q.level!==e.level||q.xp!==e.xp||now<q.createdAt.toMillis())throw new CommandError("BANK_QUOTE_STALE");
        if(now>=q.expiresAt.toMillis())throw new CommandError("OFFER_EXPIRED");
        const sequence=checkedAdd(a.sequence,1),loanId=loanIdFor(s.companyId,sequence),ref=s.companyRef.collection("loans").doc(loanId),source=`bank_origination_${loanId}`;
        if((await tx.get(ref)).exists)invalid();const op=await uniqueBankOperation(tx,s,source);
        const money=planEconomyMutation(tx,s,e.config,source,q.terms.principal,0,"bank_origination"),time=FieldValue.serverTimestamp();
        const result=money.apply();
        tx.create(ref,{schemaVersion:1,loanId,companyId:s.companyId,sequence,offerId:q.offerId,level:q.level,terms:q.terms,status:"active",remainingPrincipal:q.terms.principal,
          remainingFee:q.terms.fee,paymentSequence:0,lastPaymentSource:null,createdAt:time,closedAt:null});
        tx.update(b.ref,{sequence,activeLoanId:loanId,currentQuoteId:null});tx.update(s.companyRef.collection("loanOffers").doc(q.offerId),{status:"consumed"});
        tx.create(s.companyRef.collection("bankJournals").doc(op),{schemaVersion:1,kind:"origination",companyId:s.companyId,loanId,source,operationId:op,terms:q.terms,
          previousBalance:e.balance,balanceAfter:Number(result.balance),committedAt:time});
        return {loanId,principal:String(q.terms.principal),fee:String(q.terms.fee),liability:String(q.terms.liability),balance:result.balance,currency:c.currency,scale:c.scale};
      }
      if(!b.loan||b.loan.loanId!==p.loanId)throw new CommandError("BANK_LOAN_NOT_ACTIVE");
      const source=`bank_manual_${digest({uid,key:parseCommandRequest(data).idempotencyKey})}`,op=await uniqueBankOperation(tx,s,source);
      const payment=await prepareLoanPayment(tx,s,b,source,amountIntent(p.requestedMinorUnits),BigInt(e.balance),"manual"),r=payment.repayment;
      if(r.total===0)throw new CommandError("BANK_NO_CAPACITY");
      const money=planEconomyMutation(tx,s,e.config,source,-r.total,0,"bank_repayment");
      const result=money.apply();payment.apply();
      tx.create(s.companyRef.collection("bankJournals").doc(op),{schemaVersion:1,kind:"manual",companyId:s.companyId,loanId:b.loan.loanId,source,operationId:op,
        repayment:r,termsHash:digest(b.loan.terms),previousBalance:e.balance,balanceAfter:Number(result.balance),committedAt:FieldValue.serverTimestamp()});
      return {loanId:b.loan.loanId,actual:String(r.total),principal:String(r.principal),fee:String(r.fee),remainingDebt:String(r.debtAfter),balance:result.balance,status:r.debtAfter===0?"paid":"active",currency:c.currency,scale:c.scale};
    }
  });
}
export async function readMyBankState(db:Firestore,auth:{uid:string}|undefined,data:unknown,clock:ServerClock=serverClock){
  assertLocalEnvironment(process.env,true);const uid=requireUid(auth);validateIdentityRequest(data);
  try{return await db.runTransaction(async tx=>{const s=await readFoundation(tx,db,uid);await ready(tx,db,s);const b=await readBank(tx,db,s),e=s.economy!,l=b.loan,now=clockTime(clock);
    return {protocolVersion:1,serverTime:new Date(now).toISOString(),result:{bankReady:true,level:e.level,eligible:l===null,ineligibilityCode:l?"BANK_ACTIVE_LOAN":null,
      maximumPrincipal:String(creditLimit(b.c,e.level)),minimumPrincipal:String(b.c.minPrincipal),increment:String(b.c.increment),availableCredit:String(l?0:Math.max(0,Math.min(creditLimit(b.c,e.level),e.config.maxBalance-e.balance))),
      currency:b.c.currency,scale:b.c.scale,catalogVersion:b.c.version,currentQuote:b.quote?quoteDto(b.quote,now):null,
      loan:l?{loanId:l.loanId,status:l.status,principal:String(l.terms.principal),fee:String(l.terms.fee),remainingPrincipal:String(l.remainingPrincipal),remainingFee:String(l.remainingFee),
        remainingDebt:String(checkedAdd(l.remainingPrincipal,l.remainingFee)),repaymentBps:l.terms.repaymentBps,reserve:String(l.terms.reserve)}:null}};
  },{readOnly:true});}catch(e){if(e instanceof CommandError)throw e;throw new CommandError("INTERNAL");}
}
