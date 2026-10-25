import { DocumentData, Timestamp } from "firebase-admin/firestore";
import { CommandError } from "./commands";
import { EconomyConfig, checkedAdd as boundedAdd, configHash, exact, invalid, safe, versionId } from "./economyModel";
import { digest } from "./logisticsModel";
import { NET_POLICY } from "./operatingCosts";

export const checkedAdd=(value:number,delta:number,max=Number.MAX_SAFE_INTEGER)=>boundedAdd(value,delta,max);
export const BANK_POLICY = { settlementVersion: 3, policy: "synthetic_current_loan_fee_first_v1" } as const;
export const bankPolicy = () => ({ ...BANK_POLICY, policyHash: digest(BANK_POLICY) });
export function validateBankPolicy(raw: DocumentData) {
  exact(raw,"settlementVersion,policy,policyHash");
  if (digest(raw)!==digest(bankPolicy())) invalid();
  return bankPolicy();
}
export function amountIntent(raw: unknown): number {
  if(typeof raw!=="string" || !/^[1-9][0-9]{0,15}$/.test(raw) || !Number.isSafeInteger(Number(raw))) throw new CommandError("INVALID_ARGUMENT");
  return Number(raw);
}
export function bankCatalog(raw: DocumentData | undefined, version: string, economy: EconomyConfig) {
  exact(raw,"schemaVersion,version,approval,economyVersion,economyHash,operatingPolicy,bankPolicyHash,currency,scale,minLevel,maxLevel,tiers,minPrincipal,increment,chargeBps,minFee,repaymentBps,reserve,quoteTtlMs,budgetWindowMs,maxQuoteRequests,maxQuoteWrites,maxLiability");
  if(raw.schemaVersion!==1 || raw.version!==versionId(version) || raw.approval!=="synthetic_emulator_only" || raw.economyVersion!==economy.version ||
    raw.economyHash!==configHash(economy) || raw.currency!==economy.currency || raw.scale!==economy.scale || raw.operatingPolicy!==NET_POLICY ||
    raw.bankPolicyHash!==bankPolicy().policyHash || raw.minLevel!==1 || raw.maxLevel!==economy.thresholds.length) invalid();
  const minPrincipal=safe(raw.minPrincipal,economy.maxBalance),increment=safe(raw.increment,economy.maxBalance);
  if(minPrincipal<1 || increment<1 || minPrincipal%increment!==0 || safe(raw.minFee)<1 || safe(raw.quoteTtlMs,3600000)<1 ||
    safe(raw.budgetWindowMs,3600000)<1 || safe(raw.maxQuoteRequests,100)<1 || safe(raw.maxQuoteWrites,raw.maxQuoteRequests)<1) invalid();
  safe(raw.chargeBps,10000);safe(raw.repaymentBps,10000);safe(raw.reserve,economy.maxBalance);safe(raw.maxLiability);
  if(!Array.isArray(raw.tiers)||raw.tiers.length<1||raw.tiers.length>1000)invalid();
  let next=1;
  const tiers=raw.tiers.map((t:DocumentData)=>{
    exact(t,"from,to,maximum");
    if(t.from!==next || safe(t.to,raw.maxLevel)<t.from || safe(t.maximum,economy.maxBalance)<minPrincipal || t.maximum%increment!==0)invalid();
    next=t.to+1;
    const fee=Math.max(raw.minFee,Number(BigInt(t.maximum)*BigInt(raw.chargeBps)/10000n));
    checkedAdd(t.maximum,fee,raw.maxLiability);
    return {from:t.from as number,to:t.to as number,maximum:t.maximum as number};
  });
  if(next!==raw.maxLevel+1)invalid();
  return {...raw,tiers} as {
    schemaVersion:1;version:string;approval:string;economyVersion:string;economyHash:string;operatingPolicy:string;bankPolicyHash:string;currency:string;scale:number;
    minLevel:number;maxLevel:number;tiers:typeof tiers;minPrincipal:number;increment:number;chargeBps:number;minFee:number;repaymentBps:number;reserve:number;
    quoteTtlMs:number;budgetWindowMs:number;maxQuoteRequests:number;maxQuoteWrites:number;maxLiability:number;
  };
}
export type BankCatalog=ReturnType<typeof bankCatalog>;
export function creditLimit(c:BankCatalog,level:number) {
  const tier=c.tiers.find(t=>level>=t.from&&level<=t.to);if(!tier)invalid();return tier.maximum;
}
export function loanTerms(c:BankCatalog,principal:number,level:number) {
  if(safe(principal)<c.minPrincipal || principal>creditLimit(c,level) || principal%c.increment!==0)throw new CommandError("INVALID_ARGUMENT");
  const fee=Math.max(c.minFee,Number(BigInt(principal)*BigInt(c.chargeBps)/10000n));
  return {principal,fee,liability:checkedAdd(principal,fee,c.maxLiability),repaymentBps:c.repaymentBps,reserve:c.reserve,
    currency:c.currency,scale:c.scale,catalogVersion:c.version,catalogHash:digest(c),bankPolicyHash:c.bankPolicyHash};
}
export type LoanTerms=ReturnType<typeof loanTerms>;
export function validateTerms(raw:DocumentData,c:BankCatalog,level:number):LoanTerms {
  if(digest(raw)!==digest(loanTerms(c,raw.principal,level)))invalid();return raw as LoanTerms;
}
export function bankAnchor(raw:DocumentData|undefined,companyId:string) {
  exact(raw,"schemaVersion,companyId,catalogVersion,catalogHash,sequence,activeLoanId,lastClosedLoanId,currentQuoteId,generation,budgetWindow,requests,writes,createdAt");
  if(raw.schemaVersion!==1||raw.companyId!==companyId||!(raw.createdAt instanceof Timestamp))invalid();
  versionId(raw.catalogVersion);if(!/^[a-f0-9]{64}$/.test(raw.catalogHash))invalid();
  for(const k of ["sequence","generation","budgetWindow","requests","writes"])safe(raw[k]);
  for(const k of ["activeLoanId","lastClosedLoanId","currentQuoteId"])if(raw[k]!==null)versionId(raw[k]);
  if(raw.sequence===0&&(raw.activeLoanId!==null||raw.lastClosedLoanId!==null))invalid();
  return raw as {schemaVersion:1;companyId:string;catalogVersion:string;catalogHash:string;sequence:number;activeLoanId:string|null;lastClosedLoanId:string|null;currentQuoteId:string|null;generation:number;budgetWindow:number;requests:number;writes:number;createdAt:Timestamp};
}
export function offerIdFor(companyId:string,generation:number) {return digest({domain:"bank_quote_v1",companyId,generation});}
export function loanIdFor(companyId:string,sequence:number) {return digest({domain:"bank_loan_v1",companyId,sequence});}
export function validateQuote(raw:DocumentData|undefined,id:string,companyId:string,c:BankCatalog) {
  exact(raw,"schemaVersion,offerId,companyId,status,generation,sequence,level,xp,terms,createdAt,expiresAt");
  if(raw.schemaVersion!==1||raw.offerId!==id||raw.companyId!==companyId||!['available','replaced','cancelled','consumed'].includes(raw.status)||
    safe(raw.generation)<1||id!==offerIdFor(companyId,raw.generation)||!(raw.createdAt instanceof Timestamp)||!(raw.expiresAt instanceof Timestamp)||
    raw.expiresAt.toMillis()!==checkedAdd(raw.createdAt.toMillis(),c.quoteTtlMs,253402300799999))invalid();
  safe(raw.sequence);safe(raw.xp);safe(raw.level,c.maxLevel);validateTerms(raw.terms,c,raw.level);
  return raw as {schemaVersion:1;offerId:string;companyId:string;status:string;generation:number;sequence:number;level:number;xp:number;terms:LoanTerms;createdAt:Timestamp;expiresAt:Timestamp};
}
export function validateLoan(raw:DocumentData|undefined,id:string,companyId:string,c:BankCatalog) {
  exact(raw,"schemaVersion,loanId,companyId,sequence,offerId,level,terms,status,remainingPrincipal,remainingFee,paymentSequence,lastPaymentSource,createdAt,closedAt");
  if(raw.schemaVersion!==1||raw.loanId!==id||raw.companyId!==companyId||safe(raw.sequence)<1||id!==loanIdFor(companyId,raw.sequence)||
    !['active','paid'].includes(raw.status)||!(raw.createdAt instanceof Timestamp))invalid();
  versionId(raw.offerId);validateTerms(raw.terms,c,raw.level);
  safe(raw.remainingPrincipal,raw.terms.principal);safe(raw.remainingFee,raw.terms.fee);safe(raw.paymentSequence);
  if(raw.remainingFee>0&&raw.remainingPrincipal!==raw.terms.principal)invalid();
  const debt=checkedAdd(raw.remainingPrincipal,raw.remainingFee,c.maxLiability);
  if((raw.status==='paid')!==(debt===0)||(raw.status==='paid'?!(raw.closedAt instanceof Timestamp):raw.closedAt!==null)||
    (raw.paymentSequence===0 ? raw.lastPaymentSource!==null||debt!==raw.terms.liability : typeof raw.lastPaymentSource!=="string"))invalid();
  return raw as {schemaVersion:1;loanId:string;companyId:string;sequence:number;offerId:string;level:number;terms:LoanTerms;status:"active"|"paid";remainingPrincipal:number;remainingFee:number;paymentSequence:number;lastPaymentSource:string|null;createdAt:Timestamp;closedAt:Timestamp|null};
}
export type Loan=ReturnType<typeof validateLoan>;
export function repayment(loan:Loan,requested:number,available:bigint) {
  safe(requested);const debt=checkedAdd(loan.remainingPrincipal,loan.remainingFee);
  const capacity=available-BigInt(loan.terms.reserve);
  const total=Number([BigInt(requested),BigInt(debt),capacity>0n?capacity:0n].reduce((a,b)=>a<b?a:b));
  const fee=Math.min(total,loan.remainingFee),principal=total-fee;
  return {total,fee,principal,debtBefore:debt,debtAfter:debt-total,remainingPrincipal:loan.remainingPrincipal-principal,remainingFee:loan.remainingFee-fee};
}
export function quoteDto(q:ReturnType<typeof validateQuote>,now:number) {
  return {offerId:q.offerId,status:q.status==='available'&&now>=q.expiresAt.toMillis()?'expired':q.status,principal:String(q.terms.principal),fee:String(q.terms.fee),
    liability:String(q.terms.liability),repaymentBps:q.terms.repaymentBps,reserve:String(q.terms.reserve),currency:q.terms.currency,scale:q.terms.scale,
    catalogVersion:q.terms.catalogVersion,expiresAt:q.expiresAt.toDate().toISOString()};
}
