import { FieldValue, Timestamp, Transaction } from "firebase-admin/firestore";
import { ContractTerms } from "./contractModel";
import { operationId, prepareEconomyReward } from "./economy";
import { checkedAdd, exact, invalid, safe } from "./economyModel";
import { CompanyState } from "./logisticsContext";
import { digest } from "./logisticsModel";
import { assertLocalEnvironment } from "./localEnvironment";

/** BANK-01 may extend this plan inside the same transaction. No debt/payment side effect exists here. */
export function settlementAccounting(terms:ContractTerms,eligibleXp:number,deliveryId:string) {
  const grossRevenue=terms.reward,totalOperatingExpenses=terms.settlement?.totalOperatingCosts??0;
  return {grossRevenue,totalOperatingExpenses,netOperatingProfit:grossRevenue-totalOperatingExpenses,
    finalWalletDelta:terms.settlement?.netReward??grossRevenue,eligibleXp,source:`delivery_${deliveryId}`};
}
export async function prepareDeliverySettlement(tx:Transaction,state:CompanyState,deliveryId:string,terms:ContractTerms,xp:number) {
  assertLocalEnvironment(process.env,true);
  const accounting=settlementAccounting(terms,xp,deliveryId),opId=operationId(state.companyId,accounting.source);
  const journalRef=state.companyRef.collection("settlementJournals").doc(opId);
  if(terms.settlement && (await tx.get(journalRef)).exists)invalid();
  const payment=await prepareEconomyReward(tx,state,deliveryId,accounting.finalWalletDelta,xp,terms.currency,terms.scale,terms.economyHash,
    terms.settlement?"delivery_net_reward":"delivery_reward");
  const previousBalance=state.economy!.balance;
  return {accounting,apply:()=>{
    const result=payment.apply();
    if(terms.settlement)tx.create(journalRef,{schemaVersion:1,settlementVersion:2,companyId:state.companyId,deliveryId,source:accounting.source,operationId:opId,
      termsHash:digest(terms),quote:terms.settlement,currency:terms.currency,scale:terms.scale,economyVersion:terms.economyVersion,economyHash:terms.economyHash,
      logisticsVersion:terms.logisticsVersion,logisticsHash:terms.logisticsHash,previousBalance,balanceAfter:Number(result.balance),eligibleXp:xp,committedAt:FieldValue.serverTimestamp()});
    return result;
  }};
}
/** Completed v2 reads validate explanation against the original financial claim; never repair it. */
export async function validateSettlementJournal(tx:Transaction,state:CompanyState,deliveryId:string,terms:ContractTerms,xp:number) {
  assertLocalEnvironment(process.env,true);
  if(!terms.settlement)return;
  if(!state.economy)invalid();
  const source=`delivery_${deliveryId}`,opId=operationId(state.companyId,source),config=state.economy.config;
  const [journal,ledger,claim]=await tx.getAll(state.companyRef.collection("settlementJournals").doc(opId),state.companyRef.collection("ledger").doc(opId),state.companyRef.collection("claims").doc(source));
  const j=journal!.data(),l=ledger!.data(),c=claim!.data();
  exact(j,"schemaVersion,settlementVersion,companyId,deliveryId,source,operationId,termsHash,quote,currency,scale,economyVersion,economyHash,logisticsVersion,logisticsHash,previousBalance,balanceAfter,eligibleXp,committedAt");
  exact(l,"schemaVersion,operationId,reason,delta,balanceAfter,currency,scale,source,configVersion,rulesVersion,committedAt");
  exact(c,"schemaVersion,source,operationId,configVersion,committedAt");
  if(j.schemaVersion!==1||j.settlementVersion!==2||j.companyId!==state.companyId||j.deliveryId!==deliveryId||j.source!==source||j.operationId!==opId||
    j.termsHash!==digest(terms)||digest(j.quote)!==digest(terms.settlement)||j.currency!==terms.currency||j.scale!==terms.scale||
    j.economyVersion!==terms.economyVersion||j.economyHash!==terms.economyHash||j.logisticsVersion!==terms.logisticsVersion||j.logisticsHash!==terms.logisticsHash||
    j.eligibleXp!==xp||!(j.committedAt instanceof Timestamp)||
    checkedAdd(safe(j.previousBalance,config.maxBalance),terms.settlement.netReward,config.maxBalance)!==j.balanceAfter||
    l.schemaVersion!==1||l.operationId!==opId||l.reason!=="delivery_net_reward"||l.delta!==terms.settlement.netReward||l.balanceAfter!==j.balanceAfter||
    l.currency!==terms.currency||l.scale!==terms.scale||l.source!==source||l.configVersion!==terms.economyVersion||l.rulesVersion!==config.rulesVersion||
    !(l.committedAt instanceof Timestamp)||!l.committedAt.isEqual(j.committedAt)||c.schemaVersion!==1||c.source!==source||c.operationId!==opId||
    c.configVersion!==terms.economyVersion||!(c.committedAt instanceof Timestamp)||!c.committedAt.isEqual(j.committedAt))invalid();
}
