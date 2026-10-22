import {createHash} from 'node:crypto';
import {MAX, integer, add, basisPoints, money, ModelError} from './model.mjs';
// BANK-00 offline policy model. No server endpoints or persistence adapters.
const fail = code => { throw new ModelError(code); };
const exact = (v, keys) => {
  if (!v || Object.getPrototypeOf(v)!==Object.prototype || Object.keys(v).sort().join()!==keys.split(',').sort().join()) fail('INVALID_SCHEMA');
};
export function hash(v) {
  const ordered = x => Array.isArray(x)?x.map(ordered):x && typeof x==='object'?Object.fromEntries(Object.keys(x).sort().map(k=>[k,ordered(x[k])])):x;
  return createHash('sha256').update(JSON.stringify(ordered(v))).digest('hex');
}
export const candidate = {
  version:'bank-00-v1', approval:'unapproved_design_candidate', currency:'LGC', scale:2,
  economyVersion:'offline-economy-v1', costVersion:'offline-cost-v1',
  minPrincipal:money('1000'), step:money('1000'), minFee:1, chargeBps:800, repaymentBps:3500,
  reserve:money('5000'), maxBalance:money('100000000'), maxLevel:50,
  tiers:[{from:1,to:4,max:money('20000')},{from:5,to:9,max:money('35000')},
    {from:10,to:19,max:money('60000')},{from:20,to:50,max:money('120000')}],
};
export function validateConfig(c) {
  exact(c,'version,approval,currency,scale,economyVersion,costVersion,minPrincipal,step,minFee,chargeBps,repaymentBps,reserve,maxBalance,maxLevel,tiers');
  if(c.version!=='bank-00-v1'||c.approval!=='unapproved_design_candidate'||c.currency!=='LGC'||c.scale!==2)fail('INVALID_CONFIG');
  for(const k of ['economyVersion','costVersion'])if(typeof c[k]!=='string'||!/^[a-zA-Z0-9_-]{1,64}$/.test(c[k]))fail('INVALID_CONFIG');
  for(const k of ['minPrincipal','step','minFee','maxBalance'])integer(c[k],1);
  integer(c.reserve,0,c.maxBalance);integer(c.maxLevel,1,1000);
  integer(c.chargeBps,0,10000);integer(c.repaymentBps,0,10000);
  if(!Array.isArray(c.tiers)||c.tiers.length<1||c.tiers.length>c.maxLevel)fail('INVALID_TIERS');
  let next=1;
  for(const t of c.tiers){
    exact(t,'from,to,max');integer(t.from,1,c.maxLevel);integer(t.to,t.from,c.maxLevel);
    integer(t.max,c.minPrincipal,c.maxBalance);
    if(t.from!==next || (t.max-c.minPrincipal)%c.step!==0)fail('INVALID_TIERS');
    integer(add(t.max,Math.max(c.minFee,basisPoints(t.max,c.chargeBps))));next=t.to+1;
  }
  if(next!==c.maxLevel+1)fail('INVALID_TIERS');return c;
}
export const ready = {identity:true,owner:true,economy:true,starter:true,migrationBlocked:false};
function readiness(r){
  exact(r,'identity,owner,economy,starter,migrationBlocked');
  if(Object.values(r).some(v=>typeof v!=='boolean'))fail('INVALID_READINESS');
  if(!r.identity||!r.owner||!r.economy||!r.starter||r.migrationBlocked)fail('NOT_READY');
}
export function limit(c,level){validateConfig(c);integer(level,1,c.maxLevel);return c.tiers.find(t=>level>=t.from&&level<=t.to).max;}
export function parseAmount(text){
  if(typeof text!=='string'||text.length>16||!/^[1-9][0-9]*$/.test(text))fail('INVALID_AMOUNT');
  return integer(Number(text),1);
}
export function initial(c=candidate,{cash=money('15000'),level=1,readiness:proof=ready}={}){
  validateConfig(c);readiness(proof);integer(cash,0,c.maxBalance);limit(c,level);
  return {configHash:hash(c),readiness:structuredClone(proof),level,initialCash:cash,cash,completed:0,revenue:0,costs:0,
    capital:0,purchaseAt:null,principalReceived:0,feesCharged:0,principalRepaid:0,feesRepaid:0,sequence:0,activeId:null,loans:[]};
}
export const debt = s => s.activeId===null?0:add(s.loans[s.activeId-1].principalRemaining,s.loans[s.activeId-1].feeRemaining);
export function assertState(s,c){
  validateConfig(c);exact(s,'configHash,readiness,level,initialCash,cash,completed,revenue,costs,capital,purchaseAt,principalReceived,feesCharged,principalRepaid,feesRepaid,sequence,activeId,loans');
  if(s.configHash!==hash(c))fail('CONFIG_DRIFT');readiness(s.readiness);limit(c,s.level);
  for(const k of ['initialCash','cash'])integer(s[k],0,c.maxBalance);
  for(const k of ['completed','revenue','costs','capital','principalReceived','feesCharged','principalRepaid','feesRepaid','sequence'])integer(s[k]);
  if(!Array.isArray(s.loans)||s.loans.length!==s.sequence)fail('INVALID_STATE');
  if(s.purchaseAt!==null)integer(s.purchaseAt,0,s.completed);
  if((s.purchaseAt===null)!==(s.capital===0))fail('INVALID_STATE');
  let principal=0,fees=0,pr=0,fr=0,active=null;
  s.loans.forEach((l,i)=>{
    exact(l,'id,source,terms,principalRemaining,feeRemaining,status');
    exact(l.terms,'principal,fee,repaymentBps,reserve,level,configHash');
    if(l.id!==i+1||l.source!==`loan_${i+1}`||l.terms.configHash!==s.configHash)fail('INVALID_STATE');
    const p=l.terms.principal;integer(p,c.minPrincipal,limit(c,l.terms.level));
    if((p-c.minPrincipal)%c.step!==0||l.terms.fee!==Math.max(c.minFee,basisPoints(p,c.chargeBps))||
      l.terms.repaymentBps!==c.repaymentBps||l.terms.reserve!==c.reserve)fail('INVALID_TERMS');
    integer(l.principalRemaining,0,p);integer(l.feeRemaining,0,l.terms.fee);
    if(l.feeRemaining>0&&l.principalRemaining!==p)fail('INVALID_ALLOCATION');
    const unpaid=add(l.principalRemaining,l.feeRemaining)>0;
    if(l.status!==(unpaid?'active':'paid')||unpaid&&active!==null||unpaid&&i!==s.loans.length-1)fail('INVALID_STATE');
    if(unpaid)active=l.id;
    principal=add(principal,p);fees=add(fees,l.terms.fee);pr=add(pr,p-l.principalRemaining);fr=add(fr,l.terms.fee-l.feeRemaining);
  });
  if(active!==s.activeId||principal!==s.principalReceived||fees!==s.feesCharged||pr!==s.principalRepaid||fr!==s.feesRepaid||
    s.cash!==add(s.initialCash,s.revenue,-s.costs,principal,-pr,-fr,-s.capital))fail('CONSERVATION_FAILURE');
  return s;
}
export function borrow(s,c,requested){
  assertState(s,c);if(s.activeId!==null)fail('ACTIVE_LOAN');
  const p=parseAmount(requested);integer(p,c.minPrincipal,limit(c,s.level));if((p-c.minPrincipal)%c.step!==0)fail('INVALID_STEP');
  const fee=Math.max(c.minFee,basisPoints(p,c.chargeBps)),id=add(s.sequence,1);
  integer(add(p,fee));const cash=integer(add(s.cash,p),0,c.maxBalance);
  const loan={id,source:`loan_${id}`,terms:{principal:p,fee,repaymentBps:c.repaymentBps,reserve:c.reserve,level:s.level,configHash:s.configHash},
    principalRemaining:p,feeRemaining:fee,status:'active'};
  return assertState({...s,cash,sequence:id,activeId:id,loans:[...s.loans,loan],principalReceived:add(s.principalReceived,p),feesCharged:add(s.feesCharged,fee)},c);
}
function pay(s,c,amount){
  integer(amount,0,Math.min(s.cash,debt(s)));if(amount===0)return s;
  const loan=s.loans[s.activeId-1],fee=Math.min(amount,loan.feeRemaining),principal=amount-fee;
  const paid=amount===debt(s),updated={...loan,feeRemaining:loan.feeRemaining-fee,principalRemaining:loan.principalRemaining-principal,status:paid?'paid':'active'};
  return assertState({...s,cash:s.cash-amount,feesRepaid:add(s.feesRepaid,fee),principalRepaid:add(s.principalRepaid,principal),
    activeId:paid?null:s.activeId,loans:s.loans.map(l=>l.id===loan.id?updated:l)},c);
}
export function repay(s,c,requested){
  assertState(s,c);const amount=parseAmount(requested);if(s.activeId===null)fail('NO_ACTIVE_LOAN');
  const actual=Math.min(amount,debt(s),Math.max(0,s.cash-s.loans[s.activeId-1].terms.reserve));
  if(actual===0)fail('NO_REPAYMENT_CAPACITY');return pay(s,c,actual);
}
export function levelUp(s,c,level){assertState(s,c);integer(level,s.level,c.maxLevel);return assertState({...s,level},c);}
export function settle(s,c,{gross,costs,upfront}){
  assertState(s,c);integer(gross);integer(costs);integer(upfront,0,costs);
  if(s.cash<upfront)fail('INSUFFICIENT_OPERATING_CASH');
  const profit=add(gross,-costs),available=add(s.cash,profit);if(available<0)fail('INSUFFICIENT_SETTLEMENT_CASH');
  const terms=s.activeId===null?null:s.loans[s.activeId-1].terms;
  const payment=terms?Math.min(debt(s),basisPoints(Math.max(0,profit),terms.repaymentBps),Math.max(0,available-terms.reserve)):0;
  // Check final balance, not a fictitious intermediate credit before atomic repayment.
  integer(add(available,-payment),0,c.maxBalance);
  const operations={...s,cash:available,completed:add(s.completed,1),revenue:add(s.revenue,gross),costs:add(s.costs,costs)};
  return assertState(pay(operations,c,payment),c);
}
export function acquire(s,c,price=money('50000'),reserve=c.reserve){
  assertState(s,c);integer(price,1);integer(reserve);if(s.purchaseAt!==null)fail('ALREADY_ACQUIRED');
  if(s.cash<add(price,reserve))fail('UNAFFORDABLE');return assertState({...s,cash:s.cash-price,capital:price,purchaseAt:s.completed},c);
}
