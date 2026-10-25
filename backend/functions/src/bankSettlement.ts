import { FieldValue, Timestamp, Transaction } from "firebase-admin/firestore";
import { prepareLoanPayment, readBank } from "./bank";
import { checkedAdd, bankCatalog, repayment, validateBankPolicy, validateLoan } from "./bankModel";
import { ContractTerms } from "./contractModel";
import { operationId, prepareEconomyReward } from "./economy";
import { exact, invalid, safe } from "./economyModel";
import { CompanyState } from "./logisticsContext";
import { digest } from "./logisticsModel";
import { assertLocalEnvironment } from "./localEnvironment";

export async function prepareBankSettlement(tx:Transaction,s:CompanyState,id:string,t:ContractTerms,xp:number){
  assertLocalEnvironment(process.env,true);validateBankPolicy(t.bankPolicy!);if(!t.settlement||!s.economy)invalid();
  const b=await readBank(tx,s.companyRef.firestore,s),profit=t.settlement.netReward,source=`delivery_${id}`,op=operationId(s.companyId,source);
  const ref=s.companyRef.collection("settlementJournals").doc(op);if((await tx.get(ref)).exists)invalid();
  const loan=b.loan,available=BigInt(s.economy.balance)+BigInt(profit);
  const target=loan?Number(BigInt(profit)*BigInt(loan.terms.repaymentBps)/10000n):0;
  const payment=loan?await prepareLoanPayment(tx,s,b,source,target,available,"delivery",profit):null;
  const r=payment?.repayment??{total:0,fee:0,principal:0,debtBefore:0,debtAfter:0,remainingPrincipal:0,remainingFee:0};
  const delta=profit-r.total,money=await prepareEconomyReward(tx,s,id,delta,xp,t.currency,t.scale,t.economyHash,"delivery_net_reward");
  const accounting={grossRevenue:t.reward,totalOperatingExpenses:t.settlement.totalOperatingCosts,netOperatingProfit:profit,finalWalletDelta:delta,eligibleXp:xp,source};
  const bankRepayment={settlementVersion:3,loanId:loan?.loanId??null,total:String(r.total),fee:String(r.fee),principal:String(r.principal),debtBefore:String(r.debtBefore),debtAfter:String(r.debtAfter),finalWalletDelta:String(delta)};
  return {accounting,bankRepayment,apply(){const paid=money.apply();payment?.apply();
    tx.create(ref,{schemaVersion:2,settlementVersion:3,companyId:s.companyId,deliveryId:id,source,operationId:op,termsHash:digest(t),quote:t.settlement,bankPolicy:t.bankPolicy,
      currency:t.currency,scale:t.scale,economyVersion:t.economyVersion,economyHash:t.economyHash,logisticsVersion:t.logisticsVersion,logisticsHash:t.logisticsHash,
      previousBalance:s.economy!.balance,balanceAfter:Number(paid.balance),eligibleXp:xp,loanId:loan?.loanId??null,loanTerms:loan?.terms??null,loanLevel:loan?.level??null,
      principalBefore:loan?.remainingPrincipal??0,feeBefore:loan?.remainingFee??0,repayment:r,finalWalletDelta:delta,committedAt:FieldValue.serverTimestamp()});return paid;
  }};
}
export async function validateBankSettlement(tx:Transaction,s:CompanyState,id:string,t:ContractTerms,xp:number){
  assertLocalEnvironment(process.env,true);validateBankPolicy(t.bankPolicy!);if(!t.settlement||!s.economy)invalid();
  const source=`delivery_${id}`,op=operationId(s.companyId,source),e=s.economy.config;
  const [js,ls,cs]=await tx.getAll(s.companyRef.collection("settlementJournals").doc(op),s.companyRef.collection("ledger").doc(op),s.companyRef.collection("claims").doc(source));
  const j=js!.data(),l=ls!.data(),c=cs!.data();
  exact(j,"schemaVersion,settlementVersion,companyId,deliveryId,source,operationId,termsHash,quote,bankPolicy,currency,scale,economyVersion,economyHash,logisticsVersion,logisticsHash,previousBalance,balanceAfter,eligibleXp,loanId,loanTerms,loanLevel,principalBefore,feeBefore,repayment,finalWalletDelta,committedAt");
  exact(l,"schemaVersion,operationId,reason,delta,balanceAfter,currency,scale,source,configVersion,rulesVersion,committedAt");exact(c,"schemaVersion,source,operationId,configVersion,committedAt");
  if(j.schemaVersion!==2||j.settlementVersion!==3||j.companyId!==s.companyId||j.deliveryId!==id||j.source!==source||j.operationId!==op||j.termsHash!==digest(t)||
    digest(j.quote)!==digest(t.settlement)||digest(j.bankPolicy)!==digest(t.bankPolicy)||j.currency!==t.currency||j.scale!==t.scale||j.economyVersion!==t.economyVersion||j.economyHash!==t.economyHash||
    j.logisticsVersion!==t.logisticsVersion||j.logisticsHash!==t.logisticsHash||j.eligibleXp!==xp||!(j.committedAt instanceof Timestamp))invalid();
  let expected={total:0,fee:0,principal:0,debtBefore:0,debtAfter:0,remainingPrincipal:0,remainingFee:0};
  if(j.loanId!==null){
    const raw=(await tx.get(s.companyRef.collection("loans").doc(j.loanId))).data();
    const catalog=bankCatalog((await tx.get(s.companyRef.firestore.collection("v2BankCatalog").doc(j.loanTerms?.catalogVersion))).data(),j.loanTerms?.catalogVersion,e);
    const loan=validateLoan(raw,j.loanId,s.companyId,catalog);
    if(digest(loan.terms)!==digest(j.loanTerms)||loan.level!==j.loanLevel)invalid();
    safe(j.principalBefore,loan.terms.principal);safe(j.feeBefore,loan.terms.fee);
    const before={...loan,remainingPrincipal:j.principalBefore,remainingFee:j.feeBefore};
    if(checkedAdd(before.remainingPrincipal,before.remainingFee)===0)invalid();
    expected=repayment(before,Number(BigInt(t.settlement.netReward)*BigInt(loan.terms.repaymentBps)/10000n),BigInt(safe(j.previousBalance,e.maxBalance))+BigInt(t.settlement.netReward));
    if(expected.total>0){const h=(await tx.get(s.companyRef.collection("loans").doc(j.loanId).collection("repayments").doc(source))).data();
      exact(h,"schemaVersion,loanId,companyId,source,kind,paymentSequence,total,fee,principal,debtBefore,debtAfter,remainingPrincipal,remainingFee,walletDelta,previousBalance,balanceAfter,committedAt");
      if(h.schemaVersion!==1||h.loanId!==loan.loanId||h.companyId!==s.companyId||h.source!==source||h.kind!=="delivery"||safe(h.paymentSequence)<1||h.paymentSequence>loan.paymentSequence||
        h.walletDelta!==j.finalWalletDelta||h.previousBalance!==j.previousBalance||h.balanceAfter!==j.balanceAfter||!(h.committedAt instanceof Timestamp)||!h.committedAt.isEqual(j.committedAt))invalid();
      for(const key of Object.keys(expected) as (keyof typeof expected)[])if(h[key]!==expected[key])invalid();
    }
  }else if(j.loanTerms!==null||j.loanLevel!==null||j.principalBefore!==0||j.feeBefore!==0)invalid();
  if(digest(j.repayment)!==digest(expected)||j.finalWalletDelta!==t.settlement.netReward-expected.total||
    checkedAdd(safe(j.previousBalance,e.maxBalance),j.finalWalletDelta,e.maxBalance)!==j.balanceAfter||l.schemaVersion!==1||l.operationId!==op||l.reason!=="delivery_net_reward"||
    l.delta!==j.finalWalletDelta||l.balanceAfter!==j.balanceAfter||l.currency!==t.currency||l.scale!==t.scale||l.source!==source||l.configVersion!==e.version||l.rulesVersion!==e.rulesVersion||
    !(l.committedAt instanceof Timestamp)||!l.committedAt.isEqual(j.committedAt)||c.schemaVersion!==1||c.source!==source||c.operationId!==op||c.configVersion!==e.version||
    !(c.committedAt instanceof Timestamp)||!c.committedAt.isEqual(j.committedAt))invalid();
}
