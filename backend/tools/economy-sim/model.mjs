import {createHash} from "node:crypto";
// Offline design model only. No Firebase/runtime imports, network, clocks or randomness.
export const MAX = Number.MAX_SAFE_INTEGER;
export class ModelError extends Error {
  constructor(code) { super(code); this.code = code; }
}
const fail = code => { throw new ModelError(code); };
export function integer(value, min = 0, max = MAX) {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < min || value > max) fail('INVALID_INTEGER');
  return value;
}
function exact(value, fields) {
  if (!value || Object.getPrototypeOf(value) !== Object.prototype || Object.keys(value).sort().join(',') !== fields.split(',').sort().join(',')) fail('INVALID_SCHEMA');
}
export function checked(value) {
  if (value > BigInt(MAX) || value < -BigInt(MAX)) fail('OVERFLOW');
  return Number(value);
}
export const add = (...values) => checked(values.reduce((s,v) => s + BigInt(integer(v,-MAX)), 0n));
export function basisPoints(amount, bps) {
  integer(amount); integer(bps,0,10000);
  return checked(BigInt(amount) * BigInt(bps) / 10000n);
}
export function money(text) {
  if (typeof text !== 'string' || !/^(0|[1-9][0-9]*)(\.[0-9]{1,2})?$/.test(text)) fail('INVALID_MONEY_TEXT');
  const [whole, fraction = ''] = text.split('.');
  return checked(BigInt(whole)*100n + BigInt(fraction.padEnd(2,'0')));
}
export function display(value) {
  integer(value,-MAX);const n=BigInt(value),abs=n<0n?-n:n;
  return `${n<0n?'-':''}${abs/100n}.${String(abs%100n).padStart(2,'0')}`;
}
export function validateScenario(raw) {
  exact(raw,'version,approval,name,currency,scale,startingCash,gross,costs,truckCost,driverCost,purchaseReserve,cashReserve,deliveries,costTiming,loan,borrowAfter,autoAcquire');
  if (raw.version !== 'economy-00-v1' || raw.approval !== 'unapproved_design_candidate' || raw.currency !== 'LGC' || raw.scale !== 2 ||
      typeof raw.name !== 'string' || !/^[a-z0-9_-]{1,64}$/.test(raw.name) || typeof raw.autoAcquire !== 'boolean') fail('INVALID_SCENARIO');
  for (const k of ['startingCash','gross','purchaseReserve','cashReserve']) integer(raw[k]);
  integer(raw.truckCost,1);integer(raw.driverCost,1);integer(raw.deliveries,1,10000);
  exact(raw.costs,'fuel,wages,maintenance,tolls');for(const v of Object.values(raw.costs)) integer(v);
  exact(raw.loan,'principal,chargeBps,unlockDeliveries,repaymentBps');integer(raw.loan.principal,1);integer(raw.loan.chargeBps,0,10000);
  integer(raw.loan.unlockDeliveries,0,10000);integer(raw.loan.repaymentBps,0,10000);
  if (!['net_completion','fuel_tolls_upfront','all_upfront'].includes(raw.costTiming)) fail('INVALID_TIMING');
  if (raw.borrowAfter !== null) { integer(raw.borrowAfter,0,raw.deliveries);if(raw.borrowAfter<raw.loan.unlockDeliveries)fail('LOAN_LOCKED'); }
  integer(add(raw.truckCost,raw.driverCost,raw.purchaseReserve));integer(add(...Object.values(raw.costs)));
  integer(add(raw.loan.principal,basisPoints(raw.loan.principal,raw.loan.chargeBps)));
  return structuredClone(raw);
}
function configHash(config) {
  const ordered=v=>v && typeof v === "object" ? Object.fromEntries(Object.keys(v).sort().map(k=>[k,ordered(v[k])])) : v;
  return createHash("sha256").update(JSON.stringify(ordered(config))).digest("hex");
}
function sameConfig(state,config) { validateScenario(config);if(state.configHash!==configHash(config))fail("CONFIG_DRIFT"); }
export function initialState(config) {
  validateScenario(config);
  return {configHash:configHash(config),initialCash:config.startingCash,cash:config.startingCash,completed:0,revenue:0,expenses:0,operatingProfit:0,
    principalReceived:0,financeCharges:0,principalRepaid:0,feesRepaid:0,capitalSpent:0,acquiredPairs:0,purchaseAt:null,loan:null,everBorrowed:false};
}
export const liability = state => state.loan ? add(state.loan.principalRemaining,state.loan.feeRemaining) : 0;
export function assertState(s) {
  exact(s,'configHash,initialCash,cash,completed,revenue,expenses,operatingProfit,principalReceived,financeCharges,principalRepaid,feesRepaid,capitalSpent,acquiredPairs,purchaseAt,loan,everBorrowed');
  if(typeof s.configHash!=='string' || !/^[a-f0-9]{64}$/.test(s.configHash))fail('INVALID_STATE');
  for(const key of ['initialCash','cash','completed','revenue','expenses','principalReceived','financeCharges','principalRepaid','feesRepaid','capitalSpent']) integer(s[key]);
  integer(s.operatingProfit,-MAX);integer(s.acquiredPairs,0,1);
  if(typeof s.everBorrowed!=='boolean' || s.everBorrowed !== (s.loan!==null))fail('INVALID_STATE');
  if(s.acquiredPairs===0 ? s.purchaseAt!==null || s.capitalSpent!==0 : !Number.isSafeInteger(s.purchaseAt) || s.purchaseAt<0 || s.purchaseAt>s.completed || s.capitalSpent<1)fail('INVALID_STATE');
  if(s.loan){
    exact(s.loan,'principalRemaining,feeRemaining,status');integer(s.loan.principalRemaining);integer(s.loan.feeRemaining);
    if(s.loan.status!==(liability(s)===0?'paid':'active') || s.principalReceived<1)fail('INVALID_STATE');
  } else if(s.principalReceived!==0 || s.financeCharges!==0 || s.principalRepaid!==0 || s.feesRepaid!==0)fail('INVALID_STATE');
  if(add(s.revenue,-s.expenses)!==s.operatingProfit ||
    add(s.principalReceived,-s.principalRepaid)!==(s.loan?.principalRemaining??0) || add(s.financeCharges,-s.feesRepaid)!==(s.loan?.feeRemaining??0) ||
    add(s.initialCash,s.operatingProfit,s.principalReceived,-s.principalRepaid,-s.feesRepaid,-s.capitalSpent)!==s.cash)fail('CONSERVATION_FAILURE');
  return s;
}
export function takeLoan(state,config) {
  assertState(state);sameConfig(state,config);
  if(state.completed<config.loan.unlockDeliveries)fail('LOAN_LOCKED');
  // This one-loan experiment deliberately has no reborrow policy, even after payoff.
  if(state.everBorrowed)fail('REBORROW_NOT_AUTHORIZED');
  const fee=basisPoints(config.loan.principal,config.loan.chargeBps);
  const cash=integer(add(state.cash,config.loan.principal));
  return assertState({...state,cash,principalReceived:config.loan.principal,financeCharges:fee,everBorrowed:true,
    loan:{principalRemaining:config.loan.principal,feeRemaining:fee,status:'active'}});
}
function pay(state,amount) {
  integer(amount,0,Math.min(state.cash,liability(state)));
  if(!state.loan || amount===0)return state;
  // PROPOSED allocation: fixed fee first, then principal. Neither is operating expense.
  const fee=Math.min(amount,state.loan.feeRemaining),principal=amount-fee;
  return assertState({...state,cash:state.cash-amount,feesRepaid:add(state.feesRepaid,fee),principalRepaid:add(state.principalRepaid,principal),
    loan:{principalRemaining:state.loan.principalRemaining-principal,feeRemaining:state.loan.feeRemaining-fee,status:liability(state)===amount?'paid':'active'}});
}
export function earlyRepay(state,config,requested) {
  assertState(state);sameConfig(state,config);integer(requested,1);
  if(!state.loan || state.loan.status!=='active')fail('NO_ACTIVE_LOAN');
  return pay(state,Math.min(requested,liability(state),Math.max(0,state.cash-config.cashReserve)));
}
export function settleDelivery(state,config) {
  assertState(state);sameConfig(state,config);
  const expenses=add(...Object.values(config.costs)),profit=add(config.gross,-expenses);
  const upfront=config.costTiming==='all_upfront'?expenses:config.costTiming==='fuel_tolls_upfront'?add(config.costs.fuel,config.costs.tolls):0;
  if(state.cash<upfront)fail('INSUFFICIENT_OPERATING_CASH');
  const cash=add(state.cash,profit);if(cash<0)fail('INSUFFICIENT_SETTLEMENT_CASH');
  const target=basisPoints(Math.max(0,profit),config.loan.repaymentBps);
  const payment=Math.min(liability(state),target,Math.max(0,cash-config.cashReserve));
  const operations=assertState({...state,cash,completed:add(state.completed,1),revenue:add(state.revenue,config.gross),expenses:add(state.expenses,expenses),
    operatingProfit:add(state.operatingProfit,profit)});
  const next=pay(operations,payment);
  return {state:next,gross:config.gross,expenses,profit,upfront,targetRepayment:target,repayment:payment,
    feeRepayment:next.feesRepaid-state.feesRepaid,principalRepayment:next.principalRepaid-state.principalRepaid,cashAfterOperations:cash,cashAfterSettlement:next.cash};
}
export function affordable(state,config) { assertState(state);sameConfig(state,config);return state.cash>=add(config.truckCost,config.driverCost,config.purchaseReserve); }
export function acquirePair(state,config) {
  if(state.acquiredPairs!==0)fail('SECOND_EXPANSION_NOT_MODELED');
  if(!affordable(state,config))fail('ACQUISITION_UNAFFORDABLE');
  const cost=add(config.truckCost,config.driverCost);
  return assertState({...state,cash:state.cash-cost,capitalSpent:cost,acquiredPairs:1,purchaseAt:state.completed});
}
export function metrics(state) {
  assertState(state);
  return {cash:state.cash,debt:liability(state),operatingProfit:state.operatingProfit,
    realizedCompanyProfit:add(state.operatingProfit,-state.feesRepaid),
    profitAfterCommittedCharge:add(state.operatingProfit,-state.financeCharges),
    netFinancialPosition:add(state.cash,-liability(state)),
    financialPositionPlusAcquiredCost:add(state.cash,state.capitalSpent,-liability(state))};
}
export function simulate(input) {
  const config=validateScenario(input);let state=initialState(config),rows=[],blocked=null;
  function decisions() {
    let disbursement=0,charge=0,purchased=false;
    if(config.borrowAfter===state.completed && !state.everBorrowed){state=takeLoan(state,config);disbursement=config.loan.principal;charge=state.financeCharges;}
    const canBuy=affordable(state,config);
    if(config.autoAcquire && state.acquiredPairs===0 && canBuy){state=acquirePair(state,config);purchased=true;}
    return {disbursement,charge,affordable:canBuy,purchased};
  }
  const initialDecision=decisions();const opening={...initialDecision,...metrics(state)};
  for(let i=1;i<=config.deliveries;i++){
    let settled;const before=state;
    try{settled=settleDelivery(state,config);}catch(error){
      if(!(error instanceof ModelError) || !['INSUFFICIENT_OPERATING_CASH','INSUFFICIENT_SETTLEMENT_CASH'].includes(error.code))throw error;
      blocked={attempt:i,reason:error.code,cash:state.cash,completed:state.completed};break;
    }
    state=settled.state;const decision=decisions();
    rows.push({delivery:state.completed,gross:settled.gross,expenses:settled.expenses,profit:settled.profit,upfront:settled.upfront,
      targetRepayment:settled.targetRepayment,repayment:settled.repayment,feeRepayment:settled.feeRepayment,principalRepayment:settled.principalRepayment,
      cashBefore:before.cash,cashAfterOperations:settled.cashAfterOperations,cashAfterSettlement:settled.cashAfterSettlement,
      ...decision,...metrics(state),fleetPairs:1+state.acquiredPairs});
  }
  return {config,opening,rows,blocked,state,summary:{...metrics(state),completed:state.completed,purchaseAt:state.purchaseAt,
    cashAtPurchase:state.purchaseAt===0?opening.cash:rows.find(r=>r.purchased)?.cash??null,
    payoffAt:rows.find(r=>r.debt===0 && r.repayment>0)?.delivery??null}};
}
/** Capacity-only lower bound; assumes full utilization, enough upfront cash and equal duration. Not a scheduler. */
export function waitingBound(result,offersPerWave,durationMinutes) {
  integer(offersPerWave,1,100);integer(durationMinutes,1,100000);
  let completed=0,waves=0;
  while(completed<result.summary.completed){const fleet=result.summary.purchaseAt!==null && completed>=result.summary.purchaseAt?2:1;completed+=Math.min(fleet,offersPerWave,result.summary.completed-completed);waves++;}
  return {waves,minutes:checked(BigInt(waves)*BigInt(durationMinutes))};
}
